using System.Globalization;
using System.Text.RegularExpressions;

namespace MLTest;

internal static class ArtifactRetentionService
{
    public const int DefaultPredictionRunsToKeep = 5;
    public const int DefaultEvaluationRunsToKeep = 5;
    public const int DefaultComparisonRunsToKeep = 5;

    private static readonly Regex PredictionsCsvRegex = new(
        @"^predictions_(?<ts>\d{8}_\d{6})\.csv$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex PredictSummaryRegex = new(
        @"^summary_predict_(?<ts>\d{8}_\d{6})\.json$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex EvaluationCsvRegex = new(
        @"^evaluation_(?<ts>\d{8}_\d{6})\.csv$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex EvaluationSummaryRegex = new(
        @"^summary_evaluation_(?<ts>\d{8}_\d{6})\.json$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex ComparisonCsvRegex = new(
        @"^comparison_report_(?<ts>\d{8}_\d{6})\.csv$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex ComparisonJsonRegex = new(
        @"^comparison_report_(?<ts>\d{8}_\d{6})\.json$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    public static RetentionOutcome PrunePredictionArtifacts(string outputCsvPath, string summaryJsonPath, int runsToKeep = DefaultPredictionRunsToKeep)
    {
        if (!TryGetDirectory(outputCsvPath, out var csvDir))
        {
            return RetentionOutcome.None;
        }

        if (!TryGetDirectory(summaryJsonPath, out var summaryDir))
        {
            summaryDir = csvDir;
        }

        if (string.Equals(csvDir, summaryDir, StringComparison.OrdinalIgnoreCase))
        {
            var result = PruneGroupedRuns(csvDir, runsToKeep, PredictionsCsvRegex, PredictSummaryRegex);
            return new RetentionOutcome(runsToKeep, result.DeletedRunCount, result.DeletedFileCount, false);
        }

        var csvResult = PruneSinglePattern(csvDir, runsToKeep, PredictionsCsvRegex);
        var summaryResult = PruneSinglePattern(summaryDir, runsToKeep, PredictSummaryRegex);

        return new RetentionOutcome(
            runsToKeep,
            csvResult.DeletedRunCount + summaryResult.DeletedRunCount,
            csvResult.DeletedFileCount + summaryResult.DeletedFileCount,
            false);
    }

    public static RetentionOutcome PruneEvaluationArtifacts(string outputCsvPath, string summaryJsonPath, int runsToKeep = DefaultEvaluationRunsToKeep)
    {
        if (!TryGetDirectory(outputCsvPath, out var csvDir) || !TryGetDirectory(summaryJsonPath, out var summaryDir))
        {
            return RetentionOutcome.None;
        }

        if (!string.Equals(csvDir, summaryDir, StringComparison.OrdinalIgnoreCase))
        {
            return new RetentionOutcome(runsToKeep, 0, 0, true);
        }

        var result = PruneGroupedRuns(csvDir, runsToKeep, EvaluationCsvRegex, EvaluationSummaryRegex);
        return new RetentionOutcome(runsToKeep, result.DeletedRunCount, result.DeletedFileCount, false);
    }

    public static RetentionOutcome PruneComparisonArtifacts(string outputCsvPath, string outputJsonPath, int runsToKeep = DefaultComparisonRunsToKeep)
    {
        if (!TryGetDirectory(outputCsvPath, out var csvDir) || !TryGetDirectory(outputJsonPath, out var jsonDir))
        {
            return RetentionOutcome.None;
        }

        if (!string.Equals(csvDir, jsonDir, StringComparison.OrdinalIgnoreCase))
        {
            return new RetentionOutcome(runsToKeep, 0, 0, true);
        }

        var result = PruneGroupedRuns(csvDir, runsToKeep, ComparisonCsvRegex, ComparisonJsonRegex);
        return new RetentionOutcome(runsToKeep, result.DeletedRunCount, result.DeletedFileCount, false);
    }

    private static bool TryGetDirectory(string filePath, out string directory)
    {
        directory = string.Empty;

        if (string.IsNullOrWhiteSpace(filePath))
        {
            return false;
        }

        var fullPath = Path.GetFullPath(filePath);
        var dir = Path.GetDirectoryName(fullPath);
        if (string.IsNullOrWhiteSpace(dir))
        {
            return false;
        }

        directory = dir;
        return true;
    }

    private static RetentionResult PruneGroupedRuns(string directory, int runsToKeep, params Regex[] patterns)
    {
        try
        {
            if (!Directory.Exists(directory))
            {
                return RetentionResult.None;
            }

            var groups = new Dictionary<string, RetentionRunGroup>(StringComparer.OrdinalIgnoreCase);

            foreach (var path in Directory.EnumerateFiles(directory, "*", SearchOption.TopDirectoryOnly))
            {
                var fileName = Path.GetFileName(path);
                var matched = false;

                foreach (var pattern in patterns)
                {
                    var match = pattern.Match(fileName);
                    if (!match.Success)
                    {
                        continue;
                    }

                    matched = true;
                    var key = match.Groups["ts"].Value;
                    var sortTime = ParseRunTime(key, path);

                    if (!groups.TryGetValue(key, out var group))
                    {
                        group = new RetentionRunGroup(key, sortTime);
                        groups[key] = group;
                    }

                    group.SortTime = sortTime > group.SortTime ? sortTime : group.SortTime;
                    group.Files.Add(path);
                    break;
                }

                _ = matched;
            }

            if (groups.Count <= runsToKeep)
            {
                return new RetentionResult(groups.Count, 0, 0);
            }

            var ordered = groups.Values
                .OrderByDescending(g => g.SortTime)
                .ThenByDescending(g => g.RunKey, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var toDelete = ordered.Skip(runsToKeep).ToList();
            var deletedFiles = 0;
            foreach (var group in toDelete)
            {
                foreach (var file in group.Files.Distinct(StringComparer.OrdinalIgnoreCase))
                {
                    deletedFiles += TryDeleteFile(file) ? 1 : 0;
                }
            }

            return new RetentionResult(runsToKeep, toDelete.Count, deletedFiles);
        }
        catch
        {
            return RetentionResult.None;
        }
    }

    private static RetentionResult PruneSinglePattern(string directory, int runsToKeep, Regex pattern)
    {
        try
        {
            if (!Directory.Exists(directory))
            {
                return RetentionResult.None;
            }

            var candidates = Directory
                .EnumerateFiles(directory, "*", SearchOption.TopDirectoryOnly)
                .Select(path => new
                {
                    Path = path,
                    FileName = Path.GetFileName(path),
                    Match = pattern.Match(Path.GetFileName(path))
                })
                .Where(x => x.Match.Success)
                .Select(x => new
                {
                    x.Path,
                    RunKey = x.Match.Groups["ts"].Value,
                    SortTime = ParseRunTime(x.Match.Groups["ts"].Value, x.Path)
                })
                .OrderByDescending(x => x.SortTime)
                .ThenByDescending(x => x.RunKey, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (candidates.Count <= runsToKeep)
            {
                return new RetentionResult(candidates.Count, 0, 0);
            }

            var deletedFiles = 0;
            foreach (var file in candidates.Skip(runsToKeep))
            {
                deletedFiles += TryDeleteFile(file.Path) ? 1 : 0;
            }

            return new RetentionResult(runsToKeep, candidates.Count - runsToKeep, deletedFiles);
        }
        catch
        {
            return RetentionResult.None;
        }
    }

    private static DateTimeOffset ParseRunTime(string runKey, string fallbackPath)
    {
        if (DateTimeOffset.TryParseExact(
                runKey,
                "yyyyMMdd_HHmmss",
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeLocal,
                out var parsed))
        {
            return parsed;
        }

        try
        {
            return File.GetLastWriteTimeUtc(fallbackPath);
        }
        catch
        {
            return DateTimeOffset.MinValue;
        }
    }

    private static bool TryDeleteFile(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                return false;
            }

            File.Delete(path);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public readonly record struct RetentionOutcome(int RunsToKeep, int DeletedRunCount, int DeletedFileCount, bool Skipped)
    {
        public static RetentionOutcome None => new(0, 0, 0, false);
    }

    private sealed class RetentionRunGroup(string runKey, DateTimeOffset sortTime)
    {
        public string RunKey { get; } = runKey;
        public DateTimeOffset SortTime { get; set; } = sortTime;
        public List<string> Files { get; } = [];
    }

    private readonly record struct RetentionResult(int KeptRunCount, int DeletedRunCount, int DeletedFileCount)
    {
        public static RetentionResult None => new(0, 0, 0);
    }
}