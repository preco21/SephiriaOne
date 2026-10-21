using System.Reflection;
using SephiriaOne;

internal static class LobbyDiceTests
{
    private static object Call(string name, params object[] args) => typeof(StartingResourceHooks)
        .GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, args);
    private static PlayerAvatar Fresh()
    {
        ((System.Collections.IDictionary)typeof(StartingResourceHooks)
            .GetField("states", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!).Clear();
        typeof(StartingResourceHooks).GetProperty("Available")!.SetValue(null, true);
        SaveManager.CurrentRun = new();
        SaveManager.CurrentRun.SetInt("SaveVersion", 1);
        DungeonManager.Instance = new();
        Mirror.NetworkServer.active = true;
        PlayerSpawner.MultiplayerList.Clear();
        ResourceRuntime.Policy.Clear();
        return Player();
    }
    private static PlayerAvatar Player()
    {
        var player = new PlayerAvatar();
        player.spawner = new() { PlayerAvatar = player };
        PlayerSpawner.MultiplayerList.Add(player.spawner);
        return player;
    }
    private static StateWriteBatch Plan(PlayerAvatar player, int target)
    {
        var batch = new StateWriteBatch(() => true);
        StartingResourceHooks.AppendLobbyDice(batch, player, target, target - StartingResourceHooks.NativeDice(player));
        return batch;
    }
    private static bool Apply(PlayerAvatar player, int target) => Plan(player, target).TryCommit(out _);

    internal static void Run(Action<bool, string> check)
    {
        var player = Fresh();
        ResourceRuntime.Policy[ResourceKind.Dice] = new(ResourceMode.Multiplier, 3);
        Call("InitializeDice", player, 3);
        check(player.rerollDice == 9 && player.maxRerollDice == 9, "Dice setting before lobby initialization grants once");
        player.rerollDice = 7;
        check(Apply(player, 9) && player.rerollDice == 7, "First ready-player inheritance does not duplicate initialization grant");
        check(Apply(player, 12) && player.rerollDice == 10 && player.maxRerollDice == 12,
            "Late lobby edit adjusts current and maximum dice by difference, preserving spending");
        check(SaveManager.CurrentRun.GetInt("Player0RerollDice") == 10 && StartingResourceHooks.NativeDice(player) == 3,
            "Late dice edit checkpoints current balance and retains native baseline");
        Call("InitializeDice", player, 3);
        check(player.rerollDice == 10, "Duplicate initialization cannot overwrite live lobby change");
        check(Apply(player, 3) && player.rerollDice == 1 && player.maxRerollDice == 3,
            "Lobby reset removes only owned dice, not native spending");
        player.rerollDice = 15;
        check(Apply(player, 6) && player.rerollDice == 18, "Dice earned above native maximum survive late setting changes");

        player = Fresh();
        Call("InitializeDice", player, 3);
        check(Apply(player, 9), "Reconnect scenario live grant accepted");
        player.rerollDice = 4;
        Call("BeforeDisconnect", new Mirror.NetworkConnectionToClient { identity = new Mirror.NetworkIdentity { Player = player } });
        PlayerSpawner.MultiplayerList.Remove(player.spawner);
        player = Player();
        Call("InitializeDice", player, 3);
        check(player.rerollDice == 4 && player.maxRerollDice == 9 && Apply(player, 9) && player.rerollDice == 4,
            "Fresh reconnect restores late allowance and spent balance without regrant");
        check(Apply(player, 12) && player.rerollDice == 7, "New policy while absent applies only difference after reconnect");

        player = Fresh();
        Call("InitializeDice", player, 3);
        Apply(player, 9);
        player.rerollDice = 0;
        bool rejected = false;
        try { Plan(player, 3); } catch (InvalidOperationException) { rejected = true; }
        check(rejected && player.maxRerollDice == 9 && player.rerollDice == 0,
            "Spent addon dice cannot be reclaimed by clamping then minted on toggle-back");
        check(Apply(player, 9) && player.rerollDice == 0, "Repeated setting preserves exact saved zero");

        foreach (string boundary in new[] { "live", "saved", "dungeon", "client" })
        {
            player = Fresh();
            Call("InitializeDice", player, 3);
            if (boundary == "live") DungeonManager.Instance.isRunStarted = true;
            if (boundary == "saved") SaveManager.CurrentRun.SetBool("RunStarted", true);
            if (boundary == "dungeon") DungeonManager.Instance.dungeonEnvironment["IsInDungeon"] = 1;
            if (boundary == "client") Mirror.NetworkServer.active = false;
            check(!StartingResourceHooks.CanEditLobbyDice && Apply(player, 9) && player.rerollDice == 3 && player.maxRerollDice == 3,
                "No lobby grant after " + boundary + " boundary, including later town visits");
        }

        player = Fresh();
        Call("InitializeDice", player, 3);
        var pending = Plan(player, 9);
        DungeonManager.Instance.isRunStarted = true;
        check(!pending.TryCommit(out _) && !pending.MayHaveWritten && player.rerollDice == 3,
            "Departure between planning and commit rejects before writes");

        foreach (string fault in new[] { "maximum", "balance" })
        {
            player = Fresh();
            Call("InitializeDice", player, 3);
            player.rerollDice = 1;
            pending = Plan(player, 9);
            if (fault == "maximum") player.OnMaxDiceWrite = _ => throw new Exception("after maximum write");
            else player.OnDiceWrite = _ => throw new Exception("after balance write");
            check(!pending.TryCommit(out _) && pending.MayHaveWritten &&
                SaveManager.CurrentRun.GetString("SephiriaOne.Starting.v1.Player0.DicePending") != "",
                "Partial " + fault + " write retains durable quarantine");
            player.OnMaxDiceWrite = player.OnDiceWrite = null;
            check(pending.TryRecover(out _) && player.rerollDice == 7 && player.maxRerollDice == 9 &&
                StartingResourceHooks.NativeDice(player) == 3, "Recover absolute " + fault + " writes without replaying delta");
            check(pending.TryRecover(out _) && player.rerollDice == 7, "Completed recovery cannot duplicate grant");
        }

        player = Fresh();
        Call("InitializeDice", player, 3);
        pending = Plan(player, 9);
        player.OnMaxDiceWrite = _ => DungeonManager.Instance.isRunStarted = true;
        check(!pending.TryCommit(out _) && player.rerollDice == 3 && !pending.TryRecover(out _),
            "Departure callback during a partial write cannot move the dice grant into a running game");

        player = Fresh();
        Call("InitializeDice", player, 3);
        Apply(player, 9);
        player.rerollDice = 5;
        StartingResourceHooks.Uninstall();
        check(player.maxRerollDice == 3 && player.rerollDice == 5 && SaveManager.CurrentRun.GetInt("Player0RerollDice") == 5,
            "Unload after live grant removes maximum ownership but preserves current saved balance");
    }
}
