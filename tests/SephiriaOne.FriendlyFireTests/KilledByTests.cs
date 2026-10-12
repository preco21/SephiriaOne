using SephiriaOne;

internal static class KilledByTests
{
    internal static void Run(Action<bool, string> check)
    {
        PlayerAvatar a = null, b = null, c = null;
        void Reset()
        {
            FriendlyFireRuntime.Clear(); FriendlyFireParticipants.Clear();
            SessionSettings.FriendlyFireForHit = new(true, 100);
            NativePlayerChat.Messages.Clear(); NativePlayerChat.Fail = false;
            a = new() { Name = "A" }; b = new() { Name = "B" }; c = new() { Name = "C" };
        }
        void Kill(PlayerAvatar killer, PlayerAvatar victim)
        { victim.Revive(100); victim.ApplyDamage(new() { origin = killer, damage = 200 }); }
        string Last() => NativePlayerChat.Messages.Last().Message;
        Reset(); Kill(a, b);
        check(NativePlayerChat.Messages.Count == 1 && NativePlayerChat.Messages[0].Player == b &&
            Last() == "Killed by ‘A’ (Matchup: Me 0–1 Opponent)", "Only the victim sees the first confirmed kill from their own perspective");
        b.Die(5, new() { origin = a });
        check(NativePlayerChat.Messages.Count == 1, "Repeated Die does not duplicate message or matchup");
        for (int i = 1; i < 3; i++) Kill(a, b);
        for (int i = 0; i < 4; i++) Kill(b, a);
        check(Last() == "Killed by ‘B’ (Matchup: Me 3–4 Opponent)", "Alternating kills accumulate in victim-first order");
        Kill(c, a);
        check(Last() == "Killed by ‘C’ (Matchup: Me 0–1 Opponent)", "Different opponent has an independent matchup");
        FriendlyFireKda.ResetScores(); Kill(b, a);
        check(Last() == "Killed by ‘B’ (Matchup: Me 0–1 Opponent)", "KDA reset clears both matchup directions");
        FriendlyFireKda.ClearLives(); Kill(b, a);
        check(Last() == "Killed by ‘B’ (Matchup: Me 0–2 Opponent)", "Life/round invalidation retains matchup totals");
        SessionSettings.FriendlyFireForHit = default; SessionSettings.FriendlyFireForHit = new(true, 100); Kill(b, a);
        check(Last() == "Killed by ‘B’ (Matchup: Me 0–1 Opponent)", "Friendly-fire toggle reset clears matchup totals");
        Reset(); a.Name = "<color=red>A</color>\n"; Kill(a, b);
        check(Last().StartsWith("Killed by ‘A’"), "Killer nickname is sanitized before formatting");
        Reset(); b.OnDeath = damage => { a.Name = ""; damage.origin = c; }; Kill(a, b);
        check(Last() == "Killed by ‘A’ (Matchup: Me 0–1 Opponent)", "Death callbacks cannot replace the captured killer name or damage source");
        Reset(); a.Name = c.Name = "Same"; Kill(a, b); Kill(c, b);
        check(Last() == "Killed by ‘Same’ (Matchup: Me 0–1 Opponent)", "Duplicate nicknames never merge opponent records");
        Reset(); b.ApplyDamage(new() { origin = c, damage = 10 });
        b.ApplyDamage(new() { origin = a, damage = 200 });
        check(FriendlyFireKda.For(c).Assists == 1 && FriendlyFireKda.KillsAgainst(FriendlyFireKda.For(c), FriendlyFireKda.For(b)) == 0,
            "Assists do not increment head-to-head kills");
        Reset(); var companion = new UnitAvatar { NetworkLeader = a };
        b.ApplyDamage(new() { origin = companion, damage = 200 });
        check(Last() == "Killed by ‘A’ (Matchup: Me 0–1 Opponent)", "Companion lethal hit identifies the owning player");
        Reset(); b.IsParrying = true;
        b.OnParry = _ => a.ApplyDamage(new() { origin = b, damage = 200, id = "Weapon_Reflect", fromType = EDamageFromType.None });
        b.ApplyDamage(new() { origin = a, damage = 10 });
        check(Last() == "Killed by ‘B’ (Matchup: Me 0–1 Opponent)", "Lethal reflection uses the same victim-first notice");
        foreach (string missing in new[] { "", " ", "?", "<color=red></color>" })
        {
            Reset(); a.Name = missing; Kill(a, b);
            check(NativePlayerChat.Messages.Count == 0 && FriendlyFireKda.For(a).Kills == 1,
                "Unknown name omits private notice without losing confirmed scores: " + missing);
        }
        foreach (string transition in new[] { "off", "reset", "selection", "scope", "leave-killer", "leave-victim", "revive", "error" })
        {
            Reset(); b.OnDeath = _ =>
            {
                switch (transition)
                {
                    case "off": SessionSettings.FriendlyFireForHit = default; break;
                    case "reset": FriendlyFireKda.ResetScores(); break;
                    case "selection": FriendlyFireParticipants.Select(c); break;
                    case "scope": FriendlyFireParticipants.Clear(); break;
                    case "leave-killer": FriendlyFireParticipants.Depart(a); break;
                    case "leave-victim": FriendlyFireParticipants.Depart(b); break;
                    case "revive": b.Revive(100); break;
                    case "error": throw new InvalidOperationException("native death failed");
                }
            };
            try { Kill(a, b); } catch (InvalidOperationException) { }
            check(NativePlayerChat.Messages.Count == 0, "No private stale or unconfirmed notice after " + transition);
        }
        Reset(); b.Die(5, new() { origin = b });
        c.Die(5, new() { origin = new UnitAvatar { faction = "enemy" } });
        check(NativePlayerChat.Messages.Count == 0, "Self/environment/native-enemy deaths have no killed-by notice");
        Reset(); b.ExtraLife = true; Kill(a, b);
        check(NativePlayerChat.Messages.Count == 0, "Extra-life save has no killed-by notice");
        Reset(); NativePlayerChat.Fail = true; Kill(a, b);
        check(FriendlyFireKda.For(a).Kills == 1 && DungeonManager.Instance.Messages.Last().Contains("A(1/0/0)"),
            "Private transport failure cannot cancel scores or the public kill log");
        Reset();
    }
}

namespace SephiriaOne
{
    internal static class NativePlayerChat
    {
        internal static readonly List<(PlayerAvatar Player, string Message)> Messages = new();
        internal static bool Fail;
        // Real native Unicode normalization is exercised against the installed
        // game by GameNativePlayerChatTests; fixtures model ordinary names here.
        internal static string ReadableName(string name) => CombatNames.Safe(name) is var plain && plain != "?" ? plain : null;
        internal static void Show(PlayerAvatar player, string message)
        { if (Fail) throw new TypeLoadException("optional notice unavailable"); Messages.Add((player, message)); }
        internal static void Reset() { }
    }
}
