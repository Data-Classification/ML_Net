using static System.Console;

namespace MLTest;

internal class Program
{
    static int Main(string[] args)
    {
        // Ensure relative model path (SentimentModel.mlnet) resolves from app output directory.
        Directory.SetCurrentDirectory(AppContext.BaseDirectory);

        WriteLine("=== MLTest - Predict GradeClass from CSV (using existing model) ===");

        var csvPath = args.Length > 0 ? args[0] : ResolveDefaultCsvPath();
        if (string.IsNullOrWhiteSpace(csvPath))
        {
            WriteLine("ERROR: No CSV file path was provided and default file 'data_predict.csv' was not found.");
            WriteLine();
            WriteLine("Usage:");
            WriteLine("  dotnet run --project .\\MLTest\\MLTest.csproj -- <path-to-test-csv>");
            WriteLine();
            WriteLine("Example:");
            WriteLine("  dotnet run --project .\\MLTest\\MLTest.csproj -- C:\\data\\student_test.csv");
            return 1;
        }

        if (args.Length == 0)
        {
            WriteLine($"No args detected. Using default CSV: {csvPath}");
            WriteLine();
        }

        var exitCode = CsvBatchPredictor.Run(csvPath);

        WriteLine();
        WriteLine(exitCode == 0 ? "Done." : "Finished with errors.");
        return exitCode;
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
