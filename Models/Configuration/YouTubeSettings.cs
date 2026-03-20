namespace Infrastructure.Configuration;

public class YouTubeSettings
{
    public string ApiKey { get; set; } = string.Empty;
    public string ChannelId { get; set; } = string.Empty;
    public int MaxResults { get; set; } = 50;
    public int MaxVideoDurationSeconds { get; set; } = 60;
}

public class InstagramSettings
{
    public string AccessToken { get; set; } = string.Empty;
    public string BusinessAccountId { get; set; } = string.Empty;
    public int MaxRetries { get; set; } = 3;
    public int RetryDelaySeconds { get; set; } = 5;
}

public class VideoProcessingSettings
{
    public string DownloadPath { get; set; } = "./downloads";
    public string ThumbnailPath { get; set; } = "./thumbnails";
    public int MaxVideoDurationSeconds { get; set; } = 60;
    public string FFmpegPath { get; set; } = "ffmpeg";
}
