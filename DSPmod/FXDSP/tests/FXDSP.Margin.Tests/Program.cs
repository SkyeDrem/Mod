using System;
using System.IO;
using System.Reflection;
using FXDSP.Core;

internal static class Program
{
    private static int _passed;
    private static void Main()
    {
        Check("zero funds cannot open or withdraw", () => {
            var m = new MarginMarket(); Reject(() => m.Open(PositionSide.Long, 1, 5)); Reject(() => m.Withdraw(1));
            Near(0, m.Equity); Require(m.Position == null);
        });
        Check("deposit and withdraw are one copper per ore", () => {
            var m = new MarginMarket(); m.Deposit(10); Near(10, m.State.Balance); m.Withdraw(3);
            Near(7, m.State.Balance); Near(0, m.NetPnL); Require(m.State.TotalDeposited == 10 && m.State.TotalWithdrawn == 3);
        });
        Check("opening reserves margin plus notional fee", () => {
            var m = new MarginMarket(); m.Deposit(100); Near(.2m, m.OpenFee(10, 20));
            Near(10.2m, m.OpenCost(10, 20)); m.Open(PositionSide.Long, 10, 20);
            Near(89.8m, m.State.Balance); Near(10, m.UsedMargin); Near(200, m.Position.Notional);
            Near(-.2m, m.State.RealizedPnL);
        });
        Check("long profits from a higher index", () => {
            var m = new MarginMarket(); m.Deposit(100); m.Open(PositionSide.Long, 10, 5);
            var entry = m.Position.EntryPrice; SetMid(m, 1.1m);
            var pnl = 50m * (m.Bid - entry) / entry; Near(pnl, m.UnrealizedPnL); Require(pnl > 0);
            var exitFee = Fee(50m * m.Bid / entry); m.Close();
            Near(100m - .05m + pnl - exitFee, m.State.Balance);
            Near(pnl - .05m - exitFee, m.State.RealizedPnL); Require(m.Position == null);
        });
        Check("short profits from a lower index", () => {
            var m = new MarginMarket(); m.Deposit(100); m.Open(PositionSide.Short, 10, 5);
            var entry = m.Position.EntryPrice; SetMid(m, .9m);
            var pnl = 50m * (entry - m.Ask) / entry; Near(pnl, m.UnrealizedPnL); Require(pnl > 0);
            m.Close(); Require(m.State.Balance > 100m);
        });
        Check("both directions pay spread and fees on immediate roundtrip", () => {
            foreach (var side in new[] { PositionSide.Long, PositionSide.Short }) {
                var m = new MarginMarket(); m.Deposit(100); m.Open(side, 10, 10); m.Close();
                Require(m.State.Balance < 100m && m.NetPnL < 0m);
            }
        });
        Check("margin and floating profits cannot be withdrawn", () => {
            var m = new MarginMarket(); m.Deposit(10); m.Open(PositionSide.Long, 1, 5);
            byte[] before = Bytes(m); Reject(() => m.Withdraw(9)); Equal(before, Bytes(m));
            SetMid(m, 1.5m); Require(m.Equity > 10m); Reject(() => m.Withdraw(10));
            m.Withdraw(8); Near(.995m, m.State.Balance);
        });
        Check("gap liquidation cannot debit uncommitted copper", () => {
            var m = new MarginMarket(); m.Deposit(100); m.Open(PositionSide.Long, 10, 50);
            decimal free = m.State.Balance; SetMid(m, .2m); m.AdvanceTo(0);
            Require(m.Position == null); Near(free, m.State.Balance); Near(-10.5m, m.State.RealizedPnL);
            Require(m.State.Balance >= 0 && m.RecentLogs[m.RecentLogs.Count - 1].Contains("爆仓"));
        });
        Check("batched ticks check intermediate liquidation prices", () => {
            var a = new MarginMarket(); a.Deposit(100); a.AdvanceTo(0); a.Open(PositionSide.Long, 1, 50);
            var b = Read(Bytes(a)); a.AdvanceTo(60000);
            for (long tick = 300; tick <= 60000; tick += 300) b.AdvanceTo(tick);
            Equal(Bytes(a), Bytes(b));
        });
        Check("invalid leverage or second position fails atomically", () => {
            var m = new MarginMarket(); m.Deposit(100);
            foreach (var leverage in new[] { 0, -1, 101 }) { byte[] before = Bytes(m); Reject(() => m.Open(PositionSide.Long, 1, leverage)); Equal(before, Bytes(m)); }
            Reject(() => m.Open(PositionSide.Long, 0, 5)); m.Open(PositionSide.Short, 2, 5);
            byte[] active = Bytes(m); Reject(() => m.Open(PositionSide.Long, 1, 5)); Equal(active, Bytes(m));
        });
        Check("position serialization retains assets and RNG continuity", () => {
            var a = new MarginMarket(); a.Deposit(100); a.AdvanceTo(101); a.Open(PositionSide.Short, 2, 10);
            a.AdvanceTo(701); var b = Read(Bytes(a)); Equal(Bytes(a), Bytes(b));
            a.AdvanceTo(3701); b.AdvanceTo(3701); Equal(Bytes(a), Bytes(b));
        });
        Check("every truncated margin record is rejected", () => {
            var m = new MarginMarket(); m.Deposit(10); m.AdvanceTo(0); m.Open(PositionSide.Long, 1, 5);
            byte[] complete = Bytes(m);
            for (int n = 0; n < complete.Length; n++) {
                var shortRecord = new byte[n]; Array.Copy(complete, shortRecord, n);
                bool failed = false; try { Read(shortRecord); } catch (InvalidDataException) { failed = true; }
                Require(failed);
            }
        });
        Check("v0.2 copper custody migrates one-to-one without price multiplication", () => {
            var old = new SpotMarket(); old.Deposit(10); old.AdvanceTo(1000);
            var migrated = Read(LegacyBytes(old)); Near(10, migrated.State.Balance);
            Near(0, migrated.NetPnL); Require(migrated.State.MigratedFromSpot);
            Near(old.State.MidPrice, migrated.MidPrice);
            var control = Read(Bytes(migrated));
            migrated.AdvanceTo(4000); control.AdvanceTo(4000); Equal(Bytes(migrated), Bytes(control));
        });
        Check("legacy credit is converted once at old executable buy cost", () => {
            var old = new SpotMarket(); old.Deposit(100); old.AdvanceTo(101); old.Sell(40);
            var expected = old.State.ItemQuantity + old.State.SettlementBalance / (old.Ask * (1m + old.FeeRate));
            var migrated = Read(LegacyBytes(old)); Near(expected, migrated.State.Balance);
            var reloaded = Read(Bytes(migrated)); Near(expected, reloaded.State.Balance);
            Near(expected - 100m, reloaded.State.RealizedPnL);
        });
        Check("funding-adjusted equity agrees with displayed profit", () => {
            var m = new MarginMarket(); m.AdvanceTo(0); m.Deposit(100);
            m.Open(PositionSide.Long, 1, 5); m.AdvanceTo(1800);
            Near(m.NetPnL, m.Equity + m.State.TotalWithdrawn - m.State.TotalDeposited);
            if (m.Position != null) m.Close(); m.Withdraw(1);
            Near(m.NetPnL, m.Equity + m.State.TotalWithdrawn - m.State.TotalDeposited);
        });
        Check("opening overflow does not reserve or lose copper", () => {
            var m = new MarginMarket(); m.Deposit(100); byte[] before = Bytes(m);
            Reject(() => m.Open(PositionSide.Long, decimal.MaxValue, 50)); Equal(before, Bytes(m));
        });
        Check("every slider leverage is accepted and survives serialization", () => {
            for (int leverage = 1; leverage <= 100; leverage++) {
                var m = new MarginMarket(); m.Deposit(100); m.Open(PositionSide.Long, 1, leverage);
                Require(m.Position.Leverage == leverage);
                var restored = Read(Bytes(m)); Require(restored.Position.Leverage == leverage);
            }
        });
        Check("allocation includes fees and full allocation is affordable at every leverage", () => {
            for (int leverage = 1; leverage <= 100; leverage++)
                foreach (int percent in new[] { 1, 25, 50, 75, 100 }) {
                    var m = new MarginMarket(); m.Deposit(17);
                    decimal margin = m.MarginForAllocation(percent, leverage);
                    decimal cost = m.OpenCost(margin, leverage), budget = 17m * percent / 100m;
                    Require(cost <= budget && budget - cost < .000003m);
                    m.Open(PositionSide.Long, margin, leverage); Require(m.State.Balance >= 0);
                }
        });
        Check("allocation rejects invalid percentages and handles dust without mutation", () => {
            var m = new MarginMarket(); Near(0, m.MarginForAllocation(100, 100));
            m.State.Balance = .000001m;
            byte[] before = Bytes(m); Near(0, m.MarginForAllocation(100, 100));
            Reject(() => m.MarginForAllocation(0, 5)); Reject(() => m.MarginForAllocation(101, 5));
            Reject(() => m.MarginForAllocation(50, 101)); Equal(before, Bytes(m));
        });
        Check("close notification reports the entire trade net of both fees", () => {
            foreach (var side in new[] { PositionSide.Long, PositionSide.Short }) {
                var m = new MarginMarket(); m.Deposit(100); m.Open(side, 10, 73);
                SetMid(m, side == PositionSide.Long ? 1.02m : .98m); m.Close();
                var result = m.LastSettlement; Require(result != null && !result.Liquidated);
                Near(m.State.Balance - 100, result.NetPnL); Near(m.State.FeesPaid, result.Fees);
                Near(result.NetPnL / 10m, result.ReturnRatio.Value);
                Require(m.RecentLogs[m.RecentLogs.Count - 1].Contains("收益率 " + MoneyDisplay.Percent(result.ReturnRatio)));
                Require(result.Side == side && result.Leverage == 73); Near(10, result.Margin);
                Require(Read(Bytes(m)).LastSettlement == null);
            }
        });
        Check("liquidation notification protects free funds and includes opening fee", () => {
            var m = new MarginMarket(); m.Deposit(100); m.Open(PositionSide.Long, 10, 100);
            decimal free = m.State.Balance; SetMid(m, .2m); m.AdvanceTo(0);
            var result = m.LastSettlement; Require(result != null && result.Liquidated);
            Near(free, m.State.Balance); Near(-11, result.NetPnL);
            Near(0, result.Returned); Near(1, result.Fees);
            Near(-1.1m, result.ReturnRatio.Value);
            Require(m.RecentLogs[m.RecentLogs.Count - 1].Contains("收益率 -110.00%"));
        });
        Check("each quote creates a candle with a body following the price direction", () => {
            var prices = new[] { Point(0, 1), Point(60, 1.1m), Point(120, .9m), Point(180, 1.05m) };
            var bars = MarketCandles.Build(prices); Require(bars.Count == 4);
            Near(1, bars[0].Open); Near(1, bars[0].Close);
            Near(1, bars[1].Open); Near(1.1m, bars[1].Close); Near(1.1m, bars[1].High);
            Require(bars[2].StartTick == 120); Near(1.1m, bars[2].Open); Near(.9m, bars[2].Close);
            Near(.9m, bars[2].Low); Near(1.1m, bars[2].High);
        });
        Check("candles handle empty single partial and gapped history without synthetic wicks", () => {
            Require(MarketCandles.Build(null).Count == 0);
            var bars = MarketCandles.Build(new[] { Point(1200, 1.5m), Point(1500, 1.6m), Point(3600, .8m) });
            Require(bars.Count == 3); Near(1.5m, bars[0].Open); Near(1.6m, bars[1].Close);
            Near(1.5m, bars[1].Open);
            Near(.8m, bars[2].Open); Near(.8m, bars[2].High); Near(.8m, bars[2].Low);
        });
        Check("quotes advance exactly once per 60 game ticks", () => {
            var m = new MarginMarket(); m.AdvanceTo(0); var first = m.MidPrice;
            m.AdvanceTo(59); Near(first, m.MidPrice); Require(m.History.Count == 1);
            m.AdvanceTo(60); Require(m.History.Count == 2 && m.MidPrice != first);
            var second = m.MidPrice; m.AdvanceTo(119); Near(second, m.MidPrice);
            m.AdvanceTo(120); Require(m.History.Count == 3 && m.MidPrice != second);
        });
        Check("legacy five-second margin cadence migrates without retroactive quotes or lost position", () => {
            var old = new MarginMarket(); old.AdvanceTo(0); old.Deposit(100); old.Open(PositionSide.Long, 2, 10);
            var feed = (SpotMarket)typeof(MarginMarket).GetField("_feed", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(old);
            feed.UseQuoteInterval(300); feed.AdvanceTo(279);
            var restored = Read(Bytes(old));
            Near(old.State.Balance, restored.State.Balance); Near(old.MidPrice, restored.MidPrice);
            Require(restored.Position != null && restored.History[0].GameTick == 0);
            Require(restored.History[restored.History.Count - 1].GameTick == 279);
            restored.AdvanceTo(338); Near(old.MidPrice, restored.MidPrice);
            restored.AdvanceTo(339); Require(restored.MidPrice != old.MidPrice);
            Require(restored.Position != null); Equal(Bytes(restored), Bytes(Read(Bytes(restored))));
        });
        Check("cosmetic wicks are stable enclose OHLC and never alter trading randomness", () => {
            var m = new MarginMarket(); m.AdvanceTo(0); m.AdvanceTo(900);
            var control = Read(Bytes(m)); byte[] before = Bytes(m);
            var bars = MarketCandles.Build(m.History);
            for (int count = 0; count < 20; count++) {
                var again = MarketCandles.Build(m.History); Require(again.Count == bars.Count);
                for (int i = 0; i < bars.Count; i++) {
                    Near(bars[i].DisplayHigh, again[i].DisplayHigh); Near(bars[i].DisplayLow, again[i].DisplayLow);
                    Require(bars[i].DisplayHigh >= bars[i].High && bars[i].DisplayLow <= bars[i].Low && bars[i].DisplayLow > 0);
                }
            }
            Equal(before, Bytes(m)); m.AdvanceTo(2700); control.AdvanceTo(2700); Equal(Bytes(m), Bytes(control));
        });
        Check("liquidation line agrees with exact execution and fee rules on both sides", () => {
            foreach (var side in new[] { PositionSide.Long, PositionSide.Short })
                foreach (int leverage in new[] { 1, 7, 25, 100 }) {
                    var m = new MarginMarket(); m.Deposit(100); m.Open(side, 10, leverage);
                    decimal line = m.LiquidationPrice; Require(line > 0);
                    decimal adverse = side == PositionSide.Long ? -.000002m : .000002m;
                    var safe = Read(Bytes(m)); SetMid(safe, line - adverse); safe.AdvanceTo(0);
                    Require(safe.Position != null);
                    var unsafeMarket = Read(Bytes(m)); SetMid(unsafeMarket, line + adverse); unsafeMarket.AdvanceTo(0);
                    Require(unsafeMarket.Position == null && unsafeMarket.LastSettlement.Liquidated);
                    Near(line, m.LiquidationPrice);
                }
        });
        Check("liquidation threshold stays fixed after quotes and clears on closing", () => {
            var m = new MarginMarket(); m.Deposit(100); Near(0, m.LiquidationPrice);
            m.Open(PositionSide.Long, 10, 10); decimal line = m.LiquidationPrice;
            SetMid(m, 1.1m); Near(line, m.LiquidationPrice); m.Close(); Near(0, m.LiquidationPrice);
            m.Open(PositionSide.Short, 10, 5); Require(m.LiquidationPrice > m.MidPrice);
        });
        Check("three item markets keep balances positions and price streams independent", () => {
            var book = new MarketBook(true);
            var copper = book.Get(MarketCatalog.Copper); var plastic = book.Get(MarketCatalog.Plastic);
            var matrix = book.Get(MarketCatalog.Matrix);
            copper.Deposit(100); plastic.Deposit(50); matrix.Deposit(20);
            copper.Open(PositionSide.Long, 10, 5); plastic.Open(PositionSide.Short, 5, 7);
            byte[] matrixBefore = Bytes(matrix); copper.Close(); plastic.Withdraw(1); Equal(matrixBefore, Bytes(matrix));
            foreach (var market in book.Markets) { market.AdvanceTo(0); market.AdvanceTo(90); }
            Require(copper.MidPrice != plastic.MidPrice && plastic.MidPrice != matrix.MidPrice);
            Near(20, matrix.State.Balance); Require(matrix.Position == null && plastic.Position != null);
            Require(plastic.Unit == "塑料" && matrix.Unit == "电磁矩阵");
            Reject(() => book.Get(1001));
        });
        Check("market book roundtrip preserves every account and future prices", () => {
            var a = new MarketBook(true);
            foreach (var market in a.Markets) {
                market.AdvanceTo(101); market.Deposit(market.State.ItemId);
                market.Open(PositionSide.Short, 1, 5);
            }
            var b = ReadBook(BookBytes(a)); Equal(BookBytes(a), BookBytes(b));
            foreach (var market in a.Markets) market.AdvanceTo(501);
            foreach (var market in b.Markets) market.AdvanceTo(501);
            Equal(BookBytes(a), BookBytes(b));
        });
        Check("v0.4 single-account migration retains copper and creates zero new item accounts", () => {
            var old = new MarginMarket(); old.AdvanceTo(0); old.Deposit(100); old.Open(PositionSide.Long, 10, 20);
            var migrated = ReadBook(Bytes(old)); Equal(Bytes(old), Bytes(migrated.Get(MarketCatalog.Copper)));
            Near(0, migrated.Get(MarketCatalog.Plastic).State.Balance); Near(0, migrated.Get(MarketCatalog.Matrix).State.Balance);
            var reloaded = ReadBook(BookBytes(migrated)); Equal(BookBytes(migrated), BookBytes(reloaded));
        });
        Check("market book rejects truncation duplicate unknown and oversized market lists", () => {
            byte[] complete = BookBytes(new MarketBook(true));
            for (int n = 0; n < complete.Length; n++) {
                var part = new byte[n]; Array.Copy(complete, part, n); InvalidBook(part);
            }
            foreach (int count in new[] { -1, 0, 4, int.MaxValue }) {
                var bad = (byte[])complete.Clone(); Array.Copy(BitConverter.GetBytes(count), 0, bad, 8, 4); InvalidBook(bad);
            }
            foreach (bool unknown in new[] { false, true }) {
                using (var stream = new MemoryStream()) using (var writer = new BinaryWriter(stream)) {
                    writer.Write(0x4658424B); writer.Write(1); writer.Write(3);
                    new MarginMarket().Export(writer); new MarginMarket(unknown ? 1001 : 1002).Export(writer);
                    new MarginMarket(6001).Export(writer); InvalidBook(stream.ToArray());
                }
            }
        });
        Check("v0.5 three-account book upgrades without losing any balance position or RNG", () => {
            var copper = new MarginMarket(); var plastic = new MarginMarket(1115); var matrix = new MarginMarket(6001);
            foreach (var market in new[] { copper, plastic, matrix }) { market.Deposit(100); market.AdvanceTo(101); market.Open(PositionSide.Short, 2, 5); }
            byte[] old;
            using (var stream = new MemoryStream()) using (var writer = new BinaryWriter(stream)) {
                writer.Write(0x4658424B); writer.Write(1); writer.Write(3);
                copper.Export(writer); plastic.Export(writer); matrix.Export(writer); old = stream.ToArray();
            }
            var book = ReadBook(old); Require(book.Markets.Count == 9);
            foreach (var market in new[] { copper, plastic, matrix }) Equal(Bytes(market), Bytes(book.Get(market.State.ItemId)));
            foreach (var market in book.Markets)
                if (market.State.ItemId != 1002 && market.State.ItemId != 1115 && market.State.ItemId != 6001)
                    Require(market.State.Balance == 0 && market.Position == null);
            Equal(BookBytes(book), BookBytes(ReadBook(BookBytes(book))));
        });
        Check("market categories include six isolated matrices and a separate building currency", () => {
            var book = new MarketBook(true);
            Require(MarketCatalog.Category(1002) == MarketCategory.RawMaterials && MarketCatalog.Category(1115) == MarketCategory.Materials);
            for (int id = 6001; id <= 6006; id++) {
                Require(MarketCatalog.Category(id) == MarketCategory.Matrices); book.Get(id).Deposit(id - 6000);
            }
            for (int id = 6001; id <= 6006; id++) Near(id - 6000, book.Get(id).State.Balance);
            Near(0, book.Get(MarketCatalog.BuildingCoin).State.Balance);
            Require(MarketCatalog.Category(MarketCatalog.BuildingCoin) == MarketCategory.BuildingCurrency);
        });
        Check("exchange eligibility covers buildings foundation all logistics and five fighters", () => {
            Require(BuildingExchangeRules.IsEligible(2303, true));
            foreach (int id in new[] { 1131, 5001, 5002, 5003, 5101, 5102, 5103, 5111, 5112 }) {
                Require(BuildingExchangeRules.IsEligible(id, false));
                Require(BuildingExchangeRules.UnitValue(id) > 0);
            }
            foreach (int id in new[] { 1002, 1115, 6001, 6006 }) Require(!BuildingExchangeRules.IsEligible(id, false));
            Near(1210, BuildingExchangeRules.Cost(BuildingExchangeRules.UnitValue(2002), 5));
            Reject(() => BuildingExchangeRules.Cost(0, 1)); Reject(() => BuildingExchangeRules.Cost(1, 0));
            Reject(() => BuildingExchangeRules.Cost(long.MaxValue, 2));
        });
        Check("exchange fee is charged in both directions and agrees with funding-adjusted equity", () => {
            var wallet = new MarginMarket(MarketCatalog.BuildingCoin);
            wallet.SettleExchange(100, BuildingExchangeRules.Fee(100, .01m), false); Near(99, wallet.State.Balance);
            wallet.SettleExchange(50, BuildingExchangeRules.Fee(50, .01m), true); Near(48.5m, wallet.State.Balance);
            Near(-1.5m, wallet.NetPnL); Near(1.5m, wallet.State.FeesPaid);
            Near(wallet.NetPnL, wallet.Equity + wallet.State.TotalWithdrawn - wallet.State.TotalDeposited);
            Equal(Bytes(wallet), Bytes(Read(Bytes(wallet))));
        });
        Check("exchange validation rejects insufficient funds wrong currency and bad fees atomically", () => {
            var wallet = new MarginMarket(MarketCatalog.BuildingCoin); wallet.Deposit(1);
            var before = Bytes(wallet);
            Reject(() => wallet.SettleExchange(1, .01m, true)); Equal(before, Bytes(wallet));
            Reject(() => wallet.SettleExchange(1, -1, false)); Reject(() => wallet.SettleExchange(1, 1, false));
            Reject(() => new MarginMarket().SettleExchange(10, .1m, false)); Equal(before, Bytes(wallet));
            Near(.01m, BuildingExchangeRules.Fee(1, .01m));
        });
        Check("targeted closing does not change another market and stale close is harmless", () => {
            var book = new MarketBook(true);
            foreach (var market in book.Markets) { market.Deposit(100); market.Open(PositionSide.Long, 1, 5); }
            var plastic = Bytes(book.Get(MarketCatalog.Plastic));
            Require(book.CloseMarket(MarketCatalog.Copper)); Require(!book.CloseMarket(MarketCatalog.Copper));
            Equal(plastic, Bytes(book.Get(MarketCatalog.Plastic)));
            Require(book.Get(MarketCatalog.Copper).Position == null);
        });
        Check("one-click closing matches independent settlements for every market", () => {
            var book = new MarketBook(true);
            foreach (var market in book.Markets) { market.Deposit(100); market.Open(PositionSide.Short, 1, 5); }
            var control = ReadBook(BookBytes(book));
            Require(book.CloseAll() == 9 && book.CloseAll() == 0);
            foreach (var market in control.Markets) market.Close();
            Equal(BookBytes(book), BookBytes(control));
            foreach (var market in book.Markets) Require(market.Position == null && market.LastSettlement != null);
        });
        Check("batch close preflights every account before closing any position", () => {
            var book = new MarketBook(true);
            foreach (var market in book.Markets) { market.Deposit(100); market.Open(PositionSide.Long, 1, 5); }
            book.Get(MarketCatalog.BuildingCoin).State.Balance = decimal.MaxValue;
            var before = BookBytes(book); Reject(() => book.CloseAll()); Equal(before, BookBytes(book));
            foreach (var market in book.Markets) Require(market.Position != null && market.LastSettlement == null);
        });
        Check("unselected resources are not instantiated and enabling creates only that market", () => {
            var book = new MarketBook(true); Require(book.Find(1001) == null && book.Find(1003) == null);
            Require(book.SetResourceEnabled(1001, true, 500));
            Require(book.Markets.Count == 10 && book.RunningMarkets.Count == 10 && book.Find(1003) == null);
            Near(0, book.Get(1001).State.Balance); Near(1, book.Get(1001).MidPrice);
            Require(book.Get(1001).State.LastGameTick == 500);
            Require(!book.SetResourceEnabled(1001, true, 500));
        });
        Check("disabled resource receives no quotes time updates RNG or ledger mutations", () => {
            var book = new MarketBook(true); var copper = book.Get(1002);
            copper.Deposit(100); copper.AdvanceTo(100);
            book.SetResourceEnabled(1002, false, 100); byte[] paused = Bytes(copper);
            for (long tick = 101; tick <= 1000; tick++) foreach (var market in book.RunningMarkets) market.AdvanceTo(tick);
            Equal(paused, Bytes(copper)); Near(100, copper.State.Balance); Require(!book.IsEnabled(1002));
            var loaded = ReadBook(BookBytes(book)); Equal(paused, Bytes(loaded.Get(1002)));
            Require(!loaded.IsEnabled(1002) && loaded.RunningMarkets.Count == 8);
        });
        Check("reenabling does not backfill disabled prices and retains the next random draw", () => {
            var book = new MarketBook(true); var copper = book.Get(1002);
            long beforePause = 100 + MarginMarket.QuoteIntervalTicks;
            copper.Deposit(10); copper.AdvanceTo(100); copper.AdvanceTo(beforePause);
            var control = Read(Bytes(copper)); book.SetResourceEnabled(1002, false, beforePause);
            decimal price = copper.MidPrice; book.SetResourceEnabled(1002, true, 100000);
            Near(price, copper.MidPrice); Near(10, copper.State.Balance);
            copper.AdvanceTo(100000 + MarginMarket.QuoteIntervalTicks - 1); Near(price, copper.MidPrice);
            copper.AdvanceTo(100000 + MarginMarket.QuoteIntervalTicks); control.AdvanceTo(beforePause + MarginMarket.QuoteIntervalTicks); Near(control.MidPrice, copper.MidPrice);
            Require(copper.History[copper.History.Count - 1].GameTick == 100000 + MarginMarket.QuoteIntervalTicks);
        });
        Check("holding resources cannot be disabled and building currency cannot be toggled", () => {
            var book = new MarketBook(true); var copper = book.Get(1002); copper.AdvanceTo(100); copper.Deposit(100);
            copper.Open(PositionSide.Long, 10, 5); var before = BookBytes(book);
            Reject(() => book.SetResourceEnabled(1002, false, 100)); Equal(before, BookBytes(book));
            Reject(() => book.SetResourceEnabled(MarketCatalog.BuildingCoin, false, 100));
            Reject(() => book.SetResourceEnabled(2001, true, 100)); Equal(before, BookBytes(book));
            copper.Close(); Require(book.SetResourceEnabled(1002, false, 100));
        });
        Check("v3 stores enabled and dormant resource accounts with exact balances", () => {
            var book = new MarketBook(true); book.Get(1002).Deposit(100);
            book.SetResourceEnabled(1002, false, 100);
            book.SetResourceEnabled(1001, true, 100); book.Get(1001).Deposit(50);
            book.Get(1001).Open(PositionSide.Short, 5, 10);
            var read = ReadBook(BookBytes(book)); Equal(BookBytes(book), BookBytes(read));
            Require(!read.IsEnabled(1002) && read.IsEnabled(1001) && read.Get(1001).Position != null);
            Near(100, read.Get(1002).State.Balance); Require(read.Find(1003) == null);
        });
        Check("v2 nine-account snapshots retain every account and default resource selection", () => {
            var old = new MarketBook(true);
            foreach (var market in old.Markets) { market.Deposit(100); market.AdvanceTo(100); market.Open(PositionSide.Long, 1, 5); }
            byte[] bytes;
            using (var stream = new MemoryStream()) using (var writer = new BinaryWriter(stream)) {
                writer.Write(0x4658424B); writer.Write(2); writer.Write(9);
                foreach (var market in old.Markets) market.Export(writer); bytes = stream.ToArray();
            }
            var read = ReadBook(bytes); Require(read.RunningMarkets.Count == 9);
            foreach (var market in old.Markets) Equal(Bytes(market), Bytes(read.Get(market.State.ItemId)));
        });
        Check("v3 rejects invalid enable flags disabled positions and disabled building currency", () => {
            foreach (int badCase in new[] { 0, 1, 2 }) {
                var source = new MarketBook(true);
                if (badCase == 1) { var copper = source.Get(1002); copper.AdvanceTo(100); copper.Deposit(10); copper.Open(PositionSide.Long, 1, 5); }
                using (var stream = new MemoryStream()) using (var writer = new BinaryWriter(stream)) {
                    writer.Write(0x4658424B); writer.Write(3); writer.Write(9);
                    foreach (var market in source.Markets) {
                        market.Export(writer);
                        byte flag = 1;
                        if (badCase == 0 && market.State.ItemId == 1002) flag = 2;
                        if (badCase == 1 && market.State.ItemId == 1002) flag = 0;
                        if (badCase == 2 && market.State.ItemId == MarketCatalog.BuildingCoin) flag = 0;
                        writer.Write(flag);
                    }
                    InvalidBook(stream.ToArray());
                }
            }
        });
        Check("six peer categories separate raw inputs materials ammunition and consumables", () => {
            Require(MarketCatalog.Category(1002) == MarketCategory.RawMaterials);
            Require(MarketCatalog.Category(1115) == MarketCategory.Materials);
            Require(MarketCatalog.Category(1601) == MarketCategory.Weapons);
            Require(MarketCatalog.Category(1801) == MarketCategory.Consumables);
            Require(MarketCatalog.ClassifyResource(true, false, true) == MarketCategory.RawMaterials);
            Require(MarketCatalog.ClassifyResource(false, true, true) == MarketCategory.Weapons);
            Require(MarketCatalog.ClassifyResource(false, false, true) == MarketCategory.Consumables);
            Require(MarketCatalog.ClassifyResource(false, false, false) == MarketCategory.Materials);
            Require(MarketCatalog.IsResourceCategory(MarketCategory.Weapons) && !MarketCatalog.IsResourceCategory(MarketCategory.Matrices));
        });
        Check("resource reclassification changes no balances positions prices or enabled flags", () => {
            var book = new MarketBook(true); book.SetResourceEnabled(1601, true, 100);
            var market = book.Get(1601); market.Deposit(50); market.Open(PositionSide.Long, 5, 5);
            var before = BookBytes(book);
            MarketCatalog.RegisterResourceName(1601, "弹药", MarketCatalog.ClassifyResource(false, true, false));
            Require(MarketCatalog.Category(1601) == MarketCategory.Weapons && book.IsEnabled(1601));
            Equal(before, BookBytes(book));
            var loaded = ReadBook(BookBytes(book)); Require(loaded.Get(1601).Position != null);
            Near(market.State.Balance, loaded.Get(1601).State.Balance);
        });
        Check("requested raw inputs and proliferators override misleading native ammo flags", () => {
            foreach (int id in new[] { 1000, 1116, 1117, 1120, 1121 }) {
                Require(MarketCatalog.ClassifyResource(id, false, true, true) == MarketCategory.RawMaterials);
                Require(MarketCatalog.Category(id) == MarketCategory.RawMaterials);
            }
            foreach (int id in new[] { 1141, 1142, 1143 })
                Require(MarketCatalog.ClassifyResource(id, false, true, false) == MarketCategory.Consumables);
            Require(MarketCatalog.ClassifyResource(1601, false, true, true) == MarketCategory.Weapons);
        });
        Check("all six matrices pause persist and resume without catching up or losing funds", () => {
            var book = new MarketBook(true);
            for (int id = 6001; id <= 6006; id++) {
                var matrix = book.Get(id); matrix.Deposit(20); matrix.AdvanceTo(100);
                book.SetResourceEnabled(id, false, 100);
            }
            var restored = ReadBook(BookBytes(book)); Equal(BookBytes(book), BookBytes(restored));
            Require(restored.RunningMarkets.Count == 3);
            for (int id = 6001; id <= 6006; id++) {
                var matrix = restored.Get(id); var before = Bytes(matrix); var control = Read(before);
                foreach (var market in restored.RunningMarkets) market.AdvanceTo(100000);
                Equal(before, Bytes(matrix)); Near(20, matrix.State.Balance);
                restored.SetResourceEnabled(id, true, 100000); Near(control.MidPrice, matrix.MidPrice);
                matrix.AdvanceTo(100090); control.AdvanceTo(190); Near(control.MidPrice, matrix.MidPrice);
            }
        });
        Check("a held matrix must be closed before disabling", () => {
            var book = new MarketBook(true); var matrix = book.Get(6006); matrix.AdvanceTo(100); matrix.Deposit(20);
            matrix.Open(PositionSide.Short, 2, 5); var before = BookBytes(book);
            Reject(() => book.SetResourceEnabled(6006, false, 100)); Equal(before, BookBytes(book));
            matrix.Close(); Require(book.SetResourceEnabled(6006, false, 100));
        });
        Check("exchange tiers follow the highest matrix in unlock research including prerequisites", () => {
            var technologies = new System.Collections.Generic.Dictionary<int, TechnologyRequirements> {
                { 1, new TechnologyRequirements { Items = new[] { 6001 } } },
                { 2, new TechnologyRequirements { Items = new[] { 6002, 6001 }, Prerequisites = new[] { 1 } } },
                { 3, new TechnologyRequirements { Items = new[] { 6003 }, Prerequisites = new[] { 2 } } },
                { 4, new TechnologyRequirements { Items = new[] { 6004 }, Prerequisites = new[] { 3 } } },
                { 5, new TechnologyRequirements { Items = new[] { 6005 }, Prerequisites = new[] { 4 } } },
                { 6, new TechnologyRequirements { Items = new[] { 1001 }, Prerequisites = new[] { 4 } } },
                { 7, new TechnologyRequirements { Items = new[] { 6006 }, Prerequisites = new[] { 5 } } }
            };
            var tiers = new TechnologyTier(id => technologies.TryGetValue(id, out var tech) ? tech : null);
            for (int i = 1; i <= 5; i++) Require(tiers.Resolve(i) == i);
            Require(tiers.Resolve(6) == 4 && tiers.Resolve(7) == 5 && tiers.Resolve(0) == 1 && tiers.Resolve(999) == 1);
        });
        Check("tier resolution terminates on a cycle and remains independent of lookup order", () => {
            var tiers = new TechnologyTier(id => id == 1
                ? new TechnologyRequirements { Items = new[] { 6005 }, Prerequisites = new[] { 2 } }
                : new TechnologyRequirements { Items = new[] { 6002 }, Prerequisites = new[] { 1 } });
            Require(tiers.Resolve(1) == 5 && tiers.Resolve(2) == 5);
        });
        Check("legacy log presentation rounds amounts and strips item names from opening entries", () => {
            Require(TradingLogDisplay.Format("开多：14.477735 电磁矩阵保证金，5×；开仓费 0.072389 电磁矩阵。") == "开多14.48保证金");
            Require(TradingLogDisplay.Format("开空14.48保证金") == "开空14.48保证金");
            Require(TradingLogDisplay.Format("已平仓：本次 +1.234567 铜矿。") == "已平仓：本次 +1.23 铜矿。");
            var market = new MarginMarket(6001); market.Deposit(100); market.Open(PositionSide.Long, 14.477735m, 5);
            Require(market.RecentLogs[market.RecentLogs.Count - 1] == "开多14.48保证金");
        });
        Console.WriteLine("All " + _passed + " FXDSP margin checks passed.");
    }
    private static void SetMid(MarginMarket market, decimal price)
    {
        var feed = (SpotMarket)typeof(MarginMarket).GetField("_feed", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(market);
        feed.State.MidPrice = price;
    }
    private static decimal Fee(decimal amount) { return decimal.Ceiling(amount * .001m * 1000000m) / 1000000m; }
    private static PricePoint Point(long tick, decimal price) { return new PricePoint(tick, price, price, price); }
    private static byte[] BookBytes(MarketBook book) { using(var s=new MemoryStream()) using(var w=new BinaryWriter(s)) { book.Export(w); return s.ToArray(); } }
    private static MarketBook ReadBook(byte[] bytes) { using(var s=new MemoryStream(bytes)) using(var r=new BinaryReader(s)) return MarketBook.Import(r); }
    private static void InvalidBook(byte[] bytes) { try { ReadBook(bytes); } catch (InvalidDataException) { return; } throw new Exception("Expected invalid market book."); }
    private static byte[] Bytes(MarginMarket m) { using(var s=new MemoryStream()) using(var w=new BinaryWriter(s)) { m.Export(w); return s.ToArray(); } }
    private static byte[] LegacyBytes(SpotMarket m) { using(var s=new MemoryStream()) using(var w=new BinaryWriter(s)) { m.Export(w); return s.ToArray(); } }
    private static MarginMarket Read(byte[] b) { using(var s=new MemoryStream(b)) using(var r=new BinaryReader(s)) return MarginMarket.Import(r); }
    private static void Near(decimal a, decimal b) { if (Math.Abs(a-b) > .000000000000000001m) throw new Exception("Mismatch: " + a + " vs " + b); }
    private static void Require(bool condition) { if (!condition) throw new Exception("Margin invariant failed."); }
    private static void Reject(Action action) { try { action(); } catch(InvalidOperationException) {return;} catch(OverflowException) {return;} throw new Exception("Expected rejected order."); }
    private static void Equal(byte[] a, byte[] b) { Require(a.Length==b.Length); for(int i=0;i<a.Length;i++) Require(a[i]==b[i]); }
    private static void Check(string name, Action action) { action(); _passed++; Console.WriteLine("PASS: " + name); }
}
