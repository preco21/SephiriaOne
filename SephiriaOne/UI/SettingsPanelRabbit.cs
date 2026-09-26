using System.Collections.Generic;
using UnityEngine.UI;

namespace SephiriaOne
{
    public sealed partial class SettingsPanel
    {
        private readonly List<Button> rabbitOffButtons = new List<Button>();

        private void BuildRabbitEditor()
        {
            widgets.Text(pageRoot, "RabbitTitle", "Wing-Eared Rabbit", 16, 0, 273, 24, 14);
            string[] options = { "infinite", "share", "mp-cost", "suppress-survival" };
            string[] labels = { "Infinite HP potions", "Share HP healing", "Cost " + RabbitPotionSettings.MpCostPerDrink + " MP", "Stop Survival stats" };
            for (int i = 0; i < options.Length; i++)
            {
                string option = options[i];
                int y = 27 + i * 27;
                widgets.Text(pageRoot, "Rabbit" + option, labels[i], 16, y + 2, 139, 23, 10);
                changeButtons.Add(widgets.Button(pageRoot, "On", 161, y, 60, 24,
                    () => Execute("/one rabbit " + option + " on", true)));
                rabbitOffButtons.Add(widgets.Button(pageRoot, "Off", 229, y, 60, 24,
                    () => Execute("/one rabbit " + option + " off", true)));
            }
            resetAll = widgets.Button(pageRoot, "Reset rabbit options", 16, 137, 273, 25,
                () => Execute("/one rabbit reset", true));
            widgets.Text(pageRoot, "RabbitStatus", "Current options and behavior", 312, 0, 272, 22, 10).color = PanelWidgets.Muted;
            readout = widgets.Scroll(pageRoot, 312, 26, 272, 136);
        }

        private static string RabbitValues(SettingsSnapshot snapshot) =>
            "Infinite HP potions: " + (snapshot.RabbitPotions.Infinite ? "ON" : "OFF") +
            "\nNearby HP potion healing: " + (snapshot.RabbitPotions.Share ? "ON" : "OFF") +
            "\n" + RabbitPotionSettings.MpCostPerDrink + " MP per HP potion: " + (snapshot.RabbitPotions.ConsumeMp ? "ON" : "OFF") +
            "\nSuppress Survival rank 5 random stats: " + (snapshot.RabbitPotions.SuppressSurvival ? "ON" : "OFF") +
            "\n\nOnly Wing-Eared Rabbit HP potions are affected. Insufficient MP blocks the drink; keep one potion to use." +
            "\n\nSharing uses potion strength within 5 tiles on the same floor. Recipients retain their own healing penalties." +
            "\n\nHost settings also apply to unmodified guests wearing Wing-Eared Rabbit." +
            (snapshot.RabbitPotionsAvailable ? "" : "\nPotion hooks unavailable; native behavior continues.") +
            (snapshot.RabbitDescriptionAvailable ? "\nDescription additions appear on this host only." : "\nDescription adapter unavailable; see Player.log.") +
            "\nSave these options from Presets for future sessions.";
    }
}
