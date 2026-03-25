using static System.Console;

namespace MLTest;

internal class Program
{
    static int Main(string[] args)
    {
        var launchDirectory = Environment.CurrentDirectory;

        // Ensure relative model path (SentimentModel.mlnet) resolves from app output directory.
        Directory.SetCurrentDirectory(AppContext.BaseDirectory);

        if (args.Length == 0 || IsHelpCommand(args[0]))
        {
            ShowUsage();
            return 0;
        }

        var command = args[0];
        var commandArgs = args.Skip(1).ToArray();

        WriteLine("=== MLTest - YearOfStudy workflow ===");

        return command.ToLowerInvariant() switch
        {
            "train" => HandleTrain(commandArgs, launchDirectory),
            "predict" => HandlePredict(commandArgs, launchDirectory),
            "evaluate" => HandleEvaluate(commandArgs, launchDirectory),
            _ => HandleUnknownCommand(command)
        };
    }

    private static int HandleTrain(string[] args, string launchDirectory)
    {
        var modelPathArg = args.FirstOrDefault(a => !a.StartsWith("--", StringComparison.Ordinal));
        var dataPathOption = GetOptionValue(args, "--data");
        var dataPathArg = !string.IsNullOrWhiteSpace(dataPathOption)
            ? dataPathOption
            : SentimentModel.RetrainFilePath;

        var modelPath = !string.IsNullOrWhiteSpace(modelPathArg)
            ? ResolvePathOrFallback(modelPathArg, launchDirectory, mustExist: false)
            : ResolveProjectDefaultModelPath();
        var dataPath = ResolvePathOrFallback(dataPathArg, launchDirectory, mustExist: true);

        if (string.IsNullOrWhiteSpace(modelPath))
        {
            WriteLine("ERROR: Cannot resolve output model path.");
            return 1;
        }

        if (dataPath is null)
        {
            WriteLine("ERROR: Cannot find training data file.");
            return 1;
        }

        try
        {
            WriteLine("Mode              : Train/Retrain");
            WriteLine($"Training data      : {dataPath}");
            WriteLine($"Output model       : {modelPath}");
            SentimentModel.Train(modelPath, dataPath);
            WriteLine();
            WriteLine("Train completed successfully.");
            return 0;
        }
        catch (Exception ex)
        {
            WriteLine($"ERROR: Training failed - {ex.Message}");
            return 2;
        }
    }

    private static int HandlePredict(string[] args, string launchDirectory)
    {
        var verbose = args.Any(a => string.Equals(a, "--verbose", StringComparison.OrdinalIgnoreCase));
        var predictPathArg = args.FirstOrDefault(a => !a.StartsWith("--", StringComparison.Ordinal)) ?? "data_set_1.csv";
        var predictPath = ResolvePathOrFallback(predictPathArg, launchDirectory, mustExist: true);

        if (predictPath is null)
        {
            WriteLine("ERROR: Cannot find predict input CSV (expected data_set_1.csv).");
            return 1;
        }

        var exitCode = CsvBatchPredictor.Run(predictPath, new CsvBatchPredictor.RunOptions
        {
            VerbosePerRow = verbose,
            MaxPreviewRows = 20
        });

        WriteLine();
        WriteLine(exitCode == 0 ? "Predict completed." : "Predict finished with errors.");
        return exitCode;
    }

    private static int HandleEvaluate(string[] args, string launchDirectory)
    {
        var verbose = args.Any(a => string.Equals(a, "--verbose", StringComparison.OrdinalIgnoreCase));
        var positional = args.Where(a => !a.StartsWith("--", StringComparison.Ordinal)).ToList();

        var predictArg = positional.Count > 0 ? positional[0] : "data_set_1.csv";
        var groundTruthArg = positional.Count > 1 ? positional[1] : "data_set_1_full.csv";

        var predictPath = ResolvePathOrFallback(predictArg, launchDirectory, mustExist: true);
        var groundTruthPath = ResolvePathOrFallback(groundTruthArg, launchDirectory, mustExist: true);

        if (predictPath is null)
        {
            WriteLine("ERROR: Cannot find predict input CSV (expected data_set_1.csv).");
            return 1;
        }

        if (groundTruthPath is null)
        {
            WriteLine("ERROR: Cannot find ground-truth CSV (expected data_set_1_full.csv).");
            return 1;
        }

        var exitCode = CsvBatchPredictor.Run(predictPath, new CsvBatchPredictor.RunOptions
        {
            VerbosePerRow = verbose,
            MaxPreviewRows = 20,
            GroundTruthCsvPath = groundTruthPath
        });

        WriteLine();
        WriteLine(exitCode == 0 ? "Evaluate completed." : "Evaluate finished with errors.");
        return exitCode;
    }

    private static int HandleUnknownCommand(string command)
    {
        WriteLine($"ERROR: Unknown command '{command}'.");
        ShowUsage();
        return 1;
    }

    private static bool IsHelpCommand(string command) =>
        string.Equals(command, "help", StringComparison.OrdinalIgnoreCase)
        || string.Equals(command, "--help", StringComparison.OrdinalIgnoreCase)
        || string.Equals(command, "-h", StringComparison.OrdinalIgnoreCase);

    private static void ShowUsage()
    {
        WriteLine("=== MLTest - YearOfStudy workflow ===");
        WriteLine("Train model -> Predict data_set_1.csv -> Evaluate with data_set_1_full.csv");
        WriteLine();
        WriteLine("Usage:");
        WriteLine("  dotnet run --project .\\MLTest\\MLTest.csproj -- train [model-path] [--data <train-csv>]");
        WriteLine("  dotnet run --project .\\MLTest\\MLTest.csproj -- predict [predict-csv] [--verbose]");
        WriteLine("  dotnet run --project .\\MLTest\\MLTest.csproj -- evaluate [predict-csv] [ground-truth-csv] [--verbose]");
        WriteLine();
        WriteLine("Examples:");
        WriteLine("  dotnet run --project .\\MLTest\\MLTest.csproj -- train");
        WriteLine("  dotnet run --project .\\MLTest\\MLTest.csproj -- predict .\\MLTest\\data_set_1.csv");
        WriteLine("  dotnet run --project .\\MLTest\\MLTest.csproj -- evaluate .\\MLTest\\data_set_1.csv .\\MLTest\\data_set_1_full.csv");
    }

    private static string? GetOptionValue(string[] args, string optionName)
    {
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], optionName, StringComparison.OrdinalIgnoreCase))
            {
                var next = args[i + 1];
                if (!string.IsNullOrWhiteSpace(next) && !next.StartsWith("--", StringComparison.Ordinal))
                {
                    return next;
                }
            }
        }

        return null;
    }

    private static string? ResolvePathOrFallback(string pathArg, string launchDirectory, bool mustExist)
    {
        if (string.IsNullOrWhiteSpace(pathArg))
        {
            return null;
        }

        var candidates = new List<string>();

        if (Path.IsPathRooted(pathArg))
        {
            candidates.Add(pathArg);
        }
        else
        {
            candidates.Add(Path.GetFullPath(Path.Combine(launchDirectory, pathArg)));
            candidates.Add(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, pathArg)));
            candidates.Add(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", pathArg)));
            candidates.Add(Path.GetFullPath(pathArg));
        }

        foreach (var candidate in candidates.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!mustExist || File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private static string ResolveProjectDefaultModelPath()
    {
        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "SentimentModel.mlnet"));
    }
}
