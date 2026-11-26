using System.Collections.Generic;
using UnityEngine;

namespace BananaBall
{
    public enum Hand { Right = 0, Left = 1 }

    /// <summary>Fielding spots. DH bats but does not field; the catcher (C) fields but does not bat (8-man batting order).</summary>
    public enum Pos { P = 0, B1 = 1, B2 = 2, SS = 3, B3 = 4, LF = 5, CF = 6, RF = 7, DH = 8, C = 9 }

    /// <summary>Regular pitches first, then the five wacky "magic" pitches (마구).</summary>
    public enum PitchType { Four = 0, Curve, Slider, Change, Sinker, Splitter, Zigzag, Balloon, Phantom, Missile, Stop }

    public struct PitchInfo
    {
        public string name;   // 버튼 이름
        public string tag;    // 짧은 영문 표기
        public float time;    // flight time multiplier (four-seam = 1)
        public Vector2 brk;   // shape break at the plate in metres (x: arm side +, y: up +), gravity included
        public float kmh;     // displayed speed for a VEL 80 pitcher
        public bool magic;
        public Color color;

        static PitchInfo[] table;

        public static PitchInfo Of(PitchType t)
        {
            if (table == null)
            {
                table = new PitchInfo[11];
                table[(int)PitchType.Four] = new PitchInfo { name = "포심", tag = "4FB", time = 1.00f, brk = new Vector2(0.14f, -0.62f), kmh = 151f, color = Pal.Hex(0xE8453C) };
                table[(int)PitchType.Curve] = new PitchInfo { name = "커브", tag = "CB", time = 1.24f, brk = new Vector2(-0.34f, -1.55f), kmh = 124f, color = Pal.Hex(0x3C8BE8) };
                table[(int)PitchType.Slider] = new PitchInfo { name = "슬라이더", tag = "SL", time = 1.10f, brk = new Vector2(-0.62f, -0.92f), kmh = 138f, color = Pal.Hex(0x7A4FE0) };
                table[(int)PitchType.Change] = new PitchInfo { name = "체인지업", tag = "CH", time = 1.26f, brk = new Vector2(0.30f, -1.12f), kmh = 131f, color = Pal.Hex(0x2BAE66) };
                table[(int)PitchType.Sinker] = new PitchInfo { name = "싱커", tag = "SI", time = 1.03f, brk = new Vector2(0.48f, -1.00f), kmh = 147f, color = Pal.Hex(0xF08A24) };
                table[(int)PitchType.Splitter] = new PitchInfo { name = "스플리터", tag = "FS", time = 1.12f, brk = new Vector2(0.10f, -1.40f), kmh = 140f, color = Pal.Hex(0x1FA5A5) };
                table[(int)PitchType.Zigzag] = new PitchInfo { name = "지그재그볼", tag = "ZIG", time = 1.18f, brk = new Vector2(0f, -0.80f), kmh = 128f, magic = true, color = Pal.Hex(0xFFD23F) };
                table[(int)PitchType.Balloon] = new PitchInfo { name = "풍선볼", tag = "BLN", time = 2.05f, brk = new Vector2(0f, -0.70f), kmh = 64f, magic = true, color = Pal.Hex(0xFF7EB6) };
                table[(int)PitchType.Phantom] = new PitchInfo { name = "분신볼", tag = "PHM", time = 1.02f, brk = new Vector2(0.10f, -0.70f), kmh = 141f, magic = true, color = Pal.Hex(0x9AE6FF) };
                table[(int)PitchType.Missile] = new PitchInfo { name = "미사일볼", tag = "MSL", time = 0.66f, brk = new Vector2(0f, -0.25f), kmh = 199f, magic = true, color = Pal.Hex(0xFF5A1F) };
                table[(int)PitchType.Stop] = new PitchInfo { name = "일시정지볼", tag = "STP", time = 1.00f, brk = new Vector2(0.05f, -0.75f), kmh = 0f, magic = true, color = Pal.Hex(0xB6FF5C) };
            }
            return table[(int)t];
        }

        public static string Speed(PitchType t, int vel)
        {
            var p = Of(t);
            if (t == PitchType.Stop) return "??? km/h";
            float k = p.kmh + (vel - 80) * 0.45f * (p.magic ? 0.5f : 1f);
            return Mathf.RoundToInt(k) + " km/h";
        }
    }

    public enum Hair { Short, Buzz, Bald, Mullet, Afro, Mohawk, Long, Ponytail }
    public enum Beard { None, Stubble, Mustache, Handlebar, Walrus, Goatee, Full, Viking, Chops }
    public enum Gear { None, Shades, Goggles, Monocle, EyeBlack, Headband }

    // Costumes after Banana Ball's dress-up nights (kilts, overalls, the yellow tux, pool parties, the fire crew, tailgates).
    // The defaults are the plain baseball uniform.
    /// <summary>What covers the torso and arms.</summary>
    public enum Top { Jersey, Aloha, Tank, Tux, FireCoat }
    /// <summary>What covers the hips and legs.</summary>
    public enum Bottom { Pants, Shorts, Kilt, Overalls }
    /// <summary>Headwear in the field (a batter still wears his helmet, except a firefighter, whose helmet is a helmet).</summary>
    public enum Hat { Cap, Bucket, Straw, Cowboy, Fire, Bowler }
    public enum Feet { Cleats, FlipFlops, Boots }
    /// <summary>Add-ons: a swim ring round the waist, a flower lei, water wings, a snorkel (on goggles), a neckerchief, a bow tie.</summary>
    [System.Flags] public enum Extra { None = 0, Floatie = 1, Lei = 2, Wings = 4, Snorkel = 8, Bandana = 16, BowTie = 32 }

    /// <summary>Body and face description used to build a player mesh. Proportions are realistic; the wacky part is in the extremes.</summary>
    public struct Body
    {
        public float height;     // metres
        public float width;      // shoulder/hip width multiplier
        public float belly;      // 0..1
        public float muscle;     // 0..1 (arms, shoulders)
        public float head;       // head size multiplier (1 = realistic)
        public float nose, ears, chin, brow;  // multipliers
        public Color skin, hairColor;
        public Hair hair;
        public Beard beard;
        public Gear gear;
        public bool sleeves;     // long undershirt sleeves in the team colour
        public bool highSocks;   // high socks with stripes
        public bool ape;         // a gorilla in uniform: hunched, huge arms, fur, muzzle, bare feet
        public Top top;
        public Bottom bottom;
        public Hat hat;
        public Feet feet;
        public Extra extra;
        /// <summary>Picks the colourway and pattern of the costume (print layout, which team colour leads).</summary>
        public int style;

