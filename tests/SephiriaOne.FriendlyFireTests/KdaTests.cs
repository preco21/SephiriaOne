using SephiriaOne;

internal static class KdaTests
{
    internal static void Run(Action<bool, string> check)
    {
        FriendlyFireRuntime.Clear();
        SessionSettings.FriendlyFireForHit = new(true, 100);
        var a = new PlayerAvatar { Name = "A" };
        var b = new PlayerAvatar { Name = "B" };
        var c = new PlayerAvatar { Name = "C" };
        void Hit(PlayerAvatar target, UnitAvatar source, float damage) => target.ApplyDamage(new() { origin = source, damage = damage });
        Hit(b, c, 10); Hit(b, c, 10); Hit(b, a, 200);
        check(DungeonManager.Instance.Messages.Last() == "Friendly fire: A(1/0/0) killed B(0/1/0).", "KDA notice includes updated killer and victim totals");
        Hit(a, c, 200);
        check(DungeonManager.Instance.Messages.Last() == "Friendly fire: C(1/0/1) killed A(1/1/0).", "Repeated damage awards one assist and final blow never awards an assist");

        string Score(PlayerAvatar player) => FriendlyFireKda.Label(player, player.Name);
        void Reset()
        {
            SessionSettings.FriendlyFireForHit = default;
            SessionSettings.FriendlyFireForHit = new(true, 100);
            a = new() { Name = "A" }; b = new() { Name = "B" }; c = new() { Name = "C" };
        }
        foreach (string blocked in new[] { "guard", "invulnerable", "zero", "off", "nan", "negative" })
        {
            Reset();
            b.IsGuarding = blocked == "guard"; b.IsInvulnerable = blocked == "invulnerable";
            if (blocked == "zero") SessionSettings.FriendlyFireForHit = new(true, 0);
            if (blocked == "off") SessionSettings.FriendlyFireForHit = default;
            Hit(b, c, blocked == "nan" ? float.NaN : blocked == "negative" ? -1 : 10);
            b.IsGuarding = b.IsInvulnerable = false; SessionSettings.FriendlyFireForHit = new(true, 100);
            Hit(b, a, 200);
            check(Score(c) == "C(0/0/0)", "No assist from " + blocked);
        }
        foreach (bool mpShield in new[] { false, true })
        {
            Reset(); b.Shield = mpShield ? 0 : 10; b.IsMpShield = mpShield; b.Mp = mpShield ? 10 : 0;
            Hit(b, c, 10); Hit(b, a, 200);
            check(Score(c) == "C(0/0/1)", "Absorbed shield damage earns one assist, MP=" + mpShield);
        }
        Reset(); Hit(b, c, 10); b.ExtraLife = true; Hit(b, a, 200);
        check(Score(a) == "A(0/0/0)" && Score(b) == "B(0/0/0)", "Extra life grants no kill or death");
        Hit(b, a, 200); b.Die(5, new() { origin = a });
        check(Score(a) == "A(1/0/0)" && Score(c) == "C(0/0/1)" && Score(b) == "B(0/1/0)", "Final death counts once with contributors from before extra life");
        b.Revive(100); Hit(b, a, 200);
        check(Score(c) == "C(0/0/1)" && Score(b) == "B(0/2/0)", "Revival starts an empty assist life");
        Reset(); Hit(b, c, 10); b.Die(5, new() { origin = new UnitAvatar { faction = "enemy" } }); b.Revive(100); Hit(b, a, 200);
        check(Score(c) == "C(0/0/0)" && Score(b) == "B(0/1/0)", "Environmental death clears assists without a PvP death");
        Reset(); Hit(b, c, 10); b.IsDead = true; b.Revive(100); Hit(b, a, 200);
        check(Score(c) == "C(0/0/0)", "Reviving after ForceDie cannot retain old-life assists");
        Reset(); var npc = new UnitAvatar { faction = "friends" }; npc.ApplyDamage(new() { origin = a, damage = 200 });
        check(Score(a) == "A(0/0/0)" && DungeonManager.Instance.Messages.Last().Contains("A(0/0/0)"), "NPC kill log displays player totals without awarding a player kill");
        var companion = new UnitAvatar { NetworkLeader = c }; Hit(b, companion, 10); Hit(b, a, 200);
        check(Score(c) == "C(0/0/1)", "Companion assist credits its owner");
        b.Revive(100); Hit(b, companion, 200);
        check(Score(c) == "C(1/0/1)", "Companion final blow credits its owner");
        Reset(); b.ApplyDamage(new() { origin = c, damage = 10, damageType = EDamageType.ElementalEffectDamage }); Hit(b, a, 200);
        check(Score(c) == "C(0/0/1)", "Damage-over-time ticks contribute assists");
        Reset(); var d = new PlayerAvatar { Name = "D" }; Hit(b, c, 10); Hit(b, d, 10); Hit(b, a, 200);
        check(Score(c) == "C(0/0/1)" && Score(d) == "D(0/0/1)", "All distinct contributors receive an assist");
        Reset(); b.IsParrying = true;
        b.OnParry = _ => a.ApplyDamage(new() { origin = b, damage = 200, id = "Weapon_Reflect", fromType = EDamageFromType.None });
        Hit(b, a, 10);
        check(Score(b) == "B(1/0/0)" && Score(a) == "A(0/1/0)", "Lethal reflection credits the reflecting player without credit for the parried attack");
        Reset(); Hit(b, c, 10); SessionSettings.FriendlyFireForHit = new(true, 50); Hit(b, a, 200);
        check(Score(c) == "C(0/0/1)", "Changing only damage percent preserves contributions");
        SessionSettings.FriendlyFireForHit = default;
        check(Score(a) == "A(0/0/0)" && Score(b) == "B(0/0/0)" && Score(c) == "C(0/0/0)", "Disabling immediately clears every score");
        SessionSettings.FriendlyFireForHit = new(true, 100); b.Revive(100); Hit(b, a, 200);
        check(Score(a) == "A(1/0/0)" && Score(c) == "C(0/0/0)", "Re-enabling starts clean");
        Reset(); Hit(b, c, 10);
        b.OnHit = (_, _) => { SessionSettings.FriendlyFireForHit = default; SessionSettings.FriendlyFireForHit = new(true, 100); };
        Hit(b, a, 200);
        check(Score(a) == "A(0/0/0)" && Score(b) == "B(0/0/0)" && Score(c) == "C(0/0/0)", "Mid-hit toggle cannot repopulate reset scores");
        Reset(); Hit(b, c, 10); b.OnDeath = _ => throw new InvalidOperationException("death fixture");
        try { Hit(b, a, 200); } catch (InvalidOperationException) { }
        check(Score(a) == "A(1/0/0)" && Score(b) == "B(0/1/0)" && Score(c) == "C(0/0/1)", "Confirmed death records even if a later native callback throws");

        PlayerAvatar Account(ulong id, string name)
        {
            var player = new PlayerAvatar { Name = name };
            player.spawner = new PlayerSpawner { steamID = id, PlayerAvatar = player };
            return player;
        }
        Reset(); a = Account(1, "same"); b = Account(2, "same"); c = Account(3, "C");
        Hit(b, c, 10); Hit(b, a, 200); a.Destroyed = true; a = Account(1, "renamed");
        check(Score(a) == "renamed(1/0/0)" && Score(b) == "same(0/1/0)", "Stable account retains scores across reconnect, rename and duplicate nicknames");
        b = Account(2, "B again"); Hit(b, a, 200);
        check(Score(b) == "B again(0/2/0)" && Score(c) == "C(0/0/1)", "Rejoined victim receives a new empty life but retains totals");
        Reset(); Hit(b, c, 10); FriendlyFireKda.ClearLives(); Hit(b, a, 200);
        check(Score(a) == "A(1/0/0)" && Score(c) == "C(0/0/0)", "New run drops pending assists");
        b.Revive(100); Hit(b, c, 10); FriendlyFireKda.ForgetLife(b); Hit(b, a, 200);
        check(Score(a) == "A(2/0/0)" && Score(c) == "C(0/0/0)", "Departure drops pending victim assists and preserves totals");
        Reset(); Hit(b, c, 10); Mirror.NetworkClient.active = false; Hit(b, a, 200); Mirror.NetworkClient.active = true;
        check(Score(a) == "A(1/0/0)" && Score(c) == "C(0/0/1)", "Host scoring is independent of local chat availability");
        b.Revive(100); Hit(b, c, 1);
        long allocationStart = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 1000; i++) FriendlyFireKda.Damage(c, b, FriendlyFireKda.Epoch);
        check(GC.GetAllocatedBytesForCurrentThread() == allocationStart, "Warmed contribution tracking allocates zero bytes per hit");
        FriendlyFireRuntime.Clear();
        check(Score(a) == "A(0/0/0)", "Unload clears KDA");
    }
}
