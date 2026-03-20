using Dropbox.Api;
using Dropbox.Api.Files;
using Dropbox.Api.Sharing;
using YoutubeDLSharp;
using YoutubeDLSharp.Options;

public class YouTubeDownloadService
{
    private readonly ILogger<YouTubeDownloadService> _logger;
    private readonly string _tempFolder;
    private readonly string _dropboxAccessToken = "sl.u.AGQuygP2hElsiq_w-JcmVmTk_tuvmwqB1YERhcvWsCJM4HEc_rFxjf7zAEpEZzy_G-hNRQCLVsvPLt_v4czcRm2cyXGZY9evfB1Frkh9qFDgiS4VuDsxOH5EQ18NWpPvdqDNLhtr8b-NTFZbtztz5i3rpuXgGfKBm83Vsn8qifqwczKgw9-0QhhkCYPGzdSnvl5p3V1ftc87SeHJ6KhlLcmEsT0LC1rRVygSAzoBDLM-QJATHDY_z1qjrwqpwfZCsNuYtDqFE0cwYmivD0sob50Of0pkgkt9fNJvu6RAmF07WHsRBfzQiy8r_sYPvFCAezoTb18Xgx4kpKc8NqXx0wHoH2FF3uHPuqiOV7LQJUiSrCn0QIJmwdgSHw63oeiB5hfOsy1tnXLm_EiDRSuBOJRxlhD6HQpUpntbdr45oH6uwR6CXHOo5nA0t15tCmkY8Y-3E8xat4Jjri3QAHxHv6YnBns0F3_Sp0DwaQp2PBlhZ4iGqaSyTS3ihLL5CEnQAphnxvpJMbdZqwi9DtUN4aMCmaiYMKZnuXjpk3RtD61tzrRjjFK3GwO8puFKdh1BHE90aCntE04Tm3O7te63PiheEuH-J7pNS7kpUnkehRAXT6JM-1Pokor8PtfbAw67tfQ2B2dg90m2P1EqN4a5DSwLZi3qw3yk3WfASSsX21lUamRWm8axwyMKF02PjxepQZMQtRTn41CV5x1nbon4rpPuBSuZuZ8ZA7gtpu8NfapTJ0UWyWWea1GbFxqVzlRZ6pCbKuFKFLk4wvinZ6dXCbZnxRD7vtmLhjR4NzdwDNvKYGnMK0nZ4y0nxVre8_x-4wL5EW2A_aU4y__sVU0KaCB_xyC2JtBx9OeKAdNjCx3r-pIVDhPVkBxGht5VsNUdlLN-k1iPCA1rnWt-hgV2farwBJOFcYDP7Cm6VXAgmauDI_7BY0YTfS6-VmiET-VTMpCAyPWvROBWAyMustgtHLnh9jS91VzLAAn1dzfWAz28rmqVQan7QZCS2kkWao1JdtP5gJ6EIGzyMe-gwZ_DH6R0HkgnzF6HuVbg789-HGO1FCu9ARunHXalshlFMoH8cAjwipb2QqzfUXxau5fzgM5xTdc-XmVbhXgjyL2kKFxfCQUzRnW1Hhol-mXeHA-Fb9-7b9QK0Gkd-EFJyOSuPWltOn2X0Qy6L6GGsnltF6TuL-xsXnlYAcoygnEgesMn2p72Zh2N3OXuWHOJDjKG-VOT11P2vAOBP_eRfDFj2BnDJU7HWmfd6uP-o5Lj65INmKBKXdV82RD0QJwxscC0TvnCNrw6la6edJlSa565_fC8dA";
    private readonly string _dropboxFolderPath = "/insta"; // Dropbox path starts with /

    public YouTubeDownloadService(ILogger<YouTubeDownloadService> logger, IWebHostEnvironment env)
    {
        _logger = logger;
        _tempFolder = Path.Combine(env.WebRootPath ?? "wwwroot", "temp");
        if (!Directory.Exists(_tempFolder)) Directory.CreateDirectory(_tempFolder);
    }

