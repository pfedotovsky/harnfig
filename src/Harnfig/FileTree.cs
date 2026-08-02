namespace Harnfig;

internal static class FileTree
{
    public static bool Exists(string path) => Directory.Exists(path) || File.Exists(path);

    public static void Delete(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
        else if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    public static void CopyDirectory(string source, string destination)
    {
        var sourceInfo = new DirectoryInfo(source);
        if (!sourceInfo.Exists)
        {
            throw new DirectoryNotFoundException(source);
        }

        Directory.CreateDirectory(destination);
        CopyUnixMode(source, destination, isDirectory: true);

        foreach (var directory in Directory.EnumerateDirectories(source, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(source, directory);
            var target = Path.Combine(destination, relative);
            Directory.CreateDirectory(target);
            CopyUnixMode(directory, target, isDirectory: true);
        }

        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(source, file);
            var target = Path.Combine(destination, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, overwrite: false);
            CopyUnixMode(file, target, isDirectory: false);
        }
    }

    private static void CopyUnixMode(string source, string destination, bool isDirectory)
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        try
        {
            var mode = File.GetUnixFileMode(source);
            File.SetUnixFileMode(destination, mode);
        }
        catch (PlatformNotSupportedException)
        {
            // The content is still valid on a platform without Unix mode APIs.
        }
    }
}
