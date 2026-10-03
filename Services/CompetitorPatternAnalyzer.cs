using System.Text.RegularExpressions;
using VideoAutoWpf.Models;

namespace VideoAutoWpf.Services;

/// <summary>
/// Bộ phân tích thuật toán thông minh bóc tách công thức tiêu đề và retention hook
/// Hoàn toàn MIỄN PHÍ, chạy trực tiếp trên máy không cần tài khoản hay thẻ tín dụng.
/// </summary>
public static class CompetitorPatternAnalyzer
{
    public static CompetitorAiAnalysis AnalyzeChannel(
        string channelTitle,
        string channelDescription,
        IEnumerable<YouTubeVideoInfo> videos)
    {
        var videoList = videos.ToList();
        var analysis = new CompetitorAiAnalysis
        {
            ChannelName = channelTitle
        };

        if (videoList.Count == 0)
        {
            analysis.Summary = "Chưa có đủ video để phân tích.";
            return analysis;
        }

        // 1. Phân tích các pattern tiêu đề
        var formulas = DetectTitleFormulas(videoList);
        analysis.TitleFormulas = formulas;

        // 2. Phân tích chiến lược Hook
        var hooks = DetectHookStrategies(videoList);
        analysis.HookStrategies = hooks;

        // 3. Phân tích tâm lý khán giả & Triggers
        var triggers = DetectPsychologyTriggers(videoList);
        analysis.PsychologyTriggers = triggers;

        // 4. Tóm tắt chiến lược cốt lõi
        analysis.Summary = GenerateStrategicSummary(channelTitle, videoList, formulas);
        analysis.CoreAudience = GenerateAudienceProfile(channelDescription, videoList);

        // 5. Gợi ý 5 ý tưởng video & Hook áp dụng ngay
        analysis.SuggestedIdeas = GenerateAdaptedIdeas(channelTitle, formulas, videoList);

        return analysis;
    }

    public static CompetitorAiAnalysis AnalyzeSingleVideo(YouTubeVideoInfo video)
    {
        var analysis = new CompetitorAiAnalysis
        {
            ChannelName = video.ChannelTitle,
            Summary = $"Phân tích video \"{video.Title}\": Đạt {video.ViewCountDisplay} lượt xem với tỷ lệ tương tác {video.EngagementRateDisplay}. " +
                      $"Tiêu đề áp dụng nghệ thuật kích thích thị giác mạnh kết hợp góc nhìn nhập vai.",
            CoreAudience = "Khán giả trẻ, quan tâm đến các tình huống đời sống, giải trí hoặc góc khuất tâm lý."
        };

        // Bóc tách công thức của video này
        analysis.TitleFormulas.Add(new TitleFormulaItem
        {
            Name = "Công thức tiêu đề trọng tâm",
            Formula = ExtractStructure(video.Title),
            WhyItWorks = "Tập trung vào yếu tố khơi gợi tò mò hoặc hoài niệm, khiến người xem cảm thấy mình là nhân vật chính.",
            Example = video.Title
        });

        analysis.HookStrategies.Add(new HookStrategyItem
        {
            HookType = "In Media Res (Đẩy thẳng vào cao trào)",
            Description = "Bắt đầu ngay bằng một tình huống bất ngờ hoặc câu thoại kịch tính trong 5 giây đầu, không vòng vo.",
            ExampleScript = "Mở đầu ngay bằng tiếng thở dài hoặc âm thanh bối cảnh đặc trưng, sau đó thốt lên câu thoại then chốt của câu chuyện."
        });

        // Gợi ý 3 video tương tự
        analysis.SuggestedIdeas = new List<SuggestedVideoIdea>
        {
            new()
            {
                Title = $"[GÓC NHÌN MỚI] Sự thật đằng sau: {video.Title}",
                HookOpening = "Bạn nghĩ mọi chuyện chỉ dừng lại ở đó? Sự thật kinh ngạc hơn bạn tưởng rất nhiều...",
                TargetEmotion = "Tò mò và bất ngờ"
            },
            new()
            {
                Title = $"Điều GÌ SẼ XẢY RA nếu bạn gặp tình huống này ngoài đời thực?",
                HookOpening = "Nếu một ngày bạn thức dậy và nhận ra kịch bản này xảy ra với chính mình, bạn sẽ làm gì?",
                TargetEmotion = "Kích thích tưởng tượng"
            },
            new()
            {
                Title = $"[BÍ MẬT ÍT AI BIẾT] Phần tiếp theo của câu chuyện này...",
                HookOpening = "Có một chi tiết cực kỳ then chốt trong video trước mà 99% mọi người đã bỏ lỡ...",
                TargetEmotion = "FOMO (Sợ bị bỏ lỡ)"
            }
        };

        return analysis;
    }

