using System;
using SephiriaOne;
static class Program
{
    static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }

    static void Main()
    {
        var geometry = new PanelWindowGeometry(800, 480);
        geometry.Fit(1000, 700);
        Check(geometry.Scale == 1, "Full size on roomy canvas");
        geometry.Move(900, -900);
        Check(geometry.X == 90 && geometry.Y == -100, "Drag stays inside margin");
        geometry.Fit(420, 260);
        Check(Math.Abs(geometry.Scale - 0.5f) < .001 && geometry.X == 0 && geometry.Y == 0, "Resize rescales and reclamps");
        geometry.Fit(1000, 700);
        geometry.Move(25, 40);
        geometry.Center();
        Check(geometry.X == 0 && geometry.Y == 0, "Center resets position");

        var toggle = new PanelToggleState();
        int actions = 0;
        toggle.Observe(true);
        Check(toggle.Value && actions == 0, "Observations never execute edits");
        Check(toggle.CanEdit(false, false, true), "Allowed disable survives unavailable enable");
        Check(!toggle.CanEdit(true, false, true), "Unavailable enable is rejected");
        toggle.Observe(false);
        Check(!toggle.CanEdit(true, true, false), "Lost authority rejects edits");
        toggle.Observe(true);
        toggle.UserEdit(false, false, true, value => { actions++; return false; });
        Check(toggle.Value && actions == 1, "Failed edits retain authoritative value");
        toggle.UserEdit(false, false, true, value => { actions++; return true; });
        Check(!toggle.Value && actions == 2, "Successful disable applies");

        PanelBindingLifetimeTests.Run();
        Console.WriteLine("Passed 10 window geometry and checkbox intent checks.");
    }
}
