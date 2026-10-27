using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Bootstrap;
using crecheng.DSPModSave;
using FXDSP.Configuration;
using FXDSP.Persistence;
using FXDSP.Runtime;
using FXDSP.UI;
using HarmonyLib;

namespace FXDSP
{
    [BepInPlugin(PluginInfo.Guid, PluginInfo.Name, PluginInfo.Version)]
    [BepInDependency(DSPModSavePlugin.MODGUID, "1.2.2")]
    public sealed class Plugin : BaseUnityPlugin, IModCanSave
    {
        private const string RuntimeOwnerKey = "FXDSP.PluginRuntimeOwner.v1";
        private readonly string _owner = Guid.NewGuid().ToString("N");
        private Harmony _harmony;
        private ModSettings _settings;
        private MarketSession _session;
        private MarketWindow _window;
        private UIMessageBox _achievementPopup;
        private PositionHud _hud;
        private DeliveryService _delivery;
        private bool _useDelivery;
        private bool _registered;
        private bool _saveWarningShown;
        private string _notice = "";
        private int _selectedLeverage = 5;
        private int _selectedItemId = Core.MarketCatalog.BuildingCoin;
        private int _exchangeItemId = 2001;
        private int _exchangeTier = 1;
        private readonly List<MarketOption> _marketOptions = new List<MarketOption>();
        private readonly Dictionary<int, Core.PositionSettlement> _shownSettlements = new Dictionary<int, Core.PositionSettlement>();
        internal static Plugin Active { get; private set; }
        internal bool BypassAbnormalityChecks
        {
            get { return OwnsRuntime && _settings != null && _settings.BypassAbnormalityChecks.Value; }
        }

        private bool OwnsRuntime
        {
            get { return Equals(AppDomain.CurrentDomain.GetData(RuntimeOwnerKey), _owner); }
        }

        private void Awake()
        {
            try
            {
                AppDomain.CurrentDomain.SetData(RuntimeOwnerKey, _owner);
                Active = this;
                ModLocalization.Update();
                _settings = new ModSettings(Config);
                _settings.VerboseLogging.SettingChanged += OnVerboseLoggingChanged;
                _session = new MarketSession();
                _delivery = new DeliveryService(_session);
                _window = new MarketWindow(GetSnapshot, GetPortfolio, Execute, _session.AcknowledgeIntroduction, () => _session.Achievements);
                _hud = new PositionHud(GetPortfolio, Execute, _window.ShowAllPositions, _window.ShowExchange,
                    () => _settings.PositionHudMode.Value, mode => _settings.PositionHudMode.Value = mode);

                // A separate owner per instance prevents an old instance's delayed
                // OnDestroy during reload from removing the new instance's patches.
                _harmony = new Harmony(PluginInfo.Guid + ".runtime." + Guid.NewGuid().ToString("N"));
                _harmony.PatchAll(typeof(Plugin).Assembly);
                Logger.LogInfo("Local abnormality bypass: " + BypassAbnormalityChecks + ". Existing records retained; sandbox/account eligibility unchanged.");
                TryRegisterSaveHandler();
                var research = ResearchProgression.Read();
                _session.SetTradingFeeRate(research.TradingFee);
                Logger.LogInfo(PluginInfo.Name + " " + PluginInfo.Version + " loaded. F8: copper-margin market; F6: ScriptEngine reload.");
                if (_settings.VerboseLogging.Value)
                    Logger.LogInfo("Harmony owner: " + _harmony.Id);
            }
            catch (Exception exception)
            {
                Cleanup();
                Logger.LogError("FXDSP initialization failed: " + exception);
                throw;
            }
        }

