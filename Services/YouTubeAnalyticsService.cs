using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using System.Xml;
using Google.Apis.Services;
using Google.Apis.YouTube.v3;
using VideoAutoWpf.Models;

namespace VideoAutoWpf.Services;

/// <summary>
/// Service tích hợp YouTube Data API v3 để phân tích kênh, video, trending
/// </summary>
public class YouTubeAnalyticsService
{
    private readonly GoogleAuthService _authService;
    private YouTubeService? _youtubeService;

    public YouTubeAnalyticsService(GoogleAuthService authService)
    {
        _authService = authService;
    }

    /// <summary>
    /// Khởi tạo YouTubeService với Service Account credentials
    /// </summary>
    private YouTubeService GetService()
    {
        if (_youtubeService != null) return _youtubeService;

        var credential = _authService.GetCredential();
        if (credential == null)
            throw new InvalidOperationException("Chưa cấu hình Google credentials. Vui lòng kiểm tra file Service Account key.");

        var scopedCredential = credential.CreateScoped(
            "https://www.googleapis.com/auth/youtube.readonly",
            "https://www.googleapis.com/auth/cloud-platform"
        );

        _youtubeService = new YouTubeService(new BaseClientService.Initializer
        {
            HttpClientInitializer = scopedCredential,
            ApplicationName = "VideoAutoWpf YouTube Analytics"
        });

        return _youtubeService;
    }

    /// <summary>
    /// Trích xuất Channel ID từ URL hoặc handle YouTube
    /// </summary>
    public async Task<string> ResolveChannelIdAsync(string input)
    {
        input = input.Trim();

        // Nếu đã là Channel ID (bắt đầu UC)
        if (input.StartsWith("UC") && input.Length == 24)
            return input;

        // Trích xuất từ URL
        var channelIdMatch = Regex.Match(input, @"youtube\.com/channel/(UC[\w-]{22})");
        if (channelIdMatch.Success)
            return channelIdMatch.Groups[1].Value;

        // Handle @username hoặc URL /@username
        var handleMatch = Regex.Match(input, @"@([\w.-]+)");
        if (handleMatch.Success)
        {
            var handle = handleMatch.Groups[1].Value;
            return await FindChannelByHandleAsync(handle);
        }

        // Custom URL /c/name hoặc /user/name
        var customUrlMatch = Regex.Match(input, @"youtube\.com/(?:c|user)/([\w.-]+)");
        if (customUrlMatch.Success)
        {
            return await SearchChannelByNameAsync(customUrlMatch.Groups[1].Value);
        }

        // Thử tìm kiếm bằng tên kênh
        return await SearchChannelByNameAsync(input);
    }

    private async Task<string> FindChannelByHandleAsync(string handle)
    {
        var service = GetService();
        var request = service.Channels.List("id");
        request.ForHandle = handle;
        var response = await request.ExecuteAsync();

        if (response.Items != null && response.Items.Count > 0)
            return response.Items[0].Id;

        throw new Exception($"Không tìm thấy kênh YouTube với handle @{handle}");
    }

    private async Task<string> SearchChannelByNameAsync(string query)
    {
        var service = GetService();
        var searchRequest = service.Search.List("snippet");
        searchRequest.Q = query;
        searchRequest.Type = "channel";
        searchRequest.MaxResults = 1;
        var response = await searchRequest.ExecuteAsync();

        if (response.Items != null && response.Items.Count > 0)
            return response.Items[0].Snippet.ChannelId;

        throw new Exception($"Không tìm thấy kênh YouTube với tên '{query}'");
    }

    /// <summary>
    /// Lấy thông tin chi tiết kênh YouTube
    /// </summary>
    public async Task<YouTubeChannelInfo> GetChannelInfoAsync(string channelId)
    {
        var service = GetService();
        var request = service.Channels.List("snippet,statistics,brandingSettings");
        request.Id = channelId;
        var response = await request.ExecuteAsync();

        if (response.Items == null || response.Items.Count == 0)
            throw new Exception($"Không tìm thấy kênh với ID: {channelId}");

        var ch = response.Items[0];
        return new YouTubeChannelInfo
        {
            ChannelId = ch.Id,
            Title = ch.Snippet.Title ?? string.Empty,
            Description = ch.Snippet.Description ?? string.Empty,
            ThumbnailUrl = ch.Snippet.Thumbnails?.Medium?.Url ?? ch.Snippet.Thumbnails?.Default__?.Url ?? string.Empty,
            CustomUrl = ch.Snippet.CustomUrl ?? string.Empty,
            PublishedAt = ch.Snippet.PublishedAtDateTimeOffset?.UtcDateTime ?? DateTime.MinValue,
            SubscriberCount = (long)(ch.Statistics.SubscriberCount ?? 0),
            ViewCount = (long)(ch.Statistics.ViewCount ?? 0),
            VideoCount = (long)(ch.Statistics.VideoCount ?? 0),
            Country = ch.Snippet.Country ?? "N/A"
        };
    }

