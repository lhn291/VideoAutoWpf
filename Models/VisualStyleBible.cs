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
    public bool IsIllustrationStyle =>
        StyleKey != "realistic-photo" && StyleKey != "cinematic-horror";

    [JsonIgnore]
    public string? SampleImagePath => SampleAsset?.DisplayImagePath;

    /// <summary>
    /// Khởi tạo VisualStyleBible từ styleKey được chọn bên ngoài (khớp với StyleCard và GeminiScriptService.StylePrompts).
    /// </summary>
    public static VisualStyleBible CreateFromStyleKey(string styleKey, string? title = null, string? description = null, string aspectRatio = "9:16")
    {
        var bible = new VisualStyleBible
        {
            StyleKey = styleKey,
            AspectRatio = aspectRatio
        };

        if (Services.GeminiScriptService.StylePrompts.TryGetValue(styleKey, out var promptPrefix))
        {
            bible.GlobalPromptPrefix = promptPrefix.Trim();
        }

        var styleCard = StyleCard.GetDefaultStyles().FirstOrDefault(s => s.Key == styleKey);
        bible.StyleDisplayName = title ?? styleCard?.Title ?? styleKey;
        bible.StyleDescription = description ?? styleCard?.Description ?? string.Empty;

        switch (styleKey)
        {
            case "dark-anime":
                bible.ColorPalette = "dark indigo, ash grey, crimson red, dim amber";
                bible.LightingStyle = "dramatic cel shading, sharp cast shadows, moody backlight";
                bible.CameraStyle = "anime cinematic angle, high contrast";
                bible.DepthOfField = "sharp line art, 2D graphic novel depth";
                break;
            case "cartoon-2d":
                bible.ColorPalette = "solid retro palette, bold black ink outlines";
                bible.LightingStyle = "flat illustration lighting, minimal gradients";
                bible.CameraStyle = "classic 2D animation framing";
                bible.DepthOfField = "flat 2D plane";
                break;
            case "cyberpunk":
                bible.ColorPalette = "neon cyan, hot magenta, electric violet, deep midnight black";
                bible.LightingStyle = "neon rim lighting, reflections on wet asphalt";
                bible.CameraStyle = "wide dynamic anime angle";
                bible.DepthOfField = "2D anime cel shading with glowing highlights";
                break;
            case "horror-cartoon":
                bible.ColorPalette = "charcoal black, venom green, deep violet, dark red";
                bible.LightingStyle = "eerie harsh shadows, sketchy silhouette";
                bible.CameraStyle = "unsettling Dutch angle";
                bible.DepthOfField = "hand-drawn sketchy textures";
                break;
            case "realistic-photo":
                bible.ColorPalette = "natural grading, authentic documentary colors";
                bible.LightingStyle = "natural daylight, soft bounce, ambient practical lights";
                bible.CameraStyle = "DSLR photography 50mm lens";
                bible.DepthOfField = "shallow depth of field, natural bokeh";
                break;
            case "cinematic-horror":
                bible.ColorPalette = "desaturated cool teal, sickly olive, deep shadow black";
                bible.LightingStyle = "volumetric fog lighting, low-key chiaroscuro";
                bible.CameraStyle = "slow creeping cinematic wide shot";
                bible.DepthOfField = "anamorphic lens flare, shallow focus";
                break;
            default:
                bible.ColorPalette = "moody atmospheric crime palette";
                bible.LightingStyle = "dramatic cinematic lighting";
                bible.CameraStyle = "cinematic framing";
                bible.DepthOfField = "atmospheric depth";
                break;
        }

        return bible;
    }

    /// <summary>Tạo global prompt prefix từ các thông số đã cài đặt.</summary>
    public string BuildPromptPrefix()
    {
        var parts = new List<string>();

        // 1. Prefix gốc định hình chất liệu vẽ (2D anime comic novel / cartoon / painting / photo)
        if (!string.IsNullOrWhiteSpace(GlobalPromptPrefix))
        {
            parts.Add(GlobalPromptPrefix.Trim().TrimEnd('.'));
        }
        else if (Services.GeminiScriptService.StylePrompts.TryGetValue(StyleKey, out var registeredPrompt))
        {
            parts.Add(registeredPrompt.Trim().TrimEnd('.'));
        }
        else if (!string.IsNullOrWhiteSpace(StyleDisplayName))
        {
            parts.Add($"In a {StyleDisplayName} art style");
        }

        // 2. Tinh chỉnh ánh sáng, bảng màu
        if (!string.IsNullOrWhiteSpace(LightingStyle))
            parts.Add(LightingStyle);
        if (!string.IsNullOrWhiteSpace(ColorPalette))
            parts.Add($"color palette: {ColorPalette}");

        return string.Join(", ", parts);
    }
}
