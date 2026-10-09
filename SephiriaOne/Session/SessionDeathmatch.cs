using Mirror;

namespace SephiriaOne
{
    internal static partial class SessionSettings
    {
        internal static int DeathmatchDuration => policy.DeathmatchDuration;
        internal static bool TrySetDeathmatchDuration(int seconds, out string message)
        {
            message = DeathmatchCommand.Usage;
            if (!NetworkServer.active || !DeathmatchSettings.ValidDuration(seconds) || !EnsureResourceScope()) return false;
            policy.RecordDeathmatchDuration(seconds); intentRevision++; NotifySettingsChanged();
            message = L.F("Deathmatch duration set to {0} seconds for the next match.", seconds); return true;
        }
        internal static void DeathmatchChanged() { intentRevision++; NotifySettingsChanged(); }
        internal static void EndDeathmatch()
        {
            // Policy-only shutdown must remain available with loading players or
            // unrelated failed writes. No native stats or inventories are touched.
            policy.Record(new FriendlyFireCommand(false)); FriendlyFireKda.SetEnabled(false);
        }
    }
}
