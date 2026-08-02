namespace Harnfig.Tests;

public sealed class RepoReferenceParserTests
{
    [Theory]
    [InlineData("owner/repo", "owner/repo", "https://github.com/owner/repo.git")]
    [InlineData("https://github.com/owner/repo.git", "owner/repo", "https://github.com/owner/repo.git")]
    [InlineData("git@github.com:owner/repo.git", "owner/repo", "git@github.com:owner/repo.git")]
    public void NormalizesSupportedReferences(string input, string normalized, string cloneUrl)
    {
        var result = RepoReferenceParser.Parse(input);

        Assert.Equal(normalized, result.Normalized);
        Assert.Equal(cloneUrl, result.CloneUrl);
    }

    [Theory]
    [InlineData("https://gitlab.com/owner/repo")]
    [InlineData("https://token@github.com/owner/repo")]
    [InlineData("file:///tmp/repo")]
    public void RejectsUnsupportedOrCredentialedReferences(string input)
        => Assert.Throws<ConfigurationException>(() => RepoReferenceParser.Parse(input));
}
