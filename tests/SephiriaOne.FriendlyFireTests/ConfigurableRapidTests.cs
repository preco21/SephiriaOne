using SephiriaOne;
using UnityEngine;

internal static class ConfigurableRapidTests
{
    internal static void Run(Action<bool, string> check)
    {
        PlayerAvatar source = null, target = null;
        DamageInstance hit = null;
        void Fresh(bool frequency = true)
        {
            FriendlyFireParticipants.Clear(); FriendlyFireRuntime.Clear();
            CombatMatchRuntime.PairRule = null; Mirror.NetworkServer.active = true;
            Time.time = 10;
            SessionSettings.FriendlyFireForHit = new(true, 100, balance: true, hitFrequency: frequency);
            source = new(); target = new() { Hp = 10000, MaxHp = 10000 };
            hit = new() { origin = source, damage = 10, fromType = EDamageFromType.DirectAttack };
        }
        void Set(string text)
        {
            check(FriendlyFireCommand.Parse("/one friendlyfire " + text, out var change) == FriendlyFireParseResult.Valid,
                "Rapid command accepted: " + text);
            SessionSettings.FriendlyFireForHit = SessionSettings.FriendlyFireForHit.Apply(change);
        }
        float Strike()
        {
            float hp = target.Hp; target.ApplyDamage(hit); return hp - target.Hp;
        }
        bool Near(double a, double b) => Math.Abs(a - b) < .001;

        Fresh(); Set("rapid window 2");
        for (int i = 0; i < 8; i++) check(Strike() == 10, "Two-second window admits eight full hits");
        check(Near(Strike(), 10 * Math.Sqrt(8d / 9)), "Window scales count threshold by configured rate");
        Time.time += 1;
        check(Near(Strike(), 10 * Math.Sqrt(8d / 10)), "Long window retains hits older than the original second");
        Time.time += 2;
        check(Strike() == 10, "Configured window ages history out without a timer task");

        Fresh(); Set("rapid window 0.1"); Set("rapid rate 0.1");
        check(Strike() == 10 && Near(Strike(), 10 / Math.Sqrt(2)), "Short-window thresholds keep the first hit at full damage");
        Time.time += .2f; check(Strike() == 10, "Short detection window recovers promptly");
        Fresh(); Set("rapid rate 2"); Set("rapid exponent 1");
        Strike(); Strike(); check(Near(Strike(), 10 * 2d / 3), "Configured linear strength applies above the full-damage rate");
        Set("rapid exponent 2");
        check(Strike() == 10, "Live strength edit clears only old cadence history");
        Strike(); check(Near(Strike(), 10 * 4d / 9), "Configured squared strength applies after threshold");
        Set("rapid exponent 0");
        for (int i = 0; i < 30; i++) check(Strike() == 10, "Zero exponent disables frequency reduction without disabling the shared budget");

        Fresh(); Strike(); Strike(); Strike(); Strike(); Set("rapid burst 30");
        check(Near(Strike(), 10 * Math.Sqrt(4d / 5)), "Burst edits retain existing hit history");
        Set("refill 50"); check(Near(Strike(), 10 * Math.Sqrt(4d / 6)), "Refill edits retain existing hit history");

        Fresh(false); target.Hp = target.MaxHp = 100; hit.damage = float.MaxValue;
        Set("rapid burst 5");
        check(Strike() == 5 && Strike() == 1, "Configured capacity limits burst with frequency scaling off; exhausted hit still lands");
        Set("rapid burst 40");
        check(Strike() == 1, "Increasing capacity grants no immediate credit");
        Set("rapid window 2");
        check(Strike() == 1, "Detection edit grants no fresh burst");
        Time.time += .1f; check(Near(Strike(), 7), "Capacity increase fills only through continuous elapsed refill");
        Set("rapid burst 1"); Time.time += 1;
        check(Strike() == 1, "Decreased capacity clamps stored credit on the next hit");
        Set("rapid reset"); check(Strike() == 1, "Numeric reset preserves depleted allowance");

        Fresh(); Set("rapid window 10"); Set("rapid rate 0.1"); Set("rapid exponent 2");
        target.MaxHp = target.Hp = 100000;
        for (int i = 0; i < 100; i++) Strike();
        int nativeHits = target.Hits;
        long allocated = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 1000; i++) Strike();
        check(GC.GetAllocatedBytesForCurrentThread() == allocated, "Configured warmed hits allocate nothing");
        check(target.Hits == nativeHits + 1000, "Strong frequency reduction never discards valid hits");
        Arithmetic(check);
        SessionSettings.FriendlyFireForHit = default;
        FriendlyFireParticipants.Clear(); FriendlyFireRuntime.Clear();
    }

    private static void Arithmetic(Action<bool, string> check)
    {
        bool Near(double a, double b) => Math.Abs(a - b) < 1e-9;
        var budget = new FriendlyFireDamageBudget();
        check(budget.Take(100, 100, 0, capacity: .1) == 10, "Capacity sets initial stored allowance");
        check(budget.Take(100, 100, 10, capacity: .4) == 10, "Capacity increase cannot retroactively fill expanded headroom");
        check(budget.Take(100, 100, 11, capacity: .4) == 40, "Increased capacity can fill on subsequent elapsed time");
        budget.Refund(1, .05);
        check(budget.Take(100, 100, 11, capacity: .05) == 5, "Refund clamps to a smaller live capacity");
        budget.Refund(1, .4);
        check(budget.Take(100, 100, 11, capacity: .4) == 5, "Refund cannot fill a larger unobserved capacity");
        check(budget.Take(100, 100, 11, out double paid, minimumDamage: 1, capacity: .4) == 1 && paid == 0,
            "Configured exhausted budget retains an unfunded minimum");
        foreach (double invalid in new[] { 0d, -1d, double.NaN, double.PositiveInfinity })
            check(budget.Take(100, 100, 12, capacity: invalid) == 0, "Invalid capacity cannot corrupt stored credit");
        check(Near(budget.Take(100, 100, 12, capacity: .4), 40), "Invalid capacity leaves valid budget usable");
        check(budget.Take(100, 100, 1, capacity: .1) == 10, "Clock rollback initializes the current configured capacity");

        var frequency = new FriendlyFireHitFrequency();
        var settings = new PvpRapidSettings(windowSeconds: 2, hitsPerSecond: 2, exponent: .75m);
        for (int i = 1; i <= 16; i++)
            check(Near(frequency.Next(0, settings), Math.Min(1, Math.Pow(4d / i, .75))), "Arbitrary configured exponent follows bounded curve");
        check(frequency.Next(2, settings) == 1, "History expires at the configured window");
        settings = new PvpRapidSettings(windowSeconds: .1m, hitsPerSecond: 100);
        check(frequency.Next(2, settings) == 1, "Changing bucket width safely retires incompatible bucket timestamps");
        foreach (var window in new[] { .1m, 1m, 10m })
        {
            frequency = default;
            settings = new PvpRapidSettings(windowSeconds: window, hitsPerSecond: 4);
            double scale = 0;
            for (int i = 0; i < 1000; i++) scale = frequency.Next(i * (double)window / 8, settings);
            // The ring approximates history within one bucket. At a boundary,
            // floating-point time can retain seven rather than eight past hits.
            double expectedCount = Math.Max(1, 4 * (double)window);
            check(scale > 0 && scale <= 1 && scale >= Math.Min(1, Math.Sqrt(expectedCount / 9)),
                "Window extremes keep bounded positive steady-hit scaling");
        }
    }
}
