using VideoAutoWpf.Models.Automation;

namespace VideoAutoWpf.Services.Automation;

/// <summary>
/// Máy trạng thái phát hành (Publishing State Machine) điều phối việc đăng tải video
/// độc lập trên từng nền tảng (TikTok và YouTube). Đảm bảo tính Idempotent tuyệt đối:
/// nếu một nền tảng thành công thì không bao giờ đăng lại nền tảng đó khi thử lại nền tảng còn lại.
/// </summary>
public class PublishingStateMachine
{
    private readonly DatabaseService _db;
    private readonly YouTubePublishService _youtubeService;
    private readonly TikTokPublishService _tiktokService;

    public event Action<AutomationJob, string, double>? PlatformProgress; // (job, platform, pct)
    public event Action<string>? LogMessage;

    public PublishingStateMachine(
        DatabaseService db,
        YouTubePublishService youtubeService,
        TikTokPublishService tiktokService)
    {
        _db = db;
        _youtubeService = youtubeService;
        _tiktokService = tiktokService;
    }

    /// <summary>
    /// Xử lý quy trình đăng video cho job đã được phê duyệt (Approved)
    /// </summary>
    public async Task ProcessPublishAsync(AutomationJob job, AutomationConfig config, CancellationToken ct = default)
    {
        job.Status = JobStatus.Uploading;
        job.UpdatedAt = DateTime.UtcNow;
        await _db.UpdateJobAsync(job);
        LogMessage?.Invoke($"[Publishing] 🚀 Bắt đầu quy trình phát hành cho video: {job.Title} (ID: {job.Id})");

        bool needTikTok = config.TikTokEnabled && job.TikTokStatus != PlatformPublishState.Published;
        bool needYouTube = config.YouTubeEnabled && job.YouTubeStatus != PlatformPublishState.Published;

        // Nếu cả hai nền tảng đều không bật hoặc đã đăng xong
        if (!needTikTok && !needYouTube)
        {
            job.Status = JobStatus.Published;
            job.FinishedAt = DateTime.UtcNow;
            await _db.UpdateJobAsync(job);
            LogMessage?.Invoke($"[Publishing] ✅ Tất cả nền tảng đều đã hoàn tất hoặc không yêu cầu đăng cho video: {job.Title}");
            return;
        }

        // ── 1. XỬ LÝ ĐĂNG TIKTOK ĐỘC LẬP ──
        if (needTikTok)
        {
            await ProcessTikTokPlatformAsync(job, config, ct);
        }

        // ── 2. XỬ LÝ ĐĂNG YOUTUBE ĐỘC LẬP ──
        if (needYouTube)
        {
            await ProcessYouTubePlatformAsync(job, config, ct);
        }

        // ── 3. TỔNG HỢP VÀ CẬP NHẬT TRẠNG THÁI CUỐI CÙNG ──
        EvaluateFinalJobStatus(job, config);
        await _db.UpdateJobAsync(job);
    }

