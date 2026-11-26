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

    internal static void CopySave(string sourceRoot, string destinationRoot, string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName) || Path.GetFileName(fileName) != fileName
            || !fileName.EndsWith(".sav", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Invalid save filename.");
        var source = Path.Combine(Path.GetFullPath(sourceRoot), fileName);
        var destination = Path.Combine(Path.GetFullPath(destinationRoot), fileName);
        if (string.Equals(source, destination, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("The copy must use a separate save directory.");
        if ((File.GetAttributes(source) & FileAttributes.ReparsePoint) != 0)
            throw new InvalidDataException("Save links cannot be imported.");
        Directory.CreateDirectory(destinationRoot);
        var temporary = destination + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            // Deny concurrent writers while taking the snapshot. Publish only a complete
            // copy, and never overwrite a multiplayer save, even if another copy raced us.
            using (var input = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                input.CopyTo(output);
                output.Flush(true);
            }
            File.Move(temporary, destination);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
