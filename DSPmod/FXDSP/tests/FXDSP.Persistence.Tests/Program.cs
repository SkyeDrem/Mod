using System;
using System.IO;
using FXDSP.Core;
using FXDSP.Persistence;

internal static class Program
{
    private const string CacheKey = "FXDSP.MarketSession.Handoff.v1";
    private const string OwnerKey = "FXDSP.MarketSession.Owner.v1";
    private static int _passed;

    private static int Main()
    {
        try
        {
            Run("mid-game first activation cannot silently initialize or save", UnknownAccount);
            Run("explicit new/missing-data callback creates zero account", ZeroAccount);
            Run("matching world/tick restores every byte of market state", MatchingSnapshot);
            Run("wrong world locks and preserves original payload and prior assets", WrongWorld);
            Run("wrong save tick locks and preserves original payload", WrongTick);
            Run("corrupt and trailing records lock without clearing prior assets", CorruptSnapshot);
            Run("oversized input blocks saving instead of replacing old data", OversizedSnapshot);
            Run("explicit switch to new save clears old account", NewSaveClearsOldState);
            Run("unexpected GameData replacement cannot retain trading authority", UnexpectedDataReplacement);
            Run("F6 same-session handoff preserves assets/history/random continuation", ReloadContinuity);
            Run("old deferred cleanup cannot overwrite the new owner", OldOwnerCannotOverwrite);
            Run("handoff requires exact GameData identity and monotonic time", HandoffIdentityAndTime);
            Run("pause and menu demo disable trading and quote advancement", PauseAndMenu);
            Run("postload payload can wait for GameData then validate", DelayedGameBinding);
            Run("same-session time rollback locks and keeps prior snapshot", TimeRollback);
            Run("quote boundary between periodic caches survives immediate F6", QuoteBoundaryHandoff);
            Run("leaving game releases the cached GameData reference", MenuReleasesData);
            Run("v0.2 sidecar and live handoff migrate to copper margin once", LegacySnapshotAndHandoff);
            Run("three market snapshots preserve balances positions and random streams", ThreeMarketSnapshot);
            Run("F6 preserves all three markets and switching saves clears each account", ThreeMarketHandoff);
            Run("v0.4 active copper handoff adds empty plastic and matrix accounts", MarginLegacyHandoff);
            Run("resource selection and frozen assets survive live F6 and disk reload", ResourceSelectionPersistence);
            Run("disabled matrices survive F6 and disk reload with their exact assets and price history", MatrixSelectionPersistence);
            Run("paused selection is rejected and a new save resets optional resource accounts", ResourceSelectionPauseAndReset);
            Run("delivery platform identity and unbounded reserve survive F6 and disk snapshots", DeliveryPersistence);
            Run("paused physical storage remains available while trade mutations are disabled", PausedDeliveryAccess);
            Run("introduction is acknowledged once per save and persists through F6 and disk reload", IntroductionPersistence);
            Run("achievements and incomplete counters survive F6 and disk; new saves reset", AchievementPersistence);
            Run("version two snapshot opens with empty achievements and retains funds", PreviousSnapshotAchievements);
            Console.WriteLine("Persistence checks passed: " + _passed);
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 1;
        }
    }

    private static void Run(string name, Action action)
    {
        Reset();
        action();
        _passed++;
        Console.WriteLine("PASS " + name);
    }

    private static void Reset()
    {
        AppDomain.CurrentDomain.SetData(CacheKey, null);
        AppDomain.CurrentDomain.SetData(OwnerKey, null);
        GameMain.instance = new GameMain();
        GameMain.isRunning = true;
        GameMain.isPaused = false;
        SetWorld(71, 100);
    }

    private static void SetWorld(int seed, long tick)
    {
        GameMain.data = new GameData
        {
            gameDesc = new GameDesc
            {
                creationTime = new DateTime(2026, 10, 25, 0, 0, 0, DateTimeKind.Utc).AddMinutes(seed),
                galaxySeed = seed
            }
        };
        GameMain.gameTick = tick;
    }

