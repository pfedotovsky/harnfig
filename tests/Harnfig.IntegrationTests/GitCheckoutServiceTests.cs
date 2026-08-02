using System.Diagnostics;

namespace Harnfig.IntegrationTests;

public sealed class GitCheckoutServiceTests
{
    [Fact]
    public async Task FetchesARevisionFromALocalGitRepository()
    {
        using var temporary = new TemporaryDirectory();
        var source = Path.Combine(temporary.Path, "source");
        var destination = Path.Combine(temporary.Path, "checkout");
        Directory.CreateDirectory(source);
        RunGit(source, "init", "--quiet");
        File.WriteAllText(Path.Combine(source, "file.txt"), "content");
        RunGit(source, "add", "file.txt");
        RunGit(source, "-c", "user.name=Harnfig Tests", "-c", "user.email=harnfig@example.invalid", "commit", "--quiet", "-m", "fixture");

        var service = new GitCheckoutService(new GitRunner());
        var checkout = await service.CheckoutAsync(new RepoReference("local/fixture", source), "HEAD", destination, CancellationToken.None);

        Assert.True(File.Exists(Path.Combine(checkout.Directory, "file.txt")));
        Assert.Equal(40, checkout.Commit.Length);
    }

    private static void RunGit(string workingDirectory, params string[] arguments)
    {
        var start = new ProcessStartInfo("git")
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        using var process = Process.Start(start) ?? throw new InvalidOperationException("Could not start Git.");
        process.WaitForExit();
        Assert.True(process.ExitCode == 0, process.StandardError.ReadToEnd());
    }
}
