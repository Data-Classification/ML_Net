using MLNET_API.Contracts;

namespace MLNET_API.Services;

public interface IPredictionApiService
{
    HealthResponse GetHealth();
    Task<PredictOneResponse> PredictOneAsync(PredictOneRequest request, CancellationToken cancellationToken = default);
    PredictBatchResponse PredictBatch(PredictBatchRequest request);
}
