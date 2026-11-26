using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BananaBall
{
    /// <summary>
    /// Portrait broadcast-style UI built in code (1080x1920 reference). Top: TV scorebug (teams, runs, inning, bases, outs, count)
    /// and the rival tag. Middle: batter/pitcher cards, comic pop-ups, banners. Bottom: controls for the current role —
    /// batting (tap/drag, bunt, auto, MEGA BAT), pitching (joystick, pitch buttons with the accuracy ring), fielding (tap a base
    /// to throw) and running (tap a base to send a runner, arrow to send him back). Lobby with line-up editor, result screen.
    /// </summary>
    public class Hud
    {
        readonly BananaBallGame G;
        readonly TMP_FontAsset font;
        readonly Sprite round, roundS, dot, ringSp, burstSp;
        Canvas canvas;
        Image flashImg;      // white full-screen flash on a big hit
        float flashA;
        RectTransform root, safe, matchUi;
        Rect lastSafe;

        // scorebug
        TextMeshProUGUI tAwayAbbr, tHomeAbbr, tAwayR, tHomeR, tInning, tCount, tRival, tRivalSub;
        Image awayStrip, homeStrip, awayBat, homeBat;
        readonly Image[] baseDots = new Image[3], outDots = new Image[2];

        // cards
        RectTransform cardBat, cardPit, nowStrip;
        TextMeshProUGUI cbName, cbNum, cbPos, cbTrait, cpName, cpNum, cpPitches, tNow, tPitchInfo;
        readonly Image[] cbBars = new Image[4], cpBars = new Image[4];
        Image cbStripe, cpStripe;
        float cardT, pitchInfoT;

        // controls
        RectTransform batCtl, pitCtl, diamond, hintRt;
        TextMeshProUGUI tHint, tAuto, tBunt, tMega, tDiamondTitle;
        Image autoImg, buntImg, megaImg, megaFill;
        Joystick joystick;
        readonly HoldButton[] pitchBtn = new HoldButton[4];
        readonly Image[] pitchImg = new Image[4];
        readonly TextMeshProUGUI[] pitchTxt = new TextMeshProUGUI[4];
        Image magicFill;
        readonly Button[] baseBtn = new Button[4];
        readonly Image[] baseImg = new Image[4];
        readonly TextMeshProUGUI[] baseTxt = new TextMeshProUGUI[4];
        readonly Button[] backBtn = new Button[4];
        Button skipBtn;

        // banners and pop-ups
        RectTransform bannerRt;
        CanvasGroup bannerGroup;
        TextMeshProUGUI tBanner, tBannerSub;
        float bannerT, bannerDur;
        class Pop { public RectTransform rt, subRt; public Image burst, subBg; public TextMeshProUGUI txt, sub; public float t, life, size, rot; public bool on; }
        readonly List<Pop> pops = new List<Pop>();
        RectTransform toastRt;
        TextMeshProUGUI tToast;
        float toastT;

        // screens
        RectTransform lobby, result, lineupPanel;
        TextMeshProUGUI tLobbyFans, tLobbyRec, tLobbyRival, tLobbyRivalSub, tResTitle, tResLine, tResSub, tResFans;
        readonly List<TextMeshProUGUI> lineupRows = new List<TextMeshProUGUI>();
        readonly List<Image> lineupBg = new List<Image>();
        int lineupPick = -1;
        int[] lineupOrder;
        public TeamData nextRival;
        int rivalPick;

        bool debugPitch;
        public bool DebugPitchHeld => debugPitch;

        public Hud(BananaBallGame game, TMP_FontAsset fontAsset)
        {
            G = game;
            font = fontAsset;
            round = Tex.RoundRect(64, 18);
            roundS = Tex.RoundRect(32, 6);
            dot = Tex.SpriteOf(Tex.Circle(128));
            ringSp = Tex.SpriteOf(Tex.RingTex(128, 0.8f));
            burstSp = Tex.SpriteOf(G.mats.burst);
            Build();
        }

        // ------------------------------------------------------------------ helpers

        static RectTransform Make(string n, Transform parent, Vector2 aMin, Vector2 aMax, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(n, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = aMin;
            rt.anchorMax = aMax;
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

        static RectTransform Fill(string n, Transform parent, float pad = 0f) =>
            Make(n, parent, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-pad * 2f, -pad * 2f));

        Image Img(RectTransform rt, Color c, Sprite s = null, bool ray = false, float ppu = 1f)
        {
            var i = rt.gameObject.AddComponent<Image>();
            i.sprite = s ?? round;
            i.type = s == null || s == round || s == roundS ? Image.Type.Sliced : Image.Type.Simple;
            i.pixelsPerUnitMultiplier = ppu;
            i.color = c;
            i.raycastTarget = ray;
            return i;
        }

        TextMeshProUGUI Txt(RectTransform parent, string text, float size, Color c, TextAlignmentOptions align, float outline = 0f, Color outlineCol = default)
        {
            var rt = Fill("T", parent);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            if (font != null) t.font = font;
            t.text = text;
            t.fontSize = size;
            t.color = c;
            t.alignment = align;
            t.raycastTarget = false;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.overflowMode = TextOverflowModes.Overflow;
            if (outline > 0f)
            {
                t.fontMaterial.EnableKeyword("OUTLINE_ON");
                t.outlineWidth = outline;
                t.outlineColor = outlineCol;
            }
            return t;
        }

        Button Btn(RectTransform rt, Image img, System.Action click)
        {
            var b = rt.gameObject.AddComponent<Button>();
            b.targetGraphic = img;
            var cb = b.colors;
            cb.pressedColor = new Color(0.75f, 0.75f, 0.75f, 1f);
            cb.disabledColor = new Color(0.6f, 0.6f, 0.6f, 0.5f);
            b.colors = cb;
            b.onClick.AddListener(() => { Sfx.Play(Sfx.Id.Tap, 0.5f); click(); });
            return b;
        }

        static void On(Component c, bool on) { if (c != null && c.gameObject.activeSelf != on) c.gameObject.SetActive(on); }
        static void Set(TextMeshProUGUI t, string s) { if (t != null && t.text != s) t.text = s; }

        // ------------------------------------------------------------------ build

        void Build()
        {
            if (Object.FindFirstObjectByType<EventSystem>() == null)
                new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            var cgo = new GameObject("HUD", typeof(RectTransform));
            canvas = cgo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = cgo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0f;
            cgo.AddComponent<GraphicRaycaster>();
            root = (RectTransform)cgo.transform;
            safe = Fill("Safe", root);

            matchUi = Fill("MatchUI", safe);
            BuildScorebug();
            BuildCards();
            BuildControls();
            BuildOverlays();
            BuildLobby();
            BuildResult();
            flashImg = Img(Fill("HitFlash", root), new Color(1f, 1f, 1f, 0f));
            flashImg.raycastTarget = false;
            flashImg.enabled = false;
            ShowLobby();
        }

        /// <summary>A white flash over the whole screen (bat on ball); it fades in real time, through the freeze-frame.</summary>
        public void Flash(float alpha) { flashA = Mathf.Max(flashA, Mathf.Clamp01(alpha)); }

        void BuildScorebug()
        {
            var bug = Make("Scorebug", matchUi, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -28f), new Vector2(560f, 170f));
            Img(bug, new Color(0.05f, 0.07f, 0.11f, 0.92f), round, false, 0.6f);
            var rows = Make("Rows", bug, new Vector2(0f, 0f), new Vector2(0.56f, 1f), new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero);
            for (int r = 0; r < 2; r++)
            {
                var row = Make("Row" + r, rows, new Vector2(0f, r == 0 ? 0.5f : 0f), new Vector2(1f, r == 0 ? 1f : 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-14f, -12f));
                var strip = Make("Strip", row, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(10f, 0f), new Vector2(14f, -10f));
                var si = Img(strip, Color.white, roundS);
                var batInd = Make("Bat", row, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(42f, 0f), new Vector2(18f, 18f));
                var bi = Img(batInd, Pal.Gold, dot);
                var ab = Make("Abbr", row, new Vector2(0f, 0f), new Vector2(0.72f, 1f), new Vector2(0f, 0.5f), new Vector2(60f, 0f), new Vector2(-60f, 0f));
                var ta = Txt(ab, "BAN", 50, Color.white, TextAlignmentOptions.MidlineLeft);
                var rr = Make("Runs", row, new Vector2(0.72f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), new Vector2(-6f, 0f), Vector2.zero);
                var trr = Txt(rr, "0", 60, Color.white, TextAlignmentOptions.MidlineRight);
                if (r == 0) { awayStrip = si; awayBat = bi; tAwayAbbr = ta; tAwayR = trr; }
                else { homeStrip = si; homeBat = bi; tHomeAbbr = ta; tHomeR = trr; }
            }
            var div = Make("Div", bug, new Vector2(0.56f, 0.1f), new Vector2(0.56f, 0.9f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(3f, 0f));
            Img(div, new Color(1f, 1f, 1f, 0.15f), roundS);
            var right = Make("Right", bug, new Vector2(0.56f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            tInning = Txt(Make("Inning", right, new Vector2(0f, 0.55f), new Vector2(0.48f, 1f), new Vector2(0.5f, 0.5f), new Vector2(8f, -4f), Vector2.zero), "▲9", 50, Pal.Gold, TextAlignmentOptions.Center);
            var dia = Make("Diamond", right, new Vector2(0.74f, 0.66f), new Vector2(0.74f, 0.66f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(110f, 80f));
            Vector2[] bp = { new Vector2(30f, -8f), new Vector2(0f, 22f), new Vector2(-30f, -8f) };
            for (int i = 0; i < 3; i++)
            {
                var b = Make("Base" + (i + 1), dia, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), bp[i], new Vector2(30f, 30f));
                b.localRotation = Quaternion.Euler(0f, 0f, 45f);
                baseDots[i] = Img(b, new Color(1f, 1f, 1f, 0.25f), roundS);
            }
            tCount = Txt(Make("Count", right, new Vector2(0f, 0f), new Vector2(0.55f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(10f, 4f), Vector2.zero), "0-0", 44, Color.white, TextAlignmentOptions.Center);
            for (int i = 0; i < 2; i++)
            {
                var d = Make("Out" + i, right, new Vector2(0.7f, 0.25f), new Vector2(0.7f, 0.25f), new Vector2(0.5f, 0.5f), new Vector2(i * 38f, 0f), new Vector2(26f, 26f));
                outDots[i] = Img(d, new Color(1f, 1f, 1f, 0.25f), dot);
            }

            var rival = Make("Rival", matchUi, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-24f, -28f), new Vector2(380f, 100f));
            Img(rival, new Color(0.05f, 0.07f, 0.11f, 0.85f), round, false, 0.6f);
            tRival = Txt(Make("Name", rival, new Vector2(0f, 0.45f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -2f), new Vector2(-30f, 0f)), "VS RIVAL", 34, Color.white, TextAlignmentOptions.MidlineRight);
            tRivalSub = Txt(Make("Sub", rival, new Vector2(0f, 0f), new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 4f), new Vector2(-30f, 0f)), "", 26, new Color(1f, 1f, 1f, 0.7f), TextAlignmentOptions.MidlineRight);

            tPitchInfo = Txt(Make("PitchInfo", matchUi, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -320f), new Vector2(700f, 60f)), "", 40, Color.white, TextAlignmentOptions.Center, 0.25f, Pal.Ink);
        }

        void BuildCards()
        {
            cardBat = Card("BatterCard", true, out cbStripe, out cbNum, out cbName, out cbPos, out cbTrait, cbBars, new[] { "콘택트", "파워", "주력", "선구안" });
            cardPit = Card("PitcherCard", false, out cpStripe, out cpNum, out cpName, out var cpPos, out cpPitches, cpBars, new[] { "구속", "제구", "변화", "체력" });
            Set(cpPos, "투수");
            nowStrip = Make("NowBatting", matchUi, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 560f), new Vector2(-80f, 64f));
            Img(nowStrip, new Color(0.05f, 0.07f, 0.11f, 0.75f), round, false, 0.6f);
            tNow = Txt(nowStrip, "", 32, Color.white, TextAlignmentOptions.Center);
        }

        RectTransform Card(string n, bool left, out Image stripe, out TextMeshProUGUI num, out TextMeshProUGUI name, out TextMeshProUGUI pos, out TextMeshProUGUI extra, Image[] bars, string[] labels)
        {
            var c = Make(n, matchUi, new Vector2(left ? 0f : 1f, 1f), new Vector2(left ? 0f : 1f, 1f), new Vector2(left ? 0f : 1f, 1f), new Vector2(left ? 24f : -24f, -220f), new Vector2(500f, 300f));
            Img(c, new Color(0.06f, 0.09f, 0.14f, 0.94f), round, false, 0.6f);
            var st = Make("Stripe", c, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 0f), new Vector2(0f, 10f));
            stripe = Img(st, Pal.Gold, roundS);
            num = Txt(Make("Num", c, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(18f, -10f), new Vector2(120f, 100f)), "00", 84, Pal.Gold, TextAlignmentOptions.Center);
            name = Txt(Make("Name", c, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(150f, -16f), new Vector2(-165f, 56f)), "", 38, Color.white, TextAlignmentOptions.MidlineLeft);
            name.enableAutoSizing = true; name.fontSizeMin = 22; name.fontSizeMax = 38;
            pos = Txt(Make("Pos", c, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(150f, -70f), new Vector2(-165f, 40f)), "", 28, new Color(1f, 1f, 1f, 0.75f), TextAlignmentOptions.MidlineLeft);
            for (int i = 0; i < 4; i++)
            {
                float y = -124f - i * 36f;
                Txt(Make("L" + i, c, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(22f, y), new Vector2(110f, 30f)), labels[i], 24, new Color(1f, 1f, 1f, 0.8f), TextAlignmentOptions.MidlineLeft);
                var bg = Make("B" + i, c, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 0.5f), new Vector2(140f, y), new Vector2(-170f, 16f));
                Img(bg, new Color(1f, 1f, 1f, 0.12f), roundS);
                var fill = Make("F", bg, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero);
                bars[i] = Img(fill, Pal.Gold, roundS);
            }
            extra = Txt(Make("Extra", c, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 18f), new Vector2(-36f, 40f)), "", 26, Pal.Gold, TextAlignmentOptions.MidlineLeft);
            extra.enableAutoSizing = true; extra.fontSizeMin = 16; extra.fontSizeMax = 26;
            return c;
        }

        void BuildControls()
        {
            // batting
            batCtl = Fill("Batting", matchUi);
            var bunt = Make("Bunt", batCtl, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0.5f, 0.5f), new Vector2(130f, 330f), new Vector2(180f, 180f));
            buntImg = Img(bunt, new Color(0.1f, 0.14f, 0.22f, 0.9f), dot, true);
            tBunt = Txt(bunt, "번트", 40, Color.white, TextAlignmentOptions.Center);
            Btn(bunt, buntImg, () => G.match.ToggleBunt());
            var auto = Make("Auto", batCtl, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0.5f, 0.5f), new Vector2(150f, 150f), new Vector2(230f, 96f));
            autoImg = Img(auto, new Color(0.1f, 0.14f, 0.22f, 0.9f), round, true, 0.5f);
            tAuto = Txt(auto, "자동 조준", 32, Color.white, TextAlignmentOptions.Center);
            Btn(auto, autoImg, () => G.match.ToggleAuto());
            var mega = Make("Mega", batCtl, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0.5f), new Vector2(-150f, 220f), new Vector2(230f, 230f));
            var megaBg = Img(mega, new Color(0.1f, 0.14f, 0.22f, 0.9f), dot, true);
            var mf = Make("Fill", mega, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            megaFill = Img(mf, Pal.Gold, dot);
            megaFill.type = Image.Type.Filled;
            megaFill.fillMethod = Image.FillMethod.Vertical;
            megaFill.fillOrigin = 0;
            var mi = Make("Face", mega, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(190f, 190f));
            megaImg = Img(mi, new Color(0.08f, 0.1f, 0.16f, 0.95f), dot);
            tMega = Txt(mi, "MEGA\nBAT", 40, Pal.Gold, TextAlignmentOptions.Center);
            Btn(mega, megaBg, () => G.match.HumanMega());

            // pitching
            pitCtl = Fill("Pitching", matchUi);
            var joyRt = Make("Joystick", pitCtl, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0.5f, 0.5f), new Vector2(210f, 250f), new Vector2(320f, 320f));
            Img(joyRt, new Color(0.08f, 0.11f, 0.18f, 0.7f), dot, true);
            var ring = Make("Ring", joyRt, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            Img(ring, new Color(1f, 1f, 1f, 0.3f), ringSp);
            var knob = Make("Knob", joyRt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(140f, 140f));
            Img(knob, new Color(1f, 1f, 1f, 0.92f), dot);
            Txt(knob, "조준", 30, Pal.Ink, TextAlignmentOptions.Center);
            joystick = joyRt.gameObject.AddComponent<Joystick>();
            joystick.knob = knob;
            joystick.radius = 120f;
            Vector2[] slots = { new Vector2(-300f, 140f), new Vector2(-150f, 300f), new Vector2(-110f, 110f), new Vector2(-300f, 380f) };
            for (int i = 0; i < 4; i++)
            {
                int slot = i;
                var b = Make("Pitch" + i, pitCtl, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0.5f), slots[i] + new Vector2(10f, 30f), new Vector2(170f, 170f));
                pitchImg[i] = Img(b, Pal.Blue, dot, true);
                pitchTxt[i] = Txt(b, "", 30, Color.white, TextAlignmentOptions.Center, 0.2f, new Color(0f, 0f, 0f, 0.6f));
                pitchTxt[i].textWrappingMode = TextWrappingModes.Normal;
                var hb = b.gameObject.AddComponent<HoldButton>();
                hb.onDown = () => G.match.HumanPitchDown(slot);
                hb.onUp = () => G.match.HumanPitchUp();
                pitchBtn[i] = hb;
                if (i == 3)
                {
                    var mfr = Make("Gauge", b, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(16f, 16f));
                    magicFill = Img(mfr, Pal.Gold, ringSp);
                    magicFill.type = Image.Type.Filled;
                    magicFill.fillMethod = Image.FillMethod.Radial360;
                    magicFill.fillOrigin = 2;
                }
            }

            // base diamond (throw and run)
            diamond = Make("Diamond", matchUi, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 90f), new Vector2(620f, 470f));
            Img(diamond, new Color(0.05f, 0.07f, 0.11f, 0.78f), round, false, 0.5f);
            tDiamondTitle = Txt(Make("Title", diamond, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -12f), new Vector2(-30f, 50f)), "", 34, Color.white, TextAlignmentOptions.Center);
            Vector2[] bpos = { new Vector2(0f, -160f), new Vector2(190f, -10f), new Vector2(0f, 140f), new Vector2(-190f, -10f) };
            string[] bname = { "홈", "1루", "2루", "3루" };
            for (int i = 0; i < 4; i++)
            {
                int b = i == 0 ? 4 : i;
                var r = Make("Base" + b, diamond, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), bpos[i] + new Vector2(0f, -20f), new Vector2(150f, 150f));
                baseImg[i] = Img(r, new Color(0.9f, 0.92f, 0.95f, 0.95f), round, true, 0.7f);
                baseTxt[i] = Txt(r, bname[i], 36, Pal.Ink, TextAlignmentOptions.Center);
                baseTxt[i].textWrappingMode = TextWrappingModes.Normal;
                baseBtn[i] = Btn(r, baseImg[i], () => BaseTapped(b));
                var back = Make("Back" + b, diamond, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), bpos[i] + new Vector2(i == 1 ? -105f : i == 3 ? 105f : -100f, i == 2 ? -70f : -80f), new Vector2(84f, 84f));
                var bi = Img(back, Pal.Red, dot, true);
                Txt(back, "◀", 40, Color.white, TextAlignmentOptions.Center);
                backBtn[i] = Btn(back, bi, () => G.match.play?.HumanBack(b));
            }

            hintRt = Make("Hint", matchUi, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 470f), new Vector2(900f, 60f));
            tHint = Txt(hintRt, "", 32, Color.white, TextAlignmentOptions.Center, 0.25f, Pal.Ink);

            var skip = Make("Skip", matchUi, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-24f, -150f), new Vector2(210f, 80f));
            var si = Img(skip, new Color(0.05f, 0.07f, 0.11f, 0.8f), round, true, 0.5f);
            Txt(skip, "SKIP ▶▶", 32, Color.white, TextAlignmentOptions.Center);
            skipBtn = Btn(skip, si, () => G.match.Skip());
        }

        void BaseTapped(int b)
        {
            var M = G.match;
            if (M.play == null) return;
            if (M.ShowsThrowPrompt) M.play.HumanThrow(b);
            else if (M.ShowsRunControls) M.play.HumanAdvance(b);
        }

        void BuildOverlays()
        {
            for (int i = 0; i < 4; i++)
            {
                var rt = Make("Pop" + i, matchUi, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 260f), new Vector2(900f, 300f));
                var bt = Make("Burst", rt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(560f, 420f));
                var bi = Img(bt, Color.white, burstSp);
                var tx = Txt(rt, "", 110, Color.white, TextAlignmentOptions.Center, 0.32f, Pal.Ink);
                tx.textWrappingMode = TextWrappingModes.NoWrap;
                tx.fontStyle = FontStyles.Bold | FontStyles.Italic;
                // pitch name and speed under a call (STRIKE / BALL), on a dark pill
                var sr = Make("Sub", rt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -118f), new Vector2(430f, 64f));
                var sbg = Img(sr, new Color(0.05f, 0.06f, 0.1f, 0.82f), round, false, 0.5f);
                var st = Txt(sr, "", 38, Color.white, TextAlignmentOptions.Center);
                rt.gameObject.SetActive(false);
                pops.Add(new Pop { rt = rt, burst = bi, txt = tx, subRt = sr, subBg = sbg, sub = st });
            }
            bannerRt = Make("Banner", matchUi, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 420f), new Vector2(0f, 240f));
            Img(bannerRt, new Color(0.04f, 0.06f, 0.1f, 0.88f), round, false, 0.4f);
            tBanner = Txt(Make("Title", bannerRt, new Vector2(0f, 0.38f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-40f, 0f)), "", 84, Pal.Gold, TextAlignmentOptions.Center, 0.25f, Pal.Ink);
            tBanner.enableAutoSizing = true; tBanner.fontSizeMin = 40; tBanner.fontSizeMax = 84;
            tBannerSub = Txt(Make("Sub", bannerRt, new Vector2(0f, 0f), new Vector2(1f, 0.4f), new Vector2(0.5f, 0.5f), new Vector2(0f, 6f), new Vector2(-40f, 0f)), "", 38, Color.white, TextAlignmentOptions.Center);
            tBannerSub.enableAutoSizing = true; tBannerSub.fontSizeMin = 22; tBannerSub.fontSizeMax = 38;
            bannerGroup = bannerRt.gameObject.AddComponent<CanvasGroup>();
            bannerGroup.blocksRaycasts = false;
            bannerRt.gameObject.SetActive(false);

            toastRt = Make("Toast", matchUi, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 640f), new Vector2(960f, 110f));
            Img(toastRt, new Color(0.05f, 0.07f, 0.11f, 0.9f), round, false, 0.5f);
            tToast = Txt(toastRt, "", 32, Color.white, TextAlignmentOptions.Center);
            tToast.textWrappingMode = TextWrappingModes.Normal;
            toastRt.gameObject.SetActive(false);
        }

        void BuildLobby()
        {
            lobby = Fill("Lobby", safe);
            var shade = Make("Shade", lobby, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            Img(shade, new Color(0.02f, 0.03f, 0.06f, 0.35f), roundS);
            var title = Make("Title", lobby, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -110f), new Vector2(0f, 300f));
            var t1 = Txt(Make("W", title, new Vector2(0f, 0.5f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero), "WACKY", 170, Pal.Gold, TextAlignmentOptions.Center, 0.3f, Pal.Ink);
            t1.fontStyle = FontStyles.Bold | FontStyles.Italic;
            var t2 = Txt(Make("B", title, new Vector2(0f, 0f), new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 20f), Vector2.zero), "BASEBALL", 120, Color.white, TextAlignmentOptions.Center, 0.3f, Pal.Ink);
            t2.fontStyle = FontStyles.Bold | FontStyles.Italic;
            Txt(Make("Tag", lobby, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -420f), new Vector2(0f, 60f)), "9회 한 판 승부 · 마구와 괴짜들의 야구", 38, Color.white, TextAlignmentOptions.Center, 0.2f, Pal.Ink);

            var card = Make("Match", lobby, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 520f), new Vector2(960f, 330f));
            Img(card, new Color(0.05f, 0.07f, 0.11f, 0.9f), round, false, 0.5f);
            var mine = Make("Mine", card, new Vector2(0f, 0f), new Vector2(0.45f, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-20f, -20f));
            Txt(Make("Name", mine, new Vector2(0f, 0.6f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero), "BANANA\nBOMBERS", 46, Pal.Gold, TextAlignmentOptions.Center).textWrappingMode = TextWrappingModes.Normal;
            tLobbyFans = Txt(Make("Fans", mine, new Vector2(0f, 0.3f), new Vector2(1f, 0.6f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero), "", 32, Color.white, TextAlignmentOptions.Center);
            tLobbyRec = Txt(Make("Rec", mine, new Vector2(0f, 0f), new Vector2(1f, 0.3f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero), "", 28, new Color(1f, 1f, 1f, 0.75f), TextAlignmentOptions.Center);
            Txt(Make("VS", card, new Vector2(0.45f, 0f), new Vector2(0.55f, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero), "VS", 60, Pal.Red, TextAlignmentOptions.Center, 0.2f, Pal.Ink);
            var riv = Make("Rival", card, new Vector2(0.55f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-20f, -20f));
            tLobbyRival = Txt(Make("Name", riv, new Vector2(0f, 0.5f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero), "", 46, Color.white, TextAlignmentOptions.Center);
            tLobbyRival.textWrappingMode = TextWrappingModes.Normal;
            tLobbyRivalSub = Txt(Make("Sub", riv, new Vector2(0f, 0.22f), new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero), "", 28, new Color(1f, 1f, 1f, 0.75f), TextAlignmentOptions.Center);
            var shuffle = Make("Shuffle", riv, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 6f), new Vector2(260f, 64f));
            var shi = Img(shuffle, new Color(1f, 1f, 1f, 0.15f), round, true, 0.5f);
            Txt(shuffle, "상대 바꾸기", 28, Color.white, TextAlignmentOptions.Center);
            Btn(shuffle, shi, () => { rivalPick = (rivalPick + 1) % Roster.RivalCount; PickRival(); });

            var play = Make("Play", lobby, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 290f), new Vector2(760f, 190f));
            var pi = Img(play, Pal.Gold, round, true, 0.4f);
            Txt(play, "PLAY BALL!", 84, Pal.Ink, TextAlignmentOptions.Center);
            Btn(play, pi, () => { G.match.StartMatch(nextRival); });
            var lu = Make("Lineup", lobby, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 150f), new Vector2(460f, 100f));
            var li = Img(lu, new Color(0.05f, 0.07f, 0.11f, 0.85f), round, true, 0.5f);
            Txt(lu, "타순 편집", 40, Color.white, TextAlignmentOptions.Center);
            Btn(lu, li, OpenLineup);

            lineupPanel = Make("LineupPanel", safe, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(980f, 1260f));
            Img(lineupPanel, new Color(0.04f, 0.06f, 0.1f, 0.97f), round, true, 0.5f);
            Txt(Make("T", lineupPanel, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(0f, 70f)), "타순 편집 · 두 선수를 차례로 눌러 바꾸기", 36, Pal.Gold, TextAlignmentOptions.Center);
            for (int i = 0; i < 8; i++)
            {
                int idx = i;
                var row = Make("Row" + i, lineupPanel, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -130f - i * 128f), new Vector2(-60f, 112f));
                var bg = Img(row, new Color(1f, 1f, 1f, 0.08f), round, true, 0.6f);
                lineupBg.Add(bg);
                var tx = Txt(row, "", 34, Color.white, TextAlignmentOptions.MidlineLeft);
                tx.margin = new Vector4(30f, 0f, 30f, 0f);
                tx.richText = true;
                lineupRows.Add(tx);
                Btn(row, bg, () => LineupTap(idx));
            }
            var done = Make("Done", lineupPanel, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 30f), new Vector2(420f, 100f));
            var di = Img(done, Pal.Gold, round, true, 0.5f);
            Txt(done, "완료", 44, Pal.Ink, TextAlignmentOptions.Center);
            Btn(done, di, () => { lineupPanel.gameObject.SetActive(false); });
            lineupPanel.gameObject.SetActive(false);
        }

        void BuildResult()
        {
            result = Fill("Result", safe);
            var panel = Make("Panel", result, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 60f), new Vector2(980f, 1040f));
            Img(panel, new Color(0.04f, 0.06f, 0.1f, 0.94f), round, false, 0.5f);
            tResTitle = Txt(Make("Title", panel, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(0f, 160f)), "", 130, Pal.Gold, TextAlignmentOptions.Center, 0.3f, Pal.Ink);
            tResTitle.fontStyle = FontStyles.Bold | FontStyles.Italic;
            tResLine = Txt(Make("Line", panel, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -210f), new Vector2(-60f, 300f)), "", 40, Color.white, TextAlignmentOptions.Center);
            tResLine.textWrappingMode = TextWrappingModes.Normal;
            tResLine.richText = true;
            tResSub = Txt(Make("Sub", panel, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -540f), new Vector2(-60f, 160f)), "", 36, Color.white, TextAlignmentOptions.Center);
            tResSub.textWrappingMode = TextWrappingModes.Normal;
            tResFans = Txt(Make("Fans", panel, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -720f), new Vector2(-60f, 80f)), "", 46, Pal.Gold, TextAlignmentOptions.Center);
            var again = Make("Again", panel, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-210f, 40f), new Vector2(390f, 130f));
            var ai = Img(again, Pal.Gold, round, true, 0.5f);
            Txt(again, "재대결", 50, Pal.Ink, TextAlignmentOptions.Center);
            Btn(again, ai, () => { G.match.StartMatch(G.match.T[1]); });
            var lob = Make("Lobby", panel, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(210f, 40f), new Vector2(390f, 130f));
            var lbi = Img(lob, new Color(1f, 1f, 1f, 0.15f), round, true, 0.5f);
            Txt(lob, "로비로", 50, Color.white, TextAlignmentOptions.Center);
            Btn(lob, lbi, () => { G.match.LobbyScene(); ShowLobby(); });
            result.gameObject.SetActive(false);
        }

        // ------------------------------------------------------------------ screens

        public void ShowLobby()
        {
            On(lobby, true);
            On(result, false);
            On(matchUi, false);
            On(lineupPanel, false);
            rivalPick = Save.Matches % Roster.RivalCount;
            PickRival();
            int fans = Save.Fans;
            Set(tLobbyFans, "팬 " + fans + "명 · Lv." + Save.FanLevel(fans));
            Save.Record(out int w, out int l, out int d);
            Set(tLobbyRec, w + "승 " + l + "패 " + d + "무");
        }

        void PickRival()
        {
            nextRival = Roster.Rival(rivalPick, Save.Matches + rivalPick * 3);
            Set(tLobbyRival, nextRival.name.ToUpperInvariant().Replace(" ", "\n"));
            tLobbyRival.color = Color.Lerp(nextRival.main, Color.white, 0.35f);
            Set(tLobbyRivalSub, nextRival.nameKr + " · 투수 " + nextRival.pitcher.Short);
        }

        void OpenLineup()
        {
            lineupOrder = Save.BattingOrder(Cfg.LineupSize);
            lineupPick = -1;
            RefreshLineup();
            On(lineupPanel, true);
        }

        void RefreshLineup()
        {
            var src = G.match.DefaultLineup;
            for (int i = 0; i < 8; i++)
            {
                var p = src[lineupOrder[i]];
                string pos = PosName(p.pos);
                Set(lineupRows[i], "<color=#FFC83A>" + (i + 1) + "번</color>   #" + p.number + "  " + p.name + "   <size=26><color=#B8C2D6>" + pos + " · 콘" + p.con + " 파" + p.pow + " 주" + p.spd + "</color></size>");
                lineupBg[i].color = i == lineupPick ? new Color(1f, 0.78f, 0.23f, 0.35f) : new Color(1f, 1f, 1f, 0.08f);
            }
        }

        void LineupTap(int i)
        {
            if (lineupPick < 0) { lineupPick = i; RefreshLineup(); return; }
            if (lineupPick != i)
            {
                int tmp = lineupOrder[i];
                lineupOrder[i] = lineupOrder[lineupPick];
                lineupOrder[lineupPick] = tmp;
                Save.SetBattingOrder(lineupOrder);
            }
            lineupPick = -1;
            RefreshLineup();
        }

        public void ShowMatch()
        {
            On(lobby, false);
            On(result, false);
            On(lineupPanel, false);
            On(matchUi, true);
            cardT = 0f;
            var M = G.match;
            Set(tRival, "VS " + M.T[1].name.ToUpperInvariant());
            Set(tRivalSub, M.T[1].nameKr + " · AI 라이벌");
        }

        public void ShowResult(Match M)
        {
            On(result, true);
            On(matchUi, false);
            bool draw = M.runs[0] == M.runs[1], win = M.runs[0] > M.runs[1];
            Set(tResTitle, M.resultTitle);
            tResTitle.color = draw ? Color.white : win ? Pal.Gold : Pal.Red;
            int away = M.visitor, home = 1 - M.visitor;
            int n = Mathf.Max(M.line[away].Count, M.line[home].Count);
            var sb = new System.Text.StringBuilder();
            sb.Append("<mspace=1.15em>      ");
            for (int i = 0; i < n; i++) sb.Append(" ").Append((Cfg.StartInning + i).ToString().PadLeft(2));
            sb.Append("   R  H  E</mspace>\n");
            for (int r = 0; r < 2; r++)
            {
                int t = r == 0 ? away : home;
                sb.Append("<mspace=1.15em>").Append(M.T[t].abbr.PadRight(5)).Append(" ");
                for (int i = 0; i < n; i++)
                    sb.Append(" ").Append(i < M.line[t].Count ? M.line[t][i].ToString().PadLeft(2) : " X");
                sb.Append("  ").Append(M.runs[t].ToString().PadLeft(2)).Append(" ").Append(M.hits[t].ToString().PadLeft(2)).Append(" ").Append(M.errors[t].ToString().PadLeft(2)).Append("</mspace>\n");
            }
            Set(tResLine, sb.ToString());
            string mvp = M.mvp != null ? "MVP  #" + M.mvp.number + " " + M.mvp.name : "MVP  —";
            Set(tResSub, mvp + "\n홈런 " + M.homers[0] + " · 삼진 " + M.ks[1] + " (뺏은 삼진)");
            Set(tResFans, "+" + M.fansEarned + " 팬");
        }

        // ------------------------------------------------------------------ calls from the match

        public void Banner(string title, string sub, Color c, float secs)
        {
            Set(tBanner, title);
            tBanner.color = c;
            Set(tBannerSub, sub);
            bannerT = 0f;
            bannerDur = secs;
            On(bannerRt, true);
        }

        /// <summary>burst: comic starburst behind white text; without it the text itself takes the colour (umpire calls, as in
        /// Baseball Clash). sub: a small line under it, e.g. the pitch and its speed.</summary>
        public void Popup(string s, Color c, float size = 1f, bool burst = true, string sub = null)
        {
            Pop p = null;
            foreach (var x in pops) if (!x.on) { p = x; break; }
            if (p == null)
            {
                p = pops[0];
                foreach (var x in pops) if (x.t > p.t) p = x;
            }
            // shift older ones up
            foreach (var x in pops) if (x.on && x != p) x.rt.anchoredPosition += new Vector2(0f, 120f);
            p.on = true;
            p.t = 0f;
            p.life = 1.15f + 0.1f * s.Length / 6f;
            p.size = size;
            p.rot = Random.Range(-7f, 7f);
            Set(p.txt, s);
            p.txt.color = burst ? Color.white : c;
            p.txt.fontSize = (burst ? 104f : 118f) * size;
            p.burst.color = c;
            p.burst.enabled = burst;
            bool hasSub = !string.IsNullOrEmpty(sub);
            On(p.subRt, hasSub);
            if (hasSub) Set(p.sub, sub);
            p.rt.anchoredPosition = new Vector2(0f, 260f);
            On(p.rt, true);
        }

        public void Toast(string s, float secs = 3f)
        {
            Set(tToast, s);
            toastT = secs;
            On(toastRt, true);
        }

        public void PitchInfo(string s, Color c)
        {
            Set(tPitchInfo, s);
            tPitchInfo.color = Color.Lerp(c, Color.white, 0.35f);
            pitchInfoT = 2.2f;
        }

        public void Cards(PlayerData batter, PlayerData pitcher, TeamData bt, TeamData pt)
        {
            cardT = 2.6f;
            Set(cbNum, batter.number.ToString());
            Set(cbName, batter.name);
            Set(cbPos, PosName(batter.pos) + " · " + (batter.bats == Hand.Left ? "좌타" : "우타"));
            Set(cbTrait, "괴짜 포인트: " + batter.trait);
            cbStripe.color = bt.main;
            cbNum.color = Color.Lerp(bt.main, Color.white, 0.2f);
            float[] bs = { batter.Con, batter.Pow, batter.Spd, batter.Eye };
            for (int i = 0; i < 4; i++) { cbBars[i].rectTransform.anchorMax = new Vector2(Mathf.Clamp(0.08f + 0.92f * bs[i], 0.05f, 1f), 1f); cbBars[i].color = Color.Lerp(bt.main, Color.white, 0.15f); }
            Set(cpNum, pitcher.number.ToString());
            Set(cpName, pitcher.name);
            var sb = new System.Text.StringBuilder();
            foreach (var p in pitcher.pitches) sb.Append(BananaBall.PitchInfo.Of(p).name).Append("  ");
            cpPitches.richText = true;
            Set(cpPitches, sb.ToString());
            cpStripe.color = pt.main;
            cpNum.color = Color.Lerp(pt.main, Color.white, 0.2f);
            float[] ps = { pitcher.Vel, pitcher.Ctl, pitcher.Mov, pitcher.Sta };
            for (int i = 0; i < 4; i++) { cpBars[i].rectTransform.anchorMax = new Vector2(Mathf.Clamp(0.08f + 0.92f * ps[i], 0.05f, 1f), 1f); cpBars[i].color = Color.Lerp(pt.main, Color.white, 0.15f); }
            Set(tNow, "NOW BATTING  #" + batter.number + " " + batter.name + "   <size=26><color=#B8C2D6>" + batter.trait + "</color></size>");
            tNow.richText = true;
        }

        public void OnLive(Play p) { }

        public static string PosName(Pos p)
        {
            switch (p)
            {
                case Pos.P: return "투수";
                case Pos.C: return "포수";
                case Pos.B1: return "1루수";
                case Pos.B2: return "2루수";
                case Pos.SS: return "유격수";
                case Pos.B3: return "3루수";
                case Pos.LF: return "좌익수";
                case Pos.CF: return "중견수";
                case Pos.RF: return "우익수";
                default: return "지명타자";
            }
        }

        // debug hooks for the test bot
        public void DebugPitch(int slot, bool down)
        {
            if (down) { debugPitch = true; G.match.HumanPitchDown(slot); }
            else { debugPitch = false; G.match.HumanPitchUp(); }
        }

        public void DebugJoy(Vector2 v) { G.match.joy = v; }

        // ------------------------------------------------------------------ tick

        public void Tick(float dt)
        {
            var sa = Screen.safeArea;
            if (sa != lastSafe && Screen.width > 0 && Screen.height > 0)
            {
                lastSafe = sa;
                safe.anchorMin = new Vector2(sa.xMin / Screen.width, sa.yMin / Screen.height);
                safe.anchorMax = new Vector2(sa.xMax / Screen.width, sa.yMax / Screen.height);
            }
            TickPops(dt);
            if (flashImg != null && (flashA > 0f || flashImg.enabled))
            {
                flashA = Mathf.Max(0f, flashA - Mathf.Min(BananaBallGame.RealDelta, 0.05f) * 4.5f);
                flashImg.enabled = flashA > 0.002f;
                flashImg.color = new Color(1f, 1f, 1f, flashA);
            }
            if (bannerRt.gameObject.activeSelf)
            {
                bannerT += dt;
                float a = Mathf.Clamp01(bannerT / 0.2f) * Mathf.Clamp01((bannerDur - bannerT) / 0.3f);
                bannerRt.localScale = new Vector3(1f, Mathf.Lerp(0.2f, 1f, MathX.EaseOut(Mathf.Clamp01(bannerT / 0.18f))), 1f);
                bannerGroup.alpha = a;
                if (bannerT >= bannerDur) On(bannerRt, false);
            }
            if (toastRt.gameObject.activeSelf) { toastT -= dt; if (toastT <= 0f) On(toastRt, false); }
            if (!matchUi.gameObject.activeSelf) return;

            var M = G.match;
            Scorebug(M);

            // cards
            cardT -= dt;
            bool showCards = cardT > 0f && (M.phase == Phase.AtBatIntro || M.phase == Phase.PitchReady || M.phase == Phase.Windup);
            On(cardBat, showCards);
            On(cardPit, showCards);
            if (showCards)
            {
                float k = MathX.EaseOut(Mathf.Clamp01((2.6f - cardT) / 0.25f));
                cardBat.anchoredPosition = new Vector2(Mathf.Lerp(-520f, 24f, k), -220f);
                cardPit.anchoredPosition = new Vector2(Mathf.Lerp(520f, -24f, k), -540f);
            }
            bool atBat = M.phase == Phase.PitchReady || M.phase == Phase.Windup || M.phase == Phase.PitchFlight || M.phase == Phase.PitchEnd;
            On(nowStrip, atBat && !showCards && !M.autoPlay);

            pitchInfoT -= dt;
            On(tPitchInfo, pitchInfoT > 0f && (M.phase == Phase.PitchFlight || M.phase == Phase.PitchEnd || M.phase == Phase.Live));

            // controls
            bool batting = M.HumanBats && (M.phase == Phase.AtBatIntro || M.phase == Phase.PitchReady || M.phase == Phase.Windup || M.phase == Phase.PitchFlight);
            bool pitching = M.HumanPitches && (M.phase == Phase.PitchReady || M.phase == Phase.Windup);
            On(batCtl, batting);
            On(pitCtl, pitching);
            if (batting) BattingCtl(M);
            if (pitching) PitchingCtl(M);
            else if (joystick != null) M.joy = Vector2.zero;

            bool throwing = M.ShowsThrowPrompt;
            bool running = M.ShowsRunControls;
            On(diamond, throwing || running);
            if (throwing || running) DiamondCtl(M, throwing);

            string hint = "";
            if (batting) hint = M.bunt ? "번트 자세 · 공에 배트를 대세요" : M.autoContact ? "타이밍 맞춰 화면을 탭! (자동 조준)" : "드래그로 조준 · 손을 떼면 스윙";
            else if (pitching) hint = M.phase == Phase.Windup ? "원이 가장 작아질 때 손을 떼세요!" : "조이스틱으로 코스 조준 → 구종을 눌러 와인드업";
            else if (throwing) hint = "";
            Set(tHint, hint);
            On(hintRt, hint.Length > 0);

            bool skippable = (M.phase == Phase.Live && M.play != null && (M.play.kind == Play.Kind.HomeRun || M.play.kind == Play.Kind.GroundRule)) || M.phase == Phase.Intro || M.phase == Phase.HalfIntro || M.phase == Phase.HalfEnd || M.phase == Phase.GameOver || M.phase == Phase.PlayEnd;
            On(skipBtn, skippable);
        }

        void Scorebug(Match M)
        {
            int away = M.visitor, home = 1 - M.visitor;
            Set(tAwayAbbr, M.T[away].abbr);
            Set(tHomeAbbr, M.T[home].abbr);
            Set(tAwayR, M.runs[away].ToString());
            Set(tHomeR, M.runs[home].ToString());
            awayStrip.color = M.T[away].main;
            homeStrip.color = M.T[home].main;
            bool live = M.phase != Phase.Lobby && M.phase != Phase.Intro && M.phase != Phase.Result && M.phase != Phase.GameOver;
            On(awayBat, live && M.Bat == away);
            On(homeBat, live && M.Bat == home);
            Set(tInning, M.HalfLabel);
            Set(tCount, M.balls + "-" + M.strikes);
            for (int i = 0; i < 3; i++) baseDots[i].color = M.bases[i + 1] != null ? Pal.Gold : new Color(1f, 1f, 1f, 0.22f);
            if (M.phase == Phase.Live && M.play != null)
                for (int i = 0; i < 3; i++)
                {
                    bool occ = M.play.StandingOn(i + 1) != null || M.play.HeadingTo(i + 1) != null;
                    baseDots[i].color = occ ? Pal.Gold : new Color(1f, 1f, 1f, 0.22f);
                }
            for (int i = 0; i < 2; i++) outDots[i].color = M.outs > i ? Pal.Red : new Color(1f, 1f, 1f, 0.22f);
        }

        void BattingCtl(Match M)
        {
            Set(tAuto, M.autoContact ? "자동 조준" : "수동 조준");
            autoImg.color = M.autoContact ? new Color(0.13f, 0.4f, 0.75f, 0.95f) : new Color(0.1f, 0.14f, 0.22f, 0.9f);
            buntImg.color = M.bunt ? new Color(0.9f, 0.5f, 0.15f, 0.95f) : new Color(0.1f, 0.14f, 0.22f, 0.9f);
            Set(tBunt, M.bunt ? "번트\nON" : "번트");
            float g = M.BattingGauge;
            megaFill.fillAmount = M.megaArmed ? 1f : g;
            bool ready = M.MegaReady;
            float pulse = ready ? 1f + 0.06f * Mathf.Sin(Time.time * 9f) : 1f;
            megaImg.rectTransform.localScale = Vector3.one * pulse;
            tMega.color = M.megaArmed ? Pal.Ink : ready ? Pal.Gold : new Color(1f, 1f, 1f, 0.45f);
            megaImg.color = M.megaArmed ? Pal.Gold : new Color(0.08f, 0.1f, 0.16f, 0.95f);
            Set(tMega, M.megaArmed ? "MEGA\nON!" : "MEGA\nBAT");
        }

        void PitchingCtl(Match M)
        {
            M.joy = joystick.value;
            for (int i = 0; i < 4; i++)
            {
                var pt = M.PitchSlot(i);
                bool has = pt != null;
                On(pitchBtn[i], has);
                if (!has) continue;
                var info = BananaBall.PitchInfo.Of(pt.Value);
                Set(pitchTxt[i], info.name);
                bool locked = info.magic && !M.MagicReady;
                bool active = M.holding && M.phase == Phase.Windup && M.pType == pt.Value;
                Color c = info.color;
                pitchImg[i].color = locked ? new Color(0.2f, 0.22f, 0.28f, 0.85f) : active ? Color.Lerp(c, Color.white, 0.35f) : c;
                pitchTxt[i].color = locked ? new Color(1f, 1f, 1f, 0.4f) : Color.white;
                pitchBtn[i].transform.localScale = Vector3.one * (active ? 1.12f : info.magic && !locked ? 1f + 0.05f * Mathf.Sin(Time.time * 8f) : 1f);
            }
            if (magicFill != null) magicFill.fillAmount = M.PitchingGauge;
        }

        void DiamondCtl(Match M, bool throwing)
        {
            var pl = M.play;
            Set(tDiamondTitle, throwing ? "어디로 던질까? (탭)" : "주자 진루: 베이스 탭 · ◀ 귀루");
            for (int i = 0; i < 4; i++)
            {
                int b = i == 0 ? 4 : i;
                string bn = i == 0 ? "홈" : i + "루";
                if (throwing)
                {
                    bool rec = pl.recommend == b;
                    float pulse = rec ? 1f + 0.07f * Mathf.Sin(Time.time * 10f) : 1f;
                    baseImg[i].rectTransform.localScale = Vector3.one * pulse;
                    baseImg[i].color = rec ? Pal.Gold : new Color(0.9f, 0.92f, 0.95f, 0.95f);
                    Set(baseTxt[i], rec ? bn + "\n<size=24>THROW!</size>" : bn);
                    baseTxt[i].richText = true;
                    On(backBtn[i], false);
                }
                else
                {
                    var heading = pl.HeadingTo(b);
                    var standing = b < 4 ? pl.StandingOn(b) : null;
                    baseImg[i].rectTransform.localScale = Vector3.one;
                    baseImg[i].color = heading != null ? new Color(0.45f, 0.8f, 1f, 0.95f) : standing != null ? Pal.Gold : new Color(0.9f, 0.92f, 0.95f, 0.95f);
                    string who = heading != null ? "\n<size=22>▲" + heading.p.Short + "</size>" : standing != null ? "\n<size=22>" + standing.p.Short + "</size>" : "";
                    baseTxt[i].richText = true;
                    Set(baseTxt[i], bn + who);
                    On(backBtn[i], heading != null && !heading.forced && !(heading.isBatter && heading.pos < 1f));
                }
            }
        }

        void TickPops(float dt)
        {
            foreach (var p in pops)
            {
                if (!p.on) continue;
                p.t += dt;
                float u = p.t;
                float s;
                if (u < 0.14f) s = Mathf.Lerp(0.3f, 1.18f, u / 0.14f);
                else if (u < 0.24f) s = Mathf.Lerp(1.18f, 1f, (u - 0.14f) / 0.1f);
                else s = 1f + 0.03f * Mathf.Sin(u * 12f);
                float fade = Mathf.Clamp01((p.life - u) / 0.25f);
                if (u > p.life - 0.25f) s *= 1f + (1f - fade) * 0.25f;
                p.rt.localScale = Vector3.one * s;
                p.rt.localRotation = Quaternion.Euler(0f, 0f, p.rot);
                p.burst.rectTransform.localRotation = Quaternion.Euler(0f, 0f, u * 40f);
                var c = p.burst.color; c.a = fade * 0.95f; p.burst.color = c;
                var tc = p.txt.color; tc.a = fade; p.txt.color = tc;
                if (p.subRt.gameObject.activeSelf)
                {
                    var sc = p.sub.color; sc.a = fade; p.sub.color = sc;
                    var bc = p.subBg.color; bc.a = 0.82f * fade; p.subBg.color = bc;
                    p.subRt.localRotation = Quaternion.Euler(0f, 0f, -p.rot);
                }
                if (u >= p.life) { p.on = false; On(p.rt, false); }
            }
        }
    }
}
