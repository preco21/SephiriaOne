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
                if (parts.Length == 2 && parts[0].Equals("/one", StringComparison.OrdinalIgnoreCase) &&
                    parts[1].Equals("ui", StringComparison.OrdinalIgnoreCase))
                {
                    bool host = NetworkServer.active;
                    return new SettingsActionResult(true, host, host, host ? Array.Empty<string>() :
                        new[] { "Only the host can open the session settings panel." });
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
                        return Reply(true, SessionSettings.DescribeMerchant(snapshot.MerchantSpawns, snapshot.MerchantSpawnChance) +
                            (snapshot.MerchantsAvailable ? "" : " Merchant hooks unavailable; native behavior continues. See Player.log."));
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
                            (snapshot.RabbitPotionsAvailable ? "" : " Potion hooks unavailable; native behavior continues. See Player.log."));
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
                        return Reply(true, presetError + " /one ui opens the host settings panel. /one rabbit help lists potion options. /one merchant help lists extra merchant options.");
                    if (preset == PresetAction.Invalid) return Reply(false, presetError);
                    bool success = SessionSettings.TryExecutePreset(preset, out string[] messages);
                    return new SettingsActionResult(true, success, false, messages);
                }
                if (stat == StatParseResult.List)
                {
                    var lines = new List<string>();
                    foreach (StatDefinition definition in StatCatalog.All)
                        lines.Add($"{definition.Name}: {definition.Minimum}..{definition.Maximum} {definition.Unit}; " +
                            (definition.Scale == 100 ? "up to 2 decimal places." : "whole numbers."));
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
                    new[] { (isPreset ? "Preset command" : "Command") + " failed. Check Player.log for details." });
            }
        }

        private static SettingsActionResult Reply(bool success, string message) =>
            new SettingsActionResult(true, success, false, new[] { message });
    }
}
