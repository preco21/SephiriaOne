using SephiriaOne;

internal static class NameStyleDirectoryTests
{
    internal static int Run()
    {
        int checks = 0;
        void Check(bool value, string scenario) { if (!value) throw new Exception(scenario); checks++; }
        var styles = new NameStyleDirectory();
        Check(!styles.UseGradient(7), "Unknown platform identity has no remote style");
        styles.Observe(7, "Hero", true);
        Check(styles.UseGradient(7), "Owned player styles its platform identity before name acknowledgment");
        styles.Observe(8, NameGradient.Format("Other Hero"), false);
        Check(styles.UseGradient(8), "Canonical native style reaches matching remote Steam identity");
        styles.Observe(9, "Hero", false);
        Check(!styles.UseGradient(9), "Matching nicknames never copy another player's style");
        styles.Observe(0, NameGradient.Format("Unknown"), false);
        Check(!styles.UseGradient(0), "Uninitialized zero identity is never matched");
        styles.Observe(8, NameGradient.Format("Duplicate"), false);
        Check(!styles.UseGradient(8), "Ambiguous duplicate identity does not select a remote player");
        styles.Clear();
        Check(!styles.UseGradient(7) && !styles.UseGradient(8), "Departure clears old style mapping");
        styles.Observe(8, "Replacement", false);
        Check(!styles.UseGradient(8), "Replacement avatar does not inherit departed player's style");
        styles.Clear();
        styles.Observe(8, "<color=#FF0000>Red</color>", false);
        Check(!styles.UseGradient(8), "Unrelated native markup does not advertise our style");
        styles.Clear();
        styles.Observe(8, null, false);
        Check(!styles.UseGradient(8), "Uninitialized name waits without faulting");
        return checks;
    }
}
