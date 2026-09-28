using System;
using System.Collections.Generic;
using Mirror;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SephiriaOne
{
    public sealed class SettingsPanelController : MonoBehaviour
    {
        private static SettingsPanelController current;
        private UIManager manager;
        private UI_PausePanel pause;
        private SettingsPanel panel;
        private Button entryButton;
        private TMP_FontAsset font;
        private PanelWidgets entryWidgets;
        private int languageRevision;
        private float nextRefresh, nextBind, nextCleanup, nextLookup;
        private bool loggedFailure, cleanupPending;

        private void OnEnable() { current = this; }

        private void Update()
        {
            // Escape closes through UIBase, outside the controller's call path.
            if (panel && !panel.IsOpened && panel.HasControlRegistration) cleanupPending = true;
            if (cleanupPending)
            {
                if (Time.unscaledTime < nextCleanup) return;
                nextCleanup = Time.unscaledTime + 1;
                if (!DisposePanel()) return;
            }
            UIManager nextManager = UIManager.Instance;
            UI_PausePanel nextPause = pause;
            if (!ReferenceEquals(manager, nextManager) || Time.unscaledTime >= nextLookup)
            {
                nextLookup = Time.unscaledTime + 1;
                nextPause = FindPause(nextManager);
            }
            if (!ReferenceEquals(manager, nextManager) || !ReferenceEquals(pause, nextPause))
            {
                if (!Release()) return;
                manager = nextManager; pause = nextPause;
                loggedFailure = false; nextBind = 0;
            }
            if (!entryButton && manager && pause && Time.unscaledTime >= nextBind)
            {
                nextBind = Time.unscaledTime + 1;
                try { Bind(); }
                catch (Exception exception) { ReportFailure(exception); }
            }
            if (entryButton) entryButton.gameObject.SetActive(NetworkServer.active);
            if (entryWidgets != null && languageRevision != L.Revision)
            {
                entryWidgets.RefreshLocalization();
                languageRevision = L.Revision;
            }
            if (!panel || !panel.IsOpened) return;
            if (!NetworkServer.active || !manager || !manager.connectedPlayer || !pause || !pause.ParentRoot)
            { DisposePanel(); return; }
            if (Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + 0.25f;
            try { panel.Refresh(SessionSettings.ReadSnapshot()); }
            catch (Exception exception) { ReportFailure(exception); DisposePanel(); }
        }

        private UI_PausePanel FindPause(UIManager nextManager)
        {
            if (!nextManager) return null;
            try { return nextManager.GetElement<UI_PausePanel>(); }
            catch (KeyNotFoundException) { return null; } // Title/loading managers need not contain a pause UI.
            catch (Exception exception) { ReportFailure(exception); return null; }
        }

        private void Bind()
        {
            if (!pause.ParentRoot) return;
            var nativeLabel = pause.GetComponentInChildren<TMP_Text>(true);
            font = nativeLabel && nativeLabel.font ? nativeLabel.font : TMP_Settings.defaultFontAsset;
            if (!font) return;
            entryWidgets = new PanelWidgets(font);
            languageRevision = L.Revision;
            entryButton = entryWidgets.Button(pause.transform, "SephiriaOne", 0, 0, 106, 23, OpenFromButton);
            var rect = (RectTransform)entryButton.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one;
            rect.anchoredPosition = new Vector2(-12, -12);
            entryButton.gameObject.SetActive(NetworkServer.active);
        }

        private void OpenFromButton()
        {
            if (!TryOpen(out string error))
            {
                Debug.LogWarning("[SephiriaOne] " + error);
                if (GameLogWriter.Instance) GameLogWriter.Instance.WriteLog("[SephiriaOne] " + error, Color.yellow);
            }
        }

        public static bool TryOpen(out string error)
        {
            error = L.T("Only the host can open session settings.");
            if (!NetworkServer.active) return false;
            error = L.T("Settings UI is not ready. Enter town or a run, then try /one ui again.");
            if (!current || !current.enabled || !current.manager || !current.manager.connectedPlayer ||
                !current.pause || !current.pause.ParentRoot || !current.font) return false;
            if (current.cleanupPending)
            {
                error = L.T("Settings UI cleanup is pending. It will retry automatically; see Player.log.");
                return false;
            }
            try
            {
                if (!current.panel) current.CreatePanel();
                current.panel.Show();
                error = "";
                return true;
            }
            catch (Exception exception)
            {
                current.ReportFailure(exception);
                bool released = current.DisposePanel();
                error = L.T(released ? "Settings UI could not open. Chat commands remain available; see Player.log." :
                    "Settings UI cleanup is pending. It will retry automatically; see Player.log.");
                return false;
            }
        }

        private void CreatePanel()
        {
            var rect = PanelWidgets.Rect(pause.ParentRoot.transform, "SephiriaOne.SettingsPanel", 0, 0, 0, 0);
            rect.gameObject.SetActive(false);
            PanelWidgets.Stretch(rect);
            rect.gameObject.AddComponent<CanvasGroup>();
            panel = rect.gameObject.AddComponent<SettingsPanel>();
            panel.Initialize(manager, pause.ParentRoot, font);
        }

        private bool DisposePanel()
        {
            cleanupPending = !TryDisposePanel(ref panel, ReportFailure);
            return !cleanupPending;
        }

        internal static bool TryDisposePanel(ref SettingsPanel target, Action<Exception> reportFailure)
        {
            if (!target) { target = null; return true; }
            try { target.Close(); }
            catch (Exception exception) { reportFailure(exception); }
            // Removal callbacks may fail before OR after UIManager removes us.
            // Never repeat its counter decrement after membership has gone.
            if (target.HasControlRegistration) return false;
            Destroy(target.gameObject);
            target = null;
            return true;
        }

        private bool Release()
        {
            bool released = DisposePanel();
            if (entryButton)
            {
                entryButton.onClick.RemoveListener(OpenFromButton);
                entryButton.gameObject.SetActive(false);
                Destroy(entryButton.gameObject);
            }
            entryButton = null; font = null; entryWidgets = null;
            return released;
        }

        private void ReportFailure(Exception exception)
        {
            if (loggedFailure) return;
            loggedFailure = true;
            Debug.LogWarning("[SephiriaOne] Settings UI unavailable: " + exception);
        }

        private void OnDisable()
        {
            if (!Release())
            {
                // Entry destroys its shared controller object on unload. Keep
                // unresolved native ownership in a separate cleanup-only object.
                var retry = new GameObject("SephiriaOne.SettingsPanelCleanup");
                DontDestroyOnLoad(retry);
                retry.AddComponent<SettingsPanelCleanup>().Retain(panel);
                panel = null; cleanupPending = false;
            }
            manager = null; pause = null;
            if (current == this) current = null;
        }
    }

    public sealed class SettingsPanelCleanup : MonoBehaviour
    {
        private SettingsPanel panel;
        private float nextAttempt;
        private bool loggedFailure;
        internal void Retain(SettingsPanel target) { panel = target; }
        private void Update()
        {
            if (Time.unscaledTime < nextAttempt) return;
            nextAttempt = Time.unscaledTime + 1;
            if (SettingsPanelController.TryDisposePanel(ref panel, exception =>
            {
                if (loggedFailure) return;
                loggedFailure = true;
                Debug.LogWarning("[SephiriaOne] Retained settings panel cleanup will retry: " + exception);
            })) Destroy(gameObject);
        }
    }
}
