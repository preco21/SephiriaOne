using SephiriaOne;

internal static class PvpSharedHealTests
{
    internal static void Run(Action<string, Action> check, Action<bool, string> assert,
        Func<(PlayerAvatar player, ItemController controller, WieldingPotion potion, NewItemOwnInstance item)> setup,
        Action install)
    {
        foreach (bool balance in new[] { false, true })
        foreach (string active in new[] { "source", "recipient", "both" })
            check("PvP blocks Rabbit sharing for " + active + ", balance " + balance, () =>
            {
                var source = setup();
                var recipient = new PlayerAvatar(); _ = new PlayerSpawner(recipient);
                SessionSettings.RabbitPotionsForUse = new(true, true, true, true);
                SessionSettings.FriendlyFireForHit = new() { Enabled = true, Balance = balance };
                if (active != "recipient") FriendlyFireParticipants.Selected.Add(source.player);
                if (active != "source") FriendlyFireParticipants.Selected.Add(recipient);
                install(); source.controller.RunDrink();
                assert(recipient.Heals.Count == 0 && recipient.HealVisuals == 0,
                    "PvP participant must not send or receive shared potion healing/FX");
                assert(source.player.Heals.Count == 1 && source.player.mp == 20 && source.item.Quantity == 3,
                    "PvP sharing restriction must preserve self healing, MP fee and infinite potion");
                SessionSettings.FriendlyFireForHit = default;
                source.controller.RunDrink();
                assert(recipient.Heals.Count == 1 && recipient.HealVisuals == 1,
                    "Sharing resumes immediately after friendly fire ends");
            });

        check("Spectators keep Rabbit sharing during another pair's fight", () =>
        {
            var source = setup(); var recipient = new PlayerAvatar(); _ = new PlayerSpawner(recipient);
            SessionSettings.RabbitPotionsForUse = new(true, true);
            SessionSettings.FriendlyFireForHit = new() { Enabled = true };
            CombatMatchRuntime.ParticipantRule = _ => false;
            install(); source.controller.RunDrink();
            assert(recipient.Heals.Count == 1, "Spectator-to-spectator recovery must stay native");
            CombatMatchRuntime.ParticipantRule = p => p == recipient;
            source.controller.RunDrink();
            assert(recipient.Heals.Count == 1, "A spectator must not heal a current Tournament participant");
            CombatMatchRuntime.ParticipantRule = _ => false;
            source.controller.RunDrink();
            assert(recipient.Heals.Count == 2, "Leaving the active pair immediately restores shared recovery");
        });

        check("Mid-drink PvP changes are observed before shared healing", () =>
        {
            var source = setup(); var recipient = new PlayerAvatar(); _ = new PlayerSpawner(recipient);
            SessionSettings.RabbitPotionsForUse = new(true, true);
            source.player.OnPotionEvent = () => SessionSettings.FriendlyFireForHit = new() { Enabled = true };
            install(); source.controller.RunDrink();
            assert(recipient.Heals.Count == 0, "Starting PvP inside the native drink cannot leak shared healing");
            source.player.OnPotionEvent = () => SessionSettings.FriendlyFireForHit = default;
            source.controller.RunDrink();
            assert(recipient.Heals.Count == 1, "Ending PvP inside the native drink restores sharing");
        });

        check("PvP starting in a recipient callback blocks later recipients and presentation", () =>
        {
            var source = setup(); var first = new PlayerAvatar(); _ = new PlayerSpawner(first);
            var later = new PlayerAvatar(); _ = new PlayerSpawner(later);
            SessionSettings.RabbitPotionsForUse = new(true, true);
            first.OnHealed = () => SessionSettings.FriendlyFireForHit = new() { Enabled = true };
            install(); source.controller.RunDrink();
            assert(first.Heals.Count == 1 && first.HealVisuals == 0 && later.Heals.Count == 0,
                "New PvP policy must be checked after callbacks and before every recipient");
        });
    }
}
