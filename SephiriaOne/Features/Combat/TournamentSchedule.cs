using System;
using System.Collections.Generic;

namespace SephiriaOne
{
    internal sealed class TournamentSchedule
    {
        private readonly struct Pair
        {
            internal readonly int First, Second;
            internal Pair(int first, int second) { First = first; Second = second; }
        }

        private readonly Func<int, int> random;
        private readonly int[] matches, wins;
        private readonly bool[] eliminated;
        private readonly int[,] attempts;
        private readonly List<Pair> candidates = new List<Pair>();
        private int pendingFirst = -1, pendingSecond = -1;

        // Advancement requires a win. Draws and restarted countdowns must not
        // promote a bracket level merely because an entrant has fought before.
        internal int PendingRound => pendingFirst < 0 ? 0 : 1 + Math.Min(wins[pendingFirst], wins[pendingSecond]);
        // Both reserved entrants are still eligible, so an earlier completed
        // attempt between them can only have been unresolved.
        internal bool PendingIsRematch => pendingFirst >= 0 && attempts[pendingFirst, pendingSecond] > 0;

        internal TournamentSchedule(int count, Func<int, int> random)
        {
            if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
            this.random = random ?? throw new ArgumentNullException(nameof(random));
            matches = new int[count];
            wins = new int[count];
            eliminated = new bool[count];
            attempts = new int[count, count];
        }

        // Selecting a pair reserves it. Repeated reads cannot reroll opponents or
        // count a match before the runtime supplies its completed result.
        internal bool TryNext(out int first, out int second)
        {
            if (pendingFirst < 0)
            {
                candidates.Clear();
                for (int left = 0; left < matches.Length; left++)
                {
                    if (eliminated[left]) continue;
                    for (int right = left + 1; right < matches.Length; right++)
                    {
                        if (eliminated[right] || attempts[left, right] >= 2) continue;
                        var pair = new Pair(left, right);
                        int priority = candidates.Count == 0 ? -1 : ComparePairs(pair, candidates[0]);
                        if (priority > 0) continue;
                        if (priority < 0) candidates.Clear();
                        candidates.Add(pair);
                    }
                }
                if (candidates.Count > 0)
                {
                    int chosen = random(candidates.Count);
                    if (chosen < 0 || chosen >= candidates.Count)
                        throw new InvalidOperationException("Tournament RNG returned an index outside its requested range.");
                    pendingFirst = candidates[chosen].First;
                    pendingSecond = candidates[chosen].Second;
                }
            }
            first = pendingFirst;
            second = pendingSecond;
            return first >= 0;
        }

        internal void Resolve(int first, int second, int loser)
        {
            if (pendingFirst < 0 ||
                !((first == pendingFirst && second == pendingSecond) ||
                  (first == pendingSecond && second == pendingFirst)))
                throw new InvalidOperationException("Only the pending tournament pair can be resolved.");
            if (loser != -1 && loser != first && loser != second)
                throw new ArgumentException("The loser must be a pending entrant, or -1 for a draw.", nameof(loser));

            attempts[pendingFirst, pendingSecond]++;
            matches[first]++;
            matches[second]++;
            if (loser >= 0)
            {
                eliminated[loser] = true;
                wins[loser == first ? second : first]++;
            }
            pendingFirst = pendingSecond = -1;
        }

        internal void Withdraw(int index)
        {
            ValidateIndex(index);
            eliminated[index] = true;
            // A disconnected lifetime never returns to this captured bracket.
            // An unfinished round is canceled without inventing a win or draw.
            if (index == pendingFirst || index == pendingSecond)
                pendingFirst = pendingSecond = -1;
        }

        internal int Matches(int index) { ValidateIndex(index); return matches[index]; }
        internal int Wins(int index) { ValidateIndex(index); return wins[index]; }
        internal bool IsEliminated(int index) { ValidateIndex(index); return eliminated[index]; }

        private int ComparePairs(Pair left, Pair right)
        {
            int leftFirst = left.First, leftSecond = left.Second;
            int rightFirst = right.First, rightSecond = right.Second;
            if (CompareEntrants(leftFirst, leftSecond) > 0)
            { leftFirst = left.Second; leftSecond = left.First; }
            if (CompareEntrants(rightFirst, rightSecond) > 0)
            { rightFirst = right.Second; rightSecond = right.First; }

            int priority = CompareEntrants(leftFirst, rightFirst);
            if (priority != 0) return priority;
            priority = CompareEntrants(leftSecond, rightSecond);
            if (priority != 0) return priority;
            // With equal participation priority, try a new opponent before a
            // drawn pairing retries. All remaining ties are selected randomly.
            return attempts[left.First, left.Second].CompareTo(attempts[right.First, right.Second]);
        }

        private int CompareEntrants(int left, int right)
        {
            int priority = Tier(left).CompareTo(Tier(right));
            if (priority != 0) return priority;
            priority = matches[left].CompareTo(matches[right]);
            return priority != 0 ? priority : wins[left].CompareTo(wins[right]);
        }

        private int Tier(int index) => matches[index] == 0 ? 0 : wins[index] == 0 ? 1 : 2;

        private void ValidateIndex(int index)
        {
            if (index < 0 || index >= matches.Length) throw new ArgumentOutOfRangeException(nameof(index));
        }
    }
}
