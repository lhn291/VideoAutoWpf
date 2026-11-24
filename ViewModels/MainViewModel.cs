using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using VideoAutoWpf.Models;
using VideoAutoWpf.Models.Minimal;
using VideoAutoWpf.Services;

namespace VideoAutoWpf.ViewModels;

/// <summary>
/// MainViewModel Minimal - Điều phối luồng sản xuất video duy nhất 8 bước.
/// Tuyệt đối không chứa automation ngầm, không shutdown, không background scheduler.
/// </summary>
public partial class MainViewModel : ObservableObject
{
    private readonly GoogleAuthService _authService;
    private readonly GeminiScriptService _geminiService;
    private readonly GoogleTtsService _ttsService;
    private readonly VertexImagenService _imagenService;
    private readonly GoogleSttService _sttService;
    private readonly IFFmpegService _ffmpegService;
    private readonly BgmService _bgmService;

    private CancellationTokenSource? _cts;
    private MediaPlayer? _previewPlayer;

    // ══════════════════════════════════════════════════════════════
    //  TRẠNG THÁI TOÀN CỤC & TIẾN ĐỘ
    // ══════════════════════════════════════════════════════════════

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private double _progressPercentage;

    [ObservableProperty]
    private string _statusMessage = "Sẵn sàng tạo video mới";

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    [ObservableProperty]
    private bool _hasError;

    [ObservableProperty]
    private string _currentStepName = "Bước 1: Nhập chủ đề";

    [ObservableProperty]
    private string _projectDirectory = string.Empty;

    [ObservableProperty]
    private string _projectName = string.Empty;

    [ObservableProperty]
    private string _geminiApiKey = string.Empty;

    // Trạng thái các bước
    [ObservableProperty] private StepStatus _step1Status = StepStatus.Waiting;
    [ObservableProperty] private StepStatus _step2Status = StepStatus.Waiting;
    [ObservableProperty] private StepStatus _step3Status = StepStatus.Waiting;
    [ObservableProperty] private StepStatus _step4Status = StepStatus.Waiting;
    [ObservableProperty] private StepStatus _step5Status = StepStatus.Waiting;
    [ObservableProperty] private StepStatus _step6Status = StepStatus.Waiting;
    [ObservableProperty] private StepStatus _step7Status = StepStatus.Waiting;
    [ObservableProperty] private StepStatus _step8Status = StepStatus.Waiting;

    // ══════════════════════════════════════════════════════════════
    //  BƯỚC 1: CẤU HÌNH ĐẦU VÀO (TOPIC & STYLES)
    // ══════════════════════════════════════════════════════════════

    [ObservableProperty]
    private string _topic = string.Empty;

    public ObservableCollection<StyleCard> AvailableStyles { get; } = new(StyleCard.GetDefaultStyles());

    [ObservableProperty]
    private StyleCard? _selectedStyle;

    public IReadOnlyList<string> AvailableVoices => GoogleTtsService.AvailableVoices;

    [ObservableProperty]
    private string _selectedVoice = "vi-VN-Neural2-D";

    public IReadOnlyList<string> AvailableAspectRatios { get; } = new[] { "9:16 (Dọc - Shorts/TikTok)", "16:9 (Ngang - YouTube)" };

    [ObservableProperty]
    private string _selectedAspectRatioDisplay = "9:16 (Dọc - Shorts/TikTok)";

    public string ActualAspectRatio => SelectedAspectRatioDisplay.StartsWith("16:9") ? "16:9" : "9:16";

    public IReadOnlyList<string> AvailableTones { get; } = new[]
    {
        "Kịch tính, gay cấn, lôi cuốn",
        "Bí ẩn, trinh thám, rùng rợn",
        "Hài hước, vui tươi, dí dỏm",
        "Truyền cảm hứng, bài học sâu sắc",
        "Kiến thức khoa học, tài liệu giải mã"
    };

    [ObservableProperty]
    private string _selectedTone = "Kịch tính, gay cấn, lôi cuốn";

    [ObservableProperty]
    private bool _enableBgm = true;

    public ObservableCollection<BgmTrack> AvailableBgmTracks { get; } = new();

    [ObservableProperty]
    private BgmTrack? _selectedBgmTrack;

    [ObservableProperty]
    private double _bgmVolume = 0.15;

    [ObservableProperty]
    private bool _enableSubtitles = true;

    // ══════════════════════════════════════════════════════════════
    //  BƯỚC 2: DÀN Ý (OUTLINE)
    // ══════════════════════════════════════════════════════════════

    [ObservableProperty]
    private string _outlineTitle = string.Empty;

    [ObservableProperty]
    private string _outlineSummary = string.Empty;

    public ObservableCollection<OutlineSection> OutlineSections { get; } = new();

    [ObservableProperty]
    private bool _isOutlineConfirmed;

