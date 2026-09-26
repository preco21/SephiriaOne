using Mirror;
using SephiriaOne;

internal static class RabbitRuntimeTests
{
    public static void Run(Func<PlayerSpawner> start, Func<uint, int, int, int, PlayerSpawner> add, Action<bool, string> check)
    {
        start(); RabbitPotionFeature.Available = true;
        bool Command(string text) => SettingsActions.Execute("/one rabbit " + text).Success;
        check(!SessionSettings.RabbitPotionsForDisplay.HasChanges, "Display does not initialize a new rabbit scope");
        int notifications = 0;
        bool stableCommit = false;
        Action listener = () =>
        {
            notifications++;
            if (SessionSettings.RabbitPotionsForDisplay.HasChanges)
                stableCommit = SessionSettings.ReadSnapshot().CanSave;
        };
        Action badListener = () => throw new Exception("broken UI subscriber");
        SessionSettings.SettingsChanged += badListener;
        SessionSettings.SettingsChanged += listener;
        try
        {
            check(Command("infinite on"), "Host enables infinite before synchronization frame");
            check(SessionSettings.RabbitPotionsForUse.Infinite && !SessionSettings.RabbitPotionsForUse.Share, "Independent live policy");
            check(notifications > 0, "One failing UI listener does not prevent others or fail a committed command");
            check(stableCommit, "Read-only views observe a completed command, not a processing transaction");
            long revision = SessionSettings.ReadSnapshot().Revision;
            int previous = notifications;
            check(Command("status") && notifications == previous && SessionSettings.ReadSnapshot().Revision == revision, "Rabbit status is read-only");
            check(Command("share on"), "Host enables sharing");
            var snapshot = SessionSettings.ReadSnapshot();
            check(snapshot.RabbitPotions.Infinite && snapshot.RabbitPotions.Share && snapshot.RabbitPotionsAvailable, "Panel snapshot exposes both flags");
            check(Command("mp-cost on") && Command("suppress-survival on"), "Host enables both balance options");
            snapshot = SessionSettings.ReadSnapshot();
            check(snapshot.RabbitPotions.ConsumeMp && snapshot.RabbitPotions.SuppressSurvival &&
                SessionSettings.RabbitPotionsForUse.ConsumeMp && SessionSettings.RabbitPotionsForUse.SuppressSurvival,
                "Snapshot and live policy expose independent balance flags");
            previous = notifications;
            check(Command("mp-cost 25") && notifications > previous && SessionSettings.ReadSnapshot().RabbitPotions.MpCostPerDrink == 25 &&
                SessionSettings.DescribeRabbit(SessionSettings.RabbitPotionsForUse).Contains("25 MP"), "Custom fee commits and refreshes status through shared dispatch");
            revision = SessionSettings.ReadSnapshot().Revision;
            previous = notifications;
            check(!Command("mp-cost -1") && !Command("mp-cost 10001") &&
                SessionSettings.ReadSnapshot().Revision == revision && notifications == previous && SessionSettings.RabbitPotionsForUse.MpCostPerDrink == 25,
                "Invalid fee leaves policy and revision untouched");
            check(Command("mp-cost off") && !SessionSettings.RabbitPotionsForUse.ConsumeMp &&
                SessionSettings.RabbitPotionsForUse.MpCostPerDrink == 25 && Command("mp-cost on"), "Off/on preserves chosen fee");
            var guest = add(2, 4, 2, 0);
            SessionSettings.Synchronize();
            PlayerSpawner.MultiplayerList.Remove(guest);
            SessionSettings.Synchronize();
            check(Command("mp-cost 35"), "Fee can change while guest is away");
            var rejoined = add(2, 8, 3, 1);
            SessionSettings.Synchronize();
            check(SessionSettings.RabbitPotionsForUse.Infinite && SessionSettings.RabbitPotionsForUse.Share &&
                SessionSettings.RabbitPotionsForUse.ConsumeMp && SessionSettings.RabbitPotionsForUse.SuppressSurvival &&
                SessionSettings.RabbitPotionsForUse.MpCostPerDrink == 35 &&
                rejoined.PlayerAvatar.customStats.Count == 2, "Rejoining uses current custom fee without adding player markers or healing state");
            check(SettingsActions.Execute("/one save").Success, "Rabbit flags save with common preset action");
            check(Command("reset") && !SessionSettings.RabbitPotionsForUse.HasChanges && SessionSettings.RabbitPotionsForUse.MpCostPerDrink == 10,
                "Reset clears active toggles and custom fee");
            DungeonManager.Instance = new DungeonManager();
            check(!SessionSettings.RabbitPotionsForDisplay.HasChanges, "Display never reuses old dungeon intent");
            check(SessionSettings.RabbitPotionsForUse.Infinite && SessionSettings.RabbitPotionsForUse.Share &&
                SessionSettings.RabbitPotionsForUse.ConsumeMp && SessionSettings.RabbitPotionsForUse.SuppressSurvival &&
                SessionSettings.RabbitPotionsForUse.MpCostPerDrink == 35, "First use in new session loads saved flags and cost");
            RabbitPotionFeature.Available = false;
            check(!Command("mp-cost 50") && !Command("mp-cost 0") && SessionSettings.RabbitPotionsForUse.MpCostPerDrink == 35,
                "Compatibility blocks numeric changes, including zero");
            check(!Command("infinite on") && Command("infinite off") && !SessionSettings.RabbitPotionsForUse.Infinite && SessionSettings.RabbitPotionsForUse.Share,
                "Compatibility blocks enabling but not independent disabling");
            check(Command("reset") && !SessionSettings.RabbitPotionsForUse.HasChanges, "Reset stays available when hook unavailable");
            check(!Command("mp-cost on") && !Command("suppress-survival on") &&
                Command("mp-cost off") && Command("suppress-survival off"),
                "Compatibility blocks balance enabling but keeps disabling available");
            RabbitPotionFeature.Available = true;
            check(Command("share on"), "Reenable after compatible hooks return");
            NetworkServer.active = false;
            check(!Command("mp-cost 25"), "Guest cannot change numeric Rabbit fee");
            check(!Command("infinite on") && !Command("share off") && !SessionSettings.RabbitPotionsForDisplay.HasChanges && !SessionSettings.RabbitPotionsForUse.HasChanges,
                "Guest cannot change or consume host-only policy");
            NetworkServer.active = true;
            SessionSettings.Stop();
            check(!SessionSettings.RabbitPotionsForUse.HasChanges && !SessionSettings.RabbitPotionsForDisplay.HasChanges, "Unload clears both views");
        }
        finally
        {
            SessionSettings.SettingsChanged -= listener;
            SessionSettings.SettingsChanged -= badListener;
            RabbitPotionFeature.Available = true;
        }
    }
}
