using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SlingArena.Colosseum
{
    /// <summary>The same portrait card is used in the binder, the equipped deck and card animations.</summary>
    public sealed class ColosseumHeroCardView : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] Image face, rim, portrait, footer, progressTone;
        [SerializeField] TextMeshProUGUI nameLabel, levelLabel, progressLabel;
        [SerializeField] RectTransform progressFill;
        [SerializeField] GameObject levelChip, lineupTag, lockRoot, progressRoot;
        [SerializeField] ColosseumLevelStars levelStars;
        [SerializeField] Material grayLook;

        static readonly Color32[] RarityTones = {
            new Color32(173, 203, 238, 255), new Color32(30, 239, 170, 255),
            new Color32(178, 128, 255, 255), new Color32(255, 197, 68, 255)
        };
        static readonly string[] RarityNames = { "COMMON", "RARE", "EPIC", "LEGENDARY" };
        static readonly Color32 Navy = new Color32(22, 39, 78, 255);
        internal static readonly Color32 UpgradeTrack = new Color32(26, 95, 73, 255);
        internal static readonly Color32 UpgradeFill = new Color32(82, 222, 111, 255);
        Image actionTrack;
        TextMeshProUGUI lockSource;
        GameObject upgradeDot;
        Color regularTrackColor;
        bool upgradeProgress;
        CanvasGroup veil;
        float pop;
        const float PopTime = .3f;

        public static Color RarityColor(int rarity) => RarityTones[Mathf.Clamp(rarity, 0, RarityTones.Length - 1)];
        internal static string RarityName(int rarity) => RarityNames[Mathf.Clamp(rarity, 0, RarityNames.Length - 1)];
        internal static Color BandColor(Color stock) => Color.Lerp(stock, Navy, .49f);
        internal Sprite LevelBadgeSprite => levelChip.GetComponent<Image>().sprite;

        public static Sprite Fighter(int index)
        {
            string key = ColosseumRoster.Heroes[index].Key;
            return Resources.Load<Sprite>("ColosseumCardFighters/" + key) ?? Resources.Load<Sprite>("ColosseumUnitPortraits/" + key);
        }

        public event Action<int, RectTransform> Selected;
        int hero = -1;

        void Awake() => ColosseumUiContour.PrefabShapes(gameObject);

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left && hero >= 0)
                Selected?.Invoke(hero, (RectTransform)transform);
        }

        /// <summary>Hides the card without taking it out of its place in the grid, while a copy of it is in flight.</summary>
        internal void Ghost(bool hidden)
        {
            if (veil == null)
            {
                if (!hidden) return;
                veil = GetComponent<CanvasGroup>();
                if (veil == null) veil = gameObject.AddComponent<CanvasGroup>();
            }
            veil.alpha = hidden ? 0f : 1f;
        }

        /// <summary>The collection card bounces when its hero leaves the deck.</summary>
        void Pop()
        {
            pop = PopTime;
            transform.localScale = Vector3.one * .6f;
        }

        void Update()
        {
            if (pop <= 0f) return;
            pop = Mathf.Max(0f, pop - Time.unscaledDeltaTime);
            float t = 1f - pop / PopTime;
            transform.localScale = Vector3.one * (t < .6f ? Mathf.Lerp(.6f, 1.08f, t / .6f) : Mathf.Lerp(1.08f, 1f, (t - .6f) / .4f));
        }

        void OnDisable()
        {
            if (pop <= 0f) return;
            pop = 0f;
            transform.localScale = Vector3.one;
        }

        public void Bind(int index, bool preview = false, bool showUpgradeDot = false)
        {
            BindCard(index, preview, showUpgradeDot, ColosseumRoster.Heroes[index].Locked,
                ColosseumRoster.Cards(index), ColosseumSave.Coins);
        }

        internal void BindPackReward(int index, int awarded, int coinReward)
        {
            int copies = ColosseumRoster.CardsAfterGrant(index, awarded);
            long coinsAfterClaim = (long)ColosseumSave.Coins + coinReward;
            BindCard(index, preview: true, showUpgradeDot: false, locked: false, copies: copies, coins: coinsAfterClaim);
        }

        void BindCard(int index, bool preview, bool showUpgradeDot, bool locked, int copies, long coins)
        {
            Ghost(false);
            var data = ColosseumRoster.Heroes[index];
            Color tone = RarityColor(data.Rarity);
            bool ready = !locked && ColosseumRoster.HasUpgradeResources(index, copies, coins);
            bool notify = ready && showUpgradeDot;
            if (notify && upgradeDot == null)
                upgradeDot = ColosseumNotificationDot.Create(transform, Vector2.one, new Vector2(-12f, -12f), 23f);
            if (upgradeDot != null) upgradeDot.SetActive(notify);
            face.color = tone;
            if (footer != null) footer.color = BandColor(tone);
            face.raycastTarget = true;
            rim.color = Color.Lerp(tone, Color.white, .27f);
            portrait.sprite = Fighter(index);
            portrait.color = locked ? new Color32(148, 159, 176, 255) : Color.white;
            portrait.material = locked ? grayLook : null;
            nameLabel.text = data.Name;
            levelChip.SetActive(!locked);
            bool showEquipped = !locked && !preview && ColosseumRoster.IsEquipped(index);
            if (hero == index && lineupTag.activeSelf && !showEquipped) Pop();
            lineupTag.SetActive(showEquipped);
            hero = index;
            lockRoot.SetActive(locked);
            progressRoot.SetActive(!locked);
            if (locked)
            {
                if (lockSource == null) lockSource = lockRoot.GetComponentInChildren<TextMeshProUGUI>(true);
                if (lockSource != null) lockSource.text = ColosseumRoster.UnlockHint(index);
                levelStars.SetLevel(0);
                return;
            }
            DisplayProgress(ColosseumRoster.Level(index), copies, ColosseumRoster.CardsNeeded(index), ready);
        }

        public void DisplayProgress(int level, int cards, int needed, bool ready)
        {
            levelStars.SetLevel(level);
            levelLabel.text = level.ToString();
            progressLabel.text = needed == 0 ? "MAX LEVEL" : cards + "/" + needed;
            float fill = needed == 0 ? 1f : Mathf.Clamp01((float)cards / needed);
            progressFill.anchorMax = new Vector2(fill, 1f);
            progressTone.color = upgradeProgress ? UpgradeFill
                : ready ? new Color32(72, 199, 99, 255) : new Color32(57, 151, 224, 255);
        }

        internal void UseUpgradeProgress(bool enabled)
        {
            if (enabled == upgradeProgress) return;
            if (actionTrack == null)
            {
                actionTrack = progressRoot.GetComponent<Image>();
                regularTrackColor = actionTrack.color;
            }
            upgradeProgress = enabled;
            actionTrack.color = enabled ? UpgradeTrack : regularTrackColor;
        }
    }
}
