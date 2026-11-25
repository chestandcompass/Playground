using System.Reflection;
using MelonLoader;

[assembly: AssemblyTitle(BuildInfo.Name)]
[assembly: AssemblyDescription(BuildInfo.Description)]
[assembly: AssemblyCompany(BuildInfo.Author)]
[assembly: AssemblyProduct(BuildInfo.Name)]
[assembly: AssemblyCopyright($"Created by {BuildInfo.Author}")]
[assembly: AssemblyTrademark(BuildInfo.Author)]
[assembly: AssemblyVersion(BuildInfo.MelonVersion)]
[assembly: AssemblyFileVersion(BuildInfo.MelonVersion)]


[assembly: MelonInfo(typeof(SR2MP.Main), "Playground", BuildInfo.Version, "Playground contributors; original multiplayer by Shark (py8)")]
[assembly: MelonGame("MonomiPark", "SlimeRancher2")]
[assembly: HarmonyDontPatchAll]

[assembly: VerifyLoaderVersion(0, 7, 3, true)]
[assembly: MelonColor(255, 77, 149, 203)]
