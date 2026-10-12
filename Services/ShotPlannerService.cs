using System.Text.Json;
using VideoAutoWpf.Models;
using VideoAutoWpf.Models.V2;

namespace VideoAutoWpf.Services;

/// <summary>
/// Dịch vụ lập kế hoạch góc quay (Shot Planner Service).
/// Phân rã một Scene (lời thoại/âm thanh) thành 1 hoặc nhiều Shots dựa trên thời lượng,
/// mức độ phức tạp của hành động và nhịp điệu đạo diễn điện ảnh.
/// </summary>
public class ShotPlannerService
{
    private static readonly string[] DynamicCameraMovements = new[]
    {
        "zoom_in",
        "pan_left_right",
        "zoom_out",
        "pan_up",
        "pan_right_left",
        "pan_down"
    };

    /// <summary>
    /// Lập kế hoạch phân bổ Shot cho một Scene và trả về chính Scene đó.
    /// </summary>
    public Scene PlanShotsForScene(Scene scene)
    {
        PlanShotsRuleBased(scene);
        return scene;
    }

    /// <summary>
    /// Lập kế hoạch phân bổ Shot theo thuật toán phân nhịp nội dung (Story-Beat-Aware Shot Pacing).
    /// Ưu tiên dùng Story Beat (từ AI) để quyết định số Shot và trọng số thời lượng.
    /// Tránh chia đều cơ học — một Hook cảnh mở đầu 5 giây và một Climax 18 giây phải
    /// có số Shot và nhịp hoàn toàn khác nhau.
    /// </summary>
    public void PlanShotsRuleBased(Scene scene)
    {
        if (scene == null) return;

        // ── BƯỚC 1: Xác định tổng thời lượng ──────────────────────────────────────
        // Ưu tiên: audio thực tế > ước tính từ AI hint > ước tính từ số từ
        double totalDuration = scene.AudioDurationSeconds;
        if (totalDuration <= 0.5 && scene.EstimatedDurationSeconds > 0.5)
        {
            totalDuration = scene.EstimatedDurationSeconds;
        }
        if (totalDuration <= 0.5)
        {
            var wordCount = string.IsNullOrWhiteSpace(scene.NarrationText)
                ? 10
                : scene.NarrationText.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Length;
            totalDuration = Math.Max(4.0, wordCount / 2.8);
            scene.AudioDurationSeconds = totalDuration;
        }

        // ── BƯỚC 2: Quyết định số Shot theo Story Beat (ưu tiên) + thời lượng (phụ trợ) ──
        // Beat drives weight, NOT duration alone.
        int targetShotCount = GetTargetShotCountByBeat(scene.Purpose, totalDuration);

        scene.Shots.Clear();

        // ── BƯỚC 3: Danh sách góc máy theo Story Beat ──────────────────────────────
        var shotTemplates = GetShotSequenceTemplates(targetShotCount, scene.Purpose);

        // ── BƯỚC 4: Trọng số thời lượng theo Story Beat ────────────────────────────
        // Hook: 1 fast shot = all weight on single shot
        // Clue_Reveal / Confrontation: front-heavy — lingering establishing then reveal
        // Climax: pyramid — build up, peak, reaction
        double[] weights = GetPacingWeights(targetShotCount, scene.Purpose);

        double accumulatedOffset = 0.0;

        for (int i = 0; i < targetShotCount; i++)
        {
            var shotCode = ((char)('A' + i)).ToString();
            bool isLastShot = (i == targetShotCount - 1);

            // Shot cuối nhận phần dư để tổng khớp 100% với thời lượng audio thực tế
            double shotDuration = isLastShot 
                ? Math.Max(1.0, Math.Round(totalDuration - accumulatedOffset, 2))
                : Math.Max(1.0, Math.Round(totalDuration * weights[i], 2));

            // Camera movement: Climax/Hook shots get more intense movements
            var movement = GetCameraMovementForBeat(scene.Purpose, i, scene.Index);
            var (shotType, defaultDesc) = shotTemplates[i];

            var shot = new Shot
            {
                SceneId = scene.Id,
                ShotCode = shotCode,
                StartOffsetSeconds = accumulatedOffset,
                DurationSeconds = shotDuration,
                ShotType = shotType,
                CameraMovement = movement,
                VisualDescription = $"{defaultDesc} cho phân cảnh #{scene.Index} ({shotCode})",
                LocationId = scene.LocationId,
                LocationName = scene.LocationName,
                CharacterIds = new List<string>(scene.CharacterIdsPresent),
                PacingWeight = weights[i],
                IsKeyMoment = shotType.Contains("close_up") || scene.Purpose.Contains("Climax", StringComparison.OrdinalIgnoreCase),
                NarrativeBeatDescription = $"[{scene.Purpose}] {defaultDesc} — Cảnh #{scene.Index}"
            };

            scene.Shots.Add(shot);
            accumulatedOffset += shotDuration;
        }

        scene.Status = "Đã lập Shot Plan";
    }

