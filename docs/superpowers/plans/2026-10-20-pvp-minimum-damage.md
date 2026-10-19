# PvP minimum positive damage

## Design and scope

Apply the user's one-damage minimum to positive finite resolved PvP damage at
the existing pre-shield scaling boundary. Keep fractional results above one
unchanged: rounding every hit up would unnecessarily alter normal balancing.
Zero-percent policy, native vetoes, zero/invalid resolved values, exhausted
budgets and rejected effect pulses must remain harmless. Native shield absorption
and later armor reductions must not receive a second minimum.

Balanced hits reserve at least enough pre-role budget to deliver one damage after
the incoming role modifier. Any excess over available fractional credit remains
as debt repaid by refill, so tiny hits cannot bypass the damage budget. Existing
receipt/epoch/life/floor cleanup and refund handling own this state.

## Implementation and verification

- [x] Reproduce 0.05% scaling to a zero-cost native MP shield hit with executable
  Harmony fixtures in `MinimumDamageTests.cs`.
- [x] Floor valid positive results in `FriendlyFireRuntime.Scale`; add an optional
  minimum to `FriendlyFireDamageBudget.Take` and pass `1 / incomingScale` from
  `FriendlyFireBalanceBudget`. Preserve debt and zero admission.
- [x] Verify raw/derived zero and invalid inputs, native shields, vetoes, role
  modifiers, reflection, companions, nested callbacks, pulses and repeated tiny
  hits. Update historical sub-one expectations while keeping independent
  frequency-scaling arithmetic assertions.
- [x] Run friendly-fire fixtures, Release build, portable/native contracts and
  shared runtime with `-p:DeployMod=false`. Update feature evidence/version.
- [x] Review, conventional commit and push. No deployment or live-game launch.
