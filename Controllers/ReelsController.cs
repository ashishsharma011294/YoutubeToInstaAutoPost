using InstaAutoPost.Models;
using InstaAutoPost.Services;
using Microsoft.AspNetCore.Mvc;

namespace InstaAutoPost.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ReelsController : ControllerBase
{
    private readonly YouTubeDownloadService _downloadService;
    private readonly InstagramService _instagramService;
    private readonly YouTubeTrendingService _trendingService;
    private readonly ILogger<ReelsController> _logger;

    public ReelsController(
        YouTubeDownloadService downloadService,
        InstagramService instagramService,
        YouTubeTrendingService trendingService,
        ILogger<ReelsController> logger)
    {
        _downloadService = downloadService;
        _instagramService = instagramService;
        _trendingService = trendingService;
        _logger = logger;
    }

    /// <summary>
    /// Download a YouTube video (provided URL or top trending) and post it as an Instagram Reel.
    /// </summary>
    [HttpPost("post")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> PostReel([FromBody] ReelRequest request)
    {
        try
        {
            string videoUrlToDownload;

            if (!string.IsNullOrWhiteSpace(request.YoutubeUrl))
            {
                videoUrlToDownload = request.YoutubeUrl;
                _logger.LogInformation("Using provided YouTube URL: {Url}", videoUrlToDownload);
            }
            else
            {
                videoUrlToDownload = await _trendingService.GetTopTrendingVideoUrlAsync();
                _logger.LogInformation("No URL provided, fetched top trending YouTube video: {Url}", videoUrlToDownload);
            }

            // Step 1: Download and process video for Instagram
            var videoUrl = await _downloadService.DownloadAndProcessForInstagramAsync(videoUrlToDownload);
            var fileName = Path.GetFileName(videoUrl.Split('/').Last());

            _logger.LogInformation("Video processed and ready: {FileName}", fileName);

            // Step 2: Post to Instagram
            var reelId = await _instagramService.PostReelAsync(videoUrl, request.Caption);

            return Ok(new
            {
                success = true,
                reelId,
                message = "Reel posted successfully"
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