        public bool Has(Extra e) => (extra & e) != 0;
        /// <summary>Anything but the plain uniform.</summary>
        public bool Costumed => top != Top.Jersey || bottom != Bottom.Pants || hat != Hat.Cap || feet != Feet.Cleats || extra != Extra.None;

        public string Key()
        {
            return height.ToString("F2") + width.ToString("F2") + belly.ToString("F2") + muscle.ToString("F2") + head.ToString("F2") +
                   nose.ToString("F1") + ears.ToString("F1") + chin.ToString("F1") + brow.ToString("F1") +
                   ColorUtility.ToHtmlStringRGB(skin) + ColorUtility.ToHtmlStringRGB(hairColor) + (int)hair + "_" + (int)beard + "_" + (int)gear + (sleeves ? "S" : "-") + (highSocks ? "H" : "-") + (ape ? "A" : "") +
                   (Costumed ? "|o" + (int)top + (int)bottom + (int)hat + (int)feet + "x" + (int)extra + "s" + style : "");
        }
    }

    public class PlayerData
    {
        public string name;
        public string trait;     // 한 줄 특징 (괴짜 포인트)
        public int number;
        public Pos pos;
        public Hand bats, throws;
        public int con, pow, spd, eye;   // batting 30..99
        public int vel, ctl, mov, sta;   // pitching 30..99 (pitchers)
        public PitchType[] pitches = new PitchType[0];
        public PitchType magic = PitchType.Zigzag;
        public Body body;
        public int team;

        public float Con => Mathf.Clamp01((con - 30) / 69f);
        public float Pow => Mathf.Clamp01((pow - 30) / 69f);
        public float Spd => Mathf.Clamp01((spd - 30) / 69f);
        public float Eye => Mathf.Clamp01((eye - 30) / 69f);
        public float Vel => Mathf.Clamp01((vel - 30) / 69f);
        public float Ctl => Mathf.Clamp01((ctl - 30) / 69f);
        public float Mov => Mathf.Clamp01((mov - 30) / 69f);
        public float Sta => Mathf.Clamp01((sta - 30) / 69f);

        /// <summary>Top running speed in m/s.</summary>
        public float RunSpeed => Mathf.Lerp(6.7f, 8.6f, Spd) * Mathf.Lerp(1f, 0.93f, body.belly);
        /// <summary>Throwing speed in m/s.</summary>
        public float ArmSpeed => pos == Pos.P ? Mathf.Lerp(30f, 37f, Vel) : Mathf.Lerp(29f, 36.5f, 0.35f + 0.65f * Pow);
        public string Short
        {
            get
            {
                int i = name.LastIndexOf(' ');
                return i > 0 ? name.Substring(i + 1) : name;
            }
        }
    }

    public class TeamData
    {
        public string name, nameKr, abbr, city;
        public Color main, accent, dark;     // cap and helmet = main, piping = accent
        public Color jersey, pants, stripe;  // home white or road grey
        public bool pinstripes;
        /// <summary>Dalmatian spots on the jersey (the fire crew).</summary>
        public bool spots;
        public readonly PlayerData[] lineup = new PlayerData[8];
        public PlayerData pitcher;
        /// <summary>Squats behind the plate, receives every pitch and covers home. Fields only (not in the batting order).</summary>
        public PlayerData catcher;
        public int index;

        public PlayerData AtPos(Pos p)
        {
            if (p == Pos.P) return pitcher;
            if (p == Pos.C) return catcher;
            foreach (var pl in lineup) if (pl.pos == p) return pl;
            return null;
        }
    }

    /// <summary>All game rules and tuning (Baseball Clash style: one inning from the 9th, extras to the 12th, then a draw).</summary>
    public static class Cfg
    {
        // Match structure
        public const int StartInning = 9, LastInning = 12, MaxBalls = 4, MaxStrikes = 3, Outs = 3, LineupSize = 8;

        // World
        public const float Gravity = 9.81f;
        public const float BallRadius = 0.0366f;
        public const float Drag = 0.0047f;           // k in a = -k|v|v (fits real carry: 45 m/s at 28° lands ~114 m)
        public static float PlaySpeed = 1.22f;       // live play runs this much faster than real time

        // Pitching
        public const float FastTimeSlow = 0.74f, FastTimeFast = 0.57f;   // four-seam flight time at VEL 30 / 99 (slowed for touch play)
        public const float ReleaseZ = 16.75f, ReleaseY = 1.28f, ReleaseX = 0.42f;
        public const float AimMax = 0.62f;           // aim reticle travel from the zone centre (m)
        public const float RingStart = 0.36f, RingMin = 0.025f, RingTime = 0.85f, RingOver = 1.35f;   // pitch accuracy ring
        public const float WindupToRelease = 0.36f;  // from the release input to the ball leaving the hand
        public const float BackstopZ = -17f;

        // Batting
        public const float SwingToContact = 0.16f;   // tap to bat-on-ball
        public const float SwingTime = 0.46f;
        public const float AutoAimLag = 0.10f;       // auto contact area reacts this late
        public const float ContactR = 0.09f, ContactRCon = 0.06f;        // contact area radius (m) + per CON
        public const float Window = 0.062f, WindowCon = 0.05f;           // timing window (s) + per CON
        public const float PerfectT = 0.18f, PerfectS = 0.28f;
        public const float ExitMin = 13f, ExitMax = 38.5f, ExitPow = 13f;     // m/s
        public const float AutoPower = 0.94f;
        public const float MegaBatR = 1.75f, MegaBatPower = 1.12f;

        // Gauges (0..1)
        public const float GaugeBall = 0.12f, GaugeFoul = 0.08f, GaugeHit = 0.3f, GaugeOut = 0.16f, GaugeStrike = 0.14f, GaugeK = 0.3f, GaugeOutP = 0.2f;

