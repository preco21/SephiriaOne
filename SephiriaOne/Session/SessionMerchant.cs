using Mirror;
using System.Collections.Generic;

namespace SephiriaOne
{
    internal static partial class SessionSettings
    {
        internal static bool MerchantSpawnsForUse => EnsureResourceScope() && policy.MerchantSpawns;
        internal static int MerchantSpawnChanceForUse => EnsureResourceScope() ? policy.MerchantSpawnChance : MerchantCommand.DefaultChance;
        internal static bool AnyMerchantSpawnsForUse => EnsureResourceScope() && policy.Merchants.AnyEnabled;
        internal static MerchantSettings GetMerchantSettingsForUse(string id) => EnsureResourceScope() ? policy.Merchants.Get(id) :
            MerchantSettings.Defaults(MerchantCatalog.Find(id) ?? MerchantCatalog.Find(MerchantCatalog.DefaultId));

        public static bool TryExecuteMerchant(MerchantCommand command, out string message)
        {
            message = L.T("Only the host can change merchant settings.");
            if (!NetworkServer.active) return false;
            if (!command.IsReset && !MerchantFeature.Available)
            { message = L.T("Merchant compatibility checks failed. Off/reset remain available; see Player.log."); return false; }
            if (!PrepareCommand("merchant", command.IsReset, out HostCommandContext context, out message)) return false;
            if (!Commit("merchant", context.CreateBatch(), () => policy.Record(command), out message)) return false;
            MerchantFeature.Refresh();
            message = DescribeMerchant(MerchantCatalog.Find(command.TypeId), policy.Merchants.Get(command.TypeId));
            return true;
        }

        internal static string DescribeMerchant(MerchantDefinition definition, MerchantSettings settings) =>
            L.F("{0}: {1}; chance {2}%; first eligible floor {3}; per-run limit {4} (0 = unlimited); guarantee {5}.", L.T(definition.Name),
                L.T(settings.Enabled ? "on" : "off"), settings.Chance, settings.FirstFloor, settings.MaxPerRun,
                L.T(settings.Guarantee && definition.HasGuarantee ? "on" : "off")) + " " +
            (settings.Guarantee && definition.HasGuarantee ? L.T("Each enabled type has its own guaranteed encounter on a randomly selected eligible floor, including the first when allowed by its conditions. Other eligible floors roll before and after the guarantee.") :
                L.T("Eligible floors use this type's configured chance.")) + " " +
            L.T("At most one of each type per map; different types can coexist. First eligible floor and base HP scaling use the main dungeon stage number, not the number of maps visited. HP retains native stage and multiplayer bonuses. They are hostile, cannot talk, and have no negotiation/crime penalty. Off/reset stop future spawns; existing added merchants stay exempt until floor teardown.");

        internal static string DescribeMerchants(IReadOnlyDictionary<string, MerchantSettings> settings, string selectedId = null)
        {
            var lines = new List<string>();
            foreach (var definition in MerchantCatalog.All)
                if (selectedId == null || definition.Id == selectedId)
                    lines.Add(DescribeMerchant(definition, settings == null ? MerchantSettings.Defaults(definition) : settings[definition.Id]));
            return string.Join("\n", lines);
        }
    }
}
