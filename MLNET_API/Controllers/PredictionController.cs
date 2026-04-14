using Microsoft.AspNetCore.Mvc;
using MLNET_API.Contracts;
using MLNET_API.Services;

namespace MLNET_API.Controllers;

[ApiController]
[Route("predict")]
public sealed class PredictionController(IPredictionApiService predictionApiService) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType(typeof(PredictOneResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<PredictOneResponse>> PredictOne([FromBody] PredictOneRequest request)
    {
        try
        {
            return Ok(await predictionApiService.PredictOneAsync(request, HttpContext.RequestAborted));
        }
        catch (PredictionCoreUnavailableException ex)
        {
            return Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Prediction model is unavailable",
                detail: ex.Message);
        }
        catch (ArgumentException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return ValidationProblem(ModelState);
        }
        catch (Exception ex)
        {
            return Problem(
                statusCode: StatusCodes.Status500InternalServerError,
                title: "Prediction failed",
                detail: ex.Message);
        }
    }

    [HttpPost("batch")]
    [ProducesResponseType(typeof(PredictBatchResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public ActionResult<PredictBatchResponse> PredictBatch([FromBody] PredictBatchRequest request)
    {
        try
        {
            return Ok(predictionApiService.PredictBatch(request));
        }
        catch (PredictionCoreUnavailableException ex)
        {
            return Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Prediction model is unavailable",
                detail: ex.Message);
        }
        catch (ArgumentException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return ValidationProblem(ModelState);
        }
        catch (Exception ex)
        {
            return Problem(
                statusCode: StatusCodes.Status500InternalServerError,
                title: "Batch prediction failed",
                detail: ex.Message);
        }
    }
}
