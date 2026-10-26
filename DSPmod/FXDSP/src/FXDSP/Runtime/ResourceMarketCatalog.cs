using System.Collections.Generic;
using FXDSP.Core;

namespace FXDSP.Runtime
{
    internal static class ResourceMarketCatalog
    {
        private static readonly List<MarketOption> Options = new List<MarketOption>();
        internal static void InvalidateLanguage() { Options.Clear(); }
        internal static IReadOnlyList<MarketOption> GetOptions()
        {
            if (Options.Count > 0 || LDB.items == null) return Options;
            foreach (var item in LDB.items.dataArray)
            {
                if (item == null || (!MarketCatalog.IsResource(item.ID) && !MarketCatalog.IsDarkFogDrop(item.ID)) || item.StackSize <= 0
                    || (!MarketCatalog.IsDarkFogDrop(item.ID) && (item.Type < EItemType.Resource || item.Type > EItemType.Product))
                    || BuildingExchangeRules.IsEligible(item.ID, item.CanBuild)) continue;
                bool consumable = item.CatalystType > 0 || (item.Type == EItemType.Product && item.FuelType > 0)
                    || item.ID == ItemProto.kWarperId || item.ID == ItemProto.kRocketId || item.ID == 1501;
                var category = MarketCatalog.ClassifyResource(item.ID, item.Type == EItemType.Resource, item.isAmmo || item.isBomb, consumable);
                if (MarketCatalog.IsResource(item.ID)) MarketCatalog.RegisterResourceName(item.ID, item.name, category);
                Options.Add(new MarketOption { ItemId = item.ID, Name = item.name, Icon = item.iconSprite });
            }
            for (int id = 6001; id <= 6006; id++)
            {
                var item = LDB.items.Select(id);
                if (item != null) Options.Add(new MarketOption { ItemId = id, Name = item.name, Icon = item.iconSprite });
            }
            Options.Sort((a, b) => a.ItemId.CompareTo(b.ItemId));
            return Options;
        }

        internal static void RequireResource(int id)
        {
            foreach (var option in GetOptions()) if (option.ItemId == id) return;
            throw new System.InvalidOperationException("该品种不在可选市场中。");
        }
    }
}
