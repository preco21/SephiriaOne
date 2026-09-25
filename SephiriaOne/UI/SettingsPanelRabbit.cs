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
            string[] options = { "infinite", "share" };
            string[] labels = { "Infinite potions", "Share healing" };
            for (int i = 0; i < options.Length; i++)
            {
                string option = options[i];
                int y = 31 + i * 31;
                widgets.Text(pageRoot, "Rabbit" + option, labels[i], 16, y + 2, 139, 23, 11);
                changeButtons.Add(widgets.Button(pageRoot, "On", 161, y, 60, 24,
                    () => Execute("/one rabbit " + option + " on", true)));
                rabbitOffButtons.Add(widgets.Button(pageRoot, "Off", 229, y, 60, 24,
                    () => Execute("/one rabbit " + option + " off", true)));
            }
            widgets.Text(pageRoot, "RabbitHelp", "Normal potion input and drinking time. Keep one healing potion to use. Other costumes are unchanged.", 16, 96, 273, 35, 9);
            resetAll = widgets.Button(pageRoot, "Reset rabbit options", 16, 137, 273, 25,
                () => Execute("/one rabbit reset", true));
            widgets.Text(pageRoot, "RabbitStatus", "Current options and behavior", 312, 0, 272, 22, 10).color = PanelWidgets.Muted;
            readout = widgets.Scroll(pageRoot, 312, 26, 272, 136);
        }

        private static string RabbitValues(SettingsSnapshot snapshot) =>
            "Infinite healing potions: " + (snapshot.RabbitPotions.Infinite ? "ON" : "OFF") +
            "\nNearby potion healing: " + (snapshot.RabbitPotions.Share ? "ON" : "OFF") +
            "\n\nSharing: potion strength within 5 tiles on the same floor. Recipients retain their own healing penalties." +
            "\n\nBoth options work for unmodified guests wearing Wing-Eared Rabbit." +
            (snapshot.RabbitPotionsAvailable ? "" : "\nPotion hooks unavailable; native behavior continues.") +
            (snapshot.RabbitDescriptionAvailable ? "\nDescription additions appear on this host only." : "\nDescription adapter unavailable; see Player.log.") +
            "\nSave these options from Presets for future sessions.";
    }
}