    /// <summary>
    /// Lấy danh sách video phổ biến nhất của kênh (Top Videos)
    /// </summary>
    public async Task<ObservableCollection<YouTubeVideoInfo>> GetTopVideosAsync(string channelId, int maxResults = 20)
    {
        var service = GetService();

        // Bước 1: Tìm video của kênh, sắp xếp theo lượt xem
        var searchRequest = service.Search.List("id");
        searchRequest.ChannelId = channelId;
        searchRequest.Order = SearchResource.ListRequest.OrderEnum.ViewCount;
        searchRequest.Type = "video";
        searchRequest.MaxResults = maxResults;
        var searchResponse = await searchRequest.ExecuteAsync();

        if (searchResponse.Items == null || searchResponse.Items.Count == 0)
            return new ObservableCollection<YouTubeVideoInfo>();

        // Bước 2: Lấy chi tiết từng video
        var videoIds = string.Join(",", searchResponse.Items.Select(i => i.Id.VideoId));
        var videoRequest = service.Videos.List("snippet,statistics,contentDetails");
        videoRequest.Id = videoIds;
        var videoResponse = await videoRequest.ExecuteAsync();

        var result = new ObservableCollection<YouTubeVideoInfo>();
        int rank = 1;
        foreach (var v in videoResponse.Items.OrderByDescending(v => v.Statistics.ViewCount ?? 0))
        {
            result.Add(MapVideoToInfo(v, rank++));
        }

        return result;
    }

    /// <summary>
    /// Lấy danh sách video mới nhất của kênh
    /// </summary>
    public async Task<ObservableCollection<YouTubeVideoInfo>> GetLatestVideosAsync(string channelId, int maxResults = 20)
    {
        var service = GetService();

        var searchRequest = service.Search.List("id");
        searchRequest.ChannelId = channelId;
        searchRequest.Order = SearchResource.ListRequest.OrderEnum.Date;
        searchRequest.Type = "video";
        searchRequest.MaxResults = maxResults;
        var searchResponse = await searchRequest.ExecuteAsync();

        if (searchResponse.Items == null || searchResponse.Items.Count == 0)
            return new ObservableCollection<YouTubeVideoInfo>();

        var videoIds = string.Join(",", searchResponse.Items.Select(i => i.Id.VideoId));
        var videoRequest = service.Videos.List("snippet,statistics,contentDetails");
        videoRequest.Id = videoIds;
        var videoResponse = await videoRequest.ExecuteAsync();

        var result = new ObservableCollection<YouTubeVideoInfo>();
        int rank = 1;
        foreach (var v in videoResponse.Items.OrderByDescending(v => v.Snippet.PublishedAtDateTimeOffset))
        {
            result.Add(MapVideoToInfo(v, rank++));
        }

        return result;
    }

    /// <summary>
    /// Phân tích chi tiết một video cụ thể từ URL
    /// </summary>
    public async Task<YouTubeVideoInfo> GetVideoInfoAsync(string videoUrl)
    {
        var videoId = ExtractVideoId(videoUrl);
        if (string.IsNullOrEmpty(videoId))
            throw new Exception($"Không thể trích xuất Video ID từ URL: {videoUrl}");

        var service = GetService();
        var request = service.Videos.List("snippet,statistics,contentDetails");
        request.Id = videoId;
        var response = await request.ExecuteAsync();

        if (response.Items == null || response.Items.Count == 0)
            throw new Exception($"Không tìm thấy video với ID: {videoId}");

        return MapVideoToInfo(response.Items[0], 1);
    }

