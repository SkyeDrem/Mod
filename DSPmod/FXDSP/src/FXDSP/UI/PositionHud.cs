using System;
using System.Collections.Generic;
using FXDSP.Runtime;
using UnityEngine;
using UnityEngine.UI;

namespace FXDSP.UI
{
    internal sealed class PositionHud : IDisposable
    {
        private readonly Func<PortfolioSnapshot> _snapshot;
        private readonly Action<MarketCommand, int> _execute;
        private readonly Action _details;
        private readonly Action _exchange;
        private readonly Func<int> _readMode;
        private readonly Action<int> _writeMode;
        private readonly List<Button> _buttons = new List<Button>();
        private readonly List<HudRow> _rows = new List<HudRow>();
        private RectTransform _root;
        private Image _background;
        private Button _hide, _restore, _openDetails, _openExchange;
        private Text _heading, _overflow;
        private float _nextRefresh;
        private int _mode = -1;
        private bool _faulted;
        private sealed class HudRow
        {
            internal int ItemId;
            internal Image Icon;
            internal Text Marker, PnL, Return;
            internal Button Close;
            internal HoldingActionTarget CloseTarget;
        }

        internal PositionHud(Func<PortfolioSnapshot> snapshot, Action<MarketCommand, int> execute,
            Action details, Action exchange, Func<int> readMode, Action<int> writeMode)
        { _snapshot = snapshot; _execute = execute; _details = details; _exchange = exchange; _readMode = readMode; _writeMode = writeMode; }

        internal void Tick(bool visible)
        {
            if (!visible || _faulted) { if (_root != null) _root.gameObject.SetActive(false); return; }
            if (UIRoot.instance == null || UIRoot.instance.uiGame == null || UIRoot.instance.uiGame.inserterWindow == null) return;
            try
            {
                if (_root == null) Build();
                _root.gameObject.SetActive(true);
                if (_root.GetSiblingIndex() != _root.parent.childCount - 1) _root.SetAsLastSibling();
                int mode = _readMode() == 2 ? 2 : 0;
                if (mode == _mode && Time.unscaledTime < _nextRefresh) return;
                _nextRefresh = Time.unscaledTime + .2f;
                Refresh(_snapshot(), mode);
            }
            catch (Exception error)
            {
                _faulted = true;
                if (_root != null) _root.gameObject.SetActive(false);
                Debug.LogWarning("[FXDSP] Holdings HUD unavailable: " + error);
            }
        }

        private void Build()
        {
            Dispose();
            _mode = -1;
            _nextRefresh = 0;
            var ui = new NativeUiFactory();
            _root = ui.Root("DSP-holdings-hud", 128, 100);
            _root.anchorMin = _root.anchorMax = _root.pivot = Vector2.one;
            _root.anchoredPosition = new Vector2(-18, -120);
            _background = ui.Image("background", _root, 0, 0, 128, 100, new Color(.035f, .065f, .085f, .96f));
            _background.raycastTarget = true;
            _heading = ui.Text("heading", _root, 6, 2, 70, 26, "持仓", 13, null, TextAnchor.MiddleLeft);
            _hide = Add(ui.Button("hide", _root, 106, 3, 40, 24, "隐藏", delegate { _writeMode(2); _nextRefresh = 0; }));
            _restore = Add(ui.Button("restore", _root, 96, 0, 56, 26, "持仓", delegate { _writeMode(0); _nextRefresh = 0; }));
            for (int i = 0; i < 6; i++)
            {
                var row = new HudRow();
                row.Icon = ui.Image("market-icon-" + i, _root, 6, 32, 22, 24, Color.white);
                row.Icon.preserveAspect = true;
                row.Marker = ui.Text("currency-" + i, _root, 6, 32, 22, 24, "币", 12, null, TextAnchor.MiddleCenter);
                row.PnL = ui.Text("pnl-" + i, _root, 34, 32, 58, 17, "", 12, null, TextAnchor.MiddleLeft);
                row.Return = ui.Text("return-" + i, _root, 34, 49, 58, 14, "", 10, null, TextAnchor.MiddleLeft);
                row.Return.resizeTextForBestFit = true; row.Return.resizeTextMinSize = 9; row.Return.resizeTextMaxSize = 10;
                row.Return.verticalOverflow = VerticalWrapMode.Truncate;
                row.Close = Add(ui.Button("close-" + i, _root, 98, 32, 48, 24, "平仓",
                    delegate { int id = row.CloseTarget.Consume(); if (id > 0) { _execute(MarketCommand.CloseHudPosition, id); _nextRefresh = 0; } }));
                var label = row.Close.GetComponentInChildren<Text>();
                label.fontSize = 12;
                label.resizeTextMaxSize = 12;
                label.horizontalOverflow = HorizontalWrapMode.Overflow;
                row.PnL.resizeTextForBestFit = true;
                row.PnL.resizeTextMinSize = 9; row.PnL.resizeTextMaxSize = 12;
                row.PnL.verticalOverflow = VerticalWrapMode.Truncate;
                row.CloseTarget = row.Close.gameObject.AddComponent<HoldingActionTarget>();
                _rows.Add(row);
            }
            _overflow = ui.Text("more-holdings", _root, 6, 80, 88, 22, "", 11, new Color(.6f, .72f, .78f, 1));
            _openDetails = Add(ui.Button("details", _root, 96, 80, 50, 22, "详情", delegate { _details(); }));
            _openExchange = Add(ui.Button("exchange", _root, 40, 80, 50, 22, "兑换", delegate { _exchange(); }));
            foreach (var button in _buttons)
            {
                var label = button.GetComponentInChildren<Text>();
                label.fontSize = 12;
                label.horizontalOverflow = HorizontalWrapMode.Overflow;
            }
        }

