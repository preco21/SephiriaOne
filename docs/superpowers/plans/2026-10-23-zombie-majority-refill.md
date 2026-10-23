# Zombie Majority Refill Tuning

**Goal:** Slightly reduce dynamic Zombie refill after Zombies become the majority,
without weakening minority Zombies or adding combat-loop work.

**Design:** Reuse `ZombieDynamicBalance`'s bounded smoothstep and one-second
composition smoothing. Change only its majority refill coefficient from -0.4
to -0.5. At parity the multiplier stays 1; at 3:1 it moves from 0.80 to 0.75;
at 5:1 from about 0.70 to 0.63. The asymptotic floor becomes 0.5. Incoming-damage
curves, static settings, budget history and session lifetimes stay unchanged.
No new setting, network message, allocation or per-hit calculation is required.

## Implementation and verification

- [x] Update ratio expectations independently from Human protection; verify
  continuity, bounds and real damage-budget refill against the old implementation.
- [x] Adjust the cached curve coefficient and EN/KO help ranges.
- [x] Run runtime, combat, localization/policy tests and a non-deploying Release build.
- [x] Record findings in the feature document/index and bump patch version.

Finish by inspecting the focused diff, committing conventionally and pushing.
Do not deploy.
