using System.Collections.Generic;
using UnityEngine.UI;

namespace SephiriaOne
{
    public sealed partial class SettingsPanel
    {

        private void BuildRabbitEditor()
        {
            widgets.Text(pageRoot, "RabbitTitle", "Wing-Eared Rabbit", 16, 0, 273, 24, 14);
            string[] options = { "infinite", "share", "mp-cost", "suppress-survival", "level-up-potion" };
            string[] labels = { "Infinite HP potions", "Share HP healing", "Charge MP", "Stop Survival stats", "Level-up potion" };
            for (int i = 0; i < options.Length; i++)
            {
                string option = options[i];
                int y = 25 + i * 22;
                int index = i;
                AddCheckbox(labels[i], 16, y, 273, 20,
                    snapshot => index == 0 ? snapshot.RabbitPotions.Infinite : index == 1 ? snapshot.RabbitPotions.Share :
                        index == 2 ? snapshot.RabbitPotions.ConsumeMp : index == 3 ? snapshot.RabbitPotions.SuppressSurvival : snapshot.RabbitPotions.LevelUpPotion,
                    snapshot => index == 4 ? snapshot.RabbitLevelUpPotionsAvailable : snapshot.RabbitPotionsAvailable,
                    "/one rabbit " + option);
            }
            widgets.Text(pageRoot, "RabbitMpAmount", "MP cost", 16, 139, 57, 23, 10);
            amount = widgets.Input(pageRoot, 79, 137, 64, draft.Edit);
            changeButtons.Add(widgets.Button(pageRoot, "Set & on", 151, 137, 66, 25,
                () => Execute("/one rabbit mp-cost " + draft.Text, true)));
            resetAll = widgets.Button(pageRoot, "Reset", 225, 137, 64, 25,
                () => Execute("/one rabbit reset", true));
            widgets.Text(pageRoot, "RabbitStatus", "Current settings", 312, 0, 272, 22, 10).color = PanelWidgets.Muted;
            readout = widgets.Scroll(pageRoot, 312, 26, 272, 136);
        }

        private static string RabbitValues(SettingsSnapshot snapshot) =>
            L.F("Infinite HP potions: {0}", L.T(snapshot.RabbitPotions.Infinite ? "ON" : "OFF")) +
            "\n" + L.F("Nearby HP potion healing: {0}", L.T(snapshot.RabbitPotions.Share ? "ON" : "OFF")) +
            "\n" + L.F("{0} MP per HP potion: {1}", snapshot.RabbitPotions.MpCostPerDrink, L.T(snapshot.RabbitPotions.ConsumeMp ? "ON" : "OFF")) +
            "\n" + L.F("Suppress Survival rank 5 random stats: {0}", L.T(snapshot.RabbitPotions.SuppressSurvival ? "ON" : "OFF")) +
            "\n" + L.F("Non-HP/MP potion on level-up: {0}", L.T(snapshot.RabbitPotions.LevelUpPotion ? "ON" : "OFF")) +
            "\n\n" + L.T("Rabbit only. Keep 1 HP potion; insufficient MP blocks use. Infinite HP potions bypass Tension.") +
            "\n\n" + L.T("Regeneration (Sample) stays native: consumed normally, no MP cost or sharing, Survival bonus allowed.") +
            "\n\n" + L.T("MP cost: 0..10000. Set & on enables charging; Reset restores 10 and turns all options off.") +
            "\n\n" + L.T("Shared healing: 5 tiles, same floor; recipient penalties apply. Level-up: 1 random non-HP/MP potion.") +
            (snapshot.RabbitPotionsAvailable ? "" : "\n" + L.T("Potion hooks unavailable; native behavior continues.")) +
            (snapshot.RabbitLevelUpPotionsAvailable ? "" : "\n" + L.T("Level-up potion hooks unavailable; no level-up reward is granted.")) +
            (snapshot.RabbitDescriptionAvailable ? "" : "\n" + L.T("Description adapter unavailable; see Player.log."));
    }
}