        // Fielding / running
        public const float ReachH = 0.95f, CatchTop = 2.35f, JumpTop = 3.0f, DiveReach = 2.6f;
        public const float FielderReact = 0.22f, GatherTime = 0.22f, ThrowArc = 0.09f;
        public const float BaseTouch = 0.35f;        // fielder within this of a base counts as on it
        public const float RunnerLead = 0.0f;
        public const float BatterStart = 0.32f;      // drop the bat and go
        public const float HumanThrowWait = 1.4f;    // recommended base is thrown automatically after this
        public const float ErrorChance = 0.025f;
        public const float PlayCap = 26f;
        public const float TrotCap = 52f;           // a home-run / ground-rule trot is decided and runs long; only a safety net

        // Presentation
        public const float IntroTime = 4.2f, HalfBanner = 2.2f, AtBatIntro = 1.5f, CallTime = 1.05f, PlayOutro = 1.4f, EndBanner = 3.2f;
    }

    /// <summary>Real-scale field. Home plate at the origin, +Z toward second base, +X toward first base. Units are metres.</summary>
    public static class Geo
    {
        public const float BaseDist = 27.43f;
        public static readonly float H = BaseDist / Mathf.Sqrt(2f);
        public static readonly Vector3 Home = Vector3.zero;
        public static readonly Vector3 First = new Vector3(H, 0f, H);
        public static readonly Vector3 Second = new Vector3(0f, 0f, 2f * H);
        public static readonly Vector3 Third = new Vector3(-H, 0f, H);
        public const float RubberZ = 18.44f, MoundR = 2.74f, MoundH = 0.18f;
        public static readonly Vector3 Rubber = new Vector3(0f, 0f, RubberZ);
        public const float PlateHalf = 0.216f;
        public const float FenceLine = 99f, FenceCenter = 122f;
        public const float WallD0 = 17f, WallD1 = 6.5f, WallH = 1.25f, NetH = 13f;
        public const float SkinR = 29f;

        public static Vector3 Base(int i)
        {
            switch (i)
            {
                case 1: return First;
                case 2: return Second;
                case 3: return Third;
                default: return Home;
            }
        }

        public static float FenceR(float angDeg)
        {
            float a = Mathf.Min(Mathf.Abs(angDeg), 45f) * Mathf.Deg2Rad;
            float c = Mathf.Cos(2f * a);
            return FenceLine + (FenceCenter - FenceLine) * (0.55f * c + 0.45f * c * c);
        }

        public static float FenceH(float angDeg)
        {
            float a = Mathf.Min(Mathf.Abs(angDeg), 45f);
            return Mathf.Lerp(3.2f, 2.5f, a / 45f);
        }

        public static float AngleDeg(Vector3 p) => Mathf.Atan2(p.x, p.z) * Mathf.Rad2Deg;
        public static bool FairAngle(Vector3 p) => p.z > -0.05f && Mathf.Abs(p.x) <= p.z + 0.02f;

        /// <summary>side +1 = first-base line, -1 = third-base line. s = along the line, d = distance into foul ground (negative = fair).</summary>
        public static void LineCoords(Vector3 p, out int side, out float s, out float d)
        {
            side = p.x >= 0f ? 1 : -1;
            const float k = 0.70710678f;
            s = (side * p.x + p.z) * k;
            d = (side * p.x - p.z) * k;
        }

        public static Vector3 FromLine(int side, float s, float d, float y = 0f)
        {
            const float k = 0.70710678f;
            return new Vector3(side * (s + d) * k, y, (s - d) * k);
        }

        /// <summary>Distance from the foul line to the foul-ground wall at a point s along the line.</summary>
        public static float WallD(float s) => Mathf.Lerp(WallD0, WallD1, Mathf.Clamp01(s / FenceLine));

        public static float GroundY(Vector3 p)
        {
            float dx = p.x, dz = p.z - RubberZ;
            float r2 = dx * dx + dz * dz;
            if (r2 >= MoundR * MoundR) return 0f;
            float r = Mathf.Sqrt(r2) / MoundR;
            return MoundH * (1f - r * r);
        }

        /// <summary>Stadium boundary (wall foot) from the left-field pole around home to the right-field pole, then the outfield fence back.</summary>
        public static List<Vector3> Boundary(int fenceSteps = 48, int lineSteps = 24, int arcSteps = 20)
        {
            var pts = new List<Vector3>();
            // left foul wall: from the left pole corner toward home
            pts.Add(FromLine(-1, FenceLine, 0f));
            for (int i = lineSteps; i >= 0; i--)
            {
                float s = FenceLine * i / lineSteps;
                pts.Add(FromLine(-1, s, WallD(s)));
            }
            // behind home: arc between the two line ends
            for (int i = 1; i < arcSteps; i++)
            {
                float a = Mathf.Lerp(-135f, -225f, i / (float)arcSteps) * Mathf.Deg2Rad;
                pts.Add(new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * WallD0);
            }
            for (int i = 0; i <= lineSteps; i++)
            {
                float s = FenceLine * i / lineSteps;
                pts.Add(FromLine(1, s, WallD(s)));
            }
            pts.Add(FromLine(1, FenceLine, 0f));
            // outfield fence: right pole to left pole
            for (int i = 1; i < fenceSteps; i++)
            {
                float a = Mathf.Lerp(45f, -45f, i / (float)fenceSteps);
                pts.Add(Dir(a) * FenceR(a));
            }
            return pts;
        }

        public static Vector3 Dir(float sprayDeg) => new Vector3(Mathf.Sin(sprayDeg * Mathf.Deg2Rad), 0f, Mathf.Cos(sprayDeg * Mathf.Deg2Rad));

        /// <summary>Strike zone for a batter of this height, at the plate plane z = 0.</summary>
        public static void Zone(float height, out float low, out float high)
        {
            // knees to just under the chest of the cartoon body (legs and torso are shortened, see PlayerMesh)
            low = 0.18f * height;
            high = 0.43f * height;
        }

        public static bool InZone(Vector3 p, float low, float high)
        {
            float r = Cfg.BallRadius;
            return Mathf.Abs(p.x) <= PlateHalf + r && p.y >= low - r && p.y <= high + r;
        }

