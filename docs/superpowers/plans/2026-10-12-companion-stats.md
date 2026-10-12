# Companion stat modifiers

Goal: expose all six native companion status bonuses through the shared stats
catalog, commands, panel, reconciliation and persistence. The user explicitly
requested companion stats including attack speed; these are a companion-only
extension to the earlier C-menu allowlist, not new C-menu rows.

Native evidence: installed assembly fingerprint still matches domain-findings.
Resources StatusEntity 30684–30689 define attack speed, critical, critical
inheritance, damage, defense and revival speed. UnitAvatar and the three native
companion charm timers read owner stats dynamically. No follower writes or hooks.

Catalog metadata: IsCompanion distinguishes this audited group from visible
menu rows. Commands: companionattackspeed (1–1000 total %), companiondamage
(0–10000 total %), companiondefense (0–10000 points), companioncritical
(0–100%, two decimal places), companioncriticalshare (0–100% of owner's critical
stats), companionrevive (1–1000 total speed %). Total percentages have offset 100;
critical has scale 100. Others use whole units. Follower-name aliases resolve to
canonical names. No new setting until explicitly changed.

- [x] Add failing command/catalog/planner/preset checks for the six names.
- [x] Extend StatCatalog and v21 preset gate, preserving all older schemas.
  Reuse SettingsPanel and CharacterStats without separate UI or sync paths.
- [x] Extend lifecycle fixtures across scale, native input changes, re-entry,
  restart, save/reload and reset. Verify native consumers/read arithmetic and
  owner relationship using installed-code contracts.
- [x] Add EN/KO labels/units; document native fields, semantics and C-menu scope.
- [x] Run portable/native/runtime/localization/performance checks and Debug and
  Release builds with DeployMod=false. Review and version.

Delivery: conventional commit and push per AGENTS after final checks.

Critical inheritance also retains native execution inheritance and native debug
logging. It shares CriticalDamageBonus, not the player's base 50% critical bonus.
HP and other per-artifact stats are prefab/level-specific fields, not additional
global companion owner stats. Shared all-damage/true-damage/defense-penetration
modifiers already apply via native owner reads; keep their existing controls.
No deployment or live gameplay. Tests cannot prove live multiplayer rendering.