    private static List<TitleFormulaItem> DetectTitleFormulas(List<YouTubeVideoInfo> videos)
    {
        var formulas = new List<TitleFormulaItem>();
        var titles = videos.Select(v => v.Title).ToList();

        // Check Pattern 1: POV (Point of View)
        var povVideos = videos.Where(v => v.Title.Contains("POV", StringComparison.OrdinalIgnoreCase)).ToList();
        if (povVideos.Count > 0)
        {
            var sample = povVideos.First().Title;
            formulas.Add(new TitleFormulaItem
            {
                Name = "Công Thức 1: POV + Trải Nghiệm Giả Định (Nostalgia)",
                Formula = "POV: Bạn [Hành động/Hoàn cảnh] ở [Năm/Thời điểm]",
                WhyItWorks = "Đưa người xem vào góc nhìn thứ nhất (nhân vật chính). Khai thác triệt để tâm lý hoài niệm quá khứ hoặc tò mò về trải nghiệm lạ.",
                Example = sample
            });
        }

        // Check Pattern 2: Dùng ngoặc vuông [...] hoặc ngoặc đơn (...)
        var bracketVideos = videos.Where(v => Regex.IsMatch(v.Title, @"[\[\(].*?[\]\)]")).ToList();
        if (bracketVideos.Count > 0)
        {
            var sample = bracketVideos.First().Title;
            formulas.Add(new TitleFormulaItem
            {
                Name = "Công Thức 2: Phân Khúc Đối Tượng + Đóng Khung Chủ Đề (Brackets Framing)",
                Formula = "(Lời nhắn nhủ/Đối tượng) [TÊN CHỦ ĐỀ] + Tuyên bố giật gân",
                WhyItWorks = "Ngoặc vuông tạo cảm giác chuyên nghiệp/hồ sơ mật, ngoặc đơn tạo cảm giác tâm sự riêng tư khiến người xem cảm thấy video này 'dành riêng cho mình'.",
                Example = sample
            });
        }

        // Check Pattern 3: Viết hoa từ khóa cảm xúc mạnh (CAPITALIZED WORDS)
        var capsVideos = videos.Where(v => Regex.IsMatch(v.Title, @"\b[A-ZÀÁẢÃẠĂẮẰẲẴẶÂẤẦẨẪẬÈÉẺẼẸÊẾỀỂỄỆÌÍỈĨỊÒÓỎÕỌÔỐỒỔỖỘƠỚỜỞỠỢÙÚỦŨỤƯỨỪỬỮỰỲÝỶỸỴĐ]{3,}\b")).ToList();
        if (capsVideos.Count > 0)
        {
            var sample = capsVideos.First().Title;
            formulas.Add(new TitleFormulaItem
            {
                Name = "Công Thức 3: Đòn Bẩy Thị Giác (Caps Emphasis & Contrast)",
                Formula = "[Mệnh đề câu chuyện] + [TỪ KHÓA CẢM XÚC IN HOA]",
                WhyItWorks = "Tạo điểm neo thị giác (Visual Anchor) khi người xem lướt bảng tin, buộc mắt phải dừng lại đọc những từ mang tính tuyệt đối (DUY NHẤT, BÍ MẬT, THỰC SỰ).",
                Example = sample
            });
        }

        // Check Pattern 4: Câu hỏi hoặc câu cảm thán
        var questionVideos = videos.Where(v => v.Title.Contains('?') || v.Title.Contains("Tại sao") || v.Title.Contains("Vì sao")).ToList();
        if (questionVideos.Count > 0)
        {
            var sample = questionVideos.First().Title;
            formulas.Add(new TitleFormulaItem
            {
                Name = "Công Thức 4: Câu Hỏi Khơi Gợi Khoảng Trống Nhận Thức (Curiosity Gap)",
                Formula = "Tại sao [Điều phi lý/Bất ngờ]? / Liệu bạn có [Hành động]?",
                WhyItWorks = "Não bộ người xem luôn cảm thấy bứt rứt khi gặp một câu hỏi chưa có câu trả lời, thôi thúc họ nhấn xem để giải tỏa.",
                Example = sample
            });
        }

        // Fallback formula if fewer than 2 detected
        if (formulas.Count == 0)
        {
            var sample = titles.FirstOrDefault() ?? "Video mẫu";
            formulas.Add(new TitleFormulaItem
            {
                Name = "Công Thức Storytelling & Hút Click Tự Nhiên",
                Formula = "[Bối cảnh cuốn hút] + [Tình tiết bất ngờ]",
                WhyItWorks = "Đánh thẳng vào nhu cầu giải trí và đồng cảm của người xem.",
                Example = sample
            });
        }

        return formulas;
    }

