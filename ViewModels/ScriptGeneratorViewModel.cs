using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using VideoAutoWpf.Models;
using VideoAutoWpf.Services;

namespace VideoAutoWpf.ViewModels;

public partial class ScriptGeneratorViewModel : ObservableObject
{
    private readonly GeminiScriptService _geminiService;
    private readonly GoogleTtsService? _ttsService;
    private readonly BgmService _bgmService;
    private MediaPlayer? _previewPlayer;
    private MediaPlayer? _bgmPlayer;

    public Action<bool>? RequestClose { get; set; }

    // ── Topic/Genre Cards ──
    public ObservableCollection<TopicCard> TopicCards { get; } = new(TopicCard.GetDefaultTopics());

    [ObservableProperty]
    private TopicCard? _selectedTopicCard;

    [ObservableProperty]
    private bool _isTopicPopupOpen;

    [ObservableProperty]
    private string _selectedGenreDisplayText = "🎲 Chưa chọn thể loại";

    // ── Style Cards ──
    public ObservableCollection<StyleCard> StyleCards { get; } = new(StyleCard.GetDefaultStyles());

    [ObservableProperty]
    private StyleCard? _selectedStyleCard;

    [ObservableProperty]
    private bool _isStylePopupOpen;

    [ObservableProperty]
    private string _selectedStyleDisplayText = "🎲 AI tự chọn";

    // ── Tone Cards ──
    public ObservableCollection<ToneCard> ToneCards { get; } = new(ToneCard.GetDefaultTones());

    // ── Voice Cards ──
    public ObservableCollection<VoiceCard> VoiceCards { get; } = new(VoiceCard.GetDefaultVoices());

    [ObservableProperty]
    private bool _isPreviewingVoice;

    // ── Scene Count Options ──
    public List<int> SceneCountOptions { get; } = new() { 3, 4, 5, 6, 7, 8, 10, 12, 15 };

