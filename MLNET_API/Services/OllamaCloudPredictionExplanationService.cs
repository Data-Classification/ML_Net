using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using MLNET_API.Contracts;
using MLNET_API.Options;

namespace MLNET_API.Services;

public sealed class OllamaCloudPredictionExplanationService(
    IHttpClientFactory httpClientFactory,
    IOptions<PredictionExplanationOptions> optionsAccessor) : IPredictionExplanationService
{
    private const string ProviderName = "ollama-cloud";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IHttpClientFactory httpClientFactory = httpClientFactory;
    private readonly PredictionExplanationOptions options = optionsAccessor.Value;

    public async Task<PredictionExplanationResponse> GenerateExplanationAsync(
        PredictOneRequest request,
        PredictOneResponse prediction,
        CancellationToken cancellationToken = default)
    {
        if (!options.Enabled)
        {
            return PredictionExplanationResponse.Unavailable(
                status: "disabled",
                message: "External explanation is disabled by configuration.",
                provider: ProviderName,
                model: options.Model);
        }

        var apiKey = ResolveApiKey();
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return PredictionExplanationResponse.Unavailable(
                status: "missing_api_key",
                message: "External explanation is unavailable because no Ollama Cloud API key was configured.",
                provider: ProviderName,
                model: options.Model);
        }

        if (string.IsNullOrWhiteSpace(options.Model))
        {
            return PredictionExplanationResponse.Unavailable(
                status: "missing_model",
                message: "External explanation is unavailable because no Ollama Cloud model name was configured.",
                provider: ProviderName);
        }

        var endpointResult = BuildGenerateEndpoint(options.BaseUrl);
        if (endpointResult.Status is not null)
        {
            return PredictionExplanationResponse.Unavailable(
                status: endpointResult.Status,
                message: endpointResult.Message
                    ?? "External explanation is unavailable because the Ollama Cloud base URL is invalid.",
                provider: ProviderName,
                model: options.Model);
        }

        try
        {
            using var client = httpClientFactory.CreateClient("PredictionExplanation");

            using var requestMessage = new HttpRequestMessage(HttpMethod.Post, endpointResult.Endpoint)
            {
                Content = JsonContent.Create(
                    new OllamaCloudGenerateRequest
                    {
                        Model = options.Model,
                        Prompt = BuildPrompt(request, prediction),
                        Stream = false
                    },
                    options: JsonOptions)
            };

            using var response = await client.SendAsync(requestMessage, cancellationToken);
            var payload = await ReadPayloadAsync(response, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return MapHttpFailure(
                    response.StatusCode,
                    payload.ErrorMessage,
                    options.Model);
            }

            var explanationText = payload.Response?.Response?.Trim();
            if (string.IsNullOrWhiteSpace(explanationText))
            {
                return PredictionExplanationResponse.Unavailable(
                    status: "empty_response",
                    message: "External explanation returned no usable text.",
                    provider: ProviderName,
                    model: options.Model);
            }

            return PredictionExplanationResponse.Success(
                provider: ProviderName,
                model: payload.Response?.Model ?? options.Model,
                text: explanationText);
        }
        catch (HttpRequestException ex)
        {
            return PredictionExplanationResponse.Unavailable(
                status: "connection_error",
                message: $"External explanation could not reach Ollama Cloud: {ex.Message}",
                provider: ProviderName,
                model: options.Model);
        }
        catch (OperationCanceledException)
        {
            return PredictionExplanationResponse.Unavailable(
                status: "timeout",
                message: "External explanation timed out while waiting for Ollama Cloud.",
                provider: ProviderName,
                model: options.Model);
        }
        catch (Exception ex)
        {
            return PredictionExplanationResponse.Unavailable(
                status: "error",
                message: $"External explanation failed: {ex.Message}",
                provider: ProviderName,
                model: options.Model);
        }
    }

    private string? ResolveApiKey()
    {
        if (!string.IsNullOrWhiteSpace(options.ApiKey))
        {
            return options.ApiKey;
        }

        return Environment.GetEnvironmentVariable("OLLAMA_API_KEY")
            ?? Environment.GetEnvironmentVariable("OLLAMA_CLOUD_API_KEY");
    }

    private static GenerateEndpointResult BuildGenerateEndpoint(string? baseUrl)
    {
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            return GenerateEndpointResult.Fail(
                status: "missing_base_url",
                message: "External explanation is unavailable because no Ollama Cloud base URL was configured.");
        }

        var normalizedBaseUrl = baseUrl.Trim();
        if (!normalizedBaseUrl.EndsWith("/", StringComparison.Ordinal))
        {
            normalizedBaseUrl += "/";
        }

        if (!Uri.TryCreate(normalizedBaseUrl, UriKind.Absolute, out var baseUri))
        {
            return GenerateEndpointResult.Fail(
                status: "invalid_base_url",
                message: "External explanation is unavailable because the Ollama Cloud base URL is invalid.");
        }

        if (!string.Equals(baseUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            return GenerateEndpointResult.Fail(
                status: "insecure_base_url",
                message: "External explanation is unavailable because the Ollama Cloud base URL must use HTTPS.");
        }

        if (baseUri.IsLoopback)
        {
            return GenerateEndpointResult.Fail(
                status: "local_base_url_not_allowed",
                message: "External explanation is unavailable because Ollama Cloud cannot use a localhost base URL.");
        }

        return GenerateEndpointResult.Success(new Uri("generate", UriKind.Relative));
    }

    private static PredictionExplanationResponse MapHttpFailure(HttpStatusCode statusCode, string? error, string? model)
    {
        if (statusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            return PredictionExplanationResponse.Unavailable(
                status: "unauthorized",
                message: "External explanation is unavailable because the Ollama Cloud API key was rejected.",
                provider: ProviderName,
                model: model);
        }

        if (statusCode == HttpStatusCode.NotFound &&
            !string.IsNullOrWhiteSpace(error) &&
            error.Contains("model", StringComparison.OrdinalIgnoreCase) &&
            error.Contains("not found", StringComparison.OrdinalIgnoreCase))
        {
            return PredictionExplanationResponse.Unavailable(
                status: "model_not_found",
                message: $"External explanation is unavailable because the Ollama Cloud model was not found: {error}",
                provider: ProviderName,
                model: model);
        }

        if (statusCode == HttpStatusCode.NotFound)
        {
            return PredictionExplanationResponse.Unavailable(
                status: "endpoint_not_found",
                message: "External explanation is unavailable because the Ollama Cloud generate endpoint was not found.",
                provider: ProviderName,
                model: model);
        }

        var detail = string.IsNullOrWhiteSpace(error)
            ? $"Ollama returned HTTP {(int)statusCode}."
            : $"Ollama returned HTTP {(int)statusCode}: {error}";

        return PredictionExplanationResponse.Unavailable(
            status: "http_error",
            message: $"External explanation request failed. {detail}",
            provider: ProviderName,
            model: model);
    }

    private static async Task<OllamaCloudResponseEnvelope> ReadPayloadAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(content))
        {
            return OllamaCloudResponseEnvelope.Empty;
        }

        try
        {
            var payload = JsonSerializer.Deserialize<OllamaCloudGenerateResponse>(content, JsonOptions);
            return new OllamaCloudResponseEnvelope(
                payload,
                payload?.Error ?? content.Trim());
        }
        catch (JsonException)
        {
            return new OllamaCloudResponseEnvelope(
                Response: null,
                ErrorMessage: content.Trim());
        }
    }

    private static string BuildPrompt(PredictOneRequest request, PredictOneResponse prediction)
    {
        return $"""
        You are writing a short, neutral explanation for a student analytics demo.
        The local ML model predicted YearOfStudy = {prediction.PredictedYearOfStudy}.
        Student features:
        - Age: {request.Age:0.##}
        - Gender: {request.Gender}
        - Major: {request.Major}
        - GPA: {request.Gpa:0.##}

        Write exactly 2 concise sentences in plain English.
        Sentence 1 should restate the predicted year neutrally.
        Sentence 2 should explain that the output is only a model-based estimate from the provided features, not a confirmed fact.
        Do not mention API calls, probabilities, or unsupported claims.
        """;
    }
}

internal sealed class OllamaCloudGenerateRequest
{
    [JsonPropertyName("model")]
    public required string Model { get; init; }

    [JsonPropertyName("prompt")]
    public required string Prompt { get; init; }

    [JsonPropertyName("stream")]
    public bool Stream { get; init; }
}

internal sealed class OllamaCloudGenerateResponse
{
    [JsonPropertyName("model")]
    public string? Model { get; init; }

    [JsonPropertyName("response")]
    public string? Response { get; init; }

    [JsonPropertyName("error")]
    public string? Error { get; init; }
}

internal sealed record OllamaCloudResponseEnvelope(
    OllamaCloudGenerateResponse? Response,
    string? ErrorMessage)
{
    public static OllamaCloudResponseEnvelope Empty { get; } = new(null, null);
}

internal sealed record GenerateEndpointResult(
    Uri? Endpoint,
    string? Status,
    string? Message)
{
    public static GenerateEndpointResult Success(Uri endpoint) => new(endpoint, null, null);

    public static GenerateEndpointResult Fail(string status, string message) =>
        new(null, status, message);
}
