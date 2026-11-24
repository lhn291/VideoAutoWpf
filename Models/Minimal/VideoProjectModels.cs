using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace VideoAutoWpf.Models.Minimal;

public enum StepStatus
{
    Waiting,
    Processing,
    Completed,
    Failed
}

public enum PipelineStep
{
    TopicInput = 1,
    Outline = 2,
    Script = 3,
    Voice = 4,
    Images = 5,
    Subtitles = 6,
    Render = 7,
    Completed = 8
}

public class OutlineSection
{
    [JsonPropertyName("heading")]
    public string Heading { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("estimated_seconds")]
    public int EstimatedSeconds { get; set; } = 10;
}

public class VideoOutline
{
    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("summary")]
    public string Summary { get; set; } = string.Empty;

    [JsonPropertyName("sections")]
    public List<OutlineSection> Sections { get; set; } = new();
}

public partial class VideoSceneItem : ObservableObject
{
    [JsonPropertyName("scene_number")]
    [ObservableProperty]
    private int _sceneNumber;

    [JsonPropertyName("narration_text")]
    [ObservableProperty]
    private string _narrationText = string.Empty;

    [JsonPropertyName("visual_prompt")]
    [ObservableProperty]
    private string _visualPrompt = string.Empty;

    [JsonPropertyName("audio_path")]
    [ObservableProperty]
    private string? _audioPath;

    [JsonPropertyName("image_path")]
    [ObservableProperty]
    private string? _imagePath;

    [JsonPropertyName("duration_seconds")]
    [ObservableProperty]
    private double _durationSeconds;

    [JsonPropertyName("is_audio_ready")]
    [ObservableProperty]
    private bool _isAudioReady;

    [JsonPropertyName("is_image_ready")]
    [ObservableProperty]
    private bool _isImageReady;

    [JsonIgnore]
    public bool HasImage => !string.IsNullOrEmpty(ImagePath) && File.Exists(ImagePath);

    [JsonIgnore]
    public bool HasAudio => !string.IsNullOrEmpty(AudioPath) && File.Exists(AudioPath);
}

public class VideoScript
{
    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("scenes")]
    public List<VideoSceneItem> Scenes { get; set; } = new();
}

public class VideoProjectData
{
    [JsonPropertyName("project_name")]
    public string ProjectName { get; set; } = string.Empty;

    [JsonPropertyName("project_dir")]
    public string ProjectDir { get; set; } = string.Empty;

    [JsonPropertyName("topic")]
    public string Topic { get; set; } = string.Empty;

    [JsonPropertyName("style")]
    public string Style { get; set; } = "realistic-photo";

    [JsonPropertyName("voice")]
    public string Voice { get; set; } = "vi-VN-Neural2-D";

    [JsonPropertyName("aspect_ratio")]
    public string AspectRatio { get; set; } = "9:16";

    [JsonPropertyName("enable_bgm")]
    public bool EnableBgm { get; set; } = true;

    [JsonPropertyName("bgm_track_id")]
    public string? BgmTrackId { get; set; } = "chill";

    [JsonPropertyName("bgm_volume")]
    public double BgmVolume { get; set; } = 0.15;

    [JsonPropertyName("enable_subtitles")]
    public bool EnableSubtitles { get; set; } = true;

    [JsonPropertyName("current_step")]
    public PipelineStep CurrentStep { get; set; } = PipelineStep.TopicInput;

    [JsonPropertyName("outline")]
    public VideoOutline? Outline { get; set; }

    [JsonPropertyName("script")]
    public VideoScript? Script { get; set; }

    [JsonPropertyName("voice_audio_path")]
    public string? VoiceAudioPath { get; set; }

    [JsonPropertyName("subtitle_path")]
    public string? SubtitlePath { get; set; }

    [JsonPropertyName("final_video_path")]
    public string? FinalVideoPath { get; set; }

    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    [JsonPropertyName("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.Now;

    public void Save(string projectDir)
    {
        ProjectDir = projectDir;
        UpdatedAt = DateTime.Now;
        Directory.CreateDirectory(projectDir);
        var jsonPath = Path.Combine(projectDir, "project.json");
        var options = new JsonSerializerOptions { WriteIndented = true };
        var json = JsonSerializer.Serialize(this, options);
        File.WriteAllText(jsonPath, json);
    }

    public static VideoProjectData? Load(string projectDir)
    {
        var jsonPath = Path.Combine(projectDir, "project.json");
        if (!File.Exists(jsonPath)) return null;
        var json = File.ReadAllText(jsonPath);
        return JsonSerializer.Deserialize<VideoProjectData>(json);
    }
}
