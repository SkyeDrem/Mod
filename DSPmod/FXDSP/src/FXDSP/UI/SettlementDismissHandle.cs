using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace FXDSP.UI
{
    internal sealed class SettlementDismissHandle : MonoBehaviour, IPointerClickHandler
    {
        internal Action Dismiss;
        public void OnPointerClick(PointerEventData eventData)
        {
            eventData.Use();
            Dismiss?.Invoke();
        }
        private void OnDestroy() { Dismiss = null; }
    }
}
