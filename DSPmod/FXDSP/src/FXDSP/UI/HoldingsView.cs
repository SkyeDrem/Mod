using System;
using System.Collections.Generic;
using FXDSP.Core;
using FXDSP.Runtime;
using UnityEngine;
using UnityEngine.UI;

namespace FXDSP.UI
{
    internal sealed class HoldingsView
    {
        private readonly List<Row> _rows = new List<Row>();
        private readonly Text _count;
        private readonly Button _closeAll, _previous, _next;
        private readonly Text _pageText;
        private PortfolioSnapshot _lastState;
        private int _page;
        private sealed class Row
        {
            internal int ItemId;
            internal RectTransform Root;
            internal Image Icon;
            internal Text Name, Direction, Margin, Entry, Current, Liquidation, PnL, Return, Held;
            internal Button Close, Details;
            internal HoldingActionTarget CloseTarget, DetailsTarget;
        }

        internal HoldingsView(NativeUiFactory ui, Transform parent, Action<int> details, Action<int> close, Action closeAll)
        {
            var background = ui.Image("holdings-background", parent, 20, 101, 1100, 627, new Color(.03f, .055f, .07f, 1));
            background.raycastTarget = true;
            _count = ui.Text("holdings-title", parent, 40, 113, 600, 38, "全部持仓", 21);
            _closeAll = ui.Button("close-all", parent, 916, 113, 184, 36, "一键全部平仓", delegate { closeAll(); }, new Color(.4f, .27f, .23f, 1));
            string[] labels = { "市场", "方向 / 杠杆", "保证金", "入场价", "最新价", "爆仓价", "浮动盈亏", "收益率", "持有时间" };
            float[] columns = { 40, 194, 282, 385, 473, 561, 649, 772, 865 };
            float[] widths = { 144, 80, 95, 80, 80, 80, 115, 85, 75 };
            for (int i = 0; i < labels.Length; i++)
                ui.Text("column-" + i, parent, columns[i], 157, widths[i], 38, labels[i], 12, new Color(.6f, .72f, .78f, 1), TextAnchor.MiddleLeft);
            _previous = ui.Button("holdings-prev", parent, 40, 686, 90, 30, "上一页", delegate { _page = Math.Max(0, _page - 1); Refresh(_lastState); });
            _next = ui.Button("holdings-next", parent, 1010, 686, 90, 30, "下一页", delegate { _page++; Refresh(_lastState); });
            _pageText = ui.Text("holdings-page", parent, 470, 686, 200, 30, "", 13, null, TextAnchor.MiddleCenter);
            for (int i = 0; i < 9; i++)
            {
                var row = new Row();
                row.Root = ui.Container("holding-" + i, parent, 34, 207 + i * 52, 1072, 46);
                ui.Image("row-fill", row.Root, 0, 0, 1072, 46, new Color(.065f, .11f, .135f, .9f));
                row.Icon = ui.Image("icon", row.Root, 8, 10, 28, 28, Color.white);
                row.Name = ui.Text("name", row.Root, 44, 0, 106, 46, "", 14, null, TextAnchor.MiddleLeft);
                row.Direction = ui.Text("direction", row.Root, 160, 0, 80, 46, "", 13, null, TextAnchor.MiddleLeft);
                row.Direction.supportRichText = true;
                row.Margin = ui.Text("margin", row.Root, 248, 0, 95, 46, "", 13, null, TextAnchor.MiddleLeft);
                row.Entry = ui.Text("entry", row.Root, 351, 0, 80, 46, "", 13, null, TextAnchor.MiddleLeft);
                row.Current = ui.Text("current", row.Root, 439, 0, 80, 46, "", 13, null, TextAnchor.MiddleLeft);
                row.Liquidation = ui.Text("liquidation", row.Root, 527, 0, 80, 46, "", 13, null, TextAnchor.MiddleLeft);
                row.PnL = ui.Text("pnl", row.Root, 615, 0, 115, 46, "", 14, null, TextAnchor.MiddleLeft);
                row.PnL.resizeTextForBestFit = true; row.PnL.resizeTextMinSize = 10; row.PnL.resizeTextMaxSize = 14;
                row.PnL.verticalOverflow = VerticalWrapMode.Truncate;
                row.Return = ui.Text("unrealized-return", row.Root, 738, 0, 85, 46, "", 12, null, TextAnchor.MiddleLeft);
                row.Held = ui.Text("held", row.Root, 831, 0, 75, 46, "", 13, null, TextAnchor.MiddleLeft);
                foreach (var cell in new[] { row.Name, row.Direction, row.Margin, row.Return, row.Held })
                { cell.resizeTextForBestFit = true; cell.resizeTextMinSize = 9; cell.resizeTextMaxSize = cell.fontSize; cell.verticalOverflow = VerticalWrapMode.Truncate; }
                row.Details = ui.Button("details", row.Root, 925, 9, 60, 30, "详情", delegate { details(row.DetailsTarget.Consume()); });
                row.Close = ui.Button("close", row.Root, 994, 9, 66, 30, "平仓", delegate { close(row.CloseTarget.Consume()); });
                row.DetailsTarget = row.Details.gameObject.AddComponent<HoldingActionTarget>();
                row.CloseTarget = row.Close.gameObject.AddComponent<HoldingActionTarget>();
                _rows.Add(row);
            }
        }

