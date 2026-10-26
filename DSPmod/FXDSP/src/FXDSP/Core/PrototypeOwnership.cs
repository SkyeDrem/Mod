using System;

namespace FXDSP.Core
{
    // Only native prototype references cross the bootstrap / ScriptEngine boundary.
    // A numeric ID alone cannot establish ownership when another mod uses the same ID.
    public static class PrototypeOwnership
    {
        private static string Key(string kind, int id) { return "FXDSP.PrototypeOwner.v1." + kind + "." + id; }
        public static void Mark(string kind, int id, object prototype)
        {
            if (prototype == null) throw new ArgumentNullException(nameof(prototype));
            AppDomain.CurrentDomain.SetData(Key(kind, id), prototype);
        }
        public static bool Owns(string kind, int id, object prototype)
        { return prototype != null && ReferenceEquals(AppDomain.CurrentDomain.GetData(Key(kind, id)), prototype); }
    }
}
