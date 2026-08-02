namespace Harnfig.Tests;

public sealed class ConfigParserTests
{
    [Fact]
    public void ParsesCompleteConfiguration()
    {
        const string yaml = """
            version: 1
            scope: user
            agents: [cursor, codex, claude]
            skills:
              - repo: mattpocock/skills
                path: skills/productivity/grill-me
                ref: main
            """;

        var config = new ConfigParser().Parse(yaml);

        Assert.Equal(InstallScope.User, config.Scope);
        Assert.Equal([AgentKind.Cursor, AgentKind.Codex, AgentKind.Claude], config.Agents);
        var skill = Assert.Single(config.Skills);
        Assert.Equal("mattpocock/skills", skill.Repo);
        Assert.Equal("skills/productivity/grill-me", skill.Path);
        Assert.Equal("main", skill.Ref);
    }

    [Fact]
    public void DefaultsScopeAndRef()
    {
        const string yaml = """
            version: 1
            agents: [codex]
            skills:
              - repo: owner/repo
                path: .
            """;

        var config = new ConfigParser().Parse(yaml);

        Assert.Equal(InstallScope.Project, config.Scope);
        Assert.Equal("HEAD", Assert.Single(config.Skills).Ref);
    }

    [Theory]
    [InlineData("unexpected: true", "Unknown field 'unexpected'")]
    [InlineData("scope: machine", "Unknown scope 'machine'")]
    public void RejectsInvalidTopLevelConfiguration(string replacement, string expected)
    {
        var yaml = $$"""
            version: 1
            {{replacement}}
            agents: [codex]
            skills:
              - repo: owner/repo
                path: skill
            """;

        var exception = Assert.Throws<ConfigurationException>(() => new ConfigParser().Parse(yaml));

        Assert.Contains(expected, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RejectsUnknownAgent()
    {
        const string yaml = """
            version: 1
            agents: [windsurf]
            skills:
              - repo: owner/repo
                path: skill
            """;

        var exception = Assert.Throws<ConfigurationException>(() => new ConfigParser().Parse(yaml));

        Assert.Contains("Unknown agent 'windsurf'", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("../secret")]
    [InlineData("/absolute")]
    [InlineData("folder\\skill")]
    [InlineData("folder/./skill")]
    public void RejectsUnsafeSkillPaths(string path)
    {
        var yaml = $$"""
            version: 1
            agents: [codex]
            skills:
              - repo: owner/repo
                path: {{path}}
            """;

        Assert.Throws<ConfigurationException>(() => new ConfigParser().Parse(yaml));
    }
}
