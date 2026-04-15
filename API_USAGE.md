# API Usage

## Swagger

- Start the API: `dotnet run --project .\MLNET_API\MLNET_API.csproj --launch-profile http`
- Swagger UI: `http://localhost:5157/swagger`

## Quick Ollama Cloud Setup

The explanation provider runs only for `POST /predict`. Local prediction always runs first.

Recommended local setup with user-secrets:

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

Supported config section:

```json
"PredictionExplanation": {
  "Enabled": true,
  "Provider": "ollama-cloud",
  "ApiKey": "",
  "BaseUrl": "https://ollama.com/api",
  "Model": "gemma3:4b-cloud",
  "TimeoutMilliseconds": 10000
}
```

Request details:

- Provider: `ollama-cloud`
- HTTP target: `https://ollama.com/api/generate`
- Auth: `Authorization: Bearer <API_KEY>`
- Payload includes `model`, `prompt`, and `stream=false`

Fallback behavior:

- If the API key is missing, the key is rejected, the base URL is invalid/unreachable, the provider times out, or the cloud response is empty, `/predict` still returns the local prediction with `explanation.available = false` and a status/message that explains the fallback.

## Quick Manual Test

1. Start the API.
2. Check health:

```powershell
Invoke-RestMethod -Uri "http://localhost:5157/health"
```

3. Send one prediction request:

```powershell
$body = @{
  studentId = 1
  age = 22
  gender = "Male"
  major = "Economics"
  gpa = 3.16
} | ConvertTo-Json

Invoke-RestMethod -Uri "http://localhost:5157/predict" `
  -Method Post `
  -ContentType "application/json" `
  -Body $body
```

## `GET /health`

Sample success response:

```json
{
  "status": "ok",
  "modelReady": true,
  "modelPath": "D:\\old_data\\code\\MLTest\\MLTest\\SentimentModel.mlnet",
  "configuredModelPath": null,
  "error": null
}
```

## `POST /predict`

Sample request:

```json
{
  "studentId": 1,
  "age": 22,
  "gender": "Male",
  "major": "Economics",
  "gpa": 3.16
}
```

Expected explanation success shape when a real Ollama Cloud API key and valid model are configured:

```json
{
  "modelPath": "D:\\old_data\\code\\MLTest\\MLTest\\SentimentModel.mlnet",
  "predictedYearOfStudy": 4,
  "rawPredictedLabel": 4,
  "topScore": 0.2199474,
  "scores": [0.20363325, 0.19287746, 0.2199474, 0.19200988, 0.19153203],
  "explanation": {
    "status": "success",
    "available": true,
    "provider": "ollama-cloud",
    "model": "gemma3:4b-cloud",
    "text": "The model predicts this student is in year 4. This is only a short model-based estimate from the provided features, not a confirmed fact.",
    "message": null
  }
}
```

Actual fallback example from this environment with no API key configured:

```json
{
  "modelPath": "D:\\old_data\\code\\MLTest\\MLTest\\SentimentModel.mlnet",
  "predictedYearOfStudy": 4,
  "rawPredictedLabel": 4,
  "topScore": 0.2199474,
  "scores": [0.20363325, 0.19287746, 0.2199474, 0.19200988, 0.19153203],
  "explanation": {
    "status": "missing_api_key",
    "available": false,
    "provider": "ollama-cloud",
    "model": "gemma3:4b-cloud",
    "text": null,
    "message": "External explanation is unavailable because no Ollama Cloud API key was configured."
  }
}
```

Other fallback statuses you may see during testing:

- `unauthorized`: API key was rejected by Ollama Cloud
- `connection_error`: base URL is unreachable or DNS/network failed
- `timeout`: cloud request exceeded the configured timeout
- `endpoint_not_found`: wrong API path/base URL
- `model_not_found`: configured model name was rejected by the provider
- `http_error`: other non-success HTTP response from Ollama Cloud

Sample validation error:

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": {
    "Major": [
      "Major is required."
    ],
    "Gpa": [
      "GPA must be between 0 and 4."
    ]
  }
}
```

## `POST /predict/batch`

- Batch prediction still reuses the same local prediction core.
- Batch explanation is intentionally not enabled in this demo.
- Each successful batch item keeps:

```json
{
  "explanation": {
    "status": "not_requested",
    "available": false,
    "provider": null,
    "model": null,
    "text": null,
    "message": "Explanation was not requested for this response."
  }
}
```

## Latest Manual Verification

Verification run on April 15, 2026:

- `GET /health`: passed
- `POST /predict` with no API key: passed, local prediction returned with `explanation.status = missing_api_key`
- `POST /predict` with fake API key against `https://ollama.com/api`: passed, local prediction returned with `explanation.status = unauthorized`
- `POST /predict` with bad base URL `https://example.invalid/api`: passed, local prediction returned with `explanation.status = connection_error`

What was not verified here:

- Live `success` response from Ollama Cloud, because no real API key was available in this environment.
