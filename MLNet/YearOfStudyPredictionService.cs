using Microsoft.ML;

namespace MLNet;

public sealed class YearOfStudyPredictionService
{
    private readonly Lazy<ModelRuntime> runtime;

    public YearOfStudyPredictionService(string? modelPath = null, IEnumerable<string?>? probeDirectories = null)
    {
        ModelPath = PredictionModelPathResolver.ResolveModelPathOrThrow(modelPath, probeDirectories);
        runtime = new Lazy<ModelRuntime>(() => LoadRuntime(ModelPath), isThreadSafe: true);
    }

    public string ModelPath { get; }

    public StudentPredictionResult Predict(StudentPredictionInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        if (string.IsNullOrWhiteSpace(input.Gender))
        {
            throw new ArgumentException("Gender is required.", nameof(input));
        }

        if (string.IsNullOrWhiteSpace(input.Major))
        {
            throw new ArgumentException("Major is required.", nameof(input));
        }

        var loadedRuntime = runtime.Value;
        var modelInput = new SentimentModel.ModelInput
        {
            StudentID = input.StudentId ?? 0,
            Age = input.Age,
            Gender = input.Gender,
            Major = input.Major,
            GPA = input.Gpa
        };

        var predictionEngine = loadedRuntime.MlContext.Model.CreatePredictionEngine<SentimentModel.ModelInput, SentimentModel.ModelOutput>(loadedRuntime.Model);
        var prediction = predictionEngine.Predict(modelInput);
        var topScore = prediction.Score is { Length: > 0 } ? prediction.Score.Max() : (float?)null;

        return new StudentPredictionResult
        {
            ModelPath = ModelPath,
            PredictedYearOfStudy = Convert.ToInt32(MathF.Round(prediction.PredictedLabel)),
            RawPredictedLabel = prediction.PredictedLabel,
            TopScore = topScore,
            Scores = prediction.Score?.ToArray() ?? []
        };
    }

    private static ModelRuntime LoadRuntime(string modelPath)
    {
        var mlContext = new MLContext();
        var model = mlContext.Model.Load(modelPath, out _);
        return new ModelRuntime(mlContext, model);
    }

    private sealed record ModelRuntime(MLContext MlContext, ITransformer Model);
}
