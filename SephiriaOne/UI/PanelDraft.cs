#nullable enable
namespace SephiriaOne
{
    internal sealed class PanelDraft
    {
        private object? scope;
        private long scopeEpoch, runGeneration;
        private bool observed;
        public string Text { get; private set; } = "";
        public void Edit(string value) => Text = value;
        public void Clear() => Text = "";
        public bool Observe(object? identity, long epoch, long run)
        {
            bool changed = !observed || !ReferenceEquals(scope, identity) || scopeEpoch != epoch || runGeneration != run;
            if (changed) Clear();
            scope = identity; scopeEpoch = epoch; runGeneration = run; observed = true;
            return changed;
        }
        public bool IsCurrent(object? identity, long epoch, long run) => observed && identity != null &&
            ReferenceEquals(scope, identity) && scopeEpoch == epoch && runGeneration == run;
    }
}
