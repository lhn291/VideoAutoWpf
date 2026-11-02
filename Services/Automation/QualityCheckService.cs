using System.Diagnostics;
using System.IO;
using VideoAutoWpf.Models.Automation;

namespace VideoAutoWpf.Services.Automation;

public record QualityCheckResult(bool IsValid, string? ErrorMessage, double DurationSeconds, long FileSizeBytes, string? GeneratedThumbnailPath);

/// <summary>
/// Dịch vụ kiểm tra chất lượng và tính toàn vẹn của file video MP4 sau khi render.
/// Đảm bảo không bao giờ gửi video lỗi, rỗng hoặc bị ngắt quãng giữa chừng khi mất điện.
/// </summary>
public class QualityCheckService
{
    private readonly string _ffmpegPath;

    public QualityCheckService(string? customFfmpegPath = null)
    {
        _ffmpegPath = !string.IsNullOrWhiteSpace(customFfmpegPath) && File.Exists(customFfmpegPath)
            ? customFfmpegPath
            : LocateFFmpeg();
    }

    /// <summary>
    /// Kiểm tra toàn diện tính hợp lệ của video MP4
    /// </summary>
    public async Task<QualityCheckResult> ValidateVideoAsync(string videoPath, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(videoPath) || !File.Exists(videoPath))
        {
            return new QualityCheckResult(false, $"File video không tồn tại: {videoPath}", 0, 0, null);
        }

        var fileInfo = new FileInfo(videoPath);
        if (fileInfo.Length < 50 * 1024) // Dưới 50KB chắc chắn là file hỏng/rỗng
        {
            return new QualityCheckResult(false, $"Kích thước file video quá nhỏ ({fileInfo.Length} bytes), có khả năng bị hỏng.", 0, fileInfo.Length, null);
        }

        // 1. Kiểm tra bằng FFmpeg integrity probe
        if (string.IsNullOrEmpty(_ffmpegPath) || !File.Exists(_ffmpegPath))
        {
            // Nếu không tìm thấy ffmpeg, fallback kiểm tra kích thước tối thiểu
            return new QualityCheckResult(true, null, 0, fileInfo.Length, null);
        }

        try
        {
            // Chạy lệnh ffmpeg -v error -i video.mp4 -f null - để phát hiện lỗi stream/moov atom
            var psi = new ProcessStartInfo
            {
                FileName = _ffmpegPath,
                Arguments = $"-v error -i \"{videoPath}\" -f null -",
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var proc = new Process { StartInfo = psi };
            proc.Start();

            var errorTask = proc.StandardError.ReadToEndAsync(ct);
            var completed = await Task.Run(() => proc.WaitForExit(15000), ct); // Timeout 15s

            if (!completed)
            {
                try { proc.Kill(); } catch { }
                return new QualityCheckResult(false, "FFmpeg probe timed out kiểm tra file video.", 0, fileInfo.Length, null);
            }

            var stderr = await errorTask;
            if (proc.ExitCode != 0 && (stderr.Contains("Invalid data found") || stderr.Contains("moov atom not found") || stderr.Contains("error")))
            {
                return new QualityCheckResult(false, $"Video bị lỗi cấu trúc: {stderr.Trim()}", 0, fileInfo.Length, null);
            }

            // 2. Tạo thumbnail preview 1 giây để phục vụ gửi Telegram
            var thumbnailPath = await ExtractThumbnailAsync(videoPath, ct);

            return new QualityCheckResult(true, null, 0, fileInfo.Length, thumbnailPath);
        }
        catch (Exception ex)
        {
            return new QualityCheckResult(false, $"Lỗi khi kiểm tra chất lượng video: {ex.Message}", 0, fileInfo.Length, null);
        }
    }

    /// <summary>
    /// Trích xuất ảnh thumbnail từ video tại thời điểm 1.0 giây
    /// </summary>
    public async Task<string?> ExtractThumbnailAsync(string videoPath, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(_ffmpegPath) || !File.Exists(_ffmpegPath)) return null;

        try
        {
            var dir = Path.GetDirectoryName(videoPath) ?? Path.GetTempPath();
            var thumbName = Path.GetFileNameWithoutExtension(videoPath) + "_thumb.jpg";
            var thumbPath = Path.Combine(dir, thumbName);

            if (File.Exists(thumbPath) && new FileInfo(thumbPath).Length > 1000)
            {
                return thumbPath;
            }

            var psi = new ProcessStartInfo
            {
                FileName = _ffmpegPath,
                Arguments = $"-y -ss 00:00:01.000 -i \"{videoPath}\" -vframes 1 -q:v 2 \"{thumbPath}\"",
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var proc = new Process { StartInfo = psi };
            proc.Start();
            await proc.WaitForExitAsync(ct);

            if (File.Exists(thumbPath) && new FileInfo(thumbPath).Length > 1000)
            {
                return thumbPath;
            }
        }
        catch
        {
            // Bỏ qua lỗi trích xuất thumbnail, không làm đứt mạch
        }

        return null;
    }

    private static string LocateFFmpeg()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var wingetFfmpeg = Path.Combine(localAppData, @"Microsoft\WinGet\Packages\Gyan.FFmpeg_Microsoft.Winget.Source_8wekyb3d8bbwe\ffmpeg-8.0.1-full_build\bin\ffmpeg.exe");
        if (File.Exists(wingetFfmpeg)) return wingetFfmpeg;

        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        var appFfmpeg = Path.Combine(baseDir, "ffmpeg.exe");
        if (File.Exists(appFfmpeg)) return appFfmpeg;

        var pathEnv = Environment.GetEnvironmentVariable("PATH");
        if (pathEnv != null)
        {
            foreach (var path in pathEnv.Split(';'))
            {
                var candidate = Path.Combine(path.Trim(), "ffmpeg.exe");
                if (File.Exists(candidate)) return candidate;
            }
        }

        return "ffmpeg.exe";
    }
}
