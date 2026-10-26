using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using FXDSP.Core;
using FXDSP.Persistence;

namespace FXDSP.Runtime
{
    internal sealed class DeliveryService
    {
        internal static DeliveryService Active;
        private readonly MarketSession _session;
        private DeliveryState _state;
        private bool _rebuilding;
        private readonly HashSet<PlanetFactory> _seen = new HashSet<PlanetFactory>();
        private readonly ConcurrentDictionary<StationComponent, DeliveryPlatform> _stations = new ConcurrentDictionary<StationComponent, DeliveryPlatform>();
        internal DeliveryService(MarketSession session) { _session = session; Active = this; }
        internal void Deactivate()
        { if (ReferenceEquals(Active, this)) Active = null; _seen.Clear(); _stations.Clear(); }
        internal bool Ready { get { return _session.CanAccessDelivery && PrototypesReady; } }
        internal bool PrototypesReady
        {
            get
            {
                if (LDB.items == null || LDB.recipes == null) return false;
                for (int id = DeliveryBuildings.Planetary; id <= DeliveryBuildings.Interstellar; id++)
                    if (!PrototypeOwnership.Owns("item", id, LDB.items.Select(id))
                        || !PrototypeOwnership.Owns("recipe", id, LDB.recipes.Select(id))) return false;
                return true;
            }
        }
        internal DeliveryState State { get { return _session.Delivery; } }
        internal void Tick()
        {
            if (!Ready || GameMain.data == null)
            {
                if (GameMain.data == null || !GameMain.isRunning) { _seen.Clear(); _stations.Clear(); _state = null; }
                return;
            }
            if (!PrototypesReady) return;
            if (!ReferenceEquals(_state, State)) { _state = State; _seen.Clear(); _stations.Clear(); }
            foreach (var factory in GameMain.data.factories)
                if (factory != null && _seen.Add(factory))
                    for (int id = 1; id < factory.entityCursor; id++)
                        if (factory.entityPool[id].id == id && DeliveryBuildings.Contains(factory.entityPool[id].protoId)) Register(factory, id);
            RebuildBindings();
            RestoreDetachedStock();
            foreach (var platform in State.Platforms.Where(candidate => !candidate.Orphaned).ToArray())
            {
                var factory = Factory(platform);
                if (factory == null) continue;
                if (!Matches(platform, factory)) { Retire(platform); continue; }
                Sync(platform, factory);
            }
            for (int id = DeliveryBuildings.Planetary; id <= DeliveryBuildings.Interstellar; id++)
                if (GameMain.history.ItemUnlocked(DeliveryBuildings.BaseItem(id)) && !GameMain.history.RecipeUnlocked(id)) GameMain.history.UnlockRecipe(id);
            // Empty retired records have no inventory to restore and otherwise accumulate forever.
            State.Platforms.RemoveAll(platform => platform.Orphaned && !platform.HasReserve);
        }
        internal void Register(PlanetFactory factory, int entityId)
        {
            if (!Ready || entityId <= 0 || entityId >= factory.entityPool.Length) return;
            var entity = factory.entityPool[entityId];
            if (!DeliveryBuildings.Contains(entity.protoId)) return;
            var platform = State.Platforms.Find(candidate => !candidate.Orphaned && candidate.PlanetId == factory.planetId && candidate.EntityId == entityId);
            if (platform != null && !Matches(platform, factory)) { Retire(platform); platform = null; }
            if (platform == null)
            {
                platform = new DeliveryPlatform { PlanetId = factory.planetId, EntityId = entityId, ProtoId = entity.protoId,
                    X = entity.pos.x, Y = entity.pos.y, Z = entity.pos.z };
                State.Platforms.Add(platform);
            }
            if (entity.stationId > 0) _stations[factory.transport.stationPool[entity.stationId]] = platform;
            Sync(platform, factory);
        }
        internal bool Find(StationComponent station, out DeliveryPlatform platform)
        { platform = null; return station != null && _stations.TryGetValue(station, out platform) && !platform.Orphaned && Ready && ReferenceEquals(_state, State); }
        internal bool PrepareStationSlot(PlanetTransport transport, int stationId, int index, int itemId, ref int maximum)
        {
            if (stationId <= 0 || stationId >= transport.stationPool.Length) return true;
            var station = transport.stationPool[stationId]; DeliveryPlatform platform;
            if (!Find(station, out platform)) return true;
            if (index < 0 || index >= station.storage.Length) return false;
            maximum = DeliveryBuildings.NativeBuffer;
            if (_rebuilding) return true;
            if (!State.CanAssign(platform, index, itemId))
            {
                var occupied = State.FindBinding(itemId);
                var proto = LDB.items.Select(itemId);
                Popup("物品已绑定", (proto == null ? "该物品" : proto.name) + "已在" + Location(occupied.Platform)
                    + "的第 " + (occupied.Slot + 1) + " 个槽位设置。请先清空并移除原槽位。 ");
                return false;
            }
            int old = station.storage[index].itemId;
            DeliveryStock reserve;
            if (itemId != old && platform.Reserve.TryGetValue(old, out reserve) && reserve.Count > 0)
            { Popup("槽位仍有库存", "请先取出该槽位的全部物品，再更换或移除物品。"); return false; }
            return true;
        }
        internal void StationSlotChanged(StationComponent station, int index)
        {
            DeliveryPlatform platform;
            if (_rebuilding || !Find(station, out platform) || index < 0 || index >= station.storage.Length) return;
            State.Assign(platform, index, station.storage[index].itemId);
            RestoreDetachedStock();
            _session.PreserveForReload();
        }
        private static void Popup(string title, string message)
        { UIMessageBox.Show(TextLocalization.Translate(title), TextLocalization.Translate(message), TextLocalization.Translate("知道了"), 0); }
        private static string Location(DeliveryPlatform platform)
        {
            var planet = GameMain.data.galaxy.PlanetById(platform.PlanetId);
            var factory = planet?.factory;
            int number = factory != null && Matches(platform, factory) ? factory.entityPool[platform.EntityId].stationId : platform.EntityId;
            return (planet == null ? "星球 " + platform.PlanetId : planet.displayName) + " · " + DeliveryBuildings.Name(platform.ProtoId) + " #" + number;
        }
        internal void RebuildBindings()
        {
            if (_rebuilding || !Ready) return;
            _rebuilding = true;
            try
            {
                State.ResetBindings();
                foreach (var platform in State.Platforms.Where(candidate => !candidate.Orphaned && DeliveryBuildings.IsTower(candidate.ProtoId))
                    .OrderBy(candidate => candidate.PlanetId).ThenBy(candidate => candidate.EntityId))
                {
                    var factory = Factory(platform);
                    if (factory == null || !Matches(platform, factory)) continue;
                    var station = factory.transport.stationPool[factory.entityPool[platform.EntityId].stationId];
                    lock (station.storage)
                        for (int slot = 0; slot < station.storage.Length; slot++)
                        {
                            int item = station.storage[slot].itemId;
                            if (item <= 0) continue;
                            if (State.CanAssign(platform, slot, item)) { State.Assign(platform, slot, item); continue; }
                            // Guard settings written by copy tools that bypass the picker. Invalid slots never receive unlimited storage.
                            if (station.storage[slot].count == 0)
                                factory.transport.SetStationStorage(station.id, slot, 0, DeliveryBuildings.NativeBuffer,
                                    (ELogisticStorage)0, (ELogisticStorage)0, GameMain.mainPlayer);
                            else
                            {
                                station.storage[slot].max = 0;
                                station.storage[slot].localLogic = station.storage[slot].remoteLogic = (ELogisticStorage)0;
                            }
                        }
                }
            }
            finally { _rebuilding = false; }
        }
        private void RestoreDetachedStock()
        {
            foreach (var orphan in State.Platforms.Where(candidate => candidate.Orphaned))
                foreach (var pair in orphan.Reserve.Where(pair => pair.Value.Count > 0))
                {
                    var binding = State.FindBinding(pair.Key);
                    if (binding == null) continue;
                    var factory = Factory(binding.Platform);
                    if (factory == null || !Matches(binding.Platform, factory)) continue;
                    lock (SyncRoot(binding.Platform, factory))
                    { binding.Platform.Stock(pair.Key).Add(pair.Value.Count, pair.Value.Inc); pair.Value.Count = pair.Value.Inc = 0; }
                }
        }
        internal bool SlotBound(DeliveryPlatform platform, int slot, int itemId)
        {
            if (itemId == DeliveryBuildings.Warper) return true;
            var binding = State.FindBinding(itemId);
            return binding != null && binding.Platform == platform && binding.Slot == slot;
        }
        internal DeliveryPlatform Removing(PlanetFactory factory, int entityId)
        {
            if (!Ready) return null;
            var platform = State.Platforms.Find(candidate => !candidate.Orphaned && candidate.PlanetId == factory.planetId && candidate.EntityId == entityId);
            if (platform == null || !Matches(platform, factory)) return null;
            lock (SyncRoot(platform, factory))
            {
            var entity = factory.entityPool[entityId];
            if (entity.stationId > 0)
            {
                var station = factory.transport.stationPool[entity.stationId];
                lock (station.storage)
                    for (int i = 0; i < station.storage.Length; i++)
                    {
                        var slot = station.storage[i];
                        if (slot.itemId > 0 && slot.count > 0) platform.Stock(slot.itemId).Add(slot.count, slot.inc);
                        station.storage[i].count = station.storage[i].inc = 0;
                    }
                DeliveryPlatform ignored; _stations.TryRemove(station, out ignored);
            }
            Retire(platform);
            }
            return platform;
        }
        internal void RestoreCancelledRemoval(PlanetFactory factory, DeliveryPlatform platform)
        {
            // Another Harmony prefix may veto dismantling after we have escrowed native stock.
            if (!Ready || platform == null || !platform.Orphaned || !State.Platforms.Contains(platform)
                || !Matches(platform, factory)) return;
            platform.Orphaned = false;
            _stations[factory.transport.stationPool[factory.entityPool[platform.EntityId].stationId]] = platform;
            RebuildBindings();
            Sync(platform, factory);
        }
        private void Retire(DeliveryPlatform platform)
        { State.Release(platform); platform.Orphaned = true; }
        internal DeliveryPlatform BoundPlatform(int itemId)
        {
            if (!Ready) return null;
            var binding = State.FindBinding(itemId);
            var platform = binding?.Platform;
            var factory = platform == null ? null : Factory(platform);
            if (factory == null || !Matches(platform, factory)) return null;
            var station = factory.transport.stationPool[factory.entityPool[platform.EntityId].stationId];
            return binding.Slot < station.storage.Length && station.storage[binding.Slot].itemId == itemId ? platform : null;
        }
        internal string BindingName(int itemId)
        { var platform = BoundPlatform(itemId); return platform == null ? "未设置该物品的交付槽位" : Location(platform); }
        internal IDeliveryInventory BoundInventory(int itemId)
        {
            var platform = BoundPlatform(itemId);
            if (platform == null) throw new InvalidOperationException("请先在交付物流塔中选择该物品。");
            return new Inventory(this, platform, Factory(platform), itemId);
        }
        internal DeliveryStock Available(int itemId)
        {
            try { return BoundInventory(itemId).Read(itemId); } catch (InvalidOperationException) { return new DeliveryStock(); }
        }
        internal string Transfer(MarginMarket market, int quantity, bool deposit)
        {
            var inventory = (Inventory)BoundInventory(market.State.ItemId);
            lock (inventory.SyncRoot)
            {
                int moved = deposit ? DeliveryTransfers.Deposit(market, inventory, quantity) : DeliveryTransfers.Withdraw(market, inventory, quantity);
                return "已" + (deposit ? "存入 " : "提取 ") + moved + " " + market.Unit + "。";
            }
        }
        private static PlanetFactory Factory(DeliveryPlatform platform)
        { return GameMain.data == null || GameMain.data.galaxy == null ? null : GameMain.data.galaxy.PlanetById(platform.PlanetId)?.factory; }
        private static bool Matches(DeliveryPlatform platform, PlanetFactory factory)
        {
            if (platform == null || platform.EntityId <= 0 || platform.EntityId >= factory.entityPool.Length) return false;
            var entity = factory.entityPool[platform.EntityId];
            if (entity.id != platform.EntityId || entity.protoId != platform.ProtoId || Math.Abs(entity.pos.x - platform.X) >= .001f
                || Math.Abs(entity.pos.y - platform.Y) >= .001f || Math.Abs(entity.pos.z - platform.Z) >= .001f) return false;
            var stations = factory.transport.stationPool;
            return entity.stationId > 0 && entity.stationId < stations.Length && stations[entity.stationId] != null
                && stations[entity.stationId].id == entity.stationId && stations[entity.stationId].entityId == entity.id
                && (platform.ProtoId == DeliveryBuildings.Planetary
                    ? stations[entity.stationId].planetId == 0 || stations[entity.stationId].planetId == platform.PlanetId
                    : stations[entity.stationId].planetId == platform.PlanetId);
        }
        private static object SyncRoot(DeliveryPlatform platform, PlanetFactory factory)
        {
            var entity = factory.entityPool[platform.EntityId];
            return factory.transport.stationPool[entity.stationId].storage;
        }
        private void Sync(DeliveryPlatform platform, PlanetFactory factory)
        {
            lock (SyncRoot(platform, factory))
            {
            var entity = factory.entityPool[platform.EntityId];
            if (entity.stationId > 0)
            {
                var station = factory.transport.stationPool[entity.stationId];
                lock (station.storage)
                    for (int i = 0; i < station.storage.Length; i++)
                    {
                        int id = station.storage[i].itemId;
                        bool bound = id <= 0 || SlotBound(platform, i, id);
                        station.storage[i].max = bound ? DeliveryBuildings.NativeBuffer : 0;
                        if (id > 0 && bound) Balance(platform.Stock(id), ref station.storage[i].count, ref station.storage[i].inc);
                    }
            }
            }
        }
        private static void Balance(DeliveryStock reserve, ref int count, ref int inc)
        {
            if (count > DeliveryBuildings.BufferTarget)
            {
                int spill = count - DeliveryBuildings.BufferTarget;
                int points = (int)((long)inc * spill / count);
                reserve.Add(spill, points); count -= spill; inc -= points;
            }
            else if (count < DeliveryBuildings.BufferTarget && reserve.Count > 0)
            { int points; count += reserve.Remove(DeliveryBuildings.BufferTarget - count, out points); inc += points; }
        }

