using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Mirror;
using UnityEngine;

namespace SephiriaOne
{
    internal static partial class RabbitPotionNativeHooks
    {
        private const string Owner = "preco21.SephiriaOne.RabbitPotions";
        private const float ShareRadiusSquared = 100f;
        private static Harmony harmony;
        [ThreadStatic] private static Stack<DrinkContext> drinks;
        public static bool Available { get; private set; }

        private sealed class DrinkContext
        {
            public WieldingPotion Potion;
            public PotionEffect Effect;
            public ItemController Controller;
            public PlayerAvatar Player;
            public PlayerSpawner Spawner;
            public NetworkConnectionToClient Connection;
            public GridInventory Inventory;
            public NewItemOwnInstance Item;
            public int InstanceId;
            public int EntityId;
            public int Slot;
            public object Run;
            public DungeonManager Dungeon;
            public string Floor;
            public RabbitPotionSettings Settings;
            public float HealStrength;
            public int HealCalls;
            public DrinkContext NativeDrink;
        }

        public static void Install()
        {
            if (Available) return;
            try
            {
                if (harmony != null)
                {
                    harmony.UnpatchAll(Owner);
                    harmony = null;
                }
                MethodInfo drink = AccessTools.DeclaredMethod(typeof(WieldingPotion), nameof(WieldingPotion.Drink),
                    new[] { typeof(bool).MakeByRefType(), typeof(int) });
                MethodInfo heal = AccessTools.DeclaredMethod(typeof(PotionEffect_Regeneration),
                    nameof(PotionEffect_Regeneration.CreateEffect_OnDrink), new[] { typeof(UnitAvatar) });
                MethodInfo consumer = AccessTools.DeclaredMethod(typeof(ItemController),
                    nameof(ItemController.DrinkPotionAnimation), Type.EmptyTypes);
                MethodInfo survival = AccessTools.DeclaredMethod(typeof(PassiveObject_PotionAndRandomStat),
                    "HandleDrinkPotion", new[] { typeof(PotionEffect) });
                if (drink == null || drink.ReturnType != typeof(void) || heal == null || heal.ReturnType != typeof(void) ||
                    consumer == null || consumer.ReturnType != typeof(void) ||
                    survival == null || survival.ReturnType != typeof(void) ||
                    !ValidateMpSetter() || !ValidateDrinkShape(drink) || !ValidateConsumerShape(consumer))
                { Debug.LogWarning("[SephiriaOne] Rabbit potion Drink/consumer signature or IL changed; optional hooks are disabled."); return; }
                var instance = new Harmony(Owner);
                harmony = instance;
                try
                {
                    instance.Patch(heal, transpiler: new HarmonyMethod(typeof(RabbitPotionNativeHooks), nameof(CaptureHealCall)));
                    instance.Patch(consumer, transpiler: new HarmonyMethod(typeof(RabbitPotionNativeHooks), nameof(GuardCompletedDrink)));
                    instance.Patch(survival, prefix: new HarmonyMethod(typeof(RabbitPotionNativeHooks), nameof(AllowSurvival)));
                    instance.Patch(drink, prefix: new HarmonyMethod(typeof(RabbitPotionNativeHooks), nameof(BeginDrink)),
                        postfix: new HarmonyMethod(typeof(RabbitPotionNativeHooks), nameof(CompleteDrink)),
                        finalizer: new HarmonyMethod(typeof(RabbitPotionNativeHooks), nameof(EndDrink)));
                    Available = true;
                }
                catch (Exception error)
                {
                    try { instance.UnpatchAll(Owner); harmony = null; }
                    catch (Exception rollback) { Debug.LogWarning("[SephiriaOne] Rabbit potion hook rollback failed: " + rollback); }
                    Debug.LogWarning("[SephiriaOne] Rabbit potion hooks are unavailable: " + error);
                }
            }
            catch (Exception error)
            { Available = false; Debug.LogWarning("SephiriaOne: rabbit potion compatibility check failed: " + error); }
        }

        public static void Uninstall()
        {
            Available = false;
            try { harmony?.UnpatchAll(Owner); harmony = null; }
            catch (Exception error) { Debug.LogWarning("[SephiriaOne] Rabbit potion hook removal failed: " + error); }
            drinks?.Clear();
            completions?.Clear();
            ResetHealVisuals();
        }

