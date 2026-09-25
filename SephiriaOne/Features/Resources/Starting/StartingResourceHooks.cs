using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using HarmonyLib;
using Mirror;

namespace SephiriaOne
{
    internal static class StartingResourceHooks
    {
        private const string Owner = "preco21.SephiriaOne.StartingResources";
        private const string Prefix = "SephiriaOne.Starting.v1.";
        private const string MoneyToken = "SEPHIRIAONE_START_MONEY_TOKEN";
        private const string DiceToken = "SEPHIRIAONE_START_DICE_TOKEN";
        private const string MoneyGuard = "SEPHIRIAONE_START_MONEY_PENDING";
        private const string DiceGuard = "SEPHIRIAONE_START_DICE_PENDING";
        private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;
        private static readonly Dictionary<PlayerAvatar, AvatarState> states = new Dictionary<PlayerAvatar, AvatarState>();
        private static Harmony harmony;
        public static bool Available { get; private set; }
        public static string Error { get; private set; }

        private sealed class AvatarState
        {
            public SaveData Run;
            public int Token;
            public string NativePrefix;
            public string Guid;
            public MoneyCheckpoint Money;
            public int? PendingDeparture;
            public bool MoneyFailed;
            public bool DiceFailed;
        }

        private sealed class MoneyCheckpoint
        {
            public bool Active;
            public ResourceSetting Setting;
            public int Seed;
            public int Granted;
            public bool Departed;
        }

        public static void Install()
        {
            if (harmony != null) return;
            var instance = new Harmony(Owner);
            try
            {
                MethodInfo initialize = AccessTools.DeclaredMethod(typeof(PlayerSpawner), "Initialize",
                    new[] { typeof(int), typeof(string), typeof(string), typeof(int) });
                MethodInfo departure = AccessTools.DeclaredMethod(typeof(DungeonManager), nameof(DungeonManager.LoadStageAndMove), new[] { typeof(string) });
                MethodInfo disconnect = AccessTools.DeclaredMethod(typeof(HorayNetworkManager), nameof(HorayNetworkManager.OnServerDisconnect), new[] { typeof(NetworkConnectionToClient) });
                if (initialize == null || initialize.ReturnType != typeof(bool) || departure == null ||
                    departure.ReturnType != typeof(void) || disconnect == null || disconnect.ReturnType != typeof(void))
                    throw new MissingMethodException("Native starting-resource boundaries changed.");
                instance.Patch(initialize, postfix: new HarmonyMethod(typeof(StartingResourceHooks), nameof(AfterInitialize)),
                    transpiler: new HarmonyMethod(typeof(StartingResourceHooks), nameof(InitializePatch)));
                instance.Patch(departure, transpiler: new HarmonyMethod(typeof(StartingResourceHooks), nameof(DeparturePatch)));
                instance.Patch(disconnect, prefix: new HarmonyMethod(typeof(StartingResourceHooks), nameof(BeforeDisconnect)));
                harmony = instance;
                Available = true;
                Error = null;
            }
            catch (Exception exception)
            {
                instance.UnpatchAll(Owner);
                Fail("Starting-resource hooks unavailable: " + exception.Message);
            }
        }