    private async Task ProcessTikTokPlatformAsync(AutomationJob job, AutomationConfig config, CancellationToken ct)
    {
        // Nếu đang ở trạng thái AwaitingConfirmation, thử kiểm tra lại trước khi upload lại
        if (job.TikTokStatus == PlatformPublishState.AwaitingConfirmation && !string.IsNullOrEmpty(job.TikTokPostId))
        {
            LogMessage?.Invoke($"[TikTok] 🔍 Đang kiểm tra lại trạng thái bài đăng cũ (ID: {job.TikTokPostId})...");
            var verifyResult = await _tiktokService.CheckPublishStatusAsync(config.ResolveTikTokAccessToken(), job.TikTokPostId, ct);
            if (verifyResult.Success)
            {
                job.TikTokStatus = PlatformPublishState.Published;
                job.TikTokPostUrl = verifyResult.PostUrl ?? job.TikTokPostUrl;
                job.TikTokPublishedAt = DateTime.UtcNow;
                LogMessage?.Invoke($"[TikTok] 🎉 Xác nhận video đã đăng thành công trên TikTok! Post ID: {job.TikTokPostId}");
                await _db.UpdateJobAsync(job);
                return;
            }
        }

        job.TikTokStatus = PlatformPublishState.Uploading;
        await _db.UpdateJobAsync(job);
        LogMessage?.Invoke("[TikTok] ☁️ Đang tải video lên TikTok...");

        var result = await _tiktokService.UploadAsync(job, config, pct => PlatformProgress?.Invoke(job, "TikTok", pct), ct);

        if (result.Success)
        {
            job.TikTokStatus = PlatformPublishState.Published;
            job.TikTokPostId = result.PublishId;
            job.TikTokPostUrl = result.PostUrl;
            job.TikTokErrorMessage = null;
            job.TikTokPublishedAt = DateTime.UtcNow;
            LogMessage?.Invoke($"[TikTok] 🎉 Đăng TikTok thành công! ID: {result.PublishId}");
        }
        else if (result.IsPendingConfirmation)
        {
            job.TikTokStatus = PlatformPublishState.AwaitingConfirmation;
            job.TikTokPostId = result.PublishId ?? job.TikTokPostId;
            job.TikTokErrorMessage = result.ErrorMessage;
            LogMessage?.Invoke($"[TikTok] ⚠️ Upload TikTok chưa xác định kết quả. Chuyển sang AwaitingConfirmation: {result.ErrorMessage}");
        }
        else
        {
            job.TikTokRetryCount++;
            job.TikTokErrorMessage = result.ErrorMessage;
            if (job.TikTokRetryCount < config.MaxUploadRetries)
            {
                job.TikTokStatus = PlatformPublishState.RetryPending;
                LogMessage?.Invoke($"[TikTok] 🔄 Lỗi tạm thời: {result.ErrorMessage}. Lần thử {job.TikTokRetryCount}/{config.MaxUploadRetries}. Sẽ thử lại sau.");
            }
            else
            {
                job.TikTokStatus = PlatformPublishState.FailedPermanent;
                LogMessage?.Invoke($"[TikTok] ⛔ Đăng TikTok thất bại vĩnh viễn sau {job.TikTokRetryCount} lần thử: {result.ErrorMessage}");
            }
        }

        await _db.UpdateJobAsync(job);
    }

