using System.Net.Http.Json;
using System.Text.Json;

namespace InstaAutoPost.Services;

public class InstagramService
{
    private readonly ILogger<InstagramService> _logger;
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly NgrokService _ngrokService;

    private const string GraphApiBaseUrl = "https://graph.facebook.com/v24.0";

    public InstagramService(
        ILogger<InstagramService> logger,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        NgrokService ngrokService)
    {
        _logger = logger;
        _httpClient = httpClientFactory.CreateClient();
        _configuration = configuration;
        _ngrokService = ngrokService;
    }

    public async Task<string> PostReelAsync(string videoUrl, string caption)
    {
        var businessId = _configuration["InstagramSettings:BusinessId"];
        var accessToken = _configuration["InstagramSettings:AccessToken"];

        if (string.IsNullOrEmpty(businessId) || string.IsNullOrEmpty(accessToken))
        {
            throw new InvalidOperationException("Instagram BusinessId or AccessToken not configured");
        }

        try
        {
            // 1. MODIFIED DROPBOX URL (Crucial change: dl=0 -> raw=1)
            string directDropboxUrl = "https://www.dropbox.com/scl/fi/9vzdnijp9j75b7m8ufq81/VID_290280910_084305_617.mp4?rlkey=ihl1mxbsghshpdj84mgoehc0h&st=886ah3lo&dl=1";

            // Step 1: Create media container
            var createMediaUrl = $"https://graph.facebook.com/v24.0/{businessId}/media";
            var createMediaPayload = new
            {
                media_type = "REELS",
                video_url = videoUrl,
                caption = caption,
                access_token = accessToken
            };

            _logger.LogInformation("Creating media container for video: {VideoUrl}", videoUrl);
            var createResponse = await _httpClient.PostAsJsonAsync(createMediaUrl, createMediaPayload);
            if (!createResponse.IsSuccessStatusCode)
            {
                var error = await createResponse.Content.ReadAsStringAsync();
                throw new HttpRequestException($"Container Creation Failed: {error}");
            }

            var createResult = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
            var creationId = createResult.GetProperty("id").GetString();

            // Step 2: Poll for status instead of just waiting
            _logger.LogInformation("Container {Id} created. Polling for 'FINISHED' status...", creationId);

            bool isReady = false;
            int attempts = 0;
            while (!isReady && attempts < 10) // Max 10 attempts (about 2 minutes)
            {
                await Task.Delay(TimeSpan.FromSeconds(15));
                var statusResponse = await _httpClient.GetAsync($"https://graph.facebook.com/v24.0/{creationId}?fields=status_code&access_token={accessToken}");
                var statusData = await statusResponse.Content.ReadFromJsonAsync<JsonElement>();
                string status = statusData.GetProperty("status_code").GetString();

                if (status == "FINISHED")
                {
                    isReady = true;
                }
                else if (status == "ERROR")
                {
                    if (statusData.TryGetProperty("failure_reason", out JsonElement reason))
                        {
                            string errorDetail = reason.GetString();
                            Console.WriteLine($"Error Reason: {errorDetail}");
                        }
                    throw new Exception("Transcoding failed on Instagram's server.");
                }
                attempts++;
            }

            // Step 3: Publish the reel
            // IMPORTANT: Use the BUSINESS ID (account ID) here, not the container ID in the URL
            var publishUrl = $"https://graph.facebook.com/v24.0/{businessId}/media_publish";
            var publishPayload = new
            {
                creation_id = creationId,
                access_token = accessToken // Move token into the body
            };

            _logger.LogInformation("Publishing reel...");
            var publishResponse = await _httpClient.PostAsJsonAsync(publishUrl, publishPayload);

            if (!publishResponse.IsSuccessStatusCode)
            {
                var errorContent = await publishResponse.Content.ReadAsStringAsync();
                _logger.LogError("Publish failed: {Error}", errorContent);
                throw new HttpRequestException($"Publish failed: {errorContent}");
            }

            var publishResult = await publishResponse.Content.ReadFromJsonAsync<JsonElement>();
            return publishResult.GetProperty("id").GetString() ?? string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Instagram Post Failed");
            throw;
        }
    }
}
