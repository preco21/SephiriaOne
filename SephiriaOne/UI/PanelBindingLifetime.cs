using System;

namespace SephiriaOne
{
    internal static class PanelBindingLifetime
    {
        internal static bool TryRebind(object manager, object nextManager, object launcher, object nextLauncher,
            Func<bool> releaseWindow, Action releaseLauncher)
        {
            if (!ReferenceEquals(manager, nextManager)) return releaseWindow();
            if (!ReferenceEquals(launcher, nextLauncher)) releaseLauncher();
            return true;
        }
    }
}
