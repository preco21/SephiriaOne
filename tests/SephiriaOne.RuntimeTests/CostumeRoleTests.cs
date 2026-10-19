using Mirror;
using SephiriaOne;

// The real stock sanitizer/120-character contract is checked in installed-game tests.
public static class ChatTextSanitizer
{
    public static string Sanitize(string value, int length) => value.Length <= length ? value : value.Substring(0, length);
}

internal static class CostumeRoleTests
{
    internal static void Run(Func<PlayerSpawner> start, Func<uint, PlayerSpawner> add, Action<bool, string> check)
    {
        var host = start();
        var guest = add(2);
        host.PlayerAvatar.currentCostume = "HolyRabbit";
        guest.PlayerAvatar.currentCostume = "Mole";
        NativePlayerChat.Messages.Clear();
        SessionSettings.Synchronize();
        check(NativePlayerChat.Messages.Any(m => m.Player == host.PlayerAvatar && m.Message.Contains("Healer")), "Rabbit equip privately describes healer role");
        check(NativePlayerChat.Messages.Any(m => m.Player == guest.PlayerAvatar && m.Message.Contains("Companions")), "Guest Mole equip privately describes companion role");
        int count = NativePlayerChat.Messages.Count;
        for (int i = 0; i < 10; i++) SessionSettings.Synchronize();
        check(NativePlayerChat.Messages.Count == count, "Stable role descriptions do not resend");

        string Take(PlayerAvatar player)
        {
            SessionSettings.Synchronize();
            check(NativePlayerChat.Messages.All(m => m.Message.Length <= 120), "Every role line respects stock chat limit");
            string text = string.Join(" / ", NativePlayerChat.Messages.Where(m => m.Player == player).Select(m => m.Message));
            NativePlayerChat.Messages.Clear();
            return text;
        }
        void Command(string command)
        { check(SettingsActions.Execute(command).Success, "Role setting: " + command); NativePlayerChat.Messages.Clear(); }
        Take(host.PlayerAvatar);
        Command("/one rabbit infinite on");
        check(Take(host.PlayerAvatar).Contains("Unlimited HP potions"), "Enabled infinite HP potion shown");
        Command("/one rabbit mp-cost 37");
        check(Take(host.PlayerAvatar).Contains("37 MP"), "Live configured MP fee replaces default");
        Command("/one rabbit share on");
        check(Take(host.PlayerAvatar).Contains("Shared heal outside PvP: 5 tiles"), "Shared radius uses actual healing radius and discloses the PvP exclusion");
        Command("/one rabbit level-up-potion on");
        string rabbit = Take(host.PlayerAvatar);
        check(rabbit.Contains("random non-HP/MP potion") && rabbit.Contains("Sample excluded"), "Reward type and sample exclusions are clear");
        Command("/one rabbit mp-cost off");
        check(!Take(host.PlayerAvatar).Contains("37 MP"), "Disabled MP fee is not advertised");
        Command("/stats luck +1");
        check(Take(host.PlayerAvatar) == "", "Unrelated settings do not resend unchanged summaries");
        RabbitPotionFeature.Available = false;
        rabbit = Take(host.PlayerAvatar);
        check(rabbit.Contains("Level-up") && !rabbit.Contains("Unlimited") && !rabbit.Contains("Shared heal"), "Compatibility hides only unavailable mechanisms");
        RabbitLevelUpFeature.Available = false;
        check(Take(host.PlayerAvatar) == "Wing-Eared Rabbit · Healer", "No disabled bonuses in role-only summary");
        RabbitPotionFeature.Available = RabbitLevelUpFeature.Available = true; Take(host.PlayerAvatar);
        Command("/one rabbit reset");
        check(Take(host.PlayerAvatar) == "Wing-Eared Rabbit · Healer", "Reset publishes a fresh summary without obsolete benefits");

        Command("/stats companionattackspeed x2");
        check(Take(guest.PlayerAvatar).Contains("Companion attack speed ×2"), "Companion multiplier uses current setting");
        Command("/stats companionattackspeed x3");
        string companion = Take(guest.PlayerAvatar);
        check(companion.Contains("×3") && !companion.Contains("×2"), "Updated multiplier replaces old one");
        Command("/stats companiondefense +7");
        check(Take(guest.PlayerAvatar).Contains("Companion defense: +7 points"), "Additive bonuses retain native units");
        Command("/stats companioncritical set 12.25");
        check(Take(guest.PlayerAvatar).Contains("Companion critical chance: 12.25 %"), "Set bonuses retain fractional current values");
        guest.PlayerAvatar.calculatedBonusStats["FOLLOWERCRITICAL"] = 100;
        check(Take(guest.PlayerAvatar).Contains("13.25 %"), "Native stat change updates actual absolute value");
        string speedKey = "FOLLOWERATTACKSPEED", marker = "SEPHIRIAONE_STAT_" + speedKey;
        guest.PlayerAvatar.customStats[speedKey] = -200 + guest.PlayerAvatar.customStats[marker];
        companion = Take(guest.PlayerAvatar);
        check(!companion.Contains("Companion attack speed") && companion.Contains("+7 points"), "Native penalty fallback is never advertised as applied multiplier");
        guest.PlayerAvatar.customStats[speedKey] = 0;
        check(Take(guest.PlayerAvatar).Contains("×3"), "Restored native inputs re-advertise successfully applied bonus");
        Command("/stats companionattackspeed reset");
        check(!Take(guest.PlayerAvatar).Contains("Companion attack speed"), "Stat reset removes its clause");
        Command("/one collin on");
        check(Take(guest.PlayerAvatar) == "", "Future-only Collin intent does not claim an ungranted artifact");
        CollinFeature.Gifts.Add(guest.PlayerAvatar);
        check(Take(guest.PlayerAvatar).Contains("Starting artifact: Collin"), "Registered enabled Collin gift shown");
        Command("/one collin off");
        check(!Take(guest.PlayerAvatar).Contains("Collin"), "Disabled Collin is not advertised");

        guest.PlayerAvatar.currentCostume = "Squirrel";
        check(Take(guest.PlayerAvatar).Contains("Farmer Squirrel · Companions"), "Farmer Squirrel role uses same live modifiers");
        guest.PlayerAvatar.currentCostume = "Turtle";
        check(Take(guest.PlayerAvatar).Contains("Turtle · Companions"), "Turtle role uses same live modifiers");
        guest.PlayerAvatar.currentCostume = "PinkRabbit";
        check(Take(guest.PlayerAvatar) == "", "Ordinary costume does not receive an unrelated role");
        guest.PlayerAvatar.currentCostume = "Mole";
        check(Take(guest.PlayerAvatar).Contains("Mole"), "Switching back announces again");
        guest.connectionToClient.isReady = false; guest.PlayerAvatar.currentCostume = "Turtle";
        check(Take(guest.PlayerAvatar) == "", "Loading suppresses notices");
        guest.connectionToClient.isReady = true;
        check(Take(guest.PlayerAvatar).Contains("Turtle"), "Ready avatar receives current costume");
        guest.connectionToClient = new NetworkConnectionToClient();
        FriendlyFireParticipantFixtures.Connect(guest, 2);
        check(Take(guest.PlayerAvatar).Contains("Turtle"), "New connection with reused avatar receives full summary");
        PlayerSpawner.MultiplayerList.Remove(guest); SessionSettings.Synchronize();
        guest = add(2); guest.PlayerAvatar.currentCostume = "Mole";
        check(Take(guest.PlayerAvatar).Contains("+7 points"), "Rejoining same ID receives fully applied current settings");

        NativePlayerChat.Available = false;
        guest.PlayerAvatar.currentCostume = "Squirrel";
        check(Take(guest.PlayerAvatar) == "", "Failed dispatch is not marked sent");
        NativePlayerChat.Available = true; UnityEngine.Time.unscaledTime += 1.1f;
        check(Take(guest.PlayerAvatar).Contains("Farmer Squirrel"), "Failed transport retries current summary when ready");
        Command("/one save"); Take(host.PlayerAvatar);
        SessionSettings.Stop(); SessionSettings.Start();
        check(Take(guest.PlayerAvatar).Contains("+7 points"), "New session reloads preset and clears notice cache");
        NetworkServer.active = false;
        check(Take(guest.PlayerAvatar) == "", "Authority loss never sends notices");
        NetworkServer.active = true;

        string folder = Path.Combine(Path.GetTempPath(), "SephiriaOne-Role-" + Guid.NewGuid().ToString("N"));
        try
        {
            L.Initialize(folder, _ => { });
            check(Take(guest.PlayerAvatar).Contains("동료 특화"), "Korean companion role is localized");
            Command("/one rabbit mp-cost 23");
            check(Take(host.PlayerAvatar).Contains("MP 23 소모"), "Korean healer role uses configured fee");
            L.TrySetLanguage("en", out _);
            check(Take(host.PlayerAvatar).Contains("23 MP"), "Language change refreshes role without gameplay changes");
        }
        finally { L.Shutdown(); Directory.Delete(folder, true); }

        SessionSettings.Synchronize();
        var refresh = (Action<HostPlayer>)Delegate.CreateDelegate(typeof(Action<HostPlayer>), typeof(SessionSettings)
            .GetMethod("RefreshCostumeNotice", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static));
        var subject = new HostPlayer(guest);
        for (int i = 0; i < 100; i++) refresh(subject);
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 1000; i++) refresh(subject);
        long bytes = GC.GetAllocatedBytesForCurrentThread() - before;
        check(bytes == 0, "Stable companion role observation allocates no memory");
        Console.WriteLine($"Costume role observation: {bytes} bytes/1000 checks");
    }
}