    private static MarketSession Fresh()
    {
        MarketSession session = new MarketSession();
        session.IntoOtherSave();
        session.Tick();
        foreach (int id in MarketCatalog.ItemIds) if (MarketCatalog.IsSelectable(id)) session.SetResourceEnabled(id, true);
        Require(session.CanTrade && session.CanSave, "Explicit initialization should enable account.");
        return session;
    }

    private static void UnknownAccount()
    {
        MarketSession session = new MarketSession();
        session.Tick();
        Require(!session.CanTrade && !session.CanSave, "Unknown account must remain locked.");
        bool rejected = false;
        try { Save(session); } catch (InvalidOperationException) { rejected = true; }
        Require(rejected, "Unknown account export must be rejected before sidecar writing.");
    }

    private static void ZeroAccount()
    {
        MarketSession session = Fresh();
        Require(session.Market.State.Balance == 0 && session.Market.UsedMargin == 0m,
            "A fresh account cannot receive free assets.");
    }

    private static void MatchingSnapshot()
    {
        MarketSession first = Fresh();
        first.Market.Deposit(100);
        first.Market.Open(PositionSide.Long, 10m, 5);
        first.PreserveForReload();
        byte[] snapshot = Save(first);
        byte[] market = MarketBytes(first.Market);
        ClearHandoff();
        MarketSession second = new MarketSession();
        Load(second, snapshot);
        Require(second.CanTrade && second.CanSave, "Matching snapshot must restore authority.");
        Equal(market, MarketBytes(second.Market), "Market round trip lost account/history/random state.");
    }

    private static void WrongWorld()
    {
        MarketSession source = Fresh();
        source.Market.Deposit(100);
        byte[] snapshot = Save(source);
        SetWorld(72, 100);
        MarketSession target = Fresh();
        target.Market.Deposit(7);
        Load(target, snapshot);
        Require(!target.CanTrade && target.CanSave, "Wrong-world payload must be quarantined, not cleared.");
        Require(target.Market.State.Balance == 7, "Invalid import replaced previously visible assets.");
        Equal(snapshot, Save(target), "Wrong-world payload was rewritten.");
    }

    private static void WrongTick()
    {
        byte[] snapshot = Save(Fresh());
        GameMain.gameTick++;
        MarketSession target = new MarketSession();
        Load(target, snapshot);
        Require(!target.CanTrade && target.CanSave, "Mismatched save tick was accepted.");
        Equal(snapshot, Save(target), "Wrong-tick payload was not retained byte-for-byte.");
    }

    private static void CorruptSnapshot()
    {
        MarketSession target = Fresh();
        target.Market.Deposit(7);
        byte[] malformed = new byte[] { 1, 2, 3 };
        Load(target, malformed);
        Require(!target.CanTrade && target.CanSave && target.Market.State.Balance == 7,
            "Corruption must lock account and leave existing assets untouched.");
        Equal(malformed, Save(target), "Corrupt payload was silently converted to a zero account.");

        Reset();
        byte[] valid = Save(Fresh());
        byte[] trailing = new byte[valid.Length + 1];
        Buffer.BlockCopy(valid, 0, trailing, 0, valid.Length);
        MarketSession next = new MarketSession();
        Load(next, trailing);
        Require(!next.CanTrade, "Unexpected trailing bytes should not be ignored.");
        Equal(trailing, Save(next), "Trailing payload recovery evidence was lost.");
    }

    private static void OversizedSnapshot()
    {
        MarketSession session = Fresh();
        session.Market.Deposit(8);
        Load(session, new byte[4 * 1024 * 1024 + 1]);
        Require(!session.CanTrade && !session.CanSave && session.Market.State.Balance == 8,
            "Oversized data must not silently discard original assets or allow overwriting it.");
    }

    private static void NewSaveClearsOldState()
    {
        MarketSession session = Fresh();
        session.Market.Deposit(100);
        session.Market.Open(PositionSide.Short, 10m, 5);
        session.PreserveForReload();
        SetWorld(72, 0);
        session.IntoOtherSave();
        Require(session.CanTrade && session.CanSave, "Explicit new save should initialize normally.");
        Require(session.Market.State.Balance == 0 && session.Market.UsedMargin == 0m,
            "Account leaked into another save.");
    }

