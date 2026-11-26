using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;

namespace PlaygroundSetup
{
    [DataContract]
    public sealed class ReleaseInfo
    {
        [DataMember(Name="schema")] public int Schema;
        [DataMember(Name="version")] public string Version;
        [DataMember(Name="build")] public int Build;
        [DataMember(Name="gameVersion")] public string GameVersion;
        [DataMember(Name="dllUrl")] public string DllUrl;
        [DataMember(Name="sha256")] public string Sha256;
        [DataMember(Name="gameAssemblySha256")] public string GameAssemblySha256;
    }

    static class Program
    {
        internal const string Repository = "https://github.com/robertbrania-code/Playground";
        internal const string Feed = "https://raw.githubusercontent.com/robertbrania-code/Playground/main/update.json";

        [STAThread]
        static int Main(string[] args)
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            try
            {
                string game = Argument(args, "--game");
                string wait = Argument(args, "--wait-pid");
                int pid;
                if (Int32.TryParse(wait, out pid))
                {
                    try { Process.GetProcessById(pid).WaitForExit(); }
                    catch (ArgumentException) { }
                }
                if (args.Contains("--uninstall") || args.Contains("--test-uninstall"))
                {
                    Maintenance.Uninstall(game, args.Contains("--test-uninstall"));
                    if (!args.Contains("--test-uninstall")) MessageBox.Show("Playground has been uninstalled. Your saves have been preserved.", "Playground");
                    return 0;
                }
                if (args.Contains("--verify"))
                {
                    VerifyGame(game);
                    string payload = ExtractPayload();
                    VerifyPayload(payload);
                    File.WriteAllText(Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "verify-result.txt"), "PASS payload, hashes and game path; no game files modified.");
                    return 0;
                }
                if (args.Contains("--test-install"))
                {
                    Install(game, delegate(string message) { }, true);
                    return 0;
                }
                if (args.Contains("--apply-update"))
                {
                    Install(game, delegate(string message) { }, false);
                    File.WriteAllText(Path.Combine(game, "Playground.update-result.txt"), "OK");
                    return 0;
                }
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                bool render = args.Contains("--render");
                using (SetupWindow form = new SetupWindow(game, render))
                {
                    if (render)
                    {
                        form.Show();
                        Application.DoEvents();
                        using (Bitmap bitmap = new Bitmap(form.Width, form.Height))
                        {
                            form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
                            bitmap.Save(Argument(args, "--render"));
                        }
                        form.Close();
                    }
                    else Application.Run(form);
                }
                return 0;
            }
            catch (Exception ex)
            {
                if (args.Contains("--apply-update"))
                    File.WriteAllText(Path.Combine(Argument(args, "--game"), "Playground.update-result.txt"), "ERROR: " + ex.Message);
                else if (args.Contains("--verify") || args.Contains("--test-install") || args.Contains("--test-uninstall"))
                    File.WriteAllText(Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "verify-error.txt"), ex.ToString());
                else MessageBox.Show(ex.Message, "Playground", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return 1;
            }
        }

        static string Argument(string[] args, string name)
        {
            int index = Array.IndexOf(args, name);
            return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
        }

        internal static string[] FindGames()
        {
            var roots = new List<string>();
            var installed = new List<string>();
            foreach (RegistryHive hive in new[] { RegistryHive.CurrentUser, RegistryHive.LocalMachine })
            foreach (RegistryView view in new[] { RegistryView.Registry32, RegistryView.Registry64 })
            {
                try
                {
                    using (RegistryKey registry = RegistryKey.OpenBaseKey(hive, view))
                    {
                        using (RegistryKey key = registry.OpenSubKey(@"Software\Valve\Steam"))
                        {
                            if (key != null)
                            {
                                roots.Add(key.GetValue("SteamPath") as string);
                                roots.Add(key.GetValue("InstallPath") as string);
                                string executable = key.GetValue("SteamExe") as string;
                                if (!String.IsNullOrWhiteSpace(executable)) roots.Add(Path.GetDirectoryName(executable));
                            }
                        }
                        using (RegistryKey key = registry.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\Steam App 1657630"))
                            if (key != null) installed.Add(key.GetValue("InstallLocation") as string);
                    }
                }
                catch (System.Security.SecurityException) { }
                catch (UnauthorizedAccessException) { }
            }
            roots.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam"));
            roots.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Steam"));
            return GameDiscovery.Find(roots, installed);
        }

        internal static void VerifyGame(string directory)
        {
            if (String.IsNullOrWhiteSpace(directory) || !File.Exists(Path.Combine(directory, "SlimeRancher2.exe")) ||
                !File.Exists(Path.Combine(directory, "GameAssembly.dll")) || !Directory.Exists(Path.Combine(directory, "SlimeRancher2_Data")))
                throw new InvalidOperationException("Select the folder containing your Slime Rancher 2 installation.");
        }

        static string Hash(string path)
        {
            using (var algorithm = SHA256.Create())
            using (var stream = File.OpenRead(path))
                return BitConverter.ToString(algorithm.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        }

        static string ExtractPayload()
        {
            string root = Path.Combine(Path.GetTempPath(), "PlaygroundSetup", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            string zip = Path.Combine(root, "payload.zip");
            using (Stream source = Assembly.GetExecutingAssembly().GetManifestResourceStream("Payload"))
            using (Stream destination = File.Create(zip)) source.CopyTo(destination);
            if (Hash(zip) != PayloadInfo.Sha256) throw new InvalidDataException("The installer is damaged. Download it again from the Playground repository.");
            ExtractSafe(zip, root);
            return root;
        }

        static void ExtractSafe(string zip, string root)
        {
            string prefix = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            using (ZipArchive archive = ZipFile.OpenRead(zip))
            {
                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    string target = Path.GetFullPath(Path.Combine(root, entry.FullName));
                    if (!target.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Invalid directory in the installation package.");
                    if (entry.FullName.EndsWith("/") || entry.FullName.EndsWith(@"\")) { Directory.CreateDirectory(target); continue; }
                    Directory.CreateDirectory(Path.GetDirectoryName(target));
                    entry.ExtractToFile(target, true);
                }
            }
        }

        static ReleaseInfo ReadRelease(byte[] bytes)
        {
            using (var stream = new MemoryStream(bytes))
                return (ReleaseInfo)new DataContractJsonSerializer(typeof(ReleaseInfo)).ReadObject(stream);
        }

        static void ValidateRelease(ReleaseInfo release)
        {
            Version parsed;
            if (release == null || release.Schema != 1 || !Version.TryParse(release.Version, out parsed) || release.Build < 1 ||
                release.GameVersion != "1.3.0" || !Regex.IsMatch(release.Sha256 ?? "", "^[a-fA-F0-9]{64}$") ||
                !(release.DllUrl ?? "").StartsWith(Repository + "/releases/download/", StringComparison.Ordinal))
                throw new InvalidDataException("This Playground release is not compatible with this installer or game version.");
        }

        static bool HasRuntime()
        {
            string directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), @"dotnet\shared\Microsoft.NETCore.App");
            return Directory.Exists(directory) && Directory.GetDirectories(directory, "6.*").Any(delegate(string path)
            { Version version; return Version.TryParse(Path.GetFileName(path), out version) && version >= new Version(6, 0, 36); });
        }

        static void VerifyPayload(string payload)
        {
            var release = ReadRelease(File.ReadAllBytes(Path.Combine(payload, "update.json")));
            ValidateRelease(release);
            if (Hash(Path.Combine(payload, @"Mods\Playground.dll")) != release.Sha256.ToLowerInvariant())
                throw new InvalidDataException("The Playground checksum is invalid.");
        }

        internal static void Install(string game, Action<string> progress, bool offline)
        {
            VerifyGame(game);
            if (Process.GetProcessesByName("SlimeRancher2").Length > 0) throw new InvalidOperationException("Close Slime Rancher 2, then run the installer again.");
            progress("Preparing Playground...");
            string payload = ExtractPayload();
            VerifyPayload(payload);
            ReleaseInfo release = ReadRelease(File.ReadAllBytes(Path.Combine(payload, "update.json")));
            string mod = Path.Combine(payload, @"Mods\Playground.dll");
            if (!offline)
            {
                try
                {
                    progress("Checking for the latest release...");
                    using (var client = new TimedWebClient())
                    {
                        client.Headers[HttpRequestHeader.UserAgent] = "Playground-Setup/0.1.0";
                        ReleaseInfo remote = ReadRelease(client.DownloadData(Feed));
                        ValidateRelease(remote);
                        if (new Version(remote.Version) > new Version(release.Version) ||
                            (remote.Version == release.Version && remote.Build >= release.Build))
                        {
                            string downloaded = Path.Combine(payload, "downloaded.dll");
                            client.DownloadFile(remote.DllUrl, downloaded);
                            if (new FileInfo(downloaded).Length > 8388608 || Hash(downloaded) != remote.Sha256.ToLowerInvariant())
                                throw new InvalidDataException("The downloaded update is damaged.");
                            mod = downloaded;
                            release = remote;
                        }
                    }
                }
                catch (WebException) { progress("Using the release bundled with the installer..."); }
            }
            if (!Hash(Path.Combine(game, "GameAssembly.dll")).Equals(release.GameAssemblySha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("This Playground release requires Slime Rancher 2 version 1.3.0. The installed game has a different build.");
            string previousMetadata = Path.Combine(game, "Playground.version.json");
            if (File.Exists(previousMetadata))
            {
                ReleaseInfo previous = ReadRelease(File.ReadAllBytes(previousMetadata));
                if (new Version(previous.Version) > new Version(release.Version) ||
                    (previous.Version == release.Version && previous.Build > release.Build))
                { progress("A newer version of Playground is already installed."); return; }
            }
            if (!HasRuntime())
            {
                progress("Installing the required Microsoft .NET 6 runtime...");
                var info = new ProcessStartInfo(Path.Combine(payload, @"Dependencies\dotnet-runtime-6.0.36-win-x64.exe"), "/install /quiet /norestart");
                info.UseShellExecute = true;
                using (Process process = Process.Start(info)) { process.WaitForExit(); if (process.ExitCode != 0 && process.ExitCode != 3010) throw new InvalidOperationException("Microsoft .NET installation did not complete."); }
                if (!HasRuntime()) throw new InvalidOperationException("Restart your computer after installing Microsoft .NET, then install Playground again.");
            }
            if (Process.GetProcessesByName("SlimeRancher2").Length > 0) throw new InvalidOperationException("The game has started. Close it before installing the update.");
            string existingVersionDll = Path.Combine(game, "version.dll");
            if (File.Exists(existingVersionDll) && !Directory.Exists(Path.Combine(game, "MelonLoader")))
                throw new InvalidOperationException("Another mod is using version.dll in this game installation. Playground will not replace that file.");
            string backup = Path.Combine(game, "PlaygroundBackups", DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-ffff"));
            string mods = Path.Combine(game, "Mods");
            Directory.CreateDirectory(mods);
            foreach (string legacy in new[] { "Starlight.dll", "SR2MP.dll" })
            {
                string old = Path.Combine(mods, legacy);
                if (File.Exists(old)) { Directory.CreateDirectory(backup); File.Move(old, Path.Combine(backup, legacy)); }
            }
            string existingLoader = Path.Combine(game, @"MelonLoader\net6\MelonLoader.dll");
            bool needsLoader = !File.Exists(existingLoader) || !File.Exists(existingVersionDll);
            if (!needsLoader) needsLoader = AssemblyName.GetAssemblyName(existingLoader).Version < new Version(0, 7, 3, 0);
            if (needsLoader)
            {
                if (File.Exists(Path.Combine(game, "version.dll")) && !Directory.Exists(Path.Combine(game, "MelonLoader")))
                    throw new InvalidOperationException("Another mod is using version.dll in this game installation. Playground will not replace that file.");
                progress("Installing the mod loader...");
                string loader = Path.Combine(payload, "Loader");
                Directory.CreateDirectory(loader);
                ExtractSafe(Path.Combine(payload, @"Dependencies\MelonLoader-0.7.3-x64.zip"), loader);
                foreach (string source in Directory.GetFiles(loader, "*", SearchOption.AllDirectories))
                {
                    if (Path.GetFileName(source).Equals("version.dll", StringComparison.OrdinalIgnoreCase)) continue;
                    string target = Path.Combine(game, source.Substring(loader.Length + 1));
                    Directory.CreateDirectory(Path.GetDirectoryName(target));
                    if (File.Exists(target))
                    {
                        string old = Path.Combine(backup, "Loader", source.Substring(loader.Length + 1));
                        Directory.CreateDirectory(Path.GetDirectoryName(old)); File.Copy(target, old, true);
                    }
                    File.Copy(source, target, true);
                }
                if (File.Exists(existingVersionDll)) { Directory.CreateDirectory(backup); File.Copy(existingVersionDll, Path.Combine(backup, "version.dll"), true); }
                File.Copy(Path.Combine(loader, "version.dll"), existingVersionDll, true);
            }
            progress("Installing Playground...");
            Maintenance.HideConsole(game);
            string destination = Path.Combine(mods, "Playground.dll");
            string pending = destination + ".pending";
            File.Copy(mod, pending, true);
            if (File.Exists(destination))
            {
                Directory.CreateDirectory(backup);
                File.Replace(pending, destination, Path.Combine(backup, "Playground.dll"));
            }
            else File.Move(pending, destination);
            string metadata = Path.Combine(game, "Playground.version.json");
            using (var output = File.Create(metadata)) new DataContractJsonSerializer(typeof(ReleaseInfo)).WriteObject(output, release);
            string self = Assembly.GetExecutingAssembly().Location;
            string installedSetup = Path.Combine(game, "PlaygroundSetup.exe");
            if (!Path.GetFullPath(self).Equals(Path.GetFullPath(installedSetup), StringComparison.OrdinalIgnoreCase)) File.Copy(self, installedSetup, true);
            if (!offline) Maintenance.Register(game, release.Version + " (test " + release.Build + ")");
            progress("Done! Playground is installed.");
        }
    }

    sealed class TimedWebClient : WebClient
    {
        protected override WebRequest GetWebRequest(Uri address)
        {
            WebRequest request = base.GetWebRequest(address);
            request.Timeout = 15000;
            return request;
        }
    }

    sealed class SetupWindow : Form
    {
        readonly Label status = new Label();
        readonly ProgressBar progress = new ProgressBar();
        readonly Button action = new Button();
        readonly Button remove = new Button();
        readonly string requested;
        readonly bool preview;
        string selected;
        bool installed;
        bool managing;

        internal SetupWindow(string game, bool render)
        {
            requested = game; preview = render;
            Text = "Playground"; ClientSize = new Size(580, 280); StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; BackColor = Color.FromArgb(245, 248, 250);
            var title = new Label { Text = "Playground", Font = new Font("Segoe UI", 28, FontStyle.Bold), ForeColor = Color.FromArgb(20, 165, 188), AutoSize = true, Location = new Point(28, 22) };
            var subtitle = new Label { Text = "An adventure together in Slime Rancher 2", Font = new Font("Segoe UI", 12), AutoSize = true, Location = new Point(32, 82) };
            status.Text = "Finding the game and preparing installation..."; status.Font = new Font("Segoe UI", 11); status.SetBounds(32, 126, 510, 44);
            progress.SetBounds(32, 181, 510, 12); progress.Style = ProgressBarStyle.Marquee;
            action.Text = "Please wait..."; action.Enabled = false; action.SetBounds(333, 218, 210, 38); action.FlatStyle = FlatStyle.Flat;
            action.BackColor = Color.FromArgb(232, 77, 142); action.ForeColor = Color.White; action.Font = new Font("Segoe UI", 11, FontStyle.Bold);
            remove.Text = "Uninstall"; remove.SetBounds(32, 218, 170, 38); remove.Visible = false;
            Controls.AddRange(new Control[] { title, subtitle, status, progress, action, remove });
            action.Click += async delegate { if (managing) { managing = false; await PerformInstall(); return; } if (installed && selected != null) Process.Start("steam://rungameid/1657630"); Close(); };
            remove.Click += async delegate
            {
                action.Enabled = false; remove.Enabled = false;
                try { await Task.Run(delegate { Maintenance.Uninstall(selected, false); }); status.Text = "Playground removed. Your saves have been preserved."; installed = false; managing = false; selected = null; action.Text = "Close"; remove.Visible = false; }
                catch (Exception ex) { status.Text = ex.Message; }
                action.Enabled = true; remove.Enabled = true;
            };
            Shown += async delegate { if (!preview) await BeginInstall(); };
        }

        async Task BeginInstall()
        {
            try
            {
                selected = requested;
                if (String.IsNullOrWhiteSpace(selected))
                {
                    string[] games = Program.FindGames();
                    if (games.Length == 1) selected = games[0];
                    else using (var dialog = new OpenFileDialog { Title = "Select SlimeRancher2.exe", Filter = "Slime Rancher 2|SlimeRancher2.exe" })
                    { if (dialog.ShowDialog(this) != DialogResult.OK) { Close(); return; } selected = Path.GetDirectoryName(dialog.FileName); }
                }
                if (File.Exists(Path.Combine(selected, @"Mods\Playground.dll")))
                {
                    managing = true; status.Text = "Playground is installed. Choose Update or Uninstall.";
                    progress.Style = ProgressBarStyle.Continuous; action.Text = "Update"; action.Enabled = true; remove.Visible = true; return;
                }
                await PerformInstall();
            }
            catch (Exception ex) { ShowError(ex); }
        }

        async Task PerformInstall()
        {
            try
            {
                action.Enabled = false; remove.Enabled = false; progress.Style = ProgressBarStyle.Marquee;
                await Task.Run(delegate { Program.Install(selected, delegate(string message) { BeginInvoke((Action)delegate { status.Text = message; }); }, false); });
                progress.Style = ProgressBarStyle.Continuous; progress.Value = 100;
                installed = true; action.Text = "Launch game"; action.Enabled = true; remove.Visible = true; remove.Enabled = true;
            }
            catch (Exception ex) { ShowError(ex); }
        }

        void ShowError(Exception ex)
        {
            status.Text = ex.Message; progress.Style = ProgressBarStyle.Continuous;
            selected = null; managing = false; action.Text = "Close"; action.Enabled = true;
        }
    }
}
