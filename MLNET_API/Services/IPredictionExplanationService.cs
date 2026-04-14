using MLNET_API.Contracts;

namespace MLNET_API.Services;

public interface IPredictionExplanationService
{
    Task<PredictionExplanationResponse> GenerateExplanationAsync(
        PredictOneRequest request,
        PredictOneResponse prediction,
        CancellationToken cancellationToken = default);
}
