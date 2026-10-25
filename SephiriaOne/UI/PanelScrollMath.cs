using System;

namespace SephiriaOne
{
    internal static class PanelScrollMath
    {
        internal static float Reveal(float position, float contentHeight, float viewportHeight, float top, float height)
        {
            float overflow = contentHeight - viewportHeight;
            if (overflow <= 0) return 1;
            float offset = (1 - position) * overflow;
            float next = top < offset ? top : top + height > offset + viewportHeight ? top + height - viewportHeight : offset;
            return Math.Max(0, Math.Min(1, 1 - next / overflow));
        }

        // Pixel steps stay useful for long lists; normalized fixed steps do not.
        internal static float Step(float position, float contentHeight, float viewportHeight, float pixels, int direction)
        {
            float overflow = contentHeight - viewportHeight;
            if (overflow <= 0) return 1;
            return Math.Max(0, Math.Min(1, position + direction * pixels / overflow));
        }
    }
}
