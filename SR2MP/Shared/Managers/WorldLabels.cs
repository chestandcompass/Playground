using System.Text;
using SR2MP.Shared.Utils;

namespace SR2MP.Shared.Managers;

// Display metadata only. Never rename .sav files, native GameName or guest IDs.
internal static class WorldLabels
{
    internal const int MaxCharacters = 40;
    private static string PathFor(string root, string gameName)
    {
        if (string.IsNullOrWhiteSpace(gameName)) throw new ArgumentException(nameof(gameName));
        return Path.Combine(Path.GetFullPath(root), CoopFiles.Key(gameName) + ".txt");
    }

    internal static string Read(string root, string gameName, string fallback)
    {
        var path = PathFor(root, gameName);
        if (!File.Exists(path)) return fallback;
        try
        {
            if (new FileInfo(path).Length > 1024) return fallback;
            var name = PlayerNames.Normalize(File.ReadAllText(path, Encoding.UTF8), MaxCharacters);
            return name.Length == 0 ? fallback : name;
        }
        catch (IOException) { return fallback; }
        catch (UnauthorizedAccessException) { return fallback; }
    }

    internal static void Write(string root, string gameName, string label)
    {
        var name = PlayerNames.Normalize(label, MaxCharacters);
        if (name.Length == 0) throw new ArgumentException("A world name is required.");
        CoopFiles.WriteAtomic(PathFor(root, gameName), Encoding.UTF8.GetBytes(name));
    }
}
