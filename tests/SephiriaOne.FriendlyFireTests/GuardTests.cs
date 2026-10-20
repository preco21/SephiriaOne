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
            // Keep precision/reduction regressions at an explicit 10%; test the default below.
            SessionSettings.FriendlyFireForHit = new(true, 100, balance: true, guardMpPercent: 10);
            source = new(); target = new() { IsGuarding = true, MaxMp = max, Mp = max };
            hit = new() { origin = source, damage = 20 };
        }
        Fresh(); SessionSettings.FriendlyFireForHit = new(true, 100, balance: true); target.ApplyDamage(hit);
        check(target.Mp == 920 && target.Hp == 100 && target.GuardHits == 1,
            "Default balanced guard spends 8 percent maximum MP and retains native block");
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
        check(target.Mp == 950, "Perfect guard retains native half cost");
        Fresh(); target.GuardResistance = 40; target.ApplyDamage(hit);
        check(target.Mp == 940, "Guard Efficiency retains its native reduction");
        Fresh(); target.GuardResistance = 40; target.PerfectGuard = true; target.ApplyDamage(hit);
        check(target.Mp == 970, "Guard Efficiency and perfect guard stack before percentage scaling");
        Fresh(); target.GuardResistance = 100; target.ApplyDamage(hit);
        check(target.Mp == 1000, "Genuinely zero native cost stays zero");
        for (int efficiency = 0; efficiency <= 100; efficiency++)
        {
            Fresh(); target.GuardResistance = efficiency; target.ApplyDamage(hit);
            check(target.LastMpCost == 100 - efficiency, "Native float rounding must not charge an extra MP at Guard Efficiency " + efficiency);
            Fresh(2000); target.GuardResistance = efficiency; target.PerfectGuard = true; target.ApplyDamage(hit);
            check(target.LastMpCost == 100 - efficiency, "Perfect guard float rounding must not charge an extra MP at Guard Efficiency " + efficiency);
        }
        Fresh(int.MaxValue); SessionSettings.FriendlyFireForHit = new(true, 100, balance: true, guardMpPercent: 0.01m); target.ApplyDamage(hit);
        check(target.LastMpCost == 214749, "Large MP pools retain a real fractional charge at the smallest percentage");
        Fresh(); target.OnCalculateDurabilityDamage = _ => 9.601f; target.ApplyDamage(hit);
        check(target.LastMpCost == 97, "Genuinely fractional callback costs still round up");
        Fresh(); target.OnCalculateDurabilityDamage = _ => 0.10001f; target.ApplyDamage(hit);
        check(target.LastMpCost == 2, "A real callback amount just above the native grid still rounds up");
        Fresh(); target.OnCalculateDurabilityDamage = _ => 0.1000005f; target.ApplyDamage(hit);
        check(target.LastMpCost == 2, "A nearby callback float distinct from the native reduction is preserved");
        Fresh(); target.OnCalculateDurabilityDamage = _ => float.Epsilon; target.ApplyDamage(hit);
        check(target.LastMpCost == 1, "Positive tiny callback amounts never become free guards");
        Fresh(); target.OnCalculateDurabilityDamage = cost => cost * 0.75f; target.ApplyDamage(hit);
        check(target.Mp == 925, "Native durability-cost callback still adjusts the cost once");
        Fresh(); SessionSettings.FriendlyFireForHit = new(true, 100, balance: true, guardMpPercent: 12.5m); target.ApplyDamage(hit);
        check(target.Mp == 875, "Fractional live guard percentage is exact");
        SessionSettings.FriendlyFireForHit = new(true, 100, balance: true, guardMpPercent: 20); target.ApplyDamage(hit);
        check(target.Mp == 675, "Next block uses edited guard percentage without a retained cost");
        Fresh(); SessionSettings.FriendlyFireForHit = new(true, 100, balance: true, guardMpPercent: 0); target.ApplyDamage(hit);
        check(target.Mp == 1000, "Configured zero explicitly waives balanced guard cost");
        Fresh(1); SessionSettings.FriendlyFireForHit = new(true, 100, balance: true, guardMpPercent: 0.01m); target.ApplyDamage(hit);
        check(target.LastMpCost == 1 && target.GuardBroken, "Positive fractional MP costs round up instead of creating free blocks");
        Fresh(); target.InfiniteMp = true; target.ApplyDamage(hit);
        check(target.LastMpCost == 100 && target.Mp == 1000 && !target.GuardBroken, "Native infinite-MP behavior remains in UseMp");
        Fresh(); target.IsParrying = true; target.ApplyDamage(hit);
        check(target.Mp == 1000 && target.GuardHits == 0, "Separate parry path is not charged as a guard");
        Fresh(); target.IsGuarding = false; target.IsMpShield = true; target.ApplyDamage(hit);
        check(target.Mp == 988 && target.LastMpCost == 0, "MP shield consumption still follows resolved damage, not guard percentage");
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
        target.ApplyDamage(hit); check(target.Mp == 920, "Enabling balancing inside a guard callback immediately applies current default cost");
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
            check(target.LastMpCost == 0 && target.Mp == 1000, "Invalid cost cannot poison MP: " + bad);
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
