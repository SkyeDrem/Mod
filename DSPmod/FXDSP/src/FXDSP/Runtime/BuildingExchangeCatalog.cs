using System.Collections.Generic;
using System.Linq;
using FXDSP.Core;

namespace FXDSP.Runtime
{
    internal static class BuildingExchangeCatalog
    {
        private static readonly List<ExchangeOffer> Offers = new List<ExchangeOffer>();
        internal static void InvalidateLanguage() { Offers.Clear(); }
        internal static IReadOnlyList<ExchangeOffer> GetOffers()
        {
            if (Offers.Count > 0 || LDB.items == null) return Offers;
            var tiers = new TechnologyTier(id => {
                var tech = LDB.techs.Select(id);
                return tech == null ? null : new TechnologyRequirements { Items = tech.Items,
                    Prerequisites = (tech.PreTechs ?? new int[0]).Concat(tech.PreTechsImplicit ?? new int[0]).ToArray() };
            });
            foreach (var item in LDB.items.dataArray)
            {
                if (item == null || DeliveryBuildings.Contains(item.ID) || !BuildingExchangeRules.IsEligible(item.ID, item.CanBuild)) continue;
                long unitValue;
                if (!BuildingValues.TryGet(item.ID, out unitValue)) continue;
                if (item.preTech == null) item.FindPreTech();
                Offers.Add(new ExchangeOffer { ItemId = item.ID, Name = item.name, Icon = item.iconSprite,
                    UnitValue = unitValue,
                    Tier = item.ID == 2902 ? 4 : tiers.Resolve(item.preTech == null ? item.PreTechOverride : item.preTech.ID) });
            }
            Offers.Sort((a, b) => a.ItemId.CompareTo(b.ItemId));
            return Offers;
        }

        internal static ExchangeOffer Find(int itemId)
        {
            foreach (var offer in GetOffers()) if (offer.ItemId == itemId) return offer;
            throw new System.InvalidOperationException("该物品不能兑换建筑币。");
        }
    }
}
