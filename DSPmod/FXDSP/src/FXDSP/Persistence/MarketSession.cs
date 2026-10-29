using System;
using System.IO;
using System.Collections.Generic;
using FXDSP.Core;

namespace FXDSP.Persistence
{
    /// <summary>
    /// Binds one market account to a loaded game and a matching DSPModSave snapshot.
    /// Call all methods from the game's main thread. No live balance is written to JSON.
    /// </summary>
    internal sealed class MarketSession
    {
        private const int SnapshotMagic = 0x46584453;
        private const int SnapshotVersion = 3;
        private const int MaxSnapshotBytes = 4 * 1024 * 1024;
        private const string CacheKey = "FXDSP.MarketSession.Handoff.v1";
        private const string OwnerKey = "FXDSP.MarketSession.Owner.v1";
        private const long CacheIntervalTicks = 30;

        private enum SessionMode { Uninitialized, Ready, Locked }

        private readonly string _ownerToken;
        private MarketBook _book;
        internal TradingAchievementState Achievements { get; private set; } = new TradingAchievementState();
        private AchievementTracker _achievementTracker;
        internal DeliveryState Delivery { get; private set; } = new DeliveryState();
        private SessionMode _mode;
        private GameData _boundData;
        private long _creationTicks;
        private int _galaxySeed;
        private long _lastObservedTick = -1;
        private long _lastCacheTick = -1;
        private string _status;
        private byte[] _originalPayload;
        private bool _pendingFreshAccount;
        private Snapshot _pendingSnapshot;

        internal MarketSession()
        {
            object[] handoff = AppDomain.CurrentDomain.GetData(CacheKey) as object[];
            _ownerToken = Guid.NewGuid().ToString("N");
            // Claim ownership before the old plugin's deferred OnDestroy can run.
            AppDomain.CurrentDomain.SetData(OwnerKey, _ownerToken);
            _book = new MarketBook();
            _status = "账户尚未载入，请返回主菜单重新读取存档。";
            RestoreHandoffOnce(handoff);
            _achievementTracker = new AchievementTracker(_book, Achievements);
        }

        internal MarginMarket Market { get { return _book.Get(MarketCatalog.Copper); } }
        internal IReadOnlyList<MarginMarket> Markets { get { return _book.Markets; } }
        internal MarginMarket GetMarket(int itemId) { return _book.Get(itemId); }
        internal MarginMarket FindMarket(int itemId) { return _book.Find(itemId); }
        internal bool IsMarketEnabled(int itemId) { return _book.IsEnabled(itemId); }
        internal void SetTradingFeeRate(decimal rate)
        {
            if (CanAccessDelivery && _book.SetTradingFeeRate(rate)) PreserveForReload();
        }
        internal void SetResourceEnabled(int itemId, bool enabled)
        {
            if (!CanTrade) throw new InvalidOperationException("当前无法更改资源选择。");
            _book.SetResourceEnabled(itemId, enabled, GameMain.gameTick);
            _achievementTracker.AttachMarkets();
            PreserveForReload();
        }
        internal bool ClosePosition(int itemId) { return _book.CloseMarket(itemId); }
        internal bool AcknowledgeIntroduction()
        {
            if (!CanAccessDelivery || !_book.AcknowledgeIntroduction()) return false;
            PreserveForReload(); return true;
        }
        internal int CloseAllPositions() { return _book.CloseAll(); }

        internal bool CanTrade
        {
            get
            {
                return CanAccessDelivery && !GameMain.isPaused;
            }
        }
        internal bool CanAccessDelivery
        { get { return OwnsHandoff && _mode == SessionMode.Ready && IsBoundToCurrentGame() && GameMain.isRunning; } }

        /// <summary>
        /// The plugin must block GameSave.SaveCurrentGame when this is false.
        /// DSPModSave overwrites the sidecar before calling Export, so throwing from
        /// Export alone would not protect an unknown account from being overwritten.
        /// </summary>
        internal bool CanSave
        {
            get
            {
                if (!OwnsHandoff || !IsBoundToCurrentGame()) return false;
                return _mode == SessionMode.Ready ||
                    (_mode == SessionMode.Locked && _originalPayload != null);
            }
        }

        internal string Status { get { return _status; } }

        private bool OwnsHandoff
        {
            get { return string.Equals(AppDomain.CurrentDomain.GetData(OwnerKey) as string,
                _ownerToken, StringComparison.Ordinal); }
        }

