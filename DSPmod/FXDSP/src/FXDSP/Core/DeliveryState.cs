using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;

namespace FXDSP.Core
{
    public static class DeliveryBuildings
    {
        public const int Planetary = 11702, Interstellar = 11703;
        public const int NativeBuffer = 1000000, BufferTarget = 250000;
        public const int Warper = 1210;
        public static bool IsTower(int id) { return id == Planetary || id == Interstellar; }
        public static bool Contains(int id) { return IsTower(id); }
        public static int BaseItem(int id) { return id == Planetary ? 2103 : id == Interstellar ? 2104 : 0; }
        public static string Name(int id) { return id == Planetary ? "行星级交付平台" : "星际交付平台"; }
    }

    public sealed class DeliveryStock
    {
        public BigInteger Count, Inc;
        public void Add(BigInteger count, BigInteger inc)
        {
            if (count < 0 || inc < 0 || inc > count * 10) throw new InvalidOperationException("物品数量或增产点数无效。");
            Count += count; Inc += inc;
        }
        public int Remove(int requested, out int inc)
        {
            if (requested < 0) throw new InvalidOperationException("数量无效。");
            int taken = (int)BigInteger.Min(Count, requested);
            inc = Count.IsZero ? 0 : (int)(Inc * taken / Count);
            Count -= taken; Inc -= inc;
            return taken;
        }
    }

    public sealed class DeliveryPlatform
    {
        public string Token = Guid.NewGuid().ToString("N");
        public int PlanetId, EntityId, ProtoId;
        public float X, Y, Z;
        public bool Orphaned;
        public readonly Dictionary<int, DeliveryStock> Reserve = new Dictionary<int, DeliveryStock>();
        public DeliveryStock Stock(int itemId)
        {
            DeliveryStock stock;
            if (!Reserve.TryGetValue(itemId, out stock)) Reserve.Add(itemId, stock = new DeliveryStock());
            return stock;
        }
        public bool HasReserve { get { return Reserve.Values.Any(stock => stock.Count > 0); } }
    }

