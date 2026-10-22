using System;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine.UI;

namespace SephiriaOne
{
    public sealed partial class SettingsPanel
    {
        private sealed class RapidValueEditor
        {
            internal string Key;
            internal TMP_InputField Input;
            internal bool Draft;
            internal decimal LastValue = -1;
        }
        private readonly List<RapidValueEditor> pvpRapidEditors = new List<RapidValueEditor>(4);
        private TMP_Text pvpRapidRecovery, pvpRapidExample;
        private Button pvpRapidReset;
        private decimal pvpRapidLastBurst = -1, pvpRapidLastRefill = -1, pvpRapidLastExponent = -1;

        private void BuildPvpRapidEditor()
        {
            AddCheckbox("Hit-frequency scaling", 16, 0, 273, 25, s => s.FriendlyFire.HitFrequency,
                s => s.FriendlyFireAvailable, "/one friendlyfire hit-frequency");
            BuildPvpRapidValue("window", "History window (s)", 16, 34);
            widgets.Text(pageRoot, "RapidWindowHint", "Default 1 s · 0.1..10 s\nSeparate direct / incidental / each Planet.", 16, 63, 273, 31, 10);
            BuildPvpRapidValue("rate", "Full-damage hits/s", 16, 100);
            widgets.Text(pageRoot, "RapidRateHint", "Default 4/s · 0.1..100/s\nFirst hit: no frequency reduction. Threshold = max(1, rate × window).", 16, 129, 273, 34, 10);
            BuildPvpRapidValue("exponent", "Reduction exponent", 16, 166);
            widgets.Text(pageRoot, "RapidExponentHint", "Default 0.5 · 0..2 (0 = off).\nHigher exponent reduces damage more.", 16, 195, 273, 26, 10);
            pvpRapidExample = widgets.Text(pageRoot, "RapidExample", "", 16, 224, 273, 26, 10);
            pvpRapidExample.enableAutoSizing = true;

            BuildPvpRapidValue("burst", "Stored burst (% HP)", 312, 0);
            widgets.Text(pageRoot, "RapidBurstHint", "Default 75% · 1..100% of victim max HP.\nOne shared reserve for heavy hits and barrages.", 312, 29, 272, 31, 10);
            widgets.Text(pageRoot, "RapidRefill", "PvP refill (%/s)", 312, 66, 116, 25, 12);
            friendlyRefill = widgets.Input(pageRoot, 432, 66, 68, EditFriendlyRefill);
            changeButtons.Add(widgets.Button(pageRoot, "Apply", 508, 66, 76, 25, ApplyFriendlyRefill));
            widgets.Text(pageRoot, "RapidRefillHint", "Default 70%/s · 1..300%/s of victim max HP.\nContinuous refill; no periodic reset.", 312, 95, 272, 31, 10);
            pvpRapidRecovery = widgets.Text(pageRoot, "RapidRecovery", "", 312, 130, 272, 26, 10);
            pvpRapidRecovery.enableAutoSizing = true;
            widgets.Text(pageRoot, "RapidScope", "Requires PvP balancing. All hit types share one reserve, even with frequency off. Defenses, roles and DoT intervals still apply. Minimum hits can exceed it.", 312, 160, 272, 54, 10);
            pvpRapidReset = widgets.Button(pageRoot, "Reset four rapid values", 312, 224, 272, 25, ResetPvpRapid);
        }

        private void BuildPvpRapidValue(string key, string label, float x, float y)
        {
            var row = new RapidValueEditor { Key = key };
            widgets.Text(pageRoot, "Rapid" + key, label, x, y, 116, 25, 12);
            row.Input = widgets.Input(pageRoot, x + 120, y, 68, _ => row.Draft = true);
            changeButtons.Add(widgets.Button(pageRoot, "Apply", x + 196, y, 76, 25, () => ApplyPvpRapid(row)));
            pvpRapidEditors.Add(row);
        }

        private void RefreshPvpRapid(SettingsSnapshot snapshot)
        {
            bool enabled = snapshot.CanMutate && snapshot.FriendlyFireAvailable;
            pvpRapidReset.interactable = snapshot.CanMutate || (snapshot.HostActive && snapshot.SessionIdentity != null &&
                (snapshot.FaultedFeature == "combat" || snapshot.FaultedFeature == "inheritance"));
            var rapid = snapshot.FriendlyFire.Rapid;
            foreach (var row in pvpRapidEditors)
            {
                row.Input.interactable = enabled;
                decimal value = row.Key == "window" ? rapid.WindowSeconds : row.Key == "rate" ? rapid.HitsPerSecond :
                    row.Key == "exponent" ? rapid.Exponent : rapid.BurstPercent;
                RefreshPvpRapidValue(row, value);
            }
            friendlyRefill.interactable = enabled;
            decimal refill = snapshot.FriendlyFire.RefillPercent;
            if (!friendlyRefillDraft && (pvpRapidLastRefill != refill || friendlyRefill.text.Length == 0))
                friendlyRefill.SetTextWithoutNotify(snapshot.FriendlyFire.RefillNumber);
            if (pvpRapidLastExponent != rapid.Exponent)
            {
                pvpRapidLastExponent = rapid.Exponent;
                pvpRapidExample.text = PvpRapidExample(rapid.Exponent);
            }
            if (pvpRapidLastBurst == rapid.BurstPercent && pvpRapidLastRefill == refill) return;
            pvpRapidLastBurst = rapid.BurstPercent; pvpRapidLastRefill = refill;
            pvpRapidRecovery.text = L.F("Applied empty-to-full: {0}s (burst ÷ refill).\nBefore role modifiers; no incoming hits.",
                (rapid.BurstPercent / refill).ToString("0.###", CultureInfo.InvariantCulture));
        }

        private static string PvpRapidExample(decimal exponent) => L.F("Frequency damage: 2× threshold {0}%; 4× {1}%.",
            (100 * Math.Pow(0.5, (double)exponent)).ToString("0", CultureInfo.InvariantCulture),
            (100 * Math.Pow(0.25, (double)exponent)).ToString("0", CultureInfo.InvariantCulture));

        private static void RefreshPvpRapidValue(RapidValueEditor row, decimal value)
        {
            if (row.Draft || row.LastValue == value) return;
            row.Input.SetTextWithoutNotify(value.ToString("0.##", CultureInfo.InvariantCulture));
            row.LastValue = value;
        }

        private void ApplyPvpRapid(RapidValueEditor row) => Execute(
            "/one friendlyfire rapid " + row.Key + " " + row.Input.text.Trim(), true,
            clearInputOnSuccess: () => ClearPvpRapidValue(row));
        private void ResetPvpRapid() => Execute("/one friendlyfire rapid reset", true,
            clearInputOnSuccess: ClearPvpRapid);
        private static void ClearPvpRapidValue(RapidValueEditor row)
        {
            row.Draft = false; row.LastValue = -1;
            if (row.Input) row.Input.SetTextWithoutNotify("");
        }
        private void ClearPvpRapid() { foreach (var row in pvpRapidEditors) ClearPvpRapidValue(row); }
    }
}
