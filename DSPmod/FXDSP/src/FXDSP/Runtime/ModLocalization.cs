using FXDSP.Core;
using FXDSP.UI;

namespace FXDSP.Runtime
{
    internal static class ModLocalization
    {
        private static int _language = int.MinValue;
        private static bool _namesReady;
        internal static bool Update()
        {
            if (!Localization.Loaded) return false;
            int language = Localization.CurrentLanguageLCID;
            bool changed = _language != language;
            if (changed)
            {
                _language = language; _namesReady = false;
                TextLocalization.English = !Localization.isZHCN;
                ResourceMarketCatalog.InvalidateLanguage();
                BuildingExchangeCatalog.InvalidateLanguage();
            }
            if (TextLocalization.English && !_namesReady && GameMain.isRunning && GameMain.data != null
                && LDB.items != null && LDB.items.dataArray != null)
            {
                foreach (var item in LDB.items.dataArray)
                    if (item != null) TextLocalization.RegisterNativeName(item.Name, item.name);
                _namesReady = true;
            }
            if (changed) LocalizedText.RefreshLanguage();
            return changed;
        }
    }
}
