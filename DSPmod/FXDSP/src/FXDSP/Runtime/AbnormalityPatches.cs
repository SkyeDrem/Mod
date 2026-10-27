using HarmonyLib;

namespace FXDSP.Runtime
{
    // This is a local runtime override. Never clear saved abnormality evidence,
    // change achievement/account flags, or rewrite prototype signatures.
    internal static class AbnormalityBypass
    {
        internal static bool Enabled
        {
            get { return Plugin.Active != null && Plugin.Active.BypassAbnormalityChecks; }
        }
    }

    [HarmonyPatch(typeof(ABN.GameAbnormalityData_0925), nameof(ABN.GameAbnormalityData_0925.NothingAbnormal))]
    internal static class AbnormalityStatusPatch
    {
        internal static bool Prefix(ref bool __result)
        {
            if (!AbnormalityBypass.Enabled) return true;
            __result = true;
            return false;
        }
    }

    [HarmonyPatch(typeof(ABN.GameAbnormalityData_0925), nameof(ABN.GameAbnormalityData_0925.TriggerAbnormality))]
    internal static class AbnormalityRecordPatch
    {
        internal static bool Prefix() { return !AbnormalityBypass.Enabled; }
    }

    [HarmonyPatch(typeof(AbnormalityLogic), nameof(AbnormalityLogic.GameTick))]
    internal static class AbnormalityTickPatch
    {
        internal static bool Prefix() { return !AbnormalityBypass.Enabled; }
    }
}
