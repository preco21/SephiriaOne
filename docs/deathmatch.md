# Temporary deathmatch

Added in `0.42.0`. Open `/one ui` → **Deathmatch**, enter a duration in seconds,
click **Apply duration**, then **Start deathmatch**. **Stop deathmatch** ends it
early. The shared host commands are:

```text
/one deathmatch duration 300
/one deathmatch start
/one deathmatch status
/one deathmatch stop
/one deathmatch help
/one save
```

Duration defaults to 300 seconds, accepts whole seconds from 10 to 3600 and is
captured when starting. Editing it during a match affects the next match. Timers
use host unscaled time, including time spent in menus. The existing friendly-fire
damage percentage applies; editing that percentage does not stop a match. Native
safe-area protection, invulnerability, guards and defenses remain in effect.

## Match behavior

- The host broadcasts `3...`, `2...`, `1...`, then `Deathmatch start!` through
  native chat. Friendly fire stays off during the opening countdown and turns on
  at the start. KDA starts fresh, even if friendly fire was already enabled.
- Every player death during the match gets a three-second respawn. Deaths during
  the opening countdown use the same timer; players already dead when starting
  get their timer immediately. Extra-life prevention is not a death.
- Native avatar chat displays `Respawn in: 3s`, then `2s`, then `1s`. Each message
  replaces the preceding bubble and also appears in native chat. `Respawned!`
  replaces the final countdown after native full-HP revival. Messages use the
  host's selected EN/KO language. No guest addon, assets or custom RPC is needed.
- At expiry, friendly fire turns off, pending dead players revive immediately,
  and chat lists up to five participants as `rank. nickname(K/D/A)`. Rank uses
  kills descending, deaths ascending, assists descending, then enrollment order.
  Results are captured before disabling friendly fire resets ordinary KDA.
- KDA follows the existing friendly-fire attribution: player/owned-companion
  kills, PvP deaths and prior contributing players' assists. Enemy/environmental
  deaths still respawn but do not award PvP KDA. Disconnected participants remain
  eligible for results. Native account identity preserves scores on reconnect;
  offline identities remain tied to the exact spawner, never a reused nickname.
- Touching friendly-fire **On**, **Off**, or **Reset** immediately stops the match,
  including repeated commands selecting the current value. Pending players revive;
  the requested toggle selection then takes effect. Read-only status/help and
  damage-scale edits do not stop the match. The Combat toggle remains usable to
  stop the match while a guest initializes or an unrelated stat write is faulted.

## Temporary state and recovery

Match phase, timers, score participants and respawn records exist only in memory.
The match overrides the effective friendly-fire setting consumed by combat/UI;
it does not overwrite the persisted normal setting just to enable a match. Saving
during a match stores the configured duration and ordinary settings, never match
progress or the temporary forced-on state. Nondefault durations use preset v18;
earlier presets retain the 300-second default. Match end explicitly turns the
current normal friendly-fire setting off.

Run restart, new session, replaced dungeon/save, authority loss and terminal
settlement cancel the match without applying old recovery to new state. A player
leaving loses their old avatar's timer. A newly ready avatar gets a fresh death
observation; reconnecting cannot revive its previous connection's avatar. Floor
movement inside the same run retains the match, with readiness checked before
native revival. Existing game-over settlement still cannot be undone.

Revival reuses the exact same scoped service and callback containment as
`/one reviveall`. All-dead settlement is suppressed for this match's current
player deaths, including environmental ones, while leaving ordinary game over
outside the match unchanged. Stop collects both queued respawns and currently
dead roster members, so a death not yet observed by the timer is not missed.

A stop inside native `Die` waits for the death finalizer before reviving, keeping
`RpcDie` before `RpcRevive`. A stop inside native `Revive` lets that player's
recovery finish before draining the rest. Each copied timer must still be the
current record for that life. Native revival invalidates both active and cleanup
queues, preventing a later life from receiving an old automatic revival.

Live unload first recovers the same-scope party before removing combat/revival
hooks. If a native death/revival callback is still unwinding, unload rejects at
that boundary and must be retried after recovery completes. Loading/invalid
players or failed native callbacks are reported, rather than publishing invalid
HP or claiming successful recovery; `/one reviveall` remains the manual retry.

## Implementation and evidence

- `Features/Combat/DeathmatchRuntime.cs`: scoped state machine, enrollment,
  countdown/respawn scheduling, cleanup and rankings.
- `DeathmatchCommand.cs`, `Session/SessionDeathmatch.cs`: shared command and
  duration policy. `DeathmatchSettings.cs` provides bounds/defaults.
- `FriendlyFireNames.cs` / `FriendlyFireRuntime.cs`: existing native death,
  revival and game-over boundaries. `ReviveAllAction.TryRevive` shares recovery.
- `DeathmatchFeature.cs`: validates the native bubble contract before enabling;
  `UI/SettingsPanelDeathmatch.cs` provides the tab.

Installed assembly SHA-256:
`C57A0DAEAB8E8D0AF7066A344133EEC4C57D8F303FD9E25DA9410FBFC4CF1510`.
`DungeonManager.Chat(avatar, name, message)` uses the native `RpcChat` transport.
Its stock receiver calls `PlayerAvatar.CreateChatBubble`, which destroys the old
bubble and creates a new `UI_ChatBubble` following the avatar transform. Neither
the creator nor `SetText`/`Update` rejects dead/hidden bodies. The native bubble
disables rich text and expires after three seconds; sending `Respawned!` prevents
the old `1s` message lingering after revival. These findings are native code
contracts, not proof of live client rendering.

Idle ticks return without roster scans. Active/warmup roster maintenance is
throttled to 10 Hz; messages publish only at countdown changes and match events.
Warmed fixture checks measure zero allocations across 1,000 active ticks with
living players and 1,000 idle ticks. Death, join and match-end work allocate small
bounded collections; there are no per-player coroutines or custom network state.

Regression coverage includes commands/presets, both countdowns, ending with dead
players, all manual toggle operations, slider edits preserving scores, warmup
deaths, reconnect/reused slots, lifecycle changes inside callbacks, death/revival
RPC ordering, replaced lives in copied queues, rankings and allocation checks.
The installed-game suite validates native bubble/RPC paths and shared UI command
dispatch. Builds and tests always use `-p:DeployMod=false`.

Verification on 2026-10-10: Debug and Release builds pass with zero warnings/errors;
1,370 shared-runtime checks, 261 executable combat checks, 69 Bat lifecycle checks,
2,849 catalog checks and the complete portable/installed-game suite pass. Review
findings about callback ordering, life identity and unload recovery were fixed
with regressions; the final independent review found no blocking issue.

Live validation is still required with an unmodified guest: observe both
countdowns, kill/respawn host and guests repeatedly, end with everyone dead,
toggle during a countdown, quit/rejoin, move floors, start a second run, check
EN/KO labels and native bubble placement above a dead player. No deployment or
game launch was performed during implementation.
