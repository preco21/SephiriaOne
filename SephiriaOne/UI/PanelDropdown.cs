using TMPro;
using UnityEngine;

namespace SephiriaOne
{
    internal sealed class PanelDropdown : TMP_Dropdown
    {
        protected override GameObject CreateDropdownList(GameObject template)
        {
            var popup = base.CreateDropdownList(template);
            var canvas = popup.GetComponent<Canvas>();
            var owner = GetComponentInParent<Canvas>();
            // Skip inheriting subcanvases; use the canvas that actually owns order.
            while (owner && !owner.isRootCanvas && !owner.overrideSorting)
                owner = owner.transform.parent ? owner.transform.parent.GetComponentInParent<Canvas>() : null;
            if (canvas && owner)
            {
                // TMP's 30000 default covers the native AppUI cursor (order 20).
                // Native settings roots use 0/2/10. Keep popup above its owner,
                // with room for TMP's blocker at popup order minus one.
                canvas.sortingLayerID = owner.sortingLayerID;
                canvas.sortingOrder = Mathf.Min(short.MaxValue, owner.sortingOrder + 2);
            }
            return popup;
        }
    }
}