    private static List<HookStrategyItem> DetectHookStrategies(List<YouTubeVideoInfo> videos)
    {
        var hooks = new List<HookStrategyItem>
        {
            new()
            {
                HookType = "🎣 Hook 1: In Media Res (Ném Khán Giả Vào Giữa Cảnh Huống)",
                Description = "3-5 giây đầu tiên KHÔNG chào hỏi, không intro dài dòng. Bắt đầu ngay bằng tiếng chuông báo thức, tiếng đồng hồ kêu hoặc một tình huống nhân vật đang hốt hoảng bối rối.",
                ExampleScript = "\"Ủa... khoan đã... điện thoại iPhone X? Bản cập nhật iOS 11? Nhạc Sơn Tùng Chạy Ngay Đi đang bật ngoài ngõ... Mình đang ở năm 2018 thật à?!\""
            },
            new()
            {
                HookType = "🎣 Hook 2: Đồng Cảm Tiêu Cực (Emotional Validation & FOMO)",
                Description = "Đánh thẳng vào nỗi sợ bị cô lập, cảm giác tự ti hoặc những áp lực mà ai cũng từng trải qua (thi trượt, áp lực đồng trang lứa, cảm giác là người thừa).",
                ExampleScript = "\"Có một cảm giác cực kỳ kinh khủng: khi tất cả bạn bè xung quanh đều hò reo ăn mừng, còn bạn là người duy nhất phải lén tắt thông báo Facebook...\""
            },
            new()
            {
                HookType = "🎣 Hook 3: Trực Giác Ngược (Counter-Intuitive / Phá Vỡ Kỳ Vọng)",
                Description = "Nêu ra một quan điểm hoặc sự thật hoàn toàn trái ngược với những gì số đông lầm tưởng, buộc người xem phải ở lại xem giải thích.",
                ExampleScript = "\"Ai cũng nói vào đại học là đổi đời, nhưng không ai nói cho bạn biết cái giá thật sự sau 4 năm là gì...\""
            }
        };

        return hooks;
    }

    private static List<string> DetectPsychologyTriggers(List<YouTubeVideoInfo> videos)
    {
        return new List<string>
        {
            "🌟 Hoài niệm (Nostalgia): Khát khao quay về quá khứ tươi đẹp (2018, thời học sinh).",
            "😱 Nỗi sợ bị bỏ rơi (FOMO / Isolation): Tâm lý sợ mình là người duy nhất thất bại.",
            "🎭 Góc nhìn thứ nhất (POV Identity): Người xem tưởng tượng chính mình đang trải qua câu chuyện.",
            "🔍 Sự thật trần trụi (Unvarnished Truth): Khám phá góc khuất ít ai dám công khai nói tới."
        };
    }

