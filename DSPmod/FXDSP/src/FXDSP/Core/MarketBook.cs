using System;
using System.Collections.Generic;
using System.IO;

namespace FXDSP.Core
{
    public enum MarketCategory { RawMaterials, Materials, Weapons, Consumables, Matrices, BuildingCurrency }

    public static class MarketCatalog
    {
        public const int Copper = 1002, Plastic = 1115, Matrix = 6001;
        public const int BuildingCoin = 99000001; // Internal wallet only; never a game item prototype.
        private static readonly int[] Items = { Copper, Plastic, 6001, 6002, 6003, 6004, 6005, 6006, BuildingCoin };
        public static IReadOnlyList<int> ItemIds { get; } = Array.AsReadOnly(Items);
        private static readonly Dictionary<int, string> ResourceNames = new Dictionary<int, string>();
        private static readonly Dictionary<int, MarketCategory> ResourceCategories = new Dictionary<int, MarketCategory>();
        public static bool IsDarkFogDrop(int id) { return id >= 5201 && id <= 5206; }
        public static bool IsResource(int id) { return (id >= 1000 && id < 2000) || IsDarkFogDrop(id); }
        public static bool IsSelectable(int id) { return IsResource(id) || (id >= 6001 && id <= 6006); }
        public static bool IsSelectableCategory(MarketCategory category)
        { return IsResourceCategory(category) || category == MarketCategory.Matrices; }
        public static MarketCategory ClassifyResource(int id, bool rawMaterial, bool weapon, bool consumable)
        {
            if (id == 1099) return MarketCategory.Consumables;
            if (IsDarkFogDrop(id)) return MarketCategory.Materials;
            if (id == 1000 || id == 1116 || id == 1117 || id == 1120 || id == 1121) return MarketCategory.RawMaterials;
            if (id >= 1141 && id <= 1143) return MarketCategory.Consumables;
            return ClassifyResource(rawMaterial, weapon, consumable);
        }
        public static bool Contains(int id) { return IsSelectable(id) || id == BuildingCoin; }
        public static bool IsResourceCategory(MarketCategory category)
        { return category >= MarketCategory.RawMaterials && category <= MarketCategory.Consumables; }
        public static MarketCategory ClassifyResource(bool rawMaterial, bool weapon, bool consumable)
        {
            if (weapon) return MarketCategory.Weapons;
            if (rawMaterial) return MarketCategory.RawMaterials;
            return consumable ? MarketCategory.Consumables : MarketCategory.Materials;
        }
        public static void RegisterResourceName(int id, string name, MarketCategory category = MarketCategory.Materials)
        {
            if (!IsResource(id) || !IsResourceCategory(category)) throw new ArgumentException("资源分类无效。");
            if (!string.IsNullOrEmpty(name)) ResourceNames[id] = name;
            ResourceCategories[id] = category;
        }
        public static MarketCategory Category(int id)
        {
            if (id == BuildingCoin) return MarketCategory.BuildingCurrency;
            if (id >= 6001 && id <= 6006) return MarketCategory.Matrices;
            if (IsDarkFogDrop(id)) return MarketCategory.Materials;
            if (id == 1099) return MarketCategory.Consumables;
            if (id == 1000 || id == 1116 || id == 1117 || id == 1120 || id == 1121) return MarketCategory.RawMaterials;
            if (id >= 1141 && id <= 1143) return MarketCategory.Consumables;
            MarketCategory category;
            if (ResourceCategories.TryGetValue(id, out category)) return category;
            if (id >= 1000 && id < 1100) return MarketCategory.RawMaterials;
            if (id >= 1600 && id < 1700) return MarketCategory.Weapons;
            if ((id >= 1800 && id < 1900) || (id >= 1141 && id <= 1143) || id == 1210 || id == 1501 || id == 1503)
                return MarketCategory.Consumables;
            return MarketCategory.Materials;
        }
        public static string CategoryName(MarketCategory category)
        {
            switch (category)
            {
                case MarketCategory.RawMaterials: return "原料";
                case MarketCategory.Materials: return "材料";
                case MarketCategory.Weapons: return "武器";
                case MarketCategory.Consumables: return "消耗品";
                case MarketCategory.Matrices: return "矩阵";
                default: return "建筑币";
            }
        }
        public static string Name(int id)
        {
            string registeredName;
            if (ResourceNames.TryGetValue(id, out registeredName)) return registeredName;
            switch (id)
            {
                case Copper: return "铜矿";
                case Plastic: return "塑料";
                case Matrix: return "电磁矩阵";
                case 6002: return "能量矩阵";
                case 6003: return "结构矩阵";
                case 6004: return "信息矩阵";
                case 6005: return "引力矩阵";
                case 6006: return "宇宙矩阵";
                case 5201: return "黑雾矩阵";
                case 5202: return "硅基神经元";
                case 5203: return "物质重组器";
                case 5204: return "负熵奇点";
                case 5205: return "核心素";
                case 5206: return "能量碎片";
                case 1099: return "沙土";
                case BuildingCoin: return "建筑币";
                default: string name; return ResourceNames.TryGetValue(id, out name) ? name : "物品 " + id;
            }
        }
    }

