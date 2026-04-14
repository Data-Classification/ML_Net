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
            ShowUsage(launchDirectory);
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
            "compare" => HandleCompare(commandArgs, launchDirectory),
            _ => HandleUnknownCommand(command, launchDirectory)
        };
    }

    private static int HandleTrain(string[] args, string launchDirectory)
    {
        var positional = ExtractPositionalArgs(args, new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "--trainer",
            "--data",
            "--model-out",
            "--runs-dir",
            "--seed",
            "--test-fraction",
            "--run-name",
            "--learning-rate",
            "--number-of-leaves",
            "--number-of-iterations",
            "--max-bins",
            "--l2"
        });
        var positionalModelPath = positional.Count > 0 ? positional[0] : null;

        var trainerArg = GetOptionValue(args, "--trainer") ?? "lbfgs";
        var dataPathArg = GetOptionValue(args, "--data") ?? SentimentModel.RetrainFilePath;
        var modelOutArg = GetOptionValue(args, "--model-out") ?? positionalModelPath;
        var runsDirArg = GetOptionValue(args, "--runs-dir");
        var runName = GetOptionValue(args, "--run-name");

        var seed = GetIntOptionValue(args, "--seed") ?? 42;
        var testFraction = GetFloatOptionValue(args, "--test-fraction") ?? 0.2f;
        var learningRate = GetFloatOptionValue(args, "--learning-rate");
        var numberOfLeaves = GetIntOptionValue(args, "--number-of-leaves");
        var numberOfIterations = GetIntOptionValue(args, "--number-of-iterations");
        var maxBins = GetIntOptionValue(args, "--max-bins");
        var l2 = GetFloatOptionValue(args, "--l2");

        var modelPath = !string.IsNullOrWhiteSpace(modelOutArg)
            ? ResolvePathOrFallback(modelOutArg, launchDirectory, mustExist: false)
            : ResolveProjectDefaultModelPath();

        var dataPath = ResolvePathOrFallback(dataPathArg, launchDirectory, mustExist: true);
        var metadataDirectory = !string.IsNullOrWhiteSpace(runsDirArg)
            ? ResolveDirectoryPath(runsDirArg, launchDirectory, mustExist: false)
            : AppPaths.GetTrainingRunsDirectory(launchDirectory);

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

        if (string.IsNullOrWhiteSpace(metadataDirectory))
        {
            WriteLine("ERROR: Cannot resolve training runs directory.");
            return 1;
        }

        try
        {
            WriteLine("Mode              : Train/Retrain");
            WriteLine($"Trainer            : {trainerArg}");
            WriteLine($"Seed               : {seed}");
            WriteLine($"Test fraction      : {testFraction:0.####}");
            WriteLine($"Training data      : {dataPath}");
            WriteLine($"Output model       : {modelPath}");
            WriteLine($"Runs directory     : {metadataDirectory}");

            var result = SentimentModel.TrainWithOptions(new SentimentModel.TrainingOptions
            {
                DataPath = dataPath,
                ModelOutputPath = modelPath,
                MetadataDirectory = metadataDirectory,
                Trainer = trainerArg,
                Seed = seed,
                TestFraction = testFraction,
                RunName = runName,
                LearningRate = learningRate,
                NumberOfLeaves = numberOfLeaves,
                NumberOfIterations = numberOfIterations,
                MaxBins = maxBins,
                L2 = l2
            });

            WriteLine();
            WriteLine("Train completed successfully.");
            WriteLine($"Run ID             : {result.RunId}");
            WriteLine($"MacroAccuracy      : {result.MacroAccuracy:P2}");
            WriteLine($"MicroAccuracy      : {result.MicroAccuracy:P2}");
            WriteLine($"LogLoss            : {result.LogLoss:0.######}");
            WriteLine($"Duration (sec)     : {result.DurationSeconds:0.###}");
            WriteLine($"Metadata JSONL     : {result.MetadataJsonlPath}");
            WriteLine($"Metadata CSV       : {result.MetadataCsvPath}");

            if (result.Warnings.Count > 0)
            {
                WriteLine();
                WriteLine("Warnings:");
                foreach (var warning in result.Warnings)
                {
                    WriteLine($"- {warning}");
                }
            }

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
        var positional = ExtractPositionalArgs(args, new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "--model",
            "--out",
            "--summary"
        });

        var predictPathArg = positional.Count > 0 ? positional[0] : "data_set_1.csv";
        var modelArg = GetOptionValue(args, "--model");
        var outArg = GetOptionValue(args, "--out");
        var summaryArg = GetOptionValue(args, "--summary");

        var predictPath = ResolvePathOrFallback(predictPathArg, launchDirectory, mustExist: true);
        var modelPath = !string.IsNullOrWhiteSpace(modelArg)
            ? ResolvePathOrFallback(modelArg, launchDirectory, mustExist: true)
            : null;
        var outPath = !string.IsNullOrWhiteSpace(outArg)
            ? ResolvePathOrFallback(outArg, launchDirectory, mustExist: false)
            : null;
        var summaryPath = !string.IsNullOrWhiteSpace(summaryArg)
            ? ResolvePathOrFallback(summaryArg, launchDirectory, mustExist: false)
            : null;

        if (predictPath is null)
        {
            WriteLine("ERROR: Cannot find predict input CSV (expected data_set_1.csv).");
            return 1;
        }

        if (!string.IsNullOrWhiteSpace(modelArg) && modelPath is null)
        {
            WriteLine("ERROR: Cannot find model file from --model.");
            return 1;
        }

        var exitCode = CsvBatchPredictor.Run(predictPath, new CsvBatchPredictor.RunOptions
        {
            VerbosePerRow = verbose,
            MaxPreviewRows = 20,
            ModelPath = modelPath,
            OutputCsvPath = outPath,
            SummaryJsonPath = summaryPath,
            CommandText = $"predict {string.Join(' ', args)}"
        });

        WriteLine();
        WriteLine(exitCode == 0 ? "Predict completed." : "Predict finished with errors.");
        return exitCode;
    }

    private static int HandleEvaluate(string[] args, string launchDirectory)
    {
        var verbose = args.Any(a => string.Equals(a, "--verbose", StringComparison.OrdinalIgnoreCase));
        var positional = ExtractPositionalArgs(args, new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "--model",
            "--out",
            "--summary"
        });

        var modelArg = GetOptionValue(args, "--model");
        var outArg = GetOptionValue(args, "--out");
        var summaryArg = GetOptionValue(args, "--summary");

        var predictArg = positional.Count > 0 ? positional[0] : "data_set_1.csv";
        var groundTruthArg = positional.Count > 1 ? positional[1] : "data_set_1_full.csv";

        var predictPath = ResolvePathOrFallback(predictArg, launchDirectory, mustExist: true);
        var groundTruthPath = ResolvePathOrFallback(groundTruthArg, launchDirectory, mustExist: true);
        var modelPath = !string.IsNullOrWhiteSpace(modelArg)
            ? ResolvePathOrFallback(modelArg, launchDirectory, mustExist: true)
            : null;
        var outPath = !string.IsNullOrWhiteSpace(outArg)
            ? ResolvePathOrFallback(outArg, launchDirectory, mustExist: false)
            : null;
        var summaryPath = !string.IsNullOrWhiteSpace(summaryArg)
            ? ResolvePathOrFallback(summaryArg, launchDirectory, mustExist: false)
            : null;

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

        if (!string.IsNullOrWhiteSpace(modelArg) && modelPath is null)
        {
            WriteLine("ERROR: Cannot find model file from --model.");
            return 1;
        }

        var exitCode = CsvBatchPredictor.Run(predictPath, new CsvBatchPredictor.RunOptions
        {
            VerbosePerRow = verbose,
            MaxPreviewRows = 20,
            GroundTruthCsvPath = groundTruthPath,
            ModelPath = modelPath,
            OutputCsvPath = outPath,
            SummaryJsonPath = summaryPath,
            CommandText = $"evaluate {string.Join(' ', args)}"
        });

        WriteLine();
        WriteLine(exitCode == 0 ? "Evaluate completed." : "Evaluate finished with errors.");
        return exitCode;
    }

    private static int HandleUnknownCommand(string command, string launchDirectory)
    {
        WriteLine($"ERROR: Unknown command '{command}'.");
        ShowUsage(launchDirectory);
        return 1;
    }

    private static bool IsHelpCommand(string command) =>
        string.Equals(command, "help", StringComparison.OrdinalIgnoreCase)
        || string.Equals(command, "--help", StringComparison.OrdinalIgnoreCase)
        || string.Equals(command, "-h", StringComparison.OrdinalIgnoreCase);

    private static void ShowUsage(string launchDirectory)
    {
        var defaultRunsDir = AppPaths.GetTrainingRunsDirectory(launchDirectory);
        var defaultTrainingLog = AppPaths.GetTrainingRunsJsonlPath(launchDirectory);
        var defaultEvalDir = AppPaths.ResolveProjectDirectory(launchDirectory);

        WriteLine("=== MLTest - YearOfStudy workflow ===");
        WriteLine("Train model -> Predict data_set_1.csv -> Evaluate with data_set_1_full.csv");
        WriteLine();
        WriteLine("Usage:");
        WriteLine("  dotnet run --project .\\MLTest\\MLTest.csproj -- train [model-path] [--data <train-csv>] [--model-out <model-path>] [--runs-dir <training-runs-dir>] [--trainer <lbfgs|sdca|lightgbm>] [--seed <int>] [--test-fraction <0..1>] [--run-name <name>] [--learning-rate <float>] [--number-of-leaves <int>] [--number-of-iterations <int>] [--max-bins <int>] [--l2 <float>]");
        WriteLine("  dotnet run --project .\\MLTest\\MLTest.csproj -- predict [predict-csv] [--model <model-path>] [--out <predictions-csv>] [--summary <summary-json>] [--verbose]");
        WriteLine("  dotnet run --project .\\MLTest\\MLTest.csproj -- evaluate [predict-csv] [ground-truth-csv] [--model <model-path>] [--out <evaluation-csv>] [--summary <summary-json>] [--verbose]");
        WriteLine("  dotnet run --project .\\MLTest\\MLTest.csproj -- compare [--runs-dir <training-runs-dir>] [--training-log <training_runs.jsonl>] [--eval-dir <dir-containing-summary_evaluation_json>] [--out <comparison.csv>] [--out-json <comparison.json>]");
        WriteLine();
        WriteLine("Default compare behavior:");
        WriteLine($"  Runs directory    : {defaultRunsDir}");
        WriteLine($"  Training log path : {defaultTrainingLog}");
        WriteLine($"  Evaluation dir    : {defaultEvalDir}");
        WriteLine("  Override runs dir : --runs-dir <path>");
        WriteLine("  Override jsonl    : --training-log <path>");
        WriteLine();
        WriteLine("Examples:");
        WriteLine("  dotnet run --project .\\MLTest\\MLTest.csproj -- train");
        WriteLine("  dotnet run --project .\\MLTest\\MLTest.csproj -- train --runs-dir .\\MLTest\\training_runs");
        WriteLine("  dotnet run --project .\\MLTest\\MLTest.csproj -- train --trainer sdca --seed 2026 --test-fraction 0.2 --run-name sdca_baseline");
        WriteLine("  dotnet run --project .\\MLTest\\MLTest.csproj -- train --trainer lightgbm --learning-rate 0.1 --number-of-leaves 64 --number-of-iterations 300 --max-bins 255 --l2 0.1");
        WriteLine("  dotnet run --project .\\MLTest\\MLTest.csproj -- predict .\\MLTest\\data_set_1.csv --model .\\MLTest\\SentimentModel.mlnet");
        WriteLine("  dotnet run --project .\\MLTest\\MLTest.csproj -- evaluate .\\MLTest\\data_set_1.csv .\\MLTest\\data_set_1_full.csv --model .\\MLTest\\SentimentModel.mlnet");
        WriteLine("  dotnet run --project .\\MLTest\\MLTest.csproj -- compare --eval-dir .\\MLTest");
        WriteLine("  dotnet run --project .\\MLTest\\MLTest.csproj -- compare --runs-dir .\\MLTest\\training_runs --eval-dir .\\MLTest");
    }

    private static int HandleCompare(string[] args, string launchDirectory)
    {
        var runsDirArg = GetOptionValue(args, "--runs-dir");
        var trainingLogArg = GetOptionValue(args, "--training-log");
        var evalDirArg = GetOptionValue(args, "--eval-dir");
        var outCsvArg = GetOptionValue(args, "--out");
        var outJsonArg = GetOptionValue(args, "--out-json");

        var runsDirectory = !string.IsNullOrWhiteSpace(runsDirArg)
            ? ResolveDirectoryPath(runsDirArg, launchDirectory, mustExist: false)
            : AppPaths.GetTrainingRunsDirectory(launchDirectory);

        var defaultTrainingLog = Path.Combine(runsDirectory ?? AppPaths.GetTrainingRunsDirectory(launchDirectory), "training_runs.jsonl");
        var trainingLogPath = !string.IsNullOrWhiteSpace(trainingLogArg)
            ? ResolvePathOrFallback(trainingLogArg, launchDirectory, mustExist: true)
            : defaultTrainingLog;

        var evalDirPath = !string.IsNullOrWhiteSpace(evalDirArg)
            ? ResolveDirectoryPath(evalDirArg, launchDirectory, mustExist: true)
            : AppPaths.ResolveProjectDirectory(launchDirectory);

        var outCsvPath = !string.IsNullOrWhiteSpace(outCsvArg)
            ? ResolvePathOrFallback(outCsvArg, launchDirectory, mustExist: false)
            : null;

        var outJsonPath = !string.IsNullOrWhiteSpace(outJsonArg)
            ? ResolvePathOrFallback(outJsonArg, launchDirectory, mustExist: false)
            : null;

        if (string.IsNullOrWhiteSpace(trainingLogPath))
        {
            WriteLine("ERROR: Cannot resolve training log path.");
            return 1;
        }

        if (!string.IsNullOrWhiteSpace(runsDirArg) && string.IsNullOrWhiteSpace(runsDirectory))
        {
            WriteLine("ERROR: Cannot resolve --runs-dir.");
            return 1;
        }

        if (string.IsNullOrWhiteSpace(evalDirPath))
        {
            WriteLine("ERROR: Cannot resolve evaluation summary directory.");
            return 1;
        }

        return ExperimentComparer.Run(new ExperimentComparer.CompareOptions
        {
            TrainingRunsJsonlPath = trainingLogPath,
            EvaluationSummaryDirectory = evalDirPath,
            OutputCsvPath = outCsvPath,
            OutputJsonPath = outJsonPath
        });
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

    private static int? GetIntOptionValue(string[] args, string optionName)
    {
        var value = GetOptionValue(args, optionName);
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return int.TryParse(value, out var parsed) ? parsed : null;
    }

    private static float? GetFloatOptionValue(string[] args, string optionName)
    {
        var value = GetOptionValue(args, optionName);
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return float.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
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

    private static string? ResolveDirectoryPath(string pathArg, string launchDirectory, bool mustExist)
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
            candidates.Add(Path.GetFullPath(pathArg));
        }

        foreach (var candidate in candidates.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!mustExist || Directory.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private static List<string> ExtractPositionalArgs(string[] args, HashSet<string> optionsWithValue)
    {
        var positional = new List<string>();

        for (var i = 0; i < args.Length; i++)
        {
            var token = args[i];

            if (token.StartsWith("--", StringComparison.Ordinal))
            {
                if (optionsWithValue.Contains(token) && i + 1 < args.Length)
                {
                    i++;
                }

                continue;
            }

            positional.Add(token);
        }

        return positional;
    }
}
