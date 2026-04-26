namespace MLNET_API.Contracts;

public sealed class HealthResponse
{
    public required string Service { get; init; }
    public required string Status { get; init; }
    public bool ModelReady { get; init; }
    public string? ExplanationProvider { get; init; }
    public bool ExplanationEnabled { get; init; }
    public DateTimeOffset TimestampUtc { get; init; }
    public string? Environment { get; init; }
    public string? Message { get; init; }
}
