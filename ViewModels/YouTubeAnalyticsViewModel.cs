using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VideoAutoWpf.Models;
using VideoAutoWpf.Services;

namespace VideoAutoWpf.ViewModels;

public partial class YouTubeAnalyticsViewModel : ObservableObject
{
    private readonly YouTubeAnalyticsService _ytService;
    private readonly GeminiScriptService _geminiService;

    // ── Tab Selection ──
    [ObservableProperty]
    private int _selectedTab; // 0: Kênh, 1: Video, 2: Đối thủ, 3: Trending

    // ══════════════════════════════════
    // TAB 1: PHÂN TÍCH KÊNH YOUTUBE
    // ══════════════════════════════════
    [ObservableProperty]
    private string _channelInput = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsBusy))]
    private bool _isLoadingChannel;

    [ObservableProperty]
    private YouTubeChannelInfo? _channelInfo;

    [ObservableProperty]
    private bool _hasChannelResult;

    public ObservableCollection<YouTubeVideoInfo> ChannelTopVideos { get; } = new();
    public ObservableCollection<YouTubeVideoInfo> ChannelLatestVideos { get; } = new();

    [ObservableProperty]
    private bool _showTopVideos = true;

    // ══════════════════════════════════
    // TAB 2: PHÂN TÍCH VIDEO CỤ THỂ
    // ══════════════════════════════════
    [ObservableProperty]
    private string _videoInput = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsBusy))]
    private bool _isLoadingVideo;

    [ObservableProperty]
    private YouTubeVideoInfo? _videoInfo;

    [ObservableProperty]
    private bool _hasVideoResult;

    // ══════════════════════════════════
    // TAB 3: SO SÁNH ĐỐI THỦ
    // ══════════════════════════════════
    [ObservableProperty]
    private string _myChannelInput = string.Empty;

    [ObservableProperty]
    private string _competitorChannelInput = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsBusy))]
    private bool _isLoadingComparison;

    [ObservableProperty]
    private ChannelComparisonItem? _myChannelComparison;

    [ObservableProperty]
    private ChannelComparisonItem? _competitorComparison;

    [ObservableProperty]
    private bool _hasComparisonResult;

    // ══════════════════════════════════
    // TAB 4: TRENDING & TÌM KIẾM
    // ══════════════════════════════════
    [ObservableProperty]
    private string _searchKeyword = string.Empty;

    [ObservableProperty]
    private string _regionCode = "VN";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsBusy))]
    private bool _isLoadingSearch;

    // ══════════════════════════════════
    // AI PHÂN TÍCH TITLE & HOOK
    // ══════════════════════════════════
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsBusy))]
    private bool _isLoadingAiAnalysis;

    [ObservableProperty]
    private CompetitorAiAnalysis? _channelAiAnalysis;

    [ObservableProperty]
    private bool _hasChannelAiAnalysis;

    [ObservableProperty]
    private CompetitorAiAnalysis? _videoAiAnalysis;

    [ObservableProperty]
    private bool _hasVideoAiAnalysis;

    // ══════════════════════════════════
    // TAB: TOP KÊNH THEO CHỦ ĐỀ
    // ══════════════════════════════════
    [ObservableProperty]
    private string _topicKeyword = string.Empty;

    [ObservableProperty]
    private string _topicRegionCode = "VN";

    [ObservableProperty]
    private string _topicSortBy = "views"; // views, subs, avg, videos

    public bool IsSortByViews
    {
        get => TopicSortBy == "views";
        set { if (value && TopicSortBy != "views") TopicSortBy = "views"; }
    }

    public bool IsSortBySubs
    {
        get => TopicSortBy == "subs";
        set { if (value && TopicSortBy != "subs") TopicSortBy = "subs"; }
    }

    public bool IsSortByAvg
    {
        get => TopicSortBy == "avg";
        set { if (value && TopicSortBy != "avg") TopicSortBy = "avg"; }
    }

    partial void OnTopicSortByChanged(string value)
    {
        OnPropertyChanged(nameof(IsSortByViews));
        OnPropertyChanged(nameof(IsSortBySubs));
        OnPropertyChanged(nameof(IsSortByAvg));

        if (TopicChannels.Count > 1)
        {
            ReSortTopicChannels();
        }
    }

    private void ReSortTopicChannels()
    {
        var list = TopicChannels.ToList();
        IEnumerable<YouTubeChannelInfo> sorted = TopicSortBy switch
        {
            "subs" => list.OrderByDescending(c => c.SubscriberCount),
            "avg" => list.OrderByDescending(c => c.AvgViewsPerVideo),
            "videos" => list.OrderByDescending(c => c.VideoCount),
            _ => list.OrderByDescending(c => c.ViewCount)
        };

        TopicChannels.Clear();
        int rank = 1;
        foreach (var ch in sorted)
        {
            ch.Rank = rank++;
            TopicChannels.Add(ch);
        }
    }

    [ObservableProperty]
    private int _topicMaxResults = 20;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsBusy))]
    private bool _isLoadingTopicChannels;

    [ObservableProperty]
    private bool _hasTopicChannelsResult;

    [ObservableProperty]
    private string _topicSearchSummary = string.Empty;

    [ObservableProperty]
    private string _totalTopicViewsDisplay = string.Empty;

    public ObservableCollection<YouTubeChannelInfo> TopicChannels { get; } = new();

    public bool IsBusy => IsLoadingChannel || IsLoadingVideo || IsLoadingComparison || IsLoadingSearch || IsLoadingAiAnalysis || IsLoadingTopicChannels;

    public ObservableCollection<YouTubeSearchResult> SearchResults { get; } = new();
    public ObservableCollection<YouTubeVideoInfo> TrendingVideos { get; } = new();

    [ObservableProperty]
    private bool _hasSearchResults;

    [ObservableProperty]
    private bool _hasTrendingResults;

    // ── Status / Error ──
    [ObservableProperty]
    private string _statusMessage = "Sẵn sàng phân tích YouTube";

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    [ObservableProperty]
    private bool _hasError;

    [ObservableProperty]
    private string _geminiApiKey = string.Empty;

    [ObservableProperty]
    private bool _showApiKeyConfig;

    public YouTubeAnalyticsViewModel(YouTubeAnalyticsService ytService, GeminiScriptService geminiService)
    {
        _ytService = ytService;
        _geminiService = geminiService;
        _geminiApiKey = _geminiService.ApiKey ?? string.Empty;
    }

    // ══════════════════════════════════
    //  COMMANDS: TAB 1 - KÊNH
    // ══════════════════════════════════

    [RelayCommand]
    private async Task AnalyzeChannelAsync()
    {
        if (string.IsNullOrWhiteSpace(ChannelInput))
        {
            ShowError("Vui lòng nhập URL kênh, @handle, hoặc tên kênh YouTube.");
            return;
        }

        IsLoadingChannel = true;
        ClearError();
        StatusMessage = "Đang phân tích kênh YouTube...";

        try
        {
            var channelId = await _ytService.ResolveChannelIdAsync(ChannelInput);
            ChannelInfo = await _ytService.GetChannelInfoAsync(channelId);
            HasChannelResult = true;

            StatusMessage = $"Đang tải top video của {ChannelInfo.Title}...";

            // Load top videos
            ChannelTopVideos.Clear();
            var topVids = await _ytService.GetTopVideosAsync(channelId, 15);
            foreach (var v in topVids) ChannelTopVideos.Add(v);

            // Load latest videos
            ChannelLatestVideos.Clear();
            var latestVids = await _ytService.GetLatestVideosAsync(channelId, 15);
            foreach (var v in latestVids) ChannelLatestVideos.Add(v);

            StatusMessage = $"✅ Đã phân tích xong kênh {ChannelInfo.Title} ({ChannelInfo.SubscriberDisplay} subscribers)";
        }
        catch (Exception ex)
        {
            ShowException("Lỗi phân tích kênh", ex);
        }
        finally
        {
            IsLoadingChannel = false;
        }
    }

    [RelayCommand]
    private void ToggleVideoList()
    {
        ShowTopVideos = !ShowTopVideos;
    }

    // ══════════════════════════════════
    //  COMMANDS: TAB 2 - VIDEO
    // ══════════════════════════════════

    [RelayCommand]
    private async Task AnalyzeVideoAsync()
    {
        if (string.IsNullOrWhiteSpace(VideoInput))
        {
            ShowError("Vui lòng nhập URL hoặc ID video YouTube.");
            return;
        }

        IsLoadingVideo = true;
        ClearError();
        StatusMessage = "Đang phân tích video YouTube...";

        try
        {
            VideoInfo = await _ytService.GetVideoInfoAsync(VideoInput);
            HasVideoResult = true;
            StatusMessage = $"✅ Đã phân tích xong video: {VideoInfo.Title}";
        }
        catch (Exception ex)
        {
            ShowException("Lỗi phân tích video", ex);
        }
        finally
        {
            IsLoadingVideo = false;
        }
    }

    // ══════════════════════════════════
    //  COMMANDS: TAB 3 - ĐỐI THỦ
    // ══════════════════════════════════

    [RelayCommand]
    private async Task CompareChannelsAsync()
    {
        if (string.IsNullOrWhiteSpace(MyChannelInput) || string.IsNullOrWhiteSpace(CompetitorChannelInput))
        {
            ShowError("Vui lòng nhập cả kênh của bạn và kênh đối thủ.");
            return;
        }

        IsLoadingComparison = true;
        ClearError();
        StatusMessage = "Đang so sánh hai kênh YouTube...";

        try
        {
            // Phân tích kênh của bạn
            var myChannelId = await _ytService.ResolveChannelIdAsync(MyChannelInput);
            var myInfo = await _ytService.GetChannelInfoAsync(myChannelId);
            var myTopVids = await _ytService.GetTopVideosAsync(myChannelId, 5);

            MyChannelComparison = new ChannelComparisonItem
            {
                Channel = myInfo,
                TopVideos = myTopVids,
                IsMyChannel = true
            };

            StatusMessage = "Đang phân tích kênh đối thủ...";

            // Phân tích kênh đối thủ
            var compId = await _ytService.ResolveChannelIdAsync(CompetitorChannelInput);
            var compInfo = await _ytService.GetChannelInfoAsync(compId);
            var compTopVids = await _ytService.GetTopVideosAsync(compId, 5);

            CompetitorComparison = new ChannelComparisonItem
            {
                Channel = compInfo,
                TopVideos = compTopVids,
                IsMyChannel = false
            };

            HasComparisonResult = true;
            StatusMessage = $"✅ So sánh hoàn tất: {myInfo.Title} vs {compInfo.Title}";
        }
        catch (Exception ex)
        {
            ShowException("Lỗi so sánh kênh", ex);
        }
        finally
        {
            IsLoadingComparison = false;
        }
    }

    // ══════════════════════════════════
    //  COMMANDS: TAB 4 - TRENDING
    // ══════════════════════════════════

    [RelayCommand]
    private async Task SearchKeywordAsync()
    {
        if (string.IsNullOrWhiteSpace(SearchKeyword))
        {
            ShowError("Vui lòng nhập từ khóa tìm kiếm.");
            return;
        }

        IsLoadingSearch = true;
        ClearError();
        StatusMessage = $"Đang tìm kiếm '{SearchKeyword}'...";

        try
        {
            SearchResults.Clear();
            var results = await _ytService.SearchVideosAsync(SearchKeyword, 25, RegionCode);
            foreach (var r in results) SearchResults.Add(r);
            HasSearchResults = SearchResults.Count > 0;
            StatusMessage = $"✅ Tìm thấy {SearchResults.Count} kết quả cho '{SearchKeyword}'";
        }
        catch (Exception ex)
        {
            ShowException("Lỗi tìm kiếm", ex);
        }
        finally
        {
            IsLoadingSearch = false;
        }
    }

    [RelayCommand]
    private async Task LoadTrendingAsync()
    {
        IsLoadingSearch = true;
        ClearError();
        StatusMessage = $"Đang tải video trending ({RegionCode})...";

        try
        {
            TrendingVideos.Clear();
            var results = await _ytService.GetTrendingVideosAsync(RegionCode, 25);
            foreach (var v in results) TrendingVideos.Add(v);
            HasTrendingResults = TrendingVideos.Count > 0;
            StatusMessage = $"✅ Đã tải {TrendingVideos.Count} video trending tại {RegionCode}";
        }
        catch (Exception ex)
        {
            ShowException("Lỗi tải trending", ex);
        }
        finally
        {
            IsLoadingSearch = false;
        }
    }

    // ══════════════════════════════════
    //  COMMANDS: TAB TOP KÊNH THEO CHỦ ĐỀ
    // ══════════════════════════════════

    [RelayCommand]
    private async Task SearchTopChannelsByTopicAsync()
    {
        if (string.IsNullOrWhiteSpace(TopicKeyword))
        {
            ShowError("Vui lòng nhập chủ đề kênh cần tìm (VD: review phim, kể chuyện kinh dị, kiến thức khoa học...)");
            return;
        }

        IsLoadingTopicChannels = true;
        ClearError();
        StatusMessage = $"🔍 Đang tìm kiếm các kênh có lượt view cao nhất cho chủ đề '{TopicKeyword.Trim()}'...";

        try
        {
            TopicChannels.Clear();
            var channels = await _ytService.GetTopChannelsByTopicAsync(
                TopicKeyword.Trim(), 
                TopicMaxResults, 
                string.IsNullOrWhiteSpace(TopicRegionCode) ? null : TopicRegionCode.Trim(), 
                TopicSortBy
            );

            long totalViews = 0;
            foreach (var ch in channels)
            {
                TopicChannels.Add(ch);
                totalViews += ch.ViewCount;
            }

            HasTopicChannelsResult = TopicChannels.Count > 0;
            TotalTopicViewsDisplay = YouTubeChannelInfo.FormatNumber(totalViews);
            TopicSearchSummary = $"Tìm thấy {TopicChannels.Count} kênh hàng đầu về '{TopicKeyword}' — Tổng cộng: {TotalTopicViewsDisplay} lượt xem";

            if (TopicChannels.Count > 0)
            {
                StatusMessage = $"✅ Đã tìm thấy {TopicChannels.Count} kênh có lượt view cao nhất về '{TopicKeyword}' (Tổng {TotalTopicViewsDisplay} lượt xem)!";
            }
            else
            {
                StatusMessage = $"⚠️ Không tìm thấy kênh nào cho chủ đề '{TopicKeyword}'. Vui lòng thử từ khóa khác.";
            }
        }
        catch (Exception ex)
        {
            ShowException("Lỗi tìm kiếm kênh theo chủ đề", ex);
        }
        finally
        {
            IsLoadingTopicChannels = false;
        }
    }

    [RelayCommand]
    private async Task ApplyQuickTopicAsync(string? topic)
    {
        if (string.IsNullOrWhiteSpace(topic)) return;
        TopicKeyword = topic;
        RequestSwitchTab?.Invoke(1);
        await SearchTopChannelsByTopicAsync();
    }

    [RelayCommand]
    private async Task AnalyzeSelectedChannelAsync(YouTubeChannelInfo? channel)
    {
        if (channel == null) return;
        ChannelInput = !string.IsNullOrEmpty(channel.CustomUrl) ? channel.CustomUrl : channel.ChannelId;
        RequestSwitchTab?.Invoke(0);
        await AnalyzeChannelAsync();
    }

    [RelayCommand]
    private void SwitchToTopicChannelsTab()
    {
        RequestSwitchTab?.Invoke(1);
    }

    [RelayCommand]
    private void CreateVideoFromChannel(YouTubeChannelInfo? channel)
    {
        if (channel == null) return;
        var req = BuildRequestFromChannel(channel);
        PromptCreationChoice(req);
    }

    [RelayCommand]
    private void CreateSingleVideoFromChannel(YouTubeChannelInfo? channel)
    {
        if (channel == null) return;
        var req = BuildRequestFromChannel(channel);
        req.TargetMode = VideoCreationTargetMode.SingleEpisode;
        PendingVideoRequest = req;
        RequestCloseWithAction?.Invoke();
    }

    [RelayCommand]
    private void CreateSeriesFromChannel(YouTubeChannelInfo? channel)
    {
        if (channel == null) return;
        var req = BuildRequestFromChannel(channel);
        req.TargetMode = VideoCreationTargetMode.Series;
        PendingVideoRequest = req;
        RequestCloseWithAction?.Invoke();
    }

    public CreateVideoFromAnalyticsRequest BuildRequestFromChannel(YouTubeChannelInfo channel)
    {
        var (style, tone, bgm) = DetectStyleAndTone(channel.Title, channel.Description);
        var topicName = !string.IsNullOrWhiteSpace(TopicKeyword) ? TopicKeyword : channel.Title;
        return new CreateVideoFromAnalyticsRequest
        {
            Title = $"[CHỦ ĐỀ TRIỆU VIEW] Lấy cảm hứng từ {channel.Title}",
            HookOpening = "Bạn có biết bí mật đằng sau những video hàng triệu người xem của chủ đề này là gì không?",
            HookStrategy = "Tập trung giữ chân người xem ngay từ 5s đầu bằng một tình huống tò mò",
            TargetEmotion = "Kích thích tò mò và hứng thú",
            TargetAudience = $"Khán giả quan tâm chủ đề {topicName}",
            SuggestedStyle = style,
            SuggestedTone = tone,
            SuggestedBgm = bgm,
            SourceInfo = channel.Title
        };
    }

    // ══════════════════════════════════
    //  COMMANDS: AI BÓC TÁCH TITLE & HOOK
    // ══════════════════════════════════

    [RelayCommand]
    private async Task AnalyzeChannelAiAsync()
    {
        if (ChannelInfo == null || ChannelTopVideos.Count == 0)
        {
            ShowError("Vui lòng phân tích kênh trước để có dữ liệu video cho AI phân tích.");
            return;
        }

        IsLoadingAiAnalysis = true;
        ClearError();
        StatusMessage = $"🧠 Đang giải mã toàn bộ công thức tiêu đề & retention hook của {ChannelInfo.Title}...";

        try
        {
            // 1. Thử gọi Gemini AI nếu có key hoặc Vertex AI chạy được
            ChannelAiAnalysis = await _geminiService.AnalyzeCompetitorFormulasAsync(
                ChannelInfo.Title,
                ChannelInfo.Description,
                ChannelTopVideos
            );
            HasChannelAiAnalysis = true;
            StatusMessage = $"✅ [Gemini AI] Đã bóc tách thành công {ChannelAiAnalysis.TitleFormulas.Count} công thức tiêu đề & {ChannelAiAnalysis.HookStrategies.Count} chiến lược hook!";
        }
        catch
        {
            // 2. TỰ ĐỘNG CHUYỂN SANG BỘ PHÂN TÍCH THUẬT TOÁN THÔNG MINH (Hoàn toàn MIỄN PHÍ 100%, không cần thẻ Visa hay tài khoản phụ!)
            ChannelAiAnalysis = CompetitorPatternAnalyzer.AnalyzeChannel(
                ChannelInfo.Title,
                ChannelInfo.Description,
                ChannelTopVideos
            );
            HasChannelAiAnalysis = true;
            StatusMessage = $"✅ Đã bóc tách thành công công thức triệu view & retention hook (Hoàn toàn miễn phí 100%)!";
        }
        finally
        {
            IsLoadingAiAnalysis = false;
        }
    }

    [RelayCommand]
    private async Task AnalyzeVideoAiAsync()
    {
        if (VideoInfo == null)
        {
            ShowError("Vui lòng phân tích video trước.");
            return;
        }

        IsLoadingAiAnalysis = true;
        ClearError();
        StatusMessage = $"🧠 Đang phân tích tiêu đề & hook của video: {VideoInfo.Title}...";

        try
        {
            VideoAiAnalysis = await _geminiService.AnalyzeSingleVideoAsync(VideoInfo);
            HasVideoAiAnalysis = true;
            StatusMessage = "✅ [Gemini AI] Đã bóc tách công thức tiêu đề & hook của video thành công!";
        }
        catch
        {
            // TỰ ĐỘNG DÙNG BỘ PHÂN TÍCH THUẬT TOÁN MIỄN PHÍ
            VideoAiAnalysis = CompetitorPatternAnalyzer.AnalyzeSingleVideo(VideoInfo);
            HasVideoAiAnalysis = true;
            StatusMessage = "✅ Đã bóc tách công thức tiêu đề & hook thành công (Miễn phí 100%)!";
        }
        finally
        {
            IsLoadingAiAnalysis = false;
        }
    }

    // ── Cầu Nối Tạo Video Trực Tiếp (1 Tập hoặc Series Nhiều Tập) ──
    public CreateVideoFromAnalyticsRequest? PendingVideoRequest { get; set; }
    public Action? RequestCloseWithAction { get; set; }
    public Action<int>? RequestSwitchTab { get; set; }

    [ObservableProperty]
    private bool _isCreationChoicePopupOpen;

    [ObservableProperty]
    private CreateVideoFromAnalyticsRequest? _activeCandidateRequest;

    [RelayCommand]
    private void UseSuggestedIdea(SuggestedVideoIdea? idea)
    {
        if (idea == null) return;
        try
        {
            Clipboard.SetText($"{idea.Title}\n(Hook mở đầu: {idea.HookOpening})");
            StatusMessage = $"📋 Đã copy tiêu đề & hook '{idea.Title}' vào Clipboard! Bạn có thể dán vào công cụ Soạn Kịch Bản.";
        }
        catch { }
    }

    private void PromptCreationChoice(CreateVideoFromAnalyticsRequest req)
    {
        ActiveCandidateRequest = req;
        IsCreationChoicePopupOpen = true;
    }

    [RelayCommand]
    private void CreateVideoFromIdea(SuggestedVideoIdea? idea)
    {
        if (idea == null) return;
        PromptCreationChoice(BuildRequestFromIdea(idea));
    }

    [RelayCommand]
    private void CreateVideoFromVideo(YouTubeVideoInfo? video)
    {
        if (video == null) return;
        PromptCreationChoice(BuildRequestFromVideo(video));
    }

    [RelayCommand]
    private void CreateVideoFromCurrentVideo()
    {
        if (VideoInfo != null)
        {
            PromptCreationChoice(BuildRequestFromVideo(VideoInfo));
        }
    }

    [RelayCommand]
    private void CreateVideoFromSearchResult(YouTubeSearchResult? item)
    {
        if (item == null) return;
        PromptCreationChoice(BuildRequestFromSearchResult(item));
    }

    // ── Xác nhận lựa chọn từ Popup: Tạo 1 Tập hoặc Tạo Series ──
    [RelayCommand]
    private void ConfirmCreateSingleVideo()
    {
        if (ActiveCandidateRequest == null) return;
        ActiveCandidateRequest.TargetMode = VideoCreationTargetMode.SingleEpisode;
        PendingVideoRequest = ActiveCandidateRequest;
        IsCreationChoicePopupOpen = false;
        RequestCloseWithAction?.Invoke();
    }

    [RelayCommand]
    private void ConfirmCreateSeriesVideo()
    {
        if (ActiveCandidateRequest == null) return;
        ActiveCandidateRequest.TargetMode = VideoCreationTargetMode.Series;
        PendingVideoRequest = ActiveCandidateRequest;
        IsCreationChoicePopupOpen = false;
        RequestCloseWithAction?.Invoke();
    }

    [RelayCommand]
    private void CancelCreationChoicePopup()
    {
        IsCreationChoicePopupOpen = false;
        ActiveCandidateRequest = null;
    }

    // ── Các Command Lựa Chọn Nhanh (Ví dụ từ ContextMenu) ──
    [RelayCommand]
    private void CreateSingleVideoFromIdea(SuggestedVideoIdea? idea)
    {
        if (idea == null) return;
        var req = BuildRequestFromIdea(idea);
        req.TargetMode = VideoCreationTargetMode.SingleEpisode;
        PendingVideoRequest = req;
        RequestCloseWithAction?.Invoke();
    }

    [RelayCommand]
    private void CreateSeriesFromIdea(SuggestedVideoIdea? idea)
    {
        if (idea == null) return;
        var req = BuildRequestFromIdea(idea);
        req.TargetMode = VideoCreationTargetMode.Series;
        PendingVideoRequest = req;
        RequestCloseWithAction?.Invoke();
    }

    [RelayCommand]
    private void CreateSingleVideoFromVideo(YouTubeVideoInfo? video)
    {
        if (video == null) return;
        var req = BuildRequestFromVideo(video);
        req.TargetMode = VideoCreationTargetMode.SingleEpisode;
        PendingVideoRequest = req;
        RequestCloseWithAction?.Invoke();
    }

    [RelayCommand]
    private void CreateSeriesFromVideo(YouTubeVideoInfo? video)
    {
        if (video == null) return;
        var req = BuildRequestFromVideo(video);
        req.TargetMode = VideoCreationTargetMode.Series;
        PendingVideoRequest = req;
        RequestCloseWithAction?.Invoke();
    }

    [RelayCommand]
    private void CreateSingleVideoFromCurrentVideo()
    {
        if (VideoInfo != null)
        {
            CreateSingleVideoFromVideo(VideoInfo);
        }
    }

    [RelayCommand]
    private void CreateSeriesFromCurrentVideo()
    {
        if (VideoInfo != null)
        {
            CreateSeriesFromVideo(VideoInfo);
        }
    }

    [RelayCommand]
    private void CreateSingleVideoFromSearchResult(YouTubeSearchResult? item)
    {
        if (item == null) return;
        var req = BuildRequestFromSearchResult(item);
        req.TargetMode = VideoCreationTargetMode.SingleEpisode;
        PendingVideoRequest = req;
        RequestCloseWithAction?.Invoke();
    }

    [RelayCommand]
    private void CreateSeriesFromSearchResult(YouTubeSearchResult? item)
    {
        if (item == null) return;
        var req = BuildRequestFromSearchResult(item);
        req.TargetMode = VideoCreationTargetMode.Series;
        PendingVideoRequest = req;
        RequestCloseWithAction?.Invoke();
    }

    public CreateVideoFromAnalyticsRequest BuildRequestFromIdea(SuggestedVideoIdea idea)
    {
        var source = ChannelInfo?.Title ?? VideoInfo?.ChannelTitle ?? "YouTube Analytics";
        var (style, tone, bgm) = DetectStyleAndTone(idea.Title, source);

        var hookDesc = ChannelAiAnalysis?.HookStrategies?.FirstOrDefault()?.Description
            ?? VideoAiAnalysis?.HookStrategies?.FirstOrDefault()?.Description
            ?? "Tập trung giữ chân 0-15s đầu bằng tình huống kịch tính hoặc câu hỏi bất ngờ";

        var audience = ChannelAiAnalysis?.CoreAudience ?? VideoAiAnalysis?.CoreAudience ?? string.Empty;

        return new CreateVideoFromAnalyticsRequest
        {
            Title = idea.Title,
            HookOpening = idea.HookOpening,
            HookStrategy = hookDesc,
            TargetEmotion = idea.TargetEmotion,
            TargetAudience = audience,
            SuggestedStyle = style,
            SuggestedTone = tone,
            SuggestedBgm = bgm,
            SourceInfo = source,
            VideoId = VideoInfo?.VideoId ?? string.Empty,
            VideoDuration = VideoInfo?.Duration ?? TimeSpan.Zero,
            Description = VideoInfo?.Description ?? string.Empty,
            Tags = VideoInfo?.Tags ?? string.Empty
        };
    }

    public CreateVideoFromAnalyticsRequest BuildRequestFromVideo(YouTubeVideoInfo video)
    {
        var (style, tone, bgm) = DetectStyleAndTone(video.Title, video.ChannelTitle);
        var hookDesc = "Đẩy thẳng người xem vào mâu thuẫn chính hoặc sự kiện bất thường nhất trong 5-10 giây đầu";

        return new CreateVideoFromAnalyticsRequest
        {
            VideoId = video.VideoId,
            VideoDuration = video.Duration,
            Description = video.Description,
            Title = $"[BÍ MẬT CHƯA KỂ] Sự Thật Đằng Sau: {video.Title}",
            HookOpening = "Bạn có biết đằng sau câu chuyện này còn một chi tiết kinh ngạc mà rất ít người nhận ra?",
            HookStrategy = hookDesc,
            TargetEmotion = "Tò mò và kích thích trí tò mò",
            TargetAudience = "Khán giả quan tâm các tình tiết bất ngờ, giải mã bí ẩn",
            SuggestedStyle = style,
            SuggestedTone = tone,
            SuggestedBgm = bgm,
            SourceInfo = video.ChannelTitle,
            Tags = video.Tags
        };
    }

    public CreateVideoFromAnalyticsRequest BuildRequestFromSearchResult(YouTubeSearchResult item)
    {
        var (style, tone, bgm) = DetectStyleAndTone(item.Title, item.ChannelTitle);
        return new CreateVideoFromAnalyticsRequest
        {
            VideoId = item.VideoId,
            Title = $"[XU HƯỚNG MỚI] {item.Title}",
            HookOpening = "Điều gì đang khiến hàng triệu người bất ngờ về chủ đề này?",
            HookStrategy = "Tập trung vào hiệu ứng xu hướng (Trending Hook) ngay từ 5s đầu",
            TargetEmotion = "Tò mò, bất ngờ",
            TargetAudience = "Khán giả theo dõi các tin tức và xu hướng mới nhất",
            SuggestedStyle = style,
            SuggestedTone = tone,
            SuggestedBgm = bgm,
            SourceInfo = item.ChannelTitle
        };
    }

    private static (string style, string tone, string bgm) DetectStyleAndTone(string title, string channel)
    {
        var text = $"{title} {channel}".ToLowerInvariant();

        if (text.Contains("ma") || text.Contains("kinh dị") || text.Contains("kỳ án") || text.Contains("vụ án") || 
            text.Contains("mất tích") || text.Contains("rùng rợn") || text.Contains("quỷ") || text.Contains("tâm linh") || text.Contains("đêm"))
        {
            return ("dark-anime", "Rùng rợn, bí ẩn và ly kỳ", "horror");
        }

        if (text.Contains("lịch sử") || text.Contains("chiến tranh") || text.Contains("vua") || text.Contains("tướng") || 
            text.Contains("triều đại") || text.Contains("huyền thoại") || text.Contains("dân tộc"))
        {
            return ("oil-painting", "Hào hùng, trang nghiêm và truyền cảm", "epic");
        }

        if (text.Contains("khoa học") || text.Contains("vũ trụ") || text.Contains("công nghệ") || text.Contains("tương lai") || text.Contains("ai"))
        {
            return ("cyberpunk", "Cuốn hút, hiện đại và kích thích tư duy", "dramatic");
        }

        if (text.Contains("bài học") || text.Contains("triết lý") || text.Contains("cuộc sống") || text.Contains("thành công") || text.Contains("tâm lý"))
        {
            return ("cinematic", "Sâu lắng, ấm áp và truyền cảm hứng", "chill");
        }

        if (text.Contains("hài") || text.Contains("vui") || text.Contains("thú vị") || text.Contains("giải trí"))
        {
            return ("3d-pixar", "Vui vẻ, hóm hỉnh và năng động", "upbeat");
        }

        return ("cinematic", "Kịch tính, lôi cuốn và hồi hộp", "dramatic");
    }


    // ── Helpers ──

    [RelayCommand]
    private void OpenVideoUrl(string? url)
    {
        if (string.IsNullOrEmpty(url)) return;
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch { }
    }

    [RelayCommand]
    private void CopyToClipboard(string? text)
    {
        if (string.IsNullOrEmpty(text)) return;
        try
        {
            Clipboard.SetText(text);
            StatusMessage = "✅ Đã sao chép vào Clipboard!";
        }
        catch { }
    }

    [RelayCommand]
    private void SaveGeminiApiKey()
    {
        if (string.IsNullOrWhiteSpace(GeminiApiKey))
        {
            ShowError("Vui lòng nhập API Key.");
            return;
        }

        _geminiService.ApiKey = GeminiApiKey.Trim();
        GeminiScriptService.SaveApiKey(GeminiApiKey.Trim());
        ShowApiKeyConfig = false;
        ClearError();
        StatusMessage = "✅ Đã lưu Gemini API Key! Bạn có thể bấm 'Bóc Tách Bằng AI' ngay bây giờ.";
    }

    [RelayCommand]
    private void ToggleApiKeyConfig()
    {
        ShowApiKeyConfig = !ShowApiKeyConfig;
    }

    [RelayCommand]
    private void OpenAiStudioKeyPage()
    {
        try
        {
            Process.Start(new ProcessStartInfo("https://aistudio.google.com/app/apikey") { UseShellExecute = true });
        }
        catch { }
    }

    private void ShowException(string prefix, Exception ex)
    {
        string detail;
        if (ex is Google.GoogleApiException gex)
        {
            if (gex.Error?.Message != null && (gex.Error.Message.Contains("has not been used in project") || gex.Error.Message.Contains("disabled")))
            {
                detail = "Chưa kích hoạt YouTube Data API v3 trên Google Cloud Console. Hãy vào Google Cloud bật API rồi thử lại.";
            }
            else if (gex.Error?.Message != null && gex.Error.Message.Contains("insufficient authentication scopes"))
            {
                detail = "Chưa cấp quyền YouTube API cho tài khoản Google.";
            }
            else
            {
                detail = gex.Error?.Message ?? gex.Message;
            }
        }
        else
        {
            if (ex.Message.Contains("BILLING_DISABLED") || ex.Message.Contains("billing to be enabled") || ex.Message.Contains("chưa bật Billing"))
            {
                detail = "Dự án Google Cloud Vertex AI chưa bật Billing. Bạn hãy nhập Gemini API Key miễn phí (Google AI Studio) ở thanh bên dưới để dùng hoàn toàn miễn phí!";
                ShowApiKeyConfig = true;
            }
            else
            {
                detail = ex.Message;
            }
        }

        ShowError($"{prefix}: {detail}");
    }

    private void ShowError(string message)
    {
        ErrorMessage = message;
        HasError = true;
        StatusMessage = $"❌ {message}";
    }

    private void ClearError()
    {
        ErrorMessage = string.Empty;
        HasError = false;
    }
}
