using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace VideoAutoWpf.Services;

/// <summary>
/// Service tạo hình ảnh bằng Gemini Image (thay thế Imagen 3/4 đã bị ngừng từ 08/2026).
/// Sử dụng Vertex AI generateContent API với responseModalities = IMAGE.
/// Tích hợp bộ điều tiết tốc độ (Sliding Window Rate Limiter) và cơ chế tự động chờ phục hồi Quota 429 (Exponential Backoff).
/// </summary>
public class VertexImagenService
{
    private readonly GoogleAuthService _authService;
    private static readonly HttpClient _httpClient = new();

    // Cơ chế điều tiết tốc độ (Sliding Window Rate Limiter) cho Vertex AI Image API.
    // Hạn mức Vertex AI Multimodal Image là ~5 requests/phút (RPM).
    // Đặt ngưỡng an toàn tối đa 4 requests trong cửa sổ trượt 60 giây để KHÔNG BAO GIỜ bị 429 RESOURCE_EXHAUSTED.
    private static readonly SemaphoreSlim _rateLimiterLock = new(1, 1);
    private static readonly Queue<DateTime> _recentRequestTimestamps = new();
    private const int MaxSafeRequestsPerMinute = 4;
    private const int RateLimitWindowSeconds = 60;

    public VertexImagenService(GoogleAuthService authService)
    {
        _authService = authService;
    }

    /// <summary>
    /// Điều tiết tốc độ gọi API để đảm bảo không vượt quá MaxSafeRequestsPerMinute trong bất kỳ cửa sổ 60 giây nào.
    /// </summary>
    private static async Task EnforceRateLimitAsync(Action<string>? onLog, CancellationToken ct)
    {
        await _rateLimiterLock.WaitAsync(ct);
        try
        {
            var now = DateTime.UtcNow;
            // Dọn dẹp các mốc thời gian ngoài cửa sổ 60s
            while (_recentRequestTimestamps.Count > 0 && (now - _recentRequestTimestamps.Peek()).TotalSeconds >= RateLimitWindowSeconds)
            {
                _recentRequestTimestamps.Dequeue();
            }

            // Nếu đã thực hiện đủ MaxSafeRequestsPerMinute trong 60 giây qua
            if (_recentRequestTimestamps.Count >= MaxSafeRequestsPerMinute)
            {
                var oldest = _recentRequestTimestamps.Peek();
                var elapsed = (now - oldest).TotalSeconds;
                // Chờ đủ thời gian để request cũ nhất trôi ra ngoài cửa sổ 60s + thêm 2s đệm an toàn
                var waitSeconds = Math.Max(2.0, RateLimitWindowSeconds - elapsed + 2.0);

                onLog?.Invoke($"[Điều tiết tốc độ] Đã thực hiện {_recentRequestTimestamps.Count} yêu cầu ảnh trong 60s qua (đạt hạn mức an toàn {MaxSafeRequestsPerMinute} ảnh/phút). Tự động tạm nghỉ {waitSeconds:F0}s để hồi phục Quota Vertex AI...");

                await Task.Delay(TimeSpan.FromSeconds(waitSeconds), ct);

                // Dọn dẹp lại sau khi chờ
                now = DateTime.UtcNow;
                while (_recentRequestTimestamps.Count > 0 && (now - _recentRequestTimestamps.Peek()).TotalSeconds >= RateLimitWindowSeconds)
                {
                    _recentRequestTimestamps.Dequeue();
                }
            }

            _recentRequestTimestamps.Enqueue(DateTime.UtcNow);
        }
        finally
        {
            _rateLimiterLock.Release();
        }
    }

    public async Task<string> GenerateImageAsync(
        string prompt, 
        string outputPath, 
        string aspectRatio = "9:16", 
        string location = "global",
        Action<string>? onLog = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(prompt))
            throw new ArgumentException("Mô tả hình ảnh (Image Prompt) không được để trống.", nameof(prompt));

        var projectId = _authService.ProjectId;

        // Chuẩn hóa aspect ratio: "9:16", "16:9", "1:1"
        var ratio = aspectRatio switch
        {
            "16:9" => "16:9",
            "1:1" => "1:1",
            _ => "9:16"
        };

        var requestBody = new
        {
            contents = new[]
            {
                new
                {
                    role = "user",
                    parts = new[] { new { text = prompt } }
                }
            },
            generationConfig = new
            {
                responseModalities = new[] { "IMAGE" },
                imageConfig = new
                {
                    aspectRatio = ratio
                }
            }
        };

