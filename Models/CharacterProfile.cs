using System.Collections.Generic;
using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace VideoAutoWpf.Models;

/// <summary>
/// Hồ sơ tạo hình nhân vật độc nhất (Character Profile / Casting Bible).
/// Đảm bảo các nhân vật trong video có ngoại hình, trang phục, phong cách riêng biệt, không bị trùng lặp.
/// </summary>
public partial class CharacterProfile : ObservableObject
{
    [ObservableProperty]
    [JsonPropertyName("id")]
    private string _id = string.Empty;

    [ObservableProperty]
    [JsonPropertyName("name")]
    private string _name = string.Empty; // e.g. "Lan (Nạn nhân)"

    [ObservableProperty]
    [JsonPropertyName("role")]
    private string _role = string.Empty; // e.g. "Nạn nhân", "Nghi phạm / Hung thủ", "Điều tra viên", "Nhân chứng"

    [ObservableProperty]
    [JsonPropertyName("gender_age")]
    private string _genderAge = string.Empty; // e.g. "Nữ, 22 tuổi"

    [ObservableProperty]
    [JsonPropertyName("appearance_vn")]
    private string _appearanceVn = string.Empty; // Mô tả vóc dáng, khuôn mặt, kiểu tóc bằng tiếng Việt

    [ObservableProperty]
    [JsonPropertyName("outfit_vn")]
    private string _outfitVn = string.Empty; // Trang phục & màu sắc nhận diện đặc trưng bằng tiếng Việt

    [ObservableProperty]
    [JsonPropertyName("visual_prompt")]
    private string _visualPrompt = string.Empty; // Đoạn mô tả tiếng Anh chuẩn Imagen 3 / Gemini Image

    [ObservableProperty]
    [JsonPropertyName("distinguishing_marker")]
    private string _distinguishingMarker = string.Empty; // Đặc điểm nhận diện cốt lõi (áo màu gì, phụ kiện gì...)

    // ── Mở rộng V2: Đặc tả nhận diện chi tiết (Character Bible V2) ──
    [ObservableProperty]
    [JsonPropertyName("face_description")]
    private string _faceDescription = string.Empty; // e.g. "Oval face, high cheekbones, delicate jawline"

    [ObservableProperty]
    [JsonPropertyName("hair")]
    private string _hair = string.Empty; // e.g. "Shoulder-length straight jet-black hair with wispy bangs"

    [ObservableProperty]
    [JsonPropertyName("skin")]
    private string _skin = string.Empty; // e.g. "Fair pale complexion"

    [ObservableProperty]
    [JsonPropertyName("body")]
    private string _body = string.Empty; // e.g. "Slender petite build, 160cm tall"

    [ObservableProperty]
    [JsonPropertyName("accessories")]
    private string _accessories = string.Empty; // e.g. "Thin silver necklace, beige canvas tote bag"

    [ObservableProperty]
    [JsonPropertyName("default_expressions")]
    private string _defaultExpressions = string.Empty; // e.g. "Anxious, vigilant, guarded glance"

    [ObservableProperty]
    [JsonPropertyName("default_poses")]
    private string _defaultPoses = string.Empty;

    [ObservableProperty]
    [JsonPropertyName("reference_image_path")]
    private string? _referenceImagePath;

    // ── Visual Planning Studio: Approval & Asset Management ──────────────────
    /// <summary>Nhân vật quan trọng cần phải duyệt ảnh trước khi sản xuất hàng loạt.</summary>
    [ObservableProperty]
    [JsonPropertyName("is_critical")]
    private bool _isCritical = true;

    /// <summary>Trạng thái duyệt tổng hợp (dựa trên PortraitAsset).</summary>
    [ObservableProperty]
    [JsonPropertyName("approval_status")]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    [NotifyPropertyChangedFor(nameof(IsApproved))]
    [NotifyPropertyChangedFor(nameof(IsLocked))]
    private AssetApprovalStatus _approvalStatus = AssetApprovalStatus.Pending;

    /// <summary>Ảnh chân dung chuẩn (Portrait) — ưu tiên cao nhất.</summary>
    [ObservableProperty]
    [JsonPropertyName("portrait_asset")]
    private VisualAsset _portraitAsset = new() { AssetType = "Portrait" };

    /// <summary>Ảnh toàn thân (FullBody).</summary>
    [ObservableProperty]
    [JsonPropertyName("full_body_asset")]
    private VisualAsset _fullBodyAsset = new() { AssetType = "FullBody" };

    /// <summary>Hồ sơ đã được khóa — không cho phép sửa mô tả mà không mở khóa.</summary>
    [JsonIgnore]
    public bool IsLocked => ApprovalStatus == AssetApprovalStatus.Approved;

    /// <summary>Các scene ID phụ thuộc vào nhân vật này — cần NeedsReview khi profile thay đổi.</summary>
    [JsonPropertyName("dependent_scene_ids")]
    public List<string> DependentSceneIds { get; set; } = new();

    /// <summary>Số scene dự kiến có nhân vật này xuất hiện.</summary>
    [ObservableProperty]
    [JsonPropertyName("expected_scene_count")]
    private int _expectedSceneCount;

    [JsonIgnore]
    public bool IsApproved => ApprovalStatus == AssetApprovalStatus.Approved;

    [JsonIgnore]
    public string StatusText => ApprovalStatus.ToDisplayText();

    [JsonIgnore]
    public string? DisplayImagePath => PortraitAsset.DisplayImagePath ?? FullBodyAsset.DisplayImagePath;

    [JsonIgnore]
    public bool HasAnyImage => PortraitAsset.HasImage || FullBodyAsset.HasImage;

    [JsonIgnore]
    public bool HasImage => HasAnyImage;

    [JsonIgnore]
    public bool CanApprove => HasAnyImage;

    public string DisplayTitle => string.IsNullOrWhiteSpace(Role) ? Name : $"{Name} • {Role}";
    public string DisplaySummary => $"{GenderAge} • {OutfitVn}";
    public string SceneCountText => ExpectedSceneCount > 0 ? $"{ExpectedSceneCount} cảnh" : "";

    public void NotifyImageUpdated()
    {
        OnPropertyChanged(nameof(DisplayImagePath));
        OnPropertyChanged(nameof(HasAnyImage));
        OnPropertyChanged(nameof(HasImage));
        OnPropertyChanged(nameof(CanApprove));
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(IsApproved));
        OnPropertyChanged(nameof(IsLocked));
    }

    /// <summary>
    /// Đánh dấu tất cả scene phụ thuộc là NeedsReview khi profile thay đổi.
    /// KHÔNG tự động xóa hay tạo lại ảnh.
    /// </summary>
    public void MarkDependentScenesForReview()
    {
        if (ApprovalStatus == AssetApprovalStatus.Approved)
            ApprovalStatus = AssetApprovalStatus.NeedsReview;
        PortraitAsset.MarkNeedsReview();
        FullBodyAsset.MarkNeedsReview();
        NotifyImageUpdated();
    }
}