        private void Update()
        {
            if (!OwnsRuntime || _session == null) return;
            try
            {
                TryRegisterSaveHandler();
                ModLocalization.Update();
                var research = ResearchProgression.Read();
                _session.SetTradingFeeRate(research.TradingFee);
                if (GameMain.isRunning && GameMain.data != null && GameMain.instance != null && !GameMain.instance.isMenuDemo)
                    ResourceMarketCatalog.GetOptions();
                _session.Tick();
                _delivery.Tick();
                ShowNewSettlement();
                try
                {
                    var inGame = GameMain.isRunning && GameMain.mainPlayer != null
                        && GameMain.instance != null && !GameMain.instance.isMenuDemo;
                    if (!inGame) { _window.Close(); _hud.Tick(false); }
                    else if (!research.Access)
                    {
                        if (_settings.WindowKey.Value.IsDown() && !VFInput.inputing)
                            UIRealtimeTip.Popup(Core.TextLocalization.Translate("请先研究“交易系统”：10 铁矿、10 铜矿。"), true, 0);
                        _window.Close(); _hud.Tick(false);
                    }
                    else
                    {
                        if (_settings.WindowKey.Value.IsDown() && !VFInput.inputing)
                            _window.Toggle();
                        _window.Tick();
                        _hud.Tick(!_window.HasSettlement);
                        ShowAchievements();
                    }
                }
                catch (Exception uiException)
                {
                    _window.Close();
                    _hud.Tick(false);
                    _notice = "交易窗口暂时无法创建，请查看日志后重试 F8。";
                    Logger.LogError("FXDSP window failed: " + uiException);
                }
            }
            catch (Exception exception)
            {
                _notice = "更新失败，请查看 BepInEx 日志。";
                Logger.LogError("FXDSP update failed: " + exception);
                // Disable further mutations after an unexpected runtime failure.
                enabled = false;
                _window.Close();
                if (_hud != null) _hud.Tick(false);
            }
        }

        private void ShowAchievements()
        {
            if (!_session.CanAccessDelivery || _session.Achievements.Pending.Count == 0
                || (_achievementPopup != null && _achievementPopup.gameObject.activeInHierarchy)) return;
            var achievement = Core.AchievementCatalog.Find(_session.Achievements.Pending[0]);
            _achievementPopup = UIMessageBox.Show(Core.TextLocalization.Translate("成就达成 · " + achievement.Name),
                Core.TextLocalization.Translate(achievement.Description), Core.TextLocalization.Translate("知道了"), 0);
            _session.Achievements.AcknowledgeFirst();
            _session.PreserveForReload();
        }

        private void TryRegisterSaveHandler()
        {
            if (_registered || !OwnsRuntime || DSPModSavePlugin.logger == null) return;
            if (!Chainloader.PluginInfos.ContainsKey(DSPModSavePlugin.MODGUID)) return;
            DSPModSavePlugin.RemoveModSaveManually(this);
            DSPModSavePlugin.AddModSaveManually(this);
            _registered = true;
            Logger.LogInfo("FXDSP save handler registered.");
        }

