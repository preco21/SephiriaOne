# Friendly-fire spell targeting

Implemented in `0.47.1`, 2026-10-12 (Asia/Seoul). This extends the
[artifact audit](friendly-fire-artifacts.md) to offensive magic without changing
global factions or granting new homing abilities. Host installation uses native
projectiles, movement replication, damage/debuff handling and effects for guests.
Live guest rendering and combat still require an in-game check.

## Root cause and native evidence

Inspected installed `Assembly-CSharp.dll` SHA-256:
`C57A0DAEAB8E8D0AF7066A344133EEC4C57D8F303FD9E25DA9410FBFC4CF1510`.
Private decompilation and `sharedassets0.assets` inspection remain outside Git.

`ActiveSkill_Bolt.OnServerUpdate` spawns `EDamageFromType.Magic` bullets for both
single and multiple shots. It enables homing only when the native
`BOLTMAGICHOMING` stat is positive. Some prefabs already enable it. `Bullet.Update`
acquires targets and calls its movement module inside the native server branch.
It rejects same-faction players using `GetHostileFactionLayers(None)` even when
the central friendly-fire damage path would accept them. The previous adapter
only covered artifact bullets with `FromType.None`.

The fix admits these installed Magic damage identities through the same live
hostility policy as artifacts and direct attacks:

| Spell | Native damage ID | Installed skill / projectile evidence |
| --- | --- | --- |
| Water Bolt | `Skill_FireBolt` (intentional native reuse) | Skill 5; projectile GO 58214 already enables homing. |
| Fire Bolt | `Skill_FireBolt` | Skill 6; GO 58528; native homing stat required. |
| Air Bolt | `Skill_AirBolt` | Skill 7; GO 58342; native homing stat required. |
| Earthen Bolt | `Skill_EarthBolt` | Skill 18; GO 58296; native homing stat required. |
| Lightning Bolt | `Skill_LightningBolt` | Skill 19; GO 58291; native homing stat required, also enables chain lightning. |
| Ice Bolt | `Skill_IceBolt` | Skill 29; GO 58343; native homing stat required. |
| Frost Dagger | `Skill_FrostDagger` | Skill 25; `ActiveSkill_FireBullet.turnOnHoming` enabled; GO 58356. |
| Arrow Rain | `Skill_ArrowRain` | Skill 28; `ActiveSkill_ArrowRain.turnOnHoming` enabled; GO 58311. |

These IDs are read from each current bullet, not cached by object or owner.
`Bullet.Initialize` replaces ID/type/owner. `OnDespawn` clears its target and
`DestroySelf` releases the owner and acquired homing flags. No new pooled state
is introduced. Arrow Rain's installed `BlinkToTargetPosition` mover uses cast
placement rather than steering toward `HomingTargetPos`: its shared target filter
is covered, but this patch does not change that native movement behavior.

The inspected projectile prefabs use `NetworkTransformReliable`, server-to-client
direction, synchronized position and `onlySyncOnChange`. Bolt/Frost Dagger's
`BulletMoveModule_UniformVector2` uses `HomingTargetPos` and synchronizes its move
angle. Guests need no new asset or script to receive the resulting positions.
This is source/asset evidence, not a completed live multiplayer test.

## Other audited searches

| Consumer | Change / boundary |
| --- | --- |
| `ActiveSkill_Ball.Update` (Fire Circus, including Root Mage variant) | Wraps its one nearest-point query with the shared offensive scope. Keeps the native squared radius of 5, nearest ranking and fallback direction. |
| `AutoMagicCaster.SearchTarget` | Forward faction check uses the common policy. `Charm_AutoMagic` only feeds it Bolt books. Native box size, nearest ranking and cast/cooldown timers remain. |
| `AutoMagicCaster.Update` | Before its native 1.3-second target refresh, drops a cached player who is no longer eligible. An already committed cast keeps its native fixed position; the central impact path still checks current permission. |
| `DungeonManager.SearchTargetForChainLightning` | Reverse faction check uses the common policy at each hop. Covers Lightning Bolt and Lightning Armor, plus other callers of this same offensive utility. Preserves the 6-unit search, already-hit/original-victim exclusions and nearest ranking. Native damage and RPC chain effects remain. |
| `ActiveSkill_MeteorShower.CreateMeteorCoroutine` | Player hostility participates in the existing enemy-aware landing search; native sample bounds, retries, delay, random fallback and damage remain. |
| `ActiveSkill_FireBulletsToEnemies.FireCoroutine` | Forward filter and scoped player hitbox resolution. Covers legacy homing-volley/perfect-guard spell prefabs using this class. |

