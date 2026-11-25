using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SlingArena.Colosseum
{
    public sealed partial class ColosseumHud
    {
        const float CardRelease = .10f, CardFall = .48f, CardInterval = .56f;
        const float CardCounterHold = .58f, CardCounterFade = .18f;

        sealed class RewardCardFlight
        {
            public RectTransform Card;
            public Vector2 Origin;
            public int Hero;
            public float Delay;
            public bool Landed;
        }

        readonly List<RewardCardFlight> rewardCardFlights = new List<RewardCardFlight>();
        Image deckArrivalGlow;
        CanvasGroup deckCounterBadge;
        TextMeshProUGUI deckArrivalCounter;
        int landedRewardCards;

        // Returns the time at which the final count has finished fading.
        float BuildRewardCardFlights(IReadOnlyList<int> cardsByHero)
        {
            if (cardsByHero == null) return 0f;
            for (int hero = 0; hero < Mathf.Min(cardsByHero.Count, ColosseumRoster.Heroes.Length); hero++)
            {
                for (int copy = 0; copy < cardsByHero[hero]; copy++)
                    BuildSingleRewardCard(hero);
            }
            if (rewardCardFlights.Count == 0) return 0f;

            deckArrivalGlow = Icon(victoryFlightLayer, ColosseumMedalLight.GetMasks().Halo, JourneyCenter, Vector2.zero, 120f, Color.clear);
            deckCounterBadge = Group("Cards delivered to Heroes", victoryFlightLayer).AddComponent<CanvasGroup>();
            deckCounterBadge.blocksRaycasts = false;
            deckCounterBadge.alpha = 0f;
            Color teal = new Color32(35, 102, 119, 255);
            var badge = Frame(deckCounterBadge.transform, JourneyCenter, Vector2.zero, new Vector2(84f, 39f), teal, 3.2f, 3f);
            deckArrivalCounter = Label(badge.transform, "", 24f, Color.white, JourneyCenter, Vector2.up, new Vector2(78f, 32f), bold);
            return rewardCardFlights[rewardCardFlights.Count - 1].Delay + CardFall + CardCounterHold + CardCounterFade;
        }

        void BuildSingleRewardCard(int hero)
        {
            int index = rewardCardFlights.Count;
            var data = ColosseumRoster.Heroes[hero];
            if (!rewardCardOrigins.TryGetValue(hero, out Vector2 origin))
                origin = new Vector2(.38f + .12f * (index % 3), .39f);
            var tile = (RectTransform)Group(data.Name + " reward card +1", victoryFlightLayer).transform;
            var face = Frame(tile, JourneyCenter, Vector2.zero, new Vector2(83f, 100f), ColosseumHeroCardView.RarityColor(data.Rarity), 4f, 3f);
            var fighter = Icon(face.transform, ColosseumHeroCardView.Fighter(hero), JourneyCenter, new Vector2(0f, 4f), 83f, Color.white);
            fighter.preserveAspect = true;
            ColosseumUiContour.Artwork(fighter, face.rectTransform, 7f);
            var quantity = Box(face.transform, JourneyCenter, new Vector2(0f, -32f), new Vector2(45f, 24f), new Color32(31, 41, 66, 245), 4f);
            Label(quantity.transform, "+1", 20f, Cream, JourneyCenter, Vector2.up, new Vector2(42f, 25f), bold);
            tile.gameObject.SetActive(false);
            rewardCardFlights.Add(new RewardCardFlight { Card = tile, Hero = hero, Origin = origin, Delay = CardRelease + index * CardInterval });
        }

        void TickRewardCardFlights(float age, Rect view)
        {
            if (rewardCardFlights.Count == 0) return;
            var deck = tabs[HeroesTab].Icon;
            Vector2 target = victoryFlightLayer.InverseTransformPoint(deck.TransformPoint(deck.rect.center));
            for (int i = 0; i < rewardCardFlights.Count; i++)
            {
                var card = rewardCardFlights[i];
                if (card.Landed || age < card.Delay) continue;
                float t = Mathf.Clamp01((age - card.Delay) / CardFall);
                if (t >= 1f)
                {
                    card.Card.gameObject.SetActive(false);
                    card.Landed = true;
                    landedRewardCards++;
                    deckArrivalCounter.SetText("{0}/{1}", landedRewardCards, rewardCardFlights.Count);
                    MatchSound?.Invoke("card-equip");
                    continue;
                }

                if (!card.Card.gameObject.activeSelf) MatchSound?.Invoke("card-fly");
                card.Card.gameObject.SetActive(true);
                Vector2 source = Rect.NormalizedToPoint(view, card.Origin);
                float laneX = Mathf.Max(view.xMin + 50f, target.x - 24f - (i % 3) * 12f);
                // Swing toward the left rail first, then fall down it, clear of the trophy road.
                Vector2 first = new Vector2(laneX, source.y + 58f);
                Vector2 second = new Vector2(laneX - 12f, Mathf.Lerp(source.y, target.y, .22f));
                float rest = 1f - t;
                card.Card.anchoredPosition = rest * rest * rest * source + 3f * rest * rest * t * first
                    + 3f * rest * t * t * second + t * t * t * target;
                float lift = Mathf.Sin(Mathf.PI * t);
                card.Card.localRotation = Quaternion.Euler(0f, 0f, (i % 2 == 0 ? 14f : -12f) * lift);
                card.Card.localScale = Vector3.one * (1f + .08f * lift - .56f * t * t);
            }

            if (landedRewardCards == 0) return;
            var last = rewardCardFlights[landedRewardCards - 1];
            float sinceHit = Mathf.Max(0f, age - last.Delay - CardFall);
            float pulse = Mathf.Sin(Mathf.Clamp01(sinceHit / .25f) * Mathf.PI);
            deck.localScale = new Vector3(1f + pulse * .13f, 1f - pulse * .08f, 1f);
            deckArrivalGlow.rectTransform.anchoredPosition = target;
            deckArrivalGlow.color = new Color(1f, .92f, .61f, pulse * .70f);

            var badge = (RectTransform)deckCounterBadge.transform;
            badge.anchoredPosition = target + Vector2.up * (deck.rect.height * .5f + 22f + 4f * pulse);
            badge.localScale = Vector3.one * (1f + .14f * pulse);
            deckCounterBadge.alpha = landedRewardCards < rewardCardFlights.Count ? 1f
                : 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(CardCounterHold, CardCounterHold + CardCounterFade, sinceHit));
        }

        void ClearRewardCardFlights()
        {
            if (deckArrivalGlow != null) tabs[HeroesTab].Icon.localScale = Vector3.one;
            rewardCardFlights.Clear();
            landedRewardCards = 0;
            deckArrivalGlow = null;
            deckCounterBadge = null;
            deckArrivalCounter = null;
        }
    }
}
