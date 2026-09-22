using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using VideoAutoWpf.Models;
using VideoAutoWpf.Services;
using VideoAutoWpf.Views;

namespace VideoAutoWpf.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly IFFmpegService _ffmpegService;
    private readonly GoogleAuthService _authService;
    private readonly GoogleTtsService _ttsService;
    private readonly VertexImagenService _imagenService;
    private readonly GeminiScriptService _geminiService;
    private CancellationTokenSource? _cts;

    public ObservableCollection<SceneItem> Scenes { get; } = new();

    public IReadOnlyList<string> AvailableVoices => GoogleTtsService.AvailableVoices;

    [ObservableProperty]
    private string _selectedVoice = "vi-VN-Wavenet-B";

    [ObservableProperty]
    private SceneItem? _selectedScene;

    [ObservableProperty]
    private string _aspectRatio = "9:16";

    [ObservableProperty]
    private bool _enableKenBurns = true;

    [ObservableProperty]
    private bool _enableBgm = false;

    [ObservableProperty]
    private string? _bgmPath;

    [ObservableProperty]
    private double _bgmVolume = 0.15; // 15%

    public string BgmVolumePercentageText => $"{(int)(BgmVolume * 100)}%";

    [ObservableProperty]
    private string _outputDirectory;

    [ObservableProperty]
    private string _outputFileName = "video_thanh_pham.mp4";

    [ObservableProperty]
    private bool _isRendering;

    [ObservableProperty]
    private double _progressPercentage;

    [ObservableProperty]
    private string _statusMessage = "Sẵn sàng tạo video";

    [ObservableProperty]
    private string _logText = string.Empty;

    [ObservableProperty]
    private string? _lastGeneratedVideoPath;

    [ObservableProperty]
    private string _ffmpegStatusText = "Đang kiểm tra FFmpeg...";

    [ObservableProperty]
    private string _googleStatusText = "Đang kiểm tra Google Cloud...";

    public bool HasGeneratedVideo => !string.IsNullOrEmpty(LastGeneratedVideoPath) && File.Exists(LastGeneratedVideoPath);

    public MainViewModel() : this(new FFmpegService())
    {
    }

    public MainViewModel(IFFmpegService ffmpegService)
    {
        _ffmpegService = ffmpegService;
        _authService = new GoogleAuthService();
        _ttsService = new GoogleTtsService(_authService);
        _imagenService = new VertexImagenService(_authService);
        _geminiService = new GeminiScriptService(_authService);

        var defaultOut = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyVideos), "VideoAutoOutput");
        _outputDirectory = defaultOut;

        CheckServicesAvailability();
    }

    partial void OnBgmVolumeChanged(double value)
    {
        OnPropertyChanged(nameof(BgmVolumePercentageText));
    }

    partial void OnLastGeneratedVideoPathChanged(string? value)
    {
        OnPropertyChanged(nameof(HasGeneratedVideo));
    }

    private void CheckServicesAvailability()
    {
        try
        {
            var path = _ffmpegService.LocateFfmpeg();
            if (!string.IsNullOrEmpty(path))
            {
                FfmpegStatusText = $"FFmpeg: Đã kết nối ({Path.GetFileName(path)})";
                AppendLog($"[Hệ thống] Đã tìm thấy FFmpeg: {path}");
            }
            else
            {
                FfmpegStatusText = "FFmpeg: Chưa tìm thấy!";
                AppendLog("[Cảnh báo] Không tìm thấy FFmpeg trên máy tính.");
            }
        }
        catch (Exception ex)
        {
            FfmpegStatusText = "Lỗi FFmpeg";
            AppendLog($"[Lỗi FFmpeg] {ex.Message}");
        }

        try
        {
            if (_authService.IsAuthenticated)
            {
                GoogleStatusText = $"Google Cloud: {_authService.ProjectId}";
                AppendLog($"[Hệ thống] Xác thực Google Cloud thành công: {_authService.ProjectId}");
            }
            else
            {
                GoogleStatusText = "Google Cloud: Chưa có Key JSON";
                AppendLog("[Cảnh báo] Chưa tìm thấy Google Service Account key. Hãy đặt file google_credentials.json trong thư mục app.");
            }
        }
        catch (Exception ex)
        {
            GoogleStatusText = "Lỗi xác thực Google";
            AppendLog($"[Lỗi Google Cloud] {ex.Message}");
        }
    }

    private void AppendLog(string message)
    {
        var time = DateTime.Now.ToString("HH:mm:ss");
        LogText += $"[{time}] {message}\n";
    }

    [RelayCommand]
    private void OpenScriptGenerator()
    {
        var win = new ScriptGeneratorWindow(_geminiService)
        {
            Owner = Application.Current.MainWindow
        };

        if (win.ShowDialog() == true && win.ResultWorkspace != null)
        {
            ApplyWorkspace(win.ResultWorkspace);
        }
    }

    private void ApplyWorkspace(ScriptWorkspace workspace)
    {
        if (workspace.Scenes == null || workspace.Scenes.Count == 0) return;

        if (!string.IsNullOrEmpty(workspace.Metadata?.Ratio))
            AspectRatio = workspace.Metadata.Ratio;

        if (!string.IsNullOrEmpty(workspace.Metadata?.Voice) && AvailableVoices.Contains(workspace.Metadata.Voice))
            SelectedVoice = workspace.Metadata.Voice;

        if (workspace.Metadata != null && workspace.Metadata.EnableMusic)
        {
            EnableBgm = true;
            BgmVolume = workspace.Metadata.MusicVolume > 0 ? workspace.Metadata.MusicVolume : 0.15;
            if (!string.IsNullOrEmpty(workspace.Metadata.Music) && File.Exists(workspace.Metadata.Music))
            {
                BgmPath = workspace.Metadata.Music;
            }
        }

        Scenes.Clear();
        int idx = 1;
        foreach (var sc in workspace.Scenes)
        {
            Scenes.Add(new SceneItem
            {
                Index = idx++,
                Text = sc.Text,
                ImagePrompt = sc.ImagePrompt,
                Status = "Kịch bản mới"
            });
        }

        AppendLog($"[Kịch bản] Đã nạp thành công {Scenes.Count} phân cảnh vào dự án!");
        MessageBox.Show($"Đã nạp thành công {Scenes.Count} phân cảnh vào dự án!", "Thành công", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    [RelayCommand]
    private void AddScene()
    {
        var nextIndex = Scenes.Count + 1;
        var scene = new SceneItem
        {
            Index = nextIndex,
            Status = "Chờ nhập kịch bản"
        };
        Scenes.Add(scene);
        SelectedScene = scene;
        ReindexScenes();
    }

    [RelayCommand]
    private void RemoveScene(SceneItem? scene)
    {
        var target = scene ?? SelectedScene;
        if (target != null && Scenes.Contains(target))
        {
            Scenes.Remove(target);
            ReindexScenes();
        }
    }

    [RelayCommand]
    private void ClearScenes()
    {
        if (Scenes.Count == 0) return;
        var result = MessageBox.Show("Bạn có chắc chắn muốn xóa toàn bộ phân cảnh?", "Xác nhận", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (result == MessageBoxResult.Yes)
        {
            Scenes.Clear();
            SelectedScene = null;
        }
    }

    [RelayCommand]
    private void MoveUp(SceneItem? scene)
    {
        var target = scene ?? SelectedScene;
        if (target == null) return;
        var idx = Scenes.IndexOf(target);
        if (idx > 0)
        {
            Scenes.Move(idx, idx - 1);
            ReindexScenes();
            SelectedScene = target;
        }
    }

    [RelayCommand]
    private void MoveDown(SceneItem? scene)
    {
        var target = scene ?? SelectedScene;
        if (target == null) return;
        var idx = Scenes.IndexOf(target);
        if (idx < Scenes.Count - 1 && idx >= 0)
        {
            Scenes.Move(idx, idx + 1);
            ReindexScenes();
            SelectedScene = target;
        }
    }

    private void ReindexScenes()
    {
        for (int i = 0; i < Scenes.Count; i++)
        {
            Scenes[i].Index = i + 1;
        }
    }

    [RelayCommand]
    private async Task SaveScriptJson()
    {
        if (Scenes.Count == 0)
        {
            MessageBox.Show("Chưa có phân cảnh nào để lưu.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var sfd = new SaveFileDialog
        {
            Title = "Lưu kịch bản ra file JSON",
            Filter = "File Kịch Bản (*.json)|*.json",
            FileName = "kich_ban_video.json"
        };

        if (sfd.ShowDialog() != true) return;

        try
        {
            var workspace = new ScriptWorkspace
            {
                Metadata = new ScriptMetadata
                {
                    Ratio = AspectRatio,
                    Voice = SelectedVoice,
                    EnableMusic = EnableBgm,
                    Music = BgmPath,
                    MusicVolume = BgmVolume
                },
                Scenes = Scenes.Select(s => new ScriptScene
                {
                    Text = s.Text ?? string.Empty,
                    ImagePrompt = s.ImagePrompt ?? string.Empty
                }).ToList()
            };

            var json = JsonSerializer.Serialize(workspace, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(sfd.FileName, json);
            AppendLog($"[Kịch bản] Đã lưu kịch bản vào: {sfd.FileName}");
            MessageBox.Show("Đã lưu file kịch bản thành công!", "Thành công", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Không thể lưu file kịch bản:\n{ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private async Task GenerateAudioForScene(SceneItem? scene)
    {
        var target = scene ?? SelectedScene;
        if (target == null) return;

        if (string.IsNullOrWhiteSpace(target.Text))
        {
            MessageBox.Show("Vui lòng nhập văn bản lời thoại (Text) trước khi sinh audio.", "Chưa có lời thoại", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        target.IsGeneratingAudio = true;
        target.Status = "Đang sinh audio...";
        AppendLog($"[Phân cảnh #{target.Index}] Đang gọi Google TTS ('{SelectedVoice}')...");

        try
        {
            var tempDir = Path.Combine(OutputDirectory, "generated_assets");
            Directory.CreateDirectory(tempDir);
            var audioPath = Path.Combine(tempDir, $"voice_scene_{target.Index:D3}_{DateTime.Now:HHmmss}.mp3");

            await _ttsService.SynthesizeSpeechAsync(target.Text, audioPath, SelectedVoice);
            target.AudioPath = audioPath;

            var duration = await _ffmpegService.GetAudioDurationAsync(audioPath);
            target.DurationSeconds = duration;

            target.Status = target.HasImage ? "Sẵn sàng" : "Thiếu ảnh";
            AppendLog($"[Phân cảnh #{target.Index}] Tạo audio thành công ({duration:F2}s)!");
        }
        catch (Exception ex)
        {
            target.Status = "Lỗi sinh audio";
            AppendLog($"[Lỗi TTS Cảnh #{target.Index}] {ex.Message}");
            MessageBox.Show($"Lỗi khi tạo giọng đọc:\n{ex.Message}", "Lỗi TTS", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            target.IsGeneratingAudio = false;
        }
    }

    [RelayCommand]
    private async Task GenerateImageForScene(SceneItem? scene)
    {
        var target = scene ?? SelectedScene;
        if (target == null) return;

        if (string.IsNullOrWhiteSpace(target.ImagePrompt))
        {
            MessageBox.Show("Vui lòng nhập mô tả hình ảnh (Image Prompt) trước khi sinh ảnh.", "Chưa có prompt", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        target.IsGeneratingImage = true;
        target.Status = "Đang vẽ ảnh...";
        AppendLog($"[Phân cảnh #{target.Index}] Đang gửi yêu cầu tới Vertex AI Imagen 3...");

        try
        {
            var tempDir = Path.Combine(OutputDirectory, "generated_assets");
            Directory.CreateDirectory(tempDir);
            var imagePath = Path.Combine(tempDir, $"image_scene_{target.Index:D3}_{DateTime.Now:HHmmss}.png");

            await _imagenService.GenerateImageAsync(target.ImagePrompt, imagePath, AspectRatio);
            target.ImagePath = imagePath;

            target.Status = target.HasAudio ? "Sẵn sàng" : "Thiếu audio";
            AppendLog($"[Phân cảnh #{target.Index}] Vẽ ảnh Imagen 3 thành công!");
        }
        catch (Exception ex)
        {
            target.Status = "Lỗi sinh ảnh";
            AppendLog($"[Lỗi Imagen 3 Cảnh #{target.Index}] {ex.Message}");
            MessageBox.Show($"Lỗi khi tạo hình ảnh:\n{ex.Message}", "Lỗi Imagen 3", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            target.IsGeneratingImage = false;
        }
    }

    [RelayCommand]
    private async Task BrowseImageForScene(SceneItem? scene)
    {
        var target = scene ?? SelectedScene;
        if (target == null) return;

        var ofd = new OpenFileDialog
        {
            Title = "Chọn hình ảnh cho phân cảnh",
            Filter = "Ảnh (*.jpg;*.jpeg;*.png;*.webp;*.bmp)|*.jpg;*.jpeg;*.png;*.webp;*.bmp|Tất cả tệp (*.*)|*.*"
        };

        if (ofd.ShowDialog() == true)
        {
            target.ImagePath = ofd.FileName;
            target.Status = string.IsNullOrEmpty(target.AudioPath) ? "Thiếu audio" : "Sẵn sàng";
        }
    }

    [RelayCommand]
    private async Task BrowseAudioForScene(SceneItem? scene)
    {
        var target = scene ?? SelectedScene;
        if (target == null) return;

        var ofd = new OpenFileDialog
        {
            Title = "Chọn âm thanh cho phân cảnh",
            Filter = "Âm thanh (*.mp3;*.wav;*.aac;*.m4a;*.ogg;*.flac)|*.mp3;*.wav;*.aac;*.m4a;*.ogg;*.flac|Tất cả tệp (*.*)|*.*"
        };

        if (ofd.ShowDialog() == true)
        {
            target.AudioPath = ofd.FileName;
            target.Status = "Đang đọc độ dài...";
            try
            {
                var duration = await _ffmpegService.GetAudioDurationAsync(ofd.FileName);
                target.DurationSeconds = duration;
                target.Status = string.IsNullOrEmpty(target.ImagePath) ? "Thiếu ảnh" : "Sẵn sàng";
                AppendLog($"Phân cảnh #{target.Index}: File '{Path.GetFileName(ofd.FileName)}' có độ dài {duration:F2}s");
            }
            catch (Exception ex)
            {
                target.Status = "Lỗi audio";
                AppendLog($"Lỗi đọc file âm thanh: {ex.Message}");
            }
        }
    }

    [RelayCommand]
    private void BrowseBgm()
    {
        var ofd = new OpenFileDialog
        {
            Title = "Chọn nhạc nền (BGM)",
            Filter = "Âm thanh (*.mp3;*.wav;*.aac;*.m4a;*.ogg;*.flac)|*.mp3;*.wav;*.aac;*.m4a;*.ogg;*.flac|Tất cả tệp (*.*)|*.*"
        };

        if (ofd.ShowDialog() == true)
        {
            BgmPath = ofd.FileName;
            EnableBgm = true;
            AppendLog($"Đã chọn nhạc nền: {Path.GetFileName(ofd.FileName)}");
        }
    }

    [RelayCommand]
    private void BrowseOutputDir()
    {
        var ofd = new OpenFolderDialog
        {
            Title = "Chọn thư mục lưu video thành phẩm",
            InitialDirectory = OutputDirectory
        };

        if (ofd.ShowDialog() == true)
        {
            OutputDirectory = ofd.FolderName;
        }
    }

    [RelayCommand]
    private async Task StartRender()
    {
        if (Scenes.Count == 0)
        {
            MessageBox.Show("Vui lòng thêm ít nhất 1 phân cảnh trước khi tạo video!", "Chưa có dữ liệu", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        IsRendering = true;
        ProgressPercentage = 0;
        StatusMessage = "Đang chuẩn bị tạo video...";
        LastGeneratedVideoPath = null;
        _cts = new CancellationTokenSource();
        var ct = _cts.Token;

        var tempAssetsDir = Path.Combine(OutputDirectory, "generated_assets");
        Directory.CreateDirectory(tempAssetsDir);

        try
        {
            for (int i = 0; i < Scenes.Count; i++)
            {
                ct.ThrowIfCancellationRequested();
                var s = Scenes[i];
                var sNum = i + 1;

                if (!s.HasAudio)
                {
                    if (!string.IsNullOrWhiteSpace(s.Text))
                    {
                        StatusMessage = $"Đang sinh giọng đọc cho cảnh {sNum}/{Scenes.Count}...";
                        AppendLog($"[Tự động] Cảnh #{sNum}: Đang gọi Google TTS tạo lời thoại...");
                        s.IsGeneratingAudio = true;
                        var audioOut = Path.Combine(tempAssetsDir, $"voice_scene_{sNum:D3}_{DateTime.Now:HHmmss}.mp3");
                        await _ttsService.SynthesizeSpeechAsync(s.Text, audioOut, SelectedVoice, ct: ct);
                        s.AudioPath = audioOut;
                        s.DurationSeconds = await _ffmpegService.GetAudioDurationAsync(audioOut, ct: ct);
                        s.IsGeneratingAudio = false;
                    }
                    else
                    {
                        throw new InvalidOperationException($"Phân cảnh #{sNum} chưa có file âm thanh và chưa có nội dung lời thoại!");
                    }
                }

                if (!s.HasImage)
                {
                    if (!string.IsNullOrWhiteSpace(s.ImagePrompt))
                    {
                        StatusMessage = $"Đang gọi Imagen 3 vẽ ảnh cảnh {sNum}/{Scenes.Count}...";
                        AppendLog($"[Tự động] Cảnh #{sNum}: Đang gửi prompt tới Vertex AI Imagen 3...");
                        s.IsGeneratingImage = true;
                        var imgOut = Path.Combine(tempAssetsDir, $"image_scene_{sNum:D3}_{DateTime.Now:HHmmss}.png");
                        await _imagenService.GenerateImageAsync(s.ImagePrompt, imgOut, AspectRatio, ct: ct);
                        s.ImagePath = imgOut;
                        s.IsGeneratingImage = false;
                    }
                    else
                    {
                        throw new InvalidOperationException($"Phân cảnh #{sNum} chưa có file ảnh và chưa có mô tả (prompt) để vẽ!");
                    }
                }
            }

            var config = new VideoConfig
            {
                AspectRatio = AspectRatio,
                EnableKenBurns = EnableKenBurns,
                EnableBackgroundMusic = EnableBgm,
                BackgroundMusicPath = BgmPath,
                BackgroundMusicVolume = BgmVolume,
                OutputDirectory = OutputDirectory,
                OutputFileName = string.IsNullOrWhiteSpace(OutputFileName) ? $"video_{DateTime.Now:yyyyMMdd_HHmmss}.mp4" : OutputFileName
            };

            var progress = new Progress<GenerationProgress>(p =>
            {
                if (p.Percentage > 0)
                    ProgressPercentage = p.Percentage;
                if (!string.IsNullOrEmpty(p.StepTitle))
                    StatusMessage = p.StepTitle;
                if (!string.IsNullOrEmpty(p.LogMessage))
                    AppendLog(p.LogMessage);
            });

            var resultFile = await _ffmpegService.GenerateVideoAsync(Scenes.ToList(), config, progress, ct);
            LastGeneratedVideoPath = resultFile;
            StatusMessage = "Tạo video thành công!";
            ProgressPercentage = 100;
            MessageBox.Show($"Video đã được tạo thành công tại:\n{resultFile}", "Thành công", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Đã hủy render bởi người dùng.";
            AppendLog("[Hệ thống] Quá trình tạo video đã bị dừng.");
        }
        catch (Exception ex)
        {
            StatusMessage = "Có lỗi xảy ra!";
            AppendLog($"[Lỗi nghiêm trọng] {ex.Message}");
            MessageBox.Show($"Lỗi trong quá trình tạo video:\n{ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            foreach (var sc in Scenes)
            {
                sc.IsGeneratingAudio = false;
                sc.IsGeneratingImage = false;
            }
            IsRendering = false;
            _cts?.Dispose();
            _cts = null;
        }
    }

    [RelayCommand]
    private void CancelRender()
    {
        if (_cts != null && !_cts.IsCancellationRequested)
        {
            _cts.Cancel();
            StatusMessage = "Đang gửi yêu cầu dừng...";
            AppendLog("[Hệ thống] Đang hủy tiến trình...");
        }
    }

    [RelayCommand]
    private void OpenVideo()
    {
        if (File.Exists(LastGeneratedVideoPath))
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = LastGeneratedVideoPath,
                UseShellExecute = true
            });
        }
    }

    [RelayCommand]
    private void OpenOutputDir()
    {
        if (Directory.Exists(OutputDirectory))
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = OutputDirectory,
                UseShellExecute = true
            });
        }
    }

    [RelayCommand]
    private void ClearLog()
    {
        LogText = string.Empty;
    }

    public async Task HandleDroppedFilesAsync(string[] files)
    {
        if (files == null || files.Length == 0) return;

        if (files.Length == 1 && Path.GetExtension(files[0]).Equals(".json", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                var json = await File.ReadAllTextAsync(files[0]);
                var workspace = JsonSerializer.Deserialize<ScriptWorkspace>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (workspace?.Scenes != null && workspace.Scenes.Count > 0)
                {
                    ApplyWorkspace(workspace);
                    return;
                }
            }
            catch { }
        }

        var imageExts = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".webp", ".bmp" };
        var audioExts = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".mp3", ".wav", ".aac", ".m4a", ".ogg", ".flac" };

        var allFiles = new List<string>();
        foreach (var path in files)
        {
            if (Directory.Exists(path))
            {
                allFiles.AddRange(Directory.GetFiles(path, "*.*", SearchOption.TopDirectoryOnly));
            }
            else if (File.Exists(path))
            {
                allFiles.Add(path);
            }
        }

        var imageFiles = allFiles.Where(f => imageExts.Contains(Path.GetExtension(f))).OrderBy(f => f).ToList();
        var audioFiles = allFiles.Where(f => audioExts.Contains(Path.GetExtension(f))).OrderBy(f => f).ToList();

        if (imageFiles.Count == 0 && audioFiles.Count == 0) return;

        var maxCount = Math.Max(imageFiles.Count, audioFiles.Count);
        AppendLog($"[Kéo thả] Đang xử lý {imageFiles.Count} ảnh và {audioFiles.Count} audio...");

        for (int i = 0; i < maxCount; i++)
        {
            var scene = new SceneItem
            {
                Index = Scenes.Count + 1,
                Status = "Đang nạp..."
            };

            if (i < imageFiles.Count)
                scene.ImagePath = imageFiles[i];

            if (i < audioFiles.Count)
            {
                scene.AudioPath = audioFiles[i];
                var duration = await _ffmpegService.GetAudioDurationAsync(audioFiles[i]);
                scene.DurationSeconds = duration;
            }

            scene.Status = (scene.HasImage && scene.HasAudio) ? "Sẵn sàng" : "Chưa đủ file";
            Scenes.Add(scene);
        }

        ReindexScenes();
        AppendLog($"[Kéo thả] Đã thêm thành công {maxCount} phân cảnh mới!");
    }
}
