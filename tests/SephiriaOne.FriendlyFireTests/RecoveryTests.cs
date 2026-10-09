using SephiriaOne;

internal static class RecoveryTests
{
    internal static void Run(Action<bool, string> check)
    {
        SessionSettings.FriendlyFireForHit = new(true, 100);
        var killer = new PlayerAvatar(); var victim = new PlayerAvatar(); var spawner = new PlayerSpawner(); spawner.BindDeath(victim);
        victim.ApplyDamage(new() { origin = killer, damage = 200 });
        check(victim.IsDead && spawner.GameOvers == 0, "Final friendly-fire death leaves the session recoverable instead of settling game over");
        victim.Revive(100); victim.ApplyDamage(new() { origin = new UnitAvatar { faction = "enemy" }, damage = 200, targetFactionLayers = -1 });
        check(spawner.GameOvers == 1, "Enemy-caused party wipe preserves native game over");
        victim.Revive(100); victim.Die(5, new() { isSystemDamage = true });
        check(spawner.GameOvers == 2, "Environmental death still settles native game over");
        victim.Revive(100); SessionSettings.FriendlyFireForHit = default; victim.Die(5, new() { origin = killer });
        check(spawner.GameOvers == 3, "Off and unscoped deaths retain native game-over handling");
        victim.Revive(100); SessionSettings.FriendlyFireForHit = new(true, 100); ReviveAllFeature.Available = false;
        victim.ApplyDamage(new() { origin = killer, damage = 200 });
        check(spawner.GameOvers == 4, "Unavailable recovery preserves native game over instead of stranding the party");
        ReviveAllFeature.Available = true;
    }
}
