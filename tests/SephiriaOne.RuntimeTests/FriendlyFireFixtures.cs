namespace SephiriaOne
{
    internal static class DeathmatchFeature { internal static bool Available = true; }
    internal static class ReviveAllFeature { internal static bool Available = true; }
    internal static class FriendlyFireFeature
    {
        internal static bool Available = true;
    }
    internal static class PvpCoverFeature { internal static bool Available = true; }
    internal static class PvpCoverRuntime { internal static void Tick() { } internal static void Clear() { } internal static string Describe() => ""; }
    internal static class CombatModeFeature { internal static bool Available = true; }
    internal static class LobbyRegenFeature { internal static bool Available = true; }
}
