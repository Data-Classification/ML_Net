using Microsoft.AspNetCore.Mvc;
using MLNET_API.Contracts;
using MLNET_API.Services;

namespace MLNET_API.Controllers;

[ApiController]
[Route("health")]
public sealed class HealthController(IPredictionApiService predictionApiService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(HealthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(HealthResponse), StatusCodes.Status503ServiceUnavailable)]
    public ActionResult<HealthResponse> Get()
    {
        var response = predictionApiService.GetHealth();
        return response.ModelReady
            ? Ok(response)
            : StatusCode(StatusCodes.Status503ServiceUnavailable, response);
    }
}
