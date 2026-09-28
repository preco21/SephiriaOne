using TMPro;

namespace SephiriaOne
{
    internal static class PanelFontResolver
    {
        // Borrow the game's Korean font only for addon-owned text. Do not change
        // the native language, font preferences, materials, or fallback asset lists.
        internal static TMP_FontAsset Resolve(TMP_FontAsset fallback)
        {
            if (L.Language != "ko") return fallback;
            var localization = LocalizationManager.Instance;
            if (!localization.Languages.Contains("ko-KR")) return fallback;
            string name = localization.GetText("ko-KR", "#FontName");
            return localization.fontAssets.TryGetValue(name, out TMP_FontAsset korean) && korean ? korean : fallback;
        }
    }
}
