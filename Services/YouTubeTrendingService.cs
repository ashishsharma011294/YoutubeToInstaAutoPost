using System.Net.Http.Json;
using System.Text.Json;
using Google.Apis.Services;
using Google.Apis.YouTube.v3;
using Core.Entities;
using Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace InstaAutoPost.Services;

/// <summary>
/// Fetches popular YouTube videos using the YouTube Data API.
/// </summary>
public class YouTubeTrendingService
{
    private readonly ILogger<YouTubeTrendingService> _logger;
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly YouTubeSettings _settings;

    private readonly YouTubeService _youtubeService;


    private const string ApiBase = "https://www.googleapis.com/youtube/v3";

    public YouTubeTrendingService(
        ILogger<YouTubeTrendingService> logger,
        IHttpClientFactory httpClientFactory,
        IOptions<YouTubeSettings> settings, 
        IConfiguration configuration)
    {
        _logger = logger;
        _httpClient = httpClientFactory.CreateClient();
        _settings = settings.Value;
        _configuration = configuration;

        var apiKey = _configuration["YouTubeSettings:ApiKey"];
        _youtubeService = new YouTubeService(new BaseClientService.Initializer()
        {
            ApiKey = apiKey,
            ApplicationName = "InstaAutoPost"
        });
    }

    /// <summary>
    /// Returns the watch URL for the top most-popular video in the given region.
    /// </summary>
    /// <param name="regionCode">ISO country code (default US).</param>
    /// <returns>YouTube watch URL.</returns>
    public async Task<string> GetTopTrendingVideoUrlAsync(string regionCode = "US")
    {
        var apiKey = _configuration["YouTubeSettings:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException("YouTube API key is missing in configuration (YouTubeSettings:ApiKey).");
        }

        var url =
            $"{ApiBase}/videos?part=snippet,statistics&chart=mostPopular&maxResults=1&regionCode={regionCode}&key={apiKey}";

        _logger.LogInformation("Fetching top trending YouTube video for region {Region}", regionCode);

        using var response = await _httpClient.GetAsync(url);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            _logger.LogError("YouTube API returned {Status}: {Error}", response.StatusCode, error);
            throw new HttpRequestException($"Failed to fetch trending video. Status: {response.StatusCode}. Body: {error}");
        }

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        if (!json.TryGetProperty("items", out var items) || items.GetArrayLength() == 0)
        {
            throw new InvalidOperationException("No trending videos returned by YouTube API.");
        }

        var videoId = items[0].GetProperty("id").GetString();
        if (string.IsNullOrWhiteSpace(videoId))
        {
            throw new InvalidOperationException("YouTube API response missing video id.");
        }