        var json = JsonSerializer.Serialize(requestBody);

        // Danh sách model ưu tiên:
        // Model chính: gemini-3.1-flash-image (Multimodal Image generation mới nhất của Google Vertex AI).
        // Model dự phòng: gemini-2.5-flash-image (Cùng giao thức generateContent multimodal).
        // TUYỆT ĐỐI KHÔNG dùng imagen-3.0-* vì không hỗ trợ trên dự án này (gây lỗi 404).
        var modelsToTry = new[] { "gemini-3.1-flash-image", "gemini-2.5-flash-image" };
        string? lastError = null;

        // Chủ động điều tiết tốc độ trước khi gửi request (tránh 429 ngay từ đầu)
        await EnforceRateLimitAsync(onLog, ct);

        foreach (var modelName in modelsToTry)
        {
            const int maxRetries = 5;

            for (int attempt = 0; attempt < maxRetries; attempt++)
            {
                ct.ThrowIfCancellationRequested();

                // Luôn lấy token mới nhất để tránh hết hạn access token trong quá trình chờ retry
                var token = await _authService.GetAccessTokenAsync();

                // Endpoint Gemini Multimodal Image (generateContent) tại global
                var endpoint = $"https://aiplatform.googleapis.com/v1/projects/{projectId}/locations/{location}/publishers/google/models/{modelName}:generateContent";

                using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                request.Content = new StringContent(json, Encoding.UTF8, "application/json");

                using var response = await _httpClient.SendAsync(request, ct);
                var responseContent = await response.Content.ReadAsStringAsync(ct);

                // 1. Xử lý lỗi 429 TooManyRequests / RESOURCE_EXHAUSTED
                if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests || 
                    responseContent.Contains("RESOURCE_EXHAUSTED") || 
                    responseContent.Contains("check quota"))
                {
                    lastError = $"Hạn mức Quota 429 trên {modelName}";
                    if (attempt < maxRetries - 1)
                    {
                        // Cửa sổ trượt của Vertex AI là 60s. Backoff tăng dần (15s, 30s, 45s, 60s) để đảm bảo cửa sổ trôi qua.
                        var waitSeconds = Math.Min(60, (attempt + 1) * 15);
                        onLog?.Invoke($"[Cảnh báo Quota 429] Model {modelName} bị giới hạn tốc độ. Tự động chờ {waitSeconds} giây để hồi phục Quota (Lần thử {attempt + 1}/{maxRetries})...");
                        await Task.Delay(TimeSpan.FromSeconds(waitSeconds), ct);
                        continue; // TIẾP TỤC THỬ LẠI VỚI MODEL NÀY, KHÔNG ĐỔI MODEL!
                    }
                    else
                    {
                        onLog?.Invoke($"[Cảnh báo] Model {modelName} đã thử lại {maxRetries} lần nhưng Quota vẫn chưa mở lại. Đang thử model tiếp theo...");
                        break;
                    }
                }

                // 2. Xử lý lỗi 404 Not Found -> Model không tồn tại trên vùng/dự án này -> chuyển sang model khác ngay lập tức
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    lastError = $"Model {modelName} không tìm thấy (404) tại location '{location}'.";
                    onLog?.Invoke($"[Cảnh báo 404] {lastError} Đang chuyển sang model tiếp theo...");
                    break;
                }

                // 3. Các lỗi HTTP khác
                if (!response.IsSuccessStatusCode)
                {
                    lastError = $"Lỗi gọi model {modelName} ({response.StatusCode}):\n{responseContent}";
                    if ((int)response.StatusCode >= 500 && attempt < maxRetries - 1)
                    {
                        var waitSec = (attempt + 1) * 5;
                        onLog?.Invoke($"[Máy chủ bận {response.StatusCode}] Model {modelName} đang quá tải. Thử lại sau {waitSec} giây...");
                        await Task.Delay(TimeSpan.FromSeconds(waitSec), ct);
                        continue;
                    }
                    onLog?.Invoke($"[Cảnh báo] Model {modelName} phản hồi lỗi {response.StatusCode}. Đang thử model tiếp theo...");
                    break;
                }

