namespace Core.Entities;

public class YouTubeVideo
{
    public int Id { get; set; }
    public string VideoId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ChannelId { get; set; } = string.Empty;
    public string ChannelTitle { get; set; } = string.Empty;
    public long ViewCount { get; set; }
    public long LikeCount { get; set; }
    public long CommentCount { get; set; }
    public string ThumbnailUrl { get; set; } = string.Empty;
    public string ThumbnailHighResUrl { get; set; } = string.Empty;
    public int DurationSeconds { get; set; }
    public DateTime PublishedAt { get; set; }
    public DateTime FetchedAt { get; set; }
    public string VideoUrl => $"https://www.youtube.com/watch?v={VideoId}";
    public string ShortsUrl => $"https://www.youtube.com/shorts/{VideoId}";
    public bool IsShort => DurationSeconds <= 60;
    public List<string> Tags { get; set; } = new();
}
