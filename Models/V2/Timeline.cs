using System.Text.Json.Serialization;

namespace VideoAutoWpf.Models.V2;

/// <summary>
/// Đại diện cho Timeline đa rãnh (Multi-Track Timeline) được chuẩn bị sạch sẽ trước khi đưa vào FFmpeg.
/// Tách rời hoàn toàn business logic và storytelling ra khỏi FFmpeg rendering engine.
/// </summary>
public class Timeline
{
    [JsonPropertyName("total_duration_seconds")]
    public double TotalDurationSeconds { get; set; }

    [JsonPropertyName("video_clips")]
    public List<TimelineClip> VideoClips { get; set; } = new();

    [JsonPropertyName("audio_clips")]
    public List<TimelineAudioClip> AudioClips { get; set; } = new();

    [JsonPropertyName("bgm_clip")]
    public TimelineAudioClip? BgmClip { get; set; }

    [JsonPropertyName("subtitle_file_path")]
    public string? SubtitleFilePath { get; set; }

    [JsonPropertyName("subtitle_style")]
    public string SubtitleStyle { get; set; } = "cinematic";
}

/// <summary>
/// Đại diện cho một clip hình ảnh/video trên Video Track của Timeline.
/// </summary>
public class TimelineClip
{
    [JsonPropertyName("shot_id")]
    public string ShotId { get; set; } = string.Empty;

    [JsonPropertyName("media_path")]
    public string MediaPath { get; set; } = string.Empty;

    [JsonPropertyName("timeline_start_seconds")]
    public double TimelineStartSeconds { get; set; }

    [JsonPropertyName("duration_seconds")]
    public double DurationSeconds { get; set; }

    [JsonPropertyName("motion_effect")]
    public string MotionEffect { get; set; } = "zoom_in";

    [JsonPropertyName("shot_code")]
    public string ShotCode { get; set; } = "A";
}

/// <summary>
/// Đại diện cho một clip âm thanh (Voice thuyết minh hoặc Nhạc nền) trên Audio Track.
/// </summary>
public class TimelineAudioClip
{
    [JsonPropertyName("scene_id")]
    public string? SceneId { get; set; }

    [JsonPropertyName("audio_path")]
    public string AudioPath { get; set; } = string.Empty;

    [JsonPropertyName("timeline_start_seconds")]
    public double TimelineStartSeconds { get; set; }

    [JsonPropertyName("duration_seconds")]
    public double DurationSeconds { get; set; }

    [JsonPropertyName("volume")]
    public double Volume { get; set; } = 1.0;
}
