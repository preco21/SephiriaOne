using System;
using SephiriaOne;

internal static class PanelBindingLifetimeTests
{
    internal static void Run()
    {
        var manager = new object();
        object launcher = null;
        var draft = new PanelDraft();
        draft.Observe(manager, 1, 1);
        draft.Edit("37");
        var geometry = new PanelWindowGeometry(800, 480);
        geometry.Fit(1000, 700);
        geometry.Move(40, 50);
        bool opened = true;
        int windowReleases = 0, launcherReleases = 0;
        bool ReleaseWindow()
        {
            windowReleases++;
            opened = false;
            draft.Clear();
            geometry.Center();
            return true;
        }
        void ReleaseLauncher() { launcherReleases++; }
        void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
        void RebindLauncher(object next)
        {
            Check(PanelBindingLifetime.TryRebind(manager, manager, launcher, next, ReleaseWindow, ReleaseLauncher), "Launcher rebind succeeds");
            launcher = next;
            Check(opened && draft.Text == "37" && geometry.X == 40 && geometry.Y == 50 && windowReleases == 0,
                "Optional launcher changes preserve open window, draft and position");
        }
        RebindLauncher(new object()); // Appearance after independent opening.
        RebindLauncher(null); // Removal.
        RebindLauncher(new object()); // Reappearance.
        RebindLauncher(new object()); // Replacement.
        Check(launcherReleases == 4, "Every changed launcher is released independently");
        Check(PanelBindingLifetime.TryRebind(manager, manager, launcher, launcher, ReleaseWindow, ReleaseLauncher) && launcherReleases == 4,
            "Unchanged bindings do not tear anything down");
        Check(PanelBindingLifetime.TryRebind(manager, new object(), launcher, new object(), ReleaseWindow, ReleaseLauncher) &&
            !opened && draft.Text == "" && windowReleases == 1 && launcherReleases == 4,
            "Manager change performs full teardown once");
        Check(!PanelBindingLifetime.TryRebind(manager, new object(), launcher, launcher, () => false, ReleaseLauncher),
            "Failed native window release prevents manager rebind");
        Console.WriteLine("Passed 12 optional-launcher/window lifetime checks.");
    }
}
