using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace VideoAutoWpf.Models;

public partial class SeriesPlan : ObservableObject
{
    [ObservableProperty]
    [JsonPropertyName("series_title")]
    private string _seriesTitle = string.Empty;

    [ObservableProperty]
    [JsonPropertyName("overall_premise")]
    private string _overallPremise = string.Empty;

    [ObservableProperty]
    [JsonPropertyName("character_bible")]
    private string _characterBible = string.Empty;

    [ObservableProperty]
    [JsonPropertyName("suggested_style")]
    private string _suggestedStyle = "dark-anime";

    [ObservableProperty]
    [JsonPropertyName("suggested_voice")]
    private string _suggestedVoice = "vi-VN-Wavenet-B";

    [JsonPropertyName("episodes")]
    public List<EpisodePlanItem> Episodes { get; set; } = new();
}

public partial class EpisodePlanItem : ObservableObject
{
    [ObservableProperty]
    [JsonPropertyName("episode_number")]
    private int _episodeNumber = 1;

    [ObservableProperty]
    [JsonPropertyName("episode_title")]
    private string _episodeTitle = string.Empty;

    [ObservableProperty]
    [JsonPropertyName("plot_beat")]
    private string _plotBeat = string.Empty;

    [ObservableProperty]
    [JsonPropertyName("episode_hook")]
    private string _episodeHook = string.Empty;

    [ObservableProperty]
    [JsonPropertyName("cliffhanger")]
    private string _cliffhanger = string.Empty;

    [ObservableProperty]
    [JsonPropertyName("suggested_scene_count")]
    private int _suggestedSceneCount = 5;
}
