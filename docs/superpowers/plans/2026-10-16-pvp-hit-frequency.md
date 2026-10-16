# PvP hit-frequency scaling implementation plan

**Goal:** Add optional hit-frequency damage scaling beside the existing PvP damage budget, with a user-confirmed four-hit/second threshold and default off.

**Design:** Estimate recent hits per directed owner/opponent pair using eight fixed 125 ms buckets, covering approximately the last second. Scale softened damage by `min(1, sqrt(4 / recentHits))`, then pass the result into the existing damage budget. A first/slow hit remains full strength; eight recent hits scale to about 71%, sixteen to 50%. Keep this state inside the existing balance window: one eight-counter array allocated on first use per lifetime, no hit lists, extra dictionaries or warmed per-hit allocation. Advance at most eight buckets on a hit, without polling. Preserve all native effects/callbacks.

Positive resolved hit attempts reaching the balance boundary count, including attempts later limited by the budget. Pulse-gated shock/freeze attempts, invalid/zero hits and earlier native misses/blocks do not count. Owned companions, artifacts, reflection and DoT share the owner's directed pair. Existing participant, receipt, life, floor, score and balance lifetimes reset both mechanisms. A frequency-toggle revision resets only frequency history; it must never refill the damage budget or stop a match.

**Controls:** `Combat > PvP balancing > Hit-frequency scaling`; `/one friendlyfire hit-frequency on|off|reset` (reset = off). Requires both friendly fire and PvP balancing. Save/restore with preset v29 only when enabled; older presets inherit off. English/Korean UI, help and status. No native stat writes or new packets/assets; host-only operation.

**Alternatives:** A strict hit cooldown would suppress damage entirely and harm rapid weapons' identity. Live stat changes would alter PvE and not address hit cadence. A decaying estimator or two weighted second buckets uses fewer counters, but makes a steady four-hit/second boundary less predictable. The fixed eight-bucket ring retains the expected 4/8/16-hit rates with bounded storage; timestamps are quantized to 125 ms, so hits age out after 0.875..1 seconds.

## Execution (inline)

- [x] Add failing command/preset and runtime regressions in the existing test projects. Check default off, live on/off/reset, mode overlays, older presets and malformed input.
- [x] Extend `FriendlyFireSettings`, `SessionCombat`, `SessionPreset` and the existing PvP settings panel/catalogs. Keep setting application centralized and preserve all sibling fields.
- [x] Add `FriendlyFireHitFrequency` pure arithmetic and executable damage tests. Integrate before `Budget.Take` in `FriendlyFireBalanceBudget`, after existing pulse gates. Use a separate revision for toggle edits.
- [x] Cover budget composition/refunds, rapid versus slow hits, source/target isolation, companions/procs, lifecycle resets, PvE/off behavior and allocation stability. Verify UI/native call order using installed assembly contracts.
- [x] Build with `-p:DeployMod=false`, run all three test projects, request independent review, update docs/version. Final integration uses the authorized commit/push workflow. Do not deploy or launch the game.

Validation: zero-warning Release build, 3,995 combat checks, 2,266 session plus
69 Bat checks, 83 frequency settings checks and full policy/catalog/installed
contracts including Harmony 2.9 API compatibility. Independent review found no
actionable findings. Live UI/stock-guest gameplay remains unverified.

**Verification commands:** `dotnet build SephiriaOne/SephiriaOne.csproj -c Release -p:DeployMod=false`; `dotnet run --project tests/SephiriaOne.FriendlyFireTests -c Release -p:DeployMod=false`; `dotnet run --project tests/SephiriaOne.RuntimeTests -c Release -p:DeployMod=false`; `dotnet run --project tests/SephiriaOne.Tests -c Release -p:DeployMod=false` with installed managed-directory, compiled-addon and Harmony 2.9 paths as documented in `docs/general-findings.md`.
