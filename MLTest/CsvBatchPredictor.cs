using System.Globalization;
using static System.Console;

namespace MLTest;

internal static class CsvBatchPredictor
{
    internal sealed class RunOptions
    {
        public bool VerbosePerRow { get; init; }
        public int MaxPreviewRows { get; init; } = 20;
    }

    private static readonly string[] FeatureColumns =
    [
        "Age",
        "Gender",
        "Ethnicity",
        "ParentalEducation",
        "StudyTimeWeekly",
        "Absences",
        "Tutoring",
        "ParentalSupport",
        "Extracurricular",
        "Sports",
        "Music",
        "Volunteering"
    ];

    private const string LabelColumn = "GradeClass";

    public static int Run(string csvPath, RunOptions? options = null)
    {
        options ??= new RunOptions();

        if (string.IsNullOrWhiteSpace(csvPath))
        {
            WriteLine("ERROR: CSV path is empty.");
            return 1;
        }

        if (!File.Exists(csvPath))
        {
            WriteLine($"ERROR: CSV file not found: {csvPath}");
            return 1;
        }

        var modelPath = ResolveModelPath();
        if (modelPath is null)
        {
            WriteLine("ERROR: Model file not found: SentimentModel.mlnet");
            return 1;
        }

        var lines = File.ReadLines(csvPath).GetEnumerator();
        if (!lines.MoveNext())
        {
            WriteLine("ERROR: CSV file is empty.");
            return 1;
        }

        var headerLine = lines.Current ?? string.Empty;
        var delimiter = DetectDelimiter(headerLine);
        var headers = ParseCsvLine(headerLine, delimiter)
            .Select(h => h.Trim())
            .ToArray();

        if (headers.Length == 0)
        {
            WriteLine("ERROR: Cannot parse CSV header.");
            return 1;
        }

        var headerMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < headers.Length; i++)
        {
            if (!headerMap.ContainsKey(headers[i]))
            {
                headerMap[headers[i]] = i;
            }
        }

        var missingRequired = FeatureColumns.Where(c => !headerMap.ContainsKey(c)).ToList();
        if (missingRequired.Count > 0)
        {
            WriteLine("ERROR: Missing required feature columns in CSV header:");
            WriteLine($"  - {string.Join(", ", missingRequired)}");
            WriteLine("Header found:");
            WriteLine($"  {string.Join(", ", headers)}");
            return 1;
        }

        var hasLabel = headerMap.ContainsKey(LabelColumn);
        var mode = hasLabel ? "Evaluate" : "Predict-only";

        WriteLine(hasLabel
            ? "=== Evaluate GradeClass model on labeled CSV ==="
            : "=== Predict GradeClass from CSV (predict-only) ===");

        WriteLine($"Input CSV         : {csvPath}");
        WriteLine($"Model             : {modelPath}");
        WriteLine($"Delimiter         : '{DisplayDelimiter(delimiter)}'");
        WriteLine($"Mode              : {mode}");
        WriteLine($"Has label column? : {(hasLabel ? "Yes" : "No")}");
        WriteLine($"Verbose row logs  : {(options.VerbosePerRow ? "On" : "Off")}");
        WriteLine();

        var totalRows = 0;
        var parsedRows = 0;
        var predictedRows = 0;
        var skippedFormatRows = 0;
        var detailedRowsPrinted = 0;

        var correct = 0;
        var rowsWithGroundTruth = 0;
        var perClass = new Dictionary<int, ClassStats>();

