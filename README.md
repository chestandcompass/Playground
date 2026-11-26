# Playground

An **unofficial, experimental multiplayer mod for Slime Rancher 2 1.3.0 on Windows x64**, with native game menus, separate multiplayer saves, and a local guest profile for each host world.

**LAN preview:** host world creation, saving/loading, and several native UI and synchronization paths have been checked in-game. Full host-and-guest validation on two computers is still pending. Internet connections through NAT are not currently supported.

## Download and install

Download **PlaygroundSetup.exe** from the [releases page](https://github.com/robertbrania-code/Playground/releases) and run it. The installer finds Steam libraries, installs the required components, and installs Playground. It also includes an offline copy of its release.

Steam detection checks user/system registry entries, game installation records, standard Steam folders, library configuration files, and the game's app manifest. If discovery cannot identify one valid installation, select SlimeRancher2.exe manually.

Playground uses **MelonLoader 0.7.3** and **Microsoft .NET 6**, included by the installer. It does not require a separate installation of Ranching Together, Starlight, DiscordRPC, or SharpOpenNat.

**Both computers must use the same Playground release.** Close the game before installing an update.

## Play together on LAN

1. On the host computer, choose **Play with a friend**, then create or open a multiplayer world.
2. On the other computer, choose **Join a friend → Find games on LAN**.
3. Select **Join: your world name**, enter your player name, choose your appearance, and continue.

Only open worlds with the same Playground version appear. The list refreshes automatically and removes expired hosts. A Steam invitation is not required for LAN discovery.

Alternatively, the host can copy an invitation code from the pause menu. The guest copies the received code and selects **Paste code and join**. Codes do not provide internet relay or NAT traversal.

Discovery uses **UDP 19190**. The game connection uses the host's port, **UDP 1919** by default. Allow the game to communicate on your private network. Guest Wi-Fi and router client isolation can prevent discovery.

The first guest connection prepares a separate local staging save, skips its prologue, and displays **Joining...**. It should not ask the guest to configure a new world. The staging save is reused for later connections.

## Connection status and disconnects

A small status label in the native HUD distinguishes singleplayer, connecting, connected multiplayer, and a host waiting for players. Opening a transport socket alone does not count as a completed join.

A disconnected guest cannot continue playing in the local staging world. Playground checkpoints an acknowledged guest's inventory, disables gameplay controls, and returns to the main menu with an explanation. A failed join also returns to the menu. An unresponsive host is treated as disconnected after ten seconds without host traffic.

Guest profiles use the game's native load-reference factory. New snapshots include their native reference tables and a checksum in one atomic file; legacy player snapshots remain readable. Only personal vacuum contents are restored, preserving the host's progression, currencies, health, position, and slot settings. Failed restoration preserves the existing file instead of replacing it with an empty inventory.

## Player name and appearance

Before hosting or joining, the native menu asks for **Your name**. Confirmed names are remembered locally and appear above players and on their markers. Names are limited to 24 characters; empty names are rejected. Name changes do not change player identity or save associations.

**Hair color** cycles through Original, Blond, Brown, Black, Ginger, Pink, and Purple. Original is the default. The main-menu character previews the selection. **Continue** remembers the name and color; **Back** cancels the edit. Hair customization is a Playground feature.

Original restores the game's exact hair material. Other colors use separate hair materials and locally generated textures, preserving skin, other players, and original game assets. Generated textures have bounded resolution and are cached per color.

Each player's native **Vacpack style** is synchronized separately. The guest's style is stored locally beside their profile for that host world and restored on reconnect. Appearance packets contain built-in style identifiers and a validated hair-color value, not downloaded materials or image files. Received appearance is cached until the corresponding player model exists.

## Multiplayer worlds and saves

Multiplayer saves are stored outside the game installation:

    %LOCALAPPDATA%\Playground\SlimeRancher2

Singleplayer saves stay in the game's original LocalLow location. Mod updates do not contain or replace saves. Guests keep a local profile associated with the host world's stable identifier. Shared progression and currencies come from the host; the guest profile restores personal vacuum contents.

In **My worlds**, choose a world to play, rename, or delete it. Display names are separate metadata: renaming does not change the native world identifier, save filenames, or guest-profile association.

### Deliberate singleplayer import

Choose **Copy a singleplayer world**, select a world, and confirm. Playground copies its latest valid snapshot into multiplayer storage. The original stays unchanged, and the two worlds progress independently. Importing the same world again is blocked to protect existing multiplayer progress. You can rename the imported multiplayer world afterward.

### Delete a multiplayer world

**My worlds → select a world → Delete multiplayer world** opens a separate confirmation showing the world's name, with **Cancel** selected initially. Deletion applies to that host world; it preserves the original singleplayer world and local guest profiles. Full in-game import and deletion workflows still need further validation.

## Host authority and synchronization

The host controls **Tarr, feral slimes, and the damage multiplier**. Guests receive these rules on joining, resynchronizing, and subsequent changes. Guest controls are read-only and marked **Controlled by the host**. Revision numbers prevent stale rule packets from restoring older values.

Graphics, audio, controls, subtitles, tutorials, and player identity remain personal settings.

Storage synchronization uses authoritative absolute snapshots with revisions. States received before a storage object is ready are retained for later application. Storage registration, slot definitions/order, and duplicate withdrawal notifications have been addressed.

New gadget placement from shared stock is decided by the host. Competing requests for the last item can create only one gadget and consume one item. Repeating an accepted request does not charge again; failed creation restores the reserved item. Moving an already picked-up gadget follows the native path.

These changes do not establish that every world interaction is synchronized correctly. Concurrent interactions and full two-computer play still require testing.

## Updates and uninstalling

Playground checks the public update manifest and shows updates in the native menu. The downloaded installer is checked before use, waits for the game to close, and preserves the previous DLL. Running the installer again also checks for the latest release. An older installer does not downgrade a newer installed version.

The installer verifies game-build compatibility and SHA-256 hashes. Its interface, status messages, and Playground-generated errors are in English.

The MelonLoader console is hidden by default, including when launching through Steam. Logs are still written to disk, and other loader settings are preserved.

To remove Playground, reopen the installer and choose **Uninstall**, or use **Playground — Slime Rancher 2** in Windows installed apps. Saves remain on disk. If other mods, plugins, or UserLibs need the loader, it remains active. Otherwise, Playground's recognized loader proxy is disabled. Previous files are archived in PlaygroundBackups; the shared Microsoft .NET runtime is not removed.

## Languages and accessibility

Public documentation, release notes, and the installer use English. In-game menus follow the language selected in Slime Rancher 2: English, German, French, Spanish, Portuguese, Russian, Japanese, Korean, or Simplified Chinese. A Polish catalog is retained for an available Polish locale; Playground does not add Polish localization to the base game. Unknown locales fall back to English. Player/world names are never translated.

Native menu and pause buttons resize to fit translated captions. Long labels wrap within the screen boundary. Font fallback uses an available Windows font for missing characters; font files are not redistributed. Text length and live locale changes still need broader visual checks.

## Validation status

Preview 18 adds **99 automated assertions** and native regression checks for legacy guest-profile loading, non-empty inventory round-tripping, corruption rejection, remote model/marker startup, and returning to the menu after disconnect. A player also confirmed that both the remote character and marker appeared during a brief two-computer connection. This is a limited check, not full multiplayer validation. Earlier cosmetic checks covered 23 Vacpack styles and independent hair materials.

These checks do **not** replace a full session on two computers. Reconnection, appearance delivery, world interactions, imports, deletion, and simultaneous actions need continued player testing. See [CHANGELOG.md](CHANGELOG.md) and individual [release notes](https://github.com/robertbrania-code/Playground/releases) for changes and validation limits.

## Build from source

Requirements: Windows, .NET SDK 8, your own legitimate Slime Rancher 2 1.3.0 installation, and MelonLoader 0.7.3. Launch the game with the loader once to generate local IL2CPP references.

    ./Prepare-References.ps1 -GameDirectory 'path-to-game'
    dotnet build SR2MP -c Release
    dotnet run --project tests/StorageTests -c Release
    ./Build-Installer.ps1 -GameDirectory 'path-to-game'

Game libraries are not distributed and must not be committed. For a new release, update release.json. The builder synchronizes version constants and generates the installer, DLL, and update manifest with hashes. Publish the manifest together with its matching release assets.

## License and attribution

**GPL-3.0**; see [LICENSE](LICENSE). Synchronization code was adapted from [Ranching Together / pyeight](https://github.com/pyeight/SlimeRancher2Multiplayer), by **Shark (py8) and contributors**. Upstream authorship is retained. Playground adds standalone game integration, native menus, isolated saves, an installer, and updates.

Playground is **not an official Monomi Park product**, is not affiliated with or endorsed by Monomi Park, and does not include game files.
