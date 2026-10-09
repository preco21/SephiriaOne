using UnityEngine;
using UnityEngine.EventSystems;

namespace SephiriaOne
{
    public sealed class PanelWindowDrag : MonoBehaviour, IBeginDragHandler, IDragHandler
    {
        private RectTransform canvas, window;
        private PanelWindowGeometry geometry;
        private Vector2 pointerStart, windowStart;
        internal void Initialize(RectTransform canvas, RectTransform window, PanelWindowGeometry geometry)
        {
            this.canvas = canvas; this.window = window; this.geometry = geometry;
        }
        public void OnBeginDrag(PointerEventData data)
        {
            if (data.button != PointerEventData.InputButton.Left) return;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvas, data.position, data.pressEventCamera, out pointerStart);
            windowStart = window.anchoredPosition;
        }
        public void OnDrag(PointerEventData data)
        {
            if (data.button != PointerEventData.InputButton.Left) return;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(canvas, data.position, data.pressEventCamera, out var pointer)) return;
            var next = windowStart + pointer - pointerStart;
            geometry.Move(next.x, next.y); window.anchoredPosition = new Vector2(geometry.X, geometry.Y);
        }
    }
}
