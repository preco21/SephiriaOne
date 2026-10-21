using SephiriaOne;

internal static class PerfectGuardTests
{
    internal static void Run(Action<bool, string> check)
    {
        PlayerAvatar source = null, target = null; DamageInstance hit = null;
        void Set(string edit)
        {
            check(FriendlyFireCommand.Parse("/one friendlyfire perfect-guard " + edit, out var command) == FriendlyFireParseResult.Valid,
                "Perfect Guard command accepted: " + edit);
            SessionSettings.FriendlyFireForHit = SessionSettings.FriendlyFireForHit.Apply(command);
        }
        void Fresh(int maxMp = 100, decimal floor = 5)
        {
            FriendlyFireParticipants.Clear(); FriendlyFireRuntime.Clear();
            CombatMatchRuntime.PairRule = null; Mirror.NetworkServer.active = true;
            SessionSettings.FriendlyFireForHit = new(true, 100, balance: true, guardMpPercent: floor);
            source = new(); target = new() { IsGuarding = true, PerfectGuard = true, MaxMp = maxMp, Mp = maxMp };
            hit = new() { origin = source, damage = 20 };
            Set("on");
        }
        Fresh(); target.ApplyDamage(hit);
        check(target.LastMpCost == 10 && target.GuardHits == 1 && target.Hp == 100, "Enabled default doubles native perfect-guard MP, preserving the block");
        foreach (var pair in new[] { ("x1", 5), ("x2", 10), ("x3", 15), ("2.5", 13) })
        {
            Fresh(); Set("multiplier " + pair.Item1); target.ApplyDamage(hit);
            check(target.LastMpCost == pair.Item2, "Multiplier uses native perfect cost with upward fractional rounding: " + pair.Item1);
        }
        Fresh(1000); Set("multiplier x3"); target.ApplyDamage(hit);
        check(target.LastMpCost == 15, "Enabled Perfect Guard bypasses the large-pool shield minimum");
        Fresh(1000); Set("multiplier x1"); target.ApplyDamage(hit);
        check(target.LastMpCost == 5, "x1 retains native Perfect Guard cost below the shield minimum");
        Set("off"); target.ApplyDamage(hit);
        check(target.LastMpCost == 50, "Disabling the exception restores the ordinary shield minimum immediately");
        Fresh(floor: 0); Set("multiplier x3"); target.GuardResistance = 40; target.ApplyDamage(hit);
        check(target.LastMpCost == 9, "Native Guard Efficiency is applied before the Perfect Guard multiplier");
        Fresh(floor: 0); Set("multiplier x2"); target.OnCalculateDurabilityDamage = _ => 3.9f; target.ApplyDamage(hit);
        check(target.LastMpCost == 6, "Artifact-modified native integer MP cost is multiplied after native truncation");
        Fresh(floor: 0); target.GuardResistance = 100; target.ApplyDamage(hit);
        check(target.LastMpCost == 0, "Truly zero native cost is not promoted by multiplier alone");
        Fresh(1000); target.GuardResistance = 100; target.InfiniteMp = true; target.Mp = 0;
        target.ApplyDamage(hit);
        check(target.LastMpCost == 0 && !target.GuardBroken, "Zero native Perfect Guard cost also bypasses the floor and retains native free-guard behavior");
        foreach (float nativeCost in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity, -1f, 0.9f })
        {
            Fresh(1000); target.OnCalculateDurabilityDamage = _ => nativeCost; target.ApplyDamage(hit);
            check(target.LastMpCost == 0 && target.Mp == 1000,
                "Invalid or truncated-zero Perfect Guard cost cannot debit MP or reapply the floor: " + nativeCost);
        }
        Fresh(); target.OnCalculateDurabilityDamage = _ => 20; Set("multiplier x3"); target.ApplyDamage(hit);
        check(target.LastMpCost == 60, "Cost-increasing artifacts are included once in native multiplier basis");
        Fresh(1000); target.OnCalculateDurabilityDamage = _ => 20; target.ApplyDamage(hit);
        check(target.LastMpCost == 40, "Native multiplier replaces the proportional PvP guard charge for Perfect Guard only");
        Fresh(); target.PerfectGuard = false; target.ApplyDamage(hit);
        check(target.LastMpCost == 5, "Ordinary guard does not receive the Perfect Guard multiplier");
        Fresh(); target.IsGuarding = false; target.IsMpShield = true; target.ApplyDamage(hit);
        check(target.LastMpCost == 12, "MP shields retain their existing damage-derived charge");
        Fresh(); target.IsGuarding = false; target.Shield = 100; target.ApplyDamage(hit);
        check(target.LastMpCost == 5, "Automatic shield retains only its existing floor");
        Fresh(); target.parryInvincibleApplied = 1; target.ApplyDamage(hit);
        check(target.LastMpCost == 5, "Weapon parry without a native Perfect Guard stays on the existing floor");
        Fresh(); Set("off"); target.ApplyDamage(hit); check(target.LastMpCost == 5, "Disabling option restores existing balanced guard immediately");
        Set("on"); target.ApplyDamage(hit); check(target.LastMpCost == 10, "Re-enabling applies on the next perfect guard");
        SessionSettings.FriendlyFireForHit = SessionSettings.FriendlyFireForHit.Apply(new(false, balance: false));
        target.ApplyDamage(hit); check(target.LastMpCost == 5, "Disabling balancing restores native MP cost immediately");
        Fresh(); hit.origin = new UnitAvatar { faction = "enemy" }; hit.targetFactionLayers = -1;
        target.ApplyDamage(hit); check(target.LastMpCost == 5, "Incoming PvE retains native perfect-guard cost");
        Fresh(); CombatMatchRuntime.PairRule = (_, _) => false; target.ApplyDamage(hit);
        check(target.Mp == 100, "Disallowed teams cannot trigger the multiplier");
        Fresh(); Mirror.NetworkServer.active = false; target.ApplyDamage(hit);
        check(target.Mp == 100, "Guests cannot apply authoritative multiplier spending");
        Fresh(); target.Mp = 3; target.ApplyDamage(hit);
        check(target.LastMpCost == 3 && target.Mp == 0 && target.GuardBroken, "Insufficient MP spends only available MP and triggers native break");
        Fresh(int.MaxValue, 0); Set("multiplier 100"); target.OnCalculateDurabilityDamage = _ => float.MaxValue;
        target.ApplyDamage(hit); check(target.LastMpCost == int.MaxValue && target.Mp == 0, "Extreme native cost clamps before integer conversion");
        Fresh(floor: 0); target.InfiniteMp = true; target.ApplyDamage(hit);
        check(target.Mp == 90, "Infinite MP cannot waive enabled Perfect Guard payment with the percentage floor disabled");
        Fresh(floor: 0); target.OnMpUsedServerside = _ => target.Mp += 20; target.ApplyDamage(hit);
        check(target.Mp == 90, "Synchronous MP-use refund cannot erase the enabled multiplied charge");
        Fresh(); target.OnGuardSucceeded = _ => Set("off"); target.ApplyDamage(hit);
        check(target.LastMpCost == 5, "Option is read after native guard callbacks so live off is immediate");
        Fresh(); target.OnGuardSucceeded = _ => { FriendlyFireParticipants.Depart(target); FriendlyFireParticipants.Rejoin(target); };
        target.ApplyDamage(hit); check(target.LastMpCost == 5, "Stale receipt falls back to native guard cost");
        Fresh(); var nested = new PlayerAvatar { IsGuarding = true, PerfectGuard = false };
        var proc = new Charm_FrostiumRing { NetworkAvatar = source, Target = nested, Damage = new() { origin = source, damage = 20 } };
        target.OnGuardSucceeded = _ => proc.HandleAttackUnit();
        target.ApplyDamage(hit);
        check(target.LastMpCost == 10 && nested.LastMpCost == 5, $"Nested ordinary guard neither inherits nor clears the outer Perfect Guard flag: outer={target.LastMpCost}, inner={nested.LastMpCost}");
        Fresh(); target.OnGuardSucceeded = _ => throw new InvalidOperationException("perfect fixture");
        try { target.ApplyDamage(hit); } catch (InvalidOperationException) { }
        target.OnGuardSucceeded = null; target.PerfectGuard = false; target.ApplyDamage(hit);
        check(target.LastMpCost == 5, "Exception cleanup prevents Perfect Guard flag leakage");

        Fresh(); target.Mp = 10000;
        for (int i = 0; i < 50; i++) target.ApplyDamage(hit);
        long allocation = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 1000; i++) { target.Mp = 100; target.ApplyDamage(hit); }
        check(GC.GetAllocatedBytesForCurrentThread() == allocation, "Warmed Perfect Guard multiplier allocates nothing per hit");
        SessionSettings.FriendlyFireForHit = default; Mirror.NetworkServer.active = true;
        FriendlyFireParticipants.Clear(); FriendlyFireRuntime.Clear(); CombatMatchRuntime.PairRule = null;
    }
}
