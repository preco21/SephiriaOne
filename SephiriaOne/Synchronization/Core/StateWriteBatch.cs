#nullable enable
using System;
using System.Collections.Generic;

namespace SephiriaOne
{
    // Validation is atomic; game writes are not. Retain a journal for explicit recovery.
    internal sealed class StateWriteBatch
    {
        private interface IWrite
        {
            bool Unchanged { get; }
            bool AtTarget { get; }
            string Description { get; }
            void Apply();
        }

        private sealed class Write<T> : IWrite
        {
            private readonly string name;
            private readonly Func<T> read;
            private readonly Action<T> write;
            private readonly T before;
            private readonly T target;
            public Write(string name, Func<T> read, Action<T> write, T target)
            { this.name = name; this.read = read; this.write = write; before = read(); this.target = target; }
            public bool Unchanged => EqualityComparer<T>.Default.Equals(read(), before);
            public bool AtTarget => EqualityComparer<T>.Default.Equals(read(), target);
            public string Description => $"{name}: before={before}, target={target}, observed={read()}";
            public void Apply() { if (!AtTarget) write(target); }
        }

        private readonly List<IWrite> writes = new List<IWrite>();
        private readonly List<Func<bool>> preconditions = new List<Func<bool>>();
        private readonly List<Func<bool>> postconditions = new List<Func<bool>>();
        private readonly Func<bool> identityGuard;
        private bool complete;
        public bool MayHaveWritten { get; private set; }
        public StateWriteBatch(Func<bool> identityGuard) { this.identityGuard = identityGuard; }
        public void Require(Func<bool> unchanged) => preconditions.Add(unchanged);
        public void RequireAfter(Func<bool> valid) => postconditions.Add(valid);
        public void Add<T>(string name, Func<T> read, Action<T> write, T target)
        {
            if (MayHaveWritten || complete) throw new InvalidOperationException("Batch is already executing.");
            writes.Add(new Write<T>(name, read, write, target));
        }

        public bool TryCommit(out string error) => Execute(false, out error);
        public bool TryRecover(out string error) => Execute(true, out error);

        private bool Execute(bool recovery, out string error)
        {
            error = "";
            try
            {
                if (!identityGuard()) { error = "Session, player identity or authority changed. No further writes were made."; return false; }
                if (complete)
                {
                    foreach (var write in writes)
                        if (!write.AtTarget) { error = "A completed batch changed externally; it cannot be replayed."; return false; }
                    return true;
                }
                if (MayHaveWritten && !recovery) { error = "A partial write requires explicit recovery/reset."; return false; }
                if (!recovery)
                    foreach (var precondition in preconditions)
                        if (!precondition()) { error = "Native inputs changed after planning. Retry; no command writes were made."; return false; }
                foreach (var write in writes)
                    if (!write.Unchanged && !(recovery && write.AtTarget))
                    { error = "Native value changed: " + write.Description + ". No further writes were made."; return false; }
                foreach (var write in writes)
                {
                    if (!identityGuard()) throw new InvalidOperationException("Authority or identity changed during application.");
                    if (!write.Unchanged && !write.AtTarget) throw new InvalidOperationException("A native callback changed " + write.Description);
                    if (!write.AtTarget) { MayHaveWritten = true; write.Apply(); }
                }
                foreach (var write in writes)
                    if (!write.AtTarget) throw new InvalidOperationException("Readback mismatch: " + write.Description);
                foreach (var postcondition in postconditions)
                    if (!postcondition()) throw new InvalidOperationException("Native planning inputs changed during application.");
                if (!identityGuard()) throw new InvalidOperationException("Authority or identity changed during readback.");
                complete = true;
                return true;
            }
            catch (Exception exception)
            {
                error = (MayHaveWritten ? "Partial or unverified state write: " : "State write preparation failed: ") + exception.Message;
                return false;
            }
        }

        public string Describe()
        {
            var descriptions = new List<string>();
            foreach (var write in writes)
            {
                try { descriptions.Add(write.Description); }
                catch (Exception error) { descriptions.Add("Readback unavailable: " + error.Message); }
            }
            return string.Join("; ", descriptions);
        }
    }
}
