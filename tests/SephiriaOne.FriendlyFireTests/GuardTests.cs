using SephiriaOne;
using UnityEngine;

internal static class GuardTests
{
    internal static void Run(Action<bool, string> check)
    {
        PlayerAvatar source = null, target = null;
        DamageInstance hit = null;
        void Fresh(int max = 1000)
        {
            FriendlyFireParticipants.Clear(); CombatMatchRuntime.PairRule = null; CombatMatchRuntime.Chaser = null;
            Mirror.NetworkServer.active = true;
            // Keep precision/floor regressions at an explicit 10%; test the default below.
            SessionSettings.FriendlyFireForHit = new(true, 100, balance: true, guardMpPercent: 10);
            source = new(); target = new() { IsGuarding = true, MaxMp = max, Mp = max };
            hit = new() { origin = source, damage = 20 };
        }
        Fresh(); SessionSettings.FriendlyFireForHit = new(true, 100, balance: true); target.ApplyDamage(hit);
        check(target.Mp == 950 && target.Hp == 100 && target.GuardHits == 1,
            "Default balanced guard spends 5 percent maximum MP and retains native block");
        Fresh(10_000_000); target.FinalMp = 9 * 33; target.Mp = 39_700_000;
        check(target.MaxMp < 0, "Fixture reproduces native Final MP multiplication overflow");
        SessionSettings.FriendlyFireForHit = new(true, 100, balance: true); target.ApplyDamage(hit);
        check(target.LastMpCost == 1_985_000 && target.Mp == 37_715_000,
            "Large Final MP stacks still pay five percent of full maximum MP");
        foreach (var sample in new[] {
            (Base: 1000, Final: 9 * 33, Maximum: 3970),
            (Base: 5_000_000, Final: 9 * 60, Maximum: 32_000_000),
            (Base: 10_000_000, Final: 1000, Maximum: 110_000_000),
            (Base: 2_000_000_000, Final: 50, Maximum: int.MaxValue),
            (Base: int.MaxValue, Final: int.MaxValue, Maximum: int.MaxValue),
            (Base: 1000, Final: -100, Maximum: 0),
            (Base: 1000, Final: int.MinValue, Maximum: 0) })
        {
            Fresh(sample.Base); target.FinalMp = sample.Final; target.Mp = int.MaxValue;
            SessionSettings.FriendlyFireForHit = new(true, 100, balance: true);
            target.ApplyDamage(hit);
            check(target.LastMpCost == (int)Math.Ceiling(sample.Maximum * 0.05d),
                "Effective guard maximum is safe for Final MP product/addition overflow: " + sample);
        }
        Fresh(10_000_000); target.FinalMp = 297; target.Mp = 39_700_000;
        target.GuardResistance = 40; target.PerfectGuard = true;
        SessionSettings.FriendlyFireForHit = new(true, 100, balance: true); target.ApplyDamage(hit);
        check(target.LastMpCost == 1_985_000, "Large-pool Guard Efficiency and perfect guard cannot reduce the five percent floor");
        Fresh(1000); target.OnGuardSucceeded = _ => { target.maxMp = 10_000_000; target.FinalMp = 297; target.Mp = 39_700_000; };
        SessionSettings.FriendlyFireForHit = new(true, 100, balance: true); target.ApplyDamage(hit);
        check(target.LastMpCost == 1_985_000, "Final MP and base MP are read after native guard callbacks");
        Fresh(10_000_000); target.FinalMp = 297; target.Mp = 39_700_000;
        SessionSettings.FriendlyFireForHit = new(true, 100); target.ApplyDamage(hit);
        check(target.LastMpCost == 10, "Balancing off restores native cost even for overflowing maximum MP");
        Fresh(10_000_000); target.FinalMp = 297; target.Mp = 39_700_000;
        hit.origin = new UnitAvatar { faction = "enemy" }; hit.targetFactionLayers = -1; target.ApplyDamage(hit);
        check(target.LastMpCost == 10, "PvE does not borrow the overflow-safe balanced guard charge");
        foreach (int max in new[] { 1, 10, 99, 100, 101, 300, 1000, 1000000, int.MaxValue })
        {
            Fresh(max); target.ApplyDamage(hit);
            check(target.LastMpCost == (int)Math.Ceiling(max / 10d), "Guard cost scales actual maximum MP without rounding an exact integer up: " + max);
        }
        Fresh(); target.Mp = 100; target.ApplyDamage(hit);
        check(target.Mp == 0 && target.GuardBroken && !target.IsGuarding, "Remaining MP cannot shrink cost; native guard break still occurs");
        Fresh(); target.MaxMp = 2000; target.ApplyDamage(hit);
        check(target.Mp == 800, "Live maximum MP changes apply immediately");
        Fresh(); target.PerfectGuard = true; target.ApplyDamage(hit);
        check(target.Mp == 900, "Perfect guard's native half cost cannot reduce the configured floor");
        Fresh(); target.GuardResistance = 40; target.ApplyDamage(hit);
        check(target.Mp == 900, "Guard Efficiency cannot reduce the configured floor");
        Fresh(); target.GuardResistance = 40; target.PerfectGuard = true; target.ApplyDamage(hit);
        check(target.Mp == 900, "Stacked Guard Efficiency and perfect guard cannot bypass the configured floor");
        Fresh(); target.GuardResistance = 100; target.ApplyDamage(hit);
        check(target.Mp == 900, "Zero native guard cost still pays the configured positive floor");
        for (int efficiency = 0; efficiency <= 100; efficiency++)
        {
            Fresh(); target.GuardResistance = efficiency; target.ApplyDamage(hit);
            check(target.LastMpCost == 100, "Native float rounding neither reduces nor inflates the floor at Guard Efficiency " + efficiency);
            Fresh(2000); target.GuardResistance = efficiency; target.PerfectGuard = true; target.ApplyDamage(hit);
            check(target.LastMpCost == 200, "Perfect guard keeps the exact larger-pool floor at Guard Efficiency " + efficiency);
        }
        Fresh(int.MaxValue); SessionSettings.FriendlyFireForHit = new(true, 100, balance: true, guardMpPercent: 0.01m); target.ApplyDamage(hit);
        check(target.LastMpCost == 214749, "Large MP pools retain a real fractional charge at the smallest percentage");
        Fresh(); target.OnCalculateDurabilityDamage = _ => 9.601f; target.ApplyDamage(hit);
        check(target.LastMpCost == 100, "Fractional callback reductions stop at the configured floor");
        Fresh(); target.OnCalculateDurabilityDamage = _ => 0.10001f; target.ApplyDamage(hit);
        check(target.LastMpCost == 100, "A callback just above the native reduction grid still pays the floor");
        Fresh(); target.OnCalculateDurabilityDamage = _ => 0.1000005f; target.ApplyDamage(hit);
        check(target.LastMpCost == 100, "A nearby distinct callback float below the floor cannot make a cheap guard");
        Fresh(); target.OnCalculateDurabilityDamage = _ => float.Epsilon; target.ApplyDamage(hit);
        check(target.LastMpCost == 100, "Positive tiny callback amounts still pay the full configured floor");
        Fresh(); int callbackCount = 0; target.OnCalculateDurabilityDamage = cost => { callbackCount++; return cost * 0.75f; }; target.ApplyDamage(hit);
        check(target.Mp == 900 && callbackCount == 1, "Native durability callback still runs once while its reduction respects the floor");
        Fresh(); target.OnCalculateDurabilityDamage = _ => 10.001f; target.ApplyDamage(hit);
        check(target.LastMpCost == 101, "A genuine fractional callback surcharge above the floor rounds upward");
        Fresh(); target.OnCalculateDurabilityDamage = _ => 10.1f; target.ApplyDamage(hit);
        check(target.LastMpCost == 101, "An exact native callback grid surcharge avoids an extra rounding unit");
        Fresh(); target.OnCalculateDurabilityDamage = _ => 10.100001f; target.ApplyDamage(hit);
        check(target.LastMpCost == 102, "A distinct callback float above the surcharge grid is not absorbed by tolerance");
        Fresh(); target.OnCalculateDurabilityDamage = cost => cost * 1.25f; target.ApplyDamage(hit);
        check(target.Mp == 875, "Native cost increases above ten still scale proportionally beyond the floor");
        Fresh(); SessionSettings.FriendlyFireForHit = new(true, 100, balance: true, guardMpPercent: 12.5m); target.ApplyDamage(hit);
        check(target.Mp == 875, "Fractional live guard percentage is exact");
        SessionSettings.FriendlyFireForHit = new(true, 100, balance: true, guardMpPercent: 20); target.ApplyDamage(hit);
        check(target.Mp == 675, "Next block uses edited guard percentage without a retained cost");
        Fresh(); SessionSettings.FriendlyFireForHit = new(true, 100, balance: true, guardMpPercent: 0); target.ApplyDamage(hit);
        check(target.Mp == 1000, "Configured zero explicitly waives balanced guard cost");
        Fresh(1); SessionSettings.FriendlyFireForHit = new(true, 100, balance: true, guardMpPercent: 0.01m); target.ApplyDamage(hit);
        check(target.LastMpCost == 1 && target.GuardBroken, "Positive fractional MP costs round up instead of creating free blocks");
        Fresh(); target.InfiniteMp = true; target.ApplyDamage(hit);
        check(target.LastMpCost == 100 && target.Mp == 900 && !target.GuardBroken, "Native infinite MP cannot waive an admitted PvP defense floor");
        Fresh(); target.InfiniteMp = true; target.Mp = 100; target.ApplyDamage(hit);
        check(target.Mp == 0 && target.GuardBroken, "Infinite MP cannot suppress native guard break after the floor drains available MP");
        Fresh(); target.IsParrying = true; target.ApplyDamage(hit);
        check(target.Mp == 900 && target.GuardHits == 0, "Separate successful parry pays one floor without entering ordinary guard");
        Fresh(); target.IsGuarding = false; target.IsMpShield = true; target.ApplyDamage(hit);
        check(target.Mp == 900 && target.LastMpCost == 100 && target.Hp == 100, "Passive MP shield pays the floor while preserving native full absorption");
        Fresh(); target.IsGuarding = false; target.IsMpShield = true;
        SessionSettings.FriendlyFireForHit = new(true, 100, balance: true, guardMpPercent: 0); target.ApplyDamage(hit);
        check(target.Mp == 980 && target.Hp == 100, "Zero defense percentage preserves native MP-shield damage spending rather than waiving it");
        Fresh(); SessionSettings.FriendlyFireForHit = new(true, 100); target.ApplyDamage(hit);
        check(target.Mp == 990, "Balance off restores native fixed cost immediately");
        Fresh(); SessionSettings.FriendlyFireForHit = new(false, 100, balance: true); target.ApplyDamage(hit);
        check(target.Mp == 1000 && target.GuardHits == 0, "Effective FF off rejects allied attack before MP spending");
        Fresh(); SessionSettings.FriendlyFireForHit = new(true, 0, balance: true); target.ApplyDamage(hit);
        check(target.Mp == 1000, "Zero damage setting rejects before guard cost");
        Fresh(); hit.origin = new UnitAvatar { faction = "enemy" }; hit.targetFactionLayers = -1; target.ApplyDamage(hit);
        check(target.Mp == 990, "Incoming PvE damage retains native guard cost even during balancing");
        Fresh(); var npc = new UnitAvatar { faction = "enemy", IsGuarding = true, MaxMp = 1000, Mp = 1000 }; npc.ApplyDamage(hit);
        check(npc.Mp == 990, "NPC guards retain native cost");
        Fresh(); hit.origin = new UnitAvatar { NetworkLeader = source }; target.ApplyDamage(hit);
        check(target.Mp == 900, "Owned companion attacks use the shared player-victim guard scaling");
        Fresh(); hit.isSystemDamage = true; hit.targetFactionLayers = -1; source.OnAttackUnitBeforeOperation = null; target.ApplyDamage(hit);
        check(target.Mp == 990, "System damage cannot borrow PvP guard scaling");
        Fresh(); source.OnAttackUnitBeforeOperation = null; target.OnAttackUnitBeforeOperation = null; hit.origin = target; hit.targetFactionLayers = -1; target.ApplyDamage(hit);
        check(target.Mp == 990, "Self damage keeps native guard behavior");

        Fresh(); target.OnGuardSucceeded = _ => SessionSettings.FriendlyFireForHit = new(true, 100); target.ApplyDamage(hit);
        check(target.Mp == 990, "Disabling balancing in guard callback restores native cost before spending");
        Fresh(); target.OnGuardSucceeded = _ => SessionSettings.FriendlyFireForHit = new(false, 100, balance: true); target.ApplyDamage(hit);
        check(target.Mp == 990, "Ending PvP during a guard restores native spend for the admitted block");
        Fresh(); target.OnCalculateDurabilityDamage = cost => { SessionSettings.FriendlyFireForHit = new(true, 100, balance: true, guardMpPercent: 25); return cost; };
        target.ApplyDamage(hit); check(target.Mp == 750, "Cost callbacks can edit policy at the final spend boundary");
        Fresh(); SessionSettings.FriendlyFireForHit = new(true, 100); target.OnGuardSucceeded = _ => SessionSettings.FriendlyFireForHit = new(true, 100, balance: true);
        target.ApplyDamage(hit); check(target.Mp == 950, "Enabling balancing inside a guard callback immediately applies current default cost");
        Fresh(); target.OnGuardSucceeded = _ => target.MaxMp = 2000; target.ApplyDamage(hit);
        check(target.Mp == 800, "Cost reads maximum MP after native guard callbacks");
        Fresh(); npc = new UnitAvatar { faction = "enemy", IsGuarding = true, MaxMp = 1000, Mp = 1000 };
        target.OnGuardSucceeded = _ => npc.ApplyDamage(hit); target.ApplyDamage(hit);
        check(target.Mp == 900 && npc.Mp == 990, "Nested PvE guard stays native and outer PvP scope is restored");
        Fresh(); target.OnGuardSucceeded = _ => throw new InvalidOperationException("Guard fixture failure");
        try { target.ApplyDamage(hit); } catch (InvalidOperationException) { }
        check(target.Mp == 1000 && FriendlyFireRuntime.GuardMpCost(10) == 10, "Exception leaves no spend or guard scaling scope");
        Fresh(); source.IsGuarding = true; source.MaxMp = source.Mp = 1000;
        target.OnGuardSucceeded = _ => source.ApplyDamage(new() { origin = target, damage = 20, id = "Weapon_Reflect" }); target.ApplyDamage(hit);
        check(source.Mp == 900 && target.Mp == 900, "Reflected guardable attack uses the same percentage on each defender");

        Fresh(); FriendlyFireParticipants.Select(source); target.ApplyDamage(hit);
        check(target.Mp == 1000, "Unselected victim never spends guard MP");
        FriendlyFireParticipants.Select(source, target); target.ApplyDamage(hit);
        check(target.Mp == 900, "Selecting both players admits guard cost scaling");
        Fresh(); CombatMatchRuntime.PairRule = (_, _) => false; target.ApplyDamage(hit);
        check(target.Mp == 1000, "Tournament bystander cannot be forced to spend MP");
        CombatMatchRuntime.PairRule = (_, _) => true; target.ApplyDamage(hit);
        check(target.Mp == 900, "Current tournament pair uses shared guard cost");
        Fresh(); CombatMatchRuntime.Chaser = target; target.ApplyDamage(hit);
        check(target.Mp == 900, "Play Tag chaser damage reduction does not change guard costs");
        Fresh(); FriendlyFireParticipants.Depart(target); target.ApplyDamage(hit);
        check(target.Mp == 1000, "Departed receipt is rejected before guard");
        FriendlyFireParticipants.Rejoin(target); target.ApplyDamage(hit);
        check(target.Mp == 900, "Rejoined player receives current policy without stale cost state");
        Fresh(); Mirror.NetworkServer.active = false; target.ApplyDamage(hit);
        check(target.Mp == 1000, "Clients cannot make authoritative guard MP writes");
        Fresh(int.MaxValue); target.OnCalculateDurabilityDamage = _ => float.MaxValue; target.ApplyDamage(hit);
        check(target.LastMpCost == int.MaxValue && target.Mp == 0 && target.GuardBroken, "Extreme native cost cannot overflow into MP restoration");
        foreach (float bad in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity, -1 })
        {
            Fresh(); target.OnCalculateDurabilityDamage = _ => bad; target.ApplyDamage(hit);
            check(target.LastMpCost == 100 && target.Mp == 900, "Invalid cost falls back to the configured floor without poisoning MP: " + bad);
        }
        Fresh();
        for (int i = 0; i < 1000; i++) { target.Mp = 1000; target.ApplyDamage(hit); }
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 1000; i++) { target.Mp = 1000; target.ApplyDamage(hit); }
        long bytes = GC.GetAllocatedBytesForCurrentThread() - before;
        check(bytes == 0, "Warmed guard scaling adds no per-hit allocations");
        Console.WriteLine("Balanced guard: " + bytes + " bytes / 1000 warmed blocks");
        CombatMatchRuntime.PairRule = null; CombatMatchRuntime.Chaser = null;
        SessionSettings.FriendlyFireForHit = default;
    }
}
