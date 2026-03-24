using InstaAutoPost.Services;

namespace InstaAutoPost.Services;

/// <summary>
/// Background service that periodically cleans up old video files to prevent disk space issues
/// </summary>
public class VideoCleanupService : BackgroundService
{
    private readonly ILogger<VideoCleanupService> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly TimeSpan _cleanupInterval = TimeSpan.FromMinutes(30); // Run every 30 minutes
    private readonly int _maxVideoAgeMinutes = 60; // Delete videos older than 1 hour

    public VideoCleanupService(ILogger<VideoCleanupService> logger, IServiceProvider serviceProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Video cleanup service started");
        
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Wait before first cleanup
                await Task.Delay(_cleanupInterval, stoppingToken);
                
                // Create scope to get the download service
                using var scope = _serviceProvider.CreateScope();
                var downloadService = scope.ServiceProvider.GetRequiredService<YouTubeDownloadService>();
                
                _logger.LogInformation("Running video cleanup...");
                downloadService.CleanupOldVideos(_maxVideoAgeMinutes);
                _logger.LogInformation("Video cleanup completed");
            }
            catch (OperationCanceledException)
            {
                // Service is stopping
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during video cleanup");
                
                // Wait a bit before retrying
                try
                {
                    await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
        
        _logger.LogInformation("Video cleanup service stopped");
    }
}