# 1.1.0 beta combat compatibility

Audited 2026-11-01 against Steam `2436940`, `workshop-beta`, build `26324456`.
This follows the [official DLL mod guide](https://github.com/team-horay/sephiria_modding/blob/main/docs/en/dll-mods.md)
and preserves the existing host-authoritative combat policies. Related behavior:
[PvP balancing](pvp-balancing.md), [shield MP](pvp-shield-mp.md),
[amplification](pvp-amplification.md), [critical damage](pvp-critical-damage.md),
and [elemental limits](pvp-elemental-limits.md).

## Target and evidence

- `Assembly-CSharp.dll` SHA-256:
  `383A0D5EA9218B797D6EACD1F67964A73606BA50572B7AF2D10AC5357B621D01`.
- MVID: `ba32c2d4-ef71-4869-856b-49ec7aea12f4`.
- REA managed identity: `ev_1b33e0797b6da8345bf6c55081ef2d0a945cb80394aecb56a9d470030e480efe`.
- REA selected members (`UnitAvatar`, `WeaponSimple_SwordAndShield`,
  `KeywordDatabase`): `ev_316f2f0c540bd1ec24f7b65cc4fd30f2fe04b0ff5033b7a29893bb04efe276bb`.
- REA registration/Flame Sword members:
  `ev_b4f23c434337d0306598d65f7509532c7b4eb9e970b633f1b3eaa04f114e5dd0`.

Matching private ILSpy C#/IL exports and installed-assembly Harmony instruction
contracts establish the following changes. Metadata identity and static IL are
observations; they do not establish live Unity or guest execution. REA reports
complete selected metadata coverage, with separate partial user-string decoding
because some readable strings have noncanonical terminal flags. Private exports,
game binaries and logs remain outside this repository.

## Defense and shared damage pipeline

The beta extracts `UnitAvatar.ApplyDefense(float finalDamage, int ignoreDefense,
int trueDamage)` from `ApplyDamage`. It now contains native defense reduction,
the live leader's `FOLLOWERDEFENSE`, true damage, `TOUGHNESS`, and the minimum-one
clamp. `ApplyDamage` stores its result in the same damage local immediately
before shield absorption. The old inline `TOUGHNESS` anchor therefore correctly
rejected the beta instead of silently patching an unrelated boundary.

`FriendlyFireTranspiler` accepts the extracted form only with the exact helper
signature, a single caller, matching input/output damage local, and an audited
Toughness/subtraction/minimum/return tail. Scaling remains after defense and
before shields. Sanitization remains at the common MP-shield entry, including
paths that bypass positive-damage calculations. Guard/active-defense costs,
amplification and critical adapters continue in the existing shared pipeline.
The prior inline form remains supported.

The companion stat audit follows the extracted defense consumer and retains
its live `NetworkLeader.GetCustomStatUnsafe("FOLLOWERDEFENSE")` check. The three
other companion damage/critical reads remain in `ApplyDamage`.

## Evasion

`UnitAvatar.GetEvasionChancePercent()` now computes the original capped-stat
logarithmic formula, applies signed `EVASIONRATEREDUCTION` as
`chance * (100 + modifier) / 100`, then clamps the result below at zero.
`ApplyDamage` calls this helper before attacker `IGNOREEVASION` and its RNG roll.

The PvP adapter now caps the helper's final result at the caller. This preserves
the intended 50% maximum stat-derived chance after native modifiers and before
attacker reduction. The helper itself remains unpatched, so the C menu still
shows native probability. Item dodge, guaranteed evasion, callbacks, rolls,
PvE, and session/participant permissions retain their existing behavior.

The helper validator checks all instructions, local relationships, constants,
signed modifier arithmetic and branch targets. Missing/duplicate helper calls,
changed formula/modifier/clamp, and wrong caller stack boundaries reject
installation. The previous inline formula path remains supported.

## Elemental preservation and weapon targeting

`MiniscriptGlobal.Initialize` adds `localPlayerHasCustomStatUnsafe` beside
`anyPlayerHasCustomStatUnsafe`. Each deferred predicate reads `statName` through
`UnitAvatar.GetCustomStatUnsafe`. Both are progression/script reads and now use
the existing native-stat preservation wrapper. Selection ties each method to
its named registration and rejects unknown additional stat-reading predicates;
it does not depend on compiler-generated method ordinals. The beta preservation
set contains 12 readers; the previous build retains its 11-reader set.

Solis Missio still uses the same host/combo/timer/range gates and nearest-point
target selector in `WeaponSimple_SwordAndShield.Update`. Its subsequent native
emission changed from `ComboEffect_FlameSword.FireSword` to
`FireSwordWithBonus(Vector3, bool, bool)` with both bonus flags false. The
targeting adapter needs no implementation change. Its installed contract now
checks the new exact emission signature and false flags as well as the previous
call form, preserving native beta damage semantics.

## Verification and limits

- Friendly-fire executable fixtures: **15,831 checks passed** with
  `-p:DeployMod=false`, including the previous inline damage/evasion layout.
- Six added beta Evasion checks execute the actual caller transpiler: positive,
  negative and below-minus-100 rate modifiers; 50% boundary; attacker reduction;
  native helper/UI output; and balancing-off behavior.
- Native contracts additionally reject altered helper arithmetic, lost clamp,
  missing/duplicate defense calls, and a mismatched damage local, and verify
  scaling still occurs before shields.
- Coordinated production Release build: **0 warnings and 0 errors**, with
  `-p:DeployMod=false`. Installed beta compatibility report: **73 groups passed,
  0 failed**, including defense, Evasion, amplification, critical damage,
  elemental readers, companion stats and weapon targeting.

These results are fixtures and static installed-code contracts. Live host/guest
damage, shield spending, script progression and Solis Missio targeting still
need an in-game smoke test. This combat change performs no deployment and adds
no assets, network messages, per-frame work, or stat persistence.
