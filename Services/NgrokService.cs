using System.Net.Http.Json;

namespace InstaAutoPost.Services;

public class NgrokService : BackgroundService
{
    private readonly ILogger<NgrokService> _logger;
    private readonly IConfiguration _configuration;
    private readonly HttpClient _httpClient;
    private string _ngrokBaseUrl = string.Empty;
    private readonly object _lockObject = new object();

    public NgrokService(
        ILogger<NgrokService> logger,
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _configuration = configuration;
        _httpClient = httpClientFactory.CreateClient();
        
        // Initialize from config if available
        _ngrokBaseUrl = _configuration["InstagramSettings:NgrokBaseUrl"] ?? string.Empty;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            // Wait a bit for the app to start and Ngrok to be ready
            await Task.Delay(5000, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            // Service is stopping
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await UpdateNgrokUrlAsync();
                // Check every 5 minutes
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Service is stopping
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating Ngrok URL");
                try
                {
                    await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    // Service is stopping
                    break;
                }
            }
        }
    }

    private async Task UpdateNgrokUrlAsync()
    {
        try
        {
            var response = await _httpClient.GetFromJsonAsync<NgrokTunnelsResponse>(
                "http://127.0.0.1:4040/api/tunnels");

            if (response?.Tunnels != null && response.Tunnels.Any())
            {
                var publicUrl = response.Tunnels
                    .FirstOrDefault(t => t.Proto == "https" || t.Proto == "http")?
                    .PublicUrl;

                if (!string.IsNullOrEmpty(publicUrl))
                {
                    // Remove trailing slash if present
                    publicUrl = publicUrl.TrimEnd('/');
                    
                    // Update in-memory URL (thread-safe)
                    lock (_lockObject)
                    {
                        _ngrokBaseUrl = publicUrl;
                    }
                    
                    _logger.LogInformation("Ngrok URL updated: {Url}", publicUrl);
                }
                else
                {
                    _logger.LogWarning("No public URL found in Ngrok tunnels");
                }
            }
        }
        catch (HttpRequestException)
        {
            _logger.LogWarning("Could not connect to Ngrok API at http://127.0.0.1:4040/api/tunnels. Make sure Ngrok is running.");
        }
    }

    public async Task<string> GetNgrokBaseUrlAsync()
    {
        lock (_lockObject)
        {
            if (!string.IsNullOrEmpty(_ngrokBaseUrl))
            {
                return _ngrokBaseUrl;
            }
        }

        // Try to update if empty
        await UpdateNgrokUrlAsync();

        lock (_lockObject)
        {
            return _ngrokBaseUrl;
        }
    }
}

public class NgrokTunnelsResponse
{
    public List<NgrokTunnel> Tunnels { get; set; } = new();
}

public class NgrokTunnel
{
    [System.Text.Json.Serialization.JsonPropertyName("public_url")]
    public string PublicUrl { get; set; } = string.Empty;
    [System.Text.Json.Serialization.JsonPropertyName("proto")]
    public string Proto { get; set; } = string.Empty;
}
