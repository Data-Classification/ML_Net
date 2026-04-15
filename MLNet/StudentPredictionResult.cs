namespace MLNet;

public sealed class StudentPredictionResult
{
    public required string ModelPath { get; init; }
    public int PredictedYearOfStudy { get; init; }
    public float RawPredictedLabel { get; init; }
    public float? TopScore { get; init; }
    public IReadOnlyList<float> Scores { get; init; } = [];
}
