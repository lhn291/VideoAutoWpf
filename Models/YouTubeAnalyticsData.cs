using System.Collections.ObjectModel;

namespace VideoAutoWpf.Models;

/// <summary>
/// Thông tin tổng quan về một kênh YouTube
/// </summary>
public class YouTubeChannelInfo
{
    public string ChannelId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ThumbnailUrl { get; set; } = string.Empty;
    public string CustomUrl { get; set; } = string.Empty;
    public DateTime PublishedAt { get; set; }
    public long SubscriberCount { get; set; }
    public long ViewCount { get; set; }
    public long VideoCount { get; set; }
    public string Country { get; set; } = string.Empty;

    public string SubscriberDisplay => FormatNumber(SubscriberCount);
    public string ViewCountDisplay => FormatNumber(ViewCount);
    public string VideoCountDisplay => FormatNumber(VideoCount);
    public string ChannelAge => PublishedAt == DateTime.MinValue 
        ? "Chưa xác định" 
        : $"{(DateTime.UtcNow - PublishedAt).Days / 365} năm {(DateTime.UtcNow - PublishedAt).Days % 365 / 30} tháng";
    public double AvgViewsPerVideo => VideoCount > 0 ? (double)ViewCount / VideoCount : 0;
    public string AvgViewsPerVideoDisplay => FormatNumber((long)AvgViewsPerVideo);

    public int Rank { get; set; }
    public string RankBadge => Rank switch
    {
        1 => "🥇",
        2 => "🥈",
        3 => "🥉",
        _ => $"#{Rank}"
    };
    public string Url => !string.IsNullOrEmpty(CustomUrl) 
        ? $"https://www.youtube.com/{CustomUrl}" 
        : $"https://www.youtube.com/channel/{ChannelId}";

    public static string FormatNumber(long number)
    {
        return number switch
        {
            >= 1_000_000_000 => $"{number / 1_000_000_000.0:F1}B",
            >= 1_000_000 => $"{number / 1_000_000.0:F1}M",
            >= 1_000 => $"{number / 1_000.0:F1}K",
            _ => number.ToString("N0")
        };
    }
}

/// <summary>
/// Thông tin chi tiết về một video YouTube
/// </summary>
public class YouTubeVideoInfo
{
    public string VideoId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ThumbnailUrl { get; set; } = string.Empty;
    public string ChannelTitle { get; set; } = string.Empty;
    public string ChannelId { get; set; } = string.Empty;
    public DateTime PublishedAt { get; set; }
    public TimeSpan Duration { get; set; }
    public long ViewCount { get; set; }
    public long LikeCount { get; set; }
    public long CommentCount { get; set; }
    public string Tags { get; set; } = string.Empty;
    public string CategoryId { get; set; } = string.Empty;

    // Computed
    public string ViewCountDisplay => FormatNumber(ViewCount);
    public string LikeCountDisplay => FormatNumber(LikeCount);
    public string CommentCountDisplay => FormatNumber(CommentCount);
    public string DurationDisplay => Duration.TotalHours >= 1
        ? Duration.ToString(@"h\:mm\:ss")
        : Duration.ToString(@"m\:ss");
    public double EngagementRate => ViewCount > 0 ? (double)(LikeCount + CommentCount) / ViewCount * 100 : 0;
    public string EngagementRateDisplay => $"{EngagementRate:F2}%";
    public double LikePerView => ViewCount > 0 ? (double)LikeCount / ViewCount * 100 : 0;
    public string LikePerViewDisplay => $"{LikePerView:F2}%";
    public string PublishedAgo => FormatTimeAgo(PublishedAt);
    public string Url => $"https://www.youtube.com/watch?v={VideoId}";

    // Ranking indicator
    public int Rank { get; set; }
    public string RankBadge => Rank switch
    {
        1 => "🥇",
        2 => "🥈",
        3 => "🥉",
        _ => $"#{Rank}"
    };

    private static string FormatNumber(long number)
    {
        return number switch
        {
            >= 1_000_000_000 => $"{number / 1_000_000_000.0:F1}B",
            >= 1_000_000 => $"{number / 1_000_000.0:F1}M",
            >= 1_000 => $"{number / 1_000.0:F1}K",
            _ => number.ToString("N0")
        };
    }

    private static string FormatTimeAgo(DateTime dt)
    {
        if (dt == DateTime.MinValue) return "N/A";
        var ts = DateTime.UtcNow - dt;
        if (ts.TotalDays >= 365) return $"{(int)(ts.TotalDays / 365)} năm trước";
        if (ts.TotalDays >= 30) return $"{(int)(ts.TotalDays / 30)} tháng trước";
        if (ts.TotalDays >= 1) return $"{(int)ts.TotalDays} ngày trước";
        if (ts.TotalHours >= 1) return $"{(int)ts.TotalHours} giờ trước";
        return $"{(int)ts.TotalMinutes} phút trước";
    }
}

