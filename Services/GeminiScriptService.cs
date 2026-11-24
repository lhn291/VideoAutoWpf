using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using VideoAutoWpf.Models;
using VideoAutoWpf.Models.Minimal;

namespace VideoAutoWpf.Services;

public class GeminiScriptService
{
    private readonly GoogleAuthService _authService;
    private static readonly HttpClient _httpClient = new();

    public static readonly Dictionary<string, string> StylePrompts = new()
    {
        ["realistic-photo"] = "In a hyper-realistic photographic style, DSLR quality, natural cinematic lighting, highly detailed textures, 8K resolution, photorealistic. ",
        ["dark-anime"] = "In a modern dark anime comic illustration style, high-end 2D graphic novel art, sharp line art, dramatic cel shading, moody lighting, deep shadows. ",
        ["cinematic-horror"] = "In a cinematic crime thriller movie style, atmospheric cinematic film still, moody and suspenseful shadows, volumetric lighting, dramatic tension. ",
        ["cartoon-3d"] = "In a cute 3D cartoon style, Pixar and Disney animated film render style, vibrant colors, soft volumetric lighting, friendly character designs. ",
        ["cyberpunk"] = "In a futuristic cyberpunk style, vibrant neon lights, dark shadows, high contrast, cinematic moody lighting, neon cyan and magenta accents. ",
        ["oil-painting"] = "In a classical textured oil painting style, expressive thick paint brushstrokes, texture of paint on canvas, chiaroscuro lighting, dramatic atmosphere. ",
        ["watercolor"] = "In a beautiful fluid watercolor painting style, wet-on-wet technique, soft pastel washes, bleeding paint edges, subtle ink sketch outline details. ",
        ["pixel-art"] = "In a retro pixel art style, 16-bit game aesthetic, limited color palette, clean pixel placement, sharp pixel edges. "
    };

    private string? _apiKey;

    /// <summary>
    /// Gemini API Key từ Google AI Studio (miễn phí, không cần billing / credit card)
    /// </summary>
    public string? ApiKey
    {
        get => _apiKey ??= LoadApiKey();
        set => _apiKey = value;
    }

    public static string? LoadApiKey()
    {
        var env = Environment.GetEnvironmentVariable("GEMINI_API_KEY");
        if (!string.IsNullOrWhiteSpace(env)) return env.Trim();

        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        var paths = new[]
        {
            Path.Combine(baseDir, "Config", "gemini_api_key.txt"),
            Path.Combine(baseDir, "..", "..", "..", "Config", "gemini_api_key.txt"),
            Path.Combine(baseDir, "gemini_api_key.txt"),
            Path.Combine(baseDir, "..", "..", "..", "gemini_api_key.txt")
        };

        foreach (var p in paths)
        {
            if (File.Exists(p))
            {
                var content = File.ReadAllText(p).Trim();
                if (!string.IsNullOrWhiteSpace(content)) return content;
            }
        }

        return null;
    }

