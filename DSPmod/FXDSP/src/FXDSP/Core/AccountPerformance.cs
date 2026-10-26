namespace FXDSP.Core
{
    public static class AccountPerformance
    {
        public static long HeldCandles(long openTick, long currentTick)
        { return openTick < 0 || currentTick <= openTick ? 0 : (currentTick - openTick) / MarginMarket.QuoteIntervalTicks; }

        public static decimal? UnrealizedReturnRatio(decimal unrealizedPnL, decimal margin)
        { return margin > 0 ? unrealizedPnL / margin : (decimal?)null; }

        public static decimal? ReturnRatio(decimal realizedPnL, long deposited, long withdrawn)
        {
            decimal netInvestment = (decimal)deposited - withdrawn;
            return netInvestment > 0 ? realizedPnL / netInvestment : (decimal?)null;
        }
    }
}
