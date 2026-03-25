using System.Globalization;
using System.Text;
using static System.Console;

namespace MLTest;

internal static class CsvBatchPredictor
{
    internal sealed class RunOptions
    {
        public bool VerbosePerRow { get; init; }
        public int MaxPreviewRows { get; init; } = 20;
        public string? GroundTruthCsvPath { get; init; }
        public string? OutputCsvPath { get; init; }
    }

    private static readonly string[] PredictColumns =
    [
        "StudentID",
        "Age",
        "Gender",
        "Major",
        "GPA"
    ];

    private const string LabelColumn = "YearOfStudy";

    public static int Run(string predictCsvPath, RunOptions? options = null)
    {
        options ??= new RunOptions();

        if (string.IsNullOrWhiteSpace(predictCsvPath))
        {
            WriteLine("ERROR: Predict CSV path is empty.");
            return 1;
        }

        if (!File.Exists(predictCsvPath))
        {
            WriteLine($"ERROR: Predict CSV file not found: {predictCsvPath}");
            return 1;
        }

        var modelPath = ResolveModelPath();
        if (modelPath is null)
        {
            WriteLine("ERROR: Model file not found: SentimentModel.mlnet");
            return 1;
        }

        if (!TryReadPredictRows(predictCsvPath, out var predictRows, out var predictHeader, out var predictReadError))
        {
            WriteLine($"ERROR: {predictReadError}");
            return 1;
        }

        List<GroundTruthRow>? groundTruthRows = null;
        if (!string.IsNullOrWhiteSpace(options.GroundTruthCsvPath))
        {
            if (!TryReadGroundTruthRows(options.GroundTruthCsvPath, out groundTruthRows, out var groundTruthError))
            {
                WriteLine($"ERROR: {groundTruthError}");
                return 1;
            }
        }

        var evaluateMode = groundTruthRows is { Count: > 0 };
        var mode = evaluateMode ? "Evaluate" : "Predict-only";

        WriteLine(evaluateMode
            ? "=== Evaluate YearOfStudy model on labeled CSV ==="
            : "=== Predict YearOfStudy from CSV (predict-only) ===");

        WriteLine($"Predict CSV       : {predictCsvPath}");
        WriteLine($"Ground truth CSV  : {(evaluateMode ? options.GroundTruthCsvPath : "(none)")}");
        WriteLine($"Model             : {modelPath}");
        WriteLine($"Predict columns   : {string.Join(", ", predictHeader)}");
        WriteLine($"Mode              : {mode}");
        WriteLine($"Verbose row logs  : {(options.VerbosePerRow ? "On" : "Off")}");
        WriteLine();

        var joinedGroundTruth = evaluateMode
            ? BuildGroundTruthLookup(groundTruthRows!)
            : null;

        var results = new List<PredictionResultRow>(predictRows.Count);
        var detailedRowsPrinted = 0;

        foreach (var row in predictRows)
        {
            SentimentModel.ModelOutput result;
            try
            {
                var input = new SentimentModel.ModelInput
                {
                    StudentID = row.StudentId ?? 0,
                    Age = row.Age,
                    Gender = row.Gender,
                    Major = row.Major,
                    GPA = row.Gpa
                };
                result = SentimentModel.Predict(input);
            }
            catch (Exception ex)
            {
                WriteLine($"[WARN] Row {row.SourceRowNumber}: prediction failed - {ex.Message}");
                continue;
            }

            var predictedLabel = Convert.ToInt32(MathF.Round(result.PredictedLabel), CultureInfo.InvariantCulture);
            var topScore = result.Score is { Length: > 0 } ? result.Score.Max() : float.NaN;

            int? actual = null;
            bool? isCorrect = null;

            if (evaluateMode && joinedGroundTruth is not null)
            {
                var matched = TryResolveActualYear(row, joinedGroundTruth);
                actual = matched.ActualYearOfStudy;
                if (actual.HasValue)
                {
                    isCorrect = predictedLabel == actual.Value;
                }
            }

            var resultRow = new PredictionResultRow(
                row.SequenceIndex,
                row.StudentId,
                row.Age,
                row.Gender,
                row.Major,
                row.Gpa,
                actual,
                predictedLabel,
                isCorrect,
                topScore);

            results.Add(resultRow);

            TryWriteRowDetail(options, ref detailedRowsPrinted, resultRow);
        }

        if (results.Count == 0)
        {
            WriteLine("ERROR: No valid predictions were produced.");
            return 1;
        }

        if (!options.VerbosePerRow && results.Count > detailedRowsPrinted)
        {
            WriteLine();
            WriteLine($"[INFO] Row-level output limited to first {detailedRowsPrinted} rows. Use --verbose to print all rows.");
        }

        var outputPath = ResolveOutputPath(predictCsvPath, options.OutputCsvPath, evaluateMode);
        WriteResultsCsv(outputPath, results);

        var summary = BuildSummary(results);

        WriteLine();
        WriteLine("================ SUMMARY ================");
        WriteLine($"Total samples            : {summary.TotalSamples}");
        WriteLine($"Predicted samples        : {summary.PredictedSamples}");
        WriteLine($"Rows with actual label   : {summary.RowsWithActual}");
        WriteLine($"Correct predictions      : {summary.CorrectPredictions}");
        WriteLine($"Incorrect predictions    : {summary.IncorrectPredictions}");
        WriteLine($"Accuracy                 : {(summary.Accuracy.HasValue ? summary.Accuracy.Value.ToString("P2", CultureInfo.InvariantCulture) : "N/A")}");
        WriteLine($"Output CSV               : {outputPath}");

        WriteLine();
        WriteLine("Predicted YearOfStudy distribution:");
        var totalPred = summary.PredictedDistribution.Values.Sum();
        foreach (var kv in summary.PredictedDistribution.OrderBy(k => k.Key))
        {
            var count = kv.Value;
            var ratio = totalPred > 0 ? (double)count / totalPred : 0;
            WriteLine($"  Label {kv.Key}: Count={count}, Ratio={ratio:P2}");
        }

        if (summary.RowsWithActual > 0)
        {
            WriteLine();
            WriteLine("Actual YearOfStudy distribution:");
            foreach (var kv in summary.ActualDistribution.OrderBy(k => k.Key))
            {
                var actualCount = kv.Value;
                var actualRatio = summary.RowsWithActual > 0 ? (double)actualCount / summary.RowsWithActual : 0;
                WriteLine($"  Label {kv.Key}: Count={actualCount}, Ratio={actualRatio:P2}");
            }

            if (joinedGroundTruth is not null && joinedGroundTruth.FallbackToRowOrderCount > 0)
            {
                WriteLine();
                WriteLine($"[INFO] Fallback matched by row order for {joinedGroundTruth.FallbackToRowOrderCount} record(s) where StudentID could not be matched.");
            }
        }
        else
        {
            WriteLine();
            WriteLine("[INFO] No ground-truth file was provided. Accuracy is available only in Evaluate mode.");
        }

        return 0;
    }

