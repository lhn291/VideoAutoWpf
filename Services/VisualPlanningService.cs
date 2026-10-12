using System.Collections.ObjectModel;
using System.IO;
using VideoAutoWpf.Models;
using VideoAutoWpf.Models.V2;

namespace VideoAutoWpf.Services;

/// <summary>
/// Service quản lý toàn bộ Visual Planning Studio:
/// - Phân tích kịch bản để phát hiện nhân vật và địa điểm
/// - Tạo ảnh nhân vật (portrait, full body)
/// - Tạo ảnh địa điểm
/// - Tạo ảnh style bible mẫu
/// - Tạo ảnh scene preview
/// - Quản lý versioning và không xóa asset đã duyệt
/// </summary>
public class VisualPlanningService
{
    private readonly VertexImagenService _imagenService;
    private readonly GeminiScriptService _geminiService;

    public VisualPlanningService(VertexImagenService imagenService, GeminiScriptService geminiService)
    {
        _imagenService = imagenService;
        _geminiService = geminiService;
    }

    // ════════════════════════════════════════════════════════════════════
    // PHÂN TÍCH KỊCH BẢN → NHÂN VẬT & ĐỊA ĐIỂM
    // ════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Đếm số scene dự kiến có từng nhân vật/địa điểm xuất hiện từ danh sách scenes.
    /// Cập nhật ExpectedSceneCount cho mỗi character và location.
    /// </summary>
    public void CalculateAssetUsageFromScenes(
        IEnumerable<CharacterProfile> characters,
        IEnumerable<LocationProfile> locations,
        IEnumerable<SceneItem> allScenes)
    {
        var sceneList = allScenes.ToList();

        // Đếm scene count cho nhân vật
        foreach (var ch in characters)
        {
            ch.ExpectedSceneCount = sceneList.Count(s =>
                s.CharacterIdsPresent.Contains(ch.Id) ||
                (s.CharactersPresentText?.Contains(ch.Name, StringComparison.OrdinalIgnoreCase) ?? false));
        }

        // Đếm scene count cho địa điểm
        foreach (var loc in locations)
        {
            loc.ExpectedSceneCount = sceneList.Count(s =>
                s.LocationId == loc.Id ||
                (s.LocationName?.Contains(loc.Name, StringComparison.OrdinalIgnoreCase) ?? false));
        }
    }

    /// <summary>
    /// Tạo bảng tổng hợp AssetUsageSummary từ scenes.
    /// </summary>
    public List<AssetUsageSummary> BuildUsageSummary(
        IEnumerable<CharacterProfile> characters,
        IEnumerable<LocationProfile> locations,
        IEnumerable<SceneItem> allScenes)
    {
        var sceneList = allScenes.ToList();
        var result = new List<AssetUsageSummary>();

        foreach (var ch in characters)
        {
            var usedInScenes = sceneList
                .Select((s, i) => (s, i))
                .Where(x => x.s.CharacterIdsPresent.Contains(ch.Id) ||
                            (x.s.CharactersPresentText?.Contains(ch.Name, StringComparison.OrdinalIgnoreCase) ?? false))
                .Select(x => x.i)
                .ToList();

            result.Add(new AssetUsageSummary
            {
                AssetId = ch.Id,
                AssetName = ch.DisplayTitle,
                AssetType = "Character",
                ExpectedUsageCount = usedInScenes.Count,
                RelatedSceneIndices = usedInScenes,
                ApprovalStatus = ch.ApprovalStatus
            });
        }

        foreach (var loc in locations)
        {
            var usedInScenes = sceneList
                .Select((s, i) => (s, i))
                .Where(x => x.s.LocationId == loc.Id ||
                            (x.s.LocationName?.Contains(loc.Name, StringComparison.OrdinalIgnoreCase) ?? false))
                .Select(x => x.i)
                .ToList();

            result.Add(new AssetUsageSummary
            {
                AssetId = loc.Id,
                AssetName = loc.DisplayTitle,
                AssetType = "Location",
                ExpectedUsageCount = usedInScenes.Count,
                RelatedSceneIndices = usedInScenes,
                ApprovalStatus = loc.ApprovalStatus
            });
        }

        return result.OrderByDescending(r => r.ExpectedUsageCount).ToList();
    }

