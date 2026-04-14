using Google.GenAI;
using Google.GenAI.Types;
using Microsoft.Extensions.Options;
using MLNET_API.Contracts;
using MLNET_API.Options;

namespace MLNET_API.Services;

public sealed class GoogleGenAiPredictionExplanationService(
    IOptions<PredictionExplanationOptions> optionsAccessor) : IPredictionExplanationService
{
    private const string ProviderName = "google-genai";
    private readonly PredictionExplanationOptions options = optionsAccessor.Value;

    public async Task<PredictionExplanationResponse> GenerateExplanationAsync(
        PredictOneRequest request,
        PredictOneResponse prediction,
        CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;

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
                message: "External explanation is unavailable because no API key was configured.",
                provider: ProviderName,
                model: options.Model);
        }

        if (string.IsNullOrWhiteSpace(options.Model))
        {
            return PredictionExplanationResponse.Unavailable(
                status: "missing_model",
                message: "External explanation is unavailable because no model name was configured.",
                provider: ProviderName);
        }

        try
        {
            var client = new Client(
                apiKey: apiKey,
                httpOptions: new HttpOptions
                {
                    Timeout = NormalizeTimeout(options.TimeoutMilliseconds)
                });

            var response = await client.Models.GenerateContentAsync(
                model: options.Model,
                contents: BuildPrompt(request, prediction),
                config: new GenerateContentConfig
                {
                    Temperature = 0.2f,
                    MaxOutputTokens = 120
                });

            var explanationText = ExtractText(response);
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
                model: options.Model,
                text: explanationText.Trim());
        }
        catch (ClientError ex)
        {
            return PredictionExplanationResponse.Unavailable(
                status: "client_error",
                message: $"External explanation request was rejected: {ex.Message}",
                provider: ProviderName,
                model: options.Model);
        }
        catch (ServerError ex)
        {
            return PredictionExplanationResponse.Unavailable(
                status: "server_error",
                message: $"External explanation service failed: {ex.Message}",
                provider: ProviderName,
                model: options.Model);
        }
        catch (TaskCanceledException)
        {
            return PredictionExplanationResponse.Unavailable(
                status: "timeout",
                message: "External explanation timed out.",
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

    private static int NormalizeTimeout(int configuredTimeout)
    {
        return configuredTimeout > 0 ? configuredTimeout : 10000;
    }

    private string? ResolveApiKey()
    {
        if (!string.IsNullOrWhiteSpace(options.ApiKey))
        {
            return options.ApiKey;
        }

        return System.Environment.GetEnvironmentVariable("GOOGLE_API_KEY")
            ?? System.Environment.GetEnvironmentVariable("GEMINI_API_KEY")
            ?? System.Environment.GetEnvironmentVariable("GENAI_API_KEY");
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

    private static string? ExtractText(GenerateContentResponse response)
    {
        var textParts = response.Candidates?
            .Where(candidate => candidate.Content is not null)
            .SelectMany(candidate => candidate.Content?.Parts ?? [])
            .Select(part => part.Text)
            .Where(text => !string.IsNullOrWhiteSpace(text))
            .ToArray();

        return textParts is { Length: > 0 }
            ? string.Join(" ", textParts)
            : null;
    }
}
