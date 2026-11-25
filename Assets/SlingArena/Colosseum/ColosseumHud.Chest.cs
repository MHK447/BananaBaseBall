using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SlingArena.Colosseum
{
    public sealed partial class ColosseumHud
    {
        enum ChestPhase { Closed, Opening, Summary }
        const float ChestLidStart = .54f, ChestBurstTime = .82f, ChestRevealTime = 1.68f, ChestCardCadence = .66f;
        const float ChestIdleScale = 1.5f;
        // Support points on the trimmed painting, in the hinge mesh's 256px coordinate system.
        static readonly Vector2[] ChestFeet = {
            new Vector2(68.25f - 128f, 129f - 195f) * 1.64f,
            new Vector2(72f - 128f, 129f - 195.25f) * 1.64f,
            new Vector2(148.5f - 128f, 129f - 196.25f) * 1.64f,
            new Vector2(158.5f - 128f, 129f - 196.25f) * 1.64f
        };
        static readonly float ChestGroundY = ChestFeet[2].y * ChestIdleScale;
        static readonly Vector2 ChestHome = new Vector2(0f, -45f);
        static readonly Color32[] ChestConfettiColors = {
            new Color32(255, 227, 110, 255), new Color32(136, 255, 222, 255), new Color32(250, 140, 218, 255),
            new Color32(168, 166, 255, 255), new Color32(137, 221, 255, 255)
        };

        GameObject chestScene;
        RectTransform chestLayout, chestStand, chestPropRoot, chestHeading, chestCoinCard, chestContinue, chestLightField;
        Image chestContactShadow, chestHalo, chestFlare, chestBeam, chestMouthGlow, chestImpact, chestTileHalo, chestTileRays;
        CanvasGroup chestSceneAlpha, chestStandAlpha;
        TextMeshProUGUI chestPrompt, chestCoinNumber;
        Button chestAdvanceButton, chestContinueButton;
        ColosseumChestProp chestProp;
        readonly List<PackHeroCard> chestHeroes = new List<PackHeroCard>();
        readonly List<RectTransform> chestRewardOrder = new List<RectTransform>();
        const int ChestBurstPieces = 14;
        readonly Image[] chestParticles = new Image[32];
        int chestCoins, chestShown, chestDisplayedCoins;
        float chestAge, chestPhaseAge;
        bool chestFlashed;
        ChestPhase chestPhase;
        Action chestCollect;
        Action chestOpened;   // set when the chest holds a newly won hero: called as the lid bursts, in place of the cards

        public bool JourneyChestOpen => chestScene != null && chestScene.activeSelf;

        public void ShowJourneyChest(int coins, IReadOnlyList<int> cardsByHero, Action onCollect)
        {
            if (chestScene == null) BuildJourneyChest();
            chestCollect = onCollect;
            chestOpened = null;
            chestCoins = Mathf.Max(0, coins);
            chestDisplayedCoins = -1;
            chestShown = 0;
            chestFlashed = false;
            chestAge = chestPhaseAge = 0f;
            chestPhase = ChestPhase.Closed;
            chestRewardOrder.Clear();
            chestCoinCard.gameObject.SetActive(false);
            if (chestCoins > 0) chestRewardOrder.Add(chestCoinCard);
            foreach (var old in chestHeroes) old.Root.gameObject.SetActive(false);
            int slot = 0;
            for (int hero = 0; cardsByHero != null && hero < Mathf.Min(cardsByHero.Count, ColosseumRoster.Heroes.Length); hero++)
            {
                if (cardsByHero[hero] <= 0) continue;
                var card = BindPackCard(chestHeroes, slot++, chestLayout, hero, cardsByHero[hero], chestCoins);
                chestRewardOrder.Add(card);
                card.gameObject.SetActive(false);
            }
            chestScene.SetActive(true);
            chestScene.transform.SetAsLastSibling();
            chestStand.gameObject.SetActive(true);
            chestLightField.gameObject.SetActive(true);
            chestTileHalo.enabled = chestTileRays.enabled = false;
            chestHeading.gameObject.SetActive(false);
            chestContinue.gameObject.SetActive(false);
            chestPrompt.gameObject.SetActive(true);
            chestPrompt.text = "Click to Open";
            chestPrompt.fontSize = 39f;
            chestAdvanceButton.gameObject.SetActive(true);
            chestAdvanceButton.interactable = true;
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(chestAdvanceButton.gameObject);
            MatchSound?.Invoke("chest-appear");
            TickJourneyChest(0f);
        }

        /// <summary>The same chest, tapped open the same way, but what comes out of it is the caller's to show.</summary>
        public void ShowRecruitChest(Action opened)
        {
            ShowJourneyChest(0, null, null);
            chestOpened = opened;
        }

        public void HideJourneyChest()
        {
            chestCollect = null;
            chestOpened = null;
            if (!JourneyChestOpen) return;
            var events = EventSystem.current;
            var selected = events == null ? null : events.currentSelectedGameObject;
            bool restoreFocus = menu.activeInHierarchy && events != null
                && (selected == null || selected.transform.IsChildOf(chestScene.transform));
            chestScene.SetActive(false);
            if (restoreFocus) events.SetSelectedGameObject(tabs[openTab].Plate.gameObject);
        }

        void AdvanceJourneyChest()
        {
            if (!JourneyChestOpen) return;
            if (chestPhase == ChestPhase.Closed)
            {
                chestPhase = ChestPhase.Opening;
                chestPhaseAge = 0f;
                chestPrompt.gameObject.SetActive(false);
                MatchSound?.Invoke("chest-shake");
            }
            else if (chestPhase == ChestPhase.Opening && chestPhaseAge >= ChestRevealTime && chestOpened == null)
            {
                FinishChestReveal();
            }
        }

        void FinishChestReveal()
        {
            chestPhase = ChestPhase.Summary;
            chestPhaseAge = 0f;
            chestStand.gameObject.SetActive(false);
            chestLightField.gameObject.SetActive(false);
            chestTileHalo.enabled = chestTileRays.enabled = false;
            chestPrompt.gameObject.SetActive(false);
            chestAdvanceButton.gameObject.SetActive(false);
            chestHeading.gameObject.SetActive(true);
            for (int i = 0; i < chestRewardOrder.Count; i++)
            {
                var item = chestRewardOrder[i];
                item.gameObject.SetActive(true);
                item.anchoredPosition = ChestGridPosition(i);
                item.localScale = Vector3.one * ChestGridScale();
                item.localRotation = Quaternion.identity;
            }
            if (chestCoins > 0) SetChestCoinCount(chestCoins);
            chestContinue.gameObject.SetActive(true);
            chestContinue.SetAsLastSibling();
            chestContinueButton.interactable = false;
            MatchSound?.Invoke("reward-claim");
        }

        void CollectJourneyChest()
        {
            if (chestPhase != ChestPhase.Summary || chestPhaseAge < .32f || chestCollect == null) return;
            var collect = chestCollect;
            chestCollect = null;
            chestContinueButton.interactable = false;
            if (chestCoins > 0) CaptureChestCoinOrigin(chestCoinCard);
            foreach (var hero in chestHeroes)
                if (hero.Root.gameObject.activeSelf) CaptureVictoryCardOrigin(hero.Hero, hero.Root);
            collect();
        }

        public void TickJourneyChest(float delta)
        {
            if (!JourneyChestOpen) return;
            FitRewardLayout(chestLayout);
            chestAge += delta;
            chestPhaseAge += delta;
            chestSceneAlpha.alpha = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(chestAge / .18f));
            if (chestPhase == ChestPhase.Closed)
            {
                chestStandAlpha.alpha = 1f;
                SetGroundedChest(ChestIdleAngle(chestAge), ChestIdleScale);
                chestProp.SetOpening(0f);
                RenderChestLight(0f, 0f, 0f);
                UpdateChestParticles(-1f, -1f);
                return;
            }
            if (chestPhase == ChestPhase.Summary)
            {
                if (!chestContinueButton.interactable && chestPhaseAge >= .32f && chestCollect != null)
                {
                    chestContinueButton.interactable = true;
                    if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(chestContinue.gameObject);
                }
                chestContinue.localScale = Vector3.one * RewardSettle(chestPhaseAge, 0f);
                UpdateChestParticles(-1f, ChestSummaryTime() - ChestRevealTime + chestPhaseAge);
                return;
            }

            float t = chestPhaseAge;
            if (chestOpened != null && t >= ChestRevealTime)
            {
                Action opened = chestOpened;
                HideJourneyChest();
                opened();
                return;
            }
            if (t >= ChestSummaryTime()) { FinishChestReveal(); return; }
            // The impact in the opening sound lands 60ms into the clip.
            if (t >= ChestBurstTime - .06f && !chestFlashed)
            {
                chestFlashed = true;
                MatchSound?.Invoke("chest-open");
            }
            AnimateChestProp(t);
            if (t >= ChestRevealTime) RevealChestCards(t - ChestRevealTime);
            UpdateChestParticles(t - ChestBurstTime, t - ChestRevealTime);
        }

        float ChestSummaryTime()
        {
            return ChestRevealTime + .96f + Mathf.Max(0, chestRewardOrder.Count - 1) * ChestCardCadence;
        }

        static float ChestKnock(float time, float start, float duration)
        {
            float phase = (time - start) / duration;
            if (phase <= 0f || phase >= 1f) return 0f;
            float pulse = Mathf.Sin(phase * Mathf.PI);
            return pulse * pulse;
        }

        static float ChestIdleAngle(float age)
        {
            float t = Mathf.Repeat(age, 3.76f);
            return 4.2f * ChestKnock(t, .28f, .19f) - 2.9f * ChestKnock(t, .48f, .16f)
                + .95f * ChestKnock(t, .66f, .14f) - 3.5f * ChestKnock(t, 1.98f, .22f)
                + 2.6f * ChestKnock(t, 2.31f, .17f) - 1.2f * ChestKnock(t, 2.49f, .14f);
        }

        void SetGroundedChest(float angle, float scale)
        {
            Quaternion rotation = Quaternion.Euler(0f, 0f, angle);
            Vector2 foot = ChestFeet[0];
            Vector2 contact = rotation * foot;
            for (int i = 1; i < ChestFeet.Length; i++)
            {
                Vector2 candidate = rotation * ChestFeet[i];
                if (candidate.y >= contact.y) continue;
                foot = ChestFeet[i];
                contact = candidate;
            }
            // Rock about the lowest painted sole. It stays at the floor even during the lid accent.
            chestPropRoot.localScale = Vector3.one * scale;
            chestPropRoot.localRotation = rotation;
            chestPropRoot.anchoredPosition = new Vector2((foot.x - contact.x) * scale, ChestGroundY - contact.y * scale);

            float lean = Mathf.Clamp01(Mathf.Abs(angle) / 4.2f);
            float middle = (ChestFeet[0].x + ChestFeet[2].x) * .5f;
            float pressedSide = angle >= 0f ? ChestFeet[0].x : ChestFeet[2].x;
            float shadowX = Mathf.Lerp(middle, pressedSide, lean * .66f) * scale;
            chestContactShadow.rectTransform.anchoredPosition = new Vector2(shadowX, ChestGroundY + 3f);
            chestContactShadow.rectTransform.localScale = new Vector3(1f - .33f * lean, 1f, 1f);
        }

        void AnimateChestProp(float t)
        {
            float charge = Mathf.Clamp01(t / ChestLidStart);
            float open = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(ChestLidStart, ChestBurstTime, t));
            float recoil = ChestKnock(t, ChestBurstTime, .23f);
            open -= .05f * recoil;
            float idle = ChestIdleAngle(chestAge) * (1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / .16f)));
            float angle = idle + 2.7f * ChestKnock(t, .10f, .19f) - 2f * ChestKnock(t, .295f, .17f)
                + 1.4f * recoil;
            SetGroundedChest(angle, ChestIdleScale * (1f + .055f * open));
            chestProp.SetOpening(open);
            chestStandAlpha.alpha = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(1.43f, ChestRevealTime, t));
            RenderChestLight(t, charge, open);
        }

        void RenderChestLight(float time, float charge, float open)
        {
            float spread = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(ChestBurstTime - .08f, ChestBurstTime + .15f, time));
            float flash = Mathf.InverseLerp(ChestBurstTime - .08f, ChestBurstTime, time) *
                (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(ChestBurstTime, ChestBurstTime + .32f, time)));
            float rise = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(ChestLidStart, ChestRevealTime, time));
            float afterglow = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(1.30f, ChestRevealTime + .36f, time));
            chestLightField.anchoredPosition = ChestHome + new Vector2(-14f * (1f - rise), 38f + 68f * rise);
            chestHalo.color = new Color(1f, .80f + .15f * flash, .36f + .39f * flash,
                (.08f + charge * .10f + open * .39f + flash * .43f) * afterglow);
            chestHalo.rectTransform.localScale = Vector3.one * (.84f + .46f * spread);
            chestFlare.color = new Color(1f, .86f + .11f * flash, .43f + .28f * flash, (.20f * open + .80f * flash) * afterglow);
            chestFlare.rectTransform.localScale = Vector3.one * (.44f + .35f * open + .66f * spread);
            chestFlare.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 10f + 6f * spread);

            Vector2 mouth = new Vector2(-19.7f + 9f * open, 24.6f + 25f * open);
            chestBeam.rectTransform.anchoredPosition = chestMouthGlow.rectTransform.anchoredPosition = mouth;
            chestMouthGlow.color = new Color(1f, .95f, .77f + .22f * flash, .57f * open + .43f * flash);
            chestMouthGlow.rectTransform.localScale = new Vector3(.46f + .49f * open + .55f * flash, .30f + .67f * open + .92f * flash, 1f);
            chestBeam.color = new Color(1f, 1f, 1f, .32f * open + .68f * flash);
            chestBeam.rectTransform.localScale = new Vector3(.35f + .54f * open + .43f * flash, .12f + .67f * open + .37f * flash, 1f);
            chestImpact.rectTransform.anchoredPosition = mouth + Vector2.up * 12f;
            chestImpact.rectTransform.localScale = Vector3.one * (.28f + 1.03f * spread);
            chestImpact.color = new Color(1f, 1f, .93f, flash * flash);
        }

        void RevealChestCards(float age)
        {
            if (!chestHeading.gameObject.activeSelf)
            {
                chestHeading.gameObject.SetActive(true);
                chestPrompt.gameObject.SetActive(true);
                chestPrompt.fontSize = 25f;
                chestPrompt.text = "Tap to skip";
            }
            chestHeading.localScale = Vector3.one * RewardSettle(age, 0f);
            for (int i = 0; i < chestRewardOrder.Count; i++)
            {
                float reveal = age - (i == 0 ? 0f : .83f + (i - 1) * ChestCardCadence);
                if (reveal < 0f) break;
                var item = chestRewardOrder[i];
                if (i >= chestShown)
                {
                    item.gameObject.SetActive(true);
                    chestShown = i + 1;
                    MatchSound?.Invoke(item == chestCoinCard ? "chest-coins" : "card-pick");
                }
                float pop = Mathf.Clamp01(reveal / .29f);
                Vector2 goal = ChestGridPosition(i);
                float gridScale = ChestGridScale();
                if (i == 0 && chestRewardOrder.Count > 1)
                {
                    float slide = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.58f, 1.01f, age));
                    goal = Vector2.Lerp(new Vector2(0f, 20f), goal, slide);
                    gridScale = Mathf.Lerp(1.31f, gridScale, slide);
                }
                item.anchoredPosition = goal + Vector2.down * (1f - pop) * 51f;
                float bounce = .16f * Mathf.Sin(pop * Mathf.PI);
                item.localScale = Vector3.one * (gridScale * (Mathf.Lerp(.48f, 1f, pop) + bounce));
                item.localRotation = Quaternion.Euler(0f, 0f, (1f - pop) * (i % 2 == 0 ? -9f : 9f));
                if (i == chestShown - 1) LightChestCard(goal, reveal, item == chestCoinCard);
                if (item == chestCoinCard)
                {
                    float count = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(reveal / .72f));
                    SetChestCoinCount(Mathf.RoundToInt(chestCoins * count));
                }
            }
        }

        void SetChestCoinCount(int value)
        {
            if (value == chestDisplayedCoins) return;
            chestDisplayedCoins = value;
            chestCoinNumber.text = "+" + value;
        }

        void LightChestCard(Vector2 position, float age, bool coin)
        {
            float t = Mathf.Clamp01(age / .49f);
            chestTileHalo.enabled = chestTileRays.enabled = t < 1f;
            if (t >= 1f) return;
            chestTileHalo.rectTransform.anchoredPosition = chestTileRays.rectTransform.anchoredPosition = position;
            chestTileRays.rectTransform.localScale = Vector3.one * Mathf.Lerp(.48f, 1.13f, t);
            chestTileRays.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 12f + 26f * t);
            Color light = coin ? (Color)new Color32(255, 231, 135, 255) : new Color32(188, 245, 255, 255);
            light.a = (1f - t) * .78f;
            chestTileHalo.color = light;
            light.a *= .75f;
            chestTileRays.color = light;
        }

        float ChestGridScale() => Mathf.Min(1.08f, 2.16f / Mathf.Max(1, Mathf.CeilToInt(chestRewardOrder.Count / 3f)));

        Vector2 ChestGridPosition(int index)
        {
            int count = chestRewardOrder.Count;
            int columns = count == 4 ? 2 : Mathf.Min(3, count);
            int rows = (count + columns - 1) / columns;
            int row = index / columns, rowSize = Mathf.Min(columns, count - row * columns);
            float size = ChestGridScale();
            return new Vector2((index % columns - (rowSize - 1) * .5f) * 181f * size,
                20f + ((rows - 1) * .5f - row) * 280f * size);
        }

        void BuildJourneyChest()
        {
            chestLayout = MakeRewardScene("Journey chest", out chestScene, out chestSceneAlpha, ColosseumBackdropTiles.Motif.ChestsAndCards);
            var sky = chestScene.GetComponent<ColosseumLeagueSky>();
            sky.SetGradient(new Color32(153, 223, 242, 255), new Color32(169, 161, 224, 255));
            var tiles = chestScene.GetComponentInChildren<ColosseumBackdropTiles>(true);
            tiles.color = new Color32(67, 95, 159, 39);
            var masks = ColosseumMedalLight.GetMasks();
            var overhead = Icon(chestScene.transform, masks.Halo, new Vector2(.5f, 1f), new Vector2(0f, 86f), 1f, new Color32(255, 245, 204, 170));
            overhead.rectTransform.anchorMin = new Vector2(0f, 1f);
            overhead.rectTransform.anchorMax = Vector2.one;
            overhead.rectTransform.sizeDelta = new Vector2(165f, 680f);
            overhead.rectTransform.SetSiblingIndex(chestLayout.GetSiblingIndex());

            chestLightField = Group("Chest afterglow", chestLayout).GetComponent<RectTransform>();
            chestHalo = Icon(chestLightField, masks.Halo, RewardCenter, Vector2.zero, 594f, Cream);
            chestFlare = Icon(chestLightField, masks.Rays, RewardCenter, Vector2.zero, 735f, Cream);
            chestStand = Group("Chest stage", chestLayout).GetComponent<RectTransform>();
            chestStand.anchoredPosition = ChestHome;
            chestStandAlpha = chestStand.gameObject.AddComponent<CanvasGroup>();
            var floorShadow = Icon(chestStand, masks.Halo, RewardCenter, new Vector2(-18f, ChestGroundY + 6f),
                1f, new Color32(41, 43, 83, 66));
            floorShadow.rectTransform.sizeDelta = new Vector2(420f, 46f);
            chestContactShadow = Icon(chestStand, masks.Halo, RewardCenter, Vector2.zero, 1f, new Color32(35, 29, 65, 125));
            chestContactShadow.rectTransform.sizeDelta = new Vector2(292f, 16f);
            chestProp = ColosseumChestProp.Create(chestStand);
            chestPropRoot = (RectTransform)chestProp.transform;
            // Coordinates on the front rail; both lights follow the prop's tilt and scale.
            Vector2 mouth = new Vector2(-19.7f, 24.6f);
            chestBeam = Plain(chestPropRoot, RewardCenter, mouth, new Vector2(304f, 320f), Color.clear);
            chestBeam.rectTransform.pivot = new Vector2(.5f, 0f);
            chestBeam.sprite = MakeChestBeam();
            chestMouthGlow = Icon(chestPropRoot, masks.Halo, RewardCenter, mouth, 1f, Color.clear);
            chestMouthGlow.rectTransform.sizeDelta = new Vector2(231f, 65f);
            var impact = Paint("Chest light burst", 128, (x, y) =>
            {
                if (y < 0f) y *= 1.9f;
                float cross = Mathf.Sqrt(Mathf.Abs(x)) + Mathf.Sqrt(Mathf.Abs(y));
                float diagonal = Mathf.Sqrt(Mathf.Abs(x + y) * .7071f) + Mathf.Sqrt(Mathf.Abs(x - y) * .7071f);
                return cross < .99f || diagonal < .78f;
            });
            chestImpact = Icon(chestPropRoot, impact, RewardCenter, mouth, 245f, Color.clear);
            chestImpact.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -9f);

            chestHeading = Group("Reward heading", chestLayout).GetComponent<RectTransform>();
            chestHeading.anchoredPosition = new Vector2(0f, 360f);
            for (int side = -1; side <= 1; side += 2)
            {
                Box(chestHeading, RewardCenter, new Vector2(side * 209f, 0f), new Vector2(115f, 2f), new Color32(71, 89, 146, 138));
                var gem = Box(chestHeading, RewardCenter, new Vector2(side * 147f, 0f), new Vector2(7f, 7f), new Color32(82, 98, 152, 202), 6f);
                gem.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            }
            Label(chestHeading, "Rewards", 47f, Color.white, RewardCenter, Vector2.zero, new Vector2(256f, 72f), bold);
            chestPrompt = Label(chestLayout, "Click to Open", 39f, Cream, RewardCenter, new Vector2(0f, -407f), new Vector2(570f, 66f), bold);
            chestTileHalo = Icon(chestLayout, masks.Halo, RewardCenter, Vector2.zero, 302f, Cream);
            chestTileRays = Icon(chestLayout, masks.Rays, RewardCenter, Vector2.zero, 333f, Cream);
            chestCoinCard = Box(chestLayout, RewardCenter, Vector2.zero, new Vector2(163f, 212f), RewardInk, 1.75f).rectTransform;
            var rim = Box(chestCoinCard, RewardCenter, new Vector2(0f, 1.5f), new Vector2(156f, 203f), new Color32(255, 234, 170, 255), 1.75f);
            var stock = Box(rim.transform, RewardCenter, Vector2.zero, new Vector2(149f, 195f), new Color32(247, 176, 50, 255), 1.95f);
            Shade(stock, .26f, .18f);
            Art(stock.transform, "coin", RewardCenter, new Vector2(-23f, 25f), 75f);
            Art(stock.transform, "coin", RewardCenter, new Vector2(22f, 41f), 76f);
            Art(stock.transform, "coin", RewardCenter, new Vector2(6f, 8f), 90f);
            var footer = Box(stock.transform, RewardCenter, new Vector2(0f, -73f), new Vector2(147f, 48f), new Color32(128, 76, 44, 255), 3f);
            Label(footer.transform, "Coins", 24f, Color.white, RewardCenter, Vector2.zero, new Vector2(134f, 40f), bold);
            chestCoinNumber = Label(stock.transform, "", 36f, Cream, RewardCenter, new Vector2(0f, -31f), new Vector2(139f, 43f), bold);

            for (int i = 0; i < chestParticles.Length; i++)
            {
                if (i < ChestBurstPieces)
                {
                    chestParticles[i] = (i & 1) == 0
                        ? Icon(chestLayout, star, RewardCenter, Vector2.zero, 22f + (i % 3) * 3f, Cream)
                        : Box(chestLayout, RewardCenter, Vector2.zero, new Vector2(4.5f, 27f + (i % 3) * 4f), Cream, 6f);
                }
                else
                {
                    chestParticles[i] = i % 3 == 0 ? Icon(chestLayout, star, RewardCenter, Vector2.zero, 17f, Cream)
                        : Box(chestLayout, RewardCenter, Vector2.zero, new Vector2(5f + i % 3, 13f), Cream, 6f);
                }
                chestParticles[i].enabled = false;
            }
            var target = Group("Open or skip chest", chestScene.transform, true);
            var hit = target.AddComponent<Image>();
            hit.color = Color.clear;
            chestAdvanceButton = target.AddComponent<Button>();
            chestAdvanceButton.targetGraphic = hit;
            chestAdvanceButton.transition = Selectable.Transition.None;
            chestAdvanceButton.navigation = new Navigation { mode = Navigation.Mode.None };
            chestAdvanceButton.onClick.AddListener(AdvanceJourneyChest);
            chestContinue = Press(chestLayout, "Continue", RewardCenter, new Vector2(0f, -400f), new Vector2(347f, 83f),
                new Color32(115, 212, 83, 255), new Color32(53, 134, 64, 255), 42f, CollectJourneyChest);
            chestContinue.name = "Continue";
            chestContinueButton = chestContinue.GetComponent<Button>();
            chestContinueButton.navigation = new Navigation { mode = Navigation.Mode.None };
            chestScene.SetActive(false);
        }

        static Sprite MakeChestBeam()
        {
            const int width = 128, height = 176;
            var texture = Kit.Own(new Texture2D(width, height, TextureFormat.RGBA32, false)
                { name = "Chest opening light", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp });
            var pixels = new Color32[width * height];
            for (int y = 0; y < height; y++)
            {
                float t = y / (float)(height - 1);
                float cap = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, .065f, t)) *
                    (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.20f, .99f, t)));
                for (int x = 0; x < width; x++)
                {
                    float u = ((x + .5f) * 2f / width - 1f) / Mathf.Lerp(.38f, 1f, t);
                    float edge = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(Mathf.Abs(u)));
                    float centre = Mathf.Exp(-17f * u * u);
                    float left = Mathf.Exp(-62f * (u + .51f) * (u + .51f));
                    float right = Mathf.Exp(-94f * (u - .60f) * (u - .60f));
                    float rays = Mathf.Clamp01(centre + .75f * left + .9f * right);
                    pixels[y * width + x] = new Color(1f, .76f + .24f * rays, .30f + .62f * rays,
                        cap * edge * (.18f + .82f * rays));
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return Kit.Own(Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(.5f, 0f), 100f));
        }

        void UpdateChestParticles(float burstAge, float rewardAge)
        {
            Vector2 mouth = chestLayout.InverseTransformPoint(chestMouthGlow.rectTransform.position);
            for (int i = 0; i < ChestBurstPieces; i++)
            {
                Image piece = chestParticles[i];
                int lane = i / 2;
                bool isStar = (i & 1) == 0;
                float life = (isStar ? .55f : .25f) + (lane % 3) * .045f;
                piece.enabled = burstAge >= 0f && burstAge < life;
                if (!piece.enabled) continue;

                float t = burstAge / life;
                float left = 1f - t;
                float advance = 1f - left * left * left;
                float angle = isStar ? 9f + 27f * lane : -9f + 33f * lane;
                float rad = angle * Mathf.Deg2Rad;
                Vector2 ray = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
                float reach = (isStar ? 131f : 101f) + (i * 37 % 76);
                Vector2 offset = ray * Mathf.Lerp(18f + lane % 3 * 5f, reach, advance);
                if (isStar) offset.y -= 41f * t * t;
                piece.rectTransform.anchoredPosition = mouth + offset;

                Color tint = isStar ? Color.Lerp(Cream, Yellow, advance) : (Color)new Color32(255, 237, 159, 255);
                tint.a = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.35f, 1f, t));
                piece.color = tint;
                float spin = isStar ? lane * 29f + (lane % 2 == 0 ? 72f : -72f) * t : angle - 90f;
                piece.rectTransform.localRotation = Quaternion.Euler(0f, 0f, spin);
                float shrink = Mathf.Lerp(1f, .26f, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.25f, 1f, t)));
                piece.rectTransform.localScale = isStar
                    ? Vector3.one * ((.88f + .37f * Mathf.Sin(t * Mathf.PI)) * shrink)
                    : new Vector3(shrink, .48f + .94f * Mathf.Sin(t * Mathf.PI), 1f);
            }
            for (int i = ChestBurstPieces; i < chestParticles.Length; i++)
            {
                Image piece = chestParticles[i];
                float age = rewardAge - ((i * 7) % 19) * .098f;
                float duration = 2.55f + (i % 4) * .21f;
                if (age < 0f || age >= duration) { piece.enabled = false; continue; }
                piece.enabled = true;
                float t = age / duration;
                float seed = i * 2.39996f;
                float x = Mathf.Sin(seed) * (230f + (i * 37 % 95));
                Vector2 p = new Vector2(x + Mathf.Sin(t * 9f + i) * 32f, 548f - 998f * t);
                Color color = ChestConfettiColors[i % ChestConfettiColors.Length];
                color.a = Mathf.Clamp01(age * 12f) * Mathf.Clamp01((1f - t) * 3.7f);
                piece.color = color;
                piece.rectTransform.anchoredPosition = p;
                piece.rectTransform.localRotation = Quaternion.Euler(0f, 0f, i * 37f + age * (i % 2 == 0 ? 124f : -88f));
                piece.rectTransform.localScale = i % 3 == 0 ? Vector3.one * (.76f + .32f * Mathf.Sin(t * Mathf.PI))
                    : new Vector3(.42f + .58f * Mathf.Abs(Mathf.Cos(seed + age * 5.4f)), 1f, 1f);
            }
        }
    }
}
