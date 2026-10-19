# Overhead messages without chat history

Investigated 2026-10-18 against the installed Sephiria 1.0.33 assembly and addon
`0.62.1`. The initial investigation changed no delivery behavior; the `0.63.0`
prototype below now adds cosmetic countdown text while retaining chat/bubbles.

Exception since `0.66.1`: [Zombie conversions](zombie-survival.md#faction-and-conversion-notices-0661-2026-10-19)
use one combined infection/revival bubble instead of the generic respawn bubble
and floating-text duplicate. Ordinary deaths and the rest of the trial remain.

## Conclusion

The ordinary player chat bubble cannot be separated from the HUD chat log by a
host-only change using the inspected native chat transport. Each stock client
writes the log before creating the bubble. Targeting the packet privately changes
its recipients, not that behavior. There is no bubble-only flag in its payload.

An addon on each viewing client could separate these outputs. That does not meet
this project's requirement to support unmodified guests, so no local-only filter
or new toggle was added. A host-only floating-text alternative exists in native
code, but it is a different presentation with significant limitations. Version
`0.63.0` implements the limited prototype below; live multiplayer presentation
remains unverified.

## Floating countdown prototype (0.63.0)

The user approved a side-by-side trial, explicitly retaining chat until verified.
Deathmatch/Tournament respawn countdowns, Play Tag's one-second respawn, and the
active bomb timer now also emit native cyan floating text near the character's
head. Respawn particles reuse the existing localized sentence; bomb particles
use `Bomb: {0}s` / `폭탄: {0}초`. Full instructions, chat entries and ordinary
bubbles remain unchanged. Global starts, results, opponent notices, costume role
summaries and private killed-by notices are not converted.

`NativePlayerFloatingText` caches an open native sender delegate after validating
the exact sender/reader contract, channel 1, owner inclusion, and the receiver's
cosmetic call sequence. It shares payload validation with `NativePlayerAlert`,
which already uses this receiver for private guest low-MP feedback. That existing
alert retains its private reliable delivery and host center-screen behavior.
The prototype calls the native observer broadcast once, with normal text style
and damage-visibility filtering disabled. It never invokes actual damage or
creates a second host-local particle.

The shared `BroadcastCountdown` keeps chat first and checks the captured match
token and participant receipt again before optional floating output. It also
rechecks the exact pending respawn or bomb/carrier revision, since chat callbacks
can revive a player or transfer a bomb without ending the match. Existing
displayed-second checks bound emission to countdown changes; there are no extra
pollers, coroutines or per-player components. The adapter rejects stale/unready
identities, non-finite coordinates and empty text. It normalizes text with the
native sanitizer, accepts dead avatars, and disables its cached transport with a
single warning if compatibility or dispatch fails. Feature initialization/unload
resets that cache. Previously emitted particles finish their native fade; the
host cannot recall them from stock clients when a match stops.

Automated verification: the runtime fixture suite passes 2,519 checks, including
countdown uniqueness, retained chat, optional-presentation failure, normal
revival, loading, disconnect/replacement, run restart, manual revival, stopping,
and Tag transfer/respawn timing, callback-driven revival/transfer, and simultaneous
host/guest countdowns. Installed-game contract checks validate native
registration, payload arguments, cosmetic-only receiver, owner inclusion, EN/KO
keys and changed-contract rejection. Release build passes without warnings.
These checks do not establish visibility, readability or network delivery in a
running host/guest session. No game launch or deployment was performed.

Live verification before any chat suppression is considered:

1. With a host and an unmodified guest observing each other, trigger a death in
   Deathmatch and Tournament. Check cyan `Respawn in: 3s`, `2s`, `1s` (or Korean)
   near the dead player, with each original chat entry/bubble still present.
2. In Play Tag, inspect the short bomb timer while standing and moving, transfer
   it by killing another player, and confirm the recipient's one-second respawn
   precedes their reduced bomb timer. Both host and guest carriers need coverage.
3. Check that a viewer with party-member damage numbers disabled still sees the
   notices. Test localization, camera distance, tall costumes, visual crowding,
   fade speed, and duplicate/missing updates. Channel 1 can lose messages; retained
   reliable chat remains the readable fallback.
4. Stop/change modes, disconnect/rejoin and restart the run. New particles must
   stop for the old countdown; a brief final fade is normal. Verify normal damage,
   K/D/A, revival, Rabbit low-MP alerts and chat remain unaffected.

Do not remove chat based only on automated checks. Replacing repeated chat is a
separate change after user confirmation that the visual trial is acceptable.

## Existing message paths

| Path | Overhead display | HUD chat log | Scope |
| --- | --- | --- | --- |
| `CombatMatchRuntime.Broadcast(player, text)` | Player-following chat bubble | Yes | Dungeon observers, including host |
| `CombatMatchRuntime.Broadcast(null, text)` | None | Yes | Dungeon observers, including host |
| `NativePlayerChat.Show(player, text)` | None; deliberately sends a null avatar | Yes | That player's connection only |
| Direct `PlayerAvatar.CreateChatBubble(text)` | Player-following chat bubble | No | Calling client only; no network dispatch |

Respawn countdowns, Tournament opponent notices and Play Tag carrier notices use
the first path. Global announcements and rankings use chat-only broadcasts.
Private killed-by/matchup and costume-role notices use the third path, preserving
the player's current combat bubble. These private notices are not currently
duplicated overhead.

Native `DungeonManager.RpcChat(PlayerAvatar, string, string)` has only avatar,
speaker name and message fields (hash `1586055649`, reliable channel 0).
Its receiver normalizes the text, rejects an empty message, writes both a debug
log and `GameLogWriter.WriteLog`, then calls `CreateChatBubble` if the avatar is
present. There is no sender-name/private-message exception. A blank message
suppresses both outputs; a null avatar suppresses only the bubble.

`GameLogWriter.WriteLog` invokes `OnLogAdded`; the connected
`UI_HUDLogViewer.SpawnLog` always allocates/reuses a HUD log entry. Formatting a
message so it renders blank would still consume history entries. Rich-text tricks
cannot provide clean suppression: chat sanitization strips tags, and the HUD and
bubble disable rich text. Disconnecting the viewer would hide unrelated logs too.

`CreateChatBubble` replaces the preceding bubble, attaches it to `AppWorldUI`, and
sets a following transform/height. `UI_ChatBubble` has a three-second timer and is
a local `MonoBehaviour`, not a replicated object. Calling it on the host cannot
make it run on guest clients.

## Other native paths inspected

| Candidate | Finding | Suitability |
| --- | --- | --- |
| `UnitAI_NewBasic.RpcCreateSpeechBubble` | With `showName=false`, creates NPC speech without writing chat | Follows that AI's avatar, not an arbitrary player |
| `UnitAI_NewBasic.RpcCreateSpeechBubbleCustomPosition` | Also skips chat with `showName=false`; accepts a world position | Requires an existing networked AI component; fixed position, NPC presentation, shared NPC speech lifetime |
| `Grasstown_FirstEvent.RpcCreateSpeechBubble` | Uses a scene-specific text index and also writes chat | Not a general player-notice transport |
| `UnitAvatar.TargetSendCustomMessage` | Raises a client event; inspected subscriber handles shop feedback | No stock subscriber that creates arbitrary player bubbles |
| `UnitAvatar.RpcShowDamageParticle` | Accepts arbitrary text/position/color and creates a cosmetic world-space text particle without writing chat | Possible host-only alternative, but not a following chat bubble |

The NPC RPC receiver casts its network component to `UnitAI_NewBasic`; sending
that RPC to `PlayerAvatar` does not turn a player into an NPC speech receiver.
The custom-position version could show text near a player when a suitable NPC is
present, but its availability, observer scope and speech ownership depend on that
NPC. Spawning or repurposing NPCs solely as UI proxies would add lifecycle and
gameplay risk, rather than provide a reliable shared player-notice mechanism.

The damage-particle receiver creates text at a randomly offset position; the text
moves and fades independently of the player. The native sender uses channel 1
(unreliable), and its `isPrivate` argument controls damage-visibility filtering,
not recipient privacy. The existing reliable targeted-chat contract must not be
assumed to apply to this RPC. A future replacement would need its own exact
sender/receiver validation, explicit audience handling, short readable messages,
and live host/guest checks for timing, placement, visibility and duplicates.
Do not route notices through actual damage just to produce text.

## Reusable design boundary

Audience and presentation are separate concerns. A shared notice adapter could
express broadcast/private recipients separately from chat/bubble output, but it
must expose what the selected transport actually supports. For stock clients,
the current native player-bubble transport implies a chat entry. Centralizing
callers alone cannot remove that client-side behavior.

If viewer-side addons become acceptable, a narrow receiver patch could omit only
the HUD write for explicitly marked addon notices while preserving bubble
creation and ordinary chat. Host-only patches would improve the host's UI only.
Keep global results, errors and intentional private chat separate from transient
countdown notices; do not filter every message named `SephiriaOne` indiscriminately.

## Evidence and verification boundary

Installed `Assembly-CSharp.dll` SHA-256:
`c57a0daeab8e8d0af7066a344133eec4c57d8f303fd9e25da9410fbfc4cf1510`.
Module MVID: `ccf97024-28bd-48f1-9666-c768c2bd4b2b`.

REA retained evidence (static file inspection; no game execution):

- Assembly identity: `ev_c3733c62f61d0c4e651bfc15807d5dc53d2140cdbe2e223042a8a4df533a43a1`.
- Chat, bubble, sanitizer and log types:
  `ev_60074cc1f6b62340be1b51d9e05757a03cedbe66f08dd11f97e22f1f0a1b8dce`.
- HUD viewer, NPC speech, UnitAvatar notifications and shop subscriber:
  `ev_cf33aec2f7a8435ea4bd45ea4d5540ae8c153b2b839e4af6002e98b5455f4669`.

| Finding | Evidence anchor | Confidence / remaining limit |
| --- | --- | --- |
| Player chat writes the log before creating the bubble | Chat receiver token `0x06001d4d`; call-edge view `ev_e3938e79b8904de2e484ad307b6e19a53e0a5fea3ded3e85f743f04477254dca` | Observed CIL calls and branch order |
| Bubble creation is local presentation | `PlayerAvatar.CreateChatBubble`, token `0x06004fb5`; method view `ev_b32b149791787f1e2d23f24ca9cff0471d7b13122a110b5de1711f3098a3ba5e` | Observed method body; no network send |
| NPC speech has a conditional log write | `CreateSpeechBubbleOnClient` / `CustomPosition`, tokens `0x0600734f` / `0x0600734b` | Observed receiver branch; not a player-attached transport |
| Floating text does not write chat | Damage-particle receiver token `0x060053ec`; method view `ev_96e85d5cb9a0d4a43eb9fac4d88d4eb2f3dbbcebc5a491ab3672dea01d597bed` | Observed native implementation; proposed notice reuse remains untested |

REA reported complete metadata/member coverage for the selected types. Its
user-string facet reported malformed terminal flags on some unrelated literals;
the conclusions above rely on decoded methods/calls and inspected control flow,
not completeness of a string search. Tokens are specific to this assembly.
Cross-referenced the external ILSpy game export for control flow, RPC
registrations, bubble callers and notification alternatives. The negative finding
is bounded to these native paths in this build; it is not a claim about future
game versions or every possible UI workaround.

Related: [Deathmatch transport](deathmatch.md), [private killed-by notices](friendly-fire-killed-by.md),
[costume role notices](costume-role-notices.md), and
[native private-chat compatibility tests](../tests/SephiriaOne.Tests/Compatibility/GameNativePlayerChatTests.cs).
