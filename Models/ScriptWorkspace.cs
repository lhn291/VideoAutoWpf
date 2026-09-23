using System.Text.Json.Serialization;

namespace VideoAutoWpf.Models;

public class ScriptWorkspace
{
    [JsonPropertyName("metadata")]
    public ScriptMetadata Metadata { get; set; } = new();

    [JsonPropertyName("scenes")]
    public List<ScriptScene> Scenes { get; set; } = new();
}

public class ScriptMetadata
{
    [JsonPropertyName("ratio")]
    public string Ratio { get; set; } = "9:16";

    [JsonPropertyName("voice")]
    public string Voice { get; set; } = "vi-VN-Wavenet-B";

    [JsonPropertyName("music")]
    public string? Music { get; set; }

    [JsonPropertyName("music_volume")]
    public double MusicVolume { get; set; } = 0.15;

    [JsonPropertyName("enable_music")]
    public bool EnableMusic { get; set; } = false;

    [JsonPropertyName("character_rules")]
    public string? CharacterRules { get; set; }

    [JsonPropertyName("motion_effect")]
    public string MotionEffect { get; set; } = "random";

    [JsonPropertyName("enable_fade")]
    public bool EnableFade { get; set; } = true;

    [JsonPropertyName("enable_vignette")]
    public bool EnableVignette { get; set; } = false;

    [JsonPropertyName("enable_subtitles")]
    public bool EnableSubtitles { get; set; } = true;

    [JsonPropertyName("subtitle_style")]
    public string SubtitleStyle { get; set; } = "cinematic";

    [JsonPropertyName("subtitle_font")]
    public string SubtitleFont { get; set; } = "Segoe UI Bold";
}

public class ScriptScene
{
    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;

    [JsonPropertyName("image_prompt")]
    public string ImagePrompt { get; set; } = string.Empty;

    [JsonPropertyName("motion_effect")]
    public string? MotionEffect { get; set; }

    [JsonPropertyName("engine")]
    public string? Engine { get; set; } = "imagen";
}