The volley originally calls `GetComponent<UnitAvatar>()` directly on overlap
colliders. Player root colliders are on Blockable layer 3, while combat searches
use Topdown1F layer 6. `PlayerAvatar.EquipCostume` assigns the costume hitbox's
`combatBehaviour` to the avatar. The patched volley first preserves the native
component lookup, then resolves **only players** through
`Hitbox.GetCombatBehaviour(0)` on the host. It does not broaden unrelated NPC
lookup or replace any other generic `GetComponent` call.

Audited boundaries left native:

- Ordinary player-input aim, including guest crosshair/controller assistance,
  stays native. Server projectile acquisition uses the supplied aim position.
- Fire Arrow, Tornado and Lightning Boomerang do not enable homing in the
  inspected playable assets. They gain no homing ability from this patch.
- `ActiveSkill_CallLightning.FireCoroutine` is an obsolete non-damaging stub.
  `CharacterBuff_LightningArmor` has no demonstrated current spell consumer:
  inspected references only register its prefab. Actual skill 30 uses
  `ActiveSkill_LightningArmor`, covered by the chain utility above.
- Healing, buffs, summons' spawning rules, mine-specific weapon targeting and
  unknown projectile IDs are not rewritten.

## Shared implementation and lifetime rules

`FriendlyFireTargeting` / `FriendlyFireTargetingHooks` now own the shared scoped
nearest query, homing admission/loss and call-replacement utility. Artifact and
spell hook registries only describe audited offensive consumers. Each rewrite
validates its expected native instruction shape before installation; failure
uses the existing friendly-fire rollback/compatibility handling.

All admitted player pairs use `CanAffectTeam`: host authority, positive damage
scale, current connection/owner receipts, the shared selection list and the
current Tournament pair. Existing homing targets are cleared through the native
clear/reacquire branch when that permission changes. No stale participation
snapshot or extra creature scan is added. Native self/dead/ignored/untargetable
filters, detection range, aiming-point ranking, homing power and movement stay
in place. Direct damage, reflection, debuffs and KDA retain their common paths.

To extend this later, establish a native spell/prefab path first, register only
the relevant offensive selector or exact Magic bullet ID, and add executable
and installed-IL checks. Do not add global input or faction patches to compensate
for a filter local to a spell. Physics queries also require proof that a player's
costume hitbox resolves to its avatar before the faction predicate.

## Verification and remaining manual checks

Debug and Release builds passed with zero warnings/errors and deployment
disabled. Verification passed 923 executable combat checks, 1,698 runtime/session
checks plus 69 Bat lifecycle checks, 27,864 Tournament scheduler checks, and the
complete installed-game compatibility/catalog suite. Review found and resolved
the volley costume-hitbox lookup issue before the final verification.

`SephiriaOne.FriendlyFireTests` executes the real Harmony rewrites against native-
shaped fixtures. Regressions reproduce the original Bolt failure and the costume
collider-resolution failure before their fixes. Checks cover aiming-point versus
enemy ranking, range boundaries, ignored/dead/untargetable candidates, absent
homing, off/zero, selections, Tournament pair transitions, caster/target departure,
companion ownership, pooled identity changes, host-only behavior, chain damage
scaling at 0.1/50/200 percent, unchanged native lookup and uninstallation.
Warmed spell acquisition/loss and automatic-caster checks allocate zero bytes in
the measured fixture loop; native movement/physics cost is not measured there.

`GameFriendlyFireSpellTests` checks installed methods, coroutine bodies, forward
and reverse mask direction, server acquisition/movement ordering, native homing
gates, pooled resets and rejection of changed selectors/collider lookup. Existing
artifact contracts continue to validate the extracted common hooks.

Before a release is treated as gameplay-verified, test host and unmodified guest
Bolt casts with native homing enabled, Frost Dagger, Fire Circus and Lightning
Bolt/Armor; mix monsters with selected/unselected players, change Tournament
pairs, disable friendly fire while a projectile is active, and disconnect/rejoin.
Confirm native-looking movement/effects and exactly one configured damage scale.
Development verification does not deploy the addon or launch the game.
