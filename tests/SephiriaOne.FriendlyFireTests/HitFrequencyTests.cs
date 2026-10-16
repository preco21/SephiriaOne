using SephiriaOne;
using UnityEngine;

internal static class HitFrequencyTests
{
    internal static void Run(Action<bool, string> check)
    {
        PlayerAvatar source = null, target = null; DamageInstance hit = null;
        bool Near(double a, double b) => Math.Abs(a - b) < 0.0001;
        void Fresh(bool frequency = true, bool balance = true)
        {
            FriendlyFireParticipants.Clear(); FriendlyFireRuntime.Clear(); CombatMatchRuntime.PairRule = null;
            Mirror.NetworkServer.active = true; Time.time = 10;
            SessionSettings.FriendlyFireForHit = new(true, 100, balance: balance, hitFrequency: frequency);
            source = new(); target = new(); hit = new() { origin = source, damage = 1 };
        }
        void Strike(int count = 1) { for (int i = 0; i < count; i++) target.ApplyDamage(hit); }
        Fresh(); Strike(4); float before = target.Hp; Strike();
        check(Near(before - target.Hp, Math.Sqrt(4d / 5)), "Fifth rapid hit is frequency-scaled even with spare damage budget");
        check(hit.damage == 1 && target.Hits == 5, "Frequency scaling preserves pooled input and native hit callbacks");
        Fresh(false); Strike(8); check(target.Hp == 92, "Default-off scaler leaves existing balanced damage unchanged");
        Fresh(balance: false); Strike(8); check(target.Hp == 92, "Frequency preference alone cannot change unbalanced combat");
        Fresh(); Strike(4); Time.time += 2; before = target.Hp; Strike();
        check(before - target.Hp == 1, "Idle resets recent frequency without a persistent damage penalty");
        Fresh(); for (int i = 0; i < 40; i++) { Time.time = 10 + i * 0.25f; Strike(); }
        check(target.Hp == 60, "Steady four hits per second retain full damage");
        Fresh(); Strike(8); var otherTarget = new PlayerAvatar(); otherTarget.ApplyDamage(hit);
        check(otherTarget.Hp == 99, "Other opponents have separate hit histories");
        before = target.Hp; hit.origin = new PlayerAvatar(); Strike();
        check(before - target.Hp == 1, "Other attackers have separate hit histories");
        Fresh(); Strike(4); hit.origin = new UnitAvatar { NetworkLeader = source }; before = target.Hp; Strike();
        check(Near(before - target.Hp, Math.Sqrt(4d / 5)), "Companion damage joins its owner's hit history");
        Fresh(); Strike(4); hit.fromType = EDamageFromType.Magic; hit.id = "Bolt"; before = target.Hp; Strike();
        check(Near(before - target.Hp, Math.Sqrt(4d / 5)), "Changing weapon or spell ID cannot bypass hit history");
        Fresh(); Strike(4); hit.damage = 0; Strike(20); hit.damage = 1; before = target.Hp; Strike();
        check(Near(before - target.Hp, Math.Sqrt(4d / 5)), "Zero resolved hits do not inflate frequency");
        Fresh(); Strike(4); var enemy = new UnitAvatar { faction = "enemy" }; enemy.ApplyDamage(hit);
        check(enemy.Hp == 99, "Outgoing PvE ignores the scaler");
        target.ApplyDamage(new() { origin = enemy, damage = 1, targetFactionLayers = -1 });
        check(target.Hp == 95, "Incoming PvE ignores the scaler");
        before = target.Hp; Strike(); check(Near(before - target.Hp, Math.Sqrt(4d / 5)), "PvE also leaves PvP hit history unchanged");
        Fresh(); hit.damage = float.MaxValue; Strike(100);
        check(target.Hp == 80, "Frequency-scaled hits cannot exceed the same-frame damage budget");
        SessionSettings.FriendlyFireForHit = new(true, 100, balance: true); Strike();
        SessionSettings.FriendlyFireForHit = new(true, 100, balance: true, hitFrequency: true); Strike();
        check(target.Hp == 80, "Frequency toggle edits never refill an exhausted damage budget");
        Fresh(); Strike(8);
        SessionSettings.FriendlyFireForHit = new(true, 100, balance: true);
        SessionSettings.FriendlyFireForHit = new(true, 100, balance: true, hitFrequency: true);
        before = target.Hp; Strike(); check(before - target.Hp == 1, "Off/on between hits resets only hit history");
        Fresh(); Strike(4); FriendlyFireKda.ResetScores(); before = target.Hp; Strike();
        check(before - target.Hp == 1, "Score reset retires both hit history and budget through the shared epoch");
        Fresh(); Strike(4); target.currentFloorGuid = "next"; before = target.Hp; Strike();
        check(before - target.Hp == 1, "Floor transition retires hit history");
        Fresh(); Strike(4); FriendlyFireParticipants.Depart(source); FriendlyFireParticipants.Rejoin(source); before = target.Hp; Strike();
        check(before - target.Hp == 1, "Rejoined attacker cannot inherit previous connection hit history");
        Fresh(); Strike(4); target.IsDead = true; target.Revive(1); before = target.Hp; Strike();
        check(before - target.Hp == 1, "Successful revival starts a new hit-history lifetime");
        Fresh(); Strike(4); target.IsGuarding = true; Strike(5); target.IsGuarding = false; hit.failed = EDamageFailType.None;
        before = target.Hp; Strike();
        check(Near(before - target.Hp, Math.Sqrt(4d / 5)), "Native blocked attacks do not inflate resolved hit frequency");
        Fresh(); target.ApplyDamage(EffectTests.Tick(source, "Debuff_Electric", 1));
        for (int i = 0; i < 20; i++) target.ApplyDamage(EffectTests.Tick(source, "Debuff_Electric", 1));
        before = target.Hp; Strike(3); check(before - target.Hp == 3, "Rejected shock pulses do not count toward frequency");
        before = target.Hp; target.ApplyDamage(EffectTests.Tick(source, "Debuff_Burn", 1));
        check(Near(before - target.Hp, Math.Sqrt(4d / 5)), "Admitted DoT damage shares the owner's hit frequency");
        Fresh(); target.OnHit = (_, _) => source.ApplyDamage(new() { origin = target, id = "Ability_Thorns", damage = 1 });
        Strike(4); before = source.Hp; Strike();
        check(Near(before - source.Hp, Math.Sqrt(4d / 5)), "Reflected damage scales using the reverse directed pair");
        Fresh(); var proc = new Charm_FrostiumRing { NetworkAvatar = source, Target = target, Damage = hit };
        Strike(3); target.OnHit = (_, _) => { target.OnHit = null; proc.HandleAttackUnit(); }; before = target.Hp; Strike();
        check(Near(before - target.Hp, 1 + Math.Sqrt(4d / 5)), "Nested artifact procs see outer hit frequency before native callbacks");
        Fresh(); Strike(4); FriendlyFireParticipants.Depart(target); FriendlyFireParticipants.Rejoin(target); before = target.Hp; Strike();
        check(before - target.Hp == 1, "Rejoined victim gets fresh pair history");
        Fresh(); Strike(4); FriendlyFireParticipants.Select(source, target); before = target.Hp; Strike();
        check(before - target.Hp == 1, "Participant changes use the shared epoch cleanup");
        Fresh(); Strike(4); CombatMatchRuntime.PairRule = (_, _) => false; Strike(20);
        CombatMatchRuntime.PairRule = null; before = target.Hp; Strike();
        check(Near(before - target.Hp, Math.Sqrt(4d / 5)), "Disallowed match targets cannot inflate hit history");
        Fresh(); Strike(4); Mirror.NetworkServer.active = false; Strike(20); Mirror.NetworkServer.active = true;
        before = target.Hp; Strike(); check(Near(before - target.Hp, Math.Sqrt(4d / 5)), "Guests never update host hit history");
        Fresh(); target.IsMpShield = true; Strike(8); int beforeMp = target.Mp;
        SessionSettings.FriendlyFireForHit = new(true, 100, balance: true); hit.damage = 16; Strike();
        check(target.Hp == 100 && target.Mp < beforeMp, "MP-shield rounding refunds unused budget without mutating hit scaling input");
        Fresh(); Strike(4); FriendlyFireRuntime.Clear(); before = target.Hp; Strike();
        check(before - target.Hp == 1, "Runtime cleanup releases hit history with the existing budget storage");
        Arithmetic(check);
        Fresh(); hit.damage = 0.001f; Strike(100);
        long allocated = GC.GetAllocatedBytesForCurrentThread(); Strike(1000);
        check(GC.GetAllocatedBytesForCurrentThread() == allocated, "Warmed frequency scaling adds no per-hit allocation");
        SessionSettings.FriendlyFireForHit = default; FriendlyFireRuntime.Clear();
    }

