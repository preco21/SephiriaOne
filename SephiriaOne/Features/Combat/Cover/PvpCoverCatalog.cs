namespace SephiriaOne
{
    // Audited stock props only. Physics dimensions are part of the compatibility
    // contract: placement reserves a 3x3 bypass and a 1.85x1.5 occupied footprint.
    internal static class PvpCoverCatalog
    {
        internal readonly struct Entry
        {
            internal readonly string Id;
            internal readonly uint AssetId;
            internal readonly float Width, Height, OffsetY;
            internal Entry(string id, uint assetId, float width, float height, float offsetY)
            { Id = id; AssetId = assetId; Width = width; Height = height; OffsetY = offsetY; }
        }

        internal static readonly Entry[] Entries =
        {
            new Entry("DomitoryBarrel_0", 4148004312, 0.875f, 0.875f, 0.4375f),
            new Entry("DomitoryBarrel_2", 3412844843, 0.875f, 1, 0.5f),
            new Entry("DomitoryBarrel_3_Potato", 3401681619, 0.875f, 0.875f, 0.4375f),
            new Entry("DomitoryBarrel_4_Carrot", 1484013372, 0.875f, 0.875f, 0.4375f),
            new Entry("DomitoryTable_1", 2839126442, 1.35f, 0.8f, 0.4f)
        };
    }
}
