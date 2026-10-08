namespace SephiriaOne
{
    internal static class UpdateText
    {
        internal static string Summary(UpdateSnapshot state)
        {
            if (state == null) return L.T("Updater is unavailable; see Player.log or install from GitHub Releases manually.");
            switch (state.Phase)
            {
                case UpdatePhase.Initializing: return L.T("Preparing local update settings...");
                case UpdatePhase.Checking: return L.T("Checking GitHub Releases...");
                case UpdatePhase.Installing: return L.T("Downloading and verifying the selected update. Keep the game open until installation finishes.");
                case UpdatePhase.RestartRequired: return L.F("Version {0} is installed. Fully restart Sephiria to use it.", state.Installed);
                case UpdatePhase.Available: return L.F("SephiriaOne {0} is available. Use /one update install or the Update button.", state.Candidate.Version);
                case UpdatePhase.Current: return L.T("No newer stable release is available.");
                case UpdatePhase.Failed: return L.T("Update operation failed. See Player.log for details, or install from GitHub Releases manually.");
                default: return L.T("Updates are checked locally. Choose Check for updates to refresh.");
            }
        }
        internal static string Status(UpdateSnapshot state) => Summary(state) + (state == null ? "" :
            "\n\n" + L.F("Running: {0} | Installed for next launch: {1}", state.Running, state.Installed) +
            "\n" + L.F("Automatic update checks: {0}", L.T(state.Automatic ? "On" : "Off")) +
            (state.NextCheck > System.DateTimeOffset.UtcNow ? "\n" + L.F("Next check allowed after (UTC): {0:yyyy-MM-dd HH:mm:ss}", state.NextCheck.ToUniversalTime()) : "") +
            "\n\n" + L.T("Downloading and installing require your Update action. Saved settings and translations are preserved.") +
            "\nhttps://github.com/preco21/SephiriaOne/releases");
    }
}
