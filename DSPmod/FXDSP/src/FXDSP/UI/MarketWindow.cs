using System;
using System.Collections.Generic;
using System.Globalization;
using FXDSP.Core;
using FXDSP.Runtime;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FXDSP.UI
{
    internal sealed class MarketWindow : IDisposable
    {
        private const float Width = 1140, Height = 760;
        private static readonly Color Muted = new Color(.6f, .72f, .78f, .9f);
        private static readonly Color Cyan = new Color(.4f, .82f, .85f, 1);
        private static readonly Color Warning = new Color(.85f, .64f, .36f, 1);
        private readonly Func<int, MarketSnapshot> _snapshot;
        private readonly Action<MarketCommand, int> _execute;
        private readonly Func<PortfolioSnapshot> _portfolio;
        private readonly Func<bool> _acknowledgeIntroduction;
        private readonly Func<TradingAchievementState> _achievements;
        private RectTransform _achievementsRoot;
        private AchievementsView _achievementsView;
        private Button _achievementsButton;
        private bool _achievementsMode;
        private readonly TransferSelection _depositSelection = new TransferSelection(), _withdrawSelection = new TransferSelection();
        private int _transferContextItem;
        private bool _transferContextDelivery;
        private RectTransform _positionsRoot, _sidebarRoot;
        private HoldingsView _holdings;
        private Text _inventoryLabel;
        private Button _sourceBackpack, _sourceDelivery;
        private Button _allPositionsButton;
        private bool _positionsMode;
        private bool _resourceSelectionMode;
        private MarketCategory? _categoryOverride;
        private MarketSnapshot _lastState;
        private RectTransform _resourcePickerRoot;
        private Button _chooseResources;
        private ResourceSelectionView _resourcePicker;
        private ResourceSidebarView _resourceSidebar;
        private readonly List<Button> _buttons = new List<Button>();
        private readonly List<Button> _allocationButtons = new List<Button>();
        private readonly Dictionary<int, Button> _marketButtons = new Dictionary<int, Button>();
        private readonly Queue<PositionSettlement> _settlements = new Queue<PositionSettlement>();
        private RectTransform _root, _tradeRoot, _exchangeRoot, _transferRoot, _coinFunding;
        private readonly Dictionary<MarketCategory, Button> _categoryButtons = new Dictionary<MarketCategory, Button>();
        private readonly Dictionary<int, Image> _marketIcons = new Dictionary<int, Image>();
        private readonly List<ExchangeCell> _exchangeCells = new List<ExchangeCell>();
        private Button _coinTradeTab, _exchangeBuy, _exchangeSell;
        private readonly List<Button> _exchangeTierButtons = new List<Button>();
        private readonly Text[] _priceAxis = new Text[4];
        private Text _marginPreview, _exchangeName, _exchangeValue, _exchangeBackpack, _exchangeWallet, _exchangeTotal, _exchangePageText, _coinBalance;
        private Image _exchangeIcon;
        private InputField _exchangeQuantityInput;
        private Slider _exchangeQuantitySlider;
        private int _exchangeMaximum;
        private string _exchangeQuantityValue = "1";
        private int _exchangePage;
        private bool _exchangeMode;
        private sealed class ExchangeCell
        {
            public int ItemId;
            public Button Button;
            public Image Icon, Background;
            public Text Name, Value;
        }
        private Canvas _canvas;
        private NativeUiFactory _ui;
        private InputField _depositInput, _withdrawInput;
        private Slider _allocationSlider, _leverageSlider;
        private Slider _depositSlider, _withdrawSlider;
        private Text _assets, _floating, _floatingReturn, _realized, _returnRate, _positionText, _quote, _preview;
        private Text _backpack, _withdrawable, _status, _events, _chartRange;
        private Text _chartTitle, _transferTitle, _liquidationLabel;
        private Image _liquidationBackground;
        private Text _allocationLabel, _leverageLabel;
        private MarketCandleGraphic _chart;
        private Button _long, _short, _deposit, _withdraw, _closePosition;
        private GameObject _modal;
        private PositionSettlement _pendingSettlement;
        private string _depositValue = "10", _withdrawValue = "10";
        private int _allocation = 25, _selectedLeverage = 5;
        private string _localError = "";
        private float _nextRefresh;
        private bool _dirty = true, _clearSelection, _disposed;
        private int _escapeFrame = -1, _inputEndedFrame = -1;

        internal MarketWindow(Func<int, MarketSnapshot> snapshot, Func<PortfolioSnapshot> portfolio, Action<MarketCommand, int> execute, Func<bool> acknowledgeIntroduction, Func<TradingAchievementState> achievements)
        {
            _snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
            _portfolio = portfolio ?? throw new ArgumentNullException(nameof(portfolio));
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _acknowledgeIntroduction = acknowledgeIntroduction ?? throw new ArgumentNullException(nameof(acknowledgeIntroduction));
            _achievements = achievements;
        }

        internal bool IsOpen { get { return _root != null && _root.gameObject.activeSelf; } }
        internal bool HasSettlement { get { return _modal != null; } }
        internal bool IsInputFocused
        { get { return IsOpen && ((_depositInput != null && _depositInput.isFocused) || (_withdrawInput != null && _withdrawInput.isFocused) || (_exchangeQuantityInput != null && _exchangeQuantityInput.isFocused)); } }
        internal bool BlocksGameInput { get { return IsOpen && (IsInputFocused || IsPointerOver || _modal != null); } }
        internal bool IsPointerOver
        {
            get
            {
                if (!IsOpen) return false;
                var camera = _canvas == null || _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _canvas.worldCamera;
                return RectTransformUtility.RectangleContainsScreenPoint(_root, Input.mousePosition, camera);
            }
        }

        internal void Toggle()
        {
            if (_disposed) return;
            if (IsOpen) { Close(); return; }
            if (!GameMain.isRunning || GameMain.data == null || UIRoot.instance == null
                || UIRoot.instance.uiGame == null || UIRoot.instance.uiGame.inserterWindow == null) return;
            try
            {
                Build();
                bool firstVisit = _acknowledgeIntroduction();
                if (firstVisit)
                {
                    _achievementsMode = false;
                    _categoryOverride = MarketCategory.RawMaterials;
                    _positionsMode = _exchangeMode = false;
                    _resourceSelectionMode = true;
                }
                _dirty = true;
                _nextRefresh = 0;
                Tick();
                if (firstVisit && _modal == null) BuildIntroductionPopup();
            }
            catch (Exception exception)
            {
                Close();
                Debug.LogWarning("[FXDSP] Market window is unavailable: " + exception);
            }
        }

        internal void ShowAllPositions()
        {
            _achievementsMode = false;
            if (_disposed) return;
            _positionsMode = true;
            _categoryOverride = null;
            _resourceSelectionMode = false;
            _exchangeMode = false;
            ClearInputSelection();
            if (!IsOpen) Toggle();
            else { _dirty = true; Refresh(); }
        }

        internal void ShowExchange()
        {
            _achievementsMode = false;
            if (_disposed || _modal != null) return;
            _positionsMode = _resourceSelectionMode = false;
            _categoryOverride = null; _exchangeMode = true;
            _execute(MarketCommand.SelectMarket, MarketCatalog.BuildingCoin);
            ClearInputSelection();
            if (!IsOpen) Toggle(); else { _dirty = true; Refresh(); }
        }

        internal void ShowSettlement(PositionSettlement settlement)
        {
            if (settlement == null || _disposed) return;
            if (_pendingSettlement != null)
            {
                _settlements.Enqueue(settlement);
                if (!IsOpen) Toggle();
                return;
            }
            _pendingSettlement = settlement;
            if (!IsOpen) Toggle();
            else BuildSettlementPopup();
            _dirty = true;
        }

        internal void ResetNotifications() { _settlements.Clear(); DismissSettlement(); }

        internal void Tick()
        {
            if (_disposed || !IsOpen) return;
            ConsumeEscape();
            if (!IsOpen) return;
            if (_clearSelection && !IsInputFocused) { _clearSelection = false; ClearInputSelection(); }
            FitToParent();
            if (!_dirty && Time.unscaledTime < _nextRefresh) return;
            _dirty = false;
            _nextRefresh = Time.unscaledTime + .2f;
            Refresh();
        }

        internal bool ConsumeEscape()
        {
            if (!Input.GetKeyDown(KeyCode.Escape)) return false;
            if (_escapeFrame == Time.frameCount) { VFInput.UseEscape(); return true; }
            if (!IsOpen) return false;
            _escapeFrame = Time.frameCount;
            if (_modal != null) DismissSettlement();
            else if (IsInputFocused || _inputEndedFrame == Time.frameCount) ClearInputSelection();
            else Close();
            VFInput.UseEscape();
            return true;
        }

        internal void Close()
        {
            ClearInputSelection();
            if (_depositInput != null) _depositValue = _depositInput.text;
            if (_withdrawInput != null) _withdrawValue = _withdrawInput.text;
            if (_exchangeQuantityInput != null) _exchangeQuantityValue = _exchangeQuantityInput.text;
            foreach (var input in new[] { _depositInput, _withdrawInput, _exchangeQuantityInput })
                if (input != null) { input.onValueChanged.RemoveAllListeners(); input.onEndEdit.RemoveAllListeners(); }
            if (_allocationSlider != null) _allocationSlider.onValueChanged.RemoveAllListeners();
            if (_leverageSlider != null) _leverageSlider.onValueChanged.RemoveAllListeners();
            if (_depositSlider != null) _depositSlider.onValueChanged.RemoveAllListeners();
            if (_withdrawSlider != null) _withdrawSlider.onValueChanged.RemoveAllListeners();
            if (_exchangeQuantitySlider != null) _exchangeQuantitySlider.onValueChanged.RemoveAllListeners();
            foreach (var button in _buttons) if (button != null) button.onClick.RemoveAllListeners();
            _buttons.Clear();
            _allocationButtons.Clear();
            _marketButtons.Clear();
            _marketIcons.Clear(); _categoryButtons.Clear(); _exchangeCells.Clear(); _exchangeTierButtons.Clear();
            Array.Clear(_priceAxis, 0, _priceAxis.Length);
            if (_holdings != null) { _holdings.Dispose(); _holdings = null; }
            _achievementsView?.Dispose(); _achievementsView = null; _achievementsRoot = null; _achievementsButton = null;
            _inventoryLabel = null; _sourceBackpack = _sourceDelivery = null;
            if (_resourcePicker != null) { _resourcePicker.Dispose(); _resourcePicker = null; }
            if (_resourceSidebar != null) { _resourceSidebar.Dispose(); _resourceSidebar = null; }
            _resourcePickerRoot = null; _chooseResources = null;
            _positionsRoot = _sidebarRoot = null; _allPositionsButton = null;
            DestroyModal();
            if (_root != null) { _root.gameObject.SetActive(false); UnityEngine.Object.Destroy(_root.gameObject); }
            _root = _tradeRoot = _exchangeRoot = _transferRoot = _coinFunding = null; _canvas = null; _ui = null;
            _exchangeQuantityInput = null; _exchangeBuy = _exchangeSell = _coinTradeTab = null;
            _exchangeQuantitySlider = null;
            _depositInput = _withdrawInput = null;
            _allocationSlider = _leverageSlider = null;
            _depositSlider = _withdrawSlider = null;
            _chart = null;
            _assets = _floating = _floatingReturn = _realized = _returnRate = _positionText = _quote = _preview = null;
            _backpack = _withdrawable = _status = _events = _chartRange = null;
            _chartTitle = _transferTitle = _liquidationLabel = null;
            _liquidationBackground = null;
            _allocationLabel = _leverageLabel = null;
            _long = _short = _deposit = _withdraw = _closePosition = null;
            _clearSelection = false;
            _localError = "";
        }

        public void Dispose()
        {
            if (_disposed) return;
            Close();
            _pendingSettlement = null;
            _settlements.Clear();
            _disposed = true;
        }

        private void Build()
        {
            Close();
            _ui = new NativeUiFactory();
            _root = _ui.Root("FXDSP-market-window", Width, Height);
            _canvas = _root.GetComponentInParent<Canvas>();
            var background = _ui.Image("window-background", _root, 0, 0, Width, Height, new Color(.025f, .07f, .09f, 1));
            background.raycastTarget = true;
            var header = _ui.Image("title-bar", _root, 1, 1, Width - 2, 51, new Color(.14f, .23f, .28f, .92f));
            header.raycastTarget = true;
            header.gameObject.AddComponent<MarketDragHandle>().Window = _root;
            _ui.Text("title", _root, 22, 10, 640, 36, "DSP 交易平台", 22, null, TextAnchor.MiddleLeft);
            AddButton("introduction", 990, 10, 72, 32, "说明", BuildIntroductionPopup);
            AddButton("close", 1081, 10, 40, 32, "X", Close);
            string[] categories = { "原料", "材料", "武器", "消耗品", "矩阵", "建筑币" };
            for (int i = 0; i < categories.Length; i++)
            {
                var category = (MarketCategory)i;
                _categoryButtons.Add(category, AddButton("category-" + i, 22 + i * 126, 58, 116, 28,
                    categories[i], delegate { SelectCategory(category); }));
            }
            _allPositionsButton = AddButton("all-positions", 800, 58, 148, 28, "全部持仓", ShowAllPositions);
            _achievementsButton = AddButton("achievements", 966, 58, 138, 28, "成就", delegate {
                if (_modal != null) return;
                _achievementsMode = true; ClearInputSelection(); _dirty = true; Refresh();
            });
            _sidebarRoot = _ui.Container("sidebar", _root, 0, 0, Width, Height);
            Panel("sidebar-fill", 20, 101, 168, 627, _sidebarRoot);
            _ui.Text("markets-label", _sidebarRoot, 34, 111, 140, 30, "交易", 17, Muted, TextAnchor.MiddleCenter);
            _resourceSidebar = new ResourceSidebarView(_ui, _sidebarRoot, SelectMarket, Refresh);
            _chooseResources = AddButton("choose-resources", 30, 682, 148, 34, "选择资源",
                delegate { _resourceSelectionMode = true; _positionsMode = false; _exchangeMode = false; ClearInputSelection(); _dirty = true; Refresh(); }, null, _sidebarRoot);
            _coinTradeTab = AddButton("coin-trading", 30, 151, 148, 54, "行情交易", delegate { SetExchangeMode(false); }, null, _sidebarRoot);
            _coinTradeTab.GetComponentInChildren<Text>().fontSize = 17;
            string[] tiers = { "一级兑换", "二级兑换", "三级兑换", "四级兑换", "五级兑换" };
            string[] tierColors = { "50A9EA", "E26060", "E8C14A", "AA76D3", "55BC7D" };
            for (int i = 0; i < tiers.Length; i++)
            {
                int tier = i + 1;
                var tierButton = AddButton("coin-exchange-" + tier, 30, 253 + i * 46, 148, 34,
                    tiers[i], delegate { _execute(MarketCommand.SelectExchangeTier, tier); _exchangePage = 0; SetExchangeMode(true); }, null, _sidebarRoot);
                var tierText = tierButton.GetComponentInChildren<Text>(); tierText.supportRichText = false;
                Color tierColor;
                if (ColorUtility.TryParseHtmlString("#" + tierColors[i], out tierColor)) tierText.color = tierColor;
                _exchangeTierButtons.Add(tierButton);
            }
            _tradeRoot = _ui.Container("trading-view", _root, 0, 0, Width, Height);
            BuildTrading();
            _exchangeRoot = _ui.Container("exchange-view", _root, 0, 0, Width, Height);
            BuildExchange();
            _resourcePickerRoot = _ui.Container("resource-selection-view", _root, 0, 0, Width, Height);
            _resourcePicker = new ResourceSelectionView(_ui, _resourcePickerRoot,
                (id, enabled) => { _execute(enabled ? MarketCommand.EnableResource : MarketCommand.DisableResource, id); _dirty = true; Refresh(); },
                LeaveResourceSelection, Refresh);
            _positionsRoot = _ui.Container("all-holdings-view", _root, 0, 0, Width, Height);
            _achievementsRoot = _ui.Container("achievements-view", _root, 0, 0, Width, Height);
            _achievementsView = new AchievementsView(_ui, _achievementsRoot);
            _holdings = new HoldingsView(_ui, _positionsRoot, SelectMarket,
                id => { _execute(MarketCommand.CloseMarketPosition, id); _dirty = true; Refresh(); },
                () => { _execute(MarketCommand.CloseAllPositions, 0); _dirty = true; Refresh(); });
            _status = _ui.Text("status", _root, 22, 738, 1096, 20, "", 11, Cyan);
            FitToParent();
            if (_pendingSettlement != null) BuildSettlementPopup();
        }

        private void BuildTrading()
        {
            var parent = _tradeRoot;
            Panel("chart", 204, 101, 580, 455, parent);
            _chartTitle = _ui.Text("chart-heading", parent, 220, 110, 548, 34, "", 17, null, TextAnchor.MiddleLeft);
            _chartRange = _ui.Text("chart-range", parent, 220, 147, 548, 24, "", 14, Muted);
            var graph = new GameObject("candlestick-chart", typeof(RectTransform), typeof(CanvasRenderer), typeof(MarketCandleGraphic));
            NativeUiFactory.Place((RectTransform)graph.transform, parent, 222, 180, 488, 215);
            _chart = graph.GetComponent<MarketCandleGraphic>();
            _chart.raycastTarget = false;
            for (int i = 0; i < _priceAxis.Length; i++)
                _priceAxis[i] = _ui.Text("price-axis-" + i, parent, 715, 178 + 199 * i / 3f, 55, 20, "", 11, Muted, TextAnchor.MiddleLeft);
            _liquidationBackground = _ui.Image("liquidation-tag", graph.transform, 6, 0, 204, 24, new Color(.035f, .055f, .07f, .95f));
            _liquidationLabel = _ui.Text("liquidation-price", _liquidationBackground.transform, 5, 0, 194, 24, "", 12, Warning, TextAnchor.MiddleLeft);
            _liquidationLabel.horizontalOverflow = HorizontalWrapMode.Overflow;
            _quote = _ui.Text("quote", parent, 220, 409, 548, 30, "", 15);
            _ui.Text("position-heading", parent, 220, 449, 548, 27, "当前持仓", 18, Muted);
            _positionText = _ui.Text("position", parent, 220, 482, 414, 65, "", 16, null, TextAnchor.UpperLeft);
            _positionText.supportRichText = true;
            _positionText.resizeTextForBestFit = true; _positionText.resizeTextMinSize = 13; _positionText.resizeTextMaxSize = 16;
            _positionText.verticalOverflow = VerticalWrapMode.Truncate;
            _closePosition = AddButton("close-position", 648, 510, 118, 34, "平仓", delegate { Execute(MarketCommand.ClosePosition); }, null, parent);

            Panel("order", 800, 101, 320, 455, parent);
            _ui.Text("assets-heading", parent, 816, 112, 288, 28, "账户", 18, Muted, TextAnchor.MiddleLeft);
            _assets = _ui.Text("assets", parent, 816, 147, 288, 51, "", 18, null, TextAnchor.UpperLeft);
            _assets.resizeTextForBestFit = true; _assets.resizeTextMinSize = 12; _assets.resizeTextMaxSize = 18;
            _assets.horizontalOverflow = HorizontalWrapMode.Wrap;
            _assets.verticalOverflow = VerticalWrapMode.Truncate;
            _floating = _ui.Text("floating-pnl", parent, 816, 199, 288, 23, "", 16, null, TextAnchor.MiddleLeft);
            _floatingReturn = _ui.Text("floating-return", parent, 816, 222, 288, 20, "", 14, null, TextAnchor.MiddleLeft);
            _realized = _ui.Text("realized-pnl", parent, 816, 244, 288, 23, "", 16, null, TextAnchor.MiddleLeft);
            _returnRate = _ui.Text("return-rate", parent, 816, 268, 288, 21, "", 16, null, TextAnchor.MiddleLeft);
            foreach (var moneyLine in new[] { _floating, _realized, _returnRate })
            { moneyLine.resizeTextForBestFit = true; moneyLine.resizeTextMinSize = 12; moneyLine.resizeTextMaxSize = 16; moneyLine.verticalOverflow = VerticalWrapMode.Truncate; }
            _allocationLabel = _ui.Text("allocation-heading", parent, 816, 294, 288, 22, "", 16, null, TextAnchor.MiddleLeft);
            _marginPreview = _ui.Text("margin-preview", parent, 816, 319, 288, 24, "", 19, Cyan, TextAnchor.MiddleLeft);
            _allocationSlider = _ui.Slider("allocation", parent, 818, 347, 282, 20, 1, 100, _allocation,
                delegate(float value) { _allocation = Mathf.RoundToInt(value); _localError = ""; _dirty = true; });
            int[] percentages = { 25, 50, 75, 100 };
            for (int i = 0; i < percentages.Length; i++)
            {
                int percent = percentages[i];
                _allocationButtons.Add(AddButton("allocation-" + percent, 816 + i * 73, 375, 65, 26, percent + "%",
                    delegate { _allocationSlider.value = percent; ClearInputSelection(); _dirty = true; }, null, parent));
            }
            _leverageLabel = _ui.Text("leverage-label", parent, 816, 407, 288, 24, "", 16, null, TextAnchor.MiddleLeft);
            _leverageSlider = _ui.Slider("leverage", parent, 818, 438, 282, 24, 1, 100, _selectedLeverage,
                delegate(float value) { _selectedLeverage = Mathf.RoundToInt(value); _execute(MarketCommand.ChangeLeverage, _selectedLeverage); _localError = ""; _dirty = true; });
            int[] leverages = { 2, 5, 10, 50 };
            for (int i = 0; i < leverages.Length; i++)
            {
                int leverage = leverages[i];
                AddButton("leverage-" + leverage, 816 + i * 73, 470, 65, 26, leverage + "×",
                    delegate { _leverageSlider.value = leverage; ClearInputSelection(); _dirty = true; }, null, parent);
            }
            _preview = _ui.Text("order-preview", parent, 816, 500, 288, 19, "", 12, Muted);
            _long = AddButton("long", 816, 521, 136, 28, "做多 / 开多", delegate { Execute(MarketCommand.OpenLong); }, new Color(.45f, .22f, .22f, 1), parent);
            _short = AddButton("short", 966, 521, 138, 28, "做空 / 开空", delegate { Execute(MarketCommand.OpenShort); }, new Color(.18f, .39f, .28f, 1), parent);

            _transferRoot = _ui.Container("item-transfers", parent, 0, 0, Width, Height);
            Panel("transfer", 204, 574, 580, 154, _transferRoot);
            _transferTitle = _ui.Text("transfer-heading", _transferRoot, 220, 584, 270, 28, "", 16, Muted);
            _sourceBackpack = AddButton("source-backpack", 520, 584, 114, 26, "背包", delegate { _execute(MarketCommand.UseBackpack, 0); _dirty = true; Refresh(); }, null, _transferRoot);
            _sourceDelivery = AddButton("source-delivery", 646, 584, 120, 26, "交付平台", delegate { _execute(MarketCommand.UseDelivery, 0); _dirty = true; Refresh(); }, null, _transferRoot);
            _inventoryLabel = _ui.Text("inventory-label", _transferRoot, 220, 617, 280, 20, "背包可用", 12, Muted);
            _ui.Text("withdrawable-label", _transferRoot, 510, 617, 260, 20, "可提取", 12, Muted);
            _backpack = _ui.Text("backpack-available", _transferRoot, 220, 636, 260, 26, "0", 18, Cyan);
            _withdrawable = _ui.Text("withdrawable", _transferRoot, 510, 636, 260, 26, "0", 18);
            _depositSlider = _ui.Slider("deposit-amount", _transferRoot, 220, 662, 256, 18, 0, TransferAmounts.SliderSteps, 0, value => SetTransferSlider(true, value));
            _withdrawSlider = _ui.Slider("withdraw-amount", _transferRoot, 510, 662, 256, 18, 0, TransferAmounts.SliderSteps, 0, value => SetTransferSlider(false, value));
            _depositInput = TransferInput(_transferRoot, 220, 686, 118, _depositValue, value => SetTransferInput(true, value));
            _withdrawInput = TransferInput(_transferRoot, 510, 686, 118, _withdrawValue, value => SetTransferInput(false, value));
            _deposit = AddButton("deposit", 350, 686, 126, 34, "存入", delegate { Execute(MarketCommand.Deposit); }, null, _transferRoot);
            _withdraw = AddButton("withdraw", 640, 686, 126, 34, "提取", delegate { Execute(MarketCommand.Withdraw); }, null, _transferRoot);

            _coinFunding = _ui.Container("coin-funding", parent, 0, 0, Width, Height);
            Panel("coin-balance", 204, 574, 580, 154, _coinFunding);
            _ui.Text("coin-label", _coinFunding, 220, 592, 300, 28, "可用建筑币", 14, Muted);
            _coinBalance = _ui.Text("coin-balance-value", _coinFunding, 220, 634, 340, 46, "", 24, Cyan);
            AddButton("open-exchange", 596, 674, 170, 36, "前往建筑兑换", delegate { SetExchangeMode(true); }, null, _coinFunding);
            Panel("events", 800, 574, 320, 154, parent);
            _ui.Text("events-heading", parent, 816, 584, 288, 28, "操作记录", 16, Muted);
            _events = _ui.Text("events", parent, 816, 616, 288, 108, "", 12, Muted);
            _events.lineSpacing = .9f;
            _events.horizontalOverflow = HorizontalWrapMode.Overflow;
        }

        private void BuildExchange()
        {
            var parent = _exchangeRoot;
            Panel("exchange-list", 204, 101, 580, 627, parent);
            Panel("exchange-detail", 800, 101, 320, 627, parent);
            _ui.Text("exchange-title", parent, 220, 111, 300, 32, "建筑兑换", 18, null, TextAnchor.MiddleLeft);
            _exchangePageText = _ui.Text("exchange-page", parent, 590, 114, 176, 28, "", 13, Muted, TextAnchor.MiddleRight);
            for (int i = 0; i < 28; i++)
            {
                var cell = new ExchangeCell();
                float x = 220 + (i % 4) * 137, y = 155 + (i / 4) * 72;
                cell.Button = AddButton("exchange-item-" + i, x, y, 128, 64, "",
                    delegate { if (cell.ItemId > 0) { _execute(MarketCommand.SelectExchangeItem, cell.ItemId); _dirty = true; Refresh(); } }, null, parent);
                cell.Background = _ui.Image("inventory-background", cell.Button.transform, 3, 3, 122, 58, new Color(.035f, .055f, .075f, 1));
                cell.Background.raycastTarget = false;
                cell.Icon = _ui.Image("icon", cell.Button.transform, 7, 6, 30, 30, Color.white);
                cell.Name = _ui.Text("name", cell.Button.transform, 42, 3, 81, 34, "", 11, null, TextAnchor.MiddleLeft);
                cell.Name.resizeTextForBestFit = true; cell.Name.resizeTextMinSize = 9; cell.Name.resizeTextMaxSize = 11;
                cell.Name.verticalOverflow = VerticalWrapMode.Truncate;
                cell.Value = _ui.Text("value", cell.Button.transform, 7, 40, 114, 20, "", 11, Muted);
                _exchangeCells.Add(cell);
            }
            AddButton("exchange-prev", 220, 680, 100, 32, "上一页", delegate { _exchangePage = Math.Max(0, _exchangePage - 1); _dirty = true; Refresh(); }, null, parent);
            AddButton("exchange-next", 666, 680, 100, 32, "下一页", delegate { _exchangePage++; _dirty = true; Refresh(); }, null, parent);
            _exchangeIcon = _ui.Image("selected-building", parent, 928, 126, 64, 64, Color.white);
            _exchangeIcon.preserveAspect = true;
            _exchangeName = _ui.Text("building-name", parent, 816, 211, 288, 46, "", 19, null, TextAnchor.MiddleCenter);
            _exchangeValue = _ui.Text("building-value", parent, 816, 271, 288, 44, "", 15, Muted);
            _exchangeBackpack = _ui.Text("building-backpack", parent, 816, 329, 288, 32, "", 16);
            _exchangeWallet = _ui.Text("exchange-wallet", parent, 816, 373, 288, 44, "", 17, Cyan);
            _ui.Text("exchange-quantity-heading", parent, 816, 435, 288, 26, "兑换数量", 14, Muted);
            _exchangeQuantityInput = TransferInput(parent, 816, 471, 288, _exchangeQuantityValue,
                delegate(string value) { _exchangeQuantityValue = value; });
            _exchangeQuantitySlider = _ui.Slider("exchange-quantity", parent, 816, 507, 288, 18, 0, TransferAmounts.SliderSteps, 0,
                delegate(float value) {
                    _exchangeQuantityValue = TransferAmounts.FromSlider(Mathf.RoundToInt(value), _exchangeMaximum).ToString(CultureInfo.InvariantCulture);
                    _exchangeQuantityInput.SetTextWithoutNotify(_exchangeQuantityValue); _localError = ""; _dirty = true;
                });
            _exchangeTotal = _ui.Text("exchange-total", parent, 816, 527, 288, 78, "", 15);
            _exchangeBuy = AddButton("exchange-buy", 816, 622, 288, 36, "建筑币 → 物品", delegate { Execute(MarketCommand.BuyBuilding); }, null, parent);
            _exchangeSell = AddButton("exchange-sell", 816, 676, 288, 36, "物品 → 建筑币", delegate { Execute(MarketCommand.SellBuilding); }, new Color(.24f, .36f, .33f, 1), parent);
        }

        private InputField TransferInput(Transform parent, float x, float y, float width, string initial, Action<string> save)
        {
            var input = _ui.QuantityInput(parent, x, y, width, 34);
            input.text = initial;
            input.onValueChanged.AddListener(delegate(string value) { save(value); _localError = ""; _dirty = true; });
            input.onEndEdit.AddListener(OnInputEnded);
            return input;
        }

        private void Refresh()
        {
            _achievementsRoot.gameObject.SetActive(_achievementsMode);
            _achievementsButton.image.color = _achievementsMode ? new Color(.25f, .43f, .51f, 1) : new Color(.14f, .24f, .29f, 1);
            if (_achievementsMode)
            {
                _tradeRoot.gameObject.SetActive(false); _exchangeRoot.gameObject.SetActive(false);
                _resourcePickerRoot.gameObject.SetActive(false); _sidebarRoot.gameObject.SetActive(false);
                _positionsRoot.gameObject.SetActive(false);
                _allPositionsButton.image.color = new Color(.14f, .24f, .29f, 1);
                _status.text = ""; _achievementsView.Refresh(_achievements()); return;
            }
            MarketSnapshot state;
            try { state = _snapshot(_allocation); }
            catch (Exception exception)
            {
                _status.text = "读取失败：" + exception.Message;
                _status.color = Warning;
                SetCommandsEnabled(false, false, false);
                _closePosition.interactable = false;
                return;
            }
            if (state == null) { _status.text = "市场尚未载入"; SetCommandsEnabled(false, false, false); _closePosition.interactable = false; return; }
            _lastState = state;
            string unit = state.ItemName;
            _chartTitle.text = unit;
            _transferTitle.text = unit + "存取";
            var category = _categoryOverride ?? MarketCatalog.Category(state.ItemId);
            bool resourceCategory = MarketCatalog.IsSelectableCategory(category);
            bool coin = category == MarketCategory.BuildingCurrency;
            if (!coin) _exchangeMode = false;
            if (!resourceCategory) _resourceSelectionMode = false;
            if (resourceCategory && (!state.MarketEnabled || MarketCatalog.Category(state.ItemId) != category) && !_positionsMode)
                _resourceSelectionMode = true;
            _tradeRoot.gameObject.SetActive(!_exchangeMode && !_positionsMode && !_resourceSelectionMode);
            _exchangeRoot.gameObject.SetActive(_exchangeMode && !_positionsMode && !_resourceSelectionMode);
            _resourcePickerRoot.gameObject.SetActive(_resourceSelectionMode && !_positionsMode);
            _resourceSidebar.Root.gameObject.SetActive(resourceCategory);
            _chooseResources.gameObject.SetActive(resourceCategory);
            if (resourceCategory) _chooseResources.GetComponentInChildren<Text>().text = "选择" + MarketCatalog.CategoryName(category);
            if (resourceCategory) _resourceSidebar.Refresh(state, category);
            _sidebarRoot.gameObject.SetActive(!_positionsMode);
            _positionsRoot.gameObject.SetActive(_positionsMode);
            _allPositionsButton.image.color = _positionsMode ? new Color(.25f, .43f, .51f, 1) : new Color(.14f, .24f, .29f, 1);
            _transferRoot.gameObject.SetActive(!coin);
            _coinFunding.gameObject.SetActive(coin);
            _coinBalance.text = Amount(state.BuildingCoinBalance);
            _coinTradeTab.gameObject.SetActive(coin);
            for (int i = 0; i < _exchangeTierButtons.Count; i++)
            {
                var button = _exchangeTierButtons[i];
                button.gameObject.SetActive(coin);
                button.image.color = _exchangeMode && state.ExchangeTier == i + 1 ? new Color(.25f, .43f, .51f, 1) : new Color(.14f, .24f, .29f, 1);
            }
            foreach (var pair in _categoryButtons)
                pair.Value.image.color = !_positionsMode && pair.Key == category ? new Color(.25f, .43f, .51f, 1) : new Color(.14f, .24f, .29f, 1);
            foreach (var pair in _marketButtons)
            {
                pair.Value.gameObject.SetActive(MarketCatalog.Category(pair.Key) == category);
                pair.Value.image.color = pair.Key == state.ItemId ? new Color(.25f, .43f, .51f, 1) : new Color(.14f, .24f, .29f, 1);
            }
            foreach (var option in state.Markets)
            {
                Image icon;
                if (_marketIcons.TryGetValue(option.ItemId, out icon)) { icon.sprite = option.Icon; icon.enabled = option.Icon != null; }
            }
            _assets.text = "可用　　" + Amount(state.AvailableBalance) + "\n总权益　" + Amount(state.Equity);
            _floating.text = "浮动盈亏    " + Signed(state.UnrealizedPnL);
            _floatingReturn.text = "收益率    " + MoneyDisplay.Percent(state.UnrealizedReturnRatio);
            _realized.text = "累计盈亏    " + Signed(state.RealizedPnL);
            _returnRate.text = "收益率    " + MoneyDisplay.Percent(state.ReturnRatio);
            _floating.color = PnLColor(state.UnrealizedPnL);
            _floatingReturn.color = state.UnrealizedReturnRatio.HasValue ? PnLColor(state.UnrealizedPnL) : Muted;
            _realized.color = PnLColor(state.RealizedPnL);
            _returnRate.color = state.ReturnRatio.HasValue ? PnLColor(state.ReturnRatio.Value) : Muted;
            _quote.text = "买入 " + Price(state.Ask) + "     卖出 " + Price(state.Bid)
                + "     费率 " + (state.FeeRate * 100m).ToString("0.00") + "%";
            _allocationLabel.text = "仓位比例   " + _allocation + "%";
            _selectedLeverage = state.SelectedLeverage;
            _leverageLabel.text = "杠杆   " + _selectedLeverage + "×";
            _leverageSlider.SetValueWithoutNotify(_selectedLeverage);
            _marginPreview.text = "保证金  " + Amount(state.OrderMargin);
            _preview.text = "开仓费 " + Amount(state.OpenFee);
            _positionText.text = state.HasPosition
                ? NativeUiFactory.DirectionText(state.PositionDirection) + "  " + state.PositionLeverage + "×   入场 " + Price(state.EntryPrice)
                    + "\n保证金 " + Amount(state.PositionMargin) + "    爆仓价 " + Price(state.LiquidationPrice)
                    + "\n持有时间 " + state.HeldCandles.Value.ToString("N0", CultureInfo.InvariantCulture)
                : "空仓";
            _backpack.text = state.TransferAvailable.ToString("N0");
            _inventoryLabel.text = state.ItemId == 1099 ? "沙土可用" : state.UseDelivery ? (state.DeliveryBound ? "平台可用 · " + state.DeliveryName : "请在交付塔中设置该物品") : "背包可用";
            _sourceBackpack.GetComponentInChildren<Text>().text = state.ItemId == 1099 ? "沙土" : "背包";
            _sourceDelivery.interactable = state.CanUseDelivery;
            _sourceBackpack.image.color = !state.UseDelivery ? new Color(.25f, .43f, .51f, 1) : new Color(.14f, .24f, .29f, 1);
            _sourceDelivery.image.color = state.UseDelivery ? new Color(.25f, .43f, .51f, 1) : new Color(.14f, .24f, .29f, 1);
            _withdrawable.text = decimal.Floor(state.AvailableBalance).ToString("N0");
            int depositMaximum = TransferAmounts.Maximum(state.TransferAvailable);
            int withdrawMaximum = TransferAmounts.Maximum((System.Numerics.BigInteger)decimal.Floor(state.AvailableBalance));
            if (state.ItemId != MarketCatalog.BuildingCoin)
            {
                bool changed = _transferContextItem != state.ItemId || _transferContextDelivery != state.UseDelivery;
                _depositSelection.Refresh(depositMaximum, changed); _withdrawSelection.Refresh(withdrawMaximum, changed);
                if (changed)
                {
                    _depositValue = _depositSelection.Quantity.ToString(CultureInfo.InvariantCulture);
                    _withdrawValue = _withdrawSelection.Quantity.ToString(CultureInfo.InvariantCulture);
                    _depositInput.SetTextWithoutNotify(_depositValue); _withdrawInput.SetTextWithoutNotify(_withdrawValue);
                }
                _transferContextItem = state.ItemId; _transferContextDelivery = state.UseDelivery;
            }
            _depositSlider.SetValueWithoutNotify(_depositSelection.Step);
            _withdrawSlider.SetValueWithoutNotify(_withdrawSelection.Step);
            _depositSlider.interactable = state.Ready && depositMaximum > 0;
            _withdrawSlider.interactable = state.Ready && withdrawMaximum > 0 && (!state.UseDelivery || state.DeliveryBound);
            _status.text = TradingLogDisplay.Format(!string.IsNullOrEmpty(_localError) ? _localError : state.Status);
            _status.color = state.Ready && string.IsNullOrEmpty(_localError) ? Cyan : Warning;
            _events.text = EventText(state.RecentEvents);
            _chart.SetCandles(state.Candles, state.LiquidationPrice);
            for (int i = 0; i < _priceAxis.Length; i++)
                _priceAxis[i].text = state.Candles.Count == 0 ? "" : Price((decimal)Mathf.Lerp(_chart.Maximum, _chart.Minimum, i / 3f));
            _chartRange.text = state.Candles == null || state.Candles.Count == 0 ? ""
                : "最新 " + Price(state.Candles[state.Candles.Count - 1].Close);
            _liquidationBackground.gameObject.SetActive(state.HasPosition);
            if (state.HasPosition)
            {
                float ratio = _chart.LiquidationRatio;
                _liquidationLabel.text = "爆仓价 " + Price(state.LiquidationPrice) + (ratio < 0 ? " ↓" : ratio > 1 ? " ↑" : "");
                float labelWidth = _liquidationLabel.preferredWidth;
                _liquidationLabel.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, labelWidth);
                ((RectTransform)_liquidationBackground.transform).SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, labelWidth + 10);
                float tagY = Mathf.Clamp(215 - (8 + 199 * Mathf.Clamp01(ratio)) - 25, 0, 191);
                ((RectTransform)_liquidationBackground.transform).anchoredPosition = new Vector2(6, -tagY);
            }
            int deposit, withdraw;
            bool canOpen = state.Ready && !state.HasPosition && state.OrderMargin > 0 && state.AvailableBalance >= state.OpenCost;
            SetCommandsEnabled(canOpen,
                state.Ready && TryTransfer(_depositInput, out deposit) && state.TransferAvailable >= deposit,
                state.Ready && (!state.UseDelivery || state.DeliveryBound) && TryTransfer(_withdrawInput, out withdraw) && state.AvailableBalance >= withdraw);
            _closePosition.interactable = state.Ready && state.HasPosition;
            _allocationSlider.interactable = state.Ready;
            _leverageSlider.interactable = state.Ready;
            foreach (var button in _allocationButtons) button.interactable = state.Ready;
            if (_exchangeMode && !_positionsMode) RefreshExchange(state);
            if (_positionsMode) _holdings.Refresh(_portfolio());
            if (_resourceSelectionMode && !_positionsMode) _resourcePicker.Refresh(state, category);
        }

        private void SelectMarket(int itemId)
        {
            if (_modal != null) return;
            _achievementsMode = false;
            ClearInputSelection();
            _positionsMode = false;
            _categoryOverride = null;
            _resourceSelectionMode = false;
            if (itemId != MarketCatalog.BuildingCoin) _exchangeMode = false;
            _execute(MarketCommand.SelectMarket, itemId);
            _localError = "";
            _dirty = true;
            Refresh();
        }

        private void Execute(MarketCommand command)
        {
            if (_modal != null) return;
            int argument = _allocation;
            if (command == MarketCommand.ClosePosition) argument = 0;
            else if (command == MarketCommand.BuyBuilding || command == MarketCommand.SellBuilding)
            {
                if (!TryTransfer(_exchangeQuantityInput, out argument)) { _localError = "兑换数量须为正整数"; _dirty = true; return; }
            }
            else if (command == MarketCommand.Deposit || command == MarketCommand.Withdraw)
            {
                if (!TryTransfer(command == MarketCommand.Deposit ? _depositInput : _withdrawInput, out argument))
                { _localError = "存取数量须为正整数"; _dirty = true; return; }
            }
            ClearInputSelection();
            try { _execute(command, argument); _localError = ""; }
            catch (Exception exception)
            {
                _localError = "操作未完成：" + exception.Message;
                Debug.LogError("[FXDSP] Market command failed: " + exception);
            }
            _dirty = true;
            Refresh();
        }

        private void BuildIntroductionPopup()
        {
            DestroyModal(); ClearInputSelection();
            var shade = _ui.Image("introduction-overlay", _ui.Parent, 0, 0, Width, Height, new Color(0, 0, 0, .72f));
            var overlay = (RectTransform)shade.transform;
            overlay.anchorMin = Vector2.zero; overlay.anchorMax = Vector2.one;
            overlay.pivot = new Vector2(.5f, .5f); overlay.sizeDelta = Vector2.zero; overlay.anchoredPosition = Vector2.zero;
            overlay.SetAsLastSibling(); shade.raycastTarget = true;
            shade.gameObject.AddComponent<SettlementDismissHandle>().Dismiss = DismissSettlement;
            _modal = shade.gameObject;
            var card = _ui.Image("introduction-card", shade.transform, 0, 0, 900, 720, new Color(.045f, .075f, .095f, 1));
            var rect = (RectTransform)card.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f); rect.anchoredPosition = Vector2.zero;
            rect.localScale = Vector3.one * Mathf.Max(.1f, Mathf.Min(1, Mathf.Min(overlay.rect.width / 940, overlay.rect.height / 760)));
            card.raycastTarget = true;
            // Absorb clicks within the card so reading and scrolling do not dismiss it.
            card.gameObject.AddComponent<SettlementDismissHandle>();
            _ui.Text("introduction-title", card.transform, 40, 22, 820, 42, IntroductionCopy.Title, 24, Cyan, TextAnchor.MiddleCenter);
            _ui.Image("introduction-divider", card.transform, 32, 75, 836, 1, new Color(.4f, .82f, .85f, .3f));

            const float bodyWidth = 824, viewportHeight = 548;
            var viewport = _ui.Image("introduction-viewport", card.transform, 32, 92, bodyWidth, viewportHeight, Color.clear);
            viewport.raycastTarget = true;
            viewport.gameObject.AddComponent<RectMask2D>();
            var body = _ui.Text("introduction-body", viewport.transform, 0, 0, bodyWidth, viewportHeight, IntroductionCopy.Body, 18);
            body.supportRichText = true;
            // DSP's mesh text path recognizes color tags but displays UGUI <b> tags literally.
            body.supportColorText = true;
            body.lineSpacing = 1.2f;
            float contentHeight = Mathf.Max(viewportHeight, Mathf.Ceil(body.preferredHeight) + 8);
            body.rectTransform.sizeDelta = new Vector2(bodyWidth, contentHeight);
            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = (RectTransform)viewport.transform;
            scroll.content = body.rectTransform;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 28;
            var track = _ui.Image("introduction-scrollbar", card.transform, 864, 92, 6, viewportHeight, new Color(.13f, .22f, .26f, 1));
            track.raycastTarget = true;
            var handle = _ui.Image("handle", track.transform, 0, 0, 6, viewportHeight, new Color(.4f, .72f, .78f, 1));
            var handleRect = (RectTransform)handle.transform;
            handleRect.anchorMin = Vector2.zero; handleRect.anchorMax = Vector2.one;
            handleRect.pivot = new Vector2(.5f, .5f); handleRect.sizeDelta = handleRect.anchoredPosition = Vector2.zero;
            var scrollbar = track.gameObject.AddComponent<Scrollbar>();
            scrollbar.direction = Scrollbar.Direction.BottomToTop;
            scrollbar.handleRect = handleRect; scrollbar.targetGraphic = handle;
            scrollbar.navigation = new Navigation { mode = Navigation.Mode.None };
            scroll.verticalScrollbar = scrollbar;
            track.gameObject.SetActive(contentHeight > viewportHeight);
            scroll.verticalNormalizedPosition = 1;
            AddButton("introduction-close", 840, 12, 40, 32, "X", DismissSettlement, null, card.transform);
            AddButton("introduction-start", 326, 664, 248, 36, "进入交易平台", DismissSettlement, null, card.transform);
        }

        private void BuildSettlementPopup()
        {
            DestroyModal();
            if (_root == null || _pendingSettlement == null) return;
            ClearInputSelection();
            var result = _pendingSettlement;
            string unit = MarketCatalog.Name(result.ItemId);
            bool liquidated = result.Liquidated;
            Color accent = liquidated ? new Color(.85f, .53f, .27f, 1) : new Color(.34f, .68f, .75f, 1);
            var shade = _ui.Image("settlement-overlay", _ui.Parent, 0, 0, Width, Height, new Color(0, 0, 0, .72f));
            var overlayRect = (RectTransform)shade.transform;
            overlayRect.anchorMin = Vector2.zero; overlayRect.anchorMax = Vector2.one;
            overlayRect.pivot = new Vector2(.5f, .5f); overlayRect.sizeDelta = Vector2.zero; overlayRect.anchoredPosition = Vector2.zero;
            overlayRect.SetAsLastSibling();
            shade.raycastTarget = true;
            shade.gameObject.AddComponent<SettlementDismissHandle>().Dismiss = DismissSettlement;
            _modal = shade.gameObject;
            var card = _ui.Image("settlement-card", shade.transform, 0, 0, 500, 330,
                liquidated ? new Color(.10f, .065f, .055f, 1) : new Color(.045f, .075f, .095f, 1));
            var cardRect = (RectTransform)card.transform;
            cardRect.anchorMin = cardRect.anchorMax = cardRect.pivot = new Vector2(.5f, .5f);
            cardRect.anchoredPosition = Vector2.zero;
            float scale = Mathf.Min(1, Mathf.Min(overlayRect.rect.width / 540, overlayRect.rect.height / 370));
            cardRect.localScale = Vector3.one * Mathf.Max(.1f, scale);
            card.raycastTarget = true;
            _ui.Text("heading", card.transform, 24, 18, 452, 48,
                liquidated ? "持仓已爆仓" : "平仓结算", 24, accent, TextAnchor.MiddleCenter);
            _ui.Text("direction", card.transform, 24, 68, 452, 30,
                unit + "    " + (result.Side == PositionSide.Long ? "做多" : "做空") + "  " + result.Leverage + "×", 14, Muted, TextAnchor.MiddleCenter);
            _ui.Image("pnl-area", card.transform, 24, 112, 452, 112,
                liquidated ? new Color(.20f, .115f, .065f, 1) : new Color(.065f, .12f, .145f, 1));
            _ui.Text("pnl-label", card.transform, 36, 120, 428, 26, liquidated ? "爆仓净损益" : "本次净盈亏", 13, Muted, TextAnchor.MiddleCenter);
            var pnl = _ui.Text("pnl-value", card.transform, 36, 147, 428, 42,
                Signed(result.NetPnL) + " " + unit, 28, liquidated ? accent : PnLColor(result.NetPnL), TextAnchor.MiddleCenter);
            pnl.resizeTextForBestFit = true; pnl.resizeTextMinSize = 17; pnl.resizeTextMaxSize = 28;
            _ui.Text("settlement-return", card.transform, 36, 192, 428, 25,
                "收益率 " + MoneyDisplay.Percent(result.ReturnRatio), 15, liquidated ? accent : PnLColor(result.NetPnL), TextAnchor.MiddleCenter);
            _ui.Text("fee-label", card.transform, 24, 244, 216, 24, "费用合计", 12, Muted, TextAnchor.MiddleCenter);
            _ui.Text("returned-label", card.transform, 260, 244, 216, 24, "返还余额", 12, Muted, TextAnchor.MiddleCenter);
            _ui.Text("fee-value", card.transform, 24, 274, 216, 30, Amount(result.Fees), 17, null, TextAnchor.MiddleCenter);
            _ui.Text("returned-value", card.transform, 260, 274, 216, 30, Amount(result.Returned), 17, null, TextAnchor.MiddleCenter);
        }

        private void SelectCategory(MarketCategory category)
        {
            if (_modal != null) return;
            _achievementsMode = false;
            _positionsMode = false; _exchangeMode = false;
            if (MarketCatalog.IsSelectableCategory(category))
            {
                _categoryOverride = category;
                if (!_resourceSelectionMode && _lastState != null)
                    foreach (var option in _lastState.ResourceOptions)
                        if (option.Enabled && MarketCatalog.Category(option.ItemId) == category) { SelectMarket(option.ItemId); return; }
                _resourceSelectionMode = true;
                ClearInputSelection(); _dirty = true; Refresh();
                return;
            }
            _categoryOverride = null;
            _resourceSelectionMode = false;
            SelectMarket(category == MarketCategory.Matrices ? MarketCatalog.Matrix : MarketCatalog.BuildingCoin);
        }

        private void LeaveResourceSelection()
        {
            if (_lastState == null) return;
            var category = _categoryOverride ?? MarketCatalog.Category(_lastState.ItemId);
            foreach (var option in _lastState.ResourceOptions)
                if (option.Enabled && MarketCatalog.Category(option.ItemId) == category) { SelectMarket(option.ItemId); return; }
        }

        private void SetExchangeMode(bool exchange)
        {
            if (_modal != null) return;
            _achievementsMode = false;
            ClearInputSelection();
            _positionsMode = false;
            _resourceSelectionMode = false;
            _categoryOverride = null;
            _exchangeMode = exchange;
            _dirty = true;
            Refresh();
        }

        private void RefreshExchange(MarketSnapshot state)
        {
            int pages = Math.Max(1, (state.ExchangeOffers.Count + _exchangeCells.Count - 1) / _exchangeCells.Count);
            _exchangePage = Math.Max(0, Math.Min(pages - 1, _exchangePage));
            _exchangePageText.text = (_exchangePage + 1) + " / " + pages;
            ExchangeOffer selected = null;
            foreach (var offer in state.ExchangeOffers) if (offer.ItemId == state.ExchangeItemId) { selected = offer; break; }
            for (int i = 0; i < _exchangeCells.Count; i++)
            {
                var cell = _exchangeCells[i];
                int index = _exchangePage * _exchangeCells.Count + i;
                bool visible = index < state.ExchangeOffers.Count;
                cell.Button.gameObject.SetActive(visible);
                if (!visible) { cell.ItemId = 0; continue; }
                var offer = state.ExchangeOffers[index];
                cell.ItemId = offer.ItemId;
                cell.Icon.sprite = offer.Icon; cell.Icon.enabled = offer.Icon != null;
                cell.Name.text = offer.Name; cell.Value.text = offer.UnitValue.ToString("N0") + " 建筑币";
                int owned;
                bool available = state.ExchangeUnlocked && state.ExchangeInventory.TryGetValue(offer.ItemId, out owned) && owned > 0;
                bool selectedCell = offer.ItemId == state.ExchangeItemId;
                cell.Button.image.color = selectedCell ? new Color(.35f, .59f, .68f, 1) : new Color(.10f, .16f, .20f, 1);
                cell.Background.color = selectedCell ? new Color(.24f, .43f, .51f, 1)
                    : available ? new Color(.13f, .25f, .31f, 1) : new Color(.035f, .055f, .075f, 1);
            }
            bool canBuy = false, canSell = false;
            _exchangeWallet.text = "可用建筑币\n" + Amount(state.BuildingCoinBalance);
            _exchangeBackpack.text = "背包可用  " + state.ExchangeBackpackQuantity.ToString("N0");
            _exchangeName.text = selected == null ? "" : selected.Name;
            _exchangeIcon.sprite = selected == null ? null : selected.Icon;
            _exchangeIcon.enabled = _exchangeIcon.sprite != null;
            _exchangeValue.text = selected == null ? "" : "1 件 = " + selected.UnitValue.ToString("N0") + " 建筑币";
            _exchangeTotal.fontSize = state.ExchangeUnlocked ? 15 : 21;
            _exchangeTotal.color = state.ExchangeUnlocked ? new Color(.78f, .84f, .85f, 1) : MarketCandleGraphic.Rising;
            _exchangeTotal.alignment = state.ExchangeUnlocked ? TextAnchor.UpperLeft : TextAnchor.MiddleCenter;
            _exchangeMaximum = selected == null || !state.ExchangeUnlocked ? 0
                : ExchangeAmounts.Maximum(state.BuildingCoinBalance, selected.UnitValue, state.ExchangeFeeRate, state.ExchangeBackpackQuantity);
            int sliderQuantity; int.TryParse(_exchangeQuantityInput.text, out sliderQuantity);
            _exchangeQuantitySlider.SetValueWithoutNotify(TransferAmounts.ToSlider(sliderQuantity, _exchangeMaximum));
            _exchangeQuantitySlider.interactable = state.Ready && _exchangeMaximum > 0;
            if (!state.ExchangeUnlocked)
            { _exchangeTotal.text = "需研究：建筑兑换（" + state.ExchangeTier + "）"; SetExchangeEnabled(false, false); return; }
            int quantity;
            if (selected == null || !TryTransfer(_exchangeQuantityInput, out quantity))
            { _exchangeTotal.text = "请输入兑换数量"; SetExchangeEnabled(false, false); return; }
            try
            {
                long cost = BuildingExchangeRules.Cost(selected.UnitValue, quantity);
                decimal fee = BuildingExchangeRules.Fee(cost, state.ExchangeFeeRate);
                _exchangeTotal.text = "手续费 " + (state.ExchangeFeeRate * 100m).ToString("0.00") + "%  " + Amount(fee)
                    + "\n买入需 " + Amount(cost + fee) + " 建筑币\n回收得 " + Amount(cost - fee) + " 建筑币";
                canBuy = state.Ready && state.BuildingCoinBalance >= cost + fee;
                canSell = state.Ready && state.ExchangeBackpackQuantity >= quantity;
            }
            catch (OverflowException) { _exchangeTotal.text = "兑换数量过大"; }
            SetExchangeEnabled(canBuy, canSell);
        }

        private void SetExchangeEnabled(bool buy, bool sell)
        {
            if (_exchangeBuy.interactable != buy) _exchangeBuy.interactable = buy;
            if (_exchangeSell.interactable != sell) _exchangeSell.interactable = sell;
        }

        private void DismissSettlement()
        {
            _pendingSettlement = _settlements.Count > 0 ? _settlements.Dequeue() : null;
            DestroyModal();
            if (_pendingSettlement != null && IsOpen) BuildSettlementPopup();
            _dirty = true;
        }

        private void DestroyModal()
        {
            if (_modal != null)
            {
                foreach (var button in _modal.GetComponentsInChildren<Button>(true))
                { button.onClick.RemoveAllListeners(); _buttons.Remove(button); }
                _modal.SetActive(false); UnityEngine.Object.Destroy(_modal);
            }
            _modal = null;
        }

        private void SetCommandsEnabled(bool open, bool deposit, bool withdraw)
        {
            if (_long != null) _long.interactable = open;
            if (_short != null) _short.interactable = open;
            if (_deposit != null) _deposit.interactable = deposit;
            if (_withdraw != null) _withdraw.interactable = withdraw;

        }

        private static bool TryTransfer(InputField input, out int quantity)
        { return int.TryParse(input == null ? "" : input.text, NumberStyles.None, CultureInfo.InvariantCulture, out quantity) && quantity > 0; }

        private void SetTransferSlider(bool deposit, float value)
        {
            if (_lastState == null) return;
            int maximum = TransferAmounts.Maximum(deposit ? _lastState.TransferAvailable : (System.Numerics.BigInteger)decimal.Floor(_lastState.AvailableBalance));
            var selection = deposit ? _depositSelection : _withdrawSelection;
            selection.SetSlider(Mathf.RoundToInt(value), maximum);
            string amount = selection.Quantity.ToString(CultureInfo.InvariantCulture);
            if (deposit) { _depositValue = amount; _depositInput.SetTextWithoutNotify(amount); }
            else { _withdrawValue = amount; _withdrawInput.SetTextWithoutNotify(amount); }
            _localError = ""; _dirty = true;
        }

        private void SetTransferInput(bool deposit, string value)
        {
            if (deposit) _depositValue = value; else _withdrawValue = value;
            int quantity;
            if (_lastState == null || !int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out quantity)) return;
            int maximum = TransferAmounts.Maximum(deposit ? _lastState.TransferAvailable : (System.Numerics.BigInteger)decimal.Floor(_lastState.AvailableBalance));
            (deposit ? _depositSelection : _withdrawSelection).SetInput(quantity, maximum);
        }

        private void OnInputEnded(string value)
        { _inputEndedFrame = Time.frameCount; _clearSelection = true; _dirty = true; }

        private void ClearInputSelection()
        {
            var system = EventSystem.current;
            foreach (var input in new[] { _depositInput, _withdrawInput, _exchangeQuantityInput })
            {
                if (input == null) continue;
                input.DeactivateInputField();
                if (system != null && !system.alreadySelecting && system.currentSelectedGameObject == input.gameObject)
                    system.SetSelectedGameObject(null);
            }
        }

        private void FitToParent()
        {
            if (_root == null) return;
            var parent = _root.parent as RectTransform;
            if (parent == null || parent.rect.width <= 0 || parent.rect.height <= 0) return;
            float scale = Mathf.Max(.1f, Mathf.Min(1f, Mathf.Min((parent.rect.width - 32) / Width, (parent.rect.height - 32) / Height)));
            _root.localScale = new Vector3(scale, scale, 1);
            MarketDragHandle.Clamp(_root);
        }

        private void Panel(string name, float x, float y, float width, float height, Transform parent = null)
        {
            _ui.Image(name, parent ?? _root, x, y, width, height, new Color(.03f, .055f, .07f, .78f));
            _ui.Image(name + "-line", parent ?? _root, x, y, width, 1, new Color(.32f, .58f, .67f, .3f));
        }

        private Button AddButton(string name, float x, float y, float width, float height, string label,
            UnityEngine.Events.UnityAction action, Color? color = null, Transform parent = null)
        {
            var button = _ui.Button(name, parent ?? _root, x, y, width, height, label, action, color);
            _buttons.Add(button);
            return button;
        }

        private static string Amount(decimal value) { return value.ToString("#,0.00"); }
        private static string Signed(decimal value) { return MoneyDisplay.Signed(value); }
        private static Color PnLColor(decimal value)
        { return value > 0 ? MarketCandleGraphic.Rising : value < 0 ? MarketCandleGraphic.Falling : Muted; }
        private static string Price(decimal value) { return value.ToString("0.0000", CultureInfo.InvariantCulture); }
        private string EventText(string[] events)
        {
            if (events == null || events.Length == 0) return "";
            int length = Math.Min(6, events.Length);
            var lines = new string[length];
            for (int i = 0; i < length; i++)
            {
                string line = TradingLogDisplay.Format(events[i]);
                int separator = line.IndexOf('；');
                if (separator > 0 && (line.StartsWith("存入 ") || line.StartsWith("提取 ")))
                    line = line.Substring(0, separator) + "。";
                lines[i] = FitRecordLine(TextLocalization.Translate(line));
            }
            return string.Join("\n", lines);
        }

        private string FitRecordLine(string line)
        {
            var settings = _events.GetGenerationSettings(new Vector2(0, 0));
            settings.horizontalOverflow = HorizontalWrapMode.Overflow; settings.verticalOverflow = VerticalWrapMode.Overflow;
            float width = _events.rectTransform.rect.width * _events.pixelsPerUnit;
            var generator = _events.cachedTextGeneratorForLayout;
            if (generator.GetPreferredWidth(line, settings) <= width) return line;
            int low = 0, high = line.Length;
            while (low < high)
            {
                int middle = (low + high + 1) / 2;
                if (generator.GetPreferredWidth(line.Substring(0, middle) + "…", settings) <= width) low = middle; else high = middle - 1;
            }
            return line.Substring(0, low) + "…";
        }
    }
}
