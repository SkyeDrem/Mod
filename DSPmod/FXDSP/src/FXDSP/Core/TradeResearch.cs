using System;
using System.Collections.Generic;
using System.Linq;

namespace FXDSP.Core
{
    public sealed class TradeTechnology
    {
        public int Id, Previous, Cost, IconItemId, Level, ExchangeTier;
        public string Name, Description, Conclusion, UnlockText;
        public int[] Items;
        public bool Upgrade;
        // The native text icon codec only accepts tags of at most six characters.
        public string InlineIconTag { get { return "fx" + Id.ToString(System.Globalization.CultureInfo.InvariantCulture); } }
        // LDBTool binds IDs by Name before adding protos. Keep that registration key unique.
        public string RegistrationName { get { return Upgrade ? Name + " " + Level + " 级" : Name; } }
        public bool ImplicitPrevious { get { return Upgrade && Previous == TradeResearch.AccessTech; } }
        public long HashNeeded { get { return Cost * 36L; } }
        public int[] ItemPoints { get { return Items.Select(item => 100).ToArray(); } }
    }

    public sealed class TradeResearchState
    {
        public bool Access;
        public int FeeLevel;
        public readonly bool[] Exchange = new bool[6];
        public decimal TradingFee { get { return TradeResearch.TradingFee(FeeLevel); } }
        public decimal ExchangeFee { get { return TradeResearch.ExchangeFee(FeeLevel); } }
        public bool CanExchange(int tier) { return Access && tier >= 1 && tier <= 5 && Exchange[tier]; }
        public void RequireAccess() { if (!Access) throw new InvalidOperationException("请先研究“交易系统”。"); }
        public void RequireExchange(int tier)
        {
            RequireAccess();
            if (!CanExchange(tier)) throw new InvalidOperationException("请先研究“建筑兑换（" + tier + "）”。");
        }
    }

    public static class TradeResearch
    {
        public const int AccessTech = 1951;
        public static int ExchangeTech(int tier) { return 7700 + tier; }
        public static int FeeTech(int level) { return 7800 + level; }
        private static readonly decimal[] TradingRates = { .003m, .002m, .001m, .0005m, .0002m, .0001m, 0m };
        private static readonly decimal[] ExchangeRates = { .10m, .10m, .08m, .06m, .04m, .02m, 0m };
        public static decimal TradingFee(int level) { return TradingRates[Math.Max(0, Math.Min(6, level))]; }
        public static decimal ExchangeFee(int level) { return ExchangeRates[Math.Max(0, Math.Min(6, level))]; }
        public static TradeResearchState Read(Func<int, bool> unlocked)
        {
            var state = new TradeResearchState { Access = unlocked(AccessTech) };
            for (int tier = 1; tier <= 5; tier++) state.Exchange[tier] = unlocked(ExchangeTech(tier));
            // Respect the entire upgrade chain even if a debug tool unlocks an isolated late node.
            for (int level = 1; level <= 6 && state.Access && unlocked(FeeTech(level)); level++) state.FeeLevel = level;
            return state;
        }
        public static IReadOnlyList<TradeTechnology> Definitions { get; } = Build();
        public static TradeTechnology Find(int id) { return Definitions.FirstOrDefault(definition => definition.Id == id); }
        private static IReadOnlyList<TradeTechnology> Build()
        {
            var list = new List<TradeTechnology> {
                new TradeTechnology { Id = AccessTech, Name = "交易系统", UnlockText = "开放DSP交易平台",
                    Description = "老实拍工厂很枯燥吧？创建DSP交易账户，接入主脑交易平台，FX战士伊卡洛斯！",
                    Conclusion = "开放DSP交易平台", Items = new[] { 1001, 1002 }, Cost = 10, IconItemId = 1002 }
            };
            int[] costs = { 200, 400, 1000, 2000, 4000 };
            for (int tier = 1; tier <= 5; tier++)
                list.Add(new TradeTechnology { Id = ExchangeTech(tier), Previous = tier == 1 ? AccessTech : ExchangeTech(tier - 1), Upgrade = true, Level = tier, ExchangeTier = tier,
                    Name = "建筑兑换", Description = "在建筑币市场中自由交易兑换各种建筑，或许也是一种建筑超市。",
                    UnlockText = "开放建筑币市场" + tier + "级建筑交易", Conclusion = "开放建筑币市场" + tier + "级建筑交易",
                    Items = new[] { 6000 + tier }, Cost = costs[tier - 1], IconItemId = 6000 + tier });
            int[] feeCosts = { 400, 800, 1200, 1600, 2000, 4000 };
            for (int level = 1; level <= 6; level++)
            {
                string result = "交易手续费降低至" + (TradingFee(level) * 100m).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)
                    + "%\n建筑兑换双向手续费降低至" + (ExchangeFee(level) * 100m).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + "%";
                list.Add(new TradeTechnology { Id = FeeTech(level), Previous = level == 1 ? AccessTech : FeeTech(level - 1), Upgrade = true, Level = level,
                    Name = "交易效率", UnlockText = result, Conclusion = result,
                    Description = level == 6 ? "感谢你对主脑作出的贡献，免除交易手续费！" : "为奖励伊卡洛斯上传矩阵提供的算力，降低交易佣金。",
                    Items = level == 6 ? new[] { 6006 } : Enumerable.Range(6001, level).ToArray(), Cost = feeCosts[level - 1], IconItemId = level == 6 ? 6006 : 6000 + level });
            }
            return list.AsReadOnly();
        }
    }
}
