# Remote healing and death audio implementation plan

**Goal:** deliver transient healing/death audio to host and stock guests once per event.
**Architecture:** reuse verified native cosmetic RPCs; no new client code, gameplay
events, assets, polling or state replay. User approved the native impact fallback
for remote deaths because exact `PlayerHit` audio is local-owner-only.

- [x] Trace native audio and FX paths; inspect serialized FMOD references outside
  the repo. Normal regeneration already broadcasts drink and healing sounds.
  Shared heal FX and the red bullet impact FX are silent.
- [x] Rabbit: add sound+particle delivery using the current potion's private
  `RpcCreateDrinkVisual(position)`, after existing actual-HP-gain/lifetime guards.
  Preserve the drinker's one native send. Keep silent FX fallback only when no
  audiovisual send was attempted; never retry an ambiguous send.
- [x] Combat: retain red FX; send the native sound-only `FlameBall` destroy
  presentation to current same-floor observers other than the victim (who already
  hears native death audio). Mirror targeted delivery includes the local host.
  Validate exact base module, expected sound GUID and absent visuals/fragments.
- [x] Regression tests: native payload/receiver mutations, asset guards, observer
  deduplication, host/remote victims, failure isolation, actual heal gain, no source
  replay and existing death lifecycle boundaries.
- [x] Update feature evidence/docs and version. Build Debug/Release with
  `DeployMod=false`; run focused hooks/runtime/native/alternate-Harmony checks;
  independent read-only review. No deployment.

Delivery follows the authorized conventional commit and push workflow.

Native source/asset evidence lives outside the repo. Final documentation records
the exact methods, selected event paths and remaining live multiplayer checks.