    /// <summary>
    /// Quyết định số Shot dựa trên Story Beat trước, sau đó mới tính đến thời lượng.
    /// Hook cần ít shot nhưng impact cao. Climax/Confrontation cần nhiều shot để dựng nhịp.
    /// </summary>
    private static int GetTargetShotCountByBeat(string purpose, double totalDuration)
    {
        // Story-beat-driven shot count (primary factor)
        if (purpose.Contains("Hook", StringComparison.OrdinalIgnoreCase))
            return 1; // Hook phải nhanh và dứt khoát, luôn 1 shot

        if (purpose.Contains("Cliffhanger", StringComparison.OrdinalIgnoreCase))
            return 1; // Cliffhanger cuối tập cũng 1 shot dừng đột ngột

        if (purpose.Contains("Resolution", StringComparison.OrdinalIgnoreCase))
            return totalDuration <= 8.0 ? 1 : 2;

        if (purpose.Contains("Setup", StringComparison.OrdinalIgnoreCase) ||
            purpose.Contains("Rising_Action", StringComparison.OrdinalIgnoreCase))
        {
            if (totalDuration < 7.0) return 1;
            if (totalDuration <= 12.0) return 2;
            return 3;
        }

        if (purpose.Contains("Clue_Reveal", StringComparison.OrdinalIgnoreCase))
        {
            if (totalDuration <= 10.0) return 2;
            return 3; // Manh mối cần thêm shot cận cảnh chi tiết
        }

        if (purpose.Contains("Confrontation", StringComparison.OrdinalIgnoreCase) ||
            purpose.Contains("Climax", StringComparison.OrdinalIgnoreCase))
        {
            if (totalDuration < 8.0) return 2;
            if (totalDuration <= 14.0) return 3;
            return 4; // Cao trào cần nhiều shot để tạo nhịp dồn dập
        }

        // Fallback: thuần túy dựa trên thời lượng cho các beat không rõ ràng
        if (totalDuration < 5.0) return 1;
        if (totalDuration <= 9.0) return 2;
        if (totalDuration <= 15.0) return 3;
        return 4;
    }

    /// <summary>
    /// Trả về mảng trọng số thời lượng shot được tối ưu theo Story Beat.
    /// Tổng trọng số luôn = 1.0 (shot cuối nhận phần dư).
    /// </summary>
    private static double[] GetPacingWeights(int count, string purpose)
    {
        bool isClimaxBeat = purpose.Contains("Climax", StringComparison.OrdinalIgnoreCase) ||
                            purpose.Contains("Confrontation", StringComparison.OrdinalIgnoreCase);
        bool isClueReveal = purpose.Contains("Clue_Reveal", StringComparison.OrdinalIgnoreCase);
        bool isHookBeat = purpose.Contains("Hook", StringComparison.OrdinalIgnoreCase);

        return count switch
        {
            1 => new[] { 1.0 },
            2 when isClimaxBeat || isHookBeat
                => new[] { 0.38, 0.62 }, // Nhanh→Chậm: dồn dập rồi lắng đọng
            2 when isClueReveal
                => new[] { 0.45, 0.55 }, // Bối cảnh→Cận cảnh manh mối
            2   => new[] { 0.35, 0.65 }, // Thiết lập→Diễn biến chính
            3 when isClimaxBeat
                => new[] { 0.22, 0.55, 0.23 }, // Build-up (22%), Đỉnh điểm (55%), Hậu quả (23%)
            3 when isClueReveal
                => new[] { 0.30, 0.40, 0.30 }, // Môi trường→Đối tượng→Phản ứng nhân vật
            3   => new[] { 0.25, 0.50, 0.25 }, // Thiết lập→Hành động→Phản ứng
            4 when isClimaxBeat
                => new[] { 0.15, 0.42, 0.25, 0.18 }, // Đại cảnh→Đỉnh→Chi tiết→Shock reaction
            4   => new[] { 0.20, 0.45, 0.20, 0.15 },
            _   => Enumerable.Repeat(1.0 / count, count).ToArray()
        };
    }

    /// <summary>
    /// Chọn camera movement phù hợp với Story Beat và vị trí shot trong cảnh.
    /// </summary>
    private static string GetCameraMovementForBeat(string purpose, int shotIndex, int sceneIndex)
    {
        bool isClimaxBeat = purpose.Contains("Climax", StringComparison.OrdinalIgnoreCase) ||
                            purpose.Contains("Confrontation", StringComparison.OrdinalIgnoreCase);
        bool isHookBeat = purpose.Contains("Hook", StringComparison.OrdinalIgnoreCase);
        bool isClueReveal = purpose.Contains("Clue_Reveal", StringComparison.OrdinalIgnoreCase);

        if (isHookBeat) return "zoom_in"; // Hook luôn zoom in tạo impact

        if (isClimaxBeat)
        {
            // Climax: xen kẽ các chuyển động mạnh
            return shotIndex switch
            {
                0 => "zoom_out",      // Đại cảnh pull back tạo scope
                1 => "zoom_in",       // Zoom in vào đỉnh điểm
                2 => "pan_left_right", // Hậu quả, nhân vật di chuyển
                _ => "zoom_in"
            };
        }

        if (isClueReveal)
        {
            return shotIndex switch
            {
                0 => "pan_up",    // Reveal từ dưới lên (cận cảnh hiện trường)
                1 => "zoom_in",   // Zoom vào manh mối
                _ => "pan_down"   // Nhân vật nhìn xuống manh mối
            };
        }

        // Default: xoay vòng camera movements
        return DynamicCameraMovements[(sceneIndex + shotIndex) % DynamicCameraMovements.Length];
    }

