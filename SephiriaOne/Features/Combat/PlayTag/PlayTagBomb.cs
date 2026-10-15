using System;

namespace SephiriaOne
{
    // Pure progression over the match's frozen connection indices. It never
    // revives players, awards KDA, sends messages or invokes native callbacks.
    internal sealed class PlayTagBomb
    {
        internal const int MinimumFuse = 3;
        private const int MinimumTurnsPerPlayer = 2;
        private readonly int[] minimumTurns;
        private double deadline;
        private bool immediateExplosion;
        internal int Carrier { get; private set; }
        internal int PreviousCarrier { get; private set; } = -1;
        internal int Fuse { get; private set; }
        internal bool AwaitingRespawn { get; private set; }
        internal bool Resolved { get; private set; }

        internal PlayTagBomb(int playerCount, int initialCarrier, int seconds, double now)
        {
            if (playerCount < 2) throw new ArgumentOutOfRangeException(nameof(playerCount));
            if (initialCarrier < 0 || initialCarrier >= playerCount) throw new ArgumentOutOfRangeException(nameof(initialCarrier));
            if (seconds < MinimumFuse) throw new ArgumentOutOfRangeException(nameof(seconds));
            if (!Finite(now)) throw new ArgumentOutOfRangeException(nameof(now));
            minimumTurns = new int[playerCount];
            Carrier = initialCarrier; Fuse = seconds; deadline = now + seconds;
            if (seconds == MinimumFuse) minimumTurns[Carrier] = 1;
        }

        internal bool TryPass(int killer, int victim, double now)
        {
            if (Resolved || AwaitingRespawn || !Finite(now) || IsDue(now) || killer != Carrier ||
                victim < 0 || victim >= minimumTurns.Length || victim == killer) return false;
            PreviousCarrier = Carrier; Carrier = victim;
            Fuse = Math.Max(MinimumFuse, (int)((long)Fuse * 4 / 5));
            // Counts belong to entrants, not lives, KDA or the previous passer.
            // The third receipt is due immediately, even before victim revival.
            immediateExplosion = Fuse == MinimumFuse && ++minimumTurns[victim] > MinimumTurnsPerPlayer;
            AwaitingRespawn = !immediateExplosion;
            deadline = now;
            return true;
        }

        internal bool ArmAfterRespawn(int player, double now)
        {
            if (Resolved || !AwaitingRespawn || immediateExplosion || player != Carrier || !Finite(now)) return false;
            AwaitingRespawn = false; deadline = now + Fuse;
            return true;
        }

        internal bool IsDue(double now) => !Resolved && Finite(now) &&
            (immediateExplosion || !AwaitingRespawn && now >= deadline);

        internal int Remaining(double now) => Resolved || immediateExplosion || !Finite(now) ? 0 :
            AwaitingRespawn ? Fuse : (int)Math.Max(0, Math.Min(Fuse, Math.Ceiling(deadline - now)));

        internal bool ConsumeExplosion(double now)
        {
            if (!IsDue(now)) return false;
            Resolved = true; AwaitingRespawn = false;
            return true;
        }

        private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
