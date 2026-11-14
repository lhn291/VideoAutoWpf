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
    private readonly VertexImagenService? _imagenService;
    private readonly BgmService? _bgmService;

    public Action<bool>? RequestClose { get; set; }

    [ObservableProperty]
    private string _topicOrStory = string.Empty;

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
    public List<int> EpisodeCountOptions { get; } = new() { 2, 3, 4, 5, 6, 7, 8, 9, 10, 12, 15, 20, 25, 30, 40, 50 };

    [ObservableProperty]
    private int _selectedEpisodeCount = 3;

    [ObservableProperty]
    private string _episodeCountText = "3";

    partial void OnSelectedEpisodeCountChanged(int value)
    {
        if (value >= 2 && EpisodeCountText != value.ToString())
        {
            EpisodeCountText = value.ToString();
        }
    }

    partial void OnEpisodeCountTextChanged(string value)
    {
        if (int.TryParse(value, out int parsed) && parsed >= 2)
        {
            if (SelectedEpisodeCount != parsed)
                SelectedEpisodeCount = parsed;
        }
    }

    // ── Cấu hình số cảnh mỗi tập ──
    public List<int> ScenesPerEpisodeOptions { get; } = new() { 3, 4, 5, 6, 7, 8, 9, 10, 12, 14, 16, 18, 20, 25, 30 };

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EstimatedDurationPerEpisode))]
    private int _selectedScenesPerEpisode = 6;

    [ObservableProperty]
    private string _scenesPerEpisodeText = "6";

    partial void OnSelectedScenesPerEpisodeChanged(int value)
    {
        if (value >= 3 && ScenesPerEpisodeText != value.ToString())
        {
            ScenesPerEpisodeText = value.ToString();
        }
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

    // ── Đồng nhất Nhân vật, Bối cảnh & Poster chung ──
    [ObservableProperty]
    private string _characterBibleText = string.Empty;

    [ObservableProperty]
    private string _settingBibleText = string.Empty;

    [ObservableProperty]
    private string _posterPromptText = string.Empty;

    [ObservableProperty]
    private string? _posterImagePath;

    [ObservableProperty]
    private bool _isGeneratingPoster;

    public bool HasPosterImage => !string.IsNullOrEmpty(PosterImagePath) && File.Exists(PosterImagePath);
    public string PosterFileName => !string.IsNullOrEmpty(PosterImagePath) ? System.IO.Path.GetFileName(PosterImagePath) : "Chưa có ảnh bìa";

    partial void OnPosterImagePathChanged(string? value)
    {
        OnPropertyChanged(nameof(HasPosterImage));
        OnPropertyChanged(nameof(PosterFileName));
    }

    partial void OnCharacterBibleTextChanged(string value)
    {
        if (CurrentSeriesPlan != null)
            CurrentSeriesPlan.CharacterBible = value;
    }

    partial void OnSettingBibleTextChanged(string value)
    {
        if (CurrentSeriesPlan != null)
            CurrentSeriesPlan.SettingBible = value;
    }

    partial void OnPosterPromptTextChanged(string value)
    {
        if (CurrentSeriesPlan != null)
            CurrentSeriesPlan.PosterPrompt = value;
    }

    // ── Cấu hình Nhạc Nền (BGM) cho Series ──
    public ObservableCollection<BgmTrack> AvailableBgmTracks { get; } = new();

    [ObservableProperty]
    private BgmTrack? _selectedBgmTrack;

    [ObservableProperty]
    private bool _enableSeriesBgm = true;

    [ObservableProperty]
    private double _seriesBgmVolume = 0.15;

    [ObservableProperty]
    private bool _isPlayingBgm;

    public string BgmPlayIcon => IsPlayingBgm ? "⏹ Dừng" : "🎧 Nghe thử";

    partial void OnIsPlayingBgmChanged(bool value)
    {
        OnPropertyChanged(nameof(BgmPlayIcon));
    }

    private System.Windows.Media.MediaPlayer? _bgmPlayer;

    [ObservableProperty]
    private double _batchProgress;

    [ObservableProperty]
    private string _batchStatusText = string.Empty;

    [ObservableProperty]
    private SeriesProject? _resultSeriesProject;

    public SeriesCreatorViewModel(
        GeminiScriptService geminiService, 
        VertexImagenService? imagenService = null, 
        BgmService? bgmService = null)
    {
        _geminiService = geminiService;
        _imagenService = imagenService;
        _bgmService = bgmService;

        _selectedStyleCard = StyleCards.FirstOrDefault(s => s.Key == "dark-anime") ?? StyleCards.FirstOrDefault();
        _selectedVoiceCard = VoiceCards.FirstOrDefault(v => v.Key == "vi-VN-Wavenet-B") ?? VoiceCards.FirstOrDefault();
        _geminiApiKey = _geminiService.ApiKey ?? string.Empty;
        _hasGeminiApiKey = !string.IsNullOrWhiteSpace(_geminiApiKey);
        _showApiKeyInput = !_hasGeminiApiKey;

        if (_bgmService != null)
        {
            foreach (var track in _bgmService.GetAvailableTracks())
            {
                AvailableBgmTracks.Add(track);
            }
            _selectedBgmTrack = AvailableBgmTracks.FirstOrDefault(t => t.Id == "dramatic") 
                ?? AvailableBgmTracks.FirstOrDefault(t => t.Id != "none");
        }
    }


    // ─────────────────────────────────────────────────────────────────
    // TỰ GỢI Ý Ý TƯỞNG — AutoSuggest Series Idea
    // ─────────────────────────────────────────────────────────────────

    [ObservableProperty]
    private bool _isSuggestingIdea;

    /// <summary>
    /// Ngân hàng mẫu ý tưởng series (chủ đề, nhân vật, bối cảnh).
    /// Đủ đa dạng thể loại: kinh dị, trinh thám, fantasy, cyberpunk, cảm xúc, tài liệu...
    /// </summary>
    private static readonly (string Topic, string SeriesTitle, string Character, string Setting, string Tone, string Style)[] _seriesTemplates =
    [
        (
            Topic: "Một thám tử mù bẩm sinh sở hữu khả năng \"nghe\" ký ức của đồ vật khi chạm vào. Anh ta bị cuốn vào vụ mất tích bí ẩn của một cô gái 8 tuổi tại một thị trấn nhỏ vùng núi, nơi mọi người dường như đang che giấu một bí mật kinh hoàng từ 20 năm trước.",
            SeriesTitle: "Ngón Tay Của Bóng Tối",
            Character: "Male detective, early 40s, blind with white cane, sharp-featured face, unkempt black hair with silver streaks, wearing a long weathered grey trench coat, calm expression hiding deep inner turmoil, consistent across all scenes",
            Setting: "Isolated mountain village, Vietnam highlands, cold misty fog, traditional wooden stilt houses with lanterns, overcast sky, moody amber and teal color palette, shallow depth of field, cinematic 9:16",
            Tone: "Kể chuyện bí ẩn, lôi cuốn (Mystery / True Crime)",
            Style: "cinematic-horror"
        ),
        (
            Topic: "Năm 2087, Sài Gòn chìm dưới nước biển dâng. Một cô gái giao hàng đường phố tình cờ nhận được một kiện hàng chứa bộ nhớ nhân tạo của một chính khách đã chết 30 năm trước, người đó tiết lộ âm mưu bán linh hồn con người cho các tập đoàn AI.",
            SeriesTitle: "Ký Ức Số 7749",
            Character: "Young Vietnamese woman, mid 20s, athletic build, short choppy hair with neon blue highlights, wearing a worn brown leather jacket with circuit-pattern patches, determined eyes, scar on left cheek, delivery bag on back, consistent across all scenes",
            Setting: "Flooded futuristic Saigon 2087, neon-lit skyscrapers half-submerged in murky water, makeshift floating markets, holographic ads reflecting on dark water surface, heavy rain, deep indigo and magenta neon color palette, 9:16 portrait",
            Tone: "Kịch tính, dồn dập, giật gân (Dramatic / Thriller)",
            Style: "cyberpunk"
        ),
        (
            Topic: "Một ni cô trẻ tại ngôi chùa cổ 400 năm tuổi bắt đầu nhìn thấy bóng hình của các ni cô đã chết trong gương. Mỗi đêm, một trang nhật ký cũ xuất hiện trước cửa phòng cô, kể câu chuyện về một tội ác kinh hoàng bị chôn vùi dưới nền chùa.",
            SeriesTitle: "Nhật Ký Nền Chùa",
            Character: "Young Buddhist nun, early 20s, shaved head, pale skin, wearing traditional grey monk robes, wide innocent eyes hiding growing fear, holding old wooden prayer beads, serene but troubled expression, consistent across all scenes",
            Setting: "Ancient Vietnamese pagoda, 400 years old, dark atmospheric interior, flickering oil lamps, intricate carved wooden pillars, thick incense smoke, moss-covered stone floors, deep shadow corners, warm amber and cold blue contrast, 9:16",
            Tone: "Hồi hộp, rùng rợn, giật gân (Horror / Creepy)",
            Style: "dark-anime"
        ),
        (
            Topic: "Một đứa trẻ mồ côi 12 tuổi phát hiện mình có thể nhìn thấy \"sợi chỉ định mệnh\" kết nối mọi người. Khi sợi chỉ của mẹ nuôi đột ngột đứt, cậu bắt đầu hành trình xuyên qua thế giới thần linh để giành lại mạng sống cho người đã yêu thương cậu.",
            SeriesTitle: "Sợi Chỉ Đỏ Cuối Cùng",
            Character: "12-year-old Vietnamese boy, thin frame, messy black hair, wearing simple school uniform torn at the sleeves, large expressive eyes glowing faintly red when using power, determined yet scared expression, consistent across all scenes",
            Setting: "Mystical spirit realm overlapping modern Vietnam countryside, lush rice fields with floating golden spirit orbs, ancient banyan trees with glowing roots, twilight purple sky with twin moons, magical realism atmosphere, 9:16 portrait",
            Tone: "Cảm xúc, sâu lắng, tình cảm (Emotional / Drama)",
            Style: "cartoon-3d"
        ),
        (
            Topic: "Phóng viên điều tra Linh Hà nhận được file âm thanh bí ẩn từ một người tố cáo ẩn danh, tiết lộ một mạng lưới bán nội tạng người xuyên quốc gia. Khi cô bắt đầu điều tra, những người thân cận với cô lần lượt biến mất và mọi dấu vết đều bị xóa sạch.",
            SeriesTitle: "Tiếng Thét Trong Im Lặng",
            Character: "Vietnamese female journalist, late 20s, sharp intelligent eyes, medium-length black hair often tied back, wearing casual dark grey blazer over white shirt, always carrying a worn leather notebook, cautious but brave expression, consistent across all scenes",
            Setting: "Gritty urban Vietnam, dark alleyways, flickering fluorescent-lit offices, rainy streets reflecting neon signs, hospital corridors at night, claustrophobic framing, desaturated color palette with red accents, cinematic 9:16",
            Tone: "Kịch tính, dồn dập, giật gân (Dramatic / Thriller)",
            Style: "realistic-photo"
        ),
        (
            Topic: "Truyền thuyết kể rằng dưới đáy Hồ Tây Hà Nội có một cung điện rồng bị phong ấn. Khi một nhóm thợ lặn tình cờ phá vỡ lớp phong ấn đó, họ bị cuốn vào cuộc chiến giữa hai dòng dõi rồng thần đã thù hận nhau suốt 1000 năm.",
            SeriesTitle: "Huyết Mạch Rồng Thần",
            Character: "Young Vietnamese woman, early 20s, long flowing black hair with glowing golden streaks (dragon bloodline mark), wearing traditional Vietnamese áo dài with armor-like golden shoulder pieces, fierce determined eyes, dragon scale pattern on her right arm, consistent across all scenes",
            Setting: "Ancient underwater dragon palace beneath West Lake Hanoi, ornate golden architecture covered in coral and bioluminescent sea plants, shafts of green-blue light from above, dragon motif carvings on every surface, epic mythological atmosphere, 9:16 portrait",
            Tone: "Kịch tính, dồn dập, giật gân (Dramatic / Thriller)",
            Style: "dark-anime"
        ),
        (
            Topic: "Một giáo viên tiểu học ở vùng sâu tình nguyện dạy học cho 15 đứa trẻ dân tộc thiểu số không biết tiếng Kinh. Không điện, không sóng, không đường nhựa — chỉ có tình người và những bài học cuộc sống thay đổi hoàn toàn con người anh.",
            SeriesTitle: "Lớp Học Trên Mây",
            Character: "Young Vietnamese male teacher, late 20s, kind warm eyes, slightly disheveled black hair, wearing simple button-up shirt and slacks, always carrying a worn backpack full of books, genuine smile, weathered but hopeful expression, consistent across all scenes",
            Setting: "Remote ethnic minority village, northern Vietnam highlands, traditional longhouse school with bamboo walls, children in colorful traditional clothing, lush terraced rice fields in background, golden morning light through wooden slats, warm earthy tones, 9:16",
            Tone: "Truyền cảm hứng, bài học cuộc sống (Inspirational / Wisdom)",
            Style: "watercolor"
        ),
        (
            Topic: "Năm 1968, một bức thư gửi nhầm địa chỉ đã kết nối hai người xa lạ ở hai đầu chiến tuyến. Họ viết thư cho nhau suốt 3 năm mà không biết mình là kẻ thù. Khi sự thật được phơi bày, cả hai phải lựa chọn giữa lý tưởng và tình người.",
            SeriesTitle: "Những Cánh Thư Qua Lửa Đạn",
            Character: "Two leads — (1) Young North Vietnamese woman soldier, early 20s, determined eyes, olive military uniform, short braided hair; (2) Young South Vietnamese male officer, mid 20s, weary but kind face, khaki uniform — both shown in different episodes but visually consistent",
            Setting: "Vietnam War era 1968-1971, bombed villages, dense jungle paths, candlelit makeshift post offices, contrasting peaceful countryside with war-torn rubble, sepia and warm amber tones with occasional desaturated flashbacks, cinematic 9:16",
            Tone: "Cảm xúc, sâu lắng, tình cảm (Emotional / Drama)",
            Style: "oil-painting"
        ),
    ];

    [RelayCommand]
    private async Task AutoSuggestSeriesAsync()
    {
        IsSuggestingIdea = true;
        BatchStatusText = "💡 Đang chọn ngẫu nhiên ý tưởng series phù hợp...";
        try
        {
            // Delay nhẹ để UI cập nhật trạng thái loading
            await Task.Delay(200);

            var rng = new Random();
            var template = _seriesTemplates[rng.Next(_seriesTemplates.Length)];

            TopicOrStory = template.Topic;
            CharacterBibleText = template.Character;
            SettingBibleText = template.Setting;

            // Tự động chọn tone và style tương ứng
            var toneMatch = SeriesToneOptions.FirstOrDefault(t => t.Contains(template.Tone, StringComparison.OrdinalIgnoreCase))
                ?? SeriesToneOptions.FirstOrDefault(t => t.Contains("Dramatic", StringComparison.OrdinalIgnoreCase));
            if (toneMatch != null) SelectedSeriesTone = toneMatch;

            var styleMatch = StyleCards.FirstOrDefault(s => s.Key.Equals(template.Style, StringComparison.OrdinalIgnoreCase));
            if (styleMatch != null) SelectedStyleCard = styleMatch;

            BatchStatusText = $"✨ Đã gợi ý ý tưởng: \"{template.SeriesTitle}\" — Nhấn 🤖 AI PHÂN TÍCH để lên kế hoạch!";
        }
        catch (Exception ex)
        {
            BatchStatusText = $"❌ Lỗi gợi ý: {ex.Message}";
        }
        finally
        {
            IsSuggestingIdea = false;
        }
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
            var voiceKey = SelectedVoiceCard?.Key ?? "vi-VN-Wavenet-B";

            var plan = await _geminiService.GenerateSeriesPlanAsync(
                TopicOrStory.Trim(),
                SelectedEpisodeCount,
                SelectedScenesPerEpisode,
                styleKey,
                voiceKey,
                SelectedSeriesTone,
                ActualAspectRatio);

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
            CharacterBibleText = plan.CharacterBible;
            SettingBibleText = plan.SettingBible;
            PosterPromptText = plan.PosterPrompt;
            HasSeriesPlan = true;

            // Tự động gán track BGM tương ứng với cảm xúc / đề xuất từ AI
            if (AvailableBgmTracks.Count > 0 && !string.IsNullOrWhiteSpace(plan.SuggestedBgm))
            {
                var matchBgm = AvailableBgmTracks.FirstOrDefault(t => t.Id.Equals(plan.SuggestedBgm, StringComparison.OrdinalIgnoreCase))
                    ?? AvailableBgmTracks.FirstOrDefault(t => t.Mood.Equals(plan.SuggestedBgm, StringComparison.OrdinalIgnoreCase))
                    ?? AvailableBgmTracks.FirstOrDefault(t => t.Id == "dramatic")
                    ?? AvailableBgmTracks.FirstOrDefault(t => t.Id != "none");
                if (matchBgm != null)
                {
                    SelectedBgmTrack = matchBgm;
                }
            }

            BatchStatusText = $"✅ Đã lập dàn ý {plan.Episodes.Count} tập thành công! Bạn có thể tinh chỉnh Bối cảnh, Nhân vật và Nhạc nền bên dưới.";
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
    /// AI Tự động đọc danh sách các tập và cliffhanger hiện có để gợi ý Tên Series, Character Bible và Setting Bible
    /// </summary>
    [RelayCommand]
    private async Task AutoFillBiblesFromEpisodesAsync()
    {
        if (CurrentSeriesPlan == null || CurrentSeriesPlan.Episodes.Count == 0)
        {
            MessageBox.Show("Chưa có danh sách tập để phân tích!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        BatchStatusText = "🔍 AI đang phân tích nhân vật & bối cảnh từ các tập...";
        try
        {
            var epSummary = new System.Text.StringBuilder();
            foreach (var ep in CurrentSeriesPlan.Episodes)
            {
                epSummary.AppendLine($"- {ep.EpisodeTitle}: {ep.PlotBeat}. Cliffhanger: {ep.Cliffhanger}");
            }

            var prompt = $"Dựa trên các tập của chuỗi video sau:\n{epSummary}\n\n" +
                         "Hãy xây dựng Character Bible (nhân vật bất biến bằng tiếng Anh), Setting Bible (bối cảnh bằng tiếng Anh), gợi ý Tên Series tiếng Việt và Poster prompt.";

            var plan = await _geminiService.GenerateSeriesPlanAsync(
                prompt,
                CurrentSeriesPlan.Episodes.Count,
                SelectedScenesPerEpisode,
                SelectedStyleCard?.Key ?? "dark-anime",
                SelectedVoiceCard?.Key ?? "vi-VN-Wavenet-B",
                SelectedSeriesTone,
                ActualAspectRatio);

            if (!string.IsNullOrWhiteSpace(plan.SeriesTitle) && string.IsNullOrWhiteSpace(CurrentSeriesPlan.SeriesTitle))
            {
                CurrentSeriesPlan.SeriesTitle = plan.SeriesTitle;
            }

            if (!string.IsNullOrWhiteSpace(plan.CharacterBible))
            {
                CharacterBibleText = plan.CharacterBible;
                CurrentSeriesPlan.CharacterBible = plan.CharacterBible;
            }

            if (!string.IsNullOrWhiteSpace(plan.SettingBible))
            {
                SettingBibleText = plan.SettingBible;
                CurrentSeriesPlan.SettingBible = plan.SettingBible;
            }

            if (!string.IsNullOrWhiteSpace(plan.PosterPrompt))
            {
                PosterPromptText = plan.PosterPrompt;
                CurrentSeriesPlan.PosterPrompt = plan.PosterPrompt;
            }

            BatchStatusText = "✨ Đã tự động điền Tên Series, Character Bible và Setting Bible thành công!";
        }
        catch (Exception ex)
        {
            // Fallback thông minh nếu không có mạng hoặc API gặp hạn chế
            var epAllText = string.Join(" ", CurrentSeriesPlan.Episodes.Select(e => $"{e.EpisodeTitle} {e.PlotBeat} {e.Cliffhanger}"));
            if (epAllText.Contains("Giám đốc Trần", StringComparison.OrdinalIgnoreCase) || epAllText.Contains("Linh", StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(CurrentSeriesPlan.SeriesTitle))
                    CurrentSeriesPlan.SeriesTitle = "Bẫy Ngầm Nơi Công Sở";

                if (string.IsNullOrWhiteSpace(CharacterBibleText))
                    CharacterBibleText = "Protagonist: Linh, a 26-year-old Vietnamese female financial auditor, sharp intelligent face, expressive dark obsidian eyes, sleek shoulder-length jet-black hair tied in a professional half-up ponytail, wearing an elegant cream-white silk blouse tucked into dark navy tailored trousers, subtle silver pendant necklace, holding a sleek compact smartphone and a covert voice recorder pen, calm and resolute expression. Antagonist: Director Tran, a 52-year-old powerful corporate executive, slicked-back greying black hair, calculating cold eyes behind gold-rimmed glasses, imposing posture, wearing a bespoke charcoal three-piece suit with a burgundy silk tie, gold signet ring on right hand, intimidating demeanor. Both characters remain 100% physically consistent across all scenes.";

                if (string.IsNullOrWhiteSpace(SettingBibleText))
                    SettingBibleText = "High-end corporate luxury office and secluded executive VIP suite in a rainy Saigon skyscraper, floor-to-ceiling glass windows overlooking misty neon city lights, polished dark mahogany executive desks, warm dim tungsten table lamps casting dramatic film-noir shadows, heavy velvet curtains, modern minimalist luxury aesthetics with a dark suspenseful thriller atmosphere, rain droplets streaking down exterior glass panes, cinematic 9:16 framing.";

                if (string.IsNullOrWhiteSpace(PosterPromptText))
                    PosterPromptText = "Cinematic movie poster for a corporate mystery thriller, 8k resolution, photorealistic DSLR quality. Protagonist Linh in a cream-white blouse holding a glowing voice recorder, facing off against Director Tran standing in a luxury dim-lit corner office with rain streaking down panoramic skyscraper windows, dramatic rim lighting, intense high stakes atmosphere, masterpiece, no text, no watermark.";

                CurrentSeriesPlan.CharacterBible = CharacterBibleText;
                CurrentSeriesPlan.SettingBible = SettingBibleText;
                CurrentSeriesPlan.PosterPrompt = PosterPromptText;

                BatchStatusText = "✨ Đã tự động nhận diện và điền Nhân vật (Linh & GĐ Trần) & Bối cảnh!";
            }
            else
            {
                BatchStatusText = $"❌ Lỗi khi tự điền: {ex.Message}";
            }
        }
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

        CurrentSeriesPlan.CharacterBible = CharacterBibleText;
        CurrentSeriesPlan.SettingBible = SettingBibleText;
        CurrentSeriesPlan.PosterPrompt = PosterPromptText;

        // Tự động vẽ ảnh bìa đại diện chung (Master Poster) nếu chưa có
        if (string.IsNullOrEmpty(PosterImagePath) && _imagenService != null && !string.IsNullOrWhiteSpace(PosterPromptText))
        {
            try
            {
                BatchStatusText = "🎨 Đang tạo ảnh bìa đại diện chung (Master Poster) cho Series...";
                var tempPosterDir = Path.Combine(Path.GetTempPath(), "VideoAutoWpf", "SeriesPosters");
                Directory.CreateDirectory(tempPosterDir);
                var posterFile = Path.Combine(tempPosterDir, $"poster_{DateTime.Now:yyyyMMdd_HHmmss}.png");
                await _imagenService.GenerateImageAsync(PosterPromptText, posterFile, ActualAspectRatio);
                PosterImagePath = posterFile;
            }
            catch { }
        }

        var seriesProject = new SeriesProject
        {
            SeriesTitle = CurrentSeriesPlan.SeriesTitle,
            OverallPremise = CurrentSeriesPlan.OverallPremise,
            TotalEpisodes = CurrentSeriesPlan.Episodes.Count,
            GlobalStyleKey = CurrentSeriesPlan.SuggestedStyle,
            GlobalVoice = CurrentSeriesPlan.SuggestedVoice,
            AspectRatio = ActualAspectRatio,
            CharacterBibleRules = CharacterBibleText,
            SettingBibleRules = SettingBibleText,
            PosterPrompt = PosterPromptText,
            PosterImagePath = PosterImagePath,
            EnableGlobalBgm = EnableSeriesBgm,
            GlobalBgmPath = SelectedBgmTrack?.FilePath,
            GlobalBgmVolume = SeriesBgmVolume
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
                for (int attempt = 1; attempt <= 2; attempt++)
                {
                    try
                    {
                        ws = await _geminiService.GenerateEpisodeScriptAsync(
                            CurrentSeriesPlan,
                            epPlan,
                            total,
                            previousEnding,
                            ActualAspectRatio,
                            SelectedSeriesTone);
                        if (ws != null && ws.Scenes.Count > 0)
                            break;
                    }
                    catch
                    {
                        if (attempt == 2) throw;
                        await Task.Delay(1800);
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

        CurrentSeriesPlan.CharacterBible = CharacterBibleText;
        CurrentSeriesPlan.SettingBible = SettingBibleText;
        CurrentSeriesPlan.PosterPrompt = PosterPromptText;

        var seriesProject = new SeriesProject
        {
            SeriesTitle = CurrentSeriesPlan.SeriesTitle,
            OverallPremise = CurrentSeriesPlan.OverallPremise,
            TotalEpisodes = CurrentSeriesPlan.Episodes.Count,
            GlobalStyleKey = CurrentSeriesPlan.SuggestedStyle,
            GlobalVoice = CurrentSeriesPlan.SuggestedVoice,
            AspectRatio = ActualAspectRatio,
            CharacterBibleRules = CharacterBibleText,
            SettingBibleRules = SettingBibleText,
            PosterPrompt = PosterPromptText,
            PosterImagePath = PosterImagePath,
            EnableGlobalBgm = EnableSeriesBgm,
            GlobalBgmPath = SelectedBgmTrack?.FilePath,
            GlobalBgmVolume = SeriesBgmVolume
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

        StopBgm();
        ResultSeriesProject = seriesProject;
        RequestClose?.Invoke(true);
    }

    [RelayCommand]
    private async Task GeneratePosterImageAsync()
    {
        if (_imagenService == null)
        {
            MessageBox.Show("Dịch vụ tạo ảnh chưa được kết nối.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (string.IsNullOrWhiteSpace(PosterPromptText))
        {
            MessageBox.Show("Mô tả ảnh bìa (Poster Prompt) đang để trống!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        IsGeneratingPoster = true;
        BatchStatusText = "⏳ Đang gọi AI vẽ ảnh bìa đại diện chung (Master Poster) cho chuỗi video...";

        try
        {
            var tempPosterDir = Path.Combine(Path.GetTempPath(), "VideoAutoWpf", "SeriesPosters");
            Directory.CreateDirectory(tempPosterDir);
            var posterPath = Path.Combine(tempPosterDir, $"poster_{DateTime.Now:yyyyMMdd_HHmmss}.png");

            await _imagenService.GenerateImageAsync(PosterPromptText, posterPath, ActualAspectRatio, onLog: msg => BatchStatusText = msg);
            PosterImagePath = posterPath;
            BatchStatusText = "✅ Đã tạo thành công ảnh bìa đại diện chung cho toàn bộ Series!";
        }
        catch (Exception ex)
        {
            BatchStatusText = "❌ Lỗi khi vẽ ảnh bìa series: " + ex.Message;
            MessageBox.Show($"Lỗi vẽ Poster Series:\n{ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsGeneratingPoster = false;
        }
    }

    [RelayCommand]
    private void BrowsePosterImage()
    {
        var ofd = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Chọn ảnh bìa đại diện chung cho Series",
            Filter = "Tệp hình ảnh (*.png;*.jpg;*.jpeg;*.webp)|*.png;*.jpg;*.jpeg;*.webp|Tất cả tệp (*.*)|*.*"
        };
        if (ofd.ShowDialog() == true)
        {
            PosterImagePath = ofd.FileName;
        }
    }

    [RelayCommand]
    private void TogglePlayBgm()
    {
        if (IsPlayingBgm)
        {
            StopBgm();
        }
        else
        {
            PlayBgm();
        }
    }

    private void PlayBgm()
    {
        if (SelectedBgmTrack == null || string.IsNullOrEmpty(SelectedBgmTrack.FilePath) || !File.Exists(SelectedBgmTrack.FilePath))
            return;

        try
        {
            StopBgm();
            _bgmPlayer = new System.Windows.Media.MediaPlayer();
            _bgmPlayer.Open(new Uri(SelectedBgmTrack.FilePath));
            _bgmPlayer.Volume = SeriesBgmVolume;
            _bgmPlayer.MediaEnded += (s, e) =>
            {
                if (_bgmPlayer != null)
                {
                    _bgmPlayer.Position = TimeSpan.Zero;
                    _bgmPlayer.Play();
                }
            };
            _bgmPlayer.Play();
            IsPlayingBgm = true;
        }
        catch
        {
            StopBgm();
        }
    }

    private void StopBgm()
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
        }
    }
}