                // 4. Parse response an toàn không dùng GetProperty() trực tiếp để tránh KeyNotFoundException
                try
                {
                    using var doc = JsonDocument.Parse(responseContent);
                    string? base64Data = null;
                    string mimeType = "image/png";

                    // Parse Gemini Image: candidates[0].content.parts[].inlineData.data
                    if (doc.RootElement.TryGetProperty("candidates", out var candidates) && candidates.GetArrayLength() > 0)
                    {
                        var cand = candidates[0];

                        // Kiểm tra finishReason an toàn
                        if (cand.TryGetProperty("finishReason", out var frProp))
                        {
                            var fr = frProp.GetString();
                            if (!string.IsNullOrEmpty(fr) && fr.Equals("SAFETY", StringComparison.OrdinalIgnoreCase))
                            {
                                onLog?.Invoke($"[Cảnh báo] Prompt bị bộ lọc Safety của Google chặn trên {modelName}.");
                                lastError = "Hình ảnh bị bộ lọc an toàn của Google chặn (SAFETY filter). Vui lòng điều chỉnh lại prompt hoặc từ ngữ trong cảnh.";
                                break;
                            }
                        }

                        if (cand.TryGetProperty("content", out var contentElem) &&
                            contentElem.TryGetProperty("parts", out var partsElem) &&
                            partsElem.GetArrayLength() > 0)
                        {
                            for (int i = 0; i < partsElem.GetArrayLength(); i++)
                            {
                                var part = partsElem[i];
                                if (part.TryGetProperty("inlineData", out var inlineData))
                                {
                                    if (inlineData.TryGetProperty("data", out var dataProp))
                                    {
                                        base64Data = dataProp.GetString();
                                    }
                                    if (inlineData.TryGetProperty("mimeType", out var mimeProp))
                                    {
                                        mimeType = mimeProp.GetString() ?? "image/png";
                                    }
                                    break;
                                }
                            }
                        }
                    }
                    else if (doc.RootElement.TryGetProperty("promptFeedback", out var pf) &&
                             pf.TryGetProperty("blockReason", out var br))
                    {
                        lastError = $"Prompt bị bộ lọc an toàn chặn ({br.GetString()}). Vui lòng điều chỉnh lại prompt.";
                        onLog?.Invoke($"[Cảnh báo an toàn] {lastError}");
                        break;
                    }

                    if (string.IsNullOrEmpty(base64Data))
                    {
                        lastError = $"Không tìm thấy dữ liệu ảnh base64 hợp lệ trong phản hồi của {modelName}.";
                        onLog?.Invoke($"[Cảnh báo] {lastError} Đang thử model tiếp theo...");
                        break;
                    }

                    var imageBytes = Convert.FromBase64String(base64Data);

                    // Đảm bảo extension file phù hợp với mimeType
                    var finalPath = outputPath;
                    if (mimeType.Contains("jpeg") || mimeType.Contains("jpg"))
                    {
                        finalPath = Path.ChangeExtension(outputPath, ".jpg");
                    }
                    else if (mimeType.Contains("webp"))
                    {
                        finalPath = Path.ChangeExtension(outputPath, ".webp");
                    }
                    else
                    {
                        finalPath = Path.ChangeExtension(outputPath, ".png");
                    }

                    var dir = Path.GetDirectoryName(finalPath);
                    if (!string.IsNullOrEmpty(dir))
                        Directory.CreateDirectory(dir);

                    await File.WriteAllBytesAsync(finalPath, imageBytes, ct);
                    return finalPath;
                }
                catch (Exception parseEx)
                {
                    lastError = $"Lỗi xử lý dữ liệu từ {modelName}: {parseEx.Message}";
                    onLog?.Invoke($"[Cảnh báo] {lastError} Đang thử model tiếp theo...");
                    break;
                }
            }
        }

        throw new HttpRequestException(lastError ?? "Không thể tạo ảnh: Các model Gemini Image đều gặp lỗi hoặc bị giới hạn Quota.");
    }

    /// <summary>
    /// Sinh ảnh cho một Shot cụ thể trong kiến trúc V2.
    /// Cập nhật trực tiếp trạng thái của Shot: Generating -> Ready hoặc Failed.
    /// TUYỆT ĐỐI KHÔNG COPY ẢNH CỦA SCENE/SHOT TRƯỚC NẾU THẤT BẠI.
    /// </summary>
    public async Task<string> GenerateShotImageAsync(
        VideoAutoWpf.Models.V2.Shot shot,
        string prompt,
        string outputPath,
        string aspectRatio = "9:16",
        string location = "global",
        Action<string>? onLog = null,
        string? referenceImagePath = null,
        CancellationToken ct = default)
    {
        if (shot == null) throw new ArgumentNullException(nameof(shot));

        var promptToUse = !string.IsNullOrWhiteSpace(prompt)
            ? prompt
            : (!string.IsNullOrWhiteSpace(shot.CompiledImagePrompt) ? shot.CompiledImagePrompt : shot.VisualDescription);

        if (string.IsNullOrWhiteSpace(promptToUse))
            throw new ArgumentException($"Shot {shot.ShotCode} chưa có prompt hoặc mô tả để vẽ ảnh.");

        shot.IsGenerating = true;
        shot.Status = "Generating";
        shot.ErrorMessage = null;

        try
        {
            string savedPath;
            if (!string.IsNullOrEmpty(referenceImagePath) && File.Exists(referenceImagePath))
            {
                onLog?.Invoke($"[V2 Continuity] Shot {shot.ShotCode}: Đang dùng ảnh tham chiếu nhân vật/bối cảnh để duy trì tính nhất quán...");
                savedPath = await GenerateImageWithReferenceAsync(promptToUse, referenceImagePath, outputPath, aspectRatio, location, onLog, ct);
            }
            else
            {
                savedPath = await GenerateImageAsync(promptToUse, outputPath, aspectRatio, location, onLog, ct);
            }
            shot.ImagePath = savedPath;
            shot.Status = "Ready";
            return savedPath;
        }
        catch (Exception ex)
        {
            shot.Status = "Failed";
            shot.ErrorMessage = ex.Message;
            throw;
        }
        finally
        {
            shot.IsGenerating = false;
        }
    }

    /// <summary>
    /// Sinh ảnh với ảnh tham chiếu (Reference Image) để duy trì tính nhất quán nhân vật/bối cảnh.
    /// Gửi ảnh tham chiếu dưới dạng inlineData trong Gemini multimodal request.
    /// Nếu ảnh tham chiếu không đọc được, tự động fallback về GenerateImageAsync thông thường.
    /// </summary>
    public async Task<string> GenerateImageWithReferenceAsync(
        string prompt,
        string referenceImagePath,
        string outputPath,
        string aspectRatio = "9:16",
        string location = "global",
        Action<string>? onLog = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(prompt))
            throw new ArgumentException("Prompt không được để trống.", nameof(prompt));

        // Đọc ảnh tham chiếu và mã hóa base64
        string referenceBase64;
        string referenceMimeType;
        try
        {
            var ext = Path.GetExtension(referenceImagePath).ToLowerInvariant();
            referenceMimeType = ext switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".webp" => "image/webp",
                _ => "image/png"
            };
            var bytes = await File.ReadAllBytesAsync(referenceImagePath, ct);
            referenceBase64 = Convert.ToBase64String(bytes);
        }
        catch (Exception ex)
        {
            onLog?.Invoke($"[Cảnh báo Tham chiếu] Không đọc được ảnh tham chiếu '{Path.GetFileName(referenceImagePath)}': {ex.Message}. Đang fallback về sinh ảnh thông thường...");
            return await GenerateImageAsync(prompt, outputPath, aspectRatio, location, onLog, ct);
        }

        var projectId = _authService.ProjectId;
        var ratio = aspectRatio switch
        {
            "16:9" => "16:9",
            "1:1" => "1:1",
            _ => "9:16"
        };

        // Xây dựng request multimodal: ảnh tham chiếu + text prompt
        // Gemini Image sẽ dùng ảnh tham chiếu như một style/subject anchor
        var requestBody = new
        {
            contents = new[]
            {
                new
                {
                    role = "user",
                    parts = new object[]
                    {
                        new
                        {
                            inlineData = new
                            {
                                mimeType = referenceMimeType,
                                data = referenceBase64
                            }
                        },
                        new { text = $"Using the person or location shown in the reference image above as the visual anchor for consistency, generate a new image matching this description: {prompt}" }
                    }
                }
            },
            generationConfig = new
            {
                responseModalities = new[] { "IMAGE" },
                imageConfig = new { aspectRatio = ratio }
            }
        };

        var json = JsonSerializer.Serialize(requestBody);
        var modelsToTry = new[] { "gemini-3.1-flash-image", "gemini-2.5-flash-image" };
        string? lastError = null;

        await EnforceRateLimitAsync(onLog, ct);

        foreach (var modelName in modelsToTry)
        {
            const int maxRetries = 3;
            for (int attempt = 0; attempt < maxRetries; attempt++)
            {
                ct.ThrowIfCancellationRequested();

                var token = await _authService.GetAccessTokenAsync();
                var endpoint = $"https://aiplatform.googleapis.com/v1/projects/{projectId}/locations/{location}/publishers/google/models/{modelName}:generateContent";

                using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                request.Content = new StringContent(json, Encoding.UTF8, "application/json");

                using var response = await _httpClient.SendAsync(request, ct);
                var responseContent = await response.Content.ReadAsStringAsync(ct);

                if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests ||
                    responseContent.Contains("RESOURCE_EXHAUSTED"))
                {
                    var waitSeconds = Math.Min(60, (attempt + 1) * 20);
                    onLog?.Invoke($"[Quota 429 - Reference] Chờ {waitSeconds}s... (Lần {attempt + 1}/{maxRetries})");
                    await Task.Delay(TimeSpan.FromSeconds(waitSeconds), ct);
                    continue;
                }

                if (!response.IsSuccessStatusCode)
                {
                    lastError = $"Lỗi {response.StatusCode} trên {modelName} (reference mode)";
                    onLog?.Invoke($"[Cảnh báo] {lastError}. Thử model tiếp theo...");
                    break;
                }

                try
                {
                    using var doc = JsonDocument.Parse(responseContent);
                    string? base64Data = null;
                    string mimeType = "image/png";

                    if (doc.RootElement.TryGetProperty("candidates", out var candidates) && candidates.GetArrayLength() > 0)
                    {
                        var cand = candidates[0];
                        if (cand.TryGetProperty("finishReason", out var frProp) &&
                            frProp.GetString()?.Equals("SAFETY", StringComparison.OrdinalIgnoreCase) == true)
                        {
                            onLog?.Invoke($"[Safety] Reference-mode prompt bị chặn. Fallback về sinh ảnh thông thường...");
                            return await GenerateImageAsync(prompt, outputPath, aspectRatio, location, onLog, ct);
                        }

                        if (cand.TryGetProperty("content", out var contentElem) &&
                            contentElem.TryGetProperty("parts", out var partsElem))
                        {
                            for (int i = 0; i < partsElem.GetArrayLength(); i++)
                            {
                                var part = partsElem[i];
                                if (part.TryGetProperty("inlineData", out var inlineData))
                                {
                                    if (inlineData.TryGetProperty("data", out var dataProp))
                                        base64Data = dataProp.GetString();
                                    if (inlineData.TryGetProperty("mimeType", out var mimeProp))
                                        mimeType = mimeProp.GetString() ?? "image/png";
                                    break;
                                }
                            }
                        }
                    }

                    if (string.IsNullOrEmpty(base64Data))
                    {
                        // Reference mode không trả ảnh — fallback gracefully
                        onLog?.Invoke($"[Cảnh báo] {modelName} không trả ảnh trong reference mode. Fallback về standard generation...");
                        return await GenerateImageAsync(prompt, outputPath, aspectRatio, location, onLog, ct);
                    }

                    var imageBytes = Convert.FromBase64String(base64Data);
                    var finalPath = mimeType.Contains("jpeg") || mimeType.Contains("jpg")
                        ? Path.ChangeExtension(outputPath, ".jpg")
                        : mimeType.Contains("webp")
                            ? Path.ChangeExtension(outputPath, ".webp")
                            : Path.ChangeExtension(outputPath, ".png");

                    var dir = Path.GetDirectoryName(finalPath);
                    if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

                    await File.WriteAllBytesAsync(finalPath, imageBytes, ct);
                    onLog?.Invoke($"[V2 Continuity] ✅ Sinh ảnh với tham chiếu thành công: {Path.GetFileName(finalPath)}");
                    return finalPath;
                }
                catch (Exception parseEx)
                {
                    lastError = $"Parse lỗi reference mode ({modelName}): {parseEx.Message}";
                    onLog?.Invoke($"[Cảnh báo] {lastError}. Fallback...");
                    return await GenerateImageAsync(prompt, outputPath, aspectRatio, location, onLog, ct);
                }
            }
        }

        // Nếu tất cả đều thất bại, fallback về standard generation
        onLog?.Invoke($"[Cảnh báo] Reference-mode thất bại hoàn toàn. Fallback về sinh ảnh thông thường...");
        return await GenerateImageAsync(prompt, outputPath, aspectRatio, location, onLog, ct);
    }
}
