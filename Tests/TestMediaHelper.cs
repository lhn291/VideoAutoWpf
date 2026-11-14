using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace VideoAutoWpf.Tests;

public class VideoProbeInfo
{
    public bool HasVideoStream { get; set; }
    public bool HasAudioStream { get; set; }
    public bool HasAttachedPic { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public string VideoCodec { get; set; } = string.Empty;
    public string AudioCodec { get; set; } = string.Empty;
    public double Duration { get; set; }
    public long FileSize { get; set; }
}

public static class TestMediaHelper
{
    private static readonly string FfmpegPath = "ffmpeg";
    private static readonly string FfprobePath = "ffprobe";

    /// <summary>
    /// Tạo file ảnh test với kích thước và màu sắc bất kỳ bằng FFmpeg lavfi color filter.
    /// </summary>
    public static async Task CreateTestImageAsync(string outputPath, int width, int height, string color = "blue", CancellationToken ct = default)
    {
        var dir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        var args = $"-y -f lavfi -i color=c={color}:s={width}x{height}:d=1 -frames:v 1 \"{outputPath}\"";
        await RunProcessAsync(FfmpegPath, args, ct);

        if (!File.Exists(outputPath))
            throw new FileNotFoundException($"Không tạo được file ảnh test: {outputPath}");
    }

    /// <summary>
    /// Tạo file audio test (.aac / .wav) với thời lượng và tần số bất kỳ bằng FFmpeg sine filter.
    /// </summary>
    public static async Task CreateTestAudioAsync(string outputPath, double durationSeconds, int frequency = 440, CancellationToken ct = default)
    {
        var dir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        var durStr = durationSeconds.ToString("0.00", CultureInfo.InvariantCulture);
        var args = $"-y -f lavfi -i sine=frequency={frequency}:duration={durStr} -c:a aac -b:a 128k \"{outputPath}\"";
        await RunProcessAsync(FfmpegPath, args, ct);

        if (!File.Exists(outputPath))
            throw new FileNotFoundException($"Không tạo được file audio test: {outputPath}");
    }

    /// <summary>
    /// Dùng ffprobe để phân tích luồng video, kích thước, codec, thời lượng và metadata ảnh bìa (attached_pic).
    /// </summary>
    public static async Task<VideoProbeInfo> ProbeVideoAsync(string videoPath, CancellationToken ct = default)
    {
        if (!File.Exists(videoPath))
            throw new FileNotFoundException($"File video không tồn tại để probe: {videoPath}");

        var fileInfo = new FileInfo(videoPath);
        var args = $"-v error -show_streams -show_entries format=duration,size -of json \"{videoPath}\"";

        var jsonOutput = await RunProcessWithOutputAsync(FfprobePath, args, ct);
        var probe = new VideoProbeInfo { FileSize = fileInfo.Length };

        using var doc = JsonDocument.Parse(jsonOutput);
        var root = doc.RootElement;

        if (root.TryGetProperty("streams", out var streams) && streams.ValueKind == JsonValueKind.Array)
        {
            foreach (var stream in streams.EnumerateArray())
            {
                var codecType = stream.TryGetProperty("codec_type", out var ctProp) ? ctProp.GetString() : null;
                var codecName = stream.TryGetProperty("codec_name", out var cnProp) ? cnProp.GetString() : string.Empty;

                // Kiểm tra attached_pic (bìa poster nhúng vào container MP4)
                if (stream.TryGetProperty("disposition", out var dispProp))
                {
                    if (dispProp.TryGetProperty("attached_pic", out var apProp) && apProp.GetInt32() == 1)
                    {
                        probe.HasAttachedPic = true;
                    }
                }

                if (codecType == "video")
                {
                    // Nếu là video chính (không phải attached_pic)
                    if (probe.Width == 0 && stream.TryGetProperty("width", out var wProp))
                    {
                        probe.HasVideoStream = true;
                        probe.Width = wProp.GetInt32();
                        probe.Height = stream.GetProperty("height").GetInt32();
                        probe.VideoCodec = codecName ?? string.Empty;

                        if (stream.TryGetProperty("duration", out var durProp) &&
                            double.TryParse(durProp.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var dur))
                        {
                            probe.Duration = dur;
                        }
                    }
                }
                else if (codecType == "audio")
                {
                    probe.HasAudioStream = true;
                    probe.AudioCodec = codecName ?? string.Empty;
                }
            }
        }

        // Lấy thời lượng từ format nếu stream video chưa có
        if (probe.Duration <= 0 && root.TryGetProperty("format", out var formatProp))
        {
            if (formatProp.TryGetProperty("duration", out var fmtDurProp) &&
                double.TryParse(fmtDurProp.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var fmtDur))
            {
                probe.Duration = fmtDur;
            }
        }

        return probe;
    }

    private static async Task RunProcessAsync(string filename, string arguments, CancellationToken ct)
    {
        var psi = new ProcessStartInfo
        {
            FileName = filename,
            Arguments = arguments,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var proc = new Process { StartInfo = psi };
        proc.Start();

        var stderrTask = proc.StandardError.ReadToEndAsync(ct);
        await proc.WaitForExitAsync(ct);
        var stderr = await stderrTask;

        if (proc.ExitCode != 0)
        {
            throw new InvalidOperationException($"Lỗi chạy {filename} {arguments}:\n{stderr}");
        }
    }

    private static async Task<string> RunProcessWithOutputAsync(string filename, string arguments, CancellationToken ct)
    {
        var psi = new ProcessStartInfo
        {
            FileName = filename,
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8
        };

        using var proc = new Process { StartInfo = psi };
        proc.Start();

        var stdoutTask = proc.StandardOutput.ReadToEndAsync(ct);
        var stderrTask = proc.StandardError.ReadToEndAsync(ct);

        await proc.WaitForExitAsync(ct);
        var stdout = await stdoutTask;
        var stderr = await stderrTask;

        if (proc.ExitCode != 0)
        {
            throw new InvalidOperationException($"Lỗi chạy {filename} {arguments}:\n{stderr}");
        }

        return stdout;
    }
}
