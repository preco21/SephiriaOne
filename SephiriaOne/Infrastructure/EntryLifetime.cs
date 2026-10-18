using System;

namespace SephiriaOne
{
    // AddOnLoader replaces its Entry wrapper on each database load without
    // unloading the previous wrapper. Keep one runtime and let only the latest
    // accepted wrapper request cleanup of the original resource owner.
    internal sealed class EntryLifetime
    {
        private enum Phase { Empty, Loading, Active, Unloading, Faulted }
        private Phase phase;
        private Entry owner;
        private Entry latest;

        internal bool BeginLoad(Entry entry)
        {
            if (phase == Phase.Loading || phase == Phase.Unloading)
                throw new InvalidOperationException("SephiriaOne initialization or cleanup is already in progress.");
            if (phase == Phase.Faulted)
                throw new InvalidOperationException("SephiriaOne initialization or cleanup did not finish. Retry unloading the active entry or restart the game before loading again.");
            latest = entry;
            if (phase == Phase.Active) return false;
            owner = entry;
            phase = Phase.Loading;
            return true;
        }

        internal void CompleteLoad() => phase = Phase.Active;

        internal Entry BeginUnload(Entry entry)
        {
            if (owner == null || !ReferenceEquals(latest, entry)) return null;
            if (phase == Phase.Loading || phase == Phase.Unloading)
                throw new InvalidOperationException("SephiriaOne initialization or cleanup is already in progress.");
            phase = Phase.Unloading;
            return owner;
        }

        internal void CompleteUnload()
        {
            owner = null;
            latest = null;
            phase = Phase.Empty;
        }

        // Partial initialization or cleanup may still own hooks and recovery
        // state. Do not allow a new install to stack on it or steal its retry.
        internal void Failed() => phase = Phase.Faulted;
    }
}
