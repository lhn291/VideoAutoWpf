using System.Text.Json.Serialization;

namespace VideoAutoWpf.Models;

public class ScriptWorkspace
{
    [JsonPropertyName("metadata")]
    public ScriptMetadata Metadata { get; set; } = new();

    [JsonPropertyName("scenes")]
    public List<ScriptScene> Scenes { get; set; } = new();

    [JsonPropertyName("characters")]
    public List<CharacterProfile> Characters { get; set; } = new();

    [JsonPropertyName("publish_info")]
    public VideoPublishInfo? PublishInfo { get; set; }
}

public class ScriptMetadata
{
    [JsonPropertyName("ratio")]
    public string Ratio { get; set; } = "9:16";

    [JsonPropertyName("voice")]
    public string Voice { get; set; } = "vi-VN-Neural2-D";

    [JsonPropertyName("speaking_rate")]
    public double SpeakingRate { get; set; } = 0.90;

    [JsonPropertyName("music")]
    public string? Music { get; set; }

    [JsonPropertyName("music_volume")]
    public double MusicVolume { get; set; } = 0.15;

    [JsonPropertyName("enable_music")]
    public bool EnableMusic { get; set; } = false;

    [JsonPropertyName("character_rules")]
    public string? CharacterRules { get; set; }

    [JsonPropertyName("motion_effect")]
    public string MotionEffect { get; set; } = "random";

    [JsonPropertyName("enable_fade")]
    public bool EnableFade { get; set; } = true;

    [JsonPropertyName("enable_vignette")]
    public bool EnableVignette { get; set; } = false;

    [JsonPropertyName("enable_subtitles")]
    public bool EnableSubtitles { get; set; } = true;

    [JsonPropertyName("subtitle_style")]
    public string SubtitleStyle { get; set; } = "cinematic";

    [JsonPropertyName("subtitle_font")]
    public string SubtitleFont { get; set; } = "Segoe UI Bold";
}

public class ScriptScene
{
    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;

    [JsonPropertyName("image_prompt")]
    public string ImagePrompt { get; set; } = string.Empty;

    [JsonPropertyName("motion_effect")]
    public string? MotionEffect { get; set; }

    // ── Characters & Location ──────────────────────────────────────────────
    /// <summary>Tên nhân vật xuất hiện trực tiếp trong phân cảnh (hiển thị UI)</summary>
    [JsonPropertyName("characters_present")]
    public List<string> CharactersPresent { get; set; } = new();

    /// <summary>Danh sách ID nhân vật từ Character Bible (khớp với CharacterProfile.Id)</summary>
    [JsonPropertyName("character_ids_present")]
    public List<string> CharacterIdsPresent { get; set; } = new();

    [JsonPropertyName("location_id")]
    public string? LocationId { get; set; }

    [JsonPropertyName("location_name")]
    public string? LocationName { get; set; }

    // ── Story Beat & Pacing (Phase B additions) ────────────────────────────
    /// <summary>
    /// Loại nhịp kể chuyện: Hook | Setup | Rising_Action | Clue_Reveal | Confrontation | Climax | Resolution | Cliffhanger
    /// Quyết định trọng số thời lượng và số Shot được phân bổ cho phân cảnh này.
    /// </summary>
    [JsonPropertyName("story_beat")]
    public string StoryBeat { get; set; } = "Setup";

    /// <summary>
    /// Loại nhịp điệu: Hook (nhanh, giật gân) | Normal | Suspense (chậm, căng thẳng) | Climax (dồn dập)
    /// </summary>
    [JsonPropertyName("pacing_type")]
    public string PacingType { get; set; } = "Normal";

    /// <summary>
    /// Gợi ý thời lượng mục tiêu tính bằng giây, do AI đề xuất dựa trên nhịp kể và số từ.
    /// 0 = để TTS thực tế quyết định.
    /// </summary>
    [JsonPropertyName("estimated_duration_hint")]
    public double EstimatedDurationHint { get; set; } = 0;

    /// <summary>Ghi chú ngắn cho đạo diễn về nhịp điệu và cảm xúc của phân cảnh này.</summary>
    [JsonPropertyName("pacing_note")]
    public string PacingNote { get; set; } = string.Empty;

    /// <summary>Mốc giây bắt đầu trong video nguồn (nếu phân tích từ transcript có timestamp).</summary>
    [JsonPropertyName("source_start_seconds")]
    public double? SourceStartSeconds { get; set; }

    /// <summary>Mốc giây kết thúc trong video nguồn (nếu phân tích từ transcript có timestamp).</summary>
    [JsonPropertyName("source_end_seconds")]
    public double? SourceEndSeconds { get; set; }

    [JsonPropertyName("engine")]
    public string? Engine { get; set; } = "imagen";
}
