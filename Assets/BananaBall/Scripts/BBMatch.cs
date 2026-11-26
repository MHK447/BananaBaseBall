using System.Collections.Generic;
using UnityEngine;

namespace BananaBall
{
    public enum Phase { Lobby, Intro, HalfIntro, AtBatIntro, PitchReady, Windup, PitchFlight, PitchEnd, Live, PlayEnd, HalfEnd, GameOver, Result }

    /// <summary>Running totals for balance checks (AI-vs-AI runs log one line per match).</summary>
    public static class Stats
    {
        public static int matches, runs, hits, homers, ks, walks, innings, draws, pitches, swings, contacts, fouls, errors;
        public static float seconds;

        public static void Reset() { matches = runs = hits = homers = ks = walks = innings = draws = pitches = swings = contacts = fouls = errors = 0; seconds = 0f; }

        public static string Summary()
        {
            if (matches == 0) return "";
            float m = matches;
            return "평균 득점 " + (runs / m).ToString("F2") + " · 안타 " + (hits / m).ToString("F1") + " · 홈런 " + (homers / m).ToString("F2") + " · 삼진 " + (ks / m).ToString("F1") +
                   " · 볼넷 " + (walks / m).ToString("F1") + " · 이닝 " + (innings / m).ToString("F2") + " · 무승부 " + (draws / m * 100f).ToString("F0") + "% · 스윙당 컨택 " +
                   (swings > 0 ? (contacts / (float)swings * 100f).ToString("F0") : "-") + "% · 파울 " + (fouls / m).ToString("F1") + " · 투구 " + (pitches / m).ToString("F0") + " · " + (seconds / m).ToString("F0") + "초";
        }
    }
    public enum Call { None, Strike, Ball, Foul, HitByPitch }

    /// <summary>
    /// The match. Baseball Clash rules: one inning that starts in the 9th at 0-0, standard count (4 balls, 3 strikes, fouls are
    /// strikes until two), 3 outs a half, extra innings to the 12th, then a draw; the home team does not bat when it already leads.
    /// Team 0 is the player's club; the rival club is the AI. No time limit. Each club fields a catcher who does not bat.
    /// </summary>
    public class Match
    {
        public const int Human = 0;
        readonly BananaBallGame G;
        public readonly System.Random rng = new System.Random();
        public TeamData[] T = new TeamData[2];
        /// <summary>Per club: the 8 batters, then the pitcher [8] and the catcher [9].</summary>
        readonly Ballplayer[][] guys = { new Ballplayer[10], new Ballplayer[10] };
        readonly Dictionary<PlayerData, Ballplayer> guyOf = new Dictionary<PlayerData, Ballplayer>();
        Transform people;
        Mats mats;
        Field field;
        BallView ballView;

        public Phase phase = Phase.Lobby;
        public float phaseT;
        public int visitor;                 // team that bats first
        public int inning = Cfg.StartInning;
        public bool bottom;
        public int Bat => bottom ? 1 - visitor : visitor;
        public int Fld => 1 - Bat;
        public int HomeTeam => 1 - visitor;
        public int outs, balls, strikes;
        public readonly int[] runs = new int[2], hits = new int[2], errors = new int[2], homers = new int[2], ks = new int[2];
        public readonly List<int>[] line = { new List<int>(), new List<int>() };
        public readonly int[] order = new int[2];
        public readonly PlayerData[] bases = new PlayerData[4];
        public readonly float[] batGauge = new float[2], pitchGauge = new float[2];
        readonly int[] pitchCount = new int[2];
        public bool autoPlay;
        public bool HumanBats => !autoPlay && Bat == Human;
        public bool HumanPitches => !autoPlay && Fld == Human;

        // at-bat
        public PlayerData batterP, pitcherP, catcherP;
        public Ballplayer batterG, pitcherG, catcherG;
        public readonly List<Fielder> fielders = new List<Fielder>();
        public Play play;
        Transform bat, megaBat, deckBat;
        Vector3 batV, batSpin;
        bool batFlying;
        public float zoneLow, zoneHigh;

        // pitch
        public PitchPath path;
        public float pt;
        public PitchType pType;
        bool released, resolved, crossed;
        float releaseAt = -1f;
        public float holdT, ring;
        public bool holding;
        public Vector2 aim;                  // pitch target at the plate (x, y in metres)
        public Vector2 joy;                  // joystick from the HUD
        float aiThink, aiHold;
        Vector2 aiAim;
        PitchType aiType;
        BallState after;                     // the ball once it is past the plate
        bool afterLive;
        public Call call;
        float callT;
        public string lastPitchInfo = "", lastPitchSub = "";

        // batting
        public bool autoContact = true, bunt, megaArmed;
        public Vector2 contact;              // contact area centre at the plate (x, y)
        public bool swung;
        public float swingAt;
        bool pressArmed;
        Vector2 dragLast;
        Vector2 autoErr;
        bool aiSwing;
        float aiSwingAt;
        Vector2 aiContact;
        bool aiBunt;

        // play / results
        string playText;
        Color playColor;
        float playEndT;
        public string resultTitle = "";
        public int fansEarned;
        public PlayerData mvp;
        readonly Dictionary<PlayerData, int> totalBases = new Dictionary<PlayerData, int>();
        bool walkOff;
        float matchStart;
        public bool skipRequested;

        // world UI at the plate
        Transform zoneFrame, contactRing, contactDot, aimRing, accRing;
        readonly Transform[] ghosts = new Transform[2];

        public Match(BananaBallGame game) { G = game; }

        public Ballplayer GuyOf(PlayerData p) => p != null && guyOf.TryGetValue(p, out var g) ? g : null;

        // ------------------------------------------------------------------ build

        public void Build(Mats m, Field f, BallView bv, TeamData mine, TeamData rival)
        {
            mats = m;
            field = f;
            ballView = bv;
            people = new GameObject("People").transform;
            SetTeams(mine, rival);
            bat = Prop(Shapes.Bat(1f), "Bat");
            megaBat = Prop(Shapes.Bat(2.4f, "BB_MegaBat"), "MegaBat");
            deckBat = Prop(Shapes.Bat(1f), "DeckBat");
            megaBat.gameObject.SetActive(false);
            BuildPlateUI();
            LobbyScene();
        }

        Transform Prop(Mesh mesh, string n)
        {
            var go = new GameObject(n);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mats.gear;
            return go.transform;
        }

        public void SetTeams(TeamData mine, TeamData rival)
        {
            // the bats may still be in a player's hands: take them back first, or they are destroyed along with the old players
            foreach (var b in new[] { bat, megaBat, deckBat })
                if (b != null) { b.SetParent(null, true); b.gameObject.SetActive(false); }
            batFlying = false;
            foreach (var arr in guys)
                for (int i = 0; i < arr.Length; i++)
                    if (arr[i] != null) { Object.Destroy(arr[i].gameObject); arr[i] = null; }
            guyOf.Clear();
            T[0] = mine;
            T[1] = rival;
            for (int t = 0; t < 2; t++)
            {
                var all = new List<PlayerData>(T[t].lineup) { T[t].pitcher, T[t].catcher };
                Ballplayer.Prewarm(all, T[t]);
            }
            for (int t = 0; t < 2; t++)
            {
                for (int i = 0; i < 8; i++) guys[t][i] = Make(T[t].lineup[i], T[t]);
                guys[t][8] = Make(T[t].pitcher, T[t]);
                guys[t][9] = Make(T[t].catcher, T[t]);
            }
            HideAll();
        }

        Ballplayer Make(PlayerData p, TeamData t)
        {
            var g = Ballplayer.Create(p, t, people, mats, field.shadowMesh, field.ballMesh);
            if (p.body.beard == Beard.Handlebar || p.pos == Pos.P) g.bigKick = p.team == 0 ? 1f : 0.4f;
            guyOf[p] = g;
            return g;
        }

        void BuildPlateUI()
        {
            var root = new GameObject("PlateUI").transform;
            zoneFrame = new GameObject("Zone").transform;
            zoneFrame.SetParent(root, false);
            var zm = mats.Unlit(new Color(1f, 1f, 1f, 0.55f), null, false, 3020);
            for (int i = 0; i < 4; i++)
            {
                var e = new GameObject("Edge" + i);
                e.transform.SetParent(zoneFrame, false);
                e.AddComponent<MeshFilter>().sharedMesh = field.boardMesh;
                e.AddComponent<MeshRenderer>().sharedMaterial = zm;
            }
            contactRing = Board(root, "Contact", mats.Unlit(new Color(1f, 0.86f, 0.25f, 0.85f), mats.ring, false, 3022));
            contactDot = Board(root, "ContactDot", mats.Unlit(new Color(1f, 0.86f, 0.25f, 0.9f), mats.circle, false, 3023));
            aimRing = Board(root, "Aim", mats.Unlit(new Color(1f, 1f, 1f, 0.95f), mats.ring, true, 3024));
            accRing = Board(root, "Accuracy", mats.Unlit(new Color(1f, 0.35f, 0.3f, 0.95f), mats.ring, true, 3025));
            for (int i = 0; i < 2; i++)
            {
                var g = new GameObject("Ghost" + i);
                g.AddComponent<MeshFilter>().sharedMesh = field.ballMesh;
                g.AddComponent<MeshRenderer>().sharedMaterial = mats.Unlit(new Color(0.75f, 0.95f, 1f, 0.6f), null, false, 3040);
                ghosts[i] = g.transform;
                g.SetActive(false);
            }
            ShowPlateUI(false, false);
        }

        Transform Board(Transform parent, string n, Material m)
        {
            var go = new GameObject(n);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = field.boardMesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = m;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go.transform;
        }

        void ShowPlateUI(bool batting, bool pitching)
        {
            zoneFrame.gameObject.SetActive(batting || pitching);
            contactRing.gameObject.SetActive(batting);
            contactDot.gameObject.SetActive(batting && !bunt);
            aimRing.gameObject.SetActive(pitching);
            accRing.gameObject.SetActive(pitching && holding);
        }

        void LayoutZone()
        {
            float w = Geo.PlateHalf * 2f, h = zoneHigh - zoneLow, th = 0.012f;
            float cx = 0f, cy = (zoneLow + zoneHigh) * 0.5f;
            Vector3[] pos = { new Vector3(cx, zoneHigh, 0f), new Vector3(cx, zoneLow, 0f), new Vector3(-Geo.PlateHalf, cy, 0f), new Vector3(Geo.PlateHalf, cy, 0f) };
            Vector3[] sc = { new Vector3(w + th, th, 1f), new Vector3(w + th, th, 1f), new Vector3(th, h, 1f), new Vector3(th, h, 1f) };
            for (int i = 0; i < 4; i++)
            {
                var e = zoneFrame.GetChild(i);
                e.localPosition = pos[i];
                e.localScale = sc[i];
                e.localRotation = Quaternion.identity;
            }
        }

