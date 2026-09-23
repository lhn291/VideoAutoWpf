using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace VideoAutoWpf.Services;

/// <summary>
/// Service tạo hình ảnh bằng Gemini Image (thay thế Imagen 3/4 đã bị ngừng từ 08/2026).
/// Sử dụng Vertex AI generateContent API với responseModalities = IMAGE.
/// Có cơ chế tự động thử lại (Retry with Backoff) khi gặp lỗi 429 Quota/Rate Limit và Fallback model.
/// </summary>
public class VertexImagenService
{
    private readonly GoogleAuthService _authService;
    private static readonly HttpClient _httpClient = new();

    public VertexImagenService(GoogleAuthService authService)
    {
        _authService = authService;
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

        var token = await _authService.GetAccessTokenAsync();
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

        // Các model ảnh Gemini đã được kiểm tra hoạt động tại global location
        // Thử gemini-3.1-flash-image trước, fallback sang gemini-2.5-flash-image
        var modelsToTry = new[] { "gemini-3.1-flash-image", "gemini-2.5-flash-image" };
        string? lastError = null;

        foreach (var modelName in modelsToTry)
        {
            const int maxRetries = 3;

            for (int attempt = 0; attempt < maxRetries; attempt++)
            {
                ct.ThrowIfCancellationRequested();

                var endpoint = $"https://aiplatform.googleapis.com/v1/projects/{projectId}/locations/global/publishers/google/models/{modelName}:generateContent";

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
                    lastError = $"Lỗi Quota 429 trên {modelName}";
                    if (attempt < maxRetries - 1)
                    {
                        var waitSeconds = (attempt + 1) * 6; // Lần 1 chờ 6s, lần 2 chờ 12s
                        onLog?.Invoke($"[Cảnh báo Quota 429] Model {modelName} bị giới hạn tốc độ. Tự động thử lại sau {waitSeconds} giây (Lần {attempt + 1}/{maxRetries})...");
                        await Task.Delay(waitSeconds * 1000, ct);
                        continue;
                    }
                    else
                    {
                        onLog?.Invoke($"[Cảnh báo] Model {modelName} đã hết {maxRetries} lần thử do vượt Quota. Đang chuyển sang model tiếp theo...");
                        break; // Thoát retry của model hiện tại, chuyển sang model dự phòng
                    }
                }

                // 2. Xử lý lỗi 404 Not Found -> chuyển sang model khác ngay lập tức
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    lastError = $"Model {modelName} không tìm thấy (404).";
                    break;
                }

                // 3. Các lỗi HTTP khác
                if (!response.IsSuccessStatusCode)
                {
                    lastError = $"Lỗi gọi Gemini Image model {modelName} ({response.StatusCode}):\n{responseContent}";
                    // Nếu là lỗi server 5xx thì thử lại 1 lần
                    if ((int)response.StatusCode >= 500 && attempt < maxRetries - 1)
                    {
                        onLog?.Invoke($"[Máy chủ bận] Model {modelName} phản hồi {response.StatusCode}. Thử lại sau 4 giây...");
                        await Task.Delay(4000, ct);
                        continue;
                    }
                    throw new HttpRequestException(lastError);
                }

                // 4. Thành công 200: Parse response: candidates[0].content.parts[].inlineData.data (base64)
                using var doc = JsonDocument.Parse(responseContent);
                if (!doc.RootElement.TryGetProperty("candidates", out var candidates) || candidates.GetArrayLength() == 0)
                {
                    throw new Exception("API không trả về kết quả nào. Có thể prompt bị bộ lọc an toàn chặn.");
                }

                var parts = candidates[0].GetProperty("content").GetProperty("parts");
                
                // Tìm phần chứa inlineData (ảnh)
                string? base64Data = null;
                string mimeType = "image/png";
                
                for (int i = 0; i < parts.GetArrayLength(); i++)
                {
                    var part = parts[i];
                    if (part.TryGetProperty("inlineData", out var inlineData))
                    {
                        base64Data = inlineData.GetProperty("data").GetString();
                        if (inlineData.TryGetProperty("mimeType", out var mimeProp))
                        {
                            mimeType = mimeProp.GetString() ?? "image/png";
                        }
                        break;
                    }
                }

                if (string.IsNullOrEmpty(base64Data))
                {
                    throw new Exception("Không tìm thấy dữ liệu ảnh trong phản hồi của Gemini API.");
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

                var dir = Path.GetDirectoryName(finalPath);
                if (!string.IsNullOrEmpty(dir))
                    Directory.CreateDirectory(dir);

                await File.WriteAllBytesAsync(finalPath, imageBytes, ct);
                return finalPath;
            }
        }

        throw new HttpRequestException(lastError ?? "Không thể tạo ảnh: Tất cả các model Gemini Image đều gặp lỗi hoặc bị giới hạn Quota.");
    }
}
