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
        internal static readonly Color Ink = new Color32(226, 235, 246, 255);
        internal static readonly Color Muted = new Color32(168, 188, 208, 255);
        internal static readonly Color Accent = new Color32(64, 138, 241, 255);
        public PanelWidgets(TMP_FontAsset font) { this.font = font; }

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
            text.font = font; text.fontSize = size; text.color = Ink; text.richText = false;
            text.raycastTarget = false; text.textWrappingMode = TextWrappingModes.Normal;
            text.overflowMode = TextOverflowModes.Truncate;
            text.text = value;
            return text;
        }

        public Button Button(Transform parent, string value, float x, float y, float width, float height, UnityAction action)
        {
            var rect = Rect(parent, value, x, y, width, height);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = Color.white;
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
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            var label = Text(rect, "Label", value, 2, 0, width - 4, height, 10);
            label.alignment = TextAlignmentOptions.Center;
            button.onClick.AddListener(action);
            return button;
        }

        public TMP_InputField Input(Transform parent, float x, float y, float width, UnityAction<string> edited)
        {
            var rect = Rect(parent, "Amount", x, y, width, 25);
            rect.gameObject.AddComponent<Image>().color = new Color32(14, 24, 36, 255);
            var field = rect.gameObject.AddComponent<TMP_InputField>();
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

        public PanelTextScroll Scroll(Transform parent, float x, float y, float width, float height)
        {
            var rect = Rect(parent, "Readout", x, y, width, height);
            rect.gameObject.AddComponent<Image>().color = new Color32(17, 27, 40, 255);
            var scroll = rect.gameObject.AddComponent<ScrollRect>();
            var viewport = Rect(rect, "Viewport", 5, 5, width - 10, height - 10);
            viewport.gameObject.AddComponent<RectMask2D>();
            var text = Text(viewport, "Values", "", 0, 0, width - 10, height - 10, 10);
            text.overflowMode = TextOverflowModes.Overflow;
            text.raycastTarget = true;
            scroll.viewport = viewport; scroll.content = text.rectTransform;
            scroll.horizontal = false; scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 24;
            return new PanelTextScroll(text, scroll, height - 10, width - 10);
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
