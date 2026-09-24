using System;
using System.Collections.Generic;

namespace SephiriaOne
{
    internal interface IPresentationBinding
    {
        bool IsAlive { get; }
        bool IsActive { get; }
        object Observe();
        ReconcileResult Refresh();
        void Restore();
    }

    // Bind at native creation/open/rebind boundaries. Tick only enrolled views;
    // this registry never discovers scene objects or mutates gameplay state.
    internal sealed class PresentationRegistry
    {
        private readonly Dictionary<object, IPresentationBinding> bindings = new Dictionary<object, IPresentationBinding>();
        private readonly ReconciliationCoordinator<IPresentationBinding> coordinator = new ReconciliationCoordinator<IPresentationBinding>();
        public int Count => bindings.Count;
        public string Diagnostics
        {
            get
            {
                var issues = new List<string>();
                foreach (var binding in bindings.Values)
                    foreach (var status in coordinator.Describe(binding))
                        if (status.State == ReconcileState.Faulted || status.State == ReconcileState.Rejected)
                            issues.Add(status.State + ": " + status.Detail);
                return issues.Count == 0 ? "no binding faults" : string.Join("; ", issues);
            }
        }

        public PresentationRegistry()
        {
            coordinator.Register(new ReconciliationRule<IPresentationBinding>("presentation", SyncDomain.All,
                SyncDomain.None, ReconcileMode.OnChange, view => view.IsAlive && view.IsActive,
                view => view.Observe(), view => view.Refresh()));
        }

        public void Register(object key, IPresentationBinding binding)
        {
            if (bindings.TryGetValue(key, out var previous))
            {
                if (ReferenceEquals(previous, binding)) return;
                Remove(key);
            }
            bindings.Add(key, binding);
        }

        public void Remove(object key)
        {
            if (!bindings.TryGetValue(key, out var previous)) return;
            bindings.Remove(key);
            coordinator.Forget(previous);
            previous.Restore();
        }

        public void Tick()
        {
            // Native callbacks can register/rebind while a refresh is running.
            foreach (var pair in new List<KeyValuePair<object, IPresentationBinding>>(bindings))
            {
                if (!bindings.TryGetValue(pair.Key, out var current) || !ReferenceEquals(current, pair.Value)) continue;
                if (!current.IsAlive) { Remove(pair.Key); continue; }
                coordinator.Reconcile(current);
            }
        }

        public void Clear()
        {
            foreach (var key in new List<object>(bindings.Keys)) Remove(key);
            coordinator.Clear();
        }
    }
}
