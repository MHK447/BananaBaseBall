using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SlingArena.Colosseum
{
    /// <summary>
    /// The new player's way through the lobby after the first battles: open the chest that is waiting, see HEROES
    /// come unlocked, go in and level a hero up, and then back to battle. A hand points at whichever comes next.
    /// </summary>
    public sealed partial class ColosseumHud
    {
        RectTransform guideHand, guideBubble;
        TextMeshProUGUI guideWords;
        float guideAge, heroesUnlockAge = -1f;
        bool heroesLockSeen, battleNudge;
        Vector2 padlockHome;
        ColosseumGuideGate guideGate;
        CanvasGroup guideGateFade;
        Image[] guideShade;
        float gateAge;
        static readonly Color GuideShadeColor = new Color(.04f, .03f, .12f, .68f);

        /// <summary>True while YOUR TURN (or the rival's) is waiting to come on or still across the screen.</summary>
        public bool TurnCallShowing => turnCall.Waiting || turnCall.Root.gameObject.activeSelf;

        bool HeroesUnlockWaiting => heroesLockSeen && !ColosseumRoster.HeroesTabLocked;

        int NextLessonHero()
        {
            int recruit = ColosseumRoster.PendingRecruitUpgradeLesson;
            return recruit >= 0 && ColosseumRoster.CanUpgrade(recruit) ? recruit : ColosseumRoster.PendingUpgradeLesson;
        }

        RectTransform GuideTarget(out string words)
        {
            words = null;
            if (!LobbyReadyForUpgradeCoach || matchRoot.activeSelf || heroesUnlockAge >= 0f) return null;
            if (heroSelection != null && heroSelection.activeSelf) return null;
            if (ColosseumRoster.FirstBattleLocked || ColosseumRoster.NeedsArcherBattle) return null;
            if (ColosseumRoster.HeroesTabLocked)
                return openTab == BattleTab && chestDock != null && chestDock.activeSelf ? (RectTransform)chestDock.transform : null;
            if (HeroesUnlockWaiting) return null;
            if (openTab == BattleTab)
            {
                int hero = NextLessonHero();
                if (hero >= 0 && (hero != offeredLessonHero || hero == ColosseumRoster.PendingUpgradeLesson))
                    return tabs[HeroesTab].Plate.rectTransform;
            }
            if (battleNudge && openTab == HeroesTab && cardAction == CardAction.None && coachTarget == null)
                return tabs[BattleTab].Plate.rectTransform;
            if (battleNudge && openTab == BattleTab) return battleButton;
            return null;
        }

        void TickLobbyGuide(float delta)
        {
            if (heroesUnlockAge >= 0f && menu.activeInHierarchy) TickHeroesUnlock(delta);
            else if (HeroesUnlockWaiting && openTab == BattleTab && LobbyReadyForUpgradeCoach && !matchRoot.activeSelf)
            {
                heroesUnlockAge = 0f;
                TickHeroesUnlock(0f);
            }

            RectTransform target = GuideTarget(out string words);
            // The guide's own hand points at BATTLE now, above the shade, in place of the one on the button.
            if (battleNudge && firstBattleHand && firstBattleHand.gameObject.activeSelf) firstBattleHand.gameObject.SetActive(false);
            TickGuideGate(target, delta);
            bool unlocking = heroesUnlockAge >= .5f && menu.activeInHierarchy;
            if (target == null && !unlocking)
            {
                if (guideHand != null && guideHand.gameObject.activeSelf) guideHand.gameObject.SetActive(false);
                if (guideBubble != null && guideBubble.gameObject.activeSelf) guideBubble.gameObject.SetActive(false);
                guideAge = 0f;
                return;
            }
            if (guideHand == null)
            {
                guideHand = Icon(canvas, TutorialHand, HeroCenter, Vector2.zero, 124f, Color.white).rectTransform;
                guideHand.name = "Lobby guide hand";
                guideHand.pivot = Fingertip;
                guideHand.localRotation = Quaternion.Euler(0f, 0f, 156f);
                guideHand.GetComponent<Image>().raycastTarget = false;
                var face = Frame(canvas, HeroCenter, Vector2.zero, new Vector2(330f, 54f), new Color32(32, 58, 108, 255), 3f, 2f);
                guideBubble = (RectTransform)face.transform.parent;
                guideBubble.name = "Lobby guide words";
                foreach (var graphic in guideBubble.GetComponentsInChildren<Graphic>(true)) graphic.raycastTarget = false;
                guideWords = Label(face.transform, "", 27f, BattleYellow, HeroCenter, Vector2.zero, new Vector2(314f, 40f), bold);
                guideWords.raycastTarget = false;
            }
            guideAge += delta;
            if (unlocking) { target = tabs[HeroesTab].Plate.rectTransform; words = "HEROES UNLOCKED!"; }
            Vector2 lo = canvas.InverseTransformPoint(target.TransformPoint(target.rect.min));
            Vector2 hi = canvas.InverseTransformPoint(target.TransformPoint(target.rect.max));
            Vector2 center = (lo + hi) * .5f;

            bool hand = !unlocking;
            if (guideHand.gameObject.activeSelf != hand)
            {
                guideHand.gameObject.SetActive(hand);
                guideHand.SetAsLastSibling();
            }
            // The same tap as the hand on the BATTLE button: down onto the thing, a moment's press, and away.
            float beat = Mathf.Repeat(guideAge, 1.35f);
            float approach = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.12f, .43f, beat));
            float release = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.55f, .98f, beat));
            float tap = approach * (1f - release);
            guideHand.anchoredPosition = new Vector2(Mathf.Lerp(center.x, hi.x, .45f), Mathf.Lerp(hi.y + 44f, Mathf.Lerp(center.y, hi.y, .35f), tap));
            guideHand.localScale = Vector3.one * (1f - .07f * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.8f, 1f, tap)));

            bool speak = words != null;
            if (guideBubble.gameObject.activeSelf != speak)
            {
                guideBubble.gameObject.SetActive(speak);
                guideBubble.SetAsLastSibling();
                guideHand.SetAsLastSibling();
            }
            if (!speak) return;
            guideWords.text = words;
            Rect view = canvas.rect;
            float half = guideBubble.rect.width * .5f;
            guideBubble.anchoredPosition = new Vector2(Mathf.Clamp(center.x, view.xMin + half + 12f, view.xMax - half - 12f), hi.y + 36f);
            float pop = unlocking ? Mathf.Clamp01((heroesUnlockAge - .5f) / .22f) : 1f;
            float over = pop - 1f;
            guideBubble.localScale = Vector3.one * (unlocking ? 1f + over * over * (2.7f * over + 1.7f) : 1f);
        }

        /// <summary>
        /// While the guide is pointing at something, or the padlock is coming off, the screen is shaded all round the
        /// thing pointed at (the chest, a tab, the hero's card, LEVEL UP, the BATTLE button), which stays bright with a
        /// pulsing rim, and only it can be pressed.
        /// </summary>
        void TickGuideGate(RectTransform target, float delta)
        {
            RectTransform hole = null, lit = null;
            bool shut = false;
            if (menu.activeInHierarchy && !profileRoot.activeSelf)
            {
                if (heroesUnlockAge >= 0f) { shut = true; lit = tabs[HeroesTab].Plate.rectTransform; }
                else if (target != null) { shut = true; hole = lit = target; }
                else if (coachTarget != null && !contextualHint && upgradeCoach != null && upgradeCoach.gameObject.activeSelf)
                {
                    shut = true;
                    hole = lit = coachTarget;
                }
            }
            if (!shut)
            {
                if (guideGate != null && guideGate.gameObject.activeSelf) guideGate.gameObject.SetActive(false);
                gateAge = 0f;
                return;
            }
            if (guideGate == null)
            {
                Image sheet = Plain(canvas, HeroCenter, Vector2.zero, Vector2.zero, Color.clear);
                sheet.name = "Lobby guide gate";
                sheet.rectTransform.anchorMin = Vector2.zero;
                sheet.rectTransform.anchorMax = Vector2.one;
                sheet.rectTransform.offsetMin = sheet.rectTransform.offsetMax = Vector2.zero;
                sheet.raycastTarget = true;
                guideGate = sheet.gameObject.AddComponent<ColosseumGuideGate>();
                guideGateFade = sheet.gameObject.AddComponent<CanvasGroup>();
                guideShade = new Image[8];
                for (int i = 0; i < guideShade.Length; i++)
                {
                    guideShade[i] = Plain(sheet.transform, HeroCenter, Vector2.zero, Vector2.zero, i < 4 ? GuideShadeColor : (Color)BattleYellow);
                    guideShade[i].raycastTarget = false;
                }
            }
            guideGate.Hole = hole;
            if (!guideGate.gameObject.activeSelf) guideGate.gameObject.SetActive(true);
            gateAge += delta;
            guideGateFade.alpha = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(gateAge / .25f));
            PlaceGuideShade(lit);
            // It lies over whatever has opened since (the hero's panel, say), with only the guide's hands and words above it.
            int at = guideGate.transform.GetSiblingIndex();
            bool inPlace = true;
            for (int i = at + 1; i < canvas.childCount && inPlace; i++)
            {
                Transform above = canvas.GetChild(i);
                inPlace = above == upgradeCoach || above == guideBubble || above == guideHand;
            }
            if (!inPlace)
            {
                guideGate.transform.SetAsLastSibling();
                if (upgradeCoach != null) upgradeCoach.SetAsLastSibling();
                if (guideBubble != null) guideBubble.SetAsLastSibling();
                if (guideHand != null) guideHand.SetAsLastSibling();
            }
        }

        void PlaceGuideShade(RectTransform lit)
        {
            var sheet = (RectTransform)guideGate.transform;
            Rect full = sheet.rect;
            Rect cut = new Rect(full.center, Vector2.zero);
            if (lit != null)
            {
                Vector2 a = sheet.InverseTransformPoint(lit.TransformPoint(lit.rect.min));
                Vector2 b = sheet.InverseTransformPoint(lit.TransformPoint(lit.rect.max));
                const float pad = 10f;
                cut = Rect.MinMaxRect(Mathf.Max(full.xMin, Mathf.Min(a.x, b.x) - pad), Mathf.Max(full.yMin, Mathf.Min(a.y, b.y) - pad),
                    Mathf.Min(full.xMax, Mathf.Max(a.x, b.x) + pad), Mathf.Min(full.yMax, Mathf.Max(a.y, b.y) + pad));
            }
            ShadeBox(0, full.xMin, full.xMax, cut.yMax, full.yMax, full);
            ShadeBox(1, full.xMin, full.xMax, full.yMin, cut.yMin, full);
            ShadeBox(2, full.xMin, cut.xMin, cut.yMin, cut.yMax, full);
            ShadeBox(3, cut.xMax, full.xMax, cut.yMin, cut.yMax, full);
            bool rim = lit != null;
            float glow = .55f + .45f * Mathf.Sin(gateAge * 6f);
            const float t = 5f;
            for (int i = 4; i < 8; i++)
            {
                guideShade[i].enabled = rim;
                var c = guideShade[i].color; c.a = glow; guideShade[i].color = c;
            }
            if (!rim) return;
            ShadeBox(4, cut.xMin - t, cut.xMax + t, cut.yMax, cut.yMax + t, full);
            ShadeBox(5, cut.xMin - t, cut.xMax + t, cut.yMin - t, cut.yMin, full);
            ShadeBox(6, cut.xMin - t, cut.xMin, cut.yMin, cut.yMax, full);
            ShadeBox(7, cut.xMax, cut.xMax + t, cut.yMin, cut.yMax, full);
        }

        void ShadeBox(int index, float xMin, float xMax, float yMin, float yMax, Rect full)
        {
            RectTransform box = guideShade[index].rectTransform;
            box.anchorMin = box.anchorMax = box.pivot = new Vector2(.5f, .5f);
            box.sizeDelta = new Vector2(Mathf.Max(0f, xMax - xMin), Mathf.Max(0f, yMax - yMin));
            box.anchoredPosition = new Vector2((xMin + xMax) * .5f, (yMin + yMax) * .5f) - full.center;
        }

        /// <summary>The padlock on HEROES rattles, springs open and is gone, and the tab comes alive under it.</summary>
        void TickHeroesUnlock(float delta)
        {
            Tab tab = tabs[HeroesTab];
            var padlock = (RectTransform)tab.Lock.transform;
            float before = heroesUnlockAge;
            heroesUnlockAge += delta;
            float age = heroesUnlockAge;
            if (before <= 0f)
            {
                padlockHome = padlock.anchoredPosition;
                MatchSound?.Invoke("chest-shake");
            }
            if (age < .5f)
            {
                float rattle = age / .5f;
                padlock.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(age * 46f) * 16f * rattle);
                padlock.localScale = Vector3.one * (1f + .18f * rattle);
                return;
            }
            if (before < .5f)
            {
                MatchSound?.Invoke("stage-up");
                tab.Since = 0f;
                tab.Button.interactable = true;
                tab.Icon.GetComponent<Image>().material = null;
            }
            float burst = Mathf.Clamp01((age - .5f) / .3f);
            padlock.localRotation = Quaternion.Euler(0f, 0f, 28f * burst);
            padlock.localScale = Vector3.one * (1.18f + .7f * Mathf.Sin(burst * Mathf.PI) ) * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.55f, 1f, burst)));
            padlock.anchoredPosition = padlockHome + new Vector2(0f, 22f * Mathf.SmoothStep(0f, 1f, burst));
            // The words stay up a moment longer, and then the hand comes to point the way in.
            if (age < 2.4f) return;
            padlock.localRotation = Quaternion.identity;
            padlock.localScale = Vector3.one;
            padlock.anchoredPosition = padlockHome;
            heroesLockSeen = false;
            heroesUnlockAge = -1f;
            RefreshLobby(0);
        }
    }
}
