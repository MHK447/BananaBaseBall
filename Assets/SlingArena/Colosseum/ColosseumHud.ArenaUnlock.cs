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
        GameObject arenaUnlockOverlay;
        CanvasGroup arenaUnlockGroup;
        RectTransform arenaUnlockLayout, arenaUnlockShowcase, arenaUnlockRewards, arenaUnlockConfirm;
        RawImage arenaUnlockIllustration;
        TextMeshProUGUI arenaUnlockTitle, arenaUnlockStageName, arenaUnlockStageNumber;
        TextMeshProUGUI arenaEventNames, arenaRecruitBonus;
        Image arenaEventPlate;
        readonly List<RewardHeroCard> arenaUnlockCards = new List<RewardHeroCard>();
        Action confirmNewArena;
        float arenaUnlockAge;

        public void ShowArenaUnlock(int stage, IReadOnlyList<int> heroes, Action onConfirm)
        {
            if (arenaUnlockOverlay == null) BuildArenaUnlock();
            var texture = ColosseumJourneyMaps.Texture(stage, firstArena, out _);
            arenaUnlockIllustration.texture = texture;
            arenaUnlockIllustration.enabled = texture != null;
            if (texture != null)
            {
                float fit = Mathf.Min(505f / texture.width, 325f / texture.height);
                arenaUnlockIllustration.rectTransform.sizeDelta = new Vector2(texture.width * fit, texture.height * fit);
            }
            arenaUnlockStageName.text = ColosseumJourneyMaps.Name(stage);
            arenaUnlockStageNumber.text = "ARENA " + stage.ToString("00");
            var balance = ColosseumEventTable.Load();
            var eventNames = new List<string>();
            foreach (string key in ColosseumEventTable.Keys)
            {
                var info = balance.Get(key);
                if (info.UnlockArena != stage || info.Appear == ColosseumEventTable.Appear.Off) continue;
                var style = DecreeStyle(key);
                eventNames.Add(KoreanDecree ? style.koreanTitle : style.title);
            }
            arenaEventPlate.gameObject.SetActive(eventNames.Count > 0);
            arenaEventNames.text = (KoreanDecree ? "새 이벤트   " : "NEW EVENTS   ") + string.Join("  +  ", eventNames);

            int visible = 0;
            arenaRecruitBonus.text = "";
            for (int i = 0; heroes != null && i < heroes.Count; i++)
            {
                int hero = heroes[i];
                if (hero < 0 || hero >= ColosseumRoster.Heroes.Length) continue;
                int copies = 1;
                if (arenaRecruitBonus.text.Length == 0 && ColosseumRoster.FirstArenaRecruit(hero))
                {
                    int extra = ColosseumRoster.RecruitUpgradeCopies(hero);
                    int coins = ColosseumRoster.RecruitUpgradeCoins(hero);
                    copies += extra;
                    arenaRecruitBonus.text = KoreanDecree
                        ? "해금 카드 1장 + 강화 카드 " + extra + "장" + (coins > 0 ? "   ·   +" + coins + " 코인" : "")
                        : "1 unlock + " + extra + " upgrade cards" + (coins > 0 ? "   •   +" + coins + " coins" : "");
                }
                BindRewardCard(arenaUnlockCards, visible++, arenaUnlockRewards, hero, copies);
            }
            LayoutRewardCards(arenaUnlockCards, visible, -12f);
            arenaUnlockRewards.gameObject.SetActive(visible > 0);
            arenaUnlockTitle.rectTransform.anchoredPosition = new Vector2(0f, visible > 0 ? 438f : 326f);
            arenaUnlockShowcase.anchoredPosition = new Vector2(0f, visible > 0 ? 174f : 56f);
            arenaEventPlate.rectTransform.anchoredPosition = new Vector2(0f, visible > 0 ? -42f : -163f);
            arenaUnlockConfirm.anchoredPosition = new Vector2(0f, visible > 0 ? -442f : -242f);
            arenaUnlockAge = 0f;
            confirmNewArena = onConfirm;
            arenaUnlockOverlay.SetActive(true);
            arenaUnlockOverlay.transform.SetAsLastSibling();
            arenaUnlockConfirm.GetComponent<Button>().interactable = true;
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(arenaUnlockConfirm.gameObject);
            MatchSound?.Invoke("arena-unlock");
            TickArenaUnlock(0f);
        }

        public void HideArenaUnlock()
        {
            confirmNewArena = null;
            if (arenaUnlockOverlay == null || !arenaUnlockOverlay.activeSelf) return;
            var events = EventSystem.current;
            var selected = events == null ? null : events.currentSelectedGameObject;
            bool restoreFocus = events != null && menu.activeInHierarchy
                && (selected == null || selected == arenaUnlockConfirm.gameObject || selected.transform.IsChildOf(arenaUnlockConfirm));
            arenaUnlockOverlay.SetActive(false);
            if (restoreFocus) events.SetSelectedGameObject(tabs[openTab].Plate.gameObject);
        }

        void ConfirmArenaUnlock()
        {
            Action callback = confirmNewArena;
            if (callback == null) return;
            confirmNewArena = null;
            arenaUnlockConfirm.GetComponent<Button>().interactable = false;
            callback();
        }

        public void TickArenaUnlock(float delta)
        {
            if (arenaUnlockOverlay == null || !arenaUnlockOverlay.activeSelf) return;
            FitRewardLayout(arenaUnlockLayout);
            arenaUnlockAge += delta;
            arenaUnlockGroup.alpha = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(arenaUnlockAge / .2f));
            arenaUnlockShowcase.localScale = Vector3.one * RewardSettle(arenaUnlockAge, 0f);
            arenaUnlockRewards.localScale = Vector3.one * RewardSettle(arenaUnlockAge, .15f);
            arenaUnlockConfirm.localScale = Vector3.one * RewardSettle(arenaUnlockAge, .26f);
        }

        void BuildArenaUnlock()
        {
            var layout = arenaUnlockLayout = MakeRewardScene("Arena unlocked", out arenaUnlockOverlay, out arenaUnlockGroup);
            arenaUnlockTitle = Label(layout, "New Arena", 61f, Color.white, RewardCenter, Vector2.zero, new Vector2(644f, 95f), bold);
            arenaUnlockStageNumber = Label(arenaUnlockTitle.transform, "", 21f, new Color32(255, 220, 132, 255), RewardCenter, new Vector2(0f, -47f), new Vector2(220f, 32f), slim);
            arenaUnlockShowcase = Group("New arena artwork", layout).GetComponent<RectTransform>();
            RewardLight(arenaUnlockShowcase, new Vector2(0f, 22f), 535f, new Color32(146, 236, 255, 160));
            arenaUnlockIllustration = new GameObject("Arena illustration", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
            Place(arenaUnlockIllustration.rectTransform, arenaUnlockShowcase, RewardCenter, Vector2.zero, new Vector2(500f, 320f));
            arenaUnlockIllustration.raycastTarget = false;
            var banner = Group("Arena name ribbon", arenaUnlockShowcase).GetComponent<RectTransform>();
            banner.anchoredPosition = new Vector2(0f, -154f);
            Sprite tail = Paint("Forked arena ribbon", 80, (x, y) => x > -.53f - .46f * Mathf.Abs(y) && Mathf.Abs(y) < .95f);
            for (int side = -1; side <= 1; side += 2)
            {
                var outline = Icon(banner, tail, RewardCenter, new Vector2(side * 184f, -8f), 1f, RewardInk);
                outline.rectTransform.sizeDelta = new Vector2(95f, 66f);
                if (side > 0) outline.rectTransform.localScale = new Vector3(-1f, 1f, 1f);
                var cloth = Icon(outline.transform, tail, RewardCenter, Vector2.zero, 1f, new Color32(245, 174, 44, 255));
                cloth.rectTransform.sizeDelta = new Vector2(85f, 56f);
            }
            var face = Frame(banner, RewardCenter, Vector2.zero, new Vector2(365f, 72f), new Color32(255, 198, 54, 255), 4f, 1.6f);
            arenaUnlockStageName = Label(face.transform, "", 34f, Cream, RewardCenter, new Vector2(0f, 1f), new Vector2(342f, 58f), bold);
            arenaUnlockStageName.enableAutoSizing = true;
            arenaUnlockStageName.fontSizeMin = 22f;
            arenaUnlockStageName.fontSizeMax = 34f;

            arenaEventPlate = Box(layout, RewardCenter, Vector2.zero, new Vector2(610f, 41f), new Color32(49, 46, 96, 235));
            arenaEventNames = Label(arenaEventPlate.transform, "", 23f, Cream, RewardCenter, Vector2.zero, new Vector2(594f, 40f), slim);
            arenaEventNames.enableAutoSizing = true;
            arenaEventNames.fontSizeMin = 15f;
            arenaEventNames.fontSizeMax = 23f;

            arenaUnlockRewards = Group("Unlocked cards", layout).GetComponent<RectTransform>();
            arenaUnlockRewards.anchoredPosition = new Vector2(0f, -221f);
            Label(arenaUnlockRewards, "Arena Rewards", 31f, Color.white, RewardCenter, new Vector2(0f, 135f), new Vector2(610f, 52f), bold);
            arenaRecruitBonus = Label(arenaUnlockRewards, "", 22f, Cream, RewardCenter,
                new Vector2(0f, -147f), new Vector2(630f, 33f), slim);
            arenaUnlockConfirm = Press(layout, "Confirm", RewardCenter, new Vector2(0f, -438f), new Vector2(340f, 82f), RewardLilac, RewardLip, 43f, ConfirmArenaUnlock);
            arenaUnlockConfirm.GetComponent<Button>().navigation = new Navigation { mode = Navigation.Mode.None };
            arenaUnlockOverlay.SetActive(false);
        }
    }
}