        public static void Uninstall()
        {
            if (harmony == null && !Available && states.Count == 0) return;
            bool cleanupFailed = false;
            if (NetworkServer.active && SaveManager.CurrentRun != null)
            {
                // Re-loading the addon can leave live native markers without
                // this assembly's old in-memory observations.
                foreach (PlayerSpawner spawner in PlayerSpawner.MultiplayerList)
                {
                    if (!spawner || !spawner.PlayerAvatar) continue;
                    PlayerAvatar player = spawner.PlayerAvatar;
                    try
                    {
                        AvatarState current = State(player);
                        current.Money = current.Money ?? ReadMoney(current);
                    }
                    catch (Exception exception)
                    {
                        cleanupFailed = true;
                        Fail("Starting-resource unload observation failed: " + exception.Message);
                    }
                }
            }
            // An unload is not a policy reset: return only native seed withheld
            // for a future departure that our hook will no longer intercept.
            foreach (var pair in states)
            {
                PlayerAvatar player = pair.Key;
                AvatarState state = pair.Value;
                if (!player || !player.isServer || !ReferenceEquals(state.Run, SaveManager.CurrentRun)) continue;
                if (state.MoneyFailed || state.DiceFailed) cleanupFailed = true;
                try
                {
                    if (!state.MoneyFailed && state.Money != null && !state.Money.Departed)
                    {
                        int refund = state.Money.Seed - state.Money.Granted;
                        BeginWrite(player, state, "Money", MoneyGuard, player.currentMoney, checked(player.currentMoney + refund));
                        GrantMoney(player, refund);
                        state.Money.Granted = state.Money.Seed;
                        state.Money.Active = false;
                        SaveMoney(state);
                        state.Run.SetInt(BalanceKey(state, "Money"), player.currentMoney);
                        EndWrite(player, state, "Money", MoneyGuard);
                    }
                    else if (!state.MoneyFailed && Applied(player, MoneyToken, state))
                        state.Run.SetInt(BalanceKey(state, "Money"), player.currentMoney);
                }
                catch (Exception exception)
                {
                    state.MoneyFailed = true;
                    cleanupFailed = true;
                    Fail("Starting-leaf unload could not finish; its grant will not be retried: " + exception.Message);
                }
                try
                {
                    bool ownsDice = player.customStats.TryGetValue(DiceMarker, out int owned) && owned != 0;
                    if (!state.DiceFailed && (ownsDice || Applied(player, DiceToken, state)))
                    {
                        int native = NativeDice(player);
                        BeginWrite(player, state, "Dice", DiceGuard, player.maxRerollDice, native);
                        player.NetworkmaxRerollDice = native;
                        player.customStats.Remove(DiceMarker);
                        if (player.maxRerollDice != native) throw new InvalidOperationException("Native initial-dice cleanup readback failed.");
                        state.Run.SetInt(BalanceKey(state, "RerollDice"), player.rerollDice);
                        state.Run.SetString(Key(state, "Dice"), "1|" + Encode(state.Guid) + "|0");
                        EndWrite(player, state, "Dice", DiceGuard);
                    }
                }
                catch (Exception exception)
                {
                    state.DiceFailed = true;
                    cleanupFailed = true;
                    Fail("Starting-dice unload could not finish; its write will not be retried: " + exception.Message);
                }
            }
            if (cleanupFailed)
                throw new InvalidOperationException("Starting-resource cleanup has an unresolved result; hooks remain installed to prevent a repeated grant.");
            harmony?.UnpatchAll(Owner);
            harmony = null;
            states.Clear();
            Available = false;
        }

        public static int NativeDice(PlayerAvatar player)
        {
            if (!player) throw new ArgumentNullException(nameof(player));
            if (player.customStats.TryGetValue(DiceGuard, out int pending) && pending != 0)
                throw new InvalidOperationException("A starting-dice write has an unresolved native result.");
            player.customStats.TryGetValue(DiceMarker, out int owned);
            return checked(player.maxRerollDice - owned);
        }

        public static int NativeLeaves(PlayerAvatar player)
        {
            if (!player) throw new ArgumentNullException(nameof(player));
            TreeShopItemStorage tree = player.GetComponent<TreeShopItemStorage>();
            if (!tree) throw new InvalidOperationException("Native starting-leaf storage is not ready.");
            return checked(tree.GettStartingMoney() + Math.Max(0, player.GetCustomStatUnsafe("STARTINGMONEY")));
        }

        private static string DiceMarker => ResourceCatalog.Get(ResourceKind.Dice).Marker;
        private static int MoneyMaximum => ResourceCatalog.Get(ResourceKind.Leaves).Maximum;

        private static IEnumerable<CodeInstruction> InitializePatch(IEnumerable<CodeInstruction> instructions)
            => StartingResourceTranspilers.Initialize(instructions,
                AccessTools.Method(typeof(StartingResourceHooks), nameof(InitializeMoney)),
                AccessTools.Method(typeof(StartingResourceHooks), nameof(InitializeDice)));

        private static IEnumerable<CodeInstruction> DeparturePatch(IEnumerable<CodeInstruction> instructions)
            => StartingResourceTranspilers.Departure(instructions,
                AccessTools.Method(typeof(StartingResourceHooks), nameof(PlanDepartureMoney)),
                AccessTools.Method(typeof(StartingResourceHooks), nameof(ApplyDepartureMoney)));

