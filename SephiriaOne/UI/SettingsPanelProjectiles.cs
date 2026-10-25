using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine.UI;

namespace SephiriaOne
{
    public sealed partial class SettingsPanel
    {
        private TMP_InputField projectileTotal, projectileDefault, projectileSource;
        private TMP_InputField projectileRateTotal, projectileRateSource;
        private Button projectileRateReset;
        private TMP_Dropdown projectilePicker;
        private bool projectileTotalDraft, projectileDefaultDraft, projectileSourceDraft;
        private bool projectileRateTotalDraft, projectileRateSourceDraft;
        private int projectileSourceIndex;

        private void BuildProjectileEditor()
        {
            widgets.Text(pageRoot, "ProjectileTitle", "Secondary projectiles", 16, 0, 273, 24, 12);
            AddCheckbox("Limit secondary projectiles", 16, 29, 273, 25, s => s.Projectiles.Enabled,
                s => s.ProjectilesAvailable, "/one projectiles");
            widgets.Text(pageRoot, "ProjectileTotal", "Total / player", 16, 61, 126, 25, 11);
            projectileTotal = widgets.Input(pageRoot, 148, 61, 60, _ => projectileTotalDraft = true);
            changeButtons.Add(widgets.Button(pageRoot, "Apply", 215, 61, 74, 25,
                () => Execute("/one projectiles total " + projectileTotal.text.Trim(), true)));
            widgets.Text(pageRoot, "ProjectileDefault", "Default / source", 16, 93, 126, 25, 11);
            projectileDefault = widgets.Input(pageRoot, 148, 93, 60, _ => projectileDefaultDraft = true);
            changeButtons.Add(widgets.Button(pageRoot, "Apply", 215, 93, 74, 25,
                () => Execute("/one projectiles source default " + projectileDefault.text.Trim(), true)));
            resetAll = widgets.Button(pageRoot, "Reset", 16, 137, 273, 25, () => Execute("/one projectiles reset", true));

            var names = new List<string>();
            foreach (var source in ProjectileLimitCatalog.All) names.Add(L.T(source.Label));
            projectilePicker = widgets.Dropdown(pageRoot, 312, 0, 272, 25, names, index =>
            {
                projectileSourceIndex = index; projectileSourceDraft = false; Refresh(ReadCurrentSnapshot());
            });
            projectilePicker.SetValueWithoutNotify(projectileSourceIndex);
            widgets.Text(pageRoot, "ProjectileSource", "Source limit · 1–4096", 312, 30, 272, 22, 10);
            projectileSource = widgets.Input(pageRoot, 312, 55, 88, _ => projectileSourceDraft = true);
            changeButtons.Add(widgets.Button(pageRoot, "Apply", 408, 55, 80, 25, () =>
                Execute("/one projectiles source " + ProjectileLimitCatalog.All[projectileSourceIndex].Id + " " + projectileSource.text.Trim(), true)));
            resetOne = widgets.Button(pageRoot, "Reset", 496, 55, 88, 25, () =>
                Execute("/one projectiles source " + ProjectileLimitCatalog.All[projectileSourceIndex].Id + " reset", true));
            readout = widgets.Scroll(pageRoot, 312, 91, 272, PageHeight - 91);

            AddCheckbox("Limit secondary spawn rate", 16, 174, 273, 25, s => s.Projectiles.RateEnabled,
                s => s.ProjectilesAvailable, "/one projectiles rate");
            projectileRateReset = widgets.Button(pageRoot, "Reset spawn rates", 312, 174, 272, 25, ResetProjectileRates);
            widgets.Text(pageRoot, "ProjectileRateTotal", "Creations / player / s", 16, 207, 126, 25, 10);
            projectileRateTotal = widgets.Input(pageRoot, 148, 207, 60, _ => projectileRateTotalDraft = true);
            changeButtons.Add(widgets.Button(pageRoot, "Apply", 215, 207, 74, 25, ApplyProjectileRateTotal));
            widgets.Text(pageRoot, "ProjectileRateSource", "Creations / source / s", 312, 207, 126, 25, 10);
            projectileRateSource = widgets.Input(pageRoot, 444, 207, 60, _ => projectileRateSourceDraft = true);
            changeButtons.Add(widgets.Button(pageRoot, "Apply", 511, 207, 73, 25, ApplyProjectileRateSource));
        }