        internal void Refresh(PortfolioSnapshot state)
        {
            _lastState = state;
            int count = state == null ? 0 : state.Positions.Count;
            int pages = Math.Max(1, (count + _rows.Count - 1) / _rows.Count);
            _page = Math.Max(0, Math.Min(pages - 1, _page));
            _pageText.text = (_page + 1) + " / " + pages;
            _previous.interactable = _page > 0; _next.interactable = _page + 1 < pages;
            _count.text = count == 0 ? "全部持仓 · 当前空仓" : "全部持仓  " + count;
            _closeAll.interactable = state != null && state.Ready && count > 0;
            for (int i = 0; i < _rows.Count; i++)
            {
                var row = _rows[i];
                int index = _page * _rows.Count + i;
                row.Root.gameObject.SetActive(index < count);
                if (index >= count) { row.ItemId = 0; continue; }
                var position = state.Positions[index];
                row.ItemId = position.ItemId;
                row.DetailsTarget.ItemId = row.CloseTarget.ItemId = position.ItemId;
                row.Icon.sprite = position.Icon; row.Icon.enabled = position.Icon != null;
                row.Name.text = position.Name;
                ((RectTransform)row.Name.transform).anchoredPosition = new Vector2(position.Icon == null ? 10 : 44, 0);
                row.Direction.text = NativeUiFactory.DirectionText(position.Direction) + " " + position.Leverage + "×";
                row.Margin.text = position.Margin.ToString("#,0.00");
                row.Entry.text = position.EntryPrice.ToString("0.0000");
                row.Current.text = position.CurrentPrice.ToString("0.0000");
                row.Liquidation.text = position.LiquidationPrice.ToString("0.0000");
                row.PnL.text = MoneyDisplay.Signed(position.UnrealizedPnL);
                row.PnL.color = position.UnrealizedPnL > 0 ? MarketCandleGraphic.Rising : position.UnrealizedPnL < 0 ? MarketCandleGraphic.Falling : new Color(.6f, .72f, .78f, 1);
                row.Return.text = MoneyDisplay.Percent(position.UnrealizedReturnRatio);
                row.Return.color = row.PnL.color;
                row.Held.text = position.HeldCandles.ToString("N0", System.Globalization.CultureInfo.InvariantCulture);
                row.Close.interactable = state.Ready;
            }
        }

        internal void Dispose()
        {
            if (_closeAll != null) _closeAll.onClick.RemoveAllListeners();
            if (_previous != null) _previous.onClick.RemoveAllListeners();
            if (_next != null) _next.onClick.RemoveAllListeners();
            foreach (var row in _rows)
            {
                if (row.Close != null) row.Close.onClick.RemoveAllListeners();
                if (row.Details != null) row.Details.onClick.RemoveAllListeners();
            }
            _rows.Clear();
        }
    }
}
