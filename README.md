# Year of Study Prediction with ML.NET

Project này **dự đoán `YearOfStudy` (năm học của sinh viên, nhãn 1-5)** dựa trên các thuộc tính:

- `Age`
- `Gender`
- `Major`
- `GPA`

## Phân công (nhóm 2 - Data Classification)

- **Hoàng Khôi Nguyên - DTC235210040**: phụ trách lõi dự đoán cục bộ của hệ thống.
- **Nguyễn Minh Hiếu - DTC235210023**: phụ trách lớp Web API của hệ thống (để client/ứng dụng khác gọi trực tiếp).
- **Tạ Việt Quang Khải - DTC235210074**: phụ trách tích hợp dịch vụ bên ngoài, cụ thể là `Ollama Cloud` và cấu hình API ngoài.
- **Trịnh Xuân Thiện - DTC245340018**: phụ trách luồng tổng hợp của API (ghép dự đoán cục bộ với giải thích), xử lý batch và kiểm thử toàn bộ luồng.

`StudentID` chỉ dùng để đối chiếu bản ghi khi đánh giá (evaluate), **không dùng làm feature train**.

Lưu ý quan trọng: tên file model hiện tại là `SentimentModel.mlnet` (do tên auto-generated cũ), nhưng nghiệp vụ thực tế là **phân loại YearOfStudy**, không phải sentiment.

## Mục lục

