using System;
using System.Collections.Generic;
using Mirror;
using UnityEngine;

namespace SephiriaOne
{
    internal sealed class SettingsActionResult
    {
        public bool Recognized { get; }
        public bool Success { get; }
        public bool OpenPanel { get; }
        public IReadOnlyList<string> Messages { get; }

        public SettingsActionResult(bool recognized, bool success, bool openPanel, IEnumerable<string> messages)
        {
            Recognized = recognized; Success = success; OpenPanel = openPanel;
            Messages = new List<string>(messages).AsReadOnly();
        }
    }

    // Local chat and the host panel share parsers and authoritative services.
    // An OpenPanel result is only a request; this layer never touches Unity UI.
    internal static class SettingsActions
    {
        // Chat uses this before clearing/closing its native input. Recognition
        // is deliberately independent of validation, services, and host state.
        public static bool IsCommand(string text)
        {
            string[] parts = (text ?? "").Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) return false;
            string prefix = parts[0];
            return prefix.Equals("/fountain", StringComparison.OrdinalIgnoreCase) ||
                prefix.Equals("/choices", StringComparison.OrdinalIgnoreCase) ||
                prefix.Equals("/stats", StringComparison.OrdinalIgnoreCase) ||
                prefix.Equals("/resources", StringComparison.OrdinalIgnoreCase) ||
                prefix.Equals("/one", StringComparison.OrdinalIgnoreCase);
        }

