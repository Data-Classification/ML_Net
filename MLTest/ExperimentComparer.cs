using System.Globalization;
using System.Text;
using System.Text.Json;
using static System.Console;

namespace MLTest;

internal static class ExperimentComparer
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    internal sealed class CompareOptions
    {
        public required string TrainingRunsJsonlPath { get; init; }
        public required string EvaluationSummaryDirectory { get; init; }
        public string? OutputCsvPath { get; init; }
        public string? OutputJsonPath { get; init; }
    }

    public static int Run(CompareOptions options)
    {
        if (!File.Exists(options.TrainingRunsJsonlPath))
        {
            WriteLine($"ERROR: training runs log not found: {options.TrainingRunsJsonlPath}");
            WriteLine("Hint: run 'train' first to generate metadata, or pass '--runs-dir <path>' / '--training-log <path>'.");
            return 1;
        }

        if (!Directory.Exists(options.EvaluationSummaryDirectory))
        {
            WriteLine($"ERROR: evaluation summary directory not found: {options.EvaluationSummaryDirectory}");
            return 1;
        }

        var trainingRuns = ReadTrainingRuns(options.TrainingRunsJsonlPath);
        if (trainingRuns.Count == 0)
        {
            WriteLine("ERROR: No training runs found in metadata log.");
            return 1;
        }

        var evaluationSummaries = ReadEvaluationSummaries(options.EvaluationSummaryDirectory);

        var reportRows = BuildReportRows(trainingRuns, evaluationSummaries)
            .OrderByDescending(r => r.Accuracy ?? double.MinValue)
            .ThenByDescending(r => r.TrainedAtUtc)
            .ToList();

        var csvPath = ResolveCsvPath(options);
        var jsonPath = ResolveJsonPath(options);

        WriteCsv(csvPath, reportRows);
        WriteJson(jsonPath, reportRows, options.TrainingRunsJsonlPath, options.EvaluationSummaryDirectory);
        var retention = ArtifactRetentionService.PruneComparisonArtifacts(csvPath, jsonPath);
        if (retention.Skipped)
        {
            WriteLine("[retention] comparisons: skipped because CSV and JSON are in different directories.");
        }
        else if (retention.DeletedFileCount > 0)
        {
            WriteLine($"[retention] comparison output: {csvPath}");
            WriteLine($"[retention] kept latest {retention.RunsToKeep} run(s), pruned {retention.DeletedFileCount} old file(s).");
        }

        WriteLine("=== Experiment compare report ===");
        WriteLine($"Training log        : {options.TrainingRunsJsonlPath}");
        WriteLine($"Evaluation summaries: {options.EvaluationSummaryDirectory}");
        WriteLine($"Output CSV          : {csvPath}");
        WriteLine($"Output JSON         : {jsonPath}");
        WriteLine();

        var topRows = reportRows.Take(10).ToList();
        WriteLine("Top runs by Accuracy:");
        WriteLine("RunName | Trainer | Accuracy | Macro | Micro | LogLoss | Duration(s)");
        foreach (var row in topRows)
        {
            WriteLine($"{Display(row.RunName)} | {row.Trainer} | {DisplayPercent(row.Accuracy)} | {row.MacroAccuracy.ToString("P2", CultureInfo.InvariantCulture)} | {row.MicroAccuracy.ToString("P2", CultureInfo.InvariantCulture)} | {row.LogLoss.ToString("0.####", CultureInfo.InvariantCulture)} | {row.DurationSeconds.ToString("0.###", CultureInfo.InvariantCulture)}");
        }

        return 0;
    }

    private static List<TrainingRunEntry> ReadTrainingRuns(string jsonlPath)
    {
        var list = new List<TrainingRunEntry>();

        foreach (var line in File.ReadLines(jsonlPath))
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            try
            {
                var entry = JsonSerializer.Deserialize<TrainingRunEntry>(line, JsonOptions);
                if (entry is not null)
                {
                    list.Add(entry);
                }
            }
            catch
            {
                // Ignore malformed lines.
            }
        }

        return list;
    }

    private static Dictionary<string, EvaluationSummaryEntry> ReadEvaluationSummaries(string directory)
    {
        var map = new Dictionary<string, EvaluationSummaryEntry>(StringComparer.OrdinalIgnoreCase);

        var files = Directory
            .EnumerateFiles(directory, "summary_evaluation_*.json", SearchOption.TopDirectoryOnly)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var file in files)
        {
            try
            {
                var json = File.ReadAllText(file);
                var entry = JsonSerializer.Deserialize<EvaluationSummaryEntry>(json, JsonOptions);
                if (entry?.Model is null)
                {
                    continue;
                }

                var key = entry.Model.RunId;
                if (string.IsNullOrWhiteSpace(key) && !string.IsNullOrWhiteSpace(entry.Model.Path))
                {
                    key = Path.GetFullPath(entry.Model.Path);
                }

                if (string.IsNullOrWhiteSpace(key))
                {
                    continue;
                }

                if (!map.TryGetValue(key, out var existing))
                {
                    map[key] = entry;
                    continue;
                }

                var existingTime = ParseDate(existing.GeneratedAtUtc);
                var newTime = ParseDate(entry.GeneratedAtUtc);
                if (newTime >= existingTime)
                {
                    map[key] = entry;
                }
            }
            catch
            {
                // Ignore malformed files.
            }
        }

        return map;
    }

    private static IEnumerable<ComparisonRow> BuildReportRows(
        List<TrainingRunEntry> trainingRuns,
        Dictionary<string, EvaluationSummaryEntry> evaluationMap)
    {
        foreach (var run in trainingRuns)
        {
            evaluationMap.TryGetValue(run.RunId ?? string.Empty, out var evalByRunId);

            EvaluationSummaryEntry? eval = evalByRunId;
            if (eval is null && !string.IsNullOrWhiteSpace(run.ModelPath))
            {
                var modelKey = Path.GetFullPath(run.ModelPath);
                evaluationMap.TryGetValue(modelKey, out eval);
            }

            yield return new ComparisonRow
            {
                RunId = run.RunId ?? string.Empty,
                RunName = run.RunName,
                Trainer = run.Trainer ?? "(unknown)",
                Seed = run.Seed,
                TestFraction = run.TestFraction,
                LearningRate = run.LearningRate,
                NumberOfLeaves = run.NumberOfLeaves,
                NumberOfIterations = run.NumberOfIterations,
                MaxBins = run.MaxBins,
                L2 = run.L2,
                ModelPath = run.ModelPath ?? string.Empty,
                TrainedAtUtc = run.TrainedAtUtc,
                MacroAccuracy = run.MacroAccuracy,
                MicroAccuracy = run.MicroAccuracy,
                LogLoss = run.LogLoss,
                LogLossReduction = run.LogLossReduction,
                DurationSeconds = run.DurationSeconds,
                Accuracy = eval?.Metrics?.Accuracy,
                EvaluatedSamples = eval?.Metrics?.EvaluatedSamples,
                CorrectPredictions = eval?.Metrics?.CorrectPredictions,
                IncorrectPredictions = eval?.Metrics?.IncorrectPredictions
            };
        }
    }

    private static string ResolveCsvPath(CompareOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.OutputCsvPath))
        {
            return Path.GetFullPath(options.OutputCsvPath);
        }

        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
        return Path.Combine(options.EvaluationSummaryDirectory, $"comparison_report_{timestamp}.csv");
    }

    private static string ResolveJsonPath(CompareOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.OutputJsonPath))
        {
            return Path.GetFullPath(options.OutputJsonPath);
        }

        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
        return Path.Combine(options.EvaluationSummaryDirectory, $"comparison_report_{timestamp}.json");
    }

    private static void WriteCsv(string path, List<ComparisonRow> rows)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        using var writer = new StreamWriter(path, append: false, Encoding.UTF8);
        writer.WriteLine("RunId,RunName,Trainer,Seed,TestFraction,LearningRate,NumberOfLeaves,NumberOfIterations,MaxBins,L2,ModelPath,TrainedAtUtc,MacroAccuracy,MicroAccuracy,LogLoss,LogLossReduction,DurationSeconds,Accuracy,EvaluatedSamples,CorrectPredictions,IncorrectPredictions");

        foreach (var row in rows)
        {
            var values = new[]
            {
                row.RunId,
                row.RunName ?? string.Empty,
                row.Trainer,
                row.Seed.ToString(CultureInfo.InvariantCulture),
                row.TestFraction.ToString("0.####", CultureInfo.InvariantCulture),
                row.LearningRate?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                row.NumberOfLeaves?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                row.NumberOfIterations?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                row.MaxBins?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                row.L2?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                row.ModelPath,
                row.TrainedAtUtc ?? string.Empty,
                row.MacroAccuracy.ToString("0.######", CultureInfo.InvariantCulture),
                row.MicroAccuracy.ToString("0.######", CultureInfo.InvariantCulture),
                row.LogLoss.ToString("0.######", CultureInfo.InvariantCulture),
                row.LogLossReduction.ToString("0.######", CultureInfo.InvariantCulture),
                row.DurationSeconds.ToString("0.###", CultureInfo.InvariantCulture),
                row.Accuracy?.ToString("0.######", CultureInfo.InvariantCulture) ?? string.Empty,
                row.EvaluatedSamples?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                row.CorrectPredictions?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                row.IncorrectPredictions?.ToString(CultureInfo.InvariantCulture) ?? string.Empty
            };

            writer.WriteLine(string.Join(',', values.Select(EscapeCsv)));
        }
    }

    private static void WriteJson(string path, List<ComparisonRow> rows, string trainingLogPath, string summaryDir)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        var payload = new
        {
            generatedAtUtc = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture),
            trainingLogPath,
            evaluationSummaryDirectory = summaryDir,
            sortedBy = "Accuracy desc",
            rows
        };

        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(path, json, Encoding.UTF8);
    }

    private static string EscapeCsv(string value)
    {
        if (value.Contains(',') || value.Contains('"') || value.Contains('\r') || value.Contains('\n'))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }

        return value;
    }

    private static DateTimeOffset ParseDate(string? date)
    {
        return DateTimeOffset.TryParse(date, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed)
            ? parsed
            : DateTimeOffset.MinValue;
    }

    private static string Display(string? value) => string.IsNullOrWhiteSpace(value) ? "(n/a)" : value;

    private static string DisplayPercent(double? value)
        => value.HasValue ? value.Value.ToString("P2", CultureInfo.InvariantCulture) : "(n/a)";

    private sealed class TrainingRunEntry
    {
        public string? RunId { get; set; }
        public string? RunName { get; set; }
        public string? Trainer { get; set; }
        public int Seed { get; set; }
        public float TestFraction { get; set; }
        public string? DataPath { get; set; }
        public string? ModelPath { get; set; }
        public string? TrainedAtUtc { get; set; }
        public double MacroAccuracy { get; set; }
        public double MicroAccuracy { get; set; }
        public double LogLoss { get; set; }
        public double LogLossReduction { get; set; }
        public double DurationSeconds { get; set; }
        public float? LearningRate { get; set; }
        public int? NumberOfLeaves { get; set; }
        public int? NumberOfIterations { get; set; }
        public int? MaxBins { get; set; }
        public float? L2 { get; set; }
    }

    private sealed class EvaluationSummaryEntry
    {
        public string? GeneratedAtUtc { get; set; }
        public EvaluationModelInfo? Model { get; set; }
        public EvaluationMetrics? Metrics { get; set; }
    }

    private sealed class EvaluationModelInfo
    {
        public string? Path { get; set; }
        public string? RunId { get; set; }
    }

    private sealed class EvaluationMetrics
    {
        public int? EvaluatedSamples { get; set; }
        public int? CorrectPredictions { get; set; }
        public int? IncorrectPredictions { get; set; }
        public double? Accuracy { get; set; }
    }

    private sealed class ComparisonRow
    {
        public string RunId { get; set; } = string.Empty;
        public string? RunName { get; set; }
        public string Trainer { get; set; } = string.Empty;
        public int Seed { get; set; }
        public float TestFraction { get; set; }
        public float? LearningRate { get; set; }
        public int? NumberOfLeaves { get; set; }
        public int? NumberOfIterations { get; set; }
        public int? MaxBins { get; set; }
        public float? L2 { get; set; }
        public string ModelPath { get; set; } = string.Empty;
        public string? TrainedAtUtc { get; set; }
        public double MacroAccuracy { get; set; }
        public double MicroAccuracy { get; set; }
        public double LogLoss { get; set; }
        public double LogLossReduction { get; set; }
        public double DurationSeconds { get; set; }
        public double? Accuracy { get; set; }
        public int? EvaluatedSamples { get; set; }
        public int? CorrectPredictions { get; set; }
        public int? IncorrectPredictions { get; set; }
    }
}