/// <summary>
/// Kết quả tìm kiếm trending/keyword
/// </summary>
public class YouTubeSearchResult
{
    public string VideoId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string ChannelTitle { get; set; } = string.Empty;
    public string ThumbnailUrl { get; set; } = string.Empty;
    public DateTime PublishedAt { get; set; }
    public string PublishedAgo => FormatTimeAgoStatic(PublishedAt);
    public string Url => $"https://www.youtube.com/watch?v={VideoId}";

    // Thống kê (nếu có từ video details)
    public long ViewCount { get; set; }
    public long LikeCount { get; set; }
    public string ViewCountDisplay => ViewCount > 0 ? FormatNumber(ViewCount) : "—";

    private static string FormatNumber(long number)
    {
        return number switch
        {
            >= 1_000_000_000 => $"{number / 1_000_000_000.0:F1}B",
            >= 1_000_000 => $"{number / 1_000_000.0:F1}M",
            >= 1_000 => $"{number / 1_000.0:F1}K",
            _ => number.ToString("N0")
        };
    }

    public static string FormatTimeAgoStatic(DateTime dt)
    {
        if (dt == DateTime.MinValue) return "N/A";
        var ts = DateTime.UtcNow - dt;
        if (ts.TotalDays >= 365) return $"{(int)(ts.TotalDays / 365)} năm trước";
        if (ts.TotalDays >= 30) return $"{(int)(ts.TotalDays / 30)} tháng trước";
        if (ts.TotalDays >= 1) return $"{(int)ts.TotalDays} ngày trước";
        if (ts.TotalHours >= 1) return $"{(int)ts.TotalHours} giờ trước";
        return $"{(int)ts.TotalMinutes} phút trước";
    }
}

/// <summary>
/// So sánh kênh (đối thủ)
/// </summary>
public class ChannelComparisonItem
{
    public YouTubeChannelInfo Channel { get; set; } = new();
    public ObservableCollection<YouTubeVideoInfo> TopVideos { get; set; } = new();
    public bool IsMyChannel { get; set; }
    public string Label => IsMyChannel ? "📌 Kênh của bạn" : "🆚 Đối thủ";
}

/// <summary>
/// Kết quả phân tích AI Gemini về tiêu đề, hook và chiến lược của kênh/đối thủ
/// </summary>
public class CompetitorAiAnalysis
{
    public string ChannelName { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string CoreAudience { get; set; } = string.Empty;
    public List<TitleFormulaItem> TitleFormulas { get; set; } = new();
    public List<HookStrategyItem> HookStrategies { get; set; } = new();
    public List<string> PsychologyTriggers { get; set; } = new();
    public List<SuggestedVideoIdea> SuggestedIdeas { get; set; } = new();
}

public class TitleFormulaItem
{
    public string Name { get; set; } = string.Empty;
    public string Formula { get; set; } = string.Empty;
    public string WhyItWorks { get; set; } = string.Empty;
    public string Example { get; set; } = string.Empty;
}

public class HookStrategyItem
{
    public string HookType { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ExampleScript { get; set; } = string.Empty;
}

public class SuggestedVideoIdea
{
    public string Title { get; set; } = string.Empty;
    public string HookOpening { get; set; } = string.Empty;
    public string TargetEmotion { get; set; } = string.Empty;
}

public enum VideoCreationTargetMode
{
    SingleEpisode,
    Series
}

/// <summary>
/// Dữ liệu yêu cầu chuyển trực tiếp từ YouTube Analytics sang bộ tạo video tự động
/// </summary>
public class CreateVideoFromAnalyticsRequest
{
    public string Title { get; set; } = string.Empty;
    public string HookOpening { get; set; } = string.Empty;
    public string HookStrategy { get; set; } = string.Empty;
    public string TargetEmotion { get; set; } = string.Empty;
    public string TargetAudience { get; set; } = string.Empty;
    public string SuggestedStyle { get; set; } = string.Empty;
    public string SuggestedTone { get; set; } = string.Empty;
    public string SuggestedBgm { get; set; } = string.Empty;
    public string SourceInfo { get; set; } = string.Empty;
    public string VideoId { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string RawTranscript { get; set; } = string.Empty;
    public string Tags { get; set; } = string.Empty;
    public VideoCreationTargetMode TargetMode { get; set; } = VideoCreationTargetMode.SingleEpisode;
}

