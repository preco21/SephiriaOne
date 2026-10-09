# Friendly fire

Version `0.42.0` adds [temporary deathmatches](deathmatch.md) with synchronized
friendly-fire toggles, timed respawns and top-five results. Deathmatch uses these
same damage, attribution, KDA and native recovery paths. Outside a match the
existing behavior below is preserved.

Version `0.41.0` adds [revive-all recovery](#revive-all-recovery) through command and
Combat UI, including protection against friendly-fire-triggered run settlement.

Version `0.40.0` adds K/D/A to player kill notices, for example
`PlayerA(1/2/3) killed PlayerB(1/1/3)`, inside the existing localized chat message.
The displayed totals include that death. Enabling or disabling friendly fire
resets all scores and pending assists immediately.

Version `0.39.2` fixes unnamed kill notices and extends the audited offensive
item selectors to player targets. It also permits bounded immediate debuff and
burning-death explosion damage, and preserves debuff ownership through companion
death/disconnect. The existing toggle and damage slider control these paths.

Version 0.37.1 allows native Thorns, weapon reflection and the default Venom Spore
Pouch parry return to damage the original attacker using the friendly-fire scale.
Reflected hits cannot reflect again. Off/reset or 0% blocks reflected team damage
before guard costs or callbacks.

Version 0.37.0 fixes the native player-protection veto that prevented HP damage
while sword-and-shield guarding could still spend MP. It also enables companion
attacks against other players under the same host option.

Friendly fire defaults to **off** with a
100% allied-damage scale. Open `/one ui` → **Combat**, enable it, move the slider,
and press **Apply damage**. Dragging the slider does not send gameplay updates.
The range is 0–300% in whole percentage points.

Commands use the same host service as the panel:

```text
/one friendlyfire on
/one friendlyfire damage 25
/one friendlyfire damage 200
/one friendlyfire off
/one friendlyfire status
/one friendlyfire reset
/one save
```

`off` keeps the selected percentage for later; `reset` restores off/100%.
`/one save` stores applied settings for future sessions. Old presets keep friendly
fire off. Nondefault combat settings use preset v12; malformed or duplicate rows
reject the entire preset.

## Behavior and boundaries

- Direct player attacks that reach an allied UnitAvatar can damage other players,
  followers and friendly NPC factions. Melee and projectile collision handling
  runs on the server. Existing area damage delivered to allies uses the same path.
- Player-led companions using the standard native AI (including Collin) actively
  target other players. They never target or damage their own owner. Normal sight,
  range, target validity and peaceful-area rules still apply. Their hits against
  other players use the same allied-damage scale; their hits against monsters are
  unchanged. Companion AI does not gain new targets among friendly NPCs or pets.
- Burn Ring, Fire Feather, Flame Ground Meteor, Dark Cloud (including Blazing
  Stormcloud burn), Thunderous Steps and Fire Chakram include other players in
  their offensive target filters while enabled with a positive damage scale.
  Native range, height, alive/targetable checks, cooldowns and proc chances remain.
  Other enemy-seeking spells, general projectile homing, healing and faction
  relationships keep native targeting. This is an explicit audited list, not a
  global faction change that makes every skill target allies.
- Ordinary enemy/NPC attacks, self damage and system
  damage keep native behavior. Safe-area peace mode, invulnerability, dodge and
  guard checks still apply. Guarding with sword and shield can still spend MP;
  unguarded hits now proceed to HP damage. Only the matching player's native
  team-protection callback is skipped. Other damage vetoes, NPC safe mode, crime
  and retaliation rules are not disabled.
- Allied damage scales after defense/critical/true-damage calculation, before
  normal and MP shields. Native super armor, extra lives and death follow.
  At 0%, the allied hit is rejected before shields, protection points or on-hit
  callbacks. Negative/non-finite input and non-finite derived damage are guarded;
  extreme values are capped below the native integer conversion limit.
- Native direct reflection can return one hit per effect to the actual attacker.
  The supported identifiers are `Ability_Thorns`, `Weapon_Reflect` and the default
  `Charm_VenomSporePouch`, each with native `fromType=None`. During an allied hit,
  only that hit's victim may reflect to its source. Reflection of reflection,
  unaudited nested ally procs and chains routed through enemies are rejected.
  This prevents recursion before native hit invulnerability starts while allowing
  ordinary reflection. Unrecognized/custom reflection identifiers remain blocked
  within the hit chain until their paths are audited.
- Reflection keeps its native formula and gets the same percentage once after
  the return victim's defenses, before shields. Weapon reflection uses incoming
  raw damage and its normal effect bonuses; Thorns uses native defense/thorns
  values. For example, a native weapon return of 40 becomes 10 at 25% before
  shields, assuming defenses do not change it. Enemy return damage stays native.
- When a companion attacks a player, the player's return hits the companion, not
  its owner. A companion's own reflection still cannot damage its owner.
- Native on-hit burn, frostbite, poison and other debuffs retain their own
  application conditions, duration, stacking, resistance and immunity. Their
  damage uses the current allied-damage percentage once per tick. Synchronous
  electric/Plasma stack damage can resolve inside the exact caster/target's
  admitted debuff operation. Another debuff-damage layer cannot recursively
  damage the team. Burning-death explosions can hit other players once per
  nested chain; a second burning-death explosion cannot recurse into more
  allied kills. Ordinary enemy effects keep native behavior.
- Off/reset or 0% rejects new player-team debuffs and blocks ongoing player-team
  damage ticks, including the native all-faction debuff masks. Already-applied
  status modifiers retain their native duration; toggling does not rewrite stats
  or reroll items. Native NPC-origin and self effects remain native.
- Confirmed allied deaths produce a localized `SephiriaOne` chat notice naming
  killer and victim. Extra-life revival does not count as a kill. Names have markup
  and control characters removed and are length-limited. The host's language is
  used for broadcast notices. Companion kills name the owner as killer. Logging
  failure cannot interrupt native death. Names and companion ownership are
  captured before native death callbacks. A missing/blank/markup-only/`?` player
  name falls back to its live player slot (or network ID when no valid slot is
  available). Unnamed companions identify their owner; other unnamed allies use
  an explicit NPC fallback. Both EN/KO catalogs contain these labels.

## Revive-all recovery

Use `/one reviveall` or **Combat → Revive all players**. The host can use it while
dead, with friendly fire off, or while unrelated settings are faulted. It is an
immediate action, not a saved setting. Help is available at `/one reviveall help`
and `/one help`. The button and messages have English/Korean translations.

- Calls native `UnitAvatar.Revive(player.MaxHp)` once for every ready dead player
  currently in the host roster. Each player's own finite positive maximum HP is
  used. Living HP, MP, inventories, current location and KDA totals are not reset
  by addon writes. Native revival still performs its normal inventory/event work.
- Native revival restores `IsDead`/HP SyncVars, invokes server revival callbacks,
  clears delayed HP restoration, grants brief revival invulnerability and runs
  `TakeRemoteInventory`/`RpcRevive`. Stock guests restore visible bodies and the
  camera's alternate/spectator target via `OnReviveClientside`. No guest addon or
  custom RPC is needed. The next life starts with no old KDA contributors.
- Repeated use with nobody dead is a no-op. Loading/invalid/departed players and
  native callback failures are reported; other ready candidates are still
  attempted. The host can retry after players finish loading. Authority, exact
  avatar identity, save object, run generation and settlement flags are checked
  around callbacks. A new session/run is never processed by an old action.
- Native `Revive` marks the player alive before its HP/revival callbacks, and
  sends the guest RPC only afterward. During this action, two audited event
  invocation wrappers isolate subscriber exceptions for the exact player being
  restored. Later subscribers and native inventory/protection/RPC work still
  run; callback warnings are reported. Other native/nested avatars retain normal
  callback behavior. Scope changes abort before the remaining native tail.
  This avoids an alive server avatar whose guest never received revival, without
  replaying events or toggling death flags manually.
- The exact admitted friendly-fire player's death skips
  `PlayerSpawner.HandleDieServerside`, whose only gameplay action is to emit
  `RpcGameOver` when all connected players are dead. This prevents a final team
  kill (including reflection/owned companion damage) from irreversibly ending the
  run, leaving the host able to revive the party. Normal death replication, KDA
  and death effects still run. Enemy/environmental deaths retain native game over.
  If independent recovery compatibility checks fail, native game over remains
  available rather than trapping an all-dead party without a recovery action.
- **Already settled runs cannot safely resume.** Native `ClientGameOver` settles
  quests and resets combat; `UI_GameOverLabel.OnOpened` disables saving and deletes
  the run save, including backups. This is more than a dead flag. The action
  rejects disabled run saves, victory settlement and requested lobby/restart
  transitions. It does not close guests' result screens, recreate saves or undo
  rewards. Start a new run if settlement already occurred before this patch.

There is no automatic revival, forced teleport or new timer. Candidate collection
and readiness checks happen only when the host presses the button or runs the
command. The button reuses the shared service and remains independent of
friendly-fire patch availability and normal setting-write readiness.

## Synchronization and performance

### Player KDA

- A confirmed player death awards one kill to the final attacker and one death
  to the victim. Player-owned companions credit their owner. Reflection and
  ongoing debuff damage use their already-resolved source player.
- Each other player who dealt positive HP, shield or MP-shield damage to the
  victim during that life gets one assist, regardless of hit count. There is no
  time cutoff or damage threshold. Guard/parry costs, invulnerability, denied
  hits, self damage, NPC/system damage and zero-percent hits earn no assist.
- Extra-life prevention is not a death and does not erase contributions. Actual
  death/revival starts a new life; environmental deaths clear contributions but
  award no K/D/A. Killing a companion or NPC does not award a player kill.
- A change of the enabled flag resets every total and pending contribution.
  Damage-slider edits and repeated `on` commands preserve them. `/one friendlyfire
  reset` turns the option off. New runs clear life contributions but preserve
  totals until a toggle/session end. KDA is not saved in presets.
- Nonzero native `PlayerSpawner.steamID` identifies session scores across rejoin
  and nickname changes. Uninitialized/offline identities use the exact spawner
  (or avatar) object, never a nickname or reusable slot. Rejoining avatars get
  fresh contribution lists. Leaving drops that victim's old life. Existing
  contributions by a departed attacker retain their score identity.
- The host owns counters and uses existing chat to send formatted names/totals.
  Guests need no addon or custom RPC. An unavailable local chat UI does not stop
  scoring. The Combat panel explains KDA/reset behavior in EN and KO.

Native `UnitAvatar.AddReceivedDamage(float)` is called for actual shield,
MP-shield and HP loss before `Die`; `PlayerAvatar`'s override calls the base
method. An exact active friendly-hit scope filters its postfix. No health,
faction, damage or network state is changed by bookkeeping. `Die` snapshots
names/score receipts before callbacks and completes after `IsDead` is confirmed,
including when a later callback throws. Native `Revive(float)` clears the old
life, including forced deaths which bypass `Die`. Epoch checks discard an
in-flight receipt after a toggle or run transition. Session hooks explicitly
handle committed settings, loaded presets, run start, departed players and stop.

Score dictionaries hold no player objects. Weak object-keyed life/fallback maps
release old avatars; memory is bounded by the session's accounts and current
contributors. Repeated contribution checks allocate nothing after their first
registration. There are no added frame scans, timers or combat packets.

### Damage and effects

Only the host needs the addon. UnitAvatar's native health/shield/MP/death writes
and existing `DungeonManager.Chat` RPC replicate outcomes to stock clients. There
are no custom guest messages, assets, altered player stats or faction mutations.

Shared session policy is read at hit time. Changes apply to already-existing
projectiles on their next hit, and new/reconnecting avatars use the same current
settings. Run restarts keep session settings; session replacement resolves a new
scope and its saved preset. Turning off/reset or setting damage to 0% blocks
companion hits against players immediately, including already-launched projectiles
and guard MP costs. The next native AI update calls native target-loss cleanup
for a player target that is no longer hostile. This releases held archer attacks
as well as Collin's attacks before returning to following the owner. Native AI
battle state covers retaliation begun before the first target search. Current
target relations and live
ownership are checked each time, so re-enable, leader transfer and reconnect need
no cached target cleanup. Unload removes all hooks and restores native behavior.

Each native team debuff receives an object-lifetime origin record. Its existing
Update/stack/destruction calls establish a temporary scope; companion death can
clear `NetworkLeader` without turning its remaining burn into unscaled NPC
damage. A destroyed caster/owner or changed companion owner ends that old native
effect on its next update, with expiry damage blocked. A rejoining avatar cannot
inherit the old object's receipt. Weak keys bound records to effect lifetimes;
there is no periodic player scan, delayed replay or permanent player-name cache.

There is no added combat polling, per-player cache, new scene scan or per-hit packet.
The AI hook reads policy only for a player-led companion querying a player target;
it adds no allocations, target scans or timers to the native AI loop.
Hit context is a thread-local value type, restored by a Harmony finalizer even
when callbacks throw. Debuff application allocates one origin record per team
effect; repeated selector/debuff updates allocate nothing in the warm fixture.
Only actual kill notices allocate name text or send extra chat.
The native DamageInstance is not modified by the addon, so shared attacks cannot
carry a reduced multiplier from an ally to an enemy.

## Verification

### Revive all (`0.41.0`, 2026-10-10)

Debug and Release builds pass with zero warnings/errors. All 1,306 shared-runtime
checks, 259 executable combat-hook checks, 69 Bat lifecycle checks, 2,721 catalog
checks and the full portable/installed-game compatibility suite pass with
`-p:DeployMod=false`. Recovery regressions cover dead hosts, distinct native HP,
living-player preservation, repeated use, loading/disconnected/replaced avatars,
invalid HP, independent compatibility gates, callback failures and session/run
changes. Tests confirm scope changes abort before the guest RPC. Independent
review found the native callback exception trap; event isolation was added and
verified before the final review found no further actionable issue.

Installed-game checks verify native death/HP writes, revival events, inventory,
invulnerability, stock guest RPC and camera restoration, plus the irreversible
game-over boundary. These are contract and fixture checks, not a live multiplayer
test. No deployment or game launch was performed. In game, verify both entry
points with a dead host and unmodified guest, a final friendly-fire/reflected or
companion kill, retry after a loading guest becomes ready, a second run and EN/KO
labels. Confirm normal enemy-triggered game over remains unchanged and already
settled runs reject recovery.

### KDA (`0.40.0`, 2026-10-10)

Executable tests cover updated message totals, unique assists, blocked/zero
hits, shields, extra lives and revival, environmental/forced death, NPC exclusion,
companion and debuff credit, exceptions, toggle during damage, same-state and
damage-only commands, run transitions, departure/rejoin, duplicate/changed names,
preset/controller lifetime, and chat-independent scoring. Native contracts
check all three accounting calls precede death, the player's base accounting
call, native identity type and revival method. The warmed contribution fixture
performs 1,000 updates with zero allocated bytes. Live multiplayer verification
must still confirm native kill/assist messages and toggles with stock guests.

Validation: 253 executable combat checks, 1,265 runtime integration checks, and
portable/catalog/installed-game contracts pass. Debug and Release builds have
zero warnings/errors. Independent review found no actionable issues. No deployment
or live multiplayer test was performed.

### Burn/debuff and kill-name audit (`0.39.2`, 2026-10-10)

The installed `PlayerAvatar.Name` reads `playerNameSource`, while other
`UnitAvatar.Name` values use localized `defaultNameKey`. The old logger stripped
empty/invalid names to `?` after death callbacks, without distinguishing players
from unnamed followers. The new death-boundary snapshot avoids both name and
leader teardown; it does not substitute the host's profile name for a guest.

`UnitAvatar.ApplyDebuff` itself has no faction veto. Weapon debuff addons,
`ComboEffect_Debuff` and direct burn/frostbite/electric stats already reach it
after native attacks. Missing effects were found before that boundary in the
six audited item selectors above. A shared helper replaces only their faction
predicate; all other selection/attack instructions remain native. The two
coroutine selectors resolve their generated `MoveNext`, so policy is read when
selection actually runs after a yield. Fire Chakram uses a separate bitmask
form of the same predicate. Unknown selector shapes disable installation.

Native burn/poison/frostbite/Plasma ticks use `ApplyDamage`; electric refresh can
deal damage synchronously during `ApplyDebuff`/`AddStack`. The former blanket
recursion guard rejected the latter inside an allied hit and rejected
`Charm_BurnExplosion` during an allied death. New scopes admit only the audited
caster/target and bounded explosion case. They retain the original reflection
guard and do not mutate pooled `DamageInstance` fields.

Native debuffs outlive their caster until duration/target death; burn even uses
fallback damage when `NetworkAttacker` disappears. That made current-owner-only
classification insufficient. Weak origin records now cover these native
lifetimes, including electric damage triggered by `Destroy`. The game still
creates/removes its own status instances, spawns its native debuff prefab and
replicates HP, status HUD and FX to unmodified guests.

The assembly fingerprint remains
`C57A0DAEAB8E8D0AF7066A344133EEC4C57D8F303FD9E25DA9410FBFC4CF1510`.
Regressions reproduce name teardown, Burn Ring exclusion and companion burn
losing its scale after death. Coverage includes off/zero, 25/100/200%, immediate
electric damage, recursion, ownership transfer, disconnect, replacement avatars,
yielded selectors, exceptions, nested untagged effects, EN/KO fallback names and
unload. Native IL checks verify all selectors, stock effect spawning, stacking,
expiry, damage routes and name sources. All 219 executable combat checks, 1,255
runtime integration checks and the portable/catalog/installed-game contracts
pass. Debug/Release builds have zero warnings/errors. The warmed selector and
tracked-update fixture allocates zero bytes over 1,000 iterations (not a Unity
FPS measurement). Independent review found and verified the caster-loss repair.
No deployment or live game test.

Executable fixtures exercise the production Harmony hooks: off/on, ally/follower/
enemy/self distinction, shields and true damage, 0/50/300%, recursion, reused
damage instances, death/revival, exceptional cleanup, MP integer limits, invalid
derived values, changed IL rejection and unload. Shared-runtime tests cover
command authority, immediate policy reads, run restart, replacement connections,
presets, defaults and failure/reset behavior. Installed-game compatibility checks
verify the actual damage patch locations, native player callback/guard ordering,
HP replication, collision/death/chat paths, shared AI relation consumers and
Collin's native attack/start/stop paths and both archer target-loss handlers.
New fixture regressions cover the missing
player veto, other veto subscribers, NPC protection, shield overflow, companion
ownership, reconnect, existing targets, toggle/zero and broad-mask in-flight hits.
Stateful archer fixtures cover held attacks, target-loss cleanup and owner changes
both before and after the first target search, without clearing peaceful targets
repeatedly or interrupting native attacks against monsters.

Live testing remains required; no deployment was performed. With an unmodified
guest, test host→guest and guest→host melee/projectile hits at 25/100/200%, shields,
death/revival notices, guest quit/rejoin, second run, settings changed while an
attack is in flight, and off/reset. Check enemy-targeted skills still target
enemies outside the audited item list, and verify Combat slider focus/drag/Apply and EN/KO labels in game. With
Collin already attacking a player, turn off/reset and confirm attacks stop and
in-flight hits neither hurt nor spend guard MP; re-enable without respawning Collin.
Also test A attacking B with Thorns or a reflecting shield, both players equipped
with reflection, 25/100/200%, shield/MP-shield overflow, reflected kills and off.
For this follow-up, compare direct burn and each audited item against a monster
and an unmodified guest; include debuff immunity, stack refresh and expiry. Toggle
off/zero while burning, kill the companion caster, then quit/rejoin the caster
and verify old effects cannot become unscaled/ownerless damage. Confirm player,
companion and unnamed-NPC kill labels with Korean and English selected.

Version 0.37.1 verification on 2026-10-09: Debug and Release builds passed without
warnings; 171 executable combat checks, 29 policy/preset checks, 1,250 shared
runtime checks, 69 Bat lifecycle checks, 48 localization checks, 1,449 catalog
checks, and the full portable/native
compatibility suite passed. Independent code review found no remaining issues.
Native reflection checks cover return IDs/types, incoming-source identity,
guard/parry binding, raw weapon formula input, Thorns before hit invulnerability
and pooled damage metadata resets. No live multiplayer test was performed.