        /// <summary>Where each position stands before the pitch.</summary>
        public static Vector3 Spot(Pos p)
        {
            switch (p)
            {
                case Pos.P: return new Vector3(0f, 0f, RubberZ + 0.3f);
                case Pos.C: return CatcherSpot;
                case Pos.B1: return FromLine(1, 29.5f, -3.4f);
                case Pos.B2: return new Vector3(10.6f, 0f, 34.2f);
                case Pos.SS: return new Vector3(-10.4f, 0f, 34.6f);
                case Pos.B3: return FromLine(-1, 28f, -3.2f);
                case Pos.LF: return new Vector3(-36f, 0f, 74f);
                case Pos.CF: return new Vector3(0f, 0f, 92f);
                case Pos.RF: return new Vector3(36f, 0f, 74f);
                default: return new Vector3(-14f, 0f, 3f);
            }
        }

        /// <summary>The catcher's feet behind the plate (he squats there for every pitch).</summary>
        public static readonly Vector3 CatcherSpot = new Vector3(0f, 0f, -0.98f);

        /// <summary>Where a fielder stands to take a throw at a base (home is covered from the third-base side of the plate).</summary>
        public static Vector3 Cover(int b)
        {
            Vector3 bp = Base(b == 4 ? 0 : b);
            if (b == 0 || b == 4) return new Vector3(-0.35f, 0f, -0.55f);
            Vector3 toIn = (new Vector3(0f, 0f, H) - bp).normalized;
            return bp + toIn * 0.38f;
        }

        public static Vector3 Dugout(int team) => new Vector3(team == 0 ? -21f : 21f, 0f, 2.5f);
        public static readonly Vector3 OnDeckL = new Vector3(-9.5f, 0f, -4.8f), OnDeckR = new Vector3(9.5f, 0f, -4.8f);
    }

    public static class Pal
    {
        public static readonly Color Ink = Hex(0x14161C);
        public static readonly Color Gold = Hex(0xFFC83A);
        public static readonly Color Grass = Hex(0x3F7A2C);
        public static readonly Color Grass2 = Hex(0x48862F);
        public static readonly Color Dirt = Hex(0xB86B45);
        public static readonly Color DirtDark = Hex(0x99583A);
        public static readonly Color Track = Hex(0xA05E3E);
        public static readonly Color Chalk = Hex(0xF4F1EA);
        public static readonly Color Wall = Hex(0x1E3A2E);
        public static readonly Color Pad = Hex(0x23324A);
        public static readonly Color Seat = Hex(0x2B4E86);
        public static readonly Color Seat2 = Hex(0x23427A);
        public static readonly Color Concrete = Hex(0x9A9EA5);
        public static readonly Color Steel = Hex(0x5F6670);
        public static readonly Color Leather = Hex(0x8A4B22);
        public static readonly Color LeatherDark = Hex(0x5A2E12);
        public static readonly Color Lace = Hex(0xC9A27A);
        public static readonly Color Wood = Hex(0xD9A866);
        public static readonly Color Cleat = Hex(0x15161A);
        public static readonly Color UiDark = Hex(0x0E1420);
        public static readonly Color UiPanel = Hex(0x172235);
        public static readonly Color UiLine = Hex(0x2C3D5A);
        public static readonly Color Red = Hex(0xE8453C);
        public static readonly Color Green = Hex(0x39C46A);
        public static readonly Color Blue = Hex(0x3C8BE8);

        public static readonly Color[] Skins = { Hex(0xF2C9A8), Hex(0xE3B08A), Hex(0xC98E64), Hex(0xA8714A), Hex(0x7E5236), Hex(0x5C3A26), Hex(0xF5D5BC) };
        public static readonly Color[] Hairs = { Hex(0x2A1C14), Hex(0x4A3020), Hex(0x8A5A2E), Hex(0xC9A060), Hex(0x15110E), Hex(0xB34A22), Hex(0xD8D4CC), Hex(0x3A3A3A) };

        public static Color Hex(int rgb, float a = 1f) =>
            new Color(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f, a);

        public static Color WithA(Color c, float a) { c.a = a; return c; }

        /// <summary>Vertex colour for the lit shader: alpha carries 1 - smoothness (so plain colours stay matte).</summary>
        public static Color Gloss(Color c, float smooth) { c.a = 1f - Mathf.Clamp01(smooth); return c; }

        public static Color Mul(Color c, float k) => new Color(c.r * k, c.g * k, c.b * k, c.a);
    }

    public static class MathX
    {
        public static float Damp(float a, float b, float lambda, float dt) => Mathf.Lerp(a, b, 1f - Mathf.Exp(-lambda * dt));
        public static Vector3 Damp(Vector3 a, Vector3 b, float lambda, float dt) => Vector3.Lerp(a, b, 1f - Mathf.Exp(-lambda * dt));
        public static float Horiz(Vector3 a, Vector3 b) { float dx = a.x - b.x, dz = a.z - b.z; return Mathf.Sqrt(dx * dx + dz * dz); }
        public static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
        public static float Ease(float t) { t = Mathf.Clamp01(t); return t * t * (3f - 2f * t); }
        public static float EaseOut(float t) { t = Mathf.Clamp01(t); return 1f - (1f - t) * (1f - t); }
        public static float Range(System.Random r, float a, float b) => a + (float)r.NextDouble() * (b - a);
        public static bool Chance(System.Random r, float p) => r.NextDouble() < p;
        public static float YawTo(Vector3 from, Vector3 to) { Vector3 d = to - from; return Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg; }

        public static float Gauss(System.Random r)
        {
            double u1 = 1.0 - r.NextDouble(), u2 = r.NextDouble();
            return (float)(System.Math.Sqrt(-2.0 * System.Math.Log(u1)) * System.Math.Sin(2.0 * System.Math.PI * u2));
        }

        public static T Pick<T>(System.Random r, T[] a) => a[r.Next(a.Length)];
    }

