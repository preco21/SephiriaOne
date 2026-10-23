using HarmonyLib;
using SephiriaOne;

internal static class PotionRespawnTests
{
    internal static void Run(Func<PlayerSpawner> start, Func<uint, PlayerSpawner> add, Action<bool, string> check)
    {
        var harmony = new Harmony("PotionRespawn.integration.tests");
        harmony.Patch(AccessTools.Method(typeof(UnitAvatar), "Die"),
            prefix: new HarmonyMethod(typeof(FriendlyFireRuntime), "BeforeDeath"),
            finalizer: new HarmonyMethod(typeof(FriendlyFireRuntime), "FinishDeath"));
        harmony.Patch(AccessTools.Method(typeof(UnitAvatar), "Revive"),
            prefix: new HarmonyMethod(typeof(FriendlyFireRuntime), "BeforeRevive"),
            postfix: new HarmonyMethod(typeof(FriendlyFireRuntime), "AfterRevive"),
            finalizer: new HarmonyMethod(typeof(FriendlyFireRuntime), "FinishRevive"));
        try
        {
            PlayerAvatar[] New()
            {
                UnityEngine.Time.unscaledTime = 0;
                var players = new[] { start().PlayerAvatar, add(2).PlayerAvatar, add(3).PlayerAvatar, add(4).PlayerAvatar };
                SessionSettings.EnsureResourceScope(); LargePotionGrant.Load();
                return players;
            }
            void Command(string args) => check(SettingsActions.Execute("/one friendlyfire " + args).Success, "Supply command: " + args);
            void Consume(PlayerAvatar player) { player.Inventory.inventoryMatrix.Clear(); player.Inventory.subBagMatrix.Clear(); player.Inventory.temporaryInventory.Clear(); }
            bool Has(PlayerAvatar player) => LargePotionGrant.Owns(player.Inventory);
            void Revive(PlayerAvatar player) { player.IsDead = true; player.Revive(player.MaxHp); }

            var players = New(); Command("on"); PvpPotionSupplies.Tick();
            Consume(players[0]); Consume(players[2]);
            Revive(players[0]);
            check(!Has(players[0]), "Respawn inventory writes are deferred until supply reconciliation");
            PvpPotionSupplies.Tick();
            check(Has(players[0]) && !Has(players[2]), "Successful revival replaces only the revived player's consumed potion");
            int writes = players[0].Inventory.PotionWrites;
            Revive(players[0]); PvpPotionSupplies.Tick();
            check(players[0].Inventory.PotionWrites == writes, "Retained potion is never duplicated on respawn");
            Consume(players[0]); players[0].Revive(players[0].MaxHp); PvpPotionSupplies.Tick();
            check(!Has(players[0]), "Calling Revive on an already living player does not refill supplies");

            players[0].ReviveCallback = () => throw new InvalidOperationException("failed native revival");
            try { Revive(players[0]); } catch (InvalidOperationException) { }
            players[0].ReviveCallback = null; PvpPotionSupplies.Tick();
            check(!Has(players[0]), "Failed revival that cleared IsDead does not count as successful respawn");
            players[0].ReviveCallback = () => players[0].Revive(players[0].MaxHp);
            Revive(players[0]); players[0].ReviveCallback = null; PvpPotionSupplies.Tick();
            check(Has(players[0]) && players[0].Inventory.PotionWrites == writes + 1, "Nested no-op revival schedules just one replacement");

            Consume(players[0]); players[0].Inventory.canBroadcast = 0;
            Revive(players[0]); PvpPotionSupplies.Tick(); check(!Has(players[0]), "Successful respawn waits for inventory readiness");
            players[0].Inventory.canBroadcast = 1; PvpPotionSupplies.Tick(); check(Has(players[0]), "Ready inventory receives pending respawn supply");
            Consume(players[0]); PvpPotionSupplies.Tick(); check(!Has(players[0]), "Consuming a replacement does not trigger continuous refill");

            Revive(players[0]); // Leave this life's replacement pending.
            players[0].ReviveCallback = () => throw new InvalidOperationException("newer revival failed");
            try { Revive(players[0]); } catch (InvalidOperationException) { }
            players[0].ReviveCallback = null; PvpPotionSupplies.Tick();
            check(!Has(players[0]), "Failed newer revival cancels a previous life's pending replacement");
            players[0].ReviveCallback = () =>
            {
                players[0].ReviveCallback = () => throw new InvalidOperationException("nested newer revival failed");
                try { Revive(players[0]); } catch (InvalidOperationException) { }
                players[0].ReviveCallback = null;
            };
            Revive(players[0]); PvpPotionSupplies.Tick();
            check(!Has(players[0]), "Older successful outer revival cannot release a newer failed nested life");
            bool wroteInsideRevive = false;
            players[0].ReviveCallback = () => { PvpPotionSupplies.Tick(); wroteInsideRevive = Has(players[0]); };
            Revive(players[0]); players[0].ReviveCallback = null;
            check(!wroteInsideRevive && !Has(players[0]), "Reentrant supply tick during native revival cannot deliver prematurely");
            PvpPotionSupplies.Tick(); check(Has(players[0]), "Completed revival releases its deferred supply check");
            Consume(players[0]);

            Revive(players[0]); players[0].IsDead = true;
            PvpPotionSupplies.Tick(); check(!Has(players[0]), "A second death before reconciliation waits for another successful revival");
            players[0].Revive(players[0].MaxHp); PvpPotionSupplies.Tick(); check(Has(players[0]), "Next living state receives one replacement");
            Consume(players[0]); players[0].IsDead = true;
            check(ReviveAllAction.TryExecute(out _), "Manual Revive All succeeds during casual PvP");
            PvpPotionSupplies.Tick(); check(Has(players[0]), "Revive All shares the same supplies path");
            Consume(players[0]);

            FriendlyFireParticipants.Set(FriendlyFireParticipants.Capture(players[1]), true); PvpPotionSupplies.Tick();
            Revive(players[0]); PvpPotionSupplies.Tick(); check(!Has(players[0]), "Excluded player receives no respawn supply");
            FriendlyFireParticipants.ResetSelection(); PvpPotionSupplies.Tick();
            Consume(players[0]); Command("auto-potion off"); PvpPotionSupplies.Tick();
            Revive(players[0]); PvpPotionSupplies.Tick(); check(!Has(players[0]), "Disabled automatic option prevents respawn grants");

            players = New(); Command("on"); PvpPotionSupplies.Tick(); Consume(players[0]);
            players[0].ReviveCallback = () => Command("off");
            Revive(players[0]); players[0].ReviveCallback = null; PvpPotionSupplies.Tick();
            check(!Has(players[0]), "PvP ending inside revival cancels replacement");

            players = New(); Command("on"); PvpPotionSupplies.Tick(); Consume(players[0]);
            var retired = players[0];
            retired.ReviveCallback = () => { PlayerSpawner.MultiplayerList.Remove(retired.spawner); Mirror.NetworkServer.connections.Remove(retired.spawner.connectionToClient.connectionId); };
            Revive(retired); PvpPotionSupplies.Tick(); check(!Has(retired), "Disconnected avatar cannot receive a stale respawn grant");
            var rejoined = add(1).PlayerAvatar; PvpPotionSupplies.Tick(); check(Has(rejoined), "Replacement connection receives fresh entry supply");

            players = New(); Command("on"); PvpPotionSupplies.Tick(); Consume(players[0]);
            players[0].Inventory.BeforePotionWrite = () => throw new InvalidOperationException("respawn grant failure");
            Revive(players[0]); PvpPotionSupplies.Tick(); players[0].Inventory.BeforePotionWrite = null;
            players[0].Inventory.canBroadcast = 0; PvpPotionSupplies.Tick();
            players[0].Inventory.canBroadcast = 1; PvpPotionSupplies.Tick();
            check(!Has(players[0]), "Ambiguous respawn grant failure is not retried on ordinary readiness changes");
            Revive(players[0]); PvpPotionSupplies.Tick(); check(Has(players[0]), "Later successful life may safely recheck failed grant ownership");

            foreach (var store in new[] { "belt", "bag", "overflow" })
            {
                Consume(players[0]);
                if (store == "belt") players[0].Inventory.inventoryMatrix[new ItemPosition { y = 100 }] = new() { EntityID = 1, Quantity = 2 };
                if (store == "bag") players[0].Inventory.subBagMatrix[0] = new(91, 1, 2);
                if (store == "overflow") players[0].Inventory.temporaryInventory.Add(new(92, 1, 2));
                writes = players[0].Inventory.PotionWrites; Revive(players[0]); PvpPotionSupplies.Tick();
                check(players[0].Inventory.PotionWrites == writes, store + ": owned potion prevents respawn duplicate");
            }

            players = New(); Command("on"); PvpPotionSupplies.Tick();
            players[0].IsDead = true; FriendlyFireRuntime.BeforeRevive(players[0], out var oldRevival);
            Command("off"); Command("on"); PvpPotionSupplies.Tick();
            players[0].IsDead = false; players[0].hp = players[0].MaxHp;
            PvpPotionSupplies.Tick(); Consume(players[0]);
            FriendlyFireRuntime.FinishRevive(null, players[0], oldRevival); PvpPotionSupplies.Tick();
            check(!Has(players[0]), "Revival from a previous activation cannot refresh current supply state");

            players = New(); Command("on"); // No entry tick yet.
            players[0].ReviveCallback = () => throw new InvalidOperationException("first revival failed");
            try { Revive(players[0]); } catch (InvalidOperationException) { }
            players[0].ReviveCallback = null; PvpPotionSupplies.Tick();
            check(!Has(players[0]) && players.Skip(1).All(Has), "First supply check cannot treat a failed revival as ready entry");
            Revive(players[0]); PvpPotionSupplies.Tick(); check(Has(players[0]), "Successful retry supplies an initially failed revival");

            Consume(players[0]); Revive(players[0]);
            players[0].Inventory.PermissionOpened = () =>
            {
                players[0].Inventory.PermissionOpened = null;
                players[0].ReviveCallback = () => throw new InvalidOperationException("revival failed during supply permission");
                try { Revive(players[0]); } catch (InvalidOperationException) { }
                players[0].ReviveCallback = null;
            };
            PvpPotionSupplies.Tick(); PvpPotionSupplies.Tick();
            check(!Has(players[0]), "In-flight grant cannot survive a failed newer revival inside inventory callbacks");
            Revive(players[0]); PvpPotionSupplies.Tick(); check(Has(players[0]), "Later valid life recovers after an interrupted in-flight grant");

            foreach (string mode in new[] { "deathmatch", "tournament", "tag", "zombie" })
            {
                players = New();
                check(SettingsActions.Execute("/one " + mode + " start").Success, mode + ": start supply test");
                PvpPotionSupplies.Tick(); check(players.All(Has), mode + ": start supplies all roster members");
                UnityEngine.Time.unscaledTime = 3; CombatMatchRuntime.Tick();
                var pair = players.SelectMany(a => players.Where(b => CombatMatchRuntime.AllowsPair(a, b)).Select(b => new[] { a, b })).First();
                var victim = pair[1]; Consume(victim);
                var damage = new DamageInstance(); FriendlyFireRuntime.Attribute(pair[0], victim, damage); victim.Die(0, damage);
                PvpPotionSupplies.Tick(); check(!Has(victim), mode + ": death alone does not grant a potion");
                UnityEngine.Time.unscaledTime = 6; CombatMatchRuntime.Tick(); PvpPotionSupplies.Tick();
                check(!victim.IsDead && Has(victim) && victim.Inventory.PotionWrites == 2, mode + ": countdown revival grants one replacement");
                check(players.Where(p => p != victim).All(p => p.Inventory.PotionWrites == 1), mode + ": other players retain one original supply");
                Consume(victim);
                check(SettingsActions.Execute("/one " + mode + " stop").Success, mode + ": stop supply test");
                Revive(victim); PvpPotionSupplies.Tick(); check(!Has(victim), mode + ": no grants after mode cleanup");
            }
        }
        finally { harmony.UnpatchAll(harmony.Id); }
    }
}
