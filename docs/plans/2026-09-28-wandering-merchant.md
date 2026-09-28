# Extra Wandering Merchant implementation plan

User scope: at most one hostile Wandering Merchant in a random normal dungeon room
per floor, behind an off-by-default host toggle. Chance is configurable 0–100%,
default 25%; the first eligible encounter is guaranteed each new run. Added
merchants have 3× native scaled HP. Guests use the original game. Boss-only
floors, towns, lobby, training, hidden/pocket floors and special rooms are excluded.

1. Reuse the shared command, snapshot, panel and preset infrastructure. Commands:
   `/one merchant on|off|status|reset` and `/one merchant chance N`; save through `/one save`.
2. React to the SDK's completed floor-generation event, plus explicit settings
   refreshes. Validate host/run/floor ownership and native room metadata. Never scan
   rooms every frame or regenerate anything on player join.
3. Use an existing Baba merchant prefab and native stock container. Select a native
   faction hostile to Player in the current scenario; use Aggressive targeting and
   disable dialogue. Give the instance a unique social ID, not a natural NPC's ID.
4. Reserve a namespaced per-floor marker in CurrentRun before rolling/spawning. Duplicate
   callbacks, reconnects, toggling and revisits cannot create another merchant.
   Register both actor and newly created stock container for native floor teardown.
   Mark the first successful encounter separately; a failed spawn does not consume
   that guarantee for subsequent floors. New native run saves reset the guarantee.
5. Track exact actor references. Skip only their native crime check and prevent
   their damage handler from changing shared faction relations. Keep normal death,
   loot and battle cleanup. Turning the option off stops future spawning; surviving
   addon merchants retain their exemption until teardown.
6. Verify lifecycle, failed initialization, natural-NPC isolation, safe placement and
   native IL contracts with fixture tests and installed-game inspection. Build with
   deployment disabled, independently review, document limitations, commit and push.

No custom network component, asset, RPC, item or guest-side code is introduced.
Gameplay verification with unmodified guests remains a separate manual check.
