using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace VideoAutoWpf.Models;

public partial class SeriesProject : ObservableObject
{
    [ObservableProperty]
    [JsonPropertyName("series_id")]
    private string _seriesId = Guid.NewGuid().ToString("N");

    [ObservableProperty]
    [JsonPropertyName("series_title")]
    private string _seriesTitle = "Dự Án Chuỗi Video Mới";

    [JsonIgnore]
    public string SeriesName
    {
        get => SeriesTitle;
        set => SeriesTitle = value;
    }

    [ObservableProperty]
    [JsonPropertyName("overall_premise")]
    private string _overallPremise = string.Empty;

    [ObservableProperty]
    [JsonPropertyName("total_episodes")]
    private int _totalEpisodes = 3;

    [ObservableProperty]
    [JsonPropertyName("global_style_key")]
    private string _globalStyleKey = "dark-anime";

    [ObservableProperty]
    [JsonPropertyName("global_voice")]
    private string _globalVoice = "vi-VN-Neural2-D";

    [ObservableProperty]
    [JsonPropertyName("aspect_ratio")]
    private string _aspectRatio = "9:16";

    [ObservableProperty]
    [JsonPropertyName("character_bible_rules")]
    private string _characterBibleRules = string.Empty;

    [ObservableProperty]
    [JsonPropertyName("global_bgm_path")]
    private string? _globalBgmPath;

    [JsonPropertyName("episodes")]
    public ObservableCollection<EpisodeItem> Episodes { get; set; } = new();

    /// <summary>
    /// Lưu cấu hình dự án Series thành file .json
    /// </summary>
    public async Task SaveToFileAsync(string filePath)
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        };
        var json = JsonSerializer.Serialize(this, options);
        await File.WriteAllTextAsync(filePath, json, System.Text.Encoding.UTF8);
    }

    /// <summary>
    /// Đọc cấu hình dự án Series từ file .json
    /// </summary>
    public static async Task<SeriesProject?> LoadFromFileAsync(string filePath)
    {
        if (!File.Exists(filePath)) return null;
        var json = await File.ReadAllTextAsync(filePath, System.Text.Encoding.UTF8);
        return JsonSerializer.Deserialize<SeriesProject>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });
    }

    /// <summary>
    /// Tạo dự án Series mặc định với số tập mong muốn
    /// </summary>
    public static SeriesProject CreateDefault(string title, int episodeCount = 3)
    {
        var proj = new SeriesProject
        {
            SeriesTitle = title,
            TotalEpisodes = episodeCount
        };

        for (int i = 1; i <= episodeCount; i++)
        {
            proj.Episodes.Add(new EpisodeItem
            {
                EpisodeNumber = i,
                EpisodeTitle = $"Tập {i}: Phần {i}",
                Status = "Chưa dựng"
            });
        }

        return proj;
    }
}