    /// <summary>
    /// Tìm kiếm video theo từ khóa
    /// </summary>
    public async Task<ObservableCollection<YouTubeSearchResult>> SearchVideosAsync(string keyword, int maxResults = 25, string? regionCode = null)
    {
        var service = GetService();
        var request = service.Search.List("snippet");
        request.Q = keyword;
        request.Type = "video";
        request.Order = SearchResource.ListRequest.OrderEnum.Relevance;
        request.MaxResults = maxResults;
        if (!string.IsNullOrEmpty(regionCode))
            request.RegionCode = regionCode;

        var response = await request.ExecuteAsync();
        var results = new ObservableCollection<YouTubeSearchResult>();

        if (response.Items == null) return results;

        // Lấy thêm thống kê cho từng video
        var videoIds = string.Join(",", response.Items.Where(i => i.Id?.VideoId != null).Select(i => i.Id.VideoId));
        Dictionary<string, (long views, long likes)> statsMap = new();

        if (!string.IsNullOrEmpty(videoIds))
        {
            var videoRequest = service.Videos.List("statistics");
            videoRequest.Id = videoIds;
            var videoResponse = await videoRequest.ExecuteAsync();
            foreach (var v in videoResponse.Items)
            {
                statsMap[v.Id] = ((long)(v.Statistics.ViewCount ?? 0), (long)(v.Statistics.LikeCount ?? 0));
            }
        }

        foreach (var item in response.Items)
        {
            if (item.Id?.VideoId == null) continue;
            var sr = new YouTubeSearchResult
            {
                VideoId = item.Id.VideoId,
                Title = item.Snippet.Title ?? string.Empty,
                ChannelTitle = item.Snippet.ChannelTitle ?? string.Empty,
                ThumbnailUrl = item.Snippet.Thumbnails?.Medium?.Url ?? item.Snippet.Thumbnails?.Default__?.Url ?? string.Empty,
                PublishedAt = item.Snippet.PublishedAtDateTimeOffset?.UtcDateTime ?? DateTime.MinValue,
            };

            if (statsMap.TryGetValue(item.Id.VideoId, out var stats))
            {
                sr.ViewCount = stats.views;
                sr.LikeCount = stats.likes;
            }

            results.Add(sr);
        }

        return results;
    }

    /// <summary>
    /// Lấy video trending theo quốc gia
    /// </summary>
    public async Task<ObservableCollection<YouTubeVideoInfo>> GetTrendingVideosAsync(string regionCode = "VN", int maxResults = 25, string? categoryId = null)
    {
        var service = GetService();
        var request = service.Videos.List("snippet,statistics,contentDetails");
        request.Chart = VideosResource.ListRequest.ChartEnum.MostPopular;
        request.RegionCode = regionCode;
        request.MaxResults = maxResults;
        if (!string.IsNullOrEmpty(categoryId))
            request.VideoCategoryId = categoryId;

        var response = await request.ExecuteAsync();
        var result = new ObservableCollection<YouTubeVideoInfo>();

        if (response.Items == null) return result;

        int rank = 1;
        foreach (var v in response.Items)
        {
            result.Add(MapVideoToInfo(v, rank++));
        }

        return result;
    }

    /// <summary>
    /// Tìm kiếm các kênh YouTube theo chủ đề và lấy ra những kênh có lượt xem (views) cao nhất
    /// </summary>
    public async Task<ObservableCollection<YouTubeChannelInfo>> GetTopChannelsByTopicAsync(
        string topic, 
        int maxResults = 20, 
        string? regionCode = null, 
        string sortBy = "views")
    {
        var service = GetService();
        var channelIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // 1. Tìm kiếm kênh trực tiếp theo chủ đề
        try
        {
            var channelSearch = service.Search.List("snippet");
            channelSearch.Q = topic;
            channelSearch.Type = "channel";
            channelSearch.MaxResults = 50;
            if (!string.IsNullOrEmpty(regionCode))
                channelSearch.RegionCode = regionCode;

            var channelSearchResp = await channelSearch.ExecuteAsync();
            if (channelSearchResp.Items != null)
            {
                foreach (var item in channelSearchResp.Items)
                {
                    var cid = item.Snippet?.ChannelId ?? item.Id?.ChannelId;
                    if (!string.IsNullOrEmpty(cid))
                        channelIds.Add(cid);
                }
            }
        }
        catch
        {
            // Bỏ qua lỗi tìm kiếm kênh nếu có
        }

        // 2. Tìm kiếm thêm các video có lượt view cao nhất theo chủ đề để phát hiện các kênh 'ông trùm' ngách đó
        try
        {
            var videoSearch = service.Search.List("snippet");
            videoSearch.Q = topic;
            videoSearch.Type = "video";
            videoSearch.Order = SearchResource.ListRequest.OrderEnum.ViewCount;
            videoSearch.MaxResults = 30;
            if (!string.IsNullOrEmpty(regionCode))
                videoSearch.RegionCode = regionCode;

            var videoSearchResp = await videoSearch.ExecuteAsync();
            if (videoSearchResp.Items != null)
            {
                foreach (var item in videoSearchResp.Items)
                {
                    if (!string.IsNullOrEmpty(item.Snippet?.ChannelId))
                        channelIds.Add(item.Snippet.ChannelId);
                }
            }
        }
        catch
        {
            // Bỏ qua lỗi video search nếu có
        }

        if (channelIds.Count == 0)
            return new ObservableCollection<YouTubeChannelInfo>();

        // 3. Lấy thông tin thống kê chi tiết cho tất cả kênh tìm được (tối đa 50 kênh trong 1 API call)
        var targetIds = channelIds.Take(50).ToList();
        var channelListReq = service.Channels.List("snippet,statistics,brandingSettings");
        channelListReq.Id = string.Join(",", targetIds);
        var channelListResp = await channelListReq.ExecuteAsync();

        var channelList = new List<YouTubeChannelInfo>();
        if (channelListResp.Items != null)
        {
            foreach (var ch in channelListResp.Items)
            {
                var viewCount = (long)(ch.Statistics?.ViewCount ?? 0);
                var subCount = (long)(ch.Statistics?.SubscriberCount ?? 0);
                var videoCount = (long)(ch.Statistics?.VideoCount ?? 0);

                var info = new YouTubeChannelInfo
                {
                    ChannelId = ch.Id,
                    Title = ch.Snippet?.Title ?? string.Empty,
                    Description = ch.Snippet?.Description ?? string.Empty,
                    ThumbnailUrl = ch.Snippet?.Thumbnails?.Medium?.Url ?? ch.Snippet?.Thumbnails?.Default__?.Url ?? string.Empty,
                    CustomUrl = ch.Snippet?.CustomUrl ?? string.Empty,
                    PublishedAt = ch.Snippet?.PublishedAtDateTimeOffset?.UtcDateTime ?? DateTime.MinValue,
                    SubscriberCount = subCount,
                    ViewCount = viewCount,
                    VideoCount = videoCount,
                    Country = ch.Snippet?.Country ?? "N/A"
                };
                channelList.Add(info);
            }
        }

        // 4. Sắp xếp theo tiêu chí: Mặc định là Lượt View Cao Nhất (ViewCount)
        IEnumerable<YouTubeChannelInfo> sorted = sortBy switch
        {
            "subs" => channelList.OrderByDescending(c => c.SubscriberCount),
            "avg" => channelList.OrderByDescending(c => c.AvgViewsPerVideo),
            "videos" => channelList.OrderByDescending(c => c.VideoCount),
            _ => channelList.OrderByDescending(c => c.ViewCount)
        };

        var result = new ObservableCollection<YouTubeChannelInfo>();
        int rank = 1;
        foreach (var ch in sorted.Take(maxResults))
        {
            ch.Rank = rank++;
            result.Add(ch);
        }

        return result;
    }

