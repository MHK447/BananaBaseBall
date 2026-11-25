using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SlingArena.Colosseum
{
    public sealed partial class ColosseumHud
    {
        GameObject recruitOverlay;
        CanvasGroup recruitGroup;
        ColosseumRecruitPortal recruitPortal;
        RectTransform recruitCard;
        Image recruitRim, recruitStock, recruitPortrait;
        ColosseumLevelStars recruitStars;
        TextMeshProUGUI recruitName, recruitSkill, recruitHeading, recruitContinue;
        int recruitHero = -1;
        float recruitAge;
        bool recruitAtResults, recruitSoundPlayed, recruitPortalHeard, recruitUnboxed;

        void BuildRecruitReveal()
        {
            recruitOverlay = Group("New hero reveal", canvas, true);
            recruitGroup = recruitOverlay.AddComponent<CanvasGroup>();
            recruitPortal = recruitOverlay.AddComponent<ColosseumRecruitPortal>();
            recruitPortal.raycastTarget = true;
            Button tap = recruitOverlay.AddComponent<Button>();
            tap.targetGraphic = recruitPortal;
            tap.transition = Selectable.Transition.None;
            tap.navigation = new Navigation { mode = Navigation.Mode.None };
            tap.onClick.AddListener(CollectRevealedHero);

            var center = new Vector2(.5f, .5f);
            var root = recruitOverlay.transform;
            recruitHeading = Label(root, "HERO UNLOCKED!", 47f, Color.white, center, new Vector2(0f, 337f), new Vector2(620f, 71f), bold);
            var ink = Box(root, center, Vector2.zero, new Vector2(251f, 314f), Ink, 1.15f);
            recruitCard = ink.rectTransform;
            recruitRim = Box(recruitCard, center, Vector2.zero, new Vector2(241f, 304f), Color.white, 1.16f);
            recruitStock = Box(recruitRim.transform, center, Vector2.zero, new Vector2(227f, 290f), Color.white, 1.25f);
            Shade(recruitStock, .34f, .22f);
            recruitPortrait = Plain(recruitStock.transform, center, new Vector2(0f, 56f), new Vector2(252f, 252f), Color.white);
            recruitPortrait.preserveAspect = true;
            recruitStars = ColosseumLevelStars.Create(recruitStock.transform, new Vector2(0f, -67f), new Vector2(116f, 25f));
            recruitName = Label(recruitStock.transform, "", 33f, Color.white, center, new Vector2(0f, -96f), new Vector2(220f, 41f), bold);
            recruitSkill = Label(recruitStock.transform, "", 18f, Cream, center, new Vector2(0f, -127f), new Vector2(206f, 26f), slim);
            var sticker = Box(recruitCard, center, new Vector2(102f, 125f), new Vector2(85f, 44f), Ink, 2.5f);
            sticker.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -12f);
            var stickerFace = Box(sticker.transform, center, Vector2.zero, new Vector2(78f, 37f), new Color32(242, 43, 123, 255), 2.6f);
            Label(stickerFace.transform, "NEW", 26f, Color.white, center, Vector2.zero, new Vector2(77f, 35f), bold);
            recruitContinue = Label(root, "TAP TO CONTINUE", 23f, Cream, center, new Vector2(0f, -352f), new Vector2(400f, 44f), slim);
        }

        /// <summary>New draws wait for acknowledgement, including when the game is closed during a reveal.</summary>
        public void ShowPendingRecruit(float delay = 0f)
        {
            if (recruitHero >= 0 || grantLayer != null) return;
            // From the goblin on, a hero won by wins is unlocked on its own card in HEROES, and that is all.
            int unlock = ColosseumRoster.NextWindowRecruit();
            if (unlock >= 0 && menu.activeInHierarchy && !ColosseumRoster.HeroesTabLocked)
            {
                ShowArenaCardGrant(unlock, () => ColosseumRoster.CompleteWindowRecruit(unlock), () =>
                {
                    recentArenaHero = -1;
                    ShowPendingRecruit();
                });
                return;
            }
            int next = ColosseumRoster.NextRecruitReveal();
            if (next < 0)
            {
                ShowPendingUpgradeLesson();
                return;
            }
            if (recruitOverlay == null) BuildRecruitReveal();
            recruitAtResults = endButtons.activeSelf;
            if (recruitAtResults) endButtons.SetActive(false);
            lobbyInput.interactable = false;
            lobbyInput.blocksRaycasts = false;
            recruitOverlay.SetActive(true);
            recruitOverlay.transform.SetAsLastSibling();
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(recruitOverlay);
            StartRecruitReveal(next, delay);
        }

        void StartRecruitReveal(int hero, float delay)
        {
            recruitHero = hero;
            recruitAge = -delay;
            recruitSoundPlayed = false;
            recruitPortalHeard = false;
            var data = ColosseumRoster.Heroes[hero];
            recruitName.text = data.Name;
            bool firstArcher = hero == ColosseumRoster.Archer && ColosseumRoster.NeedsArcherBattle;
            recruitSkill.text = firstArcher ? "ADDED TO YOUR TEAM" : data.Skill;
            recruitContinue.text = firstArcher ? "TAP TO CONTINUE TO BATTLE" : "TAP TO CONTINUE";
            recruitRim.color = ColosseumHeroCardView.RarityColor(data.Rarity);
            recruitStock.color = Color.Lerp(recruitRim.color, Color.white, .33f);
            recruitPortrait.sprite = ColosseumHeroCardView.Fighter(hero);
            recruitStars.SetLevel(ColosseumRoster.Level(hero));
            TickRecruitReveal(0f);
        }

        void TickRecruitReveal(float delta)
        {
            if (recruitHero < 0) return;
            recruitAge += delta;
            float time = Mathf.Max(0f, recruitAge);
            if (recruitAge >= 0f && !recruitPortalHeard)
            {
                recruitPortalHeard = true;
                MatchSound?.Invoke("recruit-portal");
            }
            recruitGroup.alpha = Mathf.SmoothStep(0f, 1f, time / .22f);
            recruitPortal.SetTime(time);
            float rise = Mathf.Clamp01((time - .25f) / .65f);
            float remaining = rise - 1f;
            float pop = 1f + remaining * remaining * (2.28f * remaining + 1.28f);
            float idle = Mathf.Max(0f, time - .9f);
            float bob = 5f * Mathf.Sin(idle * 2.25f);
            recruitCard.anchoredPosition = new Vector2(0f, Mathf.LerpUnclamped(-170f, 32f, pop) + bob);
            float turn = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.06f, .7f, rise));
            recruitCard.localScale = new Vector3(pop * Mathf.Lerp(.12f, 1f, turn), pop, 1f);
            recruitCard.localRotation = Quaternion.Euler(0f, 0f, -11f * (1f - turn) + 1.5f * Mathf.Sin(idle * 1.8f));
            recruitHeading.alpha = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.36f, .74f, time));
            recruitContinue.alpha = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.90f, 1.28f, time)) * (.86f + .14f * Mathf.Cos(idle * 2.4f));
            if (time >= .49f && !recruitSoundPlayed)
            {
                recruitSoundPlayed = true;
                MatchSound?.Invoke("recruit-reveal");
            }
        }

        void CollectRevealedHero()
        {
            if (recruitHero < 0 || recruitAge < .90f) return;
            ColosseumRoster.AcknowledgeRecruit(recruitHero);
            int next = ColosseumRoster.NextRecruitReveal();
            if (next >= 0)
            {
                StartRecruitReveal(next, 0f);
                return;
            }
            recruitHero = -1;
            recruitOverlay.SetActive(false);
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
            lobbyInput.interactable = true;
            lobbyInput.blocksRaycasts = true;
            if (recruitAtResults) endButtons.SetActive(true);
            else ShowPendingUpgradeLesson();
        }
    }
}
