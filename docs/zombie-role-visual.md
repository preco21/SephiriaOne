# Zombie role indicator (0.65.0, 2026-10-18)

Zombie Survival automatically gives each Zombie the native `Poison_Charm`
particle loop. It is a cosmetic role marker, including during death and pending
conversion; it does not apply Poison, damage, stat changes, a status HUD icon or
an attacker attribution. No configuration or guest addon is required by the
audited native transport. Live host/stock-guest rendering still needs testing.

## Effect selection and shipped evidence

The installed Poison effect is a small sprite-particle loop, without a startup
sound or startup burst prefab. Its main particles have a 0.25-second lifetime,
0.3125 size, maximum 20 particles and base emission of 4 per second (native
`ElementalParticle.SetMatrix` scales emission to actor area). This preserves the
character silhouette rather than covering it with an opaque overlay. Exact
combat readability and apparent color require live inspection; the damage-text
color is not evidence of the particle texture's color.

`sharedassets0.assets` evidence:

| Object | Native identity |
| --- | --- |
| Poison network prefab | GO 87247, `CharacterDebuff_Poison` component 186524 |
| Network identity | Component 199822; assetId **1290287067**, sceneId 0, serverOnly false |
| Production network manager | Component 222029; `spawnPrefabs[289]` references exact GO 87247 |
| Particle root | GO 53946 `Poison_Charm`; Transform + ElementalParticle |
| Particle children | GO 74436 `Fx`, GO 91048 `Burst`; Transform + ParticleSystem + ParticleSystemRenderer |

The prefab root contains only Transform, NetworkIdentity and
CharacterDebuff_Poison. `startFxPrefab` is null, `startFxEvent` is empty,
`HUD_ID` is empty, and target/attacker/stack are unset. The loop contains no
collision, damage or other script components. Native stack UI is conditional
on an attacker, which the owned carrier never has.

Rejected alternatives: `SpriteFx`, `BuffFx`, ScriptableFx and overlay sprites
are local-only. `SkillController.RpcCreateCastFx` supports a named effect but
also owns the player's shared casting-circle/charge handles; using it as an
infection marker could interrupt normal casting.

## Presentation and gameplay boundary

`ZombieRoleVisuals` creates its own clone of the registered native prefab. It
disables the cloned **component**, leaving the GameObject active for Mirror's
observer handling, before network exposure. It owns the clone before Spawn and
suppresses `CharacterDebuff.OnStartServer` only for that owned instance. The
component's disabled state prevents its automatic server Update, poison ticks,
HUD updates and native death/expiry path. Damage/resistance fields are also zero
on the clone as defense in depth. The original prefab remains untouched.

It never calls `ApplyDebuff`, `InitializeAndSpawn`, `ApplyStatus`, or registers
the carrier in the avatar's gameplay debuff collection. Ordinary Poison and
normal cleansing therefore remain independent of this role marker.

Mirror invokes network start callbacks and serializes components even when they
are disabled. `CharacterDebuff` synchronizes only stack, target and attacker;
`Behaviour.enabled` is not transmitted. Guests instantiate the original enabled
prefab and run its audited client-only loop positioning. On the host the shared
match tick invokes only native `OnUpdate_Client` through a cached delegate.
No new frame controller, per-frame reflection, network transform or snapshot
stream is introduced. Initial observer state includes the target; a delayed
target spawn can resolve through the native SyncVar getter on later updates.

## Ownership and cleanup

- One marker per exact player/connection receipt and match token. Initial role
  selection and infection create it; death/respawn do not remove a current role.
- Readiness loss removes the unavailable target's carrier. The existing match
  loop recreates it when ready, and also reconciles destruction by native
  `HorayNetworkManager.ClearNetworkObjects` during transitions.
- Match conclusion, stop, selection/master-toggle edits, connection replacement,
  run replacement, authority loss and unload use existing match cleanup.
  A rejoining player cannot inherit a former connection's marker.
- Cleanup uses **NetworkServer.Destroy**, rather than ending a gameplay debuff
  or unspawning to a pool. Stock client destruction invokes `CharacterDebuff`
  `OnDestroy`, which stops, hides and destroys the owned particle loop. Network
  delivery is subject to normal transport latency and Unity end-of-frame cleanup.
- If native callbacks end the match during Spawn, retirement waits for Spawn
  to unwind, then destroys the completed identity. Unload keeps the startup
  guard installed while a spawn is still pending. Failed/ambiguous presentation
  stops retries until addon reload, logs once, and leaves match gameplay usable.

`ZombieVisualNative` checks complete game/Mirror code fingerprints once per addon
load. Each new carrier validates current prefab registration, exact type and
components, silent startup, target defaults, and absence of custom network
spawn/unspawn handlers. `NativeAssemblyContract` is shared with the existing
Play Tag explosion adapter; the hashing behavior is unchanged.

## Evidence scope and verification

Static inspection used REA managed metadata plus ILSpy 9.1.0.7988 and UnityPy
asset extraction. Game assembly MVID: `ccf97024-28bd-48f1-9666-c768c2bd4b2b`.

```text
Assembly-CSharp SHA-256:
C57A0DAEAB8E8D0AF7066A344133EEC4C57D8F303FD9E25DA9410FBFC4CF1510
Mirror SHA-256:
C391C91BA860C537692C185B1075F4219EC83FA71A11990A2B51C13876AB7440
sharedassets0.assets SHA-256:
73135788932F461054DA76FDE2BBA1793C64748E77E9DFA3028A1AB6DA47EE36
```

REA evidence identifiers: `ev_c3733c62f61d0c4e651bfc15807d5dc53d2140cdbe2e223042a8a4df533a43a1`
(game inventory), `ev_c7ea5bd391df8b60cbd2f01d21a41257ca96a9d9799c96e189baec610cf73fab`
(selected debuff/visual members), and
`ev_331fb24e477b7df1c0ef2f30eb6dd11a31100f773a6f80334def6305c895bb8e`
(Mirror contracts). Metadata was complete; malformed terminal #US flags limited
some string decoding. Native sources/assets/private logs remain outside the repo.

Executable adapter tests cover owned-only startup suppression, normal Poison,
no server Update, follow/death/respawn, zero steady allocations, stale receipts,
native destruction/replacement, failed contract/instantiate/spawn/update,
reentrant stop/unload and cleanup failure. Runtime tests exercise actual match
creation/conversion/loading/reconnect/conclusion/interruption wiring. Installed
contracts check the native client/server split, serialized fields, disabled
component behavior, prefab spawn, loop positioning and network teardown.

Run the dedicated suite with:

```powershell
dotnet run --project tests/SephiriaOne.ZombieVisualTests -c Release -p:DeployMod=false
```

Verified: release build with zero warnings/errors; 32 executable visual checks,
2,704 runtime integration checks plus 69 Bat lifecycle checks, and 4,871
friendly-fire hook checks. Stable visual updates allocate zero bytes across
10,000 warmed observations. The complete portable/catalog/installed-game runner
passes, including all 33 compiled Harmony API references against installed
HarmonyX 2.9. Independent review found no actionable issue.

Also run the runtime, friendly-fire and installed-game/alternate-Harmony suites
from [general findings](general-findings.md). Live checks remain: host + stock
guest initial role and infection, movement/respawn, late observer, floor loading,
normal Poison alongside the marker, cure, mode stop/rejoin and unload. No game
was launched and no output was deployed for this change.
