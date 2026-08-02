namespace Harnfig;

internal sealed record GitCheckout(string Directory, string Commit);

internal interface IGitCheckoutService
{
    Task<GitCheckout> CheckoutAsync(
        RepoReference repository,
        string revision,
        string destination,
        CancellationToken cancellationToken);
}

internal sealed class GitCheckoutService(IGitRunner gitRunner) : IGitCheckoutService
{
    public async Task<GitCheckout> CheckoutAsync(
        RepoReference repository,
        string revision,
        string destination,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(destination);
        await gitRunner.RunAsync(destination, ["init", "--quiet"], repository.Normalized, cancellationToken);
        await gitRunner.RunAsync(destination, ["remote", "add", "origin", repository.CloneUrl], repository.Normalized, cancellationToken);
        await gitRunner.RunAsync(destination, ["fetch", "--quiet", "--depth", "1", "origin", revision], repository.Normalized, cancellationToken);
        await gitRunner.RunAsync(
            destination,
            ["-c", "core.autocrlf=false", "-c", "core.hooksPath=/dev/null", "checkout", "--quiet", "--detach", "FETCH_HEAD"],
            repository.Normalized,
            cancellationToken);
        var commit = await gitRunner.RunAsync(destination, ["rev-parse", "HEAD"], repository.Normalized, cancellationToken);

        if (commit.Length != 40 || !commit.All(Uri.IsHexDigit))
        {
            throw new HarnfigException($"Git returned an invalid commit for '{repository.Normalized}'.");
        }

        return new GitCheckout(destination, commit.ToLowerInvariant());
    }
}
