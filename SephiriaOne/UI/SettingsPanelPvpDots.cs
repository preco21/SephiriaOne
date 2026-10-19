using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

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
            // Scroll inside the original editor area so adding effect families
            // never pushes participant or shared combat actions out of the panel.
            var container = PanelWidgets.Rect(pageRoot, "DotIntervals", 16, 80, 273, 82);
            container.gameObject.AddComponent<Image>().color = new Color32(17, 27, 40, 255);
            var scroll = container.gameObject.AddComponent<ScrollRect>();
            var viewport = PanelWidgets.Rect(container, "Viewport", 0, 0, 259, 82);
            viewport.gameObject.AddComponent<RectMask2D>();
            var content = PanelWidgets.Rect(viewport, "Effects", 0, 0, 259, ((PvpDotSettings.Count + 1) / 2) * 64);
            scroll.viewport = viewport; scroll.content = content; scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 30;
            var track = PanelWidgets.Rect(container, "Scrollbar", 265, 0, 8, 82);
            track.gameObject.AddComponent<Image>().color = new Color32(39, 59, 82, 255);
            var bar = track.gameObject.AddComponent<Scrollbar>();
            var handle = PanelWidgets.Rect(track, "Handle", 0, 0, 0, 0);
            handle.anchorMin = Vector2.zero; handle.anchorMax = Vector2.one;
            handle.pivot = new Vector2(0.5f, 0.5f);
            var image = handle.gameObject.AddComponent<Image>(); image.color = PanelWidgets.Accent;
            bar.handleRect = handle; bar.targetGraphic = image; bar.direction = Scrollbar.Direction.BottomToTop;
            bar.navigation = new Navigation { mode = Navigation.Mode.None };
            scroll.verticalScrollbar = bar;
            for (int i = 0; i < PvpDotSettings.Count; i++)
            {
                var row = new DotIntervalEditor { Effect = (PvpDotEffect)i };
                int x = i % 2 * 134, y = i / 2 * 64;
                widgets.Text(content, "DotLabel" + i, PvpDotSettings.Label(row.Effect), x, y, 125, 15, 10);
                row.Input = widgets.Input(content, x, y + 15, 57, _ => row.Draft = true);
                changeButtons.Add(widgets.Button(content, "Apply", x + 63, y + 15, 62, 25, () => ApplyPvpDot(row)));
                if (PvpDotSettings.SupportsSchedule(row.Effect))
                    AddCheckbox("Keep schedule", x, y + 41, 125, 20,
                        s => s.FriendlyFire.DotIntervals.PreserveSchedule(row.Effect), s => s.FriendlyFireAvailable,
                        "/one friendlyfire dot " + PvpDotSettings.Key(row.Effect) + " preserve", parent: content);
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
