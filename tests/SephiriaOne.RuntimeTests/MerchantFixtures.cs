namespace SephiriaOne
{
    internal static class MerchantFeature
    {
        public static bool Available = true;
        public static int RefreshCalls;
        public static int ClearCalls;
        public static Action OnRefresh;
        public static void Refresh() { RefreshCalls++; OnRefresh?.Invoke(); }
        public static void Clear() { ClearCalls++; }
    }
}
