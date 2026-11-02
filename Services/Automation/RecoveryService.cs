using System.IO;
using VideoAutoWpf.Models.Automation;

namespace VideoAutoWpf.Services.Automation;

public record RecoverySummary(int RecoveredJobsCount, int ResumedPendingCount, int PromotedToApprovalCount, int FlaggedForConfirmationCount);

/// <summary>
/// Dịch vụ phục hồi hệ thống sau sự cố mất điện, crash hoặc tắt đột ngột (Crash & Power-Loss Recovery).
/// Quét toàn bộ SQLite DB, kiểm tra tính toàn vẹn của file MP4 trên ổ cứng và khôi phục job an toàn.
/// </summary>
public class RecoveryService
{
    private readonly DatabaseService _db;
    private readonly QualityCheckService _qualityCheckService;
    private readonly TelegramApprovalService _telegramService;
    private readonly YouTubePublishService _youtubeService;
    private readonly TikTokPublishService _tiktokService;

    public event Action<string>? LogMessage;

    public RecoveryService(
        DatabaseService db,
        QualityCheckService qualityCheckService,
        TelegramApprovalService telegramService,
        YouTubePublishService youtubeService,
        TikTokPublishService tiktokService)
    {
        _db = db;
        _qualityCheckService = qualityCheckService;
        _telegramService = telegramService;
        _youtubeService = youtubeService;
        _tiktokService = tiktokService;
    }

    /// <summary>
    /// Thực hiện quét và khôi phục toàn diện các công việc chưa hoàn tất
    /// </summary>
    public async Task<RecoverySummary> PerformCrashRecoveryAsync(AutomationConfig config, CancellationToken ct = default)
    {
        LogMessage?.Invoke("[Recovery] 🛡️ Đang kiểm tra cơ sở dữ liệu để phục hồi sau lần chạy trước...");

        int resumedPending = 0;
        int promotedToApproval = 0;
        int flaggedForConfirmation = 0;

        // 1. Tìm các job bị gián đoạn khi đang chạy (Running hoặc QualityCheck)
        var runningJobs = await _db.GetJobsByStatusAsync(JobStatus.Running, JobStatus.QualityCheck);
        foreach (var job in runningJobs)
        {
            LogMessage?.Invoke($"[Recovery] ⚠️ Phát hiện job bị ngắt quãng giữa chừng: {job.Title} (ID: {job.Id})");

            bool hasValidOutput = false;
            string? validThumbnail = null;

            if (!string.IsNullOrEmpty(job.OutputPath) && File.Exists(job.OutputPath))
            {
                var check = await _qualityCheckService.ValidateVideoAsync(job.OutputPath, ct);
                if (check.IsValid)
                {
                    hasValidOutput = true;
                    validThumbnail = check.GeneratedThumbnailPath;
                    job.DurationSeconds = check.DurationSeconds;
                }
            }

            if (hasValidOutput)
            {
                // File MP4 đã hoàn tất nguyên vẹn trước khi crash -> Tiến tới AwaitingApproval
                job.Status = JobStatus.AwaitingApproval;
                job.ThumbnailPath = validThumbnail ?? job.ThumbnailPath;
                job.LastErrorMessage = null;
                await _db.UpdateJobAsync(job);

                // Gửi lại thông báo duyệt Telegram nếu chưa gửi
                if (job.TelegramMessageId == null)
                {
                    var msgId = await _telegramService.SendApprovalRequestAsync(job, config, ct);
                    if (msgId.HasValue)
                    {
                        job.TelegramMessageId = msgId.Value;
                        await _db.UpdateJobAsync(job);
                    }
                }

                promotedToApproval++;
                LogMessage?.Invoke($"[Recovery] ✅ Video '{job.Title}' đã hoàn thiện nguyên vẹn trước khi tắt máy. Chuyển sang chờ duyệt Telegram.");
            }
            else
            {
                // File MP4 chưa hoàn tất hoặc bị cắt cụt do mất điện -> Xóa file hỏng và khôi phục về Pending
                if (!string.IsNullOrEmpty(job.OutputPath) && File.Exists(job.OutputPath))
                {
                    try
                    {
                        File.Delete(job.OutputPath);
                        LogMessage?.Invoke($"[Recovery] 🗑️ Đã xóa file video dở dang bị hỏng do mất điện: {Path.GetFileName(job.OutputPath)}");
                    }
                    catch { }
                }

                job.Status = JobStatus.Pending;
                job.StartedAt = null;
                job.LastErrorMessage = "Tự động khôi phục về hàng đợi sau sự cố tắt máy đột ngột.";
                await _db.UpdateJobAsync(job);

                resumedPending++;
                LogMessage?.Invoke($"[Recovery] 🔄 Đã đưa job '{job.Title}' về hàng đợi để render lại từ đầu an toàn.");
            }
        }

        // 2. Tìm các job bị gián đoạn khi đang Uploading
        var uploadingJobs = await _db.GetJobsByStatusAsync(JobStatus.Uploading);
        foreach (var job in uploadingJobs)
        {
            // Không tự động upload lại ngay vì có thể video đã lên nền tảng nhưng chưa kịp nhận response.
            // Đưa về AwaitingConfirmation để State Machine xác minh với API trước.
            job.Status = JobStatus.AwaitingConfirmation;
            if (job.TikTokStatus == PlatformPublishState.Uploading)
                job.TikTokStatus = PlatformPublishState.AwaitingConfirmation;
            if (job.YouTubeStatus == PlatformPublishState.Uploading)
                job.YouTubeStatus = PlatformPublishState.AwaitingConfirmation;

            job.LastErrorMessage = "Bị ngắt quãng trong lúc upload. Chờ xác minh trạng thái từ nền tảng.";
            await _db.UpdateJobAsync(job);

            flaggedForConfirmation++;
            LogMessage?.Invoke($"[Recovery] 🔎 Job '{job.Title}' bị ngắt khi đang upload. Đã chuyển sang AwaitingConfirmation để tránh đăng trùng.");
        }

        int totalRecovered = resumedPending + promotedToApproval + flaggedForConfirmation;
        if (totalRecovered > 0)
        {
            var summaryMsg = $"🛡️ <b>[KHÔI PHỤC HỆ THỐNG SAU KHỞI ĐỘNG]</b>\n\n" +
                             $"Mini PC đã phát hiện và khôi phục an toàn <b>{totalRecovered} công việc</b>:\n" +
                             $"• Đưa về hàng đợi tiếp tục tạo: {resumedPending}\n" +
                             $"• Giữ nguyên video tốt và gửi duyệt: {promotedToApproval}\n" +
                             $"• Đang xác minh tránh đăng trùng: {flaggedForConfirmation}";

            await _telegramService.SendNotificationAsync(summaryMsg, config, ct);
        }
        else
        {
            LogMessage?.Invoke("[Recovery] 🟢 Dữ liệu hoàn toàn sạch sẽ, không có job nào bị bỏ dở từ lần chạy trước.");
        }

        return new RecoverySummary(totalRecovered, resumedPending, promotedToApproval, flaggedForConfirmation);
    }
}