        private MarketSnapshot GetSnapshot(int quantity)
        {
            var snapshot = new MarketSnapshot();
            if (_session == null) return snapshot;
            if (Core.MarketCatalog.IsSelectable(_selectedItemId) && !_session.IsMarketEnabled(_selectedItemId))
            {
                var replacement = _session.Markets.FirstOrDefault(candidate => Core.MarketCatalog.IsSelectable(candidate.State.ItemId)
                    && Core.MarketCatalog.Category(candidate.State.ItemId) == Core.MarketCatalog.Category(_selectedItemId)
                    && _session.IsMarketEnabled(candidate.State.ItemId));
                if (replacement != null) _selectedItemId = replacement.State.ItemId;
            }
            var market = _session.GetMarket(_selectedItemId);
            var research = ResearchProgression.Read();
            snapshot.Ready = enabled && OwnsRuntime && _registered && _session.CanTrade && !GameMain.isPaused && research.Access;
            snapshot.ResourcesEditable = snapshot.Ready;
            snapshot.MarketEnabled = _session.IsMarketEnabled(_selectedItemId);
            snapshot.Ready &= snapshot.MarketEnabled;
            snapshot.Status = snapshot.Ready ? _notice : _session.Status;
            if (snapshot.ResourcesEditable && !snapshot.MarketEnabled)
                snapshot.Status = string.IsNullOrEmpty(_notice) ? "请选择品种" : _notice;
            if (GameMain.isPaused) snapshot.Status = "游戏已暂停，交易与行情暂停。";
            snapshot.PersistenceStatus = snapshot.Ready ? "" : _session.Status;
            if (market == null) return snapshot;
            snapshot.ItemId = market.State.ItemId;
            snapshot.ItemName = Core.MarketCatalog.Name(snapshot.ItemId);
            bool isCoin = snapshot.ItemId == Core.MarketCatalog.BuildingCoin;
            var item = LDB.items == null || isCoin ? null : LDB.items.Select(snapshot.ItemId);
            if (item != null) { snapshot.ItemName = item.name; snapshot.ItemIcon = item.iconSprite; }
            snapshot.BackpackQuantity = isCoin || !snapshot.MarketEnabled ? 0 : PlayerInventory.Count(snapshot.ItemId);
            snapshot.CanUseDelivery = snapshot.ItemId != 1099;
            snapshot.UseDelivery = _useDelivery && snapshot.CanUseDelivery;
            snapshot.TransferAvailable = snapshot.UseDelivery ? _delivery.Available(snapshot.ItemId).Count
                : isCoin || !snapshot.MarketEnabled ? 0 : PlayerInventory.Available(snapshot.ItemId);
            snapshot.DeliveryName = _delivery.BindingName(snapshot.ItemId);
            snapshot.DeliveryBound = _delivery.BoundPlatform(snapshot.ItemId) != null;
            if (_marketOptions.Count == 0 && LDB.items != null)
                foreach (int id in Core.MarketCatalog.ItemIds)
                {
                    var prototype = id == Core.MarketCatalog.BuildingCoin ? null : LDB.items.Select(id);
                    _marketOptions.Add(new MarketOption { ItemId = id, Name = Core.MarketCatalog.Name(id),
                        Icon = prototype == null ? null : prototype.iconSprite });
                }
            snapshot.Markets = _marketOptions;
            snapshot.ResourceOptions = ResourceMarketCatalog.GetOptions();
            foreach (var option in snapshot.ResourceOptions)
            {
                option.Enabled = _session.IsMarketEnabled(option.ItemId);
                var account = _session.FindMarket(option.ItemId);
                option.HasPosition = account != null && account.Position != null;
            }
            snapshot.BuildingCoinBalance = _session.GetMarket(Core.MarketCatalog.BuildingCoin).State.Balance;
            if (isCoin)
            {
                snapshot.ExchangeOffers = BuildingExchangeCatalog.GetOffers().Where(offer => offer.Tier == _exchangeTier).ToArray();
                foreach (var offer in snapshot.ExchangeOffers) snapshot.ExchangeInventory[offer.ItemId] = PlayerInventory.Count(offer.ItemId);
                snapshot.ExchangeTier = _exchangeTier;
                snapshot.ExchangeUnlocked = research.CanExchange(_exchangeTier);
                snapshot.ExchangeFeeRate = research.ExchangeFee;
                snapshot.ExchangeItemId = _exchangeItemId;
                snapshot.ExchangeBackpackQuantity = _exchangeItemId <= 0 ? 0 : PlayerInventory.Count(_exchangeItemId);
            }
            snapshot.AvailableBalance = market.State.Balance;
            snapshot.Equity = market.Equity;
            snapshot.UsedMargin = market.UsedMargin;
            snapshot.UnrealizedPnL = market.UnrealizedPnL;
            snapshot.RealizedPnL = market.State.RealizedPnL;
            snapshot.NetPnL = market.NetPnL;
            snapshot.SelectedLeverage = _selectedLeverage;
            snapshot.Bid = snapshot.MarketEnabled ? market.Bid : 0m;
            snapshot.Ask = snapshot.MarketEnabled ? market.Ask : 0m;
            snapshot.FeeRate = market.FeeRate;
            snapshot.Deposited = market.State.TotalDeposited;
            snapshot.Withdrawn = market.State.TotalWithdrawn;
            snapshot.ReturnRatio = Core.AccountPerformance.ReturnRatio(snapshot.RealizedPnL, snapshot.Deposited, snapshot.Withdrawn);
            if (snapshot.MarketEnabled) snapshot.Candles = Core.MarketCandles.Build(market.History);
            snapshot.RecentEvents = market.RecentLogs.Reverse().ToArray();
            var position = market.Position;
            snapshot.HasPosition = position != null;
            if (position != null)
            {
                snapshot.PositionDirection = position.Side == Core.PositionSide.Long ? "做多" : "做空";
                snapshot.PositionLeverage = position.Leverage;
                snapshot.EntryPrice = position.EntryPrice;
                snapshot.PositionMargin = position.Margin;
                snapshot.PositionOpenTick = position.OpenTick;
                snapshot.CurrentTick = GameMain.gameTick;
                snapshot.LiquidationPrice = market.LiquidationPrice;
            }
            if (snapshot.MarketEnabled && quantity >= 1 && quantity <= 100)
            {
                snapshot.OrderMargin = market.MarginForAllocation(quantity, _selectedLeverage);
                if (snapshot.OrderMargin > 0m)
                {
                    snapshot.OpenCost = market.OpenCost(snapshot.OrderMargin, _selectedLeverage);
                    snapshot.OpenFee = market.OpenFee(snapshot.OrderMargin, _selectedLeverage);
                }
            }
            return snapshot;
        }

