using System.Diagnostics;
using System.Text.RegularExpressions;

namespace Harnfig;

internal interface IGitRunner
{
    Task<string> RunAsync(string workingDirectory, IReadOnlyList<string> arguments, string repositoryName, CancellationToken cancellationToken);
}

internal sealed partial class GitRunner : IGitRunner
{
    [GeneratedRegex(@"(?i)(https://)[^/@\s]+@")]
    private static partial Regex HttpsCredentialsRegex();

    public async Task<string> RunAsync(
        string workingDirectory,
        IReadOnlyList<string> arguments,
        string repositoryName,
        CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo("git")
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        try
        {
            using var process = new Process { StartInfo = startInfo };
            if (!process.Start())
            {
                throw new HarnfigException("Could not start Git.");
            }

            var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);
            var stdout = await stdoutTask;
            var stderr = await stderrTask;

            if (process.ExitCode != 0)
            {
                var safeError = Redact(stderr.Trim());
                throw new HarnfigException($"Git failed for '{repositoryName}': {safeError}");
            }

            return stdout.Trim();
        }
        catch (HarnfigException)
        {
            throw;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            throw new HarnfigException($"Could not run Git for '{repositoryName}': {exception.Message}", exception);
        }
    }

    internal static string Redact(string value)
        => HttpsCredentialsRegex().Replace(value, "$1***@");
}
