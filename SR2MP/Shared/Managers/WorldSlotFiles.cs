using System.Text.Json;
using System.Security.Cryptography;

namespace SR2MP.Shared.Managers;

// Native save decoding stays in the runtime. This layer only publishes validated
// snapshots, keeping a recoverable complete copy of the destination directory.
internal static class WorldSlotFiles
{
    private sealed record Journal(string Root, string Stage, string Backup);
    private static readonly object Gate = new();
    internal static string Fingerprint(string root)
    {
        CheckDirectory(root);
        if (!Directory.Exists(root)) return "empty";
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (var path in Directory.EnumerateFiles(root).OrderBy(Path.GetFileName, StringComparer.Ordinal))
        {
            if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0) throw new IOException("Save links are not supported.");
            hash.AppendData(System.Text.Encoding.UTF8.GetBytes(Path.GetFileName(path)));
            using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var sha = SHA256.Create();
            hash.AppendData(sha.ComputeHash(file));
        }
        return Convert.ToHexString(hash.GetHashAndReset());
    }
    private static void CheckDirectory(string root)
    {
        for (var directory = new DirectoryInfo(Path.GetFullPath(root)); directory != null; directory = directory.Parent)
            if (directory.Exists && (directory.Attributes & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Linked save directories are not supported.");
    }
    private static string Name(string name)
    {
        if (Path.GetFileName(name) != name || !name.EndsWith(".sav", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Invalid save filename.");
        return name;
    }
    internal static void Recover(string root)
    {
        lock (Gate)
        {
            root = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar);
            var path = root + ".playground-transfer.json";
            if (!File.Exists(path)) return;
            CheckDirectory(root);
            var journal = JsonSerializer.Deserialize<Journal>(File.ReadAllText(path)) ?? throw new InvalidDataException("Invalid transfer journal.");
            if (journal.Root != root || !journal.Stage.StartsWith(root + ".stage.", StringComparison.Ordinal)
                || !journal.Backup.StartsWith(root + ".backup.", StringComparison.Ordinal)
                || Path.GetDirectoryName(journal.Stage) != Path.GetDirectoryName(root)
                || Path.GetDirectoryName(journal.Backup) != Path.GetDirectoryName(root))
                throw new InvalidDataException("Invalid recovery paths.");
            CheckDirectory(journal.Backup); CheckDirectory(journal.Stage);
            if (!Directory.Exists(root) && Directory.Exists(journal.Backup)) Directory.Move(journal.Backup, root);
            // Never delete a recovery copy. An interrupted stage can be inspected.
            File.Delete(path);
        }
    }
    internal static string Publish(string root, string expectedFingerprint, string fileName, byte[] bytes,
        IReadOnlyCollection<string> replacedFiles, Action? afterBackup = null)
    {
        lock (Gate)
        {
            root = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar);
            Recover(root);
            if (Fingerprint(root) != expectedFingerprint) throw new IOException("The destination changed. Select the slots again.");
            fileName = Name(fileName);
            foreach (var name in replacedFiles) Name(name);
            var stage = root + ".stage." + Guid.NewGuid().ToString("N");
            var backup = root + ".backup." + DateTime.UtcNow.ToString("yyyyMMddHHmmss") + "." + Guid.NewGuid().ToString("N");
            Directory.CreateDirectory(stage);
            var journalPath = root + ".playground-transfer.json";
            var moved = false;
            try
            {
                if (Directory.Exists(root))
                {
                    foreach (var directory in Directory.EnumerateDirectories(root))
                        throw new IOException("Unexpected save subdirectory: " + Path.GetFileName(directory));
                    foreach (var path in Directory.EnumerateFiles(root))
                    {
                        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0) throw new IOException("Linked save file.");
                        using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                        using var output = new FileStream(Path.Combine(stage,Path.GetFileName(path)),FileMode.CreateNew,FileAccess.Write);
                        input.CopyTo(output); output.Flush(true);
                    }
                }
                foreach (var name in replacedFiles) File.Delete(Path.Combine(stage,name));
                using (var output = new FileStream(Path.Combine(stage,fileName),FileMode.CreateNew,FileAccess.Write))
                { output.Write(bytes); output.Flush(true); }
                if (Fingerprint(root) != expectedFingerprint) throw new IOException("The destination changed during copying.");
                CoopFiles.WriteAtomic(journalPath, JsonSerializer.SerializeToUtf8Bytes(new Journal(root,stage,backup)));
                if (Directory.Exists(root)) { Directory.Move(root,backup); moved = true; }
                afterBackup?.Invoke();
                Directory.Move(stage,root);
                File.Delete(journalPath);
                return moved ? backup : "";
            }
            catch
            {
                if (moved && !Directory.Exists(root)) Directory.Move(backup,root);
                if (Directory.Exists(root) && File.Exists(journalPath)) File.Delete(journalPath);
                throw;
            }
            finally { if (Directory.Exists(stage)) Directory.Delete(stage,true); }
        }
    }
}