    private static void UnexpectedDataReplacement()
    {
        MarketSession session = Fresh();
        session.Market.Deposit(100);
        session.PreserveForReload();
        SetWorld(71, 100);
        session.Tick();
        Require(!session.CanTrade, "A replaced GameData without a load callback reused the old account.");
    }

    private static void ReloadContinuity()
    {
        MarketSession first = Fresh();
        first.Market.Deposit(100);
        first.Market.Open(PositionSide.Long, 10m, 5);
        GameMain.gameTick = 1000;
        first.Tick();
        first.PreserveForReload();
        byte[] expected = MarketBytes(first.Market);
        MarketSession second = new MarketSession();
        Require(second.CanTrade && second.CanSave, "Same-session reload lost authority.");
        Equal(expected, MarketBytes(second.Market), "F6 changed assets/history/RNG.");
        MarginMarket control;
        using (MemoryStream stream = new MemoryStream(expected))
        using (BinaryReader reader = new BinaryReader(stream)) control = MarginMarket.Import(reader);
        GameMain.gameTick = 2500;
        second.Tick();
        control.AdvanceTo(2500);
        Equal(MarketBytes(control), MarketBytes(second.Market), "Reload changed future random trajectory.");
    }

    private static void OldOwnerCannotOverwrite()
    {
        MarketSession first = Fresh();
        first.Market.Deposit(100);
        first.PreserveForReload();
        MarketSession second = new MarketSession();
        second.Market.Deposit(23);
        second.PreserveForReload();
        first.Market.Deposit(1000);
        first.PreserveForReload();
        first.IntoOtherSave();
        Require(!first.CanTrade && !first.CanSave, "The old instance retained live authority.");
        MarketSession third = new MarketSession();
        Require(third.Market.State.Balance == 123, "Deferred old cleanup replaced the new account.");
    }

    private static void HandoffIdentityAndTime()
    {
        MarketSession session = Fresh();
        session.Market.Deposit(100);
        session.PreserveForReload();
        SetWorld(71, 100);
        MarketSession replaced = new MarketSession();
        Require(!replaced.CanTrade && !replaced.CanSave, "Handoff accepted another GameData object.");

        Reset();
        session = Fresh();
        session.PreserveForReload();
        GameMain.gameTick = 99;
        MarketSession rewound = new MarketSession();
        Require(!rewound.CanTrade, "Handoff accepted a time rewind.");
    }

    private static void PauseAndMenu()
    {
        MarketSession session = Fresh();
        byte[] before = MarketBytes(session.Market);
        GameMain.isPaused = true;
        GameMain.gameTick += 900;
        session.Tick();
        Require(!session.CanTrade && session.CanSave, "Pause must stop trading but still allow safe saves.");
        Equal(before, MarketBytes(session.Market), "Quotes advanced while paused.");
        GameMain.instance.isMenuDemo = true;
        Require(!session.CanTrade && !session.CanSave, "Menu demo must not expose the live account.");
    }

    private static void DelayedGameBinding()
    {
        byte[] snapshot = Save(Fresh());
        ClearHandoff();
        GameData loaded = GameMain.data;
        GameMain.data = null;
        MarketSession session = new MarketSession();
        Load(session, snapshot);
        Require(!session.CanTrade && !session.CanSave, "Unbound data cannot be traded/saved.");
        GameMain.data = loaded;
        session.Tick();
        foreach (int id in MarketCatalog.ItemIds) if (MarketCatalog.IsSelectable(id)) session.SetResourceEnabled(id, true);
        Require(session.CanTrade && session.CanSave, "Postload payload did not bind when matching data appeared.");
    }

    private static void TimeRollback()
    {
        MarketSession session = Fresh();
        session.Market.Deposit(8);
        session.PreserveForReload();
        byte[] snapshot = Save(session);
        GameMain.gameTick--;
        session.Tick();
        Require(!session.CanTrade && session.CanSave, "Time rollback must lock trading and retain recovery data.");
        Equal(snapshot, Save(session), "Time rollback cleared or rewrote the last known account.");
    }

