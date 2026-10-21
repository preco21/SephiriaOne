using SephiriaOne;
using UnityEngine;

internal static class DefensiveEffectTests
{
    internal static void Run(Action<bool, string> check)
    {
        PlayerAvatar source = null, target = null;
        Charm_EvasionRestore charm = null;
        DamageInstance hit = null;
        void Fresh()
        {
            FriendlyFireParticipants.Clear(); CombatMatchRuntime.PairRule = null;
            Mirror.NetworkServer.active = true; Time.time = 100;
            SessionSettings.FriendlyFireForHit = new(true, 100, balance: true);
            source = new(); target = new() { AbsoluteEvasion = 100 };
            hit = new() { origin = source, damage = 10 };
            charm = new() { NetworkAvatar = target, Level = 2 }; charm.Attach();
            UnityEngine.Random.NextValue = 100;
        }
        Fresh(); target.ApplyDamage(hit); target.ApplyDamage(hit);
        check(target.CurrentDashModule.Restored == 2, "Elastic Band cannot refund a second dash within the same PvP window");
        Time.time += 1.99f; target.ApplyDamage(hit);
        check(target.CurrentDashModule.Restored == 2, "Refund remains gated before two seconds");
        Time.time = 102; target.ApplyDamage(hit);
        check(target.CurrentDashModule.Restored == 4, "Native level-based refund returns at the two-second boundary");
        hit.origin = new PlayerAvatar(); target.ApplyDamage(hit);
        check(target.CurrentDashModule.Restored == 4, "Multiple attackers share one defender refund cooldown");
        charm.Detach(); charm = new() { NetworkAvatar = target, Level = 2 }; charm.Attach(); target.ApplyDamage(hit);
        check(target.CurrentDashModule.Restored == 4, "Re-equipping the artifact cannot bypass cooldown");
        hit.origin = new UnitAvatar { NetworkLeader = source }; target.ApplyDamage(hit);
        check(target.CurrentDashModule.Restored == 4, "Owned companion attacks share the refund limit");
        int evades = 0; target.OnEvade += _ => evades++; target.ApplyDamage(hit);
        check(evades == 1 && target.Hp == 100, "Limit preserves the dodge and other native OnEvade callbacks");
        hit.origin = new UnitAvatar { faction = "enemy" }; hit.targetFactionLayers = -1; target.ApplyDamage(hit);
        check(target.CurrentDashModule.Restored == 6, "Concurrent PvE refund ignores the PvP cooldown");
        hit.origin = source; target.ApplyDamage(hit);
        check(target.CurrentDashModule.Restored == 6, "PvE refund does not reset the PvP cooldown");
        Fresh(); target.ApplyDamage(hit); SessionSettings.FriendlyFireForHit = new(true, 100); target.ApplyDamage(hit); target.ApplyDamage(hit);
        check(target.CurrentDashModule.Restored == 6, "Balancing off restores every native refund immediately");
        SessionSettings.FriendlyFireForHit = new(true, 100, balance: true); target.ApplyDamage(hit);
        check(target.CurrentDashModule.Restored == 8, "Re-enabling balancing starts a new refund window");
        foreach (string transition in new[] { "floor", "revive", "rejoin", "participants", "kda", "clock" })
        {
            Fresh(); target.ApplyDamage(hit);
            switch (transition)
            {
                case "floor": target.currentFloorGuid = "floor-2"; break;
                case "revive": target.IsDead = true; target.Revive(1f); break;
                case "rejoin": FriendlyFireParticipants.Depart(target); FriendlyFireParticipants.Rejoin(target); break;
                case "participants": FriendlyFireParticipants.Select(source, target); break;
                case "kda": FriendlyFireKda.Reset(); break;
                case "clock": Time.time = 0; break;
            }
            target.ApplyDamage(hit);
            check(target.CurrentDashModule.Restored == 4, transition + " retires stale refund state");
        }
        Fresh(); target.CurrentDashModule.OnRestore = () => { target.CurrentDashModule.OnRestore = null; charm.Invoke(hit); };
        target.ApplyDamage(hit);
        check(target.CurrentDashModule.Restored == 2, "Cooldown reservation precedes reentrant native callbacks");
        Fresh(); target.ApplyDamage(hit); var enemy = new UnitAvatar { faction = "enemy" };
        target.OnEvade += _ => charm.Invoke(new() { origin = enemy }); target.ApplyDamage(hit);
        check(target.CurrentDashModule.Restored == 4, "Unrelated callback damage cannot borrow the current hit cooldown");
        Fresh(); target.CurrentDashModule.OnRestore = () => throw new InvalidOperationException("fixture");
        try { target.ApplyDamage(hit); } catch (InvalidOperationException) { }
        target.CurrentDashModule.OnRestore = null; hit.origin = enemy; hit.targetFactionLayers = -1; target.ApplyDamage(hit);
        check(target.CurrentDashModule.Restored == 4, "Exception unwinds PvP scope before a subsequent PvE refund");
        Fresh(); target.AbsoluteEvasion = 0; target.isCounterInvincibleApplied = 1; target.CounterController.SetCounter(0.34f);
        check(target.ApplyDamage(hit) == EApplyDamageResult.Fail_Block, "Balanced counter retains its first 0.35 seconds");
        target.CounterController.SetCounter(0.35f);
        check(target.ApplyDamage(hit) == EApplyDamageResult.Success && target.Hp < 100, "Long counter no longer cancels hostile hits after 0.35 seconds");
        check(target.isCounterInvincibleApplied == 1 && target.CounterController.Elapsed == 0.35f && target.CounterController.furyInvincibleTime == 0.935f,
            "Counter cap never writes native flags, duration or timer");
        hit.origin = enemy; hit.targetFactionLayers = -1;
        check(target.ApplyDamage(hit) == EApplyDamageResult.Fail_Block, "Same extended counter still protects from concurrent PvE");
        hit.origin = source; target.CounterController.SetCounter(-0.1f);
        check(target.ApplyDamage(hit) == EApplyDamageResult.Fail_Block, "Native negative latency allowance is retained");
        target.CounterController.SetCounter(0.8f); SessionSettings.FriendlyFireForHit = new(true, 100);
        check(target.ApplyDamage(hit) == EApplyDamageResult.Fail_Block, "Disabling balancing restores the remaining native counter immediately");
        SessionSettings.FriendlyFireForHit = new(true, 100, balance: true); target.CounterController.SetCounter(0.8f, active: false);
        check(target.ApplyDamage(hit) == EApplyDamageResult.Fail_Block, "Unidentified counter provenance stays native");
        target.CounterController = null;
        check(target.ApplyDamage(hit) == EApplyDamageResult.Fail_Block, "Missing weapon controller cannot erase unknown protection");
        Fresh(); target.AbsoluteEvasion = 0; target.isCounterInvincibleApplied = 1;
        target.CounterController.SetCounter(0.1f, 0.125f);
        check(target.ApplyDamage(hit) == EApplyDamageResult.Fail_Block, "Shorter native counter protection is never extended or reduced");
        target.isCounterInvincibleApplied = 0;
        check(target.ApplyDamage(hit) == EApplyDamageResult.Success, "Native counter expiration remains authoritative");
        target.isCounterInvincibleApplied = 2; target.CounterController.SetCounter(0.8f);
        check(target.ApplyDamage(hit) == EApplyDamageResult.Fail_Block, "Unknown extra counter contributor remains protected");
        target.isCounterInvincibleApplied = 1; target.CounterController.unitAvatar = source;
        check(target.ApplyDamage(hit) == EApplyDamageResult.Fail_Block, "Another avatar's controller cannot shorten victim protection");
        target.CounterController.unitAvatar = target;
        source.OnAttackUnitBeforeOperation += (_, _) => SessionSettings.FriendlyFireForHit = new(true, 100);
        check(target.ApplyDamage(hit) == EApplyDamageResult.Fail_Block, "Native callback can disable balancing before the counter check");
        Fresh(); target.AbsoluteEvasion = 0; target.isCounterInvincibleApplied = 1; target.CounterController.SetCounter(0.8f);
        var pveHit = new DamageInstance { origin = new UnitAvatar { faction = "enemy" }, damage = 1, targetFactionLayers = -1 };
        EApplyDamageResult nested = EApplyDamageResult.Success;
        source.OnAttackUnitBeforeOperation += (_, _) => nested = target.ApplyDamage(pveHit);
        check(target.ApplyDamage(hit) == EApplyDamageResult.Success && nested == EApplyDamageResult.Fail_Block,
            "Nested PvE preserves full counter immunity and outer PvP cap resumes");
        Fresh(); FriendlyFireParticipants.Select(source); target.ApplyDamage(hit);
        check(target.CurrentDashModule.Restored == 0, "Excluded player never consumes a refund cooldown");
        FriendlyFireParticipants.Select(source, target); target.ApplyDamage(hit);
        check(target.CurrentDashModule.Restored == 2, "Newly eligible player can receive the first refund");
        Fresh(); CombatMatchRuntime.PairRule = (_, _) => false; target.ApplyDamage(hit);
        check(target.CurrentDashModule.Restored == 0, "Same-team/countdown protected pairs never enter refund logic");
        CombatMatchRuntime.PairRule = (_, _) => true; target.ApplyDamage(hit);
        check(target.CurrentDashModule.Restored == 2, "Opposing active match pair uses the shared refund limit");
        Fresh(); target.ApplyDamage(hit); SessionSettings.FriendlyFireForHit = new(false, 100, balance: true);
        SessionSettings.FriendlyFireForHit = new(true, 100, balance: true); target.ApplyDamage(hit);
        check(target.CurrentDashModule.Restored == 4, "Friendly-fire off/on resets defender refund state");
        Fresh(); void Strike() { target.ApplyDamage(hit); }
        for (int i = 0; i < 1000; i++) Strike();
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 1000; i++) Strike();
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        check(allocated == 0, "Defensive refund gating allocates nothing per warmed hit");
        Console.WriteLine("Defensive refund: " + allocated + " bytes / 1000 warmed hits");
        Fresh(); target.AbsoluteEvasion = 0; target.isCounterInvincibleApplied = 1; target.CounterController.SetCounter(0.8f);
        void CounterStrike() { Time.time += 1f; target.Hp = 100; target.ApplyDamage(hit); }
        for (int i = 0; i < 1000; i++) CounterStrike();
        before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 1000; i++) CounterStrike();
        allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        check(allocated == 0, "Readonly counter admission allocates nothing per warmed hit");
        Console.WriteLine("Counter admission: " + allocated + " bytes / 1000 warmed hits");
        FriendlyFireParticipants.Clear(); SessionSettings.FriendlyFireForHit = default;
    }
}
