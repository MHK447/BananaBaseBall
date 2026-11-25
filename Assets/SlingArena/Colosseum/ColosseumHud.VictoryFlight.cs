using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SlingArena.Colosseum
{
    public sealed partial class ColosseumHud
    {
        readonly Dictionary<int, Vector2> rewardCardOrigins = new Dictionary<int, Vector2>();
        Vector2? claimedCupOrigin, chestCoinOrigin;
        const float CoinTravel = .57f, CoinStagger = .07f;
        readonly List<RectTransform> flyingCoins = new List<RectTransform>(10);
        int chestCoinPayout, landedCoins;
        RectTransform victoryFlightLayer;
        int heldJourneyTrophies = -1, heldProfileTrophies = -1, flightFinalTrophies;
        float arrivalAge, cardFlightFinish;

        public bool VictoryArrivalActive => heldJourneyTrophies >= 0 || victoryFlightLayer != null;

        // Both screens share a canvas. Save normalized positions before the result screen closes
        // so the reward keeps its starting point even if the viewport changes during the flight.
        Vector2 RewardOrigin(RectTransform source)
        {
            Vector2 point = canvas.InverseTransformPoint(source.TransformPoint(source.rect.center));
            return Rect.PointToNormalized(canvas.rect, point);
        }

        void CaptureVictoryTrophyOrigin(RectTransform source) => claimedCupOrigin = RewardOrigin(source);
        void CaptureVictoryCardOrigin(int hero, RectTransform source) => rewardCardOrigins[hero] = RewardOrigin(source);
        void CaptureChestCoinOrigin(RectTransform source) => chestCoinOrigin = RewardOrigin(source);

        public void PrepareVictoryArrival(int previousJourneyTrophies, int previousScore = -1)
        {
            heldJourneyTrophies = Mathf.Max(0, previousJourneyTrophies);
            heldProfileTrophies = previousScore;
            int stage = ColosseumSave.StageFor(heldJourneyTrophies);
            int progress = ColosseumSave.StageProgressFor(heldJourneyTrophies);
            if (swiping || journeyStage != stage || !Mathf.Approximately(journeyProgress, progress))
                journeyStage = 0; // Rebind directly to the saved starting map if a presentation was interrupted.
            RefreshJourney(heldJourneyTrophies);
        }

        public void StartVictoryArrival(int previousJourneyTrophies, int finalJourneyTrophies, int trophies, IReadOnlyList<int> cardsByHero, int coins = 0)
        {
            if (victoryFlightLayer != null) return;
            if (heldJourneyTrophies < 0) PrepareVictoryArrival(previousJourneyTrophies);
            flightFinalTrophies = finalJourneyTrophies;
            arrivalAge = 0f;
            // What was won leaves for its places in the lobby; coins have a sound of their own as they spill out.
            MatchSound?.Invoke(coins > 0 ? "coin-fly" : "reward-fly");
            victoryFlightLayer = (RectTransform)Group("Victory reward delivery", menu.transform, true).transform;
            var shield = victoryFlightLayer.gameObject.AddComponent<Image>();
            shield.color = Color.clear;
            shield.raycastTarget = true;
            var focus = victoryFlightLayer.gameObject.AddComponent<Selectable>();
            focus.transition = Selectable.Transition.None;
            focus.navigation = new Navigation { mode = Navigation.Mode.None };
            focus.targetGraphic = shield;
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(focus.gameObject);

            BuildTrophyReceipt(previousJourneyTrophies, trophies);

            chestCoinPayout = coins;
            landedCoins = 0;
            if (coins > 0)
            {
                Sprite coinArt = walletCoin.GetComponent<Image>().sprite;
                // Fly each coin for small payouts. Large chests deliver the balance in six bursts.
                int coinIcons = coins <= 10 ? coins : 6;
                for (int i = 0; i < coinIcons; i++)
                {
                    var coin = Icon(victoryFlightLayer, coinArt, JourneyCenter, Vector2.zero, 50f, Color.white);
                    coin.preserveAspect = true;
                    flyingCoins.Add(coin.rectTransform);
                }
                lobbyCoins.text = (ColosseumSave.Coins - coins).ToString();
            }

            cardFlightFinish = BuildRewardCardFlights(cardsByHero);
            rewardCardOrigins.Clear();
            claimedCupOrigin = null;
            TickVictoryArrival(0f);
        }

        static void FlyClaimedItem(RectTransform item, Vector2 origin, Vector2 destination, float time, float tilt, float arc)
        {
            float t = Mathf.Clamp01(time);
            float sweep = t * t;
            float lift = 4f * t * (1f - t);
            item.anchoredPosition = Vector2.Lerp(origin, destination, sweep) + Vector2.up * (arc * lift);
            item.localScale = Vector3.one * (1f + .08f * lift - .72f * sweep);
            item.localRotation = Quaternion.Euler(0f, 0f, tilt * lift);
        }

        public void TickVictoryArrival(float delta)
        {
            if (victoryFlightLayer == null) return;
            if (!menu.activeInHierarchy || openTab != BattleTab || matchRoot.activeSelf)
            {
                CancelVictoryArrival();
                return;
            }

            arrivalAge += delta;
            Rect view = victoryFlightLayer.rect;
            TickTrophyReceipt(delta, view);
            TickRewardCardFlights(arrivalAge, view);
            if (flyingCoins.Count > 0) TickChestCoinFlight(view);

            float finish = cardFlightFinish;
            if (flyingCoins.Count > 0) finish = Mathf.Max(finish, CoinTravel + (flyingCoins.Count - 1) * CoinStagger + .2f);
            if (arrivalAge >= finish && TrophyReceiptFinished && !swiping
                && journeyStage == ColosseumSave.StageFor(flightFinalTrophies)
                && Mathf.Approximately(journeyProgress, ColosseumSave.StageProgressFor(flightFinalTrophies)))
                CancelVictoryArrival();
        }

        void TickChestCoinFlight(Rect view)
        {
            Vector2 origin = Rect.NormalizedToPoint(view, chestCoinOrigin ?? new Vector2(.5f, .5f));
            Vector2 target = victoryFlightLayer.InverseTransformPoint(walletCoin.TransformPoint(walletCoin.rect.center));
            float pulse = 0f;
            for (int i = 0; i < flyingCoins.Count; i++)
            {
                float age = arrivalAge - i * CoinStagger;
                float t = Mathf.Clamp01(age / CoinTravel);
                var coin = flyingCoins[i];
                if (t < 1f)
                {
                    coin.gameObject.SetActive(age >= 0f);
                    FlyClaimedItem(coin, origin, target, t, i % 2 == 0 ? 14f : -14f, 90f + i * 9f);
                    float spread = flyingCoins.Count > 1 ? i / (flyingCoins.Count - 1f) - .5f : 0f;
                    coin.anchoredPosition += Vector2.right * (spread * 110f * Mathf.Sin(t * Mathf.PI));
                }
                else if (coin.gameObject.activeSelf)
                {
                    coin.gameObject.SetActive(false);
                    landedCoins++;
                    int balance = ColosseumSave.Coins - chestCoinPayout;
                    lobbyCoins.text = (balance + chestCoinPayout * landedCoins / flyingCoins.Count).ToString();
                    if (landedCoins == 1 || landedCoins == flyingCoins.Count) MatchSound?.Invoke("coin");
                }
                pulse = Mathf.Max(pulse, Mathf.Sin(Mathf.PI * Mathf.Clamp01((age - CoinTravel) / .2f)));
            }
            walletCoin.localScale = Vector3.one * (1f + .12f * pulse);
        }

        public void CancelVictoryArrival()
        {
            if (!VictoryArrivalActive) return;
            ClearTrophyReceipt();
            ClearRewardCardFlights();
            heldJourneyTrophies = -1;
            heldProfileTrophies = -1;
            lobbyTrophies.text = ColosseumSave.Trophies.ToString();
            RefreshJourney(ColosseumSave.JourneyTrophies);
            if (victoryFlightLayer != null)
            {
                if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject == victoryFlightLayer.gameObject)
                    EventSystem.current.SetSelectedGameObject(menu.activeInHierarchy ? tabs[openTab].Plate.gameObject : null);
                victoryFlightLayer.gameObject.SetActive(false);
                Object.Destroy(victoryFlightLayer.gameObject);
            }
            victoryFlightLayer = null;
            rewardCardOrigins.Clear();
            claimedCupOrigin = null;
            if (flyingCoins.Count > 0)
            {
                lobbyCoins.text = ColosseumSave.Coins.ToString();
                walletCoin.localScale = Vector3.one;
                flyingCoins.Clear();
            }
            chestCoinOrigin = null;
        }
    }
}
