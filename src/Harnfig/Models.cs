namespace Harnfig;

internal enum InstallScope
{
    Project,
    User,
}

internal enum AgentKind
{
    Cursor,
    Codex,
    Claude,
}

internal sealed record SkillSpec(string Repo, string Path, string Ref);

internal sealed record HarnfigConfig(
    int Version,
    InstallScope Scope,
    IReadOnlyList<AgentKind> Agents,
    IReadOnlyList<SkillSpec> Skills);

internal sealed record RepoReference(string Normalized, string CloneUrl);

internal sealed record LockEntry(
    string Name,
    string Repo,
    string Path,
    string Ref,
    string Commit,
    string Digest);

internal sealed record HarnfigLock(int Version, IReadOnlyList<LockEntry> Skills);

internal sealed record ValidatedSkill(
    string Name,
    string SourceDirectory,
    string Repo,
    string Path,
    string Ref,
    string Commit,
    string Digest);

internal sealed record InstallRequest(
    string? ConfigPath,
    InstallScope? ScopeOverride,
    bool DryRun,
    bool Frozen,
    bool Update,
    bool Force,
    string WorkingDirectory,
    string? HomeDirectory = null);

internal sealed record InstallSummary(int Installed, int Unchanged);
