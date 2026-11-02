using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using VideoAutoWpf.Models.Automation;

namespace VideoAutoWpf.Services.Automation;

public record ShutdownEvaluationResult(bool CanShutdown, List<string> BlockingReasons);

/// <summary>
/// Bộ điều phối tắt máy an toàn (Shutdown Coordinator) tập trung.
/// Đánh giá nghiêm ngặt 12 điều kiện an toàn, quản lý giai đoạn đếm ngược (Grace Period)
/// và thực hiện lệnh tắt máy Windows bình thường (không ép buộc /f) khi toàn bộ công việc đã hoàn tất.
/// </summary>
public partial class ShutdownCoordinator : ObservableObject, IDisposable
{
    private readonly DatabaseService _db;
    private readonly TelegramApprovalService _telegramService;
    private AutomationConfig _config;

    [ObservableProperty]
    private ShutdownState _currentState = ShutdownState.Active;

    [ObservableProperty]
    private int _secondsRemaining = 0;

    [ObservableProperty]
    private string _statusMessage = "Hệ thống đang hoạt động bình thường.";

    [ObservableProperty]
    private List<string> _currentBlockingReasons = new();

    private CancellationTokenSource? _countdownCts;
    private bool _isDisposed;

    public event Action<ShutdownState>? StateChanged;
    public event Action<string>? LogMessage;

    public ShutdownCoordinator(DatabaseService db, TelegramApprovalService telegramService, AutomationConfig config)
    {
        _db = db;
        _telegramService = telegramService;
        _config = config;
    }

    public void UpdateConfig(AutomationConfig config)
    {
        _config = config;
    }

    /// <summary>
    /// Đánh giá toàn diện 12 điều kiện an toàn để quyết định có được phép tắt máy hay không.
    /// Không bao giờ dựa vào biến trong RAM, luôn đọc từ cơ sở dữ liệu SQLite.
    /// </summary>
    public async Task<ShutdownEvaluationResult> EvaluateShutdownSafetyAsync()
    {
        var reasons = new List<string>();

        if (!_config.AutoShutdownEnabled)
        {
            reasons.Add("Tính năng Auto Shutdown đang TẮT trong cấu hình.");
            return new ShutdownEvaluationResult(false, reasons);
        }

        var allJobs = await _db.GetAllJobsAsync();

        // 1. Không có job tạo video đang chạy (Running, QualityCheck)
        var runningJobs = allJobs.Where(j => j.Status == JobStatus.Running || j.Status == JobStatus.QualityCheck).ToList();
        if (runningJobs.Count > 0)
        {
            reasons.Add($"Có {runningJobs.Count} job đang render/kiểm tra chất lượng ({string.Join(", ", runningJobs.Select(j => j.Title))}).");
        }

        // 2. Không còn job tạo video đến hạn trong hàng đợi (Pending)
        var pendingJobs = allJobs.Where(j => j.Status == JobStatus.Pending).ToList();
        if (pendingJobs.Count > 0)
        {
            reasons.Add($"Có {pendingJobs.Count} job đang chờ trong hàng đợi ({string.Join(", ", pendingJobs.Select(j => j.Title))}).");
        }

        // 3. Không còn video chờ duyệt (AwaitingApproval)
        var awaitingApproval = allJobs.Where(j => j.Status == JobStatus.AwaitingApproval).ToList();
        if (awaitingApproval.Count > 0)
        {
            if (_config.WaitForPendingApproval)
            {
                reasons.Add($"Có {awaitingApproval.Count} video đang chờ bạn duyệt qua Telegram ({string.Join(", ", awaitingApproval.Select(j => j.Title))}).");
            }
        }

        // 4. Không còn video đã duyệt chờ đăng hoặc chờ lịch đăng đến hạn (Approved, Scheduled)
        var scheduledJobs = allJobs.Where(j => j.Status == JobStatus.Approved || j.Status == JobStatus.Scheduled).ToList();
        if (scheduledJobs.Count > 0)
        {
            if (_config.WaitForScheduledJobs)
            {
                reasons.Add($"Có {scheduledJobs.Count} video đã duyệt đang chờ đăng hoặc chờ đến giờ hẹn.");
            }
        }

        // 5. Không có upload đang chạy (Uploading)
        var uploadingJobs = allJobs.Where(j => j.Status == JobStatus.Uploading || j.TikTokStatus == PlatformPublishState.Uploading || j.YouTubeStatus == PlatformPublishState.Uploading).ToList();
        if (uploadingJobs.Count > 0)
        {
            reasons.Add($"Có {uploadingJobs.Count} video đang trong quá trình tải lên mạng xã hội.");
        }

        // 6. Không có upload chưa xác định kết quả (AwaitingConfirmation)
        var unconfirmedJobs = allJobs.Where(j => j.Status == JobStatus.AwaitingConfirmation || j.TikTokStatus == PlatformPublishState.AwaitingConfirmation || j.YouTubeStatus == PlatformPublishState.AwaitingConfirmation).ToList();
        if (unconfirmedJobs.Count > 0)
        {
            reasons.Add($"Có {unconfirmedJobs.Count} video upload chưa xác định kết quả từ TikTok/YouTube (cần giữ máy để xác minh).");
        }

        // 7. Không có retry đến hạn (RetryPending)
        var retryJobs = allJobs.Where(j => j.Status == JobStatus.RetryPending || j.TikTokStatus == PlatformPublishState.RetryPending || j.YouTubeStatus == PlatformPublishState.RetryPending).ToList();
        if (retryJobs.Count > 0)
        {
            reasons.Add($"Có {retryJobs.Count} công việc đang chờ đến lượt thử lại tự động (Retry).");
        }

        // 8. Không có job bị lỗi cần xử lý nếu cấu hình không cho phép tắt khi có lỗi
        var permanentFailedJobs = allJobs.Where(j => j.Status == JobStatus.FailedPermanent || j.TikTokStatus == PlatformPublishState.FailedPermanent || j.YouTubeStatus == PlatformPublishState.FailedPermanent).ToList();
        if (permanentFailedJobs.Count > 0 && !_config.ShutdownOnPermanentFailure)
        {
            reasons.Add($"Có {permanentFailedJobs.Count} job bị lỗi vĩnh viễn cần người dùng kiểm tra thủ công (Mặc định không tắt máy để tránh bỏ sót lỗi).");
        }

        CurrentBlockingReasons = reasons;
        bool canShutdown = reasons.Count == 0;
        return new ShutdownEvaluationResult(canShutdown, reasons);
    }

