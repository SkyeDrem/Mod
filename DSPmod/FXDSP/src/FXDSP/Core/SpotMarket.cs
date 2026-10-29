using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace FXDSP.Core
{
    public sealed class MarketState
    {
        public int ItemId { get; internal set; }
        public long ItemQuantity { get; internal set; }
        public decimal SettlementBalance { get; internal set; }
        public long TotalDeposited { get; internal set; }
        public long TotalWithdrawn { get; internal set; }
        public long LastGameTick { get; internal set; }
        public bool HasTimeAnchor { get; internal set; }
        public decimal MidPrice { get; internal set; }
        public long NetDeliveredGain { get { return TotalWithdrawn - TotalDeposited; } }
    }

    public sealed class PricePoint
    {
        public long GameTick { get; private set; }
        public decimal MidPrice { get; private set; }
        public decimal Bid { get; private set; }
        public decimal Ask { get; private set; }

        internal PricePoint(long gameTick, decimal midPrice, decimal bid, decimal ask)
        {
            GameTick = gameTick;
            MidPrice = midPrice;
            Bid = bid;
            Ask = ask;
        }
    }

    /// <summary>
    /// A single isolated, fully-funded spot market. There is no Unity, inventory
    /// API, free money, leverage, NPC inventory limit or profitability cap here.
    /// The caller must transfer real items before Deposit / Withdraw, and advance
    /// time only while its owning game is running and unpaused.
    /// </summary>
    public sealed class SpotMarket
    {
        public const int DefaultItemId = 1002;
        public const long QuoteIntervalTicks = 300;
        public const int HistoryLimit = 120;
        public const int LogLimit = 10;

        private const int Magic = 0x4658534D;
        private const int FormatVersion = 1;
        private const int MaxLogBytes = 1024;
        private const long MaxCatchUpSteps = 2880; // Four game hours at 60 ticks/s.
        private const decimal MinMidPrice = 0.20m;
        private const decimal MaxMidPrice = 5.00m;
        private const decimal HalfSpread = 0.005m;
        private const decimal FeeScale = 1000000m;
        // Use the same quote and fee grid. Every integer quantity's notional is
        // an exact multiple of the fee unit, so even a near-100% rounded fee
        // cannot exceed its notional or need a split-order-friendly clamp.
        private const decimal PriceScale = FeeScale;
        private static readonly Encoding StrictUtf8 = new UTF8Encoding(false, true);

        private readonly MarketState _state;
        private readonly List<PricePoint> _history = new List<PricePoint>();
        private readonly List<string> _recentLogs = new List<string>();
        private readonly IReadOnlyList<PricePoint> _historyView;
        private readonly IReadOnlyList<string> _logView;
        private readonly decimal _feeRate;
        private uint _randomState;
        internal void ReseedRandomStream() { _randomState = MarketRandom.NewSeed(_randomState); }
        private decimal _momentum;
        private long _lastPriceTick;
        private long _quoteIntervalTicks = QuoteIntervalTicks;

        internal void UseQuoteInterval(long ticks)
        {
            if (ticks <= 0 || ticks > QuoteIntervalTicks) throw new ArgumentOutOfRangeException(nameof(ticks));
            _quoteIntervalTicks = ticks;
            // A faster schedule must not backfill prices in the already-played
            // part of a legacy five-second interval, especially with an open position.
            if (_state.HasTimeAnchor && _state.LastGameTick - _lastPriceTick >= ticks)
            {
                _lastPriceTick = _state.LastGameTick;
                AddPricePoint(_lastPriceTick, _state.MidPrice);
            }
        }

        internal void ResumeClockAt(long gameTick)
        {
            if (gameTick < 0 || (_state.HasTimeAnchor && gameTick < _state.LastGameTick))
                throw new InvalidOperationException("恢复行情时间无效。");
            if (!_state.HasTimeAnchor || gameTick > _state.LastGameTick)
                AnchorTime(gameTick, false);
        }

        public MarketState State { get { return _state; } }
        public IReadOnlyList<PricePoint> History { get { return _historyView; } }
        public IReadOnlyList<string> RecentLogs { get { return _logView; } }
        public decimal Bid { get { return QuoteBid(_state.MidPrice); } }
        public decimal Ask { get { return QuoteAsk(_state.MidPrice); } }
        public decimal FeeRate { get { return _feeRate; } }

        public SpotMarket(int itemId = DefaultItemId, decimal feeRate = 0.01m, uint seed = 0x9E3779B9u)
        {
            if (itemId <= 0)
                throw new InvalidOperationException("物品编号必须为正整数。");
            if (feeRate < 0m || feeRate >= 1m)
                throw new InvalidOperationException("手续费率必须大于等于0且小于1。");
            if (seed == 0u)
                throw new InvalidOperationException("行情随机种子不能为0。");

            _state = new MarketState { ItemId = itemId, MidPrice = 1m };
            _feeRate = feeRate;
            _randomState = seed;
            _historyView = _history.AsReadOnly();
            _logView = _recentLogs.AsReadOnly();
        }

        public decimal GetBuyTotal(long quantity)
        {
            RequirePositive(quantity);
            try
            {
                decimal gross = checked(quantity * Ask);
                return checked(gross + CalculateFee(gross));
            }
            catch (OverflowException exception)
            {
                throw NumericRangeError(exception);
            }
        }

        public decimal GetSellNet(long quantity)
        {
            RequirePositive(quantity);
            try
            {
                decimal gross = checked(quantity * Bid);
                return checked(gross - CalculateFee(gross));
            }
            catch (OverflowException exception)
            {
                throw NumericRangeError(exception);
            }
        }

        public string Buy(long quantity)
        {
            decimal total = GetBuyTotal(quantity);
            if (total > _state.SettlementBalance)
                throw new InvalidOperationException("该物品市场的结算额度不足（总价已含手续费）。");

            long nextQuantity;
            try { nextQuantity = checked(_state.ItemQuantity + quantity); }
            catch (OverflowException exception) { throw NumericRangeError(exception); }

            _state.ItemQuantity = nextQuantity;
            _state.SettlementBalance -= total;
            return AddLog("买入 " + Number(quantity) + "，含费支出 " + Money(total) + " 额度。");
        }

        public string Sell(long quantity)
        {
            RequirePositive(quantity);
            if (quantity > _state.ItemQuantity)
                throw new InvalidOperationException("可卖出的物品不足；现货交易不能卖空。");

            decimal net = GetSellNet(quantity);
            decimal nextBalance;
            try { nextBalance = checked(_state.SettlementBalance + net); }
            catch (OverflowException exception) { throw NumericRangeError(exception); }

            _state.ItemQuantity -= quantity;
            _state.SettlementBalance = nextBalance;
            return AddLog("卖出 " + Number(quantity) + "，扣费到账 " + Money(net) + " 额度。");
        }

        public string Deposit(long quantity)
        {
            RequirePositive(quantity);
            long nextQuantity;
            long nextTotal;
            try
            {
                nextQuantity = checked(_state.ItemQuantity + quantity);
                nextTotal = checked(_state.TotalDeposited + quantity);
            }
            catch (OverflowException exception) { throw NumericRangeError(exception); }

            _state.ItemQuantity = nextQuantity;
            _state.TotalDeposited = nextTotal;
            return AddLog("入库 " + Number(quantity) + " 个物品。");
        }

        public string Withdraw(long quantity)
        {
            RequirePositive(quantity);
            if (quantity > _state.ItemQuantity)
                throw new InvalidOperationException("可提取的物品库存不足。");

            long nextTotal;
            try { nextTotal = checked(_state.TotalWithdrawn + quantity); }
            catch (OverflowException exception) { throw NumericRangeError(exception); }

            _state.ItemQuantity -= quantity;
            _state.TotalWithdrawn = nextTotal;
            return AddLog("提取 " + Number(quantity) + " 个物品。");
        }

        public void AdvanceTo(long gameTick)
        {
            if (gameTick < 0)
                throw new InvalidOperationException("游戏逻辑时间不能为负数。");

            if (!_state.HasTimeAnchor)
            {
                AnchorTime(gameTick, false);
                return;
            }

            if (gameTick < _state.LastGameTick)
            {
                AnchorTime(gameTick, true);
                AddLog("游戏时间回退，行情已重新同步；账户资产保持不变。");
                return;
            }

            long steps = (gameTick - _lastPriceTick) / _quoteIntervalTicks;
            if (steps > MaxCatchUpSteps * QuoteIntervalTicks / _quoteIntervalTicks)
            {
                // A disconnected/other-save tick must not run an unbounded loop.
                // No offline prices, assets or random values are manufactured.
                AnchorTime(gameTick, true);
                AddLog("游戏时间跨度过大，行情已重新同步。");
                return;
            }

            for (long step = 0; step < steps; step++)
            {
                decimal noise = NextNoise();
                _momentum = _momentum * 0.88m + noise * 0.0024m;
                decimal next = _state.MidPrice
                    + (1m - _state.MidPrice) * 0.006m
                    + _state.MidPrice * (_momentum + noise * 0.008m);
                next = Math.Max(MinMidPrice, Math.Min(MaxMidPrice, next));
                _state.MidPrice = decimal.Round(next, 8, MidpointRounding.AwayFromZero);
                _lastPriceTick += _quoteIntervalTicks;
                AddPricePoint(_lastPriceTick, _state.MidPrice);
            }
            _state.LastGameTick = gameTick;
        }

        public void Export(BinaryWriter writer)
        {
            if (writer == null) throw new ArgumentNullException("writer");
            writer.Write(Magic);
            writer.Write(FormatVersion);
            writer.Write(_state.ItemId);
            writer.Write(_state.ItemQuantity);
            writer.Write(_state.SettlementBalance);
            writer.Write(_state.TotalDeposited);
            writer.Write(_state.TotalWithdrawn);
            writer.Write(_state.MidPrice);
            writer.Write(_feeRate);
            writer.Write(_randomState);
            writer.Write(_momentum);
            writer.Write(_state.HasTimeAnchor);
            writer.Write(_state.LastGameTick);
            writer.Write(_lastPriceTick);
            writer.Write(_history.Count);
            foreach (PricePoint point in _history)
            {
                writer.Write(point.GameTick);
                writer.Write(point.MidPrice);
            }
            writer.Write(_recentLogs.Count);
            foreach (string entry in _recentLogs)
            {
                byte[] bytes = StrictUtf8.GetBytes(entry);
                writer.Write(bytes.Length);
                writer.Write(bytes);
            }
        }

        public static SpotMarket Import(BinaryReader reader)
        {
            if (reader == null) throw new ArgumentNullException("reader");
            try
            {
                if (reader.ReadInt32() != Magic)
                    throw new InvalidDataException("不是FXDSP市场存档。");
                if (reader.ReadInt32() != FormatVersion)
                    throw new InvalidDataException("不支持的FXDSP市场存档版本。");

                int itemId = reader.ReadInt32();
                long quantity = reader.ReadInt64();
                decimal balance = reader.ReadDecimal();
                long deposited = reader.ReadInt64();
                long withdrawn = reader.ReadInt64();
                decimal midPrice = reader.ReadDecimal();
                decimal feeRate = reader.ReadDecimal();
                uint random = reader.ReadUInt32();
                decimal momentum = reader.ReadDecimal();
                bool anchored = ReadBoolean(reader);
                long gameTick = reader.ReadInt64();
                long priceTick = reader.ReadInt64();
                if (quantity < 0 || balance < 0m || deposited < 0 || withdrawn < 0)
                    throw new InvalidDataException("市场存档包含负资产或负流水。");
                RequireSavedPrice(midPrice);
                if (Math.Abs(momentum) > 0.02m)
                    throw new InvalidDataException("市场存档的行情动量超出有效范围。");
                if (gameTick < 0 || priceTick < 0 || priceTick > gameTick)
                    throw new InvalidDataException("市场存档的逻辑时间无效。");
                if (anchored && gameTick - priceTick >= QuoteIntervalTicks)
                    throw new InvalidDataException("市场存档的行情时间锚点无效。");
                if (!anchored && (gameTick != 0 || priceTick != 0))
                    throw new InvalidDataException("未初始化市场不应包含时间锚点。");

                SpotMarket market = new SpotMarket(itemId, feeRate, random);
                market._state.ItemQuantity = quantity;
                market._state.SettlementBalance = balance;
                market._state.TotalDeposited = deposited;
                market._state.TotalWithdrawn = withdrawn;
                market._state.MidPrice = midPrice;
                market._state.HasTimeAnchor = anchored;
                market._state.LastGameTick = gameTick;
                market._lastPriceTick = priceTick;
                market._momentum = momentum;

                int historyCount = ReadBoundedCount(reader, HistoryLimit, "行情历史");
                if (anchored != (historyCount > 0))
                    throw new InvalidDataException("市场时间锚点与行情历史不一致。");
                long previousTick = -1;
                for (int index = 0; index < historyCount; index++)
                {
                    long tick = reader.ReadInt64();
                    decimal price = reader.ReadDecimal();
                    RequireSavedPrice(price);
                    if (tick < 0 || tick <= previousTick || tick > gameTick)
                        throw new InvalidDataException("行情历史的时间顺序无效。");
                    market.AddPricePoint(tick, price);
                    previousTick = tick;
                }
                if (historyCount > 0 && (previousTick != priceTick
                    || market._history[historyCount - 1].MidPrice != midPrice))
                    throw new InvalidDataException("当前报价与行情历史不一致。");

                int logCount = ReadBoundedCount(reader, LogLimit, "交易日志");
                for (int index = 0; index < logCount; index++)
                {
                    int byteCount = ReadBoundedCount(reader, MaxLogBytes, "日志文本");
                    if (byteCount == 0)
                        throw new InvalidDataException("市场日志不能是空文本。");
                    byte[] bytes = reader.ReadBytes(byteCount);
                    if (bytes.Length != byteCount)
                        throw new EndOfStreamException();
                    market._recentLogs.Add(StrictUtf8.GetString(bytes));
                }
                return market;
            }
            catch (InvalidDataException) { throw; }
            catch (EndOfStreamException exception)
            {
                throw new InvalidDataException("FXDSP市场存档不完整。", exception);
            }
            catch (Exception exception) when (exception is ArgumentException
                || exception is InvalidOperationException || exception is OverflowException)
            {
                throw new InvalidDataException("FXDSP市场存档包含无效数据。", exception);
            }
        }

        private void AnchorTime(long gameTick, bool clearHistory)
        {
            _state.HasTimeAnchor = true;
            _state.LastGameTick = gameTick;
            _lastPriceTick = gameTick;
            if (clearHistory) _history.Clear();
            AddPricePoint(gameTick, _state.MidPrice);
        }

        private decimal NextNoise()
        {
            uint next = _randomState;
            next ^= next << 13;
            next ^= next >> 17;
            next ^= next << 5;
            _randomState = next;
            return (decimal)next / uint.MaxValue * 2m - 1m;
        }

        private void AddPricePoint(long tick, decimal midPrice)
        {
            _history.Add(new PricePoint(tick, midPrice, QuoteBid(midPrice), QuoteAsk(midPrice)));
            if (_history.Count > HistoryLimit) _history.RemoveAt(0);
        }

        private string AddLog(string message)
        {
            _recentLogs.Add(message);
            if (_recentLogs.Count > LogLimit) _recentLogs.RemoveAt(0);
            return message;
        }

        private decimal CalculateFee(decimal gross)
        {
            // Ceil is subadditive in the safe direction: splitting cannot save fees.
            return decimal.Ceiling(gross * _feeRate * FeeScale) / FeeScale;
        }

        private static decimal QuoteBid(decimal midPrice)
        {
            return decimal.Floor(midPrice * (1m - HalfSpread) * PriceScale) / PriceScale;
        }

        private static decimal QuoteAsk(decimal midPrice)
        {
            return decimal.Ceiling(midPrice * (1m + HalfSpread) * PriceScale) / PriceScale;
        }

        private static void RequirePositive(long quantity)
        {
            if (quantity <= 0)
                throw new InvalidOperationException("物品数量必须为正整数。");
        }

        private static void RequireSavedPrice(decimal price)
        {
            if (price < MinMidPrice || price > MaxMidPrice)
                throw new InvalidDataException("市场存档的报价超出有效范围。");
        }

        private static bool ReadBoolean(BinaryReader reader)
        {
            byte value = reader.ReadByte();
            if (value > 1) throw new InvalidDataException("市场存档的布尔标志无效。");
            return value == 1;
        }

        private static int ReadBoundedCount(BinaryReader reader, int maximum, string name)
        {
            int count = reader.ReadInt32();
            if (count < 0 || count > maximum)
                throw new InvalidDataException(name + "长度无效。");
            return count;
        }

        private static InvalidOperationException NumericRangeError(OverflowException exception)
        {
            return new InvalidOperationException("数量或金额超出支持的数值范围；账户未改变。", exception);
        }

        private static string Number(long value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }

        private static string Money(decimal value)
        {
            return value.ToString("0.########", CultureInfo.InvariantCulture);
        }
    }
}
