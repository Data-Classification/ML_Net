using System.ComponentModel.DataAnnotations;

namespace MLNET_API.Contracts;

public sealed class PredictOneRequest
{
    [Range(1, int.MaxValue, ErrorMessage = "StudentId must be greater than 0 when provided.")]
    public int? StudentId { get; init; }

    [Range(typeof(float), "15", "100", ErrorMessage = "Age must be between 15 and 100.")]
    public float Age { get; init; }

    [RegularExpression(@".*\S.*", ErrorMessage = "Gender is required.")]
    [StringLength(50, ErrorMessage = "Gender must be 50 characters or fewer.")]
    public string Gender { get; init; } = string.Empty;

    [RegularExpression(@".*\S.*", ErrorMessage = "Major is required.")]
    [StringLength(200, ErrorMessage = "Major must be 200 characters or fewer.")]
    public string Major { get; init; } = string.Empty;

    [Range(typeof(float), "0", "4", ErrorMessage = "GPA must be between 0 and 4.")]
    public float Gpa { get; init; }
}
