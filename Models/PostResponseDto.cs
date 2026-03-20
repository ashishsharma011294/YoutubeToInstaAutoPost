namespace InstaAutoPost.Models;

public record PostResponseDto
{
    public bool Success { get; init; }
    public string? ReelId { get; init; }
    public string Message { get; init; } = string.Empty;
}
