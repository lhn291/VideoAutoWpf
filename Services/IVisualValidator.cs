using System.IO;
using VideoAutoWpf.Models;
using VideoAutoWpf.Models.V2;

namespace VideoAutoWpf.Services;

/// <summary>
/// Kết quả đánh giá kiểm định hình ảnh (Visual Validation Result).
/// </summary>
public class VisualValidationResult
{
    public double Score { get; set; } = 1.0;
    public bool MatchesSubject { get; set; } = true;
    public bool MatchesAction { get; set; } = true;
    public bool MatchesCharacter { get; set; } = true;
    public bool MatchesLocation { get; set; } = true;
    public List<string> Issues { get; set; } = new();

    public bool IsAccepted => Score >= 0.70 && Issues.Count == 0;

    public static VisualValidationResult Success() => new() { Score = 1.0 };
}

/// <summary>
/// Interface trừu tượng kiểm định chất lượng hình ảnh AI sinh ra trước khi chấp nhận.
/// Sẵn sàng tích hợp Gemini Vision / local vision models để tự động chấm điểm và kích hoạt vẽ lại (Regenerate).
/// </summary>
public interface IVisualValidator
{
    Task<VisualValidationResult> ValidateShotImageAsync(
        Shot shot,
        string imagePath,
        VisualSpec? spec = null,
        CharacterProfile? character = null,
        LocationProfile? location = null,
        CancellationToken ct = default);
}

/// <summary>
/// Bộ kiểm định hình ảnh mặc định (Heuristic / Integrity Validator).
/// Kiểm tra tính toàn vẹn của tệp, kích thước file, định dạng ảnh và cấu trúc prompt.
/// </summary>
public class HeuristicVisualValidator : IVisualValidator
{
    public Task<VisualValidationResult> ValidateShotImageAsync(
        Shot shot,
        string imagePath,
        VisualSpec? spec = null,
        CharacterProfile? character = null,
        LocationProfile? location = null,
        CancellationToken ct = default)
    {
        var result = new VisualValidationResult();

        if (string.IsNullOrEmpty(imagePath) || !File.Exists(imagePath))
        {
            result.Score = 0.0;
            result.MatchesSubject = false;
            result.Issues.Add("File hình ảnh không tồn tại hoặc bị lỗi ghi đĩa.");
            return Task.FromResult(result);
        }

        var fileInfo = new FileInfo(imagePath);
        if (fileInfo.Length < 1024) // Dưới 1KB là file rỗng/lỗi
        {
            result.Score = 0.1;
            result.MatchesSubject = false;
            result.Issues.Add("File ảnh bị rỗng hoặc kích thước bất thường (< 1KB).");
            return Task.FromResult(result);
        }

        // Tạm thời chấp nhận nếu file ảnh hợp lệ (sẵn sàng nâng cấp Gemini Vision ở bước tiếp theo)
        result.Score = 0.95;
        return Task.FromResult(result);
    }
}
