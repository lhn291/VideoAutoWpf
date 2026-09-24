using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace VideoAutoWpf.Models;

public partial class ScriptPlan : ObservableObject
{
    [ObservableProperty]
    [JsonPropertyName("suggested_scenes")]
    private int _suggestedScenes = 5;

    [ObservableProperty]
    [JsonPropertyName("suggested_style")]
    private string _suggestedStyle = "dark-anime";

    [ObservableProperty]
    [JsonPropertyName("style_description")]
    private string _styleDescription = string.Empty;

    [ObservableProperty]
    [JsonPropertyName("tone")]
    private string _tone = string.Empty;

    [ObservableProperty]
    [JsonPropertyName("ratio")]
    private string _ratio = "9:16";

    [ObservableProperty]
    [JsonPropertyName("voice")]
    private string _voice = "vi-VN-Wavenet-B";

    [ObservableProperty]
    [JsonPropertyName("summary")]
    private string _summary = string.Empty;

    [ObservableProperty]
    [JsonPropertyName("character_hint")]
    private string _characterHint = string.Empty;

    [ObservableProperty]
    [JsonPropertyName("motion_effect")]
    private string _motionEffect = "auto";

    [ObservableProperty]
    [JsonPropertyName("enable_fade")]
    private bool _enableFadeTransition = true;

    [ObservableProperty]
    [JsonPropertyName("enable_vignette")]
    private bool _enableVignette = false;

    [ObservableProperty]
    [JsonPropertyName("suggested_bgm")]
    private string _suggestedBgm = "dramatic";

    [ObservableProperty]
    [JsonPropertyName("enable_subtitles")]
    private bool _enableSubtitles = true;

    [ObservableProperty]
    [JsonPropertyName("subtitle_style")]
    private string _subtitleStyle = "cinematic";

    [ObservableProperty]
    [JsonPropertyName("subtitle_font")]
    private string _subtitleFont = "Segoe UI Bold";

    [ObservableProperty]
    [JsonPropertyName("subtitle_reason")]
    private string _subtitleReason = string.Empty;

    [ObservableProperty]
    [JsonPropertyName("enable_text_on_image")]
    private bool _enableTextOnImage;
}