    private static void QuoteBoundaryHandoff()
    {
        GameMain.gameTick = 101; // Deliberately not aligned with the 30-tick cache period.
        MarketSession session = Fresh();
        session.Market.Deposit(20);
        session.PreserveForReload();
        long quoteBoundary = GameMain.gameTick + MarginMarket.QuoteIntervalTicks * 3;
        GameMain.gameTick = quoteBoundary - 20;
        session.Tick(); // Periodic cache immediately before the next quote sample.
        long previousQuote = session.Market.History[session.Market.History.Count - 1].GameTick;
        GameMain.gameTick = quoteBoundary;
        session.Tick(); // New quote only 20 ticks later must bypass the 30-tick throttle.
        Require(session.Market.History[session.Market.History.Count - 1].GameTick > previousQuote,
            "Quote-boundary regression fixture did not cross a quote boundary.");
        byte[] expected = MarketBytes(session.Market);
        MarketSession reloaded = new MarketSession();
        Equal(expected, MarketBytes(reloaded.Market), "Immediate F6 rolled the just-published quote back.");
        MarginMarket control;
        using (MemoryStream stream = new MemoryStream(expected))
        using (BinaryReader reader = new BinaryReader(stream)) control = MarginMarket.Import(reader);
        GameMain.gameTick = 1001;
        reloaded.Tick();
        control.AdvanceTo(1001);
        Equal(MarketBytes(control), MarketBytes(reloaded.Market), "Quote-boundary F6 altered subsequent RNG.");
    }

    private static void MenuReleasesData()
    {
        MarketSession session = Fresh();
        session.Market.Deposit(20);
        session.PreserveForReload();
        GameMain.data = null;
        session.Tick();
        Require(AppDomain.CurrentDomain.GetData(CacheKey) == null, "Unloaded GameData stayed in the global cache.");
        Require(!session.CanTrade && !session.CanSave, "Unloaded game retained authority.");
    }

    private static void ClearHandoff()
    {
        AppDomain.CurrentDomain.SetData(CacheKey, null);
        AppDomain.CurrentDomain.SetData(OwnerKey, null);
    }

    private static void LegacySnapshotAndHandoff()
    {
        var legacy = new SpotMarket(); legacy.Deposit(10); legacy.AdvanceTo(GameMain.gameTick);
        byte[] marketBytes;
        using (var stream = new MemoryStream()) using (var writer = new BinaryWriter(stream))
        { legacy.Export(writer); marketBytes = stream.ToArray(); }
        byte[] oldSnapshot;
        using (var stream = new MemoryStream()) using (var writer = new BinaryWriter(stream))
        {
            writer.Write(0x46584453); writer.Write(1);
            writer.Write(GameMain.data.gameDesc.creationTime.Ticks);
            writer.Write(GameMain.data.gameDesc.galaxySeed); writer.Write(GameMain.gameTick);
            writer.Write(marketBytes); oldSnapshot = stream.ToArray();
        }
        var imported = new MarketSession(); Load(imported, oldSnapshot);
        Require(imported.CanTrade && imported.Market.State.Balance == 10m && imported.Market.State.MigratedFromSpot,
            "Old disk snapshot was not converted one-to-one.");
        ClearHandoff();
        AppDomain.CurrentDomain.SetData(CacheKey, new object[] { GameMain.data,
            GameMain.data.gameDesc.creationTime.Ticks, GameMain.data.gameDesc.galaxySeed,
            marketBytes, 1, "old account", oldSnapshot, GameMain.gameTick, false });
        var liveReload = new MarketSession();
        Require(liveReload.CanTrade && liveReload.Market.State.Balance == 10m,
            "Live v0.2 F6 handoff failed migration.");
        byte[] upgraded = Save(liveReload); ClearHandoff();
        var secondLoad = new MarketSession(); Load(secondLoad, upgraded);
        Require(secondLoad.CanTrade && secondLoad.Market.State.Balance == 10m,
            "Upgraded snapshot converted assets twice.");
    }

