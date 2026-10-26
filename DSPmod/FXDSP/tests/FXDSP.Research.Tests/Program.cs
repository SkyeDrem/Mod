using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using FXDSP.Core;
using FXDSP.Runtime;

internal static class Program
{
    private static int passed;
    private static void Main()
    {
        Check("fresh saves run building currency only and every item market is unselected", () => {
            var book = new MarketBook();
            Require(book.RunningMarkets.Count == 1 && book.IsEnabled(MarketCatalog.BuildingCoin));
            foreach (int id in MarketCatalog.ItemIds) if (id != MarketCatalog.BuildingCoin) Require(!book.IsEnabled(id));
            var before = Bytes(book.Get(MarketCatalog.Copper));
            foreach (var market in book.RunningMarkets) market.AdvanceTo(100000);
            Equal(before, Bytes(book.Get(MarketCatalog.Copper)));
            var loaded = LoadBook(BookBytes(book)); Require(loaded.RunningMarkets.Count == 1 && !loaded.IsEnabled(6001));
        });
        Check("access technology consumes exactly ten iron and ten copper via native research points", () => {
            var tech = TradeResearch.Definitions.Single(node => node.Id == TradeResearch.AccessTech);
            Require(tech.Id < 2000 && tech.Items.SequenceEqual(new[] { 1001, 1002 }) && tech.Cost == 10);
            Require(tech.ItemPoints.All(points => tech.HashNeeded * points / 3600m == 10m));
        });
        Check("five exchange tiers require their corresponding matrix with exact costs and sequential prerequisites", () => {
            int[] expected = { 200, 400, 1000, 2000, 4000 };
            for (int tier = 1; tier <= 5; tier++) {
                var tech = TradeResearch.Definitions.Single(node => node.Id == TradeResearch.ExchangeTech(tier));
                Require(tech.Items.SequenceEqual(new[] { 6000 + tier }) && tech.Cost == expected[tier - 1]);
                Require(tech.Previous == (tier == 1 ? TradeResearch.AccessTech : TradeResearch.ExchangeTech(tier - 1)));
                Require(tech.HashNeeded * tech.ItemPoints[0] / 3600m == expected[tier - 1]);
                Require(tech.Id >= 2000 && tech.Upgrade && tech.Level == tier && tech.ExchangeTier == tier);
            }
        });
        Check("six upgrades cost every specified matrix individually and white alone at level six", () => {
            int[] costs = { 400, 800, 1200, 1600, 2000, 4000 };
            for (int level = 1; level <= 6; level++) {
                var tech = TradeResearch.Definitions.Single(node => node.Id == TradeResearch.FeeTech(level));
                Require(tech.Upgrade && tech.Id >= 2000 && tech.Id < 12000);
                Require(tech.Items.SequenceEqual(level == 6 ? new[] { 6006 } : Enumerable.Range(6001, level)));
                Require(tech.ItemPoints.All(points => tech.HashNeeded * points / 3600m == costs[level - 1]));
                Require(tech.Previous == (level == 1 ? TradeResearch.AccessTech : TradeResearch.FeeTech(level - 1)));
            }
            Require(TradeResearch.Definitions.Select(node => node.Id).Distinct().Count() == 12);
        });
        Check("exchange gates require both platform access and the exact requested tier", () => {
            var flags = new HashSet<int>(); var state = TradeResearch.Read(flags.Contains);
            Reject(state.RequireAccess); Reject(() => state.RequireExchange(1));
            flags.Add(TradeResearch.ExchangeTech(5)); state = TradeResearch.Read(flags.Contains); Reject(() => state.RequireExchange(5));
            flags.Add(TradeResearch.AccessTech); flags.Add(TradeResearch.ExchangeTech(1)); state = TradeResearch.Read(flags.Contains);
            state.RequireExchange(1); Require(!state.CanExchange(2)); Reject(() => state.RequireExchange(2));
        });
        Check("fee research changes only after the full prerequisite chain is present", () => {
            var flags = new HashSet<int> { TradeResearch.AccessTech, TradeResearch.FeeTech(6) };
            Require(TradeResearch.Read(flags.Contains).FeeLevel == 0);
            decimal[] trade = { .003m, .002m, .001m, .0005m, .0002m, .0001m, 0m };
            decimal[] exchange = { .10m, .10m, .08m, .06m, .04m, .02m, 0m };
            flags.Remove(TradeResearch.FeeTech(6));
            for (int level = 0; level <= 6; level++) {
                if (level > 0) flags.Add(TradeResearch.FeeTech(level));
                var state = TradeResearch.Read(flags.Contains);
                Require(state.FeeLevel == level && state.TradingFee == trade[level] && state.ExchangeFee == exchange[level]);
            }
        });
        Check("held positions retain their actual entry fee when research reduces the closing fee", () => {
            var market = new MarginMarket(feeRate: .003m); market.Deposit(1000); market.Open(PositionSide.Long, 100, 10);
            Require(market.Position.OpeningFee == 3m); decimal priorPaid = market.State.FeesPaid;
            market.SetFeeRate(0); var loaded = LoadMarket(Bytes(market)); Require(loaded.Position.OpeningFee == 3m && loaded.FeeRate == 0);
            loaded.Close(); Require(loaded.LastSettlement.Fees == 3m && loaded.State.FeesPaid == priorPaid);
            Require(loaded.LastSettlement.NetPnL == loaded.State.Balance - 1000m);
        });
        Check("zero fee applies to new opens closes and both building exchange directions", () => {
            var market = new MarginMarket(feeRate: 0); market.Deposit(100); market.Open(PositionSide.Short, 10, 100); market.Close();
            Require(market.State.FeesPaid == 0 && market.LastSettlement.Fees == 0);
            var wallet = new MarginMarket(MarketCatalog.BuildingCoin); wallet.Deposit(100);
            wallet.SettleExchange(50, BuildingExchangeRules.Fee(50, 0), true); wallet.SettleExchange(50, BuildingExchangeRules.Fee(50, 0), false);
            Require(wallet.State.Balance == 100 && wallet.State.FeesPaid == 0);
        });
        Check("building exchanges start at ten percent in both directions", () => {
            var wallet = new MarginMarket(MarketCatalog.BuildingCoin); wallet.Deposit(100);
            wallet.SettleExchange(10, BuildingExchangeRules.Fee(10), true); Require(wallet.State.Balance == 89);
            wallet.SettleExchange(10, BuildingExchangeRules.Fee(10), false); Require(wallet.State.Balance == 98 && wallet.State.FeesPaid == 2);
        });
        Check("all accounts including dormant caches and future markets inherit the researched fee", () => {
            var book = new MarketBook(); Require(book.Get(MarketCatalog.BuildingCoin).FeeRate == .003m);
            book.SetTradingFeeRate(.0001m); book.SetResourceEnabled(1001, true, 100);
            Require(book.Get(1001).FeeRate == .0001m);
            var loaded = LoadBook(BookBytes(book)); foreach(var market in loaded.Markets) Require(market.FeeRate == .0001m);
            Require(!loaded.IsEnabled(MarketCatalog.Copper)); loaded.SetResourceEnabled(1003, true, 200);
            Require(loaded.Get(1003).FeeRate == .0001m);
        });
        Check("changing commission invalidates cached liquidation prices without changing position principal", () => {
            var market = new MarginMarket(feeRate: .003m); market.Deposit(1000); market.Open(PositionSide.Long, 100, 10);
            decimal previous = market.LiquidationPrice; market.SetFeeRate(0);
            Require(market.LiquidationPrice < previous && market.Position.Margin == 100 && market.Position.OpeningFee == 3);
        });
        Check("transfer sliders safely map unlimited stock and Int32 endpoints to exact integer quantities", () => {
            Require(TransferAmounts.Maximum(BigInteger.Pow(10, 50)) == int.MaxValue);
            Require(TransferAmounts.Maximum(-1) == 0 && TransferAmounts.FromSlider(1000, int.MaxValue) == int.MaxValue);
            Require(TransferAmounts.FromSlider(500, 100) == 50 && TransferAmounts.ToSlider(50, 100) == 500);
            Require(TransferAmounts.FromSlider(1000, 1) == 1 && TransferAmounts.FromSlider(1000, 0) == 0);
            Require(TransferAmounts.ToSlider(int.MaxValue, 1) == 1000);
        });
        Check("technology display names do not repeat levels and registration names stay unique", () => {
            Require(TradeResearch.Definitions.First().Name == "交易系统" && TradeResearch.Definitions.First().Level == 0);
            for (int tier = 1; tier <= 5; tier++) {
                var node = TradeResearch.Definitions.Single(n => n.Id == TradeResearch.ExchangeTech(tier));
                Require(node.Name == "建筑兑换" && node.Level == tier && node.ImplicitPrevious == (tier == 1));
            }
            for (int level = 1; level <= 6; level++) {
                var node = TradeResearch.Definitions.Single(n => n.Id == TradeResearch.FeeTech(level));
                Require(node.Name == "交易效率" && node.Level == level && node.ImplicitPrevious == (level == 1));
            }
            Require(TradeResearch.Definitions.Select(n => n.RegistrationName).Distinct().Count() == 12);
        });
        Check("all six dark fog drops support independent selected frozen and saved markets", () => {
            var book = new MarketBook();
            for (int id = 5201; id <= 5206; id++) {
                Require(MarketCatalog.IsSelectable(id) && MarketCatalog.Contains(id) && !book.IsEnabled(id));
                Require(MarketCatalog.Category(id) == MarketCategory.Materials);
                book.SetResourceEnabled(id, true, 100); book.Get(id).Deposit(id);
            }
            var loaded = LoadBook(BookBytes(book));
            for (int id = 5201; id <= 5206; id++) {
                Require(loaded.IsEnabled(id) && loaded.Get(id).State.Balance == id);
                loaded.SetResourceEnabled(id, false, 100); var before = Bytes(loaded.Get(id));
                foreach (var market in loaded.RunningMarkets) market.AdvanceTo(200);
                Equal(before, Bytes(loaded.Get(id)));
            }
        });
        Check("recipe derived exchange prices use the requested conveyor example and reject unpriced items", () => {
            Require(BuildingExchangeRules.UnitValue(2001) == 7 && BuildingExchangeRules.UnitValue(2002) == 242);
            Require(BuildingExchangeRules.UnitValue(2103) == 153405 && BuildingExchangeRules.UnitValue(1131) == 45);
            Require(BuildingExchangeRules.UnitValue(2003) == 2409 && BuildingExchangeRules.UnitValue(2104) == 586845);
            long price; Require(!BuildingValues.TryGet(11702, out price) && !BuildingValues.TryGet(11703, out price));
            Reject(() => BuildingExchangeRules.UnitValue(99999));
        });
        Check("the production catalog accounts for every native item including sand and all dark fog drops", () => {
            using var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "production-recipes.json")));
            LDB.items = new ItemSet { dataArray = json.RootElement.GetProperty("Items").EnumerateArray().Select(item => new ItemProto {
                ID = item.GetProperty("ID").GetInt32(), name = item.GetProperty("Name").GetString(),
                Type = (EItemType)item.GetProperty("Type").GetInt32(), StackSize = item.GetProperty("StackSize").GetInt32(),
                CanBuild = item.GetProperty("CanBuild").GetInt32() != 0
            }).ToArray() };
            var options = ResourceMarketCatalog.GetOptions(); var selectable = options.Select(option => option.ItemId).ToHashSet();
            Require(selectable.Count == 104 && options.Count == 104 && LDB.items.dataArray.Length == 175);
            int exchange = 0;
            foreach (var item in LDB.items.dataArray) {
                bool eligible = BuildingExchangeRules.IsEligible(item.ID, item.CanBuild);
                if (eligible) { exchange++; Require(BuildingExchangeRules.UnitValue(item.ID) > 0); }
                Require(selectable.Contains(item.ID) == !eligible);
            }
            Require(exchange == 71);
            for (int id = 5201; id <= 5206; id++) ResourceMarketCatalog.RequireResource(id);
            ResourceMarketCatalog.RequireResource(1211); ResourceMarketCatalog.RequireResource(1804);
            ResourceMarketCatalog.RequireResource(1099); Reject(() => ResourceMarketCatalog.RequireResource(2103));
            Require(MarketCatalog.Category(1099) == MarketCategory.Consumables);
            Require(options.Count(option => MarketCatalog.Category(option.ItemId) == MarketCategory.Matrices) == 6);
        });
        Check("first visit survives saving and v3 imports while fresh books show it once", () => {
            var book = new MarketBook(); Require(!book.IntroductionSeen && book.AcknowledgeIntroduction() && !book.AcknowledgeIntroduction());
            Require(LoadBook(BookBytes(book)).IntroductionSeen && !new MarketBook().IntroductionSeen);
            var v4 = BookBytes(book); var v3 = v4.Take(v4.Length - 1).ToArray(); BitConverter.GetBytes(3).CopyTo(v3, 4);
            var loaded = LoadBook(v3); Require(!loaded.IntroductionSeen && loaded.AcknowledgeIntroduction());
        });
        Check("transfer selections retain the independent ratio across items sources and empty balances", () => {
            var deposit = new TransferSelection(); var withdraw = new TransferSelection();
            deposit.Refresh(100, true); withdraw.Refresh(100, true);
            deposit.SetSlider(500, 100); withdraw.SetSlider(250, 100);
            deposit.Refresh(800, true); withdraw.Refresh(800, true);
            Require(deposit.Quantity == 400 && deposit.Step == 500 && withdraw.Quantity == 200 && withdraw.Step == 250);
            deposit.Refresh(0, true); Require(deposit.Step == 500 && deposit.Quantity == 0);
            deposit.Refresh(200, true); Require(deposit.Quantity == 100 && deposit.Step == 500);
            deposit.SetInput(75, 100); deposit.Refresh(200, true); Require(deposit.Quantity == 150 && deposit.Step == 750);
            deposit.SetSlider(500, 3); deposit.Refresh(3, false); Require(deposit.Step == 500 && deposit.Quantity == 1);
            deposit.Refresh(100, true); Require(deposit.Quantity == 50 && deposit.Step == 500);
        });
        Check("exchange slider accounts for both affordable buys and owned sells including exact fee rounding", () => {
            Require(ExchangeAmounts.Maximum(100, 6, .10m, 0) == 15);
            Require(ExchangeAmounts.Maximum(0, 6, .10m, 30) == 30);
            Require(ExchangeAmounts.Maximum(100, 6, 0, 0) == 16);
            Require(ExchangeAmounts.Maximum(1.0000001m, 1, .0000001m, 0) == 0);
            Require(ExchangeAmounts.Maximum(decimal.MaxValue, 1, 0, 0) == int.MaxValue);
        });
        Check("profit formatting uses grouping everywhere without changing price precision or source logs", () => {
            Require(MoneyDisplay.Signed(12345.678m) == "+12,345.68" && MoneyDisplay.Signed(-12345.678m) == "-12,345.68");
            Require(MoneyDisplay.Signed(-.001m) == "0.00");
            Require(TradingLogDisplay.Format("已平仓：本次+12345.678 铜矿。") == "已平仓：本次+12,345.68 铜矿。");
            Require(TradingLogDisplay.Format("开多：12345.678 铜矿保证金") == "开多12,345.68保证金");
            Require(TradingLogDisplay.Format("已平仓：本次+12,345.68 铜矿。") == "已平仓：本次+12,345.68 铜矿。");
        });
        Check("technology effect copy and bottom copy are distinct exact user text", () => {
            var access = TradeResearch.Find(TradeResearch.AccessTech);
            Require(access.UnlockText == "开放DSP交易平台");
            Require(access.Description == "老实拍工厂很枯燥吧？创建DSP交易账户，接入主脑交易平台，FX战士伊卡洛斯！");
            Require(access.Previous == 0 && !access.Upgrade);
            for (int tier = 1; tier <= 5; tier++) {
                var node = TradeResearch.Find(TradeResearch.ExchangeTech(tier));
                Require(node.UnlockText == "开放建筑币市场" + tier + "级建筑交易");
                Require(node.Description == "在建筑币市场中自由交易兑换各种建筑，或许也是一种建筑超市。");
            }
            for (int level = 1; level <= 6; level++) {
                var node = TradeResearch.Find(TradeResearch.FeeTech(level));
                Require(node.UnlockText.Split('\n').Length == 2 && node.UnlockText.StartsWith("交易手续费降低至"));
                Require(node.Description == (level == 6 ? "感谢你对主脑作出的贡献，免除交易手续费！" : "为奖励伊卡洛斯上传矩阵提供的算力，降低交易佣金。"));
            }
            Require(TradeResearch.Find(1001) == null);
        });
        Check("return rate uses cumulative realized profit over net deposits and remains read only", () => {
            Require(AccountPerformance.ReturnRatio(25, 200, 100) == .25m);
            Require(MoneyDisplay.Percent(AccountPerformance.ReturnRatio(25, 200, 100)) == "+25.00%");
            Require(MoneyDisplay.Percent(AccountPerformance.ReturnRatio(-25, 200, 0)) == "-12.50%");
            Require(MoneyDisplay.Percent(AccountPerformance.ReturnRatio(0, 200, 0)) == "0.00%");
            Require(MoneyDisplay.Percent(AccountPerformance.ReturnRatio(25, 100, 100)) == "—");
            Require(MoneyDisplay.Percent(AccountPerformance.ReturnRatio(25, 100, 200)) == "—");
            Require(AccountPerformance.ReturnRatio(1, long.MaxValue, long.MaxValue - 1) == 1);
            Require(MoneyDisplay.Percent(AccountPerformance.ReturnRatio(decimal.MaxValue, 1, 0)).EndsWith("%"));
            var market = new MarginMarket(); market.Deposit(100); market.Withdraw(25); market.Open(PositionSide.Long, 10, 2);
            var before = Bytes(market); Require(AccountPerformance.ReturnRatio(market.State.RealizedPnL, market.State.TotalDeposited, market.State.TotalWithdrawn) == market.State.RealizedPnL / 75);
            Equal(before, Bytes(market));
        });
        Check("unrealized return uses position margin and preserves sign precision and no-position state", () => {
            Require(MoneyDisplay.Percent(AccountPerformance.UnrealizedReturnRatio(25, 100)) == "+25.00%");
            Require(MoneyDisplay.Percent(AccountPerformance.UnrealizedReturnRatio(-25, 100)) == "-25.00%");
            Require(MoneyDisplay.Percent(AccountPerformance.UnrealizedReturnRatio(0, 100)) == "0.00%");
            Require(MoneyDisplay.Percent(AccountPerformance.UnrealizedReturnRatio(100, 10)) == "+1,000.00%");
            Require(MoneyDisplay.Percent(AccountPerformance.UnrealizedReturnRatio(1, 0)) == "—");
            Require(MoneyDisplay.Percent(AccountPerformance.UnrealizedReturnRatio(1, -1)) == "—");
            Require(AccountPerformance.UnrealizedReturnRatio(.0000001m, .000001m) == .1m);
        });
        Check("position return does not apply leverage twice or alter live margin and account data", () => {
            foreach (var side in new[] { PositionSide.Long, PositionSide.Short })
            {
                var market = new MarginMarket(); market.Deposit(200); market.Open(side, 40, 5);
                var before = Bytes(market);
                Require(AccountPerformance.UnrealizedReturnRatio(market.UnrealizedPnL, market.UsedMargin) == market.UnrealizedPnL / 40m);
                Require(AccountPerformance.UnrealizedReturnRatio(market.UnrealizedPnL, market.UsedMargin) != market.UnrealizedPnL / market.Position.Notional);
                Equal(before, Bytes(market));
                market.Close(); Require(!AccountPerformance.UnrealizedReturnRatio(market.UnrealizedPnL, market.UsedMargin).HasValue);
            }
        });
        Check("holding time counts completed candle intervals and clamps invalid time differences", () => {
            long interval = MarginMarket.QuoteIntervalTicks, open = 17;
            Require(AccountPerformance.HeldCandles(open, open) == 0);
            Require(AccountPerformance.HeldCandles(open, open + interval - 1) == 0);
            Require(AccountPerformance.HeldCandles(open, open + interval) == 1);
            Require(AccountPerformance.HeldCandles(open, open + interval * 2 - 1) == 1);
            Require(AccountPerformance.HeldCandles(open, open + interval * 2) == 2);
            Require(AccountPerformance.HeldCandles(open, open - 1) == 0 && AccountPerformance.HeldCandles(-1, 100) == 0);
            Require(AccountPerformance.HeldCandles(0, long.MaxValue) == long.MaxValue / interval);
        });
        Check("holding time survives snapshots beyond retained history and is read only", () => {
            var market = new MarginMarket(); market.Deposit(1000); market.AdvanceTo(60); market.Open(PositionSide.Long, 40, 1);
            long open = market.Position.OpenTick;
            market.AdvanceTo(open + MarginMarket.QuoteIntervalTicks * 200 + 13);
            Require(market.Position != null && market.History.Count == SpotMarket.HistoryLimit);
            var before = Bytes(market);
            Require(AccountPerformance.HeldCandles(market.Position.OpenTick, market.State.LastGameTick) == 200);
            Equal(before, Bytes(market));
            var restored = LoadMarket(before);
            Require(restored.Position.OpenTick == open && AccountPerformance.HeldCandles(restored.Position.OpenTick, restored.State.LastGameTick) == 200);
        });
        Check("every technology supplies a distinct valid native inline icon tag for prerequisite names", () => {
            var tags = TradeResearch.Definitions.Select(node => node.InlineIconTag).ToArray();
            Require(tags.Length == 12 && tags.Distinct().Count() == 12);
            foreach (var node in TradeResearch.Definitions) {
                Require(node.InlineIconTag.Length == 6 && node.InlineIconTag.StartsWith("fx") && node.InlineIconTag.EndsWith(node.Id.ToString()));
                Require(node.InlineIconTag.All(character => character >= 'a' && character <= 'z' || character >= '0' && character <= '9'));
                Require(!string.IsNullOrEmpty(node.Name));
            }
        });
        Console.WriteLine("All " + passed + " research and progression checks passed.");
    }
    private static byte[] Bytes(MarginMarket market) { using(var s=new MemoryStream())using(var w=new BinaryWriter(s)){market.Export(w);return s.ToArray();} }
    private static byte[] BookBytes(MarketBook book) { using(var s=new MemoryStream())using(var w=new BinaryWriter(s)){book.Export(w);return s.ToArray();} }
    private static MarginMarket LoadMarket(byte[] bytes) { using(var s=new MemoryStream(bytes))using(var r=new BinaryReader(s))return MarginMarket.Import(r); }
    private static MarketBook LoadBook(byte[] bytes) { using(var s=new MemoryStream(bytes))using(var r=new BinaryReader(s))return MarketBook.Import(r); }
    private static void Check(string name, Action action) { action(); passed++; Console.WriteLine("PASS " + name); }
    private static void Require(bool value) { if(!value) throw new Exception("Progression invariant failed."); }
    private static void Equal(byte[] a, byte[] b) { Require(a.SequenceEqual(b)); }
    private static void Reject(Action action) { try { action(); } catch(InvalidOperationException){return;} throw new Exception("Expected research gate rejection."); }
}
