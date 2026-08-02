using System.Security.Cryptography;
using System.Text;

namespace Harnfig;

internal static class DirectoryDigest
{
    public static string Calculate(string directory)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var files = Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories)
            .Select(path => new
            {
                FullPath = path,
                RelativePath = Path.GetRelativePath(directory, path).Replace(Path.DirectorySeparatorChar, '/'),
            })
            .OrderBy(item => item.RelativePath, StringComparer.Ordinal);

        foreach (var file in files)
        {
            Append(hash, file.RelativePath);
            using var stream = File.OpenRead(file.FullPath);
            var buffer = new byte[64 * 1024];
            int read;
            while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
            {
                hash.AppendData(buffer, 0, read);
            }

            hash.AppendData([0]);
        }

        return $"sha256:{Convert.ToHexStringLower(hash.GetHashAndReset())}";
    }

    private static void Append(IncrementalHash hash, string value)
    {
        hash.AppendData(Encoding.UTF8.GetBytes(value));
        hash.AppendData([0]);
    }
}
