namespace MLNET_API.Contracts;

public sealed class PredictBatchItemRequest
{
    public int? StudentId { get; init; }
    public float Age { get; init; }
    public string? Gender { get; init; }
    public string? Major { get; init; }
    public float Gpa { get; init; }
}
