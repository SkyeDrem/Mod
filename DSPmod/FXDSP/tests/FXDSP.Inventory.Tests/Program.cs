using System;
using FXDSP.Core;
using FXDSP.Runtime;

internal static class Program
{
    private static int _passed;

    private static void Main()
    {
        Check("deposit converts real copper one-to-one without extra funds", () => {
            var market = new MarginMarket(); SetPackage(100, 100);
            PlayerInventory.Deposit(market, 100);
            Assert(GameMain.mainPlayer.package.Count == 0 && market.State.Balance == 100
                && market.State.TotalDeposited == 100 && market.UsedMargin == 0);
        });
        Check("insufficient deposit preserves both sides", () => {
            var market = new MarginMarket(); SetPackage(9, 100);
            Throws(() => PlayerInventory.Deposit(market, 10));
            Assert(GameMain.mainPlayer.package.Count == 9 && market.State.Balance == 0);
        });
        Check("sprayed ore is rejected without losing proliferation points", () => {
            var market = new MarginMarket(); SetPackage(100, 100); GameMain.mainPlayer.package.Inc = 20;
            Throws(() => PlayerInventory.Deposit(market, 10));
            Assert(GameMain.mainPlayer.package.Count == 100 && GameMain.mainPlayer.package.Inc == 20
                && market.State.TotalDeposited == 0);
        });
        Check("partial package acceptance debits only inserted items", () => {
            var market = new MarginMarket(); market.Deposit(100); SetPackage(0, 30);
            PlayerInventory.Withdraw(market, 100);
            Assert(GameMain.mainPlayer.package.Count == 30 && market.State.Balance == 70
                && market.State.TotalWithdrawn == 30 && GameMain.mainPlayer.NotifiedCount == 30);
        });
        Check("full package leaves custody unchanged", () => {
            var market = new MarginMarket(); market.Deposit(100); SetPackage(10, 10);
            Throws(() => PlayerInventory.Withdraw(market, 10));
            Assert(market.State.Balance == 100 && market.State.TotalWithdrawn == 0
                && GameMain.mainPlayer.package.Count == 10);
        });
        Check("quantity overflow is checked before removing game items", () => {
            var market = new MarginMarket(); market.Deposit(long.MaxValue); SetPackage(5, 5);
            Throws(() => PlayerInventory.Deposit(market, 1));
            Assert(GameMain.mainPlayer.package.Count == 5 && market.State.Balance == long.MaxValue);
        });
        Check("withdrawal counter overflow is checked before inserting game items", () => {
            var market = new MarginMarket(); market.Deposit(10); market.State.TotalWithdrawn = long.MaxValue;
            SetPackage(0, 100); Throws(() => PlayerInventory.Withdraw(market, 1));
            Assert(GameMain.mainPlayer.package.Count == 0 && market.State.Balance == 10);
        });
        Check("invalid and missing-player operations cannot transfer items", () => {
            var market = new MarginMarket(); SetPackage(10, 100);
            Throws(() => PlayerInventory.Deposit(market, -1));
            Throws(() => PlayerInventory.Withdraw(market, 0));
            Assert(GameMain.mainPlayer.package.Count == 10 && market.State.Balance == 0);
            GameMain.mainPlayer = null; Throws(() => PlayerInventory.Deposit(market, 1));
            Assert(PlayerInventory.Count(1002) == 0);
        });
        Check("deposit credits actual removal if package changes", () => {
            var market = new MarginMarket(); SetPackage(100, 100); GameMain.mainPlayer.package.TakeLimit = 40;
            PlayerInventory.Deposit(market, 100);
            Assert(GameMain.mainPlayer.package.Count == 60 && market.State.Balance == 40
                && market.State.TotalDeposited == 40);
        });
        Check("backpack counts and transfers use the selected item's native ID", () => {
            Assert(MarketCatalog.Copper == 1002 && MarketCatalog.Plastic == 1115 && MarketCatalog.Matrix == 6001);
            foreach (int id in MarketCatalog.ItemIds) {
                if (id == MarketCatalog.BuildingCoin) continue;
                var market = new MarginMarket(id); SetPackage(43, 100);
                Assert(PlayerInventory.Count(id) == 43 && GameMain.mainPlayer.package.LastReadId == id);
                PlayerInventory.Deposit(market, 10);
                Assert(GameMain.mainPlayer.package.LastTakenId == id && market.State.Balance == 10);
                PlayerInventory.Withdraw(market, 4);
                Assert(GameMain.mainPlayer.package.LastInsertedId == id && market.State.Balance == 6);
                Assert(PlayerInventory.Count(id) == 37);
            }
        });
        Check("building sale and purchase use one fixed value without minting coins", () => {
            var wallet = new MarginMarket(MarketCatalog.BuildingCoin); wallet.Deposit(10); SetPackage(10, 100);
            PlayerInventory.SellBuilding(wallet, 2303, 4, 50, .01m);
            Assert(GameMain.mainPlayer.package.Count == 6 && wallet.State.Balance == 208);
            PlayerInventory.BuyBuilding(wallet, 2303, 4, 50, .01m);
            Assert(GameMain.mainPlayer.package.Count == 10 && wallet.State.Balance == 6);
            Assert(wallet.NetPnL == -4 && wallet.State.FeesPaid == 4 && wallet.State.TotalDeposited == 210 && wallet.State.TotalWithdrawn == 200);
        });
        Check("coin purchase charges only actual accepted buildings", () => {
            var wallet = new MarginMarket(MarketCatalog.BuildingCoin); wallet.Deposit(1010); SetPackage(0, 3);
            PlayerInventory.BuyBuilding(wallet, 5002, 5, 200, .01m);
            Assert(GameMain.mainPlayer.package.Count == 3 && GameMain.mainPlayer.package.LastInsertedId == 5002);
            Assert(wallet.State.Balance == 404 && wallet.State.TotalWithdrawn == 600 && wallet.State.FeesPaid == 6);
            Assert(GameMain.mainPlayer.NotifiedCount == 3);
        });
        Check("coin building sale credits only actual removed quantity", () => {
            var wallet = new MarginMarket(MarketCatalog.BuildingCoin); SetPackage(10, 100);
            GameMain.mainPlayer.package.TakeLimit = 2;
            PlayerInventory.SellBuilding(wallet, 5112, 5, 1500, .01m);
            Assert(GameMain.mainPlayer.package.LastTakenId == 5112 && GameMain.mainPlayer.package.Count == 8);
            Assert(wallet.State.Balance == 2970 && wallet.NetPnL == -30 && wallet.State.FeesPaid == 30);
        });
        Check("exchange failures never mutate inventory or mint credit", () => {
            var wallet = new MarginMarket(MarketCatalog.BuildingCoin); wallet.Deposit(10); SetPackage(10, 10);
            Throws(() => PlayerInventory.BuyBuilding(wallet, 2001, 11, 1, .01m));
            Throws(() => PlayerInventory.BuyBuilding(wallet, 2001, 1, 1, .01m));
            Throws(() => PlayerInventory.SellBuilding(wallet, 2001, 11, 1, .01m));
            Throws(() => PlayerInventory.SellBuilding(wallet, 2001, 1, 0, .01m));
            GameMain.mainPlayer.package.Inc = 1;
            Throws(() => PlayerInventory.SellBuilding(wallet, 2001, 1, 1, .01m));
            Throws(() => PlayerInventory.SellBuilding(new MarginMarket(), 2001, 1, 1));
            Assert(GameMain.mainPlayer.package.Count == 10 && wallet.State.Balance == 10);
        });
        Check("exchange overflow is rejected before touching native items", () => {
            var wallet = new MarginMarket(MarketCatalog.BuildingCoin); wallet.Deposit(10); SetPackage(1, 10);
            wallet.State.TotalWithdrawn = long.MaxValue;
            Throws(() => PlayerInventory.BuyBuilding(wallet, 2001, 1, 1, .01m));
            wallet.State.TotalDeposited = long.MaxValue;
            Throws(() => PlayerInventory.SellBuilding(wallet, 2001, 1, 1, .01m));
            Throws(() => PlayerInventory.SellBuilding(wallet, 2001, 2, long.MaxValue, .01m));
            Assert(GameMain.mainPlayer.package.Count == 1 && wallet.State.Balance == 10);
        });
        Check("building currency never enters the native item transfer path", () => {
            var wallet = new MarginMarket(MarketCatalog.BuildingCoin); wallet.Deposit(10); SetPackage(5, 10);
            Throws(() => PlayerInventory.Deposit(wallet, 1)); Throws(() => PlayerInventory.Withdraw(wallet, 1));
            Assert(GameMain.mainPlayer.package.Count == 5 && wallet.State.Balance == 10);
        });
        Check("exchange charge includes the fee even for the cheapest item", () => {
            var wallet = new MarginMarket(MarketCatalog.BuildingCoin); wallet.Deposit(1); SetPackage(0, 10);
            Throws(() => PlayerInventory.BuyBuilding(wallet, 2001, 1, 1, .01m));
            Assert(GameMain.mainPlayer.package.Count == 0 && wallet.State.Balance == 1);
            wallet.Deposit(1); PlayerInventory.BuyBuilding(wallet, 2001, 1, 1, .01m);
            Assert(wallet.State.Balance == .99m && wallet.State.FeesPaid == .01m);
            PlayerInventory.SellBuilding(wallet, 2001, 1, 1, .01m);
            Assert(wallet.State.Balance == 1.98m && wallet.NetPnL == -.02m);
        });
        Check("fee ledger overflow is rejected before native item mutation", () => {
            var wallet = new MarginMarket(MarketCatalog.BuildingCoin); wallet.Deposit(100); SetPackage(1, 10);
            wallet.State.FeesPaid = decimal.MaxValue;
            Throws(() => PlayerInventory.BuyBuilding(wallet, 2001, 1, 1, .01m));
            Throws(() => PlayerInventory.SellBuilding(wallet, 2001, 1, 1, .01m));
            Assert(GameMain.mainPlayer.package.Count == 1 && wallet.State.Balance == 100);
        });
        Check("sand transfers use the native sand counter without touching package inventory", () => {
            var market = new MarginMarket(1099); SetPackage(12, 12); GameMain.mainPlayer.sandCount = 3000000000L;
            Assert(PlayerInventory.Available(1099) == 3000000000L && PlayerInventory.Count(1099) == int.MaxValue);
            PlayerInventory.Deposit(market, 200); PlayerInventory.Withdraw(market, 100);
            Assert(GameMain.mainPlayer.sandCount == 2999999900L && market.State.Balance == 100);
            Assert(GameMain.mainPlayer.package.Count == 12 && GameMain.mainPlayer.SandNotifications == 2);
        });
        Check("sand availability is rechecked and rejected transfers preserve both counters", () => {
            var market = new MarginMarket(1099); SetPackage(0, 0); GameMain.mainPlayer.sandCount = 100;
            Assert(PlayerInventory.Available(1099) == 100); GameMain.mainPlayer.sandCount = 1;
            Throws(() => PlayerInventory.Deposit(market, 2)); Assert(market.State.Balance == 0 && GameMain.mainPlayer.sandCount == 1);
            market.Deposit(10); Throws(() => PlayerInventory.Withdraw(market, 11));
            Assert(market.State.Balance == 10 && GameMain.mainPlayer.sandCount == 1 && GameMain.mainPlayer.SandNotifications == 0);
        });
        Check("sand ledger overflow is rejected before native mutation and long capacity settles actual quantity", () => {
            var market = new MarginMarket(1099); SetPackage(0, 0); GameMain.mainPlayer.sandCount = 100;
            market.State.TotalDeposited = long.MaxValue; Throws(() => PlayerInventory.Deposit(market, 1));
            Assert(GameMain.mainPlayer.sandCount == 100 && market.State.Balance == 0);
            market.State.TotalDeposited = 0; market.Deposit(100); GameMain.mainPlayer.sandCount = long.MaxValue - 3;
            PlayerInventory.Withdraw(market, 10); Assert(market.State.Balance == 97 && market.State.TotalWithdrawn == 3 && GameMain.mainPlayer.sandCount == long.MaxValue);
            Throws(() => PlayerInventory.Withdraw(market, 1)); Assert(market.State.Balance == 97);
        });
        Check("sand deposit does not require an available package", () => {
            var market = new MarginMarket(1099); GameMain.mainPlayer = new Player { sandCount = 20 };
            PlayerInventory.Deposit(market, 20); PlayerInventory.Withdraw(market, 20);
            Assert(GameMain.mainPlayer.sandCount == 20 && market.State.Balance == 0);
        });
        Check("achievement purchases count actual receipt and exclude sales or failed orders", () => {
            var book = new MarketBook(); var state = new TradingAchievementState(); new AchievementTracker(book, state).Initialize();
            var wallet = book.Get(MarketCatalog.BuildingCoin); wallet.Deposit(100000); SetPackage(0, 100);
            PlayerInventory.BuyBuilding(wallet, 2001, 200, 1, 0);
            Assert(state.PurchasedKindCount == 1 && !state.Completed(AchievementId.BigPurchase));
            Throws(() => PlayerInventory.BuyBuilding(wallet, 2002, 1, 1, 0)); Assert(state.PurchasedKindCount == 1);
            PlayerInventory.SellBuilding(wallet, 2002, 100, 1, 0); Assert(state.PurchasedKindCount == 1);
            SetPackage(0, 200); PlayerInventory.BuyBuilding(wallet, 5001, 101, 1, 0);
            Assert(state.Completed(AchievementId.BigPurchase) && state.PurchasedKindCount == 2);
        });
        Console.WriteLine("All " + _passed + " FXDSP inventory adapter checks passed.");
    }

