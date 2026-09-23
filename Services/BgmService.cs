using System.Diagnostics;
using System.IO;
using VideoAutoWpf.Models;

namespace VideoAutoWpf.Services;

public class BgmService
{
    private readonly string _bgmDirectory;
    private readonly IFFmpegService _ffmpegService;

    public string BgmDirectory => _bgmDirectory;

    public BgmService(IFFmpegService ffmpegService)
    {
        _ffmpegService = ffmpegService;

        // Ưu tiên thư mục Assets/Bgm ở BaseDirectory, nếu chạy từ debug thì tạo trong thư mục dự án nếu có
        var baseDir = AppContext.BaseDirectory;
        var dirInBase = Path.Combine(baseDir, "Assets", "Bgm");
        
        // Kiểm tra xem có thư mục src/Assets/Bgm không
        var candidateProjectDir = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "Assets", "Bgm"));
        if (Directory.Exists(candidateProjectDir))
        {
            _bgmDirectory = candidateProjectDir;
        }
        else
        {
            _bgmDirectory = dirInBase;
        }

        try
        {
            if (!Directory.Exists(_bgmDirectory))
            {
                Directory.CreateDirectory(_bgmDirectory);
            }
        }
        catch
        {
            _bgmDirectory = Path.Combine(Path.GetTempPath(), "VideoAutoWpf", "Bgm");
            Directory.CreateDirectory(_bgmDirectory);
        }
    }

    /// <summary>
    /// Đảm bảo các track nhạc nền mẫu có sẵn (tạo bằng FFmpeg nếu chưa có).
    /// </summary>
    public async Task EnsureBuiltInTracksAsync()
    {
        var ffmpeg = _ffmpegService.LocateFfmpeg();
        if (string.IsNullOrEmpty(ffmpeg) || !File.Exists(ffmpeg))
        {
            return;
        }

        var presets = new (string fileName, string lavfi)[]
        {
            (
                "Chill_Ambient.mp3",
                "sine=frequency=220:duration=30[a];sine=frequency=277.18:duration=30[b];sine=frequency=329.63:duration=30[c];sine=frequency=415.30:duration=30[d];sine=frequency=493.88:duration=30[e];[a][b][c][d][e]amix=inputs=5:normalize=0,aecho=0.8:0.85:600:0.4,afade=t=in:st=0:d=1.5,afade=t=out:st=28:d=2,volume=1.9"
            ),
            (
                "Dramatic_Mystery.mp3",
                "sine=frequency=146.83:duration=30[a];sine=frequency=220:duration=30[b];sine=frequency=293.66:duration=30[c];sine=frequency=349.23:duration=30,tremolo=f=2:d=0.5[d];sine=frequency=440:duration=30[e];[a][b][c][d][e]amix=inputs=5:normalize=0,aecho=0.8:0.85:500:0.45,afade=t=in:st=0:d=2,afade=t=out:st=27:d=3,volume=1.9"
            ),
            (
                "Epic_Cinematic.mp3",
                "sine=frequency=196:duration=30[a];sine=frequency=261.63:duration=30[b];sine=frequency=329.63:duration=30[c];sine=frequency=392:duration=30,tremolo=f=1.5:d=0.4[d];sine=frequency=587.33:duration=30[e];[a][b][c][d][e]amix=inputs=5:normalize=0,aecho=0.8:0.88:700:0.5,afade=t=in:st=0:d=2,afade=t=out:st=27:d=3,volume=1.8"
            ),
            (
                "Horror_Ambience.mp3",
                "sine=frequency=220:duration=30[a];sine=frequency=311.13:duration=30[b];sine=frequency=349.23:duration=30[c];sine=frequency=466.16:duration=30,tremolo=f=3:d=0.6[d];[a][b][c][d]amix=inputs=4:normalize=0,aecho=0.8:0.85:500:0.45,afade=t=in:st=0:d=2,afade=t=out:st=27:d=3,volume=1.9"
            ),
            (
                "Upbeat_Pulse.mp3",
                "sine=frequency=261.63:duration=30[a];sine=frequency=329.63:duration=30[b];sine=frequency=392:duration=30,tremolo=f=4:d=0.7[c];sine=frequency=523.25:duration=30[d];[a][b][c][d]amix=inputs=4:normalize=0,aecho=0.8:0.75:300:0.35,afade=t=in:st=0:d=1,afade=t=out:st=28:d=2,volume=1.8"
            )
        };

        foreach (var (fileName, lavfi) in presets)
        {
            var targetPath = Path.Combine(_bgmDirectory, fileName);
            if (!File.Exists(targetPath) || new FileInfo(targetPath).Length < 1000)
            {
                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = ffmpeg,
                        Arguments = $"-y -f lavfi -i \"{lavfi}\" -c:a libmp3lame \"{targetPath}\"",
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };
                    using var p = Process.Start(psi);
                    if (p != null)
                    {
                        await p.WaitForExitAsync();
                    }
                }
                catch
                {
                    // Tiếp tục nếu có lỗi tạo track
                }
            }
        }
    }

    /// <summary>
    /// Lấy danh sách tất cả các track nhạc nền hiện có (mẫu + người dùng thêm).
    /// </summary>
    public List<BgmTrack> GetAvailableTracks()
    {
        var result = new List<BgmTrack>();

        // Tùy chọn không dùng nhạc
        result.Add(new BgmTrack
        {
            Id = "none",
            Title = "🚫 Không sử dụng nhạc nền",
            Mood = "none",
            SourceTag = "Tắt BGM"
        });

        if (!Directory.Exists(_bgmDirectory))
        {
            return result;
        }

        // Đọc các file âm thanh trong thư mục
        var extensions = new[] { "*.mp3", "*.wav", "*.aac", "*.m4a", "*.ogg", "*.flac" };
        var foundFiles = new List<string>();
        foreach (var ext in extensions)
        {
            foundFiles.AddRange(Directory.GetFiles(_bgmDirectory, ext));
        }

        foreach (var filePath in foundFiles.OrderBy(f => Path.GetFileName(f)))
        {
            var fn = Path.GetFileNameWithoutExtension(filePath);
            var track = new BgmTrack
            {
                FilePath = filePath
            };

            // Nhận diện theo preset mẫu
            if (fn.Equals("Chill_Ambient", StringComparison.OrdinalIgnoreCase))
            {
                track.Id = "chill";
                track.Title = "🧘 Thư Giãn & Lofi (Chill Ambient)";
                track.Mood = "chill";
                track.SourceTag = "Mẫu tích hợp (CC0)";
                track.IsBuiltIn = true;
            }
            else if (fn.Equals("Dramatic_Mystery", StringComparison.OrdinalIgnoreCase))
            {
                track.Id = "dramatic";
                track.Title = "🕵️ Kịch Tính & Bí Ẩn (Dramatic Mystery)";
                track.Mood = "dramatic";
                track.SourceTag = "Mẫu tích hợp (CC0)";
                track.IsBuiltIn = true;
            }
            else if (fn.Equals("Epic_Cinematic", StringComparison.OrdinalIgnoreCase))
            {
                track.Id = "epic";
                track.Title = "🏰 Hùng Tráng & Sử Thi (Epic Cinematic)";
                track.Mood = "epic";
                track.SourceTag = "Mẫu tích hợp (CC0)";
                track.IsBuiltIn = true;
            }
            else if (fn.Equals("Horror_Ambience", StringComparison.OrdinalIgnoreCase))
            {
                track.Id = "horror";
                track.Title = "👻 U Ám & Rùng Rợn (Dark Horror)";
                track.Mood = "horror";
                track.SourceTag = "Mẫu tích hợp (CC0)";
                track.IsBuiltIn = true;
            }
            else if (fn.Equals("Upbeat_Pulse", StringComparison.OrdinalIgnoreCase))
            {
                track.Id = "upbeat";
                track.Title = "⚡ Năng Động & Vui Tươi (Upbeat Pulse)";
                track.Mood = "upbeat";
                track.SourceTag = "Mẫu tích hợp (CC0)";
                track.IsBuiltIn = true;
            }
            else
            {
                // File người dùng tự thêm vào thư mục
                track.Id = fn.ToLowerInvariant();
                track.Title = $"🎵 {fn.Replace('_', ' ')}";
                track.Mood = "custom";
                track.SourceTag = "Thư viện cá nhân";
                track.IsCustom = true;
            }

            result.Add(track);
        }

        return result;
    }

    /// <summary>
    /// Mở thư mục BGM trong Windows Explorer để người dùng paste nhạc vào.
    /// </summary>
    public void OpenBgmDirectoryInExplorer()
    {
        if (!Directory.Exists(_bgmDirectory))
        {
            Directory.CreateDirectory(_bgmDirectory);
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = "explorer.exe",
            Arguments = $"\"{_bgmDirectory}\"",
            UseShellExecute = true
        });
    }
}
