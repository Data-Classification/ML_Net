# API Usage

## Swagger

- Start the API: `dotnet run --project .\MLNET_API\MLNET_API.csproj`
- Swagger UI: `http://localhost:5157/swagger`

## External Explanation

- Provider: `Google.GenAI` (Gemini Developer API).
- Flow: local ML prediction runs first; explanation generation runs afterward only for `POST /predict`.
- The API never hardcodes the secret. Keep `PredictionExplanation:ApiKey` empty in `appsettings.json` and supply the real key via user-secrets or environment variables.

Recommended local setup with user-secrets:

```powershell
dotnet user-secrets set "PredictionExplanation:ApiKey" "<your-google-api-key>" --project .\MLNET_API\MLNET_API.csproj
dotnet user-secrets set "PredictionExplanation:Model" "gemini-2.0-flash" --project .\MLNET_API\MLNET_API.csproj
```

Alternative environment variables:

```powershell
$env:GOOGLE_API_KEY = "<your-google-api-key>"
```

Supported config section:

```json
"PredictionExplanation": {
  "Enabled": true,
  "ApiKey": "",
  "Model": "gemini-2.0-flash",
  "TimeoutMilliseconds": 10000
}
```

Fallback behavior:

- If the key is missing, the model name is missing, the external provider times out, or the provider returns an error, `/predict` still returns the local prediction with `explanation.available = false` and a status/message that explains the fallback.

## `GET /health`

- Purpose: check API availability and whether the prediction model can be loaded.

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

- Purpose: predict `YearOfStudy` for one student record.

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

Sample success response:

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
    "provider": "google-genai",
    "model": "gemini-2.0-flash",
    "text": null,
    "message": "External explanation is unavailable because no API key was configured."
  }
}
```

When a valid key/model is configured and the external provider is available, the same `explanation` object switches to `status = "success"`, `available = true`, and `text` contains a short natural-language explanation.

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

- Purpose: predict `YearOfStudy` for many student records in one request.
- Item-level validation errors do not fail the entire batch. The API keeps processing the remaining valid records.

Sample request:

```json
{
  "students": [
    {
      "studentId": 1,
      "age": 22,
      "gender": "Male",
      "major": "Economics",
      "gpa": 3.16
    },
    {
      "studentId": 2,
      "age": 25,
      "gender": "Female",
      "major": "Economics",
      "gpa": 2.8
    }
  ]
}
```

Sample response:

```json
{
  "totalRecords": 2,
  "successCount": 2,
  "failureCount": 0,
  "results": [
    {
      "index": 0,
      "studentId": 1,
      "success": true,
      "prediction": {
        "modelPath": "D:\\old_data\\code\\MLTest\\MLTest\\SentimentModel.mlnet",
        "predictedYearOfStudy": 4,
        "rawPredictedLabel": 4,
        "topScore": 0.2199474,
        "scores": [0.20363325, 0.19287746, 0.2199474, 0.19200988, 0.19153203],
        "explanation": {
          "status": "not_requested",
          "available": false,
          "provider": null,
          "model": null,
          "text": null,
          "message": "Explanation was not requested for this response."
        }
      },
      "errors": []
    },
    {
      "index": 1,
      "studentId": 2,
      "success": true,
      "prediction": {
        "modelPath": "D:\\old_data\\code\\MLTest\\MLTest\\SentimentModel.mlnet",
        "predictedYearOfStudy": 5,
        "rawPredictedLabel": 5,
        "topScore": 0.20888862,
        "scores": [0.20484312, 0.17615987, 0.20737807, 0.20888862, 0.20273033],
        "explanation": {
          "status": "not_requested",
          "available": false,
          "provider": null,
          "model": null,
          "text": null,
          "message": "Explanation was not requested for this response."
        }
      },
      "errors": []
    }
  ]
}
```

Mixed valid/invalid batch behavior:

```json
{
  "totalRecords": 2,
  "successCount": 1,
  "failureCount": 1,
  "results": [
    {
      "index": 0,
      "studentId": 1,
      "success": true,
      "prediction": {
        "modelPath": "D:\\old_data\\code\\MLTest\\MLTest\\SentimentModel.mlnet",
        "predictedYearOfStudy": 4,
        "rawPredictedLabel": 4,
        "topScore": 0.2199474,
        "scores": [0.20363325, 0.19287746, 0.2199474, 0.19200988, 0.19153203],
        "explanation": {
          "status": "not_requested",
          "available": false,
          "provider": null,
          "model": null,
          "text": null,
          "message": "Explanation was not requested for this response."
        }
      },
      "errors": []
    },
    {
      "index": 1,
      "studentId": 2,
      "success": false,
      "prediction": null,
      "errors": [
        "Major is required.",
        "GPA must be between 0 and 4."
      ]
    }
  ]
}
```

Latest manual batch test summary:

- `POST /predict/batch` with 2 valid records returned `200`, `successCount=2`, `failureCount=0`.
- `POST /predict/batch` with 1 valid record and 1 invalid record returned `200`, `successCount=1`, `failureCount=1`, and the invalid item reported its own `errors` array without breaking the whole batch.

Latest manual external explanation test summary:

- Current environment had no `GOOGLE_API_KEY`, `GEMINI_API_KEY`, or user-secret configured for `MLNET_API`.
- `POST /predict` still returned `200` with the local prediction, while `explanation.status` was `missing_api_key` and `explanation.available` was `false`.
- Success-path explanation generation is code/config ready, but it was not runtime-verified in this environment because no real API key was available.
