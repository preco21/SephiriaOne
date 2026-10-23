using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using SephiriaOne;

namespace SephiriaOne
{
    // Native installation contracts run against the installed assembly separately.
    internal static class FriendlyFireElementalHooks { internal static void Install(Harmony harmony) { } }
}

public partial class UnitAvatar
{
    public int[] ElementalValues = { 20, 20, 20, 20 };
    public bool FailElementalRead;
    [MethodImpl(MethodImplOptions.NoInlining)]
    public int ConvertedElementForTest(int element)
    {
        if (FailElementalRead) throw new InvalidOperationException("native stat failure");
        return ElementalValues[element];
    }
}

internal static class ElementalStatTests
{
    internal static void Run(Action<bool, string> check)
    {
        Mirror.NetworkServer.active = true;
        FriendlyFireFeature.Available = true;
        FriendlyFireParticipants.Clear();
        CombatMatchRuntime.ParticipantRule = null;
        SessionSettings.FriendlyFireForHit = new(true, 100, balance: true);
        var player = new PlayerAvatar { ElementalValues = new[] { 100, 150, 20, -10 } };
        var runtime = typeof(FriendlyFireSettings).Assembly.GetType("SephiriaOne.PvpElementalRuntime");
        var harmony = new Harmony("fixture.elemental-stats");
        if (runtime != null)
            harmony.Patch(AccessTools.Method(typeof(UnitAvatar), nameof(UnitAvatar.ConvertedElementForTest)),
                postfix: new HarmonyMethod(AccessTools.Method(runtime, "AfterRead")));
        try
        {
            check(player.ConvertedElementForTest(0) == 87, "Balanced participant elemental stat uses the default curve, floored at native integer boundary");
            check(player.ConvertedElementForTest(1) == 100 && player.ConvertedElementForTest(2) == 20 && player.ConvertedElementForTest(3) == -10,
                "Hard cap, native baseline and costume penalties are independent");
            check(PvpElementalRuntime.ReadNativeUnsafe(player, "PHYSICALDAMAGE") == 100 && player.ConvertedElementForTest(0) == 87,
                "A native derivation read retains original value without leaking into subsequent combat reads");
            check(PvpElementalRuntime.ReadNativeEnum(player, ECustomStat.FireDamage) == 150 && player.ConvertedElementForTest(1) == 100,
                "Native enum reads preserve permanent potion decisions");
            player.FailElementalRead = true;
            try { PvpElementalRuntime.ReadNativeUnsafe(player, "ICEDAMAGE"); } catch (InvalidOperationException) { }
            player.FailElementalRead = false;
            check(player.ConvertedElementForTest(0) == 87, "Exceptional native reads cannot leave the overlay bypassed");
            var original = player.ElementalValues.ToArray();
            SessionSettings.FriendlyFireForHit = new(true, 100, balance: true,
                elements: new(true, physical: new(20, 40), lightning: new(0, 0)));
            check(player.ConvertedElementForTest(0) == 40 && player.ConvertedElementForTest(1) == 100 && player.ConvertedElementForTest(3) == -10,
                "Live independently configured limits do not affect other elements or native penalties");
            player.ElementalValues[3] = 50;
            check(player.ConvertedElementForTest(3) == 0, "Explicit zero hard limit is enforced");
            player.ElementalValues[3] = original[3];
            SessionSettings.FriendlyFireForHit = new(true, 100, balance: true, elements: new(false));
            check(player.ConvertedElementForTest(0) == 100, "Disabling only elemental limits restores the native stat immediately");
            SessionSettings.FriendlyFireForHit = new(true, 100);
            check(player.ConvertedElementForTest(0) == 100, "Balancing off leaves ordinary PvP stats native");
            SessionSettings.FriendlyFireForHit = new(false, 100, balance: true);
            check(player.ConvertedElementForTest(0) == 100, "PvP off leaves PvE stats native");
            SessionSettings.FriendlyFireForHit = new(true, 100, balance: true);
            CombatMatchRuntime.ParticipantRule = p => false;
            check(player.ConvertedElementForTest(0) == 100, "Spectator or ended pair retains native stat");
            CombatMatchRuntime.ParticipantRule = null;
            var other = new PlayerAvatar();
            FriendlyFireParticipants.Select(other);
            check(player.ConvertedElementForTest(0) == 100, "Unselected players remain native");
            FriendlyFireParticipants.Select(player, other);
            check(player.ConvertedElementForTest(0) == 87, "Selecting an existing player takes effect without stat writes");
            FriendlyFireParticipants.Depart(player);
            check(player.ConvertedElementForTest(0) == 100, "Retired connection is immediately native");
            FriendlyFireParticipants.Rejoin(player);
            check(player.ConvertedElementForTest(0) == 100, "Rejoin does not retain previous explicit participation");
            FriendlyFireParticipants.Select(player, other);
            player.ElementalValues[0] = 60;
            check(player.ConvertedElementForTest(0) == 59, "Equipment/native stat changes use the live original value");
            player.ElementalValues[0] = original[0];
            var npc = new UnitAvatar { ElementalValues = original.ToArray() };
            check(npc.ConvertedElementForTest(0) == 100, "NPC stats remain native");
            Mirror.NetworkServer.active = false;
            check(player.ConvertedElementForTest(0) == 100, "Stock guest/local-only calculations are not authoritative overlays");
            Mirror.NetworkServer.active = true;
            player.isServer = false;
            check(player.ConvertedElementForTest(0) == 100, "Nonauthoritative avatar remains native");
            player.isServer = true;
            for (int i = 0; i < 20000; i++) player.ConvertedElementForTest(i % 4);
            long bytes = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 10000; i++) player.ConvertedElementForTest(i % 4);
            long allocated = GC.GetAllocatedBytesForCurrentThread() - bytes;
            Console.WriteLine("Elemental stat overlay: " + allocated + " bytes/10000 warmed reads");
            check(allocated == 0, "Warmed elemental stat overlay allocates no managed memory");
            check(original.SequenceEqual(player.ElementalValues), "Native values are never modified by the overlay");
        }
        finally
        {
            harmony.UnpatchAll(harmony.Id);
            SessionSettings.FriendlyFireForHit = default;
            FriendlyFireParticipants.Clear();
            CombatMatchRuntime.ParticipantRule = null;
        }
    }
}
