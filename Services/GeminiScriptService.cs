using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using VideoAutoWpf.Models;

namespace VideoAutoWpf.Services;

public class GeminiScriptService
{
    private readonly GoogleAuthService _authService;
    private static readonly HttpClient _httpClient = new();

    public static readonly Dictionary<string, string> StylePrompts = new()
    {
        ["dark-anime"] = "In a 2D dark anime cartoon style, hand-drawn 2D animation style, flat cel shading, classic Japanese anime line art, eerie abandoned atmosphere, high contrast, cinematic moody lighting, no photorealism, no 3D elements, no realistic textures. Colors strictly unified: deep sepia, swamp green, and pale ghostly teal. Clean line art, portrait aspect ratio 9:16. ",
        ["cinematic-horror"] = "In a cinematic dark fantasy movie style, highly realistic 3D photography, photorealistic textures, cinematic shot, creepy and chilling atmosphere, dramatic shadows, volumetric fog, realistic skin and hair, no cartoon, no 2D anime illustration. Colors unified: charcoal black, dark ash, and eerie glowing violet. Portrait aspect ratio 9:16. ",
        ["cartoon-3d"] = "In a cute 3D cartoon style, Pixar and Disney animated film render style, vibrant colors, soft volumetric lighting, glossy surfaces, round friendly character designs, highly detailed 3D digital art, cheerful atmosphere, no 2D drawings, no photorealism. Portrait aspect ratio 9:16. ",
        ["cyberpunk"] = "In a futuristic 2D cyberpunk anime style, 2D digital anime art, vibrant neon light highlights, flat cel shading, clean line art, dark shadows, high contrast, cinematic lighting, no realistic skin texture, no 3D render quality. Colors unified: dark indigo, bright magenta, and neon cyan. Clean line art, portrait aspect ratio 9:16. ",
        ["horror-cartoon"] = "In an eerie dark 2D cartoon style, Tim Burton aesthetic, creepy hand-drawn illustration style, sketchy lines, whimsical but macabre atmosphere, high contrast, dramatic shadows, no photorealism, no 3D elements, no realistic textures. Colors strictly unified: charcoal black, venom green, deep violet, and dark red. Clean line art, portrait aspect ratio 9:16. ",
        ["oil-painting"] = "In a classical textured oil painting style, expressive thick paint brushstrokes, texture of paint on canvas, chiaroscuro lighting, dramatic shadow and light contrast, historical and moody atmosphere, no digital animation style, no photorealism. Colors unified: deep brown, burnt orange, and pale warm yellow. Portrait aspect ratio 9:16. ",
        ["watercolor"] = "In a beautiful fluid watercolor painting style, wet-on-wet technique, soft pastel washes, bleeding paint edges, subtle ink sketch outline details, artistic and dreamy atmosphere, light and airy lighting, no photorealism, no digital animation style. Portrait aspect ratio 9:16. ",
        ["cartoon-2d"] = "In a classic 2D cartoon style, retro flat illustration, solid colors, clean black line art, vintage cartoon vibes, nostalgic simple shading, no gradients, no photorealism, no 3D elements. Portrait aspect ratio 9:16. "
    };

    public GeminiScriptService(GoogleAuthService authService)
    {
        _authService = authService;
    }

    public async Task<ScriptWorkspace> GenerateScriptAsync(
        string topic,
        string styleKey = "dark-anime",
        int numScenes = 5,
        string characterRules = "",
        string location = "us-central1",
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(topic))
            throw new ArgumentException("Chủ đề hoặc nội dung kịch bản không được để trống.", nameof(topic));

        var token = await _authService.GetAccessTokenAsync();
        var projectId = _authService.ProjectId;

        var chosenStyle = StylePrompts.TryGetValue(styleKey, out var styleVal) ? styleVal : StylePrompts["dark-anime"];

