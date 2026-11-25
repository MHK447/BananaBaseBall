using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SlingArena.Colosseum
{
    public sealed partial class ColosseumHud
    {
        const int DecreeTiles = 9, DecreeCruiseSlots = 60, DecreeBrakeSlots = 9;
        const int DecreeTravel = DecreeCruiseSlots + DecreeBrakeSlots, DecreeStop = DecreeTravel % DecreeTiles;
        const float DecreePitch = 96f, DecreeUnrollAt = .42f, DecreeOpenAt = .99f;
        const float DecreePaperSoundAt = DecreeUnrollAt - .09f; // Lead-in in the parchment recording.
        const float DecreeSpinAt = .63f, DecreeCruiseFor = 3f;
        const float DecreeSpeed = DecreeCruiseSlots / DecreeCruiseFor;
        const float DecreeBrakeAt = DecreeSpinAt + DecreeCruiseFor, DecreeBrakeFor = 2f * DecreeBrakeSlots / DecreeSpeed;
        const float DecreeLockAt = DecreeBrakeAt + DecreeBrakeFor, DecreeLeaveAt = DecreeLockAt + 1.85f, DecreeEndAt = DecreeLeaveAt + .20f;
        // An event the player has never met used to stay up 2 s longer once the reel had stopped; no longer wanted (11-24).
        const float DecreeDebutHold = 0f;
        static readonly Vector2 DecreeCenter = new Vector2(.5f, .5f);
        static readonly Color32 DecreeGold = new Color32(255, 215, 87, 255);
        static readonly Color32 DecreeTileEdge = new Color32(80, 118, 148, 255);
        static readonly Color32 DecreeGoldEdge = new Color32(152, 94, 46, 255);

        RectTransform decreeRoot, decreeBand, decreePaperClip, decreeStrip, decreeSelected;
        CanvasGroup decreeFade, decreePrinting, decreeReading;
        TextMeshProUGUI decreeTitle, decreeEffect;
        readonly Image[] decreeTileFaces = new Image[DecreeTiles], decreeTileIcons = new Image[DecreeTiles];
        readonly RectTransform[] decreeTileRects = new RectTransform[DecreeTiles];
        readonly int[] decreeBoundSlots = new int[DecreeTiles];
        readonly List<string> decreeKeys = new List<string>();
        readonly List<(Sprite icon, Color32 color)> decreeTileArt = new List<(Sprite, Color32)>();
        int decreeLastTick, decreeWinningKey;
        float decreeAge, decreeSoundAt, decreeHold;
        bool decreeLocked;
        Image decreeFlash;
        RectTransform decreeGlint;
        ColosseumUpgradeStarburst decreeStars;
        // The game is in English on every phone.
        static bool KoreanDecree => false;

        public bool RoyalDecreeComplete => decreeRoot == null || !decreeRoot.gameObject.activeSelf || decreeAge >= DecreeEndAt + decreeHold;

        void BuildDecree()
        {
            decreeRoot = (RectTransform)Group("Royal decree", battleRoot.transform, true).transform;
            decreeFade = decreeRoot.gameObject.AddComponent<CanvasGroup>();
            Image inputBlocker = Plain(decreeRoot, DecreeCenter, Vector2.zero, Vector2.zero, Color.clear);
            inputBlocker.name = "Decree input blocker";
            inputBlocker.rectTransform.anchorMin = Vector2.zero;
            inputBlocker.rectTransform.anchorMax = Vector2.one;
            inputBlocker.raycastTarget = true;

            decreeBand = (RectTransform)Group("Decree band", decreeRoot).transform;
            Place(decreeBand, decreeRoot, new Vector2(.5f, .59f), Vector2.zero, new Vector2(600f, 260f));
            BuildDecreeScroll();

            // Keep the print at its natural size as the rollers open and the lower flap unfolds.
            var topCenter = new Vector2(.5f, 1f);
            decreePaperClip = (RectTransform)Group("Exposed parchment", decreeBand).transform;
            Place(decreePaperClip, decreeBand, DecreeCenter, new Vector2(0f, 86f), new Vector2(12f, 158f));
            decreePaperClip.pivot = topCenter;
            decreePaperClip.gameObject.AddComponent<RectMask2D>();
            var ink = (RectTransform)Group("Decree print", decreePaperClip).transform;
            Place(ink, decreePaperClip, topCenter, new Vector2(0f, -86f), new Vector2(600f, 310f));
            decreePrinting = ink.gameObject.AddComponent<CanvasGroup>();

            var caption = (RectTransform)Group("Event inscription", ink).transform;
            Place(caption, ink, DecreeCenter, new Vector2(0f, -101f), new Vector2(523f, 78f));
            decreeReading = caption.gameObject.AddComponent<CanvasGroup>();
            decreeTitle = Label(caption, string.Empty, 36f, new Color32(111, 51, 41, 255), DecreeCenter, new Vector2(0f, 17f), new Vector2(510f, 43f), null);
            decreeEffect = Label(caption, string.Empty, 23f, new Color32(71, 54, 57, 255), DecreeCenter, new Vector2(0f, -16f), new Vector2(510f, 46f), null);
            decreeEffect.textWrappingMode = TextWrappingModes.Normal;
            decreeEffect.enableAutoSizing = true;
            decreeEffect.fontSizeMin = 18f;
            decreeEffect.fontSizeMax = 23f;

            RectTransform window = (RectTransform)Group("Reel window", ink).transform;
            Place(window, ink, DecreeCenter, Vector2.zero, new Vector2(552f, 108f));
            window.gameObject.AddComponent<RectMask2D>();
            decreeStrip = (RectTransform)Group("Event reel", window).transform;
            for (int i = 0; i < DecreeTiles; i++)
            {
                Image tile = Box(decreeStrip, DecreeCenter, new Vector2(i * DecreePitch, 0f), new Vector2(85f, 87f), DecreeTileEdge);
                tile.name = "Decree tile " + i;
                decreeTileRects[i] = tile.rectTransform;
                Image face = Box(tile.transform, DecreeCenter, new Vector2(0f, 1.5f), new Vector2(78f, 79f), Color.white);
                Shade(face, .29f, .07f);
                decreeTileFaces[i] = face;
                Image icon = Plain(face.transform, DecreeCenter, Vector2.zero, new Vector2(73f, 73f), Color.white);
                icon.preserveAspect = true;
                decreeTileIcons[i] = icon;
                if (i == DecreeStop) decreeSelected = tile.rectTransform;
            }

            Image pointer = Plain(ink, DecreeCenter, new Vector2(0f, 50f), new Vector2(26f, 21f), DecreeGoldEdge);
            pointer.sprite = arrow;
            Image tip = Plain(pointer.transform, DecreeCenter, new Vector2(0f, 1.5f), new Vector2(20f, 17f), DecreeGold);
            tip.sprite = arrow;
            BuildDecreeBurst();
        }

        void BuildDecreeBurst()
        {
            // These highlights sit outside the reel mask so the stars can clear the rails.
            decreeFlash = Plain(decreeBand, DecreeCenter, Vector2.zero, new Vector2(175f, 124f), Color.clear);
            decreeFlash.sprite = ColosseumMedalLight.GetMasks().Halo;
            decreeFlash.name = "Winning tile flash";
            var burst = (RectTransform)Group("Decree seal burst", decreeBand).transform;
            Place(burst, decreeBand, DecreeCenter, new Vector2(0f, -14f), new Vector2(720f, 310f));
            burst.localScale = Vector3.one * .42f;
            decreeStars = burst.gameObject.AddComponent<ColosseumUpgradeStarburst>();
            decreeStars.raycastTarget = false;
            decreeGlint = Icon(decreeBand, star, DecreeCenter, new Vector2(42f, 34f), 24f, new Color32(255, 247, 172, 255)).rectTransform;
            decreeGlint.name = "Winning tile glint";
        }

        public void ShowRoyalDecree(IReadOnlyList<string> candidates, string selectedKey, string description, bool debut = false)
        {
            if (decreeRoot == null) BuildDecree();
            decreeHold = debut ? DecreeDebutHold : 0f;
            HideTurn();
            HideBanner();
            decreeKeys.Clear();
            if (candidates != null)
                foreach (string key in candidates)
                    if (!decreeKeys.Contains(key)) decreeKeys.Add(key);
            if (!decreeKeys.Contains(selectedKey)) decreeKeys.Add(selectedKey);
            // A scroll with only one or two events on it would spin the same picture round and round: the others
            // ride the reel too, though only the one it stops on comes.
            if (decreeKeys.Count < 4)
                foreach (string key in ColosseumEventTable.Keys)
                {
                    // An event that belongs to one map does not ride the reel on the others.
                    if (key == ColosseumEventTable.Tide || key == ColosseumEventTable.Fire) continue;
                    if (!decreeKeys.Contains(key) && Drawn("Edicts/" + key) != null) decreeKeys.Add(key);
                }
            decreeWinningKey = decreeKeys.IndexOf(selectedKey);
            decreeTileArt.Clear();
            foreach (string key in decreeKeys)
                decreeTileArt.Add((Drawn("Edicts/" + key), DecreeStyle(key).color));
            for (int i = 0; i < DecreeTiles; i++)
            {
                decreeBoundSlots[i] = int.MinValue;
                decreeTileIcons[i].color = Color.white;
            }
            var selection = DecreeStyle(selectedKey);
            decreeTitle.text = KoreanDecree ? selection.koreanTitle : selection.title;
            decreeEffect.text = KoreanDecree ? selection.koreanEffect : description;
            decreeAge = 0f;
            decreeLastTick = 0;
            decreeSoundAt = 0f;
            decreeLocked = false;
            decreeReading.alpha = 0f;
            decreeSelected.localScale = Vector3.one;
            decreeSelected.GetComponent<Image>().color = DecreeTileEdge;
            decreeFlash.color = Color.clear;
            decreeStars.SetAge(-1f);
            decreeGlint.localScale = Vector3.zero;
            decreeRoot.gameObject.SetActive(true);
            TickRoyalDecree(0f);
        }

        public void HideRoyalDecree()
        {
            if (decreeRoot != null) decreeRoot.gameObject.SetActive(false);
        }

        // Keep only the cells inside (or just outside) the mask. The logical slot follows the reel even
        // when a frame skips several cells, so the winning slot always arrives with its matching artwork.
        void PositionDecreeReel(float position)
        {
            int first = Mathf.FloorToInt(position) - DecreeTiles / 2;
            int count = decreeTileArt.Count;
            for (int slot = first; slot < first + DecreeTiles; slot++)
            {
                int cell = (slot + DecreeTiles) % DecreeTiles;
                if (decreeBoundSlots[cell] == slot) continue;
                decreeBoundSlots[cell] = slot;
                int key = (decreeWinningKey + slot - DecreeTravel) % count;
                var art = decreeTileArt[key < 0 ? key + count : key];
                decreeTileFaces[cell].color = art.color;
                decreeTileIcons[cell].sprite = art.icon;
                decreeTileRects[cell].anchoredPosition = new Vector2(slot * DecreePitch, 0f);
            }
            decreeStrip.anchoredPosition = new Vector2(-position * DecreePitch, 0f);
        }

        internal void TickRoyalDecree(float delta)
        {
            if (decreeRoot == null || !decreeRoot.gameObject.activeSelf) return;
            float previousAge = decreeAge;
            decreeAge += delta;
            if (previousAge < DecreePaperSoundAt && decreeAge >= DecreePaperSoundAt)
                MatchSound?.Invoke("decree-unroll");

            float enter = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(decreeAge / .15f));
            float exit = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(DecreeLeaveAt + decreeHold, DecreeEndAt + decreeHold, decreeAge));
            decreeFade.alpha = (.88f + .12f * enter) * (1f - exit);
            decreeBand.anchoredPosition = new Vector2(0f, (1f - enter) * 12f + exit * 18f);

            float spread = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(DecreeUnrollAt, DecreeOpenAt, decreeAge));
            decreeBand.localRotation = Quaternion.Euler(0f, 0f, -5f * (1f - spread));
            float after = Mathf.Max(0f, decreeAge - DecreeLockAt);
            float unfoldResult = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(after / .24f));
            SetDecreeScroll(spread, unfoldResult);
            decreePaperClip.sizeDelta = new Vector2(2f * (DecreeRollCenter(spread) - 10f), Mathf.Lerp(158f, 233f, unfoldResult));
            // The moving rollers uncover the artwork at full size while the reel is already spinning.
            decreePrinting.alpha = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.14f, .36f, spread));

            float position = Mathf.Clamp(decreeAge - DecreeSpinAt, 0f, DecreeCruiseFor) * DecreeSpeed;
            if (decreeAge > DecreeBrakeAt)
            {
                float brake = Mathf.InverseLerp(DecreeBrakeAt, DecreeLockAt, decreeAge);
                position += DecreeBrakeSlots * brake * (2f - brake);
            }
            PositionDecreeReel(position);
            int tick = Mathf.RoundToInt(position);
            if (tick != decreeLastTick)
            {
                decreeLastTick = tick;
                if (tick < DecreeTravel && decreeAge >= decreeSoundAt)
                {
                    MatchSound?.Invoke("match-tick");
                    decreeSoundAt = decreeAge + .085f;
                }
            }
            if (decreeAge < DecreeLockAt) return;
            if (!decreeLocked)
            {
                decreeLocked = true;
                decreeSelected.GetComponent<Image>().color = DecreeGold;
                for (int i = 0; i < DecreeTiles; i++)
                {
                    if (i == DecreeStop) continue;
                    decreeTileFaces[i].color = Color.Lerp(decreeTileFaces[i].color, Color.white, .22f);
                    decreeTileIcons[i].color = new Color32(238, 244, 251, 255);
                }
                MatchSound?.Invoke("match-ding");
            }
            float reveal = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.06f, .25f, after));
            decreeReading.alpha = reveal;
            float punch = Mathf.Sin(after * 24f) * Mathf.Exp(-after * 8f);
            decreeSelected.localScale = Vector3.one * (1.13f - .13f * Mathf.Exp(-16f * after) + .17f * punch);
            float flash = 1f - Mathf.Clamp01(after / .31f);
            decreeFlash.color = new Color(1f, .98f, .73f, .57f * flash * flash);
            decreeFlash.rectTransform.localScale = Vector3.one * (1.22f - .40f * flash);
            decreeStars.SetAge(after);
            float glint = Mathf.Clamp01((after - .34f) / .46f);
            decreeGlint.localScale = Vector3.one * Mathf.Sin(glint * Mathf.PI);
            decreeGlint.localRotation = Quaternion.Euler(0f, 0f, 26f - glint * 44f);
        }

        static (string title, Color32 color, string koreanTitle, string koreanEffect) DecreeStyle(string key)
        {
            switch (key)
            {
                case ColosseumEventTable.Quake: return ("EARTHQUAKE", new Color32(240, 179, 102, 255), "지진", "경기장 외벽 일부가 무너집니다.");
                case ColosseumEventTable.Gate: return ("POWER GATE", new Color32(255, 223, 142, 255), "파워 게이트", "통과하면 이번 턴의 공격력이 올라갑니다.");
                case ColosseumEventTable.Portal: return ("TWIN PORTALS", new Color32(207, 179, 255, 255), "쌍둥이 포탈", "한쪽 포탈로 들어가면 반대쪽에서 나옵니다.");
                case ColosseumEventTable.Lava: return ("LAVA VENTS", new Color32(255, 184, 133, 255), "용암 분출구", "용암이 솟으면 근처 영웅이 피해를 입습니다.");
                case ColosseumEventTable.Spike: return ("SPIKE WALLS", new Color32(255, 179, 196, 255), "가시벽", "가시벽에 세게 부딪힌 영웅은 피해를 입습니다.");
                case ColosseumEventTable.Ice: return ("ICE FIELD", new Color32(147, 222, 255, 255), "얼음판", "얼음 위에서는 영웅이 더 멀리 미끄러집니다.");
                case ColosseumEventTable.Bumper: return ("BUMPERS", new Color32(134, 237, 203, 255), "범퍼 기둥", "기둥에 부딪히면 더 빠르게 튕겨 나갑니다.");
                case ColosseumEventTable.Crate: return ("SUPPLY CRATE", new Color32(255, 205, 156, 255), "왕실 보급 상자", "상자를 먼저 부순 팀이 보상을 받습니다.");
                case ColosseumEventTable.Tide: return ("HIGH TIDE", new Color32(140, 208, 255, 255), "밀물", "물이 차오른 동안 슬링의 힘이 약해집니다.");
                case ColosseumEventTable.Fire: return ("FIRE PILLARS", new Color32(255, 170, 120, 255), "불기둥", "불기둥에 닿으면 화상을 입고 튕겨 나갑니다.");
                default: return (key.ToUpperInvariant(), new Color32(150, 173, 210, 255), key, string.Empty);
            }
        }
    }
}
