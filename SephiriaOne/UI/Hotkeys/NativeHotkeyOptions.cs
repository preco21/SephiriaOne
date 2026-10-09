using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SephiriaOne
{
    internal sealed class NativeHotkeyOptions : IDisposable
    {
        private GameObject owned;
        private PanelWidgets widgets;
        private TMP_Text current, feedback;
        private Button change, clear, cancel;
        private int languageRevision = -1;
        private string lastBinding, lastMessage;
        private bool lastListening, rendered;
        internal bool IsVisible => owned && owned.activeInHierarchy;
        internal bool Attach(UI_OptionsPanel options, Action begin, Action remove, Action abort)
        {
            if (!options || !options.tab) return false;
            foreach (var page in options.tab.tabContents)
            {
                if (!page || page.name != "Tab-Controls-Keyboard") continue;
                var scroll = page.GetComponentInChildren<ScrollRect>(true);
                if (!scroll || !scroll.content || !scroll.content.GetComponent<VerticalLayoutGroup>() ||
                    !scroll.content.GetComponent<ContentSizeFitter>()) return false;
                var label = page.GetComponentInChildren<TMP_Text>(true);
                var font = label && label.font ? label.font : TMP_Settings.defaultFontAsset;
                if (!font) return false;
                widgets = new PanelWidgets(font);
                var rect = PanelWidgets.Rect(scroll.content, "SephiriaOne.Hotkey", 0, 0, 300, 110);
                owned = rect.gameObject;
                var layout = owned.AddComponent<LayoutElement>(); layout.preferredHeight = 110; layout.minHeight = 110;
                widgets.Text(rect, "Title", "SephiriaOne shortcut", 0, 0, 300, 22, 12);
                current = widgets.Text(rect, "Current shortcut", "", 0, 23, 300, 22, 11);
                change = widgets.Button(rect, "Set / Change", 0, 48, 100, 25, () => begin());
                clear = widgets.Button(rect, "Clear", 105, 48, 80, 25, () => remove());
                cancel = widgets.Button(rect, "Cancel", 190, 48, 80, 25, () => abort());
                feedback = widgets.Text(rect, "Feedback", "", 0, 77, 300, 33, 10);
                return true;
            }
            return false;
        }
        internal void Refresh(string binding, bool listening, string message)
        {
            if (!owned) return;
            if (rendered && lastBinding == binding && lastMessage == message && lastListening == listening && languageRevision == L.Revision) return;
            rendered = true; lastBinding = binding; lastMessage = message; lastListening = listening;
            if (languageRevision != L.Revision)
            { widgets.RefreshLocalization(); languageRevision = L.Revision; }
            string value = L.T("Current shortcut: ") + (binding ?? L.T("Unassigned"));
            if (current.text != value) current.text = value;
            if (feedback.text != message) feedback.text = message;
            change.interactable = clear.interactable = !listening;
            cancel.gameObject.SetActive(listening);
        }
        public void Dispose()
        {
            if (owned) { owned.SetActive(false); UnityEngine.Object.Destroy(owned); }
            owned = null; widgets = null;
        }
    }
}
