using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SlingArena
{
    [DisallowMultipleComponent]
    public sealed class ArenaHud : MonoBehaviour
    {
        [SerializeField] private TMP_FontAsset outfitFont;
        [SerializeField] private Material blackOutline;

        public event Action FightPressed;
        public event Action PassPressed;
        public event Action RestartPressed;
        public event Action<bool> PauseChanged;

        private static readonly Color Ink = new Color32(28, 22, 27, 255);
        private static readonly Color Slate = new Color32(62, 73, 91, 255);
        private static readonly Color BlueRim = new Color32(75, 124, 184, 255);
        private static readonly Color Cyan = new Color32(74, 184, 249, 255);
        private static readonly Color Coral = new Color32(247, 99, 88, 255);
        private static readonly Color Gold = new Color32(255, 205, 103, 255);
        private static readonly Color GoldBase = new Color32(182, 106, 43, 255);
        private static readonly Color Lime = new Color32(192, 239, 75, 255);
        private static readonly Color LimeBase = new Color32(90, 155, 35, 255);
        private static readonly Vector2 Center = new Vector2(.5f, .5f);
        private static readonly Vector2 Top = new Vector2(.5f, 1);
        private static readonly Vector2 Bottom = new Vector2(.5f, 0);

        private readonly List<UnityEngine.Object> generated = new List<UnityEngine.Object>();
        private readonly Dictionary<IconKind, Sprite> icons = new Dictionary<IconKind, Sprite>();
        private Sprite rounded;
        private Sprite enamelFace;
        private Sprite circleFace;
        private Sprite circleRim;
        private Sprite meterFace;
        private RectTransform canvasRoot, safeRoot, modalSafeRoot, readyRoot, turnRoot, battleBottom, pauseControl, passControl;
        private RectTransform timerRoot, resultRoot, pauseRoot, activeModal;
        private Image dimmer, turnFill, turnAccent, resultMedal;
        private TextMeshProUGUI gameTitle, turnLabel, turnSeconds, actionLabel, resultTitle;
        private Vitals player, rival;
        private Rect lastSafeArea;
        private Vector2Int lastScreen;
        private float modalTime;
        private int lastSeconds = -1;
        private bool playing;

        public void Initialize(TMP_FontAsset outfit, Material outline)
        {
            if (canvasRoot != null)
                return;

            if (outfit != null)
                outfitFont = outfit;
            if (outline != null)
                blackOutline = outline;

            canvasRoot = Rect("Arena Canvas", transform);
            var canvas = canvasRoot.gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 40;
            var scaler = canvasRoot.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            canvasRoot.gameObject.AddComponent<GraphicRaycaster>();

            if (EventSystem.current == null)
            {
                var eventSystem = new GameObject("Arena Event System", typeof(EventSystem), typeof(StandaloneInputModule));
                eventSystem.transform.SetParent(transform, false);
            }

            rounded = BuildRoundedSprite("Rounded frame", 96, 96, 34, true);
            enamelFace = BuildRoundedSprite("Enamel face", 96, 96, 34, true, true);
            circleRim = BuildRoundedSprite("Medallion rim", 96, 96, 48, false);
            circleFace = BuildRoundedSprite("Medallion face", 96, 96, 48, false, true);
            meterFace = BuildRoundedSprite("Rounded meter", 160, 32, 12, false, true);
            safeRoot = Layer("Safe Area", canvasRoot);
            BuildTop();
            BuildReady();
            BuildBattleBottom();
            BuildModals();
            RefreshSafeArea();
            ShowReady();
        }

        public void ShowReady()
        {
            playing = false;
            Active(gameTitle.rectTransform, true);
            Active(readyRoot, true);
            Active(turnRoot, false);
            Active(battleBottom, false);
            Active(passControl, false);
            Active(pauseControl, false);
            SwitchModal(null);
        }

        public void ShowBattle()
        {
            playing = true;
            Active(gameTitle.rectTransform, false);
            Active(readyRoot, false);
            Active(turnRoot, true);
            Active(battleBottom, true);
            Active(pauseControl, true);
            SwitchModal(null);
        }

        public void SetBattle(bool playerTurn, bool canPass, float timeoutNormalized, string status,
            IReadOnlyList<int> playerHealth, int playerMax, IReadOnlyList<int> enemyHealth, int enemyMax, int activeIndex)
        {
            player.Set(playerHealth, playerMax, playerTurn, playerTurn ? activeIndex : -1);
            rival.Set(enemyHealth, enemyMax, !playerTurn, playerTurn ? -1 : activeIndex);
            SetText(turnLabel, playerTurn ? "YOUR TURN" : "RIVAL TURN");
            SetText(actionLabel, status ?? string.Empty);
            Active(passControl, playing && playerTurn && canPass);
            turnAccent.color = playerTurn ? Cyan : Coral;
            bool aiming = playing && playerTurn && timeoutNormalized > 0f;
            Active(timerRoot, aiming);
            if (aiming)
            {
                float fraction = Mathf.Clamp01(timeoutNormalized);
                turnFill.fillAmount = fraction;
                turnFill.color = fraction > .18f ? Cyan : Coral;
                int seconds = Mathf.CeilToInt(fraction * ArenaCombat.AimTurnLimit);
                if (lastSeconds != seconds)
                {
                    lastSeconds = seconds;
                    turnSeconds.SetText("{0:0}", seconds);
                }
            }
        }

        public void ShowResult(bool playerWon)
        {
            playing = false;
            resultMedal.sprite = GetIcon(playerWon ? IconKind.Trophy : IconKind.Shield);
            resultMedal.color = playerWon ? Color.white : new Color32(195, 216, 246, 255);
            SetText(resultTitle, playerWon ? "VICTORY!" : "DEFEAT");
            Active(passControl, false);
            Active(pauseControl, false);
            SwitchModal(resultRoot);
        }

        public void ShowPause(bool paused)
        {
            if (paused)
                SwitchModal(pauseRoot);
            else if (pauseRoot.gameObject.activeSelf)
                SwitchModal(null);
        }

        private void Update()
        {
            if (canvasRoot == null)
                return;
            if (lastSafeArea != Screen.safeArea || lastScreen.x != Screen.width || lastScreen.y != Screen.height)
                RefreshSafeArea();

            if (activeModal == null || modalTime >= 1)
                return;

            modalTime = Mathf.Min(1, modalTime + Time.unscaledDeltaTime * 7);
            float progress = 1 - Mathf.Pow(1 - modalTime, 3);
            activeModal.localScale = Vector3.one * Mathf.Lerp(.93f, 1, progress);
            activeModal.GetComponent<CanvasGroup>().alpha = progress;
        }

        private void RefreshSafeArea()
        {
            lastScreen = new Vector2Int(Mathf.Max(1, Screen.width), Mathf.Max(1, Screen.height));
            lastSafeArea = Screen.safeArea;
            if (lastSafeArea.width <= 0 || lastSafeArea.height <= 0)
                return;
            var minimum = new Vector2(lastSafeArea.xMin / lastScreen.x, lastSafeArea.yMin / lastScreen.y);
            var maximum = new Vector2(lastSafeArea.xMax / lastScreen.x, lastSafeArea.yMax / lastScreen.y);
            safeRoot.anchorMin = modalSafeRoot.anchorMin = minimum;
            safeRoot.anchorMax = modalSafeRoot.anchorMax = maximum;
            safeRoot.offsetMin = safeRoot.offsetMax = modalSafeRoot.offsetMin = modalSafeRoot.offsetMax = Vector2.zero;
        }

        private void BuildTop()
        {
            player = BuildVitals("Player", Bottom, new Vector2(0, 102), "YOU", Cyan, true);
            rival = BuildVitals("Rival", Top, new Vector2(-45, -100), "RIVAL", Coral, false);

            gameTitle = Label("Smash Rumble Title", safeRoot, Top, 0, -218, 700, 69, "SMASH RUMBLE", 59);
            gameTitle.characterSpacing = 5;

            turnRoot = Panel("Turn Status", safeRoot, Bottom, new Vector2(-287, 220), new Vector2(254, 60), Slate, BlueRim, 5, 3);
            turnAccent = Picture("Active Team", turnRoot, Center, -100, 4, 13, 13, circleFace, Cyan);
            turnLabel = Label("Turn", turnRoot, Center, 3, 5, 177, 36, "YOUR TURN", 27, TextAlignmentOptions.MidlineLeft);
            timerRoot = Place("Aim Countdown", turnRoot, Center, Vector2.zero, new Vector2(240, 57));
            turnSeconds = Label("Seconds", timerRoot, Center, 96, 5, 38, 33, "22", 27);
            Surface("Time Track", timerRoot, -2, -18, 197, 9, Ink);
            turnFill = Fill("Time Remaining", timerRoot, 191, 5, Cyan);
            turnFill.rectTransform.anchoredPosition = new Vector2(-2, -18);

            pauseControl = ButtonPlate("Pause", safeRoot, Top, new Vector2(376, -89), new Vector2(91, 84), Slate, GoldBase,
                () => PauseChanged?.Invoke(true));
            Icon("Pause Icon", pauseControl, Center, 0, 1, 45, 45, IconKind.Pause);
        }

        private Vitals BuildVitals(string name, Vector2 anchor, Vector2 position, string label, Color accent, bool isPlayer)
        {
            const float width = 725f;
            Color teamPaint = Color.Lerp(Ink, accent, .60f);
            RectTransform tray = Panel(name + " Heroes", safeRoot, anchor, position, new Vector2(width, 128),
                teamPaint, Color.Lerp(Ink, accent, .38f), 10, 6);
            RectTransform teamSeal = Medallion(name + " Team Crest", tray, -279, 25, 52, accent);
            Icon("Crest", teamSeal, Center, 0, 0, 42, 42, IconKind.Shield);
            Label(name + " Team", tray, Center, -279, -5, 129, 33, label, 31);
            TextMeshProUGUI survivors = Label(name + " Survivors", tray, Center, -279, -37, 122, 32, "3 LEFT", 23);

            var units = new UnitVital[ArenaCombat.UnitsPerSide];
            for (int index = 0; index < units.Length; index++)
            {
                var track = Place(name + " Hero " + (index + 1), tray, Center,
                    new Vector2(-125 + index * 185, 0), new Vector2(170, 148));
                Surface("Card Shadow", track, 0, -7, 170, 149, new Color(.06f, .04f, .07f, .37f));
                Image activeFrame = Surface("Active Frame", track, 0, 0, 178, 156, Gold);
                Active(activeFrame.rectTransform, false);
                Surface("Card Outline", track, 0, 0, 170, 148, Ink);
                Surface("Card Border", track, 0, 1, 163, 141, Color.Lerp(accent, Color.white, .38f));
                Image face = Surface("Card Face", track, 0, 0, 155, 133, Color.Lerp(Ink, accent, .34f));
                face.sprite = enamelFace;
                Image art = Surface("Portrait Ground", track, 0, 18, 137, 89, Color.Lerp(Ink, accent, .68f));
                art.sprite = enamelFace;

                var kind = (IconKind)((int)(isPlayer ? IconKind.BlueAxeHero : IconKind.RedAxeHero) + index);
                Image portrait = Icon("Hero Portrait", track, Center, 1, 19, 108, 104, kind);
                Picture("Hero Number Seal", track, Center, -61, 53, 29, 29, circleRim, Ink);
                Picture("Hero Number Face", track, Center, -61, 53, 24, 24, circleFace, accent);
                Label("Hero Number", track, Center, -61, 53, 23, 27, (index + 1).ToString(), 19);

                (IconKind Icon, string Name) weapon = ArenaCombat.WeaponForSlot(index) switch
                {
                    ArenaArt.WeaponStyle.Axe => (IconKind.Axe, "AXE"),
                    ArenaArt.WeaponStyle.Hammer => (IconKind.Hammer, "HAMMER"),
                    _ => (IconKind.Sword, "SWORD")
                };
                Icon(weapon.Name + " Icon", track, Center, -45, -28, 29, 29, weapon.Icon);
                TextMeshProUGUI weaponName = Label("Weapon", track, Center, 10, -29, 81, 23, weapon.Name, 19);
                weaponName.characterSpacing = .4f;

                Surface("Health Track", track, -14, -52, 90, 12, Ink);
                Image health = Fill("Health Fill", track, 84, 7, accent);
                health.rectTransform.anchoredPosition = new Vector2(-14, -52);
                TMP_Text healthText = Label("Health", track, Center, 54, -51, 47, 24, "-", 21);
                units[index] = new UnitVital(track, activeFrame, portrait, health, healthText, isPlayer ? 1 : -1);
            }
            return new Vitals(tray.Find("Surface").GetComponent<Image>(), teamPaint, survivors, units);
        }

        private void BuildReady()
        {
            readyRoot = Layer("Ready Controls", safeRoot);
            RectTransform fight = ButtonPlate("Play", readyRoot, Bottom, new Vector2(0, 223), new Vector2(324, 88),
                Lime, LimeBase, () => FightPressed?.Invoke());
            Label("Play Label", fight, Center, -9, 3, 226, 71, "PLAY", 54);
            Icon("Play Chevron", fight, Center, 116, 2, 44, 44, IconKind.Chevron);
        }

        private void BuildBattleBottom()
        {
            battleBottom = Layer("Battle Controls", safeRoot);
            RectTransform prompt = Panel("Action Prompt", battleBottom, Bottom, new Vector2(31, 220), new Vector2(367, 60),
                Color.Lerp(Ink, Slate, .65f), BlueRim, 5, 3);
            actionLabel = Label("Action", prompt, Center, 0, 1, 337, 46, "PULL BACK TO AIM", 27);

            passControl = ButtonPlate("Pass", battleBottom, Bottom, new Vector2(318, 220), new Vector2(176, 60), Slate, GoldBase,
                () => PassPressed?.Invoke());
            Label("Pass Label", passControl, Center, -12, 2, 122, 46, "PASS", 27);
            Icon("Pass Chevron", passControl, Center, 60, 1, 29, 29, IconKind.Chevron);
        }

        private void BuildModals()
        {
            dimmer = Layer("Modal Dimmer", canvasRoot).gameObject.AddComponent<Image>();
            dimmer.color = new Color32(10, 18, 37, 155);
            modalSafeRoot = Layer("Modal Safe Area", canvasRoot);

            resultRoot = Modal("Result", new Vector2(568, 460));
            RectTransform resultSeal = Medallion("Result Seal", resultRoot, 0, 116, 132, Gold);
            resultMedal = Icon("Match Medal", resultSeal, Center, 0, 0, 105, 105, IconKind.Trophy);
            resultTitle = Label("Result Title", resultRoot, Center, 0, -5, 487, 86, "VICTORY!", 64);
            RectTransform resultPrimary = ButtonPlate("Play Again", resultRoot, Bottom, new Vector2(0, 91), new Vector2(401, 84),
                Lime, LimeBase, () => RestartPressed?.Invoke());
            Label("Play Again Label", resultPrimary, Center, -15, 1, 319, 61, "PLAY AGAIN", 39);
            Icon("Play Again Chevron", resultPrimary, Center, 153, 1, 38, 38, IconKind.Chevron);

            pauseRoot = Modal("Pause", new Vector2(488, 412));
            RectTransform pauseSeal = Medallion("Pause Seal", pauseRoot, 0, 135, 78, Cyan);
            Icon("Pause Medal", pauseSeal, Center, 0, 0, 51, 51, IconKind.Pause);
            Label("Paused Title", pauseRoot, Top, 0, -146, 447, 70, "PAUSED", 55);
            RectTransform resume = ButtonPlate("Resume", pauseRoot, Center, new Vector2(0, -26), new Vector2(359, 76),
                Lime, LimeBase, () => PauseChanged?.Invoke(false));
            Label("Resume Label", resume, Center, -13, 0, 290, 60, "RESUME", 37);
            Icon("Resume Chevron", resume, Center, 140, 0, 38, 38, IconKind.Chevron);
            RectTransform restart = ButtonPlate("Pause Restart", pauseRoot, Bottom, new Vector2(0, 69), new Vector2(316, 60),
                Slate, BlueRim, () => RestartPressed?.Invoke());
            Label("Restart Label", restart, Center, 0, 0, 298, 51, "RESTART MATCH", 26);
        }

        private RectTransform Modal(string name, Vector2 size)
        {
            RectTransform dialog = Panel(name, modalSafeRoot, Center, Vector2.zero, size, Slate, GoldBase, 12, 7);
            Surface("Title Accent", dialog, 0, size.y * .5f - 12, size.x * .4f, 3, Gold);
            dialog.gameObject.AddComponent<CanvasGroup>();
            return dialog;
        }

        private void SwitchModal(RectTransform modal)
        {
            Active(resultRoot, modal == resultRoot);
            Active(pauseRoot, modal == pauseRoot);
            Active(dimmer.rectTransform, modal != null);
            if (activeModal == modal)
                return;
            activeModal = modal;
            modalTime = 0;
            if (modal != null)
            {
                modal.localScale = Vector3.one * .93f;
                modal.GetComponent<CanvasGroup>().alpha = 0;
            }
        }

        private RectTransform ButtonPlate(string name, Transform parent, Vector2 anchor, Vector2 position, Vector2 size,
            Color face, Color depth, Action action)
        {
            RectTransform root = Panel(name, parent, anchor, position, size, face, depth, 10);
            Image target = root.Find("Surface").GetComponent<Image>();
            target.raycastTarget = true;
            Button button = root.gameObject.AddComponent<Button>();
            button.targetGraphic = target;
            ColorBlock colors = button.colors;
            colors.highlightedColor = new Color(.98f, 1, .90f);
            colors.pressedColor = new Color(.74f, .80f, .69f);
            colors.selectedColor = Color.white;
            colors.fadeDuration = .08f;
            button.colors = colors;
            Navigation navigation = button.navigation;
            navigation.mode = Navigation.Mode.None;
            button.navigation = navigation;
            button.onClick.AddListener(() => action.Invoke());
            return root;
        }

        private RectTransform Panel(string name, Transform parent, Vector2 anchor, Vector2 position, Vector2 size,
            Color face, Color depthColor, float depth, float border = 5)
        {
            RectTransform panel = Place(name, parent, anchor, position, size);
            Vector2 inset = size - Vector2.one * (border * 2);
            if (depth > 0)
            {
                Surface("Soft Drop", panel, 0, -depth - 4, size.x + 8, size.y + 4, new Color(.045f, .07f, .15f, .23f));
                Surface("Lower Edge", panel, 0, -depth, size.x, size.y, Ink);
                Surface("Lower Glaze", panel, 0, -depth, inset.x, inset.y, depthColor);
            }

            Surface("Black Frame", panel, 0, 0, size.x, size.y, Ink);
            Surface("Inset Rim", panel, 0, 0, inset.x, inset.y, Color.Lerp(face, Color.white, .29f));
            Image finish = Surface("Surface", panel, 0, -1, inset.x - 3, inset.y - 3, face);
            finish.sprite = enamelFace;
            if (size.y >= 55)
                Surface("Glaze Highlight", panel, 0, size.y * .5f - border - 9, size.x * .56f, 2,
                    Color.Lerp(face, Color.white, .38f));
            return panel;
        }

        private RectTransform Medallion(string name, Transform parent, float x, float y, float size, Color accent)
        {
            RectTransform seal = Place(name, parent, Center, new Vector2(x, y), Vector2.one * size);
            Picture("Seal Back", seal, Center, 0, -3, size, size, circleRim, Ink);
            Picture("Seal Rim", seal, Center, 0, 1, size - 4, size - 4, circleRim, Color.Lerp(accent, Color.white, .22f));
            Picture("Seal Enamel", seal, Center, 0, 0, size - 11, size - 11, circleFace, Color.Lerp(accent, Ink, .32f));
            Picture("Seal Glint", seal, Center, -size * .15f, size * .27f, size * .18f, size * .055f, circleFace, Color.Lerp(accent, Color.white, .51f));
            return seal;
        }

        private Image Fill(string name, Transform parent, float width, float height, Color color)
        {
            Image result = Picture(name, parent, Center, 0, 0, width, height, meterFace, color);
            result.type = Image.Type.Filled;
            result.fillMethod = Image.FillMethod.Horizontal;
            return result;
        }

        private Image Surface(string name, Transform parent, float x, float y, float width, float height, Color tint)
        {
            Image result = Picture(name, parent, Center, x, y, width, height, rounded, tint);
            result.type = Image.Type.Sliced;
            result.pixelsPerUnitMultiplier = 1.29f;
            return result;
        }

        private Image Icon(string name, Transform parent, Vector2 anchor, float x, float y, float width, float height, IconKind kind)
        {
            return Picture(name, parent, anchor, x, y, width, height, GetIcon(kind), Color.white);
        }

        private static Image Picture(string name, Transform parent, Vector2 anchor, float x, float y, float width, float height,
            Sprite sprite, Color tint)
        {
            Image result = Place(name, parent, anchor, new Vector2(x, y), new Vector2(width, height)).gameObject.AddComponent<Image>();
            result.sprite = sprite;
            result.color = tint;
            result.raycastTarget = false;
            return result;
        }

        private TextMeshProUGUI Label(string name, Transform parent, Vector2 anchor, float x, float y, float width, float height,
            string value, float pointSize, TextAlignmentOptions align = TextAlignmentOptions.Center)
        {
            var label = Place(name, parent, anchor, new Vector2(x, y), new Vector2(width, height)).gameObject.AddComponent<TextMeshProUGUI>();
            label.font = outfitFont != null ? outfitFont : TMP_Settings.defaultFontAsset;
            if (outfitFont != null && blackOutline != null)
                label.fontSharedMaterial = blackOutline;
            label.color = Color.white;
            label.fontSize = pointSize;
            label.enableAutoSizing = true;
            label.fontSizeMin = pointSize * .66f;
            label.fontSizeMax = pointSize;
            label.alignment = align;
            label.characterSpacing = 2.5f;
            label.wordSpacing = 15;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Ellipsis;
            label.raycastTarget = false;
            label.text = value;
            return label;
        }

        private static RectTransform Rect(string name, Transform parent)
        {
            var child = new GameObject(name, typeof(RectTransform));
            child.layer = 5;
            RectTransform result = (RectTransform)child.transform;
            result.SetParent(parent, false);
            return result;
        }

        private static RectTransform Place(string name, Transform parent, Vector2 anchor, Vector2 position, Vector2 size)
        {
            RectTransform result = Rect(name, parent);
            result.anchorMin = result.anchorMax = anchor;
            result.anchoredPosition = position;
            result.sizeDelta = size;
            return result;
        }

        private static RectTransform Layer(string name, Transform parent)
        {
            RectTransform result = Rect(name, parent);
            result.anchorMin = Vector2.zero;
            result.anchorMax = Vector2.one;
            result.offsetMin = result.offsetMax = Vector2.zero;
            return result;
        }

        private static void Active(RectTransform rect, bool active)
        {
            if (rect.gameObject.activeSelf != active)
                rect.gameObject.SetActive(active);
        }

        private static void SetText(TMP_Text label, string value)
        {
            if (label.text != value)
                label.text = value;
        }

        private Sprite BuildRoundedSprite(string name, int width, int height, float radius, bool sliced, bool enamel = false)
        {
            var pixels = new Color32[width * height];
            for (int y = 0; y < height; y++)
            {
                float heightFraction = (y + .5f) / height;
                byte shade = enamel ? (byte)Mathf.RoundToInt(255 * Mathf.SmoothStep(.77f, 1, heightFraction)) : (byte)255;
                for (int x = 0; x < width; x++)
                {
                    float dx = Mathf.Max(radius - Mathf.Min(x + .5f, width - .5f - x), 0);
                    float dy = Mathf.Max(radius - Mathf.Min(y + .5f, height - .5f - y), 0);
                    float opacity = Mathf.Clamp01(radius + .5f - Mathf.Sqrt(dx * dx + dy * dy));
                    pixels[x + y * width] = new Color32(shade, shade, shade, (byte)Mathf.RoundToInt(255 * opacity));
                }
            }

            Vector4 border = sliced ? Vector4.one * (radius + 1) : Vector4.zero;
            return SpriteFromPixels(name, pixels, width, height, border);
        }

        private Sprite GetIcon(IconKind kind)
        {
            if (icons.TryGetValue(kind, out Sprite sprite))
                return sprite;

            const int size = 168;
            var pixels = new Color32[size * size];
            DrawIcon(pixels, size, kind);
            sprite = SpriteFromPixels(kind.ToString(), pixels, size, size, Vector4.zero);
            icons.Add(kind, sprite);
            return sprite;
        }

        private Sprite SpriteFromPixels(string name, Color32[] pixels, int width, int height, Vector4 border)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = "Arena " + name,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            var sprite = Sprite.Create(texture, new UnityEngine.Rect(0, 0, width, height), Center, 100, 0, SpriteMeshType.FullRect, border);
            sprite.name = name;
            generated.Add(sprite);
            generated.Add(texture);
            return sprite;
        }

        private void OnDestroy()
        {
            foreach (UnityEngine.Object asset in generated)
                Destroy(asset);
        }

        private enum IconKind
        {
            Sword, Axe, Hammer, Shield, Trophy, Pause, Chevron,
            BlueAxeHero, BlueSwordHero, BlueHammerHero, RedAxeHero, RedSwordHero, RedHammerHero
        }

        private static void DrawIcon(Color32[] pixels, int size, IconKind kind)
        {
            Color32 dark = Ink;
            Color32 cream = new Color32(255, 247, 218, 255);
            Color32 silver = new Color32(210, 229, 250, 255);
            Color32 shade = new Color32(117, 150, 197, 255);
            Color32 yellow = Gold;
            Color32 oak = new Color32(159, 86, 58, 255);
            void Paint(Color32 color, params float[] path) => DrawPolygon(pixels, size, color, path);

            switch (kind)
            {
                case IconKind.Sword:
                    Paint(dark, 9, 14, 20, 7, 39, 26, 48, 19, 55, 26, 46, 36, 82, 72, 92, 94, 72, 92, 33, 53, 24, 61, 17, 54, 25, 44);
                    Paint(oak, 16, 17, 21, 13, 37, 31, 31, 38);
                    Paint(yellow, 24, 51, 44, 26, 49, 29, 29, 54);
                    Paint(silver, 36, 50, 75, 87, 84, 89, 78, 75, 42, 43);
                    Paint(shade, 42, 43, 78, 75, 84, 89, 47, 51);
                    break;
                case IconKind.Axe:
                    Paint(dark, 8, 13, 19, 6, 27, 7, 65, 57, 79, 51, 92, 58, 96, 77, 86, 91, 67, 95, 51, 90, 46, 79, 53, 67);
                    Paint(oak, 15, 14, 23, 11, 64, 63, 58, 69);
                    Paint(yellow, 56, 65, 63, 60, 73, 74, 67, 81);
                    Paint(silver, 51, 79, 56, 86, 68, 90, 83, 86, 91, 76, 87, 63, 79, 57, 71, 62);
                    Paint(shade, 63, 72, 71, 76, 90, 76, 87, 63, 79, 57, 71, 62);
                    Paint(cream, 55, 80, 59, 85, 69, 87, 77, 84);
                    break;
                case IconKind.Hammer:
                    Paint(dark, 12, 12, 21, 6, 29, 7, 60, 49, 72, 40, 95, 64, 94, 74, 73, 95, 61, 94, 38, 70, 46, 61);
                    Paint(oak, 18, 14, 24, 11, 60, 57, 54, 63);
                    Paint(yellow, 48, 69, 64, 88, 72, 90, 89, 73, 90, 66, 72, 46);
                    Paint(shade, 61, 57, 82, 79, 89, 73, 90, 66, 72, 46);
                    Paint(silver, 48, 69, 66, 88, 75, 82, 57, 62);
                    Paint(cream, 68, 56, 85, 74, 88, 70, 72, 51);
                    break;
                case IconKind.Shield:
                    Paint(dark, 10, 77, 26, 86, 74, 86, 90, 77, 84, 42, 75, 27, 50, 11, 25, 27, 16, 42);
                    Paint(shade, 17, 73, 29, 80, 71, 80, 84, 73, 78, 43, 71, 32, 50, 18, 29, 32, 22, 43);
                    Paint(cream, 50, 70, 57, 52, 76, 50, 61, 38, 67, 21, 50, 31, 34, 21, 40, 38, 25, 50, 43, 52);
                    break;
                case IconKind.Trophy:
                    Paint(dark, 9, 80, 23, 79, 23, 89, 77, 89, 77, 79, 91, 80, 91, 51, 78, 43, 68, 43, 57, 33, 57, 24, 72, 24, 78, 12, 74, 7,
                        26, 7, 22, 12, 28, 24, 43, 24, 43, 33, 32, 43, 22, 43, 9, 51);
                    Paint(new Color32(215, 128, 47, 255), 15, 73, 25, 73, 32, 50, 24, 50, 15, 56);
                    Paint(new Color32(215, 128, 47, 255), 85, 73, 75, 73, 68, 50, 76, 50, 85, 56);
                    Paint(yellow, 28, 83, 72, 83, 72, 60, 63, 44, 56, 38, 52, 37, 52, 21, 67, 19, 71, 13, 29, 13, 33, 19, 48, 21, 48, 37, 44, 38, 37, 44, 28, 60);
                    Paint(cream, 32, 79, 62, 79, 64, 73, 35, 73);
                    Paint(new Color32(236, 151, 51, 255), 61, 72, 71, 73, 69, 59, 64, 49, 54, 41, 53, 48);
                    Paint(cream, 50, 70, 54, 61, 65, 60, 56, 54, 59, 43, 50, 50, 41, 43, 44, 54, 35, 60, 46, 61);
                    break;
                case IconKind.Pause:
                    Paint(dark, 14, 13, 43, 13, 43, 87, 14, 87);
                    Paint(dark, 57, 13, 86, 13, 86, 87, 57, 87);
                    Paint(cream, 21, 20, 36, 20, 36, 80, 21, 80);
                    Paint(cream, 64, 20, 79, 20, 79, 80, 64, 80);
                    break;
                case IconKind.Chevron:
                    Paint(dark, 27, 87, 38, 95, 85, 51, 85, 43, 38, 5, 26, 9, 18, 22, 55, 48, 17, 77);
                    Paint(cream, 34, 86, 76, 48, 34, 13, 26, 21, 64, 48, 25, 78);
                    Paint(yellow, 26, 21, 64, 48, 76, 48, 34, 13);
                    break;
                case IconKind.BlueAxeHero:
                case IconKind.RedAxeHero:
                case IconKind.BlueSwordHero:
                case IconKind.RedSwordHero:
                case IconKind.BlueHammerHero:
                case IconKind.RedHammerHero:
                    bool blue = (int)kind < (int)IconKind.RedAxeHero;
                    int hero = (int)kind - (int)(blue ? IconKind.BlueAxeHero : IconKind.RedAxeHero);
                    Color32 jersey = blue ? new Color32(57, 145, 234, 255) : new Color32(231, 85, 93, 255);
                    Color32 trim = blue ? new Color32(101, 221, 255, 255) : new Color32(255, 151, 96, 255);
                    Paint(dark, 7, 6, 10, 22, 20, 29, 36, 34, 65, 34, 82, 27, 91, 19, 95, 6);
                    Paint(jersey, 13, 6, 15, 20, 25, 25, 40, 31, 62, 31, 78, 23, 86, 18, 89, 6);
                    Paint(trim, 15, 16, 28, 20, 34, 13, 27, 7, 13, 6);
                    Paint(trim, 71, 17, 82, 22, 86, 18, 90, 6, 77, 7);
                    Paint(yellow, 36, 28, 43, 31, 50, 23, 58, 31, 65, 27, 58, 17, 50, 14, 42, 19);

                    if (hero == 0)
                    {
                        Color32 copper = new Color32(173, 71, 40, 255);
                        Color32 shine = new Color32(234, 118, 56, 255);
                        Paint(dark, 13, 67, 13, 80, 25, 90, 45, 97, 64, 94, 83, 88, 88, 71, 85, 57, 89, 48,
                            83, 38, 75, 39, 68, 26, 45, 21, 25, 26, 16, 35, 14, 47, 8, 49, 10, 59, 16, 60);
                        Paint(new Color32(250, 182, 124, 255), 19, 62, 24, 77, 70, 79, 79, 69, 81, 57, 86, 53, 84, 46,
                            78, 43, 75, 49, 72, 36, 63, 29, 46, 26, 28, 31, 20, 41, 18, 51, 12, 53, 16, 58);
                        Paint(new Color32(223, 130, 86, 255), 22, 43, 31, 42, 34, 33, 48, 29, 61, 31, 73, 41,
                            68, 29, 48, 25, 27, 30, 22, 34);
                        Paint(copper, 16, 76, 28, 87, 46, 93, 65, 90, 82, 84, 87, 72, 79, 63, 77, 73, 64, 76, 52, 69,
                            41, 75, 34, 64, 22, 61, 19, 68);
                        Paint(shine, 20, 77, 37, 87, 52, 89, 71, 84, 62, 82, 50, 80, 38, 72, 27, 70);
                        Paint(dark, 24, 57, 32, 62, 44, 59, 45, 49, 36, 46, 26, 48);
                        Paint(cream, 27, 55, 34, 58, 41, 55, 41, 51, 33, 49, 27, 51);
                        Paint(dark, 34, 55, 39, 55, 39, 50, 34, 50);
                        Paint(dark, 57, 58, 68, 63, 76, 55, 74, 48, 64, 46, 58, 50);
                        Paint(cream, 61, 55, 68, 59, 72, 55, 71, 51, 64, 49, 61, 51);
                        Paint(dark, 64, 55, 69, 56, 69, 51, 64, 50);
                        Paint(copper, 32, 43, 43, 45, 50, 40, 57, 45, 68, 41, 64, 37, 53, 37, 50, 35, 42, 37, 33, 39);
                        Paint(dark, 38, 36, 52, 33, 67, 37, 62, 29, 52, 28, 41, 31);
                        Paint(cream, 42, 35, 50, 34, 50, 30, 43, 32);
                        Paint(cream, 55, 34, 62, 35, 60, 31, 55, 30);
                    }
                    else if (hero == 1)
                    {
                        Color32 navy = new Color32(37, 48, 80, 255);
                        Paint(dark, 11, 52, 14, 74, 11, 91, 26, 85, 29, 98, 43, 90, 56, 98, 61, 90, 76, 94, 89, 80,
                            89, 60, 93, 48, 85, 38, 80, 27, 63, 21, 36, 21, 17, 27, 13, 40, 8, 43);
                        Paint(new Color32(219, 153, 103, 255), 17, 49, 20, 69, 31, 75, 69, 77, 81, 70, 84, 54, 88, 48,
                            82, 43, 78, 46, 78, 33, 63, 27, 36, 26, 22, 32, 18, 44, 12, 46);
                        Paint(new Color32(181, 108, 75, 255), 20, 37, 35, 36, 41, 29, 64, 30, 78, 38, 77, 32, 64, 26, 36, 25, 22, 31);
                        Paint(navy, 17, 75, 17, 84, 29, 78, 33, 91, 46, 84, 57, 91, 64, 84, 75, 89, 84, 77, 85, 63,
                            79, 57, 74, 71, 56, 73, 45, 67, 29, 69, 24, 61, 18, 62);
                        Paint(new Color32(63, 91, 139, 255), 31, 79, 35, 88, 45, 82, 57, 89, 63, 82, 73, 85, 77, 80,
                            63, 77, 50, 79, 44, 75);
                        Paint(dark, 23, 60, 34, 65, 46, 59, 45, 52, 31, 56, 24, 54);
                        Paint(dark, 55, 59, 67, 66, 77, 60, 76, 54, 68, 57, 56, 53);
                        Paint(cream, 28, 52, 35, 54, 43, 51, 41, 46, 34, 46);
                        Paint(cream, 58, 51, 66, 55, 73, 52, 71, 47, 63, 46);
                        Paint(navy, 35, 53, 40, 52, 39, 47, 35, 48);
                        Paint(navy, 63, 54, 68, 54, 67, 47, 63, 48);
                        Paint(dark, 35, 36, 43, 39, 58, 39, 66, 35, 61, 28, 41, 28);
                        Paint(cream, 39, 36, 45, 37, 58, 37, 62, 34, 59, 32, 43, 32);
                    }
                    else
                    {
                        Color32 honey = new Color32(250, 182, 50, 255);
                        Color32 curl = new Color32(255, 221, 105, 255);
                        Paint(dark, 7, 62, 8, 76, 17, 81, 21, 90, 33, 93, 42, 90, 50, 98, 63, 95, 72, 92,
                            84, 90, 94, 79, 93, 67, 88, 60, 94, 48, 91, 39, 82, 37, 76, 27, 62, 22, 39, 22, 23, 28, 17, 38, 8, 42, 9, 54);
                        Paint(new Color32(198, 120, 79, 255), 17, 57, 25, 72, 73, 74, 82, 63, 85, 52, 91, 49, 88, 42, 81, 43,
                            77, 33, 61, 27, 41, 26, 26, 32, 20, 42, 14, 44, 13, 51);
                        Paint(new Color32(227, 149, 98, 255), 26, 65, 40, 72, 68, 71, 79, 61, 79, 46, 72, 35, 58, 30, 41, 29, 27, 35, 22, 48);
                        Paint(honey, 11, 66, 14, 76, 22, 77, 26, 87, 35, 88, 43, 84, 51, 92, 62, 91, 68, 86, 76, 88,
                            84, 84, 89, 76, 88, 68, 81, 63, 72, 67, 66, 75, 57, 72, 48, 77, 40, 72, 33, 74, 24, 64, 16, 61);
                        Paint(curl, 16, 69, 19, 74, 27, 73, 31, 83, 38, 83, 43, 78, 36, 76, 33, 70, 23, 66);
                        Paint(curl, 46, 82, 51, 88, 58, 88, 65, 82, 60, 77, 51, 79);
                        Paint(curl, 71, 78, 76, 84, 83, 81, 86, 74, 80, 70, 75, 72);
                        Paint(dark, 24, 56, 28, 64, 36, 67, 45, 63, 48, 55, 43, 47, 33, 46, 26, 49);
                        Paint(cream, 28, 55, 32, 61, 38, 62, 44, 58, 44, 52, 38, 49, 32, 50);
                        Paint(dark, 36, 57, 41, 57, 41, 51, 36, 51);
                        Paint(dark, 54, 54, 58, 65, 67, 69, 76, 64, 80, 57, 75, 49, 64, 46, 57, 49);
                        Paint(cream, 58, 55, 62, 63, 69, 64, 75, 61, 76, 55, 70, 50, 64, 50);
                        Paint(dark, 66, 58, 72, 59, 71, 52, 66, 52);
                        Paint(dark, 34, 43, 48, 39, 67, 43, 68, 36, 61, 28, 45, 27, 37, 33);
                        Paint(new Color32(245, 110, 105, 255), 46, 30, 55, 33, 62, 32, 60, 29, 51, 28);
                        Paint(cream, 43, 40, 52, 39, 51, 34, 45, 34);
                    }
                    break;
            }
        }

        private static void DrawPolygon(Color32[] pixels, int size, Color32 color, float[] path)
        {
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float py = (y + .5f) * 100 / size;
                float px = (x + .5f) * 100 / size;
                bool inside = false;
                for (int i = 0, j = path.Length - 2; i < path.Length; j = i, i += 2)
                {
                    float yi = path[i + 1];
                    float yj = path[j + 1];
                    if ((yi > py) != (yj > py) && px < (path[j] - path[i]) * (py - yi) / (yj - yi) + path[i])
                        inside = !inside;
                }
                if (inside)
                    pixels[x + y * size] = color;
            }
        }

        private sealed class Vitals
        {
            private readonly Image trayFace;
            private readonly Color trayColor;
            private readonly TextMeshProUGUI survivors;
            private readonly UnitVital[] units;
            private int shownSurvivors = -1;
            private bool highlighted;

            public Vitals(Image trayFace, Color trayColor, TextMeshProUGUI survivors, UnitVital[] units)
            {
                this.trayFace = trayFace;
                this.trayColor = trayColor;
                this.survivors = survivors;
                this.units = units;
            }

            public void Set(IReadOnlyList<int> health, int max, bool ownTurn, int activeIndex)
            {
                int alive = 0;
                for (int index = 0; index < units.Length; index++)
                {
                    int hp = health != null && index < health.Count ? health[index] : 0;
                    if (hp > 0)
                        alive++;
                    units[index].Set(hp, max, index == activeIndex);
                }

                if (shownSurvivors != alive)
                {
                    shownSurvivors = alive;
                    survivors.SetText("{0:0} LEFT", alive);
                }

                if (highlighted == ownTurn)
                    return;
                highlighted = ownTurn;
                trayFace.color = ownTurn ? Color.Lerp(trayColor, Color.white, .13f) : trayColor;
            }
        }

        private sealed class UnitVital
        {
            private readonly RectTransform card;
            private readonly Image activeFrame;
            private readonly Image portrait;
            private readonly Image health;
            private readonly TMP_Text label;
            private readonly Vector2 restingPosition;
            private readonly int liftDirection;
            private int shownHealth = -1;
            private int shownMax = -1;
            private bool highlighted;

            public UnitVital(RectTransform card, Image activeFrame, Image portrait, Image health, TMP_Text label, int liftDirection)
            {
                this.card = card;
                this.activeFrame = activeFrame;
                this.portrait = portrait;
                this.health = health;
                this.label = label;
                this.liftDirection = liftDirection;
                restingPosition = card.anchoredPosition;
            }

            public void Set(int hp, int max, bool selected)
            {
                int displayHp = Mathf.Max(0, hp);
                if (shownHealth != displayHp || shownMax != max)
                {
                    shownHealth = displayHp;
                    shownMax = max;
                    health.fillAmount = max > 0 ? (float)displayHp / max : 0;
                    portrait.color = displayHp > 0 ? Color.white : new Color32(111, 122, 137, 255);
                    if (displayHp == 0)
                        SetText(label, "KO");
                    else
                        label.SetText("{0:0}", displayHp);
                }

                bool highlight = selected && displayHp > 0;
                if (highlighted == highlight)
                    return;

                highlighted = highlight;
                Active(activeFrame.rectTransform, highlight);
                card.anchoredPosition = restingPosition + Vector2.up * (highlight ? 8 * liftDirection : 0);
            }
        }
    }
}
