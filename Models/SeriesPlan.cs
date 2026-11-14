using System.Collections.ObjectModel;
using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace VideoAutoWpf.Models;

public partial class SeriesPlan : ObservableObject
{
    [ObservableProperty]
    [property: JsonPropertyName("series_title")]
    private string _seriesTitle = string.Empty;

    [ObservableProperty]
    [property: JsonPropertyName("overall_premise")]
    private string _overallPremise = string.Empty;

    [ObservableProperty]
    [property: JsonPropertyName("character_bible")]
    private string _characterBible = string.Empty;

    [ObservableProperty]
    [property: JsonPropertyName("setting_bible")]
    private string _settingBible = string.Empty;

    [ObservableProperty]
    [property: JsonPropertyName("poster_prompt")]
    private string _posterPrompt = string.Empty;

    [ObservableProperty]
    [property: JsonPropertyName("suggested_style")]
    private string _suggestedStyle = "dark-anime";

    [ObservableProperty]
    [property: JsonPropertyName("suggested_voice")]
    private string _suggestedVoice = "vi-VN-Wavenet-B";

    [ObservableProperty]
    [property: JsonPropertyName("suggested_bgm")]
    private string _suggestedBgm = "dramatic";

    [JsonPropertyName("episodes")]
    public ObservableCollection<EpisodePlanItem> Episodes { get; set; } = new();
}

public partial class EpisodePlanItem : ObservableObject
{
    [ObservableProperty]
    [property: JsonPropertyName("episode_number")]
    private int _episodeNumber = 1;

    [ObservableProperty]
    [property: JsonPropertyName("episode_title")]
    private string _episodeTitle = string.Empty;

    [ObservableProperty]
    [property: JsonPropertyName("plot_beat")]
    private string _plotBeat = string.Empty;

    [ObservableProperty]
    [property: JsonPropertyName("episode_hook")]
    private string _episodeHook = string.Empty;

    [ObservableProperty]
    [property: JsonPropertyName("cliffhanger")]
    private string _cliffhanger = string.Empty;

    [ObservableProperty]
    [property: JsonPropertyName("suggested_scene_count")]
    private int _suggestedSceneCount = 5;
}