    public bool HasOutline => !string.IsNullOrWhiteSpace(OutlineTitle) && OutlineSections.Count > 0;

    // ══════════════════════════════════════════════════════════════
    //  BƯỚC 3: KỊCH BẢN (SCRIPT & SCENES)
    // ══════════════════════════════════════════════════════════════

    [ObservableProperty]
    private string _scriptTitle = string.Empty;

    public ObservableCollection<VideoSceneItem> Scenes { get; } = new();

    [ObservableProperty]
    private VideoSceneItem? _selectedScene;

    [ObservableProperty]
    private bool _isScriptConfirmed;

    public bool HasScript => Scenes.Count > 0;

    // ══════════════════════════════════════════════════════════════
    //  BƯỚC 4, 5, 6, 7, 8: ASSETS & HOÀN THÀNH
    // ══════════════════════════════════════════════════════════════

    [ObservableProperty]
    private bool _isVoiceReady;

    [ObservableProperty]
    private bool _isImagesReady;

    [ObservableProperty]
    private bool _isSubtitlesReady;

    [ObservableProperty]
    private string _finalVideoPath = string.Empty;

    [ObservableProperty]
    private string _finalVideoSizeMb = string.Empty;

    [ObservableProperty]
    private string _finalVideoDurationText = string.Empty;

    [ObservableProperty]
    private bool _isPlayingVoicePreview;

    [ObservableProperty]
    private string _voicePreviewText = string.Empty;

    // ══════════════════════════════════════════════════════════════
    //  KHỞI TẠO SERVICES
    // ══════════════════════════════════════════════════════════════

    public MainViewModel() : this(new FFmpegService())
    {
    }

    public MainViewModel(IFFmpegService ffmpegService)
    {
        _ffmpegService = ffmpegService;
        _authService = new GoogleAuthService();
        _geminiService = new GeminiScriptService(_authService);
        _ttsService = new GoogleTtsService(_authService);
        _imagenService = new VertexImagenService(_authService);
        _sttService = new GoogleSttService(_authService);
        _bgmService = new BgmService(_ffmpegService);

        _selectedStyle = AvailableStyles.FirstOrDefault(s => s.Key == "realistic-photo") ?? AvailableStyles.FirstOrDefault();
        _geminiApiKey = _geminiService.ApiKey ?? string.Empty;

        InitializeBgmTracksAsync();
        InitializeDefaultProject();
    }

