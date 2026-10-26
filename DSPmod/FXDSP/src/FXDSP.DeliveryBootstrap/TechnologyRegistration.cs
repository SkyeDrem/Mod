using System;
using System.Linq;
using FXDSP.Core;
using UnityEngine;
using xiaoye97;

namespace FXDSP.DeliveryBootstrap
{
    internal static class TechnologyRegistration
    {
        internal static void Register()
        {
            var native = LDB.techs.dataArray;
            float upgradeY = native.Where(tech => tech.ID >= 2000).Min(tech => tech.Position.y) - 4;
            var battlefield = LDB.techs.Select(1826);
            if (battlefield == null) throw new InvalidOperationException("Cannot locate battlefield analysis technology.");
            var accessPosition = new Vector2(battlefield.Position.x - 4, battlefield.Position.y);
            float upgradeX = 9;
            int exchangeIndex = 0, feeIndex = 0;
            foreach (var definition in TradeResearch.Definitions)
            {
                if (LDB.techs.Select(definition.Id) != null) throw new InvalidOperationException("FXDSP technology ID conflict: " + definition.Id);
                var icon = LDB.items.Select(definition.IconItemId);
                var tech = new TechProto {
                    ID = definition.Id, Name = definition.RegistrationName, name = definition.Name, Desc = definition.Description, Conclusion = definition.Conclusion,
                    Published = true, Level = definition.Level, MaxLevel = definition.Level, IsLabTech = definition.Items.Any(id => id >= 6001 && id <= 6006),
                    IconPath = icon.IconPath, IconTag = definition.InlineIconTag, Items = definition.Items, ItemPoints = definition.ItemPoints, HashNeeded = definition.HashNeeded,
                    PreTechs = definition.Previous == 0 || definition.ImplicitPrevious ? Array.Empty<int>() : new[] { definition.Previous },
                    PreTechsImplicit = definition.ImplicitPrevious ? new[] { definition.Previous } : Array.Empty<int>(), PreItem = Array.Empty<int>(),
                    UnlockRecipes = Array.Empty<int>(), UnlockFunctions = Array.Empty<int>(), UnlockValues = Array.Empty<double>(),
                    AddItems = Array.Empty<int>(), AddItemCounts = Array.Empty<int>(), PropertyOverrideItems = Array.Empty<int>(), PropertyItemCounts = Array.Empty<int>(),
                    Position = !definition.Upgrade ? accessPosition : definition.ExchangeTier > 0
                        ? new Vector2(upgradeX + exchangeIndex++ * 4, upgradeY)
                        : new Vector2(upgradeX + feeIndex++ * 4, upgradeY - 4)
                };
                LDBTool.PostAddProto(tech);
                if (tech.ID != definition.Id) throw new InvalidOperationException("FXDSP technology IDs must remain stable.");
                PrototypeOwnership.Mark("tech", tech.ID, tech);
                // Binding is synchronous. Rename only after LDBTool has retained each unique ID key.
                tech.Name = definition.Name;
            }
        }
        internal static void Finalize(Proto proto)
        {
            var tech = proto as TechProto;
            if (tech == null) return;
            bool ours = PrototypeOwnership.Owns("tech", tech.ID, tech);
            if (ours) tech.Preload();
            // Native logistics techs cache unlockRecipeArray before post-added delivery recipes exist.
            // Rebuild the cache as well as the numeric recipe IDs, so the extra unlock icon can be displayed.
            bool deliveryRegistered = DeliveryRecipes.Definitions.Any(definition =>
                PrototypeOwnership.Owns("recipe", definition.ItemId, LDB.recipes.Select(definition.ItemId)));
            if (ours || deliveryRegistered && (tech.ID == 1604 || tech.ID == 1605))
            {
                tech.UnlockRecipes = tech.UnlockRecipes.Distinct().ToArray();
                tech.Preload2();
            }
            if (ours) TechnologyIcons.Apply(tech);
            if (ours) PrototypeTranslation.Apply(tech);
        }
    }
}