        while (lines.MoveNext())
        {
            totalRows++;
            var raw = lines.Current;
            if (string.IsNullOrWhiteSpace(raw))
            {
                skippedFormatRows++;
                continue;
            }

            var fields = ParseCsvLine(raw, delimiter);

            if (!TryBuildInput(fields, headerMap, out var input, out var parseError))
            {
                skippedFormatRows++;
                WriteLine($"[WARN] Row {totalRows}: {parseError}");
                continue;
            }

            parsedRows++;

            int actualLabel = 0;
            if (hasLabel && !TryReadOptionalLabel(fields, headerMap, out actualLabel, out var labelError))
            {
                skippedFormatRows++;
                WriteLine($"[WARN] Row {totalRows}: {labelError}");
                continue;
            }

            SentimentModel.ModelOutput result;
            try
            {
                result = SentimentModel.Predict(input);
                predictedRows++;
            }
            catch (Exception ex)
            {
                skippedFormatRows++;
                WriteLine($"[WARN] Row {totalRows}: prediction failed - {ex.Message}");
                continue;
            }

            var predictedLabel = Convert.ToInt32(MathF.Round(result.PredictedLabel), CultureInfo.InvariantCulture);
            var topScore = result.Score is { Length: > 0 } ? result.Score.Max() : float.NaN;

            if (hasLabel)
            {
                rowsWithGroundTruth++;
                var isCorrect = predictedLabel == actualLabel;
                if (isCorrect)
                {
                    correct++;
                }

                UpdateClassStats(perClass, actualLabel, predictedLabel, isCorrect);
                TryWriteRowDetail(
                    options,
                    ref detailedRowsPrinted,
                    $"Row {totalRows}: Actual={actualLabel}, Predicted={predictedLabel}, Correct={(isCorrect ? "Y" : "N")}, TopScore={(float.IsNaN(topScore) ? "N/A" : topScore.ToString("0.####", CultureInfo.InvariantCulture))}");
            }
            else
            {
                UpdateClassStats(perClass, actualLabel: null, predictedLabel, isCorrect: false);
                TryWriteRowDetail(
                    options,
                    ref detailedRowsPrinted,
                    $"Row {totalRows}: Predicted={predictedLabel}, TopScore={(float.IsNaN(topScore) ? "N/A" : topScore.ToString("0.####", CultureInfo.InvariantCulture))}");
            }
        }

        if (!options.VerbosePerRow && totalRows > detailedRowsPrinted)
        {
            WriteLine();
            WriteLine($"[INFO] Row-level output limited to first {detailedRowsPrinted} rows. Use --verbose to print all rows.");
        }

        WriteLine();
        WriteLine("================ SUMMARY ================");
        WriteLine($"Total data rows           : {totalRows}");
        WriteLine($"Rows parsed successfully  : {parsedRows}");
        WriteLine($"Rows predicted successfully: {predictedRows}");
        WriteLine($"Rows skipped/errors       : {skippedFormatRows}");

        WriteLine();
        WriteLine("Predicted label distribution:");
        var totalPred = perClass.Values.Sum(v => v.PredictedCount);
        foreach (var kv in perClass.OrderBy(k => k.Key))
        {
            var count = kv.Value.PredictedCount;
            if (count == 0)
            {
                continue;
            }

            var ratio = totalPred > 0 ? (double)count / totalPred : 0;
            WriteLine($"  Label {kv.Key}: Count={count}, Ratio={ratio:P2}");
        }

        if (hasLabel)
        {
            WriteLine($"Rows with ground-truth    : {rowsWithGroundTruth}");
            WriteLine($"Correct predictions       : {correct}");
            WriteLine($"Wrong predictions         : {Math.Max(0, rowsWithGroundTruth - correct)}");
            var accuracy = rowsWithGroundTruth > 0 ? (double)correct / rowsWithGroundTruth : 0;
            WriteLine($"Accuracy                  : {accuracy:P2}");

            WriteLine();
            WriteLine("Actual label distribution:");
            foreach (var kv in perClass.OrderBy(k => k.Key))
            {
                var actualCount = kv.Value.ActualCount;
                if (actualCount == 0)
                {
                    continue;
                }

                var actualRatio = rowsWithGroundTruth > 0 ? (double)actualCount / rowsWithGroundTruth : 0;
                WriteLine($"  Label {kv.Key}: Count={actualCount}, Ratio={actualRatio:P2}");
            }

            WriteLine();
            WriteLine("Per-class stats (ActualClass => ActualCount / PredictedCount / Correct):");
            foreach (var kv in perClass.OrderBy(k => k.Key))
            {
                var s = kv.Value;
                WriteLine($"  Class {kv.Key}: Actual={s.ActualCount}, Predicted={s.PredictedCount}, Correct={s.CorrectCount}");
            }
        }
        else
        {
            WriteLine();
            WriteLine("[INFO] No 'GradeClass' column detected in input header.");
            WriteLine("[INFO] Accuracy / Actual label distribution / Per-class correct count are only available in Evaluate mode.");
        }
        return 0;
    }

    private static void TryWriteRowDetail(RunOptions options, ref int detailedRowsPrinted, string message)
    {
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

    private static bool TryBuildInput(
        string[] fields,
        Dictionary<string, int> headerMap,
        out SentimentModel.ModelInput input,
        out string error)
    {
        input = new SentimentModel.ModelInput();
        error = string.Empty;

        if (!TryGetRequiredFloat(fields, headerMap, "Age", out var age, out error)
            || !TryGetRequiredFloat(fields, headerMap, "Gender", out var gender, out error)
            || !TryGetRequiredFloat(fields, headerMap, "Ethnicity", out var ethnicity, out error)
            || !TryGetRequiredFloat(fields, headerMap, "ParentalEducation", out var parentalEducation, out error)
            || !TryGetRequiredFloat(fields, headerMap, "StudyTimeWeekly", out var studyTimeWeekly, out error)
            || !TryGetRequiredFloat(fields, headerMap, "Absences", out var absences, out error)
            || !TryGetRequiredFloat(fields, headerMap, "Tutoring", out var tutoring, out error)
            || !TryGetRequiredFloat(fields, headerMap, "ParentalSupport", out var parentalSupport, out error)
            || !TryGetRequiredFloat(fields, headerMap, "Extracurricular", out var extracurricular, out error)
            || !TryGetRequiredFloat(fields, headerMap, "Sports", out var sports, out error)
            || !TryGetRequiredFloat(fields, headerMap, "Music", out var music, out error)
            || !TryGetRequiredFloat(fields, headerMap, "Volunteering", out var volunteering, out error))
        {
            return false;
        }

        input.Age = age;
        input.Gender = gender;
        input.Ethnicity = ethnicity;
        input.ParentalEducation = parentalEducation;
        input.StudyTimeWeekly = studyTimeWeekly;
        input.Absences = absences;
        input.Tutoring = tutoring;
        input.ParentalSupport = parentalSupport;
        input.Extracurricular = extracurricular;
        input.Sports = sports;
        input.Music = music;
        input.Volunteering = volunteering;

        // Label is never used as feature; only pass through when available.
        if (TryReadOptionalLabel(fields, headerMap, out var label, out _))
        {
            input.GradeClass = label;
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

    private static void UpdateClassStats(Dictionary<int, ClassStats> perClass, int? actualLabel, int predictedLabel, bool isCorrect)
    {
        if (!perClass.TryGetValue(predictedLabel, out var predStats))
        {
            predStats = new ClassStats();
            perClass[predictedLabel] = predStats;
        }
        predStats.PredictedCount++;

        if (actualLabel.HasValue)
        {
            if (!perClass.TryGetValue(actualLabel.Value, out var actualStats))
            {
                actualStats = new ClassStats();
                perClass[actualLabel.Value] = actualStats;
            }
            actualStats.ActualCount++;

            if (isCorrect)
            {
                actualStats.CorrectCount++;
            }
        }
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
            var score = FeatureColumns.Count(normalized.Contains);

            if (score > bestScore)
            {
                bestScore = score;
                bestDelimiter = delimiter;
            }
        }

        return bestDelimiter;
    }

    private static string DisplayDelimiter(char delimiter) => delimiter switch
    {
        '\t' => "\\t",
        _ => delimiter.ToString()
    };

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

    private sealed class ClassStats
    {
        public int ActualCount { get; set; }
        public int PredictedCount { get; set; }
        public int CorrectCount { get; set; }
    }
}
