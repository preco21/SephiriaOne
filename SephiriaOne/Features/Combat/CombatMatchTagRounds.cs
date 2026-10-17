using System;

namespace SephiriaOne
{
    internal static partial class CombatMatchRuntime
    {
        private static int tagRound, tagRoundLimit;

        private static void CompletePlayTagRound()
        {
            // A new combat token rejects delayed death/revival notifications from
            // the previous round. Keep roster, pending recovery and score objects.
            token++; tagRound++;
            phase = Phase.BetweenRounds;
            tagBomb = null; tagShown = -1; tagPreviousName = null;
            ChaserRevision++;
            FriendlyFireParticipants.InvalidateCombat();
            SessionSettings.DeathmatchChanged();
        }

        private static void AdvancePlayTagRound(double now, bool ready)
        {
            if (phase == Phase.Countdown && tagRound > 1 && !ready)
            {
                // Loading/death during warmup does not spend a round or let a
                // partial roster start fighting. Recovery uses the shared queue.
                phase = Phase.BetweenRounds;
                SessionSettings.DeathmatchChanged();
            }
            if (phase != Phase.BetweenRounds || !ready) return;
            startsAt = now + 3; openingShown = 3;
            phase = Phase.Countdown;
            SessionSettings.DeathmatchChanged();
            ShowPlayTagCountdown(3);
        }

        private static void ShowPlayTagCountdown(int seconds) => Broadcast(null, tagRoundLimit == 1 ? L.F("{0}...", seconds) :
            L.F("[Play Tag] Round {0}/{1}. Fight in: {2}...", tagRound, tagRoundLimit, seconds));

        private static string DescribePlayTagRound() => L.F("Round: {0}/{1}.", IsPlayTag ? tagRound : 0,
            IsPlayTag ? tagRoundLimit : SessionSettings.PlayTagRounds);
    }
}
