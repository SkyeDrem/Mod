using System;
using System.IO;
using System.Linq;
using System.Text;
using FXDSP.Core;

internal static class Program
{
    private static int _passed;

    private static int Main()
    {
        try
        {
            Run("Zero assets and first time anchor", EmptyAndFirstAnchor);
            Run("Executable prices and exact fee-inclusive costs", PricesAndFees);
            Run("Immediate round trip cannot create value", ImmediateRoundTrip);
            Run("Invalid quantities fail without mutations", InvalidInputs);
            Run("Deposit / withdrawal accounting", DepositWithdrawal);
            Run("Long quantities and atomic numeric-overflow rejection", LargeAmounts);
            Run("Profitable same-item growth is allowed", ProfitableGrowth);
            Run("Splitting orders cannot save fees", SplitOrders);
            Run("Deterministic tick progression and bounded history", Timeline);
            Run("Rollback and huge tick gaps reanchor safely", TimeReanchor);
            Run("Binary state and subsequent random stream roundtrip", Persistence);
            Run("Fresh / unanchored state can roundtrip", FreshPersistence);
            Run("Corrupt records reject before unbounded allocation", CorruptRecords);
            Run("Independent markets have no shared mutable state", IndependentMarkets);
            Console.WriteLine("All " + _passed + " FXDSP core checks passed.");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine("FAIL: " + exception);
            return 1;
        }
    }

    private static void Run(string name, Action check)
    {
        check();
        _passed++;
        Console.WriteLine("PASS: " + name);
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }

    private static void Throws<T>(Action action) where T : Exception
    {
        try { action(); }
        catch (T) { return; }
        throw new Exception("Expected " + typeof(T).Name + ".");
    }

    private static byte[] Save(SpotMarket market)
    {
        using (MemoryStream stream = new MemoryStream())
        {
            using (BinaryWriter writer = new BinaryWriter(stream, Encoding.UTF8, true))
                market.Export(writer);
            return stream.ToArray();
        }
    }

    private static SpotMarket Load(byte[] bytes)
    {
        using (MemoryStream stream = new MemoryStream(bytes))
        using (BinaryReader reader = new BinaryReader(stream, Encoding.UTF8, true))
            return SpotMarket.Import(reader);
    }

    private static void Same(SpotMarket left, SpotMarket right)
    {
        Assert(Save(left).SequenceEqual(Save(right)), "Market states are different.");
    }

    private static void UnchangedFailure(SpotMarket market, Action action)
    {
        byte[] before = Save(market);
        Throws<InvalidOperationException>(action);
        Assert(before.SequenceEqual(Save(market)), "Failed command mutated an account or log.");
    }

    private static void EmptyAndFirstAnchor()
    {
        SpotMarket market = new SpotMarket();
        Assert(market.State.ItemId == 1002 && market.State.ItemQuantity == 0
            && market.State.SettlementBalance == 0m, "Fresh market contains free assets.");
        UnchangedFailure(market, () => market.Buy(1));
        UnchangedFailure(market, () => market.Sell(1));
        UnchangedFailure(market, () => market.Withdraw(1));
        market.AdvanceTo(1000000000L);
        Assert(market.State.MidPrice == 1m && market.History.Count == 1,
            "First attachment backfilled old game history.");
    }

    private static void PricesAndFees()
    {
        SpotMarket market = new SpotMarket();
        Assert(market.Bid == 0.995m && market.Ask == 1.005m, "Unexpected initial spread.");
        Assert(market.FeeRate == 0.01m, "Unexpected default fee.");
        Assert(market.GetBuyTotal(100) == 101.505m, "Buy cost did not include exact fees.");
        Assert(market.GetSellNet(100) == 98.505m, "Sell proceeds did not deduct fees.");
        SpotMarket highFee = new SpotMarket(feeRate: 0.99999999999m);
        highFee.AdvanceTo(0);
        highFee.AdvanceTo(300);
        Assert(highFee.GetSellNet(1) >= 0m, "Rounded fee created negative proceeds.");
        Throws<InvalidOperationException>(() => new SpotMarket(itemId: 0));
        Throws<InvalidOperationException>(() => new SpotMarket(feeRate: -0.01m));
        Throws<InvalidOperationException>(() => new SpotMarket(feeRate: 1m));
        Throws<InvalidOperationException>(() => new SpotMarket(seed: 0));
    }

