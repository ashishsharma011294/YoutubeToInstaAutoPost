using YoutubeDLSharp;
using YoutubeDLSharp.Options;

namespace InstaAutoPost.Services;

public class YouTubeDownloadService
{
    private readonly ILogger<YouTubeDownloadService> _logger;
    private readonly string _tempFolder;
    private readonly NgrokService _ngrokService;
    private readonly IConfiguration _configuration;

    public YouTubeDownloadService(ILogger<YouTubeDownloadService> logger, IWebHostEnvironment env, NgrokService ngrokService, IConfiguration configuration)
    {
        _logger = logger;
        _ngrokService = ngrokService;
        _configuration = configuration;
        
        _tempFolder = Path.Combine(env.WebRootPath ?? "wwwroot", "temp");
        if (!Directory.Exists(_tempFolder)) Directory.CreateDirectory(_tempFolder);
    }

    public async Task<string> DownloadAndProcessForInstagramAsync(string youtubeUrl)
    {
        string localPath = "";
        try
        {
            // Generate unique filename
            var videoId = ExtractVideoId(youtubeUrl);
            var outputFileName = $"{videoId}_{DateTime.Now:yyyyMMdd_HHmmss}.mp4";
            var outputPath = Path.Combine(_tempFolder, outputFileName);
            
            _logger.LogInformation("Downloading and processing video {VideoId} for Instagram...", videoId);
            
            // --- STEP 1: Download with Instagram-optimized settings ---
            var ytdl = new YoutubeDL
            {
                OutputFileTemplate = outputPath,
                YoutubeDLPath = "yt-dlp.exe",
                FFmpegPath = "ffmpeg.exe"
            };

            var options = new OptionSet
            {
                Format = "best[height<=1920]", // Max Instagram resolution
                MergeOutputFormat = DownloadMergeFormat.Mp4,
                NoPlaylist = true
            };

            // Instagram-compatible video processing
            options.AddCustomOption("--postprocessor-args", 
                "ffmpeg:" +
                "-vf 'scale=1080:1920:force_original_aspect_ratio=decrease,pad=1080:1920:(ow-iw)/2:(oh-ih)/2:black,fps=30' " +
                "-c:v libx264 -preset medium -crf 23 -pix_fmt yuv420p " +
                "-c:a aac -b:a 128k -ar 44100 " +
                "-movflags +faststart"); // Optimize for streaming

            // Add Deno/Node.js runtime if available
            var denoPath = GetJSRuntimePath();
            if (!string.IsNullOrEmpty(denoPath))
            {
                options.AddCustomOption("--js-runtimes", denoPath);
            }

            var result = await ytdl.RunVideoDownload(youtubeUrl, overrideOptions: options);
            if (!result.Success)
            {
                var errorOutput = result.ErrorOutput?.ToString() ?? "Unknown error";
                _logger.LogError("Video download failed: {Error}", errorOutput);
                throw new InvalidOperationException($"Failed to download video: {errorOutput}");
            }

            // Find the downloaded file
            if (!File.Exists(outputPath))
            {
                // Fallback: find the latest MP4 file
                var files = Directory.GetFiles(_tempFolder, "*.mp4")
                    .OrderByDescending(f => new FileInfo(f).CreationTime)
                    .ToList();
                
                if (files.Any())
                {
                    outputPath = files.First();
                }
                else
                {
                    throw new InvalidOperationException("No video file was created");
                }
            }

            localPath = outputPath;
            var fileName = Path.GetFileName(outputPath);
            
            // --- STEP 2: Generate public URL using Ngrok ---
            var ngrokBaseUrl = await _ngrokService.GetNgrokBaseUrlAsync();
            if (string.IsNullOrEmpty(ngrokBaseUrl))
            {
                throw new InvalidOperationException("Ngrok URL not available. Make sure Ngrok is running.");
            }

            var publicVideoUrl = $"{ngrokBaseUrl}/temp/{fileName}";
            
            _logger.LogInformation("Video processed successfully: {FileName}", fileName);
            _logger.LogInformation("Public video URL: {Url}", publicVideoUrl);
            
            return publicVideoUrl;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing video for Instagram");
            
            // Clean up partial file if it exists
            if (!string.IsNullOrEmpty(localPath) && File.Exists(localPath))
            {
                try { File.Delete(localPath); } catch { }
            }
            
            throw;
        }
    }

    private string ExtractVideoId(string youtubeUrl)
    {
        try
        {
            var uri = new Uri(youtubeUrl);
            
            // Handle different YouTube URL formats
            if (uri.Host.Contains("youtu.be"))
            {
                // Short URL format: https://youtu.be/VIDEO_ID
                return uri.Segments.LastOrDefault()?.TrimEnd('/') ?? "unknown";
            }
            else
            {
                // Standard URL format: https://www.youtube.com/watch?v=VIDEO_ID
                var queryString = uri.Query;
                if (queryString.StartsWith("?"))
                {
                    var pairs = queryString.Substring(1).Split('&');
                    foreach (var pair in pairs)
                    {
                        var keyValue = pair.Split('=');
                        if (keyValue.Length == 2 && keyValue[0] == "v")
                        {
                            return keyValue[1];
                        }
                    }
                }
            }
            
            return $"video_{DateTime.Now:yyyyMMddHHmmss}";
        }
        catch
        {
            return $"video_{DateTime.Now:yyyyMMddHHmmss}";
        }
    }
    
    private string GetJSRuntimePath()
    {
        try
        {
            // Check for executables in project directory
            var projectDir = AppContext.BaseDirectory;
            var possibleRuntimes = new[]
            {
                Path.Combine(projectDir, "deno.exe"),
                Path.Combine(Directory.GetCurrentDirectory(), "deno.exe")
            };

            foreach (var runtime in possibleRuntimes)
            {
                if (File.Exists(runtime))
                {
                    _logger.LogInformation("Found JS runtime: {Runtime}", runtime);
                    return $"deno:{runtime}";
                }
            }

            // Try system PATH
            var pathEnv = Environment.GetEnvironmentVariable("PATH");
            if (!string.IsNullOrEmpty(pathEnv))
            {
                var pathDirs = pathEnv.Split(';');
                foreach (var dir in pathDirs)
                {
                    var denoPath = Path.Combine(dir.Trim(), "deno.exe");
                    if (File.Exists(denoPath))
                    {
                        _logger.LogInformation("Found Deno in PATH: {Path}", denoPath);
                        return $"deno:{denoPath}";
                    }
                    
                    var nodePath = Path.Combine(dir.Trim(), "node.exe");
                    if (File.Exists(nodePath))
                    {
                        _logger.LogInformation("Found Node.js in PATH: {Path}", nodePath);
                        return $"node:{nodePath}";
                    }
                }
            }

            _logger.LogWarning("No JS runtime found. Downloads may fail for some videos.");
            return string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error finding JS runtime");
            return string.Empty;
        }
    }

    /// <summary>
    /// Cleanup old video files to prevent disk space issues
    /// </summary>
    public void CleanupOldVideos(int maxAgeMinutes = 60)
    {
        try
        {
            var files = Directory.GetFiles(_tempFolder, "*.mp4")
                .Where(f => File.GetCreationTime(f) < DateTime.Now.AddMinutes(-maxAgeMinutes))
                .ToList();

            foreach (var file in files)
            {
                try
                {
                    File.Delete(file);
                    _logger.LogInformation("Cleaned up old video: {File}", Path.GetFileName(file));
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to delete old video: {File}", Path.GetFileName(file));
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during video cleanup");
        }
    }
}