    /// <summary>
    /// Chọn các scene đại diện để tạo preview dựa trên tiêu chí ưu tiên.
    /// </summary>
    public List<ScenePreviewItem> SelectRepresentativeScenes(
        IEnumerable<CharacterProfile> characters,
        IEnumerable<LocationProfile> locations,
        IEnumerable<SceneItem> allScenes,
        int episodeNumber = 1,
        int maxPreviews = 4)
    {
        var sceneList = allScenes.ToList();
        var result = new List<ScenePreviewItem>();
        var usedIndices = new HashSet<int>();

        var mainCharacter = characters.OrderByDescending(c => c.ExpectedSceneCount).FirstOrDefault();
        var mostUsedLocation = locations.OrderByDescending(l => l.ExpectedSceneCount).FirstOrDefault();

        // 1. Scene có nhân vật chính
        if (mainCharacter != null)
        {
            var idx = sceneList.FindIndex(s =>
                s.CharacterIdsPresent.Contains(mainCharacter.Id) ||
                (s.CharactersPresentText?.Contains(mainCharacter.Name, StringComparison.OrdinalIgnoreCase) ?? false));
            if (idx >= 0 && usedIndices.Add(idx))
            {
                result.Add(CreatePreviewItem(sceneList[idx], idx + 1, episodeNumber,
                    $"Scene có nhân vật chính: {mainCharacter.Name}",
                    new List<string> { mainCharacter.Id }, sceneList[idx].LocationId));
            }
        }

        // 2. Scene có nhiều nhân vật nhất
        var multiCharScene = sceneList
            .Select((s, i) => (s, i))
            .Where(x => !usedIndices.Contains(x.i) && x.s.CharacterIdsPresent.Count > 1)
            .OrderByDescending(x => x.s.CharacterIdsPresent.Count)
            .FirstOrDefault();
        if (multiCharScene.s != null && usedIndices.Add(multiCharScene.i))
        {
            result.Add(CreatePreviewItem(multiCharScene.s, multiCharScene.i + 1, episodeNumber,
                $"Scene có {multiCharScene.s.CharacterIdsPresent.Count} nhân vật",
                multiCharScene.s.CharacterIdsPresent, multiCharScene.s.LocationId));
        }

        // 3. Scene dùng địa điểm được dùng nhiều nhất
        if (mostUsedLocation != null)
        {
            var idx = sceneList.FindIndex(s =>
                !usedIndices.Contains(sceneList.IndexOf(s)) &&
                (s.LocationId == mostUsedLocation.Id ||
                 (s.LocationName?.Contains(mostUsedLocation.Name, StringComparison.OrdinalIgnoreCase) ?? false)));
            if (idx >= 0 && usedIndices.Add(idx))
            {
                result.Add(CreatePreviewItem(sceneList[idx], idx + 1, episodeNumber,
                    $"Scene tại địa điểm chủ chốt: {mostUsedLocation.Name}",
                    sceneList[idx].CharacterIdsPresent, sceneList[idx].LocationId));
            }
        }

        // 4. Scene có đạo cụ / manh mối (heuristic: tìm text có từ khóa)
        var clueKeywords = new[] { "manh mối", "vũ khí", "bằng chứng", "tang vật", "hiện trường", "dao", "súng", "máu", "thi thể" };
        var clueScene = sceneList
            .Select((s, i) => (s, i))
            .Where(x => !usedIndices.Contains(x.i) &&
                        clueKeywords.Any(k => x.s.Text?.Contains(k, StringComparison.OrdinalIgnoreCase) ?? false))
            .FirstOrDefault();
        if (clueScene.s != null && result.Count < maxPreviews && usedIndices.Add(clueScene.i))
        {
            result.Add(CreatePreviewItem(clueScene.s, clueScene.i + 1, episodeNumber,
                "Scene có đạo cụ / manh mối quan trọng",
                clueScene.s.CharacterIdsPresent, clueScene.s.LocationId));
        }

        // Fill nếu còn thiếu
        for (int i = 0; i < sceneList.Count && result.Count < maxPreviews; i++)
        {
            if (usedIndices.Add(i))
            {
                result.Add(CreatePreviewItem(sceneList[i], i + 1, episodeNumber,
                    "Scene đại diện", sceneList[i].CharacterIdsPresent, sceneList[i].LocationId));
            }
        }

        return result.Take(maxPreviews).ToList();
    }