    /// <summary>
    /// Bắt đầu quy trình đếm ngược Grace Period trước khi tắt máy.
    /// Trong thời gian này, nếu phát hiện có job mới hoặc người dùng bấm Hủy -> Hủy đếm ngược ngay lập tức.
    /// </summary>
    public async Task StartShutdownCountdownAsync(CancellationToken externalCt = default)
    {
        if (CurrentState == ShutdownState.ShutdownPending || CurrentState == ShutdownState.ShuttingDown)
            return;

        CurrentState = ShutdownState.ShutdownPending;
        StateChanged?.Invoke(CurrentState);

        var graceSeconds = Math.Max(10, _config.ShutdownGracePeriodSeconds);
        SecondsRemaining = graceSeconds;
        StatusMessage = $"⚠️ Toàn bộ công việc đã hoàn tất! Chuẩn bị tắt máy an toàn sau {graceSeconds} giây...";
        LogMessage?.Invoke($"[Shutdown Coordinator] 💤 {StatusMessage}");

        _countdownCts = CancellationTokenSource.CreateLinkedTokenSource(externalCt);
        var ct = _countdownCts.Token;

        try
        {
            // Báo Telegram người dùng biết mini PC sắp tắt
            await _telegramService.SendNotificationAsync(
                $"💤 <b>[THÔNG BÁO TỰ ĐỘNG TẮT MÁY]</b>\n\nMini PC đã hoàn tất 100% công việc và sẽ tự động tắt an toàn sau <b>{graceSeconds} giây</b>.",
                _config, ct);

            while (SecondsRemaining > 0 && !ct.IsCancellationRequested)
            {
                await Task.Delay(1000, ct);
                SecondsRemaining--;
                StatusMessage = $"Tắt máy an toàn sau: {SecondsRemaining} giây (Bấm 'Hủy Shutdown' để giữ máy chạy)...";

                // Đánh giá lại mỗi giây xem có việc mới đột xuất hoặc người dùng thêm kịch bản không
                if (SecondsRemaining % 5 == 0)
                {
                    var reEval = await EvaluateShutdownSafetyAsync();
                    if (!reEval.CanShutdown)
                    {
                        AbortShutdown($"Phát hiện công việc mới xuất hiện: {string.Join("; ", reEval.BlockingReasons)}");
                        return;
                    }
                }
            }

            // Đánh giá lần cuối trước khi ra lệnh tắt máy
            var finalEval = await EvaluateShutdownSafetyAsync();
            if (!finalEval.CanShutdown)
            {
                AbortShutdown($"Điều kiện an toàn không còn thỏa mãn ở giây cuối cùng: {string.Join("; ", finalEval.BlockingReasons)}");
                return;
            }

            // Tiến hành tắt máy
            await ExecuteWindowsShutdownAsync();
        }
        catch (OperationCanceledException)
        {
            // Bị hủy
        }
        catch (Exception ex)
        {
            AbortShutdown($"Lỗi trong quá trình chuẩn bị tắt máy: {ex.Message}");
            await _telegramService.SendNotificationAsync(
                $"❌ <b>[LỖI SHUTDOWN]</b>\n\nKhông thể tắt máy tự động: {ex.Message}. Mini PC vẫn đang tiếp tục chạy.",
                _config, default);
        }
    }

