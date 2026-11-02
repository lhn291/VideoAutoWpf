using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using VideoAutoWpf.Models.Automation;

namespace VideoAutoWpf.Services.Automation;

/// <summary>
/// Bộ điều phối công việc bền vững (Persistent Job Scheduler).
/// Chịu trách nhiệm lấy job từ SQLite, duy trì Heartbeat Lease, điều phối pipeline tạo video,
/// kích hoạt Telegram Approval, gọi Publishing State Machine và kích hoạt Shutdown Coordinator khi hoàn tất.
/// </summary>
public partial class PersistentJobScheduler : ObservableObject, IDisposable
{
    private readonly DatabaseService _db;
    private readonly QualityCheckService _qualityChecker;
    private readonly TelegramApprovalService _telegramService;
    private readonly PublishingStateMachine _publishingStateMachine;
    private readonly ShutdownCoordinator _shutdownCoordinator;
    private readonly RecoveryService _recoveryService;
    private AutomationConfig _config;

    private CancellationTokenSource? _schedulerCts;
    private bool _isRunning;
    private bool _isDisposed;

    /// <summary>
    /// Hàm thực thi pipeline tạo video được inject từ MainViewModel (sử dụng TTS, Imagen, FFmpeg hiện có)
    /// </summary>
    public Func<AutomationJob, IProgress<GenerationProgress>, CancellationToken, Task<string>>? VideoRenderAction { get; set; }

    public ObservableCollection<AutomationJob> ActiveJobs { get; } = new();

    [ObservableProperty]
    private string _schedulerStatus = "Đang dừng";

    [ObservableProperty]
    private int _pendingJobsCount;

    [ObservableProperty]
    private int _runningJobsCount;

    [ObservableProperty]
    private int _awaitingApprovalCount;

    [ObservableProperty]
    private int _completedJobsCount;

    public event Action<string>? LogMessage;

    public PersistentJobScheduler(
        DatabaseService db,
        QualityCheckService qualityChecker,
        TelegramApprovalService telegramService,
        PublishingStateMachine publishingStateMachine,
        ShutdownCoordinator shutdownCoordinator,
        RecoveryService recoveryService,
        AutomationConfig config)
    {
        _db = db;
        _qualityChecker = qualityChecker;
        _telegramService = telegramService;
        _publishingStateMachine = publishingStateMachine;
        _shutdownCoordinator = shutdownCoordinator;
        _recoveryService = recoveryService;
        _config = config;

        // Lắng nghe quyết định từ Telegram Bot
        _telegramService.ApprovalDecisionRecorded += OnApprovalDecisionRecorded;
    }

    public void UpdateConfig(AutomationConfig config)
    {
        _config = config;
        _shutdownCoordinator.UpdateConfig(config);
    }

    /// <summary>
    /// Khởi động Scheduler: Chạy Recovery trước, sau đó kích hoạt Polling Telegram và Worker Loop
    /// </summary>
    public async Task StartAsync()
    {
        if (_isRunning) return;
        _isRunning = true;
        _schedulerCts = new CancellationTokenSource();
        var ct = _schedulerCts.Token;

        SchedulerStatus = "Đang chạy";
        LogMessage?.Invoke("[Scheduler] 🚀 Khởi động Persistent Job Scheduler...");

        // 1. Phục hồi dữ liệu sau crash / mất điện từ lần chạy trước
        try
        {
            await _recoveryService.PerformCrashRecoveryAsync(_config, ct);
        }
        catch (Exception ex)
        {
            LogMessage?.Invoke($"[Scheduler Lỗi Recovery] {ex.Message}");
        }

        // 2. Kích hoạt Telegram Bot Long Polling
        _telegramService.StartPolling(_config);

        // 3. Khởi chạy vòng lặp xử lý chính (Background Worker Loop)
        _ = Task.Run(() => WorkerLoopAsync(ct), ct);
    }

    /// <summary>
    /// Dừng Scheduler
    /// </summary>
    public void Stop()
    {
        _isRunning = false;
        SchedulerStatus = "Đã dừng";
        _schedulerCts?.Cancel();
        _telegramService.StopPolling();
        LogMessage?.Invoke("[Scheduler] ⏹️ Đã dừng Persistent Job Scheduler.");
    }