    private static void TryWriteRowDetail(RunOptions options, ref int detailedRowsPrinted, PredictionResultRow row)
    {
        var message = row.ActualYearOfStudy.HasValue
            ? $"Row {row.SequenceIndex}: StudentID={DisplayNullableInt(row.StudentId)}, Age={row.Age.ToString("0.##", CultureInfo.InvariantCulture)}, Gender={row.Gender}, Major={row.Major}, GPA={row.Gpa.ToString("0.##", CultureInfo.InvariantCulture)}, Actual={row.ActualYearOfStudy.Value}, Predicted={row.PredictedYearOfStudy}, Correct={(row.IsCorrect == true ? "Y" : "N")}, TopScore={DisplayScore(row.TopScore)}"
            : $"Row {row.SequenceIndex}: StudentID={DisplayNullableInt(row.StudentId)}, Age={row.Age.ToString("0.##", CultureInfo.InvariantCulture)}, Gender={row.Gender}, Major={row.Major}, GPA={row.Gpa.ToString("0.##", CultureInfo.InvariantCulture)}, Predicted={row.PredictedYearOfStudy}, TopScore={DisplayScore(row.TopScore)}";

        if (options.VerbosePerRow)
        {
            WriteLine(message);
            detailedRowsPrinted++;
            return;
        }

        if (detailedRowsPrinted < options.MaxPreviewRows)
        {
            WriteLine(message);
            detailedRowsPrinted++;
        }
    }

    private static string DisplayNullableInt(int? value) => value.HasValue ? value.Value.ToString(CultureInfo.InvariantCulture) : "N/A";

    private static string DisplayScore(float score) => float.IsNaN(score)
        ? "N/A"
        : score.ToString("0.####", CultureInfo.InvariantCulture);