- [1. Tổng quan hệ thống](#1-tổng-quan-hệ-thống)
- [2. Kiến trúc](#2-kiến-trúc)
- [3. Dữ liệu và nhãn dự đoán](#3-dữ-liệu-và-nhãn-dự-đoán)
- [4. Pipeline ML và trainer hỗ trợ](#4-pipeline-ml-và-trainer-hỗ-trợ)
- [5. Chạy project](#5-chạy-project)
- [6. CLI workflow (MLNet)](#6-cli-workflow-MLNet)
- [7. API workflow (MLNET_API)](#7-api-workflow-mlnet_api)
- [8. Cấu hình và Ollama Cloud explanation](#8-cấu-hình-và-ollama-cloud-explanation)
- [9. Output artifacts và retention](#9-output-artifacts-và-retention)
- [10. Kết quả mẫu hiện có trong repo](#10-kết-quả-mẫu-hiện-có-trong-repo)
- [11. Giới hạn hiện tại](#11-giới-hạn-hiện-tại)
- [12. Thành viên nhóm](#12-thành-viên-nhóm)

## 1. Tổng quan hệ thống

Giải pháp gồm 2 project:

- `MLNet` (console + ML core)
  - Train / Predict / Evaluate / Compare.
  - Chứa model `.mlnet`, pipeline huấn luyện và xử lý artifact.
- `MLNET_API` (ASP.NET Core Web API)
  - Expose endpoint `health`, `predict`, `predict/batch`.
  - Tái sử dụng prediction core từ `MLNet`.
  - Có thể gọi thêm external explanation qua Ollama Cloud (tùy chọn).

## 2. Kiến trúc

```text
Client (Swagger/Postman)
        |
        v
MLNET_API (ASP.NET Core)
  |- HealthController
  |- PredictionController
  |- PredictionApiService
       |- YearOfStudyPredictionService (MLNet)
       |    |- SentimentModel.mlnet
       |
       |- OllamaCloudPredictionExplanationService (optional)
            |- https://ollama.com/api/generate
            |- Bearer API key
```

Nguyên tắc luồng:

1. Luôn dự đoán cục bộ trước.
2. Chỉ endpoint `POST /predict` mới gọi explanation service.
3. Nếu explanation lỗi, API vẫn trả kết quả dự đoán cục bộ.

## 3. Dữ liệu và nhãn dự đoán

### 3.1 File dữ liệu chính

- Train dataset: `MLNet/Student_mental_health_data.csv`
- Predict input: `MLNet/data_set_1.csv`
- Ground truth để evaluate: `MLNet/data_set_1_full.csv`

### 3.2 Schema dữ liệu

Các cột xuất hiện trong dữ liệu gốc:

- `StudentID`
- `Age`
- `Gender`
- `YearOfStudy` (label)
- `Major`
- `GPA`

Trong API/CLI predict, input thực tế gồm:

- `StudentId` (optional)
- `Age`
- `Gender`
- `Major`
- `Gpa`

Output chính:

- `PredictedYearOfStudy` (int)
- `RawPredictedLabel` (float)
- `TopScore`
- `Scores` (mảng điểm theo lớp)

## 4. Pipeline ML và trainer hỗ trợ

Pipeline huấn luyện hiện tại:

1. One-hot cho `Gender`
2. Replace missing values cho `Age`, `GPA`
3. Featurize text cho `Major`
4. Concatenate features: `Gender`, `Age`, `GPA`, `Major`
5. Map `YearOfStudy` sang key label
6. Train multiclass
7. Map key predicted label về value

Trainer hỗ trợ qua CLI:

- `lbfgs` (`lbfgs-maxent`)
- `sdca` (`sdca-maxent`)
- `lightgbm` (`lightgbm-multiclass`)

Metrics train được log:

- `MacroAccuracy`
- `MicroAccuracy`
- `LogLoss`
- `LogLossReduction`
- `DurationSeconds`

## 5. Chạy project

Yêu cầu:

- .NET SDK hỗ trợ `net10.0`

Build toàn bộ solution:

```powershell
dotnet build .\MLNet.slnx
```

## 6. CLI workflow (MLNet)

### 6.1 Train

```powershell
dotnet run --project .\MLNet\MLNet.csproj -- train
```

Ví dụ chọn trainer/tuning:

```powershell
dotnet run --project .\MLNet\MLNet.csproj -- train --trainer sdca --seed 2026 --test-fraction 0.2 --run-name sdca_baseline
dotnet run --project .\MLNet\MLNet.csproj -- train --trainer lightgbm --learning-rate 0.1 --number-of-leaves 64 --number-of-iterations 300 --max-bins 255
```

### 6.2 Predict

```powershell
dotnet run --project .\MLNet\MLNet.csproj -- predict .\MLNet\data_set_1.csv --model .\MLNet\SentimentModel.mlnet
```

### 6.3 Evaluate

```powershell
dotnet run --project .\MLNet\MLNet.csproj -- evaluate .\MLNet\data_set_1.csv .\MLNet\data_set_1_full.csv --model .\MLNet\SentimentModel.mlnet
```

### 6.4 Compare

```powershell
dotnet run --project .\MLNet\MLNet.csproj -- compare --eval-dir .\MLNet
```

CLI exit code:

- `0`: thành công
- `1`: lỗi input/path/command
- `2`: lỗi train

## 7. API workflow (MLNET_API)

### 7.1 Chạy API

```powershell
dotnet run --project .\MLNET_API\MLNET_API.csproj --launch-profile http
```

Swagger:

- `http://localhost:5157/swagger`

### 7.2 Endpoints

#### `GET /health`

- Trả `200` khi model load được (`modelReady = true`)
- Trả `503` khi model chưa sẵn sàng

#### `POST /predict`

Input validation chính:

- `StudentId`: nếu có thì phải `>= 1`
- `Age`: từ `15` đến `100`
- `Gender`: bắt buộc, tối đa 50 ký tự
- `Major`: bắt buộc, tối đa 200 ký tự
- `Gpa`: từ `0` đến `4`

Response gồm:

- dự đoán cục bộ (`predictedYearOfStudy`, `scores`, ...)
- `explanation` (nếu bật provider ngoài)

#### `POST /predict/batch`

- Nhận danh sách `students`
- Mỗi item có trạng thái thành công/thất bại riêng
- Batch hiện **không** gọi explanation, nên trả `explanation.status = not_requested` cho item thành công

## 8. Cấu hình và Ollama Cloud explanation

File cấu hình: `MLNET_API/appsettings.json`

```json
"Prediction": {
  "ModelPath": ""
},
"PredictionExplanation": {
  "Enabled": true,
  "Provider": "ollama-cloud",
  "ApiKey": "",
  "BaseUrl": "https://ollama.com/api",
  "Model": "gemma3:4b-cloud",
  "TimeoutMilliseconds": 10000
}
```

Thiết lập API key bằng user-secrets:

```powershell
dotnet user-secrets set "PredictionExplanation:ApiKey" "<your-ollama-api-key>" --project .\MLNET_API\MLNET_API.csproj
dotnet user-secrets set "PredictionExplanation:BaseUrl" "https://ollama.com/api" --project .\MLNET_API\MLNET_API.csproj
dotnet user-secrets set "PredictionExplanation:Model" "gemma3:4b-cloud" --project .\MLNET_API\MLNET_API.csproj
```

Hoặc biến môi trường:

```powershell
$env:OLLAMA_API_KEY = "<your-ollama-api-key>"
$env:PredictionExplanation__BaseUrl = "https://ollama.com/api"
$env:PredictionExplanation__Model = "gemma3:4b-cloud"
```

Một số trạng thái `explanation.status` có thể gặp:

- `success`
- `missing_api_key`
- `missing_model`
- `missing_base_url`
- `invalid_base_url`
- `insecure_base_url`
- `local_base_url_not_allowed`
- `unauthorized`
- `model_not_found`
- `endpoint_not_found`
- `connection_error`
- `timeout`
- `http_error`
- `empty_response`
- `error`

## 9. Output artifacts và retention

### 9.1 Training metadata

- `MLNet/training_runs/training_runs.jsonl`
- `MLNet/training_runs/training_runs.csv`

### 9.2 Predict/Evaluate outputs

- `predictions_yyyyMMdd_HHmmss.csv`
- `summary_predict_yyyyMMdd_HHmmss.json`
- `evaluation_yyyyMMdd_HHmmss.csv`
- `summary_evaluation_yyyyMMdd_HHmmss.json`

### 9.3 Compare outputs

- `comparison_report_yyyyMMdd_HHmmss.csv`
- `comparison_report_yyyyMMdd_HHmmss.json`

### 9.4 Retention policy

Tự động giữ **5 run mới nhất** cho mỗi nhóm artifact predict/evaluate/compare.

## 10. Kết quả mẫu hiện có trong repo

Từ file `MLNet/summary_evaluation_20260401_133641.json`:

- `totalSamples`: 2000
- `correctPredictions`: 430
- `accuracy`: 0.215 (21.5%)

Đây là kết quả mẫu theo model hiện có trong repo tại thời điểm chạy evaluate tương ứng.

## 11. Giới hạn hiện tại

- Chưa có auth/database/frontend trong phạm vi demo này.
- Tên model/class còn legacy (`SentimentModel`) dễ gây hiểu nhầm, nhưng nghiệp vụ là YearOfStudy classification.
- Để correlate tốt giữa evaluate summary và training runs trong compare, nên truyền rõ `--model .\MLNet\SentimentModel.mlnet` khi chạy predict/evaluate.

## 12. Thành viên nhóm

- Hoàng Khôi Nguyên - DTC235210040: lõi dự đoán cục bộ.
- Nguyễn Minh Hiếu - DTC235210023: lớp Web API.
- Tạ Việt Quang Khải - DTC235210074: tích hợp Ollama Cloud và cấu hình API ngoài.
- Trịnh Xuân Thiện - DTC245340018: luồng tổng hợp API, batch, kiểm thử luồng.
