using System.IO;
using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace VideoAutoWpf.Models.V2;

/// <summary>
/// Đại diện cho một Shot (góc quay / khung hình) cụ thể bên trong một Scene.
/// Trong V2, một Scene có thể chứa 1 hoặc nhiều Shots để tránh video bị cảm giác slideshow.
/// Thời lượng của Shot được phân bổ theo nhịp điệu (Pacing Weight) và thời lượng audio thực tế của Scene.
/// </summary>
public partial class Shot : ObservableObject
{
    [ObservableProperty]
    [JsonPropertyName("id")]
    private string _id = Guid.NewGuid().ToString("N");

    [ObservableProperty]
    [JsonPropertyName("scene_id")]
    private string _sceneId = string.Empty;

    [ObservableProperty]
    [JsonPropertyName("shot_code")]
    private string _shotCode = "A"; // 01A, 01B, 01C...

    [ObservableProperty]
    [JsonPropertyName("location_id")]
    private string? _locationId;

    [ObservableProperty]
    [JsonPropertyName("location_name")]
    private string? _locationName;

    [JsonPropertyName("character_ids")]
    public List<string> CharacterIds { get; set; } = new();

    [ObservableProperty]
    [JsonPropertyName("start_offset_seconds")]
    private double _startOffsetSeconds;

    [ObservableProperty]
    [JsonPropertyName("duration_seconds")]
    private double _durationSeconds = 3.0;

    [ObservableProperty]
    [JsonPropertyName("pacing_weight")]
    private double _pacingWeight = 1.0; // Trọng số thời lượng: 0.6 = ngắn/nhanh, 1.0 = bình thường, 1.4 = cận cảnh lắng đọng/bối cảnh rộng

    [ObservableProperty]
    [JsonPropertyName("is_key_moment")]
    private bool _isKeyMoment; // Đánh dấu shot này là khoảnh khắc cao trào / nút thắt chính

    [ObservableProperty]
    [JsonPropertyName("shot_type")]
    private string _shotType = "medium_shot"; // wide, medium, close_up, extreme_close_up, pov

    [ObservableProperty]
    [JsonPropertyName("camera_movement")]
    private string _cameraMovement = "zoom_in"; // zoom_in, zoom_out, pan_left_right, pan_right_left, pan_up, pan_down, none

    [ObservableProperty]
    [JsonPropertyName("visual_description")]
    private string _visualDescription = string.Empty;

    [ObservableProperty]
    [JsonPropertyName("narrative_beat_description")]
    private string _narrativeBeatDescription = string.Empty; // Mô tả hành động/khoảnh khắc lời thoại tương ứng

    [ObservableProperty]
    [JsonPropertyName("visual_spec")]
    private VisualSpec? _visualSpec;

    [ObservableProperty]
    [JsonPropertyName("compiled_image_prompt")]
    private string _compiledImagePrompt = string.Empty;

    [ObservableProperty]
    [JsonPropertyName("image_path")]
    private string? _imagePath;

    [ObservableProperty]
    [JsonPropertyName("thumbnail_path")]
    private string? _thumbnailPath;

    [ObservableProperty]
    [JsonPropertyName("status")]
    private string _status = "Pending"; // Pending, Generating, Ready, Failed, Rejected

    [ObservableProperty]
    [JsonPropertyName("error_message")]
    private string? _errorMessage;

    [ObservableProperty]
    [JsonPropertyName("quality_score")]
    private double _qualityScore = 1.0;

    [ObservableProperty]
    private bool _isGenerating;

    public bool HasImage => !string.IsNullOrEmpty(ImagePath) && File.Exists(ImagePath);

    public string ImageFileName => string.IsNullOrEmpty(ImagePath) ? "Chưa có ảnh" : Path.GetFileName(ImagePath);

    public string DurationFormatted => DurationSeconds > 0 
        ? TimeSpan.FromSeconds(DurationSeconds).ToString(@"ss\.f") + "s"
        : "0.0s";

    public string DisplayTitle => $"Shot {ShotCode} ({DurationFormatted} • {ShotType})";

    partial void OnImagePathChanged(string? value)
    {
        OnPropertyChanged(nameof(HasImage));
        OnPropertyChanged(nameof(ImageFileName));
    }

    partial void OnDurationSecondsChanged(double value)
    {
        OnPropertyChanged(nameof(DurationFormatted));
        OnPropertyChanged(nameof(DisplayTitle));
    }
}