        private static bool ValidateDrinkShape(MethodInfo drink)
        {
            var code = PatchProcessor.GetOriginalInstructions(drink).ToList();
            var effects = Enumerable.Range(0, code.Count).Where(i => code[i].operand is MethodInfo method &&
                method.DeclaringType == typeof(PotionEffect) && method.Name == nameof(PotionEffect.CreateEffect_OnDrink) &&
                method.GetParameters().Length == 1).ToList();
            var decisions = Enumerable.Range(0, code.Count).Where(i => code[i].operand is MethodInfo method &&
                method.DeclaringType == typeof(PotionEffect) && method.Name == "get_DecreaseItemOnDrink").ToList();
            var stores = Enumerable.Range(0, code.Count).Where(i => code[i].opcode == OpCodes.Stind_I1).ToList();
            return effects.Count == 1 && decisions.Count == 1 && stores.Count == 1 &&
                decisions[0] < effects[0] && effects[0] < stores[0] &&
                code.Take(effects[0]).Any(i => i.operand is FieldInfo field &&
                    field.DeclaringType == typeof(WieldingPotion) && field.Name == nameof(WieldingPotion.itemInstanceID));
        }

        private static bool ValidateConsumerShape(MethodInfo consumer)
        {
            return ValidateConsumerCode(PatchProcessor.GetOriginalInstructions(consumer).ToList());
        }

        private static bool ValidateConsumerCode(List<CodeInstruction> code)
        {
            var drinks = Enumerable.Range(0, code.Count).Where(i => code[i].operand is MethodInfo method &&
                method.DeclaringType == typeof(WieldingPotion) && method.Name == nameof(WieldingPotion.Drink) &&
                method.GetParameters().Length == 2).ToList();
            var decreases = Enumerable.Range(0, code.Count).Where(i => code[i].operand is MethodInfo method &&
                method.DeclaringType == typeof(GridInventory) && method.Name == nameof(GridInventory.DecreaseItemQuantity)).ToList();
            var events = Enumerable.Range(0, code.Count).Where(i => code[i].operand is FieldInfo field &&
                field.DeclaringType == typeof(ItemController) && field.Name == "OnDrinkPotionServerside").ToList();
            if (drinks.Count != 1 || decreases.Count != 1 || events.Count != 1 ||
                drinks[0] >= events[0] || events[0] >= decreases[0]) return false;
            return code.Skip(drinks[0] + 1).Take(decreases[0] - drinks[0] - 1).Any(i =>
                i.opcode == OpCodes.Brfalse || i.opcode == OpCodes.Brfalse_S) &&
                ValidateRejectionCleanup(code, drinks[0], decreases[0]);
        }

        private static IEnumerable<CodeInstruction> CaptureHealCall(IEnumerable<CodeInstruction> instructions)
        {
            var code = instructions.ToList();
            var matches = code.Where(i => (i.opcode == OpCodes.Call || i.opcode == OpCodes.Callvirt) &&
                i.operand is MethodInfo method && method.DeclaringType == typeof(UnitAvatar) &&
                method.Name == nameof(UnitAvatar.HealPercent) && method.ReturnType == typeof(void) &&
                method.GetParameters().Length == 1 && method.GetParameters()[0].ParameterType == typeof(float)).ToList();
            if (matches.Count != 1) throw new InvalidOperationException("Native regeneration HealPercent call changed.");
            matches[0].opcode = OpCodes.Call;
            matches[0].operand = AccessTools.Method(typeof(RabbitPotionNativeHooks), nameof(HealAndCapture));
            return code;
        }

        private static void HealAndCapture(UnitAvatar avatar, float strength)
        {
            avatar.HealPercent(strength);
            DrinkContext context = drinks != null && drinks.Count != 0 ? drinks.Peek() : null;
            if (context != null && ReferenceEquals(avatar, context.Player))
            { context.HealStrength = strength; context.HealCalls++; }
        }

        private static void BeginDrink(WieldingPotion __instance, int instanceID, out DrinkContext __state)
        {
            __state = null;
            DrinkContext completion = completions != null && completions.Count != 0 ? completions.Peek() : null;
            if (completion != null && completion.NativeDrink == null &&
                ReferenceEquals(completion.Potion, __instance) && completion.InstanceId == instanceID)
            {
                // Admission occurred before native callbacks. Do not lose it to death/wield cleanup
                // between the controller boundary and this exact first Drink invocation.
                __state = completion;
                completion.NativeDrink = completion;
            }
            else try { __state = CaptureContext(__instance, instanceID); } catch { }
            if (drinks == null) drinks = new Stack<DrinkContext>();
            drinks.Push(__state);
        }