        public static SettingsActionResult Execute(string command)
        {
            bool recognized = false;
            bool isPreset = false;
            try
            {
                string[] parts = (command ?? "").Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
                // Language is a local presentation preference, including on guests
                // and outside a hosted session. Never enter gameplay reconciliation.
                if (parts.Length >= 2 && parts[0].Equals("/one", StringComparison.OrdinalIgnoreCase) &&
                    parts[1].Equals("language", StringComparison.OrdinalIgnoreCase))
                {
                    recognized = true;
                    if (parts.Length == 2 || (parts.Length == 3 && parts[2].Equals("status", StringComparison.OrdinalIgnoreCase)))
                        return Reply(true, L.F("Language: {0}. Config: {1}", L.Language, L.ConfigPath));
                    if (parts.Length != 3) return Reply(false, L.T("Local language: /one language en|ko|reload|status."));
                    string selection = parts[2].ToLowerInvariant();
                    if (selection == "reload")
                    {
                        bool reloaded = L.Reload(out string reloadError);
                        return Reply(reloaded, reloaded ? L.F("Translation files reloaded ({0}).", L.Language) :
                            L.F("Could not reload translations: {0}", reloadError));
                    }
                    if (selection != "en" && selection != "ko")
                        return Reply(false, L.T("Local language: /one language en|ko|reload|status."));
                    bool selected = L.TrySetLanguage(selection, out string languageError);
                    return Reply(selected, selected ? L.F("Language changed to {0}.", L.Language) :
                        L.F("Could not change language: {0}", languageError));
                }
                if (parts.Length == 2 && parts[0].Equals("/one", StringComparison.OrdinalIgnoreCase) &&
                    parts[1].Equals("ui", StringComparison.OrdinalIgnoreCase))
                {
                    bool host = NetworkServer.active;
                    return new SettingsActionResult(true, host, host, host ? Array.Empty<string>() :
                        new[] { L.T("Only the host can open the session settings panel.") });
                }

                var collin = CollinCommand.Parse(command, out bool collinEnabled);
                if (collin != CollinParseResult.NotCommand)
                {
                    recognized = true;
                    if (collin == CollinParseResult.Help) return Reply(true, CollinCommand.Usage);
                    if (collin == CollinParseResult.Invalid) return Reply(false, CollinCommand.Usage);
                    if (collin == CollinParseResult.Status)
                    {
                        var snapshot = SessionSettings.ReadSnapshot();
                        if (!snapshot.HostActive || snapshot.SessionIdentity == null) return Reply(false, snapshot.AvailabilityReason);
                        return Reply(true, SessionSettings.DescribeCollin(snapshot.CollinStartingArtifact) +
                            (snapshot.CollinAvailable ? "" : L.T(" Collin starting-artifact hooks unavailable; see Player.log.")));
                    }
                    return Reply(SessionSettings.TryExecuteCollin(collinEnabled, out string collinMessage), collinMessage);
                }

                var bat = BatCommand.Parse(command, out bool batEnabled);
                if (bat != BatParseResult.NotCommand)
                {
                    recognized = true;
                    if (bat == BatParseResult.Help) return Reply(true, BatCommand.Usage);
                    if (bat == BatParseResult.Invalid) return Reply(false, BatCommand.Usage);
                    if (bat == BatParseResult.Status)
                    {
                        var snapshot = SessionSettings.ReadSnapshot();
                        if (!snapshot.HostActive || snapshot.SessionIdentity == null) return Reply(false, snapshot.AvailabilityReason);
                        return Reply(true, SessionSettings.DescribeBat(snapshot.BatHpSteal) +
                            (snapshot.BatAvailable ? "" : L.T(" Bat costume hooks unavailable; see Player.log.")));
                    }
                    return Reply(SessionSettings.TryExecuteBat(batEnabled, out string batMessage), batMessage);
                }

                var events = EventSpawnCommand.Parse(command, out var eventSettings);
                if (events != EventSpawnParseResult.NotCommand)
                {
                    recognized = true;
                    if (events == EventSpawnParseResult.Help) return Reply(true, EventSpawnCommand.Usage);
                    if (events == EventSpawnParseResult.Invalid) return Reply(false, EventSpawnCommand.Usage);
                    if (events == EventSpawnParseResult.Status)
                    {
                        var snapshot = SessionSettings.ReadSnapshot();
                        if (!snapshot.HostActive || snapshot.SessionIdentity == null) return Reply(false, snapshot.AvailabilityReason);
                        return Reply(true, SessionSettings.DescribeEventSpawns(snapshot.EventSpawns) +
                            (snapshot.EventSpawnsAvailable ? "" : L.T(" Random event hooks unavailable; see Player.log.")));
                    }
                    return Reply(SessionSettings.TryExecuteEventSpawn(eventSettings, out string eventMessage), eventMessage);
                }

                var jars = JarSpawnCommand.Parse(command, out var jarSettings);
                if (jars != JarSpawnParseResult.NotCommand)
                {
                    recognized = true;
                    if (jars == JarSpawnParseResult.Help) return Reply(true, JarSpawnCommand.Usage);
                    if (jars == JarSpawnParseResult.Invalid) return Reply(false, JarSpawnCommand.Usage);
                    if (jars == JarSpawnParseResult.Status)
                    {
                        var snapshot = SessionSettings.ReadSnapshot();
                        if (!snapshot.HostActive || snapshot.SessionIdentity == null) return Reply(false, snapshot.AvailabilityReason);
                        return Reply(true, SessionSettings.DescribeJarSpawns(snapshot.JarSpawns) +
                            (snapshot.JarSpawnsAvailable ? "" : L.T(" Mystic Jar hooks unavailable; see Player.log.")));
                    }
                    return Reply(SessionSettings.TryExecuteJarSpawn(jarSettings, out string jarMessage), jarMessage);
                }

                var combat = FriendlyFireCommand.Parse(command, out var combatCommand);
                if (combat != FriendlyFireParseResult.NotCommand)
                {
                    recognized = true;
                    if (combat == FriendlyFireParseResult.Help) return Reply(true, FriendlyFireCommand.Usage);
                    if (combat == FriendlyFireParseResult.Invalid) return Reply(false, FriendlyFireCommand.Usage);
                    if (combat == FriendlyFireParseResult.Status)
                    {
                        var snapshot = SessionSettings.ReadSnapshot();
                        if (!snapshot.HostActive || snapshot.SessionIdentity == null) return Reply(false, snapshot.AvailabilityReason);
                        return Reply(true, SessionSettings.DescribeFriendlyFire(snapshot.FriendlyFire) +
                            (snapshot.FriendlyFireAvailable ? "" : L.T(" Friendly-fire hooks unavailable; see Player.log.")));
                    }
                    return Reply(SessionSettings.TryExecuteFriendlyFire(combatCommand, out string combatMessage), combatMessage);
                }

                ItemRestrictionParseResult items = ItemRestrictionCommand.Parse(command, out bool unlock);
                if (items != ItemRestrictionParseResult.NotCommand)
                {
                    recognized = true;
                    if (items == ItemRestrictionParseResult.Help) return Reply(true, ItemRestrictionCommand.Usage);
                    if (items == ItemRestrictionParseResult.Invalid) return Reply(false, ItemRestrictionCommand.Usage);
                    if (items == ItemRestrictionParseResult.Status)
                    {
                        SettingsSnapshot snapshot = SessionSettings.ReadSnapshot();
                        if (!snapshot.HostActive || snapshot.SessionIdentity == null) return Reply(false, snapshot.AvailabilityReason);
                        return Reply(true, SessionSettings.DescribeItemUnlock(snapshot.ItemUnlock) +
                            (snapshot.ItemRestrictionsAvailable ? "" : L.T(" Item restriction hooks unavailable; see Player.log.")));
                    }
                    return Reply(SessionSettings.TryExecuteItemUnlock(unlock, out string itemMessage), itemMessage);
                }

                MerchantParseResult merchant = MerchantCommand.Parse(command, out MerchantCommand merchantCommand, out string merchantError);
                if (merchant != MerchantParseResult.NotCommand)
                {
                    recognized = true;
                    if (merchant == MerchantParseResult.Help) return Reply(true, MerchantCommand.Usage);
                    if (merchant == MerchantParseResult.Invalid) return Reply(false, merchantError);
                    if (merchant == MerchantParseResult.Status)
                    {
                        SettingsSnapshot snapshot = SessionSettings.ReadSnapshot();
                        if (!snapshot.HostActive || snapshot.SessionIdentity == null) return Reply(false, snapshot.AvailabilityReason);
                        return Reply(true, SessionSettings.DescribeMerchants(snapshot.Merchants, merchantCommand.AllTypes ? null : merchantCommand.TypeId) +
                            (snapshot.MerchantsAvailable ? "" : L.T(" Merchant hooks unavailable; native behavior continues. See Player.log.")));
                    }
                    bool success = SessionSettings.TryExecuteMerchant(merchantCommand, out string merchantMessage);
                    return Reply(success, merchantMessage);
                }

                RabbitParseResult rabbit = RabbitCommand.Parse(command, out RabbitCommand rabbitCommand, out string rabbitError);
                if (rabbit != RabbitParseResult.NotCommand)
                {
                    recognized = true;
                    if (rabbit == RabbitParseResult.Help) return Reply(true, RabbitCommand.Usage);
                    if (rabbit == RabbitParseResult.Invalid) return Reply(false, rabbitError);
                    if (rabbit == RabbitParseResult.Status)
                    {
                        SettingsSnapshot snapshot = SessionSettings.ReadSnapshot();
                        if (!snapshot.HostActive || snapshot.SessionIdentity == null) return Reply(false, snapshot.AvailabilityReason);
                        return Reply(true, SessionSettings.DescribeRabbit(snapshot.RabbitPotions) +
                            (snapshot.RabbitPotionsAvailable ? "" : L.T(" Potion hooks unavailable; native behavior continues. See Player.log.")) +
                            (snapshot.RabbitLevelUpPotionsAvailable ? "" : L.T(" Level-up potion hooks unavailable; no level-up reward is granted. See Player.log.")));
                    }
                    bool success = SessionSettings.TryExecuteRabbit(rabbitCommand, out string rabbitMessage);
                    return Reply(success, rabbitMessage);
                }

                FountainParseResult fountain = FountainCommand.Parse(command, out FountainCommand fountainCommand, out string fountainError);
                ChoiceParseResult choice = ChoiceCommand.Parse(command, out ChoiceCommand choiceCommand, out string choiceError);
                StatParseResult stat = StatCommand.Parse(command, out StatCommand statCommand, out string statError);
                ResourceParseResult resource = ResourceCommand.Parse(command, out ResourceCommand resourceCommand, out string resourceError);
                PresetAction preset = PresetCommand.Parse(command, out string presetError);
                isPreset = preset != PresetAction.NotCommand;
                recognized = isPreset || fountain != FountainParseResult.NotCommand ||
                    choice != ChoiceParseResult.NotCommand || stat != StatParseResult.NotCommand || resource != ResourceParseResult.NotCommand;
                if (!recognized) return new SettingsActionResult(false, false, false, Array.Empty<string>());
                if (resource != ResourceParseResult.NotCommand)
                {
                    if (resource == ResourceParseResult.Help) return Reply(true, ResourceCommand.Usage);
                    if (resource == ResourceParseResult.Invalid) return Reply(false, resourceError);
                    bool success = ResourceRuntime.TryExecute(resourceCommand, out string resourceMessage);
                    return Reply(success, resourceMessage);
                }

                if (isPreset)
                {
                    if (preset == PresetAction.Help)
                        return Reply(true, presetError + L.T(" /one ui opens the host settings panel. /one rabbit help lists potion options. /one merchant help lists extra merchant options.") +
                            L.T(" /one items help lists given-item options.") + L.T(" /one friendlyfire help lists allied-damage options.") + L.T(" /one jars help lists Mystic Jar spawn options.") + L.T(" /one events help lists random event options.") + L.T(" /one bat help lists Wingless Bat options.") + L.T(" /one collin help lists costume starting-artifact options.") + L.T(" Language: /one language en|ko|reload|status."));
                    if (preset == PresetAction.Invalid) return Reply(false, presetError);
                    bool success = SessionSettings.TryExecutePreset(preset, out string[] messages);
                    return new SettingsActionResult(true, success, false, messages);
                }
                if (stat == StatParseResult.List)
                {
                    var lines = new List<string>();
                    foreach (StatDefinition definition in StatCatalog.All)
                        lines.Add(L.F("{0}: {1}..{2} {3}; ", definition.Name, definition.Minimum, definition.Maximum, L.T(definition.Unit)) +
                            (definition.Scale == 100 ? L.T("up to 2 decimal places.") : L.T("whole numbers.")));
                    return new SettingsActionResult(true, true, false, lines);
                }
                if (fountain == FountainParseResult.Help || choice == ChoiceParseResult.Help || stat == StatParseResult.Help)
                    return Reply(true, fountain == FountainParseResult.Help ? FountainCommand.Usage :
                        choice == ChoiceParseResult.Help ? ChoiceCommand.Usage : StatCommand.Usage);
                if (fountain == FountainParseResult.Invalid || choice == ChoiceParseResult.Invalid || stat == StatParseResult.Invalid)
                    return Reply(false, fountain == FountainParseResult.Invalid ? fountainError :
                        choice == ChoiceParseResult.Invalid ? choiceError : statError);

                string message;
                bool applied = fountain == FountainParseResult.Valid ? FountainPoints.TryExecute(fountainCommand, out message) :
                    choice == ChoiceParseResult.Valid ? ChoicePoints.TryExecute(choiceCommand, out message) :
                    CharacterStats.TryExecute(statCommand, out message);
                return Reply(applied, message);
            }
            catch (Exception exception)
            {
                Debug.LogError($"[SephiriaOne] {(isPreset ? "Preset" : "Settings")} command failed: {exception}");
                return new SettingsActionResult(recognized, false, false,
                    new[] { (isPreset ? L.T("Preset command") : L.T("Command")) + L.T(" failed. Check Player.log for details.") });
            }
        }

        private static SettingsActionResult Reply(bool success, string message) =>
            new SettingsActionResult(true, success, false, new[] { message });
    }
}
