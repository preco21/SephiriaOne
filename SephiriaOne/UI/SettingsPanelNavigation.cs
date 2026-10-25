using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SephiriaOne
{
    public sealed partial class SettingsPanel
    {
        private TMP_Text pageTitle;

        private void BuildNavigation()
        {
            var rail = PanelWidgets.Rect(window, "Navigation", 12, 50, 210, 494);
            PanelSurface.Add(rail.gameObject, new Color32(18, 29, 43, 235), 10, 0);
            var scroll = rail.gameObject.AddComponent<ScrollRect>();
            var viewport = PanelWidgets.Rect(rail, "Viewport", 6, 6, 178, 482);
            viewport.gameObject.AddComponent<RectMask2D>();
            var content = PanelWidgets.Rect(viewport, "Sections", 0, 0, 178, 0);
            scroll.viewport = viewport; scroll.content = content; scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 32;
            widgets.AddScrollbar(scroll, rail, 190, 6, 14, 482);
            float y = 0;
            foreach (var section in PanelNavigation.Sections)
            {
                Color ink = section.Secondary ? PanelWidgets.Muted : (Color)new Color32(164, 193, 225, 255);
                var icon = PanelWidgets.Rect(content, section.Label + "Icon", 4, y + 3, 15, 15).gameObject.AddComponent<PanelSectionIcon>();
                icon.Configure((PanelSectionSymbol)section.Icon, ink);
                var heading = widgets.Text(content, "Section", section.Label, 25, y, 149, 22, 11);
                heading.color = ink; heading.fontStyle = FontStyles.Bold;
                y += 25;
                foreach (int id in section.Pages)
                {
                    int target = id;
                    var tab = widgets.Button(content, PanelNavigation.Label(id), 0, y, 178, 26, () => SelectPage(target));
                    tab.gameObject.AddComponent<PanelNavigationFocus>().Configure(scroll);
                    var label = tab.GetComponentInChildren<TMP_Text>();
                    label.alignment = TextAlignmentOptions.MidlineLeft;
                    label.rectTransform.anchoredPosition = new Vector2(10, 0);
                    label.rectTransform.sizeDelta = new Vector2(158, 26);
                    tabs[id] = tab;
                    if (id == 12) updateLabel = label;
                    y += 29;
                }
                y += 10;
            }
            content.sizeDelta = new Vector2(178, y - 10);
        }

        private void RefreshNavigation()
        {
            pageTitle.text = L.T(PanelNavigation.Label(page));
            tabs[page].GetComponent<PanelNavigationFocus>().Reveal();
            for (int i = 0; i < tabs.Length; i++)
            {
                var colors = tabs[i].colors;
                bool selected = i == page, secondary = PanelNavigation.Secondary(i);
                colors.normalColor = selected ? new Color32(36, 81, 132, 255) : secondary ?
                    new Color32(21, 32, 45, 255) : new Color32(28, 43, 61, 255);
                colors.selectedColor = colors.normalColor;
                tabs[i].colors = colors;
                tabs[i].GetComponentInChildren<TMP_Text>().color = secondary && !selected ? PanelWidgets.Muted : PanelWidgets.Ink;
            }
        }
    }
}