        private static DrinkContext CaptureContext(WieldingPotion potion, int instanceId, bool allowDead = false)
        {
            if (!Available || !NetworkServer.active || !potion || !potion.isServer || potion.netId == 0 ||
                potion.effect == null || potion.effect.GetType() != typeof(PotionEffect_Regeneration) ||
                !Allowed(potion.entityID)) return null;
            RabbitPotionSettings settings = SessionSettings.RabbitPotionsForUse;
            if (!settings.Infinite && !settings.Share && !settings.ConsumeMp &&
                !(settings.SuppressSurvival && CanSuppressSurvival(potion.entityID))) return null;
            ItemController controller = potion.NetworkController;
            PlayerAvatar player = controller?.Avatar as PlayerAvatar;
            PlayerSpawner spawner = player?.spawner;
            if (!SourceReady(potion, controller, player, spawner, allowDead)) return null;
            GridInventory inventory = player.Inventory;
            int selected = controller.SelectedQuickSlotIdx;
            if (selected < 0 || selected >= controller.quickSlotTable.Count) return null;
            int slot = controller.quickSlotTable[selected].idx;
            NewItemOwnInstance item = inventory.FindItem(inventory.IdxToPos(slot));
            if (item == null || item.Quantity <= 0 || item.InstanceID != instanceId ||
                item.EntityID != potion.entityID) return null;
            return new DrinkContext { Potion = potion, Effect = potion.effect, Controller = controller,
                Player = player, Spawner = spawner, Connection = spawner.connectionToClient, Inventory = inventory, Item = item,
                InstanceId = instanceId, EntityId = potion.entityID, Slot = slot, Run = SaveManager.CurrentRun,
                Dungeon = DungeonManager.Instance, Floor = player.currentFloorGuid, Settings = settings };
        }

        private static bool SourceReady(WieldingPotion potion, ItemController controller, PlayerAvatar player, PlayerSpawner spawner,
            bool allowDead = false)
        {
            bool deathCleanup = allowDead && player && player.IsDead;
            if (!controller || !controller.isServer || controller.netId == 0 ||
                !ReferenceEquals(controller.Avatar, player) ||
                (!ReferenceEquals(controller.NetworkcurrentWieldingItem, potion) &&
                    !(deathCleanup && !controller.NetworkcurrentWieldingItem)) ||
                !player || (player.IsDead && !allowDead) || player.currentCostume != "HolyRabbit" || !spawner ||
                !ReferenceEquals(spawner.PlayerAvatar, player) || !HostStateAdapter.IsReady(spawner) ||
                !PlayerSpawner.MultiplayerList.Contains(spawner)) return false;
            var connection = spawner.connectionToClient;
            if (connection == null || !connection.isReady || connection.identity == null ||
                !connection.identity.TryGetComponent<PlayerSpawner>(out PlayerSpawner owner) ||
                !ReferenceEquals(owner, spawner) || !ReferenceEquals(controller.connectionToClient, connection)) return false;
            foreach (var entry in NetworkServer.connections)
                if (ReferenceEquals(entry.Value, connection)) return true;
            return false;
        }

        private static bool Current(DrinkContext context, bool retainDeath = false)
        {
            // Only an operation admitted by the controller can survive death's transient wield teardown.
            // Ownership, session, costume and the original inventory item must still match.
            bool deathCleanup = retainDeath && context != null && context.Player && context.Player.IsDead &&
                IsNativeCompletion(context);
            if (context == null || !Available || !NetworkServer.active || !ReferenceEquals(SaveManager.CurrentRun, context.Run) ||
                (!deathCleanup && (!context.Potion || !context.Potion.isServer || context.Potion.netId == 0 ||
                    !ReferenceEquals(context.Potion.effect, context.Effect) || context.Potion.itemInstanceID != context.InstanceId ||
                    context.Potion.entityID != context.EntityId)) ||
                !ReferenceEquals(context.Spawner.connectionToClient, context.Connection) ||
                !ReferenceEquals(DungeonManager.Instance, context.Dungeon) ||
                context.Player.currentFloorGuid != context.Floor ||
                !SourceReady(context.Potion, context.Controller, context.Player, context.Spawner, deathCleanup) ||
                context.Controller.SelectedQuickSlotIdx < 0 ||
                context.Controller.SelectedQuickSlotIdx >= context.Controller.quickSlotTable.Count ||
                context.Controller.quickSlotTable[context.Controller.SelectedQuickSlotIdx].idx != context.Slot ||
                !ReferenceEquals(context.Player.Inventory, context.Inventory)) return false;
            NewItemOwnInstance item = context.Inventory.FindItem(context.Inventory.IdxToPos(context.Slot));
            return ReferenceEquals(item, context.Item) && item.Quantity > 0 &&
                item.InstanceID == context.InstanceId && item.EntityID == context.EntityId;
        }

