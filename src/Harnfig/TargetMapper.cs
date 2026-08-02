namespace Harnfig;

internal static class TargetMapper
{
    public static string GetSkillsRoot(
        AgentKind agent,
        InstallScope scope,
        string projectDirectory,
        string homeDirectory)
    {
        var relative = agent switch
        {
            AgentKind.Cursor => Path.Combine(".cursor", "skills"),
            AgentKind.Codex => Path.Combine(".agents", "skills"),
            AgentKind.Claude => Path.Combine(".claude", "skills"),
            _ => throw new ArgumentOutOfRangeException(nameof(agent)),
        };

        return Path.Combine(scope == InstallScope.Project ? projectDirectory : homeDirectory, relative);
    }

    public static string DisplayName(AgentKind agent) => agent switch
    {
        AgentKind.Cursor => "cursor",
        AgentKind.Codex => "codex",
        AgentKind.Claude => "claude",
        _ => throw new ArgumentOutOfRangeException(nameof(agent)),
    };
}
