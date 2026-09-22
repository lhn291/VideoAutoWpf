using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace VideoAutoWpf.Services;

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
        string location = "us-central1",
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(prompt))
            throw new ArgumentException("Mô tả hình ảnh (Image Prompt) không được để trống.", nameof(prompt));

        var token = await _authService.GetAccessTokenAsync();
        var projectId = _authService.ProjectId;

        var endpoint = $"https://{location}-aiplatform.googleapis.com/v1/projects/{projectId}/locations/{location}/publishers/google/models/imagen-3.0-generate-002:predict";

        // Chuẩn hóa aspect ratio theo API của Imagen 3: "9:16", "16:9", "1:1"
        var ratio = aspectRatio switch
        {
            "16:9" => "16:9",
            "1:1" => "1:1",
            _ => "9:16"
        };

        var requestBody = new
        {
            instances = new[]
            {
                new { prompt = prompt }
            },
            parameters = new
            {
                sampleCount = 1,
                aspectRatio = ratio,
                outputOptions = new
                {
                    mimeType = "image/png"
                }
            }
        };

        var json = JsonSerializer.Serialize(requestBody);
        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Content = new StringContent(json, Encoding.UTF8, "application/json");

        using var response = await _httpClient.SendAsync(request, ct);
        var responseContent = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"Lỗi gọi Vertex AI Imagen 3 ({response.StatusCode}):\n{responseContent}");
        }

        using var doc = JsonDocument.Parse(responseContent);
        if (!doc.RootElement.TryGetProperty("predictions", out var predictions) || predictions.GetArrayLength() == 0)
        {
            throw new Exception("API Imagen 3 không trả về ảnh nào. Có thể prompt bị bộ lọc an toàn chặn.");
        }

        var firstPred = predictions[0];
        if (!firstPred.TryGetProperty("bytesBase64Encoded", out var bytesProp))
        {
            throw new Exception("Không tìm thấy dữ liệu ảnh base64 trong phản hồi của Vertex AI.");
        }

        var base64 = bytesProp.GetString();
        if (string.IsNullOrEmpty(base64))
        {
            throw new Exception("Dữ liệu ảnh base64 rỗng.");
        }

        var imageBytes = Convert.FromBase64String(base64);

        var dir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        await File.WriteAllBytesAsync(outputPath, imageBytes, ct);
        return outputPath;
    }
}