        void HideAll()
        {
            foreach (var arr in guys) foreach (var g in arr) if (g != null) g.Show(false);
        }

        // ------------------------------------------------------------------ lobby and start

        public void LobbyScene()
        {
            phase = Phase.Lobby;
            phaseT = 0f;
            play = null;
            HideAll();
            ballView.Show(false);
            ShowPlateUI(false, false);
            field.landRing.gameObject.SetActive(false);
            bat.gameObject.SetActive(false);
            deckBat.gameObject.SetActive(false);
            // the player's club lines up on the infield grass for the menu
            for (int i = 0; i < 10; i++)
            {
                var g = guys[0][i];
                g.Show(true);
                g.Gear(false, true);
                Vector3 p = new Vector3(-6.4f + (i % 5) * 3.2f, 0f, 12.5f + (i / 5) * 3.6f + (i % 2) * 0.4f);
                g.Place(p, 180f + (p.x * -2.2f));
                g.Set(i % 3 == 0 ? Act.Wave : Act.Idle);
                g.looking = false;
            }
            G.cam.Set(CamRig.Mode.Lobby);
        }

        public void StartMatch(TeamData rival)
        {
            if (rival != null && rival != T[1]) SetTeams(T[0], rival);
            visitor = autoPlay ? rng.Next(2) : Save.NextVisitor();
            inning = Cfg.StartInning;
            bottom = false;
            outs = balls = strikes = 0;
            for (int t = 0; t < 2; t++)
            {
                runs[t] = hits[t] = errors[t] = homers[t] = ks[t] = 0;
                line[t].Clear();
                order[t] = 0;
                batGauge[t] = 0.25f;
                pitchGauge[t] = 0.35f;
                pitchCount[t] = 0;
            }
            for (int b = 0; b < 4; b++) bases[b] = null;
            totalBases.Clear();
            walkOff = false;
            megaArmed = false;
            bunt = false;
            ApplyOrder();
            phase = Phase.Intro;
            phaseT = 0f;
            matchStart = Time.time;
            HideAll();
            G.cam.Set(CamRig.Mode.Intro);
            G.hud.ShowMatch();
            G.hud.Banner(T[visitor].name.ToUpperInvariant() + "  VS  " + T[1 - visitor].name.ToUpperInvariant(), "9회부터 시작 · 0 : 0 · 연장 12회까지", Pal.Gold, Cfg.IntroTime - 0.4f);
            field.BoardMessage("PLAY BALL!", Pal.Gold, 3f);
            field.Excite(0.6f, 3f);
            Sfx.Play(Sfx.Id.Organ, 0.7f);
        }

        /// <summary>The saved batting order for the player's club.</summary>
        void ApplyOrder()
        {
            if (baseLineup == null) baseLineup = (PlayerData[])T[0].lineup.Clone();
            var ord = Save.BattingOrder(Cfg.LineupSize);
            for (int i = 0; i < 8; i++) T[0].lineup[i] = baseLineup[ord[i]];
        }

        PlayerData[] baseLineup;

        /// <summary>The player's club in its default (unsorted) order, for the line-up editor.</summary>
        public PlayerData[] DefaultLineup => baseLineup ?? (baseLineup = (PlayerData[])T[0].lineup.Clone());

        // ------------------------------------------------------------------ half innings

        void BeginHalf()
        {
            phase = Phase.HalfIntro;
            phaseT = 0f;
            outs = balls = strikes = 0;
            for (int b = 0; b < 4; b++) bases[b] = null;
            play = null;
            line[Bat].Add(0);
            HideAll();
            SetupFielders();
            string half = (inning) + "회" + (bottom ? "말" : "초");
            string who = T[Bat].name + " 공격";
            G.hud.Banner((bottom ? "▼ " : "▲ ") + half, who + (inning > Cfg.StartInning ? " · 연장전!" : ""), Bat == Human ? Pal.Gold : Pal.Hex(0x9AD0FF), Cfg.HalfBanner - 0.2f);
            G.cam.Set(CamRig.Mode.Establish);
            Sfx.Play(Sfx.Id.Whistle, 0.5f);
        }

        void SetupFielders()
        {
            fielders.Clear();
            var tm = T[Fld];
            Pos[] spots = { Pos.P, Pos.C, Pos.B1, Pos.B2, Pos.SS, Pos.B3, Pos.LF, Pos.CF, Pos.RF };
            foreach (var p in spots)
            {
                var pd = tm.AtPos(p);
                var g = GuyOf(pd);
                if (g == null) continue;
                g.Show(true);
                g.Gear(false, true);
                g.hasBall = false;
                g.bat = null;
                bool still = p == Pos.P || p == Pos.C;
                Vector3 at = Geo.Spot(p) + new Vector3(MathX.Range(rng, -1f, 1f), 0f, MathX.Range(rng, -1f, 1f)) * (still ? 0f : 1.2f);
                at.y = Geo.GroundY(at);
                g.Place(at, p == Pos.C ? 0f : MathX.YawTo(at, Vector3.zero));
                g.Set(p == Pos.P ? Act.PitchSet : p == Pos.C ? Act.Squat : Act.Ready);
                fielders.Add(new Fielder { p = pd, g = g, pos = p, at = at });
            }
            pitcherP = tm.pitcher;
            pitcherG = GuyOf(pitcherP);
            catcherP = tm.catcher;
            catcherG = GuyOf(catcherP);
            SetCatcherTarget(new Vector2(0f, 0.6f), true);
        }

        void ResetFielders()
        {
            foreach (var f in fielders)
            {
                bool still = f.pos == Pos.P || f.pos == Pos.C;
                Vector3 at = Geo.Spot(f.pos) + (still ? Vector3.zero : new Vector3(MathX.Range(rng, -1f, 1f), 0f, MathX.Range(rng, -1f, 1f)));
                at.y = Geo.GroundY(at);
                f.at = at;
                f.st = FState.Ready;
                f.g.Show(true);
                f.g.Place(at, f.pos == Pos.C ? 0f : MathX.YawTo(at, Vector3.zero));
                f.g.Set(f.pos == Pos.P ? Act.PitchSet : f.pos == Pos.C ? Act.Squat : Act.Ready);
                f.g.hasBall = f.pos == Pos.P;
                f.g.gloveW = 0f;
                f.g.looking = false;
            }
            SetCatcherTarget(new Vector2(0f, (zoneLow + zoneHigh) * 0.5f), true);
        }

        void NextBatter(bool advanceOrder)
        {
            if (advanceOrder) order[Bat] = (order[Bat] + 1) % Cfg.LineupSize;
            phase = Phase.AtBatIntro;
            phaseT = 0f;
            balls = strikes = 0;
            megaArmed = false;
            bunt = false;
            aiBunt = false;
            play = null;
            ResetFielders();
            var tm = T[Bat];
            batterP = tm.lineup[order[Bat]];
            batterG = GuyOf(batterP);
            // everyone on the batting side but the batter, the runners and the on-deck hitter goes to the dugout
            for (int i = 0; i < 10; i++)
            {
                var g = guys[Bat][i];
                bool onBase = false;
                for (int b = 1; b <= 3; b++) if (bases[b] != null && GuyOf(bases[b]) == g) onBase = true;
                g.Show(onBase);
            }
            for (int b = 1; b <= 3; b++)
            {
                if (bases[b] == null) continue;
                var g = GuyOf(bases[b]);
                g.Gear(true, false);
                Vector3 p = Geo.Base(b) + (Geo.Base(b == 3 ? 0 : b + 1) - Geo.Base(b)).normalized * 0.6f + Vector3.right * 0.0f;
                g.Place(p, MathX.YawTo(p, Geo.Base(b == 3 ? 0 : b + 1)));
                g.Set(Act.Ready);
            }
            PlaceBatter();
            if (batterP.body.ape)
            {
                // the gorilla walks up pounding his chest; he picks the bat up when the pitcher is set
                var bt = batterG.bat;
                if (bt != null) { bt.gameObject.SetActive(false); batterG.bat = null; }
                batterG.Play(Act.ChestBeat);
            }
            var deck = tm.lineup[(order[Bat] + 1) % Cfg.LineupSize];
            var dg = GuyOf(deck);
            bool deckOnBase = false;
            for (int b = 1; b <= 3; b++) if (bases[b] == deck) deckOnBase = true;
            if (!deckOnBase && dg != batterG)
            {
                dg.Show(true);
                dg.Gear(true, false);
                Vector3 dp = Bat == 0 ? Geo.OnDeckL : Geo.OnDeckR;
                dg.Place(dp, MathX.YawTo(dp, Vector3.zero) + 25f);
                dg.Set(rng.NextDouble() < 0.25 ? Act.Dance : Act.Idle);
                deckBat.gameObject.SetActive(true);
                deckBat.SetParent(dg.J[Ballplayer.HAR], false);
                deckBat.localPosition = new Vector3(0f, -0.07f, 0.02f);
                deckBat.localRotation = Quaternion.Euler(-70f, 0f, 0f);
            }
            else deckBat.gameObject.SetActive(false);

            Geo.Zone(batterP.body.height, out zoneLow, out zoneHigh);
            LayoutZone();
            contact = new Vector2(0f, (zoneLow + zoneHigh) * 0.5f);
            aim = new Vector2(0f, (zoneLow + zoneHigh) * 0.5f);
            G.hud.Cards(batterP, pitcherP, T[Bat], T[Fld]);
            G.cam.Set(HumanPitches ? CamRig.Mode.Pitching : CamRig.Mode.Batting);
            G.cam.batterLeft = batterP.bats == Hand.Left;
            ShowPlateUI(false, false);
            AIArmMega();
        }

        void PlaceBatter()
        {
            bool left = batterP.bats == Hand.Left;
            Vector3 box = new Vector3(left ? 0.84f : -0.84f, 0f, 0.04f);
            batterG.Show(true);
            batterG.Gear(true, false);
            batterG.hasBall = false;
            batterG.Place(box, left ? -90f : 90f);
            batterG.swing = -1f;
            batterG.load = 0f;
            batterG.Play(bunt ? Act.Bunt : Act.Stance);
            batterG.looking = true;
            batterG.lookAt = Geo.Rubber + Vector3.up * 1.6f;
            Transform b = megaArmed ? megaBat : bat;
            (megaArmed ? bat : megaBat).gameObject.SetActive(false);
            b.gameObject.SetActive(true);
            b.SetParent(batterG.transform, false);
            batterG.bat = b;
            batterG.megaBat = megaArmed;
            batFlying = false;
            batterG.swingAim = new Vector3(0f, (zoneLow + zoneHigh) * 0.5f, 0f);
        }

