using FXDSP.Core;

namespace FXDSP.DeliveryBootstrap
{
    internal static class PrototypeTranslation
    {
        internal static void Apply(Proto proto)
        {
            if (proto == null) return;
            var tech = proto as TechProto;
            if (tech != null && PrototypeOwnership.Owns("tech", tech.ID, tech))
            {
                var definition = TradeResearch.Find(tech.ID);
                if (definition == null) return;
                SetLanguage();
                tech.name = TextLocalization.Translate(definition.Name);
                tech.description = TextLocalization.Translate(definition.Description);
                tech.conclusion = TextLocalization.Translate(definition.Conclusion);
                // Icon mappings keep object references; update only our friendly names, never UVs.
                var mapping = Configs.builtin == null ? null : Configs.builtin.generalIconMapping;
                if (mapping != null && mapping.iconMappings != null)
                    foreach (var icon in mapping.iconMappings)
                        if (icon != null && icon.tag == definition.InlineIconTag) icon.name = tech.name;
                return;
            }
            foreach (var definition in DeliveryRecipes.Definitions)
            {
                if (proto.ID != definition.ItemId) continue;
                var item = proto as ItemProto;
                if (item != null && PrototypeOwnership.Owns("item", item.ID, item))
                {
                    SetLanguage();
                    item.name = TextLocalization.Translate(definition.Name);
                    item.description = TextLocalization.Translate(definition.Description);
                }
                var recipe = proto as RecipeProto;
                if (recipe != null && PrototypeOwnership.Owns("recipe", recipe.ID, recipe))
                {
                    SetLanguage();
                    recipe.name = TextLocalization.Translate(definition.Name);
                    recipe.description = TextLocalization.Translate(definition.Description);
                }
            }
        }
        internal static void RefreshAll()
        {
            if (LDB.techs != null)
                foreach (var definition in TradeResearch.Definitions) Apply(LDB.techs.Select(definition.Id));
            if (LDB.items == null || LDB.recipes == null) return;
            foreach (var definition in DeliveryRecipes.Definitions)
            { Apply(LDB.items.Select(definition.ItemId)); Apply(LDB.recipes.Select(definition.ItemId)); }
        }
        private static void SetLanguage() { TextLocalization.English = Localization.Loaded && !Localization.isZHCN; }
    }
}
