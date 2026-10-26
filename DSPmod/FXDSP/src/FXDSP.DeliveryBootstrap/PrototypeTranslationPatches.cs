using HarmonyLib;

namespace FXDSP.DeliveryBootstrap
{
    [HarmonyPatch(typeof(TechProto), nameof(TechProto.RefreshTranslation))]
    internal static class TechnologyTranslationPatch
    { private static void Postfix(TechProto __instance) { PrototypeTranslation.Apply(__instance); } }
    [HarmonyPatch(typeof(ItemProto), nameof(ItemProto.RefreshTranslation))]
    internal static class ItemTranslationPatch
    { private static void Postfix(ItemProto __instance) { PrototypeTranslation.Apply(__instance); } }
    [HarmonyPatch(typeof(RecipeProto), nameof(RecipeProto.RefreshTranslation))]
    internal static class RecipeTranslationPatch
    { private static void Postfix(RecipeProto __instance) { PrototypeTranslation.Apply(__instance); } }
}
