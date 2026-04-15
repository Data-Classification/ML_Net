using System.Net.Http.Headers;
using Microsoft.Extensions.Options;
using MLNET_API.Options;
using MLNET_API.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.Configure<PredictionExplanationOptions>(
    builder.Configuration.GetSection(PredictionExplanationOptions.SectionName));
builder.Services.AddHttpClient("PredictionExplanation", (serviceProvider, client) =>
{
    var options = serviceProvider.GetRequiredService<IOptions<PredictionExplanationOptions>>().Value;

    var baseAddress = BuildExplanationBaseAddress(options.BaseUrl);
    if (baseAddress is not null)
    {
        client.BaseAddress = baseAddress;
    }

    client.Timeout = TimeSpan.FromMilliseconds(NormalizeTimeout(options.TimeoutMilliseconds));

    var apiKey = ResolveApiKey(options);
    if (!string.IsNullOrWhiteSpace(apiKey))
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
    }
});
builder.Services.AddSingleton<IPredictionExplanationService, OllamaCloudPredictionExplanationService>();
builder.Services.AddSingleton<IPredictionApiService, PredictionApiService>();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.MapControllers();

app.Run();

static Uri? BuildExplanationBaseAddress(string? baseUrl)
{
    if (string.IsNullOrWhiteSpace(baseUrl))
    {
        return null;
    }

    var normalizedBaseUrl = baseUrl.Trim();
    if (!normalizedBaseUrl.EndsWith("/", StringComparison.Ordinal))
    {
        normalizedBaseUrl += "/";
    }

    return Uri.TryCreate(normalizedBaseUrl, UriKind.Absolute, out var baseAddress)
        ? baseAddress
        : null;
}

static int NormalizeTimeout(int configuredTimeout)
{
    return configuredTimeout > 0 ? configuredTimeout : 10000;
}

static string? ResolveApiKey(PredictionExplanationOptions options)
{
    if (!string.IsNullOrWhiteSpace(options.ApiKey))
    {
        return options.ApiKey;
    }

    return Environment.GetEnvironmentVariable("OLLAMA_API_KEY")
        ?? Environment.GetEnvironmentVariable("OLLAMA_CLOUD_API_KEY");
}
