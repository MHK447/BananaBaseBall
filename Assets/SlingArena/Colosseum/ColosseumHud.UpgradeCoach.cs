using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SlingArena.Colosseum
{
    public sealed partial class ColosseumHud
    {
        Sprite tutorialHand;
        Sprite TutorialHand => tutorialHand != null ? tutorialHand : (tutorialHand = MakeFinger(128));

        RectTransform upgradeCoach, coachBubble, coachHand, coachTarget, coachViewport;
        TextMeshProUGUI coachHeading, coachInstruction;
        int coachedHero = -1, recentArenaHero = -1, offeredLessonHero = -1;
        float coachAge, battleTapAge;
        bool coachingButton, contextualHint;

        bool LobbyReadyForUpgradeCoach => menu.activeInHierarchy && !IsLaunchTitleOpen && !VictoryArrivalActive &&
            !ColosseumMatchRewards.HasPendingMatch && ColosseumMatchRewards.PendingArena == 0 &&
            !JourneyChestOpen && grantLayer == null && recruitHero < 0 && !profileRoot.activeSelf;

        // The game calls this only after the title, claims and unlock reveals have had their turn.
        public void ShowPendingUpgradeLesson()
        {
            if (!LobbyReadyForUpgradeCoach || coachTarget != null) return;
            if (ColosseumRoster.HeroesTabLocked || ColosseumRoster.NeedsArcherBattle || battleNudge)
            {
                if (openTab != BattleTab) return;
                // A chest that is waiting comes before the next battle: the guide hand points at that instead.
                if (ChestComesFirst)
                {
                    if (firstBattleHand) firstBattleHand.gameObject.SetActive(false);
                    return;
                }
                if (firstBattleHand == null)
                {
                    firstBattleHand = Icon(battleButton, TutorialHand, new Vector2(.82f, 1f), Vector2.zero, 118f, Color.white).rectTransform;
                    firstBattleHand.name = "Next battle hand";
                    firstBattleHand.pivot = Fingertip;
                    firstBattleHand.GetComponent<Image>().raycastTarget = false;
                    firstBattleHand.localRotation = Quaternion.Euler(0f, 0f, 156f);
                }
                firstBattleHand.gameObject.SetActive(true);
                TickBattleLesson(0f);
                return;
            }
            int recruit = ColosseumRoster.PendingRecruitUpgradeLesson;
            int hero = recruit >= 0 && ColosseumRoster.CanUpgrade(recruit) ? recruit : ColosseumRoster.PendingUpgradeLesson;
            bool firstLesson = hero >= 0 && hero != offeredLessonHero;
            if (!firstLesson)
            {
                hero = recentArenaHero;
                if (hero < 0 || !ColosseumRoster.CanUpgrade(hero)) return;
            }
            if (openTab != HeroesTab)
            {
                // The first upgrade is the player's to walk to: the guide hand points at HEROES (see TickLobbyGuide).
                if (firstLesson) return;
                recentArenaHero = -1;
                OpenTab(HeroesTab);
            }
            recentArenaHero = -1;
            if (heroesPage == null) return;
            RectTransform card = heroesPage.FocusRewardCard(hero, out ScrollRect scroll, out float destination);
            if (card == null) return;
            if (scroll != null) scroll.verticalNormalizedPosition = destination;
            string name = ColosseumRoster.Heroes[hero].Name;
            if (firstLesson) offeredLessonHero = hero;
            PresentUpgradeCoach(hero, card, false, !firstLesson,
                firstLesson && hero != recruit ? "YOUR FIRST UPGRADE" : name + " UNLOCKED!",
                firstLesson ? "Tap " + name + " in your collection." : "Tap the card to level up.");
        }

        public void HideBattleLesson()
        {
            if (firstBattleHand) firstBattleHand.gameObject.SetActive(false);
            battleTapAge = 0f;
            battleNudge = false;
        }

        bool ChestComesFirst => ColosseumRoster.HeroesTabLocked && !ColosseumRoster.NeedsArcherBattle &&
            !ColosseumRoster.FirstBattleLocked && ColosseumSave.FirstAvailableChest() > 0;

        void TickBattleLesson(float delta)
        {
            battleTapAge = Mathf.Repeat(battleTapAge + delta, 1.35f);
            float approach = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.12f, .43f, battleTapAge));
            float release = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.55f, .98f, battleTapAge));
            float tap = approach * (1f - release);
            firstBattleHand.anchoredPosition = new Vector2(0f, Mathf.Lerp(28f, -40f, tap));
            float contact = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.8f, 1f, tap));
            firstBattleHand.localScale = Vector3.one * (1f - .07f * contact);
            battleButton.localScale = new Vector3(1f + .012f * contact, 1f - .05f * contact, 1f);
        }

        void CoachSelectedHero(int hero)
        {
            bool newRecruit = contextualHint && coachedHero == hero;
            HideUpgradeCoach();
            if (!LobbyReadyForUpgradeCoach) return;
            if (hero != ColosseumRoster.PendingUpgradeLesson && hero != ColosseumRoster.PendingRecruitUpgradeLesson && !newRecruit) return;
            if (!ColosseumRoster.CanUpgrade(hero)) return;
            int copies = ColosseumRoster.CardsNeeded(hero), coins = ColosseumRoster.UpgradeCost(hero);
            PresentUpgradeCoach(hero, (RectTransform)upgradeHero.transform, true, newRecruit,
                "TAP LEVEL UP", copies + " cards + " + coins + " coins boost attack and health.");
        }

        void PresentUpgradeCoach(int hero, RectTransform target, bool button, bool brief, string heading, string instruction)
        {
            if (upgradeCoach == null)
            {
                upgradeCoach = (RectTransform)Group("Hero upgrade hint", canvas, true).transform;
                // The real collection card and Level Up button receive the tap, even through the hand.
                var input = upgradeCoach.gameObject.AddComponent<CanvasGroup>();
                input.interactable = input.blocksRaycasts = false;
                var face = Frame(upgradeCoach, HeroCenter, Vector2.zero, new Vector2(434f, 83f),
                    new Color32(32, 58, 108, 255), 3f, 2f);
                coachBubble = (RectTransform)face.transform.parent;
                coachHeading = Label(face.transform, "", 26f, BattleYellow, HeroCenter,
                    new Vector2(0f, 14f), new Vector2(412f, 32f), bold);
                coachInstruction = Label(face.transform, "", 20f, Color.white, HeroCenter,
                    new Vector2(0f, -15f), new Vector2(406f, 31f), slim);
                coachInstruction.enableAutoSizing = true;
                coachInstruction.fontSizeMin = 15f;
                coachInstruction.fontSizeMax = 20f;
                coachHand = Icon(upgradeCoach, TutorialHand, HeroCenter, Vector2.zero, 124f, Color.white).rectTransform;
                coachHand.pivot = Fingertip;
                coachHand.localRotation = Quaternion.Euler(0f, 0f, 23f);
            }
            coachTarget = target;
            coachViewport = button ? null : target.GetComponentInParent<ScrollRect>()?.viewport;
            coachedHero = hero;
            coachingButton = button;
            contextualHint = brief;
            coachAge = 0f;
            coachHeading.text = heading;
            coachInstruction.text = instruction;
            upgradeCoach.gameObject.SetActive(true);
            upgradeCoach.SetAsLastSibling();
            TickUpgradeCoach(0f);
        }

        void HideUpgradeCoach()
        {
            coachTarget = null;
            coachedHero = -1;
            contextualHint = false;
            if (upgradeCoach != null) upgradeCoach.gameObject.SetActive(false);
        }

        void TickUpgradeCoach(float delta)
        {
            if (coachedHero < 0) return;
            coachAge += delta;
            if (coachTarget == null || !coachTarget.gameObject.activeInHierarchy || openTab != HeroesTab ||
                (contextualHint && coachAge >= 6f) || profileRoot.activeSelf ||
                grantLayer != null || recruitHero >= 0 || (!coachingButton && heroSelection != null && heroSelection.activeSelf) ||
                (coachViewport != null && !coachViewport.rect.Contains(coachViewport.InverseTransformPoint(
                    coachTarget.TransformPoint(coachTarget.rect.center)))))
            {
                HideUpgradeCoach();
                return;
            }
            Vector2 lo = canvas.InverseTransformPoint(coachTarget.TransformPoint(coachTarget.rect.min));
            Vector2 hi = canvas.InverseTransformPoint(coachTarget.TransformPoint(coachTarget.rect.max));
            Vector2 center = (lo + hi) * .5f;
            Rect view = canvas.rect;
            // Keep both lines readable when the card sits at either end of a row.
            float half = coachBubble.rect.width * .5f;
            float x = Mathf.Clamp(center.x, view.xMin + half + 12f, view.xMax - half - 12f);
            float y = coachingButton ? lo.y - 79f : hi.y + 52f;
            if (y < view.yMin + 60f) y = hi.y + 53f;
            if (y > view.yMax - 62f) y = lo.y - 63f;
            coachBubble.anchoredPosition = new Vector2(x, y);
            float lift = .5f + .5f * Mathf.Cos(coachAge * 6f);
            coachHand.anchoredPosition = new Vector2(Mathf.Lerp(center.x, hi.x, .53f) + lift * 6f,
                Mathf.Lerp(lo.y, center.y, .47f) - lift * 20f);
            coachHand.localScale = Vector3.one * (.9f + lift * .1f);
        }
    }
}