        private static AvatarState State(PlayerAvatar player)
        {
            SaveData run = SaveManager.CurrentRun ?? throw new InvalidOperationException("Native run save is unavailable.");
            int token = run.GetInt(Prefix + "Token", 0);
            if (token == 0)
            {
                token = Guid.NewGuid().GetHashCode() & int.MaxValue;
                if (token == 0) token = 1;
                run.SetInt(Prefix + "Token", token);
            }
            // CreateNewTMP starts without SaveVersion. Native Initialize reads
            // legacy fallback keys until the first native save writes version 2.
            // Our own checkpoints must always keep each player's stable slot.
            string nativePrefix = "Player" + player.spawner.currentPlayerIdxForSave;
            string guid = player.spawner.playerGuid ?? "";
            if (!states.TryGetValue(player, out AvatarState state) || !ReferenceEquals(state.Run, run) || state.Token != token)
            {
                state = new AvatarState { Run = run, Token = token, NativePrefix = nativePrefix, Guid = guid };
                state.MoneyFailed = Pending(player, state, "Money", MoneyGuard);
                state.DiceFailed = Pending(player, state, "Dice", DiceGuard);
                states[player] = state;
                if (state.MoneyFailed || state.DiceFailed)
                    Fail("Starting resources have an unresolved native write checkpoint; automatic grants are unavailable.");
            }
            if (state.NativePrefix != nativePrefix || state.Guid != guid)
                throw new InvalidOperationException("Player save identity changed during starting-resource initialization.");
            return state;
        }

        private static bool Applied(PlayerAvatar player, string marker, AvatarState state)
            => player.customStats.TryGetValue(marker, out int token) && token == state.Token;

        private static void InitializeMoney(UnitAvatar avatar, int nativeAmount)
        {
            if (!(avatar is PlayerAvatar player) || !NetworkServer.active || !player.isServer)
            { avatar.AddMoney(nativeAmount); return; }
            AvatarState state = null;
            bool wrote = false;
            try
            {
                state = State(player);
                if (Applied(player, MoneyToken, state)) return;
                string balanceKey = BalanceKey(state, "Money");
                bool restore = state.Run.ContainsKey(balanceKey);
                if (restore) nativeAmount = state.Run.GetInt(balanceKey, nativeAmount);
                MoneyCheckpoint saved = ReadMoney(state);
                state.Money = saved;
                if (state.MoneyFailed)
                {
                    if (restore)
                    {
                        wrote = true;
                        player.customStats[MoneyToken] = state.Token;
                        GrantMoney(player, nativeAmount);
                    }
                    return;
                }
                int grant = nativeAmount;
                MoneyCheckpoint checkpoint;
                if (restore)
                {
                    checkpoint = saved ?? new MoneyCheckpoint { Departed = IsInDungeon() };
                }
                else if (saved != null)
                {
                    // A checkpoint without the native balance cannot safely be
                    // replayed after a reload. Never invent a spent balance.
                    throw new InvalidOperationException("Starting leaves checkpoint has no saved balance.");
                }
                else
                {
                    ResourceSetting setting = default;
                    bool active = Available && ResourceRuntime.TryGetSetting(ResourceKind.Leaves, out setting);
                    // Capture the initial allocation. A later explicit host intent
                    // can still change its uncommitted first-departure remainder.
                    if (!active) setting = default;
                    checkpoint = new MoneyCheckpoint { Active = active, Setting = setting, Seed = nativeAmount, Granted = nativeAmount };
                    if (nativeAmount < 0) throw new InvalidOperationException("Native initial leaves are negative.");
                    if (IsInDungeon())
                    {
                        // Native newcomers already inside a dungeon get the
                        // tree seed only; they have no future departure bonus.
                        if (active && !setting.TryTarget(nativeAmount, 0, MoneyMaximum, out grant, out string error))
                        { ResourceRuntime.Report("Starting leaves use native allowance: " + error); grant = nativeAmount; checkpoint.Active = false; }
                        checkpoint.Departed = true;
                        // No outstanding allocation exists for single-phase joins.
                        checkpoint.Granted = nativeAmount;
                    }
                    else if (active && !StartingResourcePlan.TrySeed(nativeAmount, setting, MoneyMaximum, out grant, out string error))
                    { ResourceRuntime.Report("Starting leaves use native allowance: " + error); grant = nativeAmount; checkpoint.Active = false; }
                    if (!checkpoint.Departed) checkpoint.Granted = grant;
                }
                // A restored amount is native saved spendable balance, including
                // zero. Never run the configured target arithmetic over it.
                BeginWrite(player, state, "Money", MoneyGuard, player.currentMoney, checked(player.currentMoney + grant));
                wrote = true;
                player.customStats[MoneyToken] = state.Token;
                GrantMoney(player, grant);
                state.Money = checkpoint;
                state.Run.SetInt(balanceKey, player.currentMoney);
                SaveMoney(state);
                EndWrite(player, state, "Money", MoneyGuard);
            }
            catch (Exception exception)
            {
                if (state != null) state.MoneyFailed = true;
                Fail("Starting leaves initialization unavailable" + (wrote ? " after a possible native write" : "") + ": " + exception.Message);
                if (!wrote && state != null && !Applied(player, MoneyToken, state))
                {
                    // Native fallback is a terminal outcome too. A second call
                    // must not replay an old saved amount over later spending.
                    player.customStats[MoneyToken] = state.Token;
                    avatar.AddMoney(nativeAmount);
                }
            }
        }

