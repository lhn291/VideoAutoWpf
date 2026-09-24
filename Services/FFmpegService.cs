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

        // Thư mục đầu ra gốc
        var baseOutputDir = string.IsNullOrWhiteSpace(config.OutputDirectory)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyVideos), "VideoAutoOutput")
            : config.OutputDirectory;

        Directory.CreateDirectory(baseOutputDir);

        // Tạo thư mục project riêng cho mỗi video (tên video không .mp4)
        var videoName = Path.GetFileNameWithoutExtension(config.OutputFileName);
        if (string.IsNullOrWhiteSpace(videoName)) videoName = $"video_{DateTime.Now:yyyyMMdd_HHmmss}";

        // Sanitize tên folder: bỏ ký tự không hợp lệ
        foreach (var c in Path.GetInvalidFileNameChars())
            videoName = videoName.Replace(c, '_');

        var projectDir = Path.Combine(baseOutputDir, videoName);
        var imagesDir = Path.Combine(projectDir, "Images");
        var audioDir = Path.Combine(projectDir, "Audio");
        var videoDir = Path.Combine(projectDir, "Video");

        Directory.CreateDirectory(projectDir);
        Directory.CreateDirectory(imagesDir);
        Directory.CreateDirectory(audioDir);
        Directory.CreateDirectory(videoDir);

        var tempDir = Path.Combine(projectDir, $"_temp_{DateTime.Now:HHmmss}");
        Directory.CreateDirectory(tempDir);

        var finalOutputPath = Path.Combine(videoDir, config.OutputFileName);
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
                LogMessage = $"Bắt đầu xử lý {scenes.Count} phân cảnh (Tỷ lệ: {config.AspectRatio}, Độ phân giải: {width}x{height})...\n📁 Thư mục project: {projectDir}"
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

                // Sao lưu ảnh vào thư mục Images/ của project
                var imgExt = Path.GetExtension(scene.ImagePath);
                var projectImagePath = Path.Combine(imagesDir, $"scene_{sceneNum:D3}{imgExt}");
                File.Copy(scene.ImagePath, projectImagePath, true);

                // Sao lưu audio vào thư mục Audio/ của project
                var audioExt = Path.GetExtension(scene.AudioPath);
                var projectAudioPath = Path.Combine(audioDir, $"scene_{sceneNum:D3}{audioExt}");
                File.Copy(scene.AudioPath, projectAudioPath, true);

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
                var effectToUse = !string.IsNullOrEmpty(scene.MotionEffect) && scene.MotionEffect != "auto"
                    ? scene.MotionEffect
                    : config.MotionEffect;
                await RenderSegmentAsync(
                    ffmpeg, 
                    scene.ImagePath, 
                    scene.AudioPath, 
                    duration, 
                    width, 
                    height, 
                    effectToUse, 
                    config.Fps, 
                    segmentPath, 
                    progress, 
                    ct, 
                    i, 
                    config.EnableFadeTransition, 
                    config.EnableVignette,
                    config.EnableSubtitles,
                    scene.Text,
                    config.SubtitleStyle,
                    config.SubtitleFont,
                    tempDir,
                    sceneNum);

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
            await File.WriteAllTextAsync(concatListFile, sb.ToString(), new UTF8Encoding(false), ct);

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

                // Copy BGM vào thư mục Audio/ của project
                var bgmDestPath = Path.Combine(audioDir, $"bgm_{Path.GetFileName(config.BackgroundMusicPath)}");
                File.Copy(config.BackgroundMusicPath, bgmDestPath, true);

                var volStr = config.BackgroundMusicVolume.ToString("0.00", CultureInfo.InvariantCulture);
                var filterComplex = $"\"[0:a]aformat=sample_rates=44100:channel_layouts=stereo,volume=1.0[a1];[1:a]aformat=sample_rates=44100:channel_layouts=stereo,volume={volStr}[a2];[a1][a2]amix=inputs=2:duration=first:dropout_transition=3[a]\"";
                var mixArgs = $"-y -i \"{mergedVideoPath}\" -stream_loop -1 -i \"{config.BackgroundMusicPath}\" -filter_complex {filterComplex} -map 0:v -map \"[a]\" -c:v copy -c:a aac -b:a 192k \"{finalOutputPath}\"";
                await RunFfmpegAsync(ffmpeg, mixArgs, progress, ct);
            }
            else
            {
                // Không có BGM, copy file merged sang final output
                if (File.Exists(finalOutputPath)) File.Delete(finalOutputPath);
                File.Move(mergedVideoPath, finalOutputPath);
            }

            // Bước 4: Tự động tạo file .txt chứa Tiêu đề, Mô tả, Hashtags và Caption đầy đủ
            var publishInfo = config.PublishInfo ?? new VideoPublishInfo();
            if (string.IsNullOrWhiteSpace(publishInfo.Title))
            {
                publishInfo.Title = videoName.Replace('_', ' ');
            }
            if (string.IsNullOrWhiteSpace(publishInfo.Description))
            {
                var sceneTexts = scenes.Where(s => !string.IsNullOrWhiteSpace(s.Text)).Take(3).Select(s => s.Text);
                publishInfo.Description = string.Join(" ", sceneTexts);
            }
            if (string.IsNullOrWhiteSpace(publishInfo.Hashtags))
            {
                publishInfo.Hashtags = "#shorts #xuhuong #viral #fyp #tiktok #reels #video";
            }

            var txtContent = publishInfo.GenerateFormattedTxt(
                videoFileName: Path.GetFileName(finalOutputPath),
                aspectRatio: $"{config.AspectRatio} ({width}x{height})",
                voice: config.Voice,
                sceneCount: scenes.Count,
                bgmName: config.EnableBackgroundMusic && !string.IsNullOrEmpty(config.BackgroundMusicPath)
                    ? Path.GetFileName(config.BackgroundMusicPath)
                    : "Không sử dụng"
            );

            var infoFilePath = Path.Combine(projectDir, "DANG_BAI_METADATA.txt");
            await File.WriteAllTextAsync(infoFilePath, txtContent, new UTF8Encoding(true), ct);

            progress?.Report(new GenerationProgress
            {
                Percentage = 100,
                StepTitle = "Hoàn thành xuất sắc!",
                LogMessage = $"TẠO VIDEO THÀNH CÔNG!\n📁 Thư mục project: {projectDir}\n🎬 Video: {finalOutputPath}\n📝 File đăng bài (.txt): {infoFilePath}\n🖼️ Ảnh: {imagesDir} ({scenes.Count} files)\n🔊 Audio: {audioDir}"
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
        string motionEffect,
        int fps,
        string outputPath,
        IProgress<GenerationProgress>? progress,
        CancellationToken ct,
        int sceneIndex = 0,
        bool enableFade = false,
        bool enableVignette = false,
        bool enableSubtitles = false,
        string? subtitleText = null,
        string subtitleStyle = "cinematic",
        string subtitleFont = "Segoe UI Bold",
        string? tempDir = null,
        int sceneNumber = 1)
    {
        string filterString;
        var durStr = duration.ToString("0.000", CultureInfo.InvariantCulture);

        var effect = motionEffect?.ToLowerInvariant() ?? "random";
        if (effect == "random")
        {
            var dynamicEffects = new[] { "zoom_in", "pan_left_right", "zoom_out", "pan_up", "pan_right_left", "pan_down", "zoom_pan_right", "zoom_pan_left" };
            effect = dynamicEffects[sceneIndex % dynamicEffects.Length];
        }

        var numFrames = Math.Max((int)(duration * fps), 1);
        var zoomSpeed = 0.15 / numFrames;
        var zoomSpeedStr = zoomSpeed.ToString("0.000000", CultureInfo.InvariantCulture);

        string motionFilter;
        switch (effect)
        {
            case "zoom_in":
                motionFilter = $"scale={width * 2}:{height * 2},zoompan=z='min(zoom+{zoomSpeedStr},1.15)':x='iw/2-(iw/zoom/2)':y='ih/2-(ih/zoom/2)':d={numFrames}:s={width}x{height}:fps={fps}";
                break;

            case "zoom_out":
                motionFilter = $"scale={width * 2}:{height * 2},zoompan=z='if(eq(on,0),1.15,max(1.0,zoom-{zoomSpeedStr}))':x='iw/2-(iw/zoom/2)':y='ih/2-(ih/zoom/2)':d={numFrames}:s={width}x{height}:fps={fps}";
                break;

            case "pan_left_right":
                motionFilter = $"scale={width * 2}:{height * 2},zoompan=z=1.15:x='(iw-iw/zoom)*(on/{numFrames})':y='ih/2-(ih/zoom/2)':d={numFrames}:s={width}x{height}:fps={fps}";
                break;

            case "pan_right_left":
                motionFilter = $"scale={width * 2}:{height * 2},zoompan=z=1.15:x='(iw-iw/zoom)*(1-on/{numFrames})':y='ih/2-(ih/zoom/2)':d={numFrames}:s={width}x{height}:fps={fps}";
                break;

            case "pan_up":
                motionFilter = $"scale={width * 2}:{height * 2},zoompan=z=1.15:x='iw/2-(iw/zoom/2)':y='(ih-ih/zoom)*(1-on/{numFrames})':d={numFrames}:s={width}x{height}:fps={fps}";
                break;

            case "pan_down":
                motionFilter = $"scale={width * 2}:{height * 2},zoompan=z=1.15:x='iw/2-(iw/zoom/2)':y='(ih-ih/zoom)*(on/{numFrames})':d={numFrames}:s={width}x{height}:fps={fps}";
                break;

            case "zoom_pan_right":
                motionFilter = $"scale={width * 2}:{height * 2},zoompan=z='min(zoom+{zoomSpeedStr},1.20)':x='(iw-iw/zoom)*(on/{numFrames})':y='ih/2-(ih/zoom/2)':d={numFrames}:s={width}x{height}:fps={fps}";
                break;

            case "zoom_pan_left":
                motionFilter = $"scale={width * 2}:{height * 2},zoompan=z='min(zoom+{zoomSpeedStr},1.20)':x='(iw-iw/zoom)*(1-on/{numFrames})':y='ih/2-(ih/zoom/2)':d={numFrames}:s={width}x{height}:fps={fps}";
                break;

            case "none":
            default:
                motionFilter = $"scale={width}:{height}";
                break;
        }

        var filterParts = new List<string> { motionFilter };

        if (enableVignette)
        {
            filterParts.Add("vignette=PI/4");
        }

        if (enableFade && duration >= 1.0)
        {
            var fadeDur = Math.Min(0.35, duration / 4);
            var fadeDurStr = fadeDur.ToString("0.00", CultureInfo.InvariantCulture);
            var fadeOutStart = (duration - fadeDur).ToString("0.000", CultureInfo.InvariantCulture);
            filterParts.Add($"fade=t=in:st=0:d={fadeDurStr}");
            filterParts.Add($"fade=t=out:st={fadeOutStart}:d={fadeDurStr}");
        }

        filterParts.Add("setsar=1");

        // ── Xử lý Chữ Chạy / Phụ Đề (Subtitles Overlay) ──
        if (enableSubtitles && !string.IsNullOrWhiteSpace(subtitleText) && !string.IsNullOrEmpty(tempDir))
        {
            try
            {
                var isTicker = subtitleStyle.Equals("ticker", StringComparison.OrdinalIgnoreCase);
                var maxChars = width > height ? 48 : 34; // 16:9 vs 9:16
                var formattedText = isTicker 
                    ? subtitleText.Trim().Replace("\r", " ").Replace("\n", " ") 
                    : WrapText(subtitleText.Trim(), maxChars);

                var subTxtFile = Path.Combine(tempDir, $"subtitle_{sceneNumber:D3}.txt");
                await File.WriteAllTextAsync(subTxtFile, formattedText, new UTF8Encoding(false), ct);

                var safeTxt = subTxtFile.Replace('\\', '/').Replace(":", "\\:");
                var fontFile = ResolveFontFile(subtitleFont);
                var safeFont = fontFile.Replace('\\', '/').Replace(":", "\\:");

                int fontSize = width > height ? Math.Max(28, height / 26) : Math.Max(32, height / 36);
                int yOffset = width > height ? (int)(height * 0.10) : (int)(height * 0.12);

                string drawTextFilter = subtitleStyle.ToLowerInvariant() switch
                {
                    "boxed" => $"drawtext=fontfile='{safeFont}':textfile='{safeTxt}':fontsize={fontSize}:fontcolor=white:box=1:boxcolor=black@0.7:boxborderw=14:borderw=1:bordercolor=black@0.5:x=(w-text_w)/2:y=h-text_h-{yOffset}",
                    "viral_yellow" => $"drawtext=fontfile='{safeFont}':textfile='{safeTxt}':fontsize={fontSize + 2}:fontcolor=#ffd43b:borderw=4:bordercolor=black:shadowcolor=black@0.85:shadowx=3:shadowy=3:x=(w-text_w)/2:y=h-text_h-{yOffset}",
                    "neon" => $"drawtext=fontfile='{safeFont}':textfile='{safeTxt}':fontsize={fontSize}:fontcolor=#89dceb:borderw=2:bordercolor=black:shadowcolor=#1e66f5@0.85:shadowx=3:shadowy=3:box=1:boxcolor=#11111b@0.5:boxborderw=10:x=(w-text_w)/2:y=h-text_h-{yOffset}",
                    "gold" => $"drawtext=fontfile='{safeFont}':textfile='{safeTxt}':fontsize={fontSize}:fontcolor=#f9e2af:borderw=2:bordercolor=#181825:shadowcolor=black@0.65:shadowx=2:shadowy=2:x=(w-text_w)/2:y=h-text_h-{yOffset}",
                    "ticker" => $"drawtext=fontfile='{safeFont}':textfile='{safeTxt}':fontsize={Math.Max(26, fontSize - 6)}:fontcolor=white:box=1:boxcolor=#11111b@0.85:boxborderw=12:borderw=2:bordercolor=black:x='w-t*200':y=h-text_h-36",
                    "cinematic" or _ => $"drawtext=fontfile='{safeFont}':textfile='{safeTxt}':fontsize={fontSize}:fontcolor=white:borderw=3:bordercolor=black@0.9:shadowcolor=black@0.7:shadowx=2:shadowy=2:x=(w-text_w)/2:y=h-text_h-{yOffset}"
                };

                filterParts.Add(drawTextFilter);
            }
            catch (Exception ex)
            {
                progress?.Report(new GenerationProgress
                {
                    LogMessage = $"[Cảnh {sceneNumber}] Lưu ý: Không thể tạo lớp phủ chữ ({ex.Message}), tiếp tục tạo video không phụ đề."
                });
            }
        }

        filterString = $"\"{string.Join(",", filterParts)}\"";

        var args = $"-y -loop 1 -i \"{imagePath}\" -i \"{audioPath}\" -c:v libx264 -tune stillimage -r {fps} -vf {filterString} -c:a aac -b:a 192k -pix_fmt yuv420p -t {durStr} \"{outputPath}\"";
        await RunFfmpegAsync(ffmpeg, args, progress, ct);
    }

    private static string WrapText(string text, int maxCharsPerLine = 34)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;
        var words = text.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        var sb = new StringBuilder();
        var currentLine = new StringBuilder();

        foreach (var word in words)
        {
            if (currentLine.Length + word.Length + 1 > maxCharsPerLine)
            {
                if (currentLine.Length > 0)
                {
                    if (sb.Length > 0) sb.AppendLine();
                    sb.Append(currentLine.ToString());
                    currentLine.Clear();
                }
            }

            if (currentLine.Length > 0) currentLine.Append(' ');
            currentLine.Append(word);
        }

        if (currentLine.Length > 0)
        {
            if (sb.Length > 0) sb.AppendLine();
            sb.Append(currentLine.ToString());
        }

        return sb.ToString();
    }

    private static string ResolveFontFile(string fontName)
    {
        var fontsDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Fonts");
        string fileName = fontName.ToLowerInvariant() switch
        {
            var f when f.Contains("impact") => "impact.ttf",
            var f when f.Contains("arial") && f.Contains("bold") => "arialbd.ttf",
            var f when f.Contains("arial") => "arial.ttf",
            var f when f.Contains("tahoma") && f.Contains("bold") => "tahomabd.ttf",
            var f when f.Contains("tahoma") => "tahoma.ttf",
            var f when f.Contains("consolas") || f.Contains("consola") => "consola.ttf",
            var f when f.Contains("times") => "times.ttf",
            var f when f.Contains("segoe") && f.Contains("bold") => "segoeuib.ttf",
            var f when f.Contains("segoe") => "segoeui.ttf",
            _ => "segoeuib.ttf"
        };

        var fullPath = Path.Combine(fontsDir, fileName);
        if (File.Exists(fullPath)) return fullPath;

        var segoe = Path.Combine(fontsDir, "segoeui.ttf");
        if (File.Exists(segoe)) return segoe;

        var arial = Path.Combine(fontsDir, "arial.ttf");
        if (File.Exists(arial)) return arial;

        return fileName;
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
