using System;
using FXDSP.Core;
using UnityEngine;
using UnityEngine.UI;

namespace FXDSP.UI
{
    internal sealed class AchievementsView : IDisposable
    {
        private readonly Text _count, _pageText;
        private readonly Button _previous, _next;
        private readonly Text[] _names = new Text[6], _descriptions = new Text[6], _states = new Text[6];
        private readonly Image[] _cards = new Image[6];
        private TradingAchievementState _state;
        private int _page;
        internal AchievementsView(NativeUiFactory ui, Transform parent)
        {
            ui.Image("achievement-background", parent, 20, 101, 1100, 627, new Color(.03f, .055f, .07f, 1)).raycastTarget = true;
            _count = ui.Text("achievements-title", parent, 42, 117, 900, 40, "成就", 22);
            for (int i = 0; i < 6; i++)
            {
                float x = 42 + i % 2 * 542, y = 176 + i / 2 * 160;
                var card = ui.Image("achievement-card-" + i, parent, x, y, 514, 140, Color.white);
                _cards[i] = card;
                _names[i] = ui.Text("name", card.transform, 20, 14, 370, 32, "", 20);
                _states[i] = ui.Text("state", card.transform, 390, 18, 104, 26, "", 14, null, TextAnchor.MiddleRight);
                _descriptions[i] = ui.Text("description", card.transform, 20, 58, 474, 66, "", 15);
                _descriptions[i].verticalOverflow = VerticalWrapMode.Truncate;
            }
            _previous = ui.Button("previous", parent, 42, 680, 104, 32, "上一页", () => { _page--; Refresh(_state); });
            _next = ui.Button("next", parent, 994, 680, 104, 32, "下一页", () => { _page++; Refresh(_state); });
            _pageText = ui.Text("page", parent, 470, 680, 200, 32, "", 14, null, TextAnchor.MiddleCenter);
        }
        internal void Refresh(TradingAchievementState state)
        {
            _state = state;
            int pages = (AchievementCatalog.All.Count + 5) / 6;
            _page = Math.Max(0, Math.Min(pages - 1, _page));
            _count.text = "成就    " + (state == null ? 0 : state.CompletedCount) + " / " + AchievementCatalog.All.Count;
            _pageText.text = (_page + 1) + " / " + pages;
            _previous.interactable = _page > 0; _next.interactable = _page + 1 < pages;
            for (int i = 0; i < 6; i++)
            {
                int index = _page * 6 + i;
                _cards[i].gameObject.SetActive(index < AchievementCatalog.All.Count);
                if (index >= AchievementCatalog.All.Count) continue;
                var definition = AchievementCatalog.All[index]; bool complete = state != null && state.Completed(definition.Id);
                _cards[i].color = complete ? new Color(.09f, .19f, .19f, 1) : new Color(.055f, .095f, .12f, 1);
                _names[i].text = definition.Name; _descriptions[i].text = definition.Description;
                _states[i].text = complete ? "已达成" : "未达成";
                _states[i].color = complete ? new Color(.4f, .82f, .75f, 1) : new Color(.5f, .6f, .66f, 1);
            }
        }
        public void Dispose() { _previous.onClick.RemoveAllListeners(); _next.onClick.RemoveAllListeners(); }
    }
}
