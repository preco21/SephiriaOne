# Private killed-by notices and matchup history

Implemented in `0.49.0`, 2026-10-12. Native evidence uses the installed assembly
fingerprint in [domain findings](domain-findings.md).

## Player behavior

A confirmed PvP kill sends a private native chat notice to the dead player in
casual friendly fire, Deathmatch and Tournament. The host's configured language
selects the text, including for guests without the addon:

- English: `Killed by ‘PlayerX’ (Matchup: Me 3–4 Opponent)`
- Korean: `‘PlayerX’에게 처치됨 (전적: 나 3-4 상대)`

The first number is the victim's kills against that opponent; the second is the
opponent's kills against the victim, including this death. The first encounter
therefore shows `0–1`. Assists and kills against other players do not contribute.
The normal public K/D/A log and overhead respawn countdown remain unchanged.

Self/environment/enemy deaths, extra-life saves, stale connection receipts and
incomplete native death presentation produce no private notice. Unknown/empty
killer names omit it instead of displaying `?` or an invented player label.
Sanitized names are captured before death callbacks can clear or alter them.
Native `ChatTextSanitizer.Sanitize` also rejects invisible format-only names
before deciding whether the name is usable; markup stripping alone cannot do so.
Companion and reflected kills use the existing confirmed owner/killer attribution.

## Shared accounting and resets

`FriendlyFireKda.CompleteDeath` records each directed pair exactly once alongside
the existing kill/death totals. Keys are the same `Score` identities used by KDA,
never nicknames, network IDs or Unity objects. Nothing is serialized into game
state, presets or saves. History changes only on confirmed deaths and reset
boundaries; there is no additional per-frame work.

| Boundary | Matchup behavior |
| --- | --- |
| Combat/Deathmatch/Tournament Reset K/D/A command or button | Clear both directions for every pair |
| Friendly-fire on/off transition, match start/stop/end | Clear with existing KDA reset |
| Explicit participant selection edit/reset | Clear with existing score reset |
| New run, run/save/dungeon replacement, server stop, addon unload | Clear with participant/session scope |
| Revival, floor movement, damage scale change, Tournament pair/countdown change | Retain totals |
| Same-account reconnect in the same scope | Retain historical totals like KDA; new connection receives only new notices |
| Different account reusing a name/slot/network ID | Independent record |

Session score identity and connection permission remain distinct. A rejoining
player does not inherit old selection or Tournament membership. `ClearLives`
invalidates pending attribution and assists, but does not erase pair history:
Tournament uses it between rounds. `FriendlyFireParticipants.Clear` explicitly
clears history at scope replacement. Exceptional native deaths keep the existing
server-side KDA semantics, but omit private presentation because `RpcDie` may not
have completed.

## Native delivery evidence and extension guidance

`Infrastructure/NativePlayerChat` is a reusable presentation-only adapter. It
targets the existing stock `DungeonManager.RpcChat` receiver using Mirror's
protected `NetworkBehaviour.SendTargetRPCInternal`:

- Native method identity:
  `System.Void DungeonManager::RpcChat(PlayerAvatar,System.String,System.String)`
- RPC hash `1586055649`, reliable channel `0`.
- Exact native payload: `WriteNetworkBehaviour(null)`, `WriteString("SephiriaOne")`,
  `WriteString(message)`.
- The receiver normalizes text, writes it through `GameLogWriter.WriteLog`, and
  only creates an overhead bubble if the avatar argument is non-null. Null thus
  preserves Deathmatch/Tournament respawn bubbles.
- Mirror targeted RPCs use the same registered client receiver as broadcasts,
  but send to one connection. `LocalConnectionToClient.Send` also queues the
  message to the host's client; no separate host broadcast is needed.

Do not call public `DungeonManager.Chat` for private text: it broadcasts on the
server and sends a command on clients. `TargetSendCustomMessage` only invokes a
custom-message event/log path, not the required stock chat UI.

The death hook validates captured participant scope/epoch after native callbacks.
The adapter additionally checks authority, owner identity, readiness and exact
current connection-map membership. No message is queued by account for later
delivery. Serializer/reader/cosmetic-body validation runs once, fails closed and
warns once if the installed contract changes. Pooled writers are returned in a
`finally`; optional delivery failures do not cancel KDA, public logs or recovery.

## Verification and live limits

Executable combat tests cover victim-first totals, alternating/repeated kills,
independent opponents, duplicate names, sanitized/snapshotted/unknown names,
assists, companion/reflected kills, extra lives, self/environment deaths, stale
callbacks and transport failure. Shared runtime tests cover both match modes,
respawn bubble coexistence, round/rematch retention, all score reset routes,
match end, scope replacement, same-account re-entry and recycled network IDs.
Native IL tests verify payload, stock registration, target/local dispatch and
null-avatar isolation, and reject mutated payloads/readers/gameplay effects.
EN/KO tests verify both exact requested formats.

Verification: Debug/Release builds passed with zero warnings/errors; 967 combat
hook checks, 1,734 shared runtime checks, 69 Bat lifecycle checks, 27,864 Tournament
scheduler checks, 3,278 bundled catalog checks and the full portable/installed-game
contract suite passed. Independent review found the invisible-name edge case;
native sanitization and regression checks resolved it, with no remaining findings.

No game launch or deployment was performed. Live host/stock-guest rendering and
delivery remain untested: check both killer directions, at least one spectator,
respawns and reconnects, and EN/KO chat readability. A native-compatible payload
and executable fixtures do not establish a live network connection.