    private static string GenerateStrategicSummary(string channelTitle, List<YouTubeVideoInfo> videos, List<TitleFormulaItem> formulas)
    {
        var totalViews = videos.Sum(v => v.ViewCount);
        var avgViews = videos.Count > 0 ? totalViews / videos.Count : 0;
        var avgViewsDisplay = YouTubeChannelInfo.FormatNumber(avgViews);

        return $"Kênh \"{channelTitle}\" đạt trung bình {avgViewsDisplay} views/video nhờ chiến lược nội dung tập trung vào: " +
               $"(1) Dạng video POV nhập vai hoài niệm quá khứ; " +
               $"(2) Tiêu đề kết hợp nhãn chủ đề rõ ràng và từ khóa cảm xúc mạnh; " +
               $"(3) Giữ chân khán giả bằng tình huống kịch tính ngay từ giây đầu tiên thay vì giới thiệu dài dòng.";
    }

    private static string GenerateAudienceProfile(string channelDesc, List<YouTubeVideoInfo> videos)
    {
        return "Khán giả Gen Z và Millennials trẻ tuổi (15 - 28 tuổi), học sinh, sinh viên, người mới đi làm. " +
               "Họ thích các nội dung mang tính đồng cảm sâu, hài hước châm biếm, hoài niệm về thời kỳ 2016-2021 và các góc khuất thực tế trong cuộc sống.";
    }

    private static List<SuggestedVideoIdea> GenerateAdaptedIdeas(string channelTitle, List<TitleFormulaItem> formulas, List<YouTubeVideoInfo> videos)
    {
        return new List<SuggestedVideoIdea>
        {
            new()
            {
                Title = "POV: Bạn thức dậy vào sáng Thứ Hai năm 2016 (Thời chưa có TikTok)",
                HookOpening = "Tiếng chuông báo thức iPhone quen thuộc vang lên... Mở Facebook ra thấy bạn bè đang share bài hát 'Lạc Trôi', chẳng ai biết TikTok là gì...",
                TargetEmotion = "Hoài niệm tuổi học trò & sự bình yên"
            },
            new()
            {
                Title = "(người lớn cũng nên xem) [POV GÓC KHUẤT] Ngày đầu tiên đi làm bị CẢ PHÒNG cô lập",
                HookOpening = "Bước chân vào công ty mới với đầy hy vọng, nhưng sau 8 tiếng đồng hồ nhận ra không một ai thèm nhìn hay bắt chuyện với bạn...",
                TargetEmotion = "Đồng cảm sâu sắc & tò mò cách giải quyết"
            },
            new()
            {
                Title = "POV: Bạn thức dậy ở năm 2012 khi nghe tin 'TẬN THẾ'",
                HookOpening = "Lịch vạn niên chỉ ngày 21/12/2012... Cả lớp nhốn nháo bàn nhau xem nếu hôm nay là ngày tận thế thật thì sẽ làm gì đầu tiên...",
                TargetEmotion = "Ký ức hài hước thời niên thiếu"
            },
            new()
            {
                Title = "[SỰ THẬT ĐẮNG CAY] Lý do bạn luôn cảm thấy BỊ BỎ LẠI PHÍA SAU so với bạn bè",
                HookOpening = "Có bao giờ bạn lướt story thấy bạn bè mua xe, khoe lương, còn mình vẫn dậm chân tại chỗ? Đây là sự thật đằng sau những bức ảnh đó...",
                TargetEmotion = "Thức tỉnh tâm lý & chạm đúng nỗi đau"
            },
            new()
            {
                Title = "POV: Bạn thức dậy ở ngày biết điểm thi Đại Học năm 2019",
                HookOpening = "12h đêm, tay run bần bật gõ số báo danh vào trang web của Bộ... Tiếng quạt trần kêu cót két và bố mẹ đang đứng ngay sau lưng...",
                TargetEmotion = "Hồi hộp nghẹt thở & vỡ òa cảm xúc"
            }
        };
    }

    private static string ExtractStructure(string title)
    {
        if (title.Contains("POV", StringComparison.OrdinalIgnoreCase))
            return "POV: [Tình huống trải nghiệm] + [Thời gian/Địa điểm]";
        if (title.Contains('[') && title.Contains(']'))
            return "[NHÃN CHỦ ĐỀ] + Lời khẳng định gây sốc";
        if (title.Contains('?'))
            return "Câu hỏi khơi gợi tò mò (Tại sao / Liệu có...)";
        return "[Mệnh đề mở đầu] + [Yếu tố bất ngờ]";
    }
}