        private void ApplyProjectileRateTotal() => Execute("/one projectiles rate total " + projectileRateTotal.text.Trim(), true,
            clearInputOnSuccess: ClearProjectileRateTotal);
        private void ApplyProjectileRateSource() => Execute("/one projectiles rate source " + projectileRateSource.text.Trim(), true,
            clearInputOnSuccess: ClearProjectileRateSource);
        private void ResetProjectileRates() => Execute("/one projectiles rate reset", true, clearInputOnSuccess: ClearProjectileRates);
        private void ClearProjectileRateTotal() { projectileRateTotalDraft = false; if (projectileRateTotal) projectileRateTotal.SetTextWithoutNotify(""); }
        private void ClearProjectileRateSource() { projectileRateSourceDraft = false; if (projectileRateSource) projectileRateSource.SetTextWithoutNotify(""); }
        private void ClearProjectileRates() { ClearProjectileRateTotal(); ClearProjectileRateSource(); }

        private void RefreshProjectileEditor(SettingsSnapshot snapshot)
        {
            if (!projectileTotal) return;
            bool canEdit = snapshot.CanMutate && snapshot.ProjectilesAvailable;
            projectileTotal.interactable = projectileDefault.interactable = projectileSource.interactable = canEdit;
            projectileRateTotal.interactable = projectileRateSource.interactable = canEdit;
            projectileRateReset.interactable = snapshot.CanMutate || (snapshot.HostActive && snapshot.SessionIdentity != null &&
                snapshot.FaultedFeature == "inheritance");
            resetOne.interactable = canEdit;
            if (!projectileTotalDraft) projectileTotal.SetTextWithoutNotify(snapshot.Projectiles.PlayerLimit.ToString(CultureInfo.InvariantCulture));
            if (!projectileDefaultDraft) projectileDefault.SetTextWithoutNotify(snapshot.Projectiles.DefaultSourceLimit.ToString(CultureInfo.InvariantCulture));
            if (!projectileSourceDraft) projectileSource.SetTextWithoutNotify(snapshot.Projectiles.GetSourceLimit(
                ProjectileLimitCatalog.All[projectileSourceIndex].Id).ToString(CultureInfo.InvariantCulture));
            if (!projectileRateTotalDraft) projectileRateTotal.SetTextWithoutNotify(snapshot.Projectiles.PlayerRate.ToString(CultureInfo.InvariantCulture));
            if (!projectileRateSourceDraft) projectileRateSource.SetTextWithoutNotify(snapshot.Projectiles.SourceRate.ToString(CultureInfo.InvariantCulture));
        }

        private void ClearProjectileInputs()
        {
            projectileTotalDraft = projectileDefaultDraft = projectileSourceDraft = false;
            ClearProjectileRates();
            if (projectileTotal) projectileTotal.SetTextWithoutNotify("");
            if (projectileDefault) projectileDefault.SetTextWithoutNotify("");
            if (projectileSource) projectileSource.SetTextWithoutNotify("");
        }

        private string ProjectileValues(SettingsSnapshot snapshot)
        {
            string source = ProjectileLimitCatalog.All[projectileSourceIndex].Id;
            return L.F("Source: {0} · {1}", source, L.T(snapshot.Projectiles.SourceLimits.ContainsKey(source) ? "override" : "default")) +
                "\n" + L.T("Caps per player. Existing projectiles finish. Primary attacks stay native.") +
                "\n" + L.T("Independent rate budget: stores 1 second and refills continuously. Includes supported secondary effects beyond active caps; denied attempts are skipped.");
        }
    }
}