    /// <summary>Team and player data. Team 0 is the player's club; the rival is drawn from four wacky clubs.</summary>
    public static class Roster
    {
        public static TeamData Mine()
        {
            var t = new TeamData
            {
                index = 0, name = "Banana Bombers", nameKr = "바나나 봄버스", abbr = "BAN", city = "Bananaport",
                main = Pal.Hex(0xF6C928), accent = Pal.Hex(0x1C2A55), dark = Pal.Hex(0x111A38),
                jersey = Pal.Hex(0xF7F5EE), pants = Pal.Hex(0xF1EFE7), stripe = Pal.Hex(0x1C2A55), pinstripes = true
            };
            // name, trait, number, pos, bats, throws, con, pow, spd, eye, body
            t.lineup[0] = P("Zippy Dash", "번개 발 · 고글", 2, Pos.CF, Hand.Left, Hand.Left, 78, 48, 97, 70, B(1.74f, 0.92f, 0f, 0.2f, 1.04f, Hair.Mohawk, Beard.None, Gear.Goggles, 2, 4, false, true));
            t.lineup[1] = P("Noodle McGee", "2미터 장다리", 7, Pos.B2, Hand.Right, Hand.Right, 84, 52, 74, 80, B(1.98f, 0.9f, 0f, 0.1f, 1.0f, Hair.Mullet, Beard.Stubble, Gear.None, 0, 5, false, false));
            t.lineup[2] = P("Rocco Fizzwick", "말아올린 콧수염", 11, Pos.SS, Hand.Right, Hand.Right, 90, 66, 70, 84, B(1.82f, 1.0f, 0.05f, 0.4f, 1.06f, Hair.Short, Beard.Handlebar, Gear.None, 1, 0, true, false));
            t.lineup[3] = P("Big Moe Wallop", "바이킹 수염 · 맥주배", 44, Pos.B1, Hand.Right, Hand.Right, 62, 99, 34, 58, B(1.95f, 1.18f, 0.9f, 0.7f, 1.1f, Hair.Long, Beard.Viking, Gear.None, 0, 5, false, false));
            t.lineup[4] = P("Bongo", "바나나 먹는 4번 고릴라", 99, Pos.DH, Hand.Right, Hand.Right, 62, 99, 38, 52, Ape(1.98f, 1.42f, 0.45f));
            t.lineup[5] = P("Taco Bumblesby", "구레나룻 장인", 15, Pos.B3, Hand.Right, Hand.Right, 74, 76, 56, 66, B(1.79f, 1.04f, 0.3f, 0.5f, 1.08f, Hair.Afro, Beard.Chops, Gear.None, 2, 6, false, false));
            t.lineup[6] = P("Lefty Loops", "아프로 · 왼손 마법사", 21, Pos.RF, Hand.Left, Hand.Left, 80, 70, 72, 74, B(1.85f, 0.98f, 0f, 0.35f, 1.02f, Hair.Afro, Beard.None, Gear.EyeBlack, 5, 0, false, true));
            t.lineup[7] = P("Gus O'Whiff", "외알 안경 신사", 9, Pos.LF, Hand.Right, Hand.Right, 70, 64, 64, 92, B(1.80f, 1.0f, 0.2f, 0.3f, 1.08f, Hair.Bald, Beard.Walrus, Gear.Monocle, 0, 6, false, false));
            var p = P("Mustache Max", "콧수염이 투구폼", 1, Pos.P, Hand.Right, Hand.Right, 40, 40, 50, 40, B(1.90f, 1.02f, 0.1f, 0.5f, 1.06f, Hair.Short, Beard.Handlebar, Gear.None, 3, 1, true, true));
            p.vel = 90; p.ctl = 78; p.mov = 84; p.sta = 80;
            p.pitches = new[] { PitchType.Four, PitchType.Curve, PitchType.Slider };
            p.magic = PitchType.Zigzag;
            t.pitcher = p;
            // the catcher: fields only, so his batting numbers just set his arm and legs
            t.catcher = P("Chunk Mittsley", "미트가 얼굴만 함", 27, Pos.C, Hand.Right, Hand.Right, 50, 82, 36, 50, B(1.80f, 1.16f, 0.62f, 0.75f, 1.1f, Hair.Mullet, Beard.Full, Gear.None, 1, 2, true, false));
            // costume night, Banana Ball style: kilts, denim overalls, a tux and a pool party (the catcher keeps his gear)
            Dress(t.lineup[0], Top.Tank, Bottom.Shorts, Hat.Cap, Feet.FlipFlops, Extra.Wings | Extra.Snorkel, 0, "번개 발 · 스노클 수영복");
            Dress(t.lineup[1], Top.Jersey, Bottom.Kilt, Hat.Cap, Feet.Cleats, Extra.None, 0, "2미터 장다리 · 킬트");
            Dress(t.lineup[2], Top.Aloha, Bottom.Shorts, Hat.Straw, Feet.FlipFlops, Extra.Lei, 0, "말아올린 콧수염 · 알로하");
            Dress(t.lineup[3], Top.Jersey, Bottom.Overalls, Hat.Cap, Feet.Cleats, Extra.None, 0, "바이킹 수염 · 멜빵바지");
            Dress(t.lineup[4], Top.Jersey, Bottom.Pants, Hat.Cap, Feet.Cleats, Extra.Floatie, 0, "튜브 낀 4번 고릴라");
            t.lineup[4].body.gear = Gear.Shades;
            Dress(t.lineup[5], Top.Aloha, Bottom.Shorts, Hat.Bucket, Feet.Cleats, Extra.None, 1, "구레나룻 · 버킷햇");
            Dress(t.lineup[6], Top.Jersey, Bottom.Overalls, Hat.Cap, Feet.Cleats, Extra.Bandana, 1, "아프로 · 왼손 마법사");
            Dress(t.lineup[7], Top.Tux, Bottom.Pants, Hat.Bowler, Feet.Cleats, Extra.BowTie, 0, "외알 안경 · 노란 턱시도");
            Dress(t.pitcher, Top.Jersey, Bottom.Kilt, Hat.Cap, Feet.Cleats, Extra.None, 0, "콧수염 투구폼 · 킬트");
            Finish(t);
            return t;
        }

        /// <summary>Put a player in a costume (trait: his new one-line hook, or null to keep the old one).</summary>
        static void Dress(PlayerData p, Top top, Bottom bottom, Hat hat, Feet feet, Extra extra, int style, string trait = null)
        {
            p.body.top = top;
            p.body.bottom = bottom;
            p.body.hat = hat;
            p.body.feet = feet;
            p.body.extra = extra;
            p.body.style = style;
            if (bottom == Bottom.Kilt) p.body.highSocks = true;    // kilt hose
            if (top == Top.Tux || top == Top.FireCoat || top == Top.Tank || top == Top.Aloha) p.body.sleeves = false;
            if ((extra & Extra.Snorkel) != 0) p.body.gear = Gear.Goggles;
            if (trait != null) p.trait = trait;
        }