    public static void SaveApiKey(string key)
    {
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        var configDir = Path.Combine(baseDir, "Config");
        Directory.CreateDirectory(configDir);
        File.WriteAllText(Path.Combine(configDir, "gemini_api_key.txt"), key.Trim());

        try
        {
            var srcConfigDir = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "Config"));
            if (Directory.Exists(srcConfigDir))
            {
                File.WriteAllText(Path.Combine(srcConfigDir, "gemini_api_key.txt"), key.Trim());
            }
        }
        catch { }
    }

    public GeminiScriptService(GoogleAuthService authService)
    {
        _authService = authService;
    }

    public string ProjectId => _authService.ProjectId ?? string.Empty;

    private static readonly JsonSerializerOptions _geminiJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
        Converters = { new JsonStringEnumConverter() }
    };

    /// <summary>
    /// Trích xuất khối JSON sạch từ phản hồi của AI (loại bỏ markdown, text hội thoại bên ngoài)
    /// </summary>
    public static string CleanAndExtractJson(string rawText)
    {
        if (string.IsNullOrWhiteSpace(rawText)) return string.Empty;
        var text = rawText.Trim();

        var match = System.Text.RegularExpressions.Regex.Match(text, @"```(?:json)?\s*([\s\S]*?)\s*```", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (match.Success)
        {
            text = match.Groups[1].Value.Trim();
        }

        int firstBrace = text.IndexOf('{');
        int firstBracket = text.IndexOf('[');

        int start = -1;
        int end = -1;

        if (firstBrace >= 0 && (firstBracket < 0 || firstBrace < firstBracket))
        {
            start = firstBrace;
            end = text.LastIndexOf('}');
        }
        else if (firstBracket >= 0)
        {
            start = firstBracket;
            end = text.LastIndexOf(']');
        }

        if (start >= 0 && end > start)
        {
            text = text.Substring(start, end - start + 1);
        }

        return text.Trim();
    }

    private async Task<(string token, string projectId)> ResolveAuthCredentialsAsync()
    {
        var projectId = _authService.ProjectId;
        if (!string.IsNullOrWhiteSpace(ApiKey))
        {
            return (string.Empty, projectId);
        }
        var token = await _authService.GetAccessTokenAsync();
        return (token, projectId);
    }

    /// <summary>
    /// BƯỚC 2: AI Lập dàn ý video từ chủ đề (Minimal Workflow)
    /// </summary>
    public async Task<VideoOutline> GenerateMinimalOutlineAsync(
        string topic,
        string style = "realistic-photo",
        string tone = "Kịch tính, cuốn hút",
        string location = "us-central1",
        CancellationToken ct = default,
        Action<string>? onStatus = null)
    {
        if (string.IsNullOrWhiteSpace(topic))
            throw new ArgumentException("Chủ đề không được để trống.", nameof(topic));

        var (token, projectId) = await ResolveAuthCredentialsAsync();

        var systemInstruction =
            "Bạn là một chuyên gia sáng tạo nội dung video ngắn chuyên nghiệp (Shorts / Reels / TikTok). " +
            "Nhiệm vụ: Phân tích chủ đề và lập dàn ý video ngắn hấp dẫn, có hook mở màn, phát triển mạch lạc và kết thúc ấn tượng. " +
            "BẮT BUỘC trả về duy nhất một khối JSON thô, không kèm markdown backticks, theo schema:\n" +
            "{\n" +
            "  \"title\": \"Tiêu đề video cuốn hút\",\n" +
            "  \"summary\": \"Tóm tắt 1-2 câu về nội dung cốt lõi của video\",\n" +
            "  \"sections\": [\n" +
            "    { \"heading\": \"Tên phần (ví dụ: Hook mở màn, Diễn biến bí ẩn, Sự thật ngỡ ngàng)\", \"description\": \"Ý chính của phần này\", \"estimated_seconds\": 8 }\n" +
            "  ]\n" +
            "}";

        var userPrompt = $"Chủ đề video:\n{topic}\n\nTông giọng / Phong cách: {tone}\nPhong cách hình ảnh: {style}\nHãy tạo dàn ý gồm 4-6 phần rõ ràng cho video này.";

        onStatus?.Invoke("AI đang phân tích chủ đề và lập dàn ý...");
        return await CallGeminiForJson<VideoOutline>(systemInstruction, userPrompt, projectId, location, token, ct, onStatus);
    }

    /// <summary>
    /// BƯỚC 3: AI Viết kịch bản phân cảnh chi tiết dựa trên dàn ý đã xác nhận (Minimal Workflow)
    /// </summary>
    public async Task<VideoScript> GenerateMinimalScriptAsync(
        string topic,
        VideoOutline outline,
        string styleKey = "realistic-photo",
        string aspectRatio = "9:16",
        int numScenes = 5,
        string location = "us-central1",
        CancellationToken ct = default,
        Action<string>? onStatus = null)
    {
        if (outline == null || string.IsNullOrWhiteSpace(outline.Title))
            throw new ArgumentException("Dàn ý không hợp lệ.", nameof(outline));

        var (token, projectId) = await ResolveAuthCredentialsAsync();

        var chosenStyle = StylePrompts.TryGetValue(styleKey, out var promptPrefix)
            ? promptPrefix
            : StylePrompts["realistic-photo"];

        var sectionsText = new StringBuilder();
        if (outline.Sections != null)
        {
            foreach (var s in outline.Sections)
            {
                sectionsText.AppendLine($"- {s.Heading}: {s.Description} (~{s.EstimatedSeconds}s)");
            }
        }

        var systemInstruction =
            "Bạn là biên kịch video tài ba. Dựa vào dàn ý đã duyệt, hãy viết kịch bản phân cảnh chi tiết cho video.\n" +
            $"Tạo đúng {numScenes} phân cảnh (scenes). Mỗi phân cảnh tương ứng một câu thuyết minh và một bức ảnh minh họa.\n" +
            "Quy tắc vàng:\n" +
            "1. 'narration_text': Lời thoại tiếng Việt tự nhiên, lôi cuốn, độ dài từ 15 đến 30 từ mỗi cảnh. Không viết rời rạc.\n" +
            "2. 'visual_prompt': Mô tả hình ảnh chi tiết BẰNG TIẾNG ANH. BẮT BUỘC bắt đầu bằng tiền tố phong cách sau: '" + chosenStyle + "'. Mô tả đối tượng, bối cảnh, ánh sáng, góc máy cụ thể, khớp 100% với lời thoại.\n" +
            "3. 'duration_seconds': Ước tính từ 6 đến 12 giây.\n" +
            "BẮT BUỘC trả về duy nhất khối JSON theo schema:\n" +
            "{\n" +
            "  \"title\": \"Tiêu đề kịch bản\",\n" +
            "  \"scenes\": [\n" +
            "    {\n" +
            "      \"scene_number\": 1,\n" +
            "      \"narration_text\": \"Lời đọc tiếng Việt...\",\n" +
            "      \"visual_prompt\": \"Style prefix + Detailed visual description in English...\",\n" +
            "      \"duration_seconds\": 8.0\n" +
            "    }\n" +
            "  ]\n" +
            "}";

        var userPrompt = $"Chủ đề gốc:\n{topic}\n\nTiêu đề dàn ý: {outline.Title}\nTóm tắt: {outline.Summary}\nCác phần dàn ý:\n{sectionsText}\nTỷ lệ video: {aspectRatio}\nHãy viết kịch bản hoàn chỉnh gồm đúng {numScenes} cảnh.";

        onStatus?.Invoke("AI đang viết kịch bản chi tiết theo dàn ý...");
        return await CallGeminiForJson<VideoScript>(systemInstruction, userPrompt, projectId, location, token, ct, onStatus);
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
        CancellationToken ct = default,
        Action<string>? onStatus = null) where T : class
    {
        var apiKey = ApiKey;
        var modelsToTry = !string.IsNullOrWhiteSpace(apiKey)
            ? new[] { "gemini-3.5-flash-lite", "gemini-flash-lite-latest", "gemini-3.5-flash", "gemini-3.1-flash-lite", "gemini-3.8-flash" }
            : new[] { "gemini-3.5-flash-lite", "gemini-flash-lite-latest", "gemini-3.5-flash", "gemini-3.1-flash-lite", "gemini-3.8-flash" };
        string? lastError = null;

        foreach (var modelName in modelsToTry)
        {
            if (ct.IsCancellationRequested) break;
            onStatus?.Invoke($"Đang kết nối AI ({modelName})...");

            string endpoint;
            HttpRequestMessage request;

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
                generationConfig = new
                {
                    responseMimeType = "application/json",
                    maxOutputTokens = 8192,
                    temperature = 0.7
                }
            };

            var jsonContent = JsonSerializer.Serialize(requestBody);

            if (!string.IsNullOrWhiteSpace(apiKey))
            {
                endpoint = $"https://generativelanguage.googleapis.com/v1beta/models/{modelName}:generateContent?key={apiKey}";
                request = new HttpRequestMessage(HttpMethod.Post, endpoint);
                request.Content = new StringContent(jsonContent, Encoding.UTF8, "application/json");
            }
            else
            {
                endpoint = $"https://{location}-aiplatform.googleapis.com/v1/projects/{projectId}/locations/{location}/publishers/google/models/{modelName}:generateContent";
                request = new HttpRequestMessage(HttpMethod.Post, endpoint);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                request.Content = new StringContent(jsonContent, Encoding.UTF8, "application/json");
            }

            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                cts.CancelAfter(TimeSpan.FromSeconds(30));

                using var response = await _httpClient.SendAsync(request, cts.Token);
                var responseStr = await response.Content.ReadAsStringAsync(ct);

                if (response.IsSuccessStatusCode)
                {
                    using var doc = JsonDocument.Parse(responseStr);
                    if (doc.RootElement.TryGetProperty("candidates", out var candidates) && candidates.GetArrayLength() > 0)
                    {
                        var firstCandidate = candidates[0];
                        if (firstCandidate.TryGetProperty("content", out var content) &&
                            content.TryGetProperty("parts", out var parts) &&
                            parts.GetArrayLength() > 0)
                        {
                            string? textJson = null;
                            foreach (var part in parts.EnumerateArray())
                            {
                                if (part.TryGetProperty("text", out var tProp))
                                {
                                    var textCandidate = tProp.GetString();
                                    if (!string.IsNullOrEmpty(textCandidate))
                                    {
                                        textJson = textCandidate;
                                        if (textCandidate.Contains('{') || textCandidate.Contains('[')) break;
                                    }
                                }
                            }

                            if (!string.IsNullOrEmpty(textJson))
                            {
                                var cleanJson = CleanAndExtractJson(textJson);
                                if (!string.IsNullOrEmpty(cleanJson))
                                {
                                    var result = JsonSerializer.Deserialize<T>(cleanJson, _geminiJsonOptions);
                                    if (result != null)
                                    {
                                        return result;
                                    }
                                }
                            }
                        }
                    }
                }
                else
                {
                    lastError = $"Model {modelName} returned {response.StatusCode}: {responseStr}";
                    if ((int)response.StatusCode == 503 || (int)response.StatusCode == 429)
                    {
                        onStatus?.Invoke($"Máy chủ {modelName} phản hồi {response.StatusCode}, tự động chuyển model tiếp theo...");
                        await Task.Delay(800, ct);
                    }
                }
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                lastError = $"Model {modelName} phản hồi chậm quá 30s.";
                onStatus?.Invoke($"Model {modelName} quá thời gian 30s, thử model khác...");
            }
            catch (Exception ex)
            {
                lastError = ex.Message;
            }
            finally
            {
                request.Dispose();
            }
        }

        if (lastError != null && lastError.Contains("prepayment credits are depleted"))
        {
            throw new Exception("Key Gemini của bạn đang gắn với một dự án Google Cloud đã hết hạn mức trả trước.\n\n👉 Cách lấy Key Miễn Phí (Free Tier): Vào aistudio.google.com/app/apikey > Bấm 'Create API key' > Chọn 'Create API key in new project' để nhận Key Miễn Phí không cần thẻ!");
        }

        if (lastError != null && (lastError.Contains("BILLING_DISABLED") || lastError.Contains("billing to be enabled")))
        {
            throw new Exception("Dự án Google Cloud Vertex AI chưa bật Billing. Vui lòng bật Billing trên Google Cloud Console hoặc nhập Gemini API Key miễn phí từ Google AI Studio (aistudio.google.com).");
        }

        throw new Exception($"Không thể gọi Gemini AI: {lastError}");
    }

    /// <summary>
    /// Generic helper gọi Gemini API và lấy raw text trả về
    /// </summary>
    public async Task<string> CallGeminiForRawText(
        string systemInstruction,
        string userPrompt,
        string projectId,
        string location,
        string token,
        CancellationToken ct)
    {
        var apiKey = ApiKey;
        var modelsToTry = !string.IsNullOrWhiteSpace(apiKey)
            ? new[] { "gemini-3.5-flash-lite", "gemini-flash-lite-latest", "gemini-3.5-flash", "gemini-3.8-flash" }
            : new[] { "gemini-3.5-flash-lite", "gemini-flash-lite-latest", "gemini-3.5-flash" };
        string? lastError = null;

        foreach (var modelName in modelsToTry)
        {
            if (ct.IsCancellationRequested) break;

            string endpoint;
            HttpRequestMessage request;

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
                generationConfig = new
                {
                    maxOutputTokens = 8192,
                    temperature = 0.7
                }
            };

            var jsonContent = JsonSerializer.Serialize(requestBody);

            if (!string.IsNullOrWhiteSpace(apiKey))
            {
                endpoint = $"https://generativelanguage.googleapis.com/v1beta/models/{modelName}:generateContent?key={apiKey}";
                request = new HttpRequestMessage(HttpMethod.Post, endpoint);
                request.Content = new StringContent(jsonContent, Encoding.UTF8, "application/json");
            }
            else
            {
                endpoint = $"https://{location}-aiplatform.googleapis.com/v1/projects/{projectId}/locations/{location}/publishers/google/models/{modelName}:generateContent";
                request = new HttpRequestMessage(HttpMethod.Post, endpoint);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                request.Content = new StringContent(jsonContent, Encoding.UTF8, "application/json");
            }

            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                cts.CancelAfter(TimeSpan.FromSeconds(30));

                using var response = await _httpClient.SendAsync(request, cts.Token);
                var responseStr = await response.Content.ReadAsStringAsync(ct);

                if (response.IsSuccessStatusCode)
                {
                    using var doc = JsonDocument.Parse(responseStr);
                    if (doc.RootElement.TryGetProperty("candidates", out var candidates) && candidates.GetArrayLength() > 0)
                    {
                        var firstCandidate = candidates[0];
                        if (firstCandidate.TryGetProperty("content", out var content) &&
                            content.TryGetProperty("parts", out var parts) &&
                            parts.GetArrayLength() > 0)
                        {
                            foreach (var part in parts.EnumerateArray())
                            {
                                if (part.TryGetProperty("text", out var tProp))
                                {
                                    var text = tProp.GetString();
                                    if (!string.IsNullOrEmpty(text))
                                    {
                                        return text.Trim();
                                    }
                                }
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
            finally
            {
                request.Dispose();
            }
        }

        throw new Exception($"Không thể gọi Gemini AI: {lastError}");
    }
}
