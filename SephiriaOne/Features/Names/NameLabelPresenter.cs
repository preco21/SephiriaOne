using System;

namespace SephiriaOne
{
    internal readonly struct NameLabelColor
    {
        public readonly float R, G, B, A;
        public NameLabelColor(float r, float g, float b, float a) { R = r; G = g; B = b; A = a; }
    }
    internal interface INameLabel
    {
        bool IsAlive { get; }
        bool IsActive { get; }
        string Text { get; set; }
        NameLabelColor Color { get; set; }
        bool RichText { get; set; }
        bool OverrideColorTags { get; set; }
        bool EnableVertexGradient { get; set; }
    }
    // Portable rendering ownership logic; TMP is an adapter, so tests exercise
    // the exact text/color/settings mutations rather than only the formatter.
    internal sealed class NameLabelPresenter : IPresentationBinding
    {
        private readonly INameLabel label;
        private readonly Func<NameView> source;
        private NameLabelColor originalColor;
        private bool originalRichText, originalOverride, originalGradient, styled, ownsColor, ownsGradient;
        private string written = "", plain = "";
        public NameLabelPresenter(INameLabel label, Func<NameView> source) { this.label = label; this.source = source; }
        public bool IsAlive => label.IsAlive;
        public bool IsActive => label.IsAlive && label.IsActive;
        public object Observe()
        {
            var value = source();
            return (value.Text, value.Styled, value.PlainText, value.PreserveBaseColor, label.Text, label.Color, label.RichText, label.OverrideColorTags, label.EnableVertexGradient);
        }
        public ReconcileResult Refresh()
        {
            NameView value = source();
            if (value.Text == null) { Restore(); return ReconcileResult.Waiting("subject identity unavailable"); }
            if (!value.Styled)
            {
                Restore();
                if (label.Text != value.Text) label.Text = value.Text;
                return ReconcileResult.Applied();
            }
            if (!styled)
            {
                originalColor = label.Color;
                originalRichText = label.RichText;
                originalOverride = label.OverrideColorTags;
                originalGradient = label.EnableVertexGradient;
                styled = true;
            }
            // Rendering native markup gives us no ownership of that markup.
            // Only a view with an explicit restoration value may remove text.
            plain = value.PlainText ?? value.Text;
            written = value.Text;
            if (value.PreserveBaseColor) RestoreColor();
            else
            {
                if (!ownsColor) { originalColor = label.Color; ownsColor = true; }
                var white = new NameLabelColor(1, 1, 1, label.Color.A);
                if (!label.Color.Equals(white)) label.Color = white;
            }
            if (!label.RichText) label.RichText = true;
            if (label.OverrideColorTags) label.OverrideColorTags = false;
            // TMP SaveGlyphVertexInfo honors explicit color tags without applying
            // its vertex gradient. Keep mixed-label untagged content native.
            if (value.PreserveBaseColor) RestoreGradient();
            else
            {
                if (!ownsGradient) { originalGradient = label.EnableVertexGradient; ownsGradient = true; }
                if (label.EnableVertexGradient) label.EnableVertexGradient = false;
            }
            if (label.Text != written) label.Text = written;
            return ReconcileResult.Applied();
        }
        public void Restore()
        {
            if (label.IsAlive && styled)
            {
                RestoreColor();
                if (label.RichText) label.RichText = originalRichText;
                if (!label.OverrideColorTags) label.OverrideColorTags = originalOverride;
                RestoreGradient();
                if (label.Text == written) label.Text = plain;
            }
            styled = false;
        }
        private void RestoreColor()
        {
            if (!ownsColor) return;
            if (label.IsAlive && label.Color.R == 1 && label.Color.G == 1 && label.Color.B == 1)
                label.Color = new NameLabelColor(originalColor.R, originalColor.G, originalColor.B, label.Color.A);
            ownsColor = false;
        }
        private void RestoreGradient()
        {
            if (!ownsGradient) return;
            if (label.IsAlive && !label.EnableVertexGradient) label.EnableVertexGradient = originalGradient;
            ownsGradient = false;
        }
    }
}
