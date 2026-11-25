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
        static readonly Vector2 RewardCenter = new Vector2(.5f, .5f);
        static readonly Color32 RewardInk = new Color32(36, 26, 70, 255);
        static readonly Color32 RewardLilac = new Color32(176, 106, 248, 255);
        static readonly Color32 RewardLip = new Color32(103, 55, 178, 255);
        GameObject victoryOverlay, winningCrest, defeatCrest, trophyEarnings;
        RectTransform victoryLayout, victoryEmblem, victoryPack, victoryClaim;
        Image victoryTrophyOrigin, resultCoinOrigin, resultPackRim, resultPackFace, resultPackHeader;
        TextMeshProUGUI victoryCoins, victoryTrophies, resultHeading, resultPackTitle;
        CanvasGroup victoryGroup;
        float victoryAge;
        bool resultWon;
        Action claimVictory;
        readonly List<PackHeroCard> victoryCards = new List<PackHeroCard>();
        ColosseumHeroCardView packCardPrefab;

        sealed class PackHeroCard
        {
            internal int Hero;
            internal RectTransform Root;
            internal ColosseumHeroCardView View;
            internal TextMeshProUGUI Amount;
        }

        sealed class RewardHeroCard
        {
            internal int Hero;
            internal RectTransform Root;
            internal Image Stock, Fighter, Band;
            internal TextMeshProUGUI Name, Amount;
        }

        public void ShowVictoryRewards(int trophies, int coins, IReadOnlyList<int> cardsByHero, Action onClaim)
            => ShowMatchRewards(true, trophies, coins, cardsByHero, onClaim);

        public void ShowDefeatRewards(int coins, IReadOnlyList<int> cardsByHero, Action onClaim)
            => ShowMatchRewards(false, 0, coins, cardsByHero, onClaim);

        void ShowMatchRewards(bool won, int trophies, int coins, IReadOnlyList<int> cardsByHero, Action onClaim)
        {
            if (victoryOverlay == null) BuildVictoryRewards();
            SetResultStyle(won);
            victoryOverlay.SetActive(true);
            victoryOverlay.transform.SetAsLastSibling();
            claimVictory = onClaim;
            victoryAge = 0f;
            MatchSound?.Invoke(won ? "reward-show" : "reward-show-lose");
            victoryTrophies.text = won ? "+" + trophies : "";
            victoryCoins.text = "+" + coins;

            int visible = 0;
            for (int i = 0; cardsByHero != null && i < Mathf.Min(cardsByHero.Count, ColosseumRoster.Heroes.Length); i++)
            {
                if (cardsByHero[i] <= 0) continue;
                BindPackCard(victoryCards, visible++, victoryPack, i, cardsByHero[i], coins);
            }
            for (int slot = 0; slot < victoryCards.Count; slot++)
                PlaceRewardCard(victoryCards[slot].Root, slot, visible, 8f, 264f);
            victoryPack.gameObject.SetActive(visible > 0);
            victoryClaim.anchoredPosition = new Vector2(0f, visible > 0 ? -426f : -117f);
            victoryClaim.GetComponent<Button>().interactable = true;
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(victoryClaim.gameObject);
            TickVictoryRewards(0f);
        }

        public void HideVictoryRewards()
        {
            claimVictory = null;
            if (victoryOverlay != null) victoryOverlay.SetActive(false);
        }

        void CollectVictoryRewards()
        {
            Action callback = claimVictory;
            if (callback == null) return;
            claimVictory = null;
            victoryClaim.GetComponent<Button>().interactable = false;
            if (resultWon) CaptureVictoryTrophyOrigin(victoryTrophyOrigin.rectTransform);
            else CaptureChestCoinOrigin(resultCoinOrigin.rectTransform);
            foreach (var card in victoryCards)
                if (card.Root.gameObject.activeSelf) CaptureVictoryCardOrigin(card.Hero, card.Root);
            callback();
        }

        public void TickVictoryRewards(float delta)
        {
            if (victoryOverlay == null || !victoryOverlay.activeSelf) return;
            FitRewardLayout(victoryLayout);
            victoryAge += delta;
            victoryGroup.alpha = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(victoryAge / .16f));
            victoryEmblem.localScale = Vector3.one * RewardSettle(victoryAge, 0f);
            victoryPack.localScale = Vector3.one * RewardSettle(victoryAge, .13f);
            victoryClaim.localScale = Vector3.one * RewardSettle(victoryAge, .24f);
        }

        static float RewardSettle(float elapsed, float delay)
        {
            float t = Mathf.Clamp01((elapsed - delay) / .42f);
            return Mathf.Lerp(.85f, 1f, Mathf.SmoothStep(0f, 1f, t)) + .095f * Mathf.Sin(t * Mathf.PI);
        }

        void BuildVictoryRewards()
        {
            RectTransform layout = victoryLayout = MakeRewardScene("Victory rewards", out victoryOverlay, out victoryGroup);
            resultHeading = Label(layout, "Victory", 85f, Color.white, RewardCenter, new Vector2(0f, 419f), new Vector2(632f, 118f), bold);

            victoryEmblem = Group("Victory crest", layout).GetComponent<RectTransform>();
            victoryEmblem.anchoredPosition = new Vector2(0f, 243f);
            winningCrest = Group("Victory trophy", victoryEmblem);
            var crest = winningCrest.transform;
            RewardLight(crest, Vector2.zero, 480f, new Color32(255, 205, 119, 107));
            BuildVictoryLaurels((RectTransform)Group("Golden laurels", victoryEmblem).transform);
            Art(crest, "lobby-cup", RewardCenter, new Vector2(0f, 2f), 179f);
            var sparkle = Paint("Celebration glint", 48, (x, y) => Mathf.Sqrt(Mathf.Abs(x)) + Mathf.Sqrt(Mathf.Abs(y)) < 1f);
            Icon(crest, sparkle, RewardCenter, new Vector2(-65f, 84f), 28f, Cream);
            Icon(crest, sparkle, RewardCenter, new Vector2(86f, 48f), 20f, Cream);

            RectTransform earnings = Group("Match rewards", layout).GetComponent<RectTransform>();
            earnings.anchoredPosition = new Vector2(0f, 57f);
            trophyEarnings = Group("Trophy gain", earnings);
            Box(trophyEarnings.transform, RewardCenter, Vector2.zero, new Vector2(3f, 50f), new Color32(202, 192, 247, 184), 4f);
            resultCoinOrigin = Art(earnings, "coin", RewardCenter, new Vector2(-137f, 0f), 59f);
            victoryCoins = Label(earnings, "", 43f, Cream, RewardCenter, new Vector2(-67f, 1f), new Vector2(107f, 63f), bold);
            victoryTrophyOrigin = Art(trophyEarnings.transform, "lobby-cup", RewardCenter, new Vector2(74f, 0f), 58f);
            victoryTrophies = Label(trophyEarnings.transform, "", 43f, Cream, RewardCenter, new Vector2(146f, 1f), new Vector2(110f, 63f), bold);

            victoryPack = Group("Win Pack", layout).GetComponent<RectTransform>();
            victoryPack.anchoredPosition = new Vector2(0f, -191f);
            Box(victoryPack, RewardCenter, new Vector2(0f, -8f), new Vector2(612f, 327f), RewardInk, .88f);
            resultPackRim = Box(victoryPack, RewardCenter, Vector2.zero, new Vector2(609f, 326f), Color.white, .9f);
            resultPackFace = Box(resultPackRim.transform, RewardCenter, Vector2.zero, new Vector2(601f, 318f), Color.white, .95f);
            Shade(resultPackFace, .12f, .15f);
            resultPackHeader = Frame(victoryPack, RewardCenter, new Vector2(0f, 157f), new Vector2(236f, 57f), Color.white, 4f);
            resultPackTitle = Label(resultPackHeader.transform, "", 34f, Color.white, RewardCenter, Vector2.zero, new Vector2(218f, 53f), bold);
            victoryClaim = Press(layout, "Claim", RewardCenter, new Vector2(0f, -406f), new Vector2(345f, 86f), RewardLilac, RewardLip, 48f, CollectVictoryRewards);
            victoryClaim.GetComponent<Button>().navigation = new Navigation { mode = Navigation.Mode.None };
            victoryOverlay.SetActive(false);
        }

        void SetResultStyle(bool won)
        {
            resultWon = won;
            if (!won && defeatCrest == null)
            {
                defeatCrest = Group("Skull crest", victoryEmblem);
                defeatCrest.transform.SetAsFirstSibling();
                RewardLight(defeatCrest.transform, Vector2.zero, 425f, new Color32(255, 153, 123, 93));
                var skull = Icon(defeatCrest.transform, Drawn("defeat-skull"), RewardCenter, new Vector2(0f, 7f), 245f, Color.white);
                skull.name = "Defeat skull";
                skull.preserveAspect = true;
            }
            winningCrest.SetActive(won);
            if (defeatCrest != null) defeatCrest.SetActive(!won);
            trophyEarnings.SetActive(won);
            resultCoinOrigin.rectTransform.anchoredPosition = new Vector2(won ? -137f : -41f, 0f);
            victoryCoins.rectTransform.anchoredPosition = new Vector2(won ? -67f : 31f, 1f);
            victoryOverlay.name = won ? "Victory rewards" : "Defeat rewards";
            resultHeading.text = won ? "Victory" : "Defeat";
            resultHeading.color = won ? Color.white : new Color32(255, 163, 146, 255);
            resultPackTitle.text = victoryPack.name = won ? "Win Pack" : "Lose Pack";
            var sky = victoryOverlay.GetComponent<ColosseumLeagueSky>();
            sky.SetGradient(won ? new Color32(44, 39, 85, 255) : new Color32(115, 37, 51, 255),
                won ? new Color32(94, 83, 166, 255) : new Color32(194, 75, 81, 255));
            resultPackRim.color = won ? new Color32(174, 155, 255, 255) : new Color32(252, 155, 151, 255);
            resultPackFace.color = won ? new Color32(110, 96, 188, 255) : new Color32(175, 76, 94, 255);
            resultPackHeader.color = won ? new Color32(109, 83, 182, 255) : new Color32(152, 58, 79, 255);
        }

        RectTransform MakeRewardScene(string title, out GameObject overlay, out CanvasGroup group,
            ColosseumBackdropTiles.Motif motif = ColosseumBackdropTiles.Motif.SwordsAndHelmets)
        {
            overlay = Group(title, canvas, true);
            group = overlay.AddComponent<CanvasGroup>();
            var sky = overlay.AddComponent<ColosseumLeagueSky>();
            sky.SetGradient(new Color32(44, 39, 85, 255), new Color32(94, 83, 166, 255));
            sky.raycastTarget = true;
            ColosseumBackdropTiles.Add((RectTransform)overlay.transform, motif, new Color32(214, 209, 255, 15));
            var layout = Group("Content", overlay.transform).GetComponent<RectTransform>();
            layout.sizeDelta = new Vector2(670f, 1000f);
            return layout;
        }

        void FitRewardLayout(RectTransform layout)
        {
            Rect safe = Screen.safeArea;
            if (safe.width <= 0 || safe.height <= 0 || Screen.width <= 0 || Screen.height <= 0) return;
            Vector2 center = new Vector2(safe.center.x / Screen.width, safe.center.y / Screen.height);
            layout.anchorMin = layout.anchorMax = center;
            float width = canvas.rect.width * (safe.width / Screen.width);
            float height = canvas.rect.height * (safe.height / Screen.height);
            float scale = Mathf.Min((width - 32f) / layout.sizeDelta.x, (height - 48f) / layout.sizeDelta.y);
            layout.localScale = Vector3.one * Mathf.Clamp(scale, .01f, 1f);
        }

        void RewardLight(Transform parent, Vector2 center, float size, Color glow)
        {
            var masks = ColosseumMedalLight.GetMasks();
            Image halo = Icon(parent, masks.Halo, RewardCenter, center, size, glow);
            Image rays = Icon(parent, masks.Rays, RewardCenter, center, size, new Color(glow.r, glow.g, glow.b, glow.a * .49f));
            ColosseumMedalLight.Attach(halo, rays);
            halo.rectTransform.sizeDelta = Vector2.one * size;
            rays.rectTransform.sizeDelta = Vector2.one * (size * 1.19f);
        }

        RectTransform BindPackCard(List<PackHeroCard> pool, int slot, Transform parent, int hero, int amount, int coinReward)
        {
            if (slot == pool.Count)
            {
                if (packCardPrefab == null)
                    packCardPrefab = heroesPage != null ? heroesPage.CardPrefab
                        : Resources.Load<GameObject>("ColosseumPrefabs/HeroCard").GetComponent<ColosseumHeroCardView>();
                var view = UnityEngine.Object.Instantiate(packCardPrefab, parent, false);
                view.UseUpgradeProgress(true);
                var root = (RectTransform)view.transform;
                root.anchorMin = root.anchorMax = RewardCenter;
                var gain = Label(root, "", 31f, Cream, new Vector2(.5f, 0f), new Vector2(0f, -24f), new Vector2(140f, 38f), bold);
                gain.name = "Copies awarded";
                pool.Add(new PackHeroCard { Root = root, View = view, Amount = gain });
            }
            var card = pool[slot];
            card.Hero = hero;
            card.Root.gameObject.SetActive(true);
            card.View.BindPackReward(hero, amount, coinReward);
            card.Amount.text = "+" + amount;
            return card.Root;
        }

        void BindRewardCard(List<RewardHeroCard> pool, int slot, Transform parent, int hero, int amount)
        {
            if (slot == pool.Count)
            {
                var card = new RewardHeroCard();
                card.Root = Box(parent, RewardCenter, Vector2.zero, new Vector2(163f, 212f), RewardInk, 1.75f).rectTransform;
                var trim = Box(card.Root, RewardCenter, new Vector2(0f, 1.5f), new Vector2(156f, 203f), new Color32(230, 225, 255, 255), 1.75f);
                card.Stock = Box(trim.transform, RewardCenter, Vector2.zero, new Vector2(149f, 196f), Color.white, 1.95f);
                Shade(card.Stock, .23f, .10f);
                card.Fighter = Plain(card.Stock.transform, RewardCenter, new Vector2(0f, 16f), new Vector2(143f, 150f), Color.white);
                card.Fighter.preserveAspect = true;
                card.Band = Box(card.Stock.transform, RewardCenter, new Vector2(0f, -73f), new Vector2(147f, 48f), RewardInk, 3f);
                card.Name = Label(card.Band.transform, "", 22f, Color.white, RewardCenter, Vector2.zero, new Vector2(143f, 40f), bold);
                card.Amount = Label(card.Stock.transform, "", 37f, Cream, RewardCenter, new Vector2(34f, -30f), new Vector2(71f, 44f), bold);
                pool.Add(card);
            }
            var target = pool[slot];
            var data = ColosseumRoster.Heroes[hero];
            target.Hero = hero;
            target.Root.gameObject.SetActive(true);
            Color rarity = ColosseumHeroCardView.RarityColor(data.Rarity);
            target.Stock.color = rarity;
            target.Band.color = ColosseumHeroCardView.BandColor(rarity);
            target.Fighter.sprite = ColosseumHeroCardView.Fighter(hero);
            target.Name.text = data.Name;
            target.Amount.text = amount > 0 ? "+" + amount : "";
        }

        static void LayoutRewardCards(List<RewardHeroCard> pool, int count, float centerY)
        {
            for (int i = 0; i < pool.Count; i++)
                PlaceRewardCard(pool[i].Root, i, count, centerY, 226f);
        }

        static void PlaceRewardCard(RectTransform root, int index, int count, float centerY, float rowHeight)
        {
            root.gameObject.SetActive(index < count);
            if (index >= count) return;
            int columns = Mathf.Min(3, count);
            int rows = Mathf.CeilToInt(count / (float)columns);
            float scale = 1f / rows;
            int row = index / columns;
            int rowSize = Mathf.Min(columns, count - row * columns);
            root.anchoredPosition = new Vector2((index % columns - (rowSize - 1) * .5f) * 180f * scale,
                centerY + ((rows - 1) * .5f - row) * rowHeight * scale);
            root.localScale = Vector3.one * scale;
        }

        void BuildVictoryLaurels(RectTransform parent)
        {
            Sprite leaf = Paint("Victory laurel leaf", 56, (x, y) => Mathf.Abs(x) < (1f - y * y) * .91f);
            for (int side = -1; side <= 1; side += 2)
            {
                Vector2 last = new Vector2(-side * 10f, -90f);
                for (int i = 1; i <= 8; i++)
                {
                    float t = i / 8f;
                    float angle = Mathf.Lerp(-90f, 18f, t) * Mathf.Deg2Rad;
                    Vector2 p = new Vector2(side * Mathf.Cos(angle) * 169f, 45f + Mathf.Sin(angle) * 134f);
                    Vector2 branch = p - last;
                    var stem = Box(parent, RewardCenter, (last + p) * .5f, new Vector2(4.5f, branch.magnitude + 2f), new Color32(241, 158, 47, 255), 5f);
                    stem.rectTransform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(-branch.x, branch.y) * Mathf.Rad2Deg);
                    for (int dir = -1; dir <= 1; dir += 2)
                    {
                        float lean = Mathf.Atan2(-branch.x, branch.y) * Mathf.Rad2Deg + dir * 35f;
                        Quaternion rotation = Quaternion.Euler(0f, 0f, lean);
                        Vector2 size = new Vector2(14f, 38f) * (1f - .22f * t);
                        Vector2 pos = p + (Vector2)(rotation * Vector3.up) * (size.y * .30f);
                        var blade = Icon(parent, leaf, RewardCenter, pos, 1f, new Color32(236, 148, 32, 255));
                        blade.rectTransform.sizeDelta = size;
                        blade.rectTransform.localRotation = rotation;
                        var gold = Icon(blade.transform, leaf, RewardCenter, new Vector2(-.8f, 1.4f), 1f, new Color32(255, 216, 91, 255));
                        gold.rectTransform.sizeDelta = new Vector2(size.x * .85f, size.y * .88f);
                    }
                    last = p;
                }
            }
        }
    }
}
