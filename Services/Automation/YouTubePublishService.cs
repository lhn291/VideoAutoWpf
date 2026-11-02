using System.IO;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.Upload;
using Google.Apis.YouTube.v3;
using Google.Apis.YouTube.v3.Data;
using VideoAutoWpf.Models.Automation;

namespace VideoAutoWpf.Services.Automation;

public record YouTubeUploadResult(bool Success, string? VideoId, string? VideoUrl, string? ErrorMessage, bool IsPendingConfirmation);

/// <summary>
/// Dịch vụ tải video lên YouTube Shorts / YouTube Video sử dụng YouTube Data API v3 chính thống.
/// Hỗ trợ Resumable Upload, kiểm tra trạng thái video và chế độ DryRun an toàn.
/// </summary>
public class YouTubePublishService
{
    private readonly GoogleAuthService _authService;

    public YouTubePublishService(GoogleAuthService authService)
    {
        _authService = authService;
    }

    /// <summary>
    /// Thực hiện tải video lên YouTube và trả về Video ID hoặc trạng thái xác nhận
    /// </summary>
    public async Task<YouTubeUploadResult> UploadAsync(
        AutomationJob job,
        AutomationConfig config,
        Action<double>? onProgress = null,
        CancellationToken ct = default)
    {
        if (config.DryRunMode)
        {
            // Chế độ mô phỏng an toàn (Dry Run)
            for (int p = 10; p <= 100; p += 30)
            {
                ct.ThrowIfCancellationRequested();
                onProgress?.Invoke(p);
                await Task.Delay(500, ct);
            }
            var mockId = "mock_yt_" + Guid.NewGuid().ToString("N")[..8];
            return new YouTubeUploadResult(true, mockId, $"https://youtu.be/{mockId}", null, false);
        }

        if (string.IsNullOrWhiteSpace(job.OutputPath) || !File.Exists(job.OutputPath))
        {
            return new YouTubeUploadResult(false, null, null, $"File video không tồn tại: {job.OutputPath}", false);
        }

        try
        {
            var youtubeService = CreateYouTubeService();

            var video = new Video
            {
                Snippet = new VideoSnippet
                {
                    Title = Truncate(job.Title, 100),
                    Description = FormatDescription(job),
                    Tags = ParseTags(job.Hashtags),
                    CategoryId = string.IsNullOrWhiteSpace(config.YouTubeCategoryId) ? "22" : config.YouTubeCategoryId
                },
                Status = new VideoStatus
                {
                    PrivacyStatus = string.IsNullOrWhiteSpace(config.YouTubePrivacyStatus) ? "public" : config.YouTubePrivacyStatus.ToLowerInvariant(),
                    SelfDeclaredMadeForKids = false
                }
            };

            await using var fileStream = new FileStream(job.OutputPath, FileMode.Open, FileAccess.Read);
            var insertRequest = youtubeService.Videos.Insert(video, "snippet,status", fileStream, "video/mp4");
            insertRequest.ChunkSize = ResumableUpload.MinimumChunkSize * 4; // 1MB chunks

            string? uploadedVideoId = null;
            string? uploadError = null;
            bool wasInterrupted = false;

            insertRequest.ProgressChanged += progress =>
            {
                switch (progress.Status)
                {
                    case UploadStatus.Uploading:
                        if (fileStream.Length > 0)
                        {
                            var pct = (double)progress.BytesSent / fileStream.Length * 100.0;
                            onProgress?.Invoke(Math.Min(100.0, pct));
                        }
                        break;
                    case UploadStatus.Completed:
                        onProgress?.Invoke(100.0);
                        break;
                    case UploadStatus.Failed:
                        uploadError = progress.Exception?.Message ?? "Upload YouTube thất bại.";
                        break;
                }
            };

            insertRequest.ResponseReceived += responseVideo =>
            {
                uploadedVideoId = responseVideo.Id;
            };

            try
            {
                var uploadProgress = await insertRequest.UploadAsync(ct);
                if (uploadProgress.Status == UploadStatus.Completed && !string.IsNullOrEmpty(uploadedVideoId))
                {
                    var videoUrl = $"https://youtu.be/{uploadedVideoId}";
                    return new YouTubeUploadResult(true, uploadedVideoId, videoUrl, null, false);
                }
                else if (uploadProgress.Status == UploadStatus.Failed)
                {
                    return new YouTubeUploadResult(false, null, null, uploadError ?? "Lỗi upload YouTube", false);
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                wasInterrupted = true;
                uploadError = ex.Message;
            }

            // Nếu gặp sự cố ngắt kết nối giữa chừng, không coi là thất bại vĩnh viễn mà đánh dấu AwaitingConfirmation
            if (wasInterrupted || (!string.IsNullOrEmpty(uploadedVideoId) && string.IsNullOrEmpty(uploadError)))
            {
                return new YouTubeUploadResult(false, uploadedVideoId, null, uploadError ?? "Kết nối bị gián đoạn, cần xác minh trạng thái từ YouTube", true);
            }

            return new YouTubeUploadResult(false, null, null, uploadError ?? "Upload YouTube chưa hoàn tất", false);
        }
        catch (Exception ex)
        {
            return new YouTubeUploadResult(false, null, null, $"Lỗi khởi tạo YouTube API: {ex.Message}", false);
        }
    }

    /// <summary>
    /// Kiểm tra trạng thái thực tế của video trên YouTube sau upload để chống đăng trùng
    /// </summary>
    public async Task<bool> VerifyVideoExistsAsync(string videoId, CancellationToken ct = default)
    {
        if (videoId.StartsWith("mock_yt_")) return true;

        try
        {
            var service = CreateYouTubeService();
            var listReq = service.Videos.List("status");
            listReq.Id = videoId;
            var response = await listReq.ExecuteAsync(ct);
            return response.Items != null && response.Items.Count > 0;
        }
        catch
        {
            return false;
        }
    }

    private YouTubeService CreateYouTubeService()
    {
        var credential = _authService.GetCredential();
        if (credential == null)
            throw new InvalidOperationException("Chưa cấu hình Google credentials hợp lệ để upload YouTube.");

        var scopedCredential = credential.CreateScoped(
            "https://www.googleapis.com/auth/youtube.upload",
            "https://www.googleapis.com/auth/youtube"
        );

        return new YouTubeService(new BaseClientService.Initializer
        {
            HttpClientInitializer = scopedCredential,
            ApplicationName = "VideoAutoWpf Publisher"
        });
    }

    private static string FormatDescription(AutomationJob job)
    {
        var sb = new System.Text.StringBuilder();
        if (!string.IsNullOrWhiteSpace(job.Caption))
            sb.AppendLine(job.Caption.Trim());

        if (!string.IsNullOrWhiteSpace(job.Hashtags))
        {
            sb.AppendLine();
            sb.AppendLine(job.Hashtags.Trim());
        }

        return sb.ToString();
    }

    private static List<string> ParseTags(string? hashtags)
    {
        var list = new List<string>();
        if (string.IsNullOrWhiteSpace(hashtags)) return list;
        foreach (var tag in hashtags.Split(new[] { ' ', '#', ',', ';' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var clean = tag.Trim();
            if (!string.IsNullOrWhiteSpace(clean) && !list.Contains(clean))
            {
                list.Add(clean);
            }
        }
        return list;
    }

    private static string Truncate(string value, int maxLen)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        return value.Length <= maxLen ? value : value[..maxLen];
    }
}
