using System;

namespace SephiriaOne
{
    // Infection commits on death; combat membership activates after revival.
    internal sealed class ZombieSurvivalRound
    {
        private readonly bool[] infected, pending;
        internal int Humans { get; private set; }
        internal ZombieSurvivalRound(int count, int initial)
        {
            if (count < 2 || initial < 0 || initial >= count) throw new ArgumentOutOfRangeException();
            infected = new bool[count]; pending = new bool[count];
            infected[initial] = true; Humans = count - 1;
        }
        internal ZombieSurvivalRound(int count, int startingZombies, Random random)
        {
            if (count < 2 || startingZombies < 1) throw new ArgumentOutOfRangeException();
            if (random == null) throw new ArgumentNullException(nameof(random));
            infected = new bool[count]; pending = new bool[count];
            int remaining = Math.Min(startingZombies, count - 1);
            Humans = count - remaining;
            // Uniform sampling without replacement in one bounded roster pass.
            // Initial roles are immediately active; only later infection is pending.
            for (int i = 0; i < count && remaining > 0; i++)
                if (random.Next(count - i) < remaining) { infected[i] = true; remaining--; }
        }
        internal bool IsZombie(int player) => infected[player];
        internal bool IsPending(int player) => pending[player];
        internal bool Infect(int player)
        {
            if (infected[player]) return false;
            infected[player] = pending[player] = true; Humans--; return true;
        }
        internal bool Revived(int player)
        {
            if (!pending[player]) return false;
            pending[player] = false; return true;
        }
        internal bool AllowsPair(int first, int second) => first != second && !pending[first] && !pending[second] && infected[first] != infected[second];
    }
}