        private sealed class Inventory : IDeliveryInventory
        {
            private readonly DeliveryService _owner;
            private readonly DeliveryPlatform _platform;
            private readonly PlanetFactory _factory;
            private readonly int _itemId;
            internal Inventory(DeliveryService owner, DeliveryPlatform platform, PlanetFactory factory, int itemId)
            { _owner = owner; _platform = platform; _factory = factory; _itemId = itemId; }
            internal object SyncRoot { get { Validate(); return DeliveryService.SyncRoot(_platform, _factory); } }
            private void Validate()
            { if (!_owner.Ready || _platform.Orphaned || _owner.BoundPlatform(_itemId) != _platform || !Matches(_platform, _factory)) throw new InvalidOperationException("交付平台绑定已变化。"); }
            public DeliveryStock Read(int itemId)
            {
                lock (SyncRoot)
                {
                Validate(); var total = new DeliveryStock(); DeliveryStock reserve;
                if (_platform.Reserve.TryGetValue(itemId, out reserve)) total.Add(reserve.Count, reserve.Inc);
                var entity = _factory.entityPool[_platform.EntityId];
                var station = _factory.transport.stationPool[entity.stationId];
                foreach (var slot in station.storage) if (slot.itemId == itemId) total.Add(slot.count, slot.inc);
                return total;
                }
            }
            public int Remove(int itemId, int count, out int inc)
            {
                lock (SyncRoot)
                {
                Validate(); inc = 0; int removed = 0; DeliveryStock reserve;
                if (_platform.Reserve.TryGetValue(itemId, out reserve)) removed = reserve.Remove(count, out inc);
                int remaining = count - removed;
                var entity = _factory.entityPool[_platform.EntityId];
                if (remaining > 0)
                {
                    var station = _factory.transport.stationPool[entity.stationId];
                    // Native TakeItem assumes unique item slots. Warp cells may occupy several delivery slots.
                    for (int slot = 0; slot < station.storage.Length && remaining > 0; slot++)
                        if (station.storage[slot].itemId == itemId && station.storage[slot].count > 0)
                        {
                            int take = Math.Min(remaining, station.storage[slot].count);
                            int points = (int)((long)station.storage[slot].inc * take / station.storage[slot].count);
                            station.storage[slot].count -= take; station.storage[slot].inc -= points;
                            removed += take; remaining -= take; inc += points;
                        }
                }
                _owner.Sync(_platform, _factory); return removed;
                }
            }
            public int Add(int itemId, int count, int inc)
            {
                lock (SyncRoot)
                {
                Validate(); var entity = _factory.entityPool[_platform.EntityId];
                var station = _factory.transport.stationPool[entity.stationId];
                if (itemId != _itemId || !station.storage.Any(slot => slot.itemId == itemId)) return 0;
                _platform.Stock(itemId).Add(count, inc); _owner.Sync(_platform, _factory); return count;
                }
            }
        }
    }
}
