using System.ComponentModel.DataAnnotations;
using MLNET_API.Contracts;
using MLTest;

namespace MLNET_API.Services;

public sealed class PredictionApiService : IPredictionApiService
{
    private readonly string? configuredModelPath;
    private readonly IPredictionExplanationService predictionExplanationService;
    private readonly Lazy<YearOfStudyPredictionService> predictionCore;

    public PredictionApiService(
        IConfiguration configuration,
        IWebHostEnvironment environment,
        IPredictionExplanationService predictionExplanationService)
    {
        configuredModelPath = NullIfWhiteSpace(configuration["Prediction:ModelPath"]);
        this.predictionExplanationService = predictionExplanationService;
        predictionCore = new Lazy<YearOfStudyPredictionService>(
            () => new YearOfStudyPredictionService(configuredModelPath, [environment.ContentRootPath]),
            isThreadSafe: true);
    }

    public HealthResponse GetHealth()
    {
        try
        {
            var core = predictionCore.Value;
            return new HealthResponse
            {
                Status = "ok",
                ModelReady = true,
                ModelPath = core.ModelPath,
                ConfiguredModelPath = configuredModelPath
            };
        }
        catch (Exception ex)
        {
            return new HealthResponse
            {
                Status = "degraded",
                ModelReady = false,
                ModelPath = null,
                ConfiguredModelPath = configuredModelPath,
                Error = ex.Message
            };
        }
    }

    public async Task<PredictOneResponse> PredictOneAsync(PredictOneRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        EnsureValidPredictRequest(request);

        var core = GetPredictionCore();
        var basePrediction = PredictWithCore(core, request);
        var explanation = await predictionExplanationService.GenerateExplanationAsync(
            request,
            basePrediction,
            cancellationToken);

        return new PredictOneResponse
        {
            ModelPath = basePrediction.ModelPath,
            PredictedYearOfStudy = basePrediction.PredictedYearOfStudy,
            RawPredictedLabel = basePrediction.RawPredictedLabel,
            TopScore = basePrediction.TopScore,
            Scores = basePrediction.Scores,
            Explanation = explanation
        };
    }

    public PredictBatchResponse PredictBatch(PredictBatchRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Students is null || request.Students.Count == 0)
        {
            throw new ArgumentException("At least one student record is required.", nameof(request));
        }

        var core = GetPredictionCore();
        var results = new List<PredictBatchItemResult>(request.Students.Count);
        var successCount = 0;

        for (var index = 0; index < request.Students.Count; index++)
        {
            var item = request.Students[index];
            var predictRequest = ToPredictOneRequest(item);
            var errors = ValidatePredictRequest(predictRequest);

            if (errors.Count > 0)
            {
                results.Add(new PredictBatchItemResult
                {
                    Index = index,
                    StudentId = item.StudentId,
                    Success = false,
                    Errors = errors
                });
                continue;
            }

            try
            {
                var prediction = PredictWithCore(core, predictRequest);
                results.Add(new PredictBatchItemResult
                {
                    Index = index,
                    StudentId = predictRequest.StudentId,
                    Success = true,
                    Prediction = prediction
                });
                successCount++;
            }
            catch (Exception ex)
            {
                results.Add(new PredictBatchItemResult
                {
                    Index = index,
                    StudentId = item.StudentId,
                    Success = false,
                    Errors = [ex.Message]
                });
            }
        }

        return new PredictBatchResponse
        {
            TotalRecords = request.Students.Count,
            SuccessCount = successCount,
            FailureCount = results.Count - successCount,
            Results = results
        };
    }

    private YearOfStudyPredictionService GetPredictionCore()
    {
        try
        {
            return predictionCore.Value;
        }
        catch (Exception ex)
        {
            throw new PredictionCoreUnavailableException($"Prediction model is not available. {ex.Message}", ex);
        }
    }

    private static PredictOneResponse PredictWithCore(YearOfStudyPredictionService core, PredictOneRequest request)
    {
        var prediction = core.Predict(new StudentPredictionInput
        {
            StudentId = request.StudentId,
            Age = request.Age,
            Gender = request.Gender,
            Major = request.Major,
            Gpa = request.Gpa
        });

        return new PredictOneResponse
        {
            ModelPath = prediction.ModelPath,
            PredictedYearOfStudy = prediction.PredictedYearOfStudy,
            RawPredictedLabel = prediction.RawPredictedLabel,
            TopScore = prediction.TopScore,
            Scores = prediction.Scores,
            Explanation = PredictionExplanationResponse.NotRequested()
        };
    }

    private static PredictOneRequest ToPredictOneRequest(PredictBatchItemRequest item)
    {
        return new PredictOneRequest
        {
            StudentId = item.StudentId,
            Age = item.Age,
            Gender = item.Gender ?? string.Empty,
            Major = item.Major ?? string.Empty,
            Gpa = item.Gpa
        };
    }

    private static void EnsureValidPredictRequest(PredictOneRequest request)
    {
        var errors = ValidatePredictRequest(request);
        if (errors.Count == 0)
        {
            return;
        }

        throw new ArgumentException(string.Join(" ", errors), nameof(request));
    }

    private static IReadOnlyList<string> ValidatePredictRequest(PredictOneRequest request)
    {
        var validationResults = new List<ValidationResult>();
        Validator.TryValidateObject(
            request,
            new ValidationContext(request),
            validationResults,
            validateAllProperties: true);

        return validationResults
            .Select(result => result.ErrorMessage)
            .Where(message => !string.IsNullOrWhiteSpace(message))
            .Cast<string>()
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static string? NullIfWhiteSpace(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}
