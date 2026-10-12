using System.IO;
using System.Text.Json.Serialization;

namespace VideoAutoWpf.Models.V2;

/// <summary>
/// Đại diện cho một tài nguyên vật lý trong dự án (Asset Registry).
/// Quản lý vòng đời và ánh xạ giữa các file ảnh, âm thanh, phụ đề với từng Scene/Shot tương ứng.
/// </summary>
public class Asset
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    [JsonPropertyName("type")]
    public string Type { get; set; } = "image"; // image, audio, subtitle, video_segment, final_video

    [JsonPropertyName("path")]
    public string Path { get; set; } = string.Empty;

    [JsonPropertyName("source")]
    public string Source { get; set; } = "generated"; // generated, imported, stock

    [JsonPropertyName("prompt")]
    public string? Prompt { get; set; }

    [JsonPropertyName("related_scene_id")]
    public string? RelatedSceneId { get; set; }

    [JsonPropertyName("related_shot_id")]
    public string? RelatedShotId { get; set; }

    [JsonPropertyName("hash")]
    public string Hash { get; set; } = string.Empty;

    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    [JsonPropertyName("status")]
    public string Status { get; set; } = "Valid"; // Valid, Missing, Corrupted

    public bool Exists => !string.IsNullOrEmpty(Path) && File.Exists(Path);
}
