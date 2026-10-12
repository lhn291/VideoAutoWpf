using System.Text.Json.Serialization;

namespace VideoAutoWpf.Models.V2;

/// <summary>
/// Đặc tả chi tiết hình ảnh (Visual Specification) do Visual Director bóc tách từ lời thoại.
/// Quyết định người xem CẦN NHÌN THẤY CÁI GÌ một cách cụ thể trước khi biên dịch thành prompt gửi AI.
/// </summary>
public class VisualSpec
{
    [JsonPropertyName("subject")]
    public string Subject { get; set; } = string.Empty;

    [JsonPropertyName("action")]
    public string Action { get; set; } = string.Empty;

    [JsonPropertyName("emotion")]
    public string Emotion { get; set; } = string.Empty;

    [JsonPropertyName("environment")]
    public string Environment { get; set; } = string.Empty;

    [JsonPropertyName("time_of_day")]
    public string TimeOfDay { get; set; } = "night"; // dawn, morning, afternoon, sunset, night, midnight

    [JsonPropertyName("weather")]
    public string Weather { get; set; } = "clear"; // clear, rainy, foggy, stormy, overcast

    [JsonPropertyName("camera_framing")]
    public string Framing { get; set; } = "eye_level"; // low_angle, high_angle, eye_level, dutch_angle, birds_eye

    [JsonPropertyName("lighting")]
    public string Lighting { get; set; } = "cinematic_moody"; // volumetric, harsh_shadows, neon_glow, chiaroscuro, natural

    [JsonPropertyName("color_palette")]
    public string ColorPalette { get; set; } = string.Empty;

    [JsonPropertyName("key_props")]
    public List<string> KeyProps { get; set; } = new();

    [JsonPropertyName("continuity_constraints")]
    public List<string> ContinuityConstraints { get; set; } = new();

    [JsonPropertyName("negative_constraints")]
    public List<string> NegativeConstraints { get; set; } = new();
}