    private static void Arithmetic(Action<bool, string> check)
    {
        var frequency = new FriendlyFireHitFrequency();
        double scale = 0;
        for (int i = 1; i <= 16; i++)
        {
            scale = frequency.Next(0);
            check(Math.Abs(scale - Math.Min(1, Math.Sqrt(4d / i))) < 1e-10, "Burst count has predictable bounded square-root scaling");
        }
        check(scale == 0.5 && frequency.Next(2) == 1, "Sixteen hits halve damage; an idle second clears the estimate");
        check(frequency.Next(double.NaN) == 0 && frequency.Next(double.PositiveInfinity) == 0 && frequency.Next(double.MaxValue) == 0,
            "Invalid clocks cannot poison frequency state");
        check(frequency.Next(2) == 1, "Valid hit remains usable after invalid clocks");
        for (int i = 0; i < 20; i++) frequency.Next(2);
        check(frequency.Next(1) == 1, "Clock rollback clears history safely");
        foreach (int rate in new[] { 1, 2, 4, 8, 16, 32 })
        {
            frequency = default;
            for (int i = 0; i < rate * 4; i++) scale = frequency.Next(i / (double)rate);
            check(Math.Abs(scale - Math.Min(1, Math.Sqrt(4d / rate))) < 1e-10, "Steady rate matches expected scaling: " + rate);
        }
    }
}
