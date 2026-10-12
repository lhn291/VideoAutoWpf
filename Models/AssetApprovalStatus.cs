namespace VideoAutoWpf.Models;

/// <summary>
/// Trạng thái duyệt của một tài sản hình ảnh (nhân vật, địa điểm, phong cách, scene mẫu).
/// </summary>
public enum AssetApprovalStatus
{
    /// <summary>Chưa tạo ảnh / chưa có dữ liệu.</summary>
    Pending,

    /// <summary>Đang gọi API tạo ảnh.</summary>
    Generating,

    /// <summary>Đã tạo ảnh, đang chờ người dùng xem và duyệt.</summary>
    AwaitingApproval,

    /// <summary>Người dùng đã duyệt — tài sản được chốt làm reference chuẩn.</summary>
    Approved,

    /// <summary>
    /// Đã được duyệt trước đó nhưng hồ sơ/mô tả vừa thay đổi.
    /// Người dùng cần xem lại và xác nhận lại trước khi sản xuất hàng loạt.
    /// </summary>
    NeedsReview,

    /// <summary>Lỗi khi tạo ảnh — cần thử lại.</summary>
    Error
}

public static class AssetApprovalStatusExtensions
{
    public static string ToDisplayText(this AssetApprovalStatus status) => status switch
    {
        AssetApprovalStatus.Pending          => "⬜ Chưa tạo",
        AssetApprovalStatus.Generating       => "⏳ Đang tạo...",
        AssetApprovalStatus.AwaitingApproval => "👁️ Chờ duyệt",
        AssetApprovalStatus.Approved         => "✅ Đã duyệt",
        AssetApprovalStatus.NeedsReview      => "⚠️ Cần xem lại",
        AssetApprovalStatus.Error            => "❌ Lỗi",
        _                                    => "❓ Không xác định"
    };

    public static bool IsReadyForProduction(this AssetApprovalStatus status) =>
        status == AssetApprovalStatus.Approved;

    public static bool IsBlockingProduction(this AssetApprovalStatus status) =>
        status != AssetApprovalStatus.Approved;
}
