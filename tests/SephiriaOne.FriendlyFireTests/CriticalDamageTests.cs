using SephiriaOne;
using UnityEngine;

internal static class CriticalDamageTests
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
            SessionSettings.FriendlyFireForHit = new(true, 100, balance: true, criticalDamage: new(true));
            source = new() { CriticalDamageBonus = 100 }; target = new() { MaxHp = 100000 };
            hit = new() { origin = source, damage = 10, criticalDamageRate = 150, isCriticalAttack = true };
        }
        Fresh();
        check(target.ApplyDamage(hit) == EApplyDamageResult.Success && target.ObservedCriticalDamage == 11.9375f,
            "Native bonus factor is limited before resolved damage and PvP budget");
        check(source.CriticalDamageBonus == 100 && hit.damage == 10 && hit.criticalDamageRate == 150,
            "Hit-local curve does not mutate stats or reused damage objects");
        foreach (var sample in new[] { (-70, -70f), (0, 0f), (50, 50f), (80, 80f), (100, 97.5f),
            (120, 110f), (140, 117.5f), (160, 120f), (10050, 120f) })
        {
            Fresh();
            hit.criticalDamageRate = sample.Item1;
            target.ApplyDamage(hit);
            check(Math.Abs(target.ObservedCriticalDamage - sample.Item2 * 0.1f) < 0.00001f,
                "Native base, signed penalties and additional-bonus curve: " + sample.Item1);
        }
        Fresh(); hit.criticalDamageRate = 150;
        hit.isCriticalAttack = false; target.ApplyDamage(hit);
        check(target.ObservedCriticalDamage == 0, "Ordinary noncritical hits do not gain or lose critical damage");
        hit.isExecutionAttack = true; target.ApplyDamage(hit);
        check(target.ObservedCriticalDamage == 23.875f, "Execution keeps double the limited critical contribution");
        hit.isExecutionAttack = false; hit.isCriticalAttack = true;
        foreach (float multiplier in new[] { 0f, 0.75f, 1.2f, 1.6f, 2f })
        {
            Fresh();
            hit.criticalDamageMultiplier = multiplier; target.ApplyDamage(hit);
            check(Math.Abs(target.ObservedCriticalDamage - Mathf.RoundToInt(119.375f * multiplier) * 0.1f) < 0.00001f,
                "Per-attack multiplier retains native rounding after curve: " + multiplier);
            check(hit.criticalDamageRate == 150 && hit.criticalDamageMultiplier == multiplier,
                "Callbacks and reused attacks retain original critical fields");
        }
        Fresh();
        hit.criticalDamageRate = 350; source.CriticalDamageBonus = 300; target.ApplyDamage(hit);
        check(target.ObservedCriticalDamage == 12, "Native high-roll contribution caps at configured effective bonus");
        hit.criticalDamageRate = 30; source.CriticalDamageBonus = -20; target.ApplyDamage(hit);
        check(target.ObservedCriticalDamage == 3, "Negative costume penalties remain native");
        Fresh(); var companion = new UnitAvatar { NetworkLeader = source, CriticalDamageBonus = 100 };
        companion.FollowerCriticalContribute = 100; hit.origin = companion; target.ApplyDamage(hit);
        check(target.ObservedCriticalDamage == 12 && companion.CriticalDamageBonus == 100 && source.CriticalDamageBonus == 100,
            "Companion and owner bonuses are summed then capped once, not capped separately");
        Fresh();
        foreach (var originType in new[] { EDamageFromType.DirectAttack, EDamageFromType.Magic })
        {
            hit.fromType = originType; target.ApplyDamage(hit);
            check(target.ObservedCriticalDamage == 11.9375f, "Shared damage source applies critical damage curve: " + originType);
        }
        hit.damageType = EDamageType.ElementalEffectDamage; target.ApplyDamage(hit);
        check(target.ObservedCriticalDamage == 11.9375f, "Native elemental-effect damage uses the same bonus boundary");
        Fresh(); SessionSettings.FriendlyFireForHit = new(true, 100, balance: true);
        target.ApplyDamage(hit);
        check(target.ObservedCriticalDamage == 15, "CriticalDamage toggle defaults off even during balancing");
        SessionSettings.FriendlyFireForHit = new(true, 100, criticalDamage: new(true)); target.ApplyDamage(hit);
        check(target.ObservedCriticalDamage == 15, "Balancing off immediately restores native bonus");
        SessionSettings.FriendlyFireForHit = new(true, 100, balance: true, criticalDamage: new(true, 20, 40)); target.ApplyDamage(hit);
        check(target.ObservedCriticalDamage == 9, "Live limit edits affect the next impact");
        Fresh(); var enemy = new UnitAvatar { faction = "enemy" }; enemy.ApplyDamage(hit);
        check(enemy.ObservedCriticalDamage == 15, "Outgoing PvE retains full bonus during balanced PvP");
        hit.origin = new UnitAvatar { faction = "enemy", CriticalDamageBonus = 100 }; hit.targetFactionLayers = -1;
        target.ApplyDamage(hit);
        check(target.ObservedCriticalDamage == 15, "Incoming PvE retains full native bonus");
        Fresh(); companion = new UnitAvatar { NetworkLeader = source }; companion.ApplyDamage(hit);
        check(companion.ObservedCriticalDamage == 15, "Friendly companion victims are outside player-target stat normalization");
        Fresh(); hit.isSystemDamage = true; hit.targetFactionLayers = -1; source.OnAttackUnitBeforeOperation = null;
        target.ApplyDamage(hit);
        check(target.ObservedCriticalDamage == 15, "System damage cannot borrow PvP critical damage scope");
        Fresh();
        check(FriendlyFireRuntime.LimitCriticalDamage(100) == 100, "No active hit means no stat limit");
        FriendlyFireParticipants.Select(source);
        check(target.ApplyDamage(hit) == EApplyDamageResult.Fail_Absolute && target.ObservedCriticalDamage == 0,
            "Excluded participant stays protected");
        FriendlyFireParticipants.Select(source, target); target.ApplyDamage(hit);
        check(target.ObservedCriticalDamage == 11.9375f, "Selected pair uses the shared limit");
        Fresh(); CombatMatchRuntime.PairRule = (_, _) => false;
        check(target.ApplyDamage(hit) == EApplyDamageResult.Fail_Absolute, "Countdown and nonmatching pairs stay protected");
        CombatMatchRuntime.PairRule = (_, _) => true; target.ApplyDamage(hit);
        check(target.ObservedCriticalDamage == 11.9375f, "Active Tournament or other mode pair uses shared limit");
        Fresh(); CombatMatchRuntime.Chaser = source; target.ApplyDamage(hit);
        check(target.ObservedCriticalDamage == 11.9375f, "Play Tag refill roles do not bypass critical damage curve");
        Fresh(); CombatMatchRuntime.Zombies.Add(source); CombatMatchRuntime.Humans.Add(target);
        CombatMatchRuntime.PairRule = (a, b) => CombatMatchRuntime.Zombies.Contains(a) != CombatMatchRuntime.Zombies.Contains(b);
        target.ApplyDamage(hit);
        check(target.ObservedCriticalDamage == 11.9375f, "Zombie opposing teams share critical damage limit");
        Fresh(); FriendlyFireParticipants.Depart(target);
        check(target.ApplyDamage(hit) == EApplyDamageResult.Fail_Absolute, "Departed connection cannot use old balancing scope");
        FriendlyFireParticipants.Rejoin(target); target.ApplyDamage(hit);
        check(target.ObservedCriticalDamage == 11.9375f, "Rejoining uses live critical damage without stale stat state");
        Fresh(); target.OnCalculateDamage = _ => SessionSettings.FriendlyFireForHit = new(true, 100);
        target.ApplyDamage(hit);
        check(target.ObservedCriticalDamage == 15, "Disabling balancing in a callback restores native bonus immediately");
        Fresh(); target.OnCalculateDamage = _ => FriendlyFireParticipants.Select(source);
        target.ApplyDamage(hit);
        check(target.ObservedCriticalDamage == 15 && target.Hp == 100, "Revoked hit cannot use the curve or deal scaled damage");
        Fresh(); enemy = new UnitAvatar { faction = "enemy" };
        target.OnCalculateDamage = _ => enemy.ApplyDamage(hit); target.ApplyDamage(hit);
        check(enemy.ObservedCriticalDamage == 15 && target.ObservedCriticalDamage == 11.9375f,
            "Nested PvE stays native and outer PvP scope resumes");
        Fresh(); target.OnCalculateDamage = _ => throw new InvalidOperationException("CriticalDamage fixture failure");
        try { target.ApplyDamage(hit); } catch (InvalidOperationException) { }
        check(FriendlyFireRuntime.LimitCriticalDamage(100) == 100, "Exception restores native critical damage outside the hit");
        Fresh();
        void Strike() { Time.time++; target.Hp = 100; target.ApplyDamage(hit); }
        for (int i = 0; i < 1000; i++) Strike();
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 1000; i++) Strike();
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        check(allocated == 0, "Enabled critical damage adds no warmed per-hit allocation");
        Console.WriteLine("Balanced critical damage: " + allocated + " bytes / 1000 warmed hits");
        Fresh(); SessionSettings.FriendlyFireForHit = default; FriendlyFireRuntime.Clear(); FriendlyFireParticipants.Clear();
    }
}
