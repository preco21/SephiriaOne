# Blue local player name

## Design

The requested behavior is a blue (`#0000FF`) local character name in both the
character/stats panel and the existing overhead nameplate. This is a local UI
effect. It does not rename the character or change saved or networked name data.
Other players' labels, chat, and Steam lobby names are outside this feature.

Inspection of Sephiria 1.0.33 identified these existing public references:

- `UIManager.connectedPlayer` is assigned when the local player's UI connects.
- `UI_StatsPanel.characterNameText` displays that player's character name.
- `PlayerSpawner.WorldUserName` holds the existing overhead label.
- `PlayerSpawner.isOwned` distinguishes the local player from remote players.

Use one addon-owned `MonoBehaviour` to update those two text components in
`LateUpdate`. Rebind when the UI or player changes, preserve the current alpha,
and restore the original RGB and text color settings on disconnect or unload.
Override embedded color tags and disable vertex gradients on these labels to
keep the requested color uniform. Text content and visibility stay game-managed;
in this game version the local overhead label is normally hidden.

A direct component update uses the installed game's public fields and requires
no additional runtime library. A Harmony patch would add dependency loading for
this small change. Adding rich-text tags to the player's name would affect name
data used by networking and saves, so it is not used.

## Implementation plan

1. Add `LocalPlayerNameColor.cs` for local-player selection, label coloring,
   rebinding, and restoration. Avoid scene-wide text searches and name matching.
2. Create a persistent controller from `Entry.OnModLoaded`; disable and destroy
   it in `OnModUnloaded`. Retain the confirmed lifecycle log messages.
3. Reference the installed `Mirror`, `Unity.TextMeshPro`, and `UnityEngine.UI`
   assemblies with `Private=false`; update the addon version to `0.2.0`.
4. Build Debug and Release against the installed game, inspect the resulting
   references, and compare deployed DLL/metadata hashes with Release output.
5. Review ownership checks and cleanup, update development notes, then commit
   and push using the authorized repository workflow.

## Build and review results

Completed on 2026-09-23:

- Debug and Release builds succeeded with zero warnings and zero errors.
- The automatic Release deployment copied the DLL and metadata to
  `AddOns\SephiriaOne`; SHA-256 comparisons confirmed both files match the output.
- Deployed metadata reports `0.2.0`, and the compiled assembly reports `0.2.0.0`.
- Compiled references use the installed game assemblies. No extra DLLs were
  copied into Release output.
- Static review checked the local ownership path, label rebinding, transparency,
  destroyed-object handling, and unload cleanup without finding concrete issues.

## In-game verification

Build and file checks cannot prove Unity rendering. After restarting the game:

1. Enter town or a run, then open the character/stats panel. The character name
   should be blue and the rest of the panel should retain its normal colors.
2. Check `Player.log` for `[SephiriaOne] Loaded v0.2.0`, the database-ready message,
   and `[SephiriaOne] Blue local player name applied (#0000FF)`.
3. If the existing local overhead nameplate is visible, confirm it is blue.
   This mod deliberately does not make a hidden nameplate visible.
4. Reopen the panel, change floors, return to the title screen, and enter again.
   Verify the name stays blue and no exceptions appear.
5. In multiplayer, confirm other players' labels retain their colors. The
   override runs on this client; it does not advertise a color to other clients.

The earlier `0.1.0` lifecycle was confirmed in-game by the user and by inspecting
`Player.log`. Visual verification of the new color and multiplayer checks remain
pending until the new build is run.
