using System;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.UI;

namespace SephiriaOne
{
    public sealed class HotkeyController : MonoBehaviour
    {
        private static readonly Predicate<UIBase> OwnedPanel = control => control is SettingsPanel;
        private HotkeyPolicy policy;
        private UIManager manager;
        private UI_OptionsPanel options;
        private NativeHotkeyOptions adapter;
        private RebindActionUI[] nativeCaptures = Array.Empty<RebindActionUI>();
        private Keyboard keyboard;
        private KeyControl shortcut;
        private float nextLookup;
        private bool refreshOptions = true;
        private int renderedRevision = -1;
        private bool captureReleased, loggedAdapterFailure;
        private string feedbackKey = "";
        private string detail = "";

        private void OnEnable()
        {
            var store = new HotkeyStore(Path.Combine(Application.persistentDataPath, "SephiriaOne", "ui.json"));
            string binding = store.Load(message => Debug.LogWarning("[SephiriaOne] UI shortcut: " + message));
            if (binding != null && !TryKey(binding, out _))
            { Debug.LogWarning("[SephiriaOne] Invalid saved UI shortcut; using Unassigned."); binding = null; }
            policy = new HotkeyPolicy(binding, store.TrySave);
        }

        private void Update()
        {
            try { Tick(); }
            catch (Exception exception)
            {
                policy.CancelCapture();
                SetFeedback("Shortcut unavailable; chat commands remain available.", exception.Message);
                ReleaseOptions();
                nextLookup = Time.unscaledTime + 1;
                if (!loggedAdapterFailure) { loggedAdapterFailure = true; Debug.LogWarning("[SephiriaOne] Shortcut: " + exception); }
            }
        }

        private void Tick()
        {
            var nextManager = UIManager.Instance;
            if (!ReferenceEquals(manager, nextManager) || Time.unscaledTime >= nextLookup)
            {
                nextLookup = Time.unscaledTime + 1;
                UI_OptionsPanel next = null;
                if (nextManager)
                {
                    try { next = nextManager.GetElement<UI_OptionsPanel>(); }
                    catch (System.Collections.Generic.KeyNotFoundException) { }
                }
                if (!ReferenceEquals(manager, nextManager) || !ReferenceEquals(options, next))
                {
                    ReleaseOptions(); manager = nextManager; options = next;
                }
                if (options && adapter == null)
                {
                    nativeCaptures = options.GetComponentsInChildren<RebindActionUI>(true);
                    adapter = new NativeHotkeyOptions(); refreshOptions = true;
                    if (!adapter.Attach(options, BeginCapture, Clear, Cancel))
                    {
                        adapter.Dispose(); adapter = null;
                        if (!loggedAdapterFailure) { loggedAdapterFailure = true; Debug.LogWarning("[SephiriaOne] " + L.T("Shortcut unavailable; chat commands remain available.")); }
                    }
                }
            }
            var nextKeyboard = Keyboard.current;
            if (!ReferenceEquals(keyboard, nextKeyboard))
            {
                keyboard = nextKeyboard; ResolveShortcut();
                if (policy.Listening) Cancel();
                else policy.CancelCapture();
            }
            if (policy.Listening)
            {
                if (!options || !options.IsOpened || adapter == null || !adapter.IsVisible || ForeignCapture() || !OwnsOptionsInput() ||
                    (ControlsChangeHandler.Current && !ControlsChangeHandler.Current.IsUsingKeyboardAndMouse)) Cancel();
                else Capture();
            }
            // Consume the edge before looking up native focus. Blocked presses stay consumed.
            bool candidate = policy.Poll(shortcut != null && shortcut.isPressed, false);
            if (candidate && !ForeignCapture() && !TextFocused() && !OtherMenu())
            {
                string conflict = Validate(policy.Binding);
                if (conflict != null) { SetFeedback(conflict); Report(L.T(conflict)); }
                else if (!SettingsPanelController.TryToggle(out string error)) Report(error);
            }
            if (adapter != null && options && options.IsOpened && (refreshOptions || renderedRevision != L.Revision))
            {
                refreshOptions = false; renderedRevision = L.Revision;
                adapter.Refresh(policy.Binding, policy.Listening, L.T(feedbackKey) + detail);
            }
        }

