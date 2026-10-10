using Mirror;
using SephiriaOne;

internal static class FriendlyFireSelectionTests
{
    internal static void Run(Func<PlayerSpawner> start, Func<uint, PlayerSpawner> add, Action<bool, string> check)
    {
        var host = start(); var guest = add(2); var bystander = add(3);
        check(SettingsActions.Execute("/one friendlyfire players").Success, "Selection command lists current players");
        check(FriendlyFireParticipants.Includes(host.PlayerAvatar) && FriendlyFireParticipants.Includes(guest.PlayerAvatar), "Empty selection includes every current player");
        check(SettingsActions.Execute("/one friendlyfire player 1 on").Success, "Host can select a player without enabling master");
        check(!SessionSettings.FriendlyFireForHit.Enabled, "Selecting a player does not enable the master toggle");
        check(FriendlyFireParticipants.Includes(host.PlayerAvatar) && !FriendlyFireParticipants.Includes(guest.PlayerAvatar), "Nonempty selection excludes unselected players");
        check(SettingsActions.Execute("/one friendlyfire player 2 on").Success, "Host can select a second player");
        check(FriendlyFireParticipants.Includes(guest.PlayerAvatar) && !FriendlyFireParticipants.Includes(bystander.PlayerAvatar), "Two selected participants exclude bystander");
        check(SettingsActions.Execute("/one friendlyfire players all").Success && FriendlyFireParticipants.SelectedCount == 3 &&
            FriendlyFireParticipants.IsSelected(bystander.PlayerAvatar), "Select All explicitly selects every current connection");
        check(SettingsActions.Execute("/one friendlyfire player 3 off").Success && FriendlyFireParticipants.SelectedCount == 2 &&
            !FriendlyFireParticipants.Includes(bystander.PlayerAvatar), "Select All supports deselecting an exception");
        var newcomer = add(4);
        check(!FriendlyFireParticipants.Includes(newcomer.PlayerAvatar), "New arrivals stay unselected while explicit selection exists");
        SettingsActions.Execute("/one friendlyfire on"); SettingsActions.Execute("/one friendlyfire damage 0.1");
        check(SettingsActions.Execute("/one friendlyfire players reset").Success, "Reset selection command restores everyone mode");
        check(FriendlyFireParticipants.SelectedCount == 0 && FriendlyFireParticipants.Includes(newcomer.PlayerAvatar) &&
            SessionSettings.FriendlyFireForHit.Enabled && SessionSettings.FriendlyFireForHit.DamagePercent == 0.1m,
            "Selection reset includes everyone and preserves master switch and damage scale");
        check(SettingsActions.Execute("/one friendlyfire players reset").Success, "Reset empty selection is safe");
        SettingsActions.Execute("/one friendlyfire players all");
        var snapshot = SessionSettings.ReadSnapshot(SnapshotContent.Participants);
        check(snapshot.FriendlyFirePlayers.Count == 4 && snapshot.FriendlyFireSelectedCount == 4 &&
            snapshot.FriendlyFirePlayers.All(p => p.Selected && p.Identity != null), "Panel snapshot exposes explicit selections and exact connection tokens");
        object guestToken = snapshot.FriendlyFirePlayers.Single(p => p.Id == 2).Identity;
        PlayerSpawner.MultiplayerList.Remove(guest);
        var replacement = add(2);
        check(!SettingsActions.Execute("/one friendlyfire player 2 off", guestToken).Success &&
            !FriendlyFireParticipants.IsSelected(replacement.PlayerAvatar), "Stale UI click cannot select or deselect replacement player sharing old ID");
        check(SettingsActions.Execute("/one friendlyfire player 2 on").Success, "Fresh command resolves replacement connection");
        check(SettingsActions.Execute("/one save").Success, "Save persistent global combat settings with temporary selections present");
        SessionSettings.Stop(); SessionSettings.Start(); SessionSettings.EnsureResourceScope();
        check(FriendlyFireParticipants.SelectedCount == 0 && SessionSettings.FriendlyFireForHit.Enabled &&
            SessionSettings.FriendlyFireForHit.DamagePercent == 0.1m, "Preset restoration keeps master/scale but never selects previous connections");
        SettingsActions.Execute("/one friendlyfire players all");
        check(SettingsActions.Execute("/one friendlyfire off").Success && FriendlyFireParticipants.SelectedCount == 4, "Master off preserves temporary selection for later editing");
        check(SettingsActions.Execute("/one friendlyfire reset").Success && FriendlyFireParticipants.SelectedCount == 0, "Full combat reset also clears selection");
        SessionSettings.RecordFault("stats", new StateWriteBatch(() => true), "fixture fault");
        bystander.connectionToClient.isReady = false;
        check(SettingsActions.Execute("/one friendlyfire player 1 on").Success && SettingsActions.Execute("/one friendlyfire players reset").Success,
            "Policy-only selection controls stay available with unrelated stat fault and loading guest");
        foreach (var command in new[] { "player", "player 99 on", "player -1 on", "player 1 yes", "players reset extra" })
            check(!SettingsActions.Execute("/one friendlyfire " + command).Success, "Reject malformed or stale player selection: " + command);
        NetworkServer.active = false;
        check(!SettingsActions.Execute("/one friendlyfire player 1 on").Success &&
            !SettingsActions.Execute("/one friendlyfire players reset").Success &&
            !SettingsActions.Execute("/one friendlyfire players all").Success, "Guests cannot select or reset host participants");
        start();
    }
}
