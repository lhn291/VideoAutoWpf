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
    private string _charactersPresentText = string.Empty;

    [ObservableProperty]
    private string? _locationId;

    [ObservableProperty]
    private string _locationName = string.Empty;

    // ── Story Beat & Pacing (Phase B) ───────────────────────────────────────────
    /// <summary>
    /// Loại nhịp kể chuyện: Hook | Setup | Rising_Action | Clue_Reveal | Confrontation | Climax | Resolution | Cliffhanger.
    /// Điều khiển số Shot và trọng số thời lượng của phân cảnh này.
    /// </summary>
    [ObservableProperty]
    private string _storyBeat = "Setup";

    /// <summary>Loại nhịp điệu: Hook | Normal | Suspense | Climax.</summary>
    [ObservableProperty]
    private string _pacingType = "Normal";

    /// <summary>Ghi chú ngắn cho đạo diễn về nhịp điệu và cảm xúc.</summary>
    [ObservableProperty]
    private string _pacingNote = string.Empty;

    /// <summary>
    /// Thời lượng mục tiêu ước tính bởi AI (giây). 0 = để TTS thực tế quyết định.
    /// </summary>
    [ObservableProperty]
    private double _estimatedDurationHint = 0;

    /// <summary>Danh sách ID nhân vật từ Character Bible có mặt trong phân cảnh này.</summary>
    public List<string> CharacterIdsPresent { get; set; } = new();

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

    /// <summary>
    /// Danh sách các Shot (góc quay / khung hình) thuộc phân cảnh này trong Pipeline V2.
    /// </summary>
    [ObservableProperty]
    private System.Collections.ObjectModel.ObservableCollection<VideoAutoWpf.Models.V2.Shot> _shots = new();

    public bool HasMultipleShots => Shots.Count > 1;
    public int ShotsCount => Shots.Count;

    public void NotifyShotsUpdated()
    {
        OnPropertyChanged(nameof(HasMultipleShots));
        OnPropertyChanged(nameof(ShotsCount));
    }

    partial void OnShotsChanged(System.Collections.ObjectModel.ObservableCollection<VideoAutoWpf.Models.V2.Shot> value)
    {
        NotifyShotsUpdated();
    }
}
