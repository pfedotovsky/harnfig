namespace Harnfig;

internal sealed class FileTransaction : IDisposable
{
    private readonly List<CommittedMove> _committed = [];
    private readonly List<string> _staged = [];
    private bool _completed;

    public string StageDirectory(string source, string destination)
    {
        var parent = Path.GetDirectoryName(destination)
            ?? throw new HarnfigException($"Could not determine parent directory for '{destination}'.");
        Directory.CreateDirectory(parent);
        var staged = Path.Combine(parent, $".{Path.GetFileName(destination)}.harnfig-stage-{Guid.NewGuid():N}");
        FileTree.CopyDirectory(source, staged);
        _staged.Add(staged);
        return staged;
    }

    public string StageFile(string contents, string destination)
    {
        var parent = Path.GetDirectoryName(destination)
            ?? throw new HarnfigException($"Could not determine parent directory for '{destination}'.");
        Directory.CreateDirectory(parent);
        var staged = Path.Combine(parent, $".{Path.GetFileName(destination)}.harnfig-stage-{Guid.NewGuid():N}");
        File.WriteAllText(staged, contents);
        _staged.Add(staged);
        return staged;
    }

    public void Commit(string staged, string destination)
    {
        string? backup = null;
        if (FileTree.Exists(destination))
        {
            backup = $"{destination}.harnfig-backup-{Guid.NewGuid():N}";
            Move(destination, backup);
        }

        try
        {
            Move(staged, destination);
            _staged.Remove(staged);
            _committed.Add(new CommittedMove(destination, backup));
        }
        catch
        {
            if (backup is not null && FileTree.Exists(backup) && !FileTree.Exists(destination))
            {
                Move(backup, destination);
            }

            throw;
        }
    }

    public void Complete()
    {
        foreach (var move in _committed)
        {
            if (move.Backup is not null)
            {
                FileTree.Delete(move.Backup);
            }
        }

        _completed = true;
    }

    public void Dispose()
    {
        if (!_completed)
        {
            RollBack();
        }

        foreach (var staged in _staged)
        {
            if (FileTree.Exists(staged))
            {
                FileTree.Delete(staged);
            }
        }
    }

    private void RollBack()
    {
        foreach (var move in _committed.AsEnumerable().Reverse())
        {
            if (FileTree.Exists(move.Destination))
            {
                FileTree.Delete(move.Destination);
            }

            if (move.Backup is not null && FileTree.Exists(move.Backup))
            {
                Move(move.Backup, move.Destination);
            }
        }
    }

    private static void Move(string source, string destination)
    {
        if (Directory.Exists(source))
        {
            Directory.Move(source, destination);
        }
        else
        {
            File.Move(source, destination);
        }
    }

    private sealed record CommittedMove(string Destination, string? Backup);
}
