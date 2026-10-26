using System;

namespace FXDSP.Core
{
    public static class ExchangeAmounts
    {
        public static int Maximum(decimal balance, long unitValue, decimal feeRate, int owned)
        {
            if (unitValue <= 0 || feeRate < 0 || feeRate >= 1 || owned < 0 || balance < 0)
                throw new InvalidOperationException("兑换数量无效。");
            decimal affordable = decimal.Floor(balance / (unitValue * (1 + feeRate)));
            int buy = (int)Math.Min(int.MaxValue, Math.Min(long.MaxValue / unitValue, affordable));
            if (buy > 0)
            {
                long gross = BuildingExchangeRules.Cost(unitValue, buy);
                if (gross + BuildingExchangeRules.Fee(gross, feeRate) > balance) buy--;
            }
            return Math.Max(buy, owned);
        }
    }
}
