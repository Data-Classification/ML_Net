namespace MLNET_API.Options;

public sealed class PredictionExplanationOptions
{
    public const string SectionName = "PredictionExplanation";

    public bool Enabled { get; init; } = true;
    public string? ApiKey { get; init; }
    public string? Model { get; init; } = "gemini-2.0-flash";
    public int TimeoutMilliseconds { get; init; } = 10000;
}