        void ReadyPitch()
        {
            phase = Phase.PitchReady;
            phaseT = 0f;
            released = resolved = crossed = false;
            releaseAt = -1f;
            afterLive = false;
            swung = false;
            pressArmed = false;
            holding = false;
            call = Call.None;
            pt = -1f;
            camHold = 0f;
            ballView.Show(false);
            ghosts[0].gameObject.SetActive(false);
            ghosts[1].gameObject.SetActive(false);
            pitcherG.Show(true);
            pitcherG.Place(new Vector3(0f, Geo.MoundH, Geo.RubberZ + 0.25f), 180f);
            pitcherG.Play(Act.PitchSet);
            pitcherG.hasBall = true;
            pitcherG.looking = false;
            pitcherG.windStyle = 0;
            pitcherG.throwGag = 0;
            batterG.swing = -1f;
            if (batterG.Floored || batterG.InAcro || batterG.bat == null || batFlying) PlaceBatter();   // back in the box after the slapstick
            batterG.Play(bunt ? Act.Bunt : Act.Stance);
            foreach (var f in fielders) if (f.pos != Pos.P) f.g.Set(f.pos == Pos.C ? Act.Squat : Act.Ready);
            inMitt = caughtPitch = false;
            toss = 0;
            if (catcherG != null) { catcherG.hasBall = false; catcherG.looking = true; catcherG.lookAt = Geo.Rubber + Vector3.up * 1.5f; }
            aiThink = MathX.Range(rng, 0.7f, 1.5f);
            if (!HumanPitches) PlanAIPitch();
            ShowPlateUI(HumanBats, HumanPitches);
            G.cam.Set(HumanPitches ? CamRig.Mode.Pitching : CamRig.Mode.Batting);
        }

        // ------------------------------------------------------------------ human input

        public void HumanPitchDown(int slot)
        {
            if (!HumanPitches || phase != Phase.PitchReady) return;
            var pt0 = PitchSlot(slot);
            if (pt0 == null) return;
            pType = pt0.Value;
            if (PitchInfo.Of(pType).magic)
            {
                if (pitchGauge[Fld] < 1f) return;
                pitchGauge[Fld] = 0f;
                Popup(PitchInfo.Of(pType).name + "!", PitchInfo.Of(pType).color, 1.1f);
                Sfx.Play(Sfx.Id.Boing, 0.8f);
            }
            holding = true;
            holdT = 0f;
            StartWindup();
        }

        public void HumanPitchUp()
        {
            if (!holding || phase != Phase.Windup) return;
            holding = false;
            ReleaseDecided(ring, true);
        }

        public PitchType? PitchSlot(int slot)
        {
            if (pitcherP == null) return null;
            if (slot >= 0 && slot < pitcherP.pitches.Length) return pitcherP.pitches[slot];
            return null;   // no magic pitches (pause ball, balloon, phantom, missile, zigzag): real pitches only
        }

        public void ToggleAuto() { if (HumanBats) autoContact = !autoContact; }

        public void ToggleBunt()
        {
            if (!HumanBats || (phase != Phase.PitchReady && phase != Phase.Windup) || swung) return;
            bunt = !bunt;
            batterG.Play(bunt ? Act.Bunt : Act.Stance);
            Sfx.Play(Sfx.Id.Tap, 0.5f);
        }

        public void HumanMega()
        {
            if (!HumanBats || megaArmed || batGauge[Bat] < 1f) return;
            if (phase != Phase.PitchReady && phase != Phase.Windup && phase != Phase.AtBatIntro) return;
            ArmMega();
        }

        void ArmMega()
        {
            megaArmed = true;
            batGauge[Bat] = 0f;
            bat.gameObject.SetActive(false);
            megaBat.gameObject.SetActive(true);
            megaBat.SetParent(batterG.transform, false);
            batterG.bat = megaBat;
            batterG.megaBat = true;
            megaBat.localScale = Vector3.one * 0.2f;
            Popup("MEGA BAT!", Pal.Gold, 1.25f);
            Sfx.Play(Sfx.Id.Boing, 1f, 0.8f);
            field.Excite(0.7f, 2f);
            G.fx.Confetti(batterG.HeadTop, 16);
        }

        void AIArmMega()
        {
            if (HumanBats || batGauge[Bat] < 1f) return;
            bool risp = bases[2] != null || bases[3] != null;
            if (risp || rng.NextDouble() < 0.4) ArmMega();
        }

        public void Skip() { skipRequested = true; }

        // ------------------------------------------------------------------ AI pitching and batting

        void PlanAIPitch()
        {
            var p = pitcherP;
            double r = rng.NextDouble();
            aiType = r < 0.44 ? p.pitches[0] : r < 0.72 ? p.pitches[1] : p.pitches[2];
            float cx = 0f, cy = (zoneLow + zoneHigh) * 0.5f, hw = Geo.PlateHalf, hh = (zoneHigh - zoneLow) * 0.5f;
            int behind = balls - strikes;
            float inZone = behind >= 2 ? 0.9f : strikes == 2 && balls < 3 ? 0.35f : 0.62f;
            if (PitchInfo.Of(aiType).magic) inZone = 0.8f;
            if (rng.NextDouble() < inZone)
                aiAim = new Vector2(cx + MathX.Range(rng, -0.75f, 0.75f) * hw, cy + MathX.Range(rng, -0.75f, 0.75f) * hh);
            else
            {
                float side = rng.NextDouble() < 0.5 ? -1f : 1f;
                if (rng.NextDouble() < 0.6)
                    aiAim = new Vector2(side * (hw + MathX.Range(rng, 0.04f, 0.16f)), cy + MathX.Range(rng, -1f, 1f) * hh);
                else
                    aiAim = new Vector2(MathX.Range(rng, -1f, 1f) * hw, side > 0 ? zoneHigh + MathX.Range(rng, 0.05f, 0.2f) : zoneLow - MathX.Range(rng, 0.05f, 0.22f));
            }
            aiHold = MathX.Range(rng, 0.55f, 0.85f);
        }

        void StartWindup()
        {
            phase = Phase.Windup;
            phaseT = 0f;
            holdT = 0f;
            pitcherG.Play(Act.PitchWind);
            int style = PickWindup();
            pitcherG.WackyWindup(style);
            if (style == 1 || style == 2 || style == 4) { aiHold = Mathf.Max(aiHold, 0.78f); Sfx.Play(Sfx.Id.Boing, 0.5f, 1.3f); }
            Sfx.Play(Sfx.Id.Tick, 0.4f);
        }

        /// <summary>Out of nowhere the pitcher does something ridiculous on the mound (a gorilla beats his chest first).</summary>
        int PickWindup()
        {
            double r = rng.NextDouble();
            if (pitcherP.body.ape) return r < 0.25 ? 1 : 6;
            if (r < 0.48) return 0;
            if (r < 0.62) return 1;   // backflip
            if (r < 0.74) return 2;   // tornado
            if (r < 0.84) return 3;   // windmill arm
            if (r < 0.93) return 4;   // cartwheel
            return 5;                 // bunny hops
        }

        public static float RingAt(float t)
        {
            if (t <= Cfg.RingTime) return Mathf.Lerp(Cfg.RingStart, Cfg.RingMin, MathX.EaseOut(t / Cfg.RingTime));
            return Mathf.Lerp(Cfg.RingMin, Cfg.RingStart, Mathf.Clamp01((t - Cfg.RingTime) / (Cfg.RingOver - Cfg.RingTime)));
        }

        void ReleaseDecided(float ringSize, bool human)
        {
            var p = pitcherP;
            float fatigue = Mathf.Clamp01((pitchCount[Fld] - (18f + 30f * p.Sta)) / 30f);
            Vector2 err;
            Vector2 target;
            if (human)
            {
                target = aim;
                float k = ringSize * (1.1f - 0.45f * p.Ctl) * (1f + fatigue * 0.6f);
                err = new Vector2(MathX.Gauss(rng), MathX.Gauss(rng)) * k * 0.55f;
                bool perfect = holdT > Cfg.RingTime - 0.1f && holdT < Cfg.RingTime + 0.1f;
                if (perfect) { err *= 0.3f; Popup("PERFECT!", Pal.Gold, 0.9f); Sfx.Play(Sfx.Id.Ding, 0.6f); }
            }
            else
            {
                target = aiAim;
                float sd = (0.11f - 0.07f * p.Ctl) * (1f + fatigue * 0.5f);
                err = new Vector2(MathX.Gauss(rng), MathX.Gauss(rng)) * sd;
            }
            Vector2 t2 = target + err;
            pitchTarget = new Vector3(t2.x, Mathf.Clamp(t2.y, 0.15f, 2.2f), 0f);
            pitcherG.Play(Act.PitchThrow);
            // and sometimes the follow-through goes wrong: a face-plant into the dirt or a spin-out
            double g = rng.NextDouble();
            pitcherG.throwGag = g < 0.14 ? 1 : g < 0.3 ? 2 : 0;
            releaseAt = Ballplayer.PitchRelease;
            phaseT = 0f;
            accRing.gameObject.SetActive(false);
        }

        Vector3 pitchTarget;

        void ReleaseBall()
        {
            var p = pitcherP;
            float fatigue = Mathf.Clamp01((pitchCount[Fld] - (18f + 30f * p.Sta)) / 30f);
            Vector3 hand = pitcherG.HandWorld;
            Vector3 rel = PitchPath.Release(p);
            Vector3 start = new Vector3(Mathf.Lerp(rel.x, hand.x, 0.6f), Mathf.Clamp(Mathf.Lerp(rel.y, hand.y, 0.6f), 0.95f, 1.8f), Mathf.Clamp(hand.z, 15.8f, 18.2f));
            float tf = PitchPath.FlightTime(pType, p, fatigue);
            tf *= 1f - 0.04f * (p.Mov - 0.5f);
            path = PitchPath.Make(pType, start, pitchTarget, tf, p.throws == Hand.Right, MathX.Range(rng, 0f, 6f));
            pt = 0f;
            released = true;
            pitchCount[Fld]++;
            Stats.pitches++;
            pitcherG.hasBall = false;
            ballView.Show(true);
            ballView.Style(pType);
            ballView.Trail(true);
            phase = Phase.PitchFlight;
            phaseT = 0f;
            lastPitchInfo = PitchInfo.Speed(pType, p.vel) + " · " + PitchInfo.Of(pType).name;
            lastPitchSub = PitchInfo.Of(pType).name + "  " + PitchInfo.Speed(pType, p.vel);
            G.hud.PitchInfo(lastPitchInfo, PitchInfo.Of(pType).color);
            Sfx.Play(pType == PitchType.Missile ? Sfx.Id.Rocket : Sfx.Id.Whoosh, 0.7f, pType == PitchType.Balloon ? 0.6f : 1f);
            if (PitchInfo.Of(pType).magic && !HumanPitches) Popup(PitchInfo.Of(pType).name + "!", PitchInfo.Of(pType).color, 1f);
            if (pType == PitchType.Phantom) { ghosts[0].gameObject.SetActive(true); ghosts[1].gameObject.SetActive(true); }

            // where the ball will meet the catcher's mitt (the ball keeps its line a few tenths of a metre past the plate)
            {
                Vector3 pp = path.Eval(path.PlateTime), pv = path.Velocity(path.PlateTime);
                float zm = Geo.CatcherSpot.z + MittAhead;
                float tm = pv.z < -0.5f ? Mathf.Clamp((zm - pp.z) / pv.z, 0f, 0.2f) : 0f;
                catchPt = pp + pv * tm + Vector3.down * (0.5f * Cfg.Gravity * tm * tm);
            }

            // the batter's read
            Vector3 plate = path.Eval(path.PlateTime);
            autoErr = new Vector2(MathX.Gauss(rng), MathX.Gauss(rng)) * (0.05f - 0.03f * batterP.Eye);
            if (pType == PitchType.Zigzag) autoErr *= 2f;
            if (!HumanBats) PlanAISwing(plate);
        }

