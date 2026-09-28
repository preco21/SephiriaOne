using System;
using System.Collections.Generic;
using System.Globalization;
using Mirror;

namespace SephiriaOne
{
    internal static partial class SessionSettings
    {
        private sealed class SavedPresetSnapshot
        {
            public bool Valid { get; }
            public bool Exists { get; }
            public SessionPolicy Policy { get; }
            public string Error { get; }
            public SavedPresetSnapshot(bool valid, bool exists, SessionPolicy saved, string error)
            { Valid = valid; Exists = exists; Policy = saved; Error = error; }
        }

        private static SavedPresetSnapshot savedPresetSnapshot;

        private static void InvalidateSavedPresetSnapshot() => savedPresetSnapshot = null;

        private static SavedPresetSnapshot ReadSavedPreset(bool refresh)
        {
            if (savedPresetSnapshot == null || refresh)
            {
                bool valid = store.TryLoad(out SessionPolicy saved, out bool exists, out string error);
                savedPresetSnapshot = new SavedPresetSnapshot(valid, exists, saved, error);
            }
            return savedPresetSnapshot;
        }

        // No Prepare/Synchronize call is allowed here: inspecting a pending join
        // must never enroll it or apply retained settings. Only file reads cache.
        public static SettingsSnapshot ReadSnapshot(bool refreshSaved = false)
        {
            bool host = NetworkServer.active;
            bool loaded = enabled && store != null;
            bool sameSession = host && loaded && dungeon && dungeon.isServer && dungeon.netId != 0 &&
                ReferenceEquals(dungeon, DungeonManager.Instance);
            bool processing = synchronizing || applyingCommand;
            string fault = sameSession && failedBatch != null ? failedFeature : "";
            bool canSave = sameSession && !processing && failedBatch == null;
            bool canForget = host && loaded;
            string unavailable = !host ? "Only the host can inspect or save session settings." :
                !loaded ? "Session settings controller is not loaded." :
                !sameSession ? "Host session data is not ready. Enter town or a run first." :
                processing ? "Host synchronization is already processing. Retry in a moment." :
                failedBatch != null ? "A " + failedFeature + " write is faulted. Use the family reset to recover, or end this session." : "";

            var lines = new List<string>();
            var currentPlayers = new List<PlayerSettingsSnapshot>();
            if (!host || !loaded)
            {
                lines.Add(unavailable);
                return new SettingsSnapshot(null, epoch, runGeneration, intentRevision, host, false, false, false,
                    ChoiceFeature.Available, false, unavailable, "", lines, currentPlayers,
                    Array.Empty<string>(), Array.Empty<string>(), unavailable);
            }

            IReadOnlyList<string> active = sameSession ? policy.DescribeSettings() : Array.Empty<string>();
            lines.Add("Active session settings: " + (active.Count == 0 ? "none." : string.Join("; ", active)));
            lines.Add(DescribeRabbit(sameSession ? policy.RabbitPotions : default) +
                (RabbitPotionFeature.Available ? "" : " Potion hooks unavailable; native behavior continues. See Player.log."));
            lines.Add(DescribeMerchant(sameSession && policy.MerchantSpawns, sameSession ? policy.MerchantSpawnChance : MerchantCommand.DefaultChance) +
                (MerchantFeature.Available ? "" : " Merchant hooks unavailable; native behavior continues. See Player.log."));
            DescribeSynchronization(lines, sameSession);
            if (!SessionBoundaryFeature.Available)
                lines.Add("Fountain grant synchronization guard is unavailable; frame polling remains active. Check Player.log.");

            bool allReady = true;
            var seen = new HashSet<PlayerAvatar>(ReferenceComparer<PlayerAvatar>.Instance);
            foreach (PlayerSpawner spawner in PlayerSpawner.MultiplayerList)
            {
                if (!IsReady(spawner))
                {
                    // Match HostStateAdapter.TryCollect's command participants.
                    if (spawner && spawner.isServer && spawner.netId != 0) allReady = false;
                    continue;
                }
                PlayerAvatar player = spawner.PlayerAvatar;
                if (!seen.Add(player)) continue;
                string label = "Player #" + player.netId + ": ";
                player.customStats.TryGetValue(FountainPoints.ContributionKey, out int fountainOffset);
                lines.Add(label + "Fountain=" + player.Inventory.dimensionPocket + " (addon " + Signed(fountainOffset) + ").");
                var stats = new Dictionary<string, decimal>();
                var statDescriptions = new List<string>();
                foreach (StatDefinition stat in StatCatalog.All)
                {
                    player.customStats.TryGetValue(stat.Marker, out int contribution);
                    decimal value = stat.Display(player.GetCustomStatUnsafe(stat.Key));
                    stats.Add(stat.Name, value);
                    statDescriptions.Add(stat.Name + "=" + value.ToString("0.##", CultureInfo.InvariantCulture) +
                        (contribution == 0 ? "" : " (base adjustment " + Signed(contribution) + ")") +
                        (sameSession && IsRelativeStatState(player, stat, ReconcileState.NativeFallback) ? " (native fallback)" :
                            sameSession && IsRelativeStatState(player, stat, ReconcileState.Suspended) ? " (relative setting suspended)" : ""));
                }
                lines.Add(label + string.Join(", ", statDescriptions) + ". Units: /stats list.");
                var choices = new Dictionary<string, int>();
                var choiceDescriptions = new List<string>();
                string[] names = { "item", "weapon", "miracle" };
                for (int i = 0; i < ChoiceCommand.Keys.Length; i++)
                {
                    string key = ChoiceCommand.Keys[i];
                    player.customStats.TryGetValue("SEPHIRIAONE_" + key, out int contribution);
                    int value = player.GetCustomStatUnsafe(key);
                    choices.Add(names[i], value);
                    choiceDescriptions.Add(names[i] + "=" + value + " (addon " + Signed(contribution) + ")");
                }
                lines.Add(label + "extra choices: " + string.Join(", ", choiceDescriptions));
                var resources = new Dictionary<string, string>();
                foreach (var definition in ResourceCatalog.All)
                {
                    string description;
                    try
                    {
                        var value = ResourceNative.Capture(player, definition.Kind);
                        if (!ResourcePlanner.TryValue(value, true, out int native) || !ResourcePlanner.TryValue(value, false, out int total))
                            description = "Native arithmetic unavailable.";
                        else if (definition.StartingOnly)
                        {
                            int target = native;
                            bool valid = !sameSession || !policy.Resources.TryGet(definition.Kind, out ResourceSetting setting) ||
                                setting.TryTarget(native, definition.Minimum, definition.Maximum, out target, out _);
                            int balance = definition.Kind == ResourceKind.Dice ? player.rerollDice : player.currentMoney;
                            bool leaves = definition.Kind == ResourceKind.Leaves;
                            description = "Current balance " + balance + (leaves ? "; starting allowance " : "; next fresh start ") +
                                (valid ? target.ToString() : "invalid") + " (native " + native + "). " +
                                (leaves ? "Pending first departure uses the latest setting; already paid leaves cannot be reclaimed." :
                                    "Grants already begun stay unchanged.");
                        }
                        else description = "Total " + total + " (native " + native + ", addon " + Signed(value.Owned) +
                            "); " + (definition.Kind == ResourceKind.Slots ? "minimum safe capacity " : "allocated/selected ") + value.MinimumSafe +
                            (value.Busy ? "; guest menu pending" : "");
                    }
                    catch (System.Exception error) { description = "Unavailable: " + error.Message; }
                    if (!ResourceFeature.IsAvailable(definition.Kind)) description += " " + ResourceFeature.UnavailableReason(definition.Kind);
                    resources.Add(definition.Name, description);
                    lines.Add(label + definition.Label + ": " + description);
                }
                currentPlayers.Add(new PlayerSettingsSnapshot(player.netId, player.playerNameSource,
                    player.Inventory.dimensionPocket, fountainOffset, stats, choices, resources));
            }
            if (currentPlayers.Count == 0) lines.Add("No ready players; current values are unavailable.");
            if (unavailable.Length == 0)
            {
                if (!allReady) unavailable = "A player is still initializing. Retry in a moment; no command writes were made.";
                else if (currentPlayers.Count == 0) unavailable = "No ready players found. Enter town or a run first.";
            }
            SavedPresetSnapshot saved = ReadSavedPreset(refreshSaved);
            IReadOnlyList<string> savedSettings = saved.Valid && saved.Exists ? saved.Policy.DescribeSettings() : Array.Empty<string>();
            string savedSummary = !saved.Valid ? saved.Error : !saved.Exists ?
                "Saved preset: none. Use /one save to store active settings." :
                "Saved for future hosted sessions: " + (saved.Policy.HasChanges ?
                    string.Join("; ", savedSettings) : "empty (no adjustments).");
            lines.Add(savedSummary);
            lines.Add("Current values include native bonuses; base adjustments are tracked raw stat units. Commands and resets change the active session; /one save updates the saved copy.");

            return new SettingsSnapshot(sameSession ? dungeon : null, epoch, runGeneration, intentRevision, host,
                unavailable.Length == 0, canSave, canForget, ChoiceFeature.Available, saved.Valid, unavailable, fault,
                lines, currentPlayers, active, savedSettings, savedSummary, sameSession ? policy.RabbitPotions : default,
                RabbitPotionFeature.Available, RabbitDescriptionFeature.Available,
                sameSession && policy.MerchantSpawns, MerchantFeature.Available,
                sameSession ? policy.MerchantSpawnChance : MerchantCommand.DefaultChance);
        }

        private static string Signed(int value) => value.ToString("+0;-0;0", CultureInfo.InvariantCulture);
    }
}