        private static void InitializeDice(PlayerAvatar player, int nativeAmount)
        {
            if (!NetworkServer.active || !player.isServer) { player.NetworkrerollDice = nativeAmount; return; }
            AvatarState state = null;
            bool wrote = false;
            try
            {
                state = State(player);
                if (Applied(player, DiceToken, state)) return;
                string balanceKey = BalanceKey(state, "RerollDice");
                bool restore = state.Run.ContainsKey(balanceKey);
                if (restore) nativeAmount = state.Run.GetInt(balanceKey, nativeAmount);
                if (state.DiceFailed)
                {
                    if (restore)
                    {
                        wrote = true;
                        player.customStats[DiceToken] = state.Token;
                        player.NetworkrerollDice = nativeAmount;
                    }
                    return;
                }
                int baseline = NativeDice(player);
                int target = baseline;
                int contribution = 0;
                string saved = state.Run.GetString(Key(state, "Dice"), "");
                if (restore)
                {
                    if (!string.IsNullOrEmpty(saved))
                    {
                        string[] values = saved.Split('|');
                        if (values.Length != 3 || values[0] != "1" || Decode(values[1]) != state.Guid ||
                            !int.TryParse(values[2], NumberStyles.Integer, Culture, out contribution))
                            throw new InvalidOperationException("Invalid saved starting-dice checkpoint.");
                    }
                    target = checked(baseline + contribution);
                    if (target < 0) throw new InvalidOperationException("Saved starting-dice contribution is incompatible with the native baseline.");
                }
                else
                {
                    if (!string.IsNullOrEmpty(saved)) throw new InvalidOperationException("Starting dice checkpoint has no saved balance.");
                    if (Available && ResourceRuntime.TryGetSetting(ResourceKind.Dice, out ResourceSetting setting) &&
                        !setting.TryTarget(baseline, 0, ResourceCatalog.Get(ResourceKind.Dice).Maximum, out target, out string error))
                    { ResourceRuntime.Report("Starting dice use native allowance: " + error); target = baseline; }
                    contribution = checked(target - baseline);
                    nativeAmount = target;
                }
                BeginWrite(player, state, "Dice", DiceGuard, player.maxRerollDice, target);
                wrote = true;
                player.customStats[DiceToken] = state.Token;
                player.NetworkmaxRerollDice = target;
                player.customStats[DiceMarker] = contribution;
                player.NetworkrerollDice = nativeAmount;
                if (player.maxRerollDice != target || player.rerollDice != nativeAmount)
                    throw new InvalidOperationException("Native starting-dice readback failed.");
                state.Run.SetInt(balanceKey, player.rerollDice);
                state.Run.SetString(Key(state, "Dice"), "1|" + Encode(state.Guid) + "|" + contribution.ToString(Culture));
                EndWrite(player, state, "Dice", DiceGuard);
            }
            catch (Exception exception)
            {
                if (state != null) state.DiceFailed = true;
                Fail("Starting dice unavailable" + (wrote ? " after a possible native write" : "") + ": " + exception.Message);
                if (!wrote && state != null && !Applied(player, DiceToken, state))
                {
                    player.customStats[DiceToken] = state.Token;
                    player.NetworkrerollDice = nativeAmount;
                }
            }
        }

