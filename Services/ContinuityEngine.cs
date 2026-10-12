using VideoAutoWpf.Models;
using VideoAutoWpf.Models.V2;

namespace VideoAutoWpf.Services;

/// <summary>
/// Kết quả kiểm tra tính nhất quán (Continuity Check Result).
/// </summary>
public class ContinuityCheckResult
{
    public bool IsConsistent { get; set; } = true;
    public List<string> Warnings { get; set; } = new();
    public List<string> EnforcedRules { get; set; } = new();
}

/// <summary>
/// Động cơ kiểm soát tính nhất quán (Continuity Engine).
/// Theo dõi và ngăn chặn các xung đột hình ảnh phi logic giữa các Shot và Scene kế tiếp:
/// - Trang phục, kiểu tóc, đặc điểm nhận diện của nhân vật (Character Continuity).
/// - Kiến trúc, màu sắc, không gian bối cảnh (Location Continuity).
/// - Thời điểm trong ngày (Time-of-day Continuity) và Đạo cụ then chốt (Key Props Continuity).
/// </summary>
public class ContinuityEngine
{
    /// <summary>
    /// Kiểm tra và tự động áp đặt các ràng buộc nhất quán (Enforce Continuity) vào VisualSpec trước khi biên dịch prompt.
    /// </summary>
    public ContinuityCheckResult EnforceContinuity(
        VisualSpec currentSpec,
        Shot currentShot,
        Shot? previousShot,
        Scene currentScene,
        Scene? previousScene,
        CharacterProfile? activeCharacter,
        LocationProfile? activeLocation)
    {
        var result = new ContinuityCheckResult();

        if (currentSpec == null) return result;

        // 1. CHARACTER CONTINUITY: Kiểm tra trang phục và tạo hình cố định
        if (activeCharacter != null)
        {
            var outfitRule = !string.IsNullOrWhiteSpace(activeCharacter.OutfitVn)
                ? activeCharacter.OutfitVn
                : activeCharacter.DistinguishingMarker;

            if (!string.IsNullOrWhiteSpace(outfitRule))
            {
                // Kiểm tra xem lời thoại có đề cập đến việc thay trang phục không
                bool hasWardrobeChange = currentScene.NarrationText.Contains("thay đồ", StringComparison.OrdinalIgnoreCase) ||
                                         currentScene.NarrationText.Contains("thay áo", StringComparison.OrdinalIgnoreCase) ||
                                         currentScene.NarrationText.Contains("đổi trang phục", StringComparison.OrdinalIgnoreCase);

                if (!hasWardrobeChange)
                {
                    var constraint = $"Strictly preserve character outfit: {outfitRule}";
                    if (!currentSpec.ContinuityConstraints.Contains(constraint))
                    {
                        currentSpec.ContinuityConstraints.Add(constraint);
                        result.EnforcedRules.Add($"Đồng bộ trang phục nhân vật: {activeCharacter.Name} -> {outfitRule}");
                    }

                    // Thêm negative constraint ngăn AI tự ý đổi màu áo
                    currentSpec.NegativeConstraints.Add("no changing clothes or different outfit color");
                }
            }

            // Đồng bộ tóc và gương mặt
            if (!string.IsNullOrWhiteSpace(activeCharacter.Hair))
            {
                currentSpec.ContinuityConstraints.Add($"Consistent hairstyle: {activeCharacter.Hair}");
            }
        }

        // 2. LOCATION CONTINUITY: Đồng bộ kiến trúc và màu sắc không gian
        if (activeLocation != null)
        {
            if (!string.IsNullOrWhiteSpace(activeLocation.SignatureColors))
            {
                currentSpec.ColorPalette = string.IsNullOrWhiteSpace(currentSpec.ColorPalette)
                    ? activeLocation.SignatureColors
                    : $"{currentSpec.ColorPalette}, {activeLocation.SignatureColors}";
                result.EnforcedRules.Add($"Đồng bộ bảng màu bối cảnh: {activeLocation.SignatureColors}");
            }

            if (!string.IsNullOrWhiteSpace(activeLocation.InteriorDetails) && currentSpec.Environment.Contains("interior", StringComparison.OrdinalIgnoreCase))
            {
                currentSpec.ContinuityConstraints.Add($"Maintain room interior: {activeLocation.InteriorDetails}");
            }
        }

        // 3. TIME OF DAY CONTINUITY: Giữ nguyên thời điểm ngày/đêm giữa các shot trong cùng 1 scene
        if (previousShot?.VisualSpec != null)
        {
            var prevTime = previousShot.VisualSpec.TimeOfDay;
            // Nếu cùng trong 1 scene, thời gian không được phép nhảy cóc (ví dụ đang đêm nhảy sang trưa)
            if (previousShot.SceneId == currentShot.SceneId && !string.IsNullOrWhiteSpace(prevTime))
            {
                if (currentSpec.TimeOfDay != prevTime)
                {
                    result.Warnings.Add($"Phát hiện lệch thời điểm ({currentSpec.TimeOfDay} vs {prevTime}) trong cùng 1 scene. Tự động chuẩn hóa về: {prevTime}.");
                    currentSpec.TimeOfDay = prevTime;
                    result.EnforcedRules.Add($"Đồng bộ thời điểm trong ngày: {prevTime}");
                }
            }

            // Đồng bộ thời tiết trong cùng scene
            if (previousShot.SceneId == currentShot.SceneId && !string.IsNullOrWhiteSpace(previousShot.VisualSpec.Weather))
            {
                currentSpec.Weather = previousShot.VisualSpec.Weather;
            }
        }

        // 4. KEY PROPS CONTINUITY: Kế thừa đạo cụ liên hoàn
        if (previousShot?.VisualSpec?.KeyProps != null && previousShot.VisualSpec.KeyProps.Count > 0)
        {
            foreach (var prop in previousShot.VisualSpec.KeyProps)
            {
                // Nếu đạo cụ là vật chứng quan trọng, duy trì trong shot tiếp theo nếu cùng bối cảnh
                if (!currentSpec.KeyProps.Contains(prop) && currentShot.SceneId == previousShot.SceneId)
                {
                    currentSpec.KeyProps.Add(prop);
                    result.EnforcedRules.Add($"Kế thừa đạo cụ: {prop}");
                }
            }
        }

        result.IsConsistent = result.Warnings.Count == 0;
        return result;
    }
}
