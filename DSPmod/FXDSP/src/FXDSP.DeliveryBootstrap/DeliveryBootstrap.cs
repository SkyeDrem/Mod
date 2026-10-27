using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx;
using xiaoye97;
using FXDSP.Core;
using HarmonyLib;

namespace FXDSP.DeliveryBootstrap
{
    [BepInPlugin("dspfx.delivery.prototypes", "FXDSP Delivery Buildings", "1.0.0")]
    [BepInDependency("me.xiaoye97.plugin.Dyson.LDBTool", "3.0.3")]
    public sealed class DeliveryBootstrap : BaseUnityPlugin
    {
        private static readonly int[] Ids = { 11702, 11703 };
        private static readonly int[] Bases = { 2103, 2104 };
        private Harmony _harmony;
        private bool _registered;
        private int _language = int.MinValue;
        private static readonly MethodInfo CloneMethod = typeof(object).GetMethod("MemberwiseClone", BindingFlags.Instance | BindingFlags.NonPublic);
        private static T Clone<T>(T source) { return (T)CloneMethod.Invoke(source, null); }
        private void Awake()
        {
            _harmony = new Harmony("dspfx.delivery.prototypes.localization");
            _harmony.PatchAll(typeof(DeliveryBootstrap).Assembly);
            LDBTool.PostAddDataAction += Register;
            LDBTool.EditDataAction += FinalizePrototype;
            Logger.LogInfo("Delivery prototypes scheduled; reloadable trading logic remains in scripts/FXDSP.dll.");
        }
        private void Register()
        {
            // Check the complete ID set before enqueuing any of our prototypes.
            foreach (int id in Ids)
                if (LDB.items.Select(id) != null || LDB.recipes.Select(id) != null)
                    throw new InvalidOperationException("FXDSP delivery prototype ID conflict: " + id);
            foreach (var definition in TradeResearch.Definitions)
                if (LDB.techs.Select(definition.Id) != null)
                    throw new InvalidOperationException("FXDSP technology ID conflict: " + definition.Id);
            try { TechnologyIcons.Load(); Logger.LogInfo("Loaded 12 custom FXDSP technology icons."); }
            catch (Exception exception) { Logger.LogError("Custom technology icons unavailable; using native fallback icons: " + exception); }
            TechnologyRegistration.Register();
            var occupied = new HashSet<int>(LDB.recipes.dataArray.Select(recipe => recipe.GridIndex)
                .Concat(LDB.items.dataArray.Select(item => item.GridIndex)));
            foreach (var definition in DeliveryRecipes.Definitions)
            {
                int id = definition.ItemId;
                if (LDB.items.Select(id) != null || LDB.recipes.Select(id) != null)
                    throw new InvalidOperationException("FXDSP delivery prototype ID conflict: " + id);
                var original = LDB.items.Select(definition.BaseItemId);
                original.FindPreTech();
                var item = Clone(original);
                item.ID = id; item.Name = definition.Name; item.name = definition.Name;
                item.Description = item.description = definition.Description;
                int grid = DeliveryRecipes.AllocateGrid(occupied);
                item.GridIndex = grid; item.BuildIndex = 0; item.Upgrades = new int[0];
                item.PreTechOverride = original.preTech == null ? 0 : original.preTech.ID;
                item.prefabDesc = Clone(original.prefabDesc);
                item.prefabDesc.stationMaxItemCount = 1000000;
                var recipe = new RecipeProto { ID = id, Name = definition.Name, name = definition.Name,
                    Description = definition.Description, Type = ERecipeType.Assemble, Handcraft = definition.Handcraft, Explicit = false,
                    TimeSpend = definition.TimeSpend, Items = new[] { definition.BaseItemId }, ItemCounts = new[] { definition.InputCount }, Results = new[] { id }, ResultCounts = new[] { 1 },
                    GridIndex = grid, IconPath = original.IconPath, preTech = original.preTech };
                item.maincraft = recipe; item.handcraft = null;
                item.recipes = new List<RecipeProto> { recipe }; item.handcrafts = new List<RecipeProto>();
                item.makes = new List<RecipeProto>(); item.rawMats = new List<IDCNTINC>();
                item.maincraftProductCount = 1; item.handcraftProductCount = 0;
                LDBTool.PostAddProto(item); LDBTool.PostAddProto(recipe);
                // Ensure both native UI entries remain visible if the loader has a hidden grid override.
                if (item.GridIndex <= 0) item.GridIndex = grid;
                if (recipe.GridIndex <= 0) recipe.GridIndex = grid;
                if (item.ID != id || recipe.ID != id) throw new InvalidOperationException("FXDSP delivery IDs must remain stable in LDBTool configuration.");
                PrototypeOwnership.Mark("item", id, item);
                PrototypeOwnership.Mark("recipe", id, recipe);
                if (original.preTech != null)
                    original.preTech.UnlockRecipes = original.preTech.UnlockRecipes.Concat(new[] { id }).Distinct().ToArray();
            }
            Logger.LogInfo("Registered two assembler-only FXDSP delivery towers.");
            _registered = true;
        }
        private static void FinalizePrototype(Proto proto)
        {
            TechnologyRegistration.Finalize(proto);
            var recipe = proto as RecipeProto;
            if (recipe != null && Ids.Contains(recipe.ID) && PrototypeOwnership.Owns("recipe", recipe.ID, recipe))
            {
                // Post-added recipes missed the game's initial preload. Initialize native icon, tooltip and array index after registration.
                recipe.Preload(Array.IndexOf(LDB.recipes.dataArray, recipe));
                PrototypeTranslation.Apply(recipe);
                return;
            }
            var item = proto as ItemProto;
            if (item == null) return;
            int index = Array.IndexOf(Ids, item.ID);
            if (index < 0 || !PrototypeOwnership.Owns("item", item.ID, item)) return;
            var original = LDB.items.Select(Bases[index]);
            item.DescFields = (int[])original.DescFields.Clone();
            item.HpMax = original.HpMax;
            item.prefabDesc = Clone(original.prefabDesc);
            item.prefabDesc.stationMaxItemCount = 1000000;
            PrototypeTranslation.Apply(item);
        }
        private void LateUpdate()
        {
            if (!_registered || !Localization.Loaded || _language == Localization.CurrentLanguageLCID) return;
            _language = Localization.CurrentLanguageLCID;
            PrototypeTranslation.RefreshAll();
        }
        private void OnDestroy()
        {
            LDBTool.PostAddDataAction -= Register; LDBTool.EditDataAction -= FinalizePrototype;
            if (_harmony != null) _harmony.UnpatchSelf();
        }
    }
}
