using System;
using System.IO;
using System.Linq;
using System.Numerics;
using FXDSP.Core;

internal static class Program
{
    private static int passed;
    private static void Main()
    {
        Check("different items bind to independent towers without a global binding switch", () => {
            var state = new DeliveryState(); var a = Platform(DeliveryBuildings.Planetary); var b = Platform(DeliveryBuildings.Interstellar); b.EntityId++;
            state.Platforms.Add(a); state.Platforms.Add(b); state.Assign(a, 0, 1002); state.Assign(b, 0, 1115);
            Require(state.FindBinding(1002).Platform == a && state.FindBinding(1115).Platform == b);
            Reject(() => state.Assign(b, 1, 1002)); Reject(() => state.Assign(a, 1, 1002));
            Require(state.FindBinding(1002).Platform == a && state.FindBinding(1115).Platform == b);
        });
        Check("removing a slot releases only that item's binding", () => {
            var state = new DeliveryState(); var a = Platform(DeliveryBuildings.Planetary); var b = Platform(DeliveryBuildings.Interstellar); b.EntityId++;
            state.Platforms.Add(a); state.Platforms.Add(b); state.Assign(a, 0, 1002); state.Assign(a, 1, 1115);
            state.Assign(a, 0, 0); state.Assign(b, 0, 1002);
            Require(state.FindBinding(1002).Platform == b && state.FindBinding(1115).Platform == a);
        });
        Check("warp cells may be configured repeatedly within and across towers", () => {
            var state = new DeliveryState(); var a = Platform(DeliveryBuildings.Planetary); var b = Platform(DeliveryBuildings.Interstellar); b.EntityId++;
            state.Platforms.Add(a); state.Platforms.Add(b);
            state.Assign(a, 0, 1210); state.Assign(a, 1, 1210); state.Assign(b, 0, 1210);
            Require(state.FindBinding(1210).Platform == a);
            state.Release(a); Require(state.FindBinding(1210).Platform == b);
        });
        Check("delivery recipes require four planetary or five interstellar towers and 60 seconds in an assembler", () => {
            Require(DeliveryRecipes.Definitions.Count == 2);
            foreach (var recipe in DeliveryRecipes.Definitions) {
                Require(DeliveryBuildings.IsTower(recipe.ItemId) && recipe.BaseItemId == DeliveryBuildings.BaseItem(recipe.ItemId));
                Require(recipe.InputCount == (recipe.ItemId == DeliveryBuildings.Planetary ? 4 : 5) && recipe.TimeSpend == 3600 && !recipe.Handcraft && recipe.Description.Length > 20);
            }
            var occupied = new System.Collections.Generic.HashSet<int> { 2801, 2802 };
            int first = DeliveryRecipes.AllocateGrid(occupied), second = DeliveryRecipes.AllocateGrid(occupied);
            Require(first == 2803 && second == 2804 && first != second);
        });
        Check("unbounded reserve exceeds Int64 and preserves exact quantities and proliferation", () => {
            var state = new DeliveryState(); var platform = Platform(DeliveryBuildings.Planetary); state.Platforms.Add(platform);
            BigInteger count = BigInteger.Pow(10, 40); platform.Stock(1002).Add(count, count * 4);
            var loaded = Load(Bytes(state)); var stock = loaded.Platforms[0].Stock(1002);
            Require(stock.Count == count && stock.Inc == count * 4);
            int inc; Require(stock.Remove(1000, out inc) == 1000 && inc == 4000);
            Require(stock.Count == count - 1000 && stock.Inc == (count - 1000) * 4);
        });
        Check("an open UI count is never trusted for a subsequent deposit", () => {
            var market = new MarginMarket(); var inventory = new Inventory(10);
            Require(inventory.Read(1002).Count == 10);
            inventory.Stock.Count = 0;
            Reject(() => DeliveryTransfers.Deposit(market, inventory, 10));
            Require(market.State.Balance == 0 && inventory.Stock.Count == 0 && inventory.Removals == 0);
        });
        Check("deposit credits only the quantity actually removed", () => {
            var market = new MarginMarket(); var inventory = new Inventory(100) { RemoveLimit = 3 };
            Require(DeliveryTransfers.Deposit(market, inventory, 10) == 3);
            Require(market.State.Balance == 3 && market.State.TotalDeposited == 3 && inventory.Stock.Count == 97);
        });
        Check("withdrawal debits only the quantity actually inserted", () => {
            var market = new MarginMarket(); market.Deposit(100); var inventory = new Inventory(0) { AddLimit = 4 };
            Require(DeliveryTransfers.Withdraw(market, inventory, 10) == 4);
            Require(market.State.Balance == 96 && market.State.TotalWithdrawn == 4 && inventory.Stock.Count == 4);
            inventory.AddLimit = 0; Reject(() => DeliveryTransfers.Withdraw(market, inventory, 10));
            Require(market.State.Balance == 96 && inventory.Stock.Count == 4);
        });
        Check("insufficient funds and invalid slots leave both sides unchanged", () => {
            var market = new MarginMarket(); market.Deposit(10); var inventory = new Inventory(0) { AddLimit = 0 };
            Reject(() => DeliveryTransfers.Withdraw(market, inventory, 11));
            Reject(() => DeliveryTransfers.Withdraw(market, inventory, 10));
            Require(market.State.Balance == 10 && inventory.Stock.Count == 0);
        });
        Check("sprayed stock cannot be exchanged for clean account funds", () => {
            var market = new MarginMarket(); var inventory = new Inventory(100); inventory.Stock.Inc = 400;
            Reject(() => DeliveryTransfers.Deposit(market, inventory, 10));
            Require(market.State.Balance == 0 && inventory.Stock.Count == 100 && inventory.Stock.Inc == 400);
        });
        Check("deposit and withdrawal overflow are rejected before inventory mutation", () => {
            var market = new MarginMarket(); market.State.Balance = decimal.MaxValue;
            var inventory = new Inventory(10); Reject(() => DeliveryTransfers.Deposit(market, inventory, 1));
            Require(inventory.Stock.Count == 10 && inventory.Removals == 0);
            market.State.Balance = 10; market.State.TotalWithdrawn = long.MaxValue;
            Reject(() => DeliveryTransfers.Withdraw(market, inventory, 1)); Require(inventory.Stock.Count == 10);
        });
        Check("demolished reserves remain recoverable and cannot stay bound", () => {
            var state = new DeliveryState(); var platform = Platform(DeliveryBuildings.Interstellar); state.Platforms.Add(platform);
            platform.Stock(1115).Add(1000, 500); state.Assign(platform, 0, 1115); state.Release(platform); platform.Orphaned = true;
            var loaded = Load(Bytes(state)); Require(loaded.FindBinding(1115) == null && loaded.Platforms[0].Stock(1115).Count == 1000);
            Reject(() => loaded.Assign(loaded.Platforms[0], 0, 1115));
        });
        Check("duplicate live entity identities and truncated delivery records are rejected", () => {
            var state = new DeliveryState(); state.Platforms.Add(Platform(DeliveryBuildings.Planetary));
            byte[] bytes = Bytes(state);
            for (int n = 0; n < bytes.Length; n++) Invalid(bytes.Take(n).ToArray());
            state.Platforms.Add(Platform(DeliveryBuildings.Interstellar)); Invalid(Bytes(state));
        });
        Check("invalid tokens quantities and proliferation cannot enter saved state", () => {
            var state = new DeliveryState(); var platform = Platform(DeliveryBuildings.Planetary); state.Platforms.Add(platform);
            platform.Token = "bad"; Invalid(Bytes(state)); platform.Token = Guid.NewGuid().ToString("N");
            platform.Stock(1002).Count = -1; Invalid(Bytes(state));
            Reject(() => new DeliveryStock().Add(1, 11));
        });
        Check("building currency cannot enter physical delivery adapters", () => {
            var market = new MarginMarket(MarketCatalog.BuildingCoin); market.Deposit(10); var inventory = new Inventory(10);
            Reject(() => DeliveryTransfers.Deposit(market, inventory, 1)); Reject(() => DeliveryTransfers.Withdraw(market, inventory, 1));
            Require(market.State.Balance == 10 && inventory.Stock.Count == 10);
        });
        Console.WriteLine("All " + passed + " delivery checks passed.");
    }
    private static DeliveryPlatform Platform(int proto) { return new DeliveryPlatform { PlanetId = 101, EntityId = 10, ProtoId = proto, X = 100, Y = 10, Z = 3 }; }
    private static byte[] Bytes(DeliveryState state) { using(var s = new MemoryStream()) using(var w = new BinaryWriter(s)) { state.Export(w); return s.ToArray(); } }
    private static DeliveryState Load(byte[] bytes) { using(var s = new MemoryStream(bytes)) using(var r = new BinaryReader(s)) return DeliveryState.Import(r); }
    private static void Invalid(byte[] bytes) { try { Load(bytes); } catch (InvalidDataException) { return; } catch (EndOfStreamException) { return; } throw new Exception("Invalid delivery record accepted."); }
    private static void Check(string name, Action action) { action(); passed++; Console.WriteLine("PASS " + name); }
    private static void Require(bool value) { if (!value) throw new Exception("Delivery invariant failed."); }
    private static void Reject(Action action) { try { action(); } catch (InvalidOperationException) { return; } catch (OverflowException) { return; } throw new Exception("Expected rejection."); }
    private sealed class Inventory : IDeliveryInventory
    {
        public readonly DeliveryStock Stock = new DeliveryStock();
        public int RemoveLimit = int.MaxValue, AddLimit = int.MaxValue, Removals;
        internal Inventory(int count) { Stock.Add(count, 0); }
        public DeliveryStock Read(int itemId) { return new DeliveryStock { Count = Stock.Count, Inc = Stock.Inc }; }
        public int Remove(int itemId, int count, out int inc) { Removals++; return Stock.Remove(Math.Min(RemoveLimit, count), out inc); }
        public int Add(int itemId, int count, int inc) { int actual = Math.Min(AddLimit, count); Stock.Add(actual, inc); return actual; }
    }
}
