using SephiriaOne;
using UnityEngine;

internal static class AmplificationTests
{
    internal static void Run(Action<bool, string> check)
    {
        PlayerAvatar source = null, target = null;
        DamageInstance hit = null;
        void Fresh()
        {
            FriendlyFireRuntime.Clear(); FriendlyFireParticipants.Clear();
            CombatMatchRuntime.PairRule = null; CombatMatchRuntime.Chaser = null;
            CombatMatchRuntime.Zombies.Clear(); CombatMatchRuntime.Humans.Clear();
            Mirror.NetworkServer.active = true; UnityEngine.Random.NextValue = 100;
            SessionSettings.FriendlyFireForHit = new(true, 100, balance: true, amplification: new(true));
            source = new() { AllDamageBonus = 100 }; target = new() { MaxHp = 100000 };
            hit = new() { origin = source, damage = 10 };
        }
        Fresh();
        check(target.ApplyDamage(hit) == EApplyDamageResult.Success && target.ObservedAmplificationDamage == 8.75f,
            "Native bonus factor is limited before resolved damage and PvP budget");
        check(source.AllDamageBonus == 100 && hit.damage == 10,
            "Hit-local curve does not mutate stats or reused damage objects");
        source.AllDamageBonus = 300; target.ApplyDamage(hit);
        check(target.ObservedAmplificationDamage == 10, "Native high-roll contribution caps at configured effective bonus");
        source.AllDamageBonus = -20; target.ApplyDamage(hit);
        check(target.ObservedAmplificationDamage == -2, "Negative costume penalties remain native");
        Fresh(); var companion = new UnitAvatar { NetworkLeader = source, AllDamageBonus = 100 };
        hit.origin = companion; target.ApplyDamage(hit);
        check(target.ObservedAmplificationDamage == 10 && companion.AllDamageBonus == 100 && source.AllDamageBonus == 100,
            "Companion and owner bonuses are summed then capped once, not capped separately");
        Fresh();
        foreach (var originType in new[] { EDamageFromType.DirectAttack, EDamageFromType.Magic })
        {
            hit.fromType = originType; target.ApplyDamage(hit);
            check(target.ObservedAmplificationDamage == 8.75f, "Shared damage source applies amplification curve: " + originType);
        }
        hit.damageType = EDamageType.ElementalEffectDamage; target.ApplyDamage(hit);
        check(target.ObservedAmplificationDamage == 8.75f, "Native elemental-effect damage uses the same bonus boundary");
        Fresh(); SessionSettings.FriendlyFireForHit = new(true, 100, balance: true);
        target.ApplyDamage(hit);
        check(target.ObservedAmplificationDamage == 10, "Amplification toggle defaults off even during balancing");
        SessionSettings.FriendlyFireForHit = new(true, 100, amplification: new(true)); target.ApplyDamage(hit);
        check(target.ObservedAmplificationDamage == 10, "Balancing off immediately restores native bonus");
        SessionSettings.FriendlyFireForHit = new(true, 100, balance: true, amplification: new(true, 20, 40)); target.ApplyDamage(hit);
        check(target.ObservedAmplificationDamage == 4, "Live limit edits affect the next impact");
        Fresh(); var enemy = new UnitAvatar { faction = "enemy" }; enemy.ApplyDamage(hit);
        check(enemy.ObservedAmplificationDamage == 10, "Outgoing PvE retains full bonus during balanced PvP");
        hit.origin = new UnitAvatar { faction = "enemy", AllDamageBonus = 100 }; hit.targetFactionLayers = -1;
        target.ApplyDamage(hit);
        check(target.ObservedAmplificationDamage == 10, "Incoming PvE retains full native bonus");
        Fresh(); companion = new UnitAvatar { NetworkLeader = source }; companion.ApplyDamage(hit);
        check(companion.ObservedAmplificationDamage == 10, "Friendly companion victims are outside player-target stat normalization");
        Fresh(); hit.isSystemDamage = true; hit.targetFactionLayers = -1; source.OnAttackUnitBeforeOperation = null;
        target.ApplyDamage(hit);
        check(target.ObservedAmplificationDamage == 10, "System damage cannot borrow PvP amplification scope");
        Fresh();
        check(FriendlyFireRuntime.LimitAmplification(100) == 100, "No active hit means no stat limit");
        FriendlyFireParticipants.Select(source);
        check(target.ApplyDamage(hit) == EApplyDamageResult.Fail_Absolute && target.ObservedAmplificationDamage == 0,
            "Excluded participant stays protected");
        FriendlyFireParticipants.Select(source, target); target.ApplyDamage(hit);
        check(target.ObservedAmplificationDamage == 8.75f, "Selected pair uses the shared limit");
        Fresh(); CombatMatchRuntime.PairRule = (_, _) => false;
        check(target.ApplyDamage(hit) == EApplyDamageResult.Fail_Absolute, "Countdown and nonmatching pairs stay protected");
        CombatMatchRuntime.PairRule = (_, _) => true; target.ApplyDamage(hit);
        check(target.ObservedAmplificationDamage == 8.75f, "Active Tournament or other mode pair uses shared limit");
        Fresh(); CombatMatchRuntime.Chaser = source; target.ApplyDamage(hit);
        check(target.ObservedAmplificationDamage == 8.75f, "Play Tag refill roles do not bypass amplification curve");
        Fresh(); CombatMatchRuntime.Zombies.Add(source); CombatMatchRuntime.Humans.Add(target);
        CombatMatchRuntime.PairRule = (a, b) => CombatMatchRuntime.Zombies.Contains(a) != CombatMatchRuntime.Zombies.Contains(b);
        target.ApplyDamage(hit);
        check(target.ObservedAmplificationDamage == 8.75f, "Zombie opposing teams share amplification limit");
        Fresh(); FriendlyFireParticipants.Depart(target);
        check(target.ApplyDamage(hit) == EApplyDamageResult.Fail_Absolute, "Departed connection cannot use old balancing scope");
        FriendlyFireParticipants.Rejoin(target); target.ApplyDamage(hit);
        check(target.ObservedAmplificationDamage == 8.75f, "Rejoining uses live amplification without stale stat state");
        Fresh(); target.OnCalculateDamage = _ => SessionSettings.FriendlyFireForHit = new(true, 100);
        target.ApplyDamage(hit);
        check(target.ObservedAmplificationDamage == 10, "Disabling balancing in a callback restores native bonus immediately");
        Fresh(); target.OnCalculateDamage = _ => FriendlyFireParticipants.Select(source);
        target.ApplyDamage(hit);
        check(target.ObservedAmplificationDamage == 10 && target.Hp == 100, "Revoked hit cannot use the curve or deal scaled damage");
        Fresh(); enemy = new UnitAvatar { faction = "enemy" };
        target.OnCalculateDamage = _ => enemy.ApplyDamage(hit); target.ApplyDamage(hit);
        check(enemy.ObservedAmplificationDamage == 10 && target.ObservedAmplificationDamage == 8.75f,
            "Nested PvE stays native and outer PvP scope resumes");
        Fresh(); target.OnCalculateDamage = _ => throw new InvalidOperationException("Amplification fixture failure");
        try { target.ApplyDamage(hit); } catch (InvalidOperationException) { }
        check(FriendlyFireRuntime.LimitAmplification(100) == 100, "Exception restores native amplification outside the hit");
        Fresh();
        void Strike() { Time.time++; target.Hp = 100; target.ApplyDamage(hit); }
        for (int i = 0; i < 1000; i++) Strike();
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 1000; i++) Strike();
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        check(allocated == 0, "Enabled amplification adds no warmed per-hit allocation");
        Console.WriteLine("Balanced amplification: " + allocated + " bytes / 1000 warmed hits");
        Fresh(); SessionSettings.FriendlyFireForHit = default; FriendlyFireRuntime.Clear(); FriendlyFireParticipants.Clear();
    }
}
