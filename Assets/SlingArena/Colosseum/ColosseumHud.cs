using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SlingArena.Colosseum
{
    /// <summary>
    /// Chunky brawler-style HUD built at runtime: thick dark outlines, glossy plates, a health bar with level badge
    /// over every hero (green for your side, red for the rival), team strips, tilted banners and big buttons.
    /// </summary>
    public sealed partial class ColosseumHud
    {
        static readonly Color Ink = new Color32(22, 16, 34, 255);
        static readonly Color Navy = new Color32(28, 36, 72, 238);
        static readonly Color Yellow = new Color32(255, 204, 36, 255);
        static readonly Color YellowDeep = new Color32(222, 138, 0, 255);
        static readonly Color Cream = new Color32(255, 246, 222, 255);
        static readonly Color TurnInk = new Color32(25, 51, 78, 255);
        static readonly Color FriendGreen = new Color32(98, 228, 60, 255);
        static readonly Color FoeRed = new Color32(246, 70, 58, 255);
        static readonly Color GhostTone = new Color32(255, 240, 180, 255);
        const int TeamSize = ColosseumGame.UnitsPerSide;
        const float BarWide = 84f, BarTall = 16f;
        const float ScoreWide = 640f, ScoreTall = 48f, ScoreSwing = 160f;
        static readonly Color BlueSide = new Color32(62, 122, 240, 255);
        static readonly Color RedSide = new Color32(228, 66, 76, 255);

        sealed class Chip
        {
            public Image Back, Fill;
            public TextMeshProUGUI Name, Health;
        }

        /// <summary>Health bar, damage ghost, number and level badge that float over one hero.</summary>
        sealed class Plate
        {
            public RectTransform Root;
            public Image Fill, Ghost, Gloss;
            public TextMeshProUGUI Health, Level;
            public float Shown = 1f, Trail = 1f;
            public RectTransform Marker;
            public float MarkerShow, MarkerLift = 32f;
            // Badge beside the bar while the hero is powered up: crossed swords with a green up arrow.
            public RectTransform Boost;
            public bool Boosted;
            public float BoostPulse;
        }

        sealed class Popup
        {
            public TextMeshProUGUI Label;
            public Vector3 World;
            public float Age, Scale;
            public bool Active;
        }

        public event Action<bool> ModeChosen;
        public event Action RematchPressed;
        public event Action MenuPressed;

        readonly TMP_FontAsset font;
        readonly Material bold, slim;
        readonly Canvas screen;
        readonly RectTransform canvas;
        readonly CanvasScaler scaler;
        readonly RectTransform scoreHeader;
        Rect battleOutputPixels;
        readonly Sprite rounded, circle;
        readonly Chip[,] chips = new Chip[2, TeamSize];
        readonly TextMeshProUGUI[] totalLabels = new TextMeshProUGUI[2];
        readonly int[] totals = new int[2];
        RectTransform versus;
        Material scoreFace;
        static readonly int ScoreSplit = Shader.PropertyToID("_ScoreSplit");
        Sprite heart, arrow, star;
        TextMeshProUGUI lobbyName, lobbyCoins, lobbyTrophies, lobbyGain;
        readonly Dictionary<string, List<Image>> artSlots = new Dictionary<string, List<Image>>();
        readonly Dictionary<string, Sprite> artSprites = new Dictionary<string, Sprite>();
        RectTransform battleButton, firstBattleHand;
        RectTransform aimCancelMarker;
        Image aimCancelFace, aimCancelRing, aimCancelTrack;
        CanvasGroup roadAccess, roadRewardAccess;
        GameObject roadLock;
        readonly ColosseumDoll[] fightersBySlot = new ColosseumDoll[TeamSize * 2];
        const int HeroesTab = 0, BattleTab = 1, RankingsTab = 2, TabCount = 3;
        const float TabTall = 142f, TabRise = 24f;
        static readonly Color Cell = new Color32(188, 185, 239, 255);
        static readonly Color CellActive = new Color32(255, 209, 145, 255);
        static readonly Color TabGap = new Color32(142, 130, 177, 255);
        sealed class Tab
        {
            public RectTransform Icon, ContactShadow;
            public Image Plate;
            public TextMeshProUGUI Name;
            public GameObject Dot, Lock;
            public Button Button;
            public float Lit;           // 0 resting, 1 open
            public float Since = 10f;   // seconds since it was picked, for the bounce
        }
        readonly Tab[] tabs = new Tab[TabCount];
        int openTab = BattleTab;
        static Material grayLook;
        RectTransform tabBar;
        ColosseumHeroesPage heroesPage;
        ColosseumLeaguePage leaguePage;
        /// <summary>The player pressed one of the bottom tabs.</summary>
        public event Action TabPressed;
        public event Action<int> JourneyChestPressed;
        RectTransform walletCoin;
        float lobbyClock, lobbyGainLeft;
        // Lobby look: dark slanted plates, a bright trophy bar and battle button, and thick ink outlines.
        const float Lean = .16f;
        static readonly Color PlateSolid = new Color32(34, 32, 88, 255);
        static readonly Color TileBlue = new Color32(92, 186, 255, 255);
        static readonly Color RoadYellow = new Color32(255, 200, 44, 255);
        static readonly Color BattleYellow = new Color32(255, 196, 40, 255);
        static readonly Color BattleLip = new Color32(236, 130, 20, 255);
        float markerClock;
        float shownShare = .5f;
        /// <summary>One animated call-out: a bright band that whips in, with letters that slam down one by one.</summary>
        sealed class Callout
        {
            public RectTransform Root, Band;
            public Image Face, EdgeTop, EdgeBottom;
            public TextMeshProUGUI Title, Sub;
            public Color32 Tone;
            public float Age, Hold, Side;
            /// <summary>Seconds in at which the band drops to the foot of the screen to make room; negative for a band that stays put.</summary>
            public float Aside = -1f;
            public bool Waiting;
        }

        /// <summary>Where a call-out sits, measured from the middle of the screen.</summary>
        static readonly Vector2 CalloutHome = new Vector2(0f, 40f);
        /// <summary>How long a banner that makes room takes to fall to the foot of the screen.</summary>
        public const float BannerFall = .22f;
        /// <summary>How far above the bottom edge such a banner comes to rest, and how much smaller it is there.</summary>
        const float AsideFoot = 84f, AsideSize = .8f;

        static readonly Color SunnyBand = new Color32(255, 190, 34, 245);
        static readonly Color DangerBand = new Color32(255, 104, 48, 245);
        static readonly Color[] TeamBand = { new Color32(66, 148, 255, 245), new Color32(255, 98, 92, 245) };
        Callout turnCall, bannerCall;
        readonly TextMeshProUGUI[] teamLabels = new TextMeshProUGUI[2];
        readonly List<Plate> plates = new List<Plate>();
        readonly List<Popup> popups = new List<Popup>();
        readonly GameObject menu, endButtons, statusRoot, battleRoot, vignetteItem;
        readonly TextMeshProUGUI statusTitle, statusPrompt;
        TextMeshProUGUI turnNumber;
        RectTransform turnBadge;
        float turnPulse;
        Camera captureCamera;
        int popupCursor;

        public ColosseumHud(Transform parent, TMP_FontAsset brawlFont, TMP_FontAsset fallbackFont, Material fallbackOutline, Texture2D lobbyIllustration = null)
        {
            LoadDrawnArt();
            font = brawlFont != null ? brawlFont : fallbackFont != null ? fallbackFont : TMP_Settings.defaultFontAsset;
            if (brawlFont != null || fallbackOutline == null)
            {
                bold = OutlineMaterial(.30f, .16f);
                slim = OutlineMaterial(.24f, .10f);
            }
            else
            {
                bold = slim = HudTextMaterial(fallbackOutline);
            }
            rounded = MakeSprite(64, 22f, new Vector4(26f, 26f, 26f, 26f));
            circle = MakeSprite(48, 24f, Vector4.zero);
            heart = MakeHeart(72);
            arrow = MakePointer(72);
            MakeLobbySprites();

            var root = new GameObject("Colosseum HUD", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            root.transform.SetParent(parent, false);
            screen = root.GetComponent<Canvas>();
            screen.renderMode = RenderMode.ScreenSpaceOverlay;
            screen.additionalShaderChannels |= AdditionalCanvasShaderChannels.TexCoord1;
            screen.sortingOrder = 20;
            scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(720f, 1280f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            canvas = root.GetComponent<RectTransform>();

            if (EventSystem.current == null)
            {
                var events = new GameObject("Colosseum events", typeof(EventSystem), typeof(StandaloneInputModule));
                events.transform.SetParent(parent, false);
            }

            var vignette = new GameObject("Vignette", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
            vignette.rectTransform.SetParent(canvas, false);
            vignette.rectTransform.anchorMin = Vector2.zero;
            vignette.rectTransform.anchorMax = Vector2.one;
            vignette.rectTransform.offsetMin = vignette.rectTransform.offsetMax = Vector2.zero;
            vignette.texture = MakeVignette();
            vignette.raycastTarget = false;
            vignetteItem = vignette.gameObject;

            battleRoot = Group("Battle", canvas, true);
            Transform plateRoot = Group("Hero plates", battleRoot.transform).transform;
            for (int i = 0; i < TeamSize * 2; i++)
                plates.Add(MakePlate(plateRoot, i < TeamSize));
            // "Pick me" pointers that bob over each hero the player can launch this turn.
            for (int i = 0; i < TeamSize * 2; i++)
            {
                Image rim = Plain(plateRoot, new Vector2(.5f, .5f), Vector2.zero, new Vector2(44f, 40f), Ink);
                rim.sprite = arrow;
                Image face = Plain(rim.transform, new Vector2(.5f, .5f), new Vector2(0f, 1.5f), new Vector2(31f, 28f), i < TeamSize ? new Color32(70, 150, 255, 255) : new Color32(255, 92, 84, 255));
                face.sprite = arrow;
                Image shine = Plain(face.transform, new Vector2(.5f, .5f), new Vector2(-2f, 5f), new Vector2(15f, 10f), new Color(1f, 1f, 1f, .5f));
                shine.sprite = arrow;
                plates[i].Marker = rim.rectTransform;
                rim.gameObject.SetActive(false);
            }

            scoreHeader = (RectTransform)Group("Score and turn", battleRoot.transform).transform;
            scoreHeader.anchorMin = scoreHeader.anchorMax = new Vector2(.5f, 1f);
            BuildScoreBanner();
            for (int team = 0; team < 2; team++)
                BuildTeamStrip(team);

            statusRoot = Group("Status", battleRoot.transform);
            Place(statusRoot.GetComponent<RectTransform>(), battleRoot.transform, new Vector2(.5f, 0f), new Vector2(0f, 62f), new Vector2(440f, 76f));
            statusRoot.transform.localRotation = Quaternion.Euler(0f, 0f, 2f);
            statusTitle = Label(statusRoot.transform, string.Empty, 30f, Yellow, new Vector2(.5f, .5f), new Vector2(0f, 14f), new Vector2(440f, 40f), bold);
            statusPrompt = Label(statusRoot.transform, string.Empty, 17f, Color.white, new Vector2(.5f, .5f), new Vector2(0f, -16f), new Vector2(440f, 26f), slim);

            for (int i = 0; i < 16; i++)
            {
                var label = Label(battleRoot.transform, string.Empty, 38f, Color.white, new Vector2(.5f, .5f), Vector2.zero, new Vector2(460f, 64f), bold);
                label.gameObject.SetActive(false);
                popups.Add(new Popup { Label = label });
            }

            bannerCall = MakeCallout(canvas, 150f, 78f, true);
            turnCall = MakeCallout(battleRoot.transform, 104f, 92f, false);
            statusRoot.SetActive(false);

            endButtons = Group("End buttons", canvas, true);
            Press(endButtons.transform, "PLAY AGAIN", new Vector2(.5f, .5f), new Vector2(0f, -150f), new Vector2(320f, 88f), Yellow, YellowDeep, 34f,
                () => RematchPressed?.Invoke());
            Press(endButtons.transform, "MENU", new Vector2(.5f, .5f), new Vector2(0f, -256f), new Vector2(220f, 66f),
                MeshKit.Rgb(Kit.TeamMain[0], 1f), MeshKit.Rgb(Kit.TeamDark[0], 1f), 26f, () => MenuPressed?.Invoke());
            endButtons.SetActive(false);

            menu = Group("Menu", canvas, true);
            BuildLobby(menu.transform, lobbyIllustration);
            matchRoot = Group("Match", canvas, true);
            BuildMatch(matchRoot.transform);
            matchRoot.SetActive(false);
            BuildProfile();
        }

        // ------------------------------------------------------------------ lobby

        /// <summary>Paints a white icon from an inside/outside test over the square -1..1 (y up), with soft edges.</summary>
        static Sprite Paint(string name, int size, Func<float, float, bool> inside)
        {
            var texture = Kit.Own(new Texture2D(size, size, TextureFormat.RGBA32, true)
                { name = name, filterMode = FilterMode.Trilinear });
            texture.wrapMode = TextureWrapMode.Clamp;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int hits = 0;
                    for (int j = 0; j < 3; j++)
                    {
                        for (int i = 0; i < 3; i++)
                        {
                            if (inside((x + (i + .5f) / 3f) / size * 2f - 1f, (y + (j + .5f) / 3f) / size * 2f - 1f)) hits++;
                        }
                    }
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(hits * 255 / 9));
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(true, true);
            return Kit.Own(Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(.5f, .5f), 100f));
        }

        void MakeLobbySprites()
        {
            star = Paint("Colosseum UI star", 72, (x, y) =>
            {
                const float step = Mathf.PI / 5f, outer = .96f, inner = .44f;
                float angle = Mathf.Repeat(Mathf.Atan2(x, y), step * 2f);
                if (angle > step) angle = step * 2f - angle;
                float edge = outer * inner * Mathf.Sin(step) / (outer * Mathf.Sin(angle) + inner * Mathf.Sin(step - angle));
                return x * x + y * y <= edge * edge;
            });
        }

        Image Icon(Transform parent, Sprite sprite, Vector2 anchor, Vector2 position, float size, Color color)
        {
            Image image = Plain(parent, anchor, position, new Vector2(size, size), color);
            image.sprite = sprite;
            if (sprite == circle) ColosseumUiContour.Fill(image, 0f, true);
            return image;
        }

        static T Slanted<T>(T graphic, float lean = Lean) where T : Graphic
        {
            graphic.gameObject.AddComponent<ColosseumSlant>().lean = lean;
            return graphic;
        }

        Image HeaderPlate(Transform parent, Vector2 anchor, Vector2 position, Vector2 size, Color fill, Color trim)
        {
            var rim = Slanted(Box(parent, anchor, position, size + Vector2.one * 6f, trim, 2.6f));
            Image face = Slanted(Box(rim.transform, new Vector2(.5f, .5f), Vector2.zero, size, fill, 2.6f));
            Shade(face, .22f, .08f);
            return face;
        }

        static void Shade(Image image, float highlight, float shadow)
        {
            var shade = image.gameObject.AddComponent<ColosseumHudShade>();
            shade.highlight = highlight;
            shade.shadow = shadow;
        }

        /// <summary>Slot for one of the photographed toy icons; it stays hidden until the game hands the picture over.</summary>
        Image Art(Transform parent, string key, Vector2 anchor, Vector2 position, float size)
        {
            Image image = Plain(parent, anchor, position, new Vector2(size, size), Color.white);
            image.preserveAspect = true;
            image.enabled = false;
            if (key == null) return image;
            if (artSprites.TryGetValue(key, out Sprite ready))
            {
                image.sprite = ready;
                image.enabled = true;
            }
            if (!artSlots.TryGetValue(key, out List<Image> slots))
                artSlots[key] = slots = new List<Image>();
            slots.Add(image);
            return image;
        }

        /// <summary>Hands over a finished icon; every slot waiting for that name shows it from now on.</summary>
        /// <summary>
        /// Drawn UI icons (Resources/ColosseumUI) in the chunky, outlined hybrid-casual style. Where one exists it
        /// replaces the photographed model icon of the same key; the hero portraits stay photographed.
        /// </summary>
        static readonly string[] DrawnArt =
        {
            "cup", "lobby-cup", "swords", "shield", "lock", "medal0", "medal1", "medal2", "medal3", "chest", "chest-dim",
            "banner", "banner-dim", "crown", "crown-dim", "hero", "hero-dim", "hero-deck", "avatar-bg", "rival-bg"
        };
        static readonly Dictionary<string, Sprite> drawn = new Dictionary<string, Sprite>();

        static Sprite Drawn(string key)
        {
            if (drawn.TryGetValue(key, out Sprite known)) return known;
            Sprite sprite = Resources.Load<Sprite>("ColosseumUI/" + key);
            if (sprite == null)
            {
                Texture2D texture = Resources.Load<Texture2D>("ColosseumUI/" + key);
                if (texture != null)
                    sprite = Kit.Own(Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(.5f, .5f), 100f));
            }
            drawn[key] = sprite;
            return sprite;
        }

        void LoadDrawnArt()
        {
            foreach (string key in DrawnArt)
            {
                Sprite sprite = Drawn(key);
                if (sprite != null) SetArt(key, sprite);
            }
        }

        public void SetArt(string key, Sprite sprite)
        {
            Sprite own = System.Array.IndexOf(DrawnArt, key) >= 0 ? Drawn(key) : null;
            if (own != null) sprite = own;
            artSprites[key] = sprite;
            if (artSlots.TryGetValue(key, out List<Image> slots))
            {
                foreach (Image image in slots)
                {
                    image.sprite = sprite;
                    image.enabled = true;
                }
            }
            if (key == "avatar") RefreshPlayerPortraits();
        }

        /// <summary>Builds the lobby with the player profile, wallet, stage journey, battle button and tabs.</summary>
        void BuildLobby(Transform root, Texture2D illustration)
        {
            Vector2 topLeft = new Vector2(0f, 1f), topRight = new Vector2(1f, 1f), top = new Vector2(.5f, 1f);
            Vector2 middle = new Vector2(.5f, .5f), bottom = new Vector2(.5f, 0f), left = new Vector2(0f, .5f);

            // Give identity and currency distinct colour cues, including when the icons are small.
            string trophyArt = artSprites.ContainsKey("lobby-cup") ? "lobby-cup" : "cup";
            Image plate = HeaderPlate(root, topLeft, new Vector2(166f, -64f), new Vector2(176f, 72f),
                new Color32(182, 165, 221, 255), new Color32(112, 91, 151, 255));
            lobbyName = Label(plate.transform, "Player", 20f, Color.white, left, new Vector2(108f, 16f), new Vector2(124f, 28f), slim);
            lobbyName.alignment = TextAlignmentOptions.Left;
            lobbyName.richText = false;
            lobbyName.enableAutoSizing = true;
            lobbyName.fontSizeMin = 11f;
            lobbyName.fontSizeMax = 20f;
            lobbyName.overflowMode = TextOverflowModes.Ellipsis;
            Art(plate.transform, trophyArt, left, new Vector2(60f, -15f), 40f);
            lobbyTrophies = Label(plate.transform, "0", 27f, Color.white, left, new Vector2(123f, -14f), new Vector2(94f, 32f), bold);
            lobbyTrophies.alignment = TextAlignmentOptions.Left;
            lobbyTrophies.enableAutoSizing = true;
            lobbyTrophies.fontSizeMin = 12f;
            lobbyTrophies.fontSizeMax = 27f;
            Image tile = Box(root, topLeft, new Vector2(64f, -64f), new Vector2(96f, 96f), Ink, 1.3f);
            Image trim = Box(tile.transform, middle, Vector2.zero, new Vector2(87f, 87f), new Color32(206, 238, 255, 255), 1.42f);
            Image portrait = Box(trim.transform, middle, Vector2.zero, new Vector2(78f, 78f), TileBlue, 1.6f);
            float portraitRadius = 22f / portrait.pixelsPerUnitMultiplier;
            Image avatarBack = Art(portrait.transform, "avatar-bg", middle, Vector2.zero, 82f);
            ColosseumUiContour.Artwork(avatarBack, portrait.rectTransform, portraitRadius);
            lobbyProfileAvatar = Art(portrait.transform, null, middle, new Vector2(0f, -10f), 90f);
            ColosseumUiContour.Artwork(lobbyProfileAvatar, portrait.rectTransform, portraitRadius);
            Box(portrait.transform, top, new Vector2(0f, -10f), new Vector2(64f, 14f), new Color(1f, 1f, 1f, .22f), 2.4f);
            tile.name = "Open player profile";
            ProfileButton(tile, trim, OpenProfile);

            // Coins: the coin overlaps the left end of its plate, large white number.
            Image purse = HeaderPlate(root, topRight, new Vector2(-102f, -58f), new Vector2(178f, 52f),
                new Color32(241, 202, 139, 255), new Color32(160, 112, 76, 255));
            lobbyCoins = Label(purse.transform, "0", 31f, Color.white, middle, new Vector2(20f, 1f), new Vector2(130f, 40f), bold);
            walletCoin = Art(purse.transform, "coin", left, new Vector2(4f, 0f), 82f).rectTransform;
            lobbyGain = Label(root, string.Empty, 22f, RoadYellow, topRight, new Vector2(-104f, -104f), new Vector2(220f, 30f), bold);
            lobbyGain.gameObject.SetActive(false);

            BuildJourney(root, illustration, trophyArt);
            roadAccess = road.gameObject.AddComponent<CanvasGroup>();
            roadRewardAccess = journeyRewards[0].Root.parent.gameObject.AddComponent<CanvasGroup>();
            var lockFace = Frame(road.transform.parent, middle, Vector2.zero, new Vector2(276f, 48f), new Color32(61, 64, 102, 255), 3f, 2f);
            roadLock = lockFace.transform.parent.gameObject;
            roadLock.name = "Trophy road locked";
            Art(lockFace.transform, "lock", middle, new Vector2(-107f, 0f), 35f);
            Label(lockFace.transform, "WIN TO UNLOCK", 24f, Cream, middle, new Vector2(15f, 0f), new Vector2(218f, 42f), bold);
            roadLock.SetActive(false);

            // Battle: the one big yellow thing on the screen.
            battleButton = Press(root, "BATTLE", bottom, new Vector2(0f, 262f), new Vector2(392f, 124f), BattleYellow, BattleLip, 66f, () => ModeChosen?.Invoke(true), .09f);
            BuildChestDock(root);

            BuildTabs(root);
        }

        void BuildTabs(Transform root)
        {
            string[] titles = { "HEROES", "BATTLE", "RANKINGS" };
            string[] icons = { "hero-deck", "swords", "crown" };
            Vector2 middle = new Vector2(.5f, .5f), bottom = new Vector2(.5f, 0f);
            tabBar = (RectTransform)Group("Lobby tabs", root).transform;
            PlaceTabSpan(tabBar, 0f, 1f, TabTall);

            for (int i = 0; i < TabCount; i++)
            {
                float left = (float)i / TabCount, right = (float)(i + 1) / TabCount;
                Image face = Plain(tabBar, bottom, Vector2.zero, Vector2.zero, Cell);
                face.name = titles[i] + " tab";
                PlaceTabSpan(face.rectTransform, left, right, TabTall);
                Shade(face, .25f, .08f);
                var gloss = Plain(face.transform, new Vector2(.5f, 1f), new Vector2(0f, -3f), new Vector2(0f, 6f), new Color32(255, 248, 225, 136));
                gloss.rectTransform.anchorMin = new Vector2(0f, 1f);
                gloss.rectTransform.anchorMax = Vector2.one;
                if (i > 0)
                {
                    var divider = Plain(face.transform, Vector2.zero, new Vector2(1.5f, 0f), new Vector2(3f, 0f), TabGap);
                    divider.rectTransform.anchorMax = Vector2.up;
                }
                var name = Label(face.transform, titles[i], 22f, Color.white, bottom, new Vector2(0f, 20f), new Vector2(0f, 34f), bold);
                name.rectTransform.anchorMin = Vector2.zero;
                name.rectTransform.anchorMax = Vector2.right;
                Image contact = null;
                if (i == BattleTab || i == RankingsTab)
                {
                    contact = Plain(face.transform, middle, Vector2.zero, Vector2.zero, new Color32(51, 37, 71, 67));
                    contact.name = "Icon contact shadow";
                    contact.sprite = ColosseumMedalLight.GetMasks().Halo;
                }
                Image icon = Art(face.transform, icons[i], middle, Vector2.zero, 96f);
                if (contact != null)
                {
                    Shade(icon, 0f, .09f);
                    var depth = icon.gameObject.AddComponent<Shadow>();
                    depth.effectColor = new Color32(47, 36, 62, 142);
                    depth.effectDistance = new Vector2(1.7f, -3.8f);
                }
                tabs[i] = new Tab
                {
                    Plate = face,
                    Icon = icon.rectTransform,
                    ContactShadow = contact != null ? contact.rectTransform : null,
                    Name = name
                };
                if (i == HeroesTab || i == RankingsTab)
                {
                    tabs[i].Dot = ColosseumNotificationDot.Create(tabs[i].Icon, Vector2.one, new Vector2(-13f, -15f), 26f);
                    tabs[i].Dot.SetActive(false);
                    tabs[i].Lock = Art(face.transform, "lock", middle, new Vector2(29f, -7f), 34f).gameObject;
                    tabs[i].Lock.SetActive(false);
                }
                int index = i;
                face.raycastTarget = true;
                var button = face.gameObject.AddComponent<Button>();
                tabs[i].Button = button;
                button.targetGraphic = face;
                button.onClick.AddListener(() => OpenTab(index));
            }
            tabs[BattleTab].Lit = 1f;
            AnimateTabs(0f);
        }

        static void PlaceTabSpan(RectTransform rect, float left, float right, float height)
        {
            rect.anchorMin = new Vector2(left, 0f);
            rect.anchorMax = new Vector2(right, 0f);
            rect.pivot = new Vector2(.5f, 0f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(0f, height);
        }

        /// <summary>
        /// The project's grayscale UI look (the shader behind Material_UI_GrayScale), for locked things. Null if the
        /// shader is missing, in which case images simply stay in colour.
        /// </summary>
        internal static Material GrayLook()
        {
            if (grayLook != null) return grayLook;
            Shader shader = Resources.Load<Shader>("GrayscaleUI");
            if (shader == null) shader = Shader.Find("Custom/UI/Grayscale");
            if (shader == null) return null;
            grayLook = Kit.Own(new Material(shader) { name = "Colosseum UI gray" });
            grayLook.SetFloat("_GrayscaleAmount", 1f);
            return grayLook;
        }

        /// <summary>Sizes and colours every tab for this frame: the open one grows, the rest settle back.</summary>
        void AnimateTabs(float delta)
        {
            for (int i = 0; i < tabs.Length; i++)
            {
                Tab tab = tabs[i];
                bool open = i == openTab;
                tab.Lit = Mathf.MoveTowards(tab.Lit, open ? 1f : 0f, delta * (open ? 9f : 7f));
                tab.Since += delta;
                float lit = tab.Lit * tab.Lit * (3f - 2f * tab.Lit);
                // The tab that was just picked springs past its size and settles: boing.
                float boing = open ? Mathf.Exp(-tab.Since * 6f) * Mathf.Sin(tab.Since * 15f) : 0f;
                float tall = TabTall + TabRise * lit + 16f * boing;
                tab.Plate.rectTransform.sizeDelta = new Vector2(0f, tall);
                tab.Plate.color = Color.Lerp(Cell, CellActive, lit);
                float size = Mathf.Lerp(92f, 112f, lit) * (1f + .12f * boing);
                tab.Icon.sizeDelta = new Vector2(size, size);
                tab.Icon.anchoredPosition = new Vector2(0f, Mathf.Lerp(15f, 18f, lit) + 5f * boing);
                if (tab.ContactShadow != null)
                {
                    // The rounded sword hilts sit lower inside their sprite than the crown's foot.
                    float foot = i == BattleTab ? .42f : .34f;
                    tab.ContactShadow.anchoredPosition = new Vector2(2f, Mathf.Lerp(15f, 18f, lit) - size * foot);
                    tab.ContactShadow.sizeDelta = new Vector2(size * .65f, size * .105f);
                }
                tab.Name.fontSize = Mathf.Lerp(23f, 27f, lit);
                tab.Name.rectTransform.anchoredPosition = new Vector2(0f, Mathf.Lerp(18f, 22f, lit));
            }
        }

        /// <summary>
        /// Bottom tab pressed: HEROES opens the hero cards, BATTLE the lobby, RANKINGS the league table. Pressing
        /// the tab that is already open just gives it another bounce.
        /// </summary>
        public void OpenTab(int tab)
        {
            if (IsTabLocked(tab)) return;
            TabPressed?.Invoke();
            ShowTab(tab, true);
            if (openTab != BattleTab) HideBattleLesson();
            else if (ColosseumRoster.HeroesTabLocked || battleNudge) ShowPendingUpgradeLesson();
            if (openTab == HeroesTab)
            {
                // Coming in at the guide's bidding: the first upgrade is pointed out again each time, until it is done.
                if (ColosseumRoster.PendingUpgradeLesson >= 0) offeredLessonHero = -1;
                ShowPendingUpgradeLesson();
            }
        }

        static bool IsTabLocked(int tab) => tab == HeroesTab ? ColosseumRoster.HeroesTabLocked
            : tab == RankingsTab && ColosseumRoster.FirstBattleLocked;

        void ShowTab(int tab, bool bounce)
        {
            if (tab != HeroesTab && tab != RankingsTab) tab = BattleTab;
            if (tab == HeroesTab)
            {
                if (heroesPage == null)
                {
                    heroesPage = MakePage<ColosseumHeroesPage>("HeroesPage");
                    if (heroesPage != null) heroesPage.Selected += OpenHeroSelection;
                }
                if (heroesPage != null) heroesPage.Open(artSprites.TryGetValue("coin", out Sprite coin) ? coin : null);
            }
            else if (heroesPage != null)
            {
                CloseHeroSelection();
                heroesPage.Close();
            }
            if (tab == RankingsTab)
            {
                if (leaguePage == null) leaguePage = MakePage<ColosseumLeaguePage>("LeaguePage");
                if (leaguePage != null && (openTab != RankingsTab || !leaguePage.gameObject.activeInHierarchy))
                {
                    var seen = ColosseumSave.LastViewedRank;
                    int league = ColosseumSave.League;
                    bool promoted = seen.Place > 0 && seen.League >= 0 && league > seen.League;
                    leaguePage.Open(league == seen.League ? seen.Place : 0, MatchSound, promoted);
                    ColosseumSave.RememberViewedRank(league, leaguePage.Place);
                    tabs[RankingsTab].Dot.SetActive(false);
                }
            }
            else if (leaguePage != null) leaguePage.Close();

            openTab = tab;
            UpdateChestDock();
            if (bounce)
            {
                tabs[tab].Since = 0f;
                return;
            }
            // Coming back to the lobby from a match: show the tabs as they should be, with no animation.
            for (int i = 0; i < tabs.Length; i++)
            {
                if (tabs[i] == null) return;
                tabs[i].Lit = i == tab ? 1f : 0f;
                tabs[i].Since = 10f;
            }
        }

        /// <summary>Puts a page prefab into the lobby, under the tab bar so the tabs stay on top and usable.</summary>
        T MakePage<T>(string prefab) where T : Component
        {
            var asset = Resources.Load<GameObject>("ColosseumPrefabs/" + prefab);
            if (asset == null) return null;
            GameObject page = UnityEngine.Object.Instantiate(asset, menu.transform);
            ColosseumUiContour.PrefabShapes(page);
            page.transform.SetSiblingIndex(tabBar.GetSiblingIndex());
            return page.GetComponent<T>();
        }

        /// <summary>Fills the lobby with the saved progress; pass the coins just paid out to flash them under the purse.</summary>
        public void RefreshLobby(int coinsGained)
        {
            lobbyName.text = ColosseumSave.PlayerName;
            RefreshPlayerPortraits();
            lobbyCoins.text = ColosseumSave.Coins.ToString();
            lobbyTrophies.text = (heldProfileTrophies >= 0 ? heldProfileTrophies : ColosseumSave.Trophies).ToString();
            for (int i = 0; i < tabs.Length; i++)
            {
                Tab tab = tabs[i];
                if (!tab.Lock) continue;
                bool locked = IsTabLocked(i);
                // The padlock on HEROES stays until its unlocking has been shown (see TickHeroesUnlock).
                if (i == HeroesTab)
                {
                    if (locked) heroesLockSeen = true;
                    else if (heroesLockSeen) locked = true;
                }
                tab.Lock.SetActive(locked);
                tab.Button.interactable = !locked;
                tab.Icon.GetComponent<Image>().material = locked ? GrayLook() : null;
                tab.Dot.SetActive(!locked && (i == HeroesTab ? ColosseumRoster.HasAvailableUpgrade() : ColosseumRoster.HasUnseenRankGain()));
            }
            tabs[HeroesTab].Name.text = "HEROES";
            if (!battleNudge && ((!ColosseumRoster.HeroesTabLocked && !ColosseumRoster.NeedsArcherBattle) || ChestComesFirst))
            {
                if (firstBattleHand) firstBattleHand.gameObject.SetActive(false);
                battleTapAge = 0f;
            }
            bool roadLocked = ColosseumRoster.FirstBattleLocked;
            roadAccess.alpha = roadRewardAccess.alpha = roadLocked ? .38f : 1f;
            roadRewardAccess.interactable = roadRewardAccess.blocksRaycasts = !roadLocked;
            roadLock.SetActive(roadLocked);
            RefreshJourney(ColosseumSave.JourneyTrophies);
            PaintJourney(true);
            if (coinsGained > 0) MatchSound?.Invoke("reward-claim");
            lobbyGainLeft = coinsGained > 0 ? 2.6f : 0f;
            lobbyGain.text = "+" + coinsGained + " COINS";
            lobbyGain.gameObject.SetActive(coinsGained > 0);
        }

        // ------------------------------------------------------------------ building blocks

        static Material HudTextMaterial(Material source)
        {
            var material = Kit.Own(new Material(source) { name = "Colosseum HUD text" });
            // An offset SDF underlay can sample adjacent atlas glyphs and leave dark strokes above the text.
            material.DisableKeyword("UNDERLAY_ON");
            material.DisableKeyword("UNDERLAY_INNER");
            if (material.HasProperty("_Sharpness")) material.SetFloat("_Sharpness", -.22f);
            return material;
        }

        Material OutlineMaterial(float outline, float dilate)
        {
            var material = HudTextMaterial(font.material);
            material.EnableKeyword("OUTLINE_ON");
            material.SetFloat("_FaceDilate", dilate);
            material.SetFloat("_OutlineWidth", outline);
            material.SetColor("_OutlineColor", Ink);
            return material;
        }

        Sprite MakeSprite(int size, float radius, Vector4 border)
        {
            var texture = Kit.Own(new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "Colosseum UI shape" });
            texture.wrapMode = TextureWrapMode.Clamp;
            var pixels = new Color32[size * size];
            float half = size * .5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = Mathf.Max(Mathf.Abs(x + .5f - half) - (half - radius), 0f);
                    float dy = Mathf.Max(Mathf.Abs(y + .5f - half) - (half - radius), 0f);
                    float alpha = Mathf.Clamp01(radius - Mathf.Sqrt(dx * dx + dy * dy) + .5f);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return Kit.Own(Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(.5f, .5f), 100f, 0, SpriteMeshType.FullRect, border));
        }

        static Texture2D MakeVignette()
        {
            const int size = 96;
            var texture = Kit.Own(new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "Colosseum vignette" });
            texture.wrapMode = TextureWrapMode.Clamp;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + .5f) / size * 2f - 1f, dy = (y + .5f) / size * 2f - 1f;
                    float edge = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.62f, 1.35f, Mathf.Sqrt(dx * dx + dy * dy)));
                    pixels[y * size + x] = new Color32(24, 10, 30, (byte)Mathf.RoundToInt(edge * 150f));
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }

        /// <summary>Empty container; a stretched one fills its parent so children can anchor to screen edges.</summary>
        static GameObject Group(string name, Transform parent, bool stretch = false)
        {
            var item = new GameObject(name, typeof(RectTransform));
            item.transform.SetParent(parent, false);
            var rect = item.GetComponent<RectTransform>();
            rect.pivot = new Vector2(.5f, .5f);
            rect.anchorMin = stretch ? Vector2.zero : new Vector2(.5f, .5f);
            rect.anchorMax = stretch ? Vector2.one : new Vector2(.5f, .5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;
            return item;
        }

        static void Place(RectTransform rect, Transform parent, Vector2 anchor, Vector2 position, Vector2 size)
        {
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        /// <summary>Rounded rectangle; a higher roundness value gives tighter corners for small shapes.</summary>
        Image Box(Transform parent, Vector2 anchor, Vector2 position, Vector2 size, Color color, float tight = 1.6f)
        {
            var image = new GameObject("Box", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            Place(image.rectTransform, parent, anchor, position, size);
            image.sprite = rounded;
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = tight;
            image.color = color;
            image.raycastTarget = false;
            ColosseumUiContour.Fill(image, 22f / tight);
            return image;
        }

        static Image SmoothQuad(Image image)
        {
            ColosseumUiContour.Fill(image, 0f);
            return image;
        }

        static Image Plain(Transform parent, Vector2 anchor, Vector2 position, Vector2 size, Color color)
        {
            var image = new GameObject("Plain", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            Place(image.rectTransform, parent, anchor, position, size);
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        /// <summary>Plate with a thick dark border and a soft gloss across its upper half; returns the coloured face.</summary>
        Image Frame(Transform parent, Vector2 anchor, Vector2 position, Vector2 size, Color fill, float border, float tight = 1.6f)
        {
            Image outer = Box(parent, anchor, position, size, Ink, tight);
            Image face = Box(outer.transform, new Vector2(.5f, .5f), Vector2.zero, size - Vector2.one * border * 2f, fill, tight * 1.15f);
            Box(face.transform, new Vector2(.5f, 1f), new Vector2(0f, -(size.y - border * 2f) * .24f),
                new Vector2(size.x - border * 2f - 6f, (size.y - border * 2f) * .40f), new Color(1f, 1f, 1f, .16f), tight * 1.6f);
            return face;
        }

        TextMeshProUGUI Label(Transform parent, string text, float size, Color color, Vector2 anchor, Vector2 position, Vector2 area, Material look)
        {
            var label = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
            Place(label.rectTransform, parent, anchor, position, area);
            if (font != null) label.font = font;
            if (look != null) label.fontSharedMaterial = look;
            label.text = text;
            label.fontSize = size;
            label.color = color;
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Overflow;
            label.raycastTarget = false;
            return label;
        }

        RectTransform Press(Transform parent, string text, Vector2 anchor, Vector2 position, Vector2 size, Color color, Color lip, float fontSize, Action action, float lean = 0f)
        {
            Image outer = Box(parent, anchor, position + new Vector2(0f, -4f), size + new Vector2(10f, 18f), Ink);
            Image under = Box(outer.transform, new Vector2(.5f, .5f), new Vector2(0f, -4f), size, lip);
            Image face = Box(outer.transform, new Vector2(.5f, .5f), new Vector2(0f, 4f), size, color);
            Image gloss = Box(face.transform, new Vector2(.5f, 1f), new Vector2(0f, -size.y * .22f), new Vector2(size.x - 14f, size.y * .34f), new Color(1f, 1f, 1f, .28f), 2.4f);
            if (lean != 0f)
            {
                Slanted(outer, lean);
                Slanted(under, lean);
                Slanted(face, lean);
                Slanted(gloss, lean);
            }
            outer.raycastTarget = true;
            var button = outer.gameObject.AddComponent<Button>();
            button.targetGraphic = face;
            button.onClick.AddListener(() => { MatchSound?.Invoke("ui-tap"); action(); });
            Label(face.transform, text, fontSize, Color.white, new Vector2(.5f, .5f), new Vector2(0f, 2f), size, bold);
            return outer.rectTransform;
        }

        Plate MakePlate(Transform parent, bool friendly)
        {
            var plate = new Plate { Root = Group("Plate", parent).GetComponent<RectTransform>() };
            plate.Root.sizeDelta = new Vector2(130f, 34f);
            plate.Root.localScale = Vector3.one * .61f;
            Color tone = friendly ? FriendGreen : FoeRed;
            Image frame = Box(plate.Root, new Vector2(.5f, .5f), new Vector2(13f, 0f), new Vector2(BarWide + 8f, BarTall + 8f), Ink, 3.2f);
            Box(frame.transform, new Vector2(.5f, .5f), Vector2.zero, new Vector2(BarWide, BarTall), new Color32(48, 38, 64, 255), 4.2f);
            plate.Ghost = Bar(frame.transform, GhostTone);
            plate.Fill = Bar(frame.transform, tone);
            plate.Gloss = Bar(frame.transform, new Color(1f, 1f, 1f, .30f));
            plate.Gloss.rectTransform.sizeDelta = new Vector2(BarWide, BarTall * .38f);
            plate.Gloss.rectTransform.anchoredPosition = new Vector2(-BarWide * .5f, BarTall * .24f);
            for (int i = 1; i < 3; i++)
                Plain(frame.transform, new Vector2(.5f, .5f), new Vector2(-BarWide * .5f + BarWide * i / 3f, 0f), new Vector2(2f, BarTall), new Color(Ink.r, Ink.g, Ink.b, .26f));
            plate.Health = Label(frame.transform, "120", 19f, Color.white, new Vector2(.5f, .5f), new Vector2(0f, 1f), new Vector2(BarWide, 26f), bold);

            Image badge = Box(plate.Root, new Vector2(.5f, .5f), new Vector2(-46f, 0f), new Vector2(34f, 34f), Ink, 1f);
            badge.sprite = circle;
            ColosseumUiContour.Fill(badge, 0f, true);
            badge.type = Image.Type.Simple;
            Image core = Box(badge.transform, new Vector2(.5f, .5f), Vector2.zero, new Vector2(27f, 27f), friendly ? (Color)new Color32(40, 120, 240, 255) : (Color)new Color32(176, 44, 60, 255), 1f);
            core.sprite = circle;
            ColosseumUiContour.Fill(core, 0f, true);
            core.type = Image.Type.Simple;
            plate.Level = Label(badge.transform, "1", 20f, Yellow, new Vector2(.5f, .5f), new Vector2(0f, 1f), new Vector2(34f, 28f), bold);

            plate.Boost = Group("Power up", plate.Root).GetComponent<RectTransform>();
            Place(plate.Boost, plate.Root, new Vector2(.5f, .5f), new Vector2(84f, 4f), new Vector2(56f, 56f));
            Icon(plate.Boost, Drawn("swords"), new Vector2(.5f, .5f), Vector2.zero, 56f, Color.white);
            Image up = Icon(plate.Boost, arrow, new Vector2(.5f, .5f), new Vector2(19f, -14f), 27f, Ink);
            up.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 180f);
            Icon(up.transform, arrow, new Vector2(.5f, .5f), new Vector2(0f, 1f), 18f, new Color32(120, 255, 90, 255));
            plate.Boost.gameObject.SetActive(false);
            return plate;
        }

        Image Bar(Transform frame, Color color)
        {
            Image bar = Box(frame, new Vector2(.5f, .5f), Vector2.zero, new Vector2(BarWide, BarTall), color, 4.2f);
            bar.rectTransform.pivot = new Vector2(0f, .5f);
            bar.rectTransform.anchoredPosition = new Vector2(-BarWide * .5f, 0f);
            return bar;
        }

        /// <summary>
        /// Versus banner across the top: blue (you) against red (rival), with the dividing line sliding towards
        /// whichever side has less total health left.
        /// </summary>
        void BuildScoreBanner()
        {
            Transform root = scoreHeader;
            var top = new Vector2(.5f, 1f);
            var middle = new Vector2(.5f, .5f);
            Image outer = Box(root, top, new Vector2(0f, -36f), new Vector2(ScoreWide + 8f, ScoreTall + 8f), Ink, 1.7f);
            Image face = Box(outer.transform, middle, Vector2.zero, new Vector2(ScoreWide, ScoreTall), BlueSide, 2f);
            // Share one fractional edge between both teams and the gloss, including the far red corner.
            scoreFace = Kit.Own(new Material(face.material) { name = "Colosseum team score" });
            scoreFace.EnableKeyword("COLOSSEUM_TEAM_SCORE");
            scoreFace.SetColor("_RivalColor", RedSide);
            face.material = scoreFace;

            for (int team = 0; team < 2; team++)
            {
                float side = team == 0 ? -1f : 1f;
                teamLabels[team] = Label(outer.transform, team == 0 ? "YOU" : "RIVAL", 23f, Color.white, middle, new Vector2(side * 236f, 7f), new Vector2(160f, 30f), bold);
                totalLabels[team] = Label(outer.transform, "0", 14f, Cream, middle, new Vector2(side * 236f, -13f), new Vector2(160f, 18f), slim);
            }
            versus = Group("Versus", outer.transform).GetComponent<RectTransform>();
            Place(versus, outer.transform, middle, Vector2.zero, new Vector2(80f, 38f));
            Label(versus, "VS", 29f, Ink, middle, new Vector2(0f, -2.5f), new Vector2(80f, 38f), bold);
            Label(versus, "VS", 29f, Color.white, middle, Vector2.zero, new Vector2(80f, 38f), bold);

            Image turnFace = Frame(root, top, new Vector2(0f, -88f), new Vector2(126f, 54f), new Color32(178, 226, 243, 255), 3f, 2f);
            turnBadge = (RectTransform)turnFace.transform.parent;
            Label(turnFace.transform, "TURN", 14f, TurnInk, middle, new Vector2(0f, 12f), new Vector2(112f, 18f), null);
            turnNumber = Label(turnFace.transform, string.Empty, 27f, TurnInk, middle, new Vector2(0f, -8f), new Vector2(112f, 30f), null);
            SetTurnCounter(1);
        }

        public void SetTurnCounter(int number)
        {
            turnNumber.text = number + "/" + ColosseumGame.RoundLimit;
            turnNumber.color = number == ColosseumGame.RoundLimit ? (Color)new Color32(142, 48, 38, 255) : TurnInk;
            turnPulse = 1f;
        }

        /// <summary>Chunky downward-pointing triangle with rounded corners, drawn from its signed distance.</summary>
        Sprite MakePointer(int size)
        {
            var texture = Kit.Own(new Texture2D(size, size, TextureFormat.RGBA32, true) { name = "Colosseum UI pointer", filterMode = FilterMode.Trilinear });
            texture.wrapMode = TextureWrapMode.Clamp;
            var pixels = new Color32[size * size];
            Vector2 a = new Vector2(-.62f, .5f), b = new Vector2(.62f, .5f), c = new Vector2(0f, -.6f);
            const float round = .22f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    var p = new Vector2((x + .5f) / size * 2f - 1f, (y + .5f) / size * 2f - 1f);
                    float gap = Mathf.Sqrt(Mathf.Min(Nearest(p, a, b), Mathf.Min(Nearest(p, b, c), Nearest(p, c, a))));
                    bool inside = Cross(b - a, p - a) <= 0f && Cross(c - b, p - b) <= 0f && Cross(a - c, p - c) <= 0f;
                    float distance = (inside ? -gap : gap) - round;
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(Mathf.Clamp01(.5f - distance * size * .5f) * 255f));
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(true, true);
            return Kit.Own(Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(.5f, .5f), 100f));
        }

        static float Cross(Vector2 u, Vector2 v) { return u.x * v.y - u.y * v.x; }

        static float Nearest(Vector2 p, Vector2 from, Vector2 to)
        {
            Vector2 edge = to - from;
            Vector2 offset = p - from - edge * Mathf.Clamp01(Vector2.Dot(p - from, edge) / edge.sqrMagnitude);
            return offset.sqrMagnitude;
        }

        Sprite MakeHeart(int size)
        {
            var texture = Kit.Own(new Texture2D(size, size, TextureFormat.RGBA32, true) { name = "Colosseum UI heart", filterMode = FilterMode.Trilinear });
            texture.wrapMode = TextureWrapMode.Clamp;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int inside = 0;
                    for (int j = 0; j < 3; j++)
                    {
                        for (int i = 0; i < 3; i++)
                        {
                            float u = ((x + (i + .5f) / 3f) / size * 2f - 1f) * 1.28f;
                            float v = ((y + (j + .5f) / 3f) / size * 2f - 1f) * 1.28f + .12f;
                            float ball = u * u + v * v - 1f;
                            if (ball * ball * ball - u * u * v * v * v <= 0f) inside++;
                        }
                    }
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(inside * 255 / 9));
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(true, true);
            return Kit.Own(Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(.5f, .5f), 100f));
        }

        /// <summary>Per-hero chips stay built but hidden, so they can be switched back on without touching the update code.</summary>
        void BuildTeamStrip(int team)
        {
            Color deep = MeshKit.Rgb(Kit.TeamDark[team], 1f);
            GameObject shelf = Group("Hero chips " + team, battleRoot.transform);
            shelf.SetActive(false);
            for (int slot = 0; slot < TeamSize; slot++)
            {
                var chip = new Chip();
                chip.Back = Frame(shelf.transform, new Vector2(.5f, .5f), new Vector2(slot * 100f, 0f), new Vector2(94f, 70f), deep, 3.5f, 2f);
                chip.Name = Label(chip.Back.transform, "", 15f, Cream, new Vector2(.5f, 1f), new Vector2(0f, -13f), new Vector2(90f, 20f), slim);
                chip.Health = Label(chip.Back.transform, "120", 24f, Color.white, new Vector2(.5f, .5f), new Vector2(0f, -3f), new Vector2(90f, 30f), bold);
                Box(chip.Back.transform, new Vector2(.5f, 0f), new Vector2(0f, 10f), new Vector2(78f, 12f), Ink, 4.2f);
                chip.Fill = Box(chip.Back.transform, new Vector2(.5f, 0f), new Vector2(-36f, 10f), new Vector2(72f, 7f), team == 0 ? FriendGreen : FoeRed, 5f);
                chip.Fill.rectTransform.pivot = new Vector2(0f, .5f);
                chips[team, slot] = chip;
            }
        }

        // ------------------------------------------------------------------ runtime

        public void ShowMenu(bool visible)
        {
            if (!visible)
            {
                CloseHeroSelection();
                CloseProfile();
            }
            if (visible) ShowTab(BattleTab, false);   // always come back to the lobby itself, not to a page left open
            menu.SetActive(visible);
            if (visible && EventSystem.current != null)
            {
                var focus = EventSystem.current.currentSelectedGameObject;
                if (focus == null || !focus.activeInHierarchy)
                    EventSystem.current.SetSelectedGameObject(tabs[BattleTab].Plate.gameObject);
            }
            vignetteItem.SetActive(!visible);   // the arena's dark corners would muddy the bright lobby
        }
        public void ShowBattle(bool visible)
        {
            if (!visible) HideAimCancel();
            battleRoot.SetActive(visible);
        }
        public void ShowEnd(bool visible) { endButtons.SetActive(visible); }

        public void SetBattleOutput(Rect outputPixels)
        {
            if (outputPixels.width <= 0f || outputPixels.height <= 0f) return;
            battleOutputPixels = outputPixels;
            bool renderTexture = captureCamera && captureCamera.targetTexture;
            if (renderTexture)
            {
                // CanvasScaler normally follows the display. A recording can use a different aspect and resolution.
                Vector2 reference = scaler.referenceResolution;
                scaler.scaleFactor = Mathf.Min(outputPixels.width / reference.x, outputPixels.height / reference.y);
            }
            Rect safe = renderTexture ? outputPixels : Screen.safeArea;
            Vector2 anchor = new Vector2(.5f, 1f);
            if (safe.width > 0f && safe.height > 0f && safe.Overlaps(outputPixels))
            {
                float left = Mathf.Max(safe.xMin, outputPixels.xMin), right = Mathf.Min(safe.xMax, outputPixels.xMax);
                anchor = Rect.PointToNormalized(outputPixels, new Vector2((left + right) * .5f, Mathf.Min(safe.yMax, outputPixels.yMax)));
            }
            if (scoreHeader.anchorMin != anchor)
                scoreHeader.anchorMin = scoreHeader.anchorMax = anchor;
        }

        public void ShowAimCancel(Vector2 screenPoint, float diameterPixels)
        {
            if (diameterPixels <= 0f || !ToCanvas(screenPoint, out Vector2 center))
            {
                HideAimCancel();
                return;
            }
            const float designDiameter = 100f;
            if (!aimCancelMarker)
            {
                Vector2 middle = new Vector2(.5f, .5f);
                aimCancelMarker = Icon(battleRoot.transform, circle, middle, Vector2.zero, designDiameter, new Color32(72, 14, 24, 235)).rectTransform;
                aimCancelMarker.name = "Aim cancel";
                aimCancelFace = Icon(aimCancelMarker, circle, middle, Vector2.zero, 89f, Color.white);
                // A ring round the X fills clockwise from the top while the finger is held on it.
                Sprite ring = Paint("Aim cancel ring", 128, (x, y) => { float r = Mathf.Sqrt(x * x + y * y); return r > .8f && r < 1f; });
                aimCancelTrack = Icon(aimCancelMarker, ring, middle, Vector2.zero, 128f, new Color32(40, 8, 14, 160));
                aimCancelRing = Icon(aimCancelMarker, ring, middle, Vector2.zero, 128f, new Color32(255, 226, 92, 255));
                aimCancelRing.type = Image.Type.Filled;
                aimCancelRing.fillMethod = Image.FillMethod.Radial360;
                aimCancelRing.fillOrigin = (int)Image.Origin360.Top;
                aimCancelRing.fillClockwise = true;
                aimCancelRing.fillAmount = 0f;
                aimCancelTrack.transform.SetAsFirstSibling();
                aimCancelRing.transform.SetSiblingIndex(1);
                // Both outlines sit behind the two white strokes so the intersection stays clean.
                AddCross(Vector2.down, new Vector2(23f, 56f), new Color32(64, 12, 20, 255));
                AddCross(Vector2.zero, new Vector2(17f, 50f), new Color32(255, 255, 255, 255));

                void AddCross(Vector2 offset, Vector2 size, Color color)
                {
                    for (int side = -1; side <= 1; side += 2)
                    {
                        Image stroke = Box(aimCancelMarker, middle, offset, size, color, 2f);
                        stroke.rectTransform.localRotation = Quaternion.Euler(0f, 0f, side * 45f);
                    }
                }
            }
            aimCancelMarker.anchoredPosition = center;
            float diameter = diameterPixels / designDiameter;
            aimCancelMarker.localScale = battleOutputPixels.width > 0f
                ? new Vector3(diameter * canvas.rect.width / battleOutputPixels.width, diameter * canvas.rect.height / battleOutputPixels.height, 1f)
                : Vector3.one * (diameter / screen.scaleFactor);
            SetAimCancelTargeted(false);
            if (!aimCancelMarker.gameObject.activeSelf) aimCancelMarker.gameObject.SetActive(true);
        }

        public void SetAimCancelProgress(float progress)
        {
            if (!aimCancelRing) return;
            aimCancelRing.fillAmount = Mathf.Clamp01(progress);
            aimCancelTrack.enabled = aimCancelRing.enabled = progress > 0f;
        }

        public void SetAimCancelTargeted(bool targeted)
        {
            SetAimCancelProgress(0f);
            if (aimCancelFace)
                aimCancelFace.color = targeted ? new Color32(255, 58, 64, 255) : new Color32(208, 40, 52, 240);
        }

        public void HideAimCancel()
        {
            if (aimCancelMarker && aimCancelMarker.gameObject.activeSelf) aimCancelMarker.gameObject.SetActive(false);
        }

        public void SetLabels(bool againstAi, string rival = null)
        {
            teamLabels[0].text = againstAi ? "YOU" : "P1";
            teamLabels[1].text = againstAi ? (string.IsNullOrEmpty(rival) ? "RIVAL" : rival) : "P2";
        }

        public void SetStatus(string title, string prompt, int team)
        {
            bool visible = !string.IsNullOrEmpty(title);
            if (statusRoot.activeSelf != visible) statusRoot.SetActive(visible);
            if (!visible) return;
            if (statusTitle.text != title) statusTitle.text = title;
            if (statusPrompt.text != prompt) statusPrompt.text = prompt;
            statusTitle.color = team == 0 ? Yellow : (Color)MeshKit.Rgb(Kit.TeamLight[1], 1f);
        }

        /// <summary>
        /// Round and victory call-out. Tone 0/1 paints the band in that team's colour, 2 in warning orange, anything else sunny yellow.
        /// A band given 'aside' slams in across the middle like any other and then, that many seconds in, drops to the foot
        /// of the screen and stays there: map events use it so that what they announce can be seen arriving on the floor.
        /// </summary>
        public void Banner(string title, string sub, float seconds, int tone = -1, float aside = -1f)
        {
            bool team = tone == 0 || tone == 1;
            Show(bannerCall, title, sub, team ? TeamBand[tone] : tone == 2 ? DangerBand : SunnyBand, team ? (Color32)Yellow : new Color32(255, 255, 255, 255),
                tone == 1 ? 1f : -1f, Mathf.Max(.6f, seconds - .35f));
            bannerCall.Waiting = false;
            bannerCall.Age = 0f;
            bannerCall.Aside = aside;
            bannerCall.Root.anchoredPosition = CalloutHome;
            bannerCall.Root.gameObject.SetActive(true);
        }

        public void HideBanner() { bannerCall.Waiting = false; bannerCall.Root.gameObject.SetActive(false); }

        /// <summary>Shows or hides the power-up badge on a hero's health plate.</summary>
        public void SetBoost(int index, bool on)
        {
            if (index < 0 || index >= plates.Count) return;
            Plate plate = plates[index];
            if (on == plate.Boosted) return;
            plate.Boosted = on;
            plate.BoostPulse = 1f;
            plate.Boost.gameObject.SetActive(on);
        }

        /// <summary>Big centre call-out when the turn changes; waits for any round banner to clear first.</summary>
        public void AnnounceTurn(string text, int team)
        {
            Show(turnCall, text, null, TeamBand[team], team == 0 ? (Color32)Yellow : new Color32(255, 255, 255, 255), team == 0 ? -1f : 1f, 1.05f);
            turnCall.Waiting = true;
            turnCall.Root.gameObject.SetActive(false);
        }

        public void HideTurn() { turnCall.Waiting = false; turnCall.Root.gameObject.SetActive(false); }

        Callout MakeCallout(Transform parent, float tall, float titleSize, bool withSub)
        {
            var call = new Callout { Root = Group("Call-out", parent).GetComponent<RectTransform>() };
            Place(call.Root, parent, new Vector2(.5f, .5f), CalloutHome, new Vector2(720f, tall + 30f));
            call.Root.localRotation = Quaternion.Euler(0f, 0f, 3f);
            // Dark keyline, then the bright band, a glossy upper half and candy-white edges.
            Image rim = SmoothQuad(Plain(call.Root, new Vector2(.5f, .5f), Vector2.zero, new Vector2(1100f, tall + 10f), Ink));
            call.Band = rim.rectTransform;
            call.Face = SmoothQuad(Plain(call.Band, new Vector2(.5f, .5f), Vector2.zero, new Vector2(1100f, tall), Color.white));
            SmoothQuad(Plain(call.Face.transform, new Vector2(.5f, 1f), new Vector2(0f, -tall * .24f), new Vector2(1100f, tall * .48f), new Color(1f, 1f, 1f, .2f)));
            SmoothQuad(Plain(call.Face.transform, new Vector2(.5f, 0f), new Vector2(0f, tall * .09f), new Vector2(1100f, tall * .18f), new Color(0f, 0f, 0f, .1f)));
            call.EdgeTop = SmoothQuad(Plain(call.Face.transform, new Vector2(.5f, 1f), new Vector2(0f, -4.5f), new Vector2(1100f, 9f), Color.white));
            call.EdgeBottom = SmoothQuad(Plain(call.Face.transform, new Vector2(.5f, 0f), new Vector2(0f, 4.5f), new Vector2(1100f, 9f), Color.white));
            call.Title = Label(call.Root, string.Empty, titleSize, Color.white, new Vector2(.5f, .5f), new Vector2(0f, withSub ? 20f : 0f), new Vector2(720f, titleSize + 28f), bold);
            if (withSub)
                call.Sub = Label(call.Root, string.Empty, 30f, Color.white, new Vector2(.5f, .5f), new Vector2(0f, -44f), new Vector2(700f, 40f), slim);
            call.Root.gameObject.SetActive(false);
            return call;
        }

        void Show(Callout call, string title, string sub, Color band, Color32 text, float side, float hold)
        {
            call.Title.text = title;
            // Long lines shrink to stay inside the screen.
            float fit = call.Sub != null ? 78f : 92f;
            call.Title.fontSize = title.Length > 11 ? fit * 11f / title.Length : fit;
            if (call.Sub != null)
            {
                call.Sub.text = sub ?? string.Empty;
                call.Title.rectTransform.anchoredPosition = new Vector2(0f, string.IsNullOrEmpty(sub) ? 0f : 20f);
            }
            call.Tone = text;
            call.Side = side;
            call.Hold = hold;
            call.Face.color = band;
            call.Band.pivot = new Vector2(side < 0f ? 0f : 1f, .5f);
            call.Band.anchoredPosition = new Vector2(side * 550f, 0f);
        }

        void Animate(Callout call, float delta)
        {
            call.Age += delta;
            float time = call.Age;
            float leave = Mathf.Clamp01((time - call.Hold) / .3f);
            float wipe = 1f - Mathf.Pow(1f - Mathf.Clamp01(time / .18f), 3f);
            float thump = Mathf.Exp(-Mathf.Max(0f, time - .2f) * 7f) * Mathf.Sin(Mathf.Max(0f, time - .2f) * 30f) * .07f;
            // A band that makes room falls faster and faster to the foot of the screen and lands with a bump of its own.
            float down = 0f;
            if (call.Aside >= 0f)
            {
                float fall = Mathf.Clamp01((time - call.Aside) / BannerFall);
                float landed = time - call.Aside - BannerFall;
                down = fall * fall;
                if (landed > 0f) thump += Mathf.Exp(-landed * 9f) * Mathf.Sin(landed * 32f) * .05f;
            }
            float shrink = Mathf.Lerp(1f, AsideSize, down);
            call.Root.anchoredPosition = new Vector2(CalloutHome.x, Mathf.Lerp(CalloutHome.y, AsideFoot - canvas.rect.height * .5f, down));
            call.Root.localScale = Vector3.one * ((1f + thump) * shrink);
            // The band stays as wide as ever while the rest of the call-out shrinks: its ends must stay off the screen, however wide that is.
            call.Band.localScale = new Vector3(Mathf.Max(.001f, wipe) / shrink, Mathf.Max(.001f, 1f - leave * leave), 1f);
            call.Band.anchoredPosition = new Vector2(call.Side * 550f / shrink, 0f);
            call.Root.localRotation = Quaternion.Euler(0f, 0f, 3f + thump * 40f);
            if (call.Sub != null)
            {
                float shown = Mathf.Clamp01((time - .42f) / .2f);
                call.Sub.alpha = shown * (1f - leave);
                call.Sub.rectTransform.localScale = Vector3.one * (1.25f - .25f * shown * (2f - shown));
            }

            TextMeshProUGUI label = call.Title;
            label.ForceMeshUpdate();
            TMP_TextInfo info = label.textInfo;
            int count = info.characterCount;
            for (int i = 0; i < count; i++)
            {
                TMP_CharacterInfo glyph = info.characterInfo[i];
                if (!glyph.isVisible) continue;
                int order = call.Side < 0f ? i : count - 1 - i;
                float local = time - .06f - order * .034f;
                float fall = Mathf.Clamp01(local / .22f);
                float far = (1f - fall) * (1f - fall);
                float landed = Mathf.Max(0f, local - .22f);
                float squash = fall >= 1f ? Mathf.Exp(-landed * 8f) * Mathf.Cos(landed * 24f) * .34f : 0f;
                float size = 1f + far * 2.1f;
                float wide = size * (1f + squash), tall = size * (1f - squash);
                float lean = far * 42f * ((i & 1) == 0 ? 1f : -1f) + Mathf.Sin(time * 7f + i * .9f) * 3f * fall;
                float gone = Mathf.Clamp01((time - call.Hold - order * .02f) / .22f);
                var shift = new Vector3(
                    call.Side * far * 190f - call.Side * gone * gone * 640f,
                    far * 150f + Mathf.Sin(time * 9f + i * .75f) * 4.5f * fall + Mathf.Sin(gone * Mathf.PI) * 26f, 0f);
                float alpha = local <= 0f ? 0f : Mathf.Clamp01(local / .07f) * (1f - gone * gone);
                Color32 tint = Color32.Lerp(new Color32(255, 255, 255, 255), call.Tone, Mathf.Clamp01((local - .16f) / .16f));
                tint.a = (byte)Mathf.RoundToInt(alpha * 255f);

                int slot = glyph.materialReferenceIndex, first = glyph.vertexIndex;
                Vector3[] corners = info.meshInfo[slot].vertices;
                Vector4[] uvs = info.meshInfo[slot].uvs0;
                Color32[] tints = info.meshInfo[slot].colors32;
                var foot = new Vector3((corners[first].x + corners[first + 2].x) * .5f, corners[first].y, 0f);
                Quaternion spin = Quaternion.Euler(0f, 0f, lean);
                // TMP packs the SDF sampling scale in UV0.w. These glyphs change size after its layout pass.
                float samplingScale = Mathf.Min(wide, tall);
                for (int corner = 0; corner < 4; corner++)
                {
                    Vector3 offset = corners[first + corner] - foot;
                    corners[first + corner] = foot + spin * new Vector3(offset.x * wide, offset.y * tall, 0f) + shift;
                    uvs[first + corner].w *= samplingScale;
                    tints[first + corner] = tint;
                }
            }
            label.UpdateVertexData(TMP_VertexDataUpdateFlags.All);
            if (time >= call.Hold + .26f + count * .02f)
                call.Root.gameObject.SetActive(false);
        }

        public void Pop(Vector3 world, string text, Color color, float scale)
        {
            Popup popup = popups[popupCursor];
            popupCursor = (popupCursor + 1) % popups.Count;
            // Words that would land on top of others just out move up the arena until they are clear of them.
            for (int pass = 0; pass < 4; pass++)
            {
                bool moved = false;
                foreach (Popup other in popups)
                {
                    if (!other.Active || other.Age >= .45f || (other.World - world).sqrMagnitude >= 1.4f) continue;
                    world += Vector3.forward * 1.5f;
                    moved = true;
                }
                if (!moved) break;
            }
            popup.Active = true;
            popup.Age = 0f;
            popup.Scale = scale;
            popup.World = world;
            popup.Label.text = text;
            popup.Label.color = color;
            popup.Label.gameObject.SetActive(true);
            popup.Label.transform.SetAsLastSibling();
        }

        public void ClearPopups()
        {
            foreach (Popup popup in popups)
            {
                popup.Active = false;
                popup.Label.gameObject.SetActive(false);
            }
            foreach (Plate plate in plates)
                plate.Shown = plate.Trail = 1f;
        }

        /// <summary>Draw the HUD through a camera for one frame so it can be saved to an image, then switch back.</summary>
        public void BeginCapture(Camera camera)
        {
            captureCamera = camera;
            screen.renderMode = RenderMode.ScreenSpaceCamera;
            screen.worldCamera = camera;
            screen.planeDistance = 1f;
            if (camera.targetTexture)
            {
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
                SetBattleOutput(camera.pixelRect);
            }
            Canvas.ForceUpdateCanvases();
        }

        public void EndCapture()
        {
            captureCamera = null;
            screen.renderMode = RenderMode.ScreenSpaceOverlay;
            screen.worldCamera = null;
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            battleOutputPixels = default;
        }

        bool ToCanvas(Camera camera, Vector3 world, out Vector2 local)
        {
            return ToCanvas(camera.WorldToScreenPoint(world), out local);
        }

        bool ToCanvas(Vector2 screenPoint, out Vector2 local)
        {
            if (battleOutputPixels.width <= 0f)
                return RectTransformUtility.ScreenPointToLocalPointInRectangle(canvas, screenPoint, captureCamera, out local);
            Vector2 offset = screenPoint - battleOutputPixels.center;
            local = new Vector2(offset.x * canvas.rect.width / battleOutputPixels.width,
                offset.y * canvas.rect.height / battleOutputPixels.height);
            return true;
        }

        /// <summary>
        /// Where a called-out word is drawn through this camera: over its place in the arena, drifting up as it
        /// ages. One called out beside a side wall stays whole on the screen: it is held to where it fits at its
        /// biggest, so that it does not slide about as it swells and settles.
        /// </summary>
        bool PopupSpot(Camera camera, Popup popup, out Vector2 spot)
        {
            if (!ToCanvas(camera, popup.World + Vector3.up * (2.9f + popup.Age * .9f), out spot)) return false;
            float half = popup.Label.preferredWidth * popup.Scale * 1.35f * .5f + 10f, edge = canvas.rect.width * .5f;
            if (half < edge) spot.x = Mathf.Clamp(spot.x, half - edge, edge - half);
            return true;
        }

        /// <summary>
        /// Pins each health plate above its hero. The game calls this after every doll has posed for the frame, so a
        /// plate always sits over where the hero is drawn instead of where he was a frame earlier (which made the
        /// plate shiver behind a fast-moving hero, most visibly the leaping orc).
        /// </summary>
        public void PlacePlates(Camera camera, IList<ColosseumDoll> dolls)
        {
            if (camera == null) return;
            foreach (ColosseumDoll doll in dolls)
            {
                Plate plate = plates[doll.Id];
                if (!plate.Root.gameObject.activeSelf) continue;
                float bulk = doll.Bulk;
                Vector3 above = doll.State == DollState.Idle || doll.State == DollState.Aim || doll.Casting
                    ? doll.HeadPosition + Vector3.up * (.95f * ColosseumDoll.Scale * bulk + .14f + doll.PlateLift)
                    : doll.transform.position + Vector3.up * (2.05f * ColosseumDoll.Scale * bulk + .14f);
                if (ToCanvas(camera, above, out Vector2 spot))
                    plate.Root.anchoredPosition = spot;
                if (plate.Marker.gameObject.activeSelf)
                    plate.Marker.anchoredPosition = plate.Root.anchoredPosition + new Vector2(0f, plate.MarkerLift);
            }
        }

        public void Tick(float delta, Camera camera, IList<ColosseumDoll> dolls, ColosseumDoll selected, int pickTeam = -1,
            ColosseumDoll requiredHero = null)
        {
            if (IsLaunchTitleOpen)
            {
                TickLaunchTitle();
                return;
            }
            TickVictoryRewards(delta);
            TickVictoryArrival(delta);
            TickJourneyChest(delta);
            TickArenaUnlock(delta);
            TickArenaCardGrant(delta);
            TickRecruitReveal(delta);
            TickUpgradeCoach(delta);
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (recruitHero >= 0) CollectRevealedHero();
                else if (heroSelection != null && heroSelection.activeSelf) CloseHeroSelection();
                else if (profileRoot.activeSelf) CloseProfile();
            }
            if (bannerCall.Root.gameObject.activeSelf)
                Animate(bannerCall, delta);
            if (turnCall.Waiting && !bannerCall.Root.gameObject.activeSelf)
            {
                turnCall.Waiting = false;
                turnCall.Age = 0f;
                turnCall.Root.gameObject.SetActive(true);
            }
            if (turnCall.Root.gameObject.activeSelf)
                Animate(turnCall, delta);

            foreach (Popup popup in popups)
            {
                if (!popup.Active) continue;
                popup.Age += delta;
                if (popup.Age > 1f)
                {
                    popup.Active = false;
                    popup.Label.gameObject.SetActive(false);
                    continue;
                }
                float k = popup.Age;
                float punch = k < .12f ? .6f + k / .12f * .75f : 1.35f - Mathf.Min(.35f, (k - .12f) * 2.4f);
                if (PopupSpot(camera, popup, out Vector2 spot))
                    popup.Label.rectTransform.anchoredPosition = spot;
                popup.Label.transform.localScale = Vector3.one * (punch * popup.Scale);
                popup.Label.transform.localRotation = Quaternion.Euler(0f, 0f, 5f);
                popup.Label.alpha = k > .75f ? (1f - k) * 4f : 1f;
            }

            markerClock += delta;
            if (matchRoot.activeSelf)
                AnimateMatch(delta);
            if (cardAction == CardAction.LevelUp || cardAction == CardAction.Equip) TickCardAction(delta);
            if (slotPicker != null && slotPicker.activeSelf) TickSlotPicker(delta);
            // Outside the lobby this only puts the guide hand away.
            TickLobbyGuide(delta);
            if (menu.activeSelf)
            {
                // The battle button breathes gently; a fresh coin reward drifts up under the purse and fades.
                lobbyClock += delta;
                AnimateTabs(delta);
                if (openTab == BattleTab && !matchRoot.activeSelf) TickJourney(delta);
                if (firstBattleHand && firstBattleHand.gameObject.activeSelf) TickBattleLesson(delta);
                else battleButton.localScale = Vector3.one * (1f + .022f * Mathf.Sin(lobbyClock * 4f));
                if (lobbyGainLeft > 0f)
                {
                    lobbyGainLeft -= delta;
                    lobbyGain.alpha = Mathf.Clamp01(lobbyGainLeft / .6f);
                    lobbyGain.rectTransform.anchoredPosition = new Vector2(-104f, -112f + (2.6f - lobbyGainLeft) * 5f);
                    if (lobbyGainLeft <= 0f) lobbyGain.gameObject.SetActive(false);
                }
            }
            System.Array.Clear(fightersBySlot, 0, fightersBySlot.Length);
            foreach (ColosseumDoll fighter in dolls) fightersBySlot[fighter.Id] = fighter;
            for (int i = 0; i < plates.Count; i++)
            {
                Plate plate = plates[i];
                ColosseumDoll doll = fightersBySlot[i];
                bool visible = doll != null && doll.Alive;
                if (plate.Root.gameObject.activeSelf != visible) plate.Root.gameObject.SetActive(visible);
                bool pick = visible && doll.Team == pickTeam && doll.State == DollState.Idle && (requiredHero == null || requiredHero == doll);
                plate.MarkerShow = Mathf.MoveTowards(plate.MarkerShow, pick ? 1f : 0f, delta * 6f);
                bool marked = plate.MarkerShow > .01f;
                if (plate.Marker.gameObject.activeSelf != marked) plate.Marker.gameObject.SetActive(marked);
                if (!visible) continue;
                if (plate.Boosted)
                {
                    // The badge pops when it appears.
                    plate.BoostPulse = Mathf.Max(0f, plate.BoostPulse - delta * 2.6f);
                    plate.Boost.localScale = Vector3.one * (1f + Mathf.Sin(plate.BoostPulse * Mathf.PI) * .6f);
                }
                if (marked)
                {
                    // Bouncy bob: quick drop, soft hang at the top, each hero slightly out of step with the next.
                    float beat = Mathf.Abs(Mathf.Sin(markerClock * 4.2f + i * .55f));
                    float pop = plate.MarkerShow * (1.2f - .2f * plate.MarkerShow);
                    plate.MarkerLift = 32f + beat * 13f;
                    plate.Marker.localScale = new Vector3(pop * (1f + (1f - beat) * .12f), pop * (1f - (1f - beat) * .14f), 1f);
                }

                float share = Mathf.Clamp01((float)doll.Health / doll.MaxHealth);
                plate.Shown = Mathf.MoveTowards(plate.Shown, share, delta * 2.2f);
                if (plate.Trail < plate.Shown) plate.Trail = plate.Shown;
                plate.Trail = Mathf.MoveTowards(plate.Trail, plate.Shown, delta * (plate.Trail - plate.Shown > .001f ? .45f : 0f));
                plate.Fill.rectTransform.sizeDelta = new Vector2(Mathf.Max(6f, BarWide * plate.Shown), BarTall);
                plate.Gloss.rectTransform.sizeDelta = new Vector2(Mathf.Max(6f, BarWide * plate.Shown), BarTall * .38f);
                plate.Ghost.rectTransform.sizeDelta = new Vector2(Mathf.Max(6f, BarWide * plate.Trail), BarTall);
                string health = doll.Health.ToString();
                if (plate.Health.text != health) plate.Health.text = health;
                string level = doll.Level.ToString();
                if (plate.Level.text != level) plate.Level.text = level;
            }

            foreach (ColosseumDoll doll in dolls)
            {
                Chip chip = chips[doll.Team, doll.Slot];
                chip.Name.text = ColosseumRoster.Heroes[doll.HeroIndex].Name;
                chip.Health.text = doll.Alive ? doll.Health.ToString() : "KO";
                chip.Health.color = doll.Alive ? Color.white : new Color(1f, .6f, .6f, 1f);
                chip.Fill.rectTransform.sizeDelta = new Vector2(72f * doll.Health / doll.MaxHealth, 7f);
                Color deep = MeshKit.Rgb(Kit.TeamDark[doll.Team], 1f);
                chip.Back.color = doll == selected ? Yellow : doll.Alive ? deep : new Color(.2f, .2f, .26f, 1f);
                chip.Name.alpha = doll.Alive ? 1f : .45f;
            }

            if (turnPulse > 0f)
            {
                turnPulse = Mathf.Max(0f, turnPulse - delta * 2.6f);
                turnBadge.localScale = Vector3.one * (1f + Mathf.Sin(turnPulse * Mathf.PI) * .22f);
            }

            totals[0] = totals[1] = 0;
            foreach (ColosseumDoll doll in dolls)
            {
                if (doll.Alive) totals[doll.Team] += doll.Health;
            }
            int all = totals[0] + totals[1];
            float lead = all > 0 ? (float)totals[0] / all : .5f;
            shownShare = Mathf.MoveTowards(shownShare, lead, delta * .5f);
            // The divider sits exactly at blue's share of all remaining health, kept clear of the two name labels.
            float slide = Mathf.Clamp((shownShare - .5f) * ScoreWide, -ScoreSwing, ScoreSwing);
            scoreFace.SetFloat(ScoreSplit, slide / (.5f * ScoreWide));
            versus.anchoredPosition = new Vector2(slide + 6f, 0f);
            for (int team = 0; team < 2; team++)
            {
                string total = totals[team].ToString();
                if (totalLabels[team].text != total) totalLabels[team].text = total;
            }
        }
    }
}
