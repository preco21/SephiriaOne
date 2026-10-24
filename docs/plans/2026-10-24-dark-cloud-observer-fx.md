# Dark Cloud observer FX implementation

Approved scope: the user asked to implement the host-only forwarding design in
[the visual investigation](../dark-cloud-pvp-visuals.md), provided unmodified
guests can display the original effect. The installed assembly hash and native
message contract still match that investigation.

## Design

Capture hostile-player admission at `FireLightning` entry, using shared PvP
participant/mode rules. A nested-safe scope survives lethal callbacks that stop
a match, but current connection receipts, cloud identity/owner and floor must
still match when sending. Restore the previous scope in a Harmony finalizer.

Observe the existing `TargetCreateFx` send. Leave its owner send intact and
forward the same position/ice/empowered arguments to other ready, same-floor
observers of the spawned cloud identity. Consume the scope before sending to
avoid recursive forwarding. Snapshot observer connections using a pooled buffer;
revalidate each connection before dispatch. Do not retry ambiguous sends or
create any gameplay entities. Install this cosmetic hook independently so an
unsupported native layout disables forwarding without disabling combat.

## Work and verification

- [x] Add executable native-sender fixtures proving the current owner-only gap.
  Exercise owner/guest/host/spectator routes, variants, suppression, nested and
  lethal callbacks, exceptions, observer churn, reconnect/floor/reset and unload.
- [x] Implement the scoped observer adapter and isolated Harmony installation.
  Validate native sender/receiver/cosmetic layout once and cache the sender
  delegate; no new frame polling, protocol, client script or asset.
- [x] Extend installed-game compatibility checks to validate the production
  adapter against shipped IL, including mutation rejection.
- [x] Run Release build with `-p:DeployMod=false`, policy/native compatibility,
  focused observer tests, existing combat/runtime tests and allocation checks.
- [x] Update findings/index/version and complete independent code review.
  Commit and push after final diff validation; do not deploy.

Live multiplayer rendering/audio remains a separate test: host attacks guest,
guest attacks host, guest attacks guest, third observer; lightning/ice/EX;
cadence-blocked and lethal hits. Stock range rings remain owner-only.
