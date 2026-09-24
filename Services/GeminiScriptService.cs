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
        ["cartoon-2d"] = "In a classic 2D cartoon style, retro flat illustration, solid colors, clean black line art, vintage cartoon vibes, nostalgic simple shading, no gradients, no photorealism, no 3D elements. Portrait aspect ratio 9:16. ",
        ["realistic-photo"] = "In a hyper-realistic photographic style, DSLR quality, shallow depth of field, natural lighting, professional cinematic photography, highly detailed, lifelike textures, 8K resolution quality. Portrait aspect ratio 9:16. ",
        ["pixel-art"] = "In a retro pixel art style, 16-bit game aesthetic, limited color palette, clean pixel placement, nostalgic retro gaming vibes, no anti-aliasing, sharp pixel edges. Portrait aspect ratio 9:16. "
    };

    /// <summary>
    /// Danh sách style keys có sẵn, dùng cho AI chọn
    /// </summary>
    public static readonly string AvailableStyleKeys = string.Join(", ", StylePrompts.Keys);

    public GeminiScriptService(GoogleAuthService authService)
    {
        _authService = authService;
    }

    /// <summary>
    /// Bước 1: AI lên kế hoạch (plan) cho video dựa trên chủ đề
    /// </summary>
    public async Task<ScriptPlan> GeneratePlanAsync(
        string topic,
        string location = "us-central1",
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(topic))
            throw new ArgumentException("Chủ đề không được để trống.", nameof(topic));

        var token = await _authService.GetAccessTokenAsync();
        var projectId = _authService.ProjectId;

        var systemInstruction =
            "You are an expert creative video planning consultant. " +
            "Given a video topic or content description, analyze the topic and suggest the BEST creative plan for making a short video. " +
            "You MUST return ONLY the raw JSON block without markdown formatting or code block wrappers. " +
            "The JSON structure must exactly match this schema:\n" +
            "{\n" +
            "  \"suggested_scenes\": 5,\n" +
            "  \"suggested_style\": \"dark-anime\",\n" +
            "  \"style_description\": \"Mô tả ngắn gọn bằng tiếng Việt về phong cách được chọn\",\n" +
            "  \"tone\": \"Tone giọng đọc phù hợp (Rùng rợn / Vui nhộn / Trang nghiêm / Nhẹ nhàng / Hào hùng / Bí ẩn / Cảm xúc...)\",\n" +
            "  \"ratio\": \"9:16\",\n" +
            "  \"voice\": \"vi-VN-Wavenet-B\",\n" +
            "  \"motion_effect\": \"auto\",\n" +
            "  \"enable_fade\": true,\n" +
            "  \"enable_vignette\": false,\n" +
            "  \"suggested_bgm\": \"dramatic\",\n" +
            "  \"enable_subtitles\": true,\n" +
            "  \"subtitle_style\": \"cinematic\",\n" +
            "  \"subtitle_font\": \"Segoe UI Bold\",\n" +
            "  \"subtitle_reason\": \"Lý do ngắn gọn bằng tiếng Việt chọn kiểu chữ chạy/phụ đề này\",\n" +
            "  \"summary\": \"Tóm tắt kế hoạch video bằng tiếng Việt (3-5 câu): nội dung chính, cách kể chuyện, điểm nhấn\",\n" +
            "  \"character_hint\": \"Mô tả gợi ý nhân vật chính (ngoại hình, trang phục, đặc điểm) bằng tiếng Việt\"\n" +
            "}\n\n" +
            "Rules:\n" +
            "1. 'suggested_scenes' should be between 3 and 12, based on complexity and depth needed for the topic.\n" +
            $"2. 'suggested_style' MUST be one of these exact keys: [{AvailableStyleKeys}]. Choose the BEST style that matches the topic's mood and theme.\n" +
            "3. 'voice' must be one of: 'vi-VN-Wavenet-B' (nam trầm), 'vi-VN-Wavenet-A' (nữ), 'vi-VN-Wavenet-D' (nam), 'vi-VN-Neural2-A' (nữ cao cấp), 'vi-VN-Neural2-D' (nam cao cấp). Choose based on tone.\n" +
            "4. 'ratio' is always '9:16' for short video.\n" +
            "5. 'motion_effect' default is 'auto' (AI analyzes and assigns the best camera movement to each individual scene).\n" +
            "6. 'enable_vignette': set to true for dark fantasy, horror, noir, historical drama; false for bright anime, cartoon, commercial.\n" +
            "7. 'enable_fade': always true for smooth transitions.\n" +
            "8. 'suggested_bgm': must be one of ['dramatic', 'chill', 'epic', 'horror', 'upbeat']. Choose according to the emotional tone of the video.\n" +
            "9. 'subtitle_style': MUST be one of ['cinematic', 'boxed', 'viral_yellow', 'neon', 'gold', 'ticker']. Select the style that best enhances the video's mood, pacing, and platform appeal (e.g. 'viral_yellow' or 'boxed' for TikTok/Shorts hooks, 'cinematic' for documentaries/drama, 'neon' for cyberpunk/tech, 'gold' for luxury/history, 'ticker' for news crawl).\n" +
            "10. 'subtitle_font': choose from ['Segoe UI Bold', 'Arial Bold', 'Impact', 'Tahoma Bold', 'Consolas', 'Times New Roman'].\n" +
            "11. 'subtitle_reason': Giải thích ngắn gọn bằng tiếng Việt lý do đề xuất kiểu phụ đề này.\n" +
            "12. All Vietnamese text must be natural and compelling.\n" +
            "13. 'character_hint': Mô tả chi tiết nhân vật chính (nếu có) bao gồm: giới tính, tuổi, kiểu tóc, trang phục, đặc điểm nổi bật. Nếu không có nhân vật cụ thể, để trống.\n";

        var userPrompt = $"Hãy lên kế hoạch chi tiết cho video về chủ đề sau:\n'{topic}'\n\n" +
                         "Phân tích chủ đề và đề xuất số cảnh, phong cách hình ảnh, tone giọng đọc, hiệu ứng camera phù hợp nhất.";

        return await CallGeminiForJson<ScriptPlan>(systemInstruction, userPrompt, projectId, location, token, ct);
    }

    /// <summary>
    /// Bước 2: Sinh kịch bản chi tiết dựa trên plan đã duyệt
    /// </summary>
    public async Task<ScriptWorkspace> GenerateScriptAsync(
        string topic,
        ScriptPlan plan,
        string characterRules = "",
        string location = "us-central1",
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(topic))
            throw new ArgumentException("Chủ đề hoặc nội dung kịch bản không được để trống.", nameof(topic));

        var token = await _authService.GetAccessTokenAsync();
        var projectId = _authService.ProjectId;

        var chosenStyle = StylePrompts.TryGetValue(plan.SuggestedStyle, out var styleVal) ? styleVal : StylePrompts["dark-anime"];
        var numScenes = plan.SuggestedScenes > 0 ? plan.SuggestedScenes : 5;

        var systemInstruction =
            "You are an expert creative writer and short video director. " +
            "Your task is to take a video topic or raw content description and generate a complete structured video script in JSON format. " +
            "You MUST return ONLY the raw JSON block without markdown formatting or code block wrappers. " +
            "The JSON structure must exactly match this schema:\n" +
            "{\n" +
            "  \"metadata\": {\n" +
            $"    \"ratio\": \"{plan.Ratio}\",\n" +
            $"    \"voice\": \"{plan.Voice}\",\n" +
            $"    \"enable_fade\": {plan.EnableFadeTransition.ToString().ToLower()},\n" +
            $"    \"enable_vignette\": {plan.EnableVignette.ToString().ToLower()},\n" +
            $"    \"music\": \"{plan.SuggestedBgm}\",\n" +
            "    \"enable_music\": true,\n" +
            "    \"music_volume\": 0.15,\n" +
            $"    \"enable_subtitles\": {plan.EnableSubtitles.ToString().ToLower()},\n" +
            $"    \"subtitle_style\": \"{plan.SubtitleStyle}\",\n" +
            $"    \"subtitle_font\": \"{plan.SubtitleFont}\"\n" +
            "  },\n" +
            "  \"publish_info\": {\n" +
            "    \"title\": \"Tiêu đề video cực giật gân, cuốn hút cho TikTok/Shorts/Reels (dưới 80 ký tự)\",\n" +
            "    \"alternative_titles\": \"- Gợi ý tiêu đề 2 (tò mò)\\n- Gợi ý tiêu đề 3 (kịch tính)\",\n" +
            "    \"description\": \"Đoạn mô tả ngắn 2-3 câu khơi gợi sự tò mò của video, kèm kêu gọi hành động\",\n" +
            "    \"hashtags\": \"#chude #bian #kienthuc #xuhuong #fyp #shorts #tiktok #viral\"\n" +
            "  },\n" +
            "  \"scenes\": [\n" +
            "    { \"text\": \"Vietnamese voiceover text...\", \"image_prompt\": \"Detailed English Imagen 3 image generation prompt...\", \"motion_effect\": \"zoom_in\" }\n" +
            "  ]\n" +
            "}\n\n" +
            "Guidelines:\n" +
            $"1. The video ratio is {plan.Ratio} (vertical). Voice is '{plan.Voice}'.\n" +
            $"2. Generate exactly {numScenes} scenes.\n" +
            $"3. The tone of voice should be: {plan.Tone}.\n" +
            "4. The 'text' must be natural, engaging, and compelling Vietnamese voiceover. Start with a strong hook in Scene 1.\n" +
            $"5. The 'image_prompt' must be a detailed, highly descriptive prompt in English. You MUST prepend this exact style prefix to the beginning of EVERY single 'image_prompt': '{chosenStyle}'\n" +
            "6. To maintain character consistency across scenes, repeat the exact detailed description of the character(s) verbatim in every scene's image prompt where they appear.\n" +
            "7. 'motion_effect': For each scene, analyze its dramatic action, emotional beat, and visual composition. Assign the most fitting camera movement:\n" +
            "   - 'zoom_in': intense moments, emotional climax, dialogue, dramatic realization, focusing on face or clue\n" +
            "   - 'zoom_out': establishing shots, revealing expansive world/landscape, pulling back to show full scene\n" +
            "   - 'pan_left_right': character walking/moving across, tracking action, sweeping landscape\n" +
            "   - 'pan_right_left': alternating horizontal sweep\n" +
            "   - 'pan_up': revealing character outfit to face, tall structures, trees, sky (ideal for vertical 9:16 format)\n" +
            "   - 'pan_down': looking down from above, descending, gravity, shock\n" +
            "   - 'zoom_pan_right': dynamic corner focus, building tension\n" +
            "   - 'zoom_pan_left': dynamic left focus\n" +
            "8. 'publish_info': Provide an outstanding social media publishing kit for TikTok, YouTube Shorts, and Reels in Vietnamese: catchiest hook 'title', 2 'alternative_titles', engaging 'description' with call-to-action, and 10-15 trending 'hashtags'.\n";

        var userPrompt = $"Topic/Raw content to transform into a {numScenes}-scene video script:\n'{topic}'\n\n";

        // Thêm character rules từ plan hint + user rules
        var allCharacterRules = "";
        if (!string.IsNullOrWhiteSpace(plan.CharacterHint))
            allCharacterRules += plan.CharacterHint + "\n";
        if (!string.IsNullOrWhiteSpace(characterRules))
            allCharacterRules += characterRules;

        if (!string.IsNullOrWhiteSpace(allCharacterRules))
        {
            userPrompt += $"CRITICAL CHARACTER & COLOR SYNC RULES:\n{allCharacterRules.Trim()}\nEnsure every scene featuring the characters strictly includes these identical features and colors in English.\n\n";
        }
        userPrompt += $"Please generate the complete JSON script with exactly {numScenes} scenes.";

        return await CallGeminiForJson<ScriptWorkspace>(systemInstruction, userPrompt, projectId, location, token, ct);
    }

    /// <summary>
    /// Sinh hoặc tạo lại bộ Tiêu đề, Mô tả, Hashtags đăng mạng xã hội (TikTok, Shorts, Reels)
    /// </summary>
    public async Task<VideoPublishInfo> GeneratePublishInfoAsync(
        string topicOrSummary,
        string scenesContent,
        string location = "us-central1",
        CancellationToken ct = default)
    {
        var token = await _authService.GetAccessTokenAsync();
        var projectId = _authService.ProjectId;

        var systemInstruction =
            "You are a top-tier viral content marketing expert and social media strategist specializing in TikTok, YouTube Shorts, and Facebook/Instagram Reels. " +
            "Your job is to analyze the video's topic and scene scripts, and produce a viral, high-converting social media publishing kit in Vietnamese. " +
            "You MUST return ONLY the raw JSON block without markdown formatting or code block wrappers. " +
            "The JSON schema must exactly match:\n" +
            "{\n" +
            "  \"title\": \"Tiêu đề giật gân, siêu cuốn hút (dưới 80 ký tự), có thể kèm emoji gây tò mò\",\n" +
            "  \"alternative_titles\": \"- Tiêu đề thay thế 1 (Dạng câu hỏi bí ẩn)\\n- Tiêu đề thay thế 2 (Dạng giật gân A/B test)\",\n" +
            "  \"description\": \"Mô tả ngắn gọn (2-3 câu) tóm tắt kịch tính nội dung video, kèm lời kêu gọi hành động (CTA) kích thích bình luận\",\n" +
            "  \"hashtags\": \"#chude #bian #kienthuc #xuhuong #fyp #shorts #tiktok #viral (khoảng 10-15 thẻ hashtag có dấu và không dấu cách nhau bằng khoảng trắng)\"\n" +
            "}";

        var userPrompt = $"Nội dung chủ đề video:\n{topicOrSummary}\n\n" +
                         $"Các phân cảnh / lời thoại trong video:\n{scenesContent}\n\n" +
                         "Hãy tạo bộ tiêu đề, mô tả và hashtags bùng nổ tương tác cho video này.";

        return await CallGeminiForJson<VideoPublishInfo>(systemInstruction, userPrompt, projectId, location, token, ct);
    }

    /// <summary>
    /// Overload cũ để backward-compatible với code gọi trực tiếp (nếu cần)
    /// </summary>
    public async Task<ScriptWorkspace> GenerateScriptAsync(
        string topic,
        string styleKey = "dark-anime",
        int numScenes = 5,
        string characterRules = "",
        string location = "us-central1",
        CancellationToken ct = default)
    {
        var plan = new ScriptPlan
        {
            SuggestedScenes = numScenes,
            SuggestedStyle = styleKey,
            Tone = "Phù hợp với nội dung",
            Ratio = "9:16",
            Voice = "vi-VN-Wavenet-B"
        };
        return await GenerateScriptAsync(topic, plan, characterRules, location, ct);
    }

    /// <summary>
    /// Generic helper gọi Gemini API và parse JSON response
    /// </summary>
    private async Task<T> CallGeminiForJson<T>(
        string systemInstruction,
        string userPrompt,
        string projectId,
        string location,
        string token,
        CancellationToken ct) where T : class
    {
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

                            var result = JsonSerializer.Deserialize<T>(cleanJson, new JsonSerializerOptions
                            {
                                PropertyNameCaseInsensitive = true
                            });

                            if (result != null)
                            {
                                return result;
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

        throw new Exception($"Không thể gọi Gemini AI: {lastError}");
    }
}