        void PlanAISwing(Vector3 plate)
        {
            var b = batterP;
            bool inZone = Geo.InZone(plate, zoneLow, zoneHigh);
            Vector2 seen = new Vector2(plate.x, plate.y) + new Vector2(MathX.Gauss(rng), MathX.Gauss(rng)) * (0.11f - 0.06f * b.Eye);
            if (pType == PitchType.Zigzag) seen += new Vector2(MathX.Gauss(rng) * 0.08f, 0f);
            if (pType == PitchType.Phantom && rng.NextDouble() < 0.45) seen += new Vector2((rng.NextDouble() < 0.5 ? -1f : 1f) * 0.5f, 0f);
            bool seenIn = Mathf.Abs(seen.x) < Geo.PlateHalf + 0.03f && seen.y > zoneLow - 0.03f && seen.y < zoneHigh + 0.03f;
            float pSwing;
            if (seenIn) pSwing = strikes == 2 ? 0.93f : balls == 3 && strikes < 2 ? 0.55f : 0.8f;
            else pSwing = (strikes == 2 ? 0.3f : 0.14f) * (1.2f - b.Eye * 0.6f);
            if (balls == 3 && strikes == 0) pSwing *= 0.35f;
            aiBunt = !megaArmed && b.Spd > 0.7f && bases[1] != null && bases[2] == null && outs < 2 && strikes < 2 && rng.NextDouble() < 0.18;
            aiSwing = rng.NextDouble() < pSwing;
            float sigma = 0.075f - 0.03f * b.Con;
            float mu = 0f;
            if (pType == PitchType.Change || pType == PitchType.Curve) mu = -0.025f;
            if (pType == PitchType.Balloon) mu = -0.12f;
            if (pType == PitchType.Missile) mu = 0.045f;
            if (pType == PitchType.Stop) mu = rng.NextDouble() < 0.3 + 0.3 * b.Eye ? 0f : -path.pause * 0.8f;
            float e = mu + MathX.Gauss(rng) * sigma;
            aiSwingAt = path.PlateTime + e - Cfg.SwingToContact;
            aiContact = seen + new Vector2(MathX.Gauss(rng), MathX.Gauss(rng)) * (0.04f - 0.015f * b.Con);
            if (aiBunt) { bunt = true; batterG.Play(Act.Bunt); aiContact = seen; aiSwing = false; }
        }

        void Swing()
        {
            if (swung || bunt) return;
            swung = true;
            swingAt = pt;
            Stats.swings++;
            batterG.swing = 0f;
            batterG.swingAim = new Vector3(contact.x, contact.y, 0f);
            batterG.lookAt = batterG.swingAim;
            Sfx.Play(Sfx.Id.Whoosh, 0.5f, megaArmed ? 0.7f : 1.2f);
        }

        // ------------------------------------------------------------------ the pitch in flight

        void TickPitch(float dt)
        {
            pt += dt;
            // the batter strides into the pitch: front foot up and down toward the pitcher, hands back, ready to fire
            if (batterG != null && !bunt && batterG.act == Act.Stance)
            {
                float strideT = Mathf.Max(0.24f, path.PlateTime * 0.58f);
                batterG.load = Mathf.Max(batterG.load, Mathf.Clamp01((pt + 0.05f) / strideT));
            }
            Vector3 bp = path.Eval(pt);
            if (!afterLive && !inMitt) ballView.Set(bp);
            if (pType == PitchType.Phantom)
            {
                float fade = 1f - Mathf.Clamp01((pt / path.tf - 0.72f) / 0.16f);
                for (int i = 0; i < 2; i++)
                {
                    ghosts[i].gameObject.SetActive(fade > 0.02f && !afterLive);
                    ghosts[i].position = path.Ghost(pt, i);
                    ghosts[i].localScale = ballView.t.localScale;
                }
            }
            if (path.Paused(pt) && !stopShown) { stopShown = true; Popup("!?", PitchInfo.Of(PitchType.Stop).color, 0.9f); Sfx.Play(Sfx.Id.Boing, 0.6f, 1.6f); }

            // the contact area
            Vector3 plate = path.Eval(path.PlateTime);
            if (HumanBats)
            {
                if (autoContact)
                {
                    if (pt > Cfg.AutoAimLag && !swung)
                    {
                        Vector2 want = new Vector2(plate.x, plate.y) + autoErr;
                        float k = pType == PitchType.Zigzag ? 3f : 7f;
                        contact = Vector2.Lerp(contact, want, 1f - Mathf.Exp(-k * dt));
                    }
                }
                else ManualDrag();
                if (autoContact ? Ptr.FieldDown : (Ptr.FieldUp && pressArmed)) { if (pt > 0.05f) Swing(); }
            }
            else if (!bunt)
            {
                contact = Vector2.Lerp(contact, aiContact, 1f - Mathf.Exp(-8f * dt));
                if (aiSwing && !swung && pt >= aiSwingAt) Swing();
            }

            if (batterG != null && swung) batterG.swing = Mathf.Clamp01((pt - swingAt) / Cfg.SwingTime);
            if (bunt) batterG.swingAim = new Vector3(contact.x, contact.y, 0f);

            // contact
            if (swung && !resolved && pt >= swingAt + Cfg.SwingToContact)
            {
                resolved = true;
                float tc = swingAt + Cfg.SwingToContact;
                float e = tc - path.PlateTime;
                Vector3 at = path.Eval(Mathf.Min(tc, path.PlateTime + 0.2f));
                var hit = HitModel.Swing(batterP, e, new Vector2(plate.x, plate.y), contact, at, HumanBats && autoContact, megaArmed, rng);
                if (hit.contact) { Contact(hit); return; }
                // whiff
                if (HumanBats || rng.NextDouble() < 0.5) Popup(Mathf.Abs(e) > 0.2f ? "WHIFF!!" : "WHIFF!", Pal.Hex(0x9AD0FF), 0.9f);
                if (Mathf.Abs(e) > 0.12f && rng.NextDouble() < 0.6)
                {
                    // the swing carries him round and round and into the dirt, or leaves him seeing stars
                    double w = rng.NextDouble();
                    float hand = batterP.bats == Hand.Left ? -1f : 1f;
                    if (w < 0.6) batterG.Acro(Ballplayer.AcroCorkscrew, 0.06f, hand, true);
                    else if (w < 0.8) { FlingBat(); batterG.Play(Act.Dizzy); }
                    else batterG.Ragdoll(new Vector3(0f, 2.5f, -1.5f), new Vector3(-6f, 0f, hand * 3f), 1.0f);
                    Sfx.Play(Sfx.Id.SlideWhistle, 0.7f);
                }
                call = Call.Strike;
            }

            // bunt: the bat waits over the plate
            if (bunt && !resolved && pt >= path.PlateTime - 0.02f)
            {
                resolved = true;
                Vector3 at = path.Eval(path.PlateTime);
                Vector2 c = HumanBats ? contact : aiContact;
                var hit = HitModel.Bunt(batterP, new Vector2(plate.x, plate.y), c, at, rng);
                if (hit.contact) { Contact(hit); return; }
                call = Geo.InZone(plate, zoneLow, zoneHigh) ? Call.Strike : Call.Ball;
            }

            // the ball crosses the plate
            if (!crossed && pt >= path.PlateTime)
            {
                crossed = true;
                if (call == Call.None)
                {
                    bool hbp = HitBatter(plate);
                    call = hbp ? Call.HitByPitch : Geo.InZone(plate, zoneLow, zoneHigh) ? Call.Strike : Call.Ball;
                    if (hbp)
                    {
                        // launched clean out of the box
                        Vector3 pv = path.Velocity(path.PlateTime);
                        float away = batterP.bats == Hand.Left ? 1f : -1f;
                        batterG.Ragdoll(new Vector3(away * 3.4f, 4.6f, pv.z * 0.05f), new Vector3(MathX.Range(rng, -5f, 5f), MathX.Range(rng, -8f, 8f), away * 8f), 1.3f, RagdollRig.SegHead, pv.normalized * 10f);
                        G.fx.Dizzy(batterG.J[Ballplayer.HEAD], batterG.HeadTop, 2.4f);
                        G.fx.Word(G.fx.lib != null ? G.fx.lib.textPow : null, batterG.HeadTop + Vector3.up * 0.4f, 0.4f);
                        Popup("BONK!", Pal.Red, 1.2f);
                        Sfx.Play(Sfx.Id.Bonk, 1f);
                    }
                }
                after = new BallState { p = path.Eval(path.PlateTime + 0.01f), v = path.Velocity(path.PlateTime + 0.01f) };
                if (call == Call.HitByPitch) after.v = new Vector3(after.v.x * -0.3f, 3f, after.v.z * -0.15f);
                afterLive = true;
                callT = 0f;
                ghosts[0].gameObject.SetActive(false);
                ghosts[1].gameObject.SetActive(false);
            }
            if (afterLive || inMitt)
            {
                if (afterLive)
                {
                    int n = Mathf.Max(1, Mathf.CeilToInt(dt / BallSim.Dt));
                    for (int i = 0; i < n; i++) BallSim.Step(ref after, dt / n);
                    // the catcher takes it when it reaches his mitt
                    if (CatcherOn && call != Call.HitByPitch && call != Call.Foul && catcherG.act == Act.Squat && after.p.z <= catcherG.GloveWorld.z + 0.03f) CatchPitch();
                    else ballView.Set(after.p);
                }
                callT += dt;
                if (callT > 0.45f) EndPitch();
            }
        }

