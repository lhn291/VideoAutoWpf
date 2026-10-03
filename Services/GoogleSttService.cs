using System.IO;
using Google.Cloud.Speech.V2;
using Google.Protobuf;

namespace VideoAutoWpf.Services;

/// <summary>
/// Service tích hợp Google Cloud Speech-to-Text v2 (Chirp 2)
/// để nhận diện word-level timestamps từ file audio TTS,
/// phục vụ tạo phụ đề karaoke nhảy từng chữ kiểu CapCut / TikTok viral.
/// </summary>
public class GoogleSttService
{
    private readonly GoogleAuthService _authService;
    private SpeechClient? _client;

    public GoogleSttService(GoogleAuthService authService)
    {
        _authService = authService;
    }

    private SpeechClient GetClient()
    {
        if (_client != null) return _client;

        var cred = _authService.GetCredential();
        if (cred == null)
            throw new InvalidOperationException(
                "Không thể khởi tạo Google STT: Thiếu thông tin xác thực Service Account.");

        var builder = new SpeechClientBuilder
        {
            Credential = cred
        };
        _client = builder.Build();
        return _client;
    }

    /// <summary>
    /// Nhận diện giọng nói từ file audio và trả về danh sách từ kèm timestamp chính xác.
    /// Sử dụng model Chirp 2 cho tiếng Việt với độ chính xác cao nhất.
    /// </summary>
    /// <param name="audioPath">Đường dẫn file audio (MP3/WAV)</param>
    /// <param name="languageCode">Mã ngôn ngữ (mặc định: vi-VN)</param>
    /// <param name="ct">CancellationToken</param>
    /// <returns>Danh sách WordTimestamp chứa từ + thời gian bắt đầu/kết thúc</returns>
    public async Task<List<WordTimestamp>> RecognizeWordsAsync(
        string audioPath,
        string languageCode = "vi-VN",
        CancellationToken ct = default)
    {
        if (!System.IO.File.Exists(audioPath))
            throw new FileNotFoundException($"Không tìm thấy file audio: {audioPath}");

        var client = GetClient();
        var projectId = _authService.ProjectId;

        // Đọc file audio
        var audioBytes = await System.IO.File.ReadAllBytesAsync(audioPath, ct);

        // Xác định encoding dựa trên extension
        var ext = Path.GetExtension(audioPath).ToLowerInvariant();

        // Cấu hình nhận diện với word-level timestamps
        var config = new RecognitionConfig
        {
            AutoDecodingConfig = new AutoDetectDecodingConfig(),
            LanguageCodes = { languageCode },
            Model = "chirp_2",
            Features = new RecognitionFeatures
            {
                EnableWordTimeOffsets = true,
                EnableWordConfidence = true
            }
        };

        // Tạo recognizer name theo format của Speech V2 API
        var recognizerName = $"projects/{projectId}/locations/global/recognizers/_";

        var request = new RecognizeRequest
        {
            Recognizer = recognizerName,
            Config = config,
            Content = ByteString.CopyFrom(audioBytes)
        };

        var response = await client.RecognizeAsync(request, ct);

        var words = new List<WordTimestamp>();

        foreach (var result in response.Results)
        {
            if (result.Alternatives.Count == 0) continue;

            var bestAlt = result.Alternatives[0];
            foreach (var wordInfo in bestAlt.Words)
            {
                words.Add(new WordTimestamp
                {
                    Word = wordInfo.Word,
                    StartTime = wordInfo.StartOffset.ToTimeSpan().TotalSeconds,
                    EndTime = wordInfo.EndOffset.ToTimeSpan().TotalSeconds,
                    Confidence = wordInfo.Confidence
                });
            }
        }

        return words;
    }

    /// <summary>
    /// Nhận diện từng batch scene audio files song song để tăng tốc.
    /// </summary>
    public async Task<Dictionary<int, List<WordTimestamp>>> RecognizeBatchAsync(
        IReadOnlyList<(int SceneIndex, string AudioPath)> scenes,
        string languageCode = "vi-VN",
        IProgress<(int Current, int Total, string Message)>? progress = null,
        CancellationToken ct = default)
    {
        var results = new Dictionary<int, List<WordTimestamp>>();
        var total = scenes.Count;

        // Xử lý tuần tự để tránh rate limit
        for (int i = 0; i < total; i++)
        {
            ct.ThrowIfCancellationRequested();
            var (sceneIndex, audioPath) = scenes[i];

            progress?.Report((i + 1, total,
                $"Đang nhận diện từ cho cảnh {sceneIndex + 1}..."));

            try
            {
                var words = await RecognizeWordsAsync(audioPath, languageCode, ct);
                results[sceneIndex] = words;
            }
            catch (Exception ex)
            {
                // Nếu lỗi 1 scene, bỏ qua và tiếp tục
                progress?.Report((i + 1, total,
                    $"⚠️ Cảnh {sceneIndex + 1}: Không thể nhận diện ({ex.Message})"));
                results[sceneIndex] = new List<WordTimestamp>();
            }
        }

        return results;
    }
}

/// <summary>
/// Lưu trữ thông tin timestamp chính xác của từng từ trong audio.
/// Được sử dụng để tạo hiệu ứng phụ đề karaoke nhảy từng chữ.
/// </summary>
public class WordTimestamp
{
    /// <summary>Từ/chữ được nhận diện</summary>
    public string Word { get; set; } = string.Empty;

    /// <summary>Thời điểm bắt đầu phát âm (giây)</summary>
    public double StartTime { get; set; }

    /// <summary>Thời điểm kết thúc phát âm (giây)</summary>
    public double EndTime { get; set; }

    /// <summary>Độ tin cậy nhận diện (0.0 - 1.0)</summary>
    public float Confidence { get; set; }

    /// <summary>Thời lượng phát âm từ này (giây)</summary>
    public double Duration => EndTime - StartTime;
}
