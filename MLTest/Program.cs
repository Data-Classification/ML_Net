using static System.Console;

namespace MLTest;

internal class Program
{
    static int Main(string[] args)
    {
        var launchDirectory = Environment.CurrentDirectory;
        var verbose = args.Any(a => string.Equals(a, "--verbose", StringComparison.OrdinalIgnoreCase));

        // Ensure relative model path (SentimentModel.mlnet) resolves from app output directory.
        Directory.SetCurrentDirectory(AppContext.BaseDirectory);

        WriteLine("=== MLTest - GradeClass prediction/evaluation (existing model) ===");

        var pathArg = args.FirstOrDefault(a => !a.StartsWith("--", StringComparison.Ordinal));
        var csvPath = !string.IsNullOrWhiteSpace(pathArg)
            ? ResolveCsvPathArg(pathArg, launchDirectory)
            : ResolveDefaultCsvPath();

        if (string.IsNullOrWhiteSpace(csvPath))
        {
            WriteLine("ERROR: No CSV file path was provided and default file 'data_predict.csv' was not found.");
            WriteLine();
            WriteLine("Usage:");
            WriteLine("  dotnet run --project .\\MLTest\\MLTest.csproj -- <path-to-test-csv> [--verbose]");
            WriteLine();
            WriteLine("Example:");
            WriteLine("  dotnet run --project .\\MLTest\\MLTest.csproj -- .\\MLTest\\data_predict.csv");
            WriteLine("  dotnet run --project .\\MLTest\\MLTest.csproj -- .\\MLTest\\Student_performance_data _.csv");
            return 1;
        }

        if (string.IsNullOrWhiteSpace(pathArg))
        {
            WriteLine($"No args detected. Using default CSV: {csvPath}");
            WriteLine();
        }

        var exitCode = CsvBatchPredictor.Run(csvPath, new CsvBatchPredictor.RunOptions
        {
            VerbosePerRow = verbose,
            MaxPreviewRows = 20
        });

        WriteLine();
        WriteLine(exitCode == 0 ? "Done." : "Finished with errors.");
        return exitCode;
    }

    private static string ResolveCsvPathArg(string pathArg, string launchDirectory)
    {
        if (Path.IsPathRooted(pathArg))
        {
            return pathArg;
        }

        var candidates = new List<string>
        {
            Path.GetFullPath(Path.Combine(launchDirectory, pathArg)),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, pathArg)),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", pathArg)),
            Path.GetFullPath(pathArg)
        };

        foreach (var candidate in candidates.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        // Fall back to the launch directory for a clear error path if nothing exists.
        return candidates[0];
    }

    private static string? ResolveDefaultCsvPath()
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "data_predict.csv"),
            Path.GetFullPath("data_predict.csv"),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "data_predict.csv"))
        };

        return candidates.FirstOrDefault(File.Exists);
    }
}
