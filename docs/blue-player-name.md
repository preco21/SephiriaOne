# Blue player name

This records the solid-blue implementation introduced in 0.2.0/0.3.0. Version
0.8.0 replaces it with a [per-letter name gradient](name-gradient.md) from
`#408af1` to `#a8d7fa`, retaining the ownership and native multiplayer transport
described here. Use the gradient document for current behavior and live checks.

## Behavior in 0.3.0

The local character/stats name and existing overhead nameplate use blue
(`#0000FF`). While another player is in the session, the mod also publishes a
formatted character name through Sephiria's existing name synchronization. This
is intended to display blue on unmodified clients as well as modded clients.
The existing local overhead label remains hidden when the game hides it.

Only the owned player's character name is changed. Other players' names and the
Steam account/lobby nickname are unaffected. The mod restores the plain runtime
name when the player is alone again. On unload it also requests restoration if
the player is still owned and the network connection is ready. Restoration
cannot be sent after disconnect or loss of authority.

The `SaveManager.Current` profile key `PlayerName` is read, never written. The
game's own run snapshots save `PlayerAvatar.Name` in `Player{index}Name`, so a
host's snapshot can contain the color markup while multiplayer is active. The
mod does not rewrite existing saves. Normal session initialization reads the
plain profile name again. This distinction matters when inspecting run data.

## Inspected game behavior

The local installation reports Sephiria `1.0.33` / Unity `6000.3.21f1`.

- `UIManager.connectedPlayer` identifies the locally connected player UI;
  `PlayerSpawner.isOwned` and `PlayerAvatar.isOwned` guard local ownership.
- `UI_StatsPanel.characterNameText` and `PlayerSpawner.WorldUserName` are the
  original local UI targets from `0.2.0`.
- `PlayerLocalDataStorage.OnStartAuthority` obtains the character's name from
  `SaveManager.Current.GetString("PlayerName", "HOST")` for session setup.
- `PlayerAvatar.SetPlayerName` updates `NetworkplayerNameSource` on the server
  or invokes the game's existing `CmdSetPlayerName` when called by a client.
  That reliable command requires authority and its server handler assigns the
  synchronized name. Both initial and incremental serialization include it.
- `UI_MultiplayerHUD` copies the synchronized name into remote overhead labels
  and party HP-bar labels. Its name refresh runs every other four-second tick,
  so allow approximately eight seconds for those displays to refresh.
- `UI_OtherCharacterPanel` reads the selected player's synchronized name when
  opened. Reopen it after a name update to refresh its text.

The installed assets were inspected read-only using UnityPy `1.25.3` and
TypeTreeGeneratorAPI in a temporary research directory. These exact serialized
fields all have rich text enabled, color-tag overriding disabled, and vertex
gradients disabled:

| Component field | Asset |
| --- | --- |
| `PlayerSpawner.worldUserNamePrefab` | `sharedassets0.assets` |
| `UI_MultiplayerHPBar.playerNameText` | `sharedassets0.assets` |
| `UI_MultiplayerInDungeonUserIcon.nameText` | `sharedassets2.assets` |
| `UI_OtherCharacterPanel.characterNameText` | `level2` |

These settings and the inspected synchronization path support using
`<color=#0000FF>CharacterName</color>` without adding a receiver mod or custom
network protocol. See Unity's [color-tag documentation](https://docs.unity3d.com/Packages/com.unity.textmeshpro@4.0/manual/RichTextColor.html)
and Mirror's [authority documentation](https://mirror-networking.gitbook.io/docs/manual/guides/authority).
Other mods or future game versions can change these assumptions. Chat may strip
formatting; it is not one of the requested nameplate targets.

## Implementation

- `LocalPlayerNameColor.cs` retains the local text-color overrides. It preserves
  transparency, rebinds existing labels, and restores original color settings.
- `MultiplayerNameColor.cs` reads the plain profile name, checks ownership and
  network readiness, and calls the native name update. It uses the same
  `MultiplayerList.Count > 1` threshold as the game's rename UI, which disables
  renaming in multiplayer. Publishing only in multiplayer avoids feeding markup
  into the solo rename input's character limit.
- `NetworkNameState.cs` tracks pending desired names. It avoids duplicate
  commands while waiting for synchronization, handles stale responses and name
  changes, and queues a plain name if a color request is still in flight when
  multiplayer ends. Only this mod's own outer color wrappers are normalized.
- The version is `0.3.0`. No additional runtime library, network message type,
  game assembly modification, or Harmony patch is required.

## Automated verification

On 2026-09-23, all 21 synchronization checks passed. Debug and Release builds
completed with zero warnings and errors. Release deployed successfully; DLL and
metadata hashes match the build output, and no game DLLs were copied. A separate
static review found no critical or important issues. Live peer rendering remains
unverified.

Run the portable synchronization checks without loading Unity:

```powershell
dotnet run --project .\tests\SephiriaOne.Tests --configuration Release
```

The checks exercise joining, leaving, delayed responses, superseded requests,
server name resets, repeated sessions, Unicode names, duplicate wrappers, and
uninitialized data. They test request decisions, not network transport or Unity
rendering. Build the addon against the installed game separately:

```powershell
dotnet build .\SephiriaOne.slnx --configuration Debug -p:DeployMod=false
dotnet build .\SephiriaOne.slnx --configuration Release
```

## Required live multiplayer check

The earlier `0.1.0` load and database callbacks were confirmed in-game. The new
multiplayer rendering has not been observed on a second client.

1. Restart Sephiria with `0.3.0`, enter town, and confirm the local character-panel
   name is blue. The profile name and solo rename input should remain plain text.
2. Host a session and have a second player join without this addon. Confirm
   `Player.log` reports `[SephiriaOne] Multiplayer blue name synchronized (#0000FF)`.
   That message confirms the synchronized local value, not the peer's rendering.
3. Allow about eight seconds. On the other client, check the character's overhead
   name and party HP-bar name, then open its character-details panel. The character
   name should be blue; Steam nickname suffixes and other players stay unchanged.
4. Repeat with the addon user joining a host without the addon. Check a late join,
   floor changes, leaving/rejoining, and a full game restart.
5. Return to solo play and reopen the rename input. Check that the name has no
   color markup and that the saved profile `PlayerName` remains unchanged.
6. If testing live addon unload, do it while connected and allow UI refresh time
   for other clients to see the restored plain name. Do not assume unload runs
   after the connection has already closed.
