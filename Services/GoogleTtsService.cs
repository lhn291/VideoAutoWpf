using System.IO;
using Google.Cloud.TextToSpeech.V1;

namespace VideoAutoWpf.Services;

public class GoogleTtsService
{
    private readonly GoogleAuthService _authService;
    private TextToSpeechClient? _client;

    public static readonly IReadOnlyList<string> AvailableVoices = new[]
    {
        "vi-VN-Neural2-D", // Nam cao cấp (Mặc định - Truyền cảm, trầm ấm)
        "vi-VN-Neural2-A", // Nữ cao cấp (Truyền cảm, tự nhiên)
        "vi-VN-Wavenet-B", // Nam (Trầm)
        "vi-VN-Wavenet-A", // Nữ (Nhẹ nhàng)
        "vi-VN-Wavenet-C", // Nữ (Sáng)
        "vi-VN-Wavenet-D"  // Nam (Năng động)
    };

    public GoogleTtsService(GoogleAuthService authService)
    {
        _authService = authService;
    }

    private TextToSpeechClient GetClient()
    {
        if (_client != null) return _client;

        var cred = _authService.GetCredential();
        if (cred == null)
            throw new InvalidOperationException("Không thể khởi tạo Google TTS: Thiếu thông tin xác thực Service Account.");

        var builder = new TextToSpeechClientBuilder
        {
            Credential = cred
        };
        _client = builder.Build();
        return _client;
    }

    public async Task<string> SynthesizeSpeechAsync(
        string text, 
        string outputPath, 
        string voiceName = "vi-VN-Neural2-D", 
        string languageCode = "vi-VN",
        double speakingRate = 0.90,
        double pitch = 0.0,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(text))
            throw new ArgumentException("Nội dung thuyết minh không được để trống.", nameof(text));

        var client = GetClient();

        var input = new SynthesisInput { Text = text };

        var voice = new VoiceSelectionParams
        {
            LanguageCode = languageCode,
            Name = voiceName
        };

        var audioConfig = new AudioConfig
        {
            AudioEncoding = AudioEncoding.Mp3,
            SpeakingRate = Math.Clamp(speakingRate, 0.25, 2.0),
            Pitch = Math.Clamp(pitch, -20.0, 20.0)
        };

        var response = await client.SynthesizeSpeechAsync(input, voice, audioConfig, ct);

        var dir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        using (var output = File.Create(outputPath))
        {
            response.AudioContent.WriteTo(output);
        }

        return outputPath;
    }
}
