using System;
using SephiriaOne;

internal static class Program
{
    private static int checks;

    private static void Check(bool condition, string message)
    {
        checks++;
        if (!condition) throw new InvalidOperationException(message);
    }

    private static void Near(float actual, float expected, string message)
    {
        Check(Math.Abs(actual - expected) < 0.0001f, message);
    }

    private static void Main()
    {
        var panel = PanelShapeGeometry.Create(-470, -280, 940, 560, 12, 1);
        Check(panel.IsValid, "normal window is drawable");
        Near(panel.Radius, 12, "window radius retained");
        Near(panel.BorderWidth, 1, "window border retained");
        Check(panel.HasFill, "normal window retains inner fill");

        foreach (float side in new[] { 0.001f, 1f, 6f, 18f, 25f, 560f })
        foreach (float requestedRadius in new[] { -3f, 0f, 3f, 6f, 12f, 1000f })
        foreach (float border in new[] { -1f, 0f, 1f, 1000f })
        {
            var shape = PanelShapeGeometry.Create(-7, 13, side * 2, side, requestedRadius, border);
            Check(shape.IsValid, "positive rectangle is drawable");
            Check(shape.Radius >= 0 && shape.Radius <= side / 2, "radius fits short side");
            Check(shape.BorderWidth >= 0 && shape.BorderWidth <= side / 2, "border fits short side");
            Check(shape.HasFill == (shape.BorderWidth < side / 2), "consumed border removes fill");
            ValidateContour(shape, false);
            ValidateContour(shape, true);
        }

        foreach (float dimension in new[] { 0f, -1f, float.NaN, float.PositiveInfinity, float.NegativeInfinity })
        {
            Check(!PanelShapeGeometry.Create(0, 0, dimension, 10, 6, 1).IsValid, "invalid width is empty");
            Check(!PanelShapeGeometry.Create(0, 0, 10, dimension, 6, 1).IsValid, "invalid height is empty");
        }
        Check(!PanelShapeGeometry.Create(float.NaN, 0, 10, 10, 6, 1).IsValid, "invalid origin is empty");
        Check(!PanelShapeGeometry.Create(float.MaxValue, 0, float.MaxValue, 10, 6, 1).IsValid,
            "overflowing right bound is empty");

        var invalidStyle = PanelShapeGeometry.Create(0, 0, 20, 10, float.NaN, float.PositiveInfinity);
        Check(invalidStyle.IsValid && invalidStyle.Radius == 0 && invalidStyle.BorderWidth == 0,
            "non-finite style values safely use square fill");

        var square = PanelShapeGeometry.Create(-10, -5, 20, 10, 0, 0);
        square.GetPoint(0, false, out float x, out float y);
        Near(x, 10, "square right extent"); Near(y, 5, "square top extent");
        var round = PanelShapeGeometry.Create(-10, -5, 20, 10, 100, 1);
        round.GetPoint(0, false, out x, out y);
        Near(x, 10, "rounded right extent"); Near(y, 0, "radius clamps to half short side");
        round.GetPoint(PanelShapeGeometry.CornerSegments, false, out x, out y);
        Near(x, 5, "rounded top tangent"); Near(y, 5, "rounded top extent");

        // Warm the static corner samples before measuring the hot rebuild calculation.
        float sum = 0;
        for (int i = 0; i < 1000; i++) sum += Sample(i);
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 10000; i++) sum += Sample(i);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Check(allocated == 0, "geometry rebuild calculation allocates no managed objects");
        GC.KeepAlive(sum);
        Console.WriteLine($"PASS: {checks} panel shape checks; 0 bytes across 10,000 geometry samples");
    }

    private static float Sample(int index)
    {
        var shape = PanelShapeGeometry.Create(-470, -280, 940, 560, 12, 1);
        shape.GetPoint(index % PanelShapeGeometry.PointCount, false, out float x, out float y);
        return x + y;
    }

    private static void ValidateContour(PanelShapeGeometry shape, bool inner)
    {
        float inset = inner ? shape.BorderWidth : 0;
        float left = shape.X + inset, right = shape.X + shape.Width - inset;
        float bottom = shape.Y + inset, top = shape.Y + shape.Height - inset;
        double area = 0;
        for (int i = 0; i < PanelShapeGeometry.PointCount; i++)
        {
            shape.GetPoint(i, inner, out float x, out float y);
            shape.GetPoint((i + 1) % PanelShapeGeometry.PointCount, inner, out float nextX, out float nextY);
            Check(float.IsFinite(x) && float.IsFinite(y), "finite contour point");
            Check(x >= left - 0.0001f && x <= right + 0.0001f && y >= bottom - 0.0001f && y <= top + 0.0001f,
                "contour remains inside intended rectangle");
            // A convex outline is necessary for the center fan and border strip.
            shape.GetPoint((i + 2) % PanelShapeGeometry.PointCount, inner, out float afterX, out float afterY);
            double cross = (nextX - x) * (afterY - nextY) - (nextY - y) * (afterX - nextX);
            Check(cross >= -0.0001, "contour stays convex and counterclockwise");
            area += (double)x * nextY - (double)y * nextX;
        }
        Check(area >= -0.0001, "outline has nonnegative signed area");
        if (!inner || shape.HasFill) Check(area > 0, "drawable contour has positive area");
    }
}
