using System;
using System.Collections.Generic;

namespace SephiriaOne
{
    internal sealed class PanelControlLifetime<T> where T : class
    {
        private readonly T control;
        private readonly Func<IEnumerable<IEnumerable<T>>> readStack;
        public PanelControlLifetime(T control, Func<IEnumerable<IEnumerable<T>>> readStack)
        { this.control = control; this.readStack = readStack; }
        // Native callbacks can throw after changing membership. A local flag
        // cannot establish whether removal (and its counter decrement) is due.
        public bool IsRegistered
        {
            get
            {
                foreach (var group in readStack())
                    foreach (T item in group)
                        if (ReferenceEquals(item, control)) return true;
                return false;
            }
        }
        public void Open(Action open)
        {
            if (IsRegistered) throw new InvalidOperationException("Panel cleanup is still pending.");
            open();
        }
        public void Close(Action remove, Action deactivate)
        {
            try { if (IsRegistered) remove(); }
            finally { deactivate(); }
        }
    }
}
