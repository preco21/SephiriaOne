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
        // Ordinary panel pages need live values and permissions, but only Status
        // and chat consume the formatted diagnostic report. Do not cache values:
        // native edits and language changes can occur without an intent revision.
        public static SettingsSnapshot ReadSnapshot(bool refreshSaved = false, bool includeDiagnostics = true)
        {
            bool host = NetworkServer.active;
            bool loaded = enabled && store != null;
            bool sameSession = host && loaded && dungeon && dungeon.isServer && dungeon.netId != 0 &&
                ReferenceEquals(dungeon, DungeonManager.Instance);
            bool processing = synchronizing || applyingCommand;
            string fault = sameSession && failedBatch != null ? failedFeature : "";
            bool canSave = sameSession && !processing && failedBatch == null;
            bool canMutate = canSave;
            bool canForget = host && loaded;
            string unavailable = !host ? L.T("Only the host can inspect or save session settings.") :
                !loaded ? L.T("Session settings controller is not loaded.") :
                !sameSession ? L.T("Host session data is not ready. Enter town or a run first.") :
                processing ? L.T("Host synchronization is already processing. Retry in a moment.") :
                failedBatch != null ? L.F("A {0} write is faulted. Use the family reset to recover, or end this session.", failedFeature) : "";

            var lines = new List<string>();
            var currentPlayers = new List<PlayerSettingsSnapshot>();
            if (!host || !loaded)
            {
                if (includeDiagnostics) lines.Add(unavailable);
                return new SettingsSnapshot(null, epoch, runGeneration, intentRevision, host, false, false, false,
                    ChoiceFeature.Available, false, unavailable, "", lines, currentPlayers,
                    Array.Empty<string>(), Array.Empty<string>(), unavailable);
            }

            IReadOnlyList<string> active = sameSession ? policy.DescribeSettings() : Array.Empty<string>();
            if (includeDiagnostics)
            {
                lines.Add(L.T("Active session settings: ") + (active.Count == 0 ? L.T("none.") : string.Join("; ", active)));
                lines.Add(DescribeRabbit(sameSession ? policy.RabbitPotions : default) +
                    (RabbitPotionFeature.Available ? "" : L.T(" Potion hooks unavailable; native behavior continues. See Player.log.")) +
                    (RabbitLevelUpFeature.Available ? "" : L.T(" Level-up potion hooks unavailable; no level-up reward is granted. See Player.log.")));
                lines.Add(DescribeMerchants(sameSession ? policy.Merchants.Snapshot : null) +
                    (MerchantFeature.Available ? "" : L.T(" Merchant hooks unavailable; native behavior continues. See Player.log.")));
                DescribeSynchronization(lines, sameSession);
                lines.Add(DescribeFriendlyFire(sameSession ? policy.FriendlyFire : default) +
                    (FriendlyFireFeature.Available ? "" : L.T(" Friendly-fire hooks unavailable; see Player.log.")));
                lines.Add(DescribeItemUnlock(sameSession && policy.ItemUnlock) +
                    (ItemRestrictionFeature.Available ? "" : L.T(" Item restriction hooks unavailable; see Player.log.")));
                if (!SessionBoundaryFeature.Available)
                    lines.Add(L.T("Fountain grant synchronization guard is unavailable; frame polling remains active. Check Player.log."));
            }

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
                string label = includeDiagnostics ? L.T("Player #") + player.netId + ": " : "";
                player.customStats.TryGetValue(FountainPoints.ContributionKey, out int fountainOffset);
                if (includeDiagnostics) lines.Add(label + L.F("Fountain={0} (addon {1}).", player.Inventory.dimensionPocket, Signed(fountainOffset)));
                var stats = new Dictionary<string, decimal>();
                var statDescriptions = includeDiagnostics ? new List<string>() : null;
                foreach (StatDefinition stat in StatCatalog.All)
                {
                    decimal value = stat.Display(player.GetCustomStatUnsafe(stat.Key));
                    stats.Add(stat.Name, value);
                    if (includeDiagnostics)
                    {
                        player.customStats.TryGetValue(stat.Marker, out int contribution);
                        statDescriptions.Add(stat.Name + "=" + value.ToString("0.##", CultureInfo.InvariantCulture) +
                            (contribution == 0 ? "" : L.T(" (base adjustment ") + Signed(contribution) + ")") +
                            (sameSession && IsRelativeStatState(player, stat, ReconcileState.NativeFallback) ? L.T(" (native fallback)") :
                                sameSession && IsRelativeStatState(player, stat, ReconcileState.Suspended) ? L.T(" (relative setting suspended)") : ""));
                    }
                }
                if (includeDiagnostics) lines.Add(label + string.Join(", ", statDescriptions) + L.T(". Units: /stats list."));
                var choices = new Dictionary<string, int>();
                var choiceDescriptions = includeDiagnostics ? new List<string>() : null;
                string[] names = { "item", "weapon", "miracle" };
                for (int i = 0; i < ChoiceCommand.Keys.Length; i++)
                {
                    string key = ChoiceCommand.Keys[i];
                    int value = player.GetCustomStatUnsafe(key);
                    choices.Add(names[i], value);
                    if (includeDiagnostics)
                    {
                        player.customStats.TryGetValue("SEPHIRIAONE_" + key, out int contribution);
                        choiceDescriptions.Add(L.F("{0}={1} (addon {2})", names[i], value, Signed(contribution)));
                    }
                }
                if (includeDiagnostics) lines.Add(label + L.T("extra choices: ") + string.Join(", ", choiceDescriptions));
                var resources = new Dictionary<string, string>();
                foreach (var definition in ResourceCatalog.All)
                {
                    string description;
                    try
                    {
                        var value = ResourceNative.Capture(player, definition.Kind);
                        if (!ResourcePlanner.TryValue(value, true, out int native) || !ResourcePlanner.TryValue(value, false, out int total))
                            description = L.T("Native arithmetic unavailable.");
                        else if (definition.StartingOnly)
                        {
                            int target = native;
                            bool valid = !sameSession || !policy.Resources.TryGet(definition.Kind, out ResourceSetting setting) ||
                                setting.TryTarget(native, definition.Minimum, definition.Maximum, out target, out _);
                            int balance = definition.Kind == ResourceKind.Dice ? player.rerollDice : player.currentMoney;
                            bool leaves = definition.Kind == ResourceKind.Leaves;
                            description = L.T("Current balance ") + balance + (leaves ? L.T("; starting allowance ") : L.T("; next fresh start ")) +
                                (valid ? target.ToString() : L.T("invalid")) + L.T(" (native ") + native + "). " +
                                (leaves ? L.T("Pending first departure uses the latest setting; already paid leaves cannot be reclaimed.") :
                                    L.T("Grants already begun stay unchanged."));
                        }
                        else description = L.T("Total ") + total + L.T(" (native ") + native + L.T(", addon ") + Signed(value.Owned) +
                            "); " + (definition.Kind == ResourceKind.Slots ? L.T("minimum safe capacity ") : L.T("allocated/selected ")) + value.MinimumSafe +
                            (value.Busy ? L.T("; guest menu pending") : "");
                    }
                    catch (System.Exception error) { description = L.T("Unavailable: ") + error.Message; }
                    if (!ResourceFeature.IsAvailable(definition.Kind)) description += " " + ResourceFeature.UnavailableReason(definition.Kind);
                    resources.Add(definition.Name, description);
                    if (includeDiagnostics) lines.Add(label + L.T(definition.Label) + ": " + description);
                }
                currentPlayers.Add(PlayerSettingsSnapshot.FromOwnedCapture(player.netId, player.playerNameSource,
                    player.Inventory.dimensionPocket, fountainOffset, stats, choices, resources));
            }
            if (includeDiagnostics && currentPlayers.Count == 0) lines.Add(L.T("No ready players; current values are unavailable."));
            if (canMutate)
            {
                if (!allReady) unavailable = L.T("A player is still initializing. Retry in a moment; no command writes were made.");
                else if (currentPlayers.Count == 0) unavailable = L.T("No ready players found. Enter town or a run first.");
                canMutate = allReady && currentPlayers.Count > 0;
            }
            SavedPresetSnapshot saved = ReadSavedPreset(refreshSaved);
            IReadOnlyList<string> savedSettings = saved.Valid && saved.Exists ? saved.Policy.DescribeSettings() : Array.Empty<string>();
            string savedSummary = !saved.Valid ? saved.Error : !saved.Exists ?
                L.T("Saved preset: none. Use /one save to store active settings.") :
                L.T("Saved for future hosted sessions: ") + (saved.Policy.HasChanges ?
                    string.Join("; ", savedSettings) : L.T("empty (no adjustments)."));
            if (includeDiagnostics)
            {
                lines.Add(savedSummary);
                lines.Add(L.T("Current values include native bonuses; base adjustments are tracked raw stat units. Commands and resets change the active session; /one save updates the saved copy."));
            }

            return new SettingsSnapshot(sameSession ? dungeon : null, epoch, runGeneration, intentRevision, host,
                canMutate, canSave, canForget, ChoiceFeature.Available, saved.Valid, unavailable, fault,
                lines, currentPlayers, active, savedSettings, savedSummary, sameSession ? policy.RabbitPotions : default,
                RabbitPotionFeature.Available, RabbitDescriptionFeature.Available,
                sameSession && policy.MerchantSpawns, MerchantFeature.Available,
                sameSession ? policy.MerchantSpawnChance : MerchantCommand.DefaultChance,
                sameSession ? policy.Merchants.Snapshot : null, RabbitLevelUpFeature.Available,
                sameSession && policy.ItemUnlock, ItemRestrictionFeature.Available,
                sameSession ? policy.FriendlyFire : default, FriendlyFireFeature.Available);
        }

        private static string Signed(int value) => value.ToString("+0;-0;0", CultureInfo.InvariantCulture);
    }
}
