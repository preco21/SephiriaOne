using SephiriaOne;
using HarmonyLib;

internal static class ZombieAnnouncementTests
{
    internal static void Run(Func<PlayerSpawner> start, Func<uint, PlayerSpawner> add, Action<bool, string> check)
    {
        PlayerAvatar[] New(int count = 4, int zombies = 1, bool longNames = false)
        {
            var players = new List<PlayerAvatar> { start().PlayerAvatar };
            for (uint i = 2; i <= count; i++) players.Add(add(i).PlayerAvatar);
            for (int i = 0; i < count; i++) players[i].playerNameSource = longNames ? new string((char)('A' + i), 32) : "Player" + i;
            UnityEngine.Time.unscaledTime = 0;
            check(SettingsActions.Execute("/one zombie count " + zombies).Success && SettingsActions.Execute("/one zombie start").Success, "Announcement match starts");
            DungeonManager.Instance.ChatMessages.Clear();
            UnityEngine.Time.unscaledTime = 3; CombatMatchRuntime.Tick();
            return players.ToArray();
        }
        void At(float time) { UnityEngine.Time.unscaledTime = time; CombatMatchRuntime.Tick(); }
        string[] Bubbles(PlayerAvatar player) => DungeonManager.Instance.ChatMessages.Where(m => m.Player == player).Select(m => m.Message).ToArray();
        var all = New();
        var zombie = all.Single(CombatMatchRuntime.IsActiveZombie);
        check(DungeonManager.Instance.ChatMessages.Any(m => m.Player == null && m.Message == "[Z] \"" + zombie.Name + "\" is a Zombie!"), "Start announces actual initial Zombie name with safe marker");
        check(Bubbles(zombie).Count(m => m.Contains("You are a Zombie")) == 1 &&
            all.Where(p => p != zombie).All(p => Bubbles(p).Length == 0), "Only initial Zombies receive a role bubble");
        check(CombatMatchRuntime.DescribeZombie().Contains("[H] Humans") && CombatMatchRuntime.DescribeZombie().Contains("[Z] Zombies"), "Status distinguishes both factions");
        foreach (var player in all)
            check(CombatMatchRuntime.ZombieFactionMarker(FriendlyFireParticipants.Capture(player)) == (player == zombie ? "[Z]" : "[H]"), "Participant UI marker follows current role");
        all = New(9, 8, true);
        var initial = all.Where(CombatMatchRuntime.IsActiveZombie).ToArray();
        var announcements = DungeonManager.Instance.ChatMessages.Where(m => m.Player == null && m.Message.StartsWith("[Z] Zombies:")).Select(m => m.Message).ToArray();
        check(announcements.Length > 1 && announcements.All(m => m.Length <= 120) &&
            initial.All(p => announcements.Count(m => m.Contains("\"" + p.Name + "\"")) == 1), "Long starting roster packs every full name exactly once into native-sized messages");
        check(all.Where(p => !CombatMatchRuntime.IsActiveZombie(p)).All(p => Bubbles(p).Length == 0), "Multi-Zombie opening leaves Human bubbles untouched");

        var harmony = new Harmony("Zombie.announcements.tests");
        ReviveAllHooks.Install();
        harmony.Patch(AccessTools.Method(typeof(UnitAvatar), "Die"), prefix: new HarmonyMethod(typeof(FriendlyFireRuntime), "BeforeDeath"),
            finalizer: new HarmonyMethod(typeof(FriendlyFireRuntime), "FinishDeath"));
        harmony.Patch(AccessTools.Method(typeof(UnitAvatar), "Revive"), prefix: new HarmonyMethod(typeof(FriendlyFireRuntime), "BeforeRevive"),
            postfix: new HarmonyMethod(typeof(FriendlyFireRuntime), "AfterRevive"), finalizer: new HarmonyMethod(typeof(FriendlyFireRuntime), "FinishRevive"));
        try
        {
            all = New(); zombie = all.Single(CombatMatchRuntime.IsActiveZombie);
            var human = all.First(p => p != zombie);
            string killerName = zombie.Name;
            DungeonManager.Instance.ChatMessages.Clear();
            human.DieCallback = _ => zombie.playerNameSource = "Renamed after death entry";
            var hit = new DamageInstance(); FriendlyFireRuntime.Attribute(zombie, human, hit); human.Die(0, hit);
            var bubbles = Bubbles(human);
            check(bubbles.Length == 1 && bubbles[0].Contains("\"" + killerName + "\" infected you!") && bubbles[0].Contains("Zombie in 1s") &&
                bubbles[0].Contains("hunt remaining [H] Humans"), "Conversion has one infector, actual timer and hunt instruction, using death-entry name: " + string.Join(" | ", bubbles));
            check(!bubbles.Any(m => m.StartsWith("Respawn in:")), "Converted Human never receives the generic respawn countdown");
            check(CombatMatchRuntime.ZombieFactionMarker(FriendlyFireParticipants.Capture(human)) == "[Z]", "Pending conversion already shows Zombie marker");
            At(3.5f); check(Bubbles(human).Length == 1, "Repeated ticks do not duplicate conversion notice");
            At(4); check(!human.IsDead && CombatMatchRuntime.IsActiveZombie(human) && Bubbles(human).Length == 1,
                "Successful conversion revival does not overwrite instruction with another role or generic bubble");
            human.DieCallback = null; human.Die(0, null);
            check(Bubbles(human).Last() == "Respawn in: 1s", "Later ordinary Zombie death does not retain old infector or conversion notice");
            At(5);
            var environmental = all.First(p => p != zombie && p != human);
            DungeonManager.Instance.ChatMessages.Clear();
            environmental.Die(0, null);
            check(Bubbles(environmental).SequenceEqual(new[] { "Respawn in: 1s" }), "Unconverted environmental Human death keeps native mode countdown");
            At(6);
            check(SettingsActions.Execute("/one zombie any-death on").Success, "Enable environmental conversion");
            DungeonManager.Instance.ChatMessages.Clear();
            environmental.Die(0, null);
            check(Bubbles(environmental).Length == 1 && Bubbles(environmental)[0].Contains("Infected!") &&
                Bubbles(environmental)[0].Contains("Zombie in 1s") && !Bubbles(environmental)[0].Contains("infected you"),
                "Opt-in environmental conversion combines countdown without inventing an infector");

            all = New(); zombie = all.Single(CombatMatchRuntime.IsActiveZombie); human = all.First(p => p != zombie);
            DungeonManager.Instance.ChatMessages.Clear();
            bool nestedTick = false;
            DungeonManager.Instance.ChatCallback = () =>
            {
                if (nestedTick || !DungeonManager.Instance.ChatMessages.Last().Message.StartsWith("Friendly fire:")) return;
                nestedTick = true; At(4);
            };
            hit = new DamageInstance(); FriendlyFireRuntime.Attribute(zombie, human, hit); human.Die(0, hit);
            DungeonManager.Instance.ChatCallback = null;
            check(nestedTick && human.IsDead && Bubbles(human).Length == 1 && Bubbles(human)[0].Contains("Zombie in 0s"),
                "Reentrant tick cannot revive or announce before death finalization; conversion uses actual remaining deadline");
            At(4.2f); check(!human.IsDead && Bubbles(human).Length == 1, "Expired conversion deadline recovers cleanly after native death completes");

            all = New(); zombie = all.Single(CombatMatchRuntime.IsActiveZombie); human = all.First(p => p != zombie);
            var oldReceipt = FriendlyFireParticipants.Capture(human);
            DungeonManager.Instance.ChatMessages.Clear();
            DungeonManager.Instance.ChatCallback = () =>
            {
                if (!DungeonManager.Instance.ChatMessages.Last().Message.Contains("infected you!")) return;
                DungeonManager.Instance.ChatCallback = null;
                SettingsActions.Execute("/one zombie stop");
            };
            hit = new DamageInstance(); FriendlyFireRuntime.Attribute(zombie, human, hit); human.Die(0, hit);
            check(!CombatMatchRuntime.IsRunning && !human.IsDead && Bubbles(human).Length == 1 &&
                CombatMatchRuntime.ZombieFactionMarker(oldReceipt) == "", "Stopping inside conversion chat cleans recovery and markers without an overlapping bubble");

            all = New(); CombatMatchRuntime.Stop(true, false);
            UnityEngine.Time.unscaledTime = 0;
            PlayerAvatar infectedDuringStart = null;
            DungeonManager.Instance.ChatMessages.Clear();
            DungeonManager.Instance.ChatCallback = () =>
            {
                if (!DungeonManager.Instance.ChatMessages.Last().Message.EndsWith("is a Zombie!")) return;
                DungeonManager.Instance.ChatCallback = null;
                var initialZombie = all.Single(CombatMatchRuntime.IsActiveZombie);
                infectedDuringStart = all.Last(p => p != initialZombie);
                var openingHit = new DamageInstance();
                FriendlyFireRuntime.Attribute(initialZombie, infectedDuringStart, openingHit);
                infectedDuringStart.Die(0, openingHit);
            };
            check(SettingsActions.Execute("/one zombie start").Success, "Opening callback match starts");
            At(3);
            check(infectedDuringStart != null && Bubbles(infectedDuringStart).Length == 1 && Bubbles(infectedDuringStart)[0].Contains("infected you!"),
                "Opening role notices never overwrite conversion of an initially Human player inside announcement callbacks");
        }
        finally { CombatMatchRuntime.Stop(true, false); harmony.UnpatchAll("Zombie.announcements.tests"); ReviveAllHooks.Uninstall(); start(); }
    }
}
