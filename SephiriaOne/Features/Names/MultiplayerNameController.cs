using UnityEngine;

namespace SephiriaOne
{
    // Publish through native name replication; each peer uses its own native UI.
    public sealed class MultiplayerNameController : MonoBehaviour
    {
        private readonly MultiplayerNameColor multiplayerName = new MultiplayerNameColor();
        public static string Diagnostics { get; private set; } = "name synchronization not initialized";

        private void LateUpdate()
        {
            UIManager ui = UIManager.Instance;
            GameObject playerObject = ui ? ui.connectedPlayer : null;
            PlayerSpawner player = playerObject ? playerObject.GetComponent<PlayerSpawner>() : null;
            if (player && player.isOwned) multiplayerName.Update(player);
            else multiplayerName.Restore();
            Diagnostics = multiplayerName.Status;
        }

        private void OnDisable()
        {
            multiplayerName.Restore();
            Diagnostics = "name synchronization disabled";
        }
    }
}