        var systemInstruction = 
            "You are an expert creative writer and short video director. " +
            "Your task is to take a video topic or raw content description and generate a complete structured video script in JSON format. " +
            "You MUST return ONLY the raw JSON block without markdown formatting or code block wrappers. " +
            "The JSON structure must exactly match this schema:\n" +
            "{\n" +
            "  \"metadata\": {\n" +
            "    \"ratio\": \"9:16\",\n" +
            "    \"voice\": \"vi-VN-Wavenet-B\",\n" +
            "    \"music\": \"\",\n" +
            "    \"music_volume\": 0.15\n" +
            "  },\n" +
            "  \"scenes\": [\n" +
            "    { \"text\": \"Vietnamese voiceover text...\", \"image_prompt\": \"Detailed English Imagen 3 image generation prompt...\" }\n" +
            "  ]\n" +
            "}\n\n" +
            "Guidelines:\n" +
            "1. The video ratio is 9:16 (vertical). Voice is 'vi-VN-Wavenet-B'.\n" +
            $"2. Generate exactly {numScenes} scenes.\n" +
            "3. The 'text' must be natural, engaging, and compelling Vietnamese voiceover. Start with a strong hook in Scene 1.\n" +
            $"4. The 'image_prompt' must be a detailed, highly descriptive prompt in English. You MUST prepend this exact style prefix to the beginning of EVERY single 'image_prompt': '{chosenStyle}'\n" +
            "5. To maintain character consistency across scenes, repeat the exact detailed description of the character(s) verbatim in every scene's image prompt where they appear.\n";

        var userPrompt = $"Topic/Raw content to transform into a {numScenes}-scene video script:\n'{topic}'\n\n";
        if (!string.IsNullOrWhiteSpace(characterRules))
        {
            userPrompt += $"CRITICAL CHARACTER & COLOR SYNC RULES:\n{characterRules}\nEnsure every scene featuring the characters strictly includes these identical features and colors in English.\n\n";
        }
        userPrompt += $"Please generate the complete JSON script with exactly {numScenes} scenes.";

        // Thử model gemini-2.5-flash trước, nếu lỗi thử gemini-1.5-flash
        var modelsToTry = new[] { "gemini-2.5-flash", "gemini-1.5-flash" };
        string? lastError = null;

        foreach (var modelName in modelsToTry)
        {
            var endpoint = $"https://{location}-aiplatform.googleapis.com/v1/projects/{projectId}/locations/{location}/publishers/google/models/{modelName}:generateContent";

            var requestBody = new
            {
                system_instruction = new
                {
                    parts = new[] { new { text = systemInstruction } }
                },
                contents = new[]
                {
                    new
                    {
                        role = "user",
                        parts = new[] { new { text = userPrompt } }
                    }
                },
                generation_config = new
                {
                    response_mime_type = "application/json"
                }
            };

            var jsonContent = JsonSerializer.Serialize(requestBody);
            using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            request.Content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            try
            {
                using var response = await _httpClient.SendAsync(request, ct);
                var responseStr = await response.Content.ReadAsStringAsync(ct);

                if (response.IsSuccessStatusCode)
                {
                    using var doc = JsonDocument.Parse(responseStr);
                    var candidates = doc.RootElement.GetProperty("candidates");
                    if (candidates.GetArrayLength() > 0)
                    {
                        var textJson = candidates[0]
                            .GetProperty("content")
                            .GetProperty("parts")[0]
                            .GetProperty("text")
                            .GetString();

                        if (!string.IsNullOrEmpty(textJson))
                        {
                            var cleanJson = textJson.Trim();
                            if (cleanJson.StartsWith("```json")) cleanJson = cleanJson[7..];
                            if (cleanJson.StartsWith("```")) cleanJson = cleanJson[3..];
                            if (cleanJson.EndsWith("```")) cleanJson = cleanJson[..^3];
                            cleanJson = cleanJson.Trim();

                            var workspace = JsonSerializer.Deserialize<ScriptWorkspace>(cleanJson, new JsonSerializerOptions
                            {
                                PropertyNameCaseInsensitive = true
                            });

                            if (workspace != null && workspace.Scenes != null && workspace.Scenes.Count > 0)
                            {
                                return workspace;
                            }
                        }
                    }
                }
                else
                {
                    lastError = $"Model {modelName} returned {response.StatusCode}: {responseStr}";
                }
            }
            catch (Exception ex)
            {
                lastError = ex.Message;
            }
        }

        throw new Exception($"Không thể sinh kịch bản bằng Gemini AI: {lastError}");
    }
}