        /// <summary>Owns market advancement; Plugin must not advance it a second time.</summary>
        internal void Tick()
        {
            if (!OwnsHandoff) return;
            GameData data;
            long creationTicks;
            int seed;
            if (!TryGetCurrentGame(out data, out creationTicks, out seed))
            {
                if (_mode == SessionMode.Ready)
                    _status = "等待进入游戏；账户仅属于刚才载入的存档。";
                // Do not keep an entire unloaded factory alive through the
                // AppDomain handoff cache while the player is at the main menu.
                if (_boundData != null)
                {
                    _boundData = null;
                    AppDomain.CurrentDomain.SetData(CacheKey, null);
                }
                return;
            }

            if (_pendingFreshAccount)
            {
                _book = new MarketBook();
                Delivery = new DeliveryState();
                Achievements = new TradingAchievementState();
                _achievementTracker = new AchievementTracker(_book, Achievements);
                Bind(data, creationTicks, seed);
                _pendingFreshAccount = false;
                _originalPayload = null;
                _mode = SessionMode.Ready;
                _status = "";
            }
            else if (_pendingSnapshot != null)
            {
                ValidateAndApplyPendingSnapshot(data, creationTicks, seed);
            }

            if (_mode == SessionMode.Ready && !IsBoundToCurrentGame())
            {
                LockForCurrentGame("游戏会话或时间发生变化，请重新读取对应存档。");
            }

            if (_mode != SessionMode.Ready) return;
            if (GameMain.gameTick < _lastObservedTick)
            {
                LockForCurrentGame("检测到游戏时间回退，已停止交易；请重新读取存档。");
                return;
            }

            _lastObservedTick = GameMain.gameTick;
            if (!GameMain.isRunning || GameMain.isPaused)
            {
                _status = "游戏已暂停，行情与交易暂停。";
                return;
            }

            bool quoteChanged = false;
            _achievementTracker.Initialize();
            foreach (var market in _book.RunningMarkets)
            {
                long before = market.History.Count == 0 ? -1 : market.History[market.History.Count - 1].GameTick;
                market.AdvanceTo(GameMain.gameTick);
                long after = market.History.Count == 0 ? -1 : market.History[market.History.Count - 1].GameTick;
                quoteChanged |= before != after;
            }
            _status = "";
            if (quoteChanged || _lastCacheTick < 0 ||
                GameMain.gameTick - _lastCacheTick >= CacheIntervalTicks)
                PreserveForReload();
        }

        internal void Export(BinaryWriter writer)
        {
            if (writer == null) throw new ArgumentNullException("writer");
            Tick();
            if (!CanSave)
                throw new InvalidOperationException("FXDSP account is not initialized; reload the save before saving.");

            if (_mode == SessionMode.Locked)
            {
                // Keep the exact bad/mismatched block as recovery evidence. Never
                // replace it with an empty account merely because validation failed.
                writer.Write(_originalPayload);
                return;
            }

            byte[] payload = SerializeSnapshot(GameMain.gameTick);
            writer.Write(payload);
            _originalPayload = payload;
            PreserveForReload();
        }

        internal void Import(BinaryReader reader)
        {
            if (!OwnsHandoff) return;
            _pendingFreshAccount = false;
            _pendingSnapshot = null;
            _originalPayload = null;
            try
            {
                if (reader == null) throw new InvalidDataException("读取器为空。");
                byte[] payload = ReadRemainingBounded(reader);
                _originalPayload = payload;
                Snapshot parsed = ParseSnapshot(payload);
                _pendingSnapshot = parsed;
                _mode = SessionMode.Uninitialized;
                _status = "交易账户已读取，正在核对游戏快照。";

                // DSPModSave's postload callback runs before the next Update. Verify
                // its exact tick here rather than comparing after simulation advances.
                GameData data;
                long creationTicks;
                int seed;
                if (TryGetCurrentGame(out data, out creationTicks, out seed))
                    ValidateAndApplyPendingSnapshot(data, creationTicks, seed);
            }
            catch (Exception exception)
            {
                LockForCurrentGame("交易存档无法验证，已锁定交易：" + exception.Message);
            }
            PreserveForReload();
        }

        /// <summary>Only call from DSPModSave's explicit new/missing-mod-data callback.</summary>
        internal void IntoOtherSave()
        {
            if (!OwnsHandoff) return;
            _book = new MarketBook();
            Delivery = new DeliveryState();
            Achievements = new TradingAchievementState();
            _achievementTracker = new AchievementTracker(_book, Achievements);
            _pendingSnapshot = null;
            _originalPayload = null;
            _pendingFreshAccount = true;
            _boundData = null;
            _mode = SessionMode.Uninitialized;
            _lastObservedTick = -1;
            _lastCacheTick = -1;
            _status = "该存档没有 FXDSP 数据，等待建立零余额账户。";
            Tick();
            PreserveForReload();
        }

