using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace VideoAutoWpf.Models;

/// <summary>
/// Model phân tích nội dung có cấu trúc từ YouTube URL hoặc nguồn văn bản đầu vào.
/// Lưu trữ các sự kiện, mốc thời gian, nhân vật, bối cảnh, phân biệt tình tiết đã xác minh vs suy luận.
/// </summary>
public partial class ContentAnalysis : ObservableObject
{
    [ObservableProperty]
    [JsonPropertyName("id")]
    private string _id = Guid.NewGuid().ToString("N");

    [ObservableProperty]
    [JsonPropertyName("source_type")]
    private string _sourceType = "YouTube"; // "YouTube", "RawText", "FactCheck"

    [ObservableProperty]
    [JsonPropertyName("source_url")]
    private string? _sourceUrl;

    [ObservableProperty]
    [JsonPropertyName("source_title")]
    private string _sourceTitle = string.Empty;

    [ObservableProperty]
    [JsonPropertyName("main_topic")]
    private string _mainTopic = string.Empty;

    [ObservableProperty]
    [JsonPropertyName("core_message")]
    private string _coreMessage = string.Empty;

    [ObservableProperty]
    [JsonPropertyName("summary")]
    private string _summary = string.Empty;

    [JsonPropertyName("events")]
    public List<ChronologicalEvent> Events { get; set; } = new();

    [JsonPropertyName("detected_characters")]
    public List<DetectedEntity> DetectedCharacters { get; set; } = new();

    [JsonPropertyName("detected_locations")]
    public List<DetectedEntity> DetectedLocations { get; set; } = new();

    [JsonPropertyName("verified_facts")]
    public List<string> VerifiedFacts { get; set; } = new();

    [JsonPropertyName("unverified_claims")]
    public List<string> UnverifiedClaims { get; set; } = new();

    [ObservableProperty]
    [JsonPropertyName("has_transcript_timestamps")]
    private bool _hasTranscriptTimestamps;

    [ObservableProperty]
    [JsonPropertyName("created_at")]
    private DateTime _createdAt = DateTime.Now;
}

/// <summary>
/// Sự kiện diễn biến theo trình tự thời gian được bóc tách từ nội dung nguồn.
/// </summary>
public class ChronologicalEvent
{
    [JsonPropertyName("order")]
    public int Order { get; set; }

    [JsonPropertyName("time_marker")]
    public string TimeMarker { get; set; } = string.Empty; // e.g. "20:30 ngày 15/10", "12 phút trước vụ án"

    [JsonPropertyName("timestamp_seconds")]
    public double? TimestampSeconds { get; set; } // Mốc giây từ YouTube transcript nếu có

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("cause")]
    public string Cause { get; set; } = string.Empty;

    [JsonPropertyName("effect")]
    public string Effect { get; set; } = string.Empty;

    [JsonPropertyName("is_verified")]
    public bool IsVerified { get; set; } = true;

    [JsonPropertyName("source_quote")]
    public string SourceQuote { get; set; } = string.Empty;
}

/// <summary>
/// Thực thể (Nhân vật hoặc Địa điểm) được bóc tách từ nội dung nguồn.
/// </summary>
public class DetectedEntity
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("role_or_function")]
    public string RoleOrFunction { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;
}
