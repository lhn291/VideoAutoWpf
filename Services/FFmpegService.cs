using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using VideoAutoWpf.Models;

namespace VideoAutoWpf.Services;

public partial class FFmpegService : IFFmpegService
{
    private string? _cachedFfmpeg;
    private string? _cachedFfprobe;

    public string? LocateFfmpeg(string? customPath = null)
    {
        if (!string.IsNullOrWhiteSpace(customPath) && File.Exists(customPath))
            return customPath;

        if (!string.IsNullOrEmpty(_cachedFfmpeg) && File.Exists(_cachedFfmpeg))
            return _cachedFfmpeg;

        // 1. Kiểm tra PATH môi trường
        var fromPath = FindInPath("ffmpeg.exe");
        if (fromPath != null)
        {
            _cachedFfmpeg = fromPath;
            return fromPath;
        }

        // 2. Kiểm tra WinGet packages của Windows
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrEmpty(userProfile))
        {
            var wingetDir = Path.Combine(userProfile, "AppData", "Local", "Microsoft", "WinGet", "Packages");
            if (Directory.Exists(wingetDir))
            {
                var files = Directory.GetFiles(wingetDir, "ffmpeg.exe", SearchOption.AllDirectories);
                if (files.Length > 0)
                {
                    _cachedFfmpeg = files[0];
                    return files[0];
                }
            }
        }

        // 3. Kiểm tra ngay tại thư mục app
        var localExe = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ffmpeg.exe");
        if (File.Exists(localExe))
        {
            _cachedFfmpeg = localExe;
            return localExe;
        }

