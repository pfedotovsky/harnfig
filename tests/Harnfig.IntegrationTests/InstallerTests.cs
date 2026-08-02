namespace Harnfig.IntegrationTests;

public sealed class InstallerTests
{
    [Fact]
    public async Task InstallsForEveryAgentAndIsIdempotent()
    {
        using var fixture = new InstallFixture();
        fixture.WriteConfig(scope: "project", agents: "cursor, codex, claude");

        var first = await fixture.InstallAsync();
        var second = await fixture.InstallAsync();

        Assert.Equal(3, first.Installed);
        Assert.Equal(0, first.Unchanged);
        Assert.Equal(0, second.Installed);
        Assert.Equal(3, second.Unchanged);
        Assert.True(File.Exists(Path.Combine(fixture.Project, ".cursor", "skills", "test-skill", "SKILL.md")));
        Assert.True(File.Exists(Path.Combine(fixture.Project, ".agents", "skills", "test-skill", "SKILL.md")));
        Assert.True(File.Exists(Path.Combine(fixture.Project, ".claude", "skills", "test-skill", "SKILL.md")));
        Assert.True(File.Exists(Path.Combine(fixture.Project, "harnfig.lock")));
        Assert.Equal(2, fixture.Checkout.Calls);
    }

    [Fact]
    public async Task InstallsInUserScope()
    {
        using var fixture = new InstallFixture();
        fixture.WriteConfig(scope: "user", agents: "cursor, codex, claude");

        await fixture.InstallAsync();

        Assert.True(File.Exists(Path.Combine(fixture.Home, ".cursor", "skills", "test-skill", "SKILL.md")));
        Assert.True(File.Exists(Path.Combine(fixture.Home, ".agents", "skills", "test-skill", "SKILL.md")));
        Assert.True(File.Exists(Path.Combine(fixture.Home, ".claude", "skills", "test-skill", "SKILL.md")));
    }

    [Fact]
    public async Task DryRunWritesNothing()
    {
        using var fixture = new InstallFixture();
        fixture.WriteConfig(scope: "project", agents: "codex");

        var result = await fixture.InstallAsync(dryRun: true);

        Assert.Equal(1, result.Installed);
        Assert.False(File.Exists(Path.Combine(fixture.Project, "harnfig.lock")));
        Assert.False(Directory.Exists(Path.Combine(fixture.Project, ".agents")));
    }

    [Fact]
    public async Task RejectsModifiedDestinationUnlessForced()
    {
        using var fixture = new InstallFixture();
        fixture.WriteConfig(scope: "project", agents: "codex");
        await fixture.InstallAsync();
        var installed = Path.Combine(fixture.Project, ".agents", "skills", "test-skill", "SKILL.md");
        await File.AppendAllTextAsync(installed, "\nlocal change");

        await Assert.ThrowsAsync<HarnfigException>(() => fixture.InstallAsync());
        var result = await fixture.InstallAsync(force: true);

        Assert.Equal(1, result.Installed);
        Assert.DoesNotContain("local change", await File.ReadAllTextAsync(installed), StringComparison.Ordinal);
    }

    [Fact]
    public async Task FrozenRejectsConfigurationChanges()
    {
        using var fixture = new InstallFixture();
        fixture.WriteConfig(scope: "project", agents: "codex", @ref: "main");
        await fixture.InstallAsync();
        fixture.WriteConfig(scope: "project", agents: "codex", @ref: "next");

        await Assert.ThrowsAsync<ConfigurationException>(() => fixture.InstallAsync(frozen: true));
    }

    [Fact]
    public async Task UpdateRefreshesAResolvedRef()
    {
        using var fixture = new InstallFixture();
        fixture.WriteConfig(scope: "project", agents: "codex");
        await fixture.InstallAsync();
        fixture.Checkout.CommitCharacter = 'b';

        var locked = await fixture.InstallAsync();
        var updated = await fixture.InstallAsync(update: true);

        Assert.Equal(1, locked.Unchanged);
        Assert.Equal(1, updated.Unchanged);
        var lockContents = await File.ReadAllTextAsync(Path.Combine(fixture.Project, "harnfig.lock"));
        Assert.Contains(new string('b', 40), lockContents, StringComparison.Ordinal);
    }

    [Fact]
    public void FileTransactionRestoresCommittedDestinationsWhenNotCompleted()
    {
        using var temporary = new TemporaryDirectory();
        var original = Path.Combine(temporary.Path, "original");
        var replacement = Path.Combine(temporary.Path, "replacement");
        Directory.CreateDirectory(original);
        Directory.CreateDirectory(replacement);
        File.WriteAllText(Path.Combine(original, "value.txt"), "old");
        File.WriteAllText(Path.Combine(replacement, "value.txt"), "new");

        using (var transaction = new FileTransaction())
        {
            var staged = transaction.StageDirectory(replacement, original);
            transaction.Commit(staged, original);
            Assert.Equal("new", File.ReadAllText(Path.Combine(original, "value.txt")));
        }

        Assert.Equal("old", File.ReadAllText(Path.Combine(original, "value.txt")));
    }

    private sealed class InstallFixture : IDisposable
    {
        private readonly TemporaryDirectory _temporary = new();

        public InstallFixture()
        {
            Project = Path.Combine(_temporary.Path, "project");
            Home = Path.Combine(_temporary.Path, "home");
            var repository = Path.Combine(_temporary.Path, "fixture-repository");
            var skill = Path.Combine(repository, "skills", "test-skill");
            Directory.CreateDirectory(Project);
            Directory.CreateDirectory(Home);
            Directory.CreateDirectory(skill);
            File.WriteAllText(Path.Combine(skill, "SKILL.md"), """
                ---
                name: test-skill
                description: Use this fixture skill in installer tests.
                ---
                Test instructions.
                """);
            File.WriteAllText(Path.Combine(skill, "asset.txt"), "asset");
            Checkout = new FixtureCheckoutService(repository);
        }

        public string Project { get; }

        public string Home { get; }

        public FixtureCheckoutService Checkout { get; }

        public void WriteConfig(string scope, string agents, string @ref = "main")
        {
            File.WriteAllText(Path.Combine(Project, "harnfig.yaml"), $$"""
                version: 1
                scope: {{scope}}
                agents: [{{agents}}]
                skills:
                  - repo: owner/repo
                    path: skills/test-skill
                    ref: {{@ref}}
                """);
        }

        public Task<InstallSummary> InstallAsync(bool dryRun = false, bool frozen = false, bool force = false, bool update = false)
        {
            var installer = new Installer(new ConfigParser(), new LockFileStore(), Checkout, new SkillValidator());
            var request = new InstallRequest(null, null, dryRun, frozen, update, force, Project, Home);
            return installer.InstallAsync(request, TextWriter.Null, CancellationToken.None);
        }

        public void Dispose() => _temporary.Dispose();
    }

    internal sealed class FixtureCheckoutService(string source) : IGitCheckoutService
    {
        public int Calls { get; private set; }

        public char CommitCharacter { get; set; } = 'a';

        public Task<GitCheckout> CheckoutAsync(RepoReference repository, string revision, string destination, CancellationToken cancellationToken)
        {
            Calls++;
            FileTree.CopyDirectory(source, destination);
            var commitCharacter = revision.Length == 40 ? revision[0] : CommitCharacter;
            return Task.FromResult(new GitCheckout(destination, new string(commitCharacter, 40)));
        }
    }
}
