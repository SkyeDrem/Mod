using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace FXDSP.Core
{
    public enum AchievementId
    {
        NewTrader, FirstProfit, FloatingLoss, FullLeverage, QuickProfit, PatientCapital,
        ValueInvesting, FullLiquidation, ProfitRoundTrip, TenPositions, CoinTrader,
        TenBuildings, BigPurchase, A9, BuildingFreedom, NoFactory, Leaderboard, Master
    }
    public sealed class AchievementDefinition
    {
        public readonly AchievementId Id;
        public readonly string Name, Description;
        public AchievementDefinition(AchievementId id, string name, string description)
        { Id = id; Name = name; Description = description; }
    }
    public static class AchievementCatalog
    {
        public static readonly IReadOnlyList<AchievementDefinition> All = Array.AsReadOnly(new[] {
            new AchievementDefinition(AchievementId.NewTrader, "交易新人", "开仓第一笔交易"),
            new AchievementDefinition(AchievementId.FirstProfit, "第一桶金", "从一笔交易中获利"),
            new AchievementDefinition(AchievementId.FloatingLoss, "浮亏不算亏", "某笔持仓达到10%浮亏"),
            new AchievementDefinition(AchievementId.FullLeverage, "韭卡洛斯", "满仓100x杠杆交易"),
            new AchievementDefinition(AchievementId.QuickProfit, "短线擒龙", "开仓后2根k线内平仓并获利"),
            new AchievementDefinition(AchievementId.PatientCapital, "耐心资本", "某笔持仓超过1000根k线"),
            new AchievementDefinition(AchievementId.ValueInvesting, "价值投资", "在0.8及以下价格开仓做多并以1及以上价格平仓"),
            new AchievementDefinition(AchievementId.FullLiquidation, "血本无归", "满仓爆仓"),
            new AchievementDefinition(AchievementId.ProfitRoundTrip, "盈亏同源", "某物品累计收益达到100%及以上后全部亏损(不能提取过该物品)"),
            new AchievementDefinition(AchievementId.TenPositions, "开超市", "同时具有10笔或以上持仓"),
            new AchievementDefinition(AchievementId.CoinTrader, "炒币", "首次在建筑币市场开仓"),
            new AchievementDefinition(AchievementId.TenBuildings, "逛超市", "累计换取10种不同的建筑"),
            new AchievementDefinition(AchievementId.BigPurchase, "进货", "一次性兑换超过100个同类建筑"),
            new AchievementDefinition(AchievementId.A9, "A9", "持有超过100,000,000建筑币"),
            new AchievementDefinition(AchievementId.BuildingFreedom, "建筑自由", "赚取1,000,000,000建筑币"),
            new AchievementDefinition(AchievementId.NoFactory, "工厂？不需要了！", "累计收益100000宇宙矩阵"),
            new AchievementDefinition(AchievementId.Leaderboard, "龙虎榜一", "某物品累计收益率超过10000%(不能提取过该物品)"),
            new AchievementDefinition(AchievementId.Master, "投资达人", "完成其余全部成就")
        });
        public static AchievementDefinition Find(AchievementId id) { return All.First(item => item.Id == id); }
    }

    public sealed class TradingAchievementState
    {
        private readonly HashSet<AchievementId> _completed = new HashSet<AchievementId>();
        private readonly List<AchievementId> _pending = new List<AchievementId>();
        internal readonly HashSet<int> DoubledMarkets = new HashSet<int>();
        internal readonly HashSet<int> PurchasedKinds = new HashSet<int>();
        internal readonly Dictionary<int, long> FullPositions = new Dictionary<int, long>();
        internal bool Initialized;
        public int CompletedCount { get { return _completed.Count; } }
        public int PurchasedKindCount { get { return PurchasedKinds.Count; } }
        public IReadOnlyList<AchievementId> Pending { get { return _pending; } }
        public bool Completed(AchievementId id) { return _completed.Contains(id); }
        public bool AllComplete { get { return Completed(AchievementId.Master); } }
        public bool NeedsQuotes { get { return !Completed(AchievementId.FloatingLoss) || !Completed(AchievementId.PatientCapital) || !Completed(AchievementId.A9); } }
        public void ClearPending() { _pending.Clear(); }
        public void AcknowledgeFirst() { if (_pending.Count > 0) _pending.RemoveAt(0); }
        internal void Award(AchievementId id)
        {
            if (!_completed.Add(id)) return;
            _pending.Add(id);
            if (id == AchievementId.FullLiquidation) FullPositions.Clear();
            if (id == AchievementId.ProfitRoundTrip) DoubledMarkets.Clear();
            if (id == AchievementId.TenBuildings) PurchasedKinds.Clear();
            if (id != AchievementId.Master && AchievementCatalog.All.All(item => item.Id == AchievementId.Master || Completed(item.Id)))
                Award(AchievementId.Master);
        }
        public void Export(BinaryWriter writer)
        {
            writer.Write(1); writer.Write(Initialized);
            writer.Write(_completed.Count); foreach (var id in _completed.OrderBy(id => id)) writer.Write((int)id);
            writer.Write(_pending.Count); foreach (var id in _pending) writer.Write((int)id);
            writer.Write(DoubledMarkets.Count); foreach (int id in DoubledMarkets.OrderBy(id => id)) writer.Write(id);
            writer.Write(PurchasedKinds.Count); foreach (int id in PurchasedKinds.OrderBy(id => id)) writer.Write(id);
            writer.Write(FullPositions.Count);
            foreach (var pair in FullPositions.OrderBy(pair => pair.Key)) { writer.Write(pair.Key); writer.Write(pair.Value); }
        }
        public static TradingAchievementState Import(BinaryReader reader)
        {
            if (reader.ReadInt32() != 1) throw new InvalidDataException("成就数据版本无效。");
            var result = new TradingAchievementState();
            byte flag = reader.ReadByte(); if (flag > 1) throw new InvalidDataException("成就状态无效。"); result.Initialized = flag == 1;
            for (int i = Count(reader, 64); i > 0; i--)
            {
                var id = ReadId(reader);
                if (!result._completed.Add(id)) throw new InvalidDataException("成就重复。");
            }
            for (int i = Count(reader, 64); i > 0; i--)
            {
                var id = ReadId(reader);
                if (!result.Completed(id) || result._pending.Contains(id)) throw new InvalidDataException("成就提示无效。");
                result._pending.Add(id);
            }
            for (int i = Count(reader, 256); i > 0; i--) if (!result.DoubledMarkets.Add(reader.ReadInt32())) throw new InvalidDataException("成就市场重复。");
            for (int i = Count(reader, 256); i > 0; i--) if (!result.PurchasedKinds.Add(reader.ReadInt32())) throw new InvalidDataException("成就兑换物品重复。");
            for (int i = Count(reader, 256); i > 0; i--)
            {
                int item = reader.ReadInt32(); long tick = reader.ReadInt64();
                if (tick < 0 || result.FullPositions.ContainsKey(item)) throw new InvalidDataException("成就持仓无效。");
                result.FullPositions.Add(item, tick);
            }
            return result;
        }
        private static int Count(BinaryReader reader, int maximum)
        { int count = reader.ReadInt32(); if (count < 0 || count > maximum) throw new InvalidDataException("成就记录数量无效。"); return count; }
        private static AchievementId ReadId(BinaryReader reader)
        { int id = reader.ReadInt32(); if (!Enum.IsDefined(typeof(AchievementId), id)) throw new InvalidDataException("未知成就。"); return (AchievementId)id; }
    }

    internal enum AchievementEvent { Opened, Settled, FundsChanged, Quote, Purchased }

    public sealed class AchievementTracker
    {
        private readonly MarketBook _book;
        public readonly TradingAchievementState State;
        private readonly HashSet<MarginMarket> _attached = new HashSet<MarginMarket>();
        private bool _quotes;
        public AchievementTracker(MarketBook book, TradingAchievementState state)
        { _book = book; State = state; _quotes = state.NeedsQuotes; AttachMarkets(); }
        public void AttachMarkets()
        {
            foreach (var market in _book.Markets)
                if (_attached.Add(market))
                { market.AchievementObserver = State.AllComplete ? null : (Action<MarginMarket, AchievementEvent, int, int>)Observe; market.ObserveAchievementQuotes = !State.AllComplete && State.NeedsQuotes; }
        }
        public void Initialize()
        {
            if (State.Initialized) return;
            State.Initialized = true;
            foreach (var market in _book.Markets)
            {
                if (market.Position != null) { State.Award(AchievementId.NewTrader); if (market.State.ItemId == MarketCatalog.BuildingCoin) State.Award(AchievementId.CoinTrader); }
                if (market.State.RealizedPnL > 0) { State.Award(AchievementId.NewTrader); State.Award(AchievementId.FirstProfit); }
                ObserveMetrics(market); ObserveQuote(market);
            }
            ObservePositionCount(); RefreshMonitoring();
        }
        private void Observe(MarginMarket market, AchievementEvent kind, int first, int second)
        {
            if (State.AllComplete) return;
            int item = market.State.ItemId;
            if (kind == AchievementEvent.Opened)
            {
                State.Award(AchievementId.NewTrader);
                if (item == MarketCatalog.BuildingCoin) State.Award(AchievementId.CoinTrader);
                if (first == 100)
                {
                    if (market.Position.Leverage == 100) State.Award(AchievementId.FullLeverage);
                    if (!State.Completed(AchievementId.FullLiquidation)) State.FullPositions[item] = market.Position.OpenTick;
                }
                else State.FullPositions.Remove(item);
                ObservePositionCount(); ObserveQuote(market);
            }
            else if (kind == AchievementEvent.Settled)
            {
                var closed = market.LastSettlement;
                if (!closed.Liquidated && closed.NetPnL > 0)
                {
                    State.Award(AchievementId.FirstProfit);
                    if (closed.CloseTick - closed.OpenTick <= MarginMarket.QuoteIntervalTicks * 2) State.Award(AchievementId.QuickProfit);
                }
                if (!closed.Liquidated && closed.Side == PositionSide.Long && closed.EntryPrice <= .8m && closed.ExitPrice >= 1m) State.Award(AchievementId.ValueInvesting);
                long fullTick;
                if (closed.Liquidated && State.FullPositions.TryGetValue(item, out fullTick) && fullTick == closed.OpenTick) State.Award(AchievementId.FullLiquidation);
                State.FullPositions.Remove(item);
                if (!State.Completed(AchievementId.ProfitRoundTrip) && State.DoubledMarkets.Contains(item) && closed.NetPnL < 0
                    && market.State.TotalWithdrawn == 0 && market.State.TotalDeposited > 0 && market.Equity < 1m) State.Award(AchievementId.ProfitRoundTrip);
            }
            else if (kind == AchievementEvent.Purchased)
            {
                if (!State.Completed(AchievementId.TenBuildings))
                { State.PurchasedKinds.Add(first); if (State.PurchasedKinds.Count >= 10) State.Award(AchievementId.TenBuildings); }
                if (second > 100) State.Award(AchievementId.BigPurchase);
            }
            if (kind == AchievementEvent.Quote) ObserveQuote(market);
            else ObserveMetrics(market);
            RefreshMonitoring();
        }
        private void ObserveMetrics(MarginMarket market)
        {
            var account = market.State;
            if (account.TotalWithdrawn == 0 && (!State.Completed(AchievementId.ProfitRoundTrip) || !State.Completed(AchievementId.Leaderboard)))
            {
                var rate = AccountPerformance.ReturnRatio(account.RealizedPnL, account.TotalDeposited, account.TotalWithdrawn);
                if (rate >= 1m && !State.Completed(AchievementId.ProfitRoundTrip)) State.DoubledMarkets.Add(account.ItemId);
                if (rate > 100m) State.Award(AchievementId.Leaderboard);
            }
            if (account.TotalWithdrawn > 0) State.DoubledMarkets.Remove(account.ItemId);
            if (account.ItemId == MarketCatalog.BuildingCoin)
            {
                ObserveCoinEquity(market);
                if (account.RealizedPnL >= 1000000000m) State.Award(AchievementId.BuildingFreedom);
            }
            if (account.ItemId == 6006 && account.RealizedPnL >= 100000m) State.Award(AchievementId.NoFactory);
        }
        private void ObserveQuote(MarginMarket market)
        {
            var position = market.Position;
            if (position != null)
            {
                if (!State.Completed(AchievementId.FloatingLoss) && market.UnrealizedPnL / position.Margin <= -.1m) State.Award(AchievementId.FloatingLoss);
                if (!State.Completed(AchievementId.PatientCapital) && AccountPerformance.HeldCandles(position.OpenTick, market.State.LastGameTick) > 1000) State.Award(AchievementId.PatientCapital);
            }
            if (market.State.ItemId == MarketCatalog.BuildingCoin) ObserveCoinEquity(market);
        }
        private void ObserveCoinEquity(MarginMarket market)
        { if (!State.Completed(AchievementId.A9) && market.Equity > 100000000m) State.Award(AchievementId.A9); }
        private void ObservePositionCount()
        { if (!State.Completed(AchievementId.TenPositions) && _book.Markets.Count(market => market.Position != null) >= 10) State.Award(AchievementId.TenPositions); }
        private void RefreshMonitoring()
        {
            bool quotes = State.NeedsQuotes && !State.AllComplete;
            if (_quotes == quotes && !State.AllComplete) return;
            _quotes = quotes;
            foreach (var market in _attached)
            { market.ObserveAchievementQuotes = quotes; if (State.AllComplete) market.AchievementObserver = null; }
        }
    }
}
