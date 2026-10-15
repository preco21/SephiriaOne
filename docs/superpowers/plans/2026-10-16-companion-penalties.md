# Friendly-fire companion penalty exemption

**Goal:** Companion deaths caused by admitted friendly-fire damage must not
create native crime, negotiation/reputation penalties or global hostility.

**Architecture:** Reuse the existing nested hit context and its captured owner
receipt. Suppress the exact NPC crime check for that dying companion and damage
instance. Rewrite only the two global-hostility calls in its native damage
callback; preserve native personal targeting, death cleanup and summon removal.
No global faction/stat edits, saved exemption or additional gameplay toggle.

**Scope:** Player-led companions, including the attacker's own companion, only
when the shared friendly-fire admission path allowed the hit. Normal NPCs,
enemies, system/environmental deaths and unrelated/nested deaths stay native.
Keep protection through death callbacks which clear ownership or change settings.
Do not remove pre-existing penalties. Host-only installation; native replication.

## Evidence and alternatives

Installed `DungeonManager.NPCDeadCheckServerside` applies the party crime buff,
writes `CrimeCount`, and for merchant factions writes both relations,
`RedMerchant` and its broadcast. `UnitAI_NewBasic.OnDie` calls it between target
cleanup and StopBattle. `OnDamaged` can mutate global temporary relations and
notify wanderer hostility before death. `Charm_SummonUnit.HandleUnitDie` clears
the leader, so live ownership alone is insufficient during callback dispatch.
Full native-source search found no second crime writer. New-player initialization
reapplies the crime buff from saved `CrimeCount`.

Removing buffs afterward would miss saved counters and hostility; blanket NPC
exemption would change ordinary merchants. Use exact admitted-hit identity.
Keep all native exports outside Git. No deployment or game launch.

## Tasks

- [x] Add representative native penalty fixtures and failing companion-kill test
  to `tests/SephiriaOne.FriendlyFireTests`.
- [x] Add focused hooks/runtime predicate in `Features/Combat/Companions`, install
  under the existing friendly-fire Harmony owner, with native contract guards.
- [x] Cover direct/debuff/reflected hits, own/other companions, callbacks clearing
  ownership/toggling, nested ordinary deaths, reused damage, exceptions, disabled
  policy, player/NPC/PvE boundaries and preserved native cleanup.
- [x] Verify installed game IL and rewrite rejection, merchant-hook coexistence,
  allocation behavior, shared runtime and nondeploy Release build.
- [x] Review, document findings, bump patch version, conventional commit and push.
