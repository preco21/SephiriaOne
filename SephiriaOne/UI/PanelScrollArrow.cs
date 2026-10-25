using UnityEngine;
using UnityEngine.UI;

namespace SephiriaOne
{
    // Serialized references remap to the cloned list when TMP opens a dropdown.
    internal sealed class PanelScrollArrow : MonoBehaviour
    {
        [SerializeField] private ScrollRect scroll;
        [SerializeField] private int direction;
        private Button button;

        internal void Configure(ScrollRect target, int stepDirection)
        { scroll = target; direction = stepDirection; }

        private void OnEnable()
        {
            button = GetComponent<Button>();
            if (button) button.onClick.AddListener(Advance);
        }

        private void OnDisable()
        { if (button) button.onClick.RemoveListener(Advance); }

        private void Advance()
        {
            if (!scroll || !scroll.content || !scroll.viewport) return;
            scroll.StopMovement();
            scroll.verticalNormalizedPosition = PanelScrollMath.Step(scroll.verticalNormalizedPosition,
                scroll.content.rect.height, scroll.viewport.rect.height, 48, direction);
        }
    }
}