    /// <summary>
    /// Biên dịch câu prompt ảnh độc lập cho từng Shot dựa trên góc máy, bối cảnh và nhân vật,
    /// tránh trùng lặp prompt cơ học giữa các shot trong cùng một phân cảnh.
    /// </summary>
    public void CompileShotPrompts(
        Scene scene,
        IEnumerable<CharacterProfile>? characters = null,
        LocationProfile? location = null,
        string styleKey = "dark-anime",
        string aspectRatio = "9:16")
    {
        if (scene?.Shots == null || scene.Shots.Count == 0) return;

        var basePrompt = !string.IsNullOrWhiteSpace(scene.VisualSpec?.Environment) 
            ? scene.VisualSpec.Environment 
            : scene.NarrationText;

        for (int i = 0; i < scene.Shots.Count; i++)
        {
            var shot = scene.Shots[i];
            
            // Xây dựng góc nhìn điện ảnh cụ thể cho từng shot
            string framingSpec = shot.ShotType switch
            {
                "wide_shot" or "extreme_wide" => "cinematic wide establishing view showing the complete environment",
                "close_up" or "extreme_close_up" => "intense dramatic close-up on expression and key detail",
                "over_shoulder" => "over-the-shoulder perspective framing the subject",
                _ => "cinematic medium shot capturing action and subject"
            };

            var spec = new VisualSpec
            {
                Subject = !string.IsNullOrWhiteSpace(scene.CharactersPresentText) && scene.CharactersPresentText != "🏙️ Ngoại cảnh / Hiện trường"
                    ? scene.CharactersPresentText
                    : (location?.Name ?? "The investigative environment"),
                Action = $"depicting '{scene.NarrationText}' from a {framingSpec}",
                Environment = location?.ArchitecturalStyle ?? "realistic investigative scene",
                Lighting = location?.LightingDefault ?? "dramatic cinematic lighting",
                Framing = shot.ShotType
            };

            if (location?.FixedElements != null)
            {
                foreach (var el in location.FixedElements)
                    spec.KeyProps.Add(el);
            }

            // Biên dịch qua PromptBuilder với Location Bible và Character Bible
            var activeChars = characters?.Where(c => scene.CharacterIdsPresent.Contains(c.Id));
            shot.CompiledImagePrompt = PromptBuilder.BuildPrompt(spec, styleKey, activeChars, location, aspectRatio);
            shot.VisualSpec = spec;
        }
    }

    /// <summary>
    /// Đồng bộ kế hoạch Shot cho tất cả các Scene trong VideoProject.
    /// </summary>
    public void PlanShotsForProject(VideoProject project)
    {
        if (project?.Scenes == null) return;
        foreach (var scene in project.Scenes)
        {
            PlanShotsRuleBased(scene);
        }
    }

    /// <summary>
    /// Chuỗi chuyển góc máy mẫu tùy biến theo mục đích kể chuyện của Scene.
    /// </summary>
    private static List<(string ShotType, string Description)> GetShotSequenceTemplates(int count, string purpose)
    {
        var list = new List<(string ShotType, string Description)>();

        switch (count)
        {
            case 1:
                list.Add(("medium_shot", "Toàn cảnh trọng tâm"));
                break;

            case 2:
                if (purpose.Contains("Hook", StringComparison.OrdinalIgnoreCase) || purpose.Contains("Climax", StringComparison.OrdinalIgnoreCase))
                {
                    list.Add(("close_up", "Cận cảnh cảm xúc / manh mối bất ngờ"));
                    list.Add(("wide_shot", "Toàn cảnh xung quanh"));
                }
                else
                {
                    list.Add(("wide_shot", "Thiết lập bối cảnh"));
                    list.Add(("medium_shot", "Hành động của nhân vật"));
                }
                break;

            case 3:
                list.Add(("wide_shot", "Bao quát không gian & bối cảnh"));
                list.Add(("medium_shot", "Tương tác và diễn biến"));
                list.Add(("close_up", "Chi tiết then chốt / Biểu cảm điểm nhấn"));
                break;

            case 4:
            default:
                list.Add(("extreme_wide", "Đại cảnh toàn cảnh không gian"));
                list.Add(("medium_shot", "Nhân vật xuất hiện trong bối cảnh"));
                list.Add(("close_up", "Hành động hoặc manh mối cụ thể"));
                list.Add(("over_shoulder", "Góc nhìn phản ứng và kết nối"));
                break;
        }

        return list;
    }
}
