namespace MLNET_API.Contracts;

public sealed class PredictionExplanationResponse
{
    public required string Status { get; init; }
    public bool Available { get; init; }
    public string? Provider { get; init; }
    public string? Model { get; init; }
    public string? Text { get; init; }
    public string? Message { get; init; }

    public static PredictionExplanationResponse Success(string provider, string model, string text)
    {
        return new PredictionExplanationResponse
        {
            Status = "success",
            Available = true,
            Provider = provider,
            Model = model,
            Text = text,
            Message = null
        };
    }

    public static PredictionExplanationResponse Unavailable(string status, string message, string? provider = null, string? model = null)
    {
        return new PredictionExplanationResponse
        {
            Status = status,
            Available = false,
            Provider = provider,
            Model = model,
            Text = null,
            Message = message
        };
    }

    public static PredictionExplanationResponse NotRequested()
    {
        return Unavailable("not_requested", "Explanation was not requested for this response.");
    }
}