        /// <summary>Also call immediately after every successful account mutation.</summary>
        internal void PreserveForReload()
        {
            if (!OwnsHandoff) return;
            GameData data;
            long creationTicks;
            int seed;
            if (!TryGetCurrentGame(out data, out creationTicks, out seed)) return;

            if (_mode == SessionMode.Ready && !IsBoundToCurrentGame()) return;
            byte[] marketBytes;
            using (MemoryStream stream = new MemoryStream())
            {
                using (BinaryWriter writer = new BinaryWriter(stream))
                {
                    _book.Export(writer);
                    Delivery.Export(writer);
                    Achievements.Export(writer);
                    marketBytes = stream.ToArray();
                }
            }

            if (_mode == SessionMode.Ready)
                _originalPayload = SerializeSnapshot(GameMain.gameTick);

            // Only framework types cross reload boundaries. Never cache MarketBook,
            // MarketSession, a delegate, or another type owned by the old assembly.
            AppDomain.CurrentDomain.SetData(CacheKey, new object[]
            {
                data, creationTicks, seed, marketBytes, (int)_mode, _status,
                _originalPayload, GameMain.gameTick, _pendingFreshAccount
            });
            _lastCacheTick = GameMain.gameTick;
        }

        private void ValidateAndApplyPendingSnapshot(GameData data, long creationTicks, int seed)
        {
            Snapshot snapshot = _pendingSnapshot;
            _pendingSnapshot = null;
            if (snapshot.CreationTicks != creationTicks || snapshot.GalaxySeed != seed)
            {
                LockForCurrentGame("交易旁档属于其它世界，已停止交易；请使用成对的游戏存档和旁档。");
                return;
            }
            if (snapshot.SavedTick != GameMain.gameTick)
            {
                LockForCurrentGame("游戏与交易旁档的保存时间不一致，已停止交易；请恢复匹配的存档。");
                return;
            }

            _book = snapshot.Book;
            // Only a validated disk load starts a new future. Raw deserialization
            // and same-session F6 handoff must retain the exact random stream.
            _book.ReseedRandomStreams();
            Delivery = snapshot.Delivery;
            Achievements = snapshot.Achievements;
            _achievementTracker = new AchievementTracker(_book, Achievements);
            Bind(data, creationTicks, seed);
            _mode = SessionMode.Ready;
            _status = "交易账户与游戏快照核对通过。";
            PreserveForReload();
        }

        private void Bind(GameData data, long creationTicks, int seed)
        {
            _boundData = data;
            _creationTicks = creationTicks;
            _galaxySeed = seed;
            _lastObservedTick = GameMain.gameTick;
            _lastCacheTick = -1;
        }

        private void LockForCurrentGame(string reason)
        {
            _pendingSnapshot = null;
            _pendingFreshAccount = false;
            _mode = SessionMode.Locked;
            _status = reason;
            GameData data;
            long creationTicks;
            int seed;
            if (TryGetCurrentGame(out data, out creationTicks, out seed))
                Bind(data, creationTicks, seed);
            PreserveForReload();
        }

        private bool IsBoundToCurrentGame()
        {
            GameData data;
            long creationTicks;
            int seed;
            return TryGetCurrentGame(out data, out creationTicks, out seed) &&
                ReferenceEquals(_boundData, data) && creationTicks == _creationTicks &&
                seed == _galaxySeed && GameMain.gameTick >= _lastObservedTick;
        }

        private static bool TryGetCurrentGame(out GameData data, out long creationTicks, out int seed)
        {
            data = GameMain.data;
            creationTicks = 0;
            seed = 0;
            if (data == null || data.gameDesc == null || GameMain.instance == null ||
                GameMain.instance.isMenuDemo) return false;
            creationTicks = data.gameDesc.creationTime.Ticks;
            seed = data.gameDesc.galaxySeed;
            return creationTicks > 0 && GameMain.gameTick >= 0;
        }

        private byte[] SerializeSnapshot(long gameTick)
        {
            using (MemoryStream stream = new MemoryStream())
            {
                using (BinaryWriter writer = new BinaryWriter(stream))
                {
                    writer.Write(SnapshotMagic);
                    writer.Write(SnapshotVersion);
                    writer.Write(_creationTicks);
                    writer.Write(_galaxySeed);
                    writer.Write(gameTick);
                    _book.Export(writer);
                    Delivery.Export(writer);
                    Achievements.Export(writer);
                    return stream.ToArray();
                }
            }
        }

