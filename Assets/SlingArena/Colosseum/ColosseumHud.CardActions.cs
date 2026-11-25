using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SlingArena.Colosseum
{
    public sealed partial class ColosseumHud
    {
        enum CardAction { None, LevelUp, Equip, AwaitingOkay }
        CardAction cardAction;
        GameObject cardStage, upgradeStage, cardBack;
        RectTransform actionCardRoot, equipDestination, levelBarFill, upgradeOkay, resultStats;
        ColosseumHeroCardView actionCard;
        RawImage upgradeArena;
        TextMeshProUGUI resultLevel, resultCards, resultAttack, resultHealth;
        Image chargeRim, chargeFlash;
        ColosseumUpgradeStarburst upgradeBurst;
        ColosseumUpgradeStreaks upgradeStreaks;
        RectTransform foilSweep;
        CanvasGroup foilLight;
        readonly RectTransform[] rimGleams = new RectTransform[2];
        CanvasGroup resultProgress, statOpacity;
        Vector2Int attackRange, healthRange;
        const float LevelReveal = 1.23f, LevelComplete = 2.16f;
        static readonly Color32 StarGold = new Color32(255, 192, 50, 255);
        readonly RectTransform[] actionGlints = new RectTransform[12];
        readonly Image[] statArrows = new Image[4];
        readonly RectTransform[] materialCards = new RectTransform[3];
        readonly Image[] materialPictures = new Image[3];
        Vector2 flightStart;
        Vector3 destinationScale;
        float cardAge, remainingFill;
        int beforeLevel, afterLevel, materialCount;
        bool cardImpact;

        void BuildCardActionView()
        {
            cardStage = Group("Card action", heroSelection.transform, true);
            upgradeStage = Group("Level up result", cardStage.transform, true);
            var sky = Group("Daylight arena sky", upgradeStage.transform, true).AddComponent<ColosseumUpgradeBackdrop>();
            sky.raycastTarget = false;
            ColosseumBackdropTiles.Add(sky.rectTransform, ColosseumBackdropTiles.Motif.SwordsAndHelmets, new Color32(245, 252, 255, 53));
            upgradeArena = Group("Distant arena", sky.transform).AddComponent<RawImage>();
            upgradeArena.rectTransform.anchorMin = new Vector2(0f, .5f);
            upgradeArena.rectTransform.anchorMax = new Vector2(1f, .5f);
            upgradeArena.rectTransform.sizeDelta = new Vector2(180f, 0f);
            upgradeArena.rectTransform.anchoredPosition = new Vector2(0f, -142f);
            upgradeArena.color = new Color32(244, 246, 255, 115);
            upgradeArena.raycastTarget = false;
            var arenaFit = upgradeArena.gameObject.AddComponent<AspectRatioFitter>();
            arenaFit.aspectMode = AspectRatioFitter.AspectMode.WidthControlsHeight;
            upgradeStreaks = Group("Rising foil streaks", upgradeStage.transform).AddComponent<ColosseumUpgradeStreaks>();
            upgradeStreaks.rectTransform.sizeDelta = new Vector2(760f, 680f);
            upgradeStreaks.raycastTarget = false;

            resultLevel = Label(upgradeStage.transform, "", 39f, Cream, HeroCenter, new Vector2(0f, -42f), new Vector2(560f, 56f), bold);
            var bar = Frame(upgradeStage.transform, HeroCenter, new Vector2(0f, -103f), new Vector2(340f, 35f),
                ColosseumHeroCardView.UpgradeTrack, 3f, 3f);
            resultProgress = bar.transform.parent.gameObject.AddComponent<CanvasGroup>();
            var fill = Box(bar.transform, new Vector2(0f,.5f), new Vector2(2f, 0f), new Vector2(0f, 25f),
                ColosseumHeroCardView.UpgradeFill, 4f);
            Shade(fill, .22f, .14f);
            levelBarFill = fill.rectTransform;
            levelBarFill.pivot = new Vector2(0f, .5f);
            resultCards = Label(bar.transform, "", 19f, Color.white, HeroCenter, Vector2.zero, new Vector2(320f, 30f), slim);
            resultStats = Group("Upgraded stats", upgradeStage.transform).GetComponent<RectTransform>();
            resultStats.anchoredPosition = new Vector2(0f, -185f);
            statOpacity = resultStats.gameObject.AddComponent<CanvasGroup>();
            resultAttack = StatTile(resultStats, new Vector2(-108f, 0f), "ATTACK", new Vector2(203f, 75f));
            resultHealth = StatTile(resultStats, new Vector2(108f, 0f), "HEALTH", new Vector2(203f, 75f));
            for (int i = 0; i < statArrows.Length; i++)
            {
                statArrows[i] = Icon(resultStats, arrow, HeroCenter, Vector2.zero, i % 2 == 0 ? 28f : 19f, FriendGreen);
                statArrows[i].rectTransform.localRotation = Quaternion.Euler(0f, 0f, 180f);
            }
            upgradeOkay = Press(upgradeStage.transform, "OK", HeroCenter, new Vector2(0f, -342f),
                new Vector2(202f, 60f), SocketBlue, new Color32(21, 64, 138, 255), 30f, CloseHeroSelection);

            actionCardRoot = Group("Animated hero card", cardStage.transform).GetComponent<RectTransform>();
            var native = (RectTransform)heroesPage.CardPrefab.transform;
            actionCardRoot.sizeDelta = native.sizeDelta;
            chargeRim = Box(actionCardRoot, HeroCenter, Vector2.zero, native.sizeDelta + Vector2.one * 5f, Color.clear, 4f);
            chargeRim.name = "Charged card edge";
            actionCard = Object.Instantiate(heroesPage.CardPrefab, actionCardRoot);
            Place((RectTransform)actionCard.transform, actionCardRoot, HeroCenter, Vector2.zero, native.sizeDelta);
            var back = Frame(actionCardRoot, HeroCenter, Vector2.zero, native.sizeDelta, new Color32(32, 65, 126, 255), 4f);
            cardBack = back.transform.parent.gameObject;
            var seal = Icon(back.transform, star, HeroCenter, Vector2.zero, 81f, new Color32(138, 79, 32, 255));
            seal.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 36f);
            Icon(seal.transform, star, HeroCenter, new Vector2(0f, 2f), 72f, StarGold);
            var foilMask = Group("Card foil mask", actionCardRoot);
            var window = (RectTransform)foilMask.transform;
            window.sizeDelta = native.sizeDelta - Vector2.one * 12f;
            foilMask.AddComponent<RectMask2D>();
            foilSweep = (RectTransform)Group("Face reflection", window).transform;
            foilSweep.localRotation = Quaternion.Euler(0f, 0f, -25f);
            foilLight = foilSweep.gameObject.AddComponent<CanvasGroup>();
            foilLight.blocksRaycasts = false;
            float stripeHeight = native.sizeDelta.y * 1.6f;
            Plain(foilSweep, HeroCenter, new Vector2(-13f, 0f), new Vector2(23f, stripeHeight), new Color32(255, 246, 190, 92));
            Plain(foilSweep, HeroCenter, new Vector2(6f, 0f), new Vector2(6f, stripeHeight), new Color32(254, 255, 232, 197));
            Sprite diamond = Paint("Upgrade sparkle", 48, (x, y) => Mathf.Sqrt(Mathf.Abs(x)) + Mathf.Sqrt(Mathf.Abs(y)) < 1f);
            for (int i = 0; i < rimGleams.Length; i++)
            {
                var gleam = Icon(actionCardRoot, diamond, new Vector2(i, .5f), Vector2.zero,
                    i == 0 ? 18f : 23f, new Color32(255, 251, 210, 255));
                gleam.name = i == 0 ? "Left rim gleam" : "Right rim gleam";
                rimGleams[i] = gleam.rectTransform;
            }
            for (int i = 0; i < actionGlints.Length; i++)
            {
                float size = 23f + (i * 7 % 20);
                bool isStar = i % 3 != 1;
                var glint = Icon(cardStage.transform, isStar ? star : diamond, HeroCenter, Vector2.zero,
                    size, isStar ? new Color32(189, 105, 35, 255) : new Color32(161, 241, 255, 255));
                if (isStar)
                {
                    Image face = Icon(glint.transform, star, HeroCenter, new Vector2(0f, 1.2f), size * .82f, StarGold);
                    Shade(face, .67f, .07f);
                }
                actionGlints[i] = glint.rectTransform;
            }
            for (int i = 0; i < materialCards.Length; i++)
            {
                var card = Frame(cardStage.transform, HeroCenter, Vector2.zero, new Vector2(74f, 84f), SocketBlue, 3f);
                materialPictures[i] = Plain(card.transform, HeroCenter, Vector2.zero, new Vector2(60f, 60f), Color.white);
                materialCards[i] = (RectTransform)card.transform.parent;
            }
            upgradeBurst = Group("Upgrade starburst", cardStage.transform, true).AddComponent<ColosseumUpgradeStarburst>();
            upgradeBurst.raycastTarget = false;
            chargeFlash = Plain(cardStage.transform, HeroCenter, Vector2.zero, Vector2.zero, Color.clear);
            chargeFlash.name = "Full screen charge and impact";
            chargeFlash.rectTransform.anchorMin = Vector2.zero;
            chargeFlash.rectTransform.anchorMax = Vector2.one;
            cardStage.SetActive(false);
        }

        void StartCardAction(CardAction action)
        {
            cardAction = action;
            cardAge = 0f;
            cardImpact = false;
            cardStage.SetActive(true);
            bool upgrade = action == CardAction.LevelUp;
            upgradeStage.SetActive(upgrade);
            cardBack.SetActive(false);
            upgradeOkay.gameObject.SetActive(false);
            resultStats.gameObject.SetActive(false);
            heroPanel.gameObject.SetActive(false);
            slotPicker.SetActive(false);
            actionCard.gameObject.SetActive(true);
            actionCard.UseUpgradeProgress(upgrade);
            actionCard.Bind(inspectedHero, true);
            actionCardRoot.gameObject.SetActive(true);
            chargeRim.color = Color.clear;
            foilLight.alpha = 0f;
            chargeFlash.color = Color.clear;
            chargeFlash.gameObject.SetActive(upgrade);
            upgradeBurst.SetAge(-1f);
            upgradeBurst.gameObject.SetActive(upgrade);
            foreach (var gleam in rimGleams) gleam.localScale = Vector3.zero;
            for (int i = 0; i < actionGlints.Length; i++) actionGlints[i].localScale = Vector3.zero;
            for (int i = 0; i < materialCards.Length; i++) materialCards[i].gameObject.SetActive(upgrade && i < materialCount);
        }

        void BeginLevelUp(int oldLevel, int oldCards, int costInCards)
        {
            beforeLevel = oldLevel;
            afterLevel = ColosseumRoster.Level(inspectedHero);
            materialCount = Mathf.Min(costInCards, materialCards.Length);
            StartCardAction(CardAction.LevelUp);
            upgradeArena.texture = currentMap.Illustration.texture;
            upgradeArena.enabled = upgradeArena.texture != null;
            if (upgradeArena.enabled)
                upgradeArena.GetComponent<AspectRatioFitter>().aspectRatio = (float)upgradeArena.texture.width / upgradeArena.texture.height;
            actionCard.DisplayProgress(oldLevel, oldCards, costInCards, true);
            int cards = ColosseumRoster.Cards(inspectedHero), needed = ColosseumRoster.CardsNeeded(inspectedHero);
            remainingFill = needed > 0 ? Mathf.Clamp01((float)cards / needed) : 1f;
            resultCards.text = needed > 0 ? "CARDS  " + cards + " / " + needed : "MAX LEVEL";
            resultCards.alpha = 0f;
            resultLevel.text = "LEVEL " + beforeLevel;
            resultLevel.alpha = 0f;
            resultProgress.alpha = 0f;
            var hero = ColosseumRoster.Heroes[inspectedHero];
            for (int i = 0; i < materialPictures.Length; i++) materialPictures[i].sprite = detailPortrait.sprite;
            var before = ColosseumUnitInfo.For(hero.Weapon, beforeLevel);
            var after = ColosseumUnitInfo.For(hero.Weapon, afterLevel);
            attackRange = new Vector2Int(before.Attack, after.Attack);
            healthRange = new Vector2Int(before.Health, after.Health);
            MatchSound?.Invoke("upgrade-charge");
            TickCardAction(0f);
        }

        void BeginEquip(int slot)
        {
            equipDestination = heroesPage.LineupSlot(slot);
            destinationScale = equipDestination.localScale;
            flightStart = canvas.InverseTransformPoint(inspectedSource.TransformPoint(inspectedSource.rect.center));
            // The source can be a collection card or a slot containing its own card.
            var leaving = inspectedSource.GetComponentInChildren<ColosseumHeroCardView>();
            if (leaving != null) leaving.Ghost(true);
            StartCardAction(CardAction.Equip);
            selectionShade.color = Color.clear;
            MatchSound?.Invoke("card-pick");
            TickCardAction(0f);
        }

        void TickCardAction(float delta)
        {
            cardAge += delta;
            if (cardAction == CardAction.Equip)
            {
                float t = Mathf.Clamp01(cardAge / .36f);
                float travel = Mathf.SmoothStep(0f, 1f, t);
                Vector2 target = canvas.InverseTransformPoint(equipDestination.TransformPoint(equipDestination.rect.center));
                actionCardRoot.anchoredPosition = Vector2.Lerp(flightStart, target, travel) + Vector2.up * (45f * Mathf.Sin(t * Mathf.PI));
                float endScale = equipDestination.rect.height / ((RectTransform)actionCard.transform).rect.height;
                actionCardRoot.localScale = Vector3.one * (Mathf.Lerp(1f, endScale, travel) + .1f * Mathf.Sin(t * Mathf.PI));
                actionCardRoot.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sign(target.x - flightStart.x) * -7f * Mathf.Sin(t * Mathf.PI));
                if (t < 1f) return;
                if (!cardImpact)
                {
                    cardImpact = true;
                    heroesPage.Refresh();
                    actionCardRoot.gameObject.SetActive(false);
                    MatchSound?.Invoke("card-equip");
                }
                float settle = Mathf.Clamp01((cardAge - .36f) / .34f);
                equipDestination.localScale = destinationScale * (1f + .12f * Mathf.Sin(Mathf.PI * settle));
                AnimateEquipGlints(target, settle);
                if (settle >= 1f) CloseHeroSelection();
                return;
            }
            TickLevelUp();
        }

        static float LevelEase(float from, float to, float age)
        {
            return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(from, to, age));
        }

        void TickLevelUp()
        {
            float enter = LevelEase(0f, .22f, cardAge);
            float gather = LevelEase(.12f, 1.02f, cardAge);
            float flip = LevelEase(1.02f, 1.33f, cardAge);
            float facing = Mathf.Cos(2f * Mathf.PI * flip);
            float sinceReveal = Mathf.Max(0f, cardAge - LevelReveal);
            float strike = Mathf.Sin(Mathf.PI * Mathf.Clamp01(sinceReveal / .30f));
            float climb = LevelEase(1.25f, 1.68f, cardAge);
            Vector2 centre = new Vector2(0f, Mathf.Lerp(32f, 160f, climb) - 23f * (1f - enter));

            // Hold the face while the stars converge; turn the card just as their last trails disappear.
            float tension = gather * (1f - flip);
            float scale = Mathf.Lerp(.95f, 1.58f, enter) - .19f * tension + .30f * strike;
            actionCardRoot.anchoredPosition = centre;
            actionCardRoot.localScale = new Vector3(scale * Mathf.Max(.012f, Mathf.Abs(facing)), scale, 1f);
            actionCardRoot.localRotation = Quaternion.Euler(0f, 0f, -5f * (1f - enter)
                + tension * 2.4f * Mathf.Sin(cardAge * (15f + 13f * gather)));
            cardBack.SetActive(facing < 0f);
            actionCard.gameObject.SetActive(facing >= 0f);

            upgradeStreaks.rectTransform.anchoredPosition = centre;
            upgradeStreaks.SetPhase(gather, sinceReveal, cardAge >= LevelReveal);
            float sweep = LevelEase(.05f, .43f, sinceReveal);
            float sweepWidth = actionCardRoot.rect.width * .96f;
            foilSweep.anchoredPosition = new Vector2(Mathf.Lerp(-sweepWidth, sweepWidth, sweep), 0f);
            foilLight.alpha = Mathf.Sin(Mathf.PI * sweep);

            for (int i = 0; i < materialCount; i++)
            {
                float feed = LevelEase(.08f + i * .13f, .55f + i * .13f, cardAge);
                materialCards[i].gameObject.SetActive(feed > 0f && feed < 1f);
                float side = i % 2 == 0 ? -1f : 1f;
                Vector2 from = new Vector2(side * (212f + i * 15f), -142f + i * 142f);
                materialCards[i].anchoredPosition = Vector2.Lerp(from, centre, feed * feed) + Vector2.up * (54f * Mathf.Sin(feed * Mathf.PI));
                materialCards[i].localRotation = Quaternion.Euler(0f, 0f, side * 18f * (1f - feed));
                materialCards[i].localScale = Vector3.one * (Mathf.SmoothStep(0f, 1f, feed * 5f) * (1f - .85f * feed));
            }

            if (!cardImpact && cardAge >= LevelReveal)
            {
                cardImpact = true;
                actionCard.Bind(inspectedHero, true);
                foreach (RectTransform spark in actionGlints) spark.localScale = Vector3.zero;
                resultLevel.text = "LEVEL " + afterLevel + "  <size=67%><color=#A8FF69>+" + (afterLevel - beforeLevel) + "</color></size>";
                MatchSound?.Invoke("upgrade-burst");
            }
            AnimateChargeRim(gather, sinceReveal);
            float illumination;
            if (cardImpact)
            {
                // One white impact on the chime; stars crest as the veil leaves the screen.
                illumination = .98f * (1f - LevelEase(.045f, .245f, sinceReveal));
                upgradeBurst.SetAge(sinceReveal);
            }
            else
            {
                illumination = .30f * LevelEase(.12f, .89f, cardAge) + .37f * LevelEase(.70f, LevelReveal, cardAge);
                GatherUpgradeGlints(centre);
            }
            chargeFlash.color = new Color(1f, 1f, 1f, illumination);

            float show = LevelEase(1.54f, 1.85f, cardAge);
            resultLevel.alpha = show;
            resultLevel.rectTransform.localScale = Vector3.one * (1f + .10f * Mathf.Sin(show * Mathf.PI));
            resultProgress.alpha = show;
            levelBarFill.sizeDelta = new Vector2(329f * remainingFill * show, 25f);
            resultCards.alpha = show;
            resultStats.gameObject.SetActive(show > 0f);
            statOpacity.alpha = show;
            if (show > 0f)
            {
                RollUpgradeStat(resultAttack, attackRange, LevelEase(1.60f, 1.90f, cardAge));
                RollUpgradeStat(resultHealth, healthRange, LevelEase(1.69f, 2.02f, cardAge));
            }
            resultStats.localScale = Vector3.one * (.88f + .12f * show + .07f * Mathf.Sin(show * Mathf.PI));
            resultStats.anchoredPosition = new Vector2(0f, -185f - 18f * (1f - show));
            for (int i = 0; i < statArrows.Length; i++)
            {
                float rise = Mathf.InverseLerp(1.61f + i * .035f, 1.98f + i * .035f, cardAge);
                float x = (i < 2 ? -108f : 108f) + (i % 2 == 0 ? -58f : 52f);
                statArrows[i].rectTransform.anchoredPosition = new Vector2(x, 19f + rise * 45f);
                statArrows[i].color = new Color(.63f, 1f, .40f, Mathf.Sin(Mathf.PI * rise));
            }
            if (cardAge >= LevelComplete)
            {
                upgradeOkay.gameObject.SetActive(true);
                cardAction = CardAction.AwaitingOkay;
            }
        }

        static void RollUpgradeStat(TextMeshProUGUI label, Vector2Int range, float progress)
        {
            int value = Mathf.RoundToInt(Mathf.Lerp(range.x, range.y, progress));
            if (value == range.x)
                label.SetText("{0}", value);
            else
                label.SetText("{0}  <size=80%><color=#9AFFA6>+{1}</color></size>", value, value - range.x);
            label.rectTransform.localScale = Vector3.one * (1f + .09f * Mathf.Sin(progress * Mathf.PI));
        }

        void AnimateChargeRim(float gather, float sinceReveal)
        {
            bool revealed = cardAge >= LevelReveal;
            Color rim = revealed ? new Color32(255, 243, 181, 255) : StarGold;
            rim.a = revealed ? 1f - LevelEase(.04f, .32f, sinceReveal) : .76f * gather;
            chargeRim.color = rim;

            float halfHeight = actionCardRoot.rect.height * .5f;
            for (int i = 0; i < rimGleams.Length; i++)
            {
                float travel = LevelEase(.18f + i * .09f, 1.05f + i * .09f, cardAge);
                float y = Mathf.Lerp(-halfHeight + 11f, halfHeight - 10f, travel);
                rimGleams[i].anchoredPosition = new Vector2(i == 0 ? -1f : 1f, y);
                float wink = Mathf.Sin(Mathf.PI * travel);
                if (revealed && i == 1) wink = 1f - LevelEase(.025f, .24f, sinceReveal);
                rimGleams[i].localScale = new Vector3(.85f, 1.25f, 1f) * wink;
            }
        }

        void GatherUpgradeGlints(Vector2 centre)
        {
            for (int i = 0; i < actionGlints.Length; i++)
            {
                int bank = i / 2;
                float side = i % 2 == 0 ? -1f : 1f;
                float stagger = .046f * bank + (i % 2) * .025f;
                float t = Mathf.InverseLerp(.06f + stagger, .78f + stagger, cardAge);
                Vector2 seam = new Vector2(side * (106f + bank % 2 * 6f), -87f + bank * 34f);
                Vector2 offset = new Vector2((68f + bank * 23 % 74) * side, -80f - bank % 3 * 26f) * (1f - t * t);
                RectTransform spark = actionGlints[i];
                spark.anchoredPosition = centre + seam + offset;
                spark.localRotation = Quaternion.Euler(0f, 0f, side * (9f + 40f * t));
                spark.localScale = Vector3.one * (.86f * Mathf.Sin(t * Mathf.PI));
            }
        }

        void AnimateEquipGlints(Vector2 center, float progress)
        {
            for (int i = 0; i < actionGlints.Length; i++)
            {
                float t = Mathf.InverseLerp(.022f * (i % 4), .81f + .06f * (i % 4), progress);
                float travel = 1f - (1f - t) * (1f - t);
                float theta = (i + .3f) * Mathf.PI * 2f / actionGlints.Length;
                Vector2 offset = new Vector2(Mathf.Cos(theta) * 64f, Mathf.Sin(theta) * 80f);
                actionGlints[i].anchoredPosition = center + offset * (.55f + travel * (.60f + .10f * (i % 3)))
                    + Vector2.down * (t * t * 12.8f);
                actionGlints[i].localRotation = Quaternion.Euler(0f, 0f, 18f * i + (i % 2 == 0 ? 85f : -65f) * travel);
                actionGlints[i].localScale = Vector3.one * Mathf.Sin(Mathf.PI * t) * .78f;
            }
        }

        void ResetCardAction()
        {
            if (cardAction == CardAction.Equip)
            {
                equipDestination.localScale = destinationScale;
                if (!cardImpact) heroesPage.Refresh();
            }
            cardAction = CardAction.None;
            cardStage.SetActive(false);
        }
    }
}