        bool stopShown;
        bool lastHomer;      // the ball in play is a no-doubt home run (the batter admires it and flips his bat)
        float camHold;       // after contact the camera stays on the hitter for a beat before it follows the ball

        bool HitBatter(Vector3 plate)
        {
            bool left = batterP.bats == Hand.Left;
            float x = left ? -plate.x : plate.x;
            return x < -0.5f && plate.y > 0.2f && plate.y < batterP.body.height * 0.78f && !swung && !bunt;
        }

        void ManualDrag()
        {
            if (Ptr.FieldDown) { dragLast = Ptr.Pos; pressArmed = true; }
            if (Ptr.FieldHeld)
            {
                Vector2 d = Ptr.Pos - dragLast;
                dragLast = Ptr.Pos;
                float k = 1.15f / Mathf.Max(400f, Screen.height);
                contact += d * k;
                contact.x = Mathf.Clamp(contact.x, -0.55f, 0.55f);
                contact.y = Mathf.Clamp(contact.y, zoneLow - 0.35f, zoneHigh + 0.35f);
            }
        }

        void Contact(HitResult hit)
        {
            Stats.contacts++;
            if (hit.foulBack) Stats.fouls++;
            ballView.Trail(true);
            ghosts[0].gameObject.SetActive(false);
            ghosts[1].gameObject.SetActive(false);
            if (hit.foulBack)
            {
                after = new BallState { p = hit.pos, v = hit.vel };
                afterLive = true;
                crossed = true;
                callT = -0.6f;
                call = Call.Foul;
                Sfx.Play(Sfx.Id.Crack, 0.5f, 1.3f);
                return;
            }
            lastHomer = LeavesPark(hit);
            Impact(hit, lastHomer);
            if (hit.perfect)
            {
                Popup("KA-BOOM!", Pal.Hex(0xFF6A2B), 1.35f);
                G.fx.BigHit(hit.pos);
                ballView.Fire(true);
                field.Excite(1f, 3f);
            }
            else if (hit.quality < 0.22f && !hit.bunt && rng.NextDouble() < 0.5) BrokenBat(hit.pos);
            if (lastHomer) { ballView.Streak(); field.Excite(1f, 4f); }
            // the batter keeps the bat through his follow-through and lets it go as he sets off (TossBat)
            batterG.swing = Mathf.Max(batterG.swing, Cfg.SwingToContact / Cfg.SwingTime + 0.02f);
            batterG.lookAt = hit.pos;
            phase = Phase.Live;
            phaseT = 0f;
            ShowPlateUI(false, false);
            foreach (var f in fielders) f.at = f.g.pos;
            play = new Play(this, fielders, batterG, batterP, batterG.pos, hit, !autoPlay && Fld == Human, !autoPlay && Bat == Human, rng);
            // a no-doubter: he holds his finish and watches it go, flips the bat, then trots
            if (lastHomer) play.batter.delay = 0.75f;
            // the camera stays on the hitter for a beat (the ball rockets off the bat; on a homer, the bat flip), then picks up the ball
            camHold = hit.bunt ? 0.1f : lastHomer ? 0.95f : 0.3f;
            G.hud.OnLive(play);
        }

        /// <summary>The crack of the bat, felt: a freeze-frame, a white flash, the camera kicks in and shakes, a burst on the ball, a heavy
        /// thump under the crack and the crowd coming up out of its seats. All of it scales with how well the ball was struck; a
        /// no-doubt home run gets the lot plus a moment of slow motion.</summary>
        void Impact(HitResult hit, bool homer)
        {
            float q = Mathf.Clamp01(hit.quality);
            float ev = Mathf.Clamp01((hit.exit - 18f) / 20f);
            float k = hit.bunt ? 0.1f : homer || hit.perfect ? 1f : Mathf.Clamp01(0.2f + 0.45f * q + 0.35f * ev);
            float freeze = hit.bunt ? 0.02f : 0.035f + 0.075f * k + (homer ? 0.05f : 0f);
            G.HitStop(freeze, homer ? 0.28f : 1f, homer ? 0.9f : 0f);
            G.hud.Flash(hit.bunt ? 0.1f : 0.18f + 0.42f * k);
            G.cam.Punch(hit.bunt ? 1f : 2f + 7f * k, freeze);
            G.cam.Shake(hit.bunt ? 0.04f : 0.1f + 0.32f * k);
            G.fx.Impact(hit.pos, k, homer && !hit.perfect);
            Sfx.Play(Sfx.Id.Crack, 0.7f + 0.3f * k, hit.bunt ? 1.6f : Mathf.Lerp(1.1f, 0.92f, k));
            if (k > 0.4f) Sfx.Play(Sfx.Id.Smack, 0.35f + 0.65f * k, Mathf.Lerp(1.1f, 0.85f, k));
            if (k > 0.55f) { Sfx.Play(Sfx.Id.Cheer, 0.25f + 0.55f * k); field.Excite(0.5f + 0.5f * k, 1.5f + 2f * k); }
        }

        /// <summary>A no-doubt home run: the flight clears the fence so high nobody could rob it.</summary>
        static bool LeavesPark(HitResult hit)
        {
            if (hit.bunt || hit.foulBack) return false;
            var pr = BallSim.Predict(new BallState { p = hit.pos, v = hit.vel }, 9f, null);
            return pr.homeRun && pr.end.y > Geo.FenceH(Geo.AngleDeg(pr.end)) + 2.5f;
        }

        /// <summary>The batter lets the bat go as he sets off for first: tossed aside, or on a no-doubt homer flipped high end over end.</summary>
        void TossBat(bool flip)
        {
            Transform b = batterG.bat;
            if (b == null) return;
            batterG.bat = null;
            b.SetParent(null, true);
            float away = batterP.bats == Hand.Left ? 1f : -1f;   // away from the plate, to the batter's own side
            if (flip)
            {
                batV = new Vector3(away * MathX.Range(rng, 0.6f, 1.4f), MathX.Range(rng, 6.8f, 8.2f), MathX.Range(rng, -1.2f, -0.2f));
                Vector3 axis = Vector3.Cross(b.forward, Vector3.up);
                if (axis.sqrMagnitude < 1e-4f) axis = Vector3.right;
                batSpin = axis.normalized * MathX.Range(rng, 820f, 1080f) + Vector3.up * MathX.Range(rng, -120f, 120f);
                Sfx.Play(Sfx.Id.Whoosh, 0.5f, 0.8f);
            }
            else
            {
                batV = new Vector3(away * MathX.Range(rng, 1.2f, 2.6f), MathX.Range(rng, 1.6f, 3f), MathX.Range(rng, -2.4f, -0.8f));
                batSpin = new Vector3(MathX.Range(rng, -500f, 500f), MathX.Range(rng, -800f, 800f), MathX.Range(rng, -300f, 300f));
            }
            batFlying = true;
            flyingBat = b;
        }

        Transform flyingBat;

        void BrokenBat(Vector3 at)
        {
            Popup("CRACK!", Pal.Hex(0xD9A866), 1f);
            Sfx.Play(Sfx.Id.Crack, 1f, 0.7f);
            G.fx.Splinters(at);
        }

        void TickBat(float dt)
        {
            if (!batFlying || flyingBat == null) return;
            batV.y -= Cfg.Gravity * dt;
            flyingBat.position += batV * dt;
            flyingBat.Rotate(batSpin * dt, Space.World);
            if (flyingBat.position.y < 0.05f)
            {
                var p = flyingBat.position;
                p.y = 0.05f;
                flyingBat.position = p;
                if (Mathf.Abs(batV.y) < 1.5f) { batFlying = false; flyingBat.rotation = Quaternion.Euler(0f, flyingBat.eulerAngles.y, 90f); }
                batV.y = -batV.y * 0.3f;
                batV.x *= 0.6f; batV.z *= 0.6f;
                batSpin *= 0.5f;
            }
        }

        void EndPitch()
        {
            if (phase != Phase.PitchFlight) return;
            phase = Phase.PitchEnd;
            phaseT = 0f;
            ballView.Trail(false);
            ShowPlateUI(false, false);
            switch (call)
            {
                case Call.Strike:
                    strikes++;
                    pitchGauge[Fld] = Mathf.Min(1f, pitchGauge[Fld] + Cfg.GaugeStrike);
                    if (strikes >= Cfg.MaxStrikes)
                    {
                        Popup(swung ? "STRIKE OUT!" : "STRIKE 3!", Pal.Red, 1.25f, true, lastPitchSub);
                        ks[Bat]++;
                        pitchGauge[Fld] = Mathf.Min(1f, pitchGauge[Fld] + Cfg.GaugeK);
                        batGauge[Bat] = Mathf.Min(1f, batGauge[Bat] + Cfg.GaugeOut);
                        if (!batterG.Floored && !batterG.InAcro)
                        {
                            double r = rng.NextDouble();
                            if (!swung && r < 0.45) { batterG.Acro(Ballplayer.AcroPlank); Sfx.Play(Sfx.Id.SlideWhistle, 0.6f, 0.7f); }   // faints stiff as a board
                            else if (r < 0.75) { FlingBat(); batterG.Play(Act.Tantrum); }
                            else { FlingBat(); batterG.Play(Act.Sad); }
                        }
                        Sfx.Play(Sfx.Id.Aww, 0.7f);
                    }
                    else { Popup("STRIKE!", Pal.Gold, 0.95f, false, lastPitchSub); if (!caughtPitch) Sfx.Play(Sfx.Id.Pop, 0.8f); }
                    break;
                case Call.Ball:
                    balls++;
                    batGauge[Bat] = Mathf.Min(1f, batGauge[Bat] + Cfg.GaugeBall);
                    if (balls >= Cfg.MaxBalls) { Popup("WALK!", Pal.Green, 1.15f); Sfx.Play(Sfx.Id.Cheer, 0.4f); }
                    else Popup("BALL", Pal.Hex(0xB58CFF), 0.9f, false, lastPitchSub);
                    break;
                case Call.Foul:
                    if (bunt && strikes >= 2) { strikes = 3; Popup("FOUL BUNT\nSTRIKE OUT!", Pal.Red, 1.05f); ks[Bat]++; }
                    else { if (strikes < 2) strikes++; Popup("FOUL!", Pal.Hex(0x7FD0FF), 0.95f, false, lastPitchSub); }
                    batGauge[Bat] = Mathf.Min(1f, batGauge[Bat] + Cfg.GaugeFoul);
                    break;
                case Call.HitByPitch:
                    batGauge[Bat] = Mathf.Min(1f, batGauge[Bat] + Cfg.GaugeBall);
                    break;
            }
        }

