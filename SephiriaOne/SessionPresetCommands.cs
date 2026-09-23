using System.Collections.Generic;
using System.Globalization;
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
            Report("Loaded saved preset for this hosted session. Use /mod status to inspect it.", true);
        }

        public static bool TryExecutePreset(PresetAction action, out string[] messages)
        {
            messages = new[] { "Only the host can inspect or save session settings." };
            if (!NetworkServer.active) return false;
            messages = new[] { "Session settings controller is not loaded." };
            if (!enabled || store == null) return false;
            if (action == PresetAction.Status) return DescribeStatus(out messages);
            if (action == PresetAction.Forget)
            {
                bool removed = store.TryForget(out string error);
                messages = new[] { removed ? "Removed the saved preset. Current session settings are unchanged." : error };
                return removed;
            }
            if (action != PresetAction.Save) { messages = new[] { PresetCommand.Usage }; return false; }
            if (!Prepare(out string failure)) { messages = new[] { failure }; return false; }
            bool saved = store.TrySave(policy, out string saveError);
            messages = new[] { saved ? "Saved current session settings for future hosted sessions: " + store.FilePath : saveError };
            return saved;
        }

        private static bool DescribeStatus(out string[] messages)
        {
            // Do not call Synchronize here: inspecting must not apply pending joins.
            var lines = new List<string>();
            bool sameSession = dungeon && ReferenceEquals(dungeon, DungeonManager.Instance);
            IReadOnlyList<string> active = sameSession ? policy.DescribeSettings() : new string[0];
            lines.Add("Active session settings: " + (active.Count == 0 ? "none." : string.Join("; ", active)));
            int ready = 0;
            foreach (PlayerSpawner spawner in PlayerSpawner.MultiplayerList)
            {
                if (!IsReady(spawner)) continue;
                ready++;
                PlayerAvatar player = spawner.PlayerAvatar;
                string label = "Player #" + player.netId + ": ";
                player.customStats.TryGetValue(FountainPoints.ContributionKey, out int fountainOffset);
                lines.Add(label + "Fountain=" + player.Inventory.dimensionPocket + " (addon " + Signed(fountainOffset) + ").");
                var stats = new List<string>();
                foreach (StatDefinition stat in StatCatalog.All)
                {
                    player.customStats.TryGetValue(stat.Marker, out int contribution);
                    string value = stat.Display(player.GetCustomStatUnsafe(stat.Key)).ToString("0.##", CultureInfo.InvariantCulture);
                    stats.Add(stat.Name + "=" + value + (contribution == 0 ? "" : " (base adjustment " + Signed(contribution) + ")") +
                        (sameSession && IsRelativeStatSuspended(player, stat) ? " (relative offset suspended)" : ""));
                }
                lines.Add(label + string.Join(", ", stats) + ". Units: /stats list.");
                var choices = new List<string>();
                string[] names = { "item", "weapon", "miracle" };
                for (int i = 0; i < ChoiceCommand.Keys.Length; i++)
                {
                    string key = ChoiceCommand.Keys[i];
                    player.customStats.TryGetValue("SEPHIRIAONE_" + key, out int contribution);
                    choices.Add(names[i] + "=" + player.GetCustomStatUnsafe(key) + " (addon " + Signed(contribution) + ")");
                }
                lines.Add(label + "extra choices: " + string.Join(", ", choices));
            }
            if (ready == 0) lines.Add("No ready players; current values are unavailable.");
            bool valid = store.TryLoad(out SessionPolicy saved, out bool exists, out string error);
            if (!valid) lines.Add(error);
            else if (!exists) lines.Add("Saved preset: none. Use /mod save to store active settings.");
            else lines.Add("Saved for future hosted sessions: " + (saved.HasChanges ? string.Join("; ", saved.DescribeSettings()) : "empty (no adjustments)."));
            lines.Add("Current values include native bonuses; base adjustments are tracked raw stat units. Commands and resets change the active session; /mod save updates the saved copy.");
            messages = lines.ToArray();
            return valid;
        }

        private static string Signed(int value) => value.ToString("+0;-0;0", CultureInfo.InvariantCulture);
    }
}
