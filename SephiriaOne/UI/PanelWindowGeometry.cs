using System;

namespace SephiriaOne
{
    // Canvas-local geometry, independent of Unity and pointer devices.
    internal sealed class PanelWindowGeometry
    {
        private readonly float width, height;
        private float canvasWidth, canvasHeight;
        internal float Scale { get; private set; } = 1;
        internal float X { get; private set; }
        internal float Y { get; private set; }
        internal PanelWindowGeometry(float width, float height) { this.width = width; this.height = height; }
        internal void Fit(float width, float height)
        {
            canvasWidth = Math.Max(0, width); canvasHeight = Math.Max(0, height);
            Scale = Math.Max(0.01f, Math.Min(1, Math.Min(Math.Max(0, width - 20) / this.width, Math.Max(0, height - 20) / this.height)));
            Move(X, Y);
        }
        internal void Move(float x, float y)
        {
            float mx = Math.Max(0, (canvasWidth - width * Scale) / 2 - 10);
            float my = Math.Max(0, (canvasHeight - height * Scale) / 2 - 10);
            X = Math.Max(-mx, Math.Min(mx, x)); Y = Math.Max(-my, Math.Min(my, y));
        }
        internal void Center() { X = Y = 0; }
    }
}