        private static void CompleteDrink(ref bool itemDecreased, DrinkContext __state)
        {
            try
            {
                if (!Current(__state, retainDeath: true) || __state.HealCalls != 1) return;
                RabbitPotionSettings now = SessionSettings.RabbitPotionsForUse;
                if (__state.Settings.Infinite && now.Infinite && itemDecreased) itemDecreased = false;
                if (__state.Settings.Share && now.Share && IsFinitePositive(__state.HealStrength) && Current(__state)) Share(__state);
            }
            catch { /* An optional callback must never cancel the native drink. */ }
        }

        private static Exception EndDrink(Exception __exception)
        {
            if (drinks != null && drinks.Count != 0) drinks.Pop();
            return __exception;
        }

        private static void Share(DrinkContext context)
        {
            var seen = new HashSet<PlayerAvatar>();
            seen.Add(context.Player);
            foreach (var entry in NetworkServer.connections.ToArray())
            {
                try
                {
                    var connection = entry.Value;
                    if (!NetworkServer.connections.TryGetValue(entry.Key, out var live) ||
                        !ReferenceEquals(live, connection) || connection == null || !connection.isReady || connection.identity == null ||
                        !connection.identity.TryGetComponent<PlayerSpawner>(out PlayerSpawner spawner) ||
                        !ReferenceEquals(spawner.connectionToClient, connection) ||
                        !PlayerSpawner.MultiplayerList.Contains(spawner) || !HostStateAdapter.IsReady(spawner)) continue;
                    PlayerAvatar recipient = spawner.PlayerAvatar;
                    if (!recipient || !seen.Add(recipient) || recipient.IsDead ||
                        recipient.currentFloorGuid != context.Player.currentFloorGuid) continue;
                    Vector3 delta = recipient.transform.position - context.Player.transform.position;
                    if (!(delta.sqrMagnitude < ShareRadiusSquared) || !Current(context) ||
                        !SessionSettings.RabbitPotionsForUse.Share) continue;
                    float before = recipient.hp;
                    uint recipientId = recipient.netId;
                    var identity = connection.identity;
                    string floor = recipient.currentFloorGuid;
                    recipient.HealPercent(context.HealStrength);
                    // Native HP callbacks may end the run, move a player, replace
                    // their avatar/connection or disable sharing. FX is transient;
                    // never send it against a replaced lifetime or replay the heal.
                    if (!float.IsNaN(before) && !float.IsInfinity(before) &&
                        !float.IsNaN(recipient.hp) && !float.IsInfinity(recipient.hp) && recipient.hp > before &&
                        Current(context) && SessionSettings.RabbitPotionsForUse.Share &&
                        NetworkServer.connections.TryGetValue(entry.Key, out live) && ReferenceEquals(live, connection) &&
                        connection.isReady && ReferenceEquals(connection.identity, identity) &&
                        connection.identity.TryGetComponent<PlayerSpawner>(out var owner) && ReferenceEquals(owner, spawner) &&
                        ReferenceEquals(spawner.connectionToClient, connection) && PlayerSpawner.MultiplayerList.Contains(spawner) &&
                        HostStateAdapter.IsReady(spawner) && ReferenceEquals(spawner.PlayerAvatar, recipient) &&
                        ReferenceEquals(recipient.spawner, spawner) && recipient.netId == recipientId &&
                        !recipient.IsDead && recipient.currentFloorGuid == floor && floor == context.Floor)
                        ShowSharedHealVisual(recipient);
                }
                catch { /* A stale recipient or failed heal cannot cancel the native drink. */ }
            }
        }

        private static bool Allowed(int id) => id == 0 || id == 1 || id == 37;

        // Sample regeneration (37) retains native Survival procs, even while
        // other Rabbit options still admit the drink into a completion scope.
        private static bool CanSuppressSurvival(int id) => id == 0 || id == 1;
        private static bool IsFinitePositive(float value) => value > 0f && !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
