using Mirror;
using SephiriaOne;

internal static class LobbyRegenTests
{
    internal static void Run(Func<PlayerSpawner> start, Func<uint, PlayerSpawner> add, Action<bool, string> check)
    {
        LobbyRegenHooks.Install();
        try
        {
            var host = start().PlayerAvatar; var guest = add(27).PlayerAvatar;
            void Command(string op) => check(SettingsActions.Execute("/one friendlyfire " + op).Success, "Recovery control: " + op);
            void Tick(PlayerAvatar player, bool blocked, string why)
            {
                float hp = player.hp; int mp = player.mp, ticks = player.UpdateCalls;
                bool place = DungeonManager.Instance.generatedFloors.TryGetValue(player.currentFloorGuid, out var floor) && player.Race.healPlaces.Contains(floor.name);
                player.TickRecovery();
                check(player.hp == hp + (blocked ? 0 : place ? 11 : 1) && player.mp == mp + (blocked ? 0 : 1), why);
                check(player.UpdateCalls == ticks + 1, "Unrelated native Update continues");
            }
            Tick(host, false, "Defaults preserve recovery"); Command("block-lobby-regen on");
            Tick(host, false, "Option alone does not suppress recovery"); Command("on");
            Tick(host, true, "Host automatic recovery is blocked"); Tick(guest, true, "Guest automatic recovery is blocked by host");
            float hp = guest.hp; int mp = guest.mp;
            guest.Heal(5f); guest.Heal(10f, false, false); guest.HealMp(7);
            check(guest.hp == hp + 15 && guest.mp == mp + 7, "Potion/skill/direct healing remains native");
            guest.IsDead = true; guest.Revive(80);
            check(!guest.IsDead && guest.hp == 80, "Revival still restores HP");
            host.Follower = new UnitAvatar(); Tick(host, true, "Player still blocked with follower");
            check(host.Follower.hp == 5, "Native follower healing remains intact");
            FriendlyFireParticipants.Set(FriendlyFireParticipants.Capture(host), true);
            Tick(host, true, "Selected player blocked"); Tick(guest, false, "Excluded player keeps recovery");
            FriendlyFireParticipants.ResetSelection(); Tick(guest, true, "Empty selection enables everyone");
            var receipt = FriendlyFireParticipants.Capture(guest);
            FriendlyFireParticipants.Retire(guest.spawner);
            Tick(guest, false, "Retired connection cannot inherit suppression");
            PlayerSpawner.MultiplayerList.Remove(guest.spawner);
            guest = add(27).PlayerAvatar; SessionSettings.Synchronize();
            Tick(guest, true, "New connection immediately uses current policy");
            DungeonManager.Instance.generatedFloors["dungeon"] = new FloorData { stageName = "chapter1", name = "room" };
            guest.currentFloorGuid = "dungeon"; Tick(guest, false, "Dungeon HP/MP recovery remains native");
            guest.currentFloorGuid = "missing"; Tick(guest, false, "Unknown floor fails open");
            guest.currentFloorGuid = "town"; Tick(guest, true, "Returning to waiting room reevaluates policy");
            Command("off"); Tick(guest, false, "Turning friendly fire off restores recovery immediately");
            Command("on"); Command("block-lobby-regen off"); Tick(guest, false, "Turning recovery option off restores native recovery");
            Command("block-lobby-regen on");
            LobbyRegenFeature.Available = false;
            Tick(guest, false, "Unavailable optional hook preserves native behavior");
            check(!SettingsActions.Execute("/one friendlyfire block-lobby-regen on").Success &&
                SettingsActions.Execute("/one friendlyfire damage 50").Success && SettingsActions.Execute("/one friendlyfire block-lobby-regen off").Success,
                "Optional compatibility failure leaves combat and off recovery available");
            LobbyRegenFeature.Available = true; Command("block-lobby-regen on");
            check(SettingsActions.Execute("/one save").Success, "Save recovery preference");
            SessionSettings.Stop(); SessionSettings.Start(); SessionSettings.Synchronize();
            Tick(guest, true, "Saved recovery preference restores with fresh scope");
            FriendlyFireFeature.Available = false;
            Tick(guest, false, "Saved intent cannot suppress recovery when core combat hooks are unavailable");
            FriendlyFireFeature.Available = true;
            check(SettingsActions.Execute("/one forget").Success, "Forget recovery preset");
            Command("reset"); Tick(guest, false, "Reset restores recovery");
            NetworkServer.active = false;
            check(!SettingsActions.Execute("/one friendlyfire block-lobby-regen on").Success, "Guest cannot edit recovery setting");
            NetworkServer.active = true;
        }
        finally { LobbyRegenHooks.Uninstall(); LobbyRegenFeature.Available = true; }
    }
}
