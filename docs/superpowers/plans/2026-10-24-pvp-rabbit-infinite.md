# PvP Rabbit infinite-potion override

**Goal:** Default-on PvP control prevents Rabbit's costume option from bypassing finite PvP potions.

**Design:** Reuse live participant admission and the existing native drink completion. General PvP infinite HP wins when enabled. Otherwise block Rabbit retention for active participants when the new option is on, independent of balancing. Capture admission and recheck at completion to cover death and settings callbacks. Apply the same restriction to Rabbit's associated Tension exemption. Preserve other Rabbit settings and all nonparticipants/PvE.

No inventory snapshot/restore: the user explicitly permits skipping it. Restoring quantities across gifts, purchases, trades and drinks would overwrite legitimate inventory changes. Finite drinks use native consumption; leaving PvP restores only the costume rule.

## Implementation and verification

- [x] Add a failing native-drink regression: Rabbit infinite on, general PvP infinite off, active participant => quantity 3 becomes 2.
- [x] Add `FriendlyFireSettings.BlockRabbitInfinite` (default true), `/one friendlyfire block-rabbit-infinite on|off|reset`, versioned preset persistence and status.
- [x] Gate Rabbit retention at admission/completion through `PvpPotionPolicy.BlocksRabbitInfinite`, and gate its Tension exception. Keep general PvP retention and Survival/MP rules intact.
- [x] Add Combat supplies checkbox, English/Korean copy, conditional costume descriptions and role notices.
- [x] Verify settings precedence, host/guest, Small/Large/Sample, live edits, death, admission loss, all shared modes and preset compatibility.
- [x] Run Release build and affected fixture/native-contract suites with `-p:DeployMod=false`, review, update docs/version, Conventional Commit and push. Never deploy.
