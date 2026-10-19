using System.Globalization;
using TMPro;
using UnityEngine.UI;

namespace SephiriaOne
{
    public sealed partial class SettingsPanel
    {
        private Button zombieStart, zombieStop, zombieApply, zombieResetDuration, zombieResetKda;
        private Button zombieRefillApply, zombieIncomingApply;
        private Button zombieCountApply, zombieCountReset;
        private TMP_InputField zombieCount, zombieRefill, zombieIncoming;
        private bool zombieDurationDraft, zombieCountDraft, zombieRefillDraft, zombieIncomingDraft;
        private void BuildZombieEditor()
        {
            widgets.Text(pageRoot, "ZombieDuration", "Round (s)", 16, 0, 72, 25, 11);
            amount = widgets.Input(pageRoot, 94, 0, 51, EditZombieDuration);
            zombieApply = widgets.Button(pageRoot, "Apply", 153, 0, 62, 25, ApplyZombieDuration);
            zombieResetDuration = widgets.Button(pageRoot, "Reset", 223, 0, 66, 25,
                () => Execute("/one zombie duration reset", true, clearInputOnSuccess: ClearZombieDuration));
            widgets.Text(pageRoot, "ZombieCount", "Starting Zombies", 16, 27, 118, 25, 11);
            zombieCount = widgets.Input(pageRoot, 140, 27, 49, EditZombieCount);
            zombieCountApply = widgets.Button(pageRoot, "Apply", 197, 27, 40, 25, ApplyZombieCount);
            zombieCountReset = widgets.Button(pageRoot, "Reset", 245, 27, 44, 25,
                () => Execute("/one zombie count reset", true, clearInputOnSuccess: ClearZombieCount));
            zombieStart = widgets.Button(pageRoot, "Start match", 16, 54, 87, 25, () => Execute("/one zombie start", true));
            zombieStop = widgets.Button(pageRoot, "Stop match", 109, 54, 87, 25, () => Execute("/one zombie stop", true));
            zombieResetKda = widgets.Button(pageRoot, "Reset K/D/A", 202, 54, 87, 25,
                () => Execute("/one zombie resetkda", true));
            AddCheckbox("Any death converts", 16, 81, 273, 25, s => SessionSettings.ZombieAnyDeath, s => true,
                "/one zombie any-death", s => s.HostActive && s.SessionIdentity != null);
            AddCheckbox("Refill (x)", 16, 108, 150, 25, s => SessionSettings.ZombieModifiers.RefillEnabled, s => true,
                "/one zombie refill", s => s.HostActive && s.SessionIdentity != null);
            zombieRefill = widgets.Input(pageRoot, 174, 108, 56, EditZombieRefill);
            zombieRefillApply = widgets.Button(pageRoot, "Apply", 238, 108, 51, 25, ApplyZombieRefill);
            AddCheckbox("Incoming (%)", 16, 135, 150, 25, s => SessionSettings.ZombieModifiers.IncomingEnabled, s => true,
                "/one zombie damage", s => s.HostActive && s.SessionIdentity != null);
            zombieIncoming = widgets.Input(pageRoot, 174, 135, 56, EditZombieIncoming);
            zombieIncomingApply = widgets.Button(pageRoot, "Apply", 238, 135, 51, 25, ApplyZombieIncoming);
            BuildParticipantsEditor();
        }
        private void RefreshZombie(SettingsSnapshot snapshot)
        {
            bool host = snapshot.HostActive && snapshot.SessionIdentity != null;
            zombieStart.interactable = host && CombatMatchRuntime.CanStartZombie;
            zombieStop.interactable = host && CombatMatchRuntime.IsZombieSurvival;
            zombieResetKda.interactable = host && SessionSettings.CanResetFriendlyFireScores;
            zombieApply.interactable = zombieResetDuration.interactable = amount.interactable = host;
            zombieCount.interactable = zombieCountApply.interactable = zombieCountReset.interactable = host;
            zombieRefill.interactable = zombieIncoming.interactable = zombieRefillApply.interactable = zombieIncomingApply.interactable = host;
            if (!zombieDurationDraft) amount.SetTextWithoutNotify(SessionSettings.ZombieDuration.ToString(CultureInfo.InvariantCulture));
            if (!zombieCountDraft) zombieCount.SetTextWithoutNotify(SessionSettings.ZombieStartingCount.ToString(CultureInfo.InvariantCulture));
            var modifiers = SessionSettings.ZombieModifiers;
            if (!zombieRefillDraft) zombieRefill.SetTextWithoutNotify(modifiers.RefillNumber);
            if (!zombieIncomingDraft) zombieIncoming.SetTextWithoutNotify(modifiers.IncomingNumber);
            availability.text = CombatMatchRuntime.DescribeZombie();
            availability.color = PanelWidgets.Muted;
            if (!DeathmatchFeature.Available || !FriendlyFireFeature.Available || !ReviveAllFeature.Available)
                availability.text = L.T("Zombie Survival compatibility checks failed. Stop remains available; see Player.log.");
            RefreshParticipants(snapshot);
        }
        private void EditZombieDuration(string text) => zombieDurationDraft = true;
        private void EditZombieCount(string text) => zombieCountDraft = true;
        private void EditZombieRefill(string text) => zombieRefillDraft = true;
        private void EditZombieIncoming(string text) => zombieIncomingDraft = true;
        private void ApplyZombieDuration() => Execute("/one zombie duration " + amount.text.Trim(), true,
            clearInputOnSuccess: ClearZombieDuration);
        private void ApplyZombieCount() => Execute("/one zombie count " + zombieCount.text.Trim(), true,
            clearInputOnSuccess: ClearZombieCount);
        private void ApplyZombieRefill() => Execute("/one zombie refill " + zombieRefill.text.Trim(), true,
            clearInputOnSuccess: ClearZombieRefill);
        private void ApplyZombieIncoming() => Execute("/one zombie damage " + zombieIncoming.text.Trim(), true,
            clearInputOnSuccess: ClearZombieIncoming);
        private void ClearZombieDuration() { zombieDurationDraft = false; if (amount) amount.SetTextWithoutNotify(""); }
        private void ClearZombieCount() { zombieCountDraft = false; if (zombieCount) zombieCount.SetTextWithoutNotify(""); }
        private void ClearZombieRefill() { zombieRefillDraft = false; if (zombieRefill) zombieRefill.SetTextWithoutNotify(""); }
        private void ClearZombieIncoming() { zombieIncomingDraft = false; if (zombieIncoming) zombieIncoming.SetTextWithoutNotify(""); }
    }
}
