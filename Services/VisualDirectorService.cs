using System.Text.Json;
using VideoAutoWpf.Models;
using VideoAutoWpf.Models.V2;

namespace VideoAutoWpf.Services;

/// <summary>
/// Dịch vụ Đạo diễn hình ảnh (Visual Director Service).
/// Chuyển đổi nội dung lời thoại (Narration) thành đặc tả hình ảnh (VisualSpec) có thể nhìn thấy cụ thể.
/// Triệt tiêu hoàn toàn các hình ảnh ẩn dụ viển vông, ưu tiên kể chuyện thị giác cụ thể (Concrete Storytelling).
/// </summary>
public class VisualDirectorService
{
    private readonly GeminiScriptService _geminiService;

    public VisualDirectorService(GeminiScriptService geminiService)
    {
        _geminiService = geminiService;
    }

    /// <summary>
    /// Đạo diễn cho một Shot cụ thể: Sinh VisualSpec và biên dịch câu prompt hoàn chỉnh.
    /// </summary>
    public async Task<VisualSpec> DirectShotAsync(
        Scene scene,
        Shot shot,
        IEnumerable<CharacterProfile>? characters = null,
        LocationProfile? location = null,
        string styleKey = "dark-anime",
        string aspectRatio = "9:16",
        CancellationToken ct = default)
    {
        VisualSpec spec;

        try
        {
            spec = await CallAiForVisualSpecAsync(scene, shot, characters, location, ct);
        }
        catch
        {
            // Fallback an toàn nếu AI lỗi kết nối hoặc parse JSON thất bại
            spec = PromptBuilder.CreateFallbackVisualSpec(scene, shot);
        }

        shot.VisualSpec = spec;
        shot.CompiledImagePrompt = PromptBuilder.BuildPrompt(
            spec,
            styleKey,
            characters?.Where(c => scene.CharacterIdsPresent.Contains(c.Id)),
            location,
            aspectRatio);

        return spec;
    }

    /// <summary>
    /// Đạo diễn toàn bộ các Shot trong một phân cảnh (Scene).
    /// </summary>
    public async Task DirectSceneShotsAsync(
        Scene scene,
        IEnumerable<CharacterProfile>? characters = null,
        LocationProfile? location = null,
        string styleKey = "dark-anime",
        string aspectRatio = "9:16",
        CancellationToken ct = default)
    {
        if (scene?.Shots == null || scene.Shots.Count == 0) return;

        foreach (var shot in scene.Shots)
        {
            ct.ThrowIfCancellationRequested();
            await DirectShotAsync(scene, shot, characters, location, styleKey, aspectRatio, ct);
        }
    }

    /// <summary>
    /// Gọi Gemini AI bóc tách VisualSpec từ lời thoại và góc quay.
    /// </summary>
    private async Task<VisualSpec> CallAiForVisualSpecAsync(
        Scene scene,
        Shot shot,
        IEnumerable<CharacterProfile>? characters,
        LocationProfile? location,
        CancellationToken ct)
    {
        var systemInstruction =
            "You are a master cinematic film director. " +
            "Your job is to translate a specific piece of voiceover narration into a CONCRETE, VISUALLY OBSERVABLE shot specification (VisualSpec) in JSON format. " +
            "RULES:\n" +
            "1. NO METAPHORS or abstract concepts. Describe ONLY what a physical movie camera can record.\n" +
            "2. Subject must be a physical person, vehicle, clue prop, or building.\n" +
            "3. Action must be a clear physical posture or motion (e.g., 'examining shattered glass on asphalt', NOT 'feeling deep sorrow').\n" +
            "4. Return ONLY valid JSON matching this schema:\n" +
            "{\n" +
            "  \"subject\": \"Short description of central subject\",\n" +
            "  \"action\": \"Concrete physical action\",\n" +
            "  \"emotion\": \"Facial expression / emotional nuance\",\n" +
            "  \"environment\": \"Immediate physical background setting\",\n" +
            "  \"time_of_day\": \"night / sunset / day / dawn\",\n" +
            "  \"weather\": \"clear / rainy / foggy / overcast\",\n" +
            "  \"camera_framing\": \"eye_level / low_angle / high_angle / dutch_angle\",\n" +
            "  \"lighting\": \"dramatic_shadows / volumetric_fog / neon_glow / chiaroscuro\",\n" +
            "  \"color_palette\": \"specific color accent\",\n" +
            "  \"key_props\": [\"visible clue 1\", \"visible clue 2\"],\n" +
            "  \"negative_constraints\": [\"no fantasy\", \"no distortion\"]\n" +
            "}";

        var userPrompt =
            $"Scene Narration: \"{scene.NarrationText}\"\n" +
            $"Scene Purpose: {scene.Purpose}\n" +
            $"Shot Code: {shot.ShotCode} (Shot Type: {shot.ShotType}, Duration: {shot.DurationSeconds:F1}s)\n" +
            (location != null ? $"Location: {location.Name} ({location.ArchitecturalStyle})\n" : "") +
            (characters != null && characters.Any() ? $"Characters Present: {string.Join(", ", characters.Select(c => c.Name))}\n" : "") +
            "Please provide the concrete VisualSpec JSON for this exact camera shot.";

        var rawJson = await _geminiService.CallGeminiForRawText(
            systemInstruction,
            userPrompt,
            _geminiService.ProjectId,
            "us-central1",
            string.Empty,
            ct);

        if (string.IsNullOrWhiteSpace(rawJson))
            throw new Exception("Empty AI response");

        var clean = rawJson.Trim();
        if (clean.StartsWith("```json")) clean = clean[7..];
        if (clean.StartsWith("```")) clean = clean[3..];
        if (clean.EndsWith("```")) clean = clean[..^3];
        clean = clean.Trim();

        var result = JsonSerializer.Deserialize<VisualSpec>(clean, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        return result ?? throw new Exception("Failed to deserialize VisualSpec");
    }
}