        private void Execute(MarketCommand command, int quantity)
        {
            if (command == MarketCommand.UseBackpack || command == MarketCommand.UseDelivery)
            { _useDelivery = command == MarketCommand.UseDelivery; _notice = ""; return; }
            if (command == MarketCommand.SelectExchangeTier)
            {
                if (quantity < 1 || quantity > 5) return;
                _exchangeTier = quantity;
                var offers = BuildingExchangeCatalog.GetOffers().Where(offer => offer.Tier == quantity).ToArray();
                if (!offers.Any(offer => offer.ItemId == _exchangeItemId)) _exchangeItemId = offers.Length == 0 ? 0 : offers[0].ItemId;
                _notice = "";
                return;
            }
            if (command == MarketCommand.SelectExchangeItem)
            {
                BuildingExchangeCatalog.Find(quantity);
                _exchangeItemId = quantity;
                _notice = "";
                return;
            }
            if (command == MarketCommand.SelectMarket && Core.MarketCatalog.Contains(quantity))
            {
                _selectedItemId = quantity;
                _notice = "";
                return;
            }
            if (!OwnsRuntime || !_registered || !_session.CanTrade || GameMain.isPaused || !enabled)
            {
                _notice = "当前不能交易，请先进入存档并检查市场状态。";
                return;
            }
            try
            {
                var research = ResearchProgression.Read();
                research.RequireAccess();
                _session.SetTradingFeeRate(research.TradingFee);
                var market = _session.GetMarket(_selectedItemId);
                if ((command == MarketCommand.Deposit || command == MarketCommand.Withdraw || command == MarketCommand.OpenLong
                    || command == MarketCommand.OpenShort) && !_session.IsMarketEnabled(_selectedItemId))
                    throw new InvalidOperationException("请先启用该市场。");
                switch (command)
                {
                    case MarketCommand.EnableResource:
                    case MarketCommand.DisableResource:
                        ResourceMarketCatalog.RequireResource(quantity);
                        _session.SetResourceEnabled(quantity, command == MarketCommand.EnableResource);
                        _notice = "";
                        break;
                    case MarketCommand.Deposit: _notice = _useDelivery && _selectedItemId != 1099 ? _delivery.Transfer(market, quantity, true) : PlayerInventory.Deposit(market, quantity); break;
                    case MarketCommand.Withdraw: _notice = _useDelivery && _selectedItemId != 1099 ? _delivery.Transfer(market, quantity, false) : PlayerInventory.Withdraw(market, quantity); break;
                    case MarketCommand.OpenLong: _notice = market.Open(Core.PositionSide.Long,
                        market.MarginForAllocation(quantity, _selectedLeverage), _selectedLeverage, quantity); break;
                    case MarketCommand.OpenShort: _notice = market.Open(Core.PositionSide.Short,
                        market.MarginForAllocation(quantity, _selectedLeverage), _selectedLeverage, quantity); break;
                    case MarketCommand.ClosePosition: _notice = market.Close(); break;
                    case MarketCommand.CloseMarketPosition:
                    case MarketCommand.CloseHudPosition:
                        _notice = _session.ClosePosition(quantity) ? "持仓已平仓。" : "该市场当前空仓。";
                        if (command == MarketCommand.CloseHudPosition)
                            _shownSettlements[quantity] = _session.GetMarket(quantity).LastSettlement;
                        break;
                    case MarketCommand.CloseAllPositions:
                        _notice = "已平仓 " + _session.CloseAllPositions() + " 项持仓。";
                        break;
                    case MarketCommand.BuyBuilding:
                    case MarketCommand.SellBuilding:
                        var offer = BuildingExchangeCatalog.Find(_exchangeItemId);
                        research.RequireExchange(offer.Tier);
                        var wallet = _session.GetMarket(Core.MarketCatalog.BuildingCoin);
                        _notice = command == MarketCommand.BuyBuilding
                            ? PlayerInventory.BuyBuilding(wallet, offer.ItemId, quantity, offer.UnitValue, research.ExchangeFee)
                            : PlayerInventory.SellBuilding(wallet, offer.ItemId, quantity, offer.UnitValue, research.ExchangeFee);
                        break;
                    case MarketCommand.ChangeLeverage:
                        if (!Core.MarginMarket.IsAllowedLeverage(quantity)) throw new InvalidOperationException("不支持该杠杆。");
                        _selectedLeverage = quantity;
                        _notice = "";
                        break;
                    default: throw new InvalidOperationException("未知交易指令。");
                }
                _session.PreserveForReload();
                ShowNewSettlement();
                if (_settings.VerboseLogging.Value) Logger.LogInfo(_notice);
            }
            catch (InvalidOperationException exception) { _notice = exception.Message; }
            catch (OverflowException) { _notice = "数量或金额超出安全数值范围，本次操作取消。"; }
            catch (Exception exception)
            {
                _notice = "操作异常，交易已停用，请保留日志。";
                Logger.LogError("FXDSP command failed: " + exception);
                enabled = false;
            }
        }

