using System;

namespace FXDSP.Core
{
    public static class BuildingExchangeRules
    {
        public const decimal FeeRate = .10m;
        public static decimal Fee(long gross, decimal feeRate = FeeRate)
        {
            if (gross <= 0 || feeRate < 0m || feeRate >= 1m) throw new InvalidOperationException("兑换金额或费率无效。");
            return decimal.Ceiling(gross * feeRate * 1000000m) / 1000000m;
        }
        public static bool IsEligible(int itemId, bool canBuild)
        {
            return canBuild || itemId == 1131 || itemId == 5001 || itemId == 5002 || itemId == 5003
                || itemId == 5101 || itemId == 5102 || itemId == 5103 || itemId == 5111 || itemId == 5112;
        }

        // Both exchange directions use the same price generated from the audited production graph.
        public static long UnitValue(int itemId)
        {
            long price;
            if (!BuildingValues.TryGet(itemId, out price)) throw new InvalidOperationException("该物品尚未设定建筑币兑换价值。");
            return price;
        }

        public static long Cost(long unitValue, int quantity)
        {
            if (unitValue <= 0 || quantity <= 0) throw new InvalidOperationException("兑换数量无效。");
            return checked(unitValue * quantity);
        }
    }
}
