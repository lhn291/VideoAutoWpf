using System.Collections.ObjectModel;
using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace VideoAutoWpf.Models;

/// <summary>
/// Hồ sơ phong cách hình ảnh chung cho cả dự án (Visual Style Bible).
/// Mọi ảnh scene phải tuân theo style này trừ khi scene có yêu cầu riêng được duyệt.
/// </summary>
public partial class VisualStyleBible : ObservableObject
{
    [ObservableProperty]
    [JsonPropertyName("style_id")]
    private string _styleId = Guid.NewGuid().ToString("N")[..8];

    // ── Phong cách tổng thể ──────────────────────────────────────────────────
    /// <summary>Key phong cách từ GeminiScriptService.StylePrompts, ví dụ: "dark-anime", "cinematic", "noir".</summary>
    [ObservableProperty]
    [JsonPropertyName("style_key")]
    private string _styleKey = "cinematic";

    [ObservableProperty]
    [JsonPropertyName("style_display_name")]
    private string _styleDisplayName = "Cinematic Realism";

    [ObservableProperty]
    [JsonPropertyName("style_description")]
    private string _styleDescription = "Phong cách điện ảnh thực tế, tối, huyền bí — phù hợp thể loại kỳ án.";

    // ── Bảng màu & Ánh sáng ─────────────────────────────────────────────────
    [ObservableProperty]
    [JsonPropertyName("color_palette")]
    private string _colorPalette = "deep navy, ash grey, blood crimson, amber dim";

    [ObservableProperty]
    [JsonPropertyName("lighting_style")]
    private string _lightingStyle = "chiaroscuro dramatic shadows, single key light, moody backlight";

    [ObservableProperty]
    [JsonPropertyName("contrast_level")]
    private string _contrastLevel = "high";

    // ── Camera & Framing ────────────────────────────────────────────────────
    [ObservableProperty]
    [JsonPropertyName("camera_style")]
    private string _cameraStyle = "handheld slightly unstable, ground-level perspective";

    [ObservableProperty]
    [JsonPropertyName("depth_of_field")]
    private string _depthOfField = "shallow, bokeh background, sharp subject";

    [ObservableProperty]
    [JsonPropertyName("aspect_ratio")]
    private string _aspectRatio = "9:16";

    // ── Quy tắc thể loại ────────────────────────────────────────────────────
    [JsonPropertyName("genre_rules")]
    public List<string> GenreRules { get; set; } = new()
    {
        "No fantasy elements or supernatural creatures",
        "Realistic modern Vietnamese setting",
        "Evidence and clues must be clearly visible",
        "Facial expressions convey tension and suspicion",
        "No watermarks or text overlays in image"
    };

    // ── Prompt prefix chuẩn ─────────────────────────────────────────────────
    [ObservableProperty]
    [JsonPropertyName("global_prompt_prefix")]
    private string _globalPromptPrefix = string.Empty;

    // ── Ảnh mẫu phong cách ──────────────────────────────────────────────────
    [ObservableProperty]
    [JsonPropertyName("status")]
    [NotifyPropertyChangedFor(nameof(IsApproved))]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    private AssetApprovalStatus _status = AssetApprovalStatus.Pending;

    [ObservableProperty]
    [JsonPropertyName("sample_asset")]
    private VisualAsset? _sampleAsset;

    [JsonIgnore]
    public bool IsApproved => Status == AssetApprovalStatus.Approved;

    [JsonIgnore]
    public string StatusText => Status.ToDisplayText();

    [JsonIgnore]
    public string? SampleImagePath => SampleAsset?.DisplayImagePath;

    /// <summary>Tạo global prompt prefix từ các thông số đã cài đặt.</summary>
    public string BuildPromptPrefix()
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(StyleDisplayName))
            parts.Add(StyleDisplayName);
        if (!string.IsNullOrWhiteSpace(LightingStyle))
            parts.Add(LightingStyle);
        if (!string.IsNullOrWhiteSpace(ColorPalette))
            parts.Add($"color palette: {ColorPalette}");
        if (!string.IsNullOrWhiteSpace(DepthOfField))
            parts.Add(DepthOfField);
        return string.Join(", ", parts);
    }
}