        private PortfolioSnapshot GetPortfolio()
        {
            var state = new PortfolioSnapshot();
            if (_session == null) return state;
            state.Ready = enabled && OwnsRuntime && _registered && _session.CanTrade;
            var positions = new List<PositionSnapshot>();
            foreach (var market in _session.Markets)
            {
                var position = market.Position;
                if (position == null) continue;
                int id = market.State.ItemId;
                var item = id == Core.MarketCatalog.BuildingCoin || LDB.items == null ? null : LDB.items.Select(id);
                positions.Add(new PositionSnapshot { ItemId = id, Name = Core.MarketCatalog.Name(id),
                    Icon = item == null ? null : item.iconSprite, Direction = position.Side == Core.PositionSide.Long ? "做多" : "做空",
                    Leverage = position.Leverage, Margin = position.Margin, EntryPrice = position.EntryPrice,
                    CurrentPrice = market.MidPrice, LiquidationPrice = market.LiquidationPrice, UnrealizedPnL = market.UnrealizedPnL,
                    OpenTick = position.OpenTick, CurrentTick = GameMain.gameTick });
            }
            state.Positions = positions;
            return state;
        }

        private void ShowNewSettlement()
        {
            if (!_session.CanTrade) return;
            foreach (var market in _session.Markets)
            {
                var result = market.LastSettlement;
                Core.PositionSettlement shown;
                if (result == null || (_shownSettlements.TryGetValue(market.State.ItemId, out shown) && ReferenceEquals(result, shown))) continue;
                _shownSettlements[market.State.ItemId] = result;
                _notice = "";
                _window.ShowSettlement(result);
            }
        }

