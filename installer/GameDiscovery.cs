using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace PlaygroundSetup
{
    // Keep filesystem discovery independent from the registry for repeatable tests.
    internal static class GameDiscovery
    {
        internal static string[] Find(IEnumerable<string> steamRoots, IEnumerable<string> installedLocations)
        {
            var libraries = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string root in steamRoots)
            {
                string normalized = Normalize(root);
                if (normalized == null) continue;
                libraries.Add(normalized);
                foreach (string relative in new[] { @"steamapps\libraryfolders.vdf", @"config\libraryfolders.vdf" })
                {
                    string contents = Read(Path.Combine(normalized, relative));
                    // Modern path entries and the older numbered string entries.
                    foreach (Match match in Regex.Matches(contents, "\"(?:path|[0-9]+)\"\\s+\"([^\"]+)\""))
                    {
                        string library = Normalize(match.Groups[1].Value.Replace(@"\\", @"\"));
                        if (library != null) libraries.Add(library);
                    }
                }
            }
            var candidates = new List<string>(installedLocations.Where(p => !String.IsNullOrWhiteSpace(p)));
            foreach (string library in libraries)
            {
                string apps = Path.Combine(library, "steamapps");
                string manifest = Read(Path.Combine(apps, "appmanifest_1657630.acf"));
                Match match = Regex.Match(manifest, "\"installdir\"\\s+\"([^\"]+)\"", RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    string folder = match.Groups[1].Value;
                    if (folder != "." && folder != ".." && folder.IndexOfAny(new[] { '\\', '/', ':' }) < 0)
                        candidates.Add(Path.Combine(apps, "common", folder));
                }
                candidates.Add(Path.Combine(apps, "common", "Slime Rancher 2"));
            }
            return candidates.Select(Normalize).Where(p => p != null && IsGame(p))
                .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        }

        private static bool IsGame(string path)
        {
            return File.Exists(Path.Combine(path, "SlimeRancher2.exe"))
                && File.Exists(Path.Combine(path, "GameAssembly.dll"))
                && Directory.Exists(Path.Combine(path, "SlimeRancher2_Data"));
        }

        private static string Normalize(string path)
        {
            if (String.IsNullOrWhiteSpace(path)) return null;
            try { return Path.IsPathRooted(path) ? Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) : null; }
            catch (ArgumentException) { return null; }
            catch (NotSupportedException) { return null; }
            catch (PathTooLongException) { return null; }
        }

        private static string Read(string path)
        {
            try { return File.Exists(path) ? File.ReadAllText(path) : ""; }
            catch (IOException) { return ""; }
            catch (UnauthorizedAccessException) { return ""; }
        }
    }
}
