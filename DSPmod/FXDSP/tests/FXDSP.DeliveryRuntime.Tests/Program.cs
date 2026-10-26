using System;
using System.IO;
using System.Linq;
using FXDSP.Core;
using FXDSP.Persistence;
using FXDSP.Runtime;

internal static class Program
{
    private static MarketSession session;
    private static DeliveryService service;
    private static PlanetFactory factory;
    private static int passed;
    private static void Main()
    {
        Check("choosing different items automatically binds independent towers", () => {
            Require(Set(1, 0, 1002) && Set(2, 0, 1115));
            Require(service.BoundPlatform(1002).EntityId == 1 && service.BoundPlatform(1115).EntityId == 2);
            session.Market.Deposit(20); session.GetMarket(1115).Deposit(30);
            service.Transfer(session.Market, 20, false); service.Transfer(session.GetMarket(1115), 30, false);
            Require(service.Available(1002).Count == 20 && service.Available(1115).Count == 30);
        });
        Check("duplicates within a tower and across towers are blocked with a location popup", () => {
            Require(Set(1, 0, 1002)); Require(!Set(1, 1, 1002) && !Set(2, 0, 1002));
            Require(UIMessageBox.LastTitle == "物品已绑定" && UIMessageBox.LastMessage.Contains("Test Planet") && UIMessageBox.LastMessage.Contains("#1"));
            Require(factory.transport.stationPool[1].storage[1].itemId == 0 && factory.transport.stationPool[2].storage[0].itemId == 0);
        });
        Check("warp cells are exempt from uniqueness in the same and other delivery towers", () => {
            Require(Set(1, 0, 1210) && Set(1, 1, 1210) && Set(2, 0, 1210));
            service.Tick(); Require(factory.transport.stationPool[1].storage[1].itemId == 1210 && factory.transport.stationPool[2].storage[0].itemId == 1210);
        });
        Check("native inventory changes after a UI read are observed by deposits", () => {
            Require(Set(1, 0, 1002)); var station = factory.transport.stationPool[1]; station.storage[0].count = 10;
            Require(service.Available(1002).Count == 10); int item = 1002, count = 10, inc; station.TakeItem(ref item, ref count, out inc);
            Reject(() => service.Transfer(session.Market, 10, true)); Require(session.Market.State.Balance == 0);
        });
        Check("warp market deposits can consume multiple configured warp slots without double counting", () => {
            Require(Set(1, 0, 1210) && Set(1, 1, 1210));
            session.SetResourceEnabled(1210, true);
            var station = factory.transport.stationPool[1]; station.storage[0].count = 100; station.storage[1].count = 200;
            Require(service.Available(1210).Count == 300);
            service.Transfer(session.GetMarket(1210), 300, true);
            Require(session.GetMarket(1210).State.Balance == 300 && service.Available(1210).Count == 0);
        });
        Check("unbounded transfers preserve the sum of native buffers and extended stock", () => {
            Require(Set(1, 0, 1002)); session.Market.Deposit(2000000000); service.Transfer(session.Market, 2000000000, false);
            Require(service.Available(1002).Count == 2000000000 && session.Market.State.Balance == 0);
            service.Transfer(session.Market, 1900000000, true);
            Require(service.Available(1002).Count == 100000000 && session.Market.State.Balance == 1900000000);
        });
        Check("removing an empty slot frees only that binding and reassignment is immediate", () => {
            Require(Set(1, 0, 1002) && Set(1, 1, 1115)); Require(Set(1, 0, 0)); Require(Set(2, 0, 1002));
            Require(service.BoundPlatform(1002).EntityId == 2 && service.BoundPlatform(1115).EntityId == 1);
        });
        Check("a slot with extended stock cannot be replaced or cleared", () => {
            Require(Set(1, 0, 1002)); session.Market.Deposit(1000000); service.Transfer(session.Market, 1000000, false);
            Require(!Set(1, 0, 1115) && !Set(1, 0, 0));
            Require(service.Available(1002).Count == 1000000 && service.BoundPlatform(1002).EntityId == 1);
        });
        Check("native tower slot counts and logistics modes remain intact", () => {
            Require(Set(1, 0, 1002)); var station = factory.transport.stationPool[1];
            station.storage[0].localLogic = ELogisticStorage.Supply; station.storage[0].remoteLogic = ELogisticStorage.Demand;
            station.storage[0].count = 900000; station.storage[0].inc = 3600000; service.Tick();
            var node = service.BoundPlatform(1002);
            Require(station.storage.Length == 3 && factory.transport.stationPool[2].storage.Length == 5);
            Require(station.storage[0].localLogic == ELogisticStorage.Supply && station.storage[0].remoteLogic == ELogisticStorage.Demand);
            Require(node.Stock(1002).Count + station.storage[0].count == 900000 && node.Stock(1002).Inc + station.storage[0].inc == 3600000);
        });
        Check("dismantled stock is restored once when that item is configured in another tower", () => {
            Require(Set(1, 0, 1002)); session.Market.Deposit(1000000000); service.Transfer(session.Market, 1000000000, false);
            var node = service.BoundPlatform(1002); service.Removing(factory, 1); service.Removing(factory, 1);
            Require(service.BoundPlatform(1002) == null && node.Orphaned && node.Stock(1002).Count == 1000000000);
            factory.entityPool[1].id = 0;
            Require(Set(2, 0, 1002)); service.Tick(); service.Tick();
            Require(service.Available(1002).Count == 1000000000 && node.Stock(1002).Count == 0);
        });
        Check("F6 rebuilds per-item bindings from native slots and keeps inventories", () => {
            Require(Set(1, 0, 1002) && Set(2, 0, 1115)); session.Market.Deposit(1000000000); service.Transfer(session.Market, 1000000000, false);
            session.PreserveForReload(); var next = new MarketSession(); var nextService = new DeliveryService(next); nextService.Tick();
            Require(next.CanTrade && nextService.Available(1002).Count == 1000000000);
            Require(nextService.BoundPlatform(1115).EntityId == 2 && !service.Ready); nextService.Deactivate();
        });
        Check("export after native save observation does not move physical inventory", () => {
            Require(Set(1, 0, 1002)); session.Market.Deposit(1000000000); service.Transfer(session.Market, 1000000000, false);
            var node = service.BoundPlatform(1002); var before = node.Stock(1002).Count; int native = factory.transport.stationPool[1].storage[0].count;
            using(var stream = new MemoryStream()) using(var writer = new BinaryWriter(stream)) session.Export(writer);
            Require(node.Stock(1002).Count == before && factory.transport.stationPool[1].storage[0].count == native);
        });
        Check("ordinary logistics towers do not join delivery bindings", () => {
            int max = 12345; Require(service.PrepareStationSlot(factory.transport, 3, 0, 1002, ref max) && max == 12345);
            DeliveryPlatform platform; Require(!service.Find(factory.transport.stationPool[3], out platform));
        });
        Check("directly copied duplicate empty settings are cleared instead of becoming unlimited storage", () => {
            Require(Set(1, 0, 1002)); factory.transport.stationPool[2].storage[0].itemId = 1002;
            service.Tick(); Require(factory.transport.stationPool[2].storage[0].itemId == 0 && service.BoundPlatform(1002).EntityId == 1);
        });
        Check("planetary stations accept their native zero planet id but reject a foreign planet", () => {
            Require(factory.transport.stationPool[1].planetId == 0 && Set(1, 0, 1002));
            Require(service.BoundPlatform(1002).EntityId == 1);
            factory.transport.stationPool[1].planetId = 999; service.Tick();
            Require(service.BoundPlatform(1002) == null);
        });
        Check("entity and component reuse cannot silently inherit a binding", () => {
            Require(Set(1, 0, 1002)); var node = service.BoundPlatform(1002); factory.entityPool[1].pos.x += 1; service.Tick();
            Require(service.BoundPlatform(1002) == null && node.Orphaned && !session.Delivery.Platforms.Contains(node));
        });
        Check("foreign item prototypes with identical IDs remain ordinary towers", () => {
            var station = factory.transport.stationPool[1]; station.storage[0].max = 9000; station.storage[0].count = 700;
            LDB.items.Entries[DeliveryBuildings.Planetary] = new ItemProto { name = "Foreign tower" };
            service.Register(factory, 1); service.Tick(); int max = 9000;
            Require(!service.Ready && service.PrepareStationSlot(factory.transport, 1, 0, 1002, ref max));
            Require(max == 9000 && station.storage[0].count == 700 && service.BoundPlatform(1002) == null);
        });
        Check("foreign recipes cannot enable delivery logic even when item ownership matches", () => {
            LDB.recipes.Entries[DeliveryBuildings.Interstellar] = new object();
            Require(!service.PrototypesReady && !service.Ready);
        });
        Check("foreign unlocked technology IDs grant neither trading access nor fee upgrades", () => {
            var state = ResearchProgression.Read();
            Require(!state.Access && state.TradingFee == .003m && state.ExchangeFee == .10m);
            foreach (var tech in TradeResearch.Definitions) PrototypeOwnership.Mark("tech", tech.Id, LDB.techs.Select(tech.Id));
            state = ResearchProgression.Read(); Require(state.Access && state.TradingFee == 0m && state.ExchangeFee == 0m);
            LDB.techs.Entries[TradeResearch.AccessTech] = new TechProto { ID = TradeResearch.AccessTech };
            Require(!ResearchProgression.Read().Access);
        });
        Check("technology text patch leaves a foreign prototype at our ID untouched", () => {
            var prefix = typeof(TradeTechnologyTextPatch).GetMethod("Prefix", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            var foreign = LDB.techs.Select(TradeResearch.AccessTech); object[] args = { foreign, "Foreign mod text" };
            Require((bool)prefix.Invoke(null, args) && (string)args[1] == "Foreign mod text");
            PrototypeOwnership.Mark("tech", foreign.ID, foreign);
            args = new object[] { foreign, "" };
            Require(!(bool)prefix.Invoke(null, args) && (string)args[1] == TradeResearch.Find(foreign.ID).UnlockText);
        });
        Check("a cancelled dismantle restores binding and all inventory including proliferator points", () => {
            Require(Set(1, 0, 1002)); var station = factory.transport.stationPool[1];
            station.storage[0].count = 900000; station.storage[0].inc = 3600000;
            var node = service.Removing(factory, 1); Require(node.Orphaned && station.storage[0].count == 0);
            service.RestoreCancelledRemoval(factory, node); service.RestoreCancelledRemoval(factory, node); service.Tick();
            Require(!node.Orphaned && service.BoundPlatform(1002) == node);
            Require(service.Available(1002).Count == 900000 && service.Available(1002).Inc == 3600000);
        });
        Check("completed dismantles are not restored and empty retired metadata is pruned", () => {
            var node = service.Removing(factory, 1); factory.entityPool[1].id = 0;
            service.RestoreCancelledRemoval(factory, node); Require(node.Orphaned);
            service.Tick(); Require(!session.Delivery.Platforms.Contains(node));
        });
        Check("retired stock is retained until recovered while empty records stop accumulating", () => {
            Require(Set(1, 0, 1002)); factory.transport.stationPool[1].storage[0].count = 40;
            var node = service.Removing(factory, 1); factory.entityPool[1].id = 0; service.Tick();
            Require(session.Delivery.Platforms.Contains(node) && node.Stock(1002).Count == 40);
            Require(Set(2, 0, 1002)); service.Tick();
            Require(!session.Delivery.Platforms.Contains(node) && service.Available(1002).Count == 40);
        });
        Console.WriteLine("All " + passed + " delivery runtime checks passed.");
    }
    private static bool Set(int stationId, int slot, int item)
    {
        int max = 10000;
        if (!service.PrepareStationSlot(factory.transport, stationId, slot, item, ref max)) return false;
        factory.transport.SetStationStorage(stationId, slot, item, max, ELogisticStorage.Supply, ELogisticStorage.None, GameMain.mainPlayer);
        service.StationSlotChanged(factory.transport.stationPool[stationId], slot); return true;
    }
    private static void Check(string name, Action action) { Setup(); action(); service.Deactivate(); passed++; Console.WriteLine("PASS " + name); }
    private static void Setup()
    {
        LDB.items = new ItemSet(); LDB.recipes = new RecipeSet(); LDB.techs = new TechSet();
        foreach (int id in new[] { DeliveryBuildings.Planetary, DeliveryBuildings.Interstellar })
        {
            PrototypeOwnership.Mark("item", id, LDB.items.Select(id));
            PrototypeOwnership.Mark("recipe", id, LDB.recipes.Select(id));
        }
        AppDomain.CurrentDomain.SetData("FXDSP.MarketSession.Handoff.v1", null); AppDomain.CurrentDomain.SetData("FXDSP.MarketSession.Owner.v1", null);
        GameMain.isPaused = false; GameMain.isRunning = true; factory = new PlanetFactory();
        for(int id = 1; id <= 3; id++)
        {
            factory.entityPool[id] = new EntityData { id = id, protoId = id == 1 ? DeliveryBuildings.Planetary : id == 2 ? DeliveryBuildings.Interstellar : 2103,
                stationId = id, pos = new UnityEngine.Vector3 { x = 100, y = id, z = 0 } };
            factory.transport.stationPool[id] = new StationComponent { id = id, entityId = id, planetId = id == 2 ? 101 : 0, storage = new StationStore[id == 2 ? 5 : 3] };
        }
        var planet = new PlanetData { id = 101, factory = factory };
        GameMain.data = new GameData { factories = new[] { factory }, galaxy = new GalaxyData { planet = planet } };
        session = new MarketSession(); session.IntoOtherSave(); service = new DeliveryService(session); service.Tick();
        UIMessageBox.LastTitle = UIMessageBox.LastMessage = "";
    }
    private static void Require(bool value) { if (!value) throw new Exception("Runtime delivery invariant failed."); }
    private static void Reject(Action action) { try { action(); } catch (InvalidOperationException) { return; } throw new Exception("Expected delivery rejection."); }
}
