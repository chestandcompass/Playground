# Changelog

All releases are experimental LAN previews for Slime Rancher 2 1.3.0 on Windows x64. Full host-and-guest validation on two computers remains pending.

## Preview 21

- Rename the main-menu entry to Playground in every language.

## Preview 20

- Manage worlds in the game's Save Files screen, with separate Singleplayer and Multiplayer tabs and six slots each.
- Rename worlds without changing their save identity or guest progress.
- Copy worlds in either direction, choose the destination slot, and confirm overwrites. Keep the source and a backup of the destination.
- Keep the multiplayer status label hidden during ordinary singleplayer games.

## Preview 19

- Keep automatic updates working through the studio account rename.
- Preserve compatibility with existing installers.

## Preview 18

- Fix the guest-profile load translator that could disconnect returning guests before player models and markers were created.
- Store reference tables and checksums with new guest snapshots, retain legacy reading, and restore only personal vacuum contents.
- Display explicit singleplayer, connecting, connected, and host-waiting states in the HUD.
- Stop disconnected guests from continuing offline: checkpoint inventory, disable interaction, and return to the menu with an explanation.
- Detect silent hosts and route guest disconnect cleanup through the game thread. Ignore queued client packets after disconnect.
- Assign remote player identity before activating the model.
- Validation: 99 automated assertions and eight native regression checks passed, including the reported legacy profile and native return to menu. A brief two-computer check confirmed remote character/marker visibility; broader multiplayer testing remains pending.

## Preview 17

- Make public documentation and all Playground installer labels, progress messages, and generated errors English.
- Add this English release history and translate the two original Polish release descriptions.
- Preserve in-game localization and existing gameplay behavior.

<details>
<summary>Older releases</summary>

## Preview 16

- Add remembered hair-color selection beside the player name, with a main-menu character preview.
- Synchronize validated hair colors, retaining original hair as the default and isolating materials from skin and other players.
- Validation: 90 automated assertions and 40 in-game cosmetic checks passed in one instance. Menu typing and color previews were inspected; two-PC delivery remains unverified.

## Preview 15

- Synchronize each player's native Vacpack style independently, including late-created models.
- Remember guest styles locally per host world and restore them on reconnect.
- Validation: 87 automated assertions and 29 cosmetic checks passed in one instance, including 23 native styles.

## Preview 14

- Repair storage registration, delayed binding, slot definitions/order, and duplicate withdrawal notifications.
- Reconcile guest storage using authoritative snapshots with revisions.
- Arbitrate new gadget placement on the host, deduplicate retries, and restore stock after failed creation.
- Validation: 87 automated assertions and 12 native checks passed: nine storage checks and three gadget-placement checks.

## Preview 13

- Make Tarr, feral-slime, and damage-multiplier rules host-controlled and read-only for guests.
- Synchronize rule changes with revision checks while preserving personal settings.
- Validation: 79 automated assertions and seven native checks passed in one instance with a simulated guest role.

## Preview 12

- Fix Continue and Back after typing a name without repeatedly stealing native button focus.
- Add multiplayer world display names stored separately from stable world identifiers and guest profiles.
- Check pointer confirmation, cancellation, Enter, and remembered-name prefill in-game for that release.

## Preview 11

- Add a native editable player-name field before hosting or joining.
- Remember confirmed names, limit length, escape unsafe markup, and support missing characters through a system-font fallback.

## Preview 10

- Prepare guest staging saves automatically instead of showing world-creation settings when joining.
- Skip the guest staging prologue behind Joining and reuse the staging save later.

## Preview 9

- Add LAN discovery and a native active-world browser, with version filtering and expired-host removal.

## Preview 8

- Resize native Playground buttons for translated captions, expanding left while preserving their right edge.
- Wrap long captions and increase button height within screen bounds.

## Preview 7

- Expand Steam detection across registry views, installation records, standard locations, library files, and manifest directories.
- Skip incomplete installations and retain manual selection when discovery is ambiguous.

## Preview 6

- Localize native menus, help, pause, confirmations, and updates into the game's nine languages, plus a Polish catalog for an available Polish locale.

## Preview 5

- Add host multiplayer world deletion with a named confirmation and Cancel initially selected.

## Preview 4

- Add deliberate singleplayer world import without modifying the original save.
- Protect existing multiplayer worlds from repeated imports.

## Preview 3

- Clarify clipboard joining, invitation-code help, and invalid-code feedback.
- Defer navigation until submit callbacks finish and preserve page focus during update checks.

## Preview 2

- Hide the loader console while retaining file logs.
- Add Update and Uninstall actions and a Windows installed-apps entry.
- Preserve saves, other mods, and shared loader requirements when uninstalling.

## Preview 1

- Introduce native host/join menus, separate multiplayer saves, local guest profiles, an installer, and public update delivery.
- Check host creation, saving/loading, save isolation, and initial file/UDP behavior. Internet NAT traversal was not supported.

</details>
