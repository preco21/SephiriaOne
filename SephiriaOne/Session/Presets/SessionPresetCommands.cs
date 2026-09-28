using System.Collections.Generic;
using Mirror;

namespace SephiriaOne
{
    internal static partial class SessionSettings
    {
        private static PresetStore store;

        private static void LoadPreset()
        {
            if (!store.TryLoad(out SessionPolicy saved, out bool exists, out string error))
            {
                Report(error, false);
                return;
            }
            if (!exists) return;
            policy = saved;
            MerchantFeature.Refresh();
            NotifySettingsChanged();
            Report(L.T("Loaded saved preset for this hosted session. Use /one status to inspect it."), true);
        }

        public static bool TryExecutePreset(PresetAction action, out string[] messages)
        {
            messages = new[] { L.T("Only the host can inspect or save session settings.") };
            if (!NetworkServer.active) return false;
            messages = new[] { L.T("Session settings controller is not loaded.") };
            if (!enabled || store == null) return false;
            if (action == PresetAction.Status) return DescribeStatus(out messages);
            if (action == PresetAction.Forget)
            {
                bool removed = store.TryForget(out string error);
                InvalidateSavedPresetSnapshot();
                messages = new[] { removed ? L.T("Removed the saved preset. Current session settings are unchanged.") : error };
                return removed;
            }
            if (action != PresetAction.Save) { messages = new[] { PresetCommand.Usage }; return false; }
            if (!Prepare(out string failure)) { messages = new[] { failure }; return false; }
            if (failedBatch != null)
            { messages = new[] { L.F("Resolve the faulted {0} write before saving. Inspect /one status.", failedFeature) }; return false; }
            bool saved = store.TrySave(policy, out string saveError);
            InvalidateSavedPresetSnapshot();
            messages = new[] { saved ? L.F("Saved current session settings for future hosted sessions: {0}", store.FilePath) : saveError };
            return saved;
        }

        private static bool DescribeStatus(out string[] messages)
        {
            SettingsSnapshot snapshot = ReadSnapshot(true);
            messages = new List<string>(snapshot.Lines).ToArray();
            return snapshot.SavedValid;
        }
    }
}
