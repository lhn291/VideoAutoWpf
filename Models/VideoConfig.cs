namespace VideoAutoWpf.Models;

public class VideoConfig
{
    public string AspectRatio { get; set; } = "9:16"; // 9:16, 16:9, 1:1
    public bool EnableKenBurns { get; set; } = true;
    public string? BackgroundMusicPath { get; set; }
    public double BackgroundMusicVolume { get; set; } = 0.15;
    public bool EnableBackgroundMusic { get; set; } = false;
    public string OutputDirectory { get; set; } = string.Empty;
    public string OutputFileName { get; set; } = "output_video.mp4";
    public string? CustomFfmpegPath { get; set; }
    public int Fps { get; set; } = 25;

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
