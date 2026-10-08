namespace SephiriaOne
{
    internal static class UpdateFeature
    {
        internal static UpdateCoordinator Service { get; private set; }
        internal static UpdateSnapshot Snapshot => Service?.Snapshot;
        internal static void Bind(UpdateCoordinator service) { Stop(); Service = service; }
        internal static void Stop() { Service?.Dispose(); Service = null; }
    }
}
