namespace Harnfig;

internal static class ConfigLocator
{
    public static string Find(string workingDirectory, string? explicitPath)
    {
        if (!string.IsNullOrWhiteSpace(explicitPath))
        {
            var fullPath = Path.GetFullPath(explicitPath, workingDirectory);
            return File.Exists(fullPath)
                ? fullPath
                : throw new ConfigurationException($"Configuration file not found: {fullPath}");
        }

        var current = new DirectoryInfo(Path.GetFullPath(workingDirectory));
        while (current is not null)
        {
            var candidate = Path.Combine(current.FullName, "harnfig.yaml");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            if (Directory.Exists(Path.Combine(current.FullName, ".git")) || File.Exists(Path.Combine(current.FullName, ".git")))
            {
                break;
            }

            current = current.Parent;
        }

        throw new ConfigurationException("Could not find harnfig.yaml in the current directory or its repository parents.");
    }
}
