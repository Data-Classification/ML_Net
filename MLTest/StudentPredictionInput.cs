namespace MLTest;

public sealed class StudentPredictionInput
{
    public int? StudentId { get; init; }
    public float Age { get; init; }
    public required string Gender { get; init; }
    public required string Major { get; init; }
    public float Gpa { get; init; }
}
