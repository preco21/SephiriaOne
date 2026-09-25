# Multiplayer disconnect investigation

2026-09-25, installed Sephiria 1.0.33 and SephiriaOne 0.15.2. The installed addon
SHA-256 matches the Release build at `92529ce`.

## Incident evidence

The host's `Player.log` contains five successfully enrolled players. Connection 1
left after Library floor generation (`10712`: Steam `k_EResultNoConnection`,
`10713`: connection lost). Connection 3 left after a miniboss fight (`39369` onward),
without a preceding reason. The other departures occurred at session end.
These are log line numbers in the original local log, not timestamps.
The host recalls the players being idle and has not ruled out voluntary exits;
proximity to these logged game events does not establish a trigger.

There are no host exceptions, Mirror invalid-message disconnect reports, addon
write/guard failures, out-of-memory reports, or send-buffer limit warnings around
either incident. `Unknown Connection` and null-object destruction warnings occur
after the first connection has already been removed. They do not establish its
cause. The shutdown ComputeBuffer disposal warning is also later than both drops.

The saved preset currently contains Fountain x3, item choices +3, luck x3,
cooldown x3, talents +20 and fruit +2. It is a current file snapshot, not proof that
every value was unchanged throughout the run. RaidRaid was also active, including
level inventory bonuses, rewards and combat changes. The log does not establish
which addon, native game behavior, client failure or transport condition caused
the drops. No guest log or crash report is available locally.

## Native code findings

- FizzySteam `NextServer.Send` treats `NoConnection` and `InvalidParam` as lost
  connections. This incident reports the former, not the latter.
- `NextServer.OnConnectionStatusChanged` routes both `ClosedByPeer` and
  `ProblemDetectedLocally` into `InternalDisconnect` without recording Steam's
  end reason/debug detail. `InternalDisconnect` closes the socket afterward.
  This loses the evidence needed to distinguish these cases in the host log.
- Mirror's message wrapper can disconnect a sender when its command handler
  throws. `PlayerAvatar.CmdLoadPassiveStat` calls the guarded talent loader;
  ordinary managed-budget refusal currently throws. Native preset switching
  sends a separate talent reset before that load. This is a real additional risk,
  but its required addon warning is absent from this incident's host log.
- Native initialization must still stop if saved allocations cannot be preserved.
  Suppressing all guard errors would allow partial initialization and later saves
  to overwrite allocations. Any rejection handling must distinguish the standalone
  live-menu command from the larger initialization command.

## Review coverage

The recent typed observations, weak ownership caches and reused scratch buffers
do not change native packet layouts or polling frequency. Resource/stat writes
skip unchanged targets. Added player loops use native collections without a
four-player array limit. Ownership markers are ordinary string dictionary entries
tolerated by native callbacks. Talent capacity uses the native integer SyncVar;
inventory uses native capacity notifications. Checkpoints are host-local save
metadata. Dice/leaves maintenance does not repeatedly refill live balances.

These observations narrow the investigation; they cannot prove that a client
never receives a problematic native combination of values or runs out of memory.

## Implementation and verification plan

- [x] Reproduce the standalone talent-command rejection escaping its handler;
  contain only the addon's recognized rejection, retaining initialization guards
  and unrelated native exceptions.
- [x] Add optional, read-only disconnect diagnostics before native socket close
  and player destruction. Capture Steam state/reason, connection/net ID, managed
  heap estimate and existing sync state. No synchronization, network writes,
  polling, identifiers from Steam profiles, or gameplay changes in this path.
- [x] Exercise five-player state maintenance, two departures and new connection
  lifetimes; verify unchanged ticks do not write native state.
- [x] Run existing regression suites, installed-game contract checks and builds
  with `DeployMod=false`; review the diff and record limitations.

## Changes in 0.15.3

The talent regression failed before the fix with the exact managed-budget error
escaping the woven menu command. A dedicated rejection exception now unwinds that
standalone command and is contained by its finalizer. Initial player-data loading
and direct native loading retain the abort. Unrelated native loader failures and
unexpected non-validation prefix failures still escape; a partial-write fault is
not cleared by the finalizer. The host receives a log and local game-log warning.

This does **not** undo the separate native reset sent before loading a preset.
After rejection, allocations may be empty. The host should raise the budget or
the player should select a fitting loadout and retry. Native UI considers its
request complete without an acknowledgment, and later closing the native talent
panel can save those current allocations. There is no guest-side error toast or
claim that a rejected preset restores the previous allocation/profile.

Disconnect hooks are optional and isolated from resource compatibility gates.
Before socket close they query Steam's existing connection information; before
player removal they read existing reconciliation results and raw stat fields.
They do not call `Synchronize`, native getters, recovery, save APIs, or RPCs. Logger,
Steam-query and snapshot failures are contained so cleanup still runs. There is
no per-frame diagnostic polling and no retained player/connection history.
The managed heap measurement is a host estimate, not client, total-process or GPU
memory. Transport details are length-limited and stripped of control characters.
The addon does not log Steam IDs, profile names, GUIDs or addresses explicitly;
Steam's own diagnostic text may contain connection information, so review logs
before sharing publicly.

Five-player fixture coverage includes the incident's saved configuration, native
stat/amplifier changes, stage notifications, eight cycles of two simultaneous
departures with reused IDs/new avatars, full live-family inheritance and 500 idle
ticks per cycle. The two five-player allocation checks measure zero bytes per
unchanged tick (10,000 ticks, .NET fixture). They do not simulate Steam traffic,
Unity object lifetimes/rendering, native restart reset order or a guest crash.

The separate existing starting-resource and reconnect suites retain saved-zero,
spent-balance, native-gain, offline-reset and checkpoint/recovery coverage.

Verification: **1,714 checks passed** (946 portable, 631 runtime, 57 starting hooks,
66 budget/inventory hooks and 14 disconnect boundaries), plus two five-player
allocation budgets. Debug and Release builds produced no warnings/errors. Native
IL checks confirmed the woven talent command is a single load, the transport hooks
match installed signatures, and Steam exposes the queried close-reason API.
Independent review found no blocking issue. Installed addon hash/metadata still identify 0.15.2; no deploy
script ran and 0.15.3 has not been exercised in a live multiplayer session.

Run the additional diagnostic fixture suite with:

```powershell
dotnet run --project tests/SephiriaOne.DisconnectTests -c Release -p:DeployMod=false
```

## Evidence needed if it recurs

The 2026-09-26 recurrence was reviewed in
[penalty costumes and synchronization](penalty-stat-sync-review.md). That review
connects the stat fallback status to the Fountain freshness warning and fixes
incompatible multiplier inheritance. It records new Steam send-buffer/timeout
evidence, but does not establish the cause of the peer disconnects.

Keep the host and affected guest `Player.log`/`Player-prev.log` before another
launch replaces them. A guest-side Mirror exception/deserialization trace or a
Unity crash log can distinguish invalid client state from Steam connection loss.
Record whether the game closed, returned to the lobby, froze, or displayed an
error, and the event immediately before it. Steam close reasons help classify a
failure but do not, by themselves, identify the mod that caused it.

Full logs and decompiled game/addon code stay outside the repository.
