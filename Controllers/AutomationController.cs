using System.Data;
using InstaAutoPost.Models;
using InstaAutoPost.Services;
using Microsoft.AspNetCore.Mvc;

namespace InstaAutoPost.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AutomationController : ControllerBase
{
    private readonly YouTubeDownloadService _downloadService;
    private readonly InstagramService _instagramService;
    private readonly YouTubeTrendingService _trendingService;
    private readonly ILogger<AutomationController> _logger;

    public AutomationController(
        YouTubeDownloadService downloadService,
        InstagramService instagramService,
        YouTubeTrendingService trendingService,
        ILogger<AutomationController> logger)
    {
        _downloadService = downloadService;
        _instagramService = instagramService;
        _trendingService = trendingService;
        _logger = logger;
    }



    /// <summary>
    /// Fetches a trending YouTube short (global or by channel) and posts it as an Instagram Reel.
    /// </summary>
    [HttpPost("post-trending")]
    [ProducesResponseType(typeof(PostResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<PostResponseDto>> PostTrendingVideo([FromBody] PostRequestDto request)
    {
        try
        {
             string videoUrlToDownload;

            if (!string.IsNullOrWhiteSpace(request.YoutubeUrl))
            {
                videoUrlToDownload = request.YoutubeUrl!;
                _logger.LogInformation("Using provided YouTube URL: {Url}", videoUrlToDownload);
            }
            else if (request.UseGlobalTrending || string.IsNullOrWhiteSpace(request.ChannelId))
            {
                // Get top most-viewed short videos in India and select the top one
                // var topShorts = await _trendingService.GetTopShortVideosAsync(50);
                // videoUrlToDownload = topShorts.FirstOrDefault() ?? throw new InvalidOperationException("No short videos available.");
                // _logger.LogInformation("Using top most-viewed short video in India: {Url}", videoUrlToDownload);
             var shorts = request.UseGlobalTrending || string.IsNullOrEmpty(request.ChannelId)
                ? await _trendingService.GetGlobalTrendingShortsAsync(10)
                : await _trendingService.GetTrendingShortsAsync(request.ChannelId!, null);
                videoUrlToDownload = shorts[0].VideoUrl.ToString() ?? throw new InvalidOperationException("No short videos available.");

            }
            else
            {
                try
                {
                    videoUrlToDownload = await _trendingService.GetChannelTopVideoUrlAsync(request.ChannelId!);
                    _logger.LogInformation("Using channel ({ChannelId}) top video: {Url}", request.ChannelId, videoUrlToDownload);
                }
                catch (InvalidOperationException ex)
                {
                    _logger.LogWarning(ex, "No videos found for channel {ChannelId}, falling back to global trending", request.ChannelId);
                    videoUrlToDownload = await _trendingService.GetTopTrendingVideoUrlAsync();
                }
            }

            // Step 1: Download video
            var videoPath = await _downloadService.DownloadAndUploadToDropboxAsync(videoUrlToDownload);
            var fileName = Path.GetFileName(videoPath);
            var videoUrl = $"/temp/{fileName}";

            _logger.LogInformation("Video downloaded, serving from: {Url}", videoUrl);

            // Step 2: Post to Instagram
            var reelId = await _instagramService.PostReelAsync(videoPath, request.Caption);

            return Ok(new PostResponseDto
            {
                Success = true,
                ReelId = reelId,
                Message = "Reel posted successfully"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error posting reel");
            return Problem(
                detail: ex.Message,
                statusCode: StatusCodes.Status500InternalServerError,
                title: "Error posting reel");
        }
    }
}
