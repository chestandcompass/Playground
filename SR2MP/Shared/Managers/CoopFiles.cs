using System.Security.Cryptography;
using System.Text;

namespace SR2MP.Shared.Managers;

// No game dependencies: the same implementation is exercised by the storage tests.
internal static class CoopFiles
{
    internal static string Key(string value)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    internal static string Child(string root, string key)
    {
        if (key.Length != 64 || key.Any(c => !Uri.IsHexDigit(c)))
            throw new InvalidDataException("Invalid multiplayer world identifier.");
        return Path.Combine(Path.GetFullPath(root), key.ToLowerInvariant());
    }

    internal static void WriteAtomic(string path, byte[] bytes)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                stream.Write(bytes);
                stream.Flush(true);
            }
            if (File.Exists(path))
                File.Replace(temporary, path, path + ".previous");
            else
                File.Move(temporary, path);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }
}
