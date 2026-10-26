using FXDSP.Core;

namespace FXDSP.Runtime
{
    internal static class ResearchProgression
    {
        internal static TradeResearchState Read()
        { return TradeResearch.Read(id => GameMain.data != null && GameMain.history != null && LDB.techs != null
            && PrototypeOwnership.Owns("tech", id, LDB.techs.Select(id)) && GameMain.history.TechUnlocked(id)); }
    }
}
