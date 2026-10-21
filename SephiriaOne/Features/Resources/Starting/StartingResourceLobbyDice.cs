using System;
using Mirror;

namespace SephiriaOne
{
    internal static partial class StartingResourceHooks
    {
        // A later town visit is not a new starting grant. Consult both live and
        // saved flags: loading can restore one before the other is populated.
        internal static bool CanEditLobbyDice => NetworkServer.active && DungeonManager.Instance &&
            !DungeonManager.Instance.isRunStarted && SaveManager.CurrentRun != null &&
            !SaveManager.CurrentRun.GetBool("RunStarted", false) && !IsInDungeon();

        internal static void AppendLobbyDice(StateWriteBatch batch, PlayerAvatar player, int target, int owned)
        {
            if (!CanEditLobbyDice) return; // Mid-run edits remain future-start intent.
            int baseline = NativeDice(player);
            int beforeMax = player.maxRerollDice, beforeBalance = player.rerollDice;
            player.customStats.TryGetValue(DiceMarker, out int beforeOwned);
            if (beforeBalance < 0 || target < 0 || (long)target - owned != baseline)
                throw new InvalidOperationException("Lobby dice inputs no longer match the planned native baseline.");
            // Preserve spending/earnings, including balances above maximum. Do
            // not clamp to zero: that would forgive spending and a later toggle
            // back up could mint dice. Unsafe decreases reject before any write.
            long balance = (long)beforeBalance + target - beforeMax;
            if (balance < 0 || balance > int.MaxValue)
                throw new InvalidOperationException(L.T("The dice decrease would reclaim dice already spent. Use a smaller decrease or apply it on a fresh run. Nobody was changed."));
            if (beforeMax == target && beforeOwned == owned) return;

            AvatarState state = State(player);
            if (state.DiceFailed || Pending(player, state, "Dice", DiceGuard))
                throw new InvalidOperationException("Starting dice have an unresolved native write.");
            var dungeon = DungeonManager.Instance;
            var spawner = player.spawner;
            var stats = player.customStats;
            string savedKey = Key(state, "Dice"), balanceKey = BalanceKey(state, "RerollDice");
            string saved = state.Run.GetString(savedKey, "");
            string checkpoint = "1|" + Encode(state.Guid) + "|" + owned.ToString(Culture);
            bool begun = false, finished = false;
            bool Current() => CanEditLobbyDice && ReferenceEquals(dungeon, DungeonManager.Instance) &&
                ReferenceEquals(state.Run, SaveManager.CurrentRun) && player && player.isServer && spawner &&
                ReferenceEquals(player.spawner, spawner) && ReferenceEquals(spawner.PlayerAvatar, player) &&
                ReferenceEquals(player.customStats, stats) && spawner.playerGuid == state.Guid &&
                "Player" + spawner.currentPlayerIdxForSave == state.NativePrefix;
            void Begin()
            {
                if (!Current()) throw new InvalidOperationException("Lobby dice identity or run changed during the write.");
                if (begun) return;
                BeginWrite(player, state, "Dice", DiceGuard, beforeMax, target);
                begun = true;
            }
            batch.Require(() => Current() && state.Run.GetString(savedKey, "") == saved &&
                !Pending(player, state, "Dice", DiceGuard));
            batch.Add("Lobby dice maximum", () => player.maxRerollDice,
                value => { Begin(); player.NetworkmaxRerollDice = value; }, target);
            batch.Add("Lobby dice balance", () => player.rerollDice,
                value => { Begin(); player.NetworkrerollDice = value; }, (int)balance);
            batch.Add<int?>("Lobby dice ownership", () => stats.TryGetValue(DiceMarker, out int value) ? value : (int?)null,
                value => { Begin(); if (value.HasValue) stats[DiceMarker] = value.Value; else stats.Remove(DiceMarker); },
                owned == 0 ? (int?)null : owned);
            batch.Add<int?>("Lobby dice grant token", () => stats.TryGetValue(DiceToken, out int value) ? value : (int?)null,
                value => { Begin(); stats[DiceToken] = value.Value; }, state.Token);
            batch.Add("Lobby dice checkpoint", () => state.Run.GetString(savedKey, ""),
                value => { Begin(); state.Run.SetString(savedKey, value); }, checkpoint);
            batch.Add<int?>("Lobby dice saved balance", () => state.Run.ContainsKey(balanceKey) ? state.Run.GetInt(balanceKey, 0) : (int?)null,
                value => { Begin(); state.Run.SetInt(balanceKey, value.Value); }, (int)balance);
            // Separate completion receipt permits the shared batch to recover
            // individual absolute writes without replaying a delta grant. The
            // durable pending marker remains until all native/checkpoint writes
            // finish, including across addon reload or connection replacement.
            batch.Add("Lobby dice completion", () => finished, value =>
            {
                Begin();
                if (player.maxRerollDice != target || player.rerollDice != (int)balance ||
                    (stats.TryGetValue(DiceMarker, out int contribution) ? contribution : 0) != owned ||
                    !Applied(player, DiceToken, state) ||
                    state.Run.GetString(savedKey, "") != checkpoint || state.Run.GetInt(balanceKey, -1) != (int)balance)
                    throw new InvalidOperationException("Lobby dice checkpoint readback failed.");
                EndWrite(player, state, "Dice", DiceGuard);
                finished = value;
            }, true);
            batch.RequireAfter(() => Current() && !Pending(player, state, "Dice", DiceGuard) && NativeDice(player) == baseline);
        }
    }
}