    private static bool TryReadPredictRows(string csvPath, out List<PredictRow> rows, out string[] header, out string error)
    {
        rows = new List<PredictRow>();
        header = [];
        error = string.Empty;

        if (!TryReadHeader(csvPath, out header, out var headerMap, out var delimiter, out error))
        {
            return false;
        }

        var missingRequired = PredictColumns.Where(c => !headerMap.ContainsKey(c)).ToList();
        if (missingRequired.Count > 0)
        {
            error = $"Missing required columns in predict CSV: {string.Join(", ", missingRequired)}";
            return false;
        }

        var sourceRow = 1;
        var sequence = 0;
        foreach (var raw in File.ReadLines(csvPath).Skip(1))
        {
            sourceRow++;
            if (string.IsNullOrWhiteSpace(raw))
            {
                continue;
            }

            var fields = ParseCsvLine(raw, delimiter);

            if (!TryGetRequiredFloat(fields, headerMap, "StudentID", out var studentIdValue, out var parseError)
                || !TryGetRequiredFloat(fields, headerMap, "Age", out var age, out parseError)
                || !TryGetRequiredText(fields, headerMap, "Gender", out var gender, out parseError)
                || !TryGetRequiredText(fields, headerMap, "Major", out var major, out parseError)
                || !TryGetRequiredFloat(fields, headerMap, "GPA", out var gpa, out parseError))
            {
                WriteLine($"[WARN] Predict row {sourceRow}: {parseError}");
                continue;
            }

            sequence++;
            rows.Add(new PredictRow(
                sourceRow,
                sequence,
                Convert.ToInt32(MathF.Round(studentIdValue), CultureInfo.InvariantCulture),
                age,
                gender,
                major,
                gpa));
        }

        if (rows.Count == 0)
        {
            error = "Predict CSV has no valid data rows after parsing.";
            return false;
        }

        return true;
    }

    private static bool TryReadGroundTruthRows(string csvPath, out List<GroundTruthRow> rows, out string error)
    {
        rows = new List<GroundTruthRow>();
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(csvPath))
        {
            error = "Ground-truth CSV path is empty.";
            return false;
        }

        if (!File.Exists(csvPath))
        {
            error = $"Ground-truth CSV file not found: {csvPath}";
            return false;
        }

        if (!TryReadHeader(csvPath, out _, out var headerMap, out var delimiter, out error))
        {
            return false;
        }

        var requiredColumns = new[] { "StudentID", LabelColumn };
        var missingRequired = requiredColumns.Where(c => !headerMap.ContainsKey(c)).ToList();
        if (missingRequired.Count > 0)
        {
            error = $"Missing required columns in ground-truth CSV: {string.Join(", ", missingRequired)}";
            return false;
        }

        var sourceRow = 1;
        var sequence = 0;
        foreach (var raw in File.ReadLines(csvPath).Skip(1))
        {
            sourceRow++;
            if (string.IsNullOrWhiteSpace(raw))
            {
                continue;
            }

            var fields = ParseCsvLine(raw, delimiter);
            if (!TryGetRequiredFloat(fields, headerMap, "StudentID", out var studentIdValue, out var parseError)
                || !TryReadOptionalLabel(fields, headerMap, out var actual, out parseError))
            {
                WriteLine($"[WARN] Ground-truth row {sourceRow}: {parseError}");
                continue;
            }

            sequence++;
            rows.Add(new GroundTruthRow(
                sourceRow,
                sequence,
                Convert.ToInt32(MathF.Round(studentIdValue), CultureInfo.InvariantCulture),
                actual));
        }

        if (rows.Count == 0)
        {
            error = "Ground-truth CSV has no valid data rows after parsing.";
            return false;
        }

