namespace MLNET_API.Contracts;

public sealed class HealthResponse
{
    public required string Status { get; init; }
    public bool ModelReady { get; init; }
    public string? ModelPath { get; init; }
    public string? ConfiguredModelPath { get; init; }
    public string? Error { get; init; }
}
