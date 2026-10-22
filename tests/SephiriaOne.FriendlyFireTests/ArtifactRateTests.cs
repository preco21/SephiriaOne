using SephiriaOne;
using UnityEngine;

internal static class ArtifactRateTests
{
    internal static void Run(Action<bool, string> check)
    {
        string[] ids = { "Charm_Guillotine", "Charm_ElectricEarring", "Charm_Meteor", "Charm_FireFeather", "Charm_Chakram", "Charm_EchoOfTheGlacier", "Charm_IceBat" };
        DamageInstance Hit(UnitAvatar owner, string id, float damage = 2) => new()
            { origin = owner, id = id, damage = damage, fromType = EDamageFromType.None, damageType = EDamageType.Projectile, targetFactionLayers = -1 };
        foreach (string id in ids)
        {
            FriendlyFireRuntime.Clear(); FriendlyFireParticipants.Clear(); Time.time = 100;
            SessionSettings.FriendlyFireForHit = new(true, 100, balance: true, artifactRate: new(true, 2));
            var source = new PlayerAvatar(); var victim = new PlayerAvatar();
            var hit = Hit(source, id);
            victim.ApplyDamage(hit);
            check(victim.Hp == 98, id + " first artifact impact is admitted");
            for (int i = 0; i < 20; i++)
            {
                Time.time = 100 + i * 0.09f;
                check(victim.ApplyDamage(hit) == EApplyDamageResult.Fail_Absolute && victim.Hp == 98 && victim.Hits == 1,
                    id + " repeated instances reject before native callbacks");
            }
            Time.time = 102; victim.ApplyDamage(hit);
            check(victim.Hp == 96, id + " exact boundary is admitted without moving deadline on rejected attempts");
            victim.IsGuarding = true; int mp = victim.Mp;
            victim.ApplyDamage(hit);
            check(victim.Mp == mp && victim.GuardHits == 0, "Suppressed repeats cannot spend guard MP");
            victim.IsGuarding = false;
            var other = new PlayerAvatar(); other.ApplyDamage(hit);
            victim.ApplyDamage(Hit(new PlayerAvatar(), id));
            check(other.Hp == 98 && victim.Hp == 94, "Per-opponent and per-attacker clocks are independent");
            string sibling = id == ids[0] ? ids[1] : ids[0];
            victim.ApplyDamage(Hit(source, sibling)); check(victim.Hp == 92, "Separate artifacts retain independent clocks");
            SessionSettings.FriendlyFireForHit = SessionSettings.FriendlyFireForHit.Apply(new(false, artifactRate: new(enabled: false)));
            victim.ApplyDamage(hit); check(victim.Hp == 90, "Disabled limiter restores native impact");
            SessionSettings.FriendlyFireForHit = SessionSettings.FriendlyFireForHit.Apply(new(false, artifactRate: new(enabled: true)));
            Time.time = 103; victim.ApplyDamage(hit); check(victim.Hp == 90, "Live enabling retains last admitted hit while off");
            SessionSettings.FriendlyFireForHit = SessionSettings.FriendlyFireForHit.Apply(new(false, artifactRate: new(seconds: 1)));
            victim.ApplyDamage(hit); check(victim.Hp == 88, "Shortening interval uses previous hit timestamp");
            SessionSettings.FriendlyFireForHit = SessionSettings.FriendlyFireForHit.Apply(new(false, artifactRate: new(seconds: 3)));
            Time.time = 104; victim.ApplyDamage(hit); check(victim.Hp == 88, "Lengthening interval does not reset clock");
            FriendlyFireKda.ResetScores(); victim.ApplyDamage(hit); check(victim.Hp == 86, "KDA/session reset retires artifact clocks");
            victim.currentFloorGuid = "next"; victim.ApplyDamage(hit); check(victim.Hp == 84, "Floor change retires artifact clocks");
            FriendlyFireParticipants.Depart(source); FriendlyFireParticipants.Rejoin(source); victim.ApplyDamage(hit);
            check(victim.Hp == 82, "Reconnect starts fresh receipt");
            victim.IsDead = true; victim.Revive(100); victim.ApplyDamage(hit); check(victim.Hp == 98, "Revival retires prior life clock");
            var enemy = new UnitAvatar { faction = "enemy" };
            enemy.ApplyDamage(hit); enemy.ApplyDamage(hit); check(enemy.Hp == 96, "Outgoing PvE never throttled");
            victim.ApplyDamage(Hit(enemy, id)); victim.ApplyDamage(Hit(enemy, id)); check(victim.Hp == 94, "Incoming PvE never throttled");
            SessionSettings.FriendlyFireForHit = new(true, 100, balance: false, artifactRate: new(true, 2));
            victim.ApplyDamage(hit); victim.ApplyDamage(hit); check(victim.Hp == 90, "Balance off restores native damage cadence");
        }
        FriendlyFireRuntime.Clear(); FriendlyFireParticipants.Clear(); Time.time = 200;
        SessionSettings.FriendlyFireForHit = new(true, 100, balance: true, artifactRate: new(true, 2));
        var attacker = new PlayerAvatar(); var target = new PlayerAvatar();
        foreach (string id in new[] { "Charm_PlitviceWaterDrop", "Charm_AirSlash", "Charm_IceBow", "Charm_IceHammer", "Charm_IceSpear", "Charm_PallasCard", "Charm_RedDew", "Ability_Planet", "Weapon_Liberation", "Magic_Bolt", "UnknownArtifact" })
        {
            target.Hp = 100; int hits = target.Hits;
            var hit = Hit(attacker, id); target.ApplyDamage(hit); target.ApplyDamage(hit);
            check(target.Hp <= 98 && target.Hits == hits + 2, "Excluded sources retain both hits: " + id);
        }
        foreach (var from in Enum.GetValues<EDamageFromType>().Where(t => t != EDamageFromType.None))
        {
            target.Hp = 100; int hits = target.Hits; var hit = Hit(attacker, ids[0]); hit.fromType = from;
            target.ApplyDamage(hit); target.ApplyDamage(hit); check(target.Hp <= 98 && target.Hits == hits + 2, "Weapon/book source tag excluded");
        }
        var companion = new UnitAvatar { NetworkLeader = attacker };
        target.Hp = 100; int companionHits = target.Hits;
        target.ApplyDamage(Hit(companion, ids[0])); target.ApplyDamage(Hit(companion, ids[0]));
        check(target.Hp <= 98 && target.Hits == companionHits + 2, "Companions excluded even with matching ID");
        FriendlyFireRuntime.Clear(); target = new PlayerAvatar();
        var artifact = Hit(attacker, ids[0]);
        target.IsInvulnerable = true; target.ApplyDamage(artifact); target.IsInvulnerable = false;
        target.ApplyDamage(Hit(attacker, ids[0], 0)); target.ApplyDamage(artifact);
        check(target.Hp == 98, "Evaded/zero/native-veto hits do not reserve cooldown");
        artifact.elementalType = EDamageElementalType.FireAndLightning;
        target.ApplyDamage(artifact); check(target.Hp == 98, "Guillotine fire variant shares ice clock");
        // A nested admitted artifact can reserve after the outer prefix but before its Scale.
        FriendlyFireRuntime.Clear(); target = new PlayerAvatar(); bool nested = false;
        target.OnCalculateDamage = _ =>
        {
            if (nested) return; nested = true;
            FriendlyFireRuntime.ApplyArtifactDamage(target, Hit(attacker, ids[0]));
        };
        target.ApplyDamage(artifact); target.OnCalculateDamage = null;
        check(target.Hp == 98, "Late recheck prevents nested callback double admission");
        // Budget depletion keeps minimum hits, but cannot bypass this explicitly requested hard rate.
        FriendlyFireRuntime.Clear(); target = new PlayerAvatar();
        for (int i = 0; i < 40; i++) target.ApplyDamage(new() { origin = attacker, damage = 2 });
        float before = target.Hp; target.ApplyDamage(artifact); float after = target.Hp;
        target.ApplyDamage(artifact);
        check(after < before && target.Hp == after, "Empty budget retains first minimum artifact hit and rate-rejects its repeat");
        // No new strings, collections or reflection in a warmed rejected-hit loop.
        for (int i = 0; i < 500; i++) target.ApplyDamage(artifact);
        long bytes = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 1000; i++) target.ApplyDamage(artifact);
        bytes = GC.GetAllocatedBytesForCurrentThread() - bytes;
        check(bytes == 0, "Warmed artifact rejection path allocates zero bytes");
        for (int i = 0; i < 500; i++) { Time.time += 2; target.Hp = 100; target.ApplyDamage(artifact); }
        bytes = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 1000; i++) { Time.time += 2; target.Hp = 100; target.ApplyDamage(artifact); }
        bytes = GC.GetAllocatedBytesForCurrentThread() - bytes;
        check(bytes == 0, "Warmed admitted artifact hits allocate zero bytes");
        Console.WriteLine("Autonomous artifact cadence: 0 bytes/1000 rejected or admitted hits");
        FriendlyFireRuntime.Clear(); SessionSettings.FriendlyFireForHit = default;
    }
}
