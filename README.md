# MLTest API Demo

## Overview

This solution contains two projects:

- `MLTest`: existing console/ML project that owns training, evaluation, comparison, artifacts, and the reusable local prediction core.
- `MLNET_API`: ASP.NET Core Web API host that exposes health, single prediction, and batch prediction endpoints.

The external explanation provider is now `ollama-cloud`. The API keeps the same safe flow:

1. Run local prediction first.
2. Call Ollama Cloud only for `POST /predict`.
3. Return the local prediction even if the external explanation fails.

## Architecture

```text
Client / Swagger / Postman
        |
        v
    MLNET_API
        |
        +--> PredictionApiService
                |
                +--> MLTest YearOfStudyPredictionService
                |       |
                |       +--> SentimentModel.mlnet
                |
                +--> OllamaCloudPredictionExplanationService (optional)
                        |
                        +--> https://ollama.com/api/generate
                        +--> Authorization: Bearer <API_KEY>
        |
        v
   JSON response
```

## Implemented Flows

- `GET /health`
- `POST /predict`
- `POST /predict/batch`
- `POST /predict` fallback when Ollama Cloud is unavailable

Still intentionally out of scope:

- Auth / database / frontend
- CSV upload API
- Evaluate / compare API
- Batch explanation generation

## Run Locally

1. Build the whole solution:

```powershell
dotnet build .\MLTest.slnx
```

2. Optional: configure Ollama Cloud explanation.

User-secrets:

```powershell
dotnet user-secrets set "PredictionExplanation:ApiKey" "<your-ollama-api-key>" --project .\MLNET_API\MLNET_API.csproj
dotnet user-secrets set "PredictionExplanation:BaseUrl" "https://ollama.com/api" --project .\MLNET_API\MLNET_API.csproj
dotnet user-secrets set "PredictionExplanation:Model" "gemma3:4b-cloud" --project .\MLNET_API\MLNET_API.csproj
```

Environment variable alternative:

```powershell
$env:OLLAMA_API_KEY = "<your-ollama-api-key>"
$env:PredictionExplanation__BaseUrl = "https://ollama.com/api"
$env:PredictionExplanation__Model = "gemma3:4b-cloud"
```

3. Run the API:

```powershell
dotnet run --project .\MLNET_API\MLNET_API.csproj --launch-profile http
```

4. Open Swagger:

- `http://localhost:5157/swagger`

## Configuration

`MLNET_API/appsettings.json` exposes two main config groups:

- `Prediction:ModelPath`
  Optional override for model resolution. If left empty, the API resolves `MLTest/SentimentModel.mlnet` automatically during local development and falls back to the output copy when needed.
- `PredictionExplanation`
  Controls Ollama Cloud explanation generation:
  - `Enabled`
  - `Provider`
  - `ApiKey`
  - `BaseUrl`
  - `Model`
  - `TimeoutMilliseconds`

Do not store a real API key in `appsettings.json`.

## External Explanation Behavior

- Default provider: `ollama-cloud`
- Target API: `https://ollama.com/api/generate`
- Auth: `Authorization: Bearer <API_KEY>`
- Request mode: `stream=false`
- Where it runs: local prediction first, explanation second, only for `POST /predict`
- Fallback behavior: if the API key is missing, the key is rejected, the base URL is invalid/unreachable, the provider times out, or the cloud response is empty, the API still returns the local prediction and marks `explanation.available = false`

Detailed payload examples are in [API_USAGE.md](/D:/old_data/code/MLTest/API_USAGE.md).

## Manual Verification Summary

Latest local verification on April 15, 2026:

- Solution build: passed
- Swagger UI endpoint: usable
- `GET /health`: passed
- `POST /predict` with no API key: passed, local prediction still returned and `explanation.status = missing_api_key`
- `POST /predict` with a fake API key against Ollama Cloud: passed, local prediction still returned and `explanation.status = unauthorized`
- `POST /predict` with bad base URL `https://example.invalid/api`: passed, local prediction still returned and `explanation.status = connection_error`
- `POST /predict/batch`: still works and keeps `explanation = not_requested`

Current environment limitation:

- No real Ollama Cloud API key was available in environment variables or user-secrets during verification, so the live success path for explanation generation is code-ready but not runtime-verified here.

## Important Files

- [MLNET_API/Program.cs](/D:/old_data/code/MLTest/MLNET_API/Program.cs)
- [MLNET_API/Controllers/PredictionController.cs](/D:/old_data/code/MLTest/MLNET_API/Controllers/PredictionController.cs)
- [MLNET_API/Services/PredictionApiService.cs](/D:/old_data/code/MLTest/MLNET_API/Services/PredictionApiService.cs)
- [MLNET_API/Services/OllamaCloudPredictionExplanationService.cs](/D:/old_data/code/MLTest/MLNET_API/Services/OllamaCloudPredictionExplanationService.cs)
- [MLTest/YearOfStudyPredictionService.cs](/D:/old_data/code/MLTest/MLTest/YearOfStudyPredictionService.cs)
- [API_USAGE.md](/D:/old_data/code/MLTest/API_USAGE.md)

## Known Limitations

- External explanation success still needs one live run with a real Ollama Cloud API key.
- Batch responses keep explanation as `not_requested` for now.
- No evaluate/compare API was added because it is outside the prompt chain scope.
