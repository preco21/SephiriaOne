using TMPro;
using UnityEngine;

namespace SephiriaOne
{
    public sealed class LocalPlayerNameColor : MonoBehaviour
    {
        private readonly BlueNameLabel characterName = new BlueNameLabel();
        private readonly BlueNameLabel overheadName = new BlueNameLabel();
        private PlayerSpawner loggedPlayer;

        private void LateUpdate()
        {
            UIManager ui = UIManager.Instance;
            GameObject playerObject = ui ? ui.connectedPlayer : null;
            PlayerSpawner player = playerObject ? playerObject.GetComponent<PlayerSpawner>() : null;

            if (!player || !player.isOwned)
            {
                RestoreLabels();
                return;
            }

            UI_StatsPanel panel = ui.GetElement<UI_StatsPanel>();
            characterName.Apply(panel ? panel.characterNameText : null);
            overheadName.Apply(player.WorldUserName);

            if (loggedPlayer != player && panel && panel.characterNameText)
            {
                loggedPlayer = player;
                Debug.Log("[SephiriaOne] Blue local player name applied (#0000FF)");
            }
        }

        private void OnDisable()
        {
            RestoreLabels();
        }

        private void RestoreLabels()
        {
            characterName.Restore();
            overheadName.Restore();
            loggedPlayer = null;
        }

        private sealed class BlueNameLabel
        {
            private TMP_Text target;
            private Color originalColor;
            private bool originalOverrideColorTags;
            private bool originalEnableVertexGradient;

            public void Apply(TMP_Text label)
            {
                if (target != label)
                {
                    Restore();
                    target = label;
                    if (target)
                    {
                        originalColor = target.color;
                        originalOverrideColorTags = target.overrideColorTags;
                        originalEnableVertexGradient = target.enableVertexGradient;
                    }
                }

                if (!target)
                {
                    return;
                }

                // Preserve game-controlled fades and never alter the name text.
                Color blue = new Color(0f, 0f, 1f, target.color.a);
                if (target.color != blue)
                {
                    target.color = blue;
                }

                if (!target.overrideColorTags)
                {
                    target.overrideColorTags = true;
                }

                if (target.enableVertexGradient)
                {
                    target.enableVertexGradient = false;
                }
            }

            public void Restore()
            {
                if (target)
                {
                    target.color = new Color(originalColor.r, originalColor.g,
                        originalColor.b, target.color.a);
                    target.overrideColorTags = originalOverrideColorTags;
                    target.enableVertexGradient = originalEnableVertexGradient;
                }

                target = null;
            }
        }
    }
}