    private static ScenePreviewItem CreatePreviewItem(SceneItem scene, int sceneIndex, int episodeNum,
        string reason, List<string> characterIds, string? locationId)
    {
        return new ScenePreviewItem
        {
            EpisodeNumber = episodeNum,
            SceneIndex = sceneIndex,
            SceneDescription = scene.Text ?? string.Empty,
            PreviewReason = reason,
            CharacterIds = characterIds.ToList(),
            LocationId = locationId,
            ImagePrompt = scene.ImagePrompt ?? string.Empty
        };
    }

    // ════════════════════════════════════════════════════════════════════
    // TẠO ẢNH NHÂN VẬT
    // ════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Tạo ảnh chân dung (portrait) cho nhân vật.
    /// Append vào danh sách Versions — không xóa ảnh cũ.
    /// </summary>
    public async Task<VisualAssetVersion> GenerateCharacterPortraitAsync(
        CharacterProfile character,
        string outputDir,
        string styleBiblePrefix,
        string aspectRatio = "1:1",
        Action<string>? onLog = null,
        CancellationToken ct = default)
    {
        character.ApprovalStatus = AssetApprovalStatus.Generating;

        var prompt = BuildCharacterPortraitPrompt(character, styleBiblePrefix);
        var fileName = $"char_{character.Id}_portrait_v{character.PortraitAsset.Versions.Count}.png";
        var outputPath = Path.Combine(outputDir, "characters", fileName);
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

        try
        {
            await _imagenService.GenerateImageAsync(prompt, outputPath, aspectRatio, onLog: onLog, ct: ct);

            character.PortraitAsset.Label = $"Chân dung — {character.Name}";
            var version = character.PortraitAsset.AddVersion(outputPath, prompt, $"Portrait v{character.PortraitAsset.Versions.Count}");
            character.ApprovalStatus = AssetApprovalStatus.AwaitingApproval;
            onLog?.Invoke($"✅ Đã tạo ảnh chân dung cho {character.Name}: {fileName}");
            return version;
        }
        catch (Exception ex)
        {
            character.ApprovalStatus = AssetApprovalStatus.Error;
            onLog?.Invoke($"❌ Lỗi tạo ảnh nhân vật {character.Name}: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Tạo ảnh toàn thân (full body) cho nhân vật.
    /// </summary>
    public async Task<VisualAssetVersion> GenerateCharacterFullBodyAsync(
        CharacterProfile character,
        string outputDir,
        string styleBiblePrefix,
        string aspectRatio = "2:3",
        Action<string>? onLog = null,
        CancellationToken ct = default)
    {
        var prompt = BuildCharacterFullBodyPrompt(character, styleBiblePrefix);
        var fileName = $"char_{character.Id}_fullbody_v{character.FullBodyAsset.Versions.Count}.png";
        var outputPath = Path.Combine(outputDir, "characters", fileName);
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

        await _imagenService.GenerateImageAsync(prompt, outputPath, aspectRatio, onLog: onLog, ct: ct);

        character.FullBodyAsset.Label = $"Toàn thân — {character.Name}";
        var version = character.FullBodyAsset.AddVersion(outputPath, prompt, $"FullBody v{character.FullBodyAsset.Versions.Count}");
        onLog?.Invoke($"✅ Đã tạo ảnh toàn thân cho {character.Name}: {fileName}");
        return version;
    }

    // ════════════════════════════════════════════════════════════════════
    // TẠO ẢNH ĐỊA ĐIỂM
    // ════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Tạo ảnh tổng thể (establishing shot) cho địa điểm.
    /// </summary>
    public async Task<VisualAssetVersion> GenerateLocationImageAsync(
        LocationProfile location,
        string outputDir,
        string styleBiblePrefix,
        string aspectRatio = "16:9",
        Action<string>? onLog = null,
        CancellationToken ct = default)
    {
        location.ApprovalStatus = AssetApprovalStatus.Generating;

        var prompt = BuildLocationPrompt(location, styleBiblePrefix);
        var fileName = $"loc_{location.Id}_v{location.LocationAsset.Versions.Count}.png";
        var outputPath = Path.Combine(outputDir, "locations", fileName);
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

        try
        {
            await _imagenService.GenerateImageAsync(prompt, outputPath, aspectRatio, onLog: onLog, ct: ct);

            location.LocationAsset.Label = $"Địa điểm — {location.Name}";
            var version = location.LocationAsset.AddVersion(outputPath, prompt, $"Location v{location.LocationAsset.Versions.Count}");
            location.ApprovalStatus = AssetApprovalStatus.AwaitingApproval;
            onLog?.Invoke($"✅ Đã tạo ảnh địa điểm {location.Name}: {fileName}");
            return version;
        }
        catch (Exception ex)
        {
            location.ApprovalStatus = AssetApprovalStatus.Error;
            onLog?.Invoke($"❌ Lỗi tạo ảnh địa điểm {location.Name}: {ex.Message}");
            throw;
        }
    }

    // ════════════════════════════════════════════════════════════════════
    // TẠO ẢNH STYLE BIBLE MẪU
    // ════════════════════════════════════════════════════════════════════

    public async Task<VisualAssetVersion> GenerateStyleSampleAsync(
        VisualStyleBible bible,
        string outputDir,
        string aspectRatio = "16:9",
        Action<string>? onLog = null,
        CancellationToken ct = default)
    {
        bible.Status = AssetApprovalStatus.Generating;

        var prompt = BuildStyleSamplePrompt(bible);
        bible.SampleAsset ??= new VisualAsset { AssetType = "StyleSample", Label = "Style Bible Sample" };

        var fileName = $"style_sample_v{bible.SampleAsset.Versions.Count}.png";
        var outputPath = Path.Combine(outputDir, "style", fileName);
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

        try
        {
            await _imagenService.GenerateImageAsync(prompt, outputPath, aspectRatio, onLog: onLog, ct: ct);
            var version = bible.SampleAsset.AddVersion(outputPath, prompt, "Style Bible Sample");
            bible.SampleAsset.Status = AssetApprovalStatus.AwaitingApproval;
            bible.Status = AssetApprovalStatus.AwaitingApproval;
            onLog?.Invoke($"✅ Đã tạo ảnh mẫu phong cách: {fileName}");
            return version;
        }
        catch (Exception ex)
        {
            bible.Status = AssetApprovalStatus.Error;
            onLog?.Invoke($"❌ Lỗi tạo ảnh style: {ex.Message}");
            throw;
        }
    }

    // ════════════════════════════════════════════════════════════════════
    // TẠO ẢNH SCENE PREVIEW
    // ════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Tạo ảnh preview cho một scene mẫu sử dụng đúng reference đã duyệt.
    /// </summary>
    public async Task<VisualAssetVersion> GenerateScenePreviewAsync(
        ScenePreviewItem preview,
        IEnumerable<CharacterProfile> allCharacters,
        IEnumerable<LocationProfile> allLocations,
        VisualStyleBible bible,
        string outputDir,
        string aspectRatio = "9:16",
        Action<string>? onLog = null,
        CancellationToken ct = default)
    {
        preview.Status = AssetApprovalStatus.Generating;

        // Chọn đúng nhân vật và địa điểm liên quan
        var relevantChars = allCharacters
            .Where(c => preview.CharacterIds.Contains(c.Id) && c.IsApproved)
            .ToList();
        var relevantLoc = allLocations
            .FirstOrDefault(l => l.Id == preview.LocationId && l.IsApproved);

        // Build prompt từ scene description + references đã duyệt
        var prompt = BuildScenePreviewPrompt(preview, relevantChars, relevantLoc, bible);
        preview.ImagePrompt = prompt;
        preview.PromptVersion++;

        var fileName = $"preview_ep{preview.EpisodeNumber}_s{preview.SceneIndex}_v{preview.PreviewAsset.Versions.Count}.png";
        var outputPath = Path.Combine(outputDir, "previews", fileName);
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

        try
        {
            await _imagenService.GenerateImageAsync(prompt, outputPath, aspectRatio, onLog: onLog, ct: ct);
            var version = preview.PreviewAsset.AddVersion(outputPath, prompt, $"Preview Tập {preview.EpisodeNumber} Cảnh {preview.SceneIndex}");
            preview.Status = AssetApprovalStatus.AwaitingApproval;
            onLog?.Invoke($"✅ Đã tạo ảnh preview Tập {preview.EpisodeNumber} Cảnh {preview.SceneIndex}: {fileName}");
            return version;
        }
        catch (Exception ex)
        {
            preview.Status = AssetApprovalStatus.Error;
            onLog?.Invoke($"❌ Lỗi tạo ảnh preview: {ex.Message}");
            throw;
        }
    }

    // ════════════════════════════════════════════════════════════════════
    // APPROVAL GATE VALIDATION
    // ════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Kiểm tra điều kiện Approval Gate. Trả về danh sách lỗi (rỗng = OK).
    /// </summary>
    public List<string> ValidateApprovalGate(
        IEnumerable<CharacterProfile> characters,
        IEnumerable<LocationProfile> locations,
        VisualStyleBible bible,
        IEnumerable<ScenePreviewItem> previews,
        int minPreviewsRequired = 2)
    {
        var errors = new List<string>();

        // Nhân vật quan trọng chưa được duyệt
        var blockedChars = characters.Where(c => c.IsCritical && !c.IsApproved).ToList();
        foreach (var ch in blockedChars)
            errors.Add($"❌ Nhân vật quan trọng chưa duyệt ảnh: {ch.Name} ({ch.StatusText})");

        // Địa điểm quan trọng chưa được duyệt
        var blockedLocs = locations.Where(l => l.IsCritical && !l.IsApproved).ToList();
        foreach (var loc in blockedLocs)
            errors.Add($"❌ Địa điểm quan trọng chưa duyệt ảnh: {loc.Name} ({loc.StatusText})");

        // Visual Style Bible chưa duyệt
        if (!bible.IsApproved)
            errors.Add($"❌ Visual Style Bible chưa được duyệt ({bible.StatusText})");

        // Scene preview chưa đủ
        var approvedPreviews = previews.Count(p => p.IsApproved);
        if (approvedPreviews < minPreviewsRequired)
            errors.Add($"❌ Cần duyệt ít nhất {minPreviewsRequired} scene mẫu (hiện có {approvedPreviews}/{minPreviewsRequired})");

        return errors;
    }

    // ════════════════════════════════════════════════════════════════════
    // PROMPT BUILDERS
    // ════════════════════════════════════════════════════════════════════

    private static string BuildCharacterPortraitPrompt(CharacterProfile ch, string stylePrefix)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(stylePrefix)) parts.Add(stylePrefix);
        parts.Add("portrait headshot, upper body");
        if (!string.IsNullOrWhiteSpace(ch.FaceDescription)) parts.Add(ch.FaceDescription);
        if (!string.IsNullOrWhiteSpace(ch.Hair)) parts.Add(ch.Hair);
        if (!string.IsNullOrWhiteSpace(ch.Skin)) parts.Add($"skin: {ch.Skin}");
        if (!string.IsNullOrWhiteSpace(ch.OutfitVn)) parts.Add($"wearing {ch.OutfitVn}");
        if (!string.IsNullOrWhiteSpace(ch.Accessories)) parts.Add(ch.Accessories);
        if (!string.IsNullOrWhiteSpace(ch.DefaultExpressions)) parts.Add($"expression: {ch.DefaultExpressions}");
        if (!string.IsNullOrWhiteSpace(ch.DistinguishingMarker)) parts.Add($"distinctive: {ch.DistinguishingMarker}");
        parts.Add("neutral background, studio lighting, high detail, photorealistic");
        parts.Add("no watermark, no text");
        return string.Join(", ", parts);
    }

    private static string BuildCharacterFullBodyPrompt(CharacterProfile ch, string stylePrefix)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(stylePrefix)) parts.Add(stylePrefix);
        parts.Add("full body shot, standing pose");
        if (!string.IsNullOrWhiteSpace(ch.FaceDescription)) parts.Add(ch.FaceDescription);
        if (!string.IsNullOrWhiteSpace(ch.Body)) parts.Add(ch.Body);
        if (!string.IsNullOrWhiteSpace(ch.Hair)) parts.Add(ch.Hair);
        if (!string.IsNullOrWhiteSpace(ch.OutfitVn)) parts.Add($"wearing {ch.OutfitVn}");
        if (!string.IsNullOrWhiteSpace(ch.Accessories)) parts.Add(ch.Accessories);
        if (!string.IsNullOrWhiteSpace(ch.DefaultPoses)) parts.Add(ch.DefaultPoses);
        parts.Add("plain background, full body visible, high detail");
        parts.Add("no watermark, no text");
        return string.Join(", ", parts);
    }

    private static string BuildLocationPrompt(LocationProfile loc, string stylePrefix)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(stylePrefix)) parts.Add(stylePrefix);
        parts.Add("wide establishing shot, architectural photography");
        if (!string.IsNullOrWhiteSpace(loc.ArchitecturalStyle)) parts.Add(loc.ArchitecturalStyle);
        if (!string.IsNullOrWhiteSpace(loc.InteriorDetails)) parts.Add(loc.InteriorDetails);
        if (!string.IsNullOrWhiteSpace(loc.ExteriorDetails)) parts.Add(loc.ExteriorDetails);
        if (!string.IsNullOrWhiteSpace(loc.SignatureColors)) parts.Add($"color palette: {loc.SignatureColors}");
        if (!string.IsNullOrWhiteSpace(loc.LightingDefault)) parts.Add($"{loc.LightingDefault} lighting");
        if (loc.KeyProps.Count > 0) parts.Add($"key props: {string.Join(", ", loc.KeyProps.Take(3))}");
        parts.Add("no people, highly detailed, cinematic, no watermark, no text");
        return string.Join(", ", parts);
    }

    private static string BuildStyleSamplePrompt(VisualStyleBible bible)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(bible.StyleDisplayName)) parts.Add(bible.StyleDisplayName);
        parts.Add("crime thriller scene, Vietnamese urban setting");
        if (!string.IsNullOrWhiteSpace(bible.LightingStyle)) parts.Add(bible.LightingStyle);
        if (!string.IsNullOrWhiteSpace(bible.ColorPalette)) parts.Add($"color: {bible.ColorPalette}");
        if (!string.IsNullOrWhiteSpace(bible.DepthOfField)) parts.Add(bible.DepthOfField);
        if (!string.IsNullOrWhiteSpace(bible.CameraStyle)) parts.Add(bible.CameraStyle);
        parts.Add("atmospheric, moody, cinematic quality, no text, no watermark");
        return string.Join(", ", parts);
    }

    private static string BuildScenePreviewPrompt(
        ScenePreviewItem preview,
        List<CharacterProfile> characters,
        LocationProfile? location,
        VisualStyleBible bible)
    {
        var parts = new List<string>();

        // Style bible prefix
        var biblePrefix = bible.BuildPromptPrefix();
        if (!string.IsNullOrWhiteSpace(biblePrefix)) parts.Add(biblePrefix);

        // Location reference
        if (location != null && !string.IsNullOrWhiteSpace(location.VisualPromptAnchor))
            parts.Add($"location: {location.VisualPromptAnchor}");
        else if (location != null)
            parts.Add($"setting: {location.Name}, {location.ArchitecturalStyle}");

        // Character references (tối đa 2 để tránh conflict)
        foreach (var ch in characters.Take(2))
        {
            if (!string.IsNullOrWhiteSpace(ch.DistinguishingMarker))
                parts.Add($"character: {ch.DistinguishingMarker}");
        }

        // Scene content
        if (!string.IsNullOrWhiteSpace(preview.SceneDescription))
        {
            var shortDesc = preview.SceneDescription.Length > 150
                ? preview.SceneDescription[..150] + "..."
                : preview.SceneDescription;
            parts.Add($"scene: {shortDesc}");
        }

        // Quality constraints
        parts.Add("cinematic, high detail, photorealistic, no watermark, no text");

        return string.Join(", ", parts);
    }
}
