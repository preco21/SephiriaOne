# Friendly-fire and deathmatch participants

Added in `0.45.0` (2026-10-11). The host controls one temporary participant list
shared by friendly fire and deathmatch. Guests do not need the addon.

## Rules and controls

- **No explicit selections:** all current valid players participate, including
  new arrivals. Friendly fire still requires the master switch and positive scale.
- **One or more selections:** both controlling players must be selected for an
  allied hit. Only selected players enter deathmatch respawn/protection/rankings.
  Selecting one player alone does not make unselected players valid opponents.
- **Select All / 모두 선택:** explicitly select everyone currently connected.
  Uncheck a few players to exclude them. This behaves like the empty list for
  current players, but future arrivals remain unselected while the list is nonempty.
- **Reset selection / 선택 초기화:** clear the list, restoring everyone mode. This
  keeps the damage percentage and, outside deathmatch, the master switch.
- Each explicit selection action stops an ongoing deathmatch, including repeated
  commands, and recovers its pending participants. Match end disables the master
  switch. Unselected dead bystanders are not revived by match cleanup.
- Selection actions reset K/D/A and pending assists. Connection/run transitions
  clear stale attribution while preserving the existing scoreboard-total rules.

Both **Combat** and **Deathmatch** tabs expose the same scrolling player
checkboxes, selection-mode label, Select All and Reset selection buttons.

```text
/one friendlyfire players
/one friendlyfire player 2 on
/one friendlyfire player 2 off
/one friendlyfire players all
/one friendlyfire players reset
```

Use the current network ID shown by `players` or the panel, not a nickname or
save slot. Commands and UI share `SessionCombatSelection`; UI actions carry the
exact displayed connection receipt, rejecting an ID reused after rendering.
`/one friendlyfire off` keeps the list for editing; `/one friendlyfire reset`
clears it as well as resetting off/100%. Presets never save player selections.
The manual **Revive all players** action remains deliberately session-wide.

## Lifetime and native evidence

`FriendlyFireParticipants` owns current receipts and selection. It validates
the exact avatar, spawner, shared NetworkIdentity, nonzero net ID, roster entry,
connection object and connection ID, `NetworkServer.connections`, and
`NetworkServer.spawned`. Dungeon identity/net ID, run-save identity and shared
run generation delimit scope. Connection readiness gates combat but does not
erase a selection or restart a pending deathmatch timer during floor loading.

Player selections clear on disconnect, destruction, successful replacement,
run/avatar reinitialization, new run, server/controller shutdown or changed
scope/authority. A reconnect is a new unselected lifetime even with identical
object references or IDs. Ordinary costume/stat changes retain selection.
When the last selected player leaves, remaining valid players enter everyone
mode immediately; the retiring player remains denied.

Inspected installed native contracts:

- `PlayerSpawner.Awake` inserts into `MultiplayerList` before assigning the
  avatar; `OnDestroy` removes authority before removing the roster entry.
  Membership alone is not evidence of a live connection.
- `PlayerSpawner.Initialize(int,string,string,int)` handles rejoin/default-data
  and restart initialization; it may invoke revival/costume callbacks. The hook
  retires the old lifetime before entry and reopens only successful completion.
  Ordinary wardrobe changes use `PlayerAvatar.LocalEquipCostume` instead.
- `HorayNetworkManager.OnServerDisconnect` saves/preserves objects before base
  destruction. Host `NetworkManager.StopClient` invokes that callback while the
  connection may still be mapped and ready. Retirement therefore precedes it.
- `NetworkServer.OnTransportDisconnected` removes the connection before the game
  callback, but its cleanup does not clear readiness. `RemoveConnection` and a
  successful `AddConnection` mark separate generations even within one frame.
- `OnStopServer` precedes `NetworkServer.Shutdown` clearing server activity. A
  separate stopped latch prevents empty-selection fallback during teardown.
- Mirror assigns and clears its connection callback fields; direct subscriptions
  cannot provide this lifetime guarantee. Hooks share the combat Harmony owner
  and installation rollback instead.

Assembly-CSharp SHA-256:
`C57A0DAEAB8E8D0AF7066A344133EEC4C57D8F303FD9E25DA9410FBFC4CF1510`.
Mirror SHA-256:
`C391C91BA860C537692C185B1075F4219EC83FA71A11990A2B51C13876AB7440`.
No native binaries or decompiled source are included in this repository.

## Combat and recovery boundaries

Direct hits, companion AI, supported artifact targeting/homing, reflection,
debuff application and delayed damage use the same controlling-player policy.
Companions still cannot damage their own owner; native enemies, self/system
damage, peace areas, immunity, item prerequisites and defenses remain native.
This does not extend the [audited artifact allowlist](friendly-fire-artifacts.md).

Hit receipts recheck participation after native attacker/leader callbacks and
before guard/HP/shield writes. Selection epochs stop an old hit from borrowing
the empty-selection fallback for nested reflection or item effects. Debuffs
capture source and target ownership receipts; owner transfer/disconnect retires
their old authority. The existing death hook remembers companion ownership
before native leader cleanup, including already-fired projectile sources.

Keep three concepts distinct when extending this code:

1. Current eligibility permits a **new** allied hit or respawn.
2. Exact lifetime receipts prevent old operations crossing into new connections.
3. An admitted lethal hit/revival must finish its native recovery sequence even
   if selection changes inside a callback. Do not retroactively remove all-dead
   protection or abort `RpcRevive` after native `IsDead` was already cleared.

Deathmatch captures eligible dying lifetimes, waits for `RpcDie` before recovery,
and validates receipts in timers and stop cleanup. Already-started revival checks
identity/scope throughout its native tail, rather than mutable selection. New
arrivals in everyone mode receive fresh timers; selected-mode reconnects do not
inherit prior participation, protection or timers.

## Verification and remaining checks

Final nondeploy verification: Debug and Release built with zero warnings/errors;
1,530 runtime integration checks, 69 Bat lifecycle checks, 729 executable combat
checks, 22 panel checks, 50 localization runtime checks, and 3,151 bundled catalog
checks passed. The portable policy suites and full installed-game IL contracts
also passed. Independent review found and resolved temporary-readiness timer loss,
selection changes interrupting an admitted revival, and overbroad stale-source
rejection against native monsters.

Coverage includes executable native-shaped lifecycle hooks, command/preset/UI
receipts, selective deathmatch recovery, live readiness loss, stale copied timers,
same-frame connection reuse, unrelated stat faults, and supported combat paths.
Installed-game IL contracts check lifecycle ordering and the pre-guard insertion.
Cached receipt reads, living-player match ticks and unchanged synchronization
retain zero measured steady allocation in the .NET fixtures. UI rows are pooled
within each open tab; only Combat/Deathmatch/Status capture participant details.

These are fixture/IL checks, not a live Unity multiplayer test. Verify in-game
with unmodified guests: all/selected/excluded pairs, guard MP, companions and
delayed effects, reconnect while the list is nonempty, deathmatch selection edits
during death/revival, both EN/KO tabs and scrolling. No build was deployed.

Raw public collection removal/readdition using identical references without an
audited lifecycle method or an intervening observation leaves no detectable
history. Other addons must use the native connection/spawn lifecycle; this
implementation does not instrument arbitrary mutations of public collections.