    private async Task ProcessYouTubePlatformAsync(AutomationJob job, AutomationConfig config, CancellationToken ct)
    {
        // Nếu đang ở trạng thái AwaitingConfirmation, thử kiểm tra lại trước khi upload lại
        if (job.YouTubeStatus == PlatformPublishState.AwaitingConfirmation && !string.IsNullOrEmpty(job.YouTubePostId))
        {
            LogMessage?.Invoke($"[YouTube] 🔍 Đang kiểm tra lại video cũ trên YouTube (ID: {job.YouTubePostId})...");
            var exists = await _youtubeService.VerifyVideoExistsAsync(job.YouTubePostId, ct);
            if (exists)
            {
                job.YouTubeStatus = PlatformPublishState.Published;
                job.YouTubePostUrl = $"https://youtu.be/{job.YouTubePostId}";
                job.YouTubePublishedAt = DateTime.UtcNow;
                LogMessage?.Invoke($"[YouTube] 🎉 Xác nhận video đã tồn tại trên YouTube! Video ID: {job.YouTubePostId}");
                await _db.UpdateJobAsync(job);
                return;
            }
        }

        job.YouTubeStatus = PlatformPublishState.Uploading;
        await _db.UpdateJobAsync(job);
        LogMessage?.Invoke("[YouTube] ☁️ Đang tải video lên YouTube Shorts...");

        var result = await _youtubeService.UploadAsync(job, config, pct => PlatformProgress?.Invoke(job, "YouTube", pct), ct);

        if (result.Success)
        {
            job.YouTubeStatus = PlatformPublishState.Published;
            job.YouTubePostId = result.VideoId;
            job.YouTubePostUrl = result.VideoUrl;
            job.YouTubeErrorMessage = null;
            job.YouTubePublishedAt = DateTime.UtcNow;
            LogMessage?.Invoke($"[YouTube] 🎉 Đăng YouTube Shorts thành công! URL: {result.VideoUrl}");
        }
        else if (result.IsPendingConfirmation)
        {
            job.YouTubeStatus = PlatformPublishState.AwaitingConfirmation;
            job.YouTubePostId = result.VideoId ?? job.YouTubePostId;
            job.YouTubeErrorMessage = result.ErrorMessage;
            LogMessage?.Invoke($"[YouTube] ⚠️ Upload YouTube chưa rõ kết quả. Chuyển sang AwaitingConfirmation: {result.ErrorMessage}");
        }
        else
        {
            job.YouTubeRetryCount++;
            job.YouTubeErrorMessage = result.ErrorMessage;
            if (job.YouTubeRetryCount < config.MaxUploadRetries)
            {
                job.YouTubeStatus = PlatformPublishState.RetryPending;
                LogMessage?.Invoke($"[YouTube] 🔄 Lỗi tạm thời: {result.ErrorMessage}. Lần thử {job.YouTubeRetryCount}/{config.MaxUploadRetries}. Sẽ thử lại sau.");
            }
            else
            {
                job.YouTubeStatus = PlatformPublishState.FailedPermanent;
                LogMessage?.Invoke($"[YouTube] ⛔ Đăng YouTube thất bại vĩnh viễn sau {job.YouTubeRetryCount} lần thử: {result.ErrorMessage}");
            }
        }

        await _db.UpdateJobAsync(job);
    }

    private static void EvaluateFinalJobStatus(AutomationJob job, AutomationConfig config)
    {
        bool ttActive = config.TikTokEnabled;
        bool ytActive = config.YouTubeEnabled;

        bool ttPublished = !ttActive || job.TikTokStatus == PlatformPublishState.Published;
        bool ytPublished = !ytActive || job.YouTubeStatus == PlatformPublishState.Published;

        if (ttPublished && ytPublished)
        {
            job.Status = JobStatus.Published;
            job.FinishedAt = DateTime.UtcNow;
            return;
        }

        // Nếu một trong hai nền tảng đang AwaitingConfirmation -> Job phải giữ AwaitingConfirmation
        if ((ttActive && job.TikTokStatus == PlatformPublishState.AwaitingConfirmation) ||
            (ytActive && job.YouTubeStatus == PlatformPublishState.AwaitingConfirmation))
        {
            job.Status = JobStatus.AwaitingConfirmation;
            return;
        }

        // Nếu một trong hai nền tảng đang Uploading -> Giữ Uploading
        if ((ttActive && job.TikTokStatus == PlatformPublishState.Uploading) ||
            (ytActive && job.YouTubeStatus == PlatformPublishState.Uploading))
        {
            job.Status = JobStatus.Uploading;
            return;
        }

        // Nếu có nền tảng RetryPending -> Chuyển RetryPending để scheduler gọi lại
        if ((ttActive && job.TikTokStatus == PlatformPublishState.RetryPending) ||
            (ytActive && job.YouTubeStatus == PlatformPublishState.RetryPending))
        {
            job.Status = JobStatus.RetryPending;
            job.ScheduledPublishAt = DateTime.UtcNow.AddSeconds(config.RetryBackoffSeconds);
            return;
        }

        // Nếu một trong hai nền tảng FailedPermanent mà không còn retry
        if ((ttActive && job.TikTokStatus == PlatformPublishState.FailedPermanent) ||
            (ytActive && job.YouTubeStatus == PlatformPublishState.FailedPermanent))
        {
            job.Status = JobStatus.FailedPermanent;
            job.FinishedAt = DateTime.UtcNow;
            return;
        }

        job.Status = JobStatus.RetryPending;
    }
}
