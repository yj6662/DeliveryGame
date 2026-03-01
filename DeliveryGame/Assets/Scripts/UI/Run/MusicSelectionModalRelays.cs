using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace DeliveryRun.UI.Run
{
    [DisallowMultipleComponent]
    internal sealed class OptionHoverRelay : MonoBehaviour, IPointerEnterHandler
    {
        public int Index;
        public Action<int> OnHover;

        public void OnPointerEnter(PointerEventData eventData)
        {
            Action<int> callback = OnHover;
            if (callback != null)
            {
                callback(Index);
            }
        }
    }

    [DisallowMultipleComponent]
    internal sealed class OptionClickRelay : MonoBehaviour, IPointerClickHandler
    {
        public int Index;
        public Action<int> OnClick;

        public void OnPointerClick(PointerEventData eventData)
        {
            Action<int> callback = OnClick;
            if (callback != null)
            {
                callback(Index);
            }
        }
    }
}
