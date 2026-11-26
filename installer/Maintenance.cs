using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace PlaygroundSetup
{
    [DataContract]
    sealed class InstallState
    {
        [DataMember] public string OriginalConsole;
        [DataMember] public string ProxyHash;
    }

    static class Maintenance
    {
        static string StatePath(string game) { return Path.Combine(game, "Playground.install-state.json"); }
        static string ConfigPath(string game) { return Path.Combine(game, @"UserData\Loader.cfg"); }

        static InstallState ReadState(string game)
        {
            string path = StatePath(game);
            if (!File.Exists(path)) return new InstallState();
            using (var input = File.OpenRead(path)) return (InstallState)new DataContractJsonSerializer(typeof(InstallState)).ReadObject(input);
        }

        internal static void HideConsole(string game)
        {
            string config = ConfigPath(game);
            bool existingState = File.Exists(StatePath(game));
            InstallState state = ReadState(game);
            string text = File.Exists(config) ? File.ReadAllText(config) : "";
            if (!existingState) state.OriginalConsole = ReadConsole(text);
            string proxy = Path.Combine(game, "version.dll");
            if (File.Exists(proxy) && Hash(proxy) == PayloadInfo.ProxySha256) state.ProxyHash = PayloadInfo.ProxySha256;
            Directory.CreateDirectory(Path.GetDirectoryName(config));
            File.WriteAllText(config, SetConsole(text, "true"), new UTF8Encoding(false));
            using (var output = File.Create(StatePath(game))) new DataContractJsonSerializer(typeof(InstallState)).WriteObject(output, state);
        }

        static string ReadConsole(string text)
        {
            bool console = false;
            foreach (string line in text.Replace("\r\n", "\n").Split('\n'))
            {
                if (Regex.IsMatch(line, @"^\s*\[")) console = Regex.IsMatch(line, @"^\s*\[console\]\s*(#.*)?$");
                if (console)
                {
                    Match match = Regex.Match(line, @"^\s*hide_console\s*=\s*(true|false)\b");
                    if (match.Success) return match.Groups[1].Value;
                }
            }
            return null;
        }

        static string SetConsole(string text, string value)
        {
            var lines = new List<string>(text.Replace("\r\n", "\n").Split('\n'));
            int section = -1, end = lines.Count, key = -1;
            for (int i = 0; i < lines.Count; i++)
            {
                if (Regex.IsMatch(lines[i], @"^\s*\[console\]\s*(#.*)?$")) { section = i; continue; }
                if (section >= 0 && Regex.IsMatch(lines[i], @"^\s*\[")) { end = i; break; }
                if (section >= 0 && Regex.IsMatch(lines[i], @"^\s*hide_console\s*=")) key = i;
            }
            if (key >= 0)
            {
                if (value == null) lines.RemoveAt(key);
                else lines[key] = Regex.Replace(lines[key], @"^(\s*hide_console\s*=\s*)(true|false)\b", delegate(Match m) { return m.Groups[1].Value + value; });
            }
            else if (value != null)
            {
                if (section < 0) { lines.Add("[console]"); lines.Add("hide_console = " + value); }
                else lines.Insert(end, "hide_console = " + value);
            }
            return String.Join("\n", lines).TrimEnd() + "\n";
        }

        static string Hash(string path)
        {
            using (var algorithm = SHA256.Create()) using (var input = File.OpenRead(path))
                return BitConverter.ToString(algorithm.ComputeHash(input)).Replace("-", "").ToLowerInvariant();
        }

        static string RegistryPath(string game)
        {
            string id;
            using (var algorithm = SHA256.Create()) id = BitConverter.ToString(algorithm.ComputeHash(Encoding.UTF8.GetBytes(Path.GetFullPath(game).TrimEnd('\\').ToUpperInvariant()))).Replace("-", "").Substring(0, 16);
            return @"Software\Microsoft\Windows\CurrentVersion\Uninstall\Playground-" + id;
        }

        internal static void Register(string game, string version)
        {
            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(RegistryPath(game)))
            {
                key.SetValue("DisplayName", "Playground — Slime Rancher 2");
                key.SetValue("DisplayVersion", version);
                key.SetValue("Publisher", "Playground");
                key.SetValue("InstallLocation", game);
                key.SetValue("DisplayIcon", Path.Combine(game, "SlimeRancher2.exe"));
                key.SetValue("UninstallString", "\"" + Path.Combine(game, "PlaygroundSetup.exe") + "\" --uninstall --game \"" + game + "\"");
                key.SetValue("NoModify", 1, RegistryValueKind.DWord);
                key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
            }
        }

        internal static void Uninstall(string game, bool test)
        {
            Program.VerifyGame(game);
            if (Process.GetProcessesByName("SlimeRancher2").Length > 0) throw new InvalidOperationException("Close Slime Rancher 2 przed odinstalowaniem Playground.");
            InstallState state = ReadState(game);
            string backup = Path.Combine(game, "PlaygroundBackups", "uninstall-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-ffff"));
            Directory.CreateDirectory(backup);
            foreach (string relative in new[] { @"Mods\Playground.dll", "Playground.version.json", "Playground.update-result.txt", "Playground.install-state.json" })
            {
                string source = Path.Combine(game, relative);
                if (!File.Exists(source)) continue;
                string target = Path.Combine(backup, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(target));
                File.Move(source, target);
            }
            string config = ConfigPath(game);
            if (File.Exists(config))
            {
                string text = File.ReadAllText(config);
                if (ReadConsole(text) == "true") File.WriteAllText(config, SetConsole(text, state.OriginalConsole), new UTF8Encoding(false));
            }
            bool otherMods = new[] { "Mods", "Plugins", "UserLibs" }.Any(delegate(string folder)
            { string path = Path.Combine(game, folder); return Directory.Exists(path) && Directory.GetFiles(path, "*.dll", SearchOption.AllDirectories).Length > 0; });
            string proxy = Path.Combine(game, "version.dll");
            if (!otherMods && File.Exists(proxy) && state.ProxyHash == PayloadInfo.ProxySha256 && Hash(proxy) == state.ProxyHash)
                File.Move(proxy, Path.Combine(backup, "version.dll"));
            if (!test) Registry.CurrentUser.DeleteSubKeyTree(RegistryPath(game), false);
            // Saved games, other mods, Microsoft .NET and loader caches are intentionally preserved.
        }
    }
}
