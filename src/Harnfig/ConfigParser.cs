using YamlDotNet.RepresentationModel;

namespace Harnfig;

internal sealed class ConfigParser
{
    public HarnfigConfig Parse(string yaml, string sourceName = "harnfig.yaml")
    {
        var root = YamlHelpers.LoadMapping(yaml, sourceName);
        YamlHelpers.RequireOnlyKeys(root, sourceName, "version", "scope", "agents", "skills");

        var version = YamlHelpers.RequiredInt(root, "version", sourceName);
        if (version != 1)
        {
            throw new ConfigurationException($"Unsupported config version '{version}'. Expected version 1.");
        }

        var scope = ParseScope(YamlHelpers.OptionalScalar(root, "scope", sourceName) ?? "project");
        var agents = ParseAgents(YamlHelpers.RequiredSequence(root, "agents", sourceName));
        var skills = ParseSkills(YamlHelpers.RequiredSequence(root, "skills", sourceName));

        if (agents.Count == 0)
        {
            throw new ConfigurationException("Field 'agents' must contain at least one agent.");
        }

        if (skills.Count == 0)
        {
            throw new ConfigurationException("Field 'skills' must contain at least one skill.");
        }

        return new HarnfigConfig(version, scope, agents, skills);
    }

    internal static InstallScope ParseScope(string value) => value switch
    {
        "project" => InstallScope.Project,
        "user" => InstallScope.User,
        _ => throw new ConfigurationException($"Unknown scope '{value}'. Expected 'project' or 'user'."),
    };

    private static IReadOnlyList<AgentKind> ParseAgents(YamlSequenceNode sequence)
    {
        var agents = new List<AgentKind>();
        var seen = new HashSet<AgentKind>();

        foreach (var node in sequence.Children)
        {
            var value = YamlHelpers.Scalar(node, "agents item").Trim();
            var agent = value switch
            {
                "cursor" => AgentKind.Cursor,
                "codex" => AgentKind.Codex,
                "claude" => AgentKind.Claude,
                _ => throw new ConfigurationException($"Unknown agent '{value}'. Expected cursor, codex, or claude."),
            };

            if (!seen.Add(agent))
            {
                throw new ConfigurationException($"Agent '{value}' is listed more than once.");
            }

            agents.Add(agent);
        }

        return agents;
    }

    private static IReadOnlyList<SkillSpec> ParseSkills(YamlSequenceNode sequence)
    {
        var skills = new List<SkillSpec>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        for (var index = 0; index < sequence.Children.Count; index++)
        {
            var context = $"skills[{index}]";
            var mapping = YamlHelpers.Mapping(sequence.Children[index], context);
            YamlHelpers.RequireOnlyKeys(mapping, context, "repo", "path", "ref");

            var repo = YamlHelpers.RequiredScalar(mapping, "repo", context);
            var path = NormalizeSkillPath(YamlHelpers.RequiredScalar(mapping, "path", context));
            var @ref = YamlHelpers.OptionalScalar(mapping, "ref", context) ?? "HEAD";
            var normalizedRepo = RepoReferenceParser.Parse(repo).Normalized;
            var identity = $"{normalizedRepo}\n{path}\n{@ref}";

            if (!seen.Add(identity))
            {
                throw new ConfigurationException($"Duplicate skill entry for '{normalizedRepo}' at '{path}' with ref '{@ref}'.");
            }

            skills.Add(new SkillSpec(repo, path, @ref));
        }

        return skills;
    }

    internal static string NormalizeSkillPath(string value)
    {
        if (value == ".")
        {
            return value;
        }

        if (value.Contains('\\', StringComparison.Ordinal) || value.StartsWith("/", StringComparison.Ordinal))
        {
            throw new ConfigurationException($"Skill path '{value}' must be a relative repository path using '/'.");
        }

        var segments = value.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0 || segments.Any(segment => segment is "." or ".."))
        {
            throw new ConfigurationException($"Skill path '{value}' is invalid or escapes the repository.");
        }

        return string.Join('/', segments);
    }
}
