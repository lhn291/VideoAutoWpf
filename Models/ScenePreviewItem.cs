using System.Collections.Generic;
using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace VideoAutoWpf.Models;

/// <summary>
/// Scene mẫu được tạo ra trong Visual Planning Studio để kiểm tra chất lượng
/// trước khi chạy production hàng loạt.
/// Mỗi ScenePreviewItem ánh xạ tới 1 SceneItem trong kịch bản + dùng các assets đã duyệt.
/// </summary>
public partial class ScenePreviewItem : ObservableObject
{
    [ObservableProperty]
    [JsonPropertyName("preview_id")]
    private string _previewId = Guid.NewGuid().ToString("N")[..8];

    /// <summary>Index tập và cảnh trong kịch bản gốc.</summary>
    [ObservableProperty]
    [JsonPropertyName("episode_number")]
    private int _episodeNumber;

    [ObservableProperty]
    [JsonPropertyName("scene_index")]
    private int _sceneIndex;

    [ObservableProperty]
    [JsonPropertyName("scene_description")]
    private string _sceneDescription = string.Empty;

    /// <summary>Lý do chọn scene này làm preview (nhân vật chính, nhiều nhân vật, địa điểm chủ chốt...).</summary>
    [ObservableProperty]
    [JsonPropertyName("preview_reason")]
    private string _previewReason = string.Empty;

    // ── Reference Mapping ───────────────────────────────────────────────────
    [JsonPropertyName("character_ids")]
    public List<string> CharacterIds { get; set; } = new();

    [ObservableProperty]
    [JsonPropertyName("location_id")]
    private string? _locationId;

    [ObservableProperty]
    [JsonPropertyName("visual_style_id")]
    private string? _visualStyleId;

    /// <summary>Prompt dùng để tạo ảnh preview (có thể sửa thủ công).</summary>
    [ObservableProperty]
    [JsonPropertyName("image_prompt")]
    private string _imagePrompt = string.Empty;

    [ObservableProperty]
    [JsonPropertyName("prompt_version")]
    private int _promptVersion = 1;

    // ── Asset ────────────────────────────────────────────────────────────────
    [ObservableProperty]
    [JsonPropertyName("preview_asset")]
    private VisualAsset _previewAsset = new() { AssetType = "ScenePreview" };

    // ── Quality Check ────────────────────────────────────────────────────────
    [ObservableProperty]
    [JsonPropertyName("quality_notes")]
    private string _qualityNotes = string.Empty;

    [ObservableProperty]
    [JsonPropertyName("status")]
    [NotifyPropertyChangedFor(nameof(IsApproved))]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    private AssetApprovalStatus _status = AssetApprovalStatus.Pending;

    [JsonIgnore]
    public bool IsApproved => Status == AssetApprovalStatus.Approved;

    [JsonIgnore]
    public string StatusText => Status.ToDisplayText();

    [JsonIgnore]
    public string? DisplayImagePath => PreviewAsset.DisplayImagePath;

    [JsonIgnore]
    public bool HasImage => PreviewAsset.HasImage;

    [JsonIgnore]
    public string DisplayTitle => $"Tập {EpisodeNumber} · Cảnh {SceneIndex}";
}

/// <summary>
/// Bảng tổng hợp asset tái sử dụng — thống kê tần suất xuất hiện của từng tài sản.
/// </summary>
public class AssetUsageSummary
{
    [JsonPropertyName("asset_id")]
    public string AssetId { get; set; } = string.Empty;

    [JsonPropertyName("asset_name")]
    public string AssetName { get; set; } = string.Empty;

    [JsonPropertyName("asset_type")]
    public string AssetType { get; set; } = string.Empty; // Character | Location | Object

    [JsonPropertyName("expected_usage_count")]
    public int ExpectedUsageCount { get; set; }

    [JsonPropertyName("related_scene_indices")]
    public List<int> RelatedSceneIndices { get; set; } = new();

    [JsonPropertyName("approval_status")]
    public AssetApprovalStatus ApprovalStatus { get; set; } = AssetApprovalStatus.Pending;

    [JsonIgnore]
    public string UsageText => $"{ExpectedUsageCount} cảnh";
}
