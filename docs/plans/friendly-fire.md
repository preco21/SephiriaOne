# Friendly fire implementation plan

Default-off host policy, 0–300% damage (default 100%), `/one friendlyfire`
commands and a Combat panel with an explicit Apply button for the slider.
Persist through the shared preset mechanism, with no per-connection state.

- [x] Add regression coverage for commands, defaults, presets and authority.
- [x] Add a bounded settings value and shared session/snapshot/preset integration.
- [x] Patch only UnitAvatar's three ally protection checks and resolved damage
  before shield absorption. Preserve self damage, enemies, AI targeting,
  invulnerability, dodging, defenses, death and revival. Do not change factions.
- [x] Send native chat notices for confirmed allied deaths; sanitize names and
  isolate presentation failures from combat. No custom guest RPCs or assets.
- [x] Exercise production hooks against executable fixtures and verify IL
  anchors against the installed game. Cover zero scale, follower protection,
  shield/true damage, repeated death, extra lives, settings changes and cleanup.
- [x] Add EN/KO text and documentation, review, build Debug/Release, run relevant
  suites, then conventional commit and push main. Never deploy.

Enemy-seeking spells retain their native target selection. Friendly fire applies
to actual melee/projectile collisions and other damage delivered to an ally.
This avoids making allies automatic spell targets or changing healing eligibility.
