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
            source = new(); target = new(); hit = new() { origin = source, damage = 2, fromType = EDamageFromType.DirectAttack };
        }
        void Strike(int count = 1) { for (int i = 0; i < count; i++) target.ApplyDamage(hit); }
        Fresh(); Strike(4); float before = target.Hp; Strike();
        check(Near(before - target.Hp, 2 * Math.Sqrt(4d / 5)), "Fifth rapid hit is frequency-scaled even with spare damage budget");
        check(hit.damage == 2 && target.Hits == 5, "Frequency scaling preserves pooled input and native hit callbacks");
        Fresh(false); Strike(8); check(target.Hp == 84, "Default-off scaler leaves existing balanced damage unchanged");
        Fresh(balance: false); Strike(8); check(target.Hp == 84, "Frequency preference alone cannot change unbalanced combat");
        Fresh(); Strike(4); Time.time += 2; before = target.Hp; Strike();
        check(before - target.Hp == 2, "Idle resets recent frequency without a persistent damage penalty");
        Fresh(); for (int i = 0; i < 40; i++) { Time.time = 10 + i * 0.25f; Strike(); }
        check(target.Hp == 20, "Steady four hits per second retain full damage");
        Fresh(); Strike(8); var otherTarget = new PlayerAvatar(); otherTarget.ApplyDamage(hit);
        check(otherTarget.Hp == 98, "Other opponents have separate hit histories");
        before = target.Hp; hit.origin = new PlayerAvatar(); Strike();
        check(before - target.Hp == 2, "Other attackers have separate hit histories");
        Fresh(); Strike(4); hit.origin = new UnitAvatar { NetworkLeader = source }; before = target.Hp; Strike();
        check(before - target.Hp == 2, "Companion damage uses incidental cadence without inheriting direct hit count");
        Fresh(); Strike(4); hit.fromType = EDamageFromType.Magic; hit.id = "Bolt"; before = target.Hp; Strike();
        check(Near(before - target.Hp, 2 * Math.Sqrt(4d / 5)), "Changing weapon or spell ID cannot bypass hit history");
        Fresh(); Strike(4); hit.damage = 0; Strike(20); hit.damage = 2; before = target.Hp; Strike();
        check(Near(before - target.Hp, 2 * Math.Sqrt(4d / 5)), "Zero resolved hits do not inflate frequency");
        Fresh(); Strike(4); var enemy = new UnitAvatar { faction = "enemy" }; enemy.ApplyDamage(hit);
        check(enemy.Hp == 98, "Outgoing PvE ignores the scaler");
        target.ApplyDamage(new() { origin = enemy, damage = 2, targetFactionLayers = -1 });
        check(target.Hp == 90, "Incoming PvE ignores the scaler");
        before = target.Hp; Strike(); check(Near(before - target.Hp, 2 * Math.Sqrt(4d / 5)), "PvE also leaves PvP hit history unchanged");
        Fresh(); hit.damage = float.MaxValue; Strike(10);
        check(target.Hp == 71, "Exhausted frequency-scaled barrage retains the one-point floor");
        SessionSettings.FriendlyFireForHit = new(true, 100, balance: true); Strike();
        SessionSettings.FriendlyFireForHit = new(true, 100, balance: true, hitFrequency: true); Strike();
        check(target.Hp == 69, "Frequency toggles grant only minimum hits, not a new burst");
        Fresh(); Strike(8);
        SessionSettings.FriendlyFireForHit = new(true, 100, balance: true);
        SessionSettings.FriendlyFireForHit = new(true, 100, balance: true, hitFrequency: true);
        before = target.Hp; Strike(); check(before - target.Hp == 2, "Off/on between hits resets only hit history");
        Fresh(); Strike(4); FriendlyFireKda.ResetScores(); before = target.Hp; Strike();
        check(before - target.Hp == 2, "Score reset retires both hit history and budget through the shared epoch");
        Fresh(); Strike(4); target.currentFloorGuid = "next"; before = target.Hp; Strike();
        check(before - target.Hp == 2, "Floor transition retires hit history");
        Fresh(); Strike(4); FriendlyFireParticipants.Depart(source); FriendlyFireParticipants.Rejoin(source); before = target.Hp; Strike();
        check(before - target.Hp == 2, "Rejoined attacker cannot inherit previous connection hit history");
        Fresh(); Strike(4); target.IsDead = true; target.Revive(100); before = target.Hp; Strike();
        check(before - target.Hp == 2, "Successful revival starts a new hit-history lifetime");
        Fresh(); Strike(4); target.IsGuarding = true; Strike(5); target.IsGuarding = false; hit.failed = EDamageFailType.None;
        before = target.Hp; Strike();
        check(Near(before - target.Hp, 2 * Math.Sqrt(4d / 5)), "Native blocked attacks do not inflate resolved hit frequency");
        Fresh(); target.ApplyDamage(EffectTests.Tick(source, "Debuff_Electric", 2));
        for (int i = 0; i < 20; i++) target.ApplyDamage(EffectTests.Tick(source, "Debuff_Electric", 2));
        before = target.Hp; Strike(3); check(before - target.Hp == 6, "Rejected shock pulses do not count toward frequency");
        before = target.Hp; target.ApplyDamage(EffectTests.Tick(source, "Debuff_Burn", 2));
        check(before - target.Hp == 2, "Admitted DoT damage uses incidental history independently of direct hits");
        Fresh(); target.OnHit = (_, _) => source.ApplyDamage(new() { origin = target, id = "Ability_Thorns", damage = 2 });
        Strike(4); before = source.Hp; Strike();
        check(Near(before - source.Hp, 2 * Math.Sqrt(4d / 5)), "Reflected damage scales using the reverse directed pair");
        Fresh(); var proc = new Charm_FrostiumRing { NetworkAvatar = source, Target = target, Damage = new() { origin = source, damage = 2, fromType = EDamageFromType.None } };
        Strike(3); target.OnHit = (_, _) => { target.OnHit = null; proc.HandleAttackUnit(); }; before = target.Hp; Strike();
        check(before - target.Hp == 4, "Nested artifact procs do not inflate outer direct cadence");
        Fresh(); Strike(4); FriendlyFireParticipants.Depart(target); FriendlyFireParticipants.Rejoin(target); before = target.Hp; Strike();
        check(before - target.Hp == 2, "Rejoined victim gets fresh pair history");
        Fresh(); Strike(4); FriendlyFireParticipants.Select(source, target); before = target.Hp; Strike();
        check(before - target.Hp == 2, "Participant changes use the shared epoch cleanup");
        Fresh(); Strike(4); CombatMatchRuntime.PairRule = (_, _) => false; Strike(20);
        CombatMatchRuntime.PairRule = null; before = target.Hp; Strike();
        check(Near(before - target.Hp, 2 * Math.Sqrt(4d / 5)), "Disallowed match targets cannot inflate hit history");
        Fresh(); Strike(4); Mirror.NetworkServer.active = false; Strike(20); Mirror.NetworkServer.active = true;
        before = target.Hp; Strike(); check(Near(before - target.Hp, 2 * Math.Sqrt(4d / 5)), "Guests never update host hit history");
        Fresh(); target.IsMpShield = true; Strike(8); int beforeMp = target.Mp;
        SessionSettings.FriendlyFireForHit = new(true, 100, balance: true); hit.damage = 16; Strike();
        check(target.Hp == 100 && target.Mp < beforeMp, "MP-shield rounding refunds unused budget without mutating hit scaling input");
        Fresh(); Strike(4); FriendlyFireRuntime.Clear(); before = target.Hp; Strike();
        check(before - target.Hp == 2, "Runtime cleanup releases hit history with the existing budget storage");
        Arithmetic(check);
        Fresh(); target.Hp = target.MaxHp = 10000; hit.damage = 0.001f; Strike(100);
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
