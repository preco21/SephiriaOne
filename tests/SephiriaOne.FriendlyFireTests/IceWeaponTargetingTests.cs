using SephiriaOne;
using UnityEngine;

internal static class IceWeaponTargetingTests
{
    internal static void Run(Action<bool, string> check)
    {
        FriendlyFireParticipants.Clear(); FriendlyFireRuntime.Clear();
        CombatMatchRuntime.PairRule = null;
        Mirror.NetworkServer.active = true;
        SessionSettings.FriendlyFireForHit = new(true, 50);
        var players = Enumerable.Range(0, 9).Select(i => new PlayerAvatar
            { transform = { position = new(i, 0) } }).ToArray();
        var owner = players[0]; var opponent = players[8];
        var bow = new Bullet { NetworkOwner = owner, damageId = "Charm_IceBow",
            Candidates = players.Cast<UnitAvatar>().ToList(), HomingTargetPos = new(8, 0) };
        var hammer = new Charm_IceHammer { NetworkAvatar = owner, Target = opponent };
        var spear = new Charm_IceSpear { NetworkAvatar = owner, Target = opponent };
        var guillotine = new Charm_Guillotine { NetworkAvatar = owner, Target = opponent };
        PlayerInputController.Candidates.Clear(); PlayerInputController.Candidates.AddRange(players);
        PlayerSpawner.MultiplayerList.Clear();
        foreach (var player in players) PlayerSpawner.MultiplayerList.Add(new() { PlayerAvatar = player });

        // Exercise the real adapters with a mutable shared pair-policy boundary.
        // Mode state machines and connection discovery have separate runtime tests.
        void Expect(bool eligible, string reason)
        {
            bow.Update();
            check(bow.HomingTarget == (eligible ? opponent : null), "Frozen Bow homing: " + reason);
            check(hammer.SearchTarget() == eligible && spear.SearchTarget() == eligible &&
                spear.SearchTargetByWeaponDirection() == eligible && guillotine.FindTargetsInRange() == eligible,
                "Ice Weapon native selectors: " + reason);
            check(FriendlyFireRuntime.OffensiveNearestPoint(owner, new(8, 0), .1f) == (eligible ? opponent : null),
                "Frozen Bow launch aim: " + reason);
        }
        Expect(true, "nine-player casual roster has no eight-player limit");
        FriendlyFireParticipants.Select(owner, opponent);
        Expect(true, "explicit participants");
        bool allowed = false;
        CombatMatchRuntime.PairRule = (a, b) => allowed && a == owner && b == opponent;
        Expect(false, "pair denied during a match transition");
        allowed = true; Expect(true, "pair enabled without replacing the artifact");
        allowed = false; Expect(false, "pair becomes allied or leaves the active duel");
        allowed = true; Expect(true, "opposing pair restored");
        opponent.IsDead = true; Expect(false, "death releases existing homing");
        opponent.IsDead = false; Expect(true, "revival reacquires the living opponent");
        FriendlyFireParticipants.Depart(opponent); Expect(false, "retired connection");
        FriendlyFireParticipants.Rejoin(opponent); Expect(false, "rejoin does not reuse explicit selection");
        FriendlyFireParticipants.Select(owner, opponent); Expect(true, "fresh participant selection");
        SessionSettings.FriendlyFireForHit = default; Expect(false, "PvP disabled");
        SessionSettings.FriendlyFireForHit = new(true, 50); Expect(true, "PvP re-enabled on existing artifacts");

        // Audit proves these native scythes are aim-following, not auto-seeking.
        // The addon must not silently convert their aim into another attack mode.
        foreach (string prefab in new[] { "IceScytheBullet", "IceScytheBullet_Flame" })
        {
            var scythe = new Bullet { NetworkOwner = owner, Candidate = opponent,
                damageId = "Charm_IceHammer", spawnedPrefabName = prefab,
                isHomingTargetEnabled = true, autoDetectHomingTarget = false, HomingTargetPos = new(2, 3) };
            scythe.Update();
            check(scythe.HomingTarget == null && scythe.HomingTargetPos.x == 2 && scythe.HomingTargetPos.y == 3,
                prefab + " keeps automatic acquisition disabled and preserves supplied aim");
        }
        foreach (string id in new[] { "Charm_AirSlash", "Charm_IceSpear", "Charm_IceHammer" })
        {
            var straight = new Bullet { NetworkOwner = owner, Candidate = opponent, damageId = id,
                isHomingTargetEnabled = false, autoDetectHomingTarget = false };
            straight.Update();
            check(!straight.isHomingTargetEnabled && !straight.autoDetectHomingTarget && straight.HomingTarget == null,
                id + " does not gain non-native homing");
        }
        CombatMatchRuntime.PairRule = null; FriendlyFireParticipants.Clear();
        SessionSettings.FriendlyFireForHit = default;
        PlayerSpawner.MultiplayerList.Clear(); PlayerInputController.Candidates.Clear();
    }
}
