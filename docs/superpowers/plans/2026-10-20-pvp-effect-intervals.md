# Complete PvP effect interval controls

Extend the shared immutable interval policy, command/UI, and saved presets;
reuse existing per-effect timers and per-attacker/victim pulse windows.

- [x] Audit installed managed metadata and native CharacterDebuff consumers.
  Burn/blue/evolved Burn and Plasma burn retain their existing controls.
  Lightning includes Electric and Plasma's distinct shock component, which emit
  the same native shock flag (Plasma overrides its damage ID in the prefab). Freeze's damage is independent of its stun.
  Wound is periodic; Chim is delayed expiry damage. Dark Cloud has a separate
  native strike gate, covering ice variants and attached burn callbacks.
- [x] Add Wound (1.33s, approximately the former 1/0.75 timing), Lightning (0.25s),
  Freeze damage (0.25s), Chim damage (0.25s), and Dark Cloud (0.5s). Retain original
  four defaults; all controls accept 0.05..10s, independent reset, host authority,
  live edits, and no per-hit formatting or allocation. Direct spells stay native.
- [x] Save new overrides under v34; v1..33 inherit defaults and reject new keys.
  Changing intervals must not reset matches, score/participant state or budgets.
- [x] Keep compact UI using a scrollable two-column interval area; preserve all
  shared participant/combat actions. Add complete EN/KO labels/status/help.
- [x] Exercise commands, persistence, runtime mode edits, independent tick/pulse
  windows, native variants, expiry, stuns, live changes, reconnects and PvE.
  Audit contracts should reject a newly introduced unclassified debuff type.
- [x] Build/tests with DeployMod=false, review, document evidence and exclusions.
  No deployment or game launch. Finish with the authorized conventional commit/push.

Excluded after audit: Magic Wound and StatusInstance only change stats; Fanatic
Flame is an unattributed self-cost CharacterBuff; QliphothDeath is a scripted
death sequence. Weapon/projectile/area attack rates remain their native mechanics
and pass through the shared damage budget; their inflicted debuffs use the above
controls. No universal elemental throttle may accidentally slow direct magic.

Independent review found a long Dark Cloud interval carrying across revival.
Its windows now share existing actor life generations; owner/victim revival and
strikes without damage are regression-tested. Still-dead owners retain their
queued-strike limit.

Serialized asset verification caught Plasma using Debuff_Plasma for both damage
components. Lightning admission now recognizes the native DEBUFF_ELECTRIC marker;
Plasma burn remains solely on its periodic timer. Both paths are fixture- and
contract-tested separately.
