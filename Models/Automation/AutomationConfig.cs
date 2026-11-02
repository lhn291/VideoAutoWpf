using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace VideoAutoWpf.Models.Automation;

/// <summary>
/// Cấu hình toàn diện cho chế độ Auto Run, Telegram Approval, Auto Publish và Safe Shutdown.
/// Được lưu trữ an toàn trong SQLite hoặc file config.
/// </summary>
public partial class AutomationConfig : ObservableObject
{
    // ── Vận hành tự động (Auto Run) ──
    [ObservableProperty]
    [JsonPropertyName("auto_run_enabled")]
    private bool _autoRunEnabled = true;

    /// <summary>
    /// Chế độ chạy thử nghiệm an toàn: Giả lập đăng video và giả lập shutdown mà không tắt máy thật
    /// </summary>
    [ObservableProperty]
    [JsonPropertyName("dry_run_mode")]
    private bool _dryRunMode = false;

    // ── Điều phối tắt máy an toàn (Shutdown Coordinator) ──
    [ObservableProperty]
    [JsonPropertyName("auto_shutdown_enabled")]
    private bool _autoShutdownEnabled = true;

    [ObservableProperty]
    [JsonPropertyName("shutdown_grace_period_seconds")]
    private int _shutdownGracePeriodSeconds = 60;

    [ObservableProperty]
    [JsonPropertyName("wait_for_pending_approval")]
    private bool _waitForPendingApproval = true;

    [ObservableProperty]
    [JsonPropertyName("wait_for_scheduled_jobs")]
    private bool _waitForScheduledJobs = true;

    [ObservableProperty]
    [JsonPropertyName("shutdown_on_permanent_failure")]
    private bool _shutdownOnPermanentFailure = false;

    [ObservableProperty]
    [JsonPropertyName("idle_check_interval_seconds")]
    private int _idleCheckIntervalSeconds = 10;

    // ── Telegram Bot & Approval ──
    [ObservableProperty]
    [JsonPropertyName("telegram_bot_token")]
    private string _telegramBotToken = string.Empty;

    [ObservableProperty]
    [JsonPropertyName("telegram_chat_id")]
    private string _telegramChatId = string.Empty;

    /// <summary>
    /// Danh sách Telegram User ID được phép bấm nút Duyệt / Từ chối (phân tách bởi dấu phẩy)
    /// </summary>
    [ObservableProperty]
    [JsonPropertyName("telegram_allowed_user_ids")]
    private string _telegramAllowedUserIds = string.Empty;

    [ObservableProperty]
    [JsonPropertyName("telegram_polling_interval_seconds")]
    private int _telegramPollingIntervalSeconds = 3;

    // ── Đăng video TikTok ──
    [ObservableProperty]
    [JsonPropertyName("tiktok_enabled")]
    private bool _tikTokEnabled = true;

    [ObservableProperty]
    [JsonPropertyName("tiktok_client_key")]
    private string _tikTokClientKey = string.Empty;

    [ObservableProperty]
    [JsonPropertyName("tiktok_client_secret")]
    private string _tikTokClientSecret = string.Empty;

    [ObservableProperty]
    [JsonPropertyName("tiktok_access_token")]
    private string _tikTokAccessToken = string.Empty;

    [ObservableProperty]
    [JsonPropertyName("tiktok_refresh_token")]
    private string _tikTokRefreshToken = string.Empty;

    // ── Đăng video YouTube ──
    [ObservableProperty]
    [JsonPropertyName("youtube_enabled")]
    private bool _youTubeEnabled = true;

    [ObservableProperty]
    [JsonPropertyName("youtube_privacy_status")]
    private string _youTubePrivacyStatus = "public"; // "public", "unlisted", "private"

    [ObservableProperty]
    [JsonPropertyName("youtube_category_id")]
    private string _youTubeCategoryId = "22"; // 22: People & Blogs, 24: Entertainment

    // ── Retry & Resiliency ──
    [ObservableProperty]
    [JsonPropertyName("max_upload_retries")]
    private int _maxUploadRetries = 3;

    [ObservableProperty]
    [JsonPropertyName("retry_backoff_seconds")]
    private int _retryBackoffSeconds = 60;

    [ObservableProperty]
    [JsonPropertyName("job_lease_timeout_minutes")]
    private int _jobLeaseTimeoutMinutes = 30;

    // ── Helpers ──
    [JsonIgnore]
    public List<long> ParsedAllowedUserIds
    {
        get
        {
            var list = new List<long>();
            if (string.IsNullOrWhiteSpace(TelegramAllowedUserIds)) return list;
            foreach (var part in TelegramAllowedUserIds.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (long.TryParse(part.Trim(), out var id))
                {
                    list.Add(id);
                }
            }
            return list;
        }
    }

    /// <summary>
    /// Lấy token từ biến môi trường hoặc cấu hình (ưu tiên biến môi trường)
    /// </summary>
    public string ResolveTelegramBotToken()
    {
        var envToken = Environment.GetEnvironmentVariable("TELEGRAM_BOT_TOKEN");
        if (!string.IsNullOrWhiteSpace(envToken)) return envToken.Trim();
        return TelegramBotToken?.Trim() ?? string.Empty;
    }

    /// <summary>
    /// Lấy token TikTok từ biến môi trường hoặc cấu hình
    /// </summary>
    public string ResolveTikTokAccessToken()
    {
        var envToken = Environment.GetEnvironmentVariable("TIKTOK_ACCESS_TOKEN");
        if (!string.IsNullOrWhiteSpace(envToken)) return envToken.Trim();
        return TikTokAccessToken?.Trim() ?? string.Empty;
    }
}
