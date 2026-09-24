using TMPro;
using UnityEngine;

namespace SephiriaOne
{
    public sealed class LocalPlayerNameColor : MonoBehaviour
    {
        private readonly GradientNameLabel characterName = new GradientNameLabel();
        private readonly GradientNameLabel overheadName = new GradientNameLabel();
        private readonly MultiplayerNameColor multiplayerName = new MultiplayerNameColor();
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

            multiplayerName.Update(player);

            UI_StatsPanel panel = ui.GetElement<UI_StatsPanel>();
            characterName.Apply(panel ? panel.characterNameText : null);
            overheadName.Apply(player.WorldUserName);

            if (loggedPlayer != player && panel && panel.characterNameText)
            {
                loggedPlayer = player;
                Debug.Log("[SephiriaOne] Local player name gradient applied (#408af1 -> #a8d7fa)");
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
            multiplayerName.Restore();
            loggedPlayer = null;
        }

        private sealed class GradientNameLabel
        {
            private readonly GradientNameText text = new GradientNameText();
            private TMP_Text target;
            private Color originalColor;
            private bool originalRichText;
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
                        originalRichText = target.richText;
                        originalOverrideColorTags = target.overrideColorTags;
                        originalEnableVertexGradient = target.enableVertexGradient;
                    }
                }

                if (!target)
                {
                    return;
                }

                // Let the same per-letter tags render locally and on peers.
                // Keep game-controlled alpha, including fades during UI changes.
                Color white = new Color(1f, 1f, 1f, target.color.a);
                if (target.color != white)
                {
                    target.color = white;
                }

                if (!target.richText) target.richText = true;
                if (target.overrideColorTags)
                {
                    target.overrideColorTags = false;
                }

                if (target.enableVertexGradient)
                {
                    target.enableVertexGradient = false;
                }

                string formatted = text.Apply(target.text ?? "");
                if (target.text != formatted) target.text = formatted;
            }

            public void Restore()
            {
                if (target)
                {
                    target.color = new Color(originalColor.r, originalColor.g,
                        originalColor.b, target.color.a);
                    target.richText = originalRichText;
                    target.overrideColorTags = originalOverrideColorTags;
                    target.enableVertexGradient = originalEnableVertexGradient;
                    string restored = text.Restore(target.text ?? "");
                    if (target.text != restored) target.text = restored;
                }
                else text.Restore("");

                target = null;
            }
        }
    }
}