        var videoUrl = $"https://www.youtube.com/watch?v={videoId}";
        _logger.LogInformation("Selected trending video: {VideoUrl}", videoUrl);
        return videoUrl;
    }

    /// <summary>
    /// Returns the most-viewed recent video for a specific channel (uses search ordered by viewCount).
    /// </summary>
    public async Task<string> GetChannelTopVideoUrlAsync(string channelId, int maxResults = 5)
    {
        var apiKey = _configuration["YouTubeSettings:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException("YouTube API key is missing in configuration (YouTubeSettings:ApiKey).");
        }

        if (string.IsNullOrWhiteSpace(channelId))
        {
            throw new ArgumentException("channelId is required", nameof(channelId));
        }

        // Use search endpoint to find most-viewed videos from the channel
        var url =
            $"{ApiBase}/search?part=snippet&channelId={channelId}&maxResults={maxResults}&order=viewCount&type=video&videoDuration=short&key={apiKey}";

        _logger.LogInformation("Fetching top viewed videos for channel {ChannelId}", channelId);

        using var response = await _httpClient.GetAsync(url);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            _logger.LogError("YouTube API channel search returned {Status}: {Error}", response.StatusCode, error);
            throw new HttpRequestException($"Failed to fetch channel videos. Status: {response.StatusCode}. Body: {error}");
        }

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        if (!json.TryGetProperty("items", out var items) || items.GetArrayLength() == 0)
        {
            throw new InvalidOperationException($"No videos returned for channel {channelId}.");
        }

        var videoId = items[0].GetProperty("id").GetProperty("videoId").GetString();
        if (string.IsNullOrWhiteSpace(videoId))
        {
            throw new InvalidOperationException("YouTube channel search response missing video id.");
        }

        var videoUrl = $"https://www.youtube.com/watch?v={videoId}";
        _logger.LogInformation("Selected channel top video: {VideoUrl}", videoUrl);
        return videoUrl;
    }

    /// <summary>
    /// Returns a list of top most-viewed short videos (duration < 60 seconds) in India, sorted by view count.
    /// </summary>
    /// <param name="count">Number of videos to return (max 50).</param>
    /// <returns>List of YouTube video URLs.</returns>
    public async Task<List<string>> GetTopShortVideosAsync(int count = 50)
    {
        try
        {
            _logger.LogInformation("Fetching top {Count} most-viewed short videos in India", count);

            if (count > 50) count = 50;
            if (count < 1) count = 1;

            // Search for videos in India ordered by view count
            var searchRequest = _youtubeService.Search.List("snippet");
            searchRequest.Type = "video";
            searchRequest.ChannelId = "UC_x5XG1OV2P6uZZ5FSM9Ttw";
            searchRequest.Type = "video";
            searchRequest.MaxResults =  count;
            searchRequest.Order = SearchResource.ListRequest.OrderEnum.ViewCount;

            var searchResponse = await searchRequest.ExecuteAsync();

            if (searchResponse.Items == null || !searchResponse.Items.Any())
            {
                _logger.LogWarning("No videos found in search");
                return new List<string>();
            }

            // Get video IDs
            var videoIds = string.Join(",", searchResponse.Items.Select(item => item.Id.VideoId));

            // Get detailed video information
            var videosRequest = _youtubeService.Videos.List("snippet,contentDetails,statistics");
            videosRequest.Id = videoIds;

            var videosResponse = await videosRequest.ExecuteAsync();

            var shortVideos = new List<(string videoId, long viewCount)>();

            foreach (var video in videosResponse.Items)
            {
                var duration = ParseDuration(video.ContentDetails.Duration);

                // Filter for shorts (60 seconds or less)
                // if (duration <= 60 && duration > 0)
                // {
                    var viewCount = (long)(video.Statistics.ViewCount ?? 0);
                    shortVideos.Add((video.Id, viewCount));
                // }
            }

            // Sort by view count descending, take top count
            var topShorts = shortVideos
                .OrderByDescending(v => v.viewCount)
                .Take(count)
                .Select(v => $"https://www.youtube.com/watch?v={v.videoId}")
                .ToList();

            _logger.LogInformation("Found {Count} most-viewed short videos in India", topShorts.Count);
            return topShorts;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching top shorts");
            throw;
        }
    }

    private static int ParseDuration(string duration)
    {
        // YouTube duration format: PT1M30S (1 minute 30 seconds)
        if (string.IsNullOrWhiteSpace(duration) || !duration.StartsWith("PT")) return 0;

        var time = duration.Substring(2); // Remove PT
        var seconds = 0;

        var match = System.Text.RegularExpressions.Regex.Match(time, @"(\d+H)?(\d+M)?(\d+S)?");
        if (match.Success)
        {
            if (int.TryParse(match.Groups[1].Value.TrimEnd('H'), out var hours)) seconds += hours * 3600;
            if (int.TryParse(match.Groups[2].Value.TrimEnd('M'), out var minutes)) seconds += minutes * 60;
            if (int.TryParse(match.Groups[3].Value.TrimEnd('S'), out var secs)) seconds += secs;
        }

        return seconds;
    }

    public async Task<List<YouTubeVideo>> GetGlobalTrendingShortsAsync(int maxResults = 50)
    {
        try
        {
            _logger.LogInformation("Fetching globally trending shorts");

            // Try multiple search strategies to find trending content
            var allVideos = new List<YouTubeVideo>();

            // Strategy 1: Search for popular videos from last 30 days (more time range)
            var searchRequest1 = _youtubeService.Search.List("snippet");
            searchRequest1.Type = "video";
            searchRequest1.MaxResults = Math.Min(maxResults * 3, 50); // Get more results
            searchRequest1.Order = SearchResource.ListRequest.OrderEnum.ViewCount;
            searchRequest1.PublishedAfter = DateTime.UtcNow.AddDays(-30); // Last 30 days
            searchRequest1.RegionCode = "US";
            searchRequest1.RelevanceLanguage = "en";

            var searchResponse1 = await searchRequest1.ExecuteAsync();
            _logger.LogInformation("Strategy 1 found {Count} videos from search", searchResponse1.Items?.Count ?? 0);

            if (searchResponse1.Items != null && searchResponse1.Items.Any())
            {
                var videoIds1 = string.Join(",", searchResponse1.Items.Select(item => item.Id.VideoId));
                var videos1 = await GetVideoDetailsBatchAsync(videoIds1);
                allVideos.AddRange(videos1);
            }

            // Strategy 2: Search without region/language restrictions
            if (allVideos.Count < maxResults)
            {
                var searchRequest2 = _youtubeService.Search.List("snippet");
                searchRequest2.Type = "video";
                searchRequest2.MaxResults = Math.Min(maxResults * 2, 50);
                searchRequest2.Order = SearchResource.ListRequest.OrderEnum.Relevance; // Try relevance instead of view count
                searchRequest2.PublishedAfter = DateTime.UtcNow.AddDays(-14); // Last 14 days
                searchRequest2.Q = "shorts"; // Search for "shorts" keyword

                var searchResponse2 = await searchRequest2.ExecuteAsync();
                _logger.LogInformation("Strategy 2 found {Count} videos from search", searchResponse2.Items?.Count ?? 0);

                if (searchResponse2.Items != null && searchResponse2.Items.Any())
                {
                    var videoIds2 = string.Join(",", searchResponse2.Items.Select(item => item.Id.VideoId));
                    var videos2 = await GetVideoDetailsBatchAsync(videoIds2);
                    allVideos.AddRange(videos2);
                }
            }

            // Strategy 3: Search for videos with high engagement (likes + comments)
            if (allVideos.Count < maxResults)
            {
                var searchRequest3 = _youtubeService.Search.List("snippet");
                searchRequest3.Type = "video";
                searchRequest3.MaxResults = Math.Min(maxResults * 2, 50);
                searchRequest3.Order = SearchResource.ListRequest.OrderEnum.Relevance;
                searchRequest3.PublishedAfter = DateTime.UtcNow.AddDays(-7);
                searchRequest3.Q = "viral"; // Search for viral content

                var searchResponse3 = await searchRequest3.ExecuteAsync();
                _logger.LogInformation("Strategy 3 found {Count} videos from search", searchResponse3.Items?.Count ?? 0);

                if (searchResponse3.Items != null && searchResponse3.Items.Any())
                {
                    var videoIds3 = string.Join(",", searchResponse3.Items.Select(item => item.Id.VideoId));
                    var videos3 = await GetVideoDetailsBatchAsync(videoIds3);
                    allVideos.AddRange(videos3);
                }
            }

            // Remove duplicates and filter for shorts
            var uniqueVideos = allVideos
                .GroupBy(v => v.VideoId)
                .Select(g => g.First())
                .Where(v => v.DurationSeconds <= _settings.MaxVideoDurationSeconds && v.DurationSeconds > 0)
                .OrderByDescending(v => v.ViewCount)
                .ThenByDescending(v => v.LikeCount + v.CommentCount) // Secondary sort by engagement
                .Take(maxResults)
                .ToList();

            _logger.LogInformation("Found {Count} globally trending shorts after filtering", uniqueVideos.Count);

            return uniqueVideos;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching globally trending shorts");
            throw;
        }
    }
 private async Task<List<YouTubeVideo>> GetVideoDetailsBatchAsync(string videoIds)
    {
        try
        {
            var videosRequest = _youtubeService.Videos.List("snippet,contentDetails,statistics");
            videosRequest.Id = videoIds;

            var videosResponse = await videosRequest.ExecuteAsync();

            var videos = new List<YouTubeVideo>();

            foreach (var video in videosResponse.Items)
            {
                var duration = ParseDuration(video.ContentDetails.Duration);

                videos.Add(new YouTubeVideo
                {
                    VideoId = video.Id,
                    Title = video.Snippet.Title,
                    Description = video.Snippet.Description,
                    ChannelId = video.Snippet.ChannelId,
                    ChannelTitle = video.Snippet.ChannelTitle,
                    ViewCount = (long)(video.Statistics.ViewCount ?? 0),
                    LikeCount = (long)(video.Statistics.LikeCount ?? 0),
                    CommentCount = (long)(video.Statistics.CommentCount ?? 0),
                    ThumbnailUrl = video.Snippet.Thumbnails.Default__.Url,
                    ThumbnailHighResUrl = video.Snippet.Thumbnails.High?.Url ?? video.Snippet.Thumbnails.Default__.Url,
                    DurationSeconds = duration,
                    PublishedAt = video.Snippet.PublishedAt ?? DateTime.UtcNow,
                    FetchedAt = DateTime.UtcNow,
                    Tags = video.Snippet.Tags?.ToList() ?? new List<string>()
                });
            }

            return videos;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting video details batch: {VideoIds}", videoIds);
            return new List<YouTubeVideo>();
        }
    }

  public async Task<List<YouTubeVideo>> GetTrendingShortsAsync(string? channelId = null, DateTime? publishedAfter = null)
    {
        try
        {
            _logger.LogInformation("Fetching trending shorts from channel: {ChannelId}", channelId);

            var searchRequest = _youtubeService.Search.List("snippet");
            searchRequest.ChannelId = channelId;
            searchRequest.Type = "video";
            searchRequest.MaxResults = _settings.MaxResults;
            searchRequest.Order = SearchResource.ListRequest.OrderEnum.Date; // Most recent first

            if (publishedAfter.HasValue)
            {
                searchRequest.PublishedAfter = publishedAfter.Value;
            }

            var searchResponse = await searchRequest.ExecuteAsync();

            if (searchResponse.Items == null || !searchResponse.Items.Any())
            {
                return new List<YouTubeVideo>();
            }

            var videoIds = string.Join(",", searchResponse.Items.Select(item => item.Id.VideoId));

            var videosRequest = _youtubeService.Videos.List("snippet,contentDetails,statistics");
            videosRequest.Id = videoIds;

            var videosResponse = await videosRequest.ExecuteAsync();

            var videos = new List<YouTubeVideo>();

            foreach (var video in videosResponse.Items)
            {
                var duration = ParseDuration(video.ContentDetails.Duration);

                if (duration <= _settings.MaxVideoDurationSeconds)
                {
                    videos.Add(new YouTubeVideo
                    {
                        VideoId = video.Id,
                        Title = video.Snippet.Title,
                        Description = video.Snippet.Description,
                        ChannelId = video.Snippet.ChannelId,
                        ChannelTitle = video.Snippet.ChannelTitle,
                        ViewCount = (long)(video.Statistics.ViewCount ?? 0),
                        LikeCount = (long)(video.Statistics.LikeCount ?? 0),
                        CommentCount = (long)(video.Statistics.CommentCount ?? 0),
                        ThumbnailUrl = video.Snippet.Thumbnails.Default__.Url,
                        ThumbnailHighResUrl = video.Snippet.Thumbnails.High?.Url ?? video.Snippet.Thumbnails.Default__.Url,
                        DurationSeconds = duration,
                        PublishedAt = video.Snippet.PublishedAt ?? DateTime.UtcNow,
                        FetchedAt = DateTime.UtcNow,
                        Tags = video.Snippet.Tags?.ToList() ?? new List<string>()
                    });
                }
            }

            // Sort by view count for trending
            videos = videos.OrderByDescending(v => v.ViewCount).ToList();

            return videos;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching trending shorts from channel: {ChannelId}", channelId);
            throw;
        }
    }

}
