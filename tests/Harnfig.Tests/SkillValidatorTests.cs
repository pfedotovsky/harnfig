namespace Harnfig.Tests;

public sealed class SkillValidatorTests
{
    [Fact]
    public void ValidatesPortableSkillAndCalculatesStableDigest()
    {
        using var temporary = new TemporaryDirectory();
        var skill = Path.Combine(temporary.Path, "skills", "test-skill");
        Directory.CreateDirectory(skill);
        File.WriteAllText(Path.Combine(skill, "SKILL.md"), """
            ---
            name: test-skill
            description: Use this skill for tests.
            custom-agent-field: preserved
            ---
            Test instructions.
            """);
        Directory.CreateDirectory(Path.Combine(skill, "references"));
        File.WriteAllText(Path.Combine(skill, "references", "example.md"), "example");

        var validated = new SkillValidator().Validate(temporary.Path, "owner/repo", "skills/test-skill");
        var first = DirectoryDigest.Calculate(validated.Directory);
        var second = DirectoryDigest.Calculate(validated.Directory);

        Assert.Equal("test-skill", validated.Name);
        Assert.Equal(first, second);
        Assert.StartsWith("sha256:", first, StringComparison.Ordinal);
    }

    [Fact]
    public void RequiresNameToMatchDirectory()
    {
        using var temporary = new TemporaryDirectory();
        var skill = Path.Combine(temporary.Path, "test-skill");
        Directory.CreateDirectory(skill);
        File.WriteAllText(Path.Combine(skill, "SKILL.md"), """
            ---
            name: other-skill
            description: A mismatched skill.
            ---
            """);

        Assert.Throws<HarnfigException>(() => new SkillValidator().Validate(temporary.Path, "owner/repo", "test-skill"));
    }

    [Fact]
    public void RejectsSymlinks()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var temporary = new TemporaryDirectory();
        var skill = Path.Combine(temporary.Path, "test-skill");
        Directory.CreateDirectory(skill);
        File.WriteAllText(Path.Combine(skill, "SKILL.md"), """
            ---
            name: test-skill
            description: A skill containing a symlink.
            ---
            """);
        File.WriteAllText(Path.Combine(skill, "target.txt"), "target");
        File.CreateSymbolicLink(Path.Combine(skill, "link.txt"), "target.txt");

        Assert.Throws<HarnfigException>(() => new SkillValidator().Validate(temporary.Path, "owner/repo", "test-skill"));
    }
}
