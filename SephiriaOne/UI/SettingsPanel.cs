using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SephiriaOne
{
    public sealed partial class SettingsPanel : UIBase
    {
        private readonly PanelWindowGeometry geometry = new PanelWindowGeometry(800, 480);
        private readonly List<Button> tabs = new List<Button>();
        private readonly List<PanelCheckbox> checkboxes = new List<PanelCheckbox>();
        private Vector2 lastBounds;
        private readonly PanelDraft draft = new PanelDraft();
        private readonly List<Button> changeButtons = new List<Button>();
        private PanelWidgets widgets;
        private RectTransform window, pageRoot;
        private TMP_Text availability, feedback, selection, units;
        private TMP_InputField amount;
        private TMP_Dropdown statPicker;
        private PanelTextScroll readout;
        private Button resetOne, resetAll, save, forget;
        private int page, statIndex, choiceIndex, resourceIndex, merchantIndex;
        private int languageRevision;
        private PanelControlLifetime<UIBase> lifetime;
        private static readonly string[] Choices = { "all", "item", "weapon", "miracle" };
        public override bool CanBeSearchedByTypeHash => false;

        internal void Initialize(UIManager owner, UIRoot root, TMP_FontAsset font)
        {
            SetRoot(root); hasControl = true; isPlayerUITHing = true; canCloseControlWithESC = true;
            // Retain this manager, rather than consulting a replacement singleton.
            lifetime = new PanelControlLifetime<UIBase>(this, () => owner ?
                (IEnumerable<IEnumerable<UIBase>>)owner.AllControlStack : Array.Empty<IEnumerable<UIBase>>());
            widgets = new PanelWidgets(font);
            languageRevision = L.Revision;
            // Transparent backdrop still consumes pointers; native UIBase gates gameplay input.
            gameObject.AddComponent<Image>().color = new Color(0.01f, 0.025f, 0.05f, 0.12f);
            window = PanelWidgets.Rect(transform, "Settings", 0, 0, 800, 480);
            window.anchorMin = window.anchorMax = window.pivot = new Vector2(0.5f, 0.5f);
            window.gameObject.AddComponent<Image>().color = new Color(0.09f, 0.14f, 0.20f, 0.88f);
            var title = PanelWidgets.Rect(window, "TitleDrag", 0, 0, 560, 40);
            title.gameObject.AddComponent<Image>().color = new Color32(23, 35, 50, 255);
            title.gameObject.AddComponent<PanelWindowDrag>().Initialize((RectTransform)transform, window, geometry);
            widgets.Text(title, "Title", "SephiriaOne", 16, 8, 530, 26, 20);
            var close = widgets.Button(window, "Close", 688, 8, 94, 28, Close);
            defaultSelectable = close.gameObject;
            scopeLabel = widgets.Text(window, "Scope", "Host controls  /  All current and joining players", 16, 44, 768, 22, 12);
            scopeLabel.color = PanelWidgets.Muted;
            availability = widgets.Text(window, "Availability", "", 16, 68, 768, 28, 12);
            string[] pages = { "Stats", "Fountain", "Choices", "Resources", "Presets", "Status", "Rabbit", "Merchant", "Items", "Combat", "Spawns", "Costumes", "Updates", "Deathmatch" };
            for (int i = 0; i < pages.Length; i++)
            {
                int target = i;
                var tab = widgets.Button(window, pages[i], 16 + 110 * (i % 7), 102 + 34 * (i / 7), 104, 30, () => SelectPage(target));
                tabs.Add(tab);
                if (i == 12) updateLabel = tab.GetComponentInChildren<TMP_Text>();
            }
            feedback = widgets.Text(window, "Feedback", "Choose an action to apply. Native menus and offers refresh normally.", 16, 422, 768, 48, 12);
            SelectPage(0);
        }

        internal void Show()
        {
            if (IsOpened) return;
            draft.Clear(); ClearInput();
            Refresh(ReadCurrentSnapshot(true));
            transform.SetAsLastSibling();
            lifetime.Open(Open);
        }

        internal bool HasControlRegistration => lifetime != null && lifetime.IsRegistered;

        internal SettingsSnapshot ReadCurrentSnapshot(bool refreshSaved = false) =>
            SessionSettings.ReadSnapshot(refreshSaved, includeDiagnostics: page == 5);

        public override void Close()
        {
            try
            {
                if (lifetime == null) gameObject.SetActive(false);
                else lifetime.Close(() =>
                {
                    // A failed earlier close may already have deactivated us.
                    if (IsOpened) base.Close();
                    else RemoveControlFromParent();
                }, () => gameObject.SetActive(false));
            }
            finally { draft.Clear(); ClearInput(); }
        }

        private void OnDestroy()
        {
            // Also balance the stack if something outside the controller destroys us.
            try { if (HasControlRegistration && ParentRoot) RemoveControlFromParent(); }
            catch (Exception exception) { Debug.LogWarning("[SephiriaOne] Externally destroyed settings panel cleanup failed: " + exception); }
        }

        internal void Refresh(SettingsSnapshot snapshot)
        {
            RefreshLanguage();
            RefreshUpdateHeader();
            if (draft.Observe(snapshot.SessionIdentity, snapshot.Epoch, snapshot.RunGeneration))
            {
                ClearInput();
                feedback.text = L.T("Session or run changed. Review the current values before applying.");
            }
            FitWindow();
            foreach (var checkbox in checkboxes) checkbox.Refresh(snapshot);
            if (page == 12) { RefreshUpdates(); return; }
            if (page == 13) { RefreshDeathmatch(snapshot); return; }
            bool choicesReady = page == 11 ? snapshot.BatAvailable && snapshot.CollinAvailable : page == 10 ? (eventSpawnsSelected ? snapshot.EventSpawnsAvailable : snapshot.JarSpawnsAvailable) : page == 9 ? snapshot.FriendlyFireAvailable : page == 8 ? snapshot.ItemRestrictionsAvailable : page == 7 ? snapshot.MerchantsAvailable : page == 6 ? snapshot.RabbitPotionsAvailable : page == 2 ? snapshot.ChoicesAvailable : page != 3 || ResourceFeature.IsAvailable(ResourceCatalog.All[resourceIndex].Kind);
            bool levelUpReady = page != 6 || snapshot.RabbitLevelUpPotionsAvailable;
            availability.text = !snapshot.CanMutate ? snapshot.AvailabilityReason : !choicesReady ?
                (page == 11 ? L.T("Some costume controls are unavailable. See the details on the right and Player.log.") :
                page == 10 ? (eventSpawnsSelected ? L.T("Random event compatibility checks failed. Reset remains available; see Player.log.") : L.T("Mystic Jar compatibility checks failed. Reset remains available; see Player.log.")) :
                page == 9 ? L.T("Friendly-fire compatibility checks failed. Off/reset remain available; see Player.log.") :
                page == 8 ? L.T("Item restriction compatibility checks failed. Off/reset remain available; see Player.log.") :
                page == 7 ? L.T("Merchant compatibility checks failed. Off/reset remain available; see Player.log.") :
                page == 6 ? L.T("Rabbit potion compatibility checks failed. Off/reset remain available; see Player.log.") :
                page == 3 ? ResourceFeature.UnavailableReason(ResourceCatalog.All[resourceIndex].Kind) :
                L.T("Extra-choice compatibility guard failed. Reset remains available; see Player.log.")) :
                !levelUpReady ? L.T("Rabbit level-up potion compatibility checks failed. Off/reset remain available; see Player.log.") :
                L.F("{0} ready player(s). Checkboxes apply immediately; amounts require an action.", snapshot.Players.Count);
            availability.color = snapshot.CanMutate && choicesReady && levelUpReady ? PanelWidgets.Muted : (Color)new Color32(255, 200, 122, 255);
            foreach (var button in changeButtons) button.interactable = snapshot.CanMutate && choicesReady;
            RefreshCombat(snapshot);
            if (collinReset) collinReset.interactable = snapshot.CanMutate || (snapshot.HostActive && snapshot.SessionIdentity != null &&
                (snapshot.FaultedFeature == "collin" || snapshot.FaultedFeature == "inheritance"));
            if (amount) amount.interactable = snapshot.CanMutate && choicesReady;
            // Full-family reset is intentionally available for the existing recovery path.
            string family = page == 0 ? "stats" : page == 1 ? "fountain" : page == 3 ? "resources" : page == 6 ? "rabbit" : page == 7 ? "merchant" : page == 8 ? "items" : page == 9 ? "combat" : page == 10 ? (eventSpawnsSelected ? "events" : "jars") : page == 11 ? "bat" : "choices";
            bool recovery = snapshot.HostActive && snapshot.SessionIdentity != null &&
                (snapshot.FaultedFeature == family || snapshot.FaultedFeature == "inheritance");
            if (resetAll) resetAll.interactable = snapshot.CanMutate || recovery;
            if (page == 9 && resetAll && DeathmatchRuntime.IsRunning && snapshot.HostActive && snapshot.SessionIdentity != null) resetAll.interactable = true;
            if (resetOne) resetOne.interactable = snapshot.CanMutate;
            if (save) save.interactable = snapshot.CanSave;
            if (forget) forget.interactable = snapshot.CanForget;
            if (page < 4) readout.SetText(PlayerValues(snapshot));
            else if (page == 6) readout.SetText(RabbitValues(snapshot));
            else if (page == 7) readout.SetText(MerchantValues(snapshot));
            else if (page == 8) readout.SetText(ItemValues(snapshot));
            else if (page == 9) readout.SetText(CombatValues(snapshot));
            else if (page == 10) readout.SetText(eventSpawnsSelected ? EventValues(snapshot) : JarValues(snapshot));
            else if (page == 11) readout.SetText(CostumeValues(snapshot));
            else if (page == 4) readout.SetText(PresetValues(snapshot));
            else readout.SetText(L.T("Automatic name gradient: #408af1 -> #a8d7fa\nHost's native multiplayer character name; colors are fixed.") +
                "\n\n" + string.Join("\n\n", snapshot.Lines));
        }

        private void RefreshLanguage()
        {
            if (languageRevision == L.Revision) return;
            widgets.RefreshLocalization();
            BuildPage(page);
            feedback.text = L.T("Language updated. Review the current values before applying.");
            feedback.color = PanelWidgets.Muted;
            languageRevision = L.Revision;
        }

        private void SelectPage(int target)
        {
            BuildPage(target);
            Refresh(ReadCurrentSnapshot());
        }

        private void BuildPage(int target)
        {
            page = target; draft.Clear(); changeButtons.Clear(); checkboxes.Clear();
            for (int i = 0; i < tabs.Count; i++) { var colors = tabs[i].colors; colors.normalColor = i == target ? PanelWidgets.Accent : (Color)new Color32(39, 59, 82, 255); colors.selectedColor = colors.normalColor; tabs[i].colors = colors; }
            statPicker = null; amount = null; resetOne = resetAll = save = forget = null;
            collinReset = null;
            updateInstall = updateCheck = null; updateAutomatic = null;
            friendlyDamage = null; friendlyPercent = null; friendlyDraft = false;
            if (pageRoot) { widgets.Forget(pageRoot); pageRoot.gameObject.SetActive(false); Destroy(pageRoot.gameObject); }
            pageRoot = PanelWidgets.Rect(window, "Page", 0, 184, 600, 162);
            pageRoot.localScale = Vector3.one * (4f / 3f);
            if (page < 4) BuildEditor();
            else if (page == 6) BuildRabbitEditor();
            else if (page == 7) BuildMerchantEditor();
            else if (page == 8) BuildItemEditor();
            else if (page == 9) BuildCombatEditor();
            else if (page == 10) BuildSpawnEditor();
            else if (page == 11) BuildCostumeEditor();
            else if (page == 12) BuildUpdatesEditor();
            else if (page == 13) BuildDeathmatchEditor();
            else
            {
                if (page == 4)
                {
                    save = widgets.Button(pageRoot, "Save current settings", 16, 0, 182, 24, () => Execute("/one save", true));
                    forget = widgets.Button(pageRoot, "Forget saved preset", 207, 0, 180, 24, () => Execute("/one forget", false));
                    widgets.Button(pageRoot, "Refresh saved copy", 396, 0, 188, 24, RefreshSaved);
                    widgets.Text(pageRoot, "SavedHelp", "Save stores applied settings for future hosted sessions. Unapplied input is excluded.", 16, 28, 568, 19, 9);
                    readout = widgets.Scroll(pageRoot, 16, 50, 568, 112);
                }
                else
                {
                    widgets.Button(pageRoot, "Refresh", 494, 0, 90, 22, RefreshSaved);
                    widgets.Text(pageRoot, "StatusTitle", "Current settings and synchronization status", 16, 1, 470, 20, 11);
                    readout = widgets.Scroll(pageRoot, 16, 26, 568, 136);
                }
            }
        }

        private void BuildEditor()
        {
            if (page != 1)
            {
                widgets.Button(pageRoot, "<", 16, 0, 24, 23, () => MoveSelection(-1));
                widgets.Button(pageRoot, ">", 265, 0, 24, 23, () => MoveSelection(1));
            }
            selection = widgets.Text(pageRoot, "Selection", "", page == 1 ? 16 : 47, 1, 212, 24, page == 3 ? 12 : 14);
            if (page == 0)
            {
                selection.gameObject.SetActive(false);
                var names = new List<string>();
                foreach (var stat in StatCatalog.All) names.Add(L.T(stat.Label));
                statPicker = widgets.Dropdown(pageRoot, 47, 0, 212, 25, names, index => {
                    statIndex = index; draft.Clear(); ClearInput(); UpdateSelection(); Refresh(ReadCurrentSnapshot());
                });
                statPicker.SetValueWithoutNotify(statIndex);
            }
            units = widgets.Text(pageRoot, "Units", "", 16, 29, 273, 21, 9);
            amount = widgets.Input(pageRoot, 16, 56, 97, draft.Edit);
            string[] operations = { "set", "add", "sub" };
            string[] labels = { "Set", "Add", "Subtract" };
            for (int i = 0; i < operations.Length; i++)
            {
                string op = operations[i];
                changeButtons.Add(widgets.Button(pageRoot, labels[i], 120 + 57 * i, 56, 53, 25,
                    () => Execute(Prefix() + " " + op + " " + draft.Text, true)));
            }
            string help = page == 0 ? "Set accepts x3 = each native stat times 3; x1 restores native. Add/subtract start a new offset after Set or xN." :
                page == 1 ? "Set accepts x3 = each native allowance times 3; x1 restores native. Add/subtract after xN start a new offset." :
                page == 3 ? "xN = native baseline times N. Dice: future starts. Leaves: change before first departure. Current balances stay unchanged; unsafe budget reductions are rejected." :
                "Amounts are extra candidates, not totals. Existing offers stay cached; new offers use the updated stats.";
            widgets.Text(pageRoot, "Help", help, 16, 88, 273, 46, 9);
            if (page == 1)
                resetAll = widgets.Button(pageRoot, "Reset Fountain", 16, 137, 273, 25, () => Execute("/fountain reset", true));
            else
            {
                resetOne = widgets.Button(pageRoot, "Reset selected", 16, 137, 132, 25, () => Execute(Prefix() + " reset", true));
                resetAll = widgets.Button(pageRoot, page == 0 ? "Reset all stats" : page == 3 ? "Reset all resources" : "Reset all choices", 156, 137, 133, 25,
                    () => Execute(page == 0 ? "/stats reset" : page == 3 ? "/resources reset" : "/choices reset", true));
            }
            widgets.Text(pageRoot, "Players", "Current player values  ·  scroll for more", 312, 0, 272, 22, 10).color = PanelWidgets.Muted;
            readout = widgets.Scroll(pageRoot, 312, 26, 272, 136);
            UpdateSelection();
        }

        private void MoveSelection(int delta)
        {
            if (page == 0) statIndex = (statIndex + delta + StatCatalog.All.Count) % StatCatalog.All.Count;
            else if (page == 3) resourceIndex = (resourceIndex + delta + ResourceCatalog.All.Count) % ResourceCatalog.All.Count;
            else choiceIndex = (choiceIndex + delta + Choices.Length) % Choices.Length;
            draft.Clear(); ClearInput(); UpdateSelection(); Refresh(ReadCurrentSnapshot());
        }

        private void UpdateSelection()
        {
            if (page == 0)
            {
                var stat = StatCatalog.All[statIndex];
                selection.text = L.T(stat.Label);
                if (statPicker) statPicker.SetValueWithoutNotify(statIndex);
                units.text = L.F("{0}  |  {1}..{2}  |  {3}", L.T(stat.Unit), stat.Minimum, stat.Maximum,
                    L.T(stat.Scale == 100 ? "2 decimal places" : "whole numbers"));
            }
            else if (page == 1) { selection.text = L.T("Wishing Fountain"); units.text = L.T("Whole-number points. Each resulting balance must be valid."); }
            else if (page == 3)
            {
                var definition = ResourceCatalog.All[resourceIndex];
                selection.text = L.T(definition.Label);
                units.text = L.F("{0}..{1} whole numbers | {2}", definition.Minimum, definition.Maximum,
                    L.T(definition.Kind == ResourceKind.Leaves ? "pending starting grant" : definition.StartingOnly ? "future starts" : "all players"));
            }
            else
            {
                selection.text = L.T(choiceIndex == 0 ? "All choice categories" : choiceIndex == 1 ? "item choices" : choiceIndex == 2 ? "weapon choices" : "miracle choices");
                units.text = L.T("Extra candidates: 0..20. Native multipliers still apply.");
            }
        }

        private string Prefix() => page == 0 ? "/stats " + StatCatalog.All[statIndex].Name : page == 1 ? "/fountain" :
            page == 3 ? "/resources " + ResourceCatalog.All[resourceIndex].Name : "/choices " + Choices[choiceIndex];

        private bool Execute(string command, bool requiresScope)
        {
            var current = ReadCurrentSnapshot();
            if (requiresScope && !draft.IsCurrent(current.SessionIdentity, current.Epoch, current.RunGeneration))
            {
                Refresh(current); feedback.text = L.T("Session changed. Review the values and enter the action again."); return false;
            }
            // Button state is advisory; the shared services revalidate authority and inputs.
            SettingsActionResult result = SettingsActions.Execute(command);
            if (result.Success) { draft.Clear(); ClearInput(); }
            Refresh(ReadCurrentSnapshot());
            feedback.text = string.Join("\n", result.Messages);
            feedback.color = result.Success ? new Color32(153, 226, 183, 255) : new Color32(255, 200, 122, 255);
            return result.Success;
        }

        private void FitWindow()
        {
            var canvas = transform as RectTransform;
            if (!canvas) return;
            var bounds = canvas.rect.size;
            if (bounds == lastBounds) return;
            lastBounds = bounds; geometry.Fit(bounds.x, bounds.y);
            window.localScale = Vector3.one * geometry.Scale;
            window.anchoredPosition = new Vector2(geometry.X, geometry.Y);
        }
        private void AddCheckbox(string label, float x, float y, float width, float height,
            Func<SettingsSnapshot, bool> value, Func<SettingsSnapshot, bool> available, string prefix, Func<SettingsSnapshot, bool> mutable = null)
        {
            checkboxes.Add(new PanelCheckbox(widgets.Checkbox(pageRoot, label, x, y, width, height), value, available,
                enabled => Execute(prefix + (enabled ? " on" : " off"), true), mutable));
        }

        private void RefreshSaved() { Refresh(ReadCurrentSnapshot(true)); feedback.text = L.T("Refreshed current values and the saved copy."); }
        private void ClearInput() { friendlyDraft = false; if (amount) amount.SetTextWithoutNotify(""); }

        private static string PresetValues(SettingsSnapshot snapshot)
        {
            string active = snapshot.ActiveSettings.Count == 0 ? L.T("None.") : string.Join("\n", snapshot.ActiveSettings);
            string saved = snapshot.SavedSettings.Count == 0 ? snapshot.SavedSummary : string.Join("\n", snapshot.SavedSettings);
            return L.T("ACTIVE SETTINGS") + "\n" + active + "\n\n" + L.T("SAVED FOR FUTURE HOSTED SESSIONS") + "\n" + saved +
                "\n\n" + L.T("Forget leaves the active settings unchanged. Resetting does not erase the saved copy.") +
                "\n" + L.T("Setting rows retain command/preset syntax in every language.");
        }

        private string PlayerValues(SettingsSnapshot snapshot)
        {
            var text = new StringBuilder();
            foreach (var player in snapshot.Players)
            {
                text.Append("#").Append(player.Id).Append("  ").Append(NameGradient.Plain(player.Name ?? "")).AppendLine();
                if (page == 0)
                {
                    var stat = StatCatalog.All[statIndex];
                    if (player.Stats.TryGetValue(stat.Name, out decimal value)) text.Append(value.ToString("0.##", CultureInfo.InvariantCulture)).Append(' ').Append(L.T(stat.Unit));
                }
                else if (page == 1) text.Append(L.F("{0} points (addon {1})", player.FountainPoints, player.FountainContribution.ToString("+0;-0;0", CultureInfo.InvariantCulture)));
                else if (page == 3)
                { if (player.Resources.TryGetValue(ResourceCatalog.All[resourceIndex].Name, out string value)) text.Append(value); }
                else foreach (var choice in player.ExtraChoices)
                    if (choiceIndex == 0 || choice.Key == Choices[choiceIndex]) text.Append(L.F("{0}: {1} extra  ", L.T(choice.Key), choice.Value));
                text.AppendLine().AppendLine();
            }
            return text.Length == 0 ? L.T("Waiting for ready player values.") : text.ToString();
        }
    }
}