        private void Refresh(PortfolioSnapshot state, int mode)
        {
            int count = state == null ? 0 : state.Positions.Count;
            bool hidden = mode == 2;
            _restore.gameObject.SetActive(hidden);
            _background.gameObject.SetActive(!hidden);
            _heading.gameObject.SetActive(!hidden);
            _hide.gameObject.SetActive(!hidden);
            _openDetails.gameObject.SetActive(!hidden);
            _openExchange.gameObject.SetActive(!hidden);
            _overflow.gameObject.SetActive(!hidden && count > _rows.Count);
            _overflow.text = "+" + Math.Max(0, count - _rows.Count) + "项";
            _heading.text = count == 0 ? "持仓" : "持仓  " + count;
            const float rowHeight = 34;
            float width = FXDSP.Core.TextLocalization.English ? 154 : 128;
            if (state != null)
                for (int i = 0; i < Math.Min(state.Positions.Count, _rows.Count); i++)
                    width = Mathf.Max(width, Mathf.Min(240, 94 + Mathf.Max(FormatPnl(state.Positions[i].UnrealizedPnL).Length * 6,
                        FXDSP.Core.MoneyDisplay.Percent(state.Positions[i].UnrealizedReturnRatio).Length * 5)));
            for (int i = 0; i < _rows.Count; i++)
            {
                var row = _rows[i];
                bool show = !hidden && i < count;
                row.Icon.gameObject.SetActive(show); row.Marker.gameObject.SetActive(show); row.PnL.gameObject.SetActive(show); row.Return.gameObject.SetActive(show);
                row.Close.gameObject.SetActive(show);
                if (!show) { row.ItemId = 0; continue; }
                var position = state.Positions[i]; row.ItemId = position.ItemId;
                row.CloseTarget.ItemId = position.ItemId;
                row.Icon.sprite = position.Icon;
                row.Icon.enabled = position.Icon != null;
                row.Marker.text = position.ItemId == FXDSP.Core.MarketCatalog.BuildingCoin ? "币" : position.Name.Substring(0, 1);
                row.Marker.gameObject.SetActive(position.Icon == null);
                row.PnL.text = FormatPnl(position.UnrealizedPnL);
                row.PnL.color = position.UnrealizedPnL > 0 ? MarketCandleGraphic.Rising : position.UnrealizedPnL < 0 ? MarketCandleGraphic.Falling : new Color(.6f, .72f, .78f, 1);
                row.Return.text = FXDSP.Core.MoneyDisplay.Percent(position.UnrealizedReturnRatio);
                row.Return.color = row.PnL.color;
                float top = 32 + i * rowHeight;
                ((RectTransform)row.Icon.transform).anchoredPosition = new Vector2(6, -(top + 4));
                ((RectTransform)row.Marker.transform).anchoredPosition = new Vector2(6, -(top + 4));
                var pnlRect = (RectTransform)row.PnL.transform;
                pnlRect.anchoredPosition = new Vector2(34, -top);
                pnlRect.sizeDelta = new Vector2(width - 94, 17);
                var returnRect = (RectTransform)row.Return.transform;
                returnRect.anchoredPosition = new Vector2(34, -(top + 17));
                returnRect.sizeDelta = new Vector2(width - 94, 14);
                ((RectTransform)row.Close.transform).anchoredPosition = new Vector2(width - 54, -(top + 4));
                row.Close.interactable = state.Ready;
            }
            float height = hidden ? 26 : 58 + Math.Min(count, _rows.Count) * rowHeight + (count > _rows.Count ? 20 : 0);
            _root.sizeDelta = new Vector2(width, height);
            ((RectTransform)_heading.transform).sizeDelta = new Vector2(width - 56, 26);
            ((RectTransform)_background.transform).sizeDelta = new Vector2(width, height);
            ((RectTransform)_overflow.transform).anchoredPosition = new Vector2(6, -(height - 44));
            ((RectTransform)_openDetails.transform).anchoredPosition = new Vector2(width - 56, -(height - 24));
            ((RectTransform)_openExchange.transform).anchoredPosition = new Vector2(width - 112, -(height - 24));
            ((RectTransform)_hide.transform).anchoredPosition = new Vector2(width - 46, -3);
            ((RectTransform)_restore.transform).anchoredPosition = new Vector2(width - 56, 0);
            var parent = _root.parent as RectTransform;
            float scale = parent == null ? 1 : Mathf.Min(1, Mathf.Max(.3f, (parent.rect.height - 150) / height));
            _root.localScale = new Vector3(scale, scale, 1);
            _mode = mode;
        }

        private static string FormatPnl(decimal value)
        {
            return FXDSP.Core.MoneyDisplay.Signed(value);
        }

        private Button Add(Button button) { _buttons.Add(button); return button; }
        public void Dispose()
        {
            foreach (var button in _buttons) if (button != null) button.onClick.RemoveAllListeners();
            _buttons.Clear(); _rows.Clear();
            if (_root != null) { _root.gameObject.SetActive(false); UnityEngine.Object.Destroy(_root.gameObject); }
            _root = null;
        }
    }
}
