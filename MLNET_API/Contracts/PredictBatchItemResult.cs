namespace MLNET_API.Contracts;

public sealed class PredictBatchItemResult
{
    public int Index { get; init; }
    public int? StudentId { get; init; }
    public bool Success { get; init; }
    public PredictOneResponse? Prediction { get; init; }
    public IReadOnlyList<string> Errors { get; init; } = [];
}