        return "ffmpeg";
    }

    public string? LocateFfprobe(string? customPath = null)
    {
        var ffmpeg = LocateFfmpeg(customPath);
        if (!string.IsNullOrEmpty(ffmpeg) && File.Exists(ffmpeg))
        {
            var dir = Path.GetDirectoryName(ffmpeg);
            if (!string.IsNullOrEmpty(dir))
            {
                var probePath = Path.Combine(dir, "ffprobe.exe");
                if (File.Exists(probePath))
                {
                    _cachedFfprobe = probePath;
                    return probePath;
                }
            }
        }

        var fromPath = FindInPath("ffprobe.exe");
        if (fromPath != null)
        {
            _cachedFfprobe = fromPath;
            return fromPath;
        }

        return "ffprobe";
    }

    private static string? FindInPath(string filename)
    {
        var pathEnv = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrEmpty(pathEnv)) return null;

        var paths = pathEnv.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);
        foreach (var p in paths)
        {
            try
            {
                var full = Path.Combine(p.Trim(), filename);
                if (File.Exists(full)) return full;
            }
            catch { }
        }
        return null;
    }

    public async Task<double> GetAudioDurationAsync(string audioPath, string? customFfmpeg = null, CancellationToken ct = default)
    {
        if (!File.Exists(audioPath))
            return 5.0;

        var ffprobe = LocateFfprobe(customFfmpeg);
        var psi = new ProcessStartInfo
        {
            FileName = ffprobe ?? "ffprobe",
            Arguments = $"-v error -show_entries format=duration -of default=noprint_wrappers=1:nokey=1 \"{audioPath}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8
        };

        try
        {
            using var process = Process.Start(psi);
            if (process == null) return 5.0;

            using var reg = ct.Register(() =>
            {
                try { if (!process.HasExited) process.Kill(); } catch { }
            });

            var output = await process.StandardOutput.ReadToEndAsync(ct);
            await process.WaitForExitAsync(ct);

            if (double.TryParse(output.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var duration) && duration > 0)
            {
                return duration;
            }
        }
        catch (Exception)
        {
            // Fallback bằng cách chạy ffmpeg -i và đọc Duration trong stderr
            var fallbackDuration = await GetAudioDurationFallbackAsync(audioPath, customFfmpeg, ct);
            if (fallbackDuration > 0) return fallbackDuration;
        }

        return 5.0;
    }

    private async Task<double> GetAudioDurationFallbackAsync(string audioPath, string? customFfmpeg, CancellationToken ct)
    {
        var ffmpeg = LocateFfmpeg(customFfmpeg);
        var psi = new ProcessStartInfo
        {
            FileName = ffmpeg ?? "ffmpeg",
            Arguments = $"-i \"{audioPath}\"",
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardErrorEncoding = Encoding.UTF8
        };

        try
        {
            using var process = Process.Start(psi);
            if (process == null) return 5.0;

            var stderr = await process.StandardError.ReadToEndAsync(ct);
            await process.WaitForExitAsync(ct);

            // Tìm chuỗi: Duration: 00:01:23.45
            var match = Regex.Match(stderr, @"Duration:\s*(\d+):(\d+):(\d+\.?\d*)");
            if (match.Success)
            {
                var hours = double.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
                var minutes = double.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
                var seconds = double.Parse(match.Groups[3].Value, CultureInfo.InvariantCulture);
                return hours * 3600 + minutes * 60 + seconds;
            }
        }
        catch { }

        return 5.0;
    }

    public async Task<string> GenerateVideoAsync(
        IReadOnlyList<SceneItem> scenes,
        VideoConfig config,
        IProgress<GenerationProgress>? progress = null,
        CancellationToken ct = default)
    {
        if (scenes == null || scenes.Count == 0)
            throw new ArgumentException("Danh sách phân cảnh trống, vui lòng thêm ít nhất 1 phân cảnh.");

        var ffmpeg = LocateFfmpeg(config.CustomFfmpegPath);
        if (string.IsNullOrEmpty(ffmpeg))
            throw new FileNotFoundException("Không tìm thấy FFmpeg trên hệ thống. Vui lòng cài đặt FFmpeg hoặc chỉ định đường dẫn.");

        // Thư mục đầu ra và thư mục tạm
        var outputDir = string.IsNullOrWhiteSpace(config.OutputDirectory)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyVideos), "VideoAutoOutput")
            : config.OutputDirectory;

        Directory.CreateDirectory(outputDir);

        var tempDir = Path.Combine(outputDir, $"temp_{DateTime.Now:yyyyMMdd_HHmmss}");
        Directory.CreateDirectory(tempDir);

        var finalOutputPath = Path.Combine(outputDir, config.OutputFileName);
        if (!finalOutputPath.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase))
            finalOutputPath += ".mp4";

        var (width, height) = config.GetDimensions();
        var tempSegmentFiles = new List<string>();

        try
        {
            progress?.Report(new GenerationProgress
            {
                Percentage = 5,
                StepTitle = "Kiểm tra tài nguyên & đo thời lượng",
                LogMessage = $"Bắt đầu xử lý {scenes.Count} phân cảnh (Tỷ lệ: {config.AspectRatio}, Độ phân giải: {width}x{height})..."
            });

            // Bước 1: Render từng phân cảnh
            for (int i = 0; i < scenes.Count; i++)
            {
                ct.ThrowIfCancellationRequested();
                var scene = scenes[i];
                var sceneNum = i + 1;

                if (string.IsNullOrEmpty(scene.ImagePath) || !File.Exists(scene.ImagePath))
                    throw new FileNotFoundException($"Không tìm thấy file ảnh cho phân cảnh #{sceneNum}: {scene.ImagePath}");

                if (string.IsNullOrEmpty(scene.AudioPath) || !File.Exists(scene.AudioPath))
                    throw new FileNotFoundException($"Không tìm thấy file âm thanh cho phân cảnh #{sceneNum}: {scene.AudioPath}");

                // Lấy thời lượng âm thanh nếu chưa có
                var duration = scene.DurationSeconds;
                if (duration <= 0.1)
                {
                    duration = await GetAudioDurationAsync(scene.AudioPath, config.CustomFfmpegPath, ct);
                    scene.DurationSeconds = duration;
                }

                var percent = 5 + (int)((i / (double)scenes.Count) * 65);
                progress?.Report(new GenerationProgress
                {
                    Percentage = percent,
                    StepTitle = $"Đang render phân cảnh {sceneNum}/{scenes.Count}",
                    LogMessage = $"[Cảnh {sceneNum}] Render ảnh '{Path.GetFileName(scene.ImagePath)}' khớp thoại ({duration:F2}s)..."
                });

                var segmentPath = Path.Combine(tempDir, $"segment_{sceneNum:D3}.mp4");
                await RenderSegmentAsync(ffmpeg, scene.ImagePath, scene.AudioPath, duration, width, height, config.EnableKenBurns, config.Fps, segmentPath, progress, ct);

                tempSegmentFiles.Add(segmentPath);
                scene.Status = "Hoàn thành";
            }

            // Bước 2: Ghép các phân đoạn thành 1 video (Concat)
            ct.ThrowIfCancellationRequested();
            progress?.Report(new GenerationProgress
            {
                Percentage = 75,
                StepTitle = "Ghép nối các phân cảnh",
                LogMessage = $"Đang nối {tempSegmentFiles.Count} phân cảnh video bằng FFmpeg Concat Demuxer..."
            });

            var concatListFile = Path.Combine(tempDir, "concat_list.txt");
            var sb = new StringBuilder();
            foreach (var seg in tempSegmentFiles)
            {
                // Chuẩn hóa đường dẫn dạng forward slash cho FFmpeg concat
                var safePath = seg.Replace('\\', '/');
                sb.AppendLine($"file '{safePath}'");
            }
            await File.WriteAllTextAsync(concatListFile, sb.ToString(), Encoding.UTF8, ct);

            var mergedVideoPath = Path.Combine(tempDir, "merged_video.mp4");
            await RunFfmpegAsync(ffmpeg, $"-y -f concat -safe 0 -i \"{concatListFile}\" -c copy \"{mergedVideoPath}\"", progress, ct);

            // Bước 3: Trộn nhạc nền (BGM) nếu có cấu hình
            ct.ThrowIfCancellationRequested();
            if (config.EnableBackgroundMusic && !string.IsNullOrEmpty(config.BackgroundMusicPath) && File.Exists(config.BackgroundMusicPath))
            {
                progress?.Report(new GenerationProgress
                {
                    Percentage = 88,
                    StepTitle = "Trộn nhạc nền (BGM)",
                    LogMessage = $"Đang trộn nhạc nền '{Path.GetFileName(config.BackgroundMusicPath)}' (Âm lượng: {(int)(config.BackgroundMusicVolume * 100)}%)..."
                });

                var volStr = config.BackgroundMusicVolume.ToString("0.00", CultureInfo.InvariantCulture);
                var filterComplex = $"\"[0:a]aformat=sample_rates=44100:channel_layouts=stereo,volume=1.0[a1];[1:a]aformat=sample_rates=44100:channel_layouts=stereo,volume={volStr}[a2];[a1][a2]amix=inputs=2:duration=first:dropout_transition=3[a]\"";
                var mixArgs = $"-y -i \"{mergedVideoPath}\" -i \"{config.BackgroundMusicPath}\" -filter_complex {filterComplex} -map 0:v -map \"[a]\" -c:v copy -c:a aac -b:a 192k \"{finalOutputPath}\"";
                await RunFfmpegAsync(ffmpeg, mixArgs, progress, ct);
            }
            else
            {
                // Không có BGM, copy file merged sang final output
                if (File.Exists(finalOutputPath)) File.Delete(finalOutputPath);
                File.Move(mergedVideoPath, finalOutputPath);
            }

            progress?.Report(new GenerationProgress
            {
                Percentage = 100,
                StepTitle = "Hoàn thành xuất sắc!",
                LogMessage = $"TẠO VIDEO THÀNH CÔNG!\nVideo đã lưu tại: {finalOutputPath}"
            });

            return finalOutputPath;
        }
        finally
        {
            // Dọn dẹp thư mục tạm
            try
            {
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, true);
                }
            }
            catch (Exception ex)
            {
                progress?.Report(new GenerationProgress
                {
                    LogMessage = $"Lưu ý: Không thể xóa thư mục tạm: {ex.Message}"
                });
            }
        }
    }

    private static async Task RenderSegmentAsync(
        string ffmpeg,
        string imagePath,
        string audioPath,
        double duration,
        int width,
        int height,
        bool enableKenBurns,
        int fps,
        string outputPath,
        IProgress<GenerationProgress>? progress,
        CancellationToken ct)
    {
        string filterString;
        var durStr = duration.ToString("0.000", CultureInfo.InvariantCulture);

        if (enableKenBurns)
        {
            var numFrames = Math.Max((int)(duration * fps), 1);
            var zoomSpeed = 0.10 / numFrames;
            var zoomSpeedStr = zoomSpeed.ToString("0.000000", CultureInfo.InvariantCulture);

            filterString = $"\"scale={width * 2}:{height * 2},zoompan=z='min(zoom+{zoomSpeedStr},1.10)':x='iw/2-(iw/zoom/2)':y='ih/2-(ih/zoom/2)':d={numFrames}:s={width}x{height},setsar=1\"";
        }
        else
        {
            filterString = $"\"scale={width}:{height},setsar=1\"";
        }

        var args = $"-y -loop 1 -i \"{imagePath}\" -i \"{audioPath}\" -c:v libx264 -tune stillimage -vf {filterString} -c:a aac -b:a 192k -pix_fmt yuv420p -t {durStr} \"{outputPath}\"";
        await RunFfmpegAsync(ffmpeg, args, progress, ct);
    }

    private static async Task RunFfmpegAsync(string ffmpeg, string arguments, IProgress<GenerationProgress>? progress, CancellationToken ct)
    {
        var psi = new ProcessStartInfo
        {
            FileName = ffmpeg,
            Arguments = arguments,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardErrorEncoding = Encoding.UTF8
        };

        using var process = new Process { StartInfo = psi };
        var errorOutput = new StringBuilder();

        process.ErrorDataReceived += (s, e) =>
        {
            if (!string.IsNullOrWhiteSpace(e.Data))
            {
                errorOutput.AppendLine(e.Data);
                // Có thể phân tích frame= hoặc time= để cập nhật % nhỏ nếu muốn
            }
        };

        if (!process.Start())
            throw new InvalidOperationException($"Không thể khởi chạy FFmpeg ({ffmpeg})");

        process.BeginErrorReadLine();

        using var reg = ct.Register(() =>
        {
            try { if (!process.HasExited) process.Kill(); } catch { }
        });

        await process.WaitForExitAsync(ct);

        if (process.ExitCode != 0)
        {
            throw new Exception($"Lỗi khi chạy FFmpeg (ExitCode {process.ExitCode}):\n{errorOutput}");
        }
    }
}
