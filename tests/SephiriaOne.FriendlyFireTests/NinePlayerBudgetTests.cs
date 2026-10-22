using SephiriaOne;
using UnityEngine;

// Explicit 20% reserve below tests legacy configured capacity, independent of shipped defaults.
internal static class NinePlayerBudgetTests
{
    internal static void Run(Action<bool, string> check)
    {
        PlayerAvatar[] Fresh()
        {
            FriendlyFireParticipants.Clear(); FriendlyFireRuntime.Clear();
            SessionSettings.FriendlyFireForHit = new(true, 100, balance: true, rapid: new PvpRapidSettings(burstPercent: 20));
            Time.time = 10;
            var players = Enumerable.Range(0, 9).Select(_ => new PlayerAvatar()).ToArray();
            FriendlyFireParticipants.Select(players);
            return players;
        }
        bool Near(double actual, double expected) => Math.Abs(actual - expected) < 0.001;
        DamageInstance Hit(PlayerAvatar source, float amount) => new() { origin = source, damage = amount };

        // These executable hooks use nine selected participant receipts. Real
        // connection admission and mode rosters belong to the runtime suite.
        var players = Fresh();
        var hit = Hit(players[0], float.MaxValue);
        for (int i = 1; i < players.Length; i++)
        {
            players[i].ApplyDamage(hit);
            check(players[i].Hp == 80, "Nine-player attacker gets a full independent burst against victim " + i);
            players[i].ApplyDamage(hit);
            check(players[i].Hp == 79, "Depleting victim " + i + " cannot borrow another victim's allowance");
        }

        players = Fresh();
        var target = players[0];
        // Healing between passes keeps this same target alive without revival,
        // clock advancement or a fresh receipt that could reset any pair budget.
        foreach (float expected in new[] { 10f, 10f, 1f, 1f })
        {
            for (int i = 1; i < players.Length; i++)
            {
                float before = target.Hp;
                target.ApplyDamage(Hit(players[i], 10));
                check(Near(before - target.Hp, expected),
                    "Nine-player victim preserves attacker " + i + " allowance across same-frame hits: " + expected);
            }
            target.Heal(100);
        }

        players = Fresh(); target = players[1]; hit = Hit(players[0], 20);
        target.ApplyDamage(hit);
        check(target.Hp == 80, "Nine-player first balanced hit uses the configured shared reserve");
        target.ApplyDamage(hit);
        check(target.Hp == 79, "Nine-player second same-frame hit retains the minimum after depletion");
        target.ApplyDamage(hit);
        check(target.Hp == 78 && target.Hits == 3, "Exhausted allowance retains a positive native impact");
        Time.time += 0.1f;
        target.ApplyDamage(hit);
        check(Near(target.Hp, 71), "Elapsed game time restores seven HP of the default allowance after one tenth second");
        float depletedHp = target.Hp;
        target.ApplyDamage(hit);
        check(target.Hp == depletedHp - 1, "The next same-frame hit remains positive after spending new credit");
        depletedHp = target.Hp;
        SessionSettings.FriendlyFireForHit = new(true, 100, balance: false);
        target.ApplyDamage(hit);
        check(Near(depletedHp - target.Hp, 20), "Disabling balancing immediately bypasses an exhausted pair budget without waiting");

        SessionSettings.FriendlyFireForHit = default;
        FriendlyFireParticipants.Clear(); FriendlyFireRuntime.Clear();
    }
}
