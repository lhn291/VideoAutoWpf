using System.Collections.Generic;
using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace VideoAutoWpf.Models.V2;

/// <summary>
/// Hồ sơ bối cảnh / địa điểm (Location Profile / Location Bible).
/// Đảm bảo một địa điểm xuất hiện trong nhiều cảnh/shot luôn duy trì phong cách kiến trúc,
/// bài trí nội thất, ánh sáng và bảng màu cố định.
/// </summary>
public partial class LocationProfile : ObservableObject
{
    [ObservableProperty]
    [JsonPropertyName("id")]
    private string _id = Guid.NewGuid().ToString("N");

    [ObservableProperty]
    [JsonPropertyName("name")]
    private string _name = string.Empty; // e.g. "Khách sạn Cecil - Tiền sảnh"

    [ObservableProperty]
    [JsonPropertyName("function_or_purpose")]
    private string _functionOrPurpose = string.Empty; // Chức năng: "Hiện trường vụ án", "Nơi làm việc", "Nơi ẩn náu"...

    [ObservableProperty]
    [JsonPropertyName("architectural_style")]
    private string _architecturalStyle = string.Empty; // e.g. "1920s Art Deco vintage hotel architecture"

    [ObservableProperty]
    [JsonPropertyName("spatial_layout")]
    private string _spatialLayout = string.Empty; // Bố cục không gian: cửa chính hướng đông, quầy tiếp tân bằng gỗ bên phải, cầu thang bộ phía sau...

    [ObservableProperty]
    [JsonPropertyName("interior_details")]
    private string _interiorDetails = string.Empty; // e.g. "Dim amber chandelier, worn mahogany reception counter, patterned wallpaper"

    [ObservableProperty]
    [JsonPropertyName("exterior_details")]
    private string _exteriorDetails = string.Empty; // e.g. "Weathered brick facade, flickering red neon sign, rain-slicked asphalt"

    [ObservableProperty]
    [JsonPropertyName("signature_colors")]
    private string _signatureColors = string.Empty; // e.g. "Murky olive green, deep mahogany brown, dim tungsten amber"

    [ObservableProperty]
    [JsonPropertyName("lighting_default")]
    private string _lightingDefault = "dim_atmospheric"; // volumetric, chiaroscuro, fluorescent, harsh_tungsten, natural_daylight

    [JsonPropertyName("key_props")]
    public List<string> KeyProps { get; set; } = new(); // Đạo cụ cố định: "Đồng hồ treo tường quả lắc", "Quầy bar gỗ sồi", "Biển báo kim loại vàng"

    [JsonPropertyName("fixed_elements")]
    public List<string> FixedElements { get; set; } = new(); // Các yếu tố TUYỆT ĐỐI không thay đổi

    [JsonPropertyName("variable_elements")]
    public List<string> VariableElements { get; set; } = new(); // Các yếu tố được phép thay đổi: thời tiết, góc máy, sự xáo trộn đồ đạc

    [ObservableProperty]
    [JsonPropertyName("interior_or_exterior")]
    private string _interiorOrExterior = "Interior";

    [ObservableProperty]
    [JsonPropertyName("visual_prompt")]
    private string _visualPrompt = string.Empty;

    [ObservableProperty]
    [JsonPropertyName("visual_prompt_anchor")]
    private string _visualPromptAnchor = string.Empty; // Cụm từ tiếng Anh chuẩn chèn vào prompt để giữ consistency

    public string EffectiveVisualPrompt => !string.IsNullOrWhiteSpace(VisualPrompt) ? VisualPrompt : VisualPromptAnchor;

    [ObservableProperty]
    [JsonPropertyName("reference_image_path")]
    private string? _referenceImagePath;

    // ── Visual Planning Studio: Approval & Asset Management ─────────────────
    /// <summary>Địa điểm quan trọng cần duyệt ảnh trước khi sản xuất hàng loạt.</summary>
    [ObservableProperty]
    [JsonPropertyName("is_critical")]
    private bool _isCritical = true;

    [ObservableProperty]
    [JsonPropertyName("approval_status")]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    [NotifyPropertyChangedFor(nameof(IsApproved))]
    [NotifyPropertyChangedFor(nameof(IsLocked))]
    private AssetApprovalStatus _approvalStatus = AssetApprovalStatus.Pending;

    /// <summary>Ảnh tổng thể chuẩn của địa điểm (wide establishing shot).</summary>
    [ObservableProperty]
    [JsonPropertyName("location_asset")]
    private VideoAutoWpf.Models.VisualAsset _locationAsset = new() { AssetType = "Location" };

    /// <summary>Số scene dự kiến sử dụng địa điểm này.</summary>
    [ObservableProperty]
    [JsonPropertyName("expected_scene_count")]
    private int _expectedSceneCount;

    [JsonPropertyName("dependent_scene_ids")]
    public List<string> DependentSceneIds { get; set; } = new();

    [JsonIgnore]
    public bool IsLocked => ApprovalStatus == AssetApprovalStatus.Approved;

    [JsonIgnore]
    public bool IsApproved => ApprovalStatus == AssetApprovalStatus.Approved;

    [JsonIgnore]
    public string StatusText => ApprovalStatus.ToDisplayText();

    [JsonIgnore]
    public string? DisplayImagePath => LocationAsset.DisplayImagePath;

    [JsonIgnore]
    public bool HasImage => LocationAsset.HasImage;

    public string DisplayTitle => string.IsNullOrWhiteSpace(ArchitecturalStyle) ? Name : $"{Name} ({ArchitecturalStyle})";
    public string SceneCountText => ExpectedSceneCount > 0 ? $"{ExpectedSceneCount} cảnh" : "";

    public void MarkDependentScenesForReview()
    {
        if (ApprovalStatus == AssetApprovalStatus.Approved)
            ApprovalStatus = AssetApprovalStatus.NeedsReview;
        LocationAsset.MarkNeedsReview();
    }
}
