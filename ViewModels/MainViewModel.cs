using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
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
    private readonly BgmService _bgmService;
    private readonly GoogleAuthService _authService;
    private readonly GoogleTtsService _ttsService;
    private readonly VertexImagenService _imagenService;
    private readonly GeminiScriptService _geminiService;
    private CancellationTokenSource? _cts;
    private MediaPlayer? _bgmPlayer;

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
    private List<MotionEffectOption> _availableMotionEffects = MotionEffectOption.GetAllOptions();

    [ObservableProperty]
    private MotionEffectOption? _selectedMotionEffect;

    [ObservableProperty]
    private bool _enableFadeTransition = true;

    [ObservableProperty]
    private bool _enableVignette = false;

    [ObservableProperty]
    private bool _enableBgm = false;

    [ObservableProperty]
    private string? _bgmPath;

    [ObservableProperty]
    private double _bgmVolume = 0.15; // 15%

    public string BgmVolumePercentageText => $"{(int)(BgmVolume * 100)}%";

    public ObservableCollection<BgmTrack> AvailableBgmTracks { get; } = new();

    [ObservableProperty]
    private BgmTrack? _selectedBgmTrack;

    [ObservableProperty]
    private bool _isPlayingBgm;

    [ObservableProperty]
    private string _playingBgmTitle = string.Empty;

    public string BgmPlayButtonIcon => IsPlayingBgm ? "⏹" : "🎧";
    public string BgmPlayButtonText => IsPlayingBgm ? "Dừng" : "Nghe thử";

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

    // ── Ước Tính Chi Phí Google Cloud API ──
    public const double ImagenCostPerImageUsd = 0.030; // $0.03 / ảnh Imagen 3
    public const double TtsCostPerCharUsd = 0.000016;  // $16 / 1M ký tự WaveNet/Neural2
    public const double GeminiCostPerScriptUsd = 0.0005; // ~$0.0005 Gemini Flash
    public const double UsdToVndRate = 25400.0; // 1 USD ≈ 25,400 VND

    [ObservableProperty]
    private string _estimatedTotalCostDisplay = "$0.00";

    [ObservableProperty]
    private string _estimatedTotalCostVndDisplay = "(0đ)";

    [ObservableProperty]
    private string _estimatedImagenCostText = "0 ảnh • $0.00";

    [ObservableProperty]
    private string _estimatedTtsCostText = "0 ký tự • $0.00";

    [ObservableProperty]
    private string _estimatedCostTooltip = "Chưa có phân cảnh nào để ước tính chi phí.";

    [ObservableProperty]
    private bool _hasEstimatedCost;

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
        _selectedMotionEffect = _availableMotionEffects.FirstOrDefault();
        _bgmService = new BgmService(_ffmpegService);

        Scenes.CollectionChanged += (s, e) =>
        {
            if (e.NewItems != null)
            {
                foreach (SceneItem item in e.NewItems)
                {
                    item.PropertyChanged += OnSceneItemPropertyChanged;
                }
            }
            if (e.OldItems != null)
            {
                foreach (SceneItem item in e.OldItems)
                {
                    item.PropertyChanged -= OnSceneItemPropertyChanged;
                }
            }
            UpdateCostEstimation();
        };

        CheckServicesAvailability();
        InitializeBgmTracksAsync();
        UpdateCostEstimation();
    }

    private void OnSceneItemPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(SceneItem.ImagePath) or nameof(SceneItem.AudioPath) or nameof(SceneItem.Text))
        {
            UpdateCostEstimation();
        }
    }

    public void UpdateCostEstimation()
    {
        if (Scenes.Count == 0)
        {
            EstimatedTotalCostDisplay = "$0.00";
            EstimatedTotalCostVndDisplay = "(0đ)";
            EstimatedImagenCostText = "0 ảnh • $0.00";
            EstimatedTtsCostText = "0 ký tự • $0.00";
            HasEstimatedCost = false;
            EstimatedCostTooltip = "Chưa có phân cảnh nào. Hãy bấm '✨ Soạn & Nạp Kịch Bản' để lên kịch bản và ước tính chi phí.";
            return;
        }

        HasEstimatedCost = true;
        int totalScenes = Scenes.Count;
        int imagesNeeded = Scenes.Count(s => !s.HasImage);
        int totalChars = Scenes.Sum(s => s.Text?.Trim().Length ?? 0);
        int charsNeeded = Scenes.Where(s => !s.HasAudio).Sum(s => s.Text?.Trim().Length ?? 0);

        // Chi phí cho các tài nguyên chưa có và cần sinh khi bấm nút
        double imagenCostUsd = imagesNeeded * ImagenCostPerImageUsd;
        double ttsCostUsd = charsNeeded * TtsCostPerCharUsd;
        double totalNeededUsd = imagenCostUsd + ttsCostUsd;
        double totalNeededVnd = totalNeededUsd * UsdToVndRate;

        // Chi phí chuẩn của toàn bộ kịch bản từ A-Z
        double fullVideoUsd = (totalScenes * ImagenCostPerImageUsd) + (totalChars * TtsCostPerCharUsd) + GeminiCostPerScriptUsd;
        double fullVideoVnd = fullVideoUsd * UsdToVndRate;

        if (imagesNeeded == 0 && charsNeeded == 0)
        {
            EstimatedTotalCostDisplay = "$0.00";
            EstimatedTotalCostVndDisplay = "(Đã có đủ ảnh & audio)";
            EstimatedImagenCostText = $"{totalScenes} ảnh • Đã tạo sẵn";
            EstimatedTtsCostText = $"{totalChars:N0} ký tự • Đã tạo sẵn";
        }
        else
        {
            EstimatedTotalCostDisplay = $"~${totalNeededUsd:F2} USD";
            EstimatedTotalCostVndDisplay = $"(~{totalNeededVnd:N0} đ)";

            if (imagesNeeded == totalScenes)
            {
                EstimatedImagenCostText = $"{imagesNeeded} ảnh • ~${imagenCostUsd:F2}";
            }
            else
            {
                EstimatedImagenCostText = $"{imagesNeeded}/{totalScenes} ảnh cần vẽ • ~${imagenCostUsd:F2}";
            }

            if (charsNeeded == totalChars)
            {
                EstimatedTtsCostText = $"{charsNeeded:N0} ký tự • ~${ttsCostUsd:F3}";
            }
            else
            {
                EstimatedTtsCostText = $"{charsNeeded:N0}/{totalChars:N0} ký tự • ~${ttsCostUsd:F3}";
            }
        }

        EstimatedCostTooltip = 
            $"📊 BẢNG TÍNH CHI PHÍ GOOGLE CLOUD CHO VIDEO NÀY:\n\n" +
            $"1. 🎨 Vertex AI Imagen 3 (Vẽ ảnh minh họa):\n" +
            $"   • Cần vẽ mới: {imagesNeeded}/{totalScenes} ảnh\n" +
            $"   • Đơn giá: $0.030 USD / ảnh (~{0.030 * UsdToVndRate:N0} đ)\n" +
            $"   • Tạm tính: ~${imagenCostUsd:F3} USD (~{imagenCostUsd * UsdToVndRate:N0} đ)\n\n" +
            $"2. 🎙️ Google Cloud Text-to-Speech (Thuyết minh):\n" +
            $"   • Cần đọc: {charsNeeded:N0}/{totalChars:N0} ký tự\n" +
            $"   • Đơn giá WaveNet/Neural2: $0.000016 USD / ký tự ($16/1M ký tự)\n" +
            $"   • Tạm tính: ~${ttsCostUsd:F4} USD (~{ttsCostUsd * UsdToVndRate:N0} đ)\n" +
            $"   *(Lưu ý: Google Cloud miễn phí 1.000.000 ký tự TTS đầu tiên mỗi tháng!)\n\n" +
            $"3. 🤖 Gemini 2.5 Flash (Kịch bản & Lập kế hoạch):\n" +
            $"   • Tạm tính: ~$0.0005 USD (~13 đ)\n\n" +
            $"4. 🎞️ FFmpeg dựng video & Nhạc nền BGM:\n" +
            $"   • Hoàn toàn miễn phí 0đ (Xử lý offline trực tiếp trên máy tính)\n\n" +
            $"═══════════════════════════════════════════════════\n" +
            $"👉 CHI PHÍ CẦN TẠO NGAY: ~${totalNeededUsd:F3} USD (~{totalNeededVnd:N0} VNĐ)\n" +
            $"(Chi phí trọn gói cả video từ đầu: ~${fullVideoUsd:F3} USD ≈ {fullVideoVnd:N0} VNĐ)";
    }

    private async void InitializeBgmTracksAsync()
    {
        try
        {
            await _bgmService.EnsureBuiltInTracksAsync();
            ReloadBgmTracks();
            SelectedBgmTrack = AvailableBgmTracks.FirstOrDefault(t => t.Id == "none") 
                               ?? AvailableBgmTracks.FirstOrDefault();
        }
        catch (Exception ex)
        {
            AppendLog($"[BGM] Lỗi khởi tạo thư viện nhạc: {ex.Message}");
        }
    }

    partial void OnIsPlayingBgmChanged(bool value)
    {
        OnPropertyChanged(nameof(BgmPlayButtonIcon));
        OnPropertyChanged(nameof(BgmPlayButtonText));
    }

    partial void OnSelectedMotionEffectChanged(MotionEffectOption? value)
    {
        EnableKenBurns = value != null && value.Id != "none";
    }

    partial void OnSelectedBgmTrackChanged(BgmTrack? value)
    {
        if (IsPlayingBgm)
        {
            StopBgmPreview();
        }

        if (value == null || value.Id == "none")
        {
            EnableBgm = false;
            BgmPath = null;
        }
        else
        {
            EnableBgm = true;
            BgmPath = value.FilePath;
        }
    }

    partial void OnEnableBgmChanged(bool value)
    {
        if (!value && IsPlayingBgm)
        {
            StopBgmPreview();
        }
        else if (value && (SelectedBgmTrack == null || SelectedBgmTrack.Id == "none"))
        {
            SelectedBgmTrack = AvailableBgmTracks.FirstOrDefault(t => t.Id != "none") 
                               ?? AvailableBgmTracks.FirstOrDefault();
        }
    }

    partial void OnBgmVolumeChanged(double value)
    {
        OnPropertyChanged(nameof(BgmVolumePercentageText));
        if (_bgmPlayer != null)
        {
            _bgmPlayer.Volume = value;
        }
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
        var win = new ScriptGeneratorWindow(_geminiService, _ttsService, _bgmService)
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

        if (workspace.Metadata != null)
        {
            if (!string.IsNullOrEmpty(workspace.Metadata.MotionEffect))
            {
                var opt = AvailableMotionEffects.FirstOrDefault(m => m.Id == workspace.Metadata.MotionEffect)
                          ?? AvailableMotionEffects.FirstOrDefault(m => m.Id == "random");
                if (opt != null) SelectedMotionEffect = opt;
            }
            EnableFadeTransition = workspace.Metadata.EnableFade;
            EnableVignette = workspace.Metadata.EnableVignette;

            if (workspace.Metadata.EnableMusic)
            {
                EnableBgm = true;
                BgmVolume = workspace.Metadata.MusicVolume > 0 ? workspace.Metadata.MusicVolume : 0.15;
                if (!string.IsNullOrEmpty(workspace.Metadata.Music))
                {
                    var match = AvailableBgmTracks.FirstOrDefault(t =>
                        t.Id.Equals(workspace.Metadata.Music, StringComparison.OrdinalIgnoreCase) ||
                        t.Mood.Equals(workspace.Metadata.Music, StringComparison.OrdinalIgnoreCase) ||
                        (File.Exists(workspace.Metadata.Music) && t.FilePath.Equals(workspace.Metadata.Music, StringComparison.OrdinalIgnoreCase)));

                    if (match != null)
                    {
                        SelectedBgmTrack = match;
                    }
                    else if (File.Exists(workspace.Metadata.Music))
                    {
                        BgmPath = workspace.Metadata.Music;
                    }
                }
                else
                {
                    SelectedBgmTrack = AvailableBgmTracks.FirstOrDefault(t => t.Id != "none") 
                                       ?? AvailableBgmTracks.FirstOrDefault();
                }
            }
            else
            {
                EnableBgm = false;
                SelectedBgmTrack = AvailableBgmTracks.FirstOrDefault(t => t.Id == "none");
                BgmPath = null;
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
                MotionEffect = string.IsNullOrEmpty(sc.MotionEffect) ? "zoom_in" : sc.MotionEffect,
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
        AppendLog($"[Phân cảnh #{target.Index}] Đang gửi yêu cầu tới Gemini Image...");

        try
        {
            var tempDir = Path.Combine(OutputDirectory, "generated_assets");
            Directory.CreateDirectory(tempDir);
            var imagePath = Path.Combine(tempDir, $"image_scene_{target.Index:D3}_{DateTime.Now:HHmmss}.png");

            await _imagenService.GenerateImageAsync(target.ImagePrompt, imagePath, AspectRatio, onLog: msg => AppendLog(msg));
            target.ImagePath = imagePath;

            target.Status = target.HasAudio ? "Sẵn sàng" : "Thiếu audio";
            AppendLog($"[Phân cảnh #{target.Index}] Vẽ ảnh Gemini Image thành công!");
        }
        catch (Exception ex)
        {
            target.Status = "Lỗi sinh ảnh";
            AppendLog($"[Lỗi Gemini Image Cảnh #{target.Index}] {ex.Message}");
            MessageBox.Show($"Lỗi khi tạo hình ảnh:\n{ex.Message}", "Lỗi Gemini Image", MessageBoxButton.OK, MessageBoxImage.Error);
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

            var existing = AvailableBgmTracks.FirstOrDefault(t => t.FilePath == ofd.FileName);
            if (existing != null)
            {
                SelectedBgmTrack = existing;
            }
            else
            {
                var customTrack = new BgmTrack
                {
                    Id = Path.GetFileNameWithoutExtension(ofd.FileName).ToLowerInvariant(),
                    Title = $"📁 {Path.GetFileNameWithoutExtension(ofd.FileName)}",
                    Mood = "custom",
                    FilePath = ofd.FileName,
                    SourceTag = "Tệp ngoài",
                    IsCustom = true
                };
                AvailableBgmTracks.Add(customTrack);
                SelectedBgmTrack = customTrack;
            }

            AppendLog($"Đã chọn nhạc nền: {Path.GetFileName(ofd.FileName)}");
        }
    }

    [RelayCommand]
    private void TogglePreviewBgm()
    {
        if (IsPlayingBgm)
        {
            StopBgmPreview();
            return;
        }

        if (string.IsNullOrEmpty(BgmPath) || !File.Exists(BgmPath))
        {
            MessageBox.Show("Vui lòng chọn một bản nhạc nền có sẵn hoặc chọn tệp từ máy tính để nghe thử.", "Chưa có nhạc nền", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        try
        {
            StopBgmPreview();
            var fullPath = Path.GetFullPath(BgmPath);
            _bgmPlayer = new MediaPlayer();
            _bgmPlayer.MediaEnded += (s, e) => Application.Current.Dispatcher.Invoke(StopBgmPreview);
            _bgmPlayer.MediaFailed += (s, e) => Application.Current.Dispatcher.Invoke(() =>
            {
                AppendLog($"[Lỗi phát BGM] Không thể phát '{Path.GetFileName(fullPath)}': {e.ErrorException?.Message}");
                StopBgmPreview();
            });
            _bgmPlayer.Open(new Uri(fullPath));
            _bgmPlayer.Volume = Math.Clamp(BgmVolume, 0.05, 1.0);
            _bgmPlayer.Play();
            IsPlayingBgm = true;
            PlayingBgmTitle = SelectedBgmTrack?.Title ?? Path.GetFileName(fullPath);
            AppendLog($"[BGM] Đang phát thử: {PlayingBgmTitle} (Âm lượng: {BgmVolumePercentageText})");
        }
        catch (Exception ex)
        {
            StopBgmPreview();
            AppendLog($"[Lỗi phát BGM] {ex.Message}");
            MessageBox.Show($"Không thể phát thử nhạc nền:\n{ex.Message}", "Lỗi phát nhạc", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void StopBgmPreview()
    {
        try
        {
            _bgmPlayer?.Stop();
            _bgmPlayer?.Close();
        }
        catch { }
        finally
        {
            _bgmPlayer = null;
            IsPlayingBgm = false;
            PlayingBgmTitle = string.Empty;
        }
    }

    [RelayCommand]
    private void OpenBgmFolder()
    {
        _bgmService.OpenBgmDirectoryInExplorer();
        ReloadBgmTracks();
    }

    [RelayCommand]
    private void ReloadBgmTracks()
    {
        var currentPath = BgmPath;
        AvailableBgmTracks.Clear();
        foreach (var track in _bgmService.GetAvailableTracks())
        {
            AvailableBgmTracks.Add(track);
        }

        if (!string.IsNullOrEmpty(currentPath))
        {
            SelectedBgmTrack = AvailableBgmTracks.FirstOrDefault(t => t.FilePath == currentPath) 
                ?? AvailableBgmTracks.FirstOrDefault(t => t.Id == "none");
        }
    }

    [RelayCommand]
    private void OpenFreeMusicSources()
    {
        var message = "Các nguồn tải nhạc nền MIỄN PHÍ 100% & KHÔNG BẢN QUYỀN (Content ID Safe):\n\n" +
                      "1. YouTube Audio Library:\n" +
                      "   https://studio.youtube.com/channel/UC/music\n" +
                      "   (Kho chính thức của YouTube, an toàn tuyệt đối khi đăng video)\n\n" +
                      "2. Pixabay Music:\n" +
                      "   https://pixabay.com/music/\n" +
                      "   (Hàng ngàn track cực hay cho video, tự do thương mại)\n\n" +
                      "3. Chosic Free Music:\n" +
                      "   https://www.chosic.com/free-music/\n" +
                      "   (Lọc theo Mood/Thể loại, hỗ trợ lọc chuẩn CC0 & YouTube safe)\n\n" +
                      "4. Tạo nhạc độc quyền bằng AI (Suno AI):\n" +
                      "   https://suno.com/\n\n" +
                      "Mẹo: Sau khi tải file MP3 về, bấm nút [📂 Thư mục] trong app và dán (paste) file vào đó để app tự nhận diện!";

        var result = MessageBox.Show(
            message + "\n\nBạn có muốn mở trang web Pixabay Music ngay bây giờ không?", 
            "Kho Nhạc Nền Miễn Phí Không Bản Quyền", 
            MessageBoxButton.YesNo, 
            MessageBoxImage.Information);

        if (result == MessageBoxResult.Yes)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "https://pixabay.com/music/",
                    UseShellExecute = true
                });
            }
            catch { }
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
                        StatusMessage = $"Đang gọi Gemini Image vẽ ảnh cảnh {sNum}/{Scenes.Count}...";
                        AppendLog($"[Tự động] Cảnh #{sNum}: Đang gửi prompt tới Gemini Image...");
                        s.IsGeneratingImage = true;
                        var imgOut = Path.Combine(tempAssetsDir, $"image_scene_{sNum:D3}_{DateTime.Now:HHmmss}.png");
                        try
                        {
                            await _imagenService.GenerateImageAsync(s.ImagePrompt, imgOut, AspectRatio, onLog: msg => AppendLog(msg), ct: ct);
                            s.ImagePath = imgOut;
                        }
                        catch (Exception ex)
                        {
                            // Cơ chế dự phòng (Fallback): Nếu bị hạn mức Quota và phân cảnh trước đã có ảnh, tái sử dụng để video không bị đứt đoạn
                            var prevSceneWithImage = Scenes.Take(i).LastOrDefault(x => !string.IsNullOrEmpty(x.ImagePath) && File.Exists(x.ImagePath));
                            if (prevSceneWithImage != null)
                            {
                                AppendLog($"[Dự phòng Quota] Cảnh #{sNum} không thể tạo ảnh mới ({ex.Message}). Sử dụng lại ảnh từ cảnh #{prevSceneWithImage.Index} để tiếp tục tạo video!");
                                File.Copy(prevSceneWithImage.ImagePath!, imgOut, true);
                                s.ImagePath = imgOut;
                            }
                            else
                            {
                                throw;
                            }
                        }
                        finally
                        {
                            s.IsGeneratingImage = false;
                        }

                        // Nghỉ một chút giữa các phân cảnh để tránh vượt hạn mức Quota (RPM) của Vertex AI
                        await Task.Delay(2500, ct);
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
                MotionEffect = SelectedMotionEffect?.Id ?? "random",
                EnableFadeTransition = EnableFadeTransition,
                EnableVignette = EnableVignette,
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
