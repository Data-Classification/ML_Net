namespace MLNET_API.Options;

public sealed class PredictionExplanationOptions
{
    public const string SectionName = "PredictionExplanation";

    public bool Enabled { get; init; } = true;
    public string? Provider { get; init; } = "ollama-cloud";
    public string? ApiKey { get; init; }
    public string? BaseUrl { get; init; } = "https://ollama.com/api";
    public string? Model { get; init; }
    public int TimeoutMilliseconds { get; init; } = 10000;
}
