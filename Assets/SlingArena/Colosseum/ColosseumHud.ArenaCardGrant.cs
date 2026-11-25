using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SlingArena.Colosseum
{
    public sealed partial class ColosseumHud
    {
        const float GrantImpactTime = .88f;

        RectTransform grantLayer, grantCard, grantFocus;
        CanvasGroup grantBadge;
        Image grantHalo;
        ColosseumUpgradeStarburst grantStars;
        readonly Image[] grantBackdrop = new Image[4], grantEdges = new Image[4];
        ScrollRect grantScroll;
        Vector3 originalGrantScale;
        float grantFromScroll, grantToScroll, grantAge;
        bool grantLocked, grantImpacted;
        Action grantImpact, grantComplete;

        public void ShowArenaCardGrant(int hero, Action onImpact, Action onComplete)
        {
            if (grantLayer != null) return;
            if (ColosseumRoster.Heroes[hero].Locked) recentArenaHero = hero;
            OpenTab(HeroesTab);
            grantCard = openTab == HeroesTab && heroesPage != null
                ? heroesPage.FocusRewardCard(hero, out grantScroll, out grantToScroll) : null;
            if (grantCard == null)
            {
                onImpact?.Invoke();
                heroesPage?.Refresh();
                RefreshLobby(0);
                onComplete?.Invoke();
                return;
            }

            grantLocked = ColosseumRoster.Heroes[hero].Locked;
            originalGrantScale = grantCard.localScale;
            grantAge = 0f;
            grantImpacted = false;
            grantImpact = onImpact;
            grantComplete = onComplete;
            grantFromScroll = grantScroll == null ? 0f : grantScroll.verticalNormalizedPosition;
            grantLayer = (RectTransform)Group("Arena collection reward", canvas, true).transform;
            var blocker = grantLayer.gameObject.AddComponent<Image>();
            blocker.color = Color.clear;
            var focus = grantLayer.gameObject.AddComponent<Selectable>();
            focus.targetGraphic = blocker;
            focus.transition = Selectable.Transition.None;
            focus.navigation = new Navigation { mode = Navigation.Mode.None };
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(grantLayer.gameObject);
            for (int i = 0; i < grantBackdrop.Length; i++)
                grantBackdrop[i] = Plain(grantLayer, JourneyCenter, Vector2.zero, Vector2.zero, Color.clear);

            grantFocus = (RectTransform)Group("Collection card highlight", grantLayer).transform;
            grantHalo = Icon(grantFocus, ColosseumMedalLight.GetMasks().Halo, JourneyCenter, Vector2.zero, 160f, Color.clear);
            for (int i = 0; i < grantEdges.Length; i++)
                grantEdges[i] = Box(grantFocus, JourneyCenter, Vector2.zero, Vector2.one, Color.clear, 8f);
            grantBadge = Group(grantLocked ? "Arena reward lock" : "Bonus hero card", grantFocus).AddComponent<CanvasGroup>();
            if (grantLocked)
                Icon(grantBadge.transform, Drawn("lock"), JourneyCenter, Vector2.zero, 71f, Color.white).preserveAspect = true;
            else
            {
                Color rarity = ColosseumHeroCardView.RarityColor(ColosseumRoster.Heroes[hero].Rarity);
                var face = Frame(grantBadge.transform, JourneyCenter, Vector2.zero, new Vector2(55f, 67f), rarity, 3f, 3.5f);
                var art = Icon(face.transform, ColosseumHeroCardView.Fighter(hero), JourneyCenter, Vector2.zero, 55f, Color.white);
                art.preserveAspect = true;
                ColosseumUiContour.Artwork(art, face.rectTransform, 5f);
                Label(face.transform, "+1", 26f, Color.white, JourneyCenter, new Vector2(14f, -20f), new Vector2(50f, 31f), bold);
            }

            var burst = (RectTransform)Group("Arena reward stars", grantFocus).transform;
            burst.sizeDelta = new Vector2(1080f, 1180f);
            burst.localScale = Vector3.one * .32f;
            grantStars = burst.gameObject.AddComponent<ColosseumUpgradeStarburst>();
            grantStars.raycastTarget = false;
            MatchSound?.Invoke(grantLocked ? "recruit-portal" : "card-pick");
            TickArenaCardGrant(0f);
        }

        public void TickArenaCardGrant(float delta)
        {
            if (grantLayer == null) return;
            grantAge += delta;
            if (grantScroll != null && grantScroll.verticalNormalizedPosition != grantToScroll)
                grantScroll.verticalNormalizedPosition = Mathf.SmoothStep(grantFromScroll, grantToScroll, Mathf.Clamp01(grantAge / .28f));

            float charge = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.18f, GrantImpactTime, grantAge));
            float after = Mathf.Max(0f, grantAge - GrantImpactTime);
            float fade = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.32f, .83f, after));
            if (!grantImpacted && grantAge >= GrantImpactTime)
            {
                grantImpacted = true;
                grantImpact?.Invoke();
                grantImpact = null;
                heroesPage.Refresh();
                RefreshLobby(0);
                MatchSound?.Invoke(grantLocked ? "recruit-reveal" : "card-equip");
            }

            float punch = .065f * Mathf.Sin(Mathf.Clamp01(after / .4f) * Mathf.PI);
            grantCard.localScale = originalGrantScale * (1f + punch);
            Rect card = new Rect();
            card.min = grantLayer.InverseTransformPoint(grantCard.TransformPoint(grantCard.rect.min));
            card.max = grantLayer.InverseTransformPoint(grantCard.TransformPoint(grantCard.rect.max));
            grantFocus.anchoredPosition = card.center;
            Rect view = grantLayer.rect;
            float shade = Mathf.Min(1f, grantAge / .20f) * fade * .27f;
            PlaceGrantShade(0, view.xMin, view.yMin, card.xMin, view.yMax, shade);
            PlaceGrantShade(1, card.xMax, view.yMin, view.xMax, view.yMax, shade);
            PlaceGrantShade(2, card.xMin, card.yMax, card.xMax, view.yMax, shade);
            PlaceGrantShade(3, card.xMin, view.yMin, card.xMax, card.yMin, shade);
            float glint = (.48f + .52f * charge) * fade * Mathf.Clamp01(grantAge * 5f);
            Color glow = new Color(1f, .85f + .14f * charge, .43f + .34f * charge, glint);
            for (int i = 0; i < grantEdges.Length; i++)
            {
                bool horizontal = i < 2;
                float side = (i % 2 == 0 ? -1f : 1f) * .5f;
                grantEdges[i].rectTransform.anchoredPosition = horizontal ? new Vector2(0f, card.height * side) : new Vector2(card.width * side, 0f);
                float width = 2f + 4f * charge;
                grantEdges[i].rectTransform.sizeDelta = horizontal ? new Vector2(card.width, width) : new Vector2(width, card.height);
                grantEdges[i].color = glow;
            }
            grantHalo.rectTransform.sizeDelta = card.size * 1.28f;
            grantHalo.color = new Color(1f, .89f, .55f, charge * fade * (grantImpacted ? .47f : .23f));
            var badge = (RectTransform)grantBadge.transform;
            float intro = Mathf.SmoothStep(.60f, 1f, Mathf.Clamp01(grantAge / .28f));
            float rise = Mathf.Clamp01(after / .45f);
            badge.anchoredPosition = new Vector2(
                grantLocked && !grantImpacted ? Mathf.Sin(grantAge * 60f) * charge * 5f : 0f,
                Mathf.Lerp(-7f, 19f, intro) + (grantLocked ? 83f : 55f) * (1f - (1f - rise) * (1f - rise)));
            badge.localScale = Vector3.one * intro * (1f + charge * .10f);
            float tilt = grantLocked && !grantImpacted ? Mathf.Sin(grantAge * 51f) * charge * 14f : -rise * 17f;
            badge.localRotation = Quaternion.Euler(0f, 0f, tilt);
            grantBadge.alpha = Mathf.Clamp01(grantAge / .12f) * (1f - rise);
            grantStars.SetAge(grantImpacted ? after : -1f);
            if (after >= ColosseumUpgradeStarburst.Duration) FinishArenaCardGrant();
        }

        void PlaceGrantShade(int index, float left, float bottom, float right, float top, float alpha)
        {
            var image = grantBackdrop[index];
            image.rectTransform.anchoredPosition = new Vector2((left + right) * .5f, (top + bottom) * .5f);
            image.rectTransform.sizeDelta = new Vector2(Mathf.Max(0f, right - left), Mathf.Max(0f, top - bottom));
            image.color = new Color(.12f, .23f, .45f, alpha);
        }

        void FinishArenaCardGrant()
        {
            grantCard.localScale = originalGrantScale;
            if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject == grantLayer.gameObject)
                EventSystem.current.SetSelectedGameObject(tabs[HeroesTab].Plate.gameObject);
            grantLayer.gameObject.SetActive(false);
            UnityEngine.Object.Destroy(grantLayer.gameObject);
            grantLayer = null;
            grantCard = null;
            grantScroll = null;
            var complete = grantComplete;
            grantComplete = null;
            complete?.Invoke();
        }
    }
}