        private static Snapshot ParseSnapshot(byte[] payload)
        {
            using (MemoryStream stream = new MemoryStream(payload, false))
            using (BinaryReader reader = new BinaryReader(stream))
            {
                if (reader.ReadInt32() != SnapshotMagic)
                    throw new InvalidDataException("交易存档标识或版本不受支持。");
                int version = reader.ReadInt32();
                if (version < 1 || version > SnapshotVersion) throw new InvalidDataException("交易存档版本不受支持。");
                Snapshot snapshot = new Snapshot();
                snapshot.CreationTicks = reader.ReadInt64();
                snapshot.GalaxySeed = reader.ReadInt32();
                snapshot.SavedTick = reader.ReadInt64();
                if (snapshot.CreationTicks <= 0 || snapshot.CreationTicks > DateTime.MaxValue.Ticks || snapshot.SavedTick < 0)
                    throw new InvalidDataException("世界标识或保存时间无效。");
                snapshot.Book = MarketBook.Import(reader);
                snapshot.Delivery = version >= 2 ? DeliveryState.Import(reader) : new DeliveryState();
                snapshot.Achievements = version >= 3 ? TradingAchievementState.Import(reader) : new TradingAchievementState();
                foreach (var market in snapshot.Book.Markets)
                    if (market.State.HasTimeAnchor && market.State.LastGameTick > snapshot.SavedTick)
                        throw new InvalidDataException("交易账户与保存快照不一致。");
                if (stream.Position != stream.Length) throw new InvalidDataException("交易账户包含多余数据。");
                return snapshot;
            }
        }

        private static byte[] ReadRemainingBounded(BinaryReader reader)
        {
            using (MemoryStream stream = new MemoryStream())
            {
                byte[] buffer = new byte[4096];
                int count;
                while ((count = reader.Read(buffer, 0, buffer.Length)) != 0)
                {
                    if (stream.Length + count > MaxSnapshotBytes)
                        throw new InvalidDataException("交易数据超出初版允许的大小；停止保存以保护原数据。");
                    stream.Write(buffer, 0, count);
                }
                return stream.ToArray();
            }
        }

        private void RestoreHandoffOnce(object[] handoff)
        {
            if (handoff == null || handoff.Length != 9) return;
            GameData data;
            long creationTicks;
            int seed;
            if (!TryGetCurrentGame(out data, out creationTicks, out seed) ||
                !ReferenceEquals(handoff[0], data)) return;
            try
            {
                if ((long)handoff[1] != creationTicks || (int)handoff[2] != seed ||
                    GameMain.gameTick < (long)handoff[7])
                    throw new InvalidDataException("热重载快照与当前世界或时间不一致。");
                MarketBook restored;
                DeliveryState delivery;
                TradingAchievementState achievements;
                using (MemoryStream stream = new MemoryStream((byte[])handoff[3], false))
                using (BinaryReader reader = new BinaryReader(stream))
                {
                    restored = MarketBook.Import(reader);
                    delivery = stream.Position < stream.Length ? DeliveryState.Import(reader) : new DeliveryState();
                    achievements = stream.Position < stream.Length ? TradingAchievementState.Import(reader) : new TradingAchievementState();
                    if (stream.Position != stream.Length)
                        throw new InvalidDataException("热重载账户数据无效。");
                }
                SessionMode mode = (SessionMode)(int)handoff[4];
                if (mode < SessionMode.Uninitialized || mode > SessionMode.Locked)
                    throw new InvalidDataException("热重载会话状态无效。");
                _book = restored;
                Delivery = delivery;
                Achievements = achievements;
                Bind(data, creationTicks, seed);
                _mode = mode;
                _status = handoff[5] as string ?? "账户状态已恢复。";
                _originalPayload = handoff[6] as byte[];
                _pendingFreshAccount = (bool)handoff[8];
            }
            catch (Exception exception)
            {
                _originalPayload = handoff[6] as byte[];
                LockForCurrentGame("热重载状态无法恢复，已锁定交易：" + exception.Message);
            }
        }

        private sealed class Snapshot
        {
            internal long CreationTicks;
            internal int GalaxySeed;
            internal long SavedTick;
            internal MarketBook Book;
            internal DeliveryState Delivery;
            internal TradingAchievementState Achievements;
        }
    }
}
