using System.Collections.Generic;
using TMPro;

namespace SephiriaOne
{
    public sealed partial class SettingsPanel
    {
        private sealed class DotIntervalEditor
        {
            internal PvpDotEffect Effect;
            internal TMP_InputField Input;
            internal bool Draft;
            internal int LastHundredths = -1;
        }
        private readonly List<DotIntervalEditor> pvpDotEditors = new List<DotIntervalEditor>(PvpDotSettings.Count);

        private void BuildPvpDotEditor()
        {
            // Four editors fit the existing 273x82 area in two columns. Keep
            // participant controls and shared KDA/revive/master actions visible.
            for (int i = 0; i < PvpDotSettings.Count; i++)
            {
                var row = new DotIntervalEditor { Effect = (PvpDotEffect)i };
                int x = 16 + i % 2 * 141, y = 80 + i / 2 * 42;
                widgets.Text(pageRoot, "DotLabel" + i, PvpDotSettings.Label(row.Effect), x, y, 132, 15, 10);
                row.Input = widgets.Input(pageRoot, x, y + 15, 62, _ => row.Draft = true);
                changeButtons.Add(widgets.Button(pageRoot, "Apply", x + 68, y + 15, 64, 25, () => ApplyPvpDot(row)));
                pvpDotEditors.Add(row);
            }
        }

        private void RefreshPvpDots(SettingsSnapshot snapshot)
        {
            foreach (var row in pvpDotEditors)
            {
                if (!row.Input) continue;
                row.Input.interactable = snapshot.CanMutate && snapshot.FriendlyFireAvailable;
                int value = snapshot.FriendlyFire.DotIntervals.Hundredths(row.Effect);
                if (row.Draft || row.LastHundredths == value) continue;
                row.Input.SetTextWithoutNotify(snapshot.FriendlyFire.DotIntervals.Number(row.Effect));
                row.LastHundredths = value;
            }
        }
        private void ApplyPvpDot(DotIntervalEditor row) => Execute(
            "/one friendlyfire dot " + PvpDotSettings.Key(row.Effect) + " " + row.Input.text.Trim(), true,
            clearInputOnSuccess: () => ClearPvpDot(row));
        private static void ClearPvpDot(DotIntervalEditor row)
        {
            row.Draft = false; row.LastHundredths = -1;
            if (row.Input) row.Input.SetTextWithoutNotify("");
        }
        private void ClearPvpDots() { foreach (var row in pvpDotEditors) ClearPvpDot(row); }
    }
}
