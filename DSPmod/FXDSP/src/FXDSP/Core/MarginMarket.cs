using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace FXDSP.Core
{
    public enum PositionSide { Long = 1, Short = -1 }

    public sealed class PositionSettlement
    {
        public int ItemId { get; internal set; }
        public bool Liquidated { get; internal set; }
        public PositionSide Side { get; internal set; }
        public int Leverage { get; internal set; }
        public decimal Margin { get; internal set; }
        public decimal NetPnL { get; internal set; }
        public decimal Fees { get; internal set; }
        public decimal Returned { get; internal set; }
        public decimal EntryPrice { get; internal set; }
        public decimal ExitPrice { get; internal set; }
        public long OpenTick { get; internal set; }
        public long CloseTick { get; internal set; }
        public decimal? ReturnRatio { get { return AccountPerformance.UnrealizedReturnRatio(NetPnL, Margin); } }
    }

    public sealed class MarginPosition
    {
        public PositionSide Side { get; internal set; }
        public decimal Margin { get; internal set; }
        public int Leverage { get; internal set; }
        public decimal EntryPrice { get; internal set; }
        public long OpenTick { get; internal set; }
        public decimal OpeningFee { get; internal set; }
        public decimal Notional { get { return checked(Margin * Leverage); } }
    }

    public sealed class MarginAccountState
    {
        public int ItemId { get; internal set; }
        public decimal Balance { get; internal set; }
        public decimal RealizedPnL { get; internal set; }
        public decimal FeesPaid { get; internal set; }
        public long TotalDeposited { get; internal set; }
        public long TotalWithdrawn { get; internal set; }
        public bool MigratedFromSpot { get; internal set; }
        public long LastGameTick { get; internal set; }
        public bool HasTimeAnchor { get; internal set; }
    }

    // The commodity is the collateral/cash unit, not the floating instrument.
    // A dimensionless simulated index is traded; PnL settles in its own item.
    // Each market supports one isolated position.
    public sealed class MarginMarket
    {
        internal Action<MarginMarket, AchievementEvent, int, int> AchievementObserver;
        internal bool ObserveAchievementQuotes;
        private void Observe(AchievementEvent kind, int first = 0, int second = 0)
        { AchievementObserver?.Invoke(this, kind, first, second); }
        public void NotifyBuildingPurchase(int itemId, int received)
        { if (received > 0) Observe(AchievementEvent.Purchased, itemId, received); }
        private void ObserveQuote()
        {
            if (ObserveAchievementQuotes && (_position != null || _state.ItemId == MarketCatalog.BuildingCoin))
            { SynchronizeTime(); Observe(AchievementEvent.Quote); }
        }
        public const long QuoteIntervalTicks = 90; // 1.5 game seconds.
        private const int Magic = 0x46584D47;
        private const int Version = 2;
        private const decimal HalfSpread = 0.0005m;
        private const decimal MaintenanceRate = 0.05m;
        private const decimal Scale = 1000000m;
        private static readonly Encoding Utf8 = new UTF8Encoding(false, true);
        private readonly SpotMarket _feed;
        private readonly MarginAccountState _state;
        private decimal _feeRate;
        private readonly List<string> _logs = new List<string>();
        private readonly List<PricePoint> _history = new List<PricePoint>();
        private readonly IReadOnlyList<PricePoint> _historyView;
        private MarginPosition _position;
        private decimal _liquidationPrice;

        public MarginAccountState State { get { SynchronizeTime(); return _state; } }
        public MarginPosition Position { get { return _position; } }
        // Transient notification only: loading/reloading never replays an old settlement.
        public PositionSettlement LastSettlement { get; private set; }
        public decimal MidPrice { get { return _feed.State.MidPrice; } }
        public decimal Bid { get { return decimal.Floor(MidPrice * (1m - HalfSpread) * Scale) / Scale; } }
        public decimal Ask { get { return decimal.Ceiling(MidPrice * (1m + HalfSpread) * Scale) / Scale; } }
        public decimal FeeRate { get { return _feeRate; } }
        public bool SetFeeRate(decimal rate)
        {
            if (rate < 0m || rate >= 1m) throw new InvalidOperationException("手续费率无效。");
            if (_feeRate == rate) return false;
            _feeRate = rate; _liquidationPrice = 0m;
            return true;
        }
        public decimal UsedMargin { get { return _position == null ? 0m : _position.Margin; } }
        public decimal UnrealizedPnL { get { return _position == null ? 0m : PnL(_position); } }
        public decimal Equity { get { return checked(_state.Balance + Math.Max(0m, UsedMargin + UnrealizedPnL)); } }
        public decimal NetPnL { get { return checked(_state.RealizedPnL + UnrealizedPnL); } }
        public string Unit { get { return MarketCatalog.Name(_state.ItemId); } }
        public decimal LiquidationPrice
        {
            get
            {
                if (_position == null) return 0m;
                if (_liquidationPrice > 0m) return _liquidationPrice;
                // Solve against the very same executable bid/ask, fee rounding
                // and maintenance rule used by TryLiquidate, on the chart's mid axis.
                decimal low = 0m, high = _position.EntryPrice * (2m / (1m - _feeRate));
                bool longSide = _position.Side == PositionSide.Long;
                for (int i = 0; i < 96 && high - low > .0000000001m; i++)
                {
                    decimal mid = low + (high - low) / 2m;
                    if (WouldLiquidate(mid) == longSide) low = mid;
                    else high = mid;
                }
                _liquidationPrice = low + (high - low) / 2m;
                return _liquidationPrice;
            }
        }
        public IReadOnlyList<string> RecentLogs { get { return _logs.AsReadOnly(); } }
        public IReadOnlyList<PricePoint> History
        {
            get
            {
                var source = _feed.History;
                if (_history.Count == source.Count && (_history.Count == 0 ||
                    (_history[_history.Count - 1].GameTick == source[source.Count - 1].GameTick &&
                     _history[_history.Count - 1].MidPrice == source[source.Count - 1].MidPrice)))
                    return _historyView;
                _history.Clear();
                foreach (var point in source)
                    _history.Add(new PricePoint(point.GameTick, point.MidPrice,
                        decimal.Floor(point.MidPrice * (1m - HalfSpread) * Scale) / Scale,
                        decimal.Ceiling(point.MidPrice * (1m + HalfSpread) * Scale) / Scale));
                return _historyView;
            }
        }

        public MarginMarket(int itemId = 1002, decimal feeRate = 0.001m, uint seed = 0x9E3779B9u)
            : this(new SpotMarket(itemId, seed: seed), feeRate) { }

        private MarginMarket(SpotMarket feed, decimal feeRate)
        {
            if (feeRate < 0m || feeRate >= 1m) throw new InvalidOperationException("手续费率无效。");
            _feed = feed;
            _feed.UseQuoteInterval(QuoteIntervalTicks);
            _historyView = _history.AsReadOnly();
            _feeRate = feeRate;
            _state = new MarginAccountState { ItemId = feed.State.ItemId };
            SynchronizeTime();
        }

        public static bool IsAllowedLeverage(int leverage)
        {
            return leverage >= 1 && leverage <= 100;
        }

        public decimal MarginForAllocation(int percentage, int leverage)
        {
            if (percentage < 1 || percentage > 100 || !IsAllowedLeverage(leverage))
                throw new InvalidOperationException("仓位比例或杠杆无效。");
            // Percentage applies to the full opening debit, including its fee.
            decimal budget = _state.Balance / 100m * percentage;
            decimal margin = decimal.Floor(budget / (1m + leverage * _feeRate) * Scale) / Scale;
            if (margin <= 0m) return 0m;
            if (OpenCost(margin, leverage) > budget) margin -= 1m / Scale;
            return Math.Max(0m, margin);
        }

        public decimal OpenFee(decimal margin, int leverage)
        {
            ValidateOrder(margin, leverage);
            return Fee(checked(margin * leverage));
        }

        public decimal OpenCost(decimal margin, int leverage)
        {
            return checked(margin + OpenFee(margin, leverage));
        }

        public string Open(PositionSide side, decimal margin, int leverage)
        { return Open(side, margin, leverage, 0); }

        public string Open(PositionSide side, decimal margin, int leverage, int allocationPercent)
        {
            if (side != PositionSide.Long && side != PositionSide.Short)
                throw new InvalidOperationException("仓位方向无效。");
            if (_position != null) throw new InvalidOperationException("请先平掉现有仓位，再开新仓。");
            decimal fee = OpenFee(margin, leverage);
            decimal cost = checked(margin + fee);
            if (_state.Balance < cost)
                throw new InvalidOperationException("可用余额不足以支付保证金和开仓费。");
            var nextRealized = checked(_state.RealizedPnL - fee);
            var nextFees = checked(_state.FeesPaid + fee);
            var position = new MarginPosition { Side = side, Margin = margin, Leverage = leverage,
                EntryPrice = side == PositionSide.Long ? Ask : Bid, OpenTick = _feed.State.LastGameTick };
            // Preflight valuation before reserving any funds.
            PnL(position);
            _state.Balance -= cost;
            _state.RealizedPnL = nextRealized;
            _state.FeesPaid = nextFees;
            _position = position;
            _position.OpeningFee = fee;
            _liquidationPrice = 0m;
            string message = Log((side == PositionSide.Long ? "开多" : "开空") + Money(margin) + "保证金");
            Observe(AchievementEvent.Opened, allocationPercent);
            TryLiquidate();
            return message;
        }

        public string Close()
        {
            if (_position == null) throw new InvalidOperationException("当前没有可平仓的仓位。");
            return Settle(false);
        }

        public string Deposit(long quantity)
        {
            RequirePositive(quantity);
            var nextBalance = checked(_state.Balance + quantity);
            var nextDeposits = checked(_state.TotalDeposited + quantity);
            _state.Balance = nextBalance;
            _state.TotalDeposited = nextDeposits;
            Observe(AchievementEvent.FundsChanged);
            return Log("存入 " + quantity + " " + Unit + "。");
        }

        public string Withdraw(long quantity)
        {
            RequirePositive(quantity);
            if (_state.Balance < quantity)
                throw new InvalidOperationException("可用余额不足。");
            var nextWithdrawn = checked(_state.TotalWithdrawn + quantity);
            _state.Balance -= quantity;
            _state.TotalWithdrawn = nextWithdrawn;
            Observe(AchievementEvent.FundsChanged);
            return Log("提取 " + quantity + " " + Unit + "。");
        }

        public void ValidateExchange(long gross, decimal fee, bool purchase)
        {
            if (_state.ItemId != MarketCatalog.BuildingCoin || gross <= 0 || fee < 0m || fee >= gross)
                throw new InvalidOperationException("建筑兑换金额无效。");
            // Check remaining capacity before decimal rounding can swallow a tiny fee.
            if (fee > decimal.MaxValue - _state.FeesPaid ||
                (_state.RealizedPnL <= 0m && fee > decimal.MaxValue + _state.RealizedPnL))
                throw new OverflowException("累计费用超出数值范围。");
            checked
            {
                decimal balance = purchase ? _state.Balance - gross - fee : _state.Balance + gross - fee;
                if (balance < 0m) throw new InvalidOperationException("可用建筑币不足。");
                long funding = purchase ? _state.TotalWithdrawn + gross : _state.TotalDeposited + gross;
                decimal realized = _state.RealizedPnL - fee;
                decimal fees = _state.FeesPaid + fee;
            }
        }

        public string SettleExchange(long gross, decimal fee, bool purchase)
        {
            ValidateExchange(gross, fee, purchase);
            _state.Balance = purchase ? _state.Balance - gross - fee : _state.Balance + gross - fee;
            if (purchase) _state.TotalWithdrawn += gross;
            else _state.TotalDeposited += gross;
            _state.RealizedPnL -= fee;
            _state.FeesPaid += fee;
            Observe(AchievementEvent.FundsChanged);
            return Log((purchase ? "兑换物品 " : "回收物品 ") + Money(gross) + " 建筑币，手续费 " + Money(fee) + "。");
        }

        public void AdvanceTo(long gameTick)
        {
            if (gameTick < 0) throw new InvalidOperationException("游戏时间不能为负。");
            if (!_feed.State.HasTimeAnchor || gameTick < _feed.State.LastGameTick)
            {
                _feed.AdvanceTo(gameTick); SynchronizeTime(); ObserveQuote(); TryLiquidate(); return;
            }
            long lastQuote = _feed.History[_feed.History.Count - 1].GameTick;
            long steps = (gameTick - lastQuote) / QuoteIntervalTicks;
            if (steps > 2880 * SpotMarket.QuoteIntervalTicks / QuoteIntervalTicks)
            {
                _feed.AdvanceTo(gameTick); SynchronizeTime(); ObserveQuote(); TryLiquidate(); return;
            }
            // Check each intermediate quote. A skipped frame cannot jump past a
            // liquidation price and revive a position at a later recovered quote.
            for (long index = 0; index < steps; index++)
            {
                lastQuote += QuoteIntervalTicks;
                _feed.AdvanceTo(lastQuote);
                ObserveQuote();
                TryLiquidate();
            }
            _feed.AdvanceTo(gameTick);
            SynchronizeTime();
            TryLiquidate();
        }

        public void ResumeAt(long gameTick)
        {
            if (_position != null) throw new InvalidOperationException("持仓中的资源不能暂停行情。");
            _feed.ResumeClockAt(gameTick);
            SynchronizeTime();
        }

        private decimal PnL(MarginPosition position)
        {
            decimal exit = position.Side == PositionSide.Long ? Bid : Ask;
            return PnL(position, exit);
        }

        private static decimal PnL(MarginPosition position, decimal exit)
        {
            return checked(position.Notional * ((exit - position.EntryPrice) / position.EntryPrice)
                * (position.Side == PositionSide.Long ? 1m : -1m));
        }

        private decimal ExitFee(MarginPosition position)
        {
            decimal exit = position.Side == PositionSide.Long ? Bid : Ask;
            return ExitFee(position, exit);
        }

        private decimal ExitFee(MarginPosition position, decimal exit)
        {
            return Fee(checked(position.Notional * exit / position.EntryPrice));
        }

        private bool WouldLiquidate(decimal mid)
        {
            decimal exit = _position.Side == PositionSide.Long
                ? decimal.Floor(mid * (1m - HalfSpread) * Scale) / Scale
                : decimal.Ceiling(mid * (1m + HalfSpread) * Scale) / Scale;
            return checked(_position.Margin + PnL(_position, exit))
                <= checked(_position.Margin * MaintenanceRate + ExitFee(_position, exit));
        }

        private void TryLiquidate()
        {
            if (_position == null) return;
            if (WouldLiquidate(MidPrice)) Settle(true);
        }

        internal PositionSettlement QuoteSettlement(bool liquidation, out decimal paidFee)
        {
            if (_position == null) throw new InvalidOperationException("当前没有持仓。");
            decimal pnl = PnL(_position);
            decimal grossReturn = Math.Max(0m, checked(_position.Margin + pnl));
            paidFee = Math.Min(grossReturn, ExitFee(_position));
            decimal returned = grossReturn - paidFee;
            decimal realizedDelta = returned - _position.Margin;
            checked
            {
                var balance = _state.Balance + returned;
                var realized = _state.RealizedPnL + realizedDelta;
                var fees = _state.FeesPaid + paidFee;
            }
            decimal entryFee = _position.OpeningFee;
            return new PositionSettlement {
                ItemId = _state.ItemId, Liquidated = liquidation, Side = _position.Side, Leverage = _position.Leverage,
                Margin = _position.Margin, NetPnL = realizedDelta - entryFee,
                Fees = entryFee + paidFee, Returned = returned,
                EntryPrice = _position.EntryPrice, ExitPrice = _position.Side == PositionSide.Long ? Bid : Ask,
                OpenTick = _position.OpenTick, CloseTick = _feed.State.LastGameTick
            };
        }

        private string Settle(bool liquidation)
        {
            decimal paidFee;
            var settlement = QuoteSettlement(liquidation, out paidFee);
            _state.Balance += settlement.Returned;
            _state.RealizedPnL += settlement.Returned - settlement.Margin;
            _state.FeesPaid += paidFee;
            _position = null;
            LastSettlement = settlement;
            Observe(AchievementEvent.Settled);
            return Log((liquidation ? "爆仓结算" : "已平仓") + " "
                + Signed(settlement.NetPnL) + "  收益率 " + MoneyDisplay.Percent(settlement.ReturnRatio));
        }

        public void Export(BinaryWriter writer)
        {
            if (writer == null) throw new ArgumentNullException(nameof(writer));
            writer.Write(Magic); writer.Write(Version);
            writer.Write(_state.Balance); writer.Write(_state.RealizedPnL); writer.Write(_state.FeesPaid);
            writer.Write(_state.TotalDeposited); writer.Write(_state.TotalWithdrawn);
            writer.Write(_state.MigratedFromSpot); writer.Write(_feeRate);
            writer.Write(_position != null);
            if (_position != null)
            {
                writer.Write((int)_position.Side); writer.Write(_position.Margin);
                writer.Write(_position.Leverage); writer.Write(_position.EntryPrice); writer.Write(_position.OpenTick);
                writer.Write(_position.OpeningFee);
            }
            _feed.Export(writer);
            writer.Write(_logs.Count);
            foreach (var log in _logs)
            {
                byte[] bytes = Utf8.GetBytes(log); writer.Write(bytes.Length); writer.Write(bytes);
            }
        }

        public static MarginMarket Import(BinaryReader reader)
        {
            if (reader == null || !reader.BaseStream.CanSeek)
                throw new InvalidDataException("账户读取器必须支持定位。");
            try
            {
                long start = reader.BaseStream.Position;
                int magic = reader.ReadInt32();
                if (magic == 0x4658534D)
                {
                    reader.BaseStream.Position = start;
                    return FromLegacy(SpotMarket.Import(reader));
                }
                int version = reader.ReadInt32();
                if (magic != Magic || (version != 1 && version != Version))
                    throw new InvalidDataException("不支持的铜本位账户格式。");
                decimal balance = reader.ReadDecimal(), realized = reader.ReadDecimal(), fees = reader.ReadDecimal();
                long deposited = reader.ReadInt64(), withdrawn = reader.ReadInt64();
                bool migrated = ReadFlag(reader);
                decimal rate = reader.ReadDecimal();
                MarginPosition position = null;
                if (ReadFlag(reader))
                {
                    position = new MarginPosition { Side = (PositionSide)reader.ReadInt32(),
                        Margin = reader.ReadDecimal(), Leverage = reader.ReadInt32(),
                        EntryPrice = reader.ReadDecimal(), OpenTick = reader.ReadInt64() };
                    position.OpeningFee = version >= 2 ? reader.ReadDecimal()
                        : decimal.Ceiling(position.Margin * position.Leverage * rate * Scale) / Scale;
                    ValidateOrder(position.Margin, position.Leverage);
                    if ((position.Side != PositionSide.Long && position.Side != PositionSide.Short)
                        || position.EntryPrice <= 0m || position.OpenTick < 0 || position.OpeningFee < 0m || position.OpeningFee > fees)
                        throw new InvalidDataException("仓位数据无效。");
                }
                var feed = SpotMarket.Import(reader);
                if (balance < 0m || fees < 0m || deposited < 0 || withdrawn < 0
                    || feed.State.ItemQuantity != 0 || feed.State.SettlementBalance != 0m
                    || (position != null && position.OpenTick > feed.State.LastGameTick))
                    throw new InvalidDataException("铜账户资产或行情字段无效。");
                var market = new MarginMarket(feed, rate);
                market._state.Balance = balance; market._state.RealizedPnL = realized;
                market._state.FeesPaid = fees; market._state.TotalDeposited = deposited;
                market._state.TotalWithdrawn = withdrawn; market._state.MigratedFromSpot = migrated;
                market._position = position;
                int count = reader.ReadInt32();
                if (count < 0 || count > 10) throw new InvalidDataException("日志数量无效。");
                for (int index = 0; index < count; index++)
                {
                    int length = reader.ReadInt32();
                    if (length <= 0 || length > 1024) throw new InvalidDataException("日志长度无效。");
                    byte[] bytes = reader.ReadBytes(length);
                    if (bytes.Length != length) throw new EndOfStreamException();
                    market._logs.Add(Utf8.GetString(bytes));
                }
                // Reject impossible/overflowing valuations before publishing state.
                var equity = market.Equity;
                var net = market.NetPnL;
                if (position != null) market.ExitFee(position);
                return market;
            }
            catch (InvalidDataException) { throw; }
            catch (Exception exception) when (exception is EndOfStreamException || exception is ArgumentException
                || exception is InvalidOperationException || exception is OverflowException)
            { throw new InvalidDataException("铜账户存档损坏或包含无效数据。", exception); }
        }

        private static MarginMarket FromLegacy(SpotMarket old)
        {
            var previous = old.State;
            decimal balance = checked(previous.ItemQuantity
                + previous.SettlementBalance / checked(old.Ask * (1m + old.FeeRate)));
            decimal realized = checked(balance + previous.TotalWithdrawn - previous.TotalDeposited);
            long deposits = previous.TotalDeposited, withdrawals = previous.TotalWithdrawn;
            // Keep the exact price history and RNG, but leave no second cash pool.
            previous.ItemQuantity = 0; previous.SettlementBalance = 0m;
            previous.TotalDeposited = 0; previous.TotalWithdrawn = 0;
            var market = new MarginMarket(old, 0.001m);
            market._state.Balance = balance; market._state.RealizedPnL = realized;
            market._state.TotalDeposited = deposits; market._state.TotalWithdrawn = withdrawals;
            market._state.MigratedFromSpot = true;
            market.Log("旧现货账户已迁移：" + Money(balance) + " 铜余额；原行情保留。");
            return market;
        }

        private decimal Fee(decimal notional) { return decimal.Ceiling(checked(notional * _feeRate * Scale)) / Scale; }
        private static void ValidateOrder(decimal margin, int leverage)
        {
            if (margin <= 0m) throw new InvalidOperationException("保证金必须大于零。");
            if (!IsAllowedLeverage(leverage)) throw new InvalidOperationException("不支持该杠杆倍数。");
            checked { var notional = margin * leverage; if (notional <= 0) throw new OverflowException(); }
        }
        private static void RequirePositive(long quantity)
        { if (quantity <= 0) throw new InvalidOperationException("数量必须是正整数。"); }
        private static bool ReadFlag(BinaryReader reader)
        { byte value = reader.ReadByte(); if (value > 1) throw new InvalidDataException("账户标志无效。"); return value == 1; }
        private void SynchronizeTime()
        { _state.HasTimeAnchor = _feed.State.HasTimeAnchor; _state.LastGameTick = _feed.State.LastGameTick; }
        private string Log(string text) { _logs.Add(text); if (_logs.Count > 10) _logs.RemoveAt(0); return text; }
        private static string Money(decimal value) { return value.ToString("0.00", CultureInfo.InvariantCulture); }
        private static string Signed(decimal value)
        { return decimal.Round(value, 2, MidpointRounding.AwayFromZero).ToString("+0.00;-0.00;0.00", CultureInfo.InvariantCulture); }
    }
}