    // ── Metadata Đăng Bài (Tiêu đề, Hashtags) do AI sinh ──
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasGeneratedPublishInfo))]
    private VideoPublishInfo? _generatedPublishInfo;

    public bool HasGeneratedPublishInfo => GeneratedPublishInfo != null && !string.IsNullOrWhiteSpace(GeneratedPublishInfo.Title);

    // ── Plan Popup States ──
    [ObservableProperty]
    private bool _isPlanStylePopupOpen;

    [ObservableProperty]
    private bool _isPlanTonePopupOpen;

    [ObservableProperty]
    private bool _isPlanVoicePopupOpen;

    [ObservableProperty]
    private bool _isPlanScenesPopupOpen;

    [ObservableProperty]
    private bool _isPlanMotionPopupOpen;

    public ObservableCollection<MotionEffectOption> PlanMotionOptions { get; } = new(MotionEffectOption.GetAllOptions());

    public string CurrentPlanMotionDisplayText
    {
        get
        {
            if (CurrentPlan == null) return "🤖 AI Tự động phân tích từng cảnh";
            var opt = MotionEffectOption.GetAllOptions().FirstOrDefault(o => o.Id == CurrentPlan.MotionEffect);
            return opt != null ? $"{opt.Icon} {opt.Name}" : "🤖 AI Tự động phân tích từng cảnh";
        }
    }

    public string CurrentPlanBgmMoodDisplayText
    {
        get
        {
            if (CurrentPlan == null) return "🎵 Tự động";
            return CurrentPlan.SuggestedBgm?.ToLowerInvariant() switch
            {
                "chill" => "🧘 Thư Giãn & Lofi",
                "dramatic" => "🕵️ Kịch Tính & Bí Ẩn",
                "epic" => "🏰 Hùng Tráng & Sử Thi",
                "horror" => "👻 U Ám & Rùng Rợn",
                "upbeat" => "⚡ Năng Động & Vui Tươi",
                "none" => "🚫 Không dùng nhạc",
                _ => $"🎵 {CurrentPlan.SuggestedBgm}"
            };
        }
    }

    public string EstimatedPlanCostDisplayText
    {
        get
        {
            if (CurrentPlan == null) return string.Empty;
            int scenes = CurrentPlan.SuggestedScenes > 0 ? CurrentPlan.SuggestedScenes : 5;
            double usd = (scenes * 0.030) + (scenes * 120 * 0.000016) + 0.0005;
            double vnd = usd * 25400.0;
            return $"~${usd:F2} USD (~{vnd:N0} đ)";
        }
    }

    [ObservableProperty]
    private bool _isPlanBgmPopupOpen;

    public List<KeyValuePair<string, string>> PlanBgmOptions { get; } = new()
    {
        new("dramatic", "🕵️ Kịch Tính & Bí Ẩn (Dramatic Mystery)"),
        new("chill", "🧘 Thư Giãn & Lofi (Chill Ambient)"),
        new("epic", "🏰 Hùng Tráng & Sử Thi (Epic Cinematic)"),
        new("horror", "👻 U Ám & Rùng Rợn (Dark Horror)"),
        new("upbeat", "⚡ Năng Động & Vui Tươi (Upbeat Pulse)"),
        new("none", "🚫 Không sử dụng nhạc nền")
    };

    [RelayCommand]
    private void SelectPlanBgm(string? bgmId)
    {
        if (string.IsNullOrEmpty(bgmId) || CurrentPlan == null) return;
        CurrentPlan.SuggestedBgm = bgmId;
        IsPlanBgmPopupOpen = false;
        OnPropertyChanged(nameof(CurrentPlanBgmMoodDisplayText));
    }

    // ── Subtitle Plan Options ──
    [ObservableProperty]
    private bool _enableSubtitles = true;

    [ObservableProperty]
    private bool _isPlanSubtitleStylePopupOpen;

    [ObservableProperty]
    private bool _isPlanSubtitleFontPopupOpen;

    public ObservableCollection<SubtitleStyleOption> PlanSubtitleStyleOptions { get; } = new(SubtitleStyleOption.GetAllOptions());

    public ObservableCollection<SubtitleFontOption> PlanSubtitleFontOptions { get; } = new(SubtitleFontOption.GetAllFonts());

    public string CurrentPlanSubtitleStyleDisplayText
    {
        get
        {
            if (CurrentPlan == null) return "🎬 Điện Ảnh Cổ Điển (Cinematic)";
            var opt = PlanSubtitleStyleOptions.FirstOrDefault(o => o.Id.Equals(CurrentPlan.SubtitleStyle, StringComparison.OrdinalIgnoreCase));
            return opt != null ? $"{opt.Icon} {opt.Name}" : "🎬 Điện Ảnh Cổ Điển (Cinematic)";
        }
    }

    public string CurrentPlanSubtitleFontDisplayText
    {
        get
        {
            if (CurrentPlan == null) return "Segoe UI Bold";
            var opt = PlanSubtitleFontOptions.FirstOrDefault(f => f.Id.Equals(CurrentPlan.SubtitleFont, StringComparison.OrdinalIgnoreCase) || f.Name.Equals(CurrentPlan.SubtitleFont, StringComparison.OrdinalIgnoreCase));
            return opt != null ? opt.Name : CurrentPlan.SubtitleFont;
        }
    }

    public string CurrentPlanSubtitleReasonText => CurrentPlan?.SubtitleReason ?? string.Empty;

    [RelayCommand]
    private void SelectPlanSubtitleStyle(SubtitleStyleOption? option)
    {
        if (option == null || CurrentPlan == null) return;
        CurrentPlan.SubtitleStyle = option.Id;
        if (!string.IsNullOrEmpty(option.DefaultFont))
        {
            CurrentPlan.SubtitleFont = option.DefaultFont;
        }
        IsPlanSubtitleStylePopupOpen = false;
        OnPropertyChanged(nameof(CurrentPlanSubtitleStyleDisplayText));
        OnPropertyChanged(nameof(CurrentPlanSubtitleFontDisplayText));
    }

    [RelayCommand]
    private void SelectPlanSubtitleFont(SubtitleFontOption? option)
    {
        if (option == null || CurrentPlan == null) return;
        CurrentPlan.SubtitleFont = option.Name;
        IsPlanSubtitleFontPopupOpen = false;
        OnPropertyChanged(nameof(CurrentPlanSubtitleFontDisplayText));
    }

    // ── Background Music (BGM) ──
    public ObservableCollection<BgmTrack> AvailableBgmTracks { get; } = new();

    [ObservableProperty]
    private BgmTrack? _selectedBgmTrack;

    [ObservableProperty]
    private bool _enableBgm = true;

    [ObservableProperty]
    private double _bgmVolume = 0.15; // 15%

    public string BgmVolumePercentageText => $"{(int)(BgmVolume * 100)}%";

    [ObservableProperty]
    private bool _isPlayingBgm;

    [ObservableProperty]
    private string _playingBgmTitle = string.Empty;

    public string BgmPlayButtonIcon => IsPlayingBgm ? "⏹" : "🎧";
    public string BgmPlayButtonText => IsPlayingBgm ? "Dừng" : "Nghe thử";

    partial void OnIsPlayingBgmChanged(bool value)
    {
        OnPropertyChanged(nameof(BgmPlayButtonIcon));
        OnPropertyChanged(nameof(BgmPlayButtonText));
    }

    partial void OnBgmVolumeChanged(double value)
    {
        OnPropertyChanged(nameof(BgmVolumePercentageText));
        if (_bgmPlayer != null)
        {
            _bgmPlayer.Volume = value;
        }
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
            if (CurrentPlan != null) CurrentPlan.SuggestedBgm = "none";
        }
        else
        {
            EnableBgm = true;
            if (CurrentPlan != null) CurrentPlan.SuggestedBgm = value.Id;
        }
        OnPropertyChanged(nameof(CurrentPlanBgmMoodDisplayText));
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

    [RelayCommand]
    private void SelectPlanMotion(MotionEffectOption? opt)
    {
        if (opt == null || CurrentPlan == null) return;
        CurrentPlan.MotionEffect = opt.Id;
        IsPlanMotionPopupOpen = false;
        OnPropertyChanged(nameof(CurrentPlanMotionDisplayText));
    }

    // ── Input ──
    [ObservableProperty]
    private string _topic = string.Empty;

    [ObservableProperty]
    private string _characterRules = string.Empty;

    // ── Plan Phase ──
    [ObservableProperty]
    private ScriptPlan? _currentPlan;

    partial void OnCurrentPlanChanged(ScriptPlan? value)
    {
        OnPropertyChanged(nameof(CurrentPlanMotionDisplayText));
        OnPropertyChanged(nameof(CurrentPlanBgmMoodDisplayText));
        OnPropertyChanged(nameof(EstimatedPlanCostDisplayText));
        OnPropertyChanged(nameof(CurrentPlanSubtitleStyleDisplayText));
        OnPropertyChanged(nameof(CurrentPlanSubtitleFontDisplayText));
        OnPropertyChanged(nameof(CurrentPlanSubtitleReasonText));

        if (value != null)
        {
            EnableSubtitles = value.EnableSubtitles;

            if (!string.IsNullOrEmpty(value.SuggestedBgm))
            {
                var match = AvailableBgmTracks.FirstOrDefault(t => 
                    t.Id.Equals(value.SuggestedBgm, StringComparison.OrdinalIgnoreCase) ||
                    t.Mood.Equals(value.SuggestedBgm, StringComparison.OrdinalIgnoreCase));

                if (match != null)
                {
                    SelectedBgmTrack = match;
                    EnableBgm = match.Id != "none";
                }
            }
        }
    }

    [ObservableProperty]
    private bool _hasPlan;

    [ObservableProperty]
    private bool _isGeneratingPlan;

    // ── Generate Phase ──
    [ObservableProperty]
    private bool _isGenerating;

    [ObservableProperty]
    private string _statusText = string.Empty;

    [ObservableProperty]
    private string _rawJson = string.Empty;

    public ObservableCollection<SceneItem> GeneratedScenes { get; } = new();

    public ScriptWorkspace? ResultWorkspace { get; private set; }

    public ScriptGeneratorViewModel(
        GeminiScriptService geminiService, 
        GoogleTtsService? ttsService = null, 
        BgmService? bgmService = null)
    {
        _geminiService = geminiService;
        _ttsService = ttsService;
        _bgmService = bgmService ?? new BgmService(new FFmpegService());

        ReloadBgmTracks();
    }

    /// <summary>
    /// Nạp dữ liệu ý tưởng, công thức hook và phong cách trực tiếp từ YouTube Analytics
    /// </summary>
    public void InitFromAnalyticsRequest(CreateVideoFromAnalyticsRequest req)
    {
        Topic = req.Title;

        var sb = new System.Text.StringBuilder();
        if (!string.IsNullOrWhiteSpace(req.HookOpening))
        {
            sb.AppendLine($"[CHIẾN LƯỢC RETENTION HOOK 0-15S TỪ {req.SourceInfo}]");
            sb.AppendLine($"- Phân cảnh 1 (Scene 1) BẮT BUỘC mở màn bằng câu Hook: \"{req.HookOpening}\"");
            if (!string.IsNullOrWhiteSpace(req.HookStrategy))
                sb.AppendLine($"- Kỹ thuật giữ chân: {req.HookStrategy}");
            if (!string.IsNullOrWhiteSpace(req.TargetEmotion))
                sb.AppendLine($"- Cảm xúc nhắm đến: {req.TargetEmotion}");
            if (!string.IsNullOrWhiteSpace(req.TargetAudience))
                sb.AppendLine($"- Khán giả mục tiêu: {req.TargetAudience}");
        }
        CharacterRules = sb.ToString();

        if (!string.IsNullOrWhiteSpace(req.SuggestedStyle))
        {
            var matchStyle = StyleCards.FirstOrDefault(s => s.Key.Equals(req.SuggestedStyle, StringComparison.OrdinalIgnoreCase));
            if (matchStyle != null)
            {
                SelectedStyleCard = matchStyle;
                SelectedStyleDisplayText = $"{matchStyle.Icon} {matchStyle.Title}";
            }
        }

        if (!string.IsNullOrWhiteSpace(req.SuggestedBgm))
        {
            var matchBgm = AvailableBgmTracks.FirstOrDefault(t =>
                t.Id.Equals(req.SuggestedBgm, StringComparison.OrdinalIgnoreCase) ||
                t.Mood.Equals(req.SuggestedBgm, StringComparison.OrdinalIgnoreCase));
            if (matchBgm != null)
            {
                SelectedBgmTrack = matchBgm;
                EnableBgm = matchBgm.Id != "none";
            }
        }

        StatusText = $"🎯 Đã nạp ý tưởng & retention hook từ {req.SourceInfo}! Đang phân tích kịch bản...";
        
        // Tự động kích hoạt AI Planning
        _ = GeneratePlan();
    }


    /// <summary>
    /// Khi user bấm vào 1 Topic Card → set genre preset (style + tone + voice)
    /// </summary>
    [RelayCommand]
    private void SelectTopic(TopicCard? card)
    {
        if (card == null) return;
        SelectedTopicCard = card;
        SelectedGenreDisplayText = $"{card.Icon} {card.Title}";
        IsTopicPopupOpen = false;

        // Tự động chọn style phù hợp với genre
        var matchingStyle = StyleCards.FirstOrDefault(s => s.Key == card.SuggestedStyleKey);
        if (matchingStyle != null)
        {
            SelectedStyleCard = matchingStyle;
            SelectedStyleDisplayText = $"{matchingStyle.Icon} {matchingStyle.Title}";
        }
    }

    [RelayCommand]
    private void ClearGenreSelection()
    {
        SelectedTopicCard = null;
        SelectedGenreDisplayText = "🎲 Chưa chọn thể loại";
        IsTopicPopupOpen = false;
    }

    [RelayCommand]
    private void SelectStyle(StyleCard? card)
    {
        if (card == null) return;
        SelectedStyleCard = card;
        SelectedStyleDisplayText = $"{card.Icon} {card.Title}";
        IsStylePopupOpen = false;
    }

    [RelayCommand]
    private void ClearStyleSelection()
    {
        SelectedStyleCard = null;
        SelectedStyleDisplayText = "🎲 AI tự chọn";
        IsStylePopupOpen = false;
    }

    // ── Plan-level popup commands ──

    [RelayCommand]
    private void SelectPlanStyle(StyleCard? card)
    {
        if (card == null || CurrentPlan == null) return;
        CurrentPlan.SuggestedStyle = card.Key;
        CurrentPlan.StyleDescription = card.Description;
        IsPlanStylePopupOpen = false;
    }

    [RelayCommand]
    private void SelectPlanTone(ToneCard? card)
    {
        if (card == null || CurrentPlan == null) return;
        CurrentPlan.Tone = card.Title;
        IsPlanTonePopupOpen = false;
    }

    [RelayCommand]
    private void SelectPlanVoice(VoiceCard? card)
    {
        if (card == null || CurrentPlan == null) return;
        CurrentPlan.Voice = card.Key;
        IsPlanVoicePopupOpen = false;
    }

    [RelayCommand]
    private void SelectPlanScenes(int count)
    {
        if (CurrentPlan == null) return;
        CurrentPlan.SuggestedScenes = count;
        IsPlanScenesPopupOpen = false;
        OnPropertyChanged(nameof(EstimatedPlanCostDisplayText));
    }

    [ObservableProperty]
    private string _previewingVoiceName = string.Empty;

    [RelayCommand]
    private async Task PreviewVoice(VoiceCard? card)
    {
        if (card == null || _ttsService == null) return;
        if (IsPreviewingVoice) return;

        IsPreviewingVoice = true;
        PreviewingVoiceName = card.Key;
        try
        {
            // Dừng và giải phóng player cũ TRƯỚC khi ghi file mới
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                if (_previewPlayer != null)
                {
                    _previewPlayer.MediaEnded -= OnMediaEnded;
                    _previewPlayer.MediaFailed -= OnMediaFailed;
                    _previewPlayer.Stop();
                    _previewPlayer.Close();
                    _previewPlayer = null;
                }
            });

            await Task.Delay(100);

            var tempDir = Path.Combine(Path.GetTempPath(), "VideoAutoWpf", "voice_preview");
            Directory.CreateDirectory(tempDir);
            var tempFile = Path.Combine(tempDir, $"preview_{card.Key}_{DateTime.Now.Ticks}.mp3");

            await _ttsService.SynthesizeSpeechAsync(card.SampleText, tempFile, card.Key);

            // Phát audio preview - giữ IsPreviewingVoice=true cho đến khi phát xong
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                _previewPlayer = new MediaPlayer();
                _previewPlayer.MediaEnded += OnMediaEnded;
                _previewPlayer.MediaFailed += OnMediaFailed;
                _previewPlayer.Open(new Uri(tempFile));
                _previewPlayer.Play();
            });
        }
        catch (Exception ex)
        {
            IsPreviewingVoice = false;
            PreviewingVoiceName = string.Empty;
            MessageBox.Show($"Không thể nghe thử giọng:\n{ex.Message}", "Lỗi Preview", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        // KHÔNG reset IsPreviewingVoice ở finally — MediaEnded sẽ xử lý
    }

    private void OnMediaEnded(object? sender, EventArgs e)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            IsPreviewingVoice = false;
            PreviewingVoiceName = string.Empty;
        });
    }

    private void OnMediaFailed(object? sender, ExceptionEventArgs e)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            IsPreviewingVoice = false;
            PreviewingVoiceName = string.Empty;
        });
    }

    [RelayCommand]
    private void StopPreview()
    {
        if (_previewPlayer != null)
        {
            _previewPlayer.MediaEnded -= OnMediaEnded;
            _previewPlayer.MediaFailed -= OnMediaFailed;
            _previewPlayer.Stop();
            _previewPlayer.Close();
            _previewPlayer = null;
        }
        IsPreviewingVoice = false;
        PreviewingVoiceName = string.Empty;
    }

    /// <summary>
    /// Bước 1: Gọi AI lên kế hoạch (plan)
    /// </summary>
    [RelayCommand]
    private async Task GeneratePlan()
    {
        if (string.IsNullOrWhiteSpace(Topic))
        {
            MessageBox.Show("Vui lòng chọn chủ đề hoặc nhập nội dung câu chuyện!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        IsGeneratingPlan = true;
        StatusText = "⏳ AI đang phân tích chủ đề và lên kế hoạch...";

        try
        {
            var plan = await _geminiService.GeneratePlanAsync(Topic.Trim());

            // Override style nếu user đã chọn trước (qua genre hoặc manual)
            if (SelectedStyleCard != null)
            {
                plan.SuggestedStyle = SelectedStyleCard.Key;
                plan.StyleDescription = SelectedStyleCard.Description;
            }

            // Override tone + voice nếu có genre preset
            if (SelectedTopicCard != null)
            {
                if (!string.IsNullOrWhiteSpace(SelectedTopicCard.SuggestedTone))
                    plan.Tone = SelectedTopicCard.SuggestedTone;
                if (!string.IsNullOrWhiteSpace(SelectedTopicCard.SuggestedVoice))
                    plan.Voice = SelectedTopicCard.SuggestedVoice;
            }

            CurrentPlan = plan;
            HasPlan = true;
            StatusText = "✅ AI đã lên kế hoạch xong! Xem và chỉnh sửa bên dưới rồi bấm Xác Nhận.";
        }
        catch (Exception ex)
        {
            StatusText = "❌ Lỗi khi gọi AI lên kế hoạch.";
            MessageBox.Show($"Lỗi AI Planning:\n{ex.Message}", "Lỗi Gemini", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsGeneratingPlan = false;
        }
    }

    /// <summary>
    /// Bước 2: Xác nhận plan → gọi AI sinh kịch bản chi tiết
    /// </summary>
    [RelayCommand]
    private async Task ConfirmPlanAndGenerate()
    {
        if (CurrentPlan == null)
        {
            MessageBox.Show("Chưa có kế hoạch nào. Vui lòng bấm 'Lên Kế Hoạch AI' trước!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        IsGenerating = true;
        StatusText = "⏳ AI đang viết kịch bản chi tiết theo kế hoạch...";

        try
        {
            var workspace = await _geminiService.GenerateScriptAsync(Topic.Trim(), CurrentPlan, CharacterRules.Trim());
            GeneratedScenes.Clear();
            int idx = 1;
            foreach (var sc in workspace.Scenes)
            {
                GeneratedScenes.Add(new SceneItem
                {
                    Index = idx++,
                    Text = sc.Text,
                    ImagePrompt = sc.ImagePrompt,
                    MotionEffect = string.IsNullOrEmpty(sc.MotionEffect) ? "zoom_in" : sc.MotionEffect,
                    CharactersPresentText = sc.CharactersPresent != null && sc.CharactersPresent.Count > 0
                        ? string.Join(", ", sc.CharactersPresent)
                        : "🏙️ Ngoại cảnh / Hiện trường",
                    Status = "Đã sinh kịch bản"
                });
            }

            StatusText = $"✅ Đã tạo thành công {GeneratedScenes.Count} phân cảnh!";
            ResultWorkspace = workspace;
            GeneratedPublishInfo = workspace.PublishInfo;
        }
        catch (Exception ex)
        {
            StatusText = "❌ Lỗi khi sinh kịch bản chi tiết.";
            MessageBox.Show($"Lỗi sinh kịch bản:\n{ex.Message}", "Lỗi Gemini", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsGenerating = false;
        }
    }

    /// <summary>
    /// Quay lại bước chọn chủ đề (xóa plan)
    /// </summary>
    [RelayCommand]
    private void BackToTopicSelection()
    {
        CurrentPlan = null;
        HasPlan = false;
        GeneratedScenes.Clear();
        StatusText = string.Empty;
    }

    [RelayCommand]
    private async Task BrowseJsonFile()
    {
        var ofd = new OpenFileDialog
        {
            Title = "Chọn file JSON kịch bản",
            Filter = "File JSON (*.json)|*.json|Tất cả tệp (*.*)|*.*"
        };

        if (ofd.ShowDialog() == true)
        {
            try
            {
                RawJson = await File.ReadAllTextAsync(ofd.FileName);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Không thể đọc file JSON:\n{ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    [RelayCommand]
    private void Apply()
    {
        // 1. Nếu có nội dung ở tab JSON
        var trimmedJson = RawJson?.Trim();
        if (!string.IsNullOrEmpty(trimmedJson))
        {
            try
            {
                var ws = JsonSerializer.Deserialize<ScriptWorkspace>(trimmedJson, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (ws?.Scenes != null && ws.Scenes.Count > 0)
                {
                    ResultWorkspace = ws;
                    Cleanup();
                    RequestClose?.Invoke(true);
                    return;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Nội dung JSON không hợp lệ:\n{ex.Message}", "Lỗi cú pháp JSON", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
        }

        // 2. Nếu lấy từ danh sách phân cảnh AI sinh ra
        if (GeneratedScenes.Count > 0)
        {
            ResultWorkspace = new ScriptWorkspace
            {
                Metadata = new ScriptMetadata
                {
                    Ratio = CurrentPlan?.Ratio ?? "9:16",
                    Voice = CurrentPlan?.Voice ?? "vi-VN-Wavenet-B",
                    MotionEffect = CurrentPlan?.MotionEffect ?? "auto",
                    EnableFade = CurrentPlan?.EnableFadeTransition ?? true,
                    EnableVignette = CurrentPlan?.EnableVignette ?? false,
                    Music = SelectedBgmTrack?.FilePath ?? SelectedBgmTrack?.Id,
                    EnableMusic = EnableBgm && SelectedBgmTrack != null && SelectedBgmTrack.Id != "none",
                    MusicVolume = BgmVolume,
                    EnableSubtitles = EnableSubtitles,
                    SubtitleStyle = CurrentPlan?.SubtitleStyle ?? "cinematic",
                    SubtitleFont = CurrentPlan?.SubtitleFont ?? "Segoe UI Bold"
                },
                Scenes = GeneratedScenes.Select(s => new ScriptScene
                {
                    Text = s.Text ?? string.Empty,
                    ImagePrompt = s.ImagePrompt ?? string.Empty,
                    MotionEffect = s.MotionEffect
                }).ToList(),
                PublishInfo = GeneratedPublishInfo ?? ResultWorkspace?.PublishInfo
            };

            Cleanup();
            RequestClose?.Invoke(true);
            return;
        }

        MessageBox.Show("Chưa có phân cảnh nào được tạo hoặc nạp.", "Chưa có dữ liệu", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    [RelayCommand]
    private void TogglePreviewBgm()
    {
        if (IsPlayingBgm)
        {
            StopBgmPreview();
            return;
        }

        var path = SelectedBgmTrack?.FilePath;
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
        {
            MessageBox.Show("Vui lòng chọn một bản nhạc nền có sẵn hoặc chọn tệp từ máy tính để nghe thử.", "Chưa có nhạc nền", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        try
        {
            StopBgmPreview();
            var fullPath = Path.GetFullPath(path);
            _bgmPlayer = new MediaPlayer();
            _bgmPlayer.MediaEnded += (s, e) => Application.Current.Dispatcher.Invoke(StopBgmPreview);
            _bgmPlayer.MediaFailed += (s, e) => Application.Current.Dispatcher.Invoke(StopBgmPreview);
            _bgmPlayer.Open(new Uri(fullPath));
            _bgmPlayer.Volume = Math.Clamp(BgmVolume, 0.05, 1.0);
            _bgmPlayer.Play();
            IsPlayingBgm = true;
            PlayingBgmTitle = SelectedBgmTrack?.Title ?? Path.GetFileName(fullPath);
        }
        catch (Exception ex)
        {
            StopBgmPreview();
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
        var currentId = SelectedBgmTrack?.Id;
        AvailableBgmTracks.Clear();
        foreach (var track in _bgmService.GetAvailableTracks())
        {
            AvailableBgmTracks.Add(track);
        }

        if (!string.IsNullOrEmpty(currentId))
        {
            SelectedBgmTrack = AvailableBgmTracks.FirstOrDefault(t => t.Id == currentId)
                               ?? AvailableBgmTracks.FirstOrDefault(t => t.Id != "none");
        }
        else
        {
            SelectedBgmTrack = AvailableBgmTracks.FirstOrDefault(t => t.Id != "none") 
                               ?? AvailableBgmTracks.FirstOrDefault();
        }
    }

    [RelayCommand]
    private void BrowseCustomBgm()
    {
        var ofd = new OpenFileDialog
        {
            Title = "Chọn nhạc nền từ máy tính (BGM)",
            Filter = "Âm thanh (*.mp3;*.wav;*.aac;*.m4a;*.ogg;*.flac)|*.mp3;*.wav;*.aac;*.m4a;*.ogg;*.flac|Tất cả tệp (*.*)|*.*"
        };

        if (ofd.ShowDialog() == true)
        {
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
            EnableBgm = true;
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
                      "Mẹo: Sau khi tải file MP3 về, bấm nút [📂 Thư mục] và dán (paste) file vào đó để app tự nhận diện!";

        var result = MessageBox.Show(
            message + "\n\nBạn có muốn mở trang web Pixabay Music ngay bây giờ không?", 
            "Kho Nhạc Nền Miễn Phí Không Bản Quyền", 
            MessageBoxButton.YesNo, 
            MessageBoxImage.Information);

        if (result == MessageBoxResult.Yes)
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "https://pixabay.com/music/",
                    UseShellExecute = true
                });
            }
            catch { }
        }
    }

    public void Cleanup()
    {
        StopBgmPreview();
        try
        {
            _previewPlayer?.Stop();
            _previewPlayer?.Close();
        }
        catch { }
    }

    [RelayCommand]
    private void Cancel()
    {
        Cleanup();
        RequestClose?.Invoke(false);
    }
}
