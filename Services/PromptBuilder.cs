using System.Text;
using VideoAutoWpf.Models;
using VideoAutoWpf.Models.V2;

namespace VideoAutoWpf.Services;

/// <summary>
/// Bộ biên dịch Prompt (Prompt Builder).
/// Nhận VisualSpec có cấu trúc kết hợp với Style Prompts, Character Bible và Location Bible
/// để tạo ra câu prompt tiếng Anh giàu chi tiết kỹ thuật cho Imagen 3 / Gemini Image.
/// Đảm bảo tính nhất quán và loại bỏ hoàn toàn các câu prompt mơ hồ, ẩn dụ.
/// </summary>
public static class PromptBuilder
{
    /// <summary>
    /// Biên dịch một VisualSpec thành câu prompt hoàn chỉnh gửi tới Image API.
    /// </summary>
    public static string BuildPrompt(
        VisualSpec spec,
        string styleKey = "dark-anime",
        IEnumerable<CharacterProfile>? activeCharacters = null,
        LocationProfile? activeLocation = null,
        string aspectRatio = "9:16",
        string? additionalShotContext = null)
    {
        var sb = new StringBuilder();

        // 1. STYLE PREFIX: Bắt buộc ở đầu câu để định hình toàn bộ phong cách nghệ thuật
        if (GeminiScriptService.StylePrompts.TryGetValue(styleKey, out var stylePrefix))
        {
            sb.Append(stylePrefix);
        }
        else
        {
            sb.Append(GeminiScriptService.StylePrompts["dark-anime"]);
        }

        // 2. LOCATION BIBLE ANCHOR: Đồng bộ phong cách kiến trúc và bảng màu bối cảnh
        if (activeLocation != null && !string.IsNullOrWhiteSpace(activeLocation.VisualPromptAnchor))
        {
            sb.Append($"Location set in {activeLocation.VisualPromptAnchor}. ");
        }
        else if (!string.IsNullOrWhiteSpace(spec.Environment))
        {
            sb.Append($"Environment: {spec.Environment}. ");
        }

        // 3. CHARACTER IDENTITY (CHARACTER BIBLE): Đồng bộ tạo hình cố định của nhân vật
        if (activeCharacters != null)
        {
            foreach (var ch in activeCharacters)
            {
                if (!string.IsNullOrWhiteSpace(ch.VisualPrompt))
                {
                    sb.Append($"Featuring {ch.VisualPrompt}. ");
                }
            }
        }

        // 4. CONCRETE VISUAL ACTION & SUBJECT: Hành động cụ thể có thể nhìn thấy
        if (!string.IsNullOrWhiteSpace(spec.Subject) || !string.IsNullOrWhiteSpace(spec.Action))
        {
            var subjectStr = string.IsNullOrWhiteSpace(spec.Subject) ? "The subject" : spec.Subject;
            var actionStr = string.IsNullOrWhiteSpace(spec.Action) ? "in a static scene" : spec.Action;
            sb.Append($"{subjectStr} {actionStr}. ");
        }

        // 5. EMOTION & EXPRESSION
        if (!string.IsNullOrWhiteSpace(spec.Emotion))
        {
            sb.Append($"Facial expression is {spec.Emotion}. ");
        }

        // 6. CAMERA FRAMING & ANGLE
        if (!string.IsNullOrWhiteSpace(spec.Framing) && spec.Framing != "eye_level")
        {
            sb.Append($"Shot with {spec.Framing.Replace('_', ' ')} angle. ");
        }

        // 7. LIGHTING, TIME OF DAY & WEATHER
        var lightingParts = new List<string>();
        if (!string.IsNullOrWhiteSpace(spec.TimeOfDay)) lightingParts.Add(spec.TimeOfDay.Replace('_', ' '));
        if (!string.IsNullOrWhiteSpace(spec.Weather) && spec.Weather != "clear") lightingParts.Add($"{spec.Weather} weather");
        if (!string.IsNullOrWhiteSpace(spec.Lighting)) lightingParts.Add($"{spec.Lighting.Replace('_', ' ')} lighting");
        if (lightingParts.Count > 0)
        {
            sb.Append($"Atmosphere: {string.Join(", ", lightingParts)}. ");
        }

        // 8. COLOR PALETTE
        if (!string.IsNullOrWhiteSpace(spec.ColorPalette))
        {
            sb.Append($"Color palette accented with {spec.ColorPalette}. ");
        }

        // 9. KEY PROPS (Vật chứng / Đạo cụ)
        if (spec.KeyProps != null && spec.KeyProps.Count > 0)
        {
            sb.Append($"Visible key props: {string.Join(", ", spec.KeyProps)}. ");
        }

        // 10. NEGATIVE CONSTRAINTS (Loại trừ chi tiết viễn tưởng hoặc sai lệch)
        if (spec.NegativeConstraints != null && spec.NegativeConstraints.Count > 0)
        {
            sb.Append($"Strictly avoid: {string.Join(", ", spec.NegativeConstraints)}. ");
        }

        // 11. ASPECT RATIO & QUALITY CLASSIFIER
        sb.Append($"High detail, cinematic masterpiece, {aspectRatio} aspect ratio.");

        return sb.ToString().Trim();
    }

    /// <summary>
    /// Tạo VisualSpec tự động theo luật (Rule-Based Fallback) từ lời thoại và shot type
    /// khi không có kết nối mạng hoặc khi chạy ở chế độ Offline.
    /// </summary>
    public static VisualSpec CreateFallbackVisualSpec(Scene scene, Shot shot)
    {
        var spec = new VisualSpec
        {
            Subject = !string.IsNullOrWhiteSpace(scene.CharactersPresentText) ? scene.CharactersPresentText : "The scene setting",
            Action = $"illustrating '{scene.NarrationText}'",
            Environment = "realistic modern investigative crime scene",
            TimeOfDay = "night",
            Lighting = "dramatic_shadows",
            Framing = shot.ShotType switch
            {
                "wide_shot" or "extreme_wide" => "wide_establishing_shot",
                "close_up" or "extreme_close_up" => "close_up_focus",
                _ => "eye_level"
            }
        };

        spec.NegativeConstraints.Add("no fantasy creatures");
        spec.NegativeConstraints.Add("no futuristic sci-fi");
        spec.NegativeConstraints.Add("no text watermark");

        return spec;
    }
}
