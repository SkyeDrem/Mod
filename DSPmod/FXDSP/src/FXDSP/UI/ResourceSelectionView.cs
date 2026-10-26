using System;
using System.Collections.Generic;
using FXDSP.Runtime;
using FXDSP.Core;
using UnityEngine;
using UnityEngine.UI;

namespace FXDSP.UI
{
    internal sealed class ResourceSelectionView : IDisposable
    {
        private readonly List<Cell> _cells = new List<Cell>();
        private readonly Button _previous, _next, _back;
        private readonly Text _pageText, _heading;
        private int _page;
        private MarketCategory? _category;
        private readonly List<MarketOption> _filtered = new List<MarketOption>();
        private sealed class Cell
        {
            internal int ItemId;
            internal bool Enabled;
            internal Button Button;
            internal Image Icon;
            internal Text Name, State;
        }

        internal ResourceSelectionView(NativeUiFactory ui, Transform parent, Action<int, bool> toggle, Action back, Action refresh)
        {
            var bg = ui.Image("resource-picker-bg", parent, 204, 101, 916, 627, new Color(.03f, .055f, .07f, 1));
            bg.raycastTarget = true;
            _heading = ui.Text("resource-picker-title", parent, 220, 112, 650, 36, "选择资源", 20);
            _back = ui.Button("resource-picker-back", parent, 966, 113, 138, 34, "返回行情", delegate { back(); });
            for (int i = 0; i < 28; i++)
            {
                var cell = new Cell();
                cell.Button = ui.Button("resource-choice-" + i, parent, 220 + (i % 4) * 223, 160 + (i / 4) * 72,
                    211, 62, "", delegate { if (cell.ItemId > 0) toggle(cell.ItemId, !cell.Enabled); });
                cell.Icon = ui.Image("icon", cell.Button.transform, 9, 14, 32, 32, Color.white);
                cell.Name = ui.Text("name", cell.Button.transform, 50, 5, 152, 29, "", 14, null, TextAnchor.MiddleLeft);
                cell.State = ui.Text("selection", cell.Button.transform, 50, 35, 152, 22, "", 12, new Color(.6f, .72f, .78f, 1));
                _cells.Add(cell);
            }
            _previous = ui.Button("resources-prev", parent, 220, 682, 100, 30, "上一页", delegate { _page = Math.Max(0, _page - 1); refresh(); });
            _next = ui.Button("resources-next", parent, 1004, 682, 100, 30, "下一页", delegate { _page++; refresh(); });
            _pageText = ui.Text("resources-page", parent, 570, 682, 200, 30, "", 13, null, TextAnchor.MiddleCenter);
        }

        internal void Refresh(MarketSnapshot state, MarketCategory category)
        {
            if (_category != category) { _page = 0; _category = category; }
            _filtered.Clear();
            foreach (var option in state.ResourceOptions) if (MarketCatalog.Category(option.ItemId) == category) _filtered.Add(option);
            int pages = Math.Max(1, (_filtered.Count + _cells.Count - 1) / _cells.Count);
            _page = Math.Max(0, Math.Min(pages - 1, _page));
            int enabled = 0;
            foreach (var option in _filtered) if (option.Enabled) enabled++;
            _heading.text = "选择" + MarketCatalog.CategoryName(category) + "    已启用 " + enabled;
            _back.interactable = enabled > 0;
            _previous.interactable = _page > 0; _next.interactable = _page + 1 < pages;
            _pageText.text = (_page + 1) + " / " + pages;
            for (int i = 0; i < _cells.Count; i++)
            {
                var cell = _cells[i];
                int index = _page * _cells.Count + i;
                cell.Button.gameObject.SetActive(index < _filtered.Count);
                if (index >= _filtered.Count) { cell.ItemId = 0; continue; }
                var option = _filtered[index];
                cell.ItemId = option.ItemId; cell.Enabled = option.Enabled;
                cell.Name.text = option.Name; cell.Icon.sprite = option.Icon; cell.Icon.enabled = option.Icon != null;
                cell.State.text = option.HasPosition ? "持仓中" : option.Enabled ? "已启用  ·  点击停用" : "点击启用";
                cell.Button.image.color = option.Enabled ? new Color(.25f, .43f, .51f, 1) : new Color(.14f, .24f, .29f, 1);
                cell.Button.interactable = state.ResourcesEditable && !option.HasPosition;
            }
        }

