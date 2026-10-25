using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace SephiriaOne
{
    // All controls are owned by this addon; native serialized listeners are never copied.
    internal sealed class PanelWidgets
    {
        private readonly TMP_FontAsset font;
        private TMP_FontAsset displayFont;
        private readonly List<(TMP_Text Text, string Key)> labels = new List<(TMP_Text, string)>();
        internal static readonly Color Ink = new Color32(226, 235, 246, 255);
        internal static readonly Color Muted = new Color32(168, 188, 208, 255);
        internal static readonly Color Accent = new Color32(64, 138, 241, 255);
        public PanelWidgets(TMP_FontAsset font) { this.font = font; displayFont = PanelFontResolver.Resolve(font); }

        public void RefreshLocalization()
        {
            displayFont = PanelFontResolver.Resolve(font);
            for (int i = labels.Count - 1; i >= 0; i--)
            {
                var binding = labels[i];
                if (!binding.Text) { labels.RemoveAt(i); continue; }
                binding.Text.font = displayFont;
                if (binding.Key.Length > 0) binding.Text.text = L.T(binding.Key);
            }
        }

        public void Forget(Transform root) => labels.RemoveAll(binding =>
            !binding.Text || binding.Text.transform.IsChildOf(root));

        public static RectTransform Rect(Transform parent, string name, float x, float y, float width, float height)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);
            return rect;
        }

        public static void Stretch(RectTransform rect, float inset = 0)
        {
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(inset, inset); rect.offsetMax = new Vector2(-inset, -inset);
        }

        public TMP_Text Text(Transform parent, string name, string value, float x, float y, float width, float height, float size = 11)
        {
            var text = Rect(parent, name, x, y, width, height).gameObject.AddComponent<TextMeshProUGUI>();
            text.font = displayFont; text.fontSize = size; text.color = Ink; text.richText = false;
            text.enableAutoSizing = value.Length > 0; text.fontSizeMax = size; text.fontSizeMin = size * 0.8f;
            text.raycastTarget = false; text.textWrappingMode = TextWrappingModes.Normal;
            text.overflowMode = TextOverflowModes.Truncate;
            text.alignment = height <= 26 ? TextAlignmentOptions.MidlineLeft : TextAlignmentOptions.TopLeft;
            // Values supplied here are addon-owned static labels. Dynamic/player text
            // is assigned by the caller and is never used as a catalog lookup key.
            text.text = L.T(value);
            labels.Add((text, value));
            return text;
        }

        public Button Button(Transform parent, string value, float x, float y, float width, float height, UnityAction action)
        {
            var rect = Rect(parent, value, x, y, width, height);
            var image = PanelSurface.Add(rect.gameObject, Color.white);
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.normalColor = new Color32(39, 59, 82, 255);
            colors.highlightedColor = new Color32(55, 88, 126, 255);
            colors.pressedColor = Accent;
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = new Color32(32, 39, 48, 255);
            button.colors = colors;
            // Mouse/keyboard first. Native cancel still handles the panel.
            button.navigation = new Navigation { mode = Navigation.Mode.Automatic };
            // Keep semantic object names/callbacks; only the visible caption changes.
            string caption = value == "Add" ? "+" : value == "Subtract" ? "−" : value == "Close" ? "×" : value;
            var label = Text(rect, "Label", caption, 2, 0, width - 4, height, 10);
            label.alignment = TextAlignmentOptions.Center;
            if (action != null) button.onClick.AddListener(action);
            return button;
        }

        public TMP_Dropdown Dropdown(Transform parent, float x, float y, float width, float height,
            List<string> options, UnityAction<int> changed)
        {
            var rect = Rect(parent, "Stat selection", x, y, width, height);
            var backgroundImage = PanelSurface.Add(rect.gameObject, new Color32(25, 42, 62, 255));
            var dropdown = rect.gameObject.AddComponent<PanelDropdown>();
            dropdown.targetGraphic = backgroundImage;
            dropdown.captionText = Text(rect, "Selection", "", 6, 0, width - 12, height, 12);
            var template = Rect(rect, "Template", 0, height, width, 150);
            template.pivot = new Vector2(0, 1);
            PanelSurface.Add(template.gameObject, new Color32(17, 27, 40, 255), 8);
            var scroll = template.gameObject.AddComponent<ScrollRect>();
            float listWidth = width - 24;
            var viewport = Rect(template, "Viewport", 3, 3, listWidth, 144);
            // TMP shrinks short popups (e.g. the two Slates); keep viewport and
            // scrollbar insets relative to the resulting popup, not its template.
            viewport.anchorMin = Vector2.zero; viewport.anchorMax = Vector2.one;
            viewport.offsetMin = new Vector2(3, 3); viewport.offsetMax = new Vector2(-21, -3);
            viewport.gameObject.AddComponent<RectMask2D>();
            var content = Rect(viewport, "Content", 0, 0, listWidth, 26);
            var item = Rect(content, "Item", 0, 0, listWidth, 26);
            var background = PanelSurface.Add(item.gameObject, new Color32(39, 59, 82, 255), 4);
            var toggle = item.gameObject.AddComponent<Toggle>(); toggle.targetGraphic = background;
            var label = Text(item, "Name", "", 6, 0, listWidth - 12, 26, 11);
            dropdown.itemText = label;
            dropdown.template = template; scroll.viewport = viewport; scroll.content = content;
            scroll.horizontal = false; scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 26;
            AddScrollbar(scroll, template, width - 17, 3, 14, 144);
            template.gameObject.SetActive(false);
            dropdown.AddOptions(options); dropdown.onValueChanged.AddListener(changed);
            return dropdown;
        }

        public Toggle Checkbox(Transform parent, string value, float x, float y, float width, float height)
        {
            var rect = Rect(parent, value, x, y, width, height);
            var background = PanelSurface.Add(rect.gameObject, new Color32(25, 42, 62, 255));
            var toggle = rect.gameObject.AddComponent<Toggle>();
            var box = Rect(rect, "Checkbox", 4, 3, height - 6, height - 6);
            PanelSurface.Add(box.gameObject, new Color32(12, 21, 32, 255), 3);
            var check = PanelSurface.Add(Rect(box, "Check", 3, 3, height - 12, height - 12).gameObject, Accent, 2, 0);
            check.raycastTarget = false;
            toggle.targetGraphic = background; toggle.graphic = check;
            toggle.toggleTransition = Toggle.ToggleTransition.None;
            var label = Text(rect, "Label", value, height + 3, 0, width - height - 7, height, 11);
            label.alignment = TextAlignmentOptions.MidlineLeft;
            toggle.navigation = new Navigation { mode = Navigation.Mode.Automatic };
            return toggle;
        }

        public TMP_InputField Input(Transform parent, float x, float y, float width, UnityAction<string> edited)
        {
            var rect = Rect(parent, "Amount", x, y, width, 25);
            var background = PanelSurface.Add(rect.gameObject, new Color32(14, 24, 36, 255));
            var field = rect.gameObject.AddComponent<TMP_InputField>();
            field.targetGraphic = background;
            var viewport = Rect(rect, "Viewport", 5, 2, width - 10, 21);
            viewport.gameObject.AddComponent<RectMask2D>();
            var text = Text(viewport, "Text", "", 0, 0, width - 10, 21, 12);
            text.alignment = TextAlignmentOptions.MidlineLeft;
            var hint = Text(viewport, "Placeholder", "Amount", 0, 0, width - 10, 21, 10);
            hint.color = Muted; hint.alignment = TextAlignmentOptions.MidlineLeft;
            field.textViewport = viewport;
            field.textComponent = (TextMeshProUGUI)text;
            field.placeholder = hint;
            field.lineType = TMP_InputField.LineType.SingleLine;
            field.contentType = TMP_InputField.ContentType.Standard;
            field.characterLimit = 24;
            field.customCaretColor = true; field.caretColor = Ink;
            field.selectionColor = new Color(0.25f, 0.54f, 0.95f, 0.5f);
            field.onValueChanged.AddListener(edited);
            return field;
        }

        public void AddScrollbar(ScrollRect scroll, Transform parent, float x, float y, float width, float height)
        {
            float bottom = ((RectTransform)parent).rect.height - y - height;
            var rail = Rect(parent, "Scrollbar", x, y, width, height);
            rail.anchorMin = Vector2.zero; rail.anchorMax = new Vector2(0, 1);
            rail.offsetMin = new Vector2(x, bottom); rail.offsetMax = new Vector2(x + width, -y);
            var track = Rect(rail, "Track", 0, width + 2, width, height - 2 * (width + 2));
            Stretch(track);
            track.offsetMin = new Vector2(0, width + 2); track.offsetMax = new Vector2(0, -width - 2);
            PanelSurface.Add(track.gameObject, new Color32(25, 39, 55, 255), 4);
            var bar = rail.gameObject.AddComponent<Scrollbar>();
            var handle = Rect(track, "Handle", 0, 0, 0, 0);
            Stretch(handle); handle.pivot = new Vector2(0.5f, 0.5f);
            var image = PanelSurface.Add(handle.gameObject, new Color32(87, 121, 158, 255), 4, 0);
            bar.handleRect = handle; bar.targetGraphic = image; bar.direction = Scrollbar.Direction.BottomToTop;
            bar.navigation = new Navigation { mode = Navigation.Mode.None };
            scroll.verticalScrollbar = bar;
            // Native AutoHide now hides the complete rail, including both arrows.
            // Keep the viewport fixed, avoiding width/reflow oscillation.
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
            var up = ScrollArrow(scroll, rail, 0, 0, width, 1);
            up.anchorMin = new Vector2(0, 1); up.anchorMax = Vector2.one;
            up.sizeDelta = new Vector2(0, width);
            var down = ScrollArrow(scroll, rail, 0, 0, width, -1);
            down.anchorMin = down.pivot = Vector2.zero; down.anchorMax = new Vector2(1, 0);
            down.anchoredPosition = Vector2.zero; down.sizeDelta = new Vector2(0, width);
        }

        private RectTransform ScrollArrow(ScrollRect scroll, Transform parent, float x, float y, float size, int direction)
        {
            var button = Button(parent, "", x, y, size, size, null);
            button.name = direction > 0 ? "ScrollUp" : "ScrollDown";
            button.gameObject.AddComponent<PanelScrollArrow>().Configure(scroll, direction);
            var icon = Rect(button.transform, "Arrow", 3, 3, size - 6, size - 6).gameObject.AddComponent<PanelSectionIcon>();
            icon.Configure(direction > 0 ? PanelSectionSymbol.Up : PanelSectionSymbol.Down, Ink);
            return (RectTransform)button.transform;
        }

        // Preserve the existing logical form width while adding room around it.
        // 6 left + 2 gap + 14 scrollbar + 4 right = 26 extra units.
        public RectTransform ScrollSection(Transform parent, string name, float x, float y,
            float contentWidth, float height, float contentHeight, float sensitivity = 30)
        {
            var container = Rect(parent, name, x, y, contentWidth + 26, height);
            PanelSurface.Add(container.gameObject, new Color32(17, 27, 40, 255), 8);
            var scroll = container.gameObject.AddComponent<ScrollRect>();
            var viewport = Rect(container, "Viewport", 6, 6, contentWidth, height - 12);
            viewport.gameObject.AddComponent<RectMask2D>();
            var content = Rect(viewport, "Content", 0, 0, contentWidth, Mathf.Max(height - 12, contentHeight));
            scroll.viewport = viewport; scroll.content = content; scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = sensitivity;
            AddScrollbar(scroll, container, contentWidth + 8, 6, 14, height - 12);
            return content;
        }

        public PanelTextScroll Scroll(Transform parent, float x, float y, float width, float height)
        {
            var rect = Rect(parent, "Readout", x, y, width, height);
            PanelSurface.Add(rect.gameObject, new Color32(17, 27, 40, 255), 8);
            var scroll = rect.gameObject.AddComponent<ScrollRect>();
            float textWidth = width - 29;
            var viewport = Rect(rect, "Viewport", 6, 5, textWidth, height - 10);
            viewport.gameObject.AddComponent<RectMask2D>();
            var text = Text(viewport, "Values", "", 0, 0, textWidth, height - 10, 10);
            text.overflowMode = TextOverflowModes.Overflow;
            text.raycastTarget = true;
            scroll.viewport = viewport; scroll.content = text.rectTransform;
            scroll.horizontal = false; scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 24;
            AddScrollbar(scroll, rect, width - 19, 5, 14, height - 10);
            return new PanelTextScroll(text, scroll, height - 10, textWidth);
        }
    }

    internal sealed class PanelTextScroll
    {
        private readonly TMP_Text text;
        private readonly ScrollRect scroll;
        private readonly float minimumHeight, width;
        public PanelTextScroll(TMP_Text text, ScrollRect scroll, float height, float width)
        { this.text = text; this.scroll = scroll; minimumHeight = height; this.width = width; }
        public void SetText(string value, bool resetScroll = false)
        {
            if (text.text != value)
            {
                text.text = value;
                text.rectTransform.sizeDelta = new Vector2(width,
                    Mathf.Max(minimumHeight, text.GetPreferredValues(value, width, Mathf.Infinity).y + 4));
            }
            if (resetScroll) scroll.verticalNormalizedPosition = 1;
        }
    }
}