        private static int PlanDepartureMoney(UnitAvatar avatar, string id)
        {
            int nativeBonus = avatar.GetCustomStatUnsafe(id);
            if (!(avatar is PlayerAvatar player) || !NetworkServer.active || !player.isServer) return nativeBonus;
            AvatarState state = null;
            try
            {
                state = State(player);
                if (state.MoneyFailed) return 0;
                state.Money = state.Money ?? ReadMoney(state);
                // Without seed history, do not invent or replay an allocation.
                if (state.Money == null) return nativeBonus;
                if (state.Money.Departed) return 0;
                if (state.PendingDeparture.HasValue) throw new InvalidOperationException("Reentrant starting-leaf departure grant.");
                MoneyCheckpoint checkpoint = state.Money;
                // Lobby initialization happens before the player can edit settings.
                // Use current host intent (including reset) for the outstanding
                // grant; never recalculate the already paid seed or live wallet.
                // No session intent means a saved pending allocation remains valid.
                if (Available && ResourceRuntime.TryGetIntent(ResourceKind.Leaves, out ResourceSetting setting))
                {
                    checkpoint.Active = !setting.Empty;
                    checkpoint.Setting = setting;
                }
                if (!StartingResourcePlan.TryDeparture(checkpoint.Seed, checkpoint.Granted, Math.Max(0, nativeBonus),
                    Available && checkpoint.Active, checkpoint.Setting, MoneyMaximum,
                    out int grant, out bool fallback, out string error)) throw new InvalidOperationException(error);
                if (fallback) ResourceRuntime.Report("Starting leaves use native allowance: " + error);
                if (grant == 0)
                {
                    checkpoint.Departed = true;
                    SaveMoney(state);
                }
                else state.PendingDeparture = grant;
                return grant;
            }
            catch (Exception exception)
            {
                if (state != null) state.MoneyFailed = true;
                Fail("Starting leaves departure unavailable: " + exception.Message);
                return 0;
            }
        }

        private static void ApplyDepartureMoney(UnitAvatar avatar, int grant)
        {
            if (!(avatar is PlayerAvatar player) || !NetworkServer.active || !player.isServer)
            { avatar.AddMoney(grant); return; }
            AvatarState state = null;
            try
            {
                state = State(player);
                if (state.MoneyFailed) return;
                if (state.Money == null) { avatar.AddMoney(grant); return; }
                if (state.Money.Departed) return;
                if (!state.PendingDeparture.HasValue || state.PendingDeparture.Value != grant)
                    throw new InvalidOperationException("Starting-leaf departure plan changed before its native write.");
                BeginWrite(player, state, "Money", MoneyGuard, player.currentMoney, checked(player.currentMoney + grant));
                GrantMoney(player, grant);
                state.Money.Departed = true;
                state.PendingDeparture = null;
                state.Run.SetInt(BalanceKey(state, "Money"), player.currentMoney);
                SaveMoney(state);
                EndWrite(player, state, "Money", MoneyGuard);
            }
            catch (Exception exception)
            {
                if (state != null) state.MoneyFailed = true;
                Fail("Starting leaves departure unavailable after a possible native write: " + exception.Message);
            }
        }

        private static void GrantMoney(PlayerAvatar player, int grant)
        {
            int before = player.currentMoney;
            int target = checked(before + grant);
            player.NetworkcurrentMoney = target;
            if (player.currentMoney != target) throw new InvalidOperationException("Starting-leaf readback failed (before " + before + ", target " + target + ").");
        }

        private static void BeforeDisconnect(NetworkConnectionToClient conn)
        {
            if (!NetworkServer.active || conn == null || !conn.identity) return;
            PlayerAvatar player = conn.identity.GetComponent<PlayerAvatar>();
            if (!player || SaveManager.CurrentRun == null) return;
            try
            {
                AvatarState state = State(player);
                // Native only saves guests after RunStarted. Preserve their town
                // spending too, without saving profile data or unrelated state.
                if (!state.MoneyFailed && Applied(player, MoneyToken, state)) state.Run.SetInt(BalanceKey(state, "Money"), player.currentMoney);
                if (!state.DiceFailed && Applied(player, DiceToken, state)) state.Run.SetInt(BalanceKey(state, "RerollDice"), player.rerollDice);
            }
            catch (Exception exception) { Fail("Starting-resource disconnect checkpoint failed: " + exception.Message); }
            states.Remove(player);
        }

