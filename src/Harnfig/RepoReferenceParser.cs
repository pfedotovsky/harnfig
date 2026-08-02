using System.Text.RegularExpressions;

namespace Harnfig;

internal static partial class RepoReferenceParser
{
    [GeneratedRegex(@"^(?<owner>[A-Za-z0-9_.-]+)/(?<repo>[A-Za-z0-9_.-]+?)(?:\.git)?$")]
    private static partial Regex ShorthandRegex();

    [GeneratedRegex(@"^git@github\.com:(?<owner>[A-Za-z0-9_.-]+)/(?<repo>[A-Za-z0-9_.-]+?)(?:\.git)?$")]
    private static partial Regex SshRegex();

    public static RepoReference Parse(string value)
    {
        var trimmed = value.Trim();
        var shorthand = ShorthandRegex().Match(trimmed);
        if (shorthand.Success)
        {
            var normalized = Normalize(shorthand);
            return new RepoReference(normalized, $"https://github.com/{normalized}.git");
        }

        var ssh = SshRegex().Match(trimmed);
        if (ssh.Success)
        {
            return new RepoReference(Normalize(ssh), trimmed.EndsWith(".git", StringComparison.Ordinal) ? trimmed : $"{trimmed}.git");
        }

        if (Uri.TryCreate(trimmed, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps)
        {
            if (!string.Equals(uri.Host, "github.com", StringComparison.OrdinalIgnoreCase))
            {
                throw new ConfigurationException("Only github.com repositories are supported in harnfig v0.1.");
            }

            if (!string.IsNullOrEmpty(uri.UserInfo))
            {
                throw new ConfigurationException("Repository URLs must not contain embedded credentials. Configure Git credentials instead.");
            }

            var path = uri.AbsolutePath.Trim('/');
            var match = ShorthandRegex().Match(path);
            if (!match.Success)
            {
                throw new ConfigurationException($"Repository URL '{trimmed}' must identify a GitHub owner and repository.");
            }

            var normalized = Normalize(match);
            return new RepoReference(normalized, $"https://github.com/{normalized}.git");
        }

        throw new ConfigurationException($"Repository '{trimmed}' must be owner/repo, a GitHub HTTPS URL, or a GitHub SSH URL.");
    }

    private static string Normalize(Match match)
        => $"{match.Groups["owner"].Value}/{match.Groups["repo"].Value}";
}