        /// <summary>Each rival club has its own dress-up theme (after the Banana Ball clubs); every player draws his own pieces of it.
        /// A separate random stream, so the players themselves (names, bodies, numbers) stay the same.</summary>
        static void Theme(TeamData t, int which, int seed)
        {
            var r = new System.Random(seed * 3571 + which * 977 + 11);
            var list = new List<PlayerData>(t.lineup) { t.pitcher };
            bool Chance(float p) => r.NextDouble() < p;
            foreach (var p in list)
            {
                Top top = Top.Jersey; Bottom bot = Bottom.Pants; Hat hat = Hat.Cap; Feet feet = Feet.Cleats; Extra ex = Extra.None;
                int style = r.Next(4);
                switch (which)
                {
                    case 0:   // kilt night (green tartan), bucket hats
                        bot = Chance(0.7f) ? Bottom.Kilt : Bottom.Pants;
                        if (Chance(0.2f)) top = Top.Tank;
                        if (Chance(0.35f)) hat = Hat.Bucket;
                        if (Chance(0.2f)) ex |= Extra.Bandana;
                        break;
                    case 1:   // showtime: tuxedos and bowlers, tank tops and shades
                        if (Chance(0.5f)) { top = Top.Tux; ex |= Extra.BowTie; if (Chance(0.65f)) hat = Hat.Bowler; }
                        else if (Chance(0.5f)) { top = Top.Tank; if (Chance(0.5f)) bot = Bottom.Shorts; if (Chance(0.6f)) p.body.gear = Gear.Shades; }
                        break;
                    case 2:   // tailgate: overalls, cowboy hats, neckerchiefs, boots
                        if (Chance(0.6f)) bot = Bottom.Overalls;
                        if (Chance(0.15f)) top = Top.Tank;
                        if (Chance(0.6f)) hat = Hat.Cowboy;
                        if (Chance(0.5f)) ex |= Extra.Bandana;
                        if (Chance(0.55f)) feet = Feet.Boots;
                        break;
                    case 3:   // pool party: aloha shirts, board shorts, flip-flops, rings, leis, water wings, snorkels
                        top = Chance(0.55f) ? Top.Aloha : Chance(0.7f) ? Top.Tank : Top.Jersey;
                        bot = Chance(0.85f) ? Bottom.Shorts : Bottom.Pants;
                        if (bot == Bottom.Shorts && Chance(0.65f)) feet = Feet.FlipFlops;
                        hat = Chance(0.3f) ? Hat.Straw : Chance(0.4f) ? Hat.Bucket : Hat.Cap;
                        if (Chance(0.25f)) ex |= Extra.Floatie;
                        if (top == Top.Aloha && Chance(0.45f)) ex |= Extra.Lei;
                        if (top == Top.Tank && Chance(0.4f)) ex |= Extra.Wings;
                        if (Chance(0.2f)) ex |= Extra.Snorkel;
                        else if (Chance(0.3f)) p.body.gear = Gear.Shades;
                        break;
                    default:  // the fire crew: turnout coats, fire helmets, boots
                        if (Chance(0.75f)) top = Top.FireCoat;
                        if (Chance(0.8f)) hat = Hat.Fire;
                        if (Chance(0.8f)) feet = Feet.Boots;
                        if (top != Top.FireCoat && Chance(0.3f)) ex |= Extra.Bandana;
                        break;
                }
                Dress(p, top, bot, hat, feet, ex, style);
                p.trait = TraitOf(p.body);
            }
        }

        public const int RivalCount = 5;