        private static void AfterInitialize(PlayerSpawner __instance, bool __result)
        {
            if (!__result && NetworkServer.active)
                Fail("Native player initialization did not complete; starting-resource grants will not be retried automatically.");
        }

        private static bool IsInDungeon()
            => DungeonManager.Instance && DungeonManager.Instance.dungeonEnvironment.TryGetValue("IsInDungeon", out int value) && value != 0;

        private static string Key(AvatarState state, string resource) => Prefix + state.NativePrefix + "." + resource;
        private static string BalanceKey(AvatarState state, string resource)
        {
            string legacy = "Player" + resource;
            // Existing legacy balances remain authoritative. Do not create
            // these shared keys in a fresh, not-yet-versioned multiplayer run.
            return state.Run.GetInt("SaveVersion", 0) == 0 && state.Run.ContainsKey(legacy)
                ? legacy : state.NativePrefix + resource;
        }
        private static string Encode(string value) => Convert.ToBase64String(Encoding.UTF8.GetBytes(value));
        private static string Decode(string value) => Encoding.UTF8.GetString(Convert.FromBase64String(value));

        private static bool Pending(PlayerAvatar player, AvatarState state, string resource, string marker)
            => (player.customStats.TryGetValue(marker, out int pending) && pending != 0) ||
                !string.IsNullOrEmpty(state.Run.GetString(Key(state, resource + "Pending"), ""));

        private static void BeginWrite(PlayerAvatar player, AvatarState state, string resource, string marker, int before, int target)
        {
            // Native run metadata and an avatar marker survive addon reloads.
            // An uncertain write is never converted into another fresh grant.
            state.Run.SetString(Key(state, resource + "Pending"), before.ToString(Culture) + "|" + target.ToString(Culture));
            player.customStats[marker] = 1;
        }

        private static void EndWrite(PlayerAvatar player, AvatarState state, string resource, string marker)
        {
            player.customStats.Remove(marker);
            state.Run.SetString(Key(state, resource + "Pending"), "");
        }

        private static MoneyCheckpoint ReadMoney(AvatarState state)
        {
            string saved = state.Run.GetString(Key(state, "Money"), "");
            if (string.IsNullOrEmpty(saved)) return null;
            string[] values = saved.Split('|');
            if (values.Length != 8 || values[0] != "1" || Decode(values[1]) != state.Guid ||
                !int.TryParse(values[2], NumberStyles.Integer, Culture, out int active) || (active != 0 && active != 1) ||
                !Enum.TryParse(values[3], out ResourceMode mode) || !Enum.IsDefined(typeof(ResourceMode), mode) ||
                !decimal.TryParse(values[4], NumberStyles.Number, Culture, out decimal amount) ||
                !int.TryParse(values[5], NumberStyles.Integer, Culture, out int seed) || seed < 0 ||
                !int.TryParse(values[6], NumberStyles.Integer, Culture, out int granted) || granted < 0 || granted > seed ||
                !int.TryParse(values[7], NumberStyles.Integer, Culture, out int departed) || (departed != 0 && departed != 1))
                throw new InvalidOperationException("Invalid saved starting-leaf checkpoint.");
            return new MoneyCheckpoint { Active = active == 1, Setting = new ResourceSetting(mode, amount), Seed = seed, Granted = granted, Departed = departed == 1 };
        }

        private static void SaveMoney(AvatarState state)
        {
            MoneyCheckpoint checkpoint = state.Money;
            state.Run.SetString(Key(state, "Money"), string.Join("|", "1", Encode(state.Guid), checkpoint.Active ? "1" : "0",
                checkpoint.Setting.Mode.ToString(), checkpoint.Setting.Amount.ToString(Culture), checkpoint.Seed.ToString(Culture),
                checkpoint.Granted.ToString(Culture), checkpoint.Departed ? "1" : "0"));
        }

        private static void Fail(string message)
        {
            Available = false;
            Error = message;
            ResourceRuntime.Report(message);
        }
    }
}