    private static void SetPackage(int quantity, int capacity)
    {
        GameMain.mainPlayer = new Player { package = new StorageComponent { Count = quantity, Capacity = capacity } };
    }
    private static void Check(string name, Action action) { action(); _passed++; Console.WriteLine("PASS: " + name); }
    private static void Assert(bool condition) { if (!condition) throw new Exception("Inventory invariant failed."); }
    private static void Throws(Action action)
    {
        try { action(); } catch (InvalidOperationException) { return; } catch (OverflowException) { return; }
        throw new Exception("Expected a rejected transfer.");
    }
}

// Minimal game boundary doubles; the compiled adapter itself is linked unchanged.
internal static class GameMain { public static Player mainPlayer; }
internal enum ESandSource { Other }
internal sealed class Player
{
    public StorageComponent package;
    public long sandCount;
    public int SandNotifications;
    public void SetSandCount(long value, ESandSource source) { sandCount = value; SandNotifications++; }
    public int NotifiedCount;
    public void NotifyPackageAddItem(int itemId, int count, int inc) { NotifiedCount += count; }
}
internal sealed class StorageComponent
{
    public int Count;
    public int Capacity;
    public int Inc;
    public int TakeLimit = int.MaxValue;
    public int LastReadId, LastTakenId, LastInsertedId;
    public int GetItemCount(int itemId) { LastReadId = itemId; return Count; }
    public int GetItemCount(int itemId, out int inc) { LastReadId = itemId; inc = Inc; return Count; }
    public int TakeItem(int itemId, int count, out int inc)
    {
        LastTakenId = itemId; inc = 0; var removed = Math.Min(Math.Min(count, Count), TakeLimit); Count -= removed; return removed;
    }
    public int AddItemStacked(int itemId, int count, int inc, out int remainInc)
    {
        LastInsertedId = itemId; remainInc = 0; var inserted = Math.Min(count, Math.Max(0, Capacity - Count)); Count += inserted; return inserted;
    }
}
