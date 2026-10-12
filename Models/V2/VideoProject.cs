using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace VideoAutoWpf.Models.V2;

/// <summary>
/// Root model đại diện cho toàn bộ dự án video trong kiến trúc V2.
/// Tích hợp đầy đủ Scenes (đa Shot), Tuyến nhân vật (Character Bible),
/// Hồ sơ bối cảnh (Location Bible), Timeline và cấu hình render.
/// </summary>
public partial class VideoProject : ObservableObject
{
    [ObservableProperty]
    [JsonPropertyName("id")]
    private string _id = Guid.NewGuid().ToString("N");

    [ObservableProperty]
    [JsonPropertyName("schema_version")]
    private int _schemaVersion = 2;

    [ObservableProperty]
    [JsonPropertyName("title")]
    private string _title = "Untitled Video Project";

    [ObservableProperty]
    [JsonPropertyName("aspect_ratio")]
    private string _aspectRatio = "9:16";

    [ObservableProperty]
    [JsonPropertyName("style_key")]
    private string _styleKey = "dark-anime";

    [JsonPropertyName("characters")]
    public ObservableCollection<CharacterProfile> Characters { get; set; } = new();

    [JsonPropertyName("locations")]
    public ObservableCollection<LocationProfile> Locations { get; set; } = new();

    [JsonPropertyName("story_blueprint")]
    public StoryBlueprint? StoryBlueprint { get; set; }

    [JsonPropertyName("content_analysis")]
    public ContentAnalysis? ContentAnalysis { get; set; }

    [JsonPropertyName("scenes")]
    public ObservableCollection<Scene> Scenes { get; set; } = new();

    [JsonPropertyName("assets")]
    public List<Asset> Assets { get; set; } = new();

    [ObservableProperty]
    [JsonPropertyName("render_settings")]
    private VideoConfig _renderSettings = new();

    [ObservableProperty]
    [JsonPropertyName("publish_info")]
    private VideoPublishInfo? _publishInfo = new();

    [ObservableProperty]
    [JsonPropertyName("created_at")]
    private DateTime _createdAt = DateTime.Now;

    [ObservableProperty]
    [JsonPropertyName("modified_at")]
    private DateTime _modifiedAt = DateTime.Now;

    public async Task SaveToFileAsync(string filePath)
    {
        ModifiedAt = DateTime.Now;
        var dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        };
        var json = JsonSerializer.Serialize(this, options);
        await File.WriteAllTextAsync(filePath, json);
    }

    public static async Task<VideoProject?> LoadFromFileAsync(string filePath)
    {
        if (!File.Exists(filePath)) return null;
        var json = await File.ReadAllTextAsync(filePath);
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
        return JsonSerializer.Deserialize<VideoProject>(json, options);
    }
}
