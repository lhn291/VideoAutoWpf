using System.Text.Json;
using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace VideoAutoWpf.Models.Automation;

/// <summary>
/// Đại diện cho một công việc tự động hóa có trạng thái lưu bền vững (Persistent Job).
/// Được lưu trữ và phục hồi từ SQLite.
/// </summary>
public partial class AutomationJob : ObservableObject
{
    [ObservableProperty]
    private string _id = Guid.NewGuid().ToString("N");

    [ObservableProperty]
    private JobType _jobType = JobType.RenderSingleVideo;

    [ObservableProperty]
    private JobStatus _status = JobStatus.Pending;

    [ObservableProperty]
    private string _channelId = "default";

    [ObservableProperty]
    private string _videoId = string.Empty;

    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private string? _outputPath;

    [ObservableProperty]
    private string? _thumbnailPath;

    [ObservableProperty]
    private double _durationSeconds;

    [ObservableProperty]
    private string? _caption;

    [ObservableProperty]
    private string? _hashtags;

    /// <summary>
    /// Dữ liệu JSON chi tiết đủ để khôi phục công việc sau crash/restart
    /// (chứa danh sách phân cảnh, video config, series project id, episode number...)
    /// </summary>
    [ObservableProperty]
    private string _payloadJson = "{}";

    [ObservableProperty]
    private DateTime _createdAt = DateTime.UtcNow;

    [ObservableProperty]
    private DateTime? _startedAt;

    [ObservableProperty]
    private DateTime _updatedAt = DateTime.UtcNow;

    [ObservableProperty]
    private DateTime? _finishedAt;

    [ObservableProperty]
    private DateTime? _scheduledPublishAt;

    [ObservableProperty]
    private int _retryCount = 0;

    [ObservableProperty]
    private int _maxRetries = 3;

    [ObservableProperty]
    private string? _lastErrorMessage;

    /// <summary>
    /// Thời gian heartbeat gần nhất của worker đang xử lý job này
    /// </summary>
    [ObservableProperty]
    private DateTime? _heartbeatAt;

    /// <summary>
    /// Thời hạn lease khóa công việc. Nếu quá hạn mà job vẫn Running -> coi như đã crash
    /// </summary>
    [ObservableProperty]
    private DateTime? _leaseExpiresAt;

    // ── Telegram Approval State ──
    [ObservableProperty]
    private int? _telegramMessageId;

    [ObservableProperty]
    private string? _approvalDecision; // "Approved" | "Rejected" | null

    [ObservableProperty]
    private DateTime? _approvalDecidedAt;

    [ObservableProperty]
    private long? _approvalDecidedByUserId;

    // ── Nền tảng TikTok ──
    [ObservableProperty]
    private PlatformPublishState _tikTokStatus = PlatformPublishState.NotStarted;

    [ObservableProperty]
    private string? _tikTokPostId;

    [ObservableProperty]
    private string? _tikTokPostUrl;

    [ObservableProperty]
    private string? _tikTokErrorMessage;

    [ObservableProperty]
    private DateTime? _tikTokPublishedAt;

    [ObservableProperty]
    private int _tikTokRetryCount = 0;

    // ── Nền tảng YouTube ──
    [ObservableProperty]
    private PlatformPublishState _youTubeStatus = PlatformPublishState.NotStarted;

    [ObservableProperty]
    private string? _youTubePostId;

    [ObservableProperty]
    private string? _youTubePostUrl;

    [ObservableProperty]
    private string? _youTubeErrorMessage;

    [ObservableProperty]
    private DateTime? _youTubePublishedAt;

    [ObservableProperty]
    private int _youTubeRetryCount = 0;

    // ── Helper Properties ──
    [JsonIgnore]
    public bool IsActive => Status == JobStatus.Running || Status == JobStatus.Uploading || Status == JobStatus.QualityCheck;

    [JsonIgnore]
    public bool IsTerminal => Status == JobStatus.Published || Status == JobStatus.Rejected || Status == JobStatus.Cancelled || Status == JobStatus.FailedPermanent;

    [JsonIgnore]
    public bool IsAwaitingApproval => Status == JobStatus.AwaitingApproval;

    [JsonIgnore]
    public bool IsPublishingOrScheduled => Status == JobStatus.Approved || Status == JobStatus.Scheduled || Status == JobStatus.Uploading || Status == JobStatus.AwaitingConfirmation;

    [JsonIgnore]
    public string DisplayStatus => Status switch
    {
        JobStatus.Pending => "⏳ Chờ xử lý",
        JobStatus.Running => "⚙️ Đang tạo video",
        JobStatus.QualityCheck => "🔍 Kiểm tra chất lượng",
        JobStatus.AwaitingApproval => "📱 Chờ duyệt Telegram",
        JobStatus.Approved => "✅ Đã duyệt",
        JobStatus.Rejected => "❌ Đã từ chối",
        JobStatus.Scheduled => "🕒 Đã lên lịch đăng",
        JobStatus.Uploading => "☁️ Đang tải lên",
        JobStatus.AwaitingConfirmation => "🔎 Chờ xác nhận đăng",
        JobStatus.Published => "🎉 Đã đăng hoàn tất",
        JobStatus.RetryPending => "🔄 Chờ thử lại",
        JobStatus.FailedPermanent => "⛔ Lỗi vĩnh viễn",
        JobStatus.Cancelled => "🚫 Đã hủy",
        _ => Status.ToString()
    };

    /// <summary>
    /// Lấy payload dưới dạng đối tượng T
    /// </summary>
    public T? GetPayload<T>() where T : class
    {
        if (string.IsNullOrWhiteSpace(PayloadJson) || PayloadJson == "{}") return null;
        try
        {
            return JsonSerializer.Deserialize<T>(PayloadJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Gán payload từ đối tượng
    /// </summary>
    public void SetPayload<T>(T payload)
    {
        PayloadJson = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = false });
    }
}