    private static void ImmediateRoundTrip()
    {
        SpotMarket market = new SpotMarket();
        market.Deposit(1000);
        market.Sell(1000);
        decimal before = market.State.SettlementBalance;
        market.Buy(100);
        market.Sell(100);
        Assert(market.State.ItemQuantity == 0 && market.State.SettlementBalance == before - 3m,
            "Immediate buy / sell produced value or incorrect fees.");
        Assert(market.State.TotalDeposited == 1000 && market.State.TotalWithdrawn == 0,
            "Trading incorrectly counted as physical deposits / withdrawals.");
    }

    private static void InvalidInputs()
    {
        SpotMarket market = new SpotMarket();
        market.Deposit(20);
        foreach (long quantity in new[] { 0L, -1L, long.MinValue })
        {
            UnchangedFailure(market, () => market.Deposit(quantity));
            UnchangedFailure(market, () => market.Withdraw(quantity));
            UnchangedFailure(market, () => market.Buy(quantity));
            UnchangedFailure(market, () => market.Sell(quantity));
            UnchangedFailure(market, () => market.GetBuyTotal(quantity));
            UnchangedFailure(market, () => market.GetSellNet(quantity));
        }
        UnchangedFailure(market, () => market.Sell(21));
        UnchangedFailure(market, () => market.Withdraw(21));
        UnchangedFailure(market, () => market.AdvanceTo(-1));
    }

    private static void DepositWithdrawal()
    {
        SpotMarket market = new SpotMarket();
        market.Deposit(100);
        market.Withdraw(100);
        Assert(market.State.TotalDeposited == 100 && market.State.TotalWithdrawn == 100
            && market.State.NetDeliveredGain == 0 && market.State.SettlementBalance == 0m,
            "Moving items minted credit or profit.");
        for (int index = 0; index < 15; index++)
        {
            market.Deposit(1);
            market.Withdraw(1);
        }
        Assert(market.RecentLogs.Count == 10 && market.State.NetDeliveredGain == 0,
            "Logs exceeded their bound or repeated transfers manufactured gain.");
    }

    private static void LargeAmounts()
    {
        SpotMarket market = new SpotMarket();
        market.Deposit(long.MaxValue);
        UnchangedFailure(market, () => market.Deposit(1));
        Assert(market.GetBuyTotal(long.MaxValue) > 0m && market.GetSellNet(long.MaxValue) > 0m,
            "Long quantity overflowed a valid quote.");
        market.Sell(long.MaxValue);
        Assert(market.State.SettlementBalance > int.MaxValue && market.State.ItemQuantity == 0,
            "Large legal trades were artificially capped.");

        SpotMarket fullItems = Load(RawRecord(quantity: long.MaxValue, balance: 100m));
        UnchangedFailure(fullItems, () => fullItems.Buy(1));
        SpotMarket fullBalance = Load(RawRecord(quantity: 1, balance: decimal.MaxValue));
        UnchangedFailure(fullBalance, () => fullBalance.Sell(1));
        SpotMarket fullWithdrawals = Load(RawRecord(quantity: 1, withdrawn: long.MaxValue));
        UnchangedFailure(fullWithdrawals, () => fullWithdrawals.Withdraw(1));
        SpotMarket fullDeposits = Load(RawRecord(deposited: long.MaxValue));
        UnchangedFailure(fullDeposits, () => fullDeposits.Deposit(1));
    }

    private static void ProfitableGrowth()
    {
        SpotMarket market = new SpotMarket(seed: 1234u);
        market.Deposit(10000);
        market.Sell(10000);
        market.AdvanceTo(0);
        bool bought = false;
        for (long index = 1; index <= 20000; index++)
        {
            market.AdvanceTo(index * SpotMarket.QuoteIntervalTicks);
            if (market.GetBuyTotal(11000) <= market.State.SettlementBalance)
            {
                market.Buy(11000);
                market.Withdraw(11000);
                bought = true;
                break;
            }
        }
        Assert(bought && market.State.NetDeliveredGain == 1000,
            "Valid profitable growth was prevented by an inventory or profitability cap.");
    }

