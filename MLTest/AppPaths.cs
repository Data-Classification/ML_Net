namespace MLTest;

internal static class AppPaths
{
    public static string ResolveProjectDirectory(string launchDirectory)
    {
        var probes = new[]
        {
            launchDirectory,
            AppContext.BaseDirectory,
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..")
        };

        foreach (var probe in probes)
        {
            var resolved = TryFindProjectDirectoryFrom(probe);
            if (resolved is not null)
            {
                return resolved;
            }
        }

        return Path.GetFullPath(launchDirectory);
    }

    public static string GetTrainingRunsDirectory(string launchDirectory)
    {
        return Path.Combine(ResolveProjectDirectory(launchDirectory), "training_runs");
    }

    public static string GetTrainingRunsJsonlPath(string launchDirectory)
    {
        return Path.Combine(GetTrainingRunsDirectory(launchDirectory), "training_runs.jsonl");
    }

    public static string GetTrainingRunsCsvPath(string launchDirectory)
    {
        return Path.Combine(GetTrainingRunsDirectory(launchDirectory), "training_runs.csv");
    }

    private static string? TryFindProjectDirectoryFrom(string startPath)
    {
        if (string.IsNullOrWhiteSpace(startPath))
        {
            return null;
        }

        var current = new DirectoryInfo(Path.GetFullPath(startPath));
        if (!current.Exists)
        {
            return null;
        }

        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "MLTest.csproj")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        return null;
    }
}