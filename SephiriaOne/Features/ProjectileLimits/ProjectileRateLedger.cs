using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace SephiriaOne
{
    // Admission-time token buckets: one second of capacity, no timers or queued work.
    // State belongs to a connection receipt, independent of projectile lifetimes.
    internal sealed class ProjectileRateLedger<TOwner> where TOwner : class
    {
        private readonly Dictionary<TOwner, OwnerState> owners =
            new Dictionary<TOwner, OwnerState>(IdentityComparer.Instance);
        private int playerRate = 64, sourceRate = 12;

        public void Configure(int total, int source, double now)
        {
            if (total < 1 || source < 1) throw new ArgumentOutOfRangeException();
            if (total == playerRate && source == sourceRate) return;
            // Settings edits are rare: settle all buckets at the OLD rate first.
            // Do not refill empty credit just because a setting changed.
            foreach (OwnerState owner in owners.Values)
            {
                if (ValidTime(now)) owner.Total.Refill(now, playerRate);
                owner.Total.Credit = Math.Min(owner.Total.Credit, total);
                foreach (Bucket bucket in owner.Sources.Values)
                {
                    if (ValidTime(now)) bucket.Refill(now, sourceRate);
                    bucket.Credit = Math.Min(bucket.Credit, source);
                }
            }
            playerRate = total; sourceRate = source;
        }

        public bool TryConsume(TOwner owner, string source, double now)
        {
            if (owner == null || source == null || !ValidTime(now)) return false;
            if (!owners.TryGetValue(owner, out OwnerState state))
            {
                state = new OwnerState(playerRate, now);
                owners.Add(owner, state);
            }
            state.Total.Refill(now, playerRate);
            if (state.Total.Credit < 1) return false;
            if (!state.Sources.TryGetValue(source, out Bucket bucket))
            {
                bucket = new Bucket(sourceRate, now);
                state.Sources.Add(source, bucket);
            }
            bucket.Refill(now, sourceRate);
            if (bucket.Credit < 1) return false;
            state.Total.Credit -= 1;
            bucket.Credit -= 1;
            return true;
        }

        public void Forget(TOwner owner) { if (owner != null) owners.Remove(owner); }
        public void Clear() => owners.Clear();
        private static bool ValidTime(double value) => !double.IsNaN(value) && !double.IsInfinity(value);

        private sealed class Bucket
        {
            internal double Credit, Last;
            internal Bucket(int capacity, double now) { Credit = capacity; Last = now; }
            internal void Refill(double now, int rate)
            {
                if (now <= Last) return;
                Credit = Math.Min(rate, Credit + (now - Last) * rate);
                Last = now;
            }
        }
        private sealed class OwnerState
        {
            internal readonly Bucket Total;
            internal readonly Dictionary<string, Bucket> Sources = new Dictionary<string, Bucket>(StringComparer.Ordinal);
            internal OwnerState(int capacity, double now) { Total = new Bucket(capacity, now); }
        }
        private sealed class IdentityComparer : IEqualityComparer<TOwner>
        {
            internal static readonly IdentityComparer Instance = new IdentityComparer();
            public bool Equals(TOwner left, TOwner right) => ReferenceEquals(left, right);
            public int GetHashCode(TOwner value) => RuntimeHelpers.GetHashCode(value);
        }
    }
}
