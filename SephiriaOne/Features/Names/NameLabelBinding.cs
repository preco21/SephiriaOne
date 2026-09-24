using System;
using TMPro;
using UnityEngine;

namespace SephiriaOne
{
    internal sealed class NameLabelBinding : IPresentationBinding
    {
        private sealed class TmpLabel : INameLabel
        {
            private readonly TMP_Text label;
            private readonly Func<bool> active;
            private readonly bool fitTextWidth;
            public TmpLabel(TMP_Text label, Func<bool> active, bool fitTextWidth)
            { this.label = label; this.active = active; this.fitTextWidth = fitTextWidth; }
            public bool IsAlive => label;
            public bool IsActive => label && label.gameObject.activeInHierarchy && (active == null || active());
            public string Text { get => label.text; set => label.text = value; }
            public NameLabelColor Color
            {
                get { var value = label.color; return new NameLabelColor(value.r, value.g, value.b, value.a); }
                set => label.color = new Color(value.R, value.G, value.B, value.A);
            }
            public bool RichText { get => label.richText; set => label.richText = value; }
            public bool OverrideColorTags { get => label.overrideColorTags; set => label.overrideColorTags = value; }
            public bool EnableVertexGradient { get => label.enableVertexGradient; set => label.enableVertexGradient = value; }
            public void UpdateLayout()
            {
                if (fitTextWidth && label)
                    label.rectTransform.sizeDelta = new Vector2(label.preferredWidth, label.rectTransform.sizeDelta.y);
            }
        }
        private readonly NameLabelPresenter presenter;
        public NameLabelBinding(TMP_Text label, Func<NameView> source, Func<bool> active = null, bool fitTextWidth = false)
        { presenter = new NameLabelPresenter(new TmpLabel(label, active, fitTextWidth), source); }
        public bool IsAlive => presenter.IsAlive;
        public bool IsActive => presenter.IsActive;
        public object Observe() => presenter.Observe();
        public ReconcileResult Refresh() => presenter.Refresh();
        public void Restore() => presenter.Restore();
    }
}
