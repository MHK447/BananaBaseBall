using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace SlingArena.Colosseum
{
    public sealed class ColosseumLineupSlotView : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] ColosseumHeroCardView card;
        int equipped = -1;
        public event Action<int, RectTransform> Selected;

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left && equipped >= 0)
                Selected?.Invoke(equipped, (RectTransform)transform);
        }

        public void Bind(int hero)
        {
            equipped = hero;
            card.gameObject.SetActive(hero >= 0);
            if (hero >= 0) card.Bind(hero, preview: true, showUpgradeDot: true);
        }
    }
}
