namespace Harnfig.Tests;

public sealed class LockFileStoreTests
{
    [Fact]
    public void SerializesDeterministicallyAndRoundTrips()
    {
        var lockFile = new HarnfigLock(1,
        [
            new LockEntry("z-skill", "owner/repo", "z-skill", "main", new string('a', 40), $"sha256:{new string('b', 64)}"),
            new LockEntry("a-skill", "owner/repo", "a-skill", "v1", new string('c', 40), $"sha256:{new string('d', 64)}"),
        ]);
        var store = new LockFileStore();

        var serialized = store.Serialize(lockFile);
        using var temporary = new TemporaryDirectory();
        var path = Path.Combine(temporary.Path, "harnfig.lock");
        File.WriteAllText(path, serialized);
        var parsed = store.Load(path);

        Assert.NotNull(parsed);
        Assert.Equal("a-skill", parsed.Skills[0].Name);
        Assert.Equal(serialized, store.Serialize(parsed));
    }
}
