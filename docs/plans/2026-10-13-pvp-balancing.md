# Optional PvP balancing implementation plan

**Goal:** an off-by-default host setting for per-hit PvP softening and the existing
Dark Cloud 0.5-second player-pair limit, shared by casual FF, Deathmatch and Tournament.

**Architecture:** keep native stats untouched; normalize only resolved participant
damage in `FriendlyFireRuntime.Scale`, after native damage calculation and the
host's percentage, before shields. Reuse settings/actions, live hostility,
participant receipts, match overlays and preset storage. No custom guest state.

## Design decisions

- User selected individual-hit softening only: no sustained/burst damage budget,
  rolling DPS estimator or total-damage throttle.
- Roster-median stat rewriting would require observing/restoring many native
  inputs and would affect PvE. A simple hard hit cap would flatten all large hits.
  Use a smooth victim-relative curve instead: for maximum HP `H`, knee `K=.05H`,
  ceiling `C=.20H`, and scaled native hit `D`, return `D` when `D<=K`, otherwise
  `K+(C-K)*(D-K)/(D-K+C-K)`. Larger hits remain stronger with diminishing returns.
- No minimum damage buff or resurrection of blocked/immune hits. Existing native
  defenses, attack speed, proc conditions, criticals, statuses and shields remain.
  Rapid hits and focus fire can still kill quickly; healing/tank builds may still
  be difficult to kill. This is power compression, not full build equality.
- Use actual `MaxHp`, never current HP or the reference 100–200 range. Invalid
  max HP cannot produce invalid output. No shared/stat snapshot or restoration.
- Scope: current permitted player/owned-source damage to a player. PvE, self,
  system damage and other ally types keep their previous path.
- The host's existing damage percentage is applied first, so low percentages
  remain meaningful and large ones cannot bypass the ceiling. A hit below the
  knee is unchanged. 100% is a useful starting percentage, never auto-reset it.
- `balance off` removes both the soft curve and Dark Cloud throttle immediately.
  New/old presets default off. `off` retains preference; `reset` clears it.
  Balance edits do not stop a match or reset KDA; the master toggle still does.
- A balance-policy revision invalidates Dark Cloud windows even for off/on edits
  with no intervening hits. Existing receipt/epoch/floor cleanup remains shared.

## Tasks and verification

- [x] Policy/control: extend FriendlyFireSettings/Command, SessionCombat,
  SessionPreset (v22), Combat checkbox, shared status text and EN/KO catalogs.
  Test parser validity, exact percentage preservation, off/reset, atomic preset
  rejection/migration, policy inheritance and edits during active matches.
- [x] Arithmetic/impact: add small pure `FriendlyFireBalance` curve; use it only
  for permitted player victims. Test multiple HP scales, monotonicity, small hits,
  bounded meaningful extreme hits, fractional settings, defenses/shields,
  reflection, debuffs, artifact procs, companions, repeated hits (no rate budget),
  native PvE and no mutation of pooled damage or player stats.
- [x] Cloud/lifecycle: gate existing throttle with Balance and revision. Test
  default/off, off/on with no strikes, active match overlay and existing epochs.
- [x] Build Debug/Release with `-p:DeployMod=false`; run combat executable,
  runtime/session, portable/native compatibility and alternate Harmony API audit.
  Check warmed arithmetic allocations. Independently review, document findings,
  bump version. Complete delivery with a conventional commit and push; do not
  deploy or start the game.

Evidence: installed `UnitAvatar.ApplyDamage` computes the native minimum clamp
immediately before `Shield`, followed by `MPSHIELD`, super armor and HP writes.
The existing verified scale hook already occupies this boundary. Live balance
and stock-guest presentation remain manual checks, not fixture conclusions.