    private static void SplitOrders()
    {
        foreach (decimal fee in new[] { 0m, 0.01m, 0.010123456789m, 0.99999999999m })
        {
            SpotMarket market = new SpotMarket(feeRate: fee);
            market.AdvanceTo(0);
            for (int step = 0; step < 50; step++)
            {
                market.AdvanceTo((step + 1) * SpotMarket.QuoteIntervalTicks);
                Assert(market.GetBuyTotal(53) <= market.GetBuyTotal(1) * 53m,
                    "Split buy orders saved fees.");
                Assert(market.GetSellNet(53) >= market.GetSellNet(1) * 53m,
                    "Split sell orders saved fees.");
                Assert(market.GetBuyTotal(1) >= market.GetSellNet(1),
                    "Same-quote round trip became profitable.");
            }
        }
    }

    private static void Timeline()
    {
        SpotMarket batched = new SpotMarket(seed: 456u);
        SpotMarket stepped = new SpotMarket(seed: 456u);
        batched.AdvanceTo(1000);
        stepped.AdvanceTo(1000);
        batched.AdvanceTo(1000 + 500 * SpotMarket.QuoteIntervalTicks + 123);
        for (int index = 1; index <= 500; index++)
            stepped.AdvanceTo(1000 + index * SpotMarket.QuoteIntervalTicks);
        stepped.AdvanceTo(1000 + 500 * SpotMarket.QuoteIntervalTicks + 123);
        Same(batched, stepped);
        Assert(batched.History.Count == SpotMarket.HistoryLimit, "History is not bounded.");
        foreach (PricePoint point in batched.History)
            Assert(point.Bid > 0m && point.Bid <= point.MidPrice && point.MidPrice <= point.Ask,
                "History contains invalid executable prices.");
        byte[] before = Save(batched);
        batched.AdvanceTo(batched.State.LastGameTick);
        Assert(before.SequenceEqual(Save(batched)), "Paused / same tick changed market state.");
    }

    private static void TimeReanchor()
    {
        SpotMarket market = new SpotMarket();
        market.Deposit(7);
        market.AdvanceTo(1000000000L);
        market.AdvanceTo(1000000300L);
        decimal before = market.State.MidPrice;
        market.AdvanceTo(42);
        Assert(market.State.ItemQuantity == 7 && market.State.MidPrice == before
            && market.State.LastGameTick == 42 && market.History.Count == 1,
            "Rollback changed assets or regenerated price history.");
        market.AdvanceTo(long.MaxValue);
        Assert(market.State.LastGameTick == long.MaxValue && market.State.MidPrice == before
            && market.History.Count == 1, "Huge tick gap did not reanchor safely.");
    }

    private static void Persistence()
    {
        SpotMarket market = new SpotMarket(feeRate: 0.0123m, seed: 2026u);
        market.Deposit(1234);
        market.Sell(321);
        market.AdvanceTo(150000);
        market.AdvanceTo(157321);
        market.Buy(2);
        market.Withdraw(1);
        SpotMarket restored = Load(Save(market));
        Same(market, restored);
        for (int index = 1; index < 300; index++)
        {
            long tick = 157321 + index * 151;
            market.AdvanceTo(tick);
            restored.AdvanceTo(tick);
        }
        Same(market, restored);

        using (MemoryStream stream = new MemoryStream())
        using (BinaryWriter writer = new BinaryWriter(stream, Encoding.UTF8, true))
        {
            market.Export(writer);
            writer.Write(123456);
            writer.Flush();
            stream.Position = 0;
            using (BinaryReader reader = new BinaryReader(stream, Encoding.UTF8, true))
            {
                SpotMarket.Import(reader);
                Assert(reader.ReadInt32() == 123456, "Import consumed data owned by its enclosing record.");
            }
            Assert(stream.CanRead, "Import closed the caller's stream.");
        }
    }

