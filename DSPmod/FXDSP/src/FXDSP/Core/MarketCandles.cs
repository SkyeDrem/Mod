using System;
using System.Collections.Generic;

namespace FXDSP.Core
{
    public sealed class MarketCandle
    {
        public long StartTick { get; internal set; }
        public decimal Open { get; internal set; }
        public decimal High { get; internal set; }
        public decimal Low { get; internal set; }
        public decimal Close { get; internal set; }
        public decimal DisplayHigh { get; internal set; }
        public decimal DisplayLow { get; internal set; }
    }

    public static class MarketCandles
    {
        public const long IntervalTicks = MarginMarket.QuoteIntervalTicks;

        public static IReadOnlyList<MarketCandle> Build(IReadOnlyList<PricePoint> prices)
        {
            var candles = new List<MarketCandle>();
            if (prices == null) return candles;
            PricePoint previous = null;
            foreach (var point in prices)
            {
                // One visual candle per actual quote. Its body follows the two
                // quotes; the shadows below are decorative, not simulated fills.
                long gap = previous == null ? 0 : point.GameTick - previous.GameTick;
                decimal open = previous != null && (gap == MarginMarket.QuoteIntervalTicks || gap == SpotMarket.QuoteIntervalTicks)
                    ? previous.MidPrice : point.MidPrice;
                candles.Add(new MarketCandle { StartTick = point.GameTick, Open = open,
                    High = Math.Max(open, point.MidPrice), Low = Math.Min(open, point.MidPrice), Close = point.MidPrice });
                previous = point;
            }
            foreach (var candle in candles)
            {
                // Cosmetic wicks use a separate, stable hash; never consume the
                // trading RNG or affect fills, equity, margin or liquidation.
                decimal span = Math.Max(candle.Open * .0015m, candle.High - candle.Low);
                candle.DisplayHigh = candle.High + span * (.08m + .30m * Noise(candle.StartTick, 0xA341316Cu));
                candle.DisplayLow = Math.Max(.000001m,
                    candle.Low - span * (.08m + .30m * Noise(candle.StartTick, 0xC8013EA4u)));
            }
            return candles;
        }

        private static decimal Noise(long tick, uint salt)
        {
            unchecked
            {
                uint value = (uint)tick ^ (uint)(tick >> 32) ^ salt;
                value ^= value >> 16; value *= 0x7FEB352Du;
                value ^= value >> 15; value *= 0x846CA68Bu;
                value ^= value >> 16;
                return (decimal)value / uint.MaxValue;
            }
        }
    }
}
