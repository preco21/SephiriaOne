using System;
using System.Collections.Generic;
using SephiriaOne;

internal static class PanelNavigationTests
{
    internal static void Run()
    {
        void Check(bool value, string name) { if (!value) throw new Exception(name); }
        int[][] expected = { new[] { 0, 1, 2, 3, 8 }, new[] { 6, 11 }, new[] { 7, 10 },
            new[] { 9, 13, 14, 15, 16, 18, 19 }, new[] { 17 }, new[] { 20 }, new[] { 5, 4, 12 } };
        var seen = new HashSet<int>();
        var icons = new HashSet<int>();
        Check(PanelNavigation.Sections.Length == expected.Length, "All seven groups are available");
        for (int i = 0; i < expected.Length; i++)
        {
            var section = PanelNavigation.Sections[i];
            Check(icons.Add(section.Icon), "Sections have distinct icons");
            Check(section.Pages.Length == expected[i].Length, "Group size matches requested layout");
            for (int n = 0; n < section.Pages.Length; n++)
            {
                int page = section.Pages[n];
                Check(page == expected[i][n] && seen.Add(page), "Stable page IDs are grouped once in requested order");
                Check(PanelNavigation.Label(page).Length > 0, "Every destination has a label");
                Check(PanelNavigation.Secondary(page) == (i == 6), "Only System destinations have secondary styling");
            }
        }
        Check(seen.Count == PanelNavigation.PageCount, "Every existing page remains reachable");
        Check(PanelScrollMath.Step(.5f, 200, 300, 48, -1) == 1, "Short content stays at top");
        Check(PanelScrollMath.Step(.5f, 300, 300, 48, 1) == 1, "Exact fit does not divide by zero");
        Check(Math.Abs(PanelScrollMath.Step(1, 700, 300, 48, -1) - .88f) < .00001f, "Down arrow scrolls 48 pixels");
        Check(Math.Abs(PanelScrollMath.Step(.88f, 700, 300, 48, 1) - 1) < .00001f, "Up arrow reverses movement");
        Check(PanelScrollMath.Step(.98f, 700, 300, 48, 1) == 1, "Top is clamped");
        Check(PanelScrollMath.Step(.02f, 700, 300, 48, -1) == 0, "Bottom is clamped");
        Check(PanelScrollMath.Step(1, 301, 300, 48, -1) == 0, "Tiny overflow remains bounded");
        Check(PanelScrollMath.Reveal(.5f, 700, 300, 250, 26) == .5f, "Visible selection does not jump the rail");
        Check(PanelScrollMath.Reveal(0, 700, 300, 0, 26) == 1, "First destination is revealed from bottom");
        Check(PanelScrollMath.Reveal(1, 700, 300, 674, 26) == 0, "Last destination is revealed from top");
        Check(PanelScrollMath.Reveal(0, 100, 300, 30, 26) == 1, "Short selection content remains at top");
        Console.WriteLine("Verified all 21 grouped destinations and scrollbar arrow bounds.");
    }
}
