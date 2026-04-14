namespace MLNET_API.Contracts;

public sealed class PredictBatchResponse
{
    public int TotalRecords { get; init; }
    public int SuccessCount { get; init; }
    public int FailureCount { get; init; }
    public IReadOnlyList<PredictBatchItemResult> Results { get; init; } = [];
}
