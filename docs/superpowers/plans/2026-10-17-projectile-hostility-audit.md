# Remaining projectile hostility paths

Extend the established host-side friendly-fire targeting adapters. Keep native
homing prerequisites, target ranking/range, projectile motion, damage and network
messages; do not introduce global faction changes or new homing abilities.

1. Trace active weapon/item resources through fire-data subclasses and inspect
   runtime `TurnOnHoming` callers. Record evidence and exclusions in a feature doc.
2. Add regression cases for the newly verified weapon prefab/damage identities,
   enhanced Mini Ballista shots, and weapon-specific nearest/proximity selectors.
3. Move weapon identity matching into a small shared registry; reuse the current
   acquisition/loss policy. Add scoped selectors for Adamah's Oath, Solis Missio,
   Fragment of Nastrond, and cached-mask crossbow mine checks. Recheck current
   ownership/participation rather than caching addon eligibility on pooled bullets.
4. Validate patched behavior and changed-IL rejection against the installed game.
   Check host/guest scope, off/zero, selection and mode transitions, native enemies,
   pool reuse, companion ownership, native homing gates, and steady-state allocation.
5. Build without deploying, run combat and installed-game contracts, obtain an
   independent review, update version/docs, commit and push.

No UI/settings changes or new guest messages. Asset graph evidence is supplemented
by source inspection because some serialized component trees cannot be parsed.