    public sealed class DeliveryState
    {
        public readonly List<DeliveryPlatform> Platforms = new List<DeliveryPlatform>();
        private readonly Dictionary<int, DeliveryBinding> _items = new Dictionary<int, DeliveryBinding>();
        private readonly Dictionary<string, DeliveryBinding> _slots = new Dictionary<string, DeliveryBinding>();
        private readonly List<DeliveryBinding> _warpers = new List<DeliveryBinding>();
        public DeliveryBinding FindBinding(int itemId)
        {
            DeliveryBinding binding;
            if (itemId == DeliveryBuildings.Warper) binding = _warpers.FirstOrDefault(candidate => !candidate.Platform.Orphaned);
            else _items.TryGetValue(itemId, out binding);
            return binding != null && !binding.Platform.Orphaned ? binding : null;
        }
        public bool CanAssign(DeliveryPlatform platform, int slot, int itemId)
        {
            if (platform == null || platform.Orphaned || !DeliveryBuildings.IsTower(platform.ProtoId) || slot < 0 || slot >= 32 || itemId < 0 || itemId >= 12000) return false;
            var other = FindBinding(itemId);
            return itemId == 0 || itemId == DeliveryBuildings.Warper || other == null || (other.Platform == platform && other.Slot == slot);
        }
        public void Assign(DeliveryPlatform platform, int slot, int itemId)
        {
            if (!CanAssign(platform, slot, itemId)) throw new InvalidOperationException("该物品已在其它交付槽位中设置。");
            string key = platform.Token + ":" + slot;
            DeliveryBinding previous;
            if (_slots.TryGetValue(key, out previous))
            {
                _slots.Remove(key);
                if (previous.ItemId == DeliveryBuildings.Warper) _warpers.Remove(previous);
                else _items.Remove(previous.ItemId);
            }
            if (itemId == 0) return;
            var binding = new DeliveryBinding(platform, slot, itemId);
            _slots.Add(key, binding);
            if (itemId == DeliveryBuildings.Warper)
            {
                _warpers.Add(binding);
                _warpers.Sort((a, b) => {
                    int order = a.Platform.PlanetId.CompareTo(b.Platform.PlanetId);
                    if (order == 0) order = a.Platform.EntityId.CompareTo(b.Platform.EntityId);
                    return order == 0 ? a.Slot.CompareTo(b.Slot) : order;
                });
            }
            else _items.Add(itemId, binding);
        }
        public void ResetBindings() { _items.Clear(); _slots.Clear(); _warpers.Clear(); }
        public void Release(DeliveryPlatform platform)
        {
            foreach (var binding in _slots.Values.Where(candidate => candidate.Platform == platform).ToArray()) Assign(platform, binding.Slot, 0);
        }
        public void Export(BinaryWriter writer)
        {
            // Native station slots are authoritative; the lookup index is rebuilt after loading/F6.
            writer.Write(2); writer.Write(Platforms.Count);
            foreach (var platform in Platforms)
            {
                writer.Write(platform.Token); writer.Write(platform.PlanetId); writer.Write(platform.EntityId); writer.Write(platform.ProtoId);
                writer.Write(platform.X); writer.Write(platform.Y); writer.Write(platform.Z); writer.Write(platform.Orphaned);
                writer.Write(platform.Reserve.Count);
                foreach (var pair in platform.Reserve.OrderBy(pair => pair.Key))
                { writer.Write(pair.Key); WriteNumber(writer, pair.Value.Count); WriteNumber(writer, pair.Value.Inc); }
            }
        }
        public static DeliveryState Import(BinaryReader reader)
        {
            int version = reader.ReadInt32();
            if (version != 2) throw new InvalidDataException("交付库存版本无效，请使用本版新存档。");
            var state = new DeliveryState();
            int count = reader.ReadInt32();
            if (count < 0 || count > 8192) throw new InvalidDataException("交付平台数量无效。");
            var tokens = new HashSet<string>();
            var liveEntities = new HashSet<string>();
            for (int i = 0; i < count; i++)
            {
                var platform = new DeliveryPlatform { Token = ReadToken(reader, false), PlanetId = reader.ReadInt32(), EntityId = reader.ReadInt32(),
                    ProtoId = reader.ReadInt32(), X = reader.ReadSingle(), Y = reader.ReadSingle(), Z = reader.ReadSingle(),
                    Orphaned = ReadFlag(reader) };
                if (!tokens.Add(platform.Token) || platform.PlanetId <= 0 || platform.EntityId <= 0 || !DeliveryBuildings.Contains(platform.ProtoId)
                    || float.IsNaN(platform.X) || float.IsInfinity(platform.X) || float.IsNaN(platform.Y) || float.IsInfinity(platform.Y)
                    || float.IsNaN(platform.Z) || float.IsInfinity(platform.Z))
                    throw new InvalidDataException("交付平台身份无效。");
                if (!platform.Orphaned && !liveEntities.Add(platform.PlanetId + ":" + platform.EntityId)) throw new InvalidDataException("交付平台实体重复。");
                int items = reader.ReadInt32();
                if (items < 0 || items > 256) throw new InvalidDataException("交付物品数量无效。");
                for (int j = 0; j < items; j++)
                {
                    int id = reader.ReadInt32(); var stock = new DeliveryStock { Count = ReadNumber(reader), Inc = ReadNumber(reader) };
                    if (id <= 0 || id >= 12000 || platform.Reserve.ContainsKey(id) || stock.Inc > stock.Count * 10)
                        throw new InvalidDataException("交付库存无效。");
                    platform.Reserve.Add(id, stock);
                }
                state.Platforms.Add(platform);
            }
            return state;
        }
        private static string ReadToken(BinaryReader reader, bool empty)
        {
            // Bounded ASCII token, avoiding BinaryReader.ReadString's unbounded allocation.
            int length = reader.ReadByte();
            if ((empty && length == 0)) return "";
            if (length != 32) throw new InvalidDataException("交付标识无效。");
            var bytes = reader.ReadBytes(length);
            if (bytes.Length != length || bytes.Any(value => !(value >= '0' && value <= '9') && !(value >= 'a' && value <= 'f')))
                throw new InvalidDataException("交付标识无效。");
            return System.Text.Encoding.ASCII.GetString(bytes);
        }
        private static bool ReadFlag(BinaryReader reader) { byte flag = reader.ReadByte(); if (flag > 1) throw new InvalidDataException("交付状态无效。"); return flag == 1; }
        private static void WriteNumber(BinaryWriter writer, BigInteger number)
        { var bytes = number.ToByteArray(); writer.Write(bytes.Length); writer.Write(bytes); }
        private static BigInteger ReadNumber(BinaryReader reader)
        {
            int length = reader.ReadInt32();
            if (length < 1 || length > 4096) throw new InvalidDataException("交付数量编码无效。");
            byte[] bytes = reader.ReadBytes(length);
            if (bytes.Length != length) throw new EndOfStreamException();
            var value = new BigInteger(bytes);
            if (value.Sign < 0) throw new InvalidDataException("交付数量不能为负。");
            return value;
        }
    }

    public sealed class DeliveryBinding
    {
        public readonly DeliveryPlatform Platform;
        public readonly int Slot, ItemId;
        public DeliveryBinding(DeliveryPlatform platform, int slot, int itemId) { Platform = platform; Slot = slot; ItemId = itemId; }
    }

    public interface IDeliveryInventory
    {
        DeliveryStock Read(int itemId);
        int Remove(int itemId, int count, out int inc);
        int Add(int itemId, int count, int inc);
    }

    public static class DeliveryTransfers
    {
        public static int Deposit(MarginMarket market, IDeliveryInventory inventory, int requested)
        {
            if (requested <= 0 || market.State.ItemId == MarketCatalog.BuildingCoin) throw new InvalidOperationException("存入数量或物品无效。");
            var current = inventory.Read(market.State.ItemId);
            if (current.Count < requested) throw new InvalidOperationException("平台可用物品不足。");
            if (current.Inc > 0) throw new InvalidOperationException("请先取出平台内已喷涂的同类物品。");
            checked { var balance = market.State.Balance + requested; var total = market.State.TotalDeposited + requested; if (balance < 0 || total < 0) throw new OverflowException(); }
            int inc; int removed = inventory.Remove(market.State.ItemId, requested, out inc);
            if (removed <= 0) throw new InvalidOperationException("平台库存已变化，请重新存入。");
            market.Deposit(removed); return removed;
        }
        public static int Withdraw(MarginMarket market, IDeliveryInventory inventory, int requested)
        {
            if (requested <= 0 || market.State.ItemId == MarketCatalog.BuildingCoin || market.State.Balance < requested)
                throw new InvalidOperationException("可用余额或提取数量无效。");
            checked { var total = market.State.TotalWithdrawn + requested; if (total < 0) throw new OverflowException(); }
            int inserted = inventory.Add(market.State.ItemId, requested, 0);
            if (inserted <= 0) throw new InvalidOperationException("平台未设置该物品槽。");
            market.Withdraw(inserted); return inserted;
        }
    }
}
