# Implementation Plan

## Current Solution State

- `MLNet` is the existing .NET console project that owns training, prediction, evaluation, comparison, artifact retention, and the current ML model assets.
- `MLNET_API` is the new ASP.NET Core Web API host. It was created from the default template and is now reduced to a clean host setup without demo endpoints.
- `MLNet.slnx` now includes both projects so the solution can be built and evolved together.

## Reusable Prediction Core Found in `MLNet`

- Core single-record inference currently lives in `MLNet/SentimentModel.consumption.cs` via `SentimentModel.Predict(ModelInput input)`.
- `SentimentModel.ModelInput` and `SentimentModel.ModelOutput` are already public and can be reused from `MLNET_API`.
- `CsvBatchPredictor.Run(...)` is not the prediction core. It is a CLI-oriented orchestration layer around CSV parsing, console logging, summary generation, evaluation matching, output file writing, and retention cleanup.
- `Program.cs` is also CLI-only orchestration. It parses commands, resolves file paths, prints usage/output, and switches the current directory for command-line execution.

## Current Constraints and Risks

- `CsvBatchPredictor` is `internal`, tightly coupled to CSV files and console output, and should not be called directly from the API.
- `AppPaths` is `internal` and currently only helps the CLI locate the project root and training metadata directories.
- `SentimentModel.consumption.cs` loads the model from `Path.GetFullPath("SentimentModel.mlnet")`, which depends on the process working directory.
- `Program.cs` works around that by calling `Directory.SetCurrentDirectory(AppContext.BaseDirectory)`, but the API host will not automatically inherit that behavior.
- `CsvBatchPredictor.ResolveModelPath(...)` uses its own fallback chain, so model resolution logic is currently duplicated and can drift between CLI and API flows.
- The static `SentimentModel.PredictEngine` singleton is convenient for the console app but risky for a multi-request Web API because prediction engine lifetime and concurrency are not host-managed.
- The default model asset is stored in `MLNet/SentimentModel.mlnet` and copied to the console app output. Without an explicit API-side loading strategy, `MLNET_API` may look in the wrong folder at runtime.

## Chosen Minimal Refactor Direction

- Keep training, evaluation, comparison, and CLI behavior in `MLNet` unchanged for now.
- Reuse `SentimentModel.ModelInput` and `SentimentModel.ModelOutput` as the low-level model contract.
- A small API-safe prediction service now isolates:
  - model path resolution
  - model loading/inference
  - single-item prediction mapping
- Keep file-based CSV/evaluation/reporting logic separate from request/response logic so the API can expose prediction without inheriting console/file side effects.

## Current Prediction Core After Prompt 2

- `MLNet/YearOfStudyPredictionService.cs` is now the reusable prediction core for single-record inference.
- `MLNet/StudentPredictionInput.cs` defines the API/host-friendly input contract: `StudentId`, `Age`, `Gender`, `Major`, `Gpa`.
- `MLNet/StudentPredictionResult.cs` defines the output contract: resolved `ModelPath`, `PredictedYearOfStudy`, `RawPredictedLabel`, `TopScore`, and raw `Scores`.
- `MLNet/CsvBatchPredictor.cs` now delegates row-by-row prediction to `YearOfStudyPredictionService` instead of calling `SentimentModel.Predict(...)` directly.

## Current API State After Prompt 3

- `MLNET_API` now exposes `GET /health` and `POST /predict`.
- `MLNET_API/Services/PredictionApiService.cs` adapts API contracts to `YearOfStudyPredictionService` instead of putting ML logic in controllers.
- Swagger UI is enabled via Swashbuckle at `/swagger`, which makes the API testable from browser/Postman without extra scaffolding.

## Current API State After Prompt 4

- `MLNET_API` now also exposes `POST /predict/batch` with JSON list input.
- Batch processing reuses the single-record validation and prediction path, but reports item-level errors without failing the whole batch unnecessarily.
- The response now includes `TotalRecords`, `SuccessCount`, `FailureCount`, and per-item results so the demo can show partial success clearly.

## Current External Explanation State After Prompt 5

- `POST /predict` now performs local ML inference first and then calls `GoogleGenAiPredictionExplanationService` to attach a short explanation when external configuration is available.
- External API configuration is isolated in `PredictionExplanation` settings plus user-secrets/environment variables, so the source code does not contain secrets.
- If the external provider is unavailable or not configured, the API still returns the local prediction and marks `explanation.available = false` with a fallback status/message.
- `POST /predict/batch` keeps explanation in a `not_requested` placeholder state for now so the batch contract can evolve later without breaking shape abruptly.

## Planned API Surface

- `GET /health` (implemented)
- `POST /predict` (implemented)
- `POST /predict/batch` (implemented)
- External API integration for `POST /predict` (implemented with fallback)

## Next Sequential Steps

1. If a real Google API key is provided later, verify the success-path explanation end-to-end against the live provider.
2. Decide whether batch prediction should keep `explanation = not_requested` or generate explanations per item.
3. Keep future API additions layered on the existing controller/service split instead of mixing ML/external calls into controllers.

## Changes Made in This Prompt

- `MLNet.slnx`: added `MLNET_API` to the solution so both projects build together.
- `MLNET_API/MLNET_API.csproj`: added a project reference to `MLNet`.
- `MLNET_API/Program.cs`: removed template comments and kept a clean host pipeline ready for real services/controllers.
- `MLNET_API/Controllers/WeatherForecastController.cs`: deleted the default demo controller.
- `MLNET_API/MLNET_API.http`: deleted the template sample request file.
- `MLNet/PredictionModelPathResolver.cs`: added a shared resolver that prefers the canonical project model during development and falls back to the app output copy when needed.
- `MLNet/YearOfStudyPredictionService.cs`: added a reusable single-record prediction service for CLI and API callers.
- `MLNet/StudentPredictionInput.cs` and `MLNet/StudentPredictionResult.cs`: added explicit input/output contracts for direct code-based prediction.
- `MLNet/CsvBatchPredictor.cs`: switched batch prediction to reuse the new core service instead of duplicating inference logic.
- `MLNet/SentimentModel.consumption.cs`: updated default model loading to use the shared model path resolver.

## Notes for the Next Prompt

- Preserve the current CLI commands in `MLNet/Program.cs`.
- Do not move training/evaluation/compare concerns into the API layer.
- Model resolution now prefers `MLNet/SentimentModel.mlnet` when the project can be found from the current host, and falls back to `AppContext.BaseDirectory/SentimentModel.mlnet` for output/published runs.
- Add Web API endpoints on top of `YearOfStudyPredictionService` instead of calling `CsvBatchPredictor` or CLI entry points.
- The next API extension should build on the same service layer and add batch prediction without duplicating validation or model-loading logic.
- The next stage should keep the same controller/service split and add external API integration on top of the already working local predict endpoints.
- The next stage can build on the new explanation service if external enrichment needs to be expanded beyond `predict one`.
