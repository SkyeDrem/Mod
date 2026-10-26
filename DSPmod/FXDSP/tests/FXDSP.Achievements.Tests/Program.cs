using System;
using System.IO;
using System.Linq;
using System.Reflection;
using FXDSP.Core;

internal static class Program
{
    private static int passed;
    private sealed class Fixture
    {
        internal MarketBook Book = new MarketBook();
        internal TradingAchievementState State = new TradingAchievementState();
        internal AchievementTracker Tracker;
        internal MarginMarket Market;
        internal Fixture(int id = MarketCatalog.Copper)
        {
            Tracker = new AchievementTracker(Book, State); Tracker.Initialize();
            Market = Book.Get(id); Market.AdvanceTo(0);
        }
        internal bool Has(AchievementId id) => State.Completed(id);
    }
    private static void Main()
    {
        Check("deposit is not a trade; failed opening does not award", () => {
            var f = new Fixture(); f.Market.Deposit(100);
            try { f.Market.Open(PositionSide.Long, 200, 1); } catch (InvalidOperationException) { }
            Require(!f.Has(AchievementId.NewTrader));
            f.Market.Open(PositionSide.Long, 10, 1); Require(f.Has(AchievementId.NewTrader));
        });
        Check("first profit and quick profit use net after both fees", () => {
            var f = Open(); SetMid(f.Market, 1.1m); f.Market.Close();
            Require(f.Has(AchievementId.FirstProfit) && f.Has(AchievementId.QuickProfit));
            var loss = Open(); loss.Market.Close(); Require(!loss.Has(AchievementId.FirstProfit));
        });
        Check("gross gain swallowed by fees is not first profit", () => {
            var f = Open(); SetMid(f.Market, 1.002m); Require(f.Market.UnrealizedPnL > 0);
            f.Market.Close(); Require(f.Market.LastSettlement.NetPnL < 0 && !f.Has(AchievementId.FirstProfit));
        });
        Check("quick profit includes 180 ticks and excludes 181", () => {
            foreach (long tick in new[] { 180L, 181L }) {
                var f = Open(); f.Market.AdvanceTo(tick); SetMid(f.Market, 1.2m); f.Market.Close();
                Require(f.Has(AchievementId.QuickProfit) == (tick == 180));
            }
        });
        Check("floating loss threshold uses posted margin", () => {
            var f = Open(); SetMid(f.Market, .92m); Quote(f.Market); Require(!f.Has(AchievementId.FloatingLoss));
            SetMid(f.Market, .89m); Quote(f.Market); Require(f.Has(AchievementId.FloatingLoss));
        });
        Check("only explicit 100 percent plus 100x awards full leverage", () => {
            var f = new Fixture(); f.Market.Deposit(1000);
            f.Market.Open(PositionSide.Long, f.Market.MarginForAllocation(99,100),100,99);
            Require(!f.Has(AchievementId.FullLeverage)); f.Market.Close();
            f.Market.Open(PositionSide.Long, f.Market.MarginForAllocation(100,100),100,100);
            Require(f.Has(AchievementId.FullLeverage));
        });
        Check("patient capital is strictly greater than 1000 candles", () => {
            var f = Open(); Feed(f.Market).State.LastGameTick = 90000; Quote(f.Market); Require(!f.Has(AchievementId.PatientCapital));
            Feed(f.Market).State.LastGameTick = 90090; Quote(f.Market); Require(f.Has(AchievementId.PatientCapital));
        });
        Check("value investing uses executable entry and exit", () => {
            var f = new Fixture(); f.Market.Deposit(100); SetMid(f.Market,.799m); f.Market.Open(PositionSide.Long,10,1);
            SetMid(f.Market,1.001m); f.Market.Close(); Require(f.Has(AchievementId.ValueInvesting));
            var g = new Fixture(); g.Market.Deposit(100); SetMid(g.Market,.8m); g.Market.Open(PositionSide.Long,10,1);
            SetMid(g.Market,1.1m); g.Market.Close(); Require(!g.Has(AchievementId.ValueInvesting));
        });
        Check("full liquidation uses original allocation flag", () => {
            foreach(int allocation in new[] {99,100}) {
                var f = new Fixture(); f.Market.Deposit(100);
                f.Market.Open(PositionSide.Long,f.Market.MarginForAllocation(allocation,10),10,allocation);
                SetMid(f.Market,.2m); f.Market.AdvanceTo(0);
                Require(f.Market.LastSettlement.Liquidated && f.Has(AchievementId.FullLiquidation) == (allocation == 100));
            }
        });
        Check("full opening flag survives serialized achievement state", () => {
            var f = new Fixture(); f.Market.Deposit(100); f.Market.Open(PositionSide.Long,f.Market.MarginForAllocation(100,10),10,100);
            var restored = RoundTrip(f.State); new AchievementTracker(f.Book, restored);
            SetMid(f.Market,.2m); f.Market.AdvanceTo(0); Require(restored.Completed(AchievementId.FullLiquidation));
        });
        Check("round trip requires previously doubled account and real loss to below one", () => {
            var f = Open(); f.Market.State.RealizedPnL=100; f.Market.State.Balance=200; Funds(f.Market);
            Require(f.State.DoubledMarkets.Contains(MarketCatalog.Copper));
            f.Market.Close(); f.Market.Open(PositionSide.Long,f.Market.MarginForAllocation(100,10),10,100);
            SetMid(f.Market,.2m); f.Market.AdvanceTo(0); Require(f.Has(AchievementId.ProfitRoundTrip));
        });
        Check("withdrawal alone cannot award round trip", () => {
            var f = new Fixture(); f.Market.Deposit(100); f.Market.State.RealizedPnL=100; f.Market.State.Balance=200;
            Funds(f.Market); f.Market.Withdraw(200); Require(!f.Has(AchievementId.ProfitRoundTrip));
        });
        Check("near zero without earlier doubling is not round trip", () => {
            var f = new Fixture(); f.Market.Deposit(100);
            f.Market.Open(PositionSide.Long,f.Market.MarginForAllocation(100,10),10,100);
            SetMid(f.Market,.2m); f.Market.AdvanceTo(0); Require(!f.Has(AchievementId.ProfitRoundTrip));
        });
        Check("any earlier withdrawal blocks both achievements even after redeposit", () => {
            var f = new Fixture(); f.Market.Deposit(100); f.Market.Withdraw(1); f.Market.Deposit(1);
            f.Market.State.RealizedPnL=10001; Funds(f.Market);
            Require(!f.Has(AchievementId.Leaderboard) && !f.State.DoubledMarkets.Contains(MarketCatalog.Copper));
            f.State.DoubledMarkets.Add(MarketCatalog.Copper);
            f.Market.Open(PositionSide.Long,f.Market.MarginForAllocation(100,10),10,100);
            SetMid(f.Market,.2m); f.Market.AdvanceTo(0); Require(!f.Has(AchievementId.ProfitRoundTrip));
        });
        Check("withdrawal after doubling cancels round trip eligibility", () => {
            var f = new Fixture(); f.Market.Deposit(100); f.Market.State.RealizedPnL=100;
            f.Market.State.Balance=200; Funds(f.Market); Require(f.State.DoubledMarkets.Contains(MarketCatalog.Copper));
            f.Market.Withdraw(1); f.Market.Deposit(1); Require(!f.State.DoubledMarkets.Contains(MarketCatalog.Copper));
            f.Market.Open(PositionSide.Long,f.Market.MarginForAllocation(100,10),10,100);
            SetMid(f.Market,.2m); f.Market.AdvanceTo(0); Require(!f.Has(AchievementId.ProfitRoundTrip));
        });
        Check("withdrawal in one market does not disqualify another", () => {
            var f = new Fixture(); f.Market.Deposit(100); f.Market.Withdraw(1);
            var other=f.Book.Get(6001); other.Deposit(100); other.State.RealizedPnL=10001; Funds(other);
            Require(f.Has(AchievementId.Leaderboard));
        });
        Check("building coin purchases count as withdrawals; completed achievements stay complete", () => {
            var f=new Fixture(MarketCatalog.BuildingCoin); f.Market.Deposit(100);
            f.Market.SettleExchange(1,0,true); f.Market.State.RealizedPnL=10001; Funds(f.Market);
            Require(!f.Has(AchievementId.Leaderboard) && !f.State.DoubledMarkets.Contains(MarketCatalog.BuildingCoin));
            f.State.Award(AchievementId.Leaderboard); Funds(f.Market); Require(f.Has(AchievementId.Leaderboard));
        });
        Check("ten positions counts coin market and a newly enabled resource", () => {
            var f = new Fixture(); f.Book.SetResourceEnabled(1001,true,0); f.Tracker.AttachMarkets();
            foreach(var m in f.Book.Markets) { m.AdvanceTo(0); m.Deposit(100); m.Open(PositionSide.Long,10,1); }
            Require(f.Has(AchievementId.TenPositions) && f.Has(AchievementId.CoinTrader));
        });
        Check("coin deposit without opening is not coin trading", () => {
            var f = new Fixture(MarketCatalog.BuildingCoin); f.Market.Deposit(100); Require(!f.Has(AchievementId.CoinTrader));
            f.Market.Open(PositionSide.Short,10,1); Require(f.Has(AchievementId.CoinTrader));
        });
        Check("all exchange item IDs count, repeat purchases do not", () => {
            var f = new Fixture(MarketCatalog.BuildingCoin);
            for(int i=0;i<20;i++) f.Market.NotifyBuildingPurchase(1131,1);
            Require(f.State.PurchasedKindCount==1);
            foreach(int id in new[]{2001,2002,2003,2101,2102,5001,5002,5101}) f.Market.NotifyBuildingPurchase(id,1);
            Require(!f.Has(AchievementId.TenBuildings)); f.Market.NotifyBuildingPurchase(5102,1);
            Require(f.Has(AchievementId.TenBuildings) && f.State.PurchasedKindCount==0);
        });
        Check("big purchase strictly exceeds 100 actual received", () => {
            var f = new Fixture(MarketCatalog.BuildingCoin); f.Market.NotifyBuildingPurchase(2001,0);
            Require(f.State.PurchasedKindCount==0); f.Market.NotifyBuildingPurchase(2001,100);
            Require(!f.Has(AchievementId.BigPurchase)); f.Market.NotifyBuildingPurchase(2001,101); Require(f.Has(AchievementId.BigPurchase));
        });
        Check("A9 strictly exceeds threshold and includes floating equity", () => {
            var f = new Fixture(MarketCatalog.BuildingCoin); f.Market.Deposit(100000000); Require(!f.Has(AchievementId.A9));
            f.Market.Open(PositionSide.Long,100,1); SetMid(f.Market,2); Quote(f.Market); Require(f.Has(AchievementId.A9));
        });
        Check("building freedom excludes funding and uses realized net", () => {
            var f = new Fixture(MarketCatalog.BuildingCoin); f.Market.Deposit(1000000001); Require(!f.Has(AchievementId.BuildingFreedom));
            f.Market.State.RealizedPnL=999999999; Funds(f.Market); Require(!f.Has(AchievementId.BuildingFreedom));
            f.Market.State.RealizedPnL=1000000000; Funds(f.Market); Require(f.Has(AchievementId.BuildingFreedom));
        });
        Check("no factory only counts universe matrix realized result", () => {
            var f = new Fixture(6005); f.Market.State.RealizedPnL=100000; Funds(f.Market); Require(!f.Has(AchievementId.NoFactory));
            var m = f.Book.Get(6006); m.State.RealizedPnL=100000; Funds(m); Require(f.Has(AchievementId.NoFactory));
        });
        Check("leaderboard is strictly above 10000 percent with positive net funding", () => {
            var f = new Fixture(); f.Market.Deposit(100); f.Market.State.RealizedPnL=10000; Funds(f.Market); Require(!f.Has(AchievementId.Leaderboard));
            f.Market.State.RealizedPnL=10000.01m; Funds(f.Market); Require(f.Has(AchievementId.Leaderboard));
            var g = new Fixture(); g.Market.State.RealizedPnL=100000; Funds(g.Market); Require(!g.Has(AchievementId.Leaderboard));
        });
        Check("master requires all other seventeen; completed notifications occur once", () => {
            var s = new TradingAchievementState(); foreach(var a in AchievementCatalog.All.Where(a=>a.Id!=AchievementId.Master).Take(16)) s.Award(a.Id);
            Require(!s.AllComplete); s.Award(AchievementId.Leaderboard); Require(s.AllComplete && s.Pending.Count==18);
            s.Award(AchievementId.Leaderboard); Require(s.Pending.Count==18); s.ClearPending(); s.Award(AchievementId.NewTrader); Require(s.Pending.Count==0);
        });
        Check("state persists completed, pending and unfinished tracking without growth", () => {
            var s = new TradingAchievementState(); s.Initialized=true; s.Award(AchievementId.NewTrader);
            s.PurchasedKinds.Add(5001); s.DoubledMarkets.Add(6006); s.FullPositions[1002]=900;
            var r=RoundTrip(s); Require(r.Initialized && r.Completed(AchievementId.NewTrader) && r.Pending.Count==1
                && r.PurchasedKinds.Contains(5001) && r.DoubledMarkets.Contains(6006) && r.FullPositions[1002]==900);
            r.AcknowledgeFirst(); Require(RoundTrip(r).Pending.Count==0);
        });
        Check("completed quote achievements remove quote monitoring", () => {
            var f = new Fixture(); f.State.Award(AchievementId.FloatingLoss); f.State.Award(AchievementId.PatientCapital); f.State.Award(AchievementId.A9);
            Funds(f.Market); Require(f.Book.Markets.All(m=>!m.ObserveAchievementQuotes));
            foreach(var a in AchievementCatalog.All.Where(a=>a.Id!=AchievementId.Leaderboard && a.Id!=AchievementId.Master)) f.State.Award(a.Id);
            f.Market.State.RealizedPnL=10001; f.Market.Deposit(100);
            Funds(f.Market); Require(f.Book.Markets.All(m=>m.AchievementObserver==null));
        });
        Check("market quote observer runs per new quote rather than per frame", () => {
            var m=new MarginMarket(); m.AdvanceTo(0); m.Deposit(100); m.Open(PositionSide.Long,10,1);
            int count=0; m.ObserveAchievementQuotes=true;
            m.AchievementObserver=(market,kind,a,b)=> { if(kind==AchievementEvent.Quote) count++; };
            for(int tick=1;tick<=89;tick++) m.AdvanceTo(tick); Require(count==0);
            m.AdvanceTo(90); Require(count==1); m.AdvanceTo(270); Require(count==3);
        });
        Console.WriteLine("Achievement checks passed: "+passed);
    }
    private static Fixture Open() { var f=new Fixture(); f.Market.Deposit(100); f.Market.Open(PositionSide.Long,10,1); return f; }
    private static SpotMarket Feed(MarginMarket m) => (SpotMarket)typeof(MarginMarket).GetField("_feed",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(m);
    private static void SetMid(MarginMarket m,decimal p) { Feed(m).State.MidPrice=p; }
    private static void Quote(MarginMarket m) { m.AchievementObserver?.Invoke(m,AchievementEvent.Quote,0,0); }
    private static void Funds(MarginMarket m) { m.AchievementObserver?.Invoke(m,AchievementEvent.FundsChanged,0,0); }
    private static TradingAchievementState RoundTrip(TradingAchievementState state) {
        using(var s=new MemoryStream()) { using(var w=new BinaryWriter(s,System.Text.Encoding.UTF8,true)) state.Export(w); s.Position=0; using(var r=new BinaryReader(s)) return TradingAchievementState.Import(r); }
    }
    private static void Require(bool value) { if(!value) throw new Exception("Achievement invariant failed."); }
    private static void Check(string name,Action action) { action(); passed++; Console.WriteLine("PASS "+name); }
}
