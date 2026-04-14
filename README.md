# MLTest API Demo

## Overview

This solution now covers the two required parts of the assignment:

- `Cung cấp API`: `MLNET_API` exposes health, single prediction, and batch prediction endpoints.
- `Sử dụng API từ nguồn khác`: `MLNET_API` calls Google Gemini through `Google.GenAI` after local prediction to generate a short explanation when external configuration is available.

Projects:

- `MLTest`: existing console/ML project. It still owns training, evaluation, comparison, artifacts, and the reusable prediction core.
- `MLNET_API`: ASP.NET Core Web API host used for demo/testing via Swagger or Postman.

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
                +--> GoogleGenAiPredictionExplanationService (optional)
                        |
                        +--> Google Gemini API via Google.GenAI
        |
        v
   JSON response
```

## Implemented Flows

- `GET /health`
- `POST /predict`
- `POST /predict/batch`
- `POST /predict` fallback when external explanation is unavailable

What is intentionally still out of scope:

- Auth / database / frontend
- CSV upload API
- Evaluate / compare API
- Batch explanation generation

## Run Locally

1. Build the whole solution:

```powershell
dotnet build .\MLTest.slnx
```

2. Optional: configure external explanation if you want Gemini explanations to run.

User-secrets:

```powershell
dotnet user-secrets set "PredictionExplanation:ApiKey" "<your-google-api-key>" --project .\MLNET_API\MLNET_API.csproj
dotnet user-secrets set "PredictionExplanation:Model" "gemini-2.0-flash" --project .\MLNET_API\MLNET_API.csproj
```

Environment variable alternative:

```powershell
$env:GOOGLE_API_KEY = "<your-google-api-key>"
```

3. Run the API:

```powershell
dotnet run --project .\MLNET_API\MLNET_API.csproj
```

4. Open Swagger:

- `http://localhost:5157/swagger`

## Configuration

`MLNET_API/appsettings.json` now exposes the two main config groups:

- `Prediction:ModelPath`
  Use this only if you want to override the default model path resolver. If left empty, the API resolves `MLTest/SentimentModel.mlnet` automatically during local development and falls back to the output copy when needed.
- `PredictionExplanation`
  Controls Google Gemini explanation generation:
  - `Enabled`
  - `ApiKey`
  - `Model`
  - `TimeoutMilliseconds`

Do not store a real API key in `appsettings.json`.

## Demo Endpoints

Detailed payload examples are in [API_USAGE.md](/D:/old_data/code/MLTest/API_USAGE.md).

Available endpoints:

- `GET /health`
- `POST /predict`
- `POST /predict/batch`

## External API Usage

- Provider: Google Gemini through `Google.GenAI`
- Where it runs:
  local prediction first, explanation second, only for `POST /predict`
- Failure behavior:
  if the key is missing, the provider errors, or timeout happens, the API still returns the local prediction and sets `explanation.available = false` with a status/message

## Manual Verification Summary

Latest local verification:

- Solution build: passed
- Swagger UI endpoint: usable
- `GET /health`: passed
- `POST /predict`: passed
- `POST /predict/batch`: passed
- `POST /predict` without external key: passed, local prediction still returned and `explanation.status = missing_api_key`

Current environment limitation:

- No real Gemini API key was available in environment variables or user-secrets during verification, so the live success path for explanation generation is code-ready but not runtime-verified here.

## Important Files

- [MLNET_API/Program.cs](/D:/old_data/code/MLTest/MLNET_API/Program.cs)
- [MLNET_API/Controllers/PredictionController.cs](/D:/old_data/code/MLTest/MLNET_API/Controllers/PredictionController.cs)
- [MLNET_API/Services/PredictionApiService.cs](/D:/old_data/code/MLTest/MLNET_API/Services/PredictionApiService.cs)
- [MLNET_API/Services/GoogleGenAiPredictionExplanationService.cs](/D:/old_data/code/MLTest/MLNET_API/Services/GoogleGenAiPredictionExplanationService.cs)
- [MLTest/YearOfStudyPredictionService.cs](/D:/old_data/code/MLTest/MLTest/YearOfStudyPredictionService.cs)
- [API_USAGE.md](/D:/old_data/code/MLTest/API_USAGE.md)

## Assignment Coverage

The system now satisfies the core goals of bai 3 in these points:

- Provided a working Web API over the existing ML model.
- Exposed demo-friendly endpoints for health, single prediction, and batch prediction.
- Integrated an external API after prediction to enrich the response with natural-language explanation.
- Preserved the original ML console workflow instead of rewriting the project from scratch.

Known limitations:

- External explanation success still needs one live run with a real key.
- Batch responses keep explanation as `not_requested` for now.
- No evaluate/compare API was added because it is outside the prompt chain scope.
