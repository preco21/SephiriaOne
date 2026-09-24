using SephiriaOne;

internal static class PresentationRegistryTests
{
    private sealed class View : IPresentationBinding
    {
        public bool IsAlive { get; set; } = true;
        public bool IsActive { get; set; } = true;
        public int Source, Rendered, Writes, Restores;
        public object Observe() => (Source, Rendered);
        public ReconcileResult Refresh() { Rendered = Source; Writes++; return ReconcileResult.Applied(); }
        public void Restore() { Restores++; }
    }
    internal static int Run()
    {
        int checks = 0;
        void Check(bool value) { if (!value) throw new Exception("Presentation registry regression"); checks++; }
        var registry = new PresentationRegistry();
        var first = new View { Source = 3 };
        registry.Register("row", first);
        registry.Tick(); registry.Tick();
        Check(first.Rendered == 3 && first.Writes == 1);
        first.Source = 4; registry.Tick();
        Check(first.Rendered == 4 && first.Writes == 2);
        first.IsActive = false; first.Source = 5; registry.Tick();
        Check(first.Rendered == 4);
        first.IsActive = true; registry.Tick();
        Check(first.Rendered == 5);
        var second = new View { Source = 8 };
        registry.Register("row", second); registry.Tick();
        Check(first.Restores == 1 && second.Rendered == 8);
        second.IsAlive = false; registry.Tick();
        Check(registry.Count == 0 && second.Restores == 1);
        registry.Register("a", new View()); registry.Clear();
        Check(registry.Count == 0);
        return checks;
    }
}
