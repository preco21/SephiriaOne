# PvP Max HP normalization

Added in 0.108.0 (2026-11-02). Host settings in **PvP 밸런싱 (기본)** /
**PvP Balance** include **최대 HP 보정 / Max HP normalization**. It defaults
off, independently of the main balancing switch.

## Settings and curve

```text
/one friendlyfire max-hp on
/one friendlyfire max-hp limits 100 50 100
/one friendlyfire max-hp off
/one friendlyfire max-hp reset
/one friendlyfire status
/one save
```

The three inputs are **baseline**, **soft additional HP**, and **hard additional
HP**. Defaults are 100 / 50 / 100. Baseline accepts 1..10000, caps accept
0 <= soft <= hard <= 10000, with up to two decimals. Edits are atomic. Off keeps
custom values; reset restores off and the defaults. Preset v59 stores nondefault
values; older presets inherit the default-off policy. Controls do not stop a
match, reset K/D/A, or replenish damage budgets.

Effective maximum is `baseline + PvpStatLimit(native maximum - baseline)`.
Native values below the baseline stay unchanged. The shared curve is documented
in [Critical Damage](pvp-critical-damage.md#shared-curve): the hard cap bounds the
**effective** bonus, rather than abruptly clamping the raw bonus at that input.

| Native Max HP | Effective Max HP with defaults |
| --- | --- |
| 80 | 80 |
| 100 | 100 |
| 150 | 150 |
| 200 | 187.5 |
| 250 | 200 |
| 500 | 200 |

Current HP percentage is preserved, as selected by the user: 250/500 becomes
100/200. If damage leaves 60/200, disabling restores 150/500, not the old 250 HP.
Dead/zero HP stays dead/zero; native overheal ratios remain intact. The native
curse maximum is an integer, so a limited cursed maximum rounds down.

## Scope and synchronization

The feature requires its toggle, PvP balancing and actual shared combat
participation. It covers casual friendly fire, Deathmatch, Tournament, Play Tag
and Zombie Survival, including both Zombie factions. Countdown players and
Tournament spectators keep native maximums. Selection changes, settings changes,
loading, initialization, disconnects, session/run changes and unload restore or
reapply using exact avatar/connection lifetime receipts.

This is a temporary **live maximum**, so it also affects participating players'
PvE interactions while balanced PvP is active. Native damage budgets, healing,
revives and HP-based effects read that same maximum. Outside that scope, native
behavior resumes. Stock guests receive `maxHp`/`cursedMaxHp` and `hp` through the
existing Mirror SyncVars; their native HP bars read those values. No new client
code, assets or network messages are required.

## Implementation boundaries

- `PvpMaxHpProjection` owns only replaced fields. It preserves the current ratio
  when applying/restoring, recomputes after native changes, and never overwrites
  an unexpected external replacement with an obsolete baseline.
- `PvpMaxHpRuntime` follows the existing Combat Mode receipt/lease pattern.
  Snapshot iteration and revision guards handle participant pruning that may
  synchronously stop a match. Native nesting survives scope resets. Stable ticks
  perform no health writes or allocations; the native MaxHp getter is untouched.
- `AddMaxHp` / `AddMaxHpPercent`, HP-curse Apply/Remove, BoneBlood Engage/Disengage
  and player initialization run inside nested native-value scopes. This prevents
  equipment removal, buffs, costume changes and saved initialization from treating
  a projected value as native. BoneBlood's Update recognizes its projected curse
  rather than fighting it every frame; native correction still runs when needed.
- Two HP reads in `PlayerSpawner.SaveCurrentSessionData` use `NativeHpForSave`.
  They serialize current HP at native scale without temporarily writing network
  state or saving an addon marker. No pre-match HP snapshot is persisted.
- Normal `Revive(hpAmount)` stays in effective units. Ending a match restores the
  native maximum before draining pending revivals. Failed native contracts disable
  only this feature; off/reset remain usable, including with an unready guest.

Current installed native evidence: Steam public build 25253592,
Assembly-CSharp SHA-256
`c57a0daeab8e8d0af7066a344133eec4c57d8f303fd9e25da9410fbfc4cf1510`, MVID
`ccf97024-28bd-48f1-9666-c768c2bd4b2b`.
REA identity `ev_b2ff80fd487641fed1eca32253a16cab08bd0d6b79d5bd38673c81845959da36`;
focused member evidence `ev_7f95c87365ec9b91503a4fde1910a8e9e8a902e0295f75e1d436afe77104fb5a`.
Native mutation/save/SyncVar/guest-UI guards and negative contract tests are in
`GameMaxHpTests`. Extracted code and private logs are not committed.

## Verification limits

Source-linked tests cover curve/settings/preset validation, ratio restoration,
native mutation nesting, saves, stale connections/reused IDs, loading, all four
match modes, active Tournament pairs, native recovery ordering and cleanup.
The steady nine-player fixture measured zero allocations and zero health writes
over 1,000 unchanged ticks. UI contracts cover draft isolation, independent
compatibility gating, three-input units, EN/KO strings and layout bounds.

Native contracts and fixtures are not live Unity or guest verification. Test
host/guest HP bars, changing equipment/costume, save/rejoin, cursed HP and mode
exit on the installed release before treating presentation timing as verified.
No deployment was performed.

Release validation: clean build; 18,351 runtime/session checks plus 69 Bat checks;
15,831 executable friendly-fire hook checks; 227 new Max HP settings/preset checks;
75 native compatibility groups; 41 compiled HarmonyX 2.9 API references;
localization and panel suites. Independent review found no concrete defect;
additional percentage-stat, curse-transition and failure-cleanup cases were added
after review. Native mutation hooks are structurally verified against installed
IL; fixture transitions do not execute Unity's actual curse/BoneBlood methods.
