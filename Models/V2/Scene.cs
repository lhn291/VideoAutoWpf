using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;
using VideoAutoWpf.Services;

namespace VideoAutoWpf.Models.V2;

/// <summary>
/// Đại diện cho một phân cảnh kể chuyện (Scene) trong V2.
/// Scene chịu trách nhiệm về nội dung lời thoại (Narration), Audio thuyết minh,
/// và chứa danh sách các góc quay (Shots) phục vụ kể chuyện bằng hình ảnh linh hoạt.
/// </summary>
public partial class Scene : ObservableObject
{
    [ObservableProperty]
    [JsonPropertyName("id")]
    private string _id = Guid.NewGuid().ToString("N");

    [ObservableProperty]
    [JsonPropertyName("index")]
    private int _index = 1;

    [ObservableProperty]
    [JsonPropertyName("narration_text")]
    private string _narrationText = string.Empty;

    [ObservableProperty]
    [JsonPropertyName("purpose")]
    private string _purpose = "Narrative beat"; // Hook, Setup, Conflict, Climax, Cliffhanger

    [ObservableProperty]
    [JsonPropertyName("pacing_type")]
    private string _pacingType = "Normal"; // Hook (nhanh), Normal, Suspense (chậm), Climax (dồn dập)

    [ObservableProperty]
    [JsonPropertyName("character_ids_present")]
    private List<string> _characterIdsPresent = new();

    [ObservableProperty]
    [JsonPropertyName("characters_present_text")]
    private string _charactersPresentText = string.Empty;

    [ObservableProperty]
    [JsonPropertyName("location_id")]
    private string? _locationId;

    [ObservableProperty]
    [JsonPropertyName("location_name")]
    private string _locationName = string.Empty;

    [ObservableProperty]
    [JsonPropertyName("visual_spec")]
    private VisualSpec? _visualSpec;

    [ObservableProperty]
    [JsonPropertyName("image_prompt")]
    private string? _imagePrompt;

    [ObservableProperty]
    [JsonPropertyName("audio_path")]
    private string? _audioPath;

    [ObservableProperty]
    [JsonPropertyName("estimated_duration_seconds")]
    private double _estimatedDurationSeconds = 0; // Ước lượng trước khi có TTS

    [ObservableProperty]
    [JsonPropertyName("audio_duration_seconds")]
    private double _audioDurationSeconds = 0; // Đo thực tế từ file audio TTS

    /// <summary>
    /// Thời lượng cơ sở áp dụng cho Scene: Ưu tiên thời lượng audio thực tế sau TTS, nếu chưa có thì dùng thời lượng ước tính.
    /// </summary>
    public double EffectiveDurationSeconds => AudioDurationSeconds > 0.1 
        ? AudioDurationSeconds 
        : (EstimatedDurationSeconds > 0.1 ? EstimatedDurationSeconds : 5.0);

    [ObservableProperty]
    [JsonPropertyName("is_audio_stale")]
    private bool _isAudioStale; // Đánh dấu khi lời thoại bị sửa sau khi đã sinh audio

    [JsonPropertyName("word_timestamps")]
    public List<WordTimestamp>? WordTimestamps { get; set; }

    [JsonPropertyName("shots")]
    public ObservableCollection<Shot> Shots { get; set; } = new();

    [ObservableProperty]
    [JsonPropertyName("status")]
    private string _status = "Draft"; // Draft, Ready, Rendering, Completed, Failed

    [ObservableProperty]
    private bool _isGeneratingAudio;

    public bool HasAudio => !string.IsNullOrEmpty(AudioPath) && File.Exists(AudioPath);

    public string AudioFileName => string.IsNullOrEmpty(AudioPath) ? "Chưa có audio" : Path.GetFileName(AudioPath);

    public string DurationFormatted => EffectiveDurationSeconds > 0 
        ? TimeSpan.FromSeconds(EffectiveDurationSeconds).ToString(@"mm\:ss\.f") 
        : "--:--";

    public int TotalShotsCount => Shots.Count;

    public bool HasAllShotsReady => Shots.Count > 0 && Shots.All(s => s.HasImage);

    partial void OnAudioPathChanged(string? value)
    {
        OnPropertyChanged(nameof(HasAudio));
        OnPropertyChanged(nameof(AudioFileName));
    }

    partial void OnAudioDurationSecondsChanged(double value)
    {
        OnPropertyChanged(nameof(EffectiveDurationSeconds));
        OnPropertyChanged(nameof(DurationFormatted));
    }

    partial void OnEstimatedDurationSecondsChanged(double value)
    {
        OnPropertyChanged(nameof(EffectiveDurationSeconds));
        OnPropertyChanged(nameof(DurationFormatted));
    }

    partial void OnNarrationTextChanged(string value)
    {
        // Khi người dùng thay đổi lời thoại, nếu đã có audio thì đánh dấu cần tạo lại
        if (HasAudio)
        {
            IsAudioStale = true;
        }
    }
}
