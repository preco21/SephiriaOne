# PvP potion and interval options

Goal: separate default-off infinite HP potions for active PvP participants, plus
the requested debuff defaults and a separate Plasma shock clock if native damage
metadata permits it. Use existing shared settings, live participation, native
drink completion and per-attacker/victim pulse clocks; no duplicate Harmony hooks,
inventory refunds, temporary stat changes, polling or custom guest state.

1. Add failing native drink fixtures for non-Rabbit players, independent Rabbit
   options, off/end/selection changes, death, unsupported/Sample/MP potions.
2. Generalize only the existing guarded drink context; keep costume-specific
   sharing, MP cost, Survival suppression and Tension restricted to Rabbit.
3. Add command/UI/status/preset flag. Validate live admission and completion;
   no restoration bookkeeping is needed because no persistent item state changes.
4. Verify native Electric/Plasma damage identity. Add separate Plasma shock control
   and clock; defaults Lightning 0.5, Plasma shock 0.9, Dark Cloud 0.7, Wound 1.33,
   Freeze 0.25, Chim 0.25. Preserve explicit saved interval overrides.
5. Test hooks, mode lifecycle, presets, localization and native contracts; review,
   update shared docs, commit/push. Release builds always use DeployMod=false.

Keep the existing Sample exclusion (ID 37); ordinary HP potion IDs 0 and 1 use
native self healing. PvP admission includes active combat, not mode countdowns
or Tournament spectators. Rabbit's independently enabled infinite option remains
effective in PvE even when the PvP toggle is off. Default new toggle is off.