    /// <summary>
    /// Hủy bỏ quá trình đếm ngược tắt máy và đưa hệ thống về trạng thái Active
    /// </summary>
    public void AbortShutdown(string reason)
    {
        if (CurrentState == ShutdownState.ShutdownPending)
        {
            _countdownCts?.Cancel();
            CurrentState = ShutdownState.Aborted;
            SecondsRemaining = 0;
            StatusMessage = $"Đã hủy tắt máy: {reason}";
            LogMessage?.Invoke($"[Shutdown Coordinator] 🛑 {StatusMessage}");
            StateChanged?.Invoke(CurrentState);

            _ = _telegramService.SendNotificationAsync(
                $"🔄 <b>[ĐÃ HỦY TẮT MÁY]</b>\n\nLệnh tắt máy đã bị hủy: {reason}.\nMini PC tiếp tục chạy bình thường.",
                _config, default);

            CurrentState = ShutdownState.Active;
            StateChanged?.Invoke(CurrentState);
        }
    }

    /// <summary>
    /// Thực hiện lệnh tắt máy Windows bình thường (không dùng /f cưỡng bức)
    /// </summary>
    private async Task ExecuteWindowsShutdownAsync()
    {
        CurrentState = ShutdownState.ShuttingDown;
        StateChanged?.Invoke(CurrentState);
        StatusMessage = "Đang gửi lệnh tắt máy Windows bình thường...";
        LogMessage?.Invoke("[Shutdown Coordinator] 🔌 Đang thực hiện tắt máy Windows...");

        await _db.LogAuditAsync("SAFE_SHUTDOWN", "ShutdownCoordinator executed safe Windows shutdown.");

        if (_config.DryRunMode)
        {
            LogMessage?.Invoke("[Shutdown Coordinator - DRY RUN] 🟢 Mô phỏng tắt máy thành công! (Không thực thi lệnh shutdown thật).");
            StatusMessage = "Đã mô phỏng tắt máy thành công (Dry Run).";
            CurrentState = ShutdownState.Active;
            StateChanged?.Invoke(CurrentState);
            return;
        }

        try
        {
            // Lệnh shutdown bình thường: /s tắt máy, /t 0 ngay lập tức, /c ghi chú lý do.
            // TUYỆT ĐỐI KHÔNG DÙNG /f để cho phép các dịch vụ hệ điều hành đóng an toàn.
            var psi = new ProcessStartInfo
            {
                FileName = "shutdown.exe",
                Arguments = "/s /t 0 /c \"VideoAutoWpf: Tat ca cong viec da hoan thanh an toan.\"",
                UseShellExecute = false,
                CreateNoWindow = true
            };

            Process.Start(psi);
        }
        catch (Exception ex)
        {
            LogMessage?.Invoke($"[Shutdown Coordinator Lỗi] Không thể gọi lệnh shutdown.exe: {ex.Message}");
            AbortShutdown($"Lỗi gọi lệnh shutdown: {ex.Message}");
        }
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;
        _countdownCts?.Cancel();
        _countdownCts?.Dispose();
        GC.SuppressFinalize(this);
    }
}
