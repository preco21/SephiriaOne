using System.Text.RegularExpressions;
using SephiriaOne;

internal static class MatchCountdownCadenceTests
{
    internal static void Run(Func<PlayerSpawner> start, Func<uint, PlayerSpawner> add, Action<bool, string> check)
    {
        void At(float time) { UnityEngine.Time.unscaledTime = time; CombatMatchRuntime.Tick(); }
        void Begin(string mode, int duration)
        {
            start(); add(2); add(3); At(0);
            NativePlayerFloatingText.Messages.Clear();
            check(SettingsActions.Execute($"/one {mode} duration {duration}").Success &&
                SettingsActions.Execute($"/one {mode} start").Success, "Start countdown cadence test: " + mode);
            At(3); At(3.01f);
        }
        int[] Notices(string mode)
        {
            string pattern = mode == "tag" ? @"^Bomb: (\d+)s\." : mode == "zombie" ? @"^\[Zombie Survival\] (\d+)s left" :
                @"^\[Tournament\] (?:Opponent:.*\| (\d+)s left|Match started:.*\((\d+) secs\))";
            return DungeonManager.Instance.ChatMessages.Select(m => Regex.Match(m.Message, pattern))
                .Where(m => m.Success).Select(m => int.Parse(m.Groups.Cast<Group>().Skip(1).First(g => g.Success).Value)).ToArray();
        }
        foreach (string mode in new[] { "zombie", "tag", "tournament" })
        {
            foreach (int duration in new[] { 17, 30 })
            {
                Begin(mode, duration);
                for (int remaining = duration; remaining > 0; remaining--)
                { At(3 + duration - remaining + .05f); At(3 + duration - remaining + .2f); }
                int[] expected = duration == 17 ? new[] { 17, 12, 7, 5, 4, 3, 2, 1 } : new[] { 30, 25, 20, 15, 10, 5, 4, 3, 2, 1 };
                check(Notices(mode).SequenceEqual(expected.SelectMany(n => Enumerable.Repeat(n, mode == "tournament" ? 2 : 1))),
                    mode + " emits initial, five-second, then final five one-second notices without duplicates: " + duration);
                if (mode == "tournament")
                    check(NativePlayerFloatingText.Messages.Count == duration * 2 &&
                        NativePlayerFloatingText.Messages.All(m => m.Message.StartsWith("[Tournament] Opponent:") && !m.Message.Contains("s left")),
                        "Two opponent-only labels refresh each second independently of timer chat");
                if (mode == "tag")
                    check(NativePlayerFloatingText.Messages.Count == duration && CombatRoleVisuals.Visible.Count == 1,
                        "Bomb floating timer and persistent chaser marker stay available between chat notices");
                if (mode == "zombie")
                    check(CombatRoleVisuals.Visible.Count == 1, "Zombie role marker persists between global timer notices");
                CombatMatchRuntime.Stop(true, false);
                int emitted = NativePlayerFloatingText.Messages.Count, chats = DungeonManager.Instance.ChatMessages.Count;
                At(100);
                check(NativePlayerFloatingText.Messages.Count == emitted && DungeonManager.Instance.ChatMessages.Count == chats,
                    "Stopped mode leaves no periodic timer or label work: " + mode);
            }
            Begin(mode, 30);
            foreach (float time in new[] { 9.2f, 9.3f, 12.2f, 15.2f, 28.2f, 30.2f, 30.3f }) At(time);
            check(Notices(mode).SequenceEqual(new[] { 30, 24, 18, 5, 3 }.SelectMany(n => Enumerable.Repeat(n, mode == "tournament" ? 2 : 1))),
                "Delayed frames emit only the current countdown; no missed-slot burst: " + mode);
            CombatMatchRuntime.Stop(true, false);
        }
        start();
    }
}