    private static void ThreeMarketSnapshot()
    {
        var first = Fresh();
        foreach (var market in first.Markets)
        {
            market.Deposit(market.State.ItemId);
            market.Open(PositionSide.Short, 1, 5);
        }
        byte[] payload = Save(first);
        ClearHandoff(); var restored = new MarketSession(); Load(restored, payload);
        Require(restored.CanTrade, "Three-market snapshot did not restore.");
        foreach (var market in first.Markets)
            Equal(MarketBytes(market), MarketBytes(restored.GetMarket(market.State.ItemId)), "An item account changed during disk restore.");
        GameMain.gameTick += 450;
        foreach (var market in first.Markets) market.AdvanceTo(GameMain.gameTick);
        restored.Tick();
        foreach (var market in first.Markets)
            Equal(MarketBytes(market), MarketBytes(restored.GetMarket(market.State.ItemId)), "An item's future prices changed after restore.");
    }

    private static void ThreeMarketHandoff()
    {
        var first = Fresh();
        foreach (var market in first.Markets) { market.Deposit(50); market.Open(PositionSide.Long, 2, 3); }
        first.PreserveForReload();
        var second = new MarketSession();
        Require(second.CanTrade, "Three-market F6 lost session authority.");
        foreach (var market in first.Markets)
            Equal(MarketBytes(market), MarketBytes(second.GetMarket(market.State.ItemId)), "F6 lost a market account.");
        GameMain.isPaused = true; GameMain.gameTick += 900; second.Tick();
        foreach (var market in first.Markets)
            Equal(MarketBytes(market), MarketBytes(second.GetMarket(market.State.ItemId)), "A hidden market advanced while paused.");
        GameMain.isPaused = false;
        second.IntoOtherSave();
        foreach (var market in second.Markets)
            Require(market.State.Balance == 0 && market.Position == null, "A new save inherited an item account.");
    }

    private static void MarginLegacyHandoff()
    {
        var old = new MarginMarket(); old.AdvanceTo(GameMain.gameTick); old.Deposit(100);
        old.Open(PositionSide.Long, 10, 20);
        AppDomain.CurrentDomain.SetData(CacheKey, new object[] { GameMain.data,
            GameMain.data.gameDesc.creationTime.Ticks, GameMain.data.gameDesc.galaxySeed,
            MarketBytes(old), 1, "", null, GameMain.gameTick, false });
        var upgraded = new MarketSession();
        Require(upgraded.CanTrade, "v0.4 margin handoff lost trading authority.");
        Equal(MarketBytes(old), MarketBytes(upgraded.Market), "v0.4 migration lost copper or an active position.");
        Require(upgraded.GetMarket(MarketCatalog.Plastic).State.Balance == 0 &&
            upgraded.GetMarket(MarketCatalog.Matrix).State.Balance == 0, "New items received free funds.");
        var saved = Save(upgraded); ClearHandoff(); var reloaded = new MarketSession(); Load(reloaded, saved);
        Require(reloaded.CanTrade && reloaded.Market.Position != null, "Upgraded account failed to reload.");
    }

    private static void ResourceSelectionPersistence()
    {
        var first = Fresh(); first.Market.Deposit(100);
        first.SetResourceEnabled(1001, true); first.GetMarket(1001).Deposit(50);
        first.SetResourceEnabled(1002, false);
        byte[] frozen = MarketBytes(first.Market);
        GameMain.gameTick += 9000; first.Tick();
        Equal(frozen, MarketBytes(first.Market), "Disabled resource was advanced by session tick.");
        var live = new MarketSession();
        Require(live.CanTrade && !live.IsMarketEnabled(1002) && live.IsMarketEnabled(1001), "F6 lost resource selection.");
        Equal(frozen, MarketBytes(live.Market), "F6 changed dormant cash or history.");
        Require(live.GetMarket(1001).State.Balance == 50 && live.FindMarket(1003) == null, "F6 created or lost resource accounts.");
        byte[] snapshot = Save(live); ClearHandoff(); var disk = new MarketSession(); Load(disk, snapshot);
        Require(disk.CanTrade && !disk.IsMarketEnabled(1002) && disk.IsMarketEnabled(1001), "Disk load lost selection.");
        Equal(frozen, MarketBytes(disk.Market), "Disk load changed dormant account.");
        decimal price = disk.Market.MidPrice; disk.SetResourceEnabled(1002, true);
        Require(disk.Market.MidPrice == price && disk.Market.State.Balance == 100, "Resuming backfilled disabled prices or lost money.");
    }

