using System.ComponentModel.DataAnnotations;

namespace MLNET_API.Contracts;

public sealed class PredictBatchRequest
{
    [MinLength(1, ErrorMessage = "At least one student record is required.")]
    public IReadOnlyList<PredictBatchItemRequest> Students { get; init; } = [];
}
