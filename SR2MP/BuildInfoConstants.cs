#pragma warning disable RCS1110 // Declare type inside namespace
// ReSharper disable once CheckNamespace

internal static class BuildInfo
{
    internal const string ID = "local.playground.coop";
    internal const string Name = "Playground";
    internal const string Description = "Local Ranching Together fork with isolated multiplayer saves";
    internal const string Author = "Shark (py8)";
    internal static readonly string[] CoAuthors = null;
    internal static readonly string[] Contributors = new[] { "AlchlcSystm, PinkTarr" };
    // MelonVersion is shown by ML on startup
    // Version is shown by Starlight
    // Version automatically gets a -dev at the end if SR2MP is compiled by GitHub Action
    internal const string MelonVersion = "0.1.0";
    internal const string Version = "0.1.0"; // Auto-Dev_Do_not_remove
    internal const int UpdateBuild = 2;
    internal const string Discord = "https://discord.com/invite/a7wfBw5feU";
    internal const string SourceCode = "https://github.com/robertbrania-code/Playground";
    internal const string Nexus = "https://www.nexusmods.com/slimerancher2/mods/118";
    internal const bool UsePrism = false;
    internal const string MinimumStarlightVersion = ""; // e.g "3.6.3", the min required SR2 version. No beta or alpha versions
    internal const string MinimumGameVersion = "1.3.0"; // e.g 1.1.0 or something similar (optional)
    internal const string ExactGameVersion = "1.3.0"; // e.g 1.1.0 or something similar (optional)
}
