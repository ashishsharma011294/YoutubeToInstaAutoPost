namespace InstaAutoPost.Models;

public record PostRequestDto
{
    /// <summary>
    /// Optional YouTube URL to post directly. If empty, service will fetch trending.
    /// </summary>
    public string? YoutubeUrl { get; init; }

    /// <summary>
    /// Optional channel ID to fetch the most viewed recent video from.
    /// </summary>
    public string? ChannelId { get; init; }

    /// <summary>
    /// Use global trending if true (overrides channel).
    /// </summary>
    public bool UseGlobalTrending { get; init; } = false;

    /// <summary>
    /// Caption to post with the reel.
    /// </summary>
    public string Caption { get; init; } = string.Empty;
}
