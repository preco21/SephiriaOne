using UnityEngine;

namespace SephiriaOne
{
    // Publish through native name replication; each peer uses its own native UI.
    public sealed class MultiplayerNameController : MonoBehaviour
    {
        private readonly MultiplayerNameColor multiplayerName = new MultiplayerNameColor();
        private static MultiplayerNameColor diagnosticSource;
        private static string diagnostics = "name synchronization not initialized";
        private static string translatedSource, translatedDiagnostics;
        private static int translatedRevision = -1;
        public static string Diagnostics
        {
            get
            {
                // Reading after a language change never advances the name scheduler.
                if (diagnosticSource != null) return diagnosticSource.Status;
                if (translatedSource == diagnostics && translatedRevision == L.Revision) return translatedDiagnostics;
                translatedSource = diagnostics;
                translatedRevision = L.Revision;
                translatedDiagnostics = diagnostics == "name synchronization disabled"
                    ? L.T("name synchronization disabled") : L.T("name synchronization not initialized");
                return translatedDiagnostics;
            }
        }

        private void LateUpdate()
        {
            UIManager ui = UIManager.Instance;
            GameObject playerObject = ui ? ui.connectedPlayer : null;
            PlayerSpawner player = playerObject ? playerObject.GetComponent<PlayerSpawner>() : null;
            if (player && player.isOwned) multiplayerName.Update(player);
            else multiplayerName.Restore();
            diagnosticSource = multiplayerName;
        }

        private void OnDisable()
        {
            multiplayerName.Restore();
            diagnosticSource = null;
            diagnostics = "name synchronization disabled";
        }
    }
}
