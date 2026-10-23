# PvP elemental stat limits

Implemented in `0.93.0`, 2026-10-24. Related material:
[All Damage Amplified](pvp-amplification.md), [PvP balancing](pvp-balancing.md),
[shared synchronization guide](synchronization-guide.md).

## Controls and curve

The PvP Balance tab has an **Elemental stat limits** toggle and independent
soft/hard inputs for Physical/Kinetic, Flame, Ice and Lightning. The toggle
defaults **on**, with **50 soft / 100 effective hard** for every element.
It requires active PvP participation and the general PvP balancing toggle.
English and Korean controls, help and status messages describe the scope.

```text
/one friendlyfire elements on
/one friendlyfire elements physical limits 50 100
/one friendlyfire elements flame limits 60 120
/one friendlyfire elements ice reset
/one friendlyfire elements off
/one friendlyfire elements reset
/one friendlyfire status
/one save
```

Use `physical`, `flame`, `ice` or `lightning`. Both limits apply atomically;
valid settings are `0 <= soft <= hard <= 10000`, up to two decimals. Turning
the toggle off retains the pairs. Per-element reset restores 50/100; global
elemental reset restores on/50/100 for all four. Edits do not stop a match,
reset K/D/A or replenish damage budgets. Preset v52 stores nondefault pairs
and explicit opt-out; older presets use on/50/100.

`PvpStatLimit` is shared with All Damage Amplified. For native stat `x`, soft
`S`, hard `H`, and `W = H - S`:

- `x <= S`: unchanged, including negative costume penalties.
- `S < x < S + 2W`: `t = (x - S) / (2W)`, result `S + W*t*(2-t)`.
- `x >= S + 2W`: result `H`.

This is the existing amplification curve, with matching slopes at both ends.
The hard setting is the **effective maximum**, not the native-input threshold:
default native 50 → 50, 100 → 87.5, 150+ → 100. Elemental getters return integers,
so the final effective value rounds down (87.5 → 87). Equal limits form a direct
cap without division by zero. These are stat points, not a percentage damage
cap. Native elemental amplification/conversion runs first.

## Scope and accepted fallback

Elemental inputs are baked into weapon, artifact, spell and summon damage before
the final target is known. Unlike All Damage Amplified, there is no single late
elemental multiplier at `ApplyDamage`. `DamageInstance.elementalType` identifies
damage type but does not describe the original formula or separate flat damage,
mixed-element scaling, cached values and conversion. Dividing a final packet by
the current stat would produce incorrect results for those sources.

The user preferred unchanged simultaneous PvE damage, but explicitly accepted
participant stat limiting during PvP if exact per-target preservation was too
complex. This implementation uses that fallback:

- Host combat reads for active participants receive capped elemental values.
  **Their attacks against monsters during balanced PvP are also affected.**
- Raw stats, synchronized stat maps, saved character progress and C-menu values
  remain native. Spectators, unselected players and NPC stat reads are native.
- Disabling elemental limits, balancing or PvP restores subsequent reads
  immediately. Shared session/connection and mode participant checks handle
  casual PvP, Deathmatch, Tournament, Play Tag and Zombie Survival.
- Already-created attacks and summons retain whatever damage the game captured
  at creation. A pre-PvP projectile can retain uncapped damage; one created during
  PvP can retain reduced damage after the mode ends until its native lifetime
  ends. This feature does not destroy, respawn or rewrite those entities.
- Native mechanics using elemental stats for other purposes can also see the
  effective value, such as elemental-scaled extra projectile counts. Generic
  stat derivation/progression paths listed below are deliberately exempt.
- Utility stats such as attack speed, movement, luck and critical chance are not
  directly changed. Stock guests require no addon or custom network state.

The C-menu displays original stats on host and guests; it is not a live view of
the effective combat overlay. Actual host-authoritative damage uses the policy;
client-only predictions or tooltips can retain native values.

## Native boundary and preservation

Installed `Assembly-CSharp.dll` SHA-256:
`C57A0DAEAB8E8D0AF7066A344133EEC4C57D8F303FD9E25DA9410FBFC4CF1510`.
MVID: `ccf97024-28bd-48f1-9666-c768c2bd4b2b`.
REA inventory evidence:
`ev_b2ff80fd487641fed1eca32253a16cab08bd0d6b79d5bd38673c81845959da36`.
Managed inspection used matching local decompilation and installed IL; keep
game binaries and decompiled source outside the repository.

Both public `UnitAvatar.GetCustomStat` overloads reach `GetCustomStatUnsafe`,
then private `GetConvertedElementalDamage(int)`. Indices are physical=0,
fire=1, ice=2, lightning=3. The converter reads raw elemental/amplification
values, selects conversion and returns the final integer. The postfix caps that
result once without writing synchronized dictionaries or recursively changing
conversion inputs.

Original-value readers are protected by replacing only their exact native
getter calls with exception-safe wrappers:

| Reader | Why it must retain original stats |
| --- | --- |
| `UnitAvatar.UpdateHighestElementalBonus` | Avoid capped ties changing synchronized derived bonuses. |
| `PotionEffect_ElementalDamageBoost.SetStatusEffect` | Preserve the native highest-element choice for potion effects. |
| `Charm_AddStatByAnotherStat.OnEnabledEffect`, `OnUpdate`, `OnCharmEffectRefreshed` | Keep cached/derived stats independent of temporary caps. |
| `CostumeUnlockEntity_RequiresStat.UpdateCheck` | Preserve unlock/progression predicates. |
| `UnitStatDependencyManager.CalculateBonus` | Preserve cached stat dependencies. |
| `AvatarStatsHooker.HookStat`, `HasStat`, `GetCustomStatUnsafe` | Preserve C-menu presentation and conditional visibility. |
| Deferred Miniscript `statName` intrinsic | Preserve script predicates; patch the invoked body, not registration. |

Only the read itself is exempt. Later callbacks in the same method still use
the current combat policy. A thread-local nesting counter is restored in
`finally`, including exceptions. Contract checks validate the converter, index
routing and preservation readers before installation. Shared friendly-fire
Harmony ownership provides rollback and unload cleanup.

## Cost and verification

The read path uses shared participation checks, four scalar policies and a few
float operations. The curve reciprocal is cached at edit time. It adds no scene
scan, per-frame reconciliation, packet, restore ledger or projectile tracking.
Shared participant admission still validates/prunes the selected connection
receipts. The executable fixture measured **0 managed bytes for 10,000 warmed
reads**, with fixture participation rather than the production selection table;
this does not measure production CPU cost or in-game frame time.

Coverage includes default/shared curves, independent settings, atomic rejection,
old/new presets, live toggles, native penalties, selection/mode eligibility,
disconnect/rejoin, exception cleanup, original values, EN/KO catalogs and UI
command/draft contracts. Installed-game checks verify converter routing, all
11 preservation readers, unchanged surrounding IL and owned installation.
The addon also passes the installed HarmonyX 2.9 API compatibility check.

No live Unity combat or host/stock-guest session was run for this feature. Verify
damage before/during/after balanced PvP, all four types, selection/spectators,
conversion artifacts, costume switches and pre-existing summons in game.
