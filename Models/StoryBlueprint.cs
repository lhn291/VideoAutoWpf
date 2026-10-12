using System.Collections.ObjectModel;
using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;
using VideoAutoWpf.Models.V2;

namespace VideoAutoWpf.Models;

/// <summary>
/// Tài liệu thiết kế câu chuyện thống nhất (Story Blueprint).
/// Dùng chung cho toàn bộ pipeline, định hình mạch truyện, nhân vật, bối cảnh
/// trước khi chia tập hay phân cảnh để bảo đảm tính liên tục (Continuity).
/// </summary>
public partial class StoryBlueprint : ObservableObject
{
    [ObservableProperty]
    [JsonPropertyName("id")]
    private string _id = Guid.NewGuid().ToString("N");

    [ObservableProperty]
    [JsonPropertyName("title")]
    private string _title = string.Empty;

    [ObservableProperty]
    [JsonPropertyName("core_theme")]
    private string _coreTheme = string.Empty;

    [ObservableProperty]
    [JsonPropertyName("main_message")]
    private string _mainMessage = string.Empty;

    [ObservableProperty]
    [JsonPropertyName("narrative_arc")]
    private string _narrativeArc = string.Empty; // Cung bậc cốt truyện: Mở đầu -> Thắt nút -> Cao trào -> Mở nút

    [JsonPropertyName("events_timeline")]
    public List<ChronologicalEvent> EventsTimeline { get; set; } = new();

    [JsonPropertyName("characters")]
    public ObservableCollection<CharacterProfile> Characters { get; set; } = new();

    [JsonPropertyName("locations")]
    public ObservableCollection<LocationProfile> Locations { get; set; } = new();

    [JsonPropertyName("must_keep_details")]
    public List<string> MustKeepDetails { get; set; } = new();

    [JsonPropertyName("suggested_episodes")]
    public List<BlueprintEpisodeOutline> SuggestedEpisodes { get; set; } = new();

    [JsonPropertyName("unresolved_points")]
    public List<string> UnresolvedPoints { get; set; } = new();

    [ObservableProperty]
    [JsonPropertyName("created_at")]
    private DateTime _createdAt = DateTime.Now;

    [ObservableProperty]
    [JsonPropertyName("modified_at")]
    private DateTime _modifiedAt = DateTime.Now;
}

/// <summary>
/// Dàn ý tập phim phân đoạn dựa trên cấu trúc câu chuyện (không chia đều cơ học).
/// </summary>
public class BlueprintEpisodeOutline
{
    [JsonPropertyName("episode_number")]
    public int EpisodeNumber { get; set; }

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("story_segment")]
    public string StorySegment { get; set; } = string.Empty;

    [JsonPropertyName("objective")]
    public string Objective { get; set; } = string.Empty;

    [JsonPropertyName("hook")]
    public string Hook { get; set; } = string.Empty;

    [JsonPropertyName("climax")]
    public string Climax { get; set; } = string.Empty;

    [JsonPropertyName("cliffhanger")]
    public string Cliffhanger { get; set; } = string.Empty;

    [JsonPropertyName("key_location_ids")]
    public List<string> KeyLocationIds { get; set; } = new();

    [JsonPropertyName("key_character_ids")]
    public List<string> KeyCharacterIds { get; set; } = new();
}
