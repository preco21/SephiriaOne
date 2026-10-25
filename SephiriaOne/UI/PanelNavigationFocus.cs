using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SephiriaOne
{
    // Navigation rows are direct children of the rail content. No polling: reveal
    // only on keyboard/gamepad selection or an explicit page change.
    internal sealed class PanelNavigationFocus : MonoBehaviour, ISelectHandler
    {
        [SerializeField] private ScrollRect scroll;
        internal void Configure(ScrollRect target) => scroll = target;
        public void OnSelect(BaseEventData data) => Reveal();
        internal void Reveal()
        {
            if (!scroll || !scroll.content || !scroll.viewport) return;
            var row = (RectTransform)transform;
            float next = PanelScrollMath.Reveal(scroll.verticalNormalizedPosition, scroll.content.rect.height,
                scroll.viewport.rect.height, -row.anchoredPosition.y, row.rect.height);
            if (Mathf.Abs(next - scroll.verticalNormalizedPosition) < .0001f) return;
            scroll.StopMovement(); scroll.verticalNormalizedPosition = next;
        }
    }
}