        public static TeamData Rival(int which, int seed)
        {
            which = ((which % RivalCount) + RivalCount) % RivalCount;
            var t = new TeamData { index = 1 };
            switch (which)
            {
                case 0:
                    t.name = "Pickle Pirates"; t.nameKr = "피클 파이리츠"; t.abbr = "PKL"; t.city = "Brineport";
                    t.main = Pal.Hex(0x3E9B3A); t.accent = Pal.Hex(0x1B1B20); t.dark = Pal.Hex(0x0F2A12);
                    t.jersey = Pal.Hex(0xBFC3C2); t.pants = Pal.Hex(0xB5B9B8); t.stripe = t.main;
                    break;
                case 1:
                    t.name = "Thunder Llamas"; t.nameKr = "썬더 라마스"; t.abbr = "TLM"; t.city = "Cloud Peak";
                    t.main = Pal.Hex(0x6C3FB5); t.accent = Pal.Hex(0xFF8C30); t.dark = Pal.Hex(0x2A1450);
                    t.jersey = Pal.Hex(0x6C3FB5); t.pants = Pal.Hex(0xE9E6EE); t.stripe = t.accent;
                    break;
                case 2:
                    t.name = "Hot Dog Hounds"; t.nameKr = "핫도그 하운즈"; t.abbr = "HDH"; t.city = "Mustard City";
                    t.main = Pal.Hex(0xD7263D); t.accent = Pal.Hex(0xF4C430); t.dark = Pal.Hex(0x5A0F18);
                    t.jersey = Pal.Hex(0xC7CACC); t.pants = Pal.Hex(0xBEC1C4); t.stripe = t.main;
                    break;
                case 3:
                    // (the old Space Walruses, now a pool-party club)
                    t.name = "Splash Walruses"; t.nameKr = "스플래시 월러스"; t.abbr = "SPW"; t.city = "Lagoon Bay";
                    t.main = Pal.Hex(0x19A090); t.accent = Pal.Hex(0xD5DEE4); t.dark = Pal.Hex(0x0B2B3A);
                    t.jersey = Pal.Hex(0x10304A); t.pants = Pal.Hex(0xD9E0E6); t.stripe = t.main;
                    break;
                default:
                    // the fire crew: red helmets, turnout coats, and dalmatian-spotted jerseys under them
                    t.name = "Blaze Dalmatians"; t.nameKr = "블레이즈 달마시안스"; t.abbr = "BLZ"; t.city = "Engine Hill";
                    t.main = Pal.Hex(0xD9302B); t.accent = Pal.Hex(0x17171B); t.dark = Pal.Hex(0x5E1210);
                    t.jersey = Pal.Hex(0xF4F1EA); t.pants = Pal.Hex(0x2A2A30); t.stripe = t.main; t.spots = true;
                    break;
            }
            var r = new System.Random(seed * 7919 + which * 131);
            Pos[] order = { Pos.CF, Pos.SS, Pos.B2, Pos.B1, Pos.DH, Pos.B3, Pos.RF, Pos.LF };
            var used = new HashSet<string>();
            var nums = new HashSet<int>();
            for (int i = 0; i < 8; i++)
            {
                float powBias = i == 3 || i == 4 ? 1f : i == 2 || i == 5 ? 0.6f : 0.25f;
                float spdBias = i == 0 || i == 1 ? 1f : i == 7 ? 0.6f : 0.3f;
                int con = Mathf.RoundToInt(Mathf.Clamp(60f + MathX.Gauss(r) * 9f + (1f - powBias) * 10f, 42, 95));
                int pow = Mathf.RoundToInt(Mathf.Clamp(50f + powBias * 38f + MathX.Gauss(r) * 7f, 35, 99));
                int spd = Mathf.RoundToInt(Mathf.Clamp(45f + spdBias * 42f + MathX.Gauss(r) * 8f, 30, 98));
                int eye = Mathf.RoundToInt(Mathf.Clamp(62f + MathX.Gauss(r) * 10f, 40, 95));
                var body = RandomBody(r, powBias, spdBias);
                var pl = P(UniqueName(r, used), "", UniqueNumber(r, nums), order[i], r.NextDouble() < 0.3 ? Hand.Left : Hand.Right, r.NextDouble() < 0.15 ? Hand.Left : Hand.Right, con, pow, spd, eye, body);
                pl.trait = TraitOf(body);
                t.lineup[i] = pl;
            }
            var pb = RandomBody(r, 0.5f, 0.3f);
            pb.height = Mathf.Max(pb.height, 1.86f);
            var p = P(UniqueName(r, used), "", UniqueNumber(r, nums), Pos.P, Hand.Right, r.NextDouble() < 0.35 ? Hand.Left : Hand.Right, 40, 40, 45, 40, pb);
            p.trait = TraitOf(pb);
            p.vel = Mathf.RoundToInt(Mathf.Clamp(80f + MathX.Gauss(r) * 7f, 64, 97));
            p.ctl = Mathf.RoundToInt(Mathf.Clamp(74f + MathX.Gauss(r) * 8f, 55, 95));
            p.mov = Mathf.RoundToInt(Mathf.Clamp(76f + MathX.Gauss(r) * 8f, 55, 96));
            p.sta = Mathf.RoundToInt(Mathf.Clamp(75f + MathX.Gauss(r) * 8f, 55, 95));
            PitchType[][] sets =
            {
                new[] { PitchType.Four, PitchType.Slider, PitchType.Change },
                new[] { PitchType.Sinker, PitchType.Curve, PitchType.Splitter },
                new[] { PitchType.Four, PitchType.Curve, PitchType.Change },
                new[] { PitchType.Four, PitchType.Splitter, PitchType.Slider },
                new[] { PitchType.Four, PitchType.Sinker, PitchType.Curve },
            };
            PitchType[] magics = { PitchType.Balloon, PitchType.Phantom, PitchType.Missile, PitchType.Stop, PitchType.Missile };
            p.pitches = sets[which];
            p.magic = magics[which];
            if (which == 1)
            {
                // the Llamas' ace is a gorilla (no extra draws, so everyone else stays the same)
                p.name = "Grumbo";
                p.body = Ape(2.0f, 1.45f, 0.35f);
                p.trait = TraitOf(p.body);
                p.vel = Mathf.Max(p.vel, 94);
            }
            t.pitcher = p;
            // the catcher is drawn last, so every other player of a club stays the same as before he existed
            var cb = RandomBody(r, 0.8f, 0f);
            cb.width = Mathf.Max(cb.width, 1.08f);
            cb.belly = Mathf.Max(cb.belly, 0.35f);
            var c = P(UniqueName(r, used), "", UniqueNumber(r, nums), Pos.C, Hand.Right, Hand.Right, 50, Mathf.RoundToInt(Mathf.Clamp(76f + MathX.Gauss(r) * 8f, 60, 97)), Mathf.RoundToInt(Mathf.Clamp(38f + MathX.Gauss(r) * 6f, 30, 60)), 50, cb);
            c.trait = TraitOf(cb);
            t.catcher = c;
            Theme(t, which, seed);
            Finish(t);
            return t;
        }

        static readonly string[] First =
        {
            "Bubba", "Moose", "Ziggy", "Rocco", "Buster", "Duke", "Skip", "Hank", "Rusty", "Boomer", "Waldo", "Gus", "Chip", "Jumbo",
            "Dash", "Noodle", "Bingo", "Pepper", "Biff", "Sully", "Tank", "Dutch", "Flip", "Ace", "Bronco", "Clem", "Elmo", "Ferris"
        };

        static readonly string[] Last =
        {
            "McBonk", "Wallop", "Dingerman", "Bunterson", "O'Whiff", "Sluggo", "Twister", "Crackins", "Zoomer", "Pickleton",
            "Muffinsky", "Fastowski", "Curvelli", "Thumpwell", "Kaboomski", "Splatt", "Wobbleton", "Fumbleby", "Bopper", "Zapata", "Grillo", "Spinner"
        };

        static string UniqueName(System.Random r, HashSet<string> used)
        {
            for (int k = 0; k < 40; k++)
            {
                string n = MathX.Pick(r, First) + " " + MathX.Pick(r, Last);
                string ln = n.Substring(n.IndexOf(' ') + 1);
                if (used.Add(ln)) return n;
            }
            return MathX.Pick(r, First) + " Jr.";
        }

        static int UniqueNumber(System.Random r, HashSet<int> used)
        {
            for (int k = 0; k < 40; k++) { int n = r.Next(1, 72); if (used.Add(n)) return n; }
            return 99;
        }

