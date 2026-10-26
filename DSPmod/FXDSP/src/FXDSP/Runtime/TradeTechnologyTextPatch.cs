using FXDSP.Core;
using HarmonyLib;

namespace FXDSP.Runtime
{
    [HarmonyPatch(typeof(TechProto), nameof(TechProto.UnlockFunctionText))]
    internal static class TradeTechnologyTextPatch
    {
        private static bool Prefix(TechProto __instance, ref string __result)
        {
            var definition = TradeResearch.Find(__instance.ID);
            if (definition == null || !PrototypeOwnership.Owns("tech", __instance.ID, __instance)) return true;
            // Return before native node layout measures text so multi-line effects size normally.
            __result = TextLocalization.Translate(definition.UnlockText);
            return false;
        }
    }
}
