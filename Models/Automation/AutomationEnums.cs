namespace VideoAutoWpf.Models.Automation;

/// <summary>
/// Trạng thái tổng thể của một công việc (Automation Job)
/// </summary>
public enum JobStatus
{
    /// <summary>Đang chờ xử lý</summary>
    Pending,

    /// <summary>Đang trong quá trình chạy tạo video / render</summary>
    Running,

    /// <summary>Đang kiểm tra chất lượng file MP4 sau khi render</summary>
    QualityCheck,

    /// <summary>Video đã sẵn sàng, đã gửi Telegram và đang chờ người dùng duyệt</summary>
    AwaitingApproval,

    /// <summary>Người dùng đã bấm Duyệt qua Telegram</summary>
    Approved,

    /// <summary>Người dùng đã bấm Từ Chối qua Telegram</summary>
    Rejected,

    /// <summary>Đã duyệt, đang chờ đến khung giờ đăng đã định</summary>
    Scheduled,

    /// <summary>Đang tải video lên nền tảng mạng xã hội</summary>
    Uploading,

    /// <summary>Đã gửi dữ liệu upload, đang kiểm tra xác nhận trạng thái từ nền tảng</summary>
    AwaitingConfirmation,

    /// <summary>Đã đăng thành công trên tất cả nền tảng được cấu hình</summary>
    Published,

    /// <summary>Gặp lỗi có thể thử lại, đang chờ đến lịch retry</summary>
    RetryPending,

    /// <summary>Lỗi vĩnh viễn không thể tự phục hồi, cần con người can thiệp</summary>
    FailedPermanent,

    /// <summary>Công việc đã bị hủy</summary>
    Cancelled
}

/// <summary>
/// Loại công việc được quản lý bởi PersistentJobScheduler
/// </summary>
public enum JobType
{
    /// <summary>Render video đơn lẻ từ phân cảnh</summary>
    RenderSingleVideo,

    /// <summary>Render một tập trong chuỗi series</summary>
    RenderSeriesEpisode,

    /// <summary>Batch render toàn bộ chuỗi series</summary>
    BatchSeries,

    /// <summary>Kiểm tra chất lượng video hoàn chỉnh</summary>
    QualityCheck,

    /// <summary>Gửi thông báo duyệt qua Telegram</summary>
    TelegramApproval,

    /// <summary>Đăng video lên TikTok</summary>
    PublishTikTok,

    /// <summary>Đăng video lên YouTube Shorts / Video</summary>
    PublishYouTube,

    /// <summary>Xác minh trạng thái bài đăng trên nền tảng</summary>
    VerifyPublish,

    /// <summary>Dọn dẹp file tạm an toàn</summary>
    Cleanup
}

/// <summary>
/// Nền tảng phát hành
/// </summary>
public enum PublishPlatform
{
    TikTok,
    YouTube
}

/// <summary>
/// Trạng thái đăng độc lập của từng nền tảng
/// </summary>
public enum PlatformPublishState
{
    NotStarted,
    Pending,
    Scheduled,
    Uploading,
    AwaitingConfirmation,
    Published,
    RetryPending,
    FailedPermanent
}

/// <summary>
/// Trạng thái của bộ điều phối tắt máy (ShutdownCoordinator)
/// </summary>
public enum ShutdownState
{
    /// <summary>Hệ thống đang hoạt động, có việc đang làm hoặc chờ làm</summary>
    Active,

    /// <summary>Tất cả điều kiện an toàn đã thỏa mãn, đang đếm ngược grace period</summary>
    ShutdownPending,

    /// <summary>Đang tiến hành gọi lệnh shutdown Windows an toàn</summary>
    ShuttingDown,

    /// <summary>Lệnh shutdown đã bị hủy (do có việc mới hoặc người dùng hủy)</summary>
    Aborted
}
