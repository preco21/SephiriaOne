# Friendly-fire KDA implementation plan

**Goal:** Add host-owned player kill/death/assist totals to existing friendly-fire chat notices and reset on each enabled-state transition.

**Design:** Keep scores separate from hit admission. Observe actual native received-damage accounting, then finalize scores only on confirmed player deaths. Track unique contributors for the victim's current life (no timeout), including companion owners. Preserve native revival, defenses, scaling and chat transport. Shield/MP-shield absorption counts; guard costs do not. NPC deaths and environmental deaths award no KDA. Any confirmed death clears that victim's pending contributions.

**Identity/lifetime:** Keep session scores by nonzero Steam identity, with object-lifetime fallback for uninitialized/offline players. Names/slots are never identity keys. Reconnecting players keep totals but start a new victim life. Clear contributions at run start and departure; keep totals until a toggle or session end. A generation token prevents callbacks from repopulating reset scores mid-hit. No disk persistence or guest protocol.

**Alternatives:** Post-hit HP comparisons miss nested deaths and shield damage; replacing damage handling risks native behavior. Prefer the existing native AddReceivedDamage boundary plus existing death hooks. Explicit session lifecycle calls cover command, panel, preset initialization and teardown without polling.

## Tasks

- [x] Add executable regressions for KDA formatting, unique assists, native defenses, revival/death, reflection/DoT/companions, lifecycle and reset.
- [x] Add Combat/FriendlyFireKda.cs score/life ledger and extend FriendlyFireNames/Runtime/Hooks for actual damage and death attribution.
- [x] Wire committed toggle/session/run/leave paths and runtime integration tests; update native contracts for received-damage and identity symbols.
- [x] Update EN/KO UI explanation, docs and version. Run combat/runtime/native suites and non-deploying builds and independent review.

Verified 253 combat checks, 1,265 runtime integration checks, native contracts and
Debug/Release builds. Independent review returned no findings. Conventional
Commit/push is the final repository integration step.

All builds use `-p:DeployMod=false`. No game launch, native writes, deployment, or new gameplay synchronization.
