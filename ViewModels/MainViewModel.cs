using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Text;
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

    // ── Cấu Hình Chữ Chạy / Phụ Đề Video ──
    [ObservableProperty]
    private bool _enableSubtitles = true;

    public ObservableCollection<SubtitleStyleOption> AvailableSubtitleStyles { get; } = new(SubtitleStyleOption.GetAllOptions());

    [ObservableProperty]
    private SubtitleStyleOption? _selectedSubtitleStyle;

    public ObservableCollection<SubtitleFontOption> AvailableSubtitleFonts { get; } = new(SubtitleFontOption.GetAllFonts());

    [ObservableProperty]
    private SubtitleFontOption? _selectedSubtitleFont;

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

    // ── Quản Lý Chuỗi Video Nhiều Tập (Series Mode) ──
    [ObservableProperty]
    private SeriesProject? _currentSeries;

    [ObservableProperty]
    private bool _isSeriesMode;

    [ObservableProperty]
    private EpisodeItem? _selectedEpisode;

    [ObservableProperty]
    private bool _isBatchRenderingSeries;

    [ObservableProperty]
    private double _batchRenderProgress;

    [ObservableProperty]
    private string _batchRenderStatusText = string.Empty;

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

    // ── Bộ Công Cụ Đăng Bài Mạng Xã Hội (Publish Info) ──
    [ObservableProperty]
    private VideoPublishInfo _currentPublishInfo = new();

    [ObservableProperty]
    private string _lastGeneratedTxtPath = string.Empty;

    [ObservableProperty]
    private string _copyFeedbackMessage = string.Empty;

    [ObservableProperty]
    private bool _isGeneratingPublishInfo;

    public bool HasPublishInfo => !string.IsNullOrWhiteSpace(CurrentPublishInfo?.Title) || !string.IsNullOrWhiteSpace(CurrentPublishInfo?.Hashtags);

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
        _selectedSubtitleStyle = AvailableSubtitleStyles.FirstOrDefault();
        _selectedSubtitleFont = AvailableSubtitleFonts.FirstOrDefault();
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
            EnableSubtitles = workspace.Metadata.EnableSubtitles;

            if (!string.IsNullOrEmpty(workspace.Metadata.SubtitleStyle))
            {
                var subStyle = AvailableSubtitleStyles.FirstOrDefault(s => s.Id.Equals(workspace.Metadata.SubtitleStyle, StringComparison.OrdinalIgnoreCase));
                if (subStyle != null) SelectedSubtitleStyle = subStyle;
            }

            if (!string.IsNullOrEmpty(workspace.Metadata.SubtitleFont))
            {
                var subFont = AvailableSubtitleFonts.FirstOrDefault(f => 
                    f.Id.Equals(workspace.Metadata.SubtitleFont, StringComparison.OrdinalIgnoreCase) || 
                    f.Name.Equals(workspace.Metadata.SubtitleFont, StringComparison.OrdinalIgnoreCase));
                if (subFont != null) SelectedSubtitleFont = subFont;
            }

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

        // Áp dụng thông tin đăng bài (Tiêu đề, Mô tả, Hashtags)
        if (workspace.PublishInfo != null)
        {
            CurrentPublishInfo = workspace.PublishInfo;
            OnPropertyChanged(nameof(CurrentPublishInfo));
            OnPropertyChanged(nameof(HasPublishInfo));

            if (!string.IsNullOrWhiteSpace(workspace.PublishInfo.Title))
            {
                var safeTitle = workspace.PublishInfo.Title;
                foreach (var c in Path.GetInvalidFileNameChars())
                    safeTitle = safeTitle.Replace(c, '_');
                safeTitle = safeTitle.Replace(' ', '_').Trim('_');
                if (safeTitle.Length > 45) safeTitle = safeTitle.Substring(0, 45);
                OutputFileName = $"{safeTitle}.mp4";
            }
        }
        else
        {
            EnsureBasicPublishInfo();
        }

        AppendLog($"[Kịch bản] Đã nạp thành công {Scenes.Count} phân cảnh vào dự án!");
        MessageBox.Show($"Đã nạp thành công {Scenes.Count} phân cảnh vào dự án!", "Thành công", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    // ══════════════════════════════════════════════════════════════
    // QUẢN LÝ CHUỖI VIDEO NHIỀU TẬP (MULTI-EPISODE SERIES MODE)
    // ══════════════════════════════════════════════════════════════

    [RelayCommand]
    private void OpenSeriesCreator()
    {
        var win = new SeriesCreatorWindow(_geminiService)
        {
            Owner = Application.Current.MainWindow
        };

        if (win.ShowDialog() == true && win.ResultSeriesProject != null)
        {
            LoadSeriesProject(win.ResultSeriesProject);
        }
    }

    public void LoadSeriesProject(SeriesProject series)
    {
        CurrentSeries = series;
        IsSeriesMode = true;

        if (!string.IsNullOrEmpty(series.AspectRatio))
            AspectRatio = series.AspectRatio;

        if (!string.IsNullOrEmpty(series.GlobalVoice) && AvailableVoices.Contains(series.GlobalVoice))
            SelectedVoice = series.GlobalVoice;

        if (series.Episodes.Count > 0)
        {
            SelectEpisode(series.Episodes[0]);
        }
        else
        {
            Scenes.Clear();
            SelectedEpisode = null;
        }

        StatusMessage = $"Đang làm việc trên Series: {series.SeriesTitle} ({series.Episodes.Count} tập)";
        AppendLog($"[Series] Đã nạp thành công series '{series.SeriesTitle}' ({series.Episodes.Count} tập).");
    }

    [RelayCommand]
    private void SelectEpisode(EpisodeItem? ep)
    {
        if (ep == null) return;

        // Lưu trạng thái của tập hiện tại trước khi chuyển sang tập mới
        if (SelectedEpisode != null && SelectedEpisode != ep)
        {
            SelectedEpisode.Scenes = new ObservableCollection<SceneItem>(Scenes);
            SelectedEpisode.PublishInfo = CurrentPublishInfo;
        }

        SelectedEpisode = ep;

        // Cập nhật trạng thái IsSelected cho tất cả các tập
        if (CurrentSeries != null)
        {
            foreach (var item in CurrentSeries.Episodes)
            {
                item.IsSelected = (item == ep);
            }
        }

        // Nạp danh sách cảnh của tập được chọn
        Scenes.Clear();
        foreach (var sc in ep.Scenes)
        {
            Scenes.Add(sc);
        }

        // Nạp thông tin đăng bài của tập
        if (ep.PublishInfo != null)
        {
            CurrentPublishInfo = ep.PublishInfo;
        }
        else
        {
            EnsureBasicPublishInfo();
            CurrentPublishInfo.Title = $"{CurrentSeries?.SeriesTitle ?? "Series"} - Tập {ep.EpisodeNumber}: {ep.EpisodeTitle}";
            CurrentPublishInfo.Description = $"Xem trọn bộ {CurrentSeries?.SeriesTitle}!\nTập {ep.EpisodeNumber}: {ep.EpisodeTitle}\n{ep.EpisodeHook}\n{ep.Cliffhanger}";
            ep.PublishInfo = CurrentPublishInfo;
        }
        OnPropertyChanged(nameof(CurrentPublishInfo));
        OnPropertyChanged(nameof(HasPublishInfo));

        // Cập nhật tên file video dự kiến
        var safeTitle = MakeSafeFileName(ep.EpisodeTitle);
        OutputFileName = $"Tap_{ep.EpisodeNumber:D2}_{safeTitle}.mp4";

        StatusMessage = $"Đang chọn Tập {ep.EpisodeNumber}: {ep.EpisodeTitle}";
        AppendLog($"[Series] Đã chuyển sang Tập {ep.EpisodeNumber}: {ep.EpisodeTitle} ({Scenes.Count} phân cảnh).");
    }

    [RelayCommand]
    private void AddNewEpisode()
    {
        if (CurrentSeries == null) return;

        var nextNum = CurrentSeries.Episodes.Count + 1;
        var newEp = new EpisodeItem
        {
            EpisodeNumber = nextNum,
            EpisodeTitle = $"Tập {nextNum}: Tiếp nối câu chuyện",
            EpisodeHook = "Tiếp nối phần trước...",
            Cliffhanger = "Chuyện gì sẽ xảy ra tiếp theo?",
            Status = "Chưa dựng"
        };
        CurrentSeries.Episodes.Add(newEp);
        SelectEpisode(newEp);
        AppendLog($"[Series] Đã thêm Tập {nextNum} vào chuỗi.");
    }

    [RelayCommand]
    private void RemoveEpisode(EpisodeItem? ep)
    {
        if (CurrentSeries == null || ep == null) return;

        if (MessageBox.Show($"Bạn có chắc chắn muốn xóa Tập {ep.EpisodeNumber}: {ep.EpisodeTitle} khỏi Series?", "Xác nhận", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
        {
            var idx = CurrentSeries.Episodes.IndexOf(ep);
            CurrentSeries.Episodes.Remove(ep);

            // Đánh lại số thứ tự các tập
            for (int i = 0; i < CurrentSeries.Episodes.Count; i++)
            {
                CurrentSeries.Episodes[i].EpisodeNumber = i + 1;
            }

            if (SelectedEpisode == ep)
            {
                if (CurrentSeries.Episodes.Count > 0)
                {
                    var newIdx = Math.Min(idx, CurrentSeries.Episodes.Count - 1);
                    SelectEpisode(CurrentSeries.Episodes[newIdx]);
                }
                else
                {
                    SelectedEpisode = null;
                    Scenes.Clear();
                }
            }
            AppendLog($"[Series] Đã xóa tập phim khỏi series.");
        }
    }

    [RelayCommand]
    private async Task ExitSeriesModeAsync()
    {
        if (SelectedEpisode != null)
        {
            SelectedEpisode.Scenes = new ObservableCollection<SceneItem>(Scenes);
            SelectedEpisode.PublishInfo = CurrentPublishInfo;
        }

        var result = MessageBox.Show(
            "Bạn có muốn lưu dự án Series (.series.json) trước khi thoát về chế độ video đơn lẻ không?",
            "Thoát chế độ Series",
            MessageBoxButton.YesNoCancel,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Cancel) return;

        if (result == MessageBoxResult.Yes && CurrentSeries != null)
        {
            await SaveSeriesProjectAsync();
        }

        IsSeriesMode = false;
        StatusMessage = "Đã thoát chế độ Series. Trở lại chế độ video đơn lẻ.";
        AppendLog("[Series] Đã thoát chế độ Series.");
    }

    [RelayCommand]
    private async Task SaveSeriesProjectAsync()
    {
        if (CurrentSeries == null) return;

        if (SelectedEpisode != null)
        {
            SelectedEpisode.Scenes = new ObservableCollection<SceneItem>(Scenes);
            SelectedEpisode.PublishInfo = CurrentPublishInfo;
        }

        var safeName = MakeSafeFileName(CurrentSeries.SeriesTitle);
        var dlg = new SaveFileDialog
        {
            Title = "Lưu dự án Series",
            Filter = "Series Project (*.series.json)|*.series.json|JSON File (*.json)|*.json",
            FileName = $"{safeName}.series.json"
        };

        if (dlg.ShowDialog() == true)
        {
            try
            {
                await CurrentSeries.SaveToFileAsync(dlg.FileName);
                AppendLog($"[Series] Đã lưu dự án Series vào: {dlg.FileName}");
                MessageBox.Show("Đã lưu dự án Series thành công!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi lưu Series:\n{ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    [RelayCommand]
    private async Task LoadSeriesProjectFromFileAsync()
    {
        var dlg = new OpenFileDialog
        {
            Title = "Mở dự án Series",
            Filter = "Series Project (*.series.json;*.json)|*.series.json;*.json|Tất cả tệp (*.*)|*.*"
        };

        if (dlg.ShowDialog() == true)
        {
            try
            {
                var series = await SeriesProject.LoadFromFileAsync(dlg.FileName);
                if (series != null)
                {
                    LoadSeriesProject(series);
                }
                else
                {
                    MessageBox.Show("Không thể đọc dữ liệu dự án Series từ tệp được chọn!", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi mở Series:\n{ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private static string MakeSafeFileName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "video";
        var safe = name;
        foreach (var c in Path.GetInvalidFileNameChars())
            safe = safe.Replace(c, '_');
        safe = safe.Replace(' ', '_').Trim('_');
        return safe.Length > 40 ? safe.Substring(0, 40) : safe;
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
                    MusicVolume = BgmVolume,
                    EnableSubtitles = EnableSubtitles,
                    SubtitleStyle = SelectedSubtitleStyle?.Id ?? "cinematic",
                    SubtitleFont = SelectedSubtitleFont?.Id ?? "Segoe UI Bold"
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

    private async Task EnsureAssetsForScenesAsync(List<SceneItem> targetScenes, string assetsDir, CancellationToken ct)
    {
        for (int i = 0; i < targetScenes.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            var s = targetScenes[i];
            var sNum = i + 1;

            if (!s.HasAudio)
            {
                if (!string.IsNullOrWhiteSpace(s.Text))
                {
                    StatusMessage = $"Đang sinh giọng đọc cho cảnh {sNum}/{targetScenes.Count}...";
                    AppendLog($"[Tự động] Cảnh #{sNum}: Đang gọi Google TTS tạo lời thoại...");
                    s.IsGeneratingAudio = true;
                    var audioOut = Path.Combine(assetsDir, $"voice_scene_{sNum:D3}_{DateTime.Now:HHmmss}.mp3");
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
                    StatusMessage = $"Đang gọi Gemini Image vẽ ảnh cảnh {sNum}/{targetScenes.Count}...";
                    AppendLog($"[Tự động] Cảnh #{sNum}: Đang gửi prompt tới Gemini Image...");
                    s.IsGeneratingImage = true;
                    var imgOut = Path.Combine(assetsDir, $"image_scene_{sNum:D3}_{DateTime.Now:HHmmss}.png");
                    try
                    {
                        await _imagenService.GenerateImageAsync(s.ImagePrompt, imgOut, AspectRatio, onLog: msg => AppendLog(msg), ct: ct);
                        s.ImagePath = imgOut;
                    }
                    catch (Exception ex)
                    {
                        // Cơ chế dự phòng (Fallback): Nếu bị hạn mức Quota và phân cảnh trước đã có ảnh, tái sử dụng để video không bị đứt đoạn
                        var prevSceneWithImage = targetScenes.Take(i).LastOrDefault(x => !string.IsNullOrEmpty(x.ImagePath) && File.Exists(x.ImagePath));
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
            await EnsureAssetsForScenesAsync(Scenes.ToList(), tempAssetsDir, ct);

            EnsureBasicPublishInfo();

            var config = new VideoConfig
            {
                AspectRatio = AspectRatio,
                MotionEffect = SelectedMotionEffect?.Id ?? "random",
                EnableFadeTransition = EnableFadeTransition,
                EnableVignette = EnableVignette,
                EnableBackgroundMusic = EnableBgm,
                BackgroundMusicPath = BgmPath,
                BackgroundMusicVolume = BgmVolume,
                EnableSubtitles = EnableSubtitles,
                SubtitleStyle = SelectedSubtitleStyle?.Id ?? "cinematic",
                SubtitleFont = SelectedSubtitleFont?.Id ?? "Segoe UI Bold",
                Voice = SelectedVoice,
                OutputDirectory = OutputDirectory,
                OutputFileName = string.IsNullOrWhiteSpace(OutputFileName) ? $"video_{DateTime.Now:yyyyMMdd_HHmmss}.mp4" : OutputFileName,
                PublishInfo = CurrentPublishInfo
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

            // Lấy thư mục project (parent của Video/)
            var projectFolder = Path.GetDirectoryName(Path.GetDirectoryName(resultFile)) ?? OutputDirectory;
            var txtFile = Path.Combine(projectFolder, "DANG_BAI_METADATA.txt");
            if (File.Exists(txtFile))
            {
                LastGeneratedTxtPath = txtFile;
            }

            var dialogResult = MessageBox.Show(
                $"🎉 Video đã được tạo thành công!\n\n" +
                $"📁 Thư mục project:\n{projectFolder}\n\n" +
                $"🎬 Video:\n{resultFile}\n\n" +
                $"📝 File đăng bài (.txt):\n{txtFile}\n\n" +
                $"👉 Bạn có muốn mở ngay 'Bộ Đăng Bài Mạng Xã Hội' để sao chép Tiêu đề, Caption và Hashtags không?",
                "Tạo Video Thành Công!",
                MessageBoxButton.YesNo,
                MessageBoxImage.Information);

            if (dialogResult == MessageBoxResult.Yes)
            {
                OpenSocialPublishWindow();
            }
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
    private async Task StartBatchRenderSeriesAsync()
    {
        if (CurrentSeries == null || CurrentSeries.Episodes.Count == 0)
        {
            MessageBox.Show("Không có tập phim nào trong Series để render!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // Đảm bảo tập hiện tại đã được đồng bộ các cảnh
        if (SelectedEpisode != null)
        {
            SelectedEpisode.Scenes = new ObservableCollection<SceneItem>(Scenes);
            SelectedEpisode.PublishInfo = CurrentPublishInfo;
        }

        var episodesToRender = CurrentSeries.Episodes.ToList();
        if (episodesToRender.All(e => e.Scenes.Count == 0))
        {
            MessageBox.Show("Tất cả các tập trong Series đều chưa có kịch bản/phân cảnh!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var confirm = MessageBox.Show(
            $"Bạn sắp bắt đầu BATCH RENDER toàn bộ chuỗi {CurrentSeries.Episodes.Count} tập phim!\n\n" +
            $"Tên Series: {CurrentSeries.SeriesTitle}\n" +
            $"Thư mục xuất: {OutputDirectory}\n\n" +
            $"Quá trình này sẽ tự động sinh giọng đọc Cloud TTS, vẽ ảnh AI Imagen 3 và render video từng tập.\n" +
            $"Bạn có muốn tiếp tục không?",
            "Xác nhận Batch Render Series",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (confirm != MessageBoxResult.Yes) return;

        IsRendering = true;
        IsBatchRenderingSeries = true;
        BatchRenderProgress = 0;
        _cts = new CancellationTokenSource();
        var ct = _cts.Token;

        var safeSeries = MakeSafeFileName(CurrentSeries.SeriesTitle);
        var seriesOutputDir = Path.Combine(OutputDirectory, $"Series_{safeSeries}");
        Directory.CreateDirectory(seriesOutputDir);

        int totalEpisodes = episodesToRender.Count;
        int completedCount = 0;

        try
        {
            for (int i = 0; i < totalEpisodes; i++)
            {
                ct.ThrowIfCancellationRequested();

                var ep = episodesToRender[i];
                var epNum = ep.EpisodeNumber;
                var safeTitle = MakeSafeFileName(ep.EpisodeTitle);

                SelectEpisode(ep);

                BatchRenderStatusText = $"[Tập {epNum}/{totalEpisodes}] Đang xử lý: {ep.EpisodeTitle}...";
                BatchRenderProgress = (double)i / totalEpisodes * 100;
                ep.Status = "Đang render";

                if (ep.Scenes.Count == 0)
                {
                    AppendLog($"[Batch Series] Bỏ qua Tập {epNum} vì chưa có phân cảnh nào.");
                    ep.Status = "Chưa dựng";
                    continue;
                }

                var epTempAssets = Path.Combine(seriesOutputDir, $"temp_assets_ep{epNum:D2}");
                Directory.CreateDirectory(epTempAssets);

                AppendLog($"[Batch Series] === BẮT ĐẦU XỬ LÝ TẬP {epNum}: {ep.EpisodeTitle} ({ep.Scenes.Count} cảnh) ===");

                // 1. Sinh âm thanh & hình ảnh AI cho tập này
                await EnsureAssetsForScenesAsync(ep.Scenes.ToList(), epTempAssets, ct);

                // 2. Cấu hình xuất video cho tập này
                var epConfig = new VideoConfig
                {
                    AspectRatio = AspectRatio,
                    MotionEffect = SelectedMotionEffect?.Id ?? "random",
                    EnableFadeTransition = EnableFadeTransition,
                    EnableVignette = EnableVignette,
                    EnableBackgroundMusic = EnableBgm,
                    BackgroundMusicPath = BgmPath,
                    BackgroundMusicVolume = BgmVolume,
                    EnableSubtitles = EnableSubtitles,
                    SubtitleStyle = SelectedSubtitleStyle?.Id ?? "cinematic",
                    SubtitleFont = SelectedSubtitleFont?.Id ?? "Segoe UI Bold",
                    Voice = SelectedVoice,
                    OutputDirectory = seriesOutputDir,
                    OutputFileName = $"Tap_{epNum:D2}_{safeTitle}.mp4",
                    PublishInfo = ep.PublishInfo ?? CurrentPublishInfo
                };

                var progress = new Progress<GenerationProgress>(p =>
                {
                    if (p.Percentage > 0)
                        ProgressPercentage = p.Percentage;
                    if (!string.IsNullOrEmpty(p.StepTitle))
                        StatusMessage = $"[Tập {epNum}/{totalEpisodes}] {p.StepTitle}";
                    if (!string.IsNullOrEmpty(p.LogMessage))
                        AppendLog($"[Tập {epNum}] {p.LogMessage}");
                });

                // 3. Render video qua FFmpeg
                var resultFile = await _ffmpegService.GenerateVideoAsync(ep.Scenes.ToList(), epConfig, progress, ct);
                ep.OutputVideoPath = resultFile;
                ep.LastRenderedDate = DateTime.Now;
                ep.Status = "Hoàn thành";
                completedCount++;

                // Dọn dẹp folder tạm nếu có
                try
                {
                    if (Directory.Exists(epTempAssets))
                        Directory.Delete(epTempAssets, true);
                }
                catch { }

                AppendLog($"[Batch Series] ✅ Hoàn thành xuất video Tập {epNum}: {resultFile}");

                // Lưu lại trạng thái dự án series vào file series_project.json
                var seriesProjectFile = Path.Combine(seriesOutputDir, "series_project.json");
                await CurrentSeries.SaveToFileAsync(seriesProjectFile);

                BatchRenderProgress = (double)(i + 1) / totalEpisodes * 100;
            }

            BatchRenderProgress = 100;
            BatchRenderStatusText = $"🎉 Hoàn thành render {completedCount}/{totalEpisodes} tập của Series!";
            StatusMessage = "Xuất toàn bộ Series thành công!";

            MessageBox.Show(
                $"🎉 Đã hoàn tất Batch Render toàn bộ chuỗi video!\n\n" +
                $"Số tập hoàn thành: {completedCount}/{totalEpisodes}\n" +
                $"Thư mục Series:\n{seriesOutputDir}\n\n" +
                $"Mỗi tập đã được lưu đầy đủ vào từng thư mục riêng (Images, Audio, Video, DANG_BAI_METADATA.txt)!",
                "Thành Công Rực Rỡ",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (OperationCanceledException)
        {
            BatchRenderStatusText = "Đã dừng render Series.";
            StatusMessage = "Đã hủy render bởi người dùng.";
            AppendLog("[Batch Series] Người dùng đã dừng quá trình render series.");
        }
        catch (Exception ex)
        {
            BatchRenderStatusText = "❌ Có lỗi xảy ra trong quá trình Batch Render.";
            StatusMessage = "Lỗi khi render series!";
            AppendLog($"[Lỗi Batch Series] {ex.Message}");
            MessageBox.Show($"Lỗi trong quá trình render series:\n{ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            foreach (var sc in Scenes)
            {
                sc.IsGeneratingAudio = false;
                sc.IsGeneratingImage = false;
            }
            IsRendering = false;
            IsBatchRenderingSeries = false;
            _cts?.Dispose();
            _cts = null;
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
        // Ưu tiên mở thư mục project (parent của Video/) nếu có video đã tạo
        var targetDir = OutputDirectory;
        if (!string.IsNullOrEmpty(LastGeneratedVideoPath) && File.Exists(LastGeneratedVideoPath))
        {
            var projectDir = Path.GetDirectoryName(Path.GetDirectoryName(LastGeneratedVideoPath));
            if (!string.IsNullOrEmpty(projectDir) && Directory.Exists(projectDir))
                targetDir = projectDir;
        }

        if (Directory.Exists(targetDir))
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = targetDir,
                UseShellExecute = true
            });
        }
    }

    [RelayCommand]
    private void ClearLog()
    {
        LogText = string.Empty;
    }

    [RelayCommand]
    private void OpenSocialPublishWindow()
    {
        EnsureBasicPublishInfo();
        var win = new SocialPublishWindow(this)
        {
            Owner = Application.Current.MainWindow
        };
        win.ShowDialog();
    }

    [RelayCommand]
    private void CopyFullCaption()
    {
        EnsureBasicPublishInfo();
        SetClipboardText(CurrentPublishInfo.FullCaption);
        TriggerCopyFeedback("✅ Đã sao chép toàn bộ bài đăng vào Clipboard!");
        AppendLog("[Clipboard] Đã sao chép toàn bộ bài đăng (Tiêu đề + Mô tả + Hashtags).");
    }

    [RelayCommand]
    private void CopyPublishTitle()
    {
        EnsureBasicPublishInfo();
        SetClipboardText(CurrentPublishInfo.Title);
        TriggerCopyFeedback("✅ Đã sao chép Tiêu đề!");
        AppendLog($"[Clipboard] Đã sao chép Tiêu đề: \"{CurrentPublishInfo.Title}\"");
    }

    [RelayCommand]
    private void CopyPublishDescription()
    {
        EnsureBasicPublishInfo();
        SetClipboardText(CurrentPublishInfo.Description);
        TriggerCopyFeedback("✅ Đã sao chép Mô tả!");
        AppendLog("[Clipboard] Đã sao chép Mô tả video.");
    }

    [RelayCommand]
    private void CopyPublishHashtags()
    {
        EnsureBasicPublishInfo();
        SetClipboardText(CurrentPublishInfo.Hashtags);
        TriggerCopyFeedback("✅ Đã sao chép Hashtags!");
        AppendLog($"[Clipboard] Đã sao chép Hashtags: {CurrentPublishInfo.Hashtags}");
    }

    [RelayCommand]
    private void OpenPublishTxtFile()
    {
        var targetFile = LastGeneratedTxtPath;
        if (string.IsNullOrEmpty(targetFile) || !File.Exists(targetFile))
        {
            if (!string.IsNullOrEmpty(LastGeneratedVideoPath))
            {
                var projectFolder = Path.GetDirectoryName(Path.GetDirectoryName(LastGeneratedVideoPath));
                if (!string.IsNullOrEmpty(projectFolder))
                {
                    var potentialFile = Path.Combine(projectFolder, "DANG_BAI_METADATA.txt");
                    if (File.Exists(potentialFile))
                        targetFile = potentialFile;
                }
            }
        }

        if (string.IsNullOrEmpty(targetFile) || !File.Exists(targetFile))
        {
            EnsureBasicPublishInfo();
            var tempDir = Path.Combine(Path.GetTempPath(), "VideoAutoWpf");
            Directory.CreateDirectory(tempDir);
            var tempTxt = Path.Combine(tempDir, "DANG_BAI_METADATA.txt");
            File.WriteAllText(tempTxt, CurrentPublishInfo.GenerateFormattedTxt(
                videoFileName: OutputFileName,
                aspectRatio: AspectRatio,
                voice: SelectedVoice,
                sceneCount: Scenes.Count,
                bgmName: SelectedBgmTrack?.Title
            ), new UTF8Encoding(true));
            targetFile = tempTxt;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = targetFile,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Không thể mở file:\n{ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    [RelayCommand]
    private async Task RegenerateSocialMetadataAsync()
    {
        if (Scenes.Count == 0)
        {
            MessageBox.Show("Chưa có phân cảnh nào trong dự án để tạo metadata!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        IsGeneratingPublishInfo = true;
        StatusMessage = "⏳ AI đang sáng tạo Tiêu đề, Mô tả và Hashtags bùng nổ...";
        AppendLog("[Gemini AI] Đang tạo bộ Tiêu đề, Mô tả và Hashtags chuẩn SEO cho video...");

        try
        {
            var scenesContent = string.Join("\n", Scenes.Select(s => $"Cảnh {s.Index}: {s.Text}"));
            var summary = $"Video gồm {Scenes.Count} cảnh, định dạng {AspectRatio}";

            var result = await _geminiService.GeneratePublishInfoAsync(summary, scenesContent);
            if (result != null)
            {
                CurrentPublishInfo = result;
                OnPropertyChanged(nameof(CurrentPublishInfo));
                OnPropertyChanged(nameof(HasPublishInfo));

                if (!string.IsNullOrWhiteSpace(result.Title))
                {
                    var safeTitle = result.Title;
                    foreach (var c in Path.GetInvalidFileNameChars())
                        safeTitle = safeTitle.Replace(c, '_');
                    safeTitle = safeTitle.Replace(' ', '_').Trim('_');
                    if (safeTitle.Length > 45) safeTitle = safeTitle.Substring(0, 45);
                    OutputFileName = $"{safeTitle}.mp4";
                }

                StatusMessage = "✅ Đã tạo mới bộ Tiêu đề & Hashtags thành công!";
                AppendLog($"[Gemini AI] Đã tạo tiêu đề mới: \"{result.Title}\" với bộ hashtags chuẩn xu hướng.");
                TriggerCopyFeedback("✅ AI đã tạo mới Tiêu đề & Hashtags!");
            }
        }
        catch (Exception ex)
        {
            StatusMessage = "Có lỗi khi AI tạo metadata.";
            AppendLog($"[Lỗi Gemini] {ex.Message}");
            MessageBox.Show($"Không thể tạo metadata bằng AI:\n{ex.Message}", "Lỗi AI", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsGeneratingPublishInfo = false;
        }
    }

    public void EnsureBasicPublishInfo()
    {
        if (CurrentPublishInfo == null) CurrentPublishInfo = new VideoPublishInfo();

        if (string.IsNullOrWhiteSpace(CurrentPublishInfo.Title))
        {
            if (Scenes.Count > 0 && !string.IsNullOrWhiteSpace(Scenes[0].Text))
            {
                var firstLine = Scenes[0].Text!.Trim();
                CurrentPublishInfo.Title = firstLine.Length > 50 ? firstLine.Substring(0, 50) + "..." : firstLine;
            }
            else
            {
                CurrentPublishInfo.Title = Path.GetFileNameWithoutExtension(OutputFileName).Replace('_', ' ');
            }
        }

        if (string.IsNullOrWhiteSpace(CurrentPublishInfo.Description))
        {
            var texts = Scenes.Where(s => !string.IsNullOrWhiteSpace(s.Text)).Take(3).Select(s => s.Text);
            CurrentPublishInfo.Description = string.Join(" ", texts);
        }

        if (string.IsNullOrWhiteSpace(CurrentPublishInfo.Hashtags))
        {
            CurrentPublishInfo.Hashtags = "#kienthuc #bian #khampha #shorts #xuhuong #viral #fyp #tiktok #reels";
        }

        OnPropertyChanged(nameof(CurrentPublishInfo));
        OnPropertyChanged(nameof(HasPublishInfo));
    }

    private static void SetClipboardText(string text)
    {
        if (string.IsNullOrEmpty(text)) return;
        try
        {
            Clipboard.SetText(text);
        }
        catch
        {
            try
            {
                Clipboard.SetDataObject(text, true);
            }
            catch { }
        }
    }

    private async void TriggerCopyFeedback(string msg)
    {
        CopyFeedbackMessage = msg;
        try
        {
            await Task.Delay(2500);
            if (CopyFeedbackMessage == msg)
                CopyFeedbackMessage = string.Empty;
        }
        catch { }
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
