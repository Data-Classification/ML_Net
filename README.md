# Demo API MLTest

## Phân công (nhóm 2 – Data Classification)

Mục tiêu: tách phần machine learning ra khỏi lớp API để hệ thống có thể tái sử dụng mô hình dự đoán một cách rõ ràng và ổn định.

- **Hoàng Khôi Nguyên**: phụ trách lõi dự đoán cục bộ của hệ thống.
- **Nguyễn Minh Hiếu**: phụ trách lớp Web API của hệ thống (để client/ứng dụng khác gọi trực tiếp).
- **Tạ Việt Quang Khải**: phụ trách tích hợp dịch vụ bên ngoài, cụ thể là `Ollama Cloud`.
- **Trịnh Xuân Thiện**: phụ trách luồng tổng hợp của API (ghép dự đoán cục bộ với giải thích), xử lý batch và kiểm thử toàn bộ luồng.

## Tổng quan

Giải pháp này gồm 2 dự án:

- **MLTest**: dự án console/ML hiện có, sở hữu các phần huấn luyện (training), đánh giá (evaluation), so sánh (comparison), lưu artifacts và **lõi dự đoán cục bộ (local prediction core)** có thể tái sử dụng.
- **MLNET_API**: ASP.NET Core Web API để cung cấp các endpoint: health check, dự đoán 1 mẫu và dự đoán theo lô (batch).

Hệ thống có thể gọi dịch vụ giải thích (explanation) bên ngoài qua **Ollama Cloud** (provider: `ollama-cloud`). Luồng an toàn được giữ nguyên:

1. Luôn chạy dự đoán cục bộ trước.
2. Chỉ gọi Ollama Cloud cho `POST /predict`.
3. Nếu giải thích từ dịch vụ ngoài bị lỗi, API **vẫn trả về kết quả dự đoán cục bộ**.

## Kiến trúc

```text
Client / Swagger / Postman
        |
        v
    MLNET_API (Web API)
        |
        +--> PredictionApiService
                |
                +--> MLTest/YearOfStudyPredictionService (dự đoán cục bộ)
                |       |
                |       +--> SentimentModel.mlnet
                |
                +--> OllamaCloudPredictionExplanationService (tùy chọn)
                        |
                        +--> https://ollama.com/api/generate
                        +--> Authorization: Bearer <API_KEY>
        |
        v
   JSON response
```

## Các luồng đã triển khai

- `GET /health`
- `POST /predict`
- `POST /predict/batch`
- Cơ chế fallback cho `POST /predict` khi Ollama Cloud không khả dụng

Chủ động **không làm** trong phạm vi hiện tại:

- Auth / database / frontend
- API upload CSV
- API evaluate / compare
- Sinh giải thích (explanation) cho batch

## Chạy cục bộ

1. Build toàn bộ giải pháp:

```powershell
dotnet build .\MLTest.slnx
```

2. (Tùy chọn) Cấu hình Ollama Cloud để sinh explanation.

Thiết lập bằng user-secrets:

```powershell
dotnet user-secrets set "PredictionExplanation:ApiKey" "<your-ollama-api-key>" --project .\MLNET_API\MLNET_API.csproj
dotnet user-secrets set "PredictionExplanation:BaseUrl" "https://ollama.com/api" --project .\MLNET_API\MLNET_API.csproj
dotnet user-secrets set "PredictionExplanation:Model" "gemma3:4b-cloud" --project .\MLNET_API\MLNET_API.csproj
```

Hoặc dùng biến môi trường:

```powershell
$env:OLLAMA_API_KEY = "<your-ollama-api-key>"
$env:PredictionExplanation__BaseUrl = "https://ollama.com/api"
$env:PredictionExplanation__Model = "gemma3:4b-cloud"
```

3. Chạy API:

```powershell
dotnet run --project .\MLNET_API\MLNET_API.csproj --launch-profile http
```

4. Mở Swagger:

- `http://localhost:5157/swagger`

## Cấu hình

File `MLNET_API/appsettings.json` có 2 nhóm cấu hình chính:

- `Prediction:ModelPath`
  Ghi đè (tùy chọn) đường dẫn model. Nếu để trống, API sẽ tự resolve `MLTest/SentimentModel.mlnet` khi dev cục bộ và fallback sang bản copy trong output khi cần.
- `PredictionExplanation`
  Điều khiển việc sinh explanation từ Ollama Cloud:
  - `Enabled`
  - `Provider`
  - `ApiKey`
  - `BaseUrl`
  - `Model`
  - `TimeoutMilliseconds`

Không lưu API key thật vào `appsettings.json`.

## Hành vi giải thích (Ollama Cloud)

- Provider mặc định: `ollama-cloud`
- Endpoint đích: `https://ollama.com/api/generate`
- Xác thực: `Authorization: Bearer <API_KEY>`
- Chế độ request: `stream=false`
- Thời điểm chạy: dự đoán cục bộ trước, giải thích sau, **chỉ** cho `POST /predict`
- Cơ chế fallback: nếu thiếu API key, API key bị từ chối, base URL không hợp lệ/không truy cập được, bị timeout, hoặc response rỗng… API vẫn trả về dự đoán cục bộ và đánh dấu `explanation.available = false`

Ví dụ payload chi tiết: xem [API_USAGE.md](API_USAGE.md).

## Tóm tắt kiểm chứng thủ công

Lần kiểm chứng cục bộ gần nhất: **15/04/2026**

- Build giải pháp: đạt
- Swagger UI: dùng được
- `GET /health`: đạt
- `POST /predict` không có API key: đạt, vẫn trả dự đoán cục bộ và `explanation.status = missing_api_key`
- `POST /predict` dùng API key giả với Ollama Cloud: đạt, vẫn trả dự đoán cục bộ và `explanation.status = unauthorized`
- `POST /predict` với base URL sai `https://example.invalid/api`: đạt, vẫn trả dự đoán cục bộ và `explanation.status = connection_error`
- `POST /predict/batch`: vẫn chạy và giữ `explanation = not_requested`

Giới hạn môi trường hiện tại:

- Không có API key Ollama Cloud thật trong biến môi trường hoặc user-secrets tại thời điểm kiểm chứng, nên luồng “explanation thành công thật” đã sẵn sàng về mặt code nhưng chưa được xác nhận bằng runtime.

## Tệp quan trọng

- [MLNET_API/Program.cs](MLNET_API/Program.cs)
- [MLNET_API/Controllers/PredictionController.cs](MLNET_API/Controllers/PredictionController.cs)
- [MLNET_API/Services/PredictionApiService.cs](MLNET_API/Services/PredictionApiService.cs)
- [MLNET_API/Services/OllamaCloudPredictionExplanationService.cs](MLNET_API/Services/OllamaCloudPredictionExplanationService.cs)
- [MLTest/YearOfStudyPredictionService.cs](MLTest/YearOfStudyPredictionService.cs)
- [API_USAGE.md](API_USAGE.md)
