namespace SephiriaOne
{
    internal static class ItemRestrictionFeature
    {
        internal static bool Available = true;
        internal static bool Enabled;
        internal static string Fault;
        internal static bool FailWrite;
        internal static void Bind(DungeonManager dungeon, bool enabled) => Enabled = dungeon != null && enabled;
        internal static void SetEnabled(bool enabled)
        {
            if (FailWrite) { Fault = "failed write"; throw new InvalidOperationException(Fault); }
            Enabled = enabled; Fault = null;
        }
        internal static void Clear() { Enabled = false; Fault = null; FailWrite = false; }
    }
}
