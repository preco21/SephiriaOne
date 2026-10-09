using SephiriaOne;

internal static class KillLogTests
{
    internal static void Run(Action<bool, string> check)
    {
        SessionSettings.FriendlyFireForHit = new(true, 100);
        var killer = new PlayerAvatar { Name = "<color=blue>Alice</color>" };
        var victim = new PlayerAvatar { Name = "Bob" };
        victim.OnDeath = _ => { killer.Name = ""; victim.Name = ""; };
        int before = DungeonManager.Instance.Messages.Count;
        victim.ApplyDamage(new() { origin = killer, damage = 200 });
        check(DungeonManager.Instance.Messages[before] == "Friendly fire: Alice killed Bob.",
            "Death callbacks cannot erase snapshotted killer/victim names");
        killer.Name = "Alice";
        foreach (string missing in new[] { "", "?", " \n\t", "<color=red></color>" })
        {
            victim = new PlayerAvatar { Name = missing, netId = 42 };
            victim.spawner = new PlayerSpawner { PlayerAvatar = victim, currentPlayerIdx = 2 };
            victim.ApplyDamage(new() { origin = killer, damage = 200 });
            check(DungeonManager.Instance.Messages.Last() == "Friendly fire: Alice killed Player #3.",
                "Missing player name has an identifiable player-slot fallback: " + missing);
        }
        victim = new PlayerAvatar { Name = "", netId = 42 };
        victim.ApplyDamage(new() { origin = killer, damage = 200 });
        check(DungeonManager.Instance.Messages.Last().Contains("Player #42"), "Uninitialized player slot falls back to network identity");
        var companion = new UnitAvatar { Name = "?", netId = 7, NetworkLeader = killer };
        companion.ApplyDamage(new() { origin = killer, damage = 200 });
        check(DungeonManager.Instance.Messages.Last().Contains("Companion of Alice"),
            "Unnamed companion retains owner attribution before native death clears its leader");
        var ally = new UnitAvatar { Name = "?", netId = 9, faction = "friends" };
        ally.ApplyDamage(new() { origin = killer, damage = 200 });
        check(DungeonManager.Instance.Messages.Last().Contains("Unnamed ally #9"), "Unnamed NPC is not misrepresented as a player");
        check(FriendlyFireRuntime.SafeName("   Alice \n") == "Alice", "Names trim whitespace after sanitization");
        string folder = Path.Combine(Path.GetTempPath(), "SephiriaOne-CombatLanguage-" + Guid.NewGuid().ToString("N"));
        L.Initialize(folder, message => throw new Exception(message));
        try
        {
            foreach (string language in new[] { "en", "ko" })
            {
                if (!L.TrySetLanguage(language, out string error)) throw new Exception(error);
                victim = new PlayerAvatar { Name = "", netId = 42 };
                victim.ApplyDamage(new() { origin = killer, damage = 200 });
                check(DungeonManager.Instance.Messages.Last().Contains(language == "ko" ? "플레이어 #42" : "Player #42"),
                    "Fallback player identity follows host language: " + language);
            }
        }
        finally { L.Shutdown(); }
    }
}
