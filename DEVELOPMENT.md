# Developer guide

## Build requirements

- Windows and .NET SDK 8.
- Your own legitimate Slime Rancher 2 1.3.0 installation.
- MelonLoader 0.7.3. Launch the game with the loader once to generate local IL2CPP references.

```powershell
./Prepare-References.ps1 -GameDirectory 'path-to-game'
dotnet build SR2MP -c Release
dotnet run --project tests/StorageTests -c Release
./Build-Installer.ps1 -GameDirectory 'path-to-game'
```

Game libraries must remain local and must not be committed or distributed. The installer includes the loader and the required Microsoft .NET 6 runtime.

## Releases

Update release.json for a new binary release. The builder synchronizes version constants and generates the installer, DLL, and update manifest with hashes. Publish the manifest together with its matching release assets. Installation checks game-build compatibility and SHA-256 hashes.

Documentation-only changes do not require a new game build.

## Saves and guest profiles

Multiplayer data is stored under the user's LocalApplicationData directory in Playground/SlimeRancher2, separately from the game's original LocalLow saves. Display names are metadata separate from stable world identifiers.

Guest snapshots use native PlayerV12 serialization and native reference tables inside a checksummed atomic envelope. Legacy snapshots remain readable through the native load-reference factory. Restoration changes only personal vacuum contents; it preserves shared progression, currencies, health, position, slot definitions, and unlock state. Failed restoration must preserve the existing file.

Do not commit player profiles, save files, private logs, screenshots from personal sessions, or local regression fixtures.

## Networking and session state

LAN discovery uses UDP 19190; game connections use UDP 1919 by default. Invitation codes do not implement relay or NAT traversal.

The HUD distinguishes transport connection attempts from acknowledged sessions. Disconnected guests are blocked from interaction and returned to the menu. Host silence is measured with a monotonic clock; the timeout is ten seconds. Guest disconnect cleanup runs on the game thread, and queued client packets are ignored after disconnect.

World rules and storage snapshots are host-authoritative and use revisions. New gadget placement reserves stock on the host, deduplicates accepted requests, and restores stock after failed creation. Player cosmetics use validated identifiers and wait for remote models to become available.

## Validation

Preview 18 passed 99 automated assertions and eight native regression checks: legacy profile restoration, preservation of state outside inventory, self-contained reference tables, non-empty inventory round-tripping, corruption rejection, remote model initialization, compass/name initialization, and native menu return after disconnect.

A brief two-computer session confirmed remote character and marker visibility. This does not establish complete multiplayer correctness. Reconnection, concurrent interactions, appearance delivery, imports, deletion, and broader world synchronization require continued testing. Historical checks are recorded in CHANGELOG.md and individual release notes.

## Repository rename compatibility

Preview 19 reads the update feed through immutable GitHub repository ID 1472210540. Release downloads allow only the original and studio account names, with exact HTTPS repository and asset paths. SHA256 validation is retained. Generated manifests deliberately retain the original owner URL so earlier installers can accept the transition release through GitHub redirects. Preview 19 passed 110 automated assertions.


## World management

Save Files reuses the native six-slot list, summary panel and delete confirmation. Tabs switch the storage role without loading another role's options profile. Leaving the screen restores singleplayer storage. Selecting a world opens Playground actions only after the native landing menu finishes rebuilding.

Display names are sidecar metadata. Copies use the native GameV11 serializer, preserve existing reference-table ordering, normalize absent legacy tables to empty tables, and validate a complete native roundtrip before publication. Each copy receives a fresh native world identity and destination slot index. Guest inventories remain tied to their original host world.

WorldSlotFiles stages a complete destination directory and retains the previous directory as a sibling backup. A journal recovers interrupted directory swaps. Fingerprints reject stale confirmations, including changed profile settings; linked paths are refused. Source files are never rewritten.

The developer-only `--playground-menu-test --compass-profile worldmanager-smoke` configuration seeds a separate singleplayer test directory under Playground/tests before storage access. `slots-smoke` checks native copies in both directions, overwriting, fresh identities and sidecar renaming. It refuses to run against the original singleplayer directory. This does not replace a full gameplay test of every imported world.
