# Bounded friendly-fire reflection

## Findings and design

The installed game applies Thorns (`Ability_Thorns`) during `UnitAvatar.ApplyDamage`
before hit invulnerability begins. `WeaponAddon_Reflect.HandleGuard` returns
`Weapon_Reflect` damage during guard handling. `Charm_VenomSporePouch.OnParry`
returns its counter directly to the incoming source. All three create a separate
damage instance attributed to the reflecting avatar with `fromType=None`, then
use normal `ApplyDamage`. The existing `InFriendlyChain` guard rejects them.

Allow these recognized return effects only when the immediate parent is an admitted
friendly hit, the reflecting avatar is that hit's victim, and the return victim
is its actual source (not the companion's owner). Mark return hits as reflections
so they cannot themselves cause a further allied reflection. Each native effect
may return once; unrelated nested procs and enemy-mediated chains remain blocked.
Keep the current live host policy, scaling after defenses/before shields, numeric
guards, other damage vetoes, ownership protection and native kill replication.
Off/reset and 0% must reject reflected team hits before guard costs and callbacks.

This is narrower than permitting every nested hit or simply allowing two arbitrary
damage levels: neither alternative identifies the genuine native return effects
and both could admit unrelated proc chains. No scheduled retries, RPC additions,
queues, per-player state or damage-instance mutation are needed. Native return
formulas are retained: weapon reflection uses incoming raw damage and its native
bonuses; Thorns uses the native defense/thorns formula. Apply the configured
friendly-fire percentage once to each resolved outgoing return hit.

## Implementation and checks

- [x] Reproduce blocked Thorns/weapon/parry returns in executable fixtures.
- [x] Add effect identification and exact reversed-hit admission to combat runtime.
- [x] Test scale, both players reflecting, independent subsequent hits, blocked
  unrelated procs/third targets, off mid-hit, shields/guard/invulnerability, numeric
  rejection, death credit, companion ownership, pooled reuse and exception cleanup.
- [x] Verify native damage IDs, from-type, callback bindings, direct return targets,
  raw weapon formula and Thorns timing against the installed assembly.
- [x] Update EN/KO help, docs and version; build and run regression suites with
  `DeployMod=false`; independent review.

No deployment. Native inspection and executable fixtures do not prove live
multiplayer behavior; live testing with unmodified guests remains required.

## Result

Version 0.37.1. The new fixture failed on the first scaled Thorns return before
the runtime change. After the fix, 171 executable combat checks passed, including
native guard/parry dispatch, live policy changes, mutual reflection and repeated
independent hits. The full portable/installed-game suite, 1,250 shared runtime
checks, 69 Bat lifecycle checks and 48 localization checks passed. Debug and
Release builds completed without warnings/errors. Independent review found no
actionable issues. No new hooks or per-hit allocations were introduced.

Spore Pouch identification uses its audited native constructor default; a custom
serialized or third-party replacement ID is intentionally not granted admission.
The repository workflow commits and pushes this change without deployment.
