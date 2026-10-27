using System;

// Small host contracts let the production prefixes run outside Unity.
// Native target existence and visibility are checked against the game DLL at build verification.
namespace FXDSP
{
    internal sealed class Plugin
    {
        internal static Plugin Active;
        internal bool BypassAbnormalityChecks;
    }
}
namespace HarmonyLib
{
    [AttributeUsage(AttributeTargets.Class)]
    internal sealed class HarmonyPatch : Attribute
    {
        internal Type Target;
        internal string Method;
        public HarmonyPatch(Type target, string method) { Target = target; Method = method; }
    }
}
namespace ABN
{
    public class GameAbnormalityData_0925
    {
        public bool NothingAbnormal() { throw new NotSupportedException(); }
        public void TriggerAbnormality(int id, int severity, long[] evidence) { throw new NotSupportedException(); }
    }
}
public class AbnormalityLogic
{
    public void GameTick(long time) { throw new NotSupportedException(); }
}