    private static void MatrixSelectionPersistence()
    {
        var first = Fresh(); var matrix = first.GetMarket(6006); matrix.Deposit(120);
        first.SetResourceEnabled(6006, false); var frozen = MarketBytes(matrix);
        GameMain.gameTick += 9000; first.Tick(); Equal(frozen, MarketBytes(matrix), "Disabled matrix advanced.");
        var live = new MarketSession();
        Require(live.CanTrade && !live.IsMarketEnabled(6006), "F6 lost matrix selection.");
        Equal(frozen, MarketBytes(live.GetMarket(6006)), "F6 changed frozen matrix assets/history.");
        var snapshot = Save(live); ClearHandoff(); var disk = new MarketSession(); Load(disk, snapshot);
        Require(disk.CanTrade && !disk.IsMarketEnabled(6006), "Disk load lost matrix selection.");
        Equal(frozen, MarketBytes(disk.GetMarket(6006)), "Disk load changed frozen matrix assets/history.");
        var price = disk.GetMarket(6006).MidPrice; disk.SetResourceEnabled(6006, true);
        Require(disk.GetMarket(6006).MidPrice == price && disk.GetMarket(6006).State.Balance == 120, "Matrix resume changed price or funds.");
    }

    private static void ResourceSelectionPauseAndReset()
    {
        var session = Fresh(); session.SetResourceEnabled(1001, true); session.GetMarket(1001).Deposit(50);
        GameMain.isPaused = true;
        bool rejected = false;
        try { session.SetResourceEnabled(1001, false); } catch (InvalidOperationException) { rejected = true; }
        Require(rejected && session.IsMarketEnabled(1001) && session.GetMarket(1001).State.Balance == 50, "Paused selection mutated account state.");
        GameMain.isPaused = false; session.IntoOtherSave();
        Require(session.FindMarket(1001) == null && !session.IsMarketEnabled(1002) && !session.IsMarketEnabled(1115), "New save inherited selection or balances.");
    }

    private static void DeliveryPersistence()
    {
        var first = Fresh(); first.Market.Deposit(50);
        var platform = new DeliveryPlatform { PlanetId = 101, EntityId = 123, ProtoId = DeliveryBuildings.Planetary, X = 100, Y = 50, Z = 30 };
        platform.Stock(1002).Add(System.Numerics.BigInteger.Pow(10, 40), 0);
        first.Delivery.Platforms.Add(platform); first.PreserveForReload();
        var reloaded = new MarketSession();
        Require(reloaded.CanTrade && reloaded.Delivery.Platforms[0].Token == platform.Token, "F6 lost delivery binding.");
        Require(reloaded.Delivery.Platforms[0].Stock(1002).Count == System.Numerics.BigInteger.Pow(10, 40), "F6 lost unbounded stock.");
        var payload = Save(reloaded); ClearHandoff(); var disk = new MarketSession(); Load(disk, payload);
        Require(disk.CanTrade && disk.Market.State.Balance == 50, "Delivery migration changed trading funds.");
        Require(disk.Delivery.Platforms[0].Token == platform.Token && disk.Delivery.Platforms[0].Stock(1002).Count == platform.Stock(1002).Count,
            "Disk snapshot changed delivery assets or identity.");
        disk.IntoOtherSave(); Require(disk.Delivery.Platforms.Count == 0 && disk.Delivery.FindBinding(1002) == null, "Delivery data crossed save boundaries.");
    }

    private static void PausedDeliveryAccess()
    {
        var first = Fresh(); GameMain.isPaused = true;
        Require(first.CanAccessDelivery && !first.CanTrade, "Pause disabled physical storage identity or enabled trading.");
        var second = new MarketSession(); Require(!first.CanAccessDelivery && second.CanAccessDelivery, "Old owner retained physical storage authority.");
        second.IntoOtherSave(); GameMain.isRunning = false;
        Require(!second.CanAccessDelivery, "Physical storage remained active in the menu.");
    }

