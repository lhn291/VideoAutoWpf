using CommunityToolkit.Mvvm.ComponentModel;
using VideoAutoWpf.Services;

namespace VideoAutoWpf.Models;

public partial class SceneItem : ObservableObject
{
    [ObservableProperty]
    private int _index = 1;

    /// <summary>
    /// Word-level timestamps từ Speech-to-Text Chirp 2.
    /// Dùng để render phụ đề karaoke nhảy từng chữ.
    /// </summary>
    public List<WordTimestamp>? WordTimestamps { get; set; }

    [ObservableProperty]
    private string? _text;

    [ObservableProperty]
    private string? _imagePrompt;

    [ObservableProperty]
    private string? _imagePath;

    [ObservableProperty]
    private string? _audioPath;

    [ObservableProperty]
    private double _durationSeconds = 0;

    [ObservableProperty]
    private string _status = "Chờ xử lý";

    [ObservableProperty]
    private string _motionEffect = "zoom_in";

    public string MotionEffectDisplayText
    {
        get
        {
            var opt = MotionEffectOption.GetAllSceneMotionOptions().FirstOrDefault(o => o.Id == MotionEffect);
            return opt != null ? $"{opt.Icon} {opt.Name}" : "🔍 Zoom In";
        }
    }

    partial void OnMotionEffectChanged(string value)
    {
        OnPropertyChanged(nameof(MotionEffectDisplayText));
    }

    [ObservableProperty]
    private bool _isGeneratingAudio;

    [ObservableProperty]
    private bool _isGeneratingImage;

    public string ImageFileName => string.IsNullOrEmpty(ImagePath) ? "Chưa có ảnh" : System.IO.Path.GetFileName(ImagePath);
    public string AudioFileName => string.IsNullOrEmpty(AudioPath) ? "Chưa có audio" : System.IO.Path.GetFileName(AudioPath);

    public string DurationFormatted => DurationSeconds > 0 
        ? TimeSpan.FromSeconds(DurationSeconds).ToString(@"mm\:ss\.f") 
        : "--:--";

    public bool HasImage => !string.IsNullOrEmpty(ImagePath) && System.IO.File.Exists(ImagePath);
    public bool HasAudio => !string.IsNullOrEmpty(AudioPath) && System.IO.File.Exists(AudioPath);

    partial void OnImagePathChanged(string? value)
    {
        OnPropertyChanged(nameof(ImageFileName));
        OnPropertyChanged(nameof(HasImage));
    }

    partial void OnAudioPathChanged(string? value)
    {
        OnPropertyChanged(nameof(AudioFileName));
        OnPropertyChanged(nameof(HasAudio));
    }

    partial void OnDurationSecondsChanged(double value)
    {
        OnPropertyChanged(nameof(DurationFormatted));
    }
}
