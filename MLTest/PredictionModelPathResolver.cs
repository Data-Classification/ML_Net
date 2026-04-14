namespace MLTest;

public static class PredictionModelPathResolver
{
    public const string DefaultModelFileName = "SentimentModel.mlnet";
    private const string ProjectFileName = "MLTest.csproj";

    public static string? TryResolveModelPath(string? explicitModelPath = null, IEnumerable<string?>? probeDirectories = null)
    {
        return BuildCandidatePaths(explicitModelPath, probeDirectories)
            .FirstOrDefault(File.Exists);
    }

    public static string ResolveModelPathOrThrow(string? explicitModelPath = null, IEnumerable<string?>? probeDirectories = null)
    {
        var candidates = BuildCandidatePaths(explicitModelPath, probeDirectories).ToArray();
        var resolved = candidates.FirstOrDefault(File.Exists);
        if (resolved is not null)
        {
            return resolved;
        }

        var attempted = candidates.Length == 0
            ? "(no candidate paths)"
            : string.Join("; ", candidates);

        throw new FileNotFoundException($"Model file '{DefaultModelFileName}' was not found. Checked: {attempted}");
    }

    private static IEnumerable<string> BuildCandidatePaths(string? explicitModelPath, IEnumerable<string?>? probeDirectories)
    {
        var candidates = new List<string>();
        var probes = GetProbeDirectories(probeDirectories).ToArray();

        if (!string.IsNullOrWhiteSpace(explicitModelPath))
        {
            if (Path.IsPathRooted(explicitModelPath))
            {
                TryAddFullPathCandidate(candidates, explicitModelPath);
            }
            else
            {
                foreach (var probe in probes)
                {
                    TryAddFullPathCandidate(candidates, Path.Combine(probe, explicitModelPath));
                }
            }
        }
        else
        {
            foreach (var probe in probes)
            {
                var projectDirectory = TryFindProjectDirectoryFrom(probe);
                if (!string.IsNullOrWhiteSpace(projectDirectory))
                {
                    TryAddFullPathCandidate(candidates, Path.Combine(projectDirectory, DefaultModelFileName));
                }
            }

            foreach (var probe in probes)
            {
                TryAddFullPathCandidate(candidates, Path.Combine(probe, DefaultModelFileName));
            }
        }

        return candidates.Distinct(StringComparer.OrdinalIgnoreCase);
    }

    private static IEnumerable<string> GetProbeDirectories(IEnumerable<string?>? additionalProbeDirectories)
    {
        var probes = new List<string?>();

        if (additionalProbeDirectories is not null)
        {
            probes.AddRange(additionalProbeDirectories);
        }

        probes.Add(AppContext.BaseDirectory);
        probes.Add(Environment.CurrentDirectory);

        foreach (var probe in probes)
        {
            if (string.IsNullOrWhiteSpace(probe))
            {
                continue;
            }

            string fullPath;
            try
            {
                fullPath = Path.GetFullPath(probe);
            }
            catch
            {
                continue;
            }

            yield return fullPath;
        }
    }

    private static void TryAddFullPathCandidate(List<string> candidates, string path)
    {
        try
        {
            candidates.Add(Path.GetFullPath(path));
        }
        catch
        {
            // Ignore invalid path candidates and keep probing the remaining locations.
        }
    }

    private static string? TryFindProjectDirectoryFrom(string startPath)
    {
        if (string.IsNullOrWhiteSpace(startPath))
        {
            return null;
        }

        DirectoryInfo? current;
        try
        {
            current = new DirectoryInfo(Path.GetFullPath(startPath));
        }
        catch
        {
            return null;
        }

        if (!current.Exists)
        {
            return null;
        }

        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, ProjectFileName)))
            {
                return current.FullName;
            }

            var siblingProjectDirectory = Path.Combine(current.FullName, "MLTest");
            if (File.Exists(Path.Combine(siblingProjectDirectory, ProjectFileName)))
            {
                return siblingProjectDirectory;
            }

            current = current.Parent;
        }

        return null;
    }
}