    // ── Helpers ──

    private static YouTubeVideoInfo MapVideoToInfo(Google.Apis.YouTube.v3.Data.Video v, int rank)
    {
        return new YouTubeVideoInfo
        {
            VideoId = v.Id,
            Title = v.Snippet.Title ?? string.Empty,
            Description = v.Snippet.Description ?? string.Empty,
            ThumbnailUrl = v.Snippet.Thumbnails?.Medium?.Url ?? v.Snippet.Thumbnails?.Default__?.Url ?? string.Empty,
            ChannelTitle = v.Snippet.ChannelTitle ?? string.Empty,
            ChannelId = v.Snippet.ChannelId ?? string.Empty,
            PublishedAt = v.Snippet.PublishedAtDateTimeOffset?.UtcDateTime ?? DateTime.MinValue,
            Duration = ParseIsoDuration(v.ContentDetails?.Duration),
            ViewCount = (long)(v.Statistics?.ViewCount ?? 0),
            LikeCount = (long)(v.Statistics?.LikeCount ?? 0),
            CommentCount = (long)(v.Statistics?.CommentCount ?? 0),
            Tags = v.Snippet.Tags != null ? string.Join(", ", v.Snippet.Tags) : string.Empty,
            CategoryId = v.Snippet.CategoryId ?? string.Empty,
            Rank = rank
        };
    }

    private static TimeSpan ParseIsoDuration(string? iso)
    {
        if (string.IsNullOrEmpty(iso)) return TimeSpan.Zero;
        try
        {
            return XmlConvert.ToTimeSpan(iso);
        }
        catch
        {
            return TimeSpan.Zero;
        }
    }

    private static string? ExtractVideoId(string input)
    {
        input = input.Trim();

        // Nếu đã là video ID (11 ký tự)
        if (Regex.IsMatch(input, @"^[\w-]{11}$"))
            return input;

        // youtube.com/watch?v=ID
        var match = Regex.Match(input, @"[?&]v=([\w-]{11})");
        if (match.Success) return match.Groups[1].Value;

        // youtu.be/ID
        match = Regex.Match(input, @"youtu\.be/([\w-]{11})");
        if (match.Success) return match.Groups[1].Value;

        // youtube.com/embed/ID hoặc /shorts/ID
        match = Regex.Match(input, @"youtube\.com/(?:embed|shorts|v)/([\w-]{11})");
        if (match.Success) return match.Groups[1].Value;

        return null;
    }
}
