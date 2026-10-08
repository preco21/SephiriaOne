using System;

namespace SephiriaOne
{
    internal static class UpdateCommand
    {
        internal static string Usage => L.T("Local updates: /one update check|status|install or /one update auto on|off.");
        internal static bool Execute(string[] parts, out bool success, out string message)
        {
            success = false; message = "";
            if (parts.Length < 2 || !parts[0].Equals("/one", StringComparison.OrdinalIgnoreCase) ||
                !parts[1].Equals("update", StringComparison.OrdinalIgnoreCase)) return false;
            bool status = parts.Length == 2 || (parts.Length == 3 && parts[2].Equals("status", StringComparison.OrdinalIgnoreCase));
            string action = parts.Length >= 3 ? parts[2].ToLowerInvariant() : "status";
            if (parts.Length == 3 && action == "help") { success = true; message = Usage; return true; }
            bool automatic = parts.Length == 4 && action == "auto" &&
                (parts[3].Equals("on", StringComparison.OrdinalIgnoreCase) || parts[3].Equals("off", StringComparison.OrdinalIgnoreCase));
            if (!status && !automatic && !(parts.Length == 3 && (action == "check" || action == "install")))
            { message = Usage; return true; }
            var service = UpdateFeature.Service;
            if (service == null) { message = L.T("Updater is unavailable; see Player.log or install from GitHub Releases manually."); return true; }
            if (status) { success = true; message = UpdateText.Status(service.Snapshot); return true; }
            success = automatic ? service.SetAutomatic(parts[3].Equals("on", StringComparison.OrdinalIgnoreCase)) :
                action == "check" ? service.Check() : service.Install();
            message = success ? (automatic ? L.T("Saving the local automatic-check preference.") : UpdateText.Summary(service.Snapshot)) :
                L.T("Update action is not ready. Wait for the current operation or check cooldown, then inspect /one update status.");
            return true;
        }
    }
}
