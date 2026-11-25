using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SlingArena.Colosseum
{
    /// <summary>
    /// The Heroes tab shows the four equipped heroes above the complete collection in roster order.
    /// </summary>
    public sealed class ColosseumHeroesPage : MonoBehaviour
    {
        [SerializeField] ColosseumLineupSlotView[] lineup;
        [SerializeField] List<ColosseumHeroCardView> cards = new List<ColosseumHeroCardView>();
        [SerializeField] RectTransform grid;
        [SerializeField] ColosseumHeroCardView cardPrefab;
        [SerializeField] TextMeshProUGUI coinsLabel, countLabel, teamAttack;
        [SerializeField] Image coinIcon;

        public event Action<int, RectTransform> Selected;
        public ColosseumHeroCardView CardPrefab => cardPrefab;
        public RectTransform LineupSlot(int slot) => (RectTransform)lineup[slot].transform;
        /// <summary>The tray the equipped heroes sit in, whatever else the prefab puts round it.</summary>
        public RectTransform DeckArea => lineup != null && lineup.Length > 0 ? (RectTransform)lineup[0].transform.parent : null;
        bool wired;

        /// <summary>Shows the page with the saved heroes; the coin picture is the one the lobby uses.</summary>
        public void Open(Sprite coin)
        {
            if (coinIcon != null && coin != null) coinIcon.sprite = coin;
            gameObject.SetActive(true);
            Refresh();
        }

        public void Close() { gameObject.SetActive(false); }

        // The collection can be taller than its viewport on compact phones. Aim for the
        // centre only when the rewarded card (and the glow around it) would be clipped.
        public RectTransform FocusRewardCard(int hero, out ScrollRect scroll, out float destination)
        {
            scroll = grid == null ? null : grid.GetComponentInParent<ScrollRect>();
            destination = 1f;
            int slot = Array.IndexOf(ColosseumRoster.CollectionOrder, hero);
            if (slot < 0 || slot >= cards.Count) return null;
            var card = (RectTransform)cards[slot].transform;
            if (scroll == null || scroll.viewport == null || scroll.content == null) return card;
            Canvas.ForceUpdateCanvases();
            scroll.StopMovement();
            var view = scroll.viewport;
            destination = scroll.verticalNormalizedPosition;
            Rect bounds = view.rect;
            float top = view.InverseTransformPoint(card.TransformPoint(card.rect.max)).y;
            float bottom = view.InverseTransformPoint(card.TransformPoint(card.rect.min)).y;
            if (bottom >= bounds.yMin + 8f && top <= bounds.yMax - 8f) return card;
            var content = scroll.content;
            float hidden = view.InverseTransformPoint(content.TransformPoint(content.rect.max)).y
                         - view.InverseTransformPoint(content.TransformPoint(content.rect.min)).y - bounds.height;
            if (hidden > 0f)
                destination = Mathf.Clamp01(destination + ((top + bottom) * .5f - bounds.center.y) / hidden);
            return card;
        }

        public void Refresh()
        {
            int count = ColosseumRoster.CollectionOrder.Length;
            while (cards.Count < count && cardPrefab != null && grid != null)
                cards.Add(Instantiate(cardPrefab, grid));
            if (!wired)
            {
                wired = true;
                foreach (ColosseumHeroCardView card in cards)
                    card.Selected += SelectHero;
                foreach (ColosseumLineupSlotView slot in lineup)
                    slot.Selected += SelectHero;
            }
            int owned = 0;
            for (int i = 0; i < cards.Count; i++)
            {
                cards[i].gameObject.SetActive(i < count);
                if (i >= count) continue;
                int hero = ColosseumRoster.CollectionOrder[i];
                if (!ColosseumRoster.Heroes[hero].Locked) owned++;
                cards[i].Bind(hero, showUpgradeDot: true);
            }
            int attack = 0;
            int[] equipped = ColosseumRoster.LineupHeroes();
            for (int i = 0; lineup != null && i < lineup.Length && i < equipped.Length; i++)
            {
                int hero = equipped[i];
                lineup[i].Bind(hero);
                if (hero >= 0)
                    attack += ColosseumUnitInfo.For(ColosseumRoster.Heroes[hero].Weapon, ColosseumRoster.Level(hero)).Attack;
            }
            if (teamAttack != null) teamAttack.SetText("TEAM ATTACK  {0}", attack);
            if (coinsLabel != null) coinsLabel.text = ColosseumSave.Coins.ToString();
            if (countLabel != null) countLabel.text = owned + "/" + count;
        }

        void SelectHero(int hero, RectTransform source)
        {
            Selected?.Invoke(hero, source);
        }
    }
}
