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

    public List<int> EpisodeCountOptions { get; } = new() { 2, 3, 4, 5, 6, 7, 8, 10 };

    [ObservableProperty]
    private int _selectedEpisodeCount = 3;

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

        IsPlanningSeries = true;
        BatchStatusText = "⏳ AI đang phân tích cốt truyện và lập dàn ý các tập...";

        try
        {
            var styleKey = SelectedStyleCard?.Key ?? "dark-anime";
            var voiceKey = SelectedVoiceCard?.Key ?? "vi-VN-Wavenet-B";

            var plan = await _geminiService.GenerateSeriesPlanAsync(
                TopicOrStory.Trim(),
                SelectedEpisodeCount,
                styleKey,
                voiceKey);

            CurrentSeriesPlan = plan;
            HasSeriesPlan = true;
            BatchStatusText = $"✅ Đã lập dàn ý {plan.Episodes.Count} tập thành công! Xem lại chi tiết bên dưới.";
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

                BatchProgress = (i / (double)total) * 100;
                BatchStatusText = $"⏳ Đang viết kịch bản chi tiết cho Tập {epNum}/{total}: '{epPlan.EpisodeTitle}'...";

                var ws = await _geminiService.GenerateEpisodeScriptAsync(
                    CurrentSeriesPlan,
                    epPlan,
                    total,
                    previousEnding);

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

    [RelayCommand]
    private void BackToInput()
    {
        HasSeriesPlan = false;
        CurrentSeriesPlan = null;
        BatchStatusText = string.Empty;
    }
}