    private static void IntroductionPersistence()
    {
        var first = Fresh(); Require(first.AcknowledgeIntroduction(), "First visit was not acknowledged.");
        Require(!first.AcknowledgeIntroduction(), "Introduction repeated within the session.");
        var hot = new MarketSession(); Require(!hot.AcknowledgeIntroduction(), "F6 lost introduction state.");
        var saved = Save(hot); ClearHandoff(); var disk = new MarketSession(); Load(disk, saved);
        Require(!disk.AcknowledgeIntroduction(), "Disk reload repeated introduction.");
        disk.IntoOtherSave(); Require(disk.AcknowledgeIntroduction(), "A new save inherited introduction state.");
    }

    private static void AchievementPersistence()
    {
        var first = Fresh(); first.Market.Deposit(100);
        first.Market.Open(PositionSide.Long, first.Market.MarginForAllocation(100, 10), 10, 100);
        var coin = first.GetMarket(MarketCatalog.BuildingCoin); coin.NotifyBuildingPurchase(5001, 3);
        first.Achievements.DoubledMarkets.Add(6006); first.PreserveForReload();
        var hot = new MarketSession();
        Require(hot.Achievements.Completed(AchievementId.NewTrader) && hot.Achievements.Pending.Count == 1
            && hot.Achievements.FullPositions[1002] == GameMain.gameTick
            && hot.Achievements.PurchasedKinds.Contains(5001) && hot.Achievements.DoubledMarkets.Contains(6006), "F6 lost achievement state.");
        var payload = Save(hot); ClearHandoff(); var disk = new MarketSession(); Load(disk, payload);
        Require(disk.CanTrade && disk.Achievements.Completed(AchievementId.NewTrader)
            && disk.Achievements.Pending.Count == 1 && disk.Achievements.PurchasedKinds.Contains(5001)
            && disk.Achievements.FullPositions.ContainsKey(1002), "Disk lost achievement state.");
        disk.Achievements.AcknowledgeFirst(); disk.PreserveForReload();
        var acknowledged = new MarketSession(); Require(acknowledged.Achievements.Pending.Count == 0, "F6 replays shown achievement.");
        acknowledged.IntoOtherSave(); Require(acknowledged.Achievements.CompletedCount == 0
            && acknowledged.Achievements.PurchasedKindCount == 0 && acknowledged.Achievements.FullPositions.Count == 0, "New save inherited achievements.");
    }

    private static void PreviousSnapshotAchievements()
    {
        var first = Fresh(); first.Market.Deposit(42);
        byte[] previous;
        using (var stream = new MemoryStream()) using (var writer = new BinaryWriter(stream))
        {
            writer.Write(0x46584453); writer.Write(2); writer.Write(GameMain.data.gameDesc.creationTime.Ticks);
            writer.Write(GameMain.data.gameDesc.galaxySeed); writer.Write(GameMain.gameTick);
            var bookField = typeof(MarketSession).GetField("_book", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            ((MarketBook)bookField.GetValue(first)).Export(writer); first.Delivery.Export(writer); previous = stream.ToArray();
        }
        ClearHandoff(); var loaded = new MarketSession(); Load(loaded, previous); loaded.Tick();
        Require(loaded.CanTrade && loaded.Market.State.Balance == 42 && loaded.Achievements.CompletedCount == 0, "Old snapshot lost assets.");
    }

    private static byte[] Save(MarketSession session)
    {
        using (MemoryStream stream = new MemoryStream())
        using (BinaryWriter writer = new BinaryWriter(stream))
        {
            session.Export(writer);
            return stream.ToArray();
        }
    }

    private static void Load(MarketSession session, byte[] payload)
    {
        using (MemoryStream stream = new MemoryStream(payload, false))
        using (BinaryReader reader = new BinaryReader(stream)) session.Import(reader);
    }

    private static byte[] MarketBytes(MarginMarket market)
    {
        using (MemoryStream stream = new MemoryStream())
        using (BinaryWriter writer = new BinaryWriter(stream))
        {
            market.Export(writer);
            return stream.ToArray();
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static void Equal(byte[] expected, byte[] actual, string message)
    {
        if (expected.Length != actual.Length) throw new InvalidOperationException(message);
        for (int index = 0; index < expected.Length; index++)
            if (expected[index] != actual[index]) throw new InvalidOperationException(message);
    }
}
