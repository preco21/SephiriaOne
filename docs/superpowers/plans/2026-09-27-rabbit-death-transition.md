# Rabbit potion death-transition repair

**Goal:** Preserve infinite consumption and Survival suppression for an admitted
Rabbit HP-potion operation when its player dies before that operation returns.

## Native evidence and scope

- Controller completion invokes Drink, then controller potion events, then item
  decrement. Drink invokes the potion effect before publishing its decrement flag.
- Regeneration invokes the native potion event (and Survival) before HealPercent.
- Die/ForceDie can cancel current actions and destroy/unwield the potion while
  the effect's existing managed call continues. Current addon revalidation rejects
  IsDead or that wield-state cleanup, abandoning both protections mid-operation.
- A completed drink admitted alive retains those two protections across death and
  its wield cleanup, only within the exact captured controller completion. Native
  death/HP state is untouched, dead sources do not share healing, and no effect is
  retried. Connection/run/dungeon/floor/costume/inventory changes still invalidate.
- An eligible late completion already dead is rejected inside the existing native
  catch/cleanup boundary, before MP/effects/events/decrement. Inactive options,
  other costumes and non-HP potions keep native behavior.
- Reuse the completion context for its exact first native Drink, keeping direct
  nested calls separate. Never retain death-specific state past the call stack.
- All builds/tests use -p:DeployMod=false. No deployment. Commit/push main when verified.

## Work

- [x] Reproduce death before Survival, during healing, after native Drink, and
  pre-completion death with real Harmony fixtures, including native wield cleanup.
- [x] Separate accepted completion protection from readiness for a fresh effect;
  reject already-dead eligible completions and retain strict sharing/ownership guards.
- [x] Test death combined with genuine lifetime invalidation, nested calls,
  independent toggles, revived/new drinks and ordinary costume/potion behavior.
- [x] Verify native death/cancellation ordering, Debug/Release builds, potion suite
  and installed-game contracts; independently review the focused change.
- [x] Update feature/history/version, record limits and commit/push without deployment.
