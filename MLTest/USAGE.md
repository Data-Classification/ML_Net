# MLTest CLI Usage (YearOfStudy)

Luồng chuẩn bài kiểm tra:

1. Train/retrain model từ `Student_mental_health_data.csv`
2. Predict trên `data_set_1.csv`
3. Evaluate bằng `data_set_1_full.csv` để lấy accuracy

## Commands

- Train:
	- `dotnet run --project .\MLTest\MLTest.csproj -- train`

- Predict:
	- `dotnet run --project .\MLTest\MLTest.csproj -- predict .\MLTest\data_set_1.csv`

- Evaluate:
	- `dotnet run --project .\MLTest\MLTest.csproj -- evaluate .\MLTest\data_set_1.csv .\MLTest\data_set_1_full.csv`

## Output files

- Predict mode tạo: `predictions_data_set_1.csv`
- Evaluate mode tạo: `evaluation_data_set_1.csv`

## Exit codes

- `0`: thành công
- `1`: lỗi input/thiếu file/command sai
- `2`: lỗi train model