    private static void FreshPersistence()
    {
        SpotMarket market = new SpotMarket();
        market.Deposit(1);
        SpotMarket restored = Load(Save(market));
        Same(market, restored);
        restored.AdvanceTo(9000000);
        Assert(restored.History.Count == 1 && restored.State.MidPrice == 1m,
            "Unanchored imported state backfilled prices.");
    }

    private static void CorruptRecords()
    {
        byte[] valid = Save(new SpotMarket());
        for (int count = 0; count < valid.Length; count++)
        {
            byte[] truncated = valid.Take(count).ToArray();
            Throws<InvalidDataException>(() => Load(truncated));
        }
        byte[] badMagic = (byte[])valid.Clone();
        badMagic[0] ^= 0xFF;
        Throws<InvalidDataException>(() => Load(badMagic));
        byte[] badVersion = (byte[])valid.Clone();
        badVersion[4] = 99;
        Throws<InvalidDataException>(() => Load(badVersion));
        Throws<InvalidDataException>(() => Load(RawRecord(itemId: 0)));
        Throws<InvalidDataException>(() => Load(RawRecord(quantity: -1)));
        Throws<InvalidDataException>(() => Load(RawRecord(balance: -1m)));
        Throws<InvalidDataException>(() => Load(RawRecord(deposited: -1)));
        Throws<InvalidDataException>(() => Load(RawRecord(withdrawn: -1)));
        Throws<InvalidDataException>(() => Load(RawRecord(price: 0m)));
        Throws<InvalidDataException>(() => Load(RawRecord(fee: 1m)));
        Throws<InvalidDataException>(() => Load(RawRecord(seed: 0)));
        Throws<InvalidDataException>(() => Load(RawRecord(momentum: 1m)));
        Throws<InvalidDataException>(() => Load(RawRecord(anchor: 2)));
        Throws<InvalidDataException>(() => Load(RawRecord(gameTick: -1)));
        Throws<InvalidDataException>(() => Load(RawRecord(historyCount: int.MaxValue)));
        Throws<InvalidDataException>(() => Load(RawRecord(logCount: int.MaxValue)));
        Throws<InvalidDataException>(() => Load(RawRecord(logCount: 1, logBytes: int.MaxValue)));
    }

    private static void IndependentMarkets()
    {
        SpotMarket left = new SpotMarket(seed: 99u);
        SpotMarket right = new SpotMarket(seed: 99u);
        SpotMarket other = new SpotMarket(itemId: 1111, seed: 12u);
        left.AdvanceTo(0);
        right.AdvanceTo(0);
        other.AdvanceTo(0);
        for (int index = 1; index <= 100; index++)
        {
            left.AdvanceTo(index * 300);
            other.AdvanceTo(index * 600);
            right.AdvanceTo(index * 300);
        }
        Same(left, right);
        left.Deposit(50);
        Assert(right.State.ItemQuantity == 0 && other.State.ItemQuantity == 0
            && right.State.SettlementBalance == 0m, "Instances shared account assets.");
    }

    // Deliberately malformed records exercise the public parser's validation.
    private static byte[] RawRecord(int itemId = 1002, long quantity = 0, decimal balance = 0m,
        long deposited = 0, long withdrawn = 0, decimal price = 1m, decimal fee = 0.01m,
        uint seed = 1, decimal momentum = 0m, byte anchor = 0, long gameTick = 0,
        int historyCount = 0, int logCount = 0, int logBytes = 0)
    {
        using (MemoryStream stream = new MemoryStream())
        using (BinaryWriter writer = new BinaryWriter(stream, Encoding.UTF8, true))
        {
            writer.Write(0x4658534D);
            writer.Write(1);
            writer.Write(itemId);
            writer.Write(quantity);
            writer.Write(balance);
            writer.Write(deposited);
            writer.Write(withdrawn);
            writer.Write(price);
            writer.Write(fee);
            writer.Write(seed);
            writer.Write(momentum);
            writer.Write(anchor);
            writer.Write(gameTick);
            writer.Write(0L);
            writer.Write(historyCount);
            writer.Write(logCount);
            if (logCount == 1) writer.Write(logBytes);
            writer.Flush();
            return stream.ToArray();
        }
    }
}
