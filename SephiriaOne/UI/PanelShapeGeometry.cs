using System;

namespace SephiriaOne
{
    // Pure, allocation-free contour calculation, shared with the portable geometry checks.
    // The exponent-three corner has a gentler square silhouette than a circular fillet.
    internal readonly struct PanelShapeGeometry
    {
        internal const int CornerSegments = 6;
        internal const int PointCount = 4 * (CornerSegments + 1);
        private static readonly float[] Curve = CreateCurve();

        internal readonly float X, Y, Width, Height, Radius, BorderWidth;
        internal bool IsValid => Width > 0 && Height > 0;
        internal bool HasFill => IsValid && BorderWidth < Math.Min(Width, Height) * 0.5f;

        private PanelShapeGeometry(float x, float y, float width, float height, float radius, float border)
        {
            X = x; Y = y; Width = width; Height = height; Radius = radius; BorderWidth = border;
        }

        internal static PanelShapeGeometry Create(float x, float y, float width, float height, float radius, float border)
        {
            if (!Finite(x) || !Finite(y) || !Finite(width) || !Finite(height) ||
                width <= 0 || height <= 0 || !Finite(x + width) || !Finite(y + height)) return default;
            float half = Math.Min(width, height) * 0.5f;
            radius = Finite(radius) ? Math.Max(0, Math.Min(radius, half)) : 0;
            border = Finite(border) ? Math.Max(0, Math.Min(border, half)) : 0;
            return new PanelShapeGeometry(x, y, width, height, radius, border);
        }

        // Counterclockwise from the right edge of the top-right corner. Matching
        // outer/inner samples let the renderer join them without temporary arrays.
        internal void GetPoint(int index, bool inner, out float x, out float y)
        {
            int corner = index / (CornerSegments + 1), sample = index % (CornerSegments + 1);
            float inset = inner ? BorderWidth : 0;
            float radius = Math.Max(0, Radius - inset);
            float left = X + inset, right = X + Width - inset;
            float bottom = Y + inset, top = Y + Height - inset;
            float cosine = radius * Curve[sample], sine = radius * Curve[CornerSegments - sample];
            switch (corner)
            {
                case 0: x = right - radius + cosine; y = top - radius + sine; break;
                case 1: x = left + radius - sine; y = top - radius + cosine; break;
                case 2: x = left + radius - cosine; y = bottom + radius - sine; break;
                default: x = right - radius + sine; y = bottom + radius - cosine; break;
            }
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        private static float[] CreateCurve()
        {
            var curve = new float[CornerSegments + 1];
            curve[0] = 1;
            for (int i = 1; i < CornerSegments; i++)
                curve[i] = (float)Math.Pow(Math.Cos(i * Math.PI / (2 * CornerSegments)), 2.0 / 3.0);
            return curve;
        }
    }
}
