namespace MLNET_API.Contracts;

public sealed class PredictOneResponse
{
    public required string ModelPath { get; init; }
    public int PredictedYearOfStudy { get; init; }
    public float RawPredictedLabel { get; init; }
    public float? TopScore { get; init; }
    public IReadOnlyList<float> Scores { get; init; } = [];
    public PredictionExplanationResponse Explanation { get; init; } = PredictionExplanationResponse.NotRequested();
}
