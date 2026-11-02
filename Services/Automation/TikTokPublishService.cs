using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using VideoAutoWpf.Models.Automation;

namespace VideoAutoWpf.Services.Automation;

public record TikTokUploadResult(bool Success, string? PublishId, string? PostUrl, string? ErrorMessage, bool IsPendingConfirmation);

/// <summary>
/// Dịch vụ đăng tải video lên TikTok sử dụng TikTok Content Posting API v2 (Direct Post).
/// Hỗ trợ khởi tạo video, truyền dữ liệu nhị phân, kiểm tra trạng thái và chế độ DryRun an toàn.
/// </summary>
public class TikTokPublishService
{
    private readonly HttpClient _httpClient;

    public TikTokPublishService(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
    }

    /// <summary>
    /// Đăng video lên TikTok qua quy trình Content Posting API v2
    /// </summary>
    public async Task<TikTokUploadResult> UploadAsync(
        AutomationJob job,
        AutomationConfig config,
        Action<double>? onProgress = null,
        CancellationToken ct = default)
    {
        if (config.DryRunMode)
        {
            // Chế độ mô phỏng an toàn (Dry Run)
            for (int p = 15; p <= 100; p += 30)
            {
                ct.ThrowIfCancellationRequested();
                onProgress?.Invoke(p);
                await Task.Delay(500, ct);
            }
            var mockId = "mock_tt_" + Guid.NewGuid().ToString("N")[..8];
            return new TikTokUploadResult(true, mockId, $"https://www.tiktok.com/@user/video/{mockId}", null, false);
        }

        var accessToken = config.ResolveTikTokAccessToken();
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            return new TikTokUploadResult(false, null, null, "Chưa cấu hình TIKTOK_ACCESS_TOKEN hoặc quyền đăng Direct Post.", false);
        }

        if (string.IsNullOrWhiteSpace(job.OutputPath) || !File.Exists(job.OutputPath))
        {
            return new TikTokUploadResult(false, null, null, $"File video không tồn tại: {job.OutputPath}", false);
        }

