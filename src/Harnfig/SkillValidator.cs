using System.Text.RegularExpressions;
using YamlDotNet.RepresentationModel;

namespace Harnfig;

internal sealed partial class SkillValidator
{
    [GeneratedRegex(@"^[a-z0-9]+(?:-[a-z0-9]+)*$")]
    private static partial Regex SkillNameRegex();

    public (string Name, string Directory) Validate(string checkoutDirectory, string repositoryName, string skillPath)
    {
        var relativePath = skillPath == "."
            ? string.Empty
            : skillPath.Replace('/', Path.DirectorySeparatorChar);
        var checkoutRoot = Path.GetFullPath(checkoutDirectory);
        var skillDirectory = Path.GetFullPath(Path.Combine(checkoutRoot, relativePath));

        if (!IsWithin(skillDirectory, checkoutRoot) || !Directory.Exists(skillDirectory))
        {
            throw new HarnfigException($"Skill path '{skillPath}' does not exist inside '{repositoryName}'.");
        }

        RejectUnsupportedEntries(skillDirectory);

        var skillFile = Path.Combine(skillDirectory, "SKILL.md");
        if (!File.Exists(skillFile))
        {
            throw new HarnfigException($"Skill path '{skillPath}' in '{repositoryName}' does not contain SKILL.md.");
        }

        var frontmatter = ParseFrontmatter(File.ReadAllText(skillFile), repositoryName, skillPath);
        var name = YamlHelpers.RequiredScalar(frontmatter, "name", "SKILL.md frontmatter");
        var description = YamlHelpers.RequiredScalar(frontmatter, "description", "SKILL.md frontmatter");

        if (name.Length > 64 || !SkillNameRegex().IsMatch(name))
        {
            throw new HarnfigException($"Skill name '{name}' must contain 1-64 lowercase letters, numbers, or single hyphens.");
        }

        if (description.Length is < 1 or > 1024)
        {
            throw new HarnfigException($"Skill '{name}' must have a description between 1 and 1024 characters.");
        }

        var expectedDirectoryName = skillPath == "."
            ? repositoryName.Split('/').Last()
            : skillPath.Split('/').Last();
        if (!string.Equals(name, expectedDirectoryName, StringComparison.Ordinal))
        {
            throw new HarnfigException($"Skill name '{name}' must match its source directory '{expectedDirectoryName}'.");
        }

        return (name, skillDirectory);
    }

    private static YamlMappingNode ParseFrontmatter(string contents, string repositoryName, string skillPath)
    {
        using var reader = new StringReader(contents);
        if (!string.Equals(reader.ReadLine()?.Trim(), "---", StringComparison.Ordinal))
        {
            throw new HarnfigException($"SKILL.md at '{repositoryName}/{skillPath}' must start with YAML frontmatter.");
        }

        var lines = new List<string>();
        string? line;
        while ((line = reader.ReadLine()) is not null && !string.Equals(line.Trim(), "---", StringComparison.Ordinal))
        {
            lines.Add(line);
        }

        if (line is null)
        {
            throw new HarnfigException($"SKILL.md at '{repositoryName}/{skillPath}' has unterminated YAML frontmatter.");
        }

        try
        {
            return YamlHelpers.LoadMapping(string.Join(Environment.NewLine, lines), "SKILL.md frontmatter");
        }
        catch (ConfigurationException exception)
        {
            throw new HarnfigException($"Invalid SKILL.md at '{repositoryName}/{skillPath}': {exception.Message}", exception);
        }
    }

    private static void RejectUnsupportedEntries(string root)
    {
        var pending = new Stack<string>();
        pending.Push(root);

        while (pending.Count > 0)
        {
            var directory = pending.Pop();
            foreach (var entry in Directory.EnumerateFileSystemEntries(directory))
            {
                var info = File.GetAttributes(entry);
                if ((info & FileAttributes.ReparsePoint) != 0 || new FileInfo(entry).LinkTarget is not null)
                {
                    throw new HarnfigException($"Skill contains a symlink, which harnfig v0.1 does not install: {Path.GetRelativePath(root, entry)}");
                }

                if (Directory.Exists(entry))
                {
                    pending.Push(entry);
                }
                else if (!File.Exists(entry))
                {
                    throw new HarnfigException($"Skill contains an unsupported filesystem entry: {Path.GetRelativePath(root, entry)}");
                }
            }
        }
    }

    private static bool IsWithin(string path, string root)
    {
        var relative = Path.GetRelativePath(root, path);
        return relative == "." || (!relative.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal) && relative != "..");
    }
}
