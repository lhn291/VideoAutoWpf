using System.Collections.ObjectModel;
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

    public Action<bool>? RequestClose { get; set; }

    [ObservableProperty]
    private string _topicOrStory = string.Empty;

    // ── Cấu hình số tập mở rộng ──
    public List<int> EpisodeCountOptions { get; } = new() { 2, 3, 4, 5, 6, 7, 8, 9, 10, 12, 15, 20, 25, 30, 40, 50 };

    [ObservableProperty]
    private int _selectedEpisodeCount = 3;

    // ── Cấu hình số cảnh mỗi tập ──
    public List<int> ScenesPerEpisodeOptions { get; } = new() { 3, 4, 5, 6, 7, 8, 9, 10, 12, 14, 16, 18, 20, 25, 30 };

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EstimatedDurationPerEpisode))]
    private int _selectedScenesPerEpisode = 6;

    public string EstimatedDurationPerEpisode
    {
        get
        {
            int secMin = SelectedScenesPerEpisode * 6;
            int secMax = SelectedScenesPerEpisode * 9;
            if (secMax < 60)
                return $"~{secMin}-{secMax}s / tập (Shorts/TikTok)";
            int minMin = secMin / 60;
            int sMin = secMin % 60;
            int minMax = secMax / 60;
            int sMax = secMax % 60;
            return $"~{minMin}p{sMin:D2}s - {minMax}p{sMax:D2}s / tập";
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

    public SeriesCreatorViewModel(GeminiScriptService geminiService)
    {
        _geminiService = geminiService;
        _selectedStyleCard = StyleCards.FirstOrDefault(s => s.Key == "dark-anime") ?? StyleCards.FirstOrDefault();
        _selectedVoiceCard = VoiceCards.FirstOrDefault(v => v.Key == "vi-VN-Wavenet-B") ?? VoiceCards.FirstOrDefault();
    }

    /// <summary>
    /// Nạp dữ liệu ý tưởng, tiêu đề và phân tích đối thủ từ YouTube Analytics vào Series Creator
    /// </summary>
    public void InitFromAnalyticsRequest(CreateVideoFromAnalyticsRequest req)
    {
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

        BatchStatusText = $"🎯 Đã nạp ý tưởng từ YouTube Analytics ({req.SourceInfo}). Sẵn sàng lập kế hoạch chuỗi nhiều tập!";
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

        if (SelectedEpisodeCount < 2) SelectedEpisodeCount = 2;
        if (SelectedScenesPerEpisode < 3) SelectedScenesPerEpisode = 3;

        IsPlanningSeries = true;
        BatchStatusText = $"⏳ AI đang phân tích cốt truyện, lập dàn ý {SelectedEpisodeCount} tập (~{SelectedScenesPerEpisode} cảnh/tập)...";

        try
        {
            var styleKey = SelectedStyleCard?.Key ?? "dark-anime";
            var voiceKey = SelectedVoiceCard?.Key ?? "vi-VN-Wavenet-B";

            var plan = await _geminiService.GenerateSeriesPlanAsync(
                TopicOrStory.Trim(),
                SelectedEpisodeCount,
                SelectedScenesPerEpisode,
                styleKey,
                voiceKey,
                SelectedSeriesTone,
                ActualAspectRatio);

            // Đảm bảo các tập có cấu hình cảnh đúng với lựa chọn ban đầu
            foreach (var ep in plan.Episodes)
            {
                if (ep.SuggestedSceneCount <= 0)
                    ep.SuggestedSceneCount = SelectedScenesPerEpisode;
            }

            CurrentSeriesPlan = plan;
            HasSeriesPlan = true;
            BatchStatusText = $"✅ Đã lập dàn ý {plan.Episodes.Count} tập thành công! Bạn có thể tùy biến số cảnh từng tập bên dưới.";
        }
        catch (Exception ex)
        {
            BatchStatusText = "❌ Lỗi khi AI lập kế hoạch Series.";
            MessageBox.Show($"Lỗi AI Series Planning:\n{ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsPlanningSeries = false;
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

                var ws = await _geminiService.GenerateEpisodeScriptAsync(
                    CurrentSeriesPlan,
                    epPlan,
                    total,
                    previousEnding,
                    ActualAspectRatio,
                    SelectedSeriesTone);

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