        try
        {
            var fileInfo = new FileInfo(job.OutputPath);
            var videoSize = fileInfo.Length;

            // Bước 1: Khởi tạo upload video với TikTok
            onProgress?.Invoke(10.0);
            var initUrl = "https://open.tiktokapis.com/v2/post/publish/video/init/";
            var initPayload = new
            {
                post_info = new
                {
                    title = FormatTitleAndCaption(job),
                    privacy_level = "PUBLIC_TO_EVERYONE",
                    disable_duet = false,
                    disable_stitch = false,
                    disable_comment = false,
                    video_cover_timestamp_ms = 1000
                },
                source_info = new
                {
                    source = "FILE_UPLOAD",
                    video_size = videoSize,
                    chunk_size = videoSize,
                    total_chunk_count = 1
                }
            };

            using var initRequest = new HttpRequestMessage(HttpMethod.Post, initUrl);
            initRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            initRequest.Content = new StringContent(JsonSerializer.Serialize(initPayload), Encoding.UTF8, "application/json");

            var initResponse = await _httpClient.SendAsync(initRequest, ct);
            var initJson = await initResponse.Content.ReadAsStringAsync(ct);

            if (!initResponse.IsSuccessStatusCode)
            {
                return new TikTokUploadResult(false, null, null, $"TikTok Init thất bại: {initJson}", false);
            }

            using var initDoc = JsonDocument.Parse(initJson);
            var root = initDoc.RootElement;
            if (root.TryGetProperty("error", out var errElem) && errElem.TryGetProperty("code", out var codeProp))
            {
                var errCode = codeProp.GetString();
                if (errCode != "ok" && !string.IsNullOrEmpty(errCode))
                {
                    var errMsg = errElem.TryGetProperty("message", out var msgProp) ? msgProp.GetString() : "Unknown";
                    return new TikTokUploadResult(false, null, null, $"TikTok Error [{errCode}]: {errMsg}", false);
                }
            }

            if (!root.TryGetProperty("data", out var dataElem) ||
                !dataElem.TryGetProperty("publish_id", out var pubIdElem) ||
                !dataElem.TryGetProperty("upload_url", out var uploadUrlElem))
            {
                return new TikTokUploadResult(false, null, null, $"Không nhận được upload_url từ TikTok: {initJson}", false);
            }

            var publishId = pubIdElem.GetString()!;
            var uploadUrl = uploadUrlElem.GetString()!;

            // Bước 2: Tải dữ liệu video stream lên upload_url
            onProgress?.Invoke(30.0);
            await using var fileStream = new FileStream(job.OutputPath, FileMode.Open, FileAccess.Read);
            using var uploadRequest = new HttpRequestMessage(HttpMethod.Put, uploadUrl);
            uploadRequest.Content = new StreamContent(fileStream);
            uploadRequest.Content.Headers.ContentType = new MediaTypeHeaderValue("video/mp4");
            uploadRequest.Content.Headers.ContentLength = videoSize;
            uploadRequest.Content.Headers.Add("Content-Range", $"bytes 0-{videoSize - 1}/{videoSize}");

            var uploadResponse = await _httpClient.SendAsync(uploadRequest, ct);
            if (!uploadResponse.IsSuccessStatusCode)
            {
                // Upload bị ngắt kết nối -> chuyển AwaitingConfirmation để kiểm tra lại bằng publish_id
                return new TikTokUploadResult(false, publishId, null, "Quá trình gửi binary video lên TikTok bị gián đoạn.", true);
            }

            onProgress?.Invoke(70.0);

            // Bước 3: Kiểm tra trạng thái xuất bản
            var statusResult = await CheckPublishStatusAsync(accessToken, publishId, ct);
            onProgress?.Invoke(100.0);

            return statusResult;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return new TikTokUploadResult(false, null, null, $"Lỗi xử lý TikTok API: {ex.Message}", false);
        }
    }

    /// <summary>
    /// Kiểm tra trạng thái xử lý video của TikTok qua publish_id
    /// </summary>
    public async Task<TikTokUploadResult> CheckPublishStatusAsync(string accessToken, string publishId, CancellationToken ct = default)
    {
        if (publishId.StartsWith("mock_tt_"))
        {
            return new TikTokUploadResult(true, publishId, $"https://www.tiktok.com/@user/video/{publishId}", null, false);
        }

        try
        {
            var statusUrl = "https://open.tiktokapis.com/v2/post/publish/status/fetch/";
            var payload = new { publish_id = publishId };

            using var request = new HttpRequestMessage(HttpMethod.Post, statusUrl);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(request, ct);
            var json = await response.Content.ReadAsStringAsync(ct);

            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("data", out var dataElem) &&
                dataElem.TryGetProperty("status", out var statusProp))
            {
                var status = statusProp.GetString();
                if (status == "PUBLISH_COMPLETE")
                {
                    return new TikTokUploadResult(true, publishId, $"https://www.tiktok.com", null, false);
                }
                else if (status == "FAILED")
                {
                    var failReason = dataElem.TryGetProperty("fail_reason", out var fr) ? fr.GetString() : "Thất bại không rõ nguyên nhân";
                    return new TikTokUploadResult(false, publishId, null, $"TikTok Publish FAILED: {failReason}", false);
                }
                else
                {
                    // Đang xử lý: PROCESSING_UPLOAD hoặc PROCESSING_DOWNLOAD
                    return new TikTokUploadResult(false, publishId, null, $"TikTok đang xử lý video ({status})", true);
                }
            }

            return new TikTokUploadResult(false, publishId, null, "Không đọc được trạng thái từ TikTok.", true);
        }
        catch (Exception ex)
        {
            return new TikTokUploadResult(false, publishId, null, ex.Message, true);
        }
    }

    private static string FormatTitleAndCaption(AutomationJob job)
    {
        var sb = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(job.Title))
            sb.Append(job.Title.Trim());

        if (!string.IsNullOrWhiteSpace(job.Hashtags))
        {
            sb.Append(' ');
            sb.Append(job.Hashtags.Trim());
        }

        var text = sb.ToString();
        // TikTok giới hạn title tối đa khoảng 150 ký tự trong Direct Post
        return text.Length > 150 ? text[..150] : text;
    }
}