    public sealed class MarketBook
    {
        private const int Magic = 0x4658424B, Version = 4;
        public bool IntroductionSeen { get; private set; }
        public bool AcknowledgeIntroduction()
        { if (IntroductionSeen) return false; IntroductionSeen = true; return true; }
        private const int MaxMarkets = 256;
        private readonly List<MarginMarket> _markets = new List<MarginMarket>();
        private readonly List<MarginMarket> _running = new List<MarginMarket>();
        private readonly Dictionary<int, MarginMarket> _byId = new Dictionary<int, MarginMarket>();
        private readonly HashSet<int> _enabled = new HashSet<int>();
        private readonly Dictionary<int, byte[]> _inactiveBytes = new Dictionary<int, byte[]>();
        private decimal _feeRate = TradeResearch.TradingFee(0);
        public IReadOnlyList<MarginMarket> Markets { get; }
        public IReadOnlyList<MarginMarket> RunningMarkets { get; }

        public MarketBook(bool enableAllMarkets = false)
        {
            foreach (int id in MarketCatalog.ItemIds) Add(Create(id), enableAllMarkets || id == MarketCatalog.BuildingCoin);
            Markets = _markets.AsReadOnly();
            RunningMarkets = _running.AsReadOnly();
        }

        private MarginMarket Create(int id)
        {
            return new MarginMarket(id, feeRate: _feeRate, seed: MarketRandom.NewSeed());
        }

        internal void ReseedRandomStreams()
        {
            foreach (var market in _markets)
            {
                market.ReseedRandomStream();
                // Dormant accounts export cached bytes; refresh them as well so
                // the new seed survives saving and F6 without advancing prices.
                if (!IsEnabled(market.State.ItemId)) CacheInactive(market);
            }
        }
        public bool SetTradingFeeRate(decimal rate)
        {
            if (rate < 0m || rate >= 1m) throw new InvalidOperationException("手续费率无效。");
            bool changed = _feeRate != rate; _feeRate = rate;
            foreach (var market in _markets)
                if (market.SetFeeRate(rate))
                { changed = true; if (!IsEnabled(market.State.ItemId)) CacheInactive(market); }
            return changed;
        }

        private void Add(MarginMarket market, bool enabled)
        {
            int id = market.State.ItemId;
            _markets.Add(market); _byId.Add(id, market);
            if (enabled) { _enabled.Add(id); _running.Add(market); }
            else CacheInactive(market);
        }

        public MarginMarket Get(int id)
        {
            MarginMarket market;
            if (_byId.TryGetValue(id, out market)) return market;
            throw new InvalidOperationException("请先选择并启用该市场。");
        }
        public MarginMarket Find(int id) { MarginMarket market; return _byId.TryGetValue(id, out market) ? market : null; }
        public bool IsEnabled(int id) { return _enabled.Contains(id); }

        public bool SetResourceEnabled(int id, bool enabled, long gameTick)
        {
            if (!MarketCatalog.IsSelectable(id) || gameTick < 0) throw new InvalidOperationException("市场选择无效。");
            if (IsEnabled(id) == enabled) return false;
            var market = Find(id);
            if (enabled)
            {
                if (market == null)
                {
                    if (_markets.Count >= MaxMarkets) throw new InvalidOperationException("已达到可保留资源数量上限。");
                    market = Create(id); market.ResumeAt(gameTick); Add(market, true);
                }
                else
                {
                    market.ResumeAt(gameTick);
                    _inactiveBytes.Remove(id); _enabled.Add(id); _running.Add(market);
                }
            }
            else
            {
                if (market.Position != null) throw new InvalidOperationException("请先平仓，再停用该品种。");
                market.AdvanceTo(gameTick);
                CacheInactive(market);
                _enabled.Remove(id); _running.Remove(market);
            }
            return true;
        }

