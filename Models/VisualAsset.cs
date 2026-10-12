using System.IO;
using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace VideoAutoWpf.Models;

/// <summary>
/// Một phiên bản ảnh đã tạo. Mỗi lần tạo lại = một VisualAssetVersion mới.
/// Không bao giờ ghi đè phiên bản cũ — chỉ thêm mới vào danh sách.
/// </summary>
public class VisualAssetVersion
{
    [JsonPropertyName("version_index")]
    public int VersionIndex { get; set; }

    /// <summary>Đường dẫn tệp ảnh trên disk.</summary>
    [JsonPropertyName("image_path")]
    public string ImagePath { get; set; } = string.Empty;

    /// <summary>Prompt đã dùng để tạo ảnh này.</summary>
    [JsonPropertyName("prompt_used")]
    public string PromptUsed { get; set; } = string.Empty;

    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    [JsonPropertyName("notes")]
    public string Notes { get; set; } = string.Empty;

    public bool FileExists => !string.IsNullOrEmpty(ImagePath) && File.Exists(ImagePath);
}

/// <summary>
/// Tài sản hình ảnh có lịch sử phiên bản và trạng thái duyệt.
/// Dùng cho ảnh chân dung nhân vật, ảnh toàn thân, ảnh địa điểm, ảnh style mẫu.
/// </summary>
public partial class VisualAsset : ObservableObject
{
    [ObservableProperty]
    [JsonPropertyName("asset_id")]
    private string _assetId = Guid.NewGuid().ToString("N")[..8];

    /// <summary>Loại tài sản: Portrait | FullBody | Location | StyleSample | ScenePreview</summary>
    [ObservableProperty]
    [JsonPropertyName("asset_type")]
    private string _assetType = "Portrait";

    /// <summary>Tên mô tả ngắn gọn (ví dụ: "Chân dung - Lan", "Toàn thân - Thám Tử").</summary>
    [ObservableProperty]
    [JsonPropertyName("label")]
    private string _label = string.Empty;

    [ObservableProperty]
    [JsonPropertyName("status")]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    [NotifyPropertyChangedFor(nameof(IsApproved))]
    [NotifyPropertyChangedFor(nameof(IsGenerating))]
    [NotifyPropertyChangedFor(nameof(CanApprove))]
    private AssetApprovalStatus _status = AssetApprovalStatus.Pending;

    [JsonPropertyName("versions")]
    public List<VisualAssetVersion> Versions { get; set; } = new();

    /// <summary>Index của phiên bản được duyệt làm chuẩn. -1 = chưa có.</summary>
    [ObservableProperty]
    [JsonPropertyName("approved_version_index")]
    [NotifyPropertyChangedFor(nameof(ApprovedImagePath))]
    private int _approvedVersionIndex = -1;

    [JsonIgnore]
    public string? ApprovedImagePath =>
        ApprovedVersionIndex >= 0 && ApprovedVersionIndex < Versions.Count
            ? Versions[ApprovedVersionIndex].ImagePath
            : Versions.LastOrDefault(v => v.FileExists)?.ImagePath;

    [JsonIgnore]
    public string? LatestImagePath =>
        Versions.LastOrDefault(v => v.FileExists)?.ImagePath;

    [JsonIgnore]
    public string? DisplayImagePath =>
        IsApproved ? ApprovedImagePath : LatestImagePath;

    [JsonIgnore]
    public string StatusText => Status.ToDisplayText();

    [JsonIgnore]
    public bool IsApproved => Status == AssetApprovalStatus.Approved;

    [JsonIgnore]
    public bool IsGenerating => Status == AssetApprovalStatus.Generating;

    [JsonIgnore]
    public bool CanApprove => Status == AssetApprovalStatus.AwaitingApproval
                           || Status == AssetApprovalStatus.NeedsReview;

    [JsonIgnore]
    public bool HasImage => Versions.Any(v => v.FileExists);

    [JsonIgnore]
    public int VersionCount => Versions.Count;

    /// <summary>Thêm một ảnh mới vào lịch sử (không xóa cũ).</summary>
    public VisualAssetVersion AddVersion(string imagePath, string promptUsed, string notes = "")
    {
        var v = new VisualAssetVersion
        {
            VersionIndex = Versions.Count,
            ImagePath = imagePath,
            PromptUsed = promptUsed,
            Notes = notes,
            CreatedAt = DateTime.Now
        };
        Versions.Add(v);
        OnPropertyChanged(nameof(LatestImagePath));
        OnPropertyChanged(nameof(DisplayImagePath));
        OnPropertyChanged(nameof(HasImage));
        OnPropertyChanged(nameof(VersionCount));
        return v;
    }

    /// <summary>Duyệt phiên bản mới nhất làm ảnh chuẩn.</summary>
    public void ApproveLatest()
    {
        if (Versions.Count == 0) return;
        ApprovedVersionIndex = Versions.Count - 1;
        Status = AssetApprovalStatus.Approved;
    }

    /// <summary>Duyệt một phiên bản cụ thể.</summary>
    public void ApproveVersion(int versionIndex)
    {
        if (versionIndex < 0 || versionIndex >= Versions.Count) return;
        ApprovedVersionIndex = versionIndex;
        Status = AssetApprovalStatus.Approved;
    }

    /// <summary>Đánh dấu cần xem lại do hồ sơ liên quan thay đổi.</summary>
    public void MarkNeedsReview()
    {
        if (Status == AssetApprovalStatus.Approved)
            Status = AssetApprovalStatus.NeedsReview;
    }
}
