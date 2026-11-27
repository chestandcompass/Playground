# Playground

An **unofficial, experimental multiplayer mod for Slime Rancher 2**, made for playing together on a local network, with an experimental online option.

Explore with a friend using the game's familiar menus, separate multiplayer worlds, and your own player name and appearance.

**Requires Slime Rancher 2 1.3.0 on Windows x64. Install the same mod version on both computers.**

## Download and install

Download **PlaygroundSetup.exe** from the [releases page](https://github.com/chestandcompass/Playground/releases) and run it.

The installer finds the game and installs the required components. If it cannot find your installation, select SlimeRancher2.exe when prompted.

Install **the same Playground version on both computers**.

## Play with a friend

1. The host chooses **Playground**, then creates or opens a multiplayer world.
2. The other player chooses **Join a friend → Find games on LAN**.
3. Select the host's world, enter your name, choose your hair color, and continue.

You can also join a LAN game using an invitation code copied by the host from the pause menu. Copy the received code and choose **Paste code and join**.

### Play over the internet (experimental, two players)

1. The host opens a multiplayer world, pauses, and chooses **Open online lobby**.
2. Once the lobby opens, choose **Copy online code** and share it with your friend.
3. The guest copies the code and chooses **Join a friend → Paste code and join**.

Both players need internet access. The mod includes the online connection; no VPN or router port forwarding is required. Online codes use our shared free relay, which has limited daily capacity. LAN games do not use that capacity.

Each online lobby lasts up to four hours. If either connection ends, ask the host to open a new lobby and share its new code. Your saved progress stays associated with the same world. Online play is still experimental and has not yet been verified in a full gameplay session between two separate networks.

If a world does not appear, check that both players have the same mod version, the host has opened the world, and both computers are on the same network. Allow the game through the firewall on your private network.

## Make it your adventure

- Choose a player name that other players can see.
- Pick Original, Blond, Brown, Black, Ginger, Pink, or Purple hair, with a preview in the menu.
- Keep your own Vacpack style.
- Create and name multiplayer worlds, or rename them later.
- Let the host choose world rules such as Tarr and feral slimes.

Your confirmed name and hair color are remembered for next time. **Continue** saves your selection; **Back** cancels it.

The on-screen multiplayer status shows whether you are connecting, connected, or hosting while waiting for friends. If the connection ends, the guest returns to the menu and can rejoin. Ordinary singleplayer games do not show this status label.

## Your worlds and progress

Multiplayer worlds are separate from your singleplayer saves. A guest's personal inventory is remembered for each host world. Renaming a multiplayer world does not reset that association.

Open **Save Files** and choose the **Singleplayer** or **Multiplayer** tab. Each tab has six separate slots. Select a world to play, rename it, or copy it to a slot in the other tab. Copying and overwriting require confirmation; the source stays unchanged and a backup of the destination is kept.

World names are stored separately from game saves. Renaming a world keeps its identity and guest progress. A copied world gets a new identity. Use the game's delete button in Save Files to remove the selected world after confirmation. **My worlds** opens the Multiplayer tab.

## Updates and uninstalling

When an update is available, Playground offers it in the game menu. You can also run the installer again. Close the game before updating, and update both computers to the same release.

To uninstall, reopen the installer and choose **Uninstall**, or use **Playground — Slime Rancher 2** in Windows installed apps. Your saves are kept.

See the [release history](CHANGELOG.md) for updates and the [developer guide](DEVELOPMENT.md) for source-code documentation.

## License and attribution

Playground is licensed under **GPL-3.0**; see [LICENSE](LICENSE). Its synchronization code was adapted from [Ranching Together / pyeight](https://github.com/pyeight/SlimeRancher2Multiplayer), by **Shark (py8) and contributors**.

Playground is not an official Monomi Park product and is not affiliated with or endorsed by Monomi Park. It does not include game files.
