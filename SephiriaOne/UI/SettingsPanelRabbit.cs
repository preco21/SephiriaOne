using System.Collections.Generic;
using UnityEngine.UI;

namespace SephiriaOne
{
    public sealed partial class SettingsPanel
    {
        private readonly List<Button> rabbitOffButtons = new List<Button>();
        private Button rabbitLevelUpOn;

        private void BuildRabbitEditor()
        {
            widgets.Text(pageRoot, "RabbitTitle", "Wing-Eared Rabbit", 16, 0, 273, 24, 14);
            string[] options = { "infinite", "share", "mp-cost", "suppress-survival", "level-up-potion" };
            string[] labels = { "Infinite HP potions", "Share HP healing", "Charge MP", "Stop Survival stats", "Level-up potion" };
            for (int i = 0; i < options.Length; i++)
            {
                string option = options[i];
                int y = 25 + i * 22;
                widgets.Text(pageRoot, "Rabbit" + option, labels[i], 16, y, 139, 20, 10);
                Button on = widgets.Button(pageRoot, "On", 161, y, 60, 20,
                    () => Execute("/one rabbit " + option + " on", true));
                if (option == "level-up-potion") rabbitLevelUpOn = on;
                else changeButtons.Add(on);
                rabbitOffButtons.Add(widgets.Button(pageRoot, "Off", 229, y, 60, 20,
                    () => Execute("/one rabbit " + option + " off", true)));
            }
            widgets.Text(pageRoot, "RabbitMpAmount", "MP cost", 16, 139, 57, 23, 10);
            amount = widgets.Input(pageRoot, 79, 137, 64, draft.Edit);
            changeButtons.Add(widgets.Button(pageRoot, "Set & on", 151, 137, 66, 25,
                () => Execute("/one rabbit mp-cost " + draft.Text, true)));
            resetAll = widgets.Button(pageRoot, "Reset", 225, 137, 64, 25,
                () => Execute("/one rabbit reset", true));
            widgets.Text(pageRoot, "RabbitStatus", "Current options and behavior", 312, 0, 272, 22, 10).color = PanelWidgets.Muted;
            readout = widgets.Scroll(pageRoot, 312, 26, 272, 136);
        }

        private static string RabbitValues(SettingsSnapshot snapshot) =>
            L.F("Infinite HP potions: {0}", L.T(snapshot.RabbitPotions.Infinite ? "ON" : "OFF")) +
            "\n" + L.F("Nearby HP potion healing: {0}", L.T(snapshot.RabbitPotions.Share ? "ON" : "OFF")) +
            "\n" + L.F("{0} MP per HP potion: {1}", snapshot.RabbitPotions.MpCostPerDrink, L.T(snapshot.RabbitPotions.ConsumeMp ? "ON" : "OFF")) +
            "\n" + L.F("Suppress Survival rank 5 random stats: {0}", L.T(snapshot.RabbitPotions.SuppressSurvival ? "ON" : "OFF")) +
            "\n" + L.F("Non-HP/MP potion on level-up: {0}", L.T(snapshot.RabbitPotions.LevelUpPotion ? "ON" : "OFF")) +
            "\n\n" + L.T("Each earned level grants one random non-HP/MP potion while wearing Wing-Eared Rabbit.") +
            "\n\n" + L.T("HP potion options apply only while wearing Wing-Eared Rabbit. Insufficient MP blocks the drink; keep one potion to use.") +
            "\n\n" + L.T("Potion of Regeneration (Sample) retains Survival's random-stat bonus.") +
            "\n\n" + L.T("MP cost: whole numbers 0..10000. Set & on applies the amount and enables charging. Off retains it; Reset restores 10 and all options off.") +
            "\n\n" + L.T("Sharing uses potion strength within 5 tiles on the same floor. Recipients retain their own healing penalties.") +
            "\n\n" + L.T("Host settings also apply to unmodified guests wearing Wing-Eared Rabbit.") +
            (snapshot.RabbitPotionsAvailable ? "" : "\n" + L.T("Potion hooks unavailable; native behavior continues.")) +
            (snapshot.RabbitLevelUpPotionsAvailable ? "" : "\n" + L.T("Level-up potion hooks unavailable; no level-up reward is granted.")) +
            "\n" + L.T(snapshot.RabbitDescriptionAvailable ? "Description additions appear on this host only." : "Description adapter unavailable; see Player.log.") +
            "\n" + L.T("Save these options from Presets for future sessions.");
    }
}
