using System;
using System.Linq;
using System.Reflection;
using FXDSP;
using FXDSP.Runtime;
using HarmonyLib;

internal static class Program
{
    private static int passed;
    private static void Main()
    {
        Check("no active runtime leaves all native checks in control", () => {
            Plugin.Active = null;
            bool result = false;
            Require(AbnormalityStatusPatch.Prefix(ref result) && !result);
            Require(AbnormalityRecordPatch.Prefix() && AbnormalityTickPatch.Prefix());
        });
        Check("disabled setting preserves both native query outcomes and event checks", () => {
            Plugin.Active = new Plugin { BypassAbnormalityChecks = false };
            foreach (bool initial in new[] { false, true }) {
                bool result = initial;
                Require(AbnormalityStatusPatch.Prefix(ref result) && result == initial);
            }
            Require(AbnormalityRecordPatch.Prefix() && AbnormalityTickPatch.Prefix());
        });
        Check("enabled setting reports normal and skips new evidence and tick detection", () => {
            Plugin.Active = new Plugin { BypassAbnormalityChecks = true };
            bool result = false;
            Require(!AbnormalityStatusPatch.Prefix(ref result) && result);
            Require(!AbnormalityRecordPatch.Prefix() && !AbnormalityTickPatch.Prefix());
        });
        Check("setting changes and runtime removal restore checks immediately", () => {
            Plugin.Active.BypassAbnormalityChecks = false;
            Require(AbnormalityRecordPatch.Prefix());
            Plugin.Active.BypassAbnormalityChecks = true;
            Require(!AbnormalityTickPatch.Prefix());
            Plugin.Active = null;
            bool result = false;
            Require(AbnormalityStatusPatch.Prefix(ref result) && !result && AbnormalityTickPatch.Prefix());
        });
        Check("patches target only abnormality APIs, not save export or achievement eligibility", () => {
            var patches = new[] { typeof(AbnormalityStatusPatch), typeof(AbnormalityRecordPatch), typeof(AbnormalityTickPatch) }
                .Select(t => t.GetCustomAttribute<HarmonyPatch>()).ToArray();
            Require(patches.All(p => p.Target == typeof(ABN.GameAbnormalityData_0925) || p.Target == typeof(AbnormalityLogic)));
            Require(patches.Select(p => p.Method).SequenceEqual(new[] { "NothingAbnormal", "TriggerAbnormality", "GameTick" }));
        });
        Console.WriteLine("All " + passed + " compatibility checks passed.");
    }
    private static void Require(bool value) { if (!value) throw new Exception("Compatibility check failed."); }
    private static void Check(string name, Action action) { action(); passed++; Console.WriteLine("PASS " + name); }
}
