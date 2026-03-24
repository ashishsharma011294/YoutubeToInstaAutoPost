using InstaAutoPost.Models;
using InstaAutoPost.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "InstaAutoPost API",
        Version = "v1",
        Description = "API for automatically posting YouTube videos as Instagram Reels",
        Contact = new Microsoft.OpenApi.Models.OpenApiContact
        {
            Name = "InstaAutoPost"
        }
    });
    
    // Include XML comments if available
    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        c.IncludeXmlComments(xmlPath);
    }
});
builder.Services.AddHttpClient();
builder.Services.AddSingleton<NgrokService>();
builder.Services.AddHostedService(provider => provider.GetRequiredService<NgrokService>());
builder.Services.AddHostedService<VideoCleanupService>();
builder.Services.AddScoped<YouTubeDownloadService>();
builder.Services.AddScoped<InstagramService>();
builder.Services.AddScoped<YouTubeTrendingService>();

var app = builder.Build();

// Configure the HTTP request pipeline
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "InstaAutoPost API v1");
    c.RoutePrefix = string.Empty; // Set Swagger UI at the app's root
});

app.UseHttpsRedirection();

// Enable static files from wwwroot/temp
app.UseStaticFiles();

// Health check endpoint
app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }))
    .WithName("HealthCheck")
    .WithTags("Health");

// API controllers
app.MapControllers();

app.Run();