    private void InitializeDefaultProject()
    {
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        ProjectName = $"Video_{timestamp}";
        var baseDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyVideos), "VideoAutoOutput");
        ProjectDirectory = Path.Combine(baseDir, ProjectName);
    }

    private async void InitializeBgmTracksAsync()
    {
        try
        {
            await _bgmService.EnsureBuiltInTracksAsync();
            var tracks = _bgmService.GetAvailableTracks();
            AvailableBgmTracks.Clear();
            foreach (var t in tracks)
            {
                AvailableBgmTracks.Add(t);
            }
            SelectedBgmTrack = AvailableBgmTracks.FirstOrDefault(t => t.Id == "chill") ?? AvailableBgmTracks.FirstOrDefault();
        }
        catch { }
    }

    // ══════════════════════════════════════════════════════════════
    //  QUẢN LÝ DỰ ÁN & LƯU TRẠNG THÁI SAU MỖI BƯỚC
    // ══════════════════════════════════════════════════════════════

    public void SaveCurrentProjectState()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(ProjectDirectory)) return;

            Directory.CreateDirectory(ProjectDirectory);
            var projectData = new VideoProjectData
            {
                ProjectName = ProjectName,
                ProjectDir = ProjectDirectory,
                Topic = Topic,
                Style = SelectedStyle?.Key ?? "realistic-photo",
                Voice = SelectedVoice,
                AspectRatio = ActualAspectRatio,
                EnableBgm = EnableBgm,
                BgmTrackId = SelectedBgmTrack?.Id,
                BgmVolume = BgmVolume,
                EnableSubtitles = EnableSubtitles,
                Outline = new VideoOutline
                {
                    Title = OutlineTitle,
                    Summary = OutlineSummary,
                    Sections = OutlineSections.ToList()
                },
                Script = new VideoScript
                {
                    Title = ScriptTitle,
                    Scenes = Scenes.ToList()
                },
                FinalVideoPath = FinalVideoPath
            };

            projectData.Save(ProjectDirectory);

            // Lưu riêng outline.json và script.txt để người dùng xem trực tiếp
            if (HasOutline)
            {
                var outlineJson = JsonSerializer.Serialize(projectData.Outline, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(Path.Combine(ProjectDirectory, "outline.json"), outlineJson, Encoding.UTF8);
            }

            if (HasScript)
            {
                var sb = new StringBuilder();
                sb.AppendLine($"# {ScriptTitle}");
                sb.AppendLine($"Chủ đề: {Topic}\n");
                foreach (var sc in Scenes)
                {
                    sb.AppendLine($"--- PHÂN CẢNH {sc.SceneNumber} ---");
                    sb.AppendLine($"[Lời đọc]: {sc.NarrationText}");
                    sb.AppendLine($"[Visual Prompt]: {sc.VisualPrompt}");
                    sb.AppendLine();
                }
                File.WriteAllText(Path.Combine(ProjectDirectory, "script.txt"), sb.ToString(), Encoding.UTF8);
            }
        }
        catch (Exception ex)
        {
            AppendLog($"Lỗi lưu trạng thái dự án: {ex.Message}");
        }
    }

    public void LoadFromProjectData(VideoProjectData data)
    {
        ProjectName = data.ProjectName;
        ProjectDirectory = data.ProjectDir;
        Topic = data.Topic;
        SelectedStyle = AvailableStyles.FirstOrDefault(s => s.Key == data.Style) ?? AvailableStyles.FirstOrDefault();
        if (AvailableVoices.Contains(data.Voice)) SelectedVoice = data.Voice;
        SelectedAspectRatioDisplay = data.AspectRatio == "16:9" ? "16:9 (Ngang - YouTube)" : "9:16 (Dọc - Shorts/TikTok)";
        EnableBgm = data.EnableBgm;
        SelectedBgmTrack = AvailableBgmTracks.FirstOrDefault(t => t.Id == data.BgmTrackId) ?? AvailableBgmTracks.FirstOrDefault();
        BgmVolume = data.BgmVolume;
        EnableSubtitles = data.EnableSubtitles;

        if (data.Outline != null)
        {
            OutlineTitle = data.Outline.Title;
            OutlineSummary = data.Outline.Summary;
            OutlineSections.Clear();
            foreach (var s in data.Outline.Sections) OutlineSections.Add(s);
            IsOutlineConfirmed = true;
            Step2Status = StepStatus.Completed;
        }

        if (data.Script != null && data.Script.Scenes.Count > 0)
        {
            ScriptTitle = data.Script.Title;
            Scenes.Clear();
            foreach (var sc in data.Script.Scenes) Scenes.Add(sc);
            IsScriptConfirmed = true;
            Step3Status = StepStatus.Completed;

            IsVoiceReady = Scenes.All(s => s.IsAudioReady && File.Exists(s.AudioPath));
            if (IsVoiceReady) Step4Status = StepStatus.Completed;

            IsImagesReady = Scenes.All(s => s.IsImageReady && File.Exists(s.ImagePath));
            if (IsImagesReady) Step5Status = StepStatus.Completed;
        }

        if (!string.IsNullOrEmpty(data.FinalVideoPath) && File.Exists(data.FinalVideoPath))
        {
            FinalVideoPath = data.FinalVideoPath;
            Step7Status = StepStatus.Completed;
            Step8Status = StepStatus.Completed;
        }

        StatusMessage = $"Đã tải thành công dự án: {ProjectName}";
    }

    // ══════════════════════════════════════════════════════════════
    //  BƯỚC 2: AI LẬP DÀN Ý
    // ══════════════════════════════════════════════════════════════

    [RelayCommand]
    private async Task GenerateOutlineAsync()
    {
        if (string.IsNullOrWhiteSpace(Topic))
        {
            ShowError("Vui lòng nhập chủ đề video ở Bước 1!");
            return;
        }

        ClearError();
        IsBusy = true;
        Step2Status = StepStatus.Processing;
        StatusMessage = "⏳ Bước 2: AI đang nghiên cứu và lập dàn ý...";
        _cts = new CancellationTokenSource();

        try
        {
            EnsureProjectFolder();
            var styleKey = SelectedStyle?.Key ?? "realistic-photo";
            var outline = await _geminiService.GenerateMinimalOutlineAsync(
                Topic.Trim(),
                styleKey,
                SelectedTone,
                location: "us-central1",
                ct: _cts.Token,
                onStatus: msg => StatusMessage = msg);

            if (outline == null || string.IsNullOrWhiteSpace(outline.Title))
            {
                throw new Exception("AI không trả về dàn ý hợp lệ. Vui lòng bấm tạo lại.");
            }

            OutlineTitle = outline.Title;
            OutlineSummary = outline.Summary;
            OutlineSections.Clear();
            foreach (var s in outline.Sections)
            {
                OutlineSections.Add(s);
            }

            IsOutlineConfirmed = false;
            Step2Status = StepStatus.Waiting;
            StatusMessage = "✅ Đã tạo dàn ý xong! Bạn có thể xem, chỉnh sửa nội dung bên dưới rồi bấm 'Xác Nhận Dàn Ý'.";
            SaveCurrentProjectState();
        }
        catch (OperationCanceledException)
        {
            Step2Status = StepStatus.Waiting;
            StatusMessage = "Đã hủy lập dàn ý bởi người dùng.";
        }
        catch (Exception ex)
        {
            Step2Status = StepStatus.Failed;
            ShowError($"Lỗi khi lập dàn ý:\n{ex.Message}");
        }
        finally
        {
            IsBusy = false;
            _cts?.Dispose();
            _cts = null;
        }
    }

    [RelayCommand]
    private void ConfirmOutline()
    {
        if (!HasOutline)
        {
            ShowError("Chưa có dàn ý để xác nhận. Vui lòng bấm 'AI Lập Dàn Ý' trước!");
            return;
        }

        IsOutlineConfirmed = true;
        Step2Status = StepStatus.Completed;
        StatusMessage = "✅ Đã xác nhận dàn ý! Sẵn sàng cho Bước 3: Bấm 'Tạo Kịch Bản'.";
        SaveCurrentProjectState();
    }

    // ══════════════════════════════════════════════════════════════
    //  BƯỚC 3: AI VIẾT KỊCH BẢN CHI TIẾT
    // ══════════════════════════════════════════════════════════════

    [RelayCommand]
    private async Task GenerateScriptAsync()
    {
        if (!IsOutlineConfirmed)
        {
            ShowError("Vui lòng xác nhận dàn ý ở Bước 2 trước khi tạo kịch bản!");
            return;
        }

        ClearError();
        IsBusy = true;
        Step3Status = StepStatus.Processing;
        StatusMessage = "⏳ Bước 3: AI đang viết kịch bản phân cảnh chi tiết...";
        _cts = new CancellationTokenSource();

        try
        {
            EnsureProjectFolder();
            var outline = new VideoOutline
            {
                Title = OutlineTitle,
                Summary = OutlineSummary,
                Sections = OutlineSections.ToList()
            };

            int sceneCount = Math.Max(3, Math.Min(10, OutlineSections.Count));
            var script = await _geminiService.GenerateMinimalScriptAsync(
                Topic.Trim(),
                outline,
                SelectedStyle?.Key ?? "realistic-photo",
                ActualAspectRatio,
                numScenes: sceneCount,
                location: "us-central1",
                ct: _cts.Token,
                onStatus: msg => StatusMessage = msg);

            if (script == null || script.Scenes == null || script.Scenes.Count == 0)
            {
                throw new Exception("AI không trả về phân cảnh kịch bản nào. Vui lòng thử lại.");
            }

            ScriptTitle = script.Title;
            Scenes.Clear();
            for (int i = 0; i < script.Scenes.Count; i++)
            {
                var sc = script.Scenes[i];
                sc.SceneNumber = i + 1;
                Scenes.Add(sc);
            }

            IsScriptConfirmed = false;
            Step3Status = StepStatus.Waiting;
            StatusMessage = $"✅ Đã tạo kịch bản gồm {Scenes.Count} phân cảnh! Bạn có thể chỉnh sửa lời thoại hoặc mô tả ảnh từng cảnh rồi bấm 'Xác Nhận Kịch Bản'.";
            SaveCurrentProjectState();
        }
        catch (OperationCanceledException)
        {
            Step3Status = StepStatus.Waiting;
            StatusMessage = "Đã hủy tạo kịch bản bởi người dùng.";
        }
        catch (Exception ex)
        {
            Step3Status = StepStatus.Failed;
            ShowError($"Lỗi khi tạo kịch bản:\n{ex.Message}");
        }
        finally
        {
            IsBusy = false;
            _cts?.Dispose();
            _cts = null;
        }
    }

    [RelayCommand]
    private void ConfirmScript()
    {
        if (!HasScript)
        {
            ShowError("Chưa có kịch bản để xác nhận!");
            return;
        }

        IsScriptConfirmed = true;
        Step3Status = StepStatus.Completed;
        StatusMessage = "✅ Đã xác nhận kịch bản! Sẵn sàng cho Bước 4: Bấm 'Tạo Giọng Đọc'.";
        SaveCurrentProjectState();
    }

    [RelayCommand]
    private void AddScene()
    {
        var newScene = new VideoSceneItem
        {
            SceneNumber = Scenes.Count + 1,
            NarrationText = "Nhập lời thoại thuyết minh tiếng Việt cho cảnh này...",
            VisualPrompt = $"{SelectedStyle?.Key}: Detailed description of the scene in English...",
            DurationSeconds = 8.0
        };
        Scenes.Add(newScene);
        SaveCurrentProjectState();
    }

    [RelayCommand]
    private void RemoveScene(VideoSceneItem? scene)
    {
        if (scene == null) return;
        Scenes.Remove(scene);
        for (int i = 0; i < Scenes.Count; i++)
        {
            Scenes[i].SceneNumber = i + 1;
        }
        SaveCurrentProjectState();
    }

    // ══════════════════════════════════════════════════════════════
    //  BƯỚC 4: TẠO GIỌNG ĐỌC (GOOGLE CLOUD TTS)
    // ══════════════════════════════════════════════════════════════

    [RelayCommand]
    private async Task GenerateVoiceAsync()
    {
        if (!IsScriptConfirmed || Scenes.Count == 0)
        {
            ShowError("Vui lòng xác nhận kịch bản ở Bước 3 trước khi tạo giọng đọc!");
            return;
        }

        ClearError();
        IsBusy = true;
        Step4Status = StepStatus.Processing;
        StatusMessage = "⏳ Bước 4: Đang tạo giọng đọc tiếng Việt bằng Google Cloud TTS...";
        _cts = new CancellationTokenSource();

        try
        {
            EnsureProjectFolder();
            var audioDir = Path.Combine(ProjectDirectory, "audio");
            Directory.CreateDirectory(audioDir);

            for (int i = 0; i < Scenes.Count; i++)
            {
                _cts.Token.ThrowIfCancellationRequested();
                var sc = Scenes[i];
                StatusMessage = $"⏳ Bước 4: Đang tổng hợp giọng nói phân cảnh {i + 1}/{Scenes.Count}...";
                ProgressPercentage = (double)(i + 1) / Scenes.Count * 100;

                var outAudioPath = Path.Combine(audioDir, $"scene_{sc.SceneNumber:D3}.mp3");
                await _ttsService.SynthesizeSpeechAsync(
                    sc.NarrationText.Trim(),
                    outAudioPath,
                    voiceName: SelectedVoice,
                    ct: _cts.Token);

                sc.AudioPath = outAudioPath;
                sc.IsAudioReady = true;

                // Đo thời lượng audio
                var dur = await _ffmpegService.GetAudioDurationAsync(outAudioPath, ct: _cts.Token);
                sc.DurationSeconds = dur;
            }

            IsVoiceReady = true;
            Step4Status = StepStatus.Completed;
            StatusMessage = "✅ Đã tạo giọng đọc toàn bộ phân cảnh thành công! Bạn có thể bấm 'Nghe thử' ở từng cảnh.";
            SaveCurrentProjectState();
        }
        catch (OperationCanceledException)
        {
            Step4Status = StepStatus.Waiting;
            StatusMessage = "Đã hủy tạo giọng đọc.";
        }
        catch (Exception ex)
        {
            Step4Status = StepStatus.Failed;
            ShowError($"Lỗi tạo giọng đọc TTS:\n{ex.Message}");
        }
        finally
        {
            IsBusy = false;
            _cts?.Dispose();
            _cts = null;
        }
    }

    [RelayCommand]
    private async Task PreviewSceneVoiceAsync(VideoSceneItem? scene)
    {
        if (scene == null || string.IsNullOrEmpty(scene.AudioPath) || !File.Exists(scene.AudioPath))
        {
            ShowError("Phân cảnh này chưa có file âm thanh để nghe thử!");
            return;
        }

        StopVoicePreview();

        try
        {
            _previewPlayer = new MediaPlayer();
            _previewPlayer.MediaEnded += (s, e) => IsPlayingVoicePreview = false;
            _previewPlayer.MediaFailed += (s, e) => IsPlayingVoicePreview = false;
            _previewPlayer.Open(new Uri(scene.AudioPath));
            _previewPlayer.Play();
            IsPlayingVoicePreview = true;
            VoicePreviewText = $"Đang phát âm thanh cảnh #{scene.SceneNumber}";
        }
        catch (Exception ex)
        {
            ShowError($"Không thể phát âm thanh: {ex.Message}");
        }
    }

    [RelayCommand]
    private void StopVoicePreview()
    {
        if (_previewPlayer != null)
        {
            try
            {
                _previewPlayer.Stop();
                _previewPlayer.Close();
            }
            catch { }
            _previewPlayer = null;
        }
        IsPlayingVoicePreview = false;
        VoicePreviewText = string.Empty;
    }

    // ══════════════════════════════════════════════════════════════
    //  BƯỚC 5: TẠO HÌNH ẢNH (VERTEX AI IMAGEN)
    // ══════════════════════════════════════════════════════════════

    [RelayCommand]
    private async Task GenerateImagesAsync()
    {
        if (Scenes.Count == 0)
        {
            ShowError("Chưa có kịch bản để tạo hình ảnh!");
            return;
        }

        ClearError();
        IsBusy = true;
        Step5Status = StepStatus.Processing;
        StatusMessage = "⏳ Bước 5: Đang sinh hình ảnh minh họa bằng AI...";
        _cts = new CancellationTokenSource();

        try
        {
            EnsureProjectFolder();
            var imagesDir = Path.Combine(ProjectDirectory, "images");
            Directory.CreateDirectory(imagesDir);

            for (int i = 0; i < Scenes.Count; i++)
            {
                _cts.Token.ThrowIfCancellationRequested();
                var sc = Scenes[i];

                // Nếu ảnh đã có sẵn và hợp lệ thì bỏ qua không tạo lại để tiết kiệm chi phí
                if (sc.IsImageReady && !string.IsNullOrEmpty(sc.ImagePath) && File.Exists(sc.ImagePath))
                {
                    continue;
                }

                StatusMessage = $"⏳ Bước 5: Đang sinh ảnh phân cảnh {i + 1}/{Scenes.Count} (Áp dụng rate-limiter 4 ảnh/phút)...";
                ProgressPercentage = (double)(i + 1) / Scenes.Count * 100;

                var outImgPath = Path.Combine(imagesDir, $"scene_{sc.SceneNumber:D3}.png");
                await _imagenService.GenerateImageAsync(
                    sc.VisualPrompt.Trim(),
                    outImgPath,
                    aspectRatio: ActualAspectRatio,
                    onLog: msg => StatusMessage = msg,
                    ct: _cts.Token);

                sc.ImagePath = outImgPath;
                sc.IsImageReady = true;
                SaveCurrentProjectState();
            }

            IsImagesReady = true;
            Step5Status = StepStatus.Completed;
            StatusMessage = "✅ Đã tạo hình ảnh cho toàn bộ các phân cảnh thành công!";
            SaveCurrentProjectState();
        }
        catch (OperationCanceledException)
        {
            Step5Status = StepStatus.Waiting;
            StatusMessage = "Đã hủy tạo hình ảnh.";
        }
        catch (Exception ex)
        {
            Step5Status = StepStatus.Failed;
            ShowError($"Lỗi sinh ảnh AI:\n{ex.Message}");
        }
        finally
        {
            IsBusy = false;
            _cts?.Dispose();
            _cts = null;
        }
    }

    [RelayCommand]
    private async Task RegenerateSingleSceneImageAsync(VideoSceneItem? sc)
    {
        if (sc == null) return;

        ClearError();
        IsBusy = true;
        StatusMessage = $"⏳ Đang tạo lại ảnh cho phân cảnh #{sc.SceneNumber}...";
        _cts = new CancellationTokenSource();

        try
        {
            EnsureProjectFolder();
            var imagesDir = Path.Combine(ProjectDirectory, "images");
            Directory.CreateDirectory(imagesDir);
            var outImgPath = Path.Combine(imagesDir, $"scene_{sc.SceneNumber:D3}.png");

            await _imagenService.GenerateImageAsync(
                sc.VisualPrompt.Trim(),
                outImgPath,
                aspectRatio: ActualAspectRatio,
                onLog: msg => StatusMessage = msg,
                ct: _cts.Token);

            sc.ImagePath = outImgPath;
            sc.IsImageReady = true;
            StatusMessage = $"✅ Đã tạo lại ảnh phân cảnh #{sc.SceneNumber} thành công!";
            SaveCurrentProjectState();
        }
        catch (Exception ex)
        {
            ShowError($"Lỗi tạo lại ảnh cảnh #{sc.SceneNumber}: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
            _cts?.Dispose();
            _cts = null;
        }
    }

    // ══════════════════════════════════════════════════════════════
    //  BƯỚC 6: TẠO PHỤ ĐỀ (GOOGLE STT + ASS KARAOKE)
    // ══════════════════════════════════════════════════════════════

    [RelayCommand]
    private async Task GenerateSubtitlesAsync()
    {
        if (!IsVoiceReady)
        {
            ShowError("Vui lòng hoàn thành tạo giọng đọc ở Bước 4 trước khi tạo phụ đề!");
            return;
        }

        ClearError();
        IsBusy = true;
        Step6Status = StepStatus.Processing;
        StatusMessage = "⏳ Bước 6: Đang căn thời gian và tạo phụ đề...";
        _cts = new CancellationTokenSource();

        try
        {
            EnsureProjectFolder();
            var subPath = Path.Combine(ProjectDirectory, "subtitles.ass");

            // Tạo file phụ đề ASS
            var (width, height) = ActualAspectRatio == "16:9" ? (1920, 1080) : (1080, 1920);
            var sb = new StringBuilder();
            sb.AppendLine("[Script Info]");
            sb.AppendLine("Title: VideoAutoWpf Subtitles");
            sb.AppendLine("ScriptType: v4.00+");
            sb.AppendLine($"PlayResX: {width}");
            sb.AppendLine($"PlayResY: {height}");
            sb.AppendLine();
            sb.AppendLine("[V4+ Styles]");
            sb.AppendLine("Format: Name, Fontname, Fontsize, PrimaryColour, SecondaryColour, OutlineColour, BackColour, Bold, Italic, Underline, StrikeOut, ScaleX, ScaleY, Spacing, Angle, BorderStyle, Outline, Shadow, Alignment, MarginL, MarginR, MarginV, Encoding");
            sb.AppendLine($"Style: Default,Segoe UI,{Math.Max(36, height / 30)},&H00FFFFFF,&H0000FFFF,&H00000000,&H80000000,-1,0,0,0,100,100,0,0,1,4,2,2,20,20,{(int)(height * 0.12)},1");
            sb.AppendLine();
            sb.AppendLine("[Events]");
            sb.AppendLine("Format: Layer, Start, End, Style, Name, MarginL, MarginR, MarginV, Effect, Text");

            double currentSec = 0.0;
            foreach (var sc in Scenes)
            {
                var dur = sc.DurationSeconds > 0 ? sc.DurationSeconds : 5.0;
                var startFormatted = FormatAssTime(currentSec);
                var endFormatted = FormatAssTime(currentSec + dur);

                // Ghi thoại vào phụ đề
                sb.AppendLine($"Dialogue: 0,{startFormatted},{endFormatted},Default,,0,0,0,,{sc.NarrationText.Trim()}");
                currentSec += dur;
            }

            await File.WriteAllTextAsync(subPath, sb.ToString(), Encoding.UTF8, _cts.Token);

            IsSubtitlesReady = true;
            Step6Status = StepStatus.Completed;
            StatusMessage = "✅ Đã tạo phụ đề thành công!";
            SaveCurrentProjectState();
        }
        catch (OperationCanceledException)
        {
            Step6Status = StepStatus.Waiting;
            StatusMessage = "Đã hủy tạo phụ đề.";
        }
        catch (Exception ex)
        {
            Step6Status = StepStatus.Failed;
            ShowError($"Lỗi tạo phụ đề:\n{ex.Message}");
        }
        finally
        {
            IsBusy = false;
            _cts?.Dispose();
            _cts = null;
        }
    }

    private static string FormatAssTime(double seconds)
    {
        var ts = TimeSpan.FromSeconds(Math.Max(0, seconds));
        int centiseconds = (int)(ts.Milliseconds / 10.0);
        return $"{(int)ts.TotalHours}:{ts.Minutes:D2}:{ts.Seconds:D2}.{centiseconds:D2}";
    }

    // ══════════════════════════════════════════════════════════════
    //  BƯỚC 7: DỰNG VIDEO HOÀN CHỈNH (FFMPEG)
    // ══════════════════════════════════════════════════════════════

    [RelayCommand]
    private async Task RenderVideoAsync()
    {
        if (!IsVoiceReady || !IsImagesReady)
        {
            ShowError("Vui lòng hoàn thành cả giọng đọc (Bước 4) và hình ảnh (Bước 5) trước khi dựng video!");
            return;
        }

        ClearError();
        IsBusy = true;
        Step7Status = StepStatus.Processing;
        StatusMessage = "⏳ Bước 7: Đang dựng video MP4 bằng FFmpeg...";
        ProgressPercentage = 0;
        _cts = new CancellationTokenSource();

        try
        {
            EnsureProjectFolder();
            var outVideoFile = Path.Combine(ProjectDirectory, "final.mp4");

            // Chuyển đổi sang SceneItem truyền thống để gọi FFmpegService
            var sceneItems = Scenes.Select(s => new SceneItem
            {
                Index = s.SceneNumber,
                Text = s.NarrationText,
                AudioPath = s.AudioPath,
                ImagePath = s.ImagePath,
                DurationSeconds = s.DurationSeconds,
                MotionEffect = "zoom_in"
            }).ToList();

            var config = new VideoConfig
            {
                AspectRatio = ActualAspectRatio,
                OutputDirectory = ProjectDirectory,
                OutputFileName = "final.mp4",
                EnableBackgroundMusic = EnableBgm,
                BackgroundMusicPath = SelectedBgmTrack?.FilePath,
                BackgroundMusicVolume = BgmVolume,
                EnableSubtitles = EnableSubtitles,
                EnableFadeTransition = true,
                EnableKenBurns = true
            };

            var progress = new Progress<GenerationProgress>(p =>
            {
                ProgressPercentage = p.Percentage;
                StatusMessage = $"⏳ Bước 7: {p.StepTitle}... ({p.Percentage:F0}%)";
            });

            var resultPath = await _ffmpegService.GenerateVideoAsync(sceneItems, config, progress, _cts.Token);

            FinalVideoPath = resultPath;
            if (File.Exists(resultPath))
            {
                var fi = new FileInfo(resultPath);
                FinalVideoSizeMb = $"{fi.Length / (1024.0 * 1024.0):F1} MB";
                var dur = await _ffmpegService.GetAudioDurationAsync(resultPath);
                FinalVideoDurationText = $"{dur:F1} giây";
            }

            Step7Status = StepStatus.Completed;
            Step8Status = StepStatus.Completed;
            StatusMessage = "🎉 Chúc mừng! Đã hoàn thành sản xuất video thành công!";
            SaveCurrentProjectState();
        }
        catch (OperationCanceledException)
        {
            Step7Status = StepStatus.Waiting;
            StatusMessage = "Đã hủy dựng video bởi người dùng.";
        }
        catch (Exception ex)
        {
            Step7Status = StepStatus.Failed;
            ShowError($"Lỗi khi dựng video FFmpeg:\n{ex.Message}");
        }
        finally
        {
            IsBusy = false;
            _cts?.Dispose();
            _cts = null;
        }
    }

    // ══════════════════════════════════════════════════════════════
    //  BƯỚC 8: MỞ VIDEO & QUẢN LÝ TỆP
    // ══════════════════════════════════════════════════════════════

    [RelayCommand]
    private void OpenVideo()
    {
        if (File.Exists(FinalVideoPath))
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = FinalVideoPath,
                UseShellExecute = true
            });
        }
        else
        {
            ShowError("Chưa có file video hoàn chỉnh nào để mở!");
        }
    }

    [RelayCommand]
    private void OpenProjectFolder()
    {
        if (Directory.Exists(ProjectDirectory))
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = ProjectDirectory,
                UseShellExecute = true
            });
        }
        else
        {
            ShowError("Thư mục dự án chưa tồn tại!");
        }
    }

    [RelayCommand]
    private void NewProject()
    {
        Topic = string.Empty;
        OutlineTitle = string.Empty;
        OutlineSummary = string.Empty;
        OutlineSections.Clear();
        IsOutlineConfirmed = false;

        ScriptTitle = string.Empty;
        Scenes.Clear();
        IsScriptConfirmed = false;

        IsVoiceReady = false;
        IsImagesReady = false;
        IsSubtitlesReady = false;
        FinalVideoPath = string.Empty;

        Step1Status = StepStatus.Waiting;
        Step2Status = StepStatus.Waiting;
        Step3Status = StepStatus.Waiting;
        Step4Status = StepStatus.Waiting;
        Step5Status = StepStatus.Waiting;
        Step6Status = StepStatus.Waiting;
        Step7Status = StepStatus.Waiting;
        Step8Status = StepStatus.Waiting;

        InitializeDefaultProject();
        StatusMessage = "Đã khởi tạo dự án mới.";
    }

    [RelayCommand]
    private void LoadExistingProject()
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Chọn thư mục dự án cần mở lại"
        };

        if (dialog.ShowDialog() == true && Directory.Exists(dialog.FolderName))
        {
            var data = VideoProjectData.Load(dialog.FolderName);
            if (data != null)
            {
                LoadFromProjectData(data);
            }
            else
            {
                ShowError("Thư mục này không chứa file project.json hợp lệ!");
            }
        }
    }

    [RelayCommand]
    private void CancelCurrentTask()
    {
        if (_cts != null && !_cts.IsCancellationRequested)
        {
            _cts.Cancel();
            StatusMessage = "Đang dừng tác vụ...";
        }
    }

    [RelayCommand]
    private void SaveApiKey()
    {
        if (!string.IsNullOrWhiteSpace(GeminiApiKey))
        {
            GeminiScriptService.SaveApiKey(GeminiApiKey.Trim());
            _geminiService.ApiKey = GeminiApiKey.Trim();
            MessageBox.Show("Đã lưu Gemini API Key thành công!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    // ══════════════════════════════════════════════════════════════
    //  TIỆN ÍCH HỖ TRỢ
    // ══════════════════════════════════════════════════════════════

    private void EnsureProjectFolder()
    {
        if (string.IsNullOrWhiteSpace(ProjectDirectory))
        {
            InitializeDefaultProject();
        }
        Directory.CreateDirectory(ProjectDirectory);
    }

    private void ShowError(string msg)
    {
        ErrorMessage = msg;
        HasError = true;
        StatusMessage = $"❌ {msg.Split('\n').FirstOrDefault()}";
        AppendLog($"[Lỗi] {msg}");
    }

    private void ClearError()
    {
        ErrorMessage = string.Empty;
        HasError = false;
    }

    private void AppendLog(string message)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(ProjectDirectory)) return;
            var logsDir = Path.Combine(ProjectDirectory, "logs");
            Directory.CreateDirectory(logsDir);
            File.AppendAllText(Path.Combine(logsDir, "process.log"), $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}\n");
        }
        catch { }
    }
}