        return true;
    }

    private static bool TryReadHeader(
        string csvPath,
        out string[] header,
        out Dictionary<string, int> headerMap,
        out char delimiter,
        out string error)
    {
        header = [];
        headerMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        delimiter = ',';
        error = string.Empty;

        var lines = File.ReadLines(csvPath).GetEnumerator();
        if (!lines.MoveNext())
        {
            error = $"CSV file is empty: {csvPath}";
            return false;
        }

        var headerLine = lines.Current ?? string.Empty;
        delimiter = DetectDelimiter(headerLine);
        header = ParseCsvLine(headerLine, delimiter)
            .Select(h => h.Trim())
            .ToArray();

        if (header.Length == 0)
        {
            error = $"Cannot parse CSV header: {csvPath}";
            return false;
        }

        for (int i = 0; i < header.Length; i++)
        {
            if (!headerMap.ContainsKey(header[i]))
            {
                headerMap[header[i]] = i;
            }
        }

        return true;
    }

    private static GroundTruthLookup BuildGroundTruthLookup(List<GroundTruthRow> rows)
    {
        var byStudentId = new Dictionary<int, Queue<GroundTruthRow>>();
        var byRowOrder = rows.ToDictionary(r => r.SequenceIndex, r => r);
        return new GroundTruthLookup(byStudentId, byRowOrder);
    }

    private static GroundTruthMatch TryResolveActualYear(PredictRow row, GroundTruthLookup lookup)
    {
        if (row.StudentId.HasValue)
        {
            if (!lookup.ByStudentId.TryGetValue(row.StudentId.Value, out var queue))
            {
                queue = new Queue<GroundTruthRow>(
                    lookup.ByRowOrder.Values.Where(g => g.StudentId == row.StudentId.Value));
                lookup.ByStudentId[row.StudentId.Value] = queue;
            }

            if (queue.Count > 0)
            {
                var matched = queue.Dequeue();
                lookup.ByRowOrder.Remove(matched.SequenceIndex);
                return new GroundTruthMatch(matched.ActualYearOfStudy);
            }
        }

        if (lookup.ByRowOrder.TryGetValue(row.SequenceIndex, out var fallback))
        {
            lookup.ByRowOrder.Remove(row.SequenceIndex);
            lookup.FallbackToRowOrderCount++;
            return new GroundTruthMatch(fallback.ActualYearOfStudy);
        }

        return new GroundTruthMatch(null);
    }

    private static PredictionSummary BuildSummary(List<PredictionResultRow> rows)
    {
        var total = rows.Count;
        var rowsWithActual = rows.Count(r => r.ActualYearOfStudy.HasValue);
        var correct = rows.Count(r => r.IsCorrect == true);
        var incorrect = rows.Count(r => r.IsCorrect == false);

        var predictedDistribution = rows
            .GroupBy(r => r.PredictedYearOfStudy)
            .ToDictionary(g => g.Key, g => g.Count());

        var actualDistribution = rows
            .Where(r => r.ActualYearOfStudy.HasValue)
            .GroupBy(r => r.ActualYearOfStudy!.Value)
            .ToDictionary(g => g.Key, g => g.Count());

        var accuracy = rowsWithActual > 0 ? (double)correct / rowsWithActual : (double?)null;

        return new PredictionSummary(
            total,
            total,
            rowsWithActual,
            correct,
            incorrect,
            accuracy,
            predictedDistribution,
            actualDistribution);
    }

    private static void WriteResultsCsv(string outputPath, List<PredictionResultRow> rows)
    {
        var directory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        using var writer = new StreamWriter(outputPath, append: false, Encoding.UTF8);
        writer.WriteLine("StudentID,Age,Gender,Major,GPA,ActualYearOfStudy,PredictedYearOfStudy,IsCorrect,TopScore");
        foreach (var row in rows)
        {
            var values = new[]
            {
                row.StudentId?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                row.Age.ToString("0.####", CultureInfo.InvariantCulture),
                row.Gender,
                row.Major,
                row.Gpa.ToString("0.####", CultureInfo.InvariantCulture),
                row.ActualYearOfStudy?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                row.PredictedYearOfStudy.ToString(CultureInfo.InvariantCulture),
                row.IsCorrect.HasValue ? (row.IsCorrect.Value ? "true" : "false") : string.Empty,
                float.IsNaN(row.TopScore) ? string.Empty : row.TopScore.ToString("0.####", CultureInfo.InvariantCulture)
            };

            writer.WriteLine(string.Join(',', values.Select(EscapeCsvValue)));
        }
    }

    private static string ResolveOutputPath(string predictCsvPath, string? explicitPath, bool evaluateMode)
    {
        if (!string.IsNullOrWhiteSpace(explicitPath))
        {
            return Path.GetFullPath(explicitPath);
        }

        var dir = Path.GetDirectoryName(Path.GetFullPath(predictCsvPath)) ?? AppContext.BaseDirectory;
        var fileName = evaluateMode ? "evaluation_data_set_1.csv" : "predictions_data_set_1.csv";
        return Path.Combine(dir, fileName);
    }

    private static string EscapeCsvValue(string value)
    {
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }

        return value;
    }

    private static bool TryGetRequiredText(
        string[] fields,
        Dictionary<string, int> headerMap,
        string column,
        out string value,
        out string error)
    {
        value = string.Empty;
        error = string.Empty;

        var index = headerMap[column];
        if (index < 0 || index >= fields.Length)
        {
            error = $"missing value for required column '{column}'";
            return false;
        }

        value = fields[index].Trim();
        if (string.IsNullOrWhiteSpace(value))
        {
            error = $"required text column '{column}' is empty";
            return false;
        }

        return true;
    }

    private static bool TryGetRequiredFloat(
        string[] fields,
        Dictionary<string, int> headerMap,
        string column,
        out float value,
        out string error)
    {
        value = 0;
        error = string.Empty;

        var index = headerMap[column];
        if (index < 0 || index >= fields.Length)
        {
            error = $"missing value for required column '{column}'";
            return false;
        }

        var raw = fields[index].Trim();
        if (!float.TryParse(raw, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out value))
        {
            error = $"cannot parse '{column}' as float. value='{raw}'";
            return false;
        }

        return true;
    }

    private static bool TryReadOptionalLabel(
        string[] fields,
        Dictionary<string, int> headerMap,
        out int label,
        out string error)
    {
        label = 0;
        error = string.Empty;

        if (!headerMap.TryGetValue(LabelColumn, out var index))
        {
            return false;
        }

        if (index < 0 || index >= fields.Length)
        {
            error = $"missing value for optional label '{LabelColumn}'";
            return false;
        }

        var raw = fields[index].Trim();
        if (!float.TryParse(raw, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var labelFloat))
        {
            error = $"cannot parse label '{LabelColumn}' as number. value='{raw}'";
            return false;
        }

        label = Convert.ToInt32(MathF.Round(labelFloat), CultureInfo.InvariantCulture);
        return true;
    }

    private static char DetectDelimiter(string header)
    {
        var candidates = new[] { ',', '\t', ';', '|' };
        var bestDelimiter = ',';
        var bestScore = int.MinValue;

        foreach (var delimiter in candidates)
        {
            var cols = ParseCsvLine(header, delimiter);
            var normalized = new HashSet<string>(cols.Select(c => c.Trim()), StringComparer.OrdinalIgnoreCase);
            var score = PredictColumns.Count(normalized.Contains);

            if (score > bestScore)
            {
                bestScore = score;
                bestDelimiter = delimiter;
            }
        }

        return bestDelimiter;
    }

    private static string? ResolveModelPath()
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "SentimentModel.mlnet"),
            Path.GetFullPath("SentimentModel.mlnet"),
            Path.GetFullPath(Path.Combine("MLTest", "SentimentModel.mlnet"))
        };

        return candidates.FirstOrDefault(File.Exists);
    }

    private static string[] ParseCsvLine(string line, char delimiter)
    {
        var values = new List<string>();
        var current = new System.Text.StringBuilder();
        var inQuotes = false;

        for (int i = 0; i < line.Length; i++)
        {
            var c = line[i];

            if (c == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    current.Append('"');
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
                continue;
            }

            if (c == delimiter && !inQuotes)
            {
                values.Add(current.ToString());
                current.Clear();
                continue;
            }

            current.Append(c);
        }

        values.Add(current.ToString());
        return values.ToArray();
    }

    private sealed record PredictRow(
        int SourceRowNumber,
        int SequenceIndex,
        int? StudentId,
        float Age,
        string Gender,
        string Major,
        float Gpa);

    private sealed record GroundTruthRow(
        int SourceRowNumber,
        int SequenceIndex,
        int? StudentId,
        int ActualYearOfStudy);

    private sealed record PredictionResultRow(
        int SequenceIndex,
        int? StudentId,
        float Age,
        string Gender,
        string Major,
        float Gpa,
        int? ActualYearOfStudy,
        int PredictedYearOfStudy,
        bool? IsCorrect,
        float TopScore);

    private sealed record PredictionSummary(
        int TotalSamples,
        int PredictedSamples,
        int RowsWithActual,
        int CorrectPredictions,
        int IncorrectPredictions,
        double? Accuracy,
        Dictionary<int, int> PredictedDistribution,
        Dictionary<int, int> ActualDistribution);

    private sealed class GroundTruthLookup(
        Dictionary<int, Queue<GroundTruthRow>> byStudentId,
        Dictionary<int, GroundTruthRow> byRowOrder)
    {
        public Dictionary<int, Queue<GroundTruthRow>> ByStudentId { get; } = byStudentId;
        public Dictionary<int, GroundTruthRow> ByRowOrder { get; } = byRowOrder;
        public int FallbackToRowOrderCount { get; set; }
    }

    private sealed record GroundTruthMatch(int? ActualYearOfStudy);
}