        void AfterCall()
        {
            FinishToss();
            if (call == Call.Strike && strikes >= Cfg.MaxStrikes || call == Call.Foul && strikes >= Cfg.MaxStrikes)
            {
                AddOut();
                if (CheckHalfOver()) return;
                NextBatter(true);
                return;
            }
            if (call == Call.Ball && balls >= Cfg.MaxBalls || call == Call.HitByPitch)
            {
                Walk(batterP);
                if (CheckGameOverWalkOff()) return;
                NextBatter(true);
                return;
            }
            ReadyPitch();
        }

        void Walk(PlayerData p)
        {
            Stats.walks++;
            if (bases[1] != null)
            {
                if (bases[2] != null)
                {
                    if (bases[3] != null) ScoreRun(bases[3]);
                    bases[3] = bases[2];
                }
                bases[2] = bases[1];
            }
            bases[1] = p;
        }

        // ------------------------------------------------------------------ scoring hooks (used by Play)

        public int AddOut()
        {
            outs++;
            pitchGauge[Fld] = Mathf.Min(1f, pitchGauge[Fld] + Cfg.GaugeOutP * 0.5f);
            return outs;
        }

        public void AddRun(Runner r, Play p) => ScoreRun(r.p);

        void ScoreRun(PlayerData p)
        {
            runs[Bat]++;
            if (line[Bat].Count > 0) line[Bat][line[Bat].Count - 1]++;
            Popup("RUN!", Pal.Gold, 0.85f);
            Sfx.Play(Sfx.Id.Cheer, 0.6f);
            field.Excite(0.85f, 2.5f);
            field.BoardMessage("RUN SCORES!", Pal.Gold, 2f);
            if (bottom && inning >= Cfg.StartInning && runs[HomeTeam] > runs[visitor]) walkOff = true;
        }

        public void RemoveRun()
        {
            runs[Bat] = Mathf.Max(0, runs[Bat] - 1);
            if (line[Bat].Count > 0) line[Bat][line[Bat].Count - 1] = Mathf.Max(0, line[Bat][line[Bat].Count - 1] - 1);
            walkOff = bottom && runs[HomeTeam] > runs[visitor];
            Popup("NO RUN!", Pal.Red, 0.8f);
        }

        public void OnRunnerOut(Runner r, OutKind k, Play p)
        {
            Sfx.Play(Sfx.Id.Pop, 0.8f);
            if (outs >= 2) field.BoardMessage("OUT!", Pal.Red, 1.5f);
        }

        public void OnBallLanded(Play p) { G.fx.Landing(p.ball.p, p.ball.v.magnitude); }

        public void OnHomeRun(Play p)
        {
            homers[Bat]++;
            Popup("HOME RUN!!", Pal.Gold, 1.6f);
            Sfx.Play(Sfx.Id.Cheer, 1f);
            Sfx.Play(Sfx.Id.Fireworks, 0.8f);
            field.Excite(1f, 6f);
            field.BoardMessage("HOME RUN!!", Pal.Gold, 6f);
            G.fx.Fireworks(new Vector3(0f, 45f, 140f));
            G.fx.Word(G.fx.lib != null ? G.fx.lib.textWow : null, BallPos + Vector3.up * 2f, 3f);
            G.cam.Set(CamRig.Mode.HomeRun);
            if (batterP.body.ape) batterG.Play(Act.ChestBeat);
            else
            {
                batterG.Play(Act.Dance);
                if (rng.NextDouble() < 0.6) batterG.Acro(Ballplayer.AcroBackflip, 2.05f);   // when the camera gets to him
            }
            // the pitcher keels over
            if (pitcherG != null && rng.NextDouble() < 0.65)
                pitcherG.Ragdoll(pitcherG.transform.forward * -1.2f + Vector3.up * 0.6f, pitcherG.transform.right * -3.5f, 2.2f);
        }

        public void OnFoulIntoStands(Play p)
        {
            if (rng.NextDouble() < 0.4) { Popup("FAN CATCH!", Pal.Hex(0x9AD0FF), 0.8f); field.Excite(0.5f, 1.5f); }
        }

        public void OnMuff(Fielder f, Play p)
        {
            Popup("BONK!", Pal.Red, 1.1f);
            Sfx.Play(Sfx.Id.Bonk, 1f);
            G.fx.Dizzy(f.g.J[Ballplayer.HEAD], f.g.HeadTop, 2f);
            G.fx.Word(G.fx.lib != null ? G.fx.lib.textBoing : null, f.g.HeadTop + Vector3.up * 0.5f, 0.5f);
        }

        public void OnCatch(Fielder f, bool fly, Play p)
        {
            Sfx.Play(Sfx.Id.Pop, 0.9f);
            G.fx.CatchPop(f.g.GloveWorld);
            if (fly) Popup(p.divingCatch ? "WHAM! 다이빙 캐치!" : "CAUGHT!", p.divingCatch ? Pal.Gold : Pal.Red, p.divingCatch ? 1.2f : 0.95f);
            if (p.divingCatch)
            {
                G.fx.Puff(f.at, 8, 1.4f);
                G.fx.Word(G.fx.lib != null ? G.fx.lib.textWham : null, f.g.HeadTop + Vector3.up * 0.8f, 0.8f);
                field.Excite(0.8f, 2f);
            }
        }

        public void OnThrowDecided(Fielder f, int b, Play p) { }

        public void OnDive(Fielder f, Play p) { Sfx.Play(Sfx.Id.Whoosh, 0.6f, 0.7f); }

        public void OnTumble(Fielder f, Play p) { G.fx.Puff(f.at, 6, 1.1f); Sfx.Play(Sfx.Id.Thud, 0.5f); }

        public void OnCollision(Runner r, Fielder f, Play p)
        {
            Popup("KABOOM!", Pal.Hex(0xFF6A2B), 1.25f);
            Sfx.Play(Sfx.Id.Bonk, 1f, 0.8f);
            Sfx.Play(Sfx.Id.Thud, 0.8f);
            G.fx.Puff(f.at, 10, 1.6f);
            G.fx.Word(G.fx.lib != null ? G.fx.lib.textWham : null, f.g.HeadTop + Vector3.up * 0.6f, 0.7f);
            G.cam.Shake(0.35f);
            field.Excite(0.7f, 2f);
        }

        public void OnSlide(Runner r) { G.fx.Puff(r.g.pos, 6, 0.9f); Sfx.Play(Sfx.Id.Slide, 0.5f); }

        public void Popup(string s, Color c, float size = 1f, bool burst = true, string sub = null) { if (G.hud != null) G.hud.Popup(s, c, size, burst, sub); }

        public void PlaySfx(Sfx.Id id, float vol) => Sfx.Play(id, vol);

        public void OnPlayFinished(Play p)
        {
            phase = Phase.PlayEnd;
            phaseT = 0f;
            playEndT = Cfg.PlayOutro;
            // bases from the play
            for (int b = 0; b < 4; b++) bases[b] = null;
            foreach (var r in p.R)
            {
                if (r.st == RState.Out || r.st == RState.Scored) continue;
                int b = Mathf.Clamp(Mathf.RoundToInt(r.pos), 1, 3);
                if (r.isBatter && p.kind == Play.Kind.Foul) continue;
                if (bases[b] == null) bases[b] = r.p; else if (b < 3 && bases[b + 1] == null) bases[b + 1] = r.p;
            }
            if (p.kind == Play.Kind.Foul)
            {
                Stats.fouls++;
                if (p.caught) { playText = "FOUL FLY OUT!"; playColor = Pal.Red; batGauge[Bat] = Mathf.Min(1f, batGauge[Bat] + Cfg.GaugeOut); }
                else
                {
                    if (strikes < 2) strikes++;
                    else if (p.batter.p == batterP && bunt) { strikes = 3; }
                    batGauge[Bat] = Mathf.Min(1f, batGauge[Bat] + Cfg.GaugeFoul);
                    playText = bunt && strikes >= 3 ? "FOUL BUNT\nSTRIKE OUT!" : "FOUL!";
                    playColor = Pal.Gold;
                    if (strikes >= 3) { ks[Bat]++; AddOut(); }
                }
                Popup(playText, playColor, 0.95f);
                return;
            }
            bool batterOut = p.batter.st == RState.Out;
            if (p.kind == Play.Kind.HomeRun) { playText = ""; hits[Bat]++; Credit(batterP, 4); }
            else if (!batterOut && !p.caught)
            {
                hits[Bat] += p.error ? 0 : 1;
                int hb = p.hitBases;
                if (p.error) { playText = "ERROR!"; playColor = Pal.Hex(0x9AD0FF); }
                else if (p.outsOnPlay > 0) { playText = "FIELDER'S CHOICE"; playColor = Pal.Hex(0x9AD0FF); }
                else
                {
                    playText = hb >= 4 ? "INSIDE-THE-PARK\nHOME RUN!" : hb == 3 ? "TRIPLE!" : hb == 2 ? "DOUBLE!" : "SINGLE!";
                    playColor = Pal.Gold;
                    Credit(batterP, hb);
                }
                batGauge[Bat] = Mathf.Min(1f, batGauge[Bat] + Cfg.GaugeHit);
                field.Excite(0.6f, 2f);
            }
            else
            {
                playText = p.doublePlay ? "DOUBLE PLAY!" : p.caught ? "FLY OUT!" : "OUT!";
                playColor = Pal.Red;
                batGauge[Bat] = Mathf.Min(1f, batGauge[Bat] + Cfg.GaugeOut);
                pitchGauge[Fld] = Mathf.Min(1f, pitchGauge[Fld] + Cfg.GaugeOutP);
            }
            if (!string.IsNullOrEmpty(playText) && p.kind != Play.Kind.HomeRun) Popup(playText, playColor, 1.05f);
            if (p.runsOnPlay > 0 && !batterOut && p.kind != Play.Kind.HomeRun && p.runsOnPlay >= 1) Credit(batterP, 0, p.runsOnPlay);
        }

        void Credit(PlayerData p, int tb, int rbi = 0)
        {
            totalBases.TryGetValue(p, out int v);
            totalBases[p] = v + tb * 2 + rbi * 3;
        }

        bool CheckHalfOver()
        {
            if (outs < Cfg.Outs) return false;
            order[Bat] = (order[Bat] + 1) % Cfg.LineupSize;
            phase = Phase.HalfEnd;
            phaseT = 0f;
            G.hud.Banner("CHANGE", "3 아웃 · 공수 교대", Pal.Hex(0x9AD0FF), Cfg.HalfBanner - 0.3f);
            field.BoardMessage("3 OUTS", Color.white, 2f);
            return true;
        }

        bool CheckGameOverWalkOff()
        {
            if (!walkOff) return false;
            EndGame("WALK-OFF!");
            return true;
        }

