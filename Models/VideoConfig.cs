namespace VideoAutoWpf.Models;

public class VideoConfig
{
    public string AspectRatio { get; set; } = "9:16"; // 9:16, 16:9, 1:1
    public string MotionEffect { get; set; } = "random"; // random, zoom_in, zoom_out, pan_left_right, pan_right_left, pan_up, pan_down, zoom_pan_right, zoom_pan_left, none
    public bool EnableKenBurns { get => MotionEffect != "none"; set { if (!value) MotionEffect = "none"; } }
    public bool EnableFadeTransition { get; set; } = true;
    public bool EnableVignette { get; set; } = false;
    public string? BackgroundMusicPath { get; set; }
    public double BackgroundMusicVolume { get; set; } = 0.15;
    public bool EnableBackgroundMusic { get; set; } = false;
    public string OutputDirectory { get; set; } = string.Empty;
    public string OutputFileName { get; set; } = "output_video.mp4";
    public string? CustomFfmpegPath { get; set; }
    public int Fps { get; set; } = 25;

    // Cấu hình chữ / phụ đề trên video
    public bool EnableSubtitles { get; set; } = true;
    public string SubtitleStyle { get; set; } = "cinematic"; // cinematic, boxed, viral_yellow, neon, gold, ticker
    public string SubtitleFont { get; set; } = "Segoe UI Bold";
    public int SubtitleFontSize { get; set; } = 0; // 0 = Auto theo độ phân giải video

    public (int width, int height) GetDimensions()
    {
        return AspectRatio switch
        {
            "16:9" => (1920, 1080),
            "1:1" => (1080, 1080),
            _ => (1080, 1920) // Default 9:16 portrait
        };
    }
}
