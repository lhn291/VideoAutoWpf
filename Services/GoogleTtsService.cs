using System.IO;
using Google.Cloud.TextToSpeech.V1;

namespace VideoAutoWpf.Services;

public class GoogleTtsService
{
    private readonly GoogleAuthService _authService;
    private TextToSpeechClient? _client;

    public static readonly IReadOnlyList<string> AvailableVoices = new[]
    {
        "vi-VN-Wavenet-B", // Nam (Mặc định)
        "vi-VN-Wavenet-A", // Nữ
        "vi-VN-Wavenet-C", // Nữ
        "vi-VN-Wavenet-D", // Nam
        "vi-VN-Neural2-A", // Nữ cao cấp
        "vi-VN-Neural2-D"  // Nam cao cấp
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
        string voiceName = "vi-VN-Wavenet-B", 
        string languageCode = "vi-VN",
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
            AudioEncoding = AudioEncoding.Mp3
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
