# MLTest CLI Usage (YearOfStudy)

Project hỗ trợ 2 luồng bài tập:

1. **Bài số 1**: Train -> Predict -> Evaluate trên bộ dữ liệu đã cho.
2. **Bài số 2**: Tuning nhiều cấu hình train và so sánh kết quả bằng report tổng hợp.

## Datasets và target

- Train dataset: `Student_mental_health_data.csv`
- Predict input: `data_set_1.csv`
- Ground truth: `data_set_1_full.csv`
- Label/Target: `YearOfStudy`

> Lưu ý: `StudentID` chỉ dùng để đối chiếu bản ghi khi evaluate, **không** dùng làm feature train.

## Commands

### 1) Train

- Baseline:
	- `dotnet run --project .\MLTest\MLTest.csproj -- train`

- Chọn trainer + tham số:
	- `dotnet run --project .\MLTest\MLTest.csproj -- train --trainer sdca --seed 2026 --test-fraction 0.2 --run-name sdca_baseline`
	- `dotnet run --project .\MLTest\MLTest.csproj -- train --trainer lightgbm --learning-rate 0.1 --number-of-leaves 64 --number-of-iterations 300 --max-bins 255`

- Option chính cho train:
	- `--trainer <lbfgs|sdca|lightgbm>`
	- `--data <train-csv>`
	- `--model-out <model-path>`
	- `--runs-dir <dir chứa training_runs.jsonl/csv>`
	- `--seed <int>`
	- `--test-fraction <float (0,1)>`
	- `--run-name <string>`
	- `--learning-rate <float>`
	- `--number-of-leaves <int>`
	- `--number-of-iterations <int>`
	- `--max-bins <int>`
	- `--l2 <float>`

### 2) Predict

- `dotnet run --project .\MLTest\MLTest.csproj -- predict .\MLTest\data_set_1.csv --model .\MLTest\SentimentModel.mlnet`

- Option:
	- `--model <model-path>`
	- `--out <predictions-csv>`
	- `--summary <summary-json>`
	- `--verbose`

### 3) Evaluate

- `dotnet run --project .\MLTest\MLTest.csproj -- evaluate .\MLTest\data_set_1.csv .\MLTest\data_set_1_full.csv --model .\MLTest\SentimentModel.mlnet`

- Option:
	- `--model <model-path>`
	- `--out <evaluation-csv>`
	- `--summary <summary-json>`
	- `--verbose`

### 4) Compare (cho bài số 2)

- `dotnet run --project .\MLTest\MLTest.csproj -- compare --eval-dir .\MLTest`

- Mặc định compare đọc training log tại:
	- `MLTest\training_runs\training_runs.jsonl`
- Có thể override bằng:
	- `--runs-dir <dir chứa training_runs.jsonl>` hoặc `--training-log <path tới training_runs.jsonl>`

- Option:
	- `--runs-dir <dir chứa training_runs.jsonl>`
	- `--training-log <training_runs.jsonl>`
	- `--eval-dir <dir chứa summary_evaluation_*.json>`
	- `--out <comparison.csv>`
	- `--out-json <comparison.json>`

## Output files

### Train

- Model: mặc định `MLTest\SentimentModel.mlnet` (hoặc theo `--model-out`)
- Training metadata:
	- `MLTest\training_runs\training_runs.jsonl`
	- `MLTest\training_runs\training_runs.csv`
	- (hoặc theo `--runs-dir` nếu truyền lúc train)

### Predict / Evaluate

- Predict CSV: `predictions_<timestamp>.csv`
- Evaluate CSV: `evaluation_<timestamp>.csv`
- Predict summary JSON: `summary_predict_<timestamp>.json`
- Evaluate summary JSON: `summary_evaluation_<timestamp>.json`

### Compare

- Comparison CSV: `comparison_report_<timestamp>.csv`
- Comparison JSON: `comparison_report_<timestamp>.json`

## Output retention (tự động)

Project tự động dọn output cũ sau khi command chạy thành công, không cần lệnh `clean` riêng.

- `predict`:
	- giữ **5 run** mới nhất cho `predictions_<timestamp>.csv`
	- và `summary_predict_<timestamp>.json` (khi cùng/khác thư mục theo chế độ an toàn)
- `evaluate`:
	- giữ **5 run** mới nhất theo **cặp run đồng bộ**:
		- `evaluation_<timestamp>.csv`
		- `summary_evaluation_<timestamp>.json`
- `compare`:
	- giữ **5 run** mới nhất theo **cặp run đồng bộ**:
		- `comparison_report_<timestamp>.csv`
		- `comparison_report_<timestamp>.json`

### Các file không bị prune

- `training_runs/training_runs.jsonl`
- `training_runs/training_runs.csv`
- Datasets nguồn:
	- `data_set_1.csv`
	- `data_set_1_full.csv`
	- `Student_mental_health_data.csv`
- Model mặc định đang dùng: `SentimentModel.mlnet`

`compare` tiếp tục đọc training logs bình thường vì retention chỉ áp dụng cho output report đúng pattern.

## Flow khuyến nghị cho bài số 2

- Train để sinh metadata:
	- `dotnet run --project .\MLTest\MLTest.csproj -- train --trainer sdca --run-name exp_sdca_01`
- Compare ngay sau train (đọc cùng training_runs mặc định):
	- `dotnet run --project .\MLTest\MLTest.csproj -- compare`
- Nếu dùng thư mục metadata riêng:
	- `dotnet run --project .\MLTest\MLTest.csproj -- train --runs-dir .\MLTest\training_runs_custom --run-name exp_custom_01`
	- `dotnet run --project .\MLTest\MLTest.csproj -- compare --runs-dir .\MLTest\training_runs_custom`

## Metric dùng để phân tích bài số 2

- Ưu tiên chính: **Accuracy** (từ evaluate summary).
- Metric bổ sung: `MacroAccuracy`, `MicroAccuracy`, `LogLoss`, `DurationSeconds`.

## Exit codes

- `0`: thành công
- `1`: lỗi input/path/command
- `2`: lỗi train model