        void AfterHalf()
        {
            int home = HomeTeam, away = visitor;
            if (!bottom)
            {
                // home team already ahead after the top half: no need to bat
                if (inning >= Cfg.StartInning && runs[home] > runs[away]) { EndGame(""); return; }
                bottom = true;
                BeginHalf();
                return;
            }
            if (runs[home] != runs[away]) { EndGame(""); return; }
            if (inning >= Cfg.LastInning) { EndGame("DRAW"); return; }
            inning++;
            bottom = false;
            BeginHalf();
        }

        void EndGame(string special)
        {
            phase = Phase.GameOver;
            phaseT = 0f;
            play = null;
            ShowPlateUI(false, false);
            ballView.Show(false);
            int me = Human, them = 1 - Human;
            bool draw = runs[me] == runs[them];
            bool win = runs[me] > runs[them];
            resultTitle = draw ? "DRAW" : win ? "VICTORY!" : "DEFEAT";
            string sub = special == "WALK-OFF!" ? "끝내기!" : draw ? "12회 무승부" : win ? "승리" : "패배";
            G.hud.Banner(resultTitle, sub, draw ? Color.white : win ? Pal.Gold : Pal.Red, Cfg.EndBanner);
            field.BoardMessage(resultTitle, win ? Pal.Gold : Color.white, 8f);
            field.Excite(win ? 1f : 0.3f, 6f);
            if (win) { Sfx.Play(Sfx.Id.Cheer, 1f); Sfx.Play(Sfx.Id.Fireworks, 0.8f); G.fx.Fireworks(new Vector3(0f, 40f, 60f)); }
            else Sfx.Play(Sfx.Id.Aww, 0.8f);
            // celebration: the winners run in and pile on top of each other (ragdolls); a draw just stands around
            HideAll();
            int winner = draw ? me : (runs[me] > runs[them] ? me : them);
            pile.Clear();
            for (int i = 0; i < 10; i++)
            {
                var g = guys[winner][i];
                if (g == null) continue;
                g.Show(true);
                g.Gear(false, true);
                float a = i / 10f * Mathf.PI * 2f;
                Vector3 p = new Vector3(Mathf.Sin(a) * 6.5f, 0f, 10f + Mathf.Cos(a) * 6.5f);
                g.Place(p, MathX.YawTo(p, new Vector3(0f, 0f, 10f)));
                if (draw) g.Play(i % 2 == 0 ? Act.Idle : Act.Sad);
                else { g.Play(Act.Run); pile.Add(new PileGuy { g = g, at = 0.55f + i * 0.24f + MathX.Range(rng, 0f, 0.1f) }); }
            }
            G.cam.Set(CamRig.Mode.Result);
            // fans and the match log
            mvp = null;
            int best = -1;
            foreach (var kv in totalBases) if (kv.Key.team == Human && kv.Value > best) { best = kv.Value; mvp = kv.Key; }
            fansEarned = (draw ? 15 : win ? 30 : 8) + runs[me] * 5 + homers[me] * 10;
            Stats.matches++;
            Stats.runs += runs[0] + runs[1];
            Stats.hits += hits[0] + hits[1];
            Stats.homers += homers[0] + homers[1];
            Stats.ks += ks[0] + ks[1];
            Stats.innings += inning - Cfg.StartInning + 1;
            if (draw) Stats.draws++;
            Stats.seconds += Time.time - matchStart;
            Debug.Log("[Wacky Baseball] match " + Stats.matches + ": " + T[visitor].abbr + " " + runs[visitor] + " - " + runs[HomeTeam] + " " + T[HomeTeam].abbr +
                      " (" + inning + "회, 안타 " + (hits[0] + hits[1]) + ", 홈런 " + (homers[0] + homers[1]) + ", 삼진 " + (ks[0] + ks[1]) + ", " + Mathf.RoundToInt(Time.time - matchStart) + "초) | " + Stats.Summary());
            if (!autoPlay)
            {
                string log = (draw ? "D" : win ? "W" : "L") + " " + runs[me] + "-" + runs[them] + " vs " + T[them].abbr + " (" + (inning) + "회)";
                Save.RecordMatch(fansEarned, log);
            }
        }

        // ------------------------------------------------------------------ tick

        public void Tick(float dt)
        {
            phaseT += dt;
            TickBat(dt);
            if (megaArmed && megaBat.gameObject.activeSelf) megaBat.localScale = Vector3.one * MathX.Damp(megaBat.localScale.x, 1f, 6f, dt);
            switch (phase)
            {
                case Phase.Lobby:
                    foreach (var g in guys[0]) g.Tick(dt);
                    return;
                case Phase.Intro:
                    if (phaseT >= Cfg.IntroTime || skipRequested) { skipRequested = false; BeginHalf(); }
                    break;
                case Phase.HalfIntro:
                    if (phaseT >= Cfg.HalfBanner || skipRequested) { skipRequested = false; NextBatter(false); }
                    break;
                case Phase.AtBatIntro:
                    if (phaseT >= Cfg.AtBatIntro || (skipRequested && phaseT > 0.4f)) { skipRequested = false; ReadyPitch(); }
                    break;
                case Phase.PitchReady:
                    if (batterG != null && batterG.swing < 0f) batterG.load = MathX.Damp(batterG.load, 0f, 6f, dt);
                    if (HumanPitches) AimInput(dt);
                    else
                    {
                        aiThink -= dt;
                        if (aiThink <= 0f) { pType = aiType; StartWindup(); }
                    }
                    if (HumanBats && !autoContact) ManualDrag();
                    break;
                case Phase.Windup:
                    holdT += dt;
                    if (HumanPitches) AimInput(dt);
                    if (releaseAt < 0f)
                    {
                        if (HumanPitches && holding)
                        {
                            ring = RingAt(holdT);
                            if (holdT >= Cfg.RingOver) { holding = false; ReleaseDecided(ring, true); }
                        }
                        else if (!HumanPitches && holdT >= aiHold) ReleaseDecided(0f, false);
                    }
                    else if (phaseT >= releaseAt) { releaseAt = -1f; stopShown = false; ReleaseBall(); }
                    if (HumanBats && !autoContact) ManualDrag();
                    break;
                case Phase.PitchFlight:
                    TickPitch(dt);
                    break;
                case Phase.PitchEnd:
                    if (batterG != null && batterG.swing < 0f) batterG.load = MathX.Damp(batterG.load, 0f, 6f, dt);
                    if (afterLive) { BallSim.Step(ref after, dt); ballView.Set(after.p); }
                    TickToss(dt);
                    bool lying = batterG != null && (batterG.Floored || batterG.InAcro) && phaseT < Cfg.CallTime + 1.6f;
                    if (phaseT >= Cfg.CallTime && !lying) AfterCall();
                    break;
                case Phase.Live:
                    if (camHold > 0f) { camHold -= dt; if (camHold <= 0f) G.cam.Set(CamRig.Mode.Field); }
                    if (batterG != null && batterG.act == Act.Stance && batterG.swing >= 0f) batterG.swing = Mathf.Min(1f, batterG.swing + dt / Cfg.SwingTime);
                    if (play != null)
                    {
                        play.Tick(dt);
                        ballView.Set(play.ballPos);
                        if (skipRequested) { skipRequested = false; play.SkipTrot(); }
                    }
                    break;
                case Phase.PlayEnd:
                    if (play != null && play.kind == Play.Kind.HomeRun) ballView.Show(false);
                    if (phaseT >= playEndT || skipRequested)
                    {
                        skipRequested = false;
                        ballView.Fire(false);
                        if (walkOff) { EndGame("WALK-OFF!"); break; }
                        if (CheckHalfOver()) break;
                        bool foulContinue = play != null && play.kind == Play.Kind.Foul && !play.caught && strikes < Cfg.MaxStrikes;
                        if (foulContinue) { RestoreAfterFoul(); ReadyPitch(); }
                        else NextBatter(true);
                    }
                    break;
                case Phase.HalfEnd:
                    if (phaseT >= Cfg.HalfBanner || skipRequested) { skipRequested = false; AfterHalf(); }
                    break;
                case Phase.GameOver:
                    if (phaseT >= Cfg.EndBanner || (skipRequested && phaseT > 1f))
                    {
                        skipRequested = false;
                        phase = Phase.Result;
                        phaseT = 0f;
                        G.cam.Set(CamRig.Mode.Result);
                        G.hud.ShowResult(this);
                    }
                    break;
                case Phase.Result:
                    break;
            }
            UpdateCatcher(dt);
            if (phase == Phase.GameOver || phase == Phase.Result) TickPile(dt);
            foreach (var arr in guys) foreach (var g in arr) if (g != null) g.Tick(dt);
            if (batterG != null && batterG.bat != null && batterG.Ragdolled) FlingBat();
            // he keeps the bat through the follow-through and lets it go as he sets off for first
            if (batterG != null && batterG.bat != null && !batFlying && (phase == Phase.Live || phase == Phase.PlayEnd) && batterG.act != Act.Stance && batterG.act != Act.Bunt)
                TossBat(lastHomer);
            if (inMitt && catcherG != null) ballView.Set(catcherG.GloveWorld);
            UpdatePlateUI();
        }

        void RestoreAfterFoul()
        {
            // the batter comes back to the box; runners were sent back to their bases by the play
            PlaceBatter();
            for (int b = 1; b <= 3; b++)
            {
                if (bases[b] == null) continue;
                var g = GuyOf(bases[b]);
                Vector3 p = Geo.Base(b) + (Geo.Base(b == 3 ? 0 : b + 1) - Geo.Base(b)).normalized * 0.6f;
                g.Show(true);
                g.Place(p, MathX.YawTo(p, Geo.Base(b == 3 ? 0 : b + 1)));
                g.Set(Act.Ready);
            }
            ResetFielders();
        }

        // ------------------------------------------------------------------ the catcher

        // ------------------------------------------------------------------ slapstick helpers

        sealed class PileGuy { public Ballplayer g; public float at, t; public bool done; }
        readonly List<PileGuy> pile = new List<PileGuy>();

        /// <summary>Winners run at the mound and dive onto the heap one after another.</summary>
        void TickPile(float dt)
        {
            Vector3 c = new Vector3(0f, 0f, 10f);
            foreach (var pg in pile)
            {
                if (pg.done) continue;
                pg.t += dt;
                var g = pg.g;
                Vector3 d = c - g.pos;
                d.y = 0f;
                float dist = d.magnitude;
                if (pg.t < pg.at)
                {
                    if (dist > 2.6f)
                    {
                        g.pos += d / dist * Mathf.Min(dist - 2.6f, 6.5f * dt);
                        g.yaw = MathX.YawTo(g.pos, c);
                        g.Set(Act.Run);
                    }
                    else g.Set(Act.Celebrate);
                    continue;
                }
                pg.done = true;
                Vector3 dir = dist > 0.01f ? d / dist : Vector3.forward;
                g.Ragdoll(dir * MathX.Range(rng, 2.2f, 3.4f) + Vector3.up * MathX.Range(rng, 4.2f, 5.6f),
                          new Vector3(MathX.Range(rng, -5f, 5f), MathX.Range(rng, -4f, 4f), MathX.Range(rng, -5f, 5f)), float.PositiveInfinity);
                Sfx.Play(Sfx.Id.Boing, 0.35f, MathX.Range(rng, 0.8f, 1.3f));
            }
        }