        static Body RandomBody(System.Random r, float powBias, float spdBias)
        {
            var b = new Body
            {
                height = Mathf.Clamp(1.84f + MathX.Gauss(r) * 0.05f + powBias * 0.03f, 1.72f, 1.96f),
                width = Mathf.Clamp(0.98f + powBias * 0.12f - spdBias * 0.06f + MathX.Gauss(r) * 0.04f, 0.86f, 1.2f),
                belly = Mathf.Clamp01((float)r.NextDouble() * 0.5f + powBias * 0.35f - spdBias * 0.4f),
                muscle = Mathf.Clamp01(0.3f + powBias * 0.5f + MathX.Gauss(r) * 0.15f),
                head = Mathf.Clamp(1.03f + MathX.Gauss(r) * 0.04f, 0.98f, 1.14f),
                nose = Mathf.Clamp(1f + MathX.Gauss(r) * 0.22f, 0.7f, 1.7f),
                ears = Mathf.Clamp(1f + MathX.Gauss(r) * 0.2f, 0.75f, 1.6f),
                chin = Mathf.Clamp(1f + MathX.Gauss(r) * 0.15f, 0.8f, 1.4f),
                brow = Mathf.Clamp(1f + MathX.Gauss(r) * 0.3f, 0.7f, 2f),
                skin = MathX.Pick(r, Pal.Skins),
                hairColor = MathX.Pick(r, Pal.Hairs),
                hair = (Hair)r.Next(8),
                beard = (Beard)r.Next(9),
                gear = r.NextDouble() < 0.55 ? (Gear)r.Next(1, 6) : Gear.None,
                sleeves = r.NextDouble() < 0.4,
                highSocks = r.NextDouble() < 0.45,
            };
            if (b.beard == Beard.Viking && b.hair == Hair.Bald) b.hair = Hair.Long;
            return b;
        }

        static string TraitOf(Body b)
        {
            if (b.ape) return b.top == Top.Tux ? "턱시도 고릴라" : "고릴라 · 괴력";
            if (b.top == Top.FireCoat) return b.hat == Hat.Fire ? "출동 소방관" : "방화복 강타자";
            if (b.top == Top.Tux) return b.hat == Hat.Bowler ? "중절모 턱시도" : "턱시도 신사";
            if (b.Has(Extra.Floatie)) return "튜브 장착";
            if (b.Has(Extra.Snorkel)) return "스노클 잠수부";
            if (b.Has(Extra.Wings)) return "팔 튜브 수영왕";
            if (b.top == Top.Aloha) return b.Has(Extra.Lei) ? "알로하 · 꽃목걸이" : "알로하 셔츠";
            if (b.bottom == Bottom.Kilt) return "킬트 차림";
            if (b.bottom == Bottom.Overalls) return b.hat == Hat.Cowboy ? "카우보이 멜빵" : "멜빵바지";
            if (b.hat == Hat.Cowboy) return "카우보이 모자";
            if (b.top == Top.Tank) return "민소매 근육";
            if (b.hat == Hat.Straw) return "밀짚모자";
            if (b.hat == Hat.Bucket) return "버킷햇";
            if (b.gear == Gear.Monocle) return "외알 안경 신사";
            if (b.beard == Beard.Viking) return "바이킹 수염";
            if (b.beard == Beard.Handlebar) return "말아올린 콧수염";
            if (b.beard == Beard.Walrus) return "바다코끼리 콧수염";
            if (b.hair == Hair.Afro) return "아프로 헬멧";
            if (b.hair == Hair.Mohawk) return "헬멧 뚫은 모히칸";
            if (b.hair == Hair.Mullet) return "뒷머리 장인";
            if (b.belly > 0.6f) return "든든한 맥주배";
            if (b.height > 1.97f) return "장다리";
            if (b.height < 1.72f) return "꼬마 거인";
            if (b.gear == Gear.Goggles) return "수영 고글";
            if (b.gear == Gear.Shades) return "선글라스 허세";
            if (b.beard == Beard.Chops) return "구레나룻 장인";
            if (b.nose > 1.35f) return "왕코";
            if (b.ears > 1.3f) return "부채 귀";
            if (b.brow > 1.5f) return "짙은 눈썹";
            return "평범해서 수상함";
        }

        static PlayerData P(string name, string trait, int num, Pos pos, Hand bats, Hand throws, int con, int pow, int spd, int eye, Body body) =>
            new PlayerData { name = name, trait = trait, number = num, pos = pos, bats = bats, throws = throws, con = con, pow = pow, spd = spd, eye = eye, body = body };

        /// <summary>A gorilla in uniform.</summary>
        static Body Ape(float h, float w, float belly) =>
            new Body
            {
                height = h, width = w, belly = belly, muscle = 1f, head = 0.96f, nose = 1f, ears = 1f, chin = 1.1f, brow = 1f,
                skin = Pal.Hex(0x5E5450), hairColor = Pal.Hex(0x2B2A2E), hair = Hair.Bald, beard = Beard.None, gear = Gear.None,
                sleeves = false, highSocks = false, ape = true
            };

        static Body B(float h, float w, float belly, float muscle, float head, Hair hair, Beard beard, Gear gear, int skin, int hairCol, bool sleeves, bool socks) =>
            new Body
            {
                height = h, width = w, belly = belly, muscle = muscle, head = head, nose = 1f, ears = 1f, chin = 1f, brow = 1f,
                skin = Pal.Skins[skin % Pal.Skins.Length], hairColor = Pal.Hairs[hairCol % Pal.Hairs.Length],
                hair = hair, beard = beard, gear = gear, sleeves = sleeves, highSocks = socks
            };

        static void Finish(TeamData t)
        {
            foreach (var p in t.lineup) p.team = t.index;
            t.pitcher.team = t.index;
            if (t.catcher != null) t.catcher.team = t.index;
            if (t.index == 0)
            {
                // a few hand-tuned faces
                t.lineup[3].body.nose = 1.35f; t.lineup[3].body.brow = 1.6f;
                t.lineup[7].body.ears = 1.45f; t.lineup[7].body.nose = 1.2f;
                t.lineup[1].body.chin = 1.35f; t.lineup[1].body.ears = 1.25f;
                t.lineup[5].body.brow = 1.8f;
                t.pitcher.body.brow = 1.5f; t.pitcher.body.chin = 1.25f;
            }
        }
    }
}
