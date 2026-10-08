# Friendly fire

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
- Enemy-seeking spells, projectile homing, healing and faction relationships
  keep native targeting. This option does not make every automatic spell seek
  allies or add targets to skills with their own enemy-only selection.
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
  unrelated nested ally procs and chains routed through enemies are rejected.
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
- Confirmed allied deaths produce a localized `SephiriaOne` chat notice naming
  killer and victim. Extra-life revival does not count as a kill. Names have markup
  and control characters removed and are length-limited. The host's language is
  used for broadcast notices. Companion kills name the owner as killer. Logging
  failure cannot interrupt native death.

## Synchronization and performance

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

There is no added combat polling, per-player cache, new scene scan or per-hit packet.
The AI hook reads policy only for a player-led companion querying a player target;
it adds no allocations, target scans or timers to the native AI loop.
Hit context is a thread-local value type, restored by a Harmony finalizer even
when callbacks throw. Only actual kill notices allocate text or send extra chat.
The native DamageInstance is not modified by the addon, so shared attacks cannot
carry a reduced multiplier from an ally to an enemy.

## Verification

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
enemies, and verify Combat slider focus/drag/Apply and EN/KO labels in game. With
Collin already attacking a player, turn off/reset and confirm attacks stop and
in-flight hits neither hurt nor spend guard MP; re-enable without respawning Collin.
Also test A attacking B with Thorns or a reflecting shield, both players equipped
with reflection, 25/100/200%, shield/MP-shield overflow, reflected kills and off.

Version 0.37.1 verification on 2026-10-09: Debug and Release builds passed without
warnings; 171 executable combat checks, 29 policy/preset checks, 1,250 shared
runtime checks, 69 Bat lifecycle checks, 48 localization checks, 1,449 catalog
checks, and the full portable/native
compatibility suite passed. Independent code review found no remaining issues.
Native reflection checks cover return IDs/types, incoming-source identity,
guard/parry binding, raw weapon formula input, Thorns before hit invulnerability
and pooled damage metadata resets. No live multiplayer test was performed.
