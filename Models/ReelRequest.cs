namespace InstaAutoPost.Models;

/// <summary>
/// Request model for posting a YouTube video as an Instagram Reel.
/// If YoutubeUrl is omitted or empty, the service will pick a top trending video.
/// </summary>
public record ReelRequest
{
    /// <summary>
    /// The YouTube video URL to download and post. Optional when using auto-trending.
    /// </summary>
    /// <example>https://www.youtube.com/watch?v=dQw4w9WgXcQ</example>
    public string? YoutubeUrl { get; init; }
    
    /// <summary>
    /// The caption to use for the Instagram Reel
    /// </summary>
    /// <example>Check out this amazing video! #shorts</example>
    public string Caption { get; init; } = string.Empty;
}
