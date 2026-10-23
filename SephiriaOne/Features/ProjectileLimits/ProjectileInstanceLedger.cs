using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace SephiriaOne
{
    internal sealed class ProjectileInstanceLedger<TOwner> where TOwner : class
    {
        // Used on the native gameplay thread. Owners are connection receipts, never account IDs.
        private readonly Dictionary<TOwner, OwnerState> owners =
            new Dictionary<TOwner, OwnerState>(OwnerIdentityComparer.Instance);
        // A short-lived source often returns to zero between emissions. Retain
        // one empty counter container, without retaining its owner or any leases.
        // Never recycle Clear/Forget states: their outstanding leases are stale.
        private OwnerState spare;

        public int OwnerCount => owners.Count;

        public int Count(TOwner owner)
        {
            if (owner == null) throw new ArgumentNullException(nameof(owner));
            return owners.TryGetValue(owner, out var state) ? state.Total : 0;
        }

        public int Count(TOwner owner, string source)
        {
            Validate(owner, source);
            return owners.TryGetValue(owner, out var state) && state.Sources.TryGetValue(source, out int count)
                ? count : 0;
        }

        // Reserve before calling native code, which may synchronously create children.
        // Release the returned lease if creation throws or produces no live instance.
        public bool TryReserve(TOwner owner, string source, int totalCap, int sourceCap, out Lease lease)
        {
            Validate(owner, source);
            lease = null;
            if (totalCap <= 0 || sourceCap <= 0) return false;
            if (owners.TryGetValue(owner, out var state))
            {
                if (state.Total >= totalCap) return false;
                if (state.Sources.TryGetValue(source, out int count) && count >= sourceCap) return false;
            }
            lease = Add(owner, source, state);
            return true;
        }

        // Enabling limits accounts for already-live instances without deleting excess instances.
        public Lease Adopt(TOwner owner, string source)
        {
            Validate(owner, source);
            owners.TryGetValue(owner, out var state);
            return Add(owner, source, state);
        }

        public bool Forget(TOwner owner)
        {
            if (owner == null) throw new ArgumentNullException(nameof(owner));
            return owners.Remove(owner);
        }

        public void Clear() { owners.Clear(); spare = null; }

        private static void Validate(TOwner owner, string source)
        {
            if (owner == null) throw new ArgumentNullException(nameof(owner));
            if (source == null) throw new ArgumentNullException(nameof(source));
        }

        private Lease Add(TOwner owner, string source, OwnerState state)
        {
            if (state == null)
            {
                state = spare ?? new OwnerState();
                spare = null;
                owners.Add(owner, state);
            }
            state.Sources.TryGetValue(source, out int count);
            int total = checked(state.Total + 1);
            int sourceCount = checked(count + 1);
            var lease = new Lease(this, owner, source, state);
            state.Sources[source] = sourceCount;
            state.Total = total;
            return lease;
        }

        private void Release(TOwner owner, string source, OwnerState expectedState)
        {
            // Clear/Forget retire the state object. An old lease cannot debit its replacement.
            if (!owners.TryGetValue(owner, out var state) || !ReferenceEquals(state, expectedState)) return;
            int count = state.Sources[source];
            if (count == 1) state.Sources.Remove(source);
            else state.Sources[source] = count - 1;
            if (--state.Total == 0)
            {
                owners.Remove(owner);
                // All leases have released exactly once and Sources is empty.
                // Only this path can safely recycle the state identity.
                if (spare == null) spare = state;
            }
        }

        public sealed class Lease : IDisposable
        {
            private ProjectileInstanceLedger<TOwner> ledger;
            private OwnerState state;

            // Keep provenance readable after parent destruction for delayed child emissions.
            public TOwner Owner { get; }
            public string Source { get; }

            internal Lease(ProjectileInstanceLedger<TOwner> ledger, TOwner owner, string source, OwnerState state)
            {
                this.ledger = ledger;
                this.state = state;
                Owner = owner;
                Source = source;
            }

            public void Release()
            {
                var currentLedger = ledger;
                if (currentLedger == null) return;
                var currentState = state;
                ledger = null;
                state = null;
                currentLedger.Release(Owner, Source, currentState);
            }

            public void Dispose() => Release();
        }

        internal sealed class OwnerState
        {
            public int Total;
            public readonly Dictionary<string, int> Sources = new Dictionary<string, int>(StringComparer.Ordinal);
        }

        private sealed class OwnerIdentityComparer : IEqualityComparer<TOwner>
        {
            public static readonly OwnerIdentityComparer Instance = new OwnerIdentityComparer();
            public bool Equals(TOwner left, TOwner right) => ReferenceEquals(left, right);
            public int GetHashCode(TOwner owner) => RuntimeHelpers.GetHashCode(owner);
        }
    }
}