    /// <summary>
    /// Đưa một job tạo video đơn lẻ vào SQLite
    /// </summary>
    public async Task<AutomationJob> EnqueueRenderJobAsync(
        string title,
        string channelId,
        string videoId,
        object payloadData,
        string? caption = null,
        string? hashtags = null)
    {
        var job = new AutomationJob
        {
            JobType = JobType.RenderSingleVideo,
            Status = JobStatus.Pending,
            Title = title,
            ChannelId = channelId,
            VideoId = videoId,
            Caption = caption,
            Hashtags = hashtags,
            PayloadJson = JsonSerializer.Serialize(payloadData)
        };

        await _db.InsertJobAsync(job);
        LogMessage?.Invoke($"[Scheduler] 📥 Đã thêm job vào SQLite: '{title}' (ID: {job.Id})");
        await RefreshCountersAsync();

        // Nếu đang trong đếm ngược shutdown, hủy ngay vì có việc mới
        _shutdownCoordinator.AbortShutdown($"Đã tiếp nhận công việc mới: {title}");

        return job;
    }

    private async Task WorkerLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && _isRunning)
        {
            try
            {
                await RefreshCountersAsync();

                if (!_config.AutoRunEnabled)
                {
                    await Task.Delay(2000, ct);
                    continue;
                }

                // ── BƯỚC 1: XỬ LÝ CÁC VIDEO ĐÃ ĐƯỢC DUYỆT ĐỂ ĐĂNG BÀI ──
                var approvedJobs = await _db.GetJobsByStatusAsync(JobStatus.Approved);
                foreach (var job in approvedJobs)
                {
                    if (ct.IsCancellationRequested) break;
                    await ProcessPublishJobAsync(job, ct);
                }

                // ── BƯỚC 2: XỬ LÝ CÁC VIDEO ĐẾN HẠN RETRY ĐĂNG BÀI ──
                var retryJobs = await _db.GetJobsByStatusAsync(JobStatus.RetryPending);
                var now = DateTime.UtcNow;
                foreach (var job in retryJobs)
                {
                    if (ct.IsCancellationRequested) break;
                    if (job.ScheduledPublishAt == null || job.ScheduledPublishAt <= now)
                    {
                        await ProcessPublishJobAsync(job, ct);
                    }
                }

                // ── BƯỚC 3: XỬ LÝ JOB TẠO VIDEO MỚI TRONG HÀNG ĐỢI ──
                var pendingJobs = await _db.GetJobsByStatusAsync(JobStatus.Pending);
                var nextJob = pendingJobs.FirstOrDefault();
                if (nextJob != null)
                {
                    await ProcessRenderJobAsync(nextJob, ct);
                }

                // ── BƯỚC 4: ĐÁNH GIÁ ĐIỀU KIỆN TẮT MÁY AN TOÀN ──
                if (_config.AutoShutdownEnabled && _shutdownCoordinator.CurrentState == ShutdownState.Active)
                {
                    var eval = await _shutdownCoordinator.EvaluateShutdownSafetyAsync();
                    if (eval.CanShutdown)
                    {
                        LogMessage?.Invoke("[Scheduler] 💤 Toàn bộ công việc đã hoàn tất an toàn. Bắt đầu đếm ngược tắt máy...");
                        _ = _shutdownCoordinator.StartShutdownCountdownAsync(ct);
                    }
                }

                var delayMs = Math.Max(3000, _config.IdleCheckIntervalSeconds * 1000);
                await Task.Delay(delayMs, ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                LogMessage?.Invoke($"[Scheduler Lỗi Vòng Lặp] {ex.Message}");
                try { await Task.Delay(5000, ct); } catch { }
            }
        }
    }

    private async Task ProcessRenderJobAsync(AutomationJob job, CancellationToken ct)
    {
        LogMessage?.Invoke($"[Scheduler] ⚙️ Bắt đầu thực thi job render: '{job.Title}' (ID: {job.Id})");

        job.Status = JobStatus.Running;
        job.StartedAt = DateTime.UtcNow;
        var leaseMinutes = Math.Max(15, _config.JobLeaseTimeoutMinutes);
        job.LeaseExpiresAt = DateTime.UtcNow.AddMinutes(leaseMinutes);
        job.HeartbeatAt = DateTime.UtcNow;
        await _db.UpdateJobAsync(job);

        // Khởi động heartbeat timer định kỳ mỗi 60 giây để duy trì lease
        using var heartbeatCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var hbToken = heartbeatCts.Token;
        var hbTask = Task.Run(async () =>
        {
            while (!hbToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(60000, hbToken);
                    await _db.UpdateHeartbeatAsync(job.Id, DateTime.UtcNow.AddMinutes(leaseMinutes));
                }
                catch { break; }
            }
        }, hbToken);

        try
        {
            string outputVideoFile;
            if (VideoRenderAction != null)
            {
                var progress = new Progress<GenerationProgress>(p =>
                {
                    if (!string.IsNullOrEmpty(p.LogMessage))
                        LogMessage?.Invoke($"[{job.Title}] {p.LogMessage}");
                });

                outputVideoFile = await VideoRenderAction(job, progress, ct);
            }
            else
            {
                throw new InvalidOperationException("Chưa đăng ký VideoRenderAction trong PersistentJobScheduler.");
            }

            job.OutputPath = outputVideoFile;
            job.Status = JobStatus.QualityCheck;
            await _db.UpdateJobAsync(job);

            // Kiểm tra chất lượng video hoàn chỉnh
            LogMessage?.Invoke($"[Scheduler] 🔍 Kiểm tra chất lượng file MP4: {Path.GetFileName(outputVideoFile)}...");
            var qc = await _qualityChecker.ValidateVideoAsync(outputVideoFile, ct);
            if (!qc.IsValid)
            {
                throw new InvalidDataException(qc.ErrorMessage ?? "File video render bị lỗi cấu trúc.");
            }

            job.DurationSeconds = qc.DurationSeconds;
            job.ThumbnailPath = qc.GeneratedThumbnailPath;

            // Chuyển sang chờ duyệt Telegram
            job.Status = JobStatus.AwaitingApproval;
            job.LastErrorMessage = null;
            await _db.UpdateJobAsync(job);
            LogMessage?.Invoke($"[Scheduler] 📱 Render hoàn tất! Đang gửi thông báo duyệt tới Telegram...");

            var msgId = await _telegramService.SendApprovalRequestAsync(job, _config, ct);
            if (msgId.HasValue)
            {
                job.TelegramMessageId = msgId.Value;
                await _db.UpdateJobAsync(job);
            }
        }
        catch (OperationCanceledException)
        {
            job.Status = JobStatus.Pending;
            job.LastErrorMessage = "Đã hủy bởi người dùng.";
            await _db.UpdateJobAsync(job);
        }
        catch (Exception ex)
        {
            job.RetryCount++;
            job.LastErrorMessage = ex.Message;
            if (job.RetryCount < job.MaxRetries)
            {
                job.Status = JobStatus.RetryPending;
                job.ScheduledPublishAt = DateTime.UtcNow.AddSeconds(_config.RetryBackoffSeconds);
                LogMessage?.Invoke($"[Scheduler] 🔄 Lỗi render: {ex.Message}. Sẽ thử lại sau.");
            }
            else
            {
                job.Status = JobStatus.FailedPermanent;
                job.FinishedAt = DateTime.UtcNow;
                LogMessage?.Invoke($"[Scheduler] ⛔ Job render thất bại vĩnh viễn: {ex.Message}");
            }
            await _db.UpdateJobAsync(job);
        }
        finally
        {
            heartbeatCts.Cancel();
            try { await hbTask; } catch { }
        }
    }

    private async Task ProcessPublishJobAsync(AutomationJob job, CancellationToken ct)
    {
        try
        {
            await _publishingStateMachine.ProcessPublishAsync(job, _config, ct);
        }
        catch (Exception ex)
        {
            LogMessage?.Invoke($"[Scheduler Lỗi Đăng Bài] {ex.Message}");
        }
    }

    private async void OnApprovalDecisionRecorded(string jobId, string decision, long userId)
    {
        try
        {
            var job = await _db.GetJobByIdAsync(jobId);
            if (job != null)
            {
                LogMessage?.Invoke($"[Scheduler] 🔔 Nhận tín hiệu Telegram: Video '{job.Title}' đã được {decision}!");
                await RefreshCountersAsync();

                if (decision == "Approved")
                {
                    // Đưa ngay vào xử lý đăng
                    _ = ProcessPublishJobAsync(job, CancellationToken.None);
                }
            }
        }
        catch (Exception ex)
        {
            LogMessage?.Invoke($"[Scheduler Lỗi Xử Lý Approval Callback] {ex.Message}");
        }
    }

    public async Task RefreshCountersAsync()
    {
        try
        {
            var all = await _db.GetAllJobsAsync();
            PendingJobsCount = all.Count(j => j.Status == JobStatus.Pending);
            RunningJobsCount = all.Count(j => j.Status == JobStatus.Running || j.Status == JobStatus.QualityCheck || j.Status == JobStatus.Uploading);
            AwaitingApprovalCount = all.Count(j => j.Status == JobStatus.AwaitingApproval);
            CompletedJobsCount = all.Count(j => j.Status == JobStatus.Published);

            Application.Current?.Dispatcher?.Invoke(() =>
            {
                ActiveJobs.Clear();
                foreach (var j in all.Take(30))
                {
                    ActiveJobs.Add(j);
                }
            });
        }
        catch { }
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;
        Stop();
        _schedulerCts?.Dispose();
        _telegramService.ApprovalDecisionRecorded -= OnApprovalDecisionRecorded;
        GC.SuppressFinalize(this);
    }
}