        public void Dispose()
        {
            foreach (var cell in _cells) if (cell.Button != null) cell.Button.onClick.RemoveAllListeners();
            _cells.Clear();
            foreach (var button in new[] { _previous, _next, _back }) if (button != null) button.onClick.RemoveAllListeners();
        }
    }

    internal sealed class ResourceSidebarView : IDisposable
    {
        internal readonly RectTransform Root;
        private readonly List<MarketOption> _enabled = new List<MarketOption>();
        private readonly List<Row> _rows = new List<Row>();
        private readonly Button _previous, _next;
        private readonly Text _pageText;
        private int _page;
        private sealed class Row
        {
            internal int ItemId;
            internal Button Button;
            internal Text Name;
            internal Image Icon;
        }

        internal ResourceSidebarView(NativeUiFactory ui, Transform parent, Action<int> select, Action refresh)
        {
            Root = ui.Container("enabled-resource-list", parent, 0, 0, 1140, 760);
            for (int i = 0; i < 8; i++)
            {
                var row = new Row();
                row.Button = ui.Button("enabled-resource-" + i, Root, 30, 151 + i * 54, 148, 46, "", delegate { if (row.ItemId > 0) select(row.ItemId); });
                row.Icon = ui.Image("icon", row.Button.transform, 8, 8, 30, 30, Color.white);
                row.Name = ui.Text("name", row.Button.transform, 44, 0, 99, 46, "", 13, null, TextAnchor.MiddleLeft);
                _rows.Add(row);
            }
            _previous = ui.Button("enabled-resources-prev", Root, 30, 632, 40, 28, "<", delegate { _page = Math.Max(0, _page - 1); refresh(); });
            _next = ui.Button("enabled-resources-next", Root, 138, 632, 40, 28, ">", delegate { _page++; refresh(); });
            _pageText = ui.Text("enabled-resources-page", Root, 72, 632, 64, 28, "", 12, null, TextAnchor.MiddleCenter);
        }

        internal void Refresh(MarketSnapshot state, MarketCategory category)
        {
            _enabled.Clear();
            foreach (var option in state.ResourceOptions)
                if (option.Enabled && MarketCatalog.Category(option.ItemId) == category) _enabled.Add(option);
            int pages = Math.Max(1, (_enabled.Count + _rows.Count - 1) / _rows.Count);
            _page = Math.Max(0, Math.Min(pages - 1, _page));
            _previous.gameObject.SetActive(pages > 1); _next.gameObject.SetActive(pages > 1); _pageText.gameObject.SetActive(pages > 1);
            _previous.interactable = _page > 0; _next.interactable = _page + 1 < pages;
            _pageText.text = (_page + 1) + "/" + pages;
            for (int i = 0; i < _rows.Count; i++)
            {
                var row = _rows[i]; int index = _page * _rows.Count + i;
                row.Button.gameObject.SetActive(index < _enabled.Count);
                if (index >= _enabled.Count) { row.ItemId = 0; continue; }
                var option = _enabled[index]; row.ItemId = option.ItemId;
                row.Name.text = option.Name; row.Icon.sprite = option.Icon; row.Icon.enabled = option.Icon != null;
                row.Button.image.color = option.ItemId == state.ItemId ? new Color(.25f, .43f, .51f, 1) : new Color(.14f, .24f, .29f, 1);
            }
        }

        public void Dispose()
        {
            foreach (var row in _rows) if (row.Button != null) row.Button.onClick.RemoveAllListeners();
            _rows.Clear();
            foreach (var button in new[] { _previous, _next }) if (button != null) button.onClick.RemoveAllListeners();
        }
    }
}
