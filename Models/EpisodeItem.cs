using System.Collections.ObjectModel;
using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace VideoAutoWpf.Models;

public partial class EpisodeItem : ObservableObject
{
    [ObservableProperty]
    [JsonPropertyName("episode_number")]
    private int _episodeNumber = 1;

    [ObservableProperty]
    [JsonPropertyName("episode_title")]
    private string _episodeTitle = "Tập 1: Khởi Đầu Bí Ẩn";

    [ObservableProperty]
    [JsonPropertyName("episode_premise")]
    private string _episodePremise = string.Empty;

    [ObservableProperty]
    [JsonPropertyName("episode_hook")]
    private string _episodeHook = string.Empty;

    [ObservableProperty]
    [JsonPropertyName("cliffhanger")]
    private string _cliffhanger = string.Empty;

    [ObservableProperty]
    [JsonPropertyName("status")]
    private string _status = "Chưa dựng"; // Chưa dựng, Kịch bản sẵn sàng, Đang render, Hoàn thành

    [JsonPropertyName("scenes")]
    public ObservableCollection<SceneItem> Scenes { get; set; } = new();

    [ObservableProperty]
    [JsonPropertyName("publish_info")]
    private VideoPublishInfo _publishInfo = new();

    [ObservableProperty]
    [JsonPropertyName("output_video_path")]
    private string? _outputVideoPath;

    [ObservableProperty]
    [JsonPropertyName("drive_video_link")]
    private string? _driveVideoLink;

    [ObservableProperty]
    [JsonPropertyName("drive_folder_link")]
    private string? _driveFolderLink;

    [ObservableProperty]
    private bool _isUploadingDrive;

    [ObservableProperty]
    [JsonPropertyName("last_rendered_date")]
    private DateTime? _lastRenderedDate;

    [ObservableProperty]
    private bool _isSelected;

    public bool HasDriveLink => !string.IsNullOrEmpty(DriveVideoLink);

    public bool IsCompleted => !string.IsNullOrEmpty(OutputVideoPath) && System.IO.File.Exists(OutputVideoPath);

    public string StatusBadgeIcon => Status switch
    {
        "Hoàn thành" => "🟢",
        "Đang render" => "⏳",
        "Kịch bản sẵn sàng" => "🟡",
        _ => "⚪"
    };

    public string DisplayTitle => $"Tập {EpisodeNumber}: {(string.IsNullOrWhiteSpace(EpisodeTitle) ? "Không có tiêu đề" : EpisodeTitle)}";
    public string ShortTitle => $"Tập {EpisodeNumber}";

    public string ChipBackground => IsSelected ? "#313244" : "#181825";
    public string ChipBorder => IsSelected ? "#89b4fa" : "#313244";
    public string ChipForeground => IsSelected ? "#ffffff" : "#cdd6f4";

    partial void OnIsSelectedChanged(bool value)
    {
        OnPropertyChanged(nameof(ChipBackground));
        OnPropertyChanged(nameof(ChipBorder));
        OnPropertyChanged(nameof(ChipForeground));
    }

    partial void OnStatusChanged(string value)
    {
        OnPropertyChanged(nameof(StatusBadgeIcon));
    }

    partial void OnEpisodeNumberChanged(int value)
    {
        OnPropertyChanged(nameof(DisplayTitle));
    }

    partial void OnEpisodeTitleChanged(string value)
    {
        OnPropertyChanged(nameof(DisplayTitle));
    }

    partial void OnOutputVideoPathChanged(string? value)
    {
        OnPropertyChanged(nameof(IsCompleted));
    }

    partial void OnDriveVideoLinkChanged(string? value)
    {
        OnPropertyChanged(nameof(HasDriveLink));
    }
}
