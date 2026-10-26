using UnityEngine;
using UnityEngine.EventSystems;

namespace FXDSP.UI
{
    internal sealed class MarketDragHandle : MonoBehaviour, IBeginDragHandler, IDragHandler
    {
        internal RectTransform Window;
        private Vector2 _startPointer;
        private Vector2 _startWindow;

        public void OnBeginDrag(PointerEventData data)
        {
            if (Window == null || data.button != PointerEventData.InputButton.Left) return;
            var parent = Window.parent as RectTransform;
            if (parent == null) return;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, data.position, data.pressEventCamera, out _startPointer);
            _startWindow = Window.anchoredPosition;
            Window.SetAsLastSibling();
        }

        public void OnDrag(PointerEventData data)
        {
            if (Window == null || data.button != PointerEventData.InputButton.Left) return;
            var parent = Window.parent as RectTransform;
            if (parent == null) return;
            Vector2 pointer;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, data.position, data.pressEventCamera, out pointer)) return;
            Window.anchoredPosition = _startWindow + pointer - _startPointer;
            Clamp(Window);
        }

        internal static void Clamp(RectTransform window)
        {
            var parent = window.parent as RectTransform;
            if (parent == null || parent.rect.width <= 0 || parent.rect.height <= 0) return;
            var halfWidth = Mathf.Max(0, (parent.rect.width - window.rect.width * window.localScale.x) * 0.5f - 8);
            var halfHeight = Mathf.Max(0, (parent.rect.height - window.rect.height * window.localScale.y) * 0.5f - 8);
            var position = window.anchoredPosition;
            window.anchoredPosition = new Vector2(Mathf.Clamp(position.x, -halfWidth, halfWidth), Mathf.Clamp(position.y, -halfHeight, halfHeight));
        }
    }
}