        private void CacheInactive(MarginMarket market)
        {
            using (var stream = new MemoryStream()) using (var writer = new BinaryWriter(stream))
            { market.Export(writer); _inactiveBytes[market.State.ItemId] = stream.ToArray(); }
        }

        public void Export(BinaryWriter writer)
        {
            writer.Write(Magic); writer.Write(Version); writer.Write(_markets.Count);
            foreach (var market in _markets)
            {
                byte[] cached;
                if (_inactiveBytes.TryGetValue(market.State.ItemId, out cached)) writer.Write(cached);
                else market.Export(writer);
                writer.Write(IsEnabled(market.State.ItemId));
            }
            writer.Write(IntroductionSeen);
        }

        public bool CloseMarket(int itemId)
        {
            var market = Get(itemId);
            if (market.Position == null) return false;
            market.Close(); return true;
        }

        public int CloseAll()
        {
            foreach (var market in _running)
                if (market.Position != null) { decimal fee; market.QuoteSettlement(false, out fee); }
            int closed = 0;
            foreach (var market in _running) if (CloseMarket(market.State.ItemId)) closed++;
            return closed;
        }

        public static MarketBook Import(BinaryReader reader)
        {
            if (reader == null || !reader.BaseStream.CanSeek) throw new InvalidDataException("账户读取器无效。");
            try
            {
                long start = reader.BaseStream.Position;
                int magic = reader.ReadInt32();
                var book = new MarketBook(true);
                if (magic == 0x46584D47 || magic == 0x4658534D)
                {
                    reader.BaseStream.Position = start;
                    var legacy = MarginMarket.Import(reader);
                    if (legacy.State.ItemId != MarketCatalog.Copper) throw new InvalidDataException("旧账户物品无效。");
                    book.ReplaceLegacy(legacy); return book;
                }
                if (magic != Magic) throw new InvalidDataException("市场账户标识无效。");
                int version = reader.ReadInt32(), count = reader.ReadInt32();
                if (!((version == 1 && count == 3) || (version == 2 && count == MarketCatalog.ItemIds.Count)
                    || ((version == 3 || version == Version) && count >= MarketCatalog.ItemIds.Count && count <= MaxMarkets)))
                    throw new InvalidDataException("市场账户版本或数量无效。");
                if (version >= 3) { book._markets.Clear(); book._running.Clear(); book._enabled.Clear(); book._byId.Clear(); book._inactiveBytes.Clear(); }
                var seen = new HashSet<int>();
                for (int i = 0; i < count; i++)
                {
                    var market = MarginMarket.Import(reader);
                    int id = market.State.ItemId;
                    bool baseItem = false;
                    foreach (int baseId in MarketCatalog.ItemIds) if (baseId == id) { baseItem = true; break; }
                    if (!MarketCatalog.Contains(id) || !seen.Add(id) || (version < 3 && !baseItem)
                        || (version == 1 && id != MarketCatalog.Copper && id != MarketCatalog.Plastic && id != MarketCatalog.Matrix))
                        throw new InvalidDataException("市场物品重复或未知。");
                    bool enabled = true;
                    if (version >= 3)
                    {
                        byte flag = reader.ReadByte();
                        if (flag > 1) throw new InvalidDataException("资源启用状态无效。");
                        enabled = flag == 1;
                        if (!enabled && (!MarketCatalog.IsSelectable(id) || market.Position != null))
                            throw new InvalidDataException("建筑币或持仓账户不能停用。");
                        book.Add(market, enabled);
                    }
                    else book.ReplaceLegacy(market);
                }
                if (version >= 3)
                    foreach (int id in MarketCatalog.ItemIds) if (!seen.Contains(id)) throw new InvalidDataException("基础账户缺失。");
                if (version >= 4)
                {
                    byte seenFlag = reader.ReadByte();
                    if (seenFlag > 1) throw new InvalidDataException("交易介绍状态无效。");
                    book.IntroductionSeen = seenFlag == 1;
                }
                book._feeRate = book.Get(MarketCatalog.BuildingCoin).FeeRate;
                return book;
            }
            catch (InvalidDataException) { throw; }
            catch (Exception error) when (error is EndOfStreamException || error is ArgumentException || error is OverflowException)
            { throw new InvalidDataException("市场账户不完整。", error); }
        }

        private void ReplaceLegacy(MarginMarket market)
        {
            int id = market.State.ItemId;
            int index = _markets.IndexOf(_byId[id]);
            _markets[index] = market;
            _running[_running.IndexOf(_byId[id])] = market;
            _byId[id] = market;
        }
    }
}