        internal void ConsumeEscape()
        {
            if (!OwnsRuntime || _window == null) return;
            try { _window.ConsumeEscape(); }
            catch (Exception exception) { Logger.LogError("FXDSP input failed: " + exception); }
        }

        internal bool AllowSave()
        {
            if (!OwnsRuntime || _session == null || GameMain.data == null
                || GameMain.instance == null || GameMain.instance.isMenuDemo) return true;
            _session.Tick();
            _delivery.Tick();
            if (_session.CanSave) return true;
            if (!_saveWarningShown)
            {
                Logger.LogWarning("FXDSP has not received this save's account state. Saving is blocked to preserve its sidecar. Return to the main menu and reload the save with FXDSP loaded.");
                _saveWarningShown = true;
            }
            _notice = "市场尚未接入当前存档，请返回主菜单重新读档后再保存。";
            return false;
        }

        public void Export(BinaryWriter writer) { _session.Export(writer); }
        public void Import(BinaryReader reader)
        {
            _session.Import(reader);
            _shownSettlements.Clear();
            _window.ResetNotifications();
            _notice = "";
            _saveWarningShown = false;
            Logger.LogInfo("FXDSP account import: " + _session.Status);
        }
        public void IntoOtherSave()
        {
            _session.IntoOtherSave();
            _shownSettlements.Clear();
            _window.ResetNotifications();
            _notice = "";
            _saveWarningShown = false;
            Logger.LogInfo("FXDSP new account: " + _session.Status);
        }

        private void OnVerboseLoggingChanged(object sender, EventArgs args)
        {
            Logger.LogInfo("VerboseLogging = " + _settings.VerboseLogging.Value);
        }

        private void OnDestroy()
        {
            Cleanup();
        }

        private void Cleanup()
        {
            if (OwnsRuntime && _delivery != null) _delivery.Tick();
            if (_session != null) _session.PreserveForReload();
            if (OwnsRuntime && _registered)
                DSPModSavePlugin.RemoveModSaveManually(this);
            _registered = false;
            if (_window != null) { _window.Dispose(); _window = null; }
            if (_hud != null) { _hud.Dispose(); _hud = null; }
            if (_delivery != null) { _delivery.Deactivate(); _delivery = null; }
            if (_settings != null)
            {
                _settings.VerboseLogging.SettingChanged -= OnVerboseLoggingChanged;
                _settings = null;
            }

            if (_harmony != null)
            {
                _harmony.UnpatchSelf();
                _harmony = null;
            }
            if (Active == this) Active = null;
        }
    }

    [HarmonyPatch(typeof(VFInput), "OnUpdate")]
    internal static class MarketInputPatch
    {
        private static void Postfix() { if (Plugin.Active != null) Plugin.Active.ConsumeEscape(); }
    }

    [HarmonyPatch(typeof(GameSave), "SaveCurrentGame")]
    internal static class MarketSaveGuardPatch
    {
        private static bool Prefix(ref bool __result)
        {
            if (Plugin.Active == null || Plugin.Active.AllowSave()) return true;
            __result = false;
            return false;
        }
    }
}
