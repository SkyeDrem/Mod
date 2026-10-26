using UnityEngine;
using UnityEngine.EventSystems;

namespace FXDSP.UI
{
    // A quote can liquidate/reorder a row between mouse-down and mouse-up.
    // Keep this click bound to the market the player actually pressed.
    internal sealed class HoldingActionTarget : MonoBehaviour, IPointerDownHandler
    {
        internal int ItemId;
        private int _pressedItemId;
        public void OnPointerDown(PointerEventData data)
        { if (data.button == PointerEventData.InputButton.Left) _pressedItemId = ItemId; }
        internal int Consume()
        {
            int id = _pressedItemId > 0 ? _pressedItemId : ItemId;
            _pressedItemId = 0;
            return id;
        }
    }
}