        private void ResolveShortcut()
        { shortcut = keyboard != null && TryKey(policy.Binding, out Key key) ? keyboard[key] : null; }
        private void BeginCapture()
        {
            if (!OwnsOptionsInput() || ForeignCapture()) return;
            policy.BeginCapture(); captureReleased = false;
            if (EventSystem.current) EventSystem.current.SetSelectedGameObject(null);
            SetFeedback("Press an unused keyboard key. Cancel keeps the current shortcut.");
        }
        private void Capture()
        {
            if (keyboard == null) return;
            if (!captureReleased)
            {
                foreach (var key in keyboard.allKeys) if (key.isPressed) return;
                captureReleased = true; return;
            }
            foreach (var key in keyboard.allKeys)
            {
                if (!key.wasPressedThisFrame) continue;
                if (policy.Capture(key.keyCode.ToString(), Validate, out string error))
                { ResolveShortcut(); SetFeedback("Shortcut saved."); }
                else if (policy.Listening) SetFeedback(error ?? "Shortcut unavailable; chat commands remain available.");
                else SetFeedback("Could not save shortcut; previous shortcut was kept.", error ?? "");
                break;
            }
        }
        private void Clear()
        {
            if (policy.Clear(out string error)) { ResolveShortcut(); SetFeedback("Shortcut cleared."); }
            else SetFeedback("Could not save shortcut; previous shortcut was kept.", error);
        }
        private void Cancel() { policy.CancelCapture(); SetFeedback("Shortcut capture cancelled."); }
        private bool OwnsOptionsInput()
        {
            var stack = manager ? manager.CurrentControlStack : null;
            return options && options.IsOpened && stack != null && stack.Count > 0 && stack[stack.Count - 1] == options;
        }
        private bool ForeignCapture()
        {
            foreach (var capture in nativeCaptures) if (capture && capture.ongoingRebind != null) return true;
            return false;
        }
        private bool OtherMenu()
        {
            var stack = manager ? manager.CurrentControlStack : null;
            return HotkeyInputOwnership.IsBlocked(stack, OwnedPanel);
        }
        private static bool TextFocused()
        {
            var selected = EventSystem.current ? EventSystem.current.currentSelectedGameObject : null;
            return selected && (selected.GetComponentInParent<TMP_InputField>() || selected.GetComponentInParent<InputField>());
        }
        private static bool TryKey(string name, out Key key)
        {
            return Enum.TryParse(name, false, out key) && Enum.IsDefined(typeof(Key), key) && name == key.ToString() && key != Key.None && !Reserved(key);
        }
        private static bool Reserved(Key key)
        {
            switch (key)
            {
                case Key.Escape: case Key.Enter: case Key.NumpadEnter: case Key.Space: case Key.Tab:
                case Key.LeftArrow: case Key.RightArrow: case Key.UpArrow: case Key.DownArrow:
                case Key.LeftShift: case Key.RightShift: case Key.LeftCtrl: case Key.RightCtrl:
                case Key.LeftAlt: case Key.RightAlt: case Key.LeftMeta: case Key.RightMeta:
                    return true;
                default: return false;
            }
        }
        private string Validate(string candidate)
        {
            if (!TryKey(candidate, out Key key)) return "This key is reserved for native navigation or modifiers.";
            if (keyboard == null) return "Shortcut unavailable; chat commands remain available.";
            var handler = ControlsChangeHandler.Current;
            if (Conflicts(options ? options.actions : null, keyboard[key]) ||
                Conflicts(OptionsBinding.Instance ? OptionsBinding.Instance.actionAsset : null, keyboard[key]) ||
                Conflicts(handler && handler.PlayerInput ? handler.PlayerInput.actions : null, keyboard[key]))
                return "This key is already bound to a native action.";
            return null;
        }
        private static bool Conflicts(InputActionAsset asset, KeyControl key)
        {
            if (!asset) return false;
            foreach (var map in asset.actionMaps)
                foreach (var binding in map.bindings)
                    if (!binding.isComposite && !string.IsNullOrEmpty(binding.effectivePath) && InputControlPath.Matches(binding.effectivePath, key)) return true;
            return false;
        }
        private void SetFeedback(string key, string message = "") { feedbackKey = key; refreshOptions = true; detail = message.Length == 0 ? "" : " " + message; }
        private void Report(string message)
        {
            Debug.LogWarning("[SephiriaOne] " + message);
            if (GameLogWriter.Instance) GameLogWriter.Instance.WriteLog("[SephiriaOne] " + message, Color.yellow);
        }
        private void ReleaseOptions()
        {
            policy?.CancelCapture(); adapter?.Dispose(); adapter = null;
            options = null; nativeCaptures = Array.Empty<RebindActionUI>();
        }
        private void OnDisable() { ReleaseOptions(); keyboard = null; shortcut = null; manager = null; policy = null; }
    }
}