    public async Task<string> DownloadAndUploadToDropboxAsync(string youtubeUrl)
    {
        string localPath = "";
        try
        {
            // --- STEP 1: Download locally first ---
            var ytdl = new YoutubeDL
            {
                OutputFileTemplate = Path.Combine(_tempFolder, "%(id)s.%(ext)s"),
                YoutubeDLPath = "yt-dlp.exe",
                FFmpegPath = "ffmpeg.exe"
            };

            // Get Deno path for yt-dlp JavaScript runtime (embedded portable JS engine)
            string denoPath = GetDenoPath();
                            
            var options = new OptionSet
                {
                    Format = "bestvideo+bestaudio/best",
                    MergeOutputFormat = DownloadMergeFormat.Mp4,
                    NoPlaylist = true,
                    JsRuntimes = "node"
                };

            // 🔥 FORCE INSTAGRAM FORMAT
            options.AddCustomOption("--recode-video", "mp4");

            options.AddCustomOption("--postprocessor-args",
            "ffmpeg:-vf scale=1080:1920:force_original_aspect_ratio=decrease," +
            "pad=1080:1920:(ow-iw)/2:(oh-ih)/2,fps=30 " +
            "-c:v libx264 -preset medium -crf 23 -pix_fmt yuv420p " +
            "-c:a aac -b:a 128k");
            // Add Deno runtime with full path - required for YouTube signature cipher decryption
            if (!string.IsNullOrEmpty(denoPath))
            {
                options.AddCustomOption("--js-runtimes", $"deno:{denoPath}");
                _logger.LogInformation("Using Deno JS runtime at: {DenoPath}", denoPath);
            }
            else
            {
                // Fallback to auto-detection (may not work if not in PATH)
                options.AddCustomOption("--js-runtimes", "deno");
                _logger.LogWarning("Deno path not found, attempting auto-detection");
            }




            var result = await ytdl.RunVideoDownload(youtubeUrl, overrideOptions: options);
            if (!result.Success)
            {
                var errorOutput = result.ErrorOutput?.ToString() ?? "No error output";
                _logger.LogError("yt-dlp download failed for URL: {Url}. Error: {Error}", youtubeUrl, errorOutput);
                throw new Exception($"YouTube download failed: {errorOutput}");
            }

             string directUrl="";
            try
            {
            // Find the file
            localPath = Directory.GetFiles(_tempFolder, "*.mp4")
                        .OrderByDescending(f => new FileInfo(f).CreationTime).First();
            string fileName = Path.GetFileName(localPath);

    // --- STEP 2: Upload to Dropbox ---
    using var dbx = new DropboxClient(_dropboxAccessToken);
    _logger.LogInformation("Uploading {FileName} to Dropbox...", fileName);
   
    using (var stream = new FileStream(localPath, FileMode.Open))
    {
        var uploadPath = $"{_dropboxFolderPath}/{fileName}";
        await dbx.Files.UploadAsync(uploadPath, WriteMode.Overwrite.Instance, body: stream);
           // --- STEP 3: Create Shared Link ---
            _logger.LogInformation("Generating shared link...");
                    SharedLinkMetadata sharedLink;
                    try
                    {
                        sharedLink = await dbx.Sharing.CreateSharedLinkWithSettingsAsync(uploadPath);
                    }
                    catch (ApiException<CreateSharedLinkWithSettingsError>)
                    {
                        var links = await dbx.Sharing.ListSharedLinksAsync(uploadPath);
                        sharedLink = links.Links.First();
                    }
                      directUrl = sharedLink.Url.Replace("dl=0", "dl=1");
                }
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Error uploading to Dropbox");
                throw;
            }
            

            
            _logger.LogInformation("Success! Direct URL: {Url}", directUrl);
            return directUrl;
        }
        finally
        {
            // Cleanup local temp file
            if (!string.IsNullOrEmpty(localPath) && File.Exists(localPath))
                File.Delete(localPath);
        }
    }

    /// <summary>
    /// Gets the full path to Deno executable (embedded portable JS runtime)
    /// </summary>
    private string GetDenoPath()
    {
        try
        {
            // Check for Deno in project directory (embedded portable version)
            var projectDenoPath = Path.Combine(AppContext.BaseDirectory, "deno.exe");
            if (File.Exists(projectDenoPath))
            {
                _logger.LogInformation("Found embedded Deno at: {Path}", projectDenoPath);
                return projectDenoPath;
            }

            // Check current working directory
            var currentDirDeno = Path.Combine(Directory.GetCurrentDirectory(), "deno.exe");
            if (File.Exists(currentDirDeno))
            {
                _logger.LogInformation("Found Deno in current directory: {Path}", currentDirDeno);
                return currentDirDeno;
            }

            // Check common Deno installation paths
            var possiblePaths = new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".deno", "bin", "deno.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "deno", "deno.exe"),
                @"C:\Program Files\deno\deno.exe",
                @"C:\Program Files (x86)\deno\deno.exe"
            };

            foreach (var path in possiblePaths)
            {
                if (File.Exists(path))
                {
                    _logger.LogInformation("Found Deno at: {Path}", path);
                    return path;
                }
            }

            // Try to find deno in PATH
            var pathEnv = Environment.GetEnvironmentVariable("PATH");
            if (!string.IsNullOrEmpty(pathEnv))
            {
                var pathDirs = pathEnv.Split(';');
                foreach (var dir in pathDirs)
                {
                    var denoExe = Path.Combine(dir.Trim(), "deno.exe");
                    if (File.Exists(denoExe))
                    {
                        _logger.LogInformation("Found Deno in PATH: {Path}", denoExe);
                        return denoExe;
                    }
                }
            }

            _logger.LogWarning("Deno executable not found. YouTube downloads may fail.");
            return string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error finding Deno path");
            return string.Empty;
        }
    }

}
