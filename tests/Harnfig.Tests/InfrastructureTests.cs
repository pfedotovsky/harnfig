namespace Harnfig.Tests;

public sealed class InfrastructureTests
{
    [Theory]
    [InlineData("cursor", ".cursor/skills")]
    [InlineData("codex", ".agents/skills")]
    [InlineData("claude", ".claude/skills")]
    public void MapsProjectAndUserTargets(string agentName, string expectedRelative)
    {
        var agent = Enum.Parse<AgentKind>(agentName, ignoreCase: true);
        var relative = expectedRelative.Replace('/', Path.DirectorySeparatorChar);

        Assert.Equal(Path.Combine("project", relative), TargetMapper.GetSkillsRoot(agent, InstallScope.Project, "project", "home"));
        Assert.Equal(Path.Combine("home", relative), TargetMapper.GetSkillsRoot(agent, InstallScope.User, "project", "home"));
    }

    [Fact]
    public void RedactsHttpsCredentials()
    {
        var message = "fatal: could not read https://secret-token@github.com/owner/repo.git";

        Assert.Equal(
            "fatal: could not read https://***@github.com/owner/repo.git",
            GitRunner.Redact(message));
    }
}