        /// <summary>The batter lets go of the bat and it cartwheels away.</summary>
        void FlingBat()
        {
            Transform b = batterG != null ? batterG.bat : null;
            if (b == null) return;
            batterG.bat = null;
            b.SetParent(null, true);
            batV = new Vector3(MathX.Range(rng, -3f, 3f), MathX.Range(rng, 3f, 6f), MathX.Range(rng, -2.5f, 1f));
            batSpin = new Vector3(MathX.Range(rng, -900f, 900f), MathX.Range(rng, -1200f, 1200f), MathX.Range(rng, -500f, 500f));
            batFlying = true;
            flyingBat = b;
        }

        /// <summary>The catcher takes the pitch this far in front of his feet.</summary>
        const float MittAhead = 0.32f;
        Vector3 catchPt;
        bool inMitt, caughtPitch;
        int toss;                 // 0 none, 1 winding up, 2 ball in the air, 3 done
        float tossT, tossDur;
        Vector3 tossA, tossB;

        /// <summary>The catcher is on the field and on screen (the batting view looks out from where he squats, so he is hidden there).</summary>
        bool CatcherOn => catcherG != null && catcherG.gameObject.activeSelf && fielders.Count > 0;

        /// <summary>Mitt target (x, y at the plate) and the sideways shuffle that brings his body behind it.</summary>
        void SetCatcherTarget(Vector2 at, bool snap = false, float k = 9f, float dt = 0f)
        {
            if (catcherG == null) return;
            Vector3 home = Geo.CatcherSpot;
            Vector3 want = new Vector3(Mathf.Clamp(at.x, -0.62f, 0.62f), Mathf.Clamp(at.y, 0.1f, 1.3f), home.z + MittAhead);
            // shuffle so the mitt shoulder stays behind the ball: further for a cross-body catch than for one on the glove side
            float gs = catcherP != null && catcherP.throws == Hand.Left ? -1f : 1f;
            float sx = want.x * gs;
            float shift = Mathf.Clamp(sx >= 0f ? sx * 1.05f : sx * 0.5f, -0.35f, 0.6f) * gs;
            Vector3 p = catcherG.pos;
            if (snap)
            {
                catcherG.gloveAt = want;
                p.x = home.x + shift;
            }
            else
            {
                catcherG.gloveAt = MathX.Damp(catcherG.gloveAt, want, k, dt);
                p.x = MathX.Damp(p.x, home.x + shift, k * 0.6f, dt);
            }
            catcherG.pos = p;
            catcherG.gloveW = 1f;
        }

        void UpdateCatcher(float dt)
        {
            if (catcherG == null) return;
            bool atBat = phase == Phase.AtBatIntro || phase == Phase.PitchReady || phase == Phase.Windup || phase == Phase.PitchFlight || phase == Phase.PitchEnd;
            bool onField = atBat || phase == Phase.HalfIntro || phase == Phase.Live || phase == Phase.PlayEnd || phase == Phase.HalfEnd;
            if (onField && fielders.Count > 0) catcherG.Show(G.cam.mode != CamRig.Mode.Batting);
            if (!atBat || catcherG.act != Act.Squat) return;
            float cy = (zoneLow + zoneHigh) * 0.5f;
            if (phase == Phase.PitchFlight && released && !crossed)
            {
                // he reads the pitch late, then snaps the mitt onto the ball's line
                float u = path.tf > 0f ? pt / path.PlateTime : 1f;
                SetCatcherTarget(new Vector2(catchPt.x, catchPt.y), false, u > 0.45f ? 16f : 3f, dt);
                catcherG.lookAt = ballView.t.position;
            }
            else if (phase == Phase.PitchReady || phase == Phase.Windup)
                SetCatcherTarget(HumanPitches ? aim : (phase == Phase.Windup || aiThink < 0.5f ? aiAim : new Vector2(0f, cy)), false, 7f, dt);
            else if (phase == Phase.AtBatIntro)
                SetCatcherTarget(new Vector2(0f, cy), false, 5f, dt);
        }

        void CatchPitch()
        {
            inMitt = caughtPitch = true;
            afterLive = false;
            ballView.Trail(false);
            Vector3 g = catcherG.GloveWorld;
            ballView.Set(g);
            Sfx.Play(Sfx.Id.Pop, 1f, 0.8f);
            G.fx.CatchPop(g);
        }

        /// <summary>After the call the catcher pops up and lobs the ball back to the pitcher.</summary>
        void TickToss(float dt)
        {
            if (catcherG == null || pitcherG == null) return;
            if (toss == 0)
            {
                if (!inMitt || phaseT < 0.08f) return;
                toss = 1;
                inMitt = false;
                ballView.Show(false);
                catcherG.hasBall = true;
                catcherG.yaw = MathX.YawTo(catcherG.pos, Geo.Rubber);
                catcherG.Play(Act.Throw);
                pitcherG.Set(Act.Idle);
                pitcherG.looking = true;
                pitcherG.lookAt = catcherG.HeadTop;
            }
            else if (toss == 1)
            {
                if (catcherG.actT < Ballplayer.ThrowRelease) return;
                toss = 2;
                tossT = 0f;
                catcherG.hasBall = false;
                tossA = catcherG.HandWorld;
                Vector3 pp = pitcherG.pos;
                tossB = new Vector3(pp.x - 0.15f, pp.y + 1.05f * pitcherG.Height / 1.85f, pp.z - 0.45f);
                tossDur = Mathf.Clamp(MathX.Horiz(tossA, tossB) / 26f, 0.45f, 0.64f);
                ballView.Style(PitchType.Four);
                ballView.Show(true);
                ballView.Trail(false);
                ballView.Set(tossA);
                Sfx.Play(Sfx.Id.Whoosh, 0.22f, 1.5f);
            }
            else if (toss == 2)
            {
                tossT += dt;
                float u = Mathf.Clamp01(tossT / tossDur);
                Vector3 p = Vector3.Lerp(tossA, tossB, u) + Vector3.up * (4f * 0.85f * u * (1f - u));
                ballView.Set(p);
                pitcherG.gloveW = 1f;
                pitcherG.gloveAt = p;
                pitcherG.lookAt = p;
                if (u >= 1f)
                {
                    toss = 3;
                    ballView.Show(false);
                    pitcherG.hasBall = true;
                    pitcherG.gloveW = 0f;
                    Sfx.Play(Sfx.Id.Pop, 0.35f, 1.25f);
                }
            }
            if (toss >= 2 && catcherG.act == Act.Throw && catcherG.actT > 0.5f) catcherG.Set(Act.Squat);
        }

        void FinishToss()
        {
            if (toss == 1 || toss == 2 || inMitt)
            {
                ballView.Show(false);
                if (catcherG != null) catcherG.hasBall = false;
                if (pitcherG != null) { pitcherG.hasBall = true; pitcherG.gloveW = 0f; }
            }
            if (catcherG != null && catcherG.act == Act.Throw) catcherG.Set(Act.Squat);
            toss = 3;
            inMitt = false;
        }

        /// <summary>World x of the plate as seen on screen: +1 when screen-right is world +x, -1 when the camera looks
        /// back at the plate from behind the mound (the pitching view), so the stick moves the aim the way it points.</summary>
        public float AimSign
        {
            get
            {
                var c = G.cam != null && G.cam.cam != null ? G.cam.cam : Camera.main;
                return c != null && Vector3.Dot(c.transform.right, Vector3.right) < 0f ? -1f : 1f;
            }
        }

        void AimInput(float dt)
        {
            if (joy.sqrMagnitude < 1e-4f) return;
            aim += new Vector2(joy.x * AimSign, joy.y) * (0.95f * dt);
            float cy = (zoneLow + zoneHigh) * 0.5f;
            aim.x = Mathf.Clamp(aim.x, -Cfg.AimMax, Cfg.AimMax);
            aim.y = Mathf.Clamp(aim.y, cy - Cfg.AimMax, cy + Cfg.AimMax);
        }

        void UpdatePlateUI()
        {
            bool pitchPhase = phase == Phase.PitchReady || phase == Phase.Windup || phase == Phase.PitchFlight;
            if (!pitchPhase) return;
            if (HumanBats)
            {
                float r = bunt ? (Cfg.ContactR + Cfg.ContactRCon * batterP.Con) * 1.45f : HitModel.Radius(batterP, autoContact, megaArmed);
                contactRing.position = new Vector3(contact.x, contact.y, -0.02f);
                contactRing.localScale = Vector3.one * r * 2.1f;
                contactDot.position = new Vector3(contact.x, contact.y, -0.025f);
                contactDot.localScale = Vector3.one * 0.03f;
                contactRing.gameObject.SetActive(true);
                contactDot.gameObject.SetActive(!bunt);
                if (!swung && batterG != null && !bunt) batterG.swingAim = new Vector3(contact.x, contact.y, 0f);
            }
            if (HumanPitches)
            {
                aimRing.position = new Vector3(aim.x, aim.y, 0f);
                aimRing.localScale = Vector3.one * 0.16f;
                bool showAcc = phase == Phase.Windup && releaseAt < 0f && holding;
                accRing.gameObject.SetActive(showAcc);
                if (showAcc)
                {
                    accRing.position = new Vector3(aim.x, aim.y, -0.01f);
                    accRing.localScale = Vector3.one * (ring * 2f + 0.06f);
                }
                aimRing.gameObject.SetActive(phase != Phase.PitchFlight);
            }
        }

        // ------------------------------------------------------------------ queries for the HUD and camera

        public string HalfLabel => (bottom ? "▼" : "▲") + inning;

        public bool ShowsThrowPrompt => play != null && phase == Phase.Live && play.prompt && play.humanField;
        public bool ShowsRunControls => play != null && phase == Phase.Live && play.humanRun && play.kind == Play.Kind.InPlay && !play.finished;
        public float BattingGauge => batGauge[Human];
        public float PitchingGauge => pitchGauge[Human];
        public bool MegaReady => batGauge[Human] >= 1f && !megaArmed;
        public bool MagicReady => pitchGauge[Human] >= 1f;
        public Vector3 BallPos => ballView.t.position;
    }
}
