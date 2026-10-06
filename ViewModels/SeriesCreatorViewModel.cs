using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VideoAutoWpf.Models;
using VideoAutoWpf.Services;

namespace VideoAutoWpf.ViewModels;

public partial class SeriesCreatorViewModel : ObservableObject
{
    private readonly GeminiScriptService _geminiService;
    private readonly YouTubeAnalyticsService? _youtubeService;

    public Action<bool>? RequestClose { get; set; }

    [ObservableProperty]
    private string _topicOrStory = string.Empty;

    // ── Nguồn tham khảo & Tra cứu tư liệu thực tế ──
    [ObservableProperty]
    private bool _isResearchingCase;

    [ObservableProperty]
    private bool _hasSourceVideo;

    [ObservableProperty]
    private string _sourceVideoId = string.Empty;

    [ObservableProperty]
    private string _sourceTitle = string.Empty;

    [ObservableProperty]
    private string _sourceDescription = string.Empty;

    // ── Cấu hình Gemini API Key (Google AI Studio - Miễn phí) ──
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ApiKeyToggleText))]
    private string _geminiApiKey = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ApiKeyToggleText))]
    private bool _hasGeminiApiKey;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ApiKeyToggleText))]
    private bool _showApiKeyInput;

    public string ApiKeyToggleText => ShowApiKeyInput ? "▲ Thu gọn" : (HasGeminiApiKey ? "✏️ Đổi Key" : "➕ Nhập Key");

    // ── Cấu hình số tập mở rộng ──
    public List<int> EpisodeCountOptions { get; } = new() { 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 18, 20, 25, 30, 40, 50 };

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TotalSeriesEstimatedDurationDisplay))]
    private int _selectedEpisodeCount = 5;

    [ObservableProperty]
    private string _episodeCountText = "5";

    partial void OnSelectedEpisodeCountChanged(int value)
    {
        if (value >= 2 && EpisodeCountText != value.ToString())
        {
            EpisodeCountText = value.ToString();
        }
        OnPropertyChanged(nameof(TotalSeriesEstimatedDurationDisplay));
    }

    partial void OnEpisodeCountTextChanged(string value)
    {
        if (int.TryParse(value, out int parsed) && parsed >= 2)
        {
            if (SelectedEpisodeCount != parsed)
                SelectedEpisodeCount = parsed;
        }
    }

    // ── Cấu hình số cảnh mỗi tập (Hỗ trợ từ Shorts đến video dài 5-15 phút/tập) ──
    public List<int> ScenesPerEpisodeOptions { get; } = new() { 6, 8, 10, 12, 15, 20, 25, 30, 35, 40, 45, 50, 55, 60, 70, 80, 90, 100 };

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EstimatedDurationPerEpisode))]
    [NotifyPropertyChangedFor(nameof(TotalSeriesEstimatedDurationDisplay))]
    private int _selectedScenesPerEpisode = 35; // Mặc định chuẩn ~5 phút/tập (theo yêu cầu)

    [ObservableProperty]
    private string _scenesPerEpisodeText = "35";

    partial void OnSelectedScenesPerEpisodeChanged(int value)
    {
        if (value >= 3 && ScenesPerEpisodeText != value.ToString())
        {
            ScenesPerEpisodeText = value.ToString();
        }
        OnPropertyChanged(nameof(EstimatedDurationPerEpisode));
        OnPropertyChanged(nameof(TotalSeriesEstimatedDurationDisplay));
    }

    partial void OnScenesPerEpisodeTextChanged(string value)
    {
        if (int.TryParse(value, out int parsed) && parsed >= 3)
        {
            if (SelectedScenesPerEpisode != parsed)
                SelectedScenesPerEpisode = parsed;
        }
    }

    public string EstimatedDurationPerEpisode
    {
        get
        {
            int secMin = SelectedScenesPerEpisode * 6;
            int secMax = SelectedScenesPerEpisode * 9;
            if (secMax < 60)
                return $"~{secMin}-{secMax}s / tập (Shorts)";
            int minMin = secMin / 60;
            int sMin = secMin % 60;
            int minMax = secMax / 60;
            int sMax = secMax % 60;
            return $"~{minMin}p{sMin:D2}s - {minMax}p{sMax:D2}s / tập";
        }
    }

    public string TotalSeriesEstimatedDurationDisplay
    {
        get
        {
            int totalEpisodes = CurrentSeriesPlan != null && CurrentSeriesPlan.Episodes.Count > 0
                ? CurrentSeriesPlan.Episodes.Count
                : SelectedEpisodeCount;

            int totalScenes = CurrentSeriesPlan != null && CurrentSeriesPlan.Episodes.Count > 0
                ? CurrentSeriesPlan.Episodes.Sum(e => e.SuggestedSceneCount > 0 ? e.SuggestedSceneCount : SelectedScenesPerEpisode)
                : SelectedEpisodeCount * SelectedScenesPerEpisode;

            int totalSecMin = totalScenes * 6;
            int totalSecMax = totalScenes * 9;
            int minMin = totalSecMin / 60;
            int minMax = totalSecMax / 60;
            if (minMax < 60)
                return $"Tổng {totalEpisodes} tập: ~{minMin}-{minMax} phút ({totalScenes} cảnh)";
            int hMin = minMin / 60;
            int mMin = minMin % 60;
            int hMax = minMax / 60;
            int mMax = minMax % 60;
            return $"Tổng {totalEpisodes} tập: ~{hMin}h{mMin:D2}p - {hMax}h{mMax:D2}p ({totalScenes} cảnh)";
        }
    }

    [RelayCommand]
    private void SetDurationPreset(string preset)
    {
        switch (preset.ToLowerInvariant())
        {
            case "shorts": // ~45s - 1 phút
                SelectedScenesPerEpisode = 6;
                SelectedAspectRatioDisplay = "9:16 (Dọc - Shorts/TikTok)";
                break;
            case "short_3m": // ~2 - 3 phút
                SelectedScenesPerEpisode = 20;
                SelectedAspectRatioDisplay = "9:16 (Dọc - Shorts/TikTok)";
                break;
            case "standard_5m": // ~5 phút (Chuẩn video YouTube)
                SelectedScenesPerEpisode = 35;
                SelectedAspectRatioDisplay = "16:9 (Ngang - YouTube)";
                break;
            case "long_8m": // ~8 - 10 phút
                SelectedScenesPerEpisode = 55;
                SelectedAspectRatioDisplay = "16:9 (Ngang - YouTube)";
                break;
            case "feature_15m": // ~12 - 15 phút
                SelectedScenesPerEpisode = 90;
                SelectedAspectRatioDisplay = "16:9 (Ngang - YouTube)";
                break;
        }
    }

    // ── Tỷ lệ khung hình (Aspect Ratio) ──
    public List<string> AspectRatioOptions { get; } = new() { "9:16 (Dọc - Shorts/TikTok)", "16:9 (Ngang - YouTube)", "1:1 (Vuông - Social)" };

    [ObservableProperty]
    private string _selectedAspectRatioDisplay = "9:16 (Dọc - Shorts/TikTok)";

    public string ActualAspectRatio => SelectedAspectRatioDisplay.StartsWith("16:9") ? "16:9" : SelectedAspectRatioDisplay.StartsWith("1:1") ? "1:1" : "9:16";

    // ── Nhịp điệu & Thể loại Series (Pacing & Tone) ──
    public List<string> SeriesToneOptions { get; } = new()
    {
        "Kịch tính, dồn dập, giật gân (Dramatic / Thriller)",
        "Kể chuyện bí ẩn, lôi cuốn (Mystery / True Crime)",
        "Hồi hộp, rùng rợn, giật gân (Horror / Creepy)",
        "Truyền cảm hứng, bài học cuộc sống (Inspirational / Wisdom)",
        "Hài hước, dí dỏm, cú twist bất ngờ (Comedy / Plot Twist)",
        "Tài liệu, giải mã, kiến thức thú vị (Documentary / Explainer)",
        "Cảm xúc, sâu lắng, tình cảm (Emotional / Drama)"
    };

    [ObservableProperty]
    private string _selectedSeriesTone = "Kịch tính, dồn dập, giật gân (Dramatic / Thriller)";

    public ObservableCollection<StyleCard> StyleCards { get; } = new(StyleCard.GetDefaultStyles());

    [ObservableProperty]
    private StyleCard? _selectedStyleCard;

    public ObservableCollection<VoiceCard> VoiceCards { get; } = new(VoiceCard.GetDefaultVoices());

    [ObservableProperty]
    private VoiceCard? _selectedVoiceCard;

    [ObservableProperty]
    private SeriesPlan? _currentSeriesPlan;

    [ObservableProperty]
    private bool _hasSeriesPlan;

    [ObservableProperty]
    private bool _isPlanningSeries;

    [ObservableProperty]
    private bool _isGeneratingEpisodes;

    [ObservableProperty]
    private double _batchProgress;

    [ObservableProperty]
    private string _batchStatusText = string.Empty;

    [ObservableProperty]
    private SeriesProject? _resultSeriesProject;

    public SeriesCreatorViewModel(GeminiScriptService geminiService, YouTubeAnalyticsService? youtubeService = null)
    {
        _geminiService = geminiService;
        _youtubeService = youtubeService;
        _selectedStyleCard = StyleCards.FirstOrDefault(s => s.Key == "dark-anime") ?? StyleCards.FirstOrDefault();
        _selectedVoiceCard = VoiceCards.FirstOrDefault(v => v.Key == "vi-VN-Neural2-D") ?? VoiceCards.FirstOrDefault();
        _geminiApiKey = _geminiService.ApiKey ?? string.Empty;
        _hasGeminiApiKey = !string.IsNullOrWhiteSpace(_geminiApiKey);
        _showApiKeyInput = !_hasGeminiApiKey;
    }

    /// <summary>
    /// Nạp dữ liệu ý tưởng, tiêu đề và phân tích đối thủ từ YouTube Analytics vào Series Creator
    /// </summary>
    public void InitFromAnalyticsRequest(CreateVideoFromAnalyticsRequest req)
    {
        SourceVideoId = req.VideoId ?? string.Empty;
        SourceTitle = req.Title ?? string.Empty;
        SourceDescription = req.Description ?? string.Empty;
        HasSourceVideo = !string.IsNullOrWhiteSpace(SourceVideoId) || !string.IsNullOrWhiteSpace(SourceDescription);

        var sb = new System.Text.StringBuilder();
        if (!string.IsNullOrWhiteSpace(req.Title))
            sb.AppendLine($"Chủ đề: {req.Title}");
        if (!string.IsNullOrWhiteSpace(req.HookOpening))
            sb.AppendLine($"Hook mở màn: {req.HookOpening}");
        if (!string.IsNullOrWhiteSpace(req.HookStrategy))
            sb.AppendLine($"Chiến lược retention hook: {req.HookStrategy}");
        if (!string.IsNullOrWhiteSpace(req.TargetEmotion))
            sb.AppendLine($"Cảm xúc mục tiêu: {req.TargetEmotion}");
        if (!string.IsNullOrWhiteSpace(req.TargetAudience))
            sb.AppendLine($"Khán giả mục tiêu: {req.TargetAudience}");
        if (!string.IsNullOrWhiteSpace(req.SourceInfo))
            sb.AppendLine($"Nguồn cảm hứng / Tham khảo: {req.SourceInfo}");

        if (!string.IsNullOrWhiteSpace(req.Description))
        {
            sb.AppendLine();
            sb.AppendLine("--- TÓM TẮT & TƯ LIỆU GỐC TỪ MÔ TẢ VIDEO ---");
            sb.AppendLine(req.Description.Trim());
        }

        TopicOrStory = sb.ToString().Trim();

        if (!string.IsNullOrWhiteSpace(req.SuggestedStyle))
        {
            var matchStyle = StyleCards.FirstOrDefault(s => s.Key.Equals(req.SuggestedStyle, StringComparison.OrdinalIgnoreCase))
                ?? StyleCards.FirstOrDefault(s => s.Title.Contains(req.SuggestedStyle, StringComparison.OrdinalIgnoreCase));
            if (matchStyle != null)
            {
                SelectedStyleCard = matchStyle;
            }
        }

        if (req.VideoDuration.TotalMinutes >= 5)
        {
            // Tự động tính toán số tập và số cảnh phù hợp với video gốc (mỗi tập ~5 phút)
            int targetEp = (int)Math.Round(req.VideoDuration.TotalMinutes / 5.0);
            if (targetEp < 2) targetEp = 2;
            if (targetEp > 25) targetEp = 25;

            if (!EpisodeCountOptions.Contains(targetEp))
            {
                EpisodeCountOptions.Add(targetEp);
                EpisodeCountOptions.Sort();
            }

            SelectedEpisodeCount = targetEp;
            SelectedScenesPerEpisode = 35; // Chuẩn ~5 phút/tập
            SelectedAspectRatioDisplay = "16:9 (Ngang - YouTube)"; // Video dài YouTube chuẩn 16:9

            var durStr = (int)req.VideoDuration.TotalHours > 0 
                ? $"{(int)req.VideoDuration.TotalHours}h{req.VideoDuration.Minutes}p" 
                : $"{req.VideoDuration.Minutes}p";

            BatchStatusText = $"💡 Video gốc dài {durStr}: Đã tự động đề xuất {targetEp} tập (~5 phút/tập, 35 cảnh, tỷ lệ 16:9) để truyền tải trọn vẹn toàn bộ vụ án!";
        }
        else
        {
            SelectedScenesPerEpisode = 35; // Mặc định chuẩn ~5 phút/tập
            BatchStatusText = $"🎯 Đã nạp ý tưởng từ YouTube Analytics ({req.SourceInfo}). Mỗi tập được đặt chuẩn ~5 phút ({SelectedScenesPerEpisode} cảnh/tập)!";
        }
    }

    /// <summary>
    /// AI Tra cứu các tình tiết có thật của vụ án từ hồ sơ thực tế, không bịa chuyện
    /// </summary>
    [RelayCommand]
    private async Task ResearchCaseFactsAsync()
    {
        if (string.IsNullOrWhiteSpace(TopicOrStory) && string.IsNullOrWhiteSpace(SourceTitle))
        {
            MessageBox.Show("Vui lòng nhập chủ đề hoặc tiêu đề vụ án cần tra cứu hồ sơ!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        IsResearchingCase = true;
        BatchStatusText = "🔍 AI đang tra cứu & phục dựng các tình tiết CÓ THẬT của vụ án từ hồ sơ thực tế...";

        try
        {
            var title = !string.IsNullOrWhiteSpace(SourceTitle) ? SourceTitle : TopicOrStory;
            var facts = await _geminiService.ResearchCaseFactsAsync(title, SourceDescription);

            if (!string.IsNullOrWhiteSpace(facts))
            {
                TopicOrStory = facts.Trim();
                BatchStatusText = "✅ Đã phục dựng thành công hồ sơ tình tiết có thật của vụ án! Sẵn sàng bấm 'Lập Dàn Ý Chuỗi Video'.";
                MessageBox.Show("Đã tra cứu và cập nhật toàn bộ hồ sơ tình tiết có thật của vụ án vào ô cốt truyện!\n\nBạn có thể đọc lại hoặc tinh chỉnh rồi bấm 'AI Phân Tích & Lập Dàn Ý Chuỗi Video'.", "Tra Cứu Thành Công", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            BatchStatusText = $"❌ Lỗi tra cứu vụ án: {ex.Message}";
            MessageBox.Show($"Không thể tra cứu hồ sơ vụ án: {ex.Message}", "Lỗi Tra Cứu", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsResearchingCase = false;
        }
    }

    /// <summary>
    /// Tự động tải phụ đề / lời thoại gốc của video YouTube
    /// </summary>
    [RelayCommand]
    private async Task FetchTranscriptAsync()
    {
        if (string.IsNullOrWhiteSpace(SourceVideoId) || _youtubeService == null)
        {
            MessageBox.Show("Không có thông tin Video ID YouTube để tải phụ đề/lời thoại.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        IsResearchingCase = true;
        BatchStatusText = "📜 Đang cào phụ đề / lời thoại gốc từ YouTube...";

        try
        {
            var transcript = await _youtubeService.GetVideoTranscriptAsync(SourceVideoId);
            if (!string.IsNullOrWhiteSpace(transcript))
            {
                var sb = new System.Text.StringBuilder();
                if (!string.IsNullOrWhiteSpace(SourceTitle))
                    sb.AppendLine($"Chủ đề: {SourceTitle}\n");
                sb.AppendLine("--- LỜI THOẠI / PHỤ ĐỀ GỐC CỦA VIDEO TRÊN YOUTUBE ---");
                sb.AppendLine(transcript.Trim());
                TopicOrStory = sb.ToString();
                BatchStatusText = $"✅ Đã tải thành công {transcript.Length:N0} ký tự lời thoại của video YouTube gốc!";
                MessageBox.Show($"Đã tải thành công {transcript.Length:N0} ký tự phụ đề/lời thoại từ YouTube và nạp vào cốt truyện!", "Tải Thành Công", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show("Video này không có phụ đề (captions/transcript) công khai trên YouTube.\n\n👉 Bạn hãy bấm nút '🔍 Tra Cứu Vụ Án Thật (AI Fact-Check)' để AI tự đối chiếu hồ sơ vụ án thực tế!", "Không có phụ đề", MessageBoxButton.OK, MessageBoxImage.Warning);
                BatchStatusText = "⚠️ Video không có phụ đề công khai. Hãy dùng 'Tra Cứu Vụ Án Thật'.";
            }
        }
        catch (Exception ex)
        {
            BatchStatusText = $"❌ Lỗi tải phụ đề: {ex.Message}";
            MessageBox.Show($"Lỗi khi tải phụ đề: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsResearchingCase = false;
        }
    }

    /// <summary>
    /// Bước 1: AI Lên kế hoạch dàn ý cho Series nhiều tập
    /// </summary>
    [RelayCommand]
    private async Task PlanSeriesAsync()
    {
        if (string.IsNullOrWhiteSpace(TopicOrStory))
        {
            MessageBox.Show("Vui lòng nhập chủ đề hoặc cốt truyện dài của chuỗi video!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // Đảm bảo số tập và số cảnh lấy đúng giá trị parsed nếu người dùng tự gõ vào ô Text
        if (int.TryParse(EpisodeCountText, out int epParsed) && epParsed >= 2)
            SelectedEpisodeCount = epParsed;
        if (int.TryParse(ScenesPerEpisodeText, out int scParsed) && scParsed >= 3)
            SelectedScenesPerEpisode = scParsed;

        if (SelectedEpisodeCount < 2) SelectedEpisodeCount = 2;
        if (SelectedScenesPerEpisode < 3) SelectedScenesPerEpisode = 3;

        IsPlanningSeries = true;
        BatchStatusText = $"⏳ AI đang phân tích cốt truyện, lập dàn ý {SelectedEpisodeCount} tập (~{SelectedScenesPerEpisode} cảnh/tập)...";

        try
        {
            var styleKey = SelectedStyleCard?.Key ?? "dark-anime";
            var voiceKey = SelectedVoiceCard?.Key ?? "vi-VN-Neural2-D";

            SeriesPlan? plan = null;
            for (int attempt = 1; attempt <= 3; attempt++)
            {
                try
                {
                    plan = await _geminiService.GenerateSeriesPlanAsync(
                        TopicOrStory.Trim(),
                        SelectedEpisodeCount,
                        SelectedScenesPerEpisode,
                        styleKey,
                        voiceKey,
                        SelectedSeriesTone,
                        ActualAspectRatio);
                    if (plan != null && plan.Episodes.Count > 0)
                        break;
                }
                catch
                {
                    if (attempt == 3) throw;
                    BatchStatusText = $"⏳ Đang thử lại lập dàn ý (Lần {attempt + 1}/3 do máy chủ AI tải nặng)...";
                    await Task.Delay(2500);
                }
            }

            if (plan == null) throw new Exception("Không nhận được dữ liệu dàn ý từ AI.");

            // 1. Luôn chuẩn hóa số tập và tiêu đề theo đúng thứ tự 1, 2, 3, 4, 5...
            for (int i = 0; i < plan.Episodes.Count; i++)
            {
                var ep = plan.Episodes[i];
                ep.EpisodeNumber = i + 1; // BẮT BUỘC ĐÁNH SỐ TẬP TỪ 1 ĐẾN N
                ep.SuggestedSceneCount = SelectedScenesPerEpisode;

                // Chuẩn hóa tiêu đề: loại bỏ "Tập 1:", "Tập 2:", "1." nếu có sẵn từ AI để tránh lặp
                var rawTitle = ep.EpisodeTitle?.Trim() ?? string.Empty;
                var cleanTitle = System.Text.RegularExpressions.Regex.Replace(rawTitle, @"^(Tập\s*\d+|Episode\s*\d+|\d+)\s*[:\.\-]\s*", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase).Trim();
                if (string.IsNullOrWhiteSpace(cleanTitle))
                    cleanTitle = string.IsNullOrWhiteSpace(rawTitle) ? $"Diễn biến gay cấn hồi {i + 1}" : rawTitle;

                ep.EpisodeTitle = $"Tập {i + 1}: {cleanTitle}";
            }

            // 2. Bảo đảm nếu AI sinh thiếu tập so với SelectedEpisodeCount, tự động sinh nối tiếp các tập còn lại
            if (plan.Episodes.Count < SelectedEpisodeCount)
            {
                int missingCount = SelectedEpisodeCount - plan.Episodes.Count;
                int currentMax = plan.Episodes.Count;
                for (int i = 1; i <= missingCount; i++)
                {
                    int epNum = currentMax + i;
                    bool isFinal = (epNum == SelectedEpisodeCount);
                    plan.Episodes.Add(new EpisodePlanItem
                    {
                        EpisodeNumber = epNum,
                        EpisodeTitle = isFinal ? $"Tập {epNum}: Đại kết cục & Hé lộ toàn bộ sự thật" : $"Tập {epNum}: Diễn biến cao trào tiếp theo",
                        PlotBeat = isFinal ? "Mọi bí mật được phơi bày, nút thắt được gỡ bỏ hoàn toàn trong cảnh kết đầy cảm xúc." 
                                           : $"Tình huống tiếp tục phát triển căng thẳng sau Tập {epNum - 1}, xung đột dâng cao.",
                        EpisodeHook = $"Mở đầu dồn dập giải quyết trực tiếp tình huống ngặt nghèo ở kết thúc Tập {epNum - 1}.",
                        Cliffhanger = isFinal ? "Cái kết đọng lại nhiều suy ngẫm sâu sắc và ấn tượng khó phai cho người xem."
                                              : $"Tình tiết bất ngờ đảo ngược cục diện ở giây cuối cùng, thúc đẩy xem Tập {epNum + 1}!",
                        SuggestedSceneCount = SelectedScenesPerEpisode
                    });
                }
            }

            // Đảm bảo lại một lần nữa 100% số thứ tự tập từ 1 đến N
            for (int i = 0; i < plan.Episodes.Count; i++)
            {
                plan.Episodes[i].EpisodeNumber = i + 1;
            }

            CurrentSeriesPlan = plan;
            HasSeriesPlan = true;
            BatchStatusText = $"✅ Đã lập dàn ý {plan.Episodes.Count} tập thành công! Bạn có thể tùy biến số cảnh từng tập bên dưới.";
        }
        catch (Exception ex)
        {
            BatchStatusText = "❌ Lỗi khi AI lập kế hoạch Series.";
            if (!HasGeminiApiKey)
            {
                ShowApiKeyInput = true;
            }
            MessageBox.Show($"Lỗi AI Series Planning:\n{ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsPlanningSeries = false;
        }
    }

    [RelayCommand]
    private void SaveGeminiApiKey()
    {
        if (string.IsNullOrWhiteSpace(GeminiApiKey))
        {
            MessageBox.Show("Vui lòng dán mã Gemini API Key từ Google AI Studio (bắt đầu bằng AIzaSy...).", "Chưa nhập Key", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var key = GeminiApiKey.Trim();
        _geminiService.ApiKey = key;
        GeminiScriptService.SaveApiKey(key);
        HasGeminiApiKey = true;
        ShowApiKeyInput = false;
        BatchStatusText = "✅ Đã lưu Gemini API Key! Bạn có thể bấm lập dàn ý chuỗi video ngay bây giờ.";
        MessageBox.Show("Đã lưu Gemini API Key thành công!\nỨng dụng sẽ sử dụng Gemini AI miễn phí từ Google AI Studio (không cần thẻ tín dụng/billing).", "Thành công", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    [RelayCommand]
    private void ToggleApiKeyInput()
    {
        ShowApiKeyInput = !ShowApiKeyInput;
    }

    [RelayCommand]
    private void OpenAiStudioKeyPage()
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "https://aistudio.google.com/app/apikey",
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Không thể mở trình duyệt: {ex.Message}\nVui lòng truy cập thủ công: https://aistudio.google.com/app/apikey", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    /// <summary>
    /// Đồng bộ số cảnh mong muốn cho toàn bộ các tập trong dàn ý
    /// </summary>
    [RelayCommand]
    private void ApplySceneCountToAllEpisodes()
    {
        if (CurrentSeriesPlan == null || CurrentSeriesPlan.Episodes.Count == 0) return;
        foreach (var ep in CurrentSeriesPlan.Episodes)
        {
            ep.SuggestedSceneCount = SelectedScenesPerEpisode;
        }
        BatchStatusText = $"✅ Đã đồng bộ {SelectedScenesPerEpisode} cảnh cho toàn bộ {CurrentSeriesPlan.Episodes.Count} tập!";
    }

    /// <summary>
    /// Thêm 1 tập mới vào dàn ý
    /// </summary>
    [RelayCommand]
    private void AddEpisode()
    {
        if (CurrentSeriesPlan == null) return;
        int nextNum = CurrentSeriesPlan.Episodes.Count + 1;
        CurrentSeriesPlan.Episodes.Add(new EpisodePlanItem
        {
            EpisodeNumber = nextNum,
            EpisodeTitle = $"Tập {nextNum}: Diễn biến gay cấn tiếp theo",
            PlotBeat = "Nội dung tiếp nối của tập trước...",
            EpisodeHook = "Tình huống hoặc câu hỏi mở đầu gây tò mò",
            Cliffhanger = "Kết thúc nghẹt thở giữ chân khán giả sang tập tiếp theo",
            SuggestedSceneCount = SelectedScenesPerEpisode
        });
        BatchStatusText = $"➕ Đã thêm Tập {nextNum}. Tổng số tập: {CurrentSeriesPlan.Episodes.Count}.";
    }

    /// <summary>
    /// Xóa 1 tập khỏi dàn ý
    /// </summary>
    [RelayCommand]
    private void RemoveEpisode(EpisodePlanItem? ep)
    {
        if (CurrentSeriesPlan == null || ep == null) return;
        if (CurrentSeriesPlan.Episodes.Count <= 2)
        {
            MessageBox.Show("Chuỗi video cần tối thiểu 2 tập!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        CurrentSeriesPlan.Episodes.Remove(ep);
        for (int i = 0; i < CurrentSeriesPlan.Episodes.Count; i++)
        {
            CurrentSeriesPlan.Episodes[i].EpisodeNumber = i + 1;
        }
        BatchStatusText = $"🗑️ Đã xóa 1 tập. Hiện còn {CurrentSeriesPlan.Episodes.Count} tập.";
    }

    /// <summary>
    /// Quay lại bước 1 để chỉnh sửa cốt truyện hoặc thông số ban đầu
    /// </summary>
    [RelayCommand]
    private void BackToInput()
    {
        HasSeriesPlan = false;
    }

    /// <summary>
    /// Bước 2: AI Sinh kịch bản chi tiết cho TẤT CẢ các tập theo quy trình tuần tự
    /// </summary>
    [RelayCommand]
    private async Task GenerateAllEpisodesAsync()
    {
        if (CurrentSeriesPlan == null || CurrentSeriesPlan.Episodes.Count == 0)
        {
            MessageBox.Show("Chưa có dàn ý Series. Vui lòng bấm 'Lập Kế Hoạch Chuỗi' trước!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        IsGeneratingEpisodes = true;
        BatchProgress = 0;

        var seriesProject = new SeriesProject
        {
            SeriesTitle = CurrentSeriesPlan.SeriesTitle,
            OverallPremise = CurrentSeriesPlan.OverallPremise,
            TotalEpisodes = CurrentSeriesPlan.Episodes.Count,
            GlobalStyleKey = CurrentSeriesPlan.SuggestedStyle,
            GlobalVoice = CurrentSeriesPlan.SuggestedVoice,
            AspectRatio = ActualAspectRatio,
            CharacterBibleRules = CurrentSeriesPlan.CharacterBible
        };

        try
        {
            string previousEnding = string.Empty;
            int total = CurrentSeriesPlan.Episodes.Count;

            for (int i = 0; i < total; i++)
            {
                var epPlan = CurrentSeriesPlan.Episodes[i];
                var epNum = epPlan.EpisodeNumber;
                var sceneCount = epPlan.SuggestedSceneCount > 0 ? epPlan.SuggestedSceneCount : SelectedScenesPerEpisode;

                BatchProgress = (i / (double)total) * 100;
                BatchStatusText = $"⏳ Đang viết kịch bản chi tiết cho Tập {epNum}/{total} ({sceneCount} cảnh): '{epPlan.EpisodeTitle}'...";

                // Gọi sinh kịch bản chi tiết với cơ chế thử lại (retry) nếu gặp gián đoạn kết nối
                ScriptWorkspace? ws = null;
                for (int attempt = 1; attempt <= 3; attempt++)
                {
                    try
                    {
                        ws = await _geminiService.GenerateEpisodeScriptAsync(
                            CurrentSeriesPlan,
                            epPlan,
                            total,
                            previousEnding,
                            ActualAspectRatio,
                            SelectedSeriesTone,
                            TopicOrStory);
                        if (ws != null && ws.Scenes.Count > 0)
                            break;
                    }
                    catch
                    {
                        if (attempt == 3) throw;
                        BatchStatusText = $"⏳ Đang thử lại Tập {epNum}/{total} (Lần {attempt + 1}/3 do máy chủ AI đang tải nặng)...";
                        await Task.Delay(2500);
                    }
                }

                if (ws == null)
                    throw new Exception($"Không nhận được dữ liệu kịch bản cho Tập {epNum}.");

                var epItem = new EpisodeItem
                {
                    EpisodeNumber = epNum,
                    EpisodeTitle = epPlan.EpisodeTitle,
                    EpisodePremise = epPlan.PlotBeat,
                    EpisodeHook = epPlan.EpisodeHook,
                    Cliffhanger = epPlan.Cliffhanger,
                    Status = "Kịch bản sẵn sàng"
                };

                if (ws.PublishInfo != null)
                {
                    epItem.PublishInfo = ws.PublishInfo;
                }

                int sIdx = 1;
                foreach (var sc in ws.Scenes)
                {
                    epItem.Scenes.Add(new SceneItem
                    {
                        Index = sIdx++,
                        Text = sc.Text,
                        ImagePrompt = sc.ImagePrompt,
                        MotionEffect = string.IsNullOrEmpty(sc.MotionEffect) ? "zoom_in" : sc.MotionEffect,
                        CharactersPresentText = sc.CharactersPresent != null && sc.CharactersPresent.Count > 0
                            ? string.Join(", ", sc.CharactersPresent)
                            : "🏙️ Ngoại cảnh / Hiện trường",
                        Status = "Đã sinh kịch bản"
                    });
                }

                // ĐẢM BẢO ĐỦ 100% SỐ CẢNH THEO YÊU CẦU:
                // Nếu AI trả về ít cảnh hơn sceneCount, tự động bổ sung cảnh nối tiếp để đủ số cảnh
                var stylePrompt = GeminiScriptService.StylePrompts.TryGetValue(CurrentSeriesPlan.SuggestedStyle, out var sp) ? sp : GeminiScriptService.StylePrompts["dark-anime"];
                while (epItem.Scenes.Count < sceneCount)
                {
                    int missingIdx = epItem.Scenes.Count + 1;
                    var lastScene = epItem.Scenes.LastOrDefault();
                    string contText = $"Diễn biến tiếp nối đầy kịch tính của Tập {epNum}, đẩy cốt truyện đến cao trào nghẹt thở...";
                    string contPrompt = (lastScene != null && !string.IsNullOrWhiteSpace(lastScene.ImagePrompt))
                        ? lastScene.ImagePrompt
                        : $"{stylePrompt} Dramatic cinematic continuation shot, {CurrentSeriesPlan.CharacterBible}";

                    epItem.Scenes.Add(new SceneItem
                    {
                        Index = missingIdx,
                        Text = contText,
                        ImagePrompt = contPrompt,
                        MotionEffect = (missingIdx % 2 == 0) ? "pan_left_right" : "zoom_in",
                        Status = "Đã sinh kịch bản"
                    });
                }

                // Ghi nhớ đoạn kết tập này để làm bối cảnh tiếp nối cho tập sau
                if (epItem.Scenes.Count > 0)
                {
                    previousEnding = epItem.Scenes.Last().Text ?? epPlan.Cliffhanger;
                }

                seriesProject.Episodes.Add(epItem);

                // Delay nhẹ giữa các lần gọi API để tránh rate limit
                await Task.Delay(1200);
            }

            BatchProgress = 100;
            BatchStatusText = $"🎉 Đã tạo hoàn tất kịch bản cho toàn bộ {total} tập của Series!";

            ResultSeriesProject = seriesProject;
            MessageBox.Show($"Chúc mừng! Đã tạo thành công kịch bản cho toàn bộ {total} tập của chuỗi '{seriesProject.SeriesTitle}'.", "Thành công", MessageBoxButton.OK, MessageBoxImage.Information);

            RequestClose?.Invoke(true);
        }
        catch (Exception ex)
        {
            BatchStatusText = "❌ Lỗi khi sinh kịch bản chi tiết.";
            MessageBox.Show($"Lỗi sinh kịch bản chuỗi:\n{ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsGeneratingEpisodes = false;
        }
    }

    /// <summary>
    /// Áp dụng dàn ý vào dự án (cho phép người dùng tự chỉnh sửa kịch bản từng tập sau)
    /// </summary>
    [RelayCommand]
    private void ApplyPlanOnly()
    {
        if (CurrentSeriesPlan == null || CurrentSeriesPlan.Episodes.Count == 0) return;

        var seriesProject = new SeriesProject
        {
            SeriesTitle = CurrentSeriesPlan.SeriesTitle,
            OverallPremise = CurrentSeriesPlan.OverallPremise,
            TotalEpisodes = CurrentSeriesPlan.Episodes.Count,
            GlobalStyleKey = CurrentSeriesPlan.SuggestedStyle,
            GlobalVoice = CurrentSeriesPlan.SuggestedVoice,
            AspectRatio = ActualAspectRatio,
            CharacterBibleRules = CurrentSeriesPlan.CharacterBible
        };

        foreach (var ep in CurrentSeriesPlan.Episodes)
        {
            seriesProject.Episodes.Add(new EpisodeItem
            {
                EpisodeNumber = ep.EpisodeNumber,
                EpisodeTitle = ep.EpisodeTitle,
                EpisodePremise = ep.PlotBeat,
                EpisodeHook = ep.EpisodeHook,
                Cliffhanger = ep.Cliffhanger,
                Status = "Chưa dựng"
            });
        }

        ResultSeriesProject = seriesProject;
        RequestClose?.Invoke(true);
    }
}
