using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace BananaBall
{
    /// <summary>
    /// The ballpark, built in code at real scale: mowed grass, dirt skin, chalk, bases, padded walls with ads, the backstop net,
    /// a two-deck bowl with roof and light towers, outfield bleachers, a video scoreboard, a skyline, ~6000 bobbing fans and
    /// two wacky inflatable tube men on the dugout roofs.
    /// </summary>
    public class Field
    {
        public readonly Transform root;
        public readonly Mesh shadowMesh, quadMesh, boardMesh, ballMesh;
        public readonly Transform ballShadow, landRing, aimRing;
        public readonly Transform[] baseRings = new Transform[4];
        readonly Mats M;
        readonly TMP_FontAsset font;
        TextMeshPro sbAway, sbHome, sbAwayR, sbHomeR, sbInning, sbMsg, sbCount, sbBatter;
        readonly List<TubeMan> tubeMen = new List<TubeMan>();
        List<Vector3> roofEdge;
        float msgT, excite, exciteT;
        static readonly int ExciteId = Shader.PropertyToID("_BBCrowdExcite");

        static readonly string[] Ads =
        {
            "BONK COLA", "WHIFF CHIPS", "DINGER DOGS", "SLIDE-O GUM", "MOOSE MOTORS", "ZIGZAG TIRES", "CURVEBALL BANK",
            "PICKLE PALACE", "SPLAT PAINT", "FOUL POLE AIR", "NOODLE NOODLES", "BOOM INSURANCE", "WACKY WATER", "MEGA MART"
        };

        static readonly int[] AdBg = { 0xD7263D, 0x1C2A55, 0xF6C928, 0x2BAE66, 0xF08A24, 0x6C3FB5, 0x19A090, 0xFFFFFF, 0x14161C, 0xE8453C, 0x3C8BE8, 0xFF7EB6 };

        public Field(Mats m, TMP_FontAsset fontAsset, TeamData home, TeamData away)
        {
            M = m;
            font = fontAsset;
            root = new GameObject("Ballpark").transform;
            shadowMesh = Shapes.Quad("BB_Shadow");
            quadMesh = Shapes.Quad("BB_Quad");
            boardMesh = Shapes.Billboard();
            ballMesh = Shapes.BallSeamed();

            BuildOutside();
            BuildField();
            BuildWalls();
            BuildStands(home, away);
            BuildScoreboard(home, away);
            BuildCenterBoard(home, away);
            BuildBillboards();
            BuildLights();
            BuildSkyline();
            BuildTubeMen(home, away);

            ballShadow = Marker("BallShadow", m.Unlit(new Color(0.02f, 0.03f, 0.02f, 0.55f), m.soft, false, 2960), 0.5f);
            landRing = Marker("LandRing", m.Unlit(new Color(1f, 0.92f, 0.25f, 0.85f), m.ring, false, 2970), 2.2f);
            aimRing = Marker("AimRing", m.Unlit(new Color(1f, 1f, 1f, 0.9f), m.ring, false, 2971), 1.4f);
            landRing.gameObject.SetActive(false);
            aimRing.gameObject.SetActive(false);
            for (int i = 0; i < 4; i++)
            {
                baseRings[i] = Marker("BaseRing" + i, m.Unlit(new Color(1f, 0.85f, 0.2f, 0.9f), m.ring, false, 2972 + i), 2.4f);
                baseRings[i].position = Geo.Base(i) + Vector3.up * 0.09f;
                baseRings[i].gameObject.SetActive(false);
            }
        }

        Transform Marker(string n, Material mat, float size)
        {
            var go = new GameObject(n);
            go.transform.SetParent(root, false);
            go.AddComponent<MeshFilter>().sharedMesh = quadMesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            go.transform.localScale = new Vector3(size, 1f, size);
            return go.transform;
        }

        GameObject Add(string n, Mesh mesh, Material mat, bool cast = true, bool receive = true)
        {
            var go = new GameObject(n);
            go.transform.SetParent(root, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = cast ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = receive;
            return go;
        }

        // ------------------------------------------------------------------ ground and field

        void BuildOutside()
        {
            var k = new MeshKit();
            k.Disc(new Vector3(0f, -0.06f, 40f), 1400f, Pal.Hex(0x55654A), 48);
            Add("Outside", k.ToMesh("BB_Outside"), M.ground, false, true);
        }

        void BuildField()
        {
            var g = new MeshKit();
            // grass everywhere inside the walls (uv2.x = 1: mowing pattern)
            g.extra = new Vector2(1f, 0f);
            var bnd = Geo.Boundary(64, 30, 24);
            g.Fan(new Vector3(0f, 0f, 30f), bnd, Pal.Grass);

            // warning track along every wall
            g.extra = Vector2.zero;
            var inner = Offset(bnd, 4.6f, true);
            for (int i = 0; i < bnd.Count; i++)
            {
                int j = (i + 1) % bnd.Count;
                Vector3 up = Vector3.up * 0.012f;
                UpQuad(g, bnd[i] + up, bnd[j] + up, inner[j] + up, inner[i] + up, Pal.Track);
            }

            // infield skin (dirt): star-shaped round the rubber
            var skin = new List<Vector3>();
            Vector3 rc = Geo.Rubber;
            skin.Add(new Vector3(0f, 0f, 3.2f));
            skin.Add(new Vector3(Geo.H + 3.4f, 0f, Geo.H - 3.4f));
            for (int i = 0; i <= 40; i++)
            {
                float a = Mathf.Lerp(72f, -72f, i / 40f) * Mathf.Deg2Rad;
                skin.Add(rc + new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * Geo.SkinR);
            }
            skin.Add(new Vector3(-Geo.H - 3.4f, 0f, Geo.H - 3.4f));
            Lift(skin, 0.02f);
            g.Fan(rc + Vector3.up * 0.02f, skin, Pal.Dirt);

            // infield grass square inside the base paths
            g.extra = new Vector2(1f, 0f);
            float ins = 1.1f * Mathf.Sqrt(2f);
            var sq = new List<Vector3>
            {
                new Vector3(0f, 0.03f, ins), new Vector3(Geo.H - ins, 0.03f, Geo.H), new Vector3(0f, 0.03f, 2f * Geo.H - ins), new Vector3(-Geo.H + ins, 0.03f, Geo.H)
            };
            g.Fan(new Vector3(0f, 0.03f, Geo.H), RoundPoly(sq, 1.2f, 4), Pal.Grass2);
            g.extra = Vector2.zero;

            // base paths home-first and third-home, cut-outs round the bases, home circle, mound
            for (int sd = -1; sd <= 1; sd += 2)
            {
                Vector3 a = Geo.FromLine(sd, 2.5f, 0f, 0.04f), b = Geo.FromLine(sd, Geo.BaseDist - 1.5f, 0f, 0.04f);
                Vector3 n = Geo.FromLine(sd, 0f, 0.95f) - Geo.FromLine(sd, 0f, 0f);
                UpQuad(g, a - n, b - n, b + n, a + n, Pal.Dirt);
            }
            for (int bi = 1; bi <= 3; bi++) g.Disc(Geo.Base(bi) + Vector3.up * 0.045f, 2.75f, Pal.Dirt, 36);
            g.Disc(new Vector3(0f, 0.05f, 0f), 3.96f, Pal.Dirt, 48);
            Mound(g);
            Add("FieldGround", g.ToMesh("BB_FieldGround"), M.ground, false, true);

            // chalk
            var ch = new MeshKit();
            Color chalk = Pal.Gloss(Pal.Chalk, 0.1f);
            for (int sd = -1; sd <= 1; sd += 2)
                ch.Strip(Geo.FromLine(sd, 0.33f, 0f, 0.062f), Geo.FromLine(sd, Geo.FenceLine - 0.2f, 0f, 0.062f), 0.1f, chalk);
            for (int sd = -1; sd <= 1; sd += 2)
            {
                float x0 = sd * 0.37f, x1 = sd * (0.37f + 1.22f);
                const float z0 = -0.92f, z1 = 0.92f;
                ch.Strip(new Vector3(x0, 0.062f, z0), new Vector3(x0, 0.062f, z1), 0.07f, chalk);
                ch.Strip(new Vector3(x1, 0.062f, z0), new Vector3(x1, 0.062f, z1), 0.07f, chalk);
                ch.Strip(new Vector3(x0, 0.062f, z1), new Vector3(x1, 0.062f, z1), 0.07f, chalk);
                ch.Strip(new Vector3(x0, 0.062f, z0), new Vector3(x1, 0.062f, z0), 0.07f, chalk);
            }
            ch.Ring(Geo.OnDeckL + Vector3.up * 0.062f, 0.66f, 0.76f, chalk, 32);
            ch.Ring(Geo.OnDeckR + Vector3.up * 0.062f, 0.66f, 0.76f, chalk, 32);
            Add("Chalk", ch.ToMesh("BB_Chalk"), M.ground, false, true);

            // bases, home plate, rubber
            var pk = new MeshKit();
            Color bag = Pal.Gloss(new Color(0.97f, 0.97f, 0.95f), 0.35f);
            for (int bi = 1; bi <= 3; bi++)
                pk.Box(Geo.Base(bi) + new Vector3(0f, 0.06f, 0f), new Vector3(0.38f, 0.09f, 0.38f), Quaternion.Euler(0f, 45f, 0f), bag);
            var plate = new List<Vector3>
            {
                new Vector3(-0.216f, 0.075f, 0.1f), new Vector3(0.216f, 0.075f, 0.1f), new Vector3(0.216f, 0.075f, -0.116f),
                new Vector3(0f, 0.075f, -0.332f), new Vector3(-0.216f, 0.075f, -0.116f)
            };
            var rim = new List<Vector3>();
            foreach (var p in plate) rim.Add(new Vector3(p.x * 1.12f, 0.07f, (p.z + 0.116f) * 1.1f - 0.116f));
            pk.Fan(new Vector3(0f, 0.07f, -0.08f), rim, Pal.Gloss(new Color(0.1f, 0.1f, 0.1f), 0.2f));
            pk.Fan(new Vector3(0f, 0.075f, -0.08f), plate, bag);
            pk.Box(new Vector3(0f, Geo.MoundH + 0.03f, Geo.RubberZ), new Vector3(0.61f, 0.05f, 0.15f), Quaternion.identity, bag);
            Add("Bases", pk.ToMesh("BB_Bases"), M.prop, true, true);
        }

        void Mound(MeshKit g)
        {
            int s0 = g.V.Count;
            const int seg = 40, rings = 8;
            for (int i = 0; i <= rings; i++)
            {
                float r = Geo.MoundR * i / rings;
                for (int j = 0; j <= seg; j++)
                {
                    float a = Mathf.PI * 2f * j / seg;
                    Vector3 p = Geo.Rubber + new Vector3(Mathf.Sin(a) * r, 0f, Mathf.Cos(a) * r);
                    p.y = Geo.GroundY(p) + 0.055f;
                    float slope = 2f * Geo.MoundH * (r / Geo.MoundR) / Geo.MoundR;
                    Vector3 n = new Vector3(Mathf.Sin(a) * slope, 1f, Mathf.Cos(a) * slope).normalized;
                    g.Vert(p, n, Pal.Dirt, MeshKit.Plain);
                }
            }
            for (int i = 0; i < rings; i++)
                for (int j = 0; j < seg; j++)
                {
                    int a = s0 + i * (seg + 1) + j, b = a + seg + 1;
                    g.T.Add(a); g.T.Add(b); g.T.Add(a + 1);
                    g.T.Add(a + 1); g.T.Add(b); g.T.Add(b + 1);
                }
        }

        static void Lift(List<Vector3> pts, float y) { for (int i = 0; i < pts.Count; i++) pts[i] = new Vector3(pts[i].x, y, pts[i].z); }

        static List<Vector3> RoundPoly(List<Vector3> poly, float r, int steps)
        {
            var o = new List<Vector3>();
            int n = poly.Count;
            for (int i = 0; i < n; i++)
            {
                Vector3 p = poly[i], a = poly[(i + n - 1) % n], b = poly[(i + 1) % n];
                Vector3 p0 = p + (a - p).normalized * r, p1 = p + (b - p).normalized * r;
                for (int k = 0; k <= steps; k++)
                {
                    float t = k / (float)steps;
                    o.Add(Vector3.Lerp(Vector3.Lerp(p0, p, t), Vector3.Lerp(p, p1, t), t));
                }
            }
            return o;
        }

        /// <summary>Offsets a counter-clockwise (seen from above) polygon inward or outward.</summary>
        static List<Vector3> Offset(List<Vector3> pts, float d, bool inward)
        {
            var o = new List<Vector3>(pts.Count);
            int n = pts.Count;
            for (int i = 0; i < n; i++)
            {
                Vector3 a = pts[(i + n - 1) % n], p = pts[i], b = pts[(i + 1) % n];
                Vector3 t0 = p - a; t0.y = 0f; t0.Normalize();
                Vector3 t1 = b - p; t1.y = 0f; t1.Normalize();
                Vector3 n0 = new Vector3(-t0.z, 0f, t0.x), n1 = new Vector3(-t1.z, 0f, t1.x);
                Vector3 nn = n0 + n1;
                if (nn.sqrMagnitude < 1e-6f) nn = n1;
                nn.Normalize();
                float cos = Mathf.Max(0.35f, Vector3.Dot(nn, n1));
                o.Add(p + nn * (inward ? d : -d) / cos);
            }
            return o;
        }

        static void UpQuad(MeshKit k, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color col)
        {
            int i0 = k.Vert(a, Vector3.up, col, MeshKit.Plain), i1 = k.Vert(b, Vector3.up, col, MeshKit.Plain), i2 = k.Vert(c, Vector3.up, col, MeshKit.Plain), i3 = k.Vert(d, Vector3.up, col, MeshKit.Plain);
            Vector3 n = Vector3.Cross(b - a, c - a);
            if (n.y >= 0f) { k.T.Add(i0); k.T.Add(i1); k.T.Add(i2); k.T.Add(i0); k.T.Add(i2); k.T.Add(i3); }
            else { k.T.Add(i0); k.T.Add(i2); k.T.Add(i1); k.T.Add(i0); k.T.Add(i3); k.T.Add(i2); }
        }

        /// <summary>Vertical quad from ground points a to b between heights y0 and y1, facing 'face'.</summary>
        static void Wall(MeshKit k, Vector3 a, Vector3 b, float y0, float y1, Vector3 face, Color col)
        {
            Vector3 p0 = new Vector3(a.x, y0, a.z), p1 = new Vector3(b.x, y0, b.z), p2 = new Vector3(b.x, y1, b.z), p3 = new Vector3(a.x, y1, a.z);
            Slab(k, p0, p1, p3, p2, face, col);
        }

        /// <summary>Quad a0-b0-b1-a1 with normal n (winding fixed to face n).</summary>
        static void Slab(MeshKit k, Vector3 a0, Vector3 b0, Vector3 a1, Vector3 b1, Vector3 n, Color col)
        {
            n.Normalize();
            int i0 = k.Vert(a0, n, col, MeshKit.Plain), i1 = k.Vert(b0, n, col, MeshKit.Plain), i2 = k.Vert(b1, n, col, MeshKit.Plain), i3 = k.Vert(a1, n, col, MeshKit.Plain);
            Vector3 fn = Vector3.Cross(b0 - a0, b1 - a0);
            if (Vector3.Dot(fn, n) >= 0f) { k.T.Add(i0); k.T.Add(i1); k.T.Add(i2); k.T.Add(i0); k.T.Add(i2); k.T.Add(i3); }
            else { k.T.Add(i0); k.T.Add(i2); k.T.Add(i1); k.T.Add(i0); k.T.Add(i3); k.T.Add(i2); }
        }

        // ------------------------------------------------------------------ walls, net, poles, ads

        void BuildWalls()
        {
            var k = new MeshKit();
            Color pad = Pal.Gloss(Pal.Wall, 0.25f), cap = Pal.Gloss(Pal.Hex(0xF6C928), 0.4f), foulPad = Pal.Gloss(Pal.Pad, 0.25f);
            const int fs = 64;
            for (int i = 0; i < fs; i++)
            {
                float a0 = Mathf.Lerp(-45f, 45f, i / (float)fs), a1 = Mathf.Lerp(-45f, 45f, (i + 1) / (float)fs);
                Vector3 p0 = Geo.Dir(a0) * Geo.FenceR(a0), p1 = Geo.Dir(a1) * Geo.FenceR(a1);
                float h = Mathf.Max(Geo.FenceH(a0), Geo.FenceH(a1));
                Vector3 inward = -(p0 + p1);
                Wall(k, p0, p1, 0f, h, inward, pad);
                Vector3 q0 = p0 + p0.normalized * 0.5f, q1 = p1 + p1.normalized * 0.5f;
                Slab(k, new Vector3(p0.x, h, p0.z), new Vector3(p1.x, h, p1.z), new Vector3(q0.x, h, q0.z), new Vector3(q1.x, h, q1.z), Vector3.up, cap);
                Wall(k, p0 - p0.normalized * 0.01f, p1 - p1.normalized * 0.01f, h - 0.14f, h, inward, cap);
            }
            var path = FoulPath();
            for (int i = 0; i < path.Count - 1; i++)
            {
                Vector3 a = path[i], b = path[i + 1];
                Vector3 t = b - a;
                Vector3 inward = new Vector3(-t.z, 0f, t.x);
                Wall(k, a, b, 0f, Geo.WallH, inward, foulPad);
                Vector3 o = -inward.normalized * 0.45f;
                Slab(k, new Vector3(a.x, Geo.WallH, a.z), new Vector3(b.x, Geo.WallH, b.z), new Vector3(a.x + o.x, Geo.WallH, a.z + o.z), new Vector3(b.x + o.x, Geo.WallH, b.z + o.z), Vector3.up, Pal.Gloss(Pal.Concrete, 0.2f));
            }
            for (int sd = -1; sd <= 1; sd += 2)
            {
                Vector3 a = Geo.FromLine(sd, Geo.FenceLine, 0f), b = Geo.FromLine(sd, Geo.FenceLine, Geo.WallD(Geo.FenceLine));
                Vector3 inward = Geo.FromLine(sd, -1f, 0f) - Geo.FromLine(sd, 0f, 0f);
                Wall(k, a, b, 0f, Geo.FenceH(45f), inward, pad);
            }
            Add("Walls", k.ToMesh("BB_Walls"), M.prop, true, true);

            // backstop net (fades out along the lines)
            var nk = new MeshKit();
            float len = 0f;
            for (int i = 0; i < path.Count - 1; i++)
            {
                Vector3 a = path[i], b = path[i + 1];
                Geo.LineCoords(a, out _, out float sa, out _);
                Geo.LineCoords(b, out _, out float sb, out _);
                float seg = (b - a).magnitude;
                if (sa < 22f && sb < 22f)
                {
                    float u0 = len / 1.1f, u1 = (len + seg) / 1.1f;
                    float y0 = Geo.WallH, f0 = Mathf.Clamp01((22f - sa) / 6f), f1 = Mathf.Clamp01((22f - sb) / 6f);
                    float ya = y0 + (Geo.NetH - y0) * f0, yb = y0 + (Geo.NetH - y0) * f1;
                    int i0 = nk.Vert(new Vector3(a.x, y0, a.z), Vector3.up, Color.white, new Vector2(u0, 0f));
                    int i1 = nk.Vert(new Vector3(b.x, y0, b.z), Vector3.up, Color.white, new Vector2(u1, 0f));
                    int i2 = nk.Vert(new Vector3(b.x, yb, b.z), Vector3.up, Color.white, new Vector2(u1, (yb - y0) / 1.1f));
                    int i3 = nk.Vert(new Vector3(a.x, ya, a.z), Vector3.up, Color.white, new Vector2(u0, (ya - y0) / 1.1f));
                    nk.T.Add(i0); nk.T.Add(i1); nk.T.Add(i2); nk.T.Add(i0); nk.T.Add(i2); nk.T.Add(i3);
                }
                len += seg;
            }
            Add("BackstopNet", nk.ToMesh("BB_Net"), M.net, false, false);

            var pk = new MeshKit();
            Color post = Pal.Gloss(Pal.Steel, 0.6f), yellow = Pal.Gloss(Pal.Hex(0xF6D02A), 0.5f);
            for (int i = 0; i < path.Count; i += 4)
            {
                Geo.LineCoords(path[i], out _, out float sa, out _);
                if (sa > 20f) continue;
                Vector3 p = path[i] + path[i].normalized * 0.2f;
                pk.Limb(new Vector3(p.x, 0f, p.z), new Vector3(p.x, Geo.NetH + 0.3f, p.z), 0.07f, 0.06f, post, 2, 8);
            }
            for (int sd = -1; sd <= 1; sd += 2)
            {
                Vector3 p = Geo.FromLine(sd, Geo.FenceLine + 0.3f, 0f);
                pk.Limb(new Vector3(p.x, 0f, p.z), new Vector3(p.x, 26f, p.z), 0.16f, 0.13f, yellow, 2, 10);
                Vector3 inF = (Geo.FromLine(sd, 0f, -1f) - Geo.FromLine(sd, 0f, 0f)).normalized;
                Vector3 s0 = p + inF * 0.15f, s1 = p + inF * 0.85f;
                float hb = Geo.FenceH(45f);
                pk.Quad4(new Vector3(s0.x, hb, s0.z), new Vector3(s1.x, hb, s1.z), new Vector3(s1.x, 24f, s1.z), new Vector3(s0.x, 24f, s0.z), yellow, true);
            }
            Add("Poles", pk.ToMesh("BB_Poles"), M.metal, true, true);

            BuildAds();
            BuildDugouts();
        }

        /// <summary>The low wall foot from the left pole round home to the right pole.</summary>
        public static List<Vector3> FoulPath()
        {
            var p = new List<Vector3>();
            const int ls = 30, arc = 24;
            for (int i = ls; i >= 0; i--) { float s = Geo.FenceLine * i / ls; p.Add(Geo.FromLine(-1, s, Geo.WallD(s))); }
            for (int i = 1; i < arc; i++)
            {
                float a = Mathf.Lerp(-135f, -225f, i / (float)arc) * Mathf.Deg2Rad;
                p.Add(new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * Geo.WallD0);
            }
            for (int i = 0; i <= ls; i++) { float s = Geo.FenceLine * i / ls; p.Add(Geo.FromLine(1, s, Geo.WallD(s))); }
            return p;
        }

        void BuildAds()
        {
            int adI = 0;
            for (int i = 0; i < 14; i++)
            {
                float a = Mathf.Lerp(-40f, 40f, i / 13f);
                if (Mathf.Abs(a) < 8f) continue;
                Vector3 p = Geo.Dir(a) * (Geo.FenceR(a) - 0.06f);
                float h = Geo.FenceH(a);
                Panel(p, -p, 9f, h * 0.55f, h * 0.5f, adI++);
            }
            for (int sd = -1; sd <= 1; sd += 2)
                for (int i = 0; i < 4; i++)
                {
                    float s = 6f + i * 16f;
                    Vector3 p = Geo.FromLine(sd, s, Geo.WallD(s) - 0.06f);
                    Vector3 inward = Geo.FromLine(sd, 0f, -1f) - Geo.FromLine(sd, 0f, 0f);
                    Panel(p, inward, 12f, 0.8f, 0.62f, adI++);
                }
            Panel(new Vector3(0f, 0f, -Geo.WallD0 + 0.06f), Vector3.forward, 11f, 0.85f, 0.62f, adI++);
        }

        void Panel(Vector3 foot, Vector3 face, float width, float height, float yCenter, int ad)
        {
            face.y = 0f;
            face.Normalize();
            var go = new GameObject("Ad_" + ad);
            go.transform.SetParent(root, false);
            go.transform.position = foot + Vector3.up * yCenter;
            go.transform.rotation = Quaternion.LookRotation(-face, Vector3.up);
            var k = new MeshKit();
            Color bg = Pal.Hex(AdBg[ad % AdBg.Length]);
            k.extra = new Vector2(0f, 0.35f);
            k.Quad(Vector3.zero, -Vector3.forward, Vector3.up, width * 0.5f, height * 0.5f, Pal.Gloss(bg, 0.5f));
            go.AddComponent<MeshFilter>().sharedMesh = k.ToMesh("BB_Ad");
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = M.glow;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            Color fg = bg.grayscale > 0.6f ? Pal.Ink : Color.white;
            var t = WorldText.Board(go.transform, Ads[ad % Ads.Length], font, fg, new Vector2(width * 0.92f, height * 0.8f), 40f);
            t.transform.localPosition = new Vector3(0f, 0f, -0.02f);
            t.transform.localRotation = Quaternion.identity;
            t.fontStyle = FontStyles.Bold | FontStyles.Italic;
        }

        void BuildDugouts()
        {
            var k = new MeshKit();
            Color dark = Pal.Gloss(Pal.Hex(0x1A1F26), 0.2f), roof = Pal.Gloss(Pal.Hex(0x2B3442), 0.3f), lip = Pal.Gloss(Pal.Hex(0xD8D8D2), 0.3f);
            for (int sd = -1; sd <= 1; sd += 2)
            {
                const float s0 = 12f, s1 = 28f;
                Vector3 a0 = Geo.FromLine(sd, s0, Geo.WallD(s0)), a1 = Geo.FromLine(sd, s1, Geo.WallD(s1));
                Vector3 o = (Geo.FromLine(sd, 0f, 1f) - Geo.FromLine(sd, 0f, 0f)).normalized;
                Vector3 inward = -o;
                Wall(k, a0 + o * 3.2f, a1 + o * 3.2f, -0.8f, 2.4f, inward, dark);
                Slab(k, a0 + Vector3.up * 2.6f + inward * 0.3f, a1 + Vector3.up * 2.6f + inward * 0.3f, a0 + o * 3.4f + Vector3.up * 2.6f, a1 + o * 3.4f + Vector3.up * 2.6f, Vector3.up, roof);
                Wall(k, a0 + inward * 0.3f, a1 + inward * 0.3f, 2.25f, 2.6f, inward, lip);
                Wall(k, a0, a0 + o * 3.4f, 0f, 2.6f, a1 - a0, dark);
                Wall(k, a1, a1 + o * 3.4f, 0f, 2.6f, a0 - a1, dark);
            }
            Add("Dugouts", k.ToMesh("BB_Dugouts"), M.prop, true, true);
        }

        // ------------------------------------------------------------------ stands and crowd

        struct SeatRow { public Vector3 a, b, facing; }

        void BuildStands(TeamData home, TeamData away)
        {
            var path = FoulPath();
            var outw = new List<Vector3>();
            for (int i = 0; i < path.Count; i++)
            {
                Vector3 t = path[Mathf.Min(i + 1, path.Count - 1)] - path[Mathf.Max(i - 1, 0)];
                outw.Add(new Vector3(t.z, 0f, -t.x).normalized);
            }
            var seats = new List<SeatRow>();
            var lower = new MeshKit();
            Tiers(lower, path, outw, 0.45f, Geo.WallH + 0.35f, 26, 0.82f, 0.42f, seats, 1, false);
            float lowBack = 0.45f + 26 * 0.82f, lowTop = Geo.WallH + 0.35f + 25 * 0.42f;
            Band(lower, path, outw, lowBack, lowTop, lowBack + 3.2f, lowTop, Pal.Gloss(Pal.Concrete, 0.15f), true, false);
            Band(lower, path, outw, lowBack + 3.2f, lowTop, lowBack + 3.2f, lowTop + 2.6f, Pal.Gloss(Pal.Hex(0x2A2F38), 0.2f), false, false);
            Add("LowerDeck", lower.ToMesh("BB_LowerDeck"), M.prop, true, true);

            var upper = new MeshKit();
            float upOff = lowBack + 1.2f, upH = lowTop + 5.2f;
            var upSeats = new List<SeatRow>();
            Tiers(upper, path, outw, upOff, upH, 22, 0.9f, 0.6f, upSeats, 2, false);
            upper.extra = new Vector2(0f, 1.6f);
            Band(upper, path, outw, upOff - 0.05f, upH - 2.6f, upOff - 0.05f, upH - 0.4f, Pal.Gloss(home.main, 0.5f), false, false);
            upper.extra = Vector2.zero;
            Band(upper, path, outw, upOff - 0.05f, upH - 0.4f, upOff - 0.05f, upH, Pal.Gloss(Pal.Hex(0xE6E6E0), 0.3f), false, false);
            float upBack = upOff + 22 * 0.9f, upTop = upH + 21 * 0.6f;
            Band(upper, path, outw, upOff, upH - 2.6f, upBack, upTop - 1.5f, Pal.Gloss(Pal.Hex(0x51565F), 0.1f), true, true);
            Band(upper, path, outw, upBack, upTop, upBack, upTop + 8f, Pal.Gloss(Pal.Concrete, 0.15f), false, false);
            Band(upper, path, outw, upOff + 4f, upTop + 8f, upBack + 1.5f, upTop + 8f, Pal.Gloss(Pal.Hex(0x5A6476), 0.25f), true, false);
            Band(upper, path, outw, upOff + 4f, upTop + 8f, upBack + 1.5f, upTop + 8f, Pal.Gloss(Pal.Hex(0x3A4252), 0.2f), true, true);
            Band(upper, path, outw, upOff + 4f, upTop + 6.8f, upOff + 4f, upTop + 8.2f, Pal.Gloss(Pal.Hex(0xEDEDE8), 0.3f), false, false);
            Add("UpperDeck", upper.ToMesh("BB_UpperDeck"), M.prop, true, true);
            roofEdge = new List<Vector3>();
            for (int i = 0; i < path.Count; i += 6) roofEdge.Add(path[i] + outw[i] * (upOff + 4f) + Vector3.up * (upTop + 8.2f));

            // outfield bleachers with the batter's eye in dead centre
            var arc = new List<Vector3>();
            var aout = new List<Vector3>();
            for (int i = 0; i <= 40; i++)
            {
                float a = Mathf.Lerp(45f, -45f, i / 40f);
                arc.Add(Geo.Dir(a) * Geo.FenceR(a));
                aout.Add(Geo.Dir(a));
            }
            var bl = new MeshKit();
            var blSeats = new List<SeatRow>();
            Tiers(bl, arc, aout, 3.2f, 4.2f, 18, 0.85f, 0.55f, blSeats, 1, true);
            Band(bl, arc, aout, 0.6f, 0f, 3.2f, 0f, Pal.Gloss(Pal.Hex(0x3C5A3A), 0.1f), true, false);
            Band(bl, arc, aout, 3.2f, 0f, 3.2f, 4.0f, Pal.Gloss(Pal.Hex(0x2D3A2C), 0.15f), false, false);
            float blBack = 3.2f + 18 * 0.85f, blTop = 4.2f + 17 * 0.55f;
            Band(bl, arc, aout, blBack, blTop, blBack, blTop + 3f, Pal.Gloss(Pal.Concrete, 0.15f), false, false);
            var eye = new MeshKit();
            for (int i = 0; i < 8; i++)
            {
                float a0 = Mathf.Lerp(-7.5f, 7.5f, i / 8f), a1 = Mathf.Lerp(-7.5f, 7.5f, (i + 1) / 8f);
                Vector3 p0 = Geo.Dir(a0) * (Geo.FenceR(a0) + 3.0f), p1 = Geo.Dir(a1) * (Geo.FenceR(a1) + 3.0f);
                Wall(eye, p0, p1, 0f, 15f, -(p0 + p1), Pal.Gloss(Pal.Hex(0x1F3524), 0.1f));
            }
            Add("Bleachers", bl.ToMesh("BB_Bleachers"), M.prop, true, true);
            Add("BattersEye", eye.ToMesh("BB_Eye"), M.prop, true, true);

            BuildCrowd(seats, upSeats, blSeats, home, away);
        }

        /// <summary>Stepped seating rows along a path, extruded along 'outward'. Collects each row's seat line for the crowd.</summary>
        void Tiers(MeshKit k, List<Vector3> path, List<Vector3> outw, float off0, float h0, int rows, float depth, float rise, List<SeatRow> seats, int every, bool skipEye)
        {
            // from the field you mostly see the seat backs: the step faces carry the seat colour too, so the bowl reads blue
            Color stair = Pal.Gloss(Pal.Hex(0xA3A7AD), 0.12f);
            Color sA = Pal.Gloss(Pal.Seat, 0.35f), sB = Pal.Gloss(Pal.Seat2, 0.35f);
            Color rA = Pal.Gloss(Color.Lerp(Pal.Seat, Color.black, 0.18f), 0.2f), rB = Pal.Gloss(Color.Lerp(Pal.Seat2, Color.black, 0.18f), 0.2f);
            float acc = 0f;
            for (int i = 0; i < path.Count - 1; i++)
            {
                Vector3 pa = path[i], pb = path[i + 1];
                Vector3 oa = outw[i], ob = outw[i + 1];
                float segLen = (pb - pa).magnitude;
                float mid = acc + segLen * 0.5f;
                acc += segLen;
                bool aisle = (mid % 16f) < 1.4f;
                if (skipEye && Mathf.Abs(Geo.AngleDeg((pa + pb) * 0.5f)) < 8.5f) continue;
                bool even = ((int)(mid / 16f) & 1) == 0;
                Color seat = even ? sA : sB, riser = aisle ? Pal.Gloss(Pal.Hex(0x7E838B), 0.1f) : (even ? rA : rB);
                Vector3 face = -(oa + ob);
                for (int j = 0; j < rows; j++)
                {
                    float o0 = off0 + j * depth, o1 = o0 + depth, y = h0 + j * rise;
                    Vector3 a0 = pa + oa * o0, b0 = pb + ob * o0, a1 = pa + oa * o1, b1 = pb + ob * o1;
                    a0.y = b0.y = a1.y = b1.y = y;
                    Slab(k, a0, b0, a1, b1, Vector3.up, aisle ? stair : seat);
                    Slab(k, a0 - Vector3.up * rise, b0 - Vector3.up * rise, a0, b0, face, riser);
                    if (!aisle && j % every == 0)
                        seats.Add(new SeatRow { a = Vector3.Lerp(a0, a1, 0.55f), b = Vector3.Lerp(b0, b1, 0.55f), facing = face.normalized });
                }
            }
        }

        /// <summary>A surface between two offsets/heights along a path (floor, wall or slope).</summary>
        static void Band(MeshKit k, List<Vector3> path, List<Vector3> outw, float o0, float y0, float o1, float y1, Color col, bool horizontal, bool down)
        {
            for (int i = 0; i < path.Count - 1; i++)
            {
                Vector3 a0 = path[i] + outw[i] * o0, b0 = path[i + 1] + outw[i + 1] * o0;
                Vector3 a1 = path[i] + outw[i] * o1, b1 = path[i + 1] + outw[i + 1] * o1;
                a0.y = b0.y = y0;
                a1.y = b1.y = y1;
                Vector3 n = horizontal ? (down ? Vector3.down : Vector3.up) : -(outw[i] + outw[i + 1]);
                Slab(k, a0, b0, a1, b1, n, col);
            }
        }

        void BuildCrowd(List<SeatRow> lower, List<SeatRow> upper, List<SeatRow> bleach, TeamData home, TeamData away)
        {
            var r = new System.Random(77);
            var kits = new List<MeshKit>();
            var k = new MeshKit();
            int count = 0;
            const int budget = 6200;
            Color[] misc = { Pal.Hex(0xF2F2F2), Pal.Hex(0x2A2D34), Pal.Hex(0xC0392B), Pal.Hex(0x2E86DE), Pal.Hex(0x7F8C8D), Pal.Hex(0xF39C12), Pal.Hex(0x16A085), Pal.Hex(0x8E44AD) };
            System.Action<List<SeatRow>, float> fill = (rows, occ) =>
            {
                foreach (var row in rows)
                {
                    float len = (row.b - row.a).magnitude;
                    int n = Mathf.Max(1, Mathf.FloorToInt(len / 0.95f));
                    for (int i = 0; i < n; i++)
                    {
                        if (count >= budget) return;
                        if (r.NextDouble() > occ) continue;
                        Vector3 p = Vector3.Lerp(row.a, row.b, (i + 0.5f) / n) + row.facing * (float)(r.NextDouble() * 0.1 - 0.05);
                        double pick = r.NextDouble();
                        Color shirt = pick < 0.46 ? home.main : pick < 0.6 ? away.main : pick < 0.68 ? home.accent : misc[r.Next(misc.Length)];
                        Fan(k, p, row.facing, shirt, Pal.Skins[r.Next(Pal.Skins.Length)], r, home);
                        count++;
                        if (k.V.Count > 54000) { kits.Add(k); k = new MeshKit(); }
                    }
                }
            };
            fill(lower, 0.58f);
            fill(bleach, 0.5f);
            fill(upper, 0.42f);
            kits.Add(k);
            for (int i = 0; i < kits.Count; i++)
                if (kits[i].V.Count > 0) Add("Crowd" + i, kits[i].ToMesh("BB_Crowd" + i), M.crowd, false, true);
        }

        static void Fan(MeshKit k, Vector3 seat, Vector3 facing, Color shirt, Color skin, System.Random r, TeamData home)
        {
            float yaw = Mathf.Atan2(facing.x, facing.z) * Mathf.Rad2Deg + (float)(r.NextDouble() * 20 - 10);
            Quaternion q = Quaternion.Euler(0f, yaw, 0f);
            float ph = (float)r.NextDouble();
            bool stand = r.NextDouble() < 0.08;
            bool arms = r.NextDouble() < 0.22;
            float h = (float)(0.95 + r.NextDouble() * 0.12);
            Vector3 b = new Vector3(seat.x, seat.y + (stand ? 0.25f : 0f), seat.z);
            // fans close to the plate (seen big behind the batter from the mound) get rounder shapes
            bool near = seat.z < 14f && new Vector2(seat.x, seat.z).magnitude < 48f;
            float wide = (float)(0.88 + r.NextDouble() * 0.3);
            Color sh = Pal.Gloss(shirt, 0.1f), sk = Pal.Gloss(skin, 0.3f);
            Vector3 up = Vector3.up;
            k.extra = new Vector2(ph, 1f);
            // little bean people like the players: a soft round body, a big round head, cap or hair
            k.Tube(t => b + up * Mathf.Lerp(0.26f, 0.8f * h, t), t => Mathf.Lerp(0.205f, 0.15f, t * t) * wide * (1f + 0.1f * Mathf.Sin(t * 3.1f)), 0f, 1f, near ? 3 : 2, near ? 7 : 6, sh);
            if (near) k.Ellipsoid(b + up * 0.79f * h, new Vector3(0.155f * wide, 0.065f, 0.125f), q, sh, 2, 7);
            Box5(k, b + new Vector3(0f, 0.19f, 0f) + q * new Vector3(0f, 0f, 0.14f), new Vector3(0.34f * wide, 0.15f, 0.3f), q, Pal.Gloss(Pal.Hex(0x30343C), 0.1f));
            Vector3 hc = b + new Vector3(0f, 0.99f * h, 0.01f);
            k.Ellipsoid(hc, new Vector3(0.175f, 0.185f, 0.17f), q, sk, near ? 4 : 3, near ? 8 : 6);
            double look = r.NextDouble();
            if (look < 0.32)
            {
                Color cap = Pal.Gloss(home.main, 0.2f);
                k.Ellipsoid(hc + new Vector3(0f, 0.075f, -0.005f), new Vector3(0.182f, 0.115f, 0.176f), q, cap, near ? 3 : 2, near ? 8 : 6);
                k.Ellipsoid(hc + q * new Vector3(0f, 0.07f, 0.155f), new Vector3(0.125f, 0.02f, 0.1f), q, cap, 2, 6);
            }
            else if (look < 0.82)
                k.Ellipsoid(hc + q * new Vector3(0f, 0.05f, -0.03f), new Vector3(0.186f, 0.16f, 0.172f), q, Pal.Gloss(Pal.Hairs[r.Next(Pal.Hairs.Length)], 0.2f), near ? 3 : 2, near ? 8 : 6);
            if (arms)
            {
                k.extra = new Vector2(ph, 2f);
                for (int sd = -1; sd <= 1; sd += 2)
                {
                    Vector3 a0 = b + q * new Vector3(sd * 0.16f, 0f, 0f) + up * 0.74f * h, a1 = a0 + q * new Vector3(sd * 0.09f, 0.4f, 0.04f);
                    k.Tube(t => Vector3.Lerp(a0, a1, t), t => 0.052f, 0f, 1f, 1, 5, sk);
                    if (near) k.Ellipsoid(a1, Vector3.one * 0.06f, q, sk, 2, 5);
                }
            }
            k.extra = Vector2.zero;
        }

        /// <summary>Box without a bottom face (fans and props).</summary>
        static void Box5(MeshKit k, Vector3 c, Vector3 size, Quaternion q, Color col)
        {
            Vector3 h = size * 0.5f;
            Vector3 X = q * Vector3.right, Y = q * Vector3.up, Z = q * Vector3.forward;
            k.Quad(c + X * h.x, X, Y, h.z, h.y, col);
            k.Quad(c - X * h.x, -X, Y, h.z, h.y, col);
            k.Quad(c + Y * h.y, Y, Z, h.x, h.z, col);
            k.Quad(c + Z * h.z, Z, Y, h.x, h.y, col);
            k.Quad(c - Z * h.z, -Z, Y, h.x, h.y, col);
        }

        // ------------------------------------------------------------------ scoreboard, lights, skyline

        void BuildScoreboard(TeamData home, TeamData away)
        {
            const float a = 24f;
            Vector3 dir = Geo.Dir(a);
            Vector3 c = dir * (Geo.FenceR(a) + 21f) + Vector3.up * 24f;
            var go = new GameObject("Scoreboard");
            go.transform.SetParent(root, false);
            go.transform.position = c;
            go.transform.rotation = Quaternion.LookRotation(dir, Vector3.up);
            var k = new MeshKit();
            Color frame = Pal.Gloss(Pal.Hex(0x1B1F27), 0.3f);
            k.Box(new Vector3(0f, 0f, 0.6f), new Vector3(38f, 19f, 1.2f), Quaternion.identity, frame);
            k.Box(new Vector3(-12f, -16f, 1f), new Vector3(1.4f, 14f, 1.4f), Quaternion.identity, Pal.Gloss(Pal.Steel, 0.4f));
            k.Box(new Vector3(12f, -16f, 1f), new Vector3(1.4f, 14f, 1.4f), Quaternion.identity, Pal.Gloss(Pal.Steel, 0.4f));
            k.extra = new Vector2(0f, 2.2f);
            k.Box(new Vector3(0f, 10.6f, 0.4f), new Vector3(38f, 2.2f, 1.4f), Quaternion.identity, Pal.Gloss(home.main, 0.4f));
            k.extra = Vector2.zero;
            go.AddComponent<MeshFilter>().sharedMesh = k.ToMesh("BB_Scoreboard");
            go.AddComponent<MeshRenderer>().sharedMaterial = M.glow;

            var sk = new MeshKit();
            sk.extra = new Vector2(0f, 1f);
            sk.Quad(new Vector3(0f, 0f, -0.05f), -Vector3.forward, Vector3.up, 18.2f, 8.8f, Pal.Gloss(Pal.Hex(0x0A0D14), 0.8f));
            var screen = new GameObject("Screen");
            screen.transform.SetParent(go.transform, false);
            screen.AddComponent<MeshFilter>().sharedMesh = sk.ToMesh("BB_Screen");
            screen.AddComponent<MeshRenderer>().sharedMaterial = M.glow;

            Transform tr = go.transform;
            sbMsg = Txt(tr, "WACKY BASEBALL", new Vector3(0f, 4.6f, -0.2f), new Vector2(34f, 6f), 60f, Pal.Gold);
            sbAway = Txt(tr, away.abbr, new Vector3(-11f, -0.6f, -0.2f), new Vector2(12f, 5f), 50f, Color.white);
            sbHome = Txt(tr, home.abbr, new Vector3(-11f, -5.6f, -0.2f), new Vector2(12f, 5f), 50f, Color.white);
            sbAwayR = Txt(tr, "0", new Vector3(1f, -0.6f, -0.2f), new Vector2(6f, 5f), 50f, Pal.Gold);
            sbHomeR = Txt(tr, "0", new Vector3(1f, -5.6f, -0.2f), new Vector2(6f, 5f), 50f, Pal.Gold);
            sbInning = Txt(tr, "9", new Vector3(10.5f, -0.6f, -0.2f), new Vector2(10f, 5f), 46f, Color.white);
            sbCount = Txt(tr, "", new Vector3(10.5f, -5.6f, -0.2f), new Vector2(12f, 5f), 40f, Color.white);
            sbBatter = Txt(tr, home.name.ToUpperInvariant(), new Vector3(0f, 10.6f, -0.8f), new Vector2(34f, 2f), 30f, Color.white);
        }

        TextMeshPro Txt(Transform parent, string s, Vector3 pos, Vector2 box, float size, Color c)
        {
            var t = WorldText.Board(parent, s, font, c, box, size);
            t.transform.localPosition = pos;
            t.transform.localRotation = Quaternion.identity;
            return t;
        }

        public void SetBoard(TeamData away, TeamData home, int awayRuns, int homeRuns, string inning, string count, string batter)
        {
            Set(sbAway, away.abbr); Set(sbHome, home.abbr);
            Set(sbAwayR, awayRuns.ToString()); Set(sbHomeR, homeRuns.ToString());
            Set(sbInning, inning); Set(sbCount, count);
            if (batter != null) Set(sbBatter, batter);
        }

        public void BoardMessage(string msg, Color c, float secs = 3f)
        {
            if (sbMsg == null) return;
            sbMsg.text = msg;
            sbMsg.color = c;
            msgT = secs;
        }

        static void Set(TextMeshPro t, string s) { if (t != null && t.text != s) t.text = s; }

        /// <summary>The big centre-field video board behind the pitcher (Baseball Clash puts a screen there): club logos and the
        /// match-up on a glowing screen, team-colour light strips, on two steel pylons above the batter's eye.</summary>
        void BuildCenterBoard(TeamData home, TeamData away)
        {
            float R = Geo.FenceR(0f) + 15f;
            var go = new GameObject("CenterBoard");
            go.transform.SetParent(root, false);
            go.transform.position = new Vector3(0f, 0f, R);
            go.transform.rotation = Quaternion.identity;
            var k = new MeshKit();
            Color frame = Pal.Gloss(Pal.Hex(0x1C222D), 0.35f), steel = Pal.Gloss(Pal.Steel, 0.5f);
            k.Box(new Vector3(-12f, 8.5f, 1.4f), new Vector3(2.4f, 17f, 2.4f), Quaternion.identity, steel);
            k.Box(new Vector3(12f, 8.5f, 1.4f), new Vector3(2.4f, 17f, 2.4f), Quaternion.identity, steel);
            k.Box(new Vector3(0f, 25.5f, 0.9f), new Vector3(40f, 19f, 1.8f), Quaternion.identity, frame);
            k.Box(new Vector3(0f, 36.2f, 1.2f), new Vector3(16f, 3.4f, 1.2f), Quaternion.identity, frame);
            var mf = go.AddComponent<MeshFilter>(); mf.sharedMesh = k.ToMesh("BB_CenterBoard");
            go.AddComponent<MeshRenderer>().sharedMaterial = M.prop;

            var gk = new MeshKit();
            gk.extra = new Vector2(0f, 1.8f);
            gk.Box(new Vector3(0f, 35.2f, 0f), new Vector3(40.4f, 0.9f, 2f), Quaternion.identity, Pal.Gloss(home.main, 0.4f));
            gk.Box(new Vector3(0f, 15.8f, 0f), new Vector3(40.4f, 0.9f, 2f), Quaternion.identity, Pal.Gloss(away.main, 0.4f));
            gk.extra = new Vector2(0f, 1f);
            gk.Quad(new Vector3(0f, 25.5f, -0.05f), Vector3.back, Vector3.up, 18.6f, 8.6f, Pal.Gloss(Pal.Hex(0x0B1530), 0.8f));
            gk.Quad(new Vector3(0f, 36.2f, 0.55f), Vector3.back, Vector3.up, 7.4f, 1.3f, Pal.Gloss(Pal.Hex(0x14161C), 0.8f));
            gk.extra = Vector2.zero;
            var glow = new GameObject("CenterScreen");
            glow.transform.SetParent(go.transform, false);
            glow.AddComponent<MeshFilter>().sharedMesh = gk.ToMesh("BB_CenterScreen");
            glow.AddComponent<MeshRenderer>().sharedMaterial = M.glow;

            var tr = go.transform;
            Txt(tr, "WACKY", new Vector3(0f, 29.4f, -0.3f), new Vector2(36f, 8f), 120f, Pal.Gold);
            Txt(tr, "BASEBALL", new Vector3(0f, 23.4f, -0.3f), new Vector2(36f, 6f), 84f, Color.white);
            Txt(tr, away.abbr + "  vs  " + home.abbr, new Vector3(0f, 18.6f, -0.3f), new Vector2(36f, 3f), 40f, Pal.Hex(0x9AD0FF));
            Txt(tr, "BANANAPORT PARK", new Vector3(0f, 36.2f, 0.2f), new Vector2(15f, 2.4f), 30f, Pal.Gold);
        }

        static readonly string[] Brands = { "BANANA COLA", "PICKLE POP", "MOON PIZZA", "TURBO SOCKS", "HOTDOG HAVEN", "ZOOM GUM" };
        static readonly int[] BrandBg = { 0xF6C928, 0x2BAE66, 0x6C3FB5, 0xF08A24, 0xD7263D, 0x3C8BE8 };
        static readonly int[] BrandInk = { 0xD7263D, 0xFFFFFF, 0xFFE066, 0x14161C, 0xFFE066, 0xFFFFFF };

        /// <summary>Lit billboards on poles above the outfield bleachers (fictional wacky brands).</summary>
        void BuildBillboards()
        {
            float[] angles = { -38f, -22f, 40f };   // the video scoreboard sits at +24 degrees
            float blBack = 3.2f + 18 * 0.85f, blTop = 4.2f + 17 * 0.55f;
            var k = new MeshKit();
            var gk = new MeshKit();
            Color pole = Pal.Gloss(Pal.Steel, 0.5f), frame = Pal.Gloss(Pal.Hex(0x1C222D), 0.3f);
            for (int i = 0; i < angles.Length; i++)
            {
                float a = angles[i];
                Vector3 dir = Geo.Dir(a);
                Vector3 c = dir * (Geo.FenceR(a) + blBack + 2.5f);
                Quaternion q = Quaternion.LookRotation(dir, Vector3.up);
                float baseY = blTop + 3.2f;
                k.Box(c + q * new Vector3(-4.5f, baseY * 0.5f, 0.4f), new Vector3(0.7f, baseY, 0.7f), q, pole);
                k.Box(c + q * new Vector3(4.5f, baseY * 0.5f, 0.4f), new Vector3(0.7f, baseY, 0.7f), q, pole);
                k.Box(c + q * new Vector3(0f, baseY + 3.2f, 0.25f), new Vector3(15f, 6.8f, 0.5f), q, frame);
                gk.extra = new Vector2(0f, 1.15f);
                gk.Quad(c + q * new Vector3(0f, baseY + 3.2f, -0.03f), -dir, Vector3.up, 7.1f, 3.0f, Pal.Gloss(Pal.Hex(BrandBg[i % BrandBg.Length]), 0.6f));
                gk.extra = Vector2.zero;
                var t = WorldText.Board(root, Brands[(i + 1) % Brands.Length], font, Pal.Hex(BrandInk[i % BrandInk.Length]), new Vector2(13.5f, 5f), 46f);
                t.transform.position = c + q * new Vector3(0f, baseY + 3.2f, -0.2f);
                t.transform.rotation = q;
            }
            Add("Billboards", k.ToMesh("BB_Billboards"), M.prop, true, true);
            Add("BillboardFaces", gk.ToMesh("BB_BillboardFaces"), M.glow, false, true);
        }

        void BuildLights()
        {
            var k = new MeshKit();
            var gk = new MeshKit();
            Color steel = Pal.Gloss(Pal.Hex(0x8C939C), 0.5f);
            var spots = new List<Vector3>();
            if (roofEdge != null && roofEdge.Count > 6)
            {
                int step = Mathf.Max(1, roofEdge.Count / 6);
                for (int i = step / 2; i < roofEdge.Count; i += step) spots.Add(roofEdge[i]);
            }
            for (int sd = -1; sd <= 1; sd += 2)
                spots.Add(Geo.Dir(sd * 33f) * (Geo.FenceR(33f) + 22f) + Vector3.up * 14f);
            foreach (var s in spots)
            {
                Vector3 toField = new Vector3(0f, 0f, 40f) - s;
                toField.y = 0f;
                toField.Normalize();
                Quaternion q = Quaternion.LookRotation(toField, Vector3.up) * Quaternion.Euler(-12f, 0f, 0f);
                k.Limb(new Vector3(s.x, s.y - 14f, s.z) - toField * 1.5f, s + Vector3.up * 9f - toField * 1.5f, 0.45f, 0.35f, steel, 2, 8);
                Vector3 c = s + Vector3.up * 12f;
                k.Box(c - toField * 0.5f, new Vector3(9.5f, 5.5f, 0.6f), q, steel);
                gk.extra = new Vector2(0f, 3.5f);
                for (int x = 0; x < 6; x++)
                    for (int y = 0; y < 3; y++)
                        gk.Box(c + q * new Vector3(-3.9f + x * 1.56f, -1.8f + y * 1.8f, 0f), new Vector3(1.2f, 1.3f, 0.25f), q, Pal.Gloss(new Color(1f, 0.97f, 0.88f), 0.9f));
                gk.extra = Vector2.zero;
            }
            Add("LightTowers", k.ToMesh("BB_Towers"), M.metal, true, true);
            Add("Lamps", gk.ToMesh("BB_Lamps"), M.glow, false, false);
        }

        void BuildSkyline()
        {
            var r = new System.Random(19);
            var k = new MeshKit();
            // dusk skyline: dark violet towers that fade into the warm haze, with lit windows facing the ballpark
            Color[] cols = { Pal.Hex(0x4A4E6E), Pal.Hex(0x5A5878), Pal.Hex(0x3E4560), Pal.Hex(0x665A76), Pal.Hex(0x52607A), Pal.Hex(0x5E6A8A) };
            Color[] lamp = { Pal.Hex(0xFFD98A), Pal.Hex(0xFFC56A), Pal.Hex(0xFFF0C8) };
            for (int i = 0; i < 46; i++)
            {
                float a = Mathf.Lerp(-58f, 58f, (i + (float)r.NextDouble() * 0.6f) / 46f);
                float d = 330f + (float)r.NextDouble() * 270f;
                float h = 24f + Mathf.Pow((float)r.NextDouble(), 1.7f) * 96f;   // low enough that the dusk sky still shows above
                float w = 18f + (float)r.NextDouble() * 26f;
                float depth = w * (0.7f + (float)r.NextDouble() * 0.5f);
                Vector3 p = Geo.Dir(a) * d + new Vector3(0f, h * 0.5f, 0f);
                Color c = Pal.Gloss(cols[r.Next(cols.Length)], 0.35f);
                var q = Quaternion.Euler(0f, a, 0f);
                k.Box(p, new Vector3(w, h, depth), q, c);
                if (r.NextDouble() < 0.3) k.Box(p + Vector3.up * (h * 0.5f + 4f), new Vector3(w * 0.5f, 8f, w * 0.5f), q, c);
                else if (r.NextDouble() < 0.25)
                {
                    // a spire or an antenna with a red beacon
                    k.Box(p + Vector3.up * (h * 0.5f + 7f), new Vector3(1.2f, 14f, 1.2f), q, c);
                    k.extra = new Vector2(0f, 2.5f);
                    k.Box(p + Vector3.up * (h * 0.5f + 14.5f), new Vector3(1.8f, 1.8f, 1.8f), q, Pal.Gloss(Pal.Hex(0xFF4040), 0f));
                    k.extra = Vector2.zero;
                }
                // windows on the face toward home plate
                Vector3 X = q * Vector3.right, Y = Vector3.up, Z = q * Vector3.forward;
                Vector3 face = p - Z * (depth * 0.5f + 0.2f);
                int cols2 = Mathf.Max(2, Mathf.FloorToInt(w / 5f)), rows = Mathf.Max(2, Mathf.FloorToInt(h / 7f));
                k.extra = new Vector2(0f, 1.25f);
                for (int yy = 0; yy < rows; yy++)
                    for (int xx = 0; xx < cols2; xx++)
                    {
                        if (r.NextDouble() < 0.45) continue;
                        float u = ((xx + 0.5f) / cols2 - 0.5f) * (w - 4f);
                        float v = ((yy + 0.5f) / rows - 0.5f) * (h - 6f);
                        k.Quad(face + X * u + Y * v, -Z, Y, 1.4f, 1.1f, Pal.Gloss(lamp[r.Next(lamp.Length)], 0f));
                    }
                k.extra = Vector2.zero;
            }
            Color[] trees = { Pal.Hex(0x3E6B32), Pal.Hex(0x4C7A36), Pal.Hex(0x35602C) };
            for (int i = 0; i < 70; i++)
            {
                float a = (float)r.NextDouble() * 360f;
                float d = 190f + (float)r.NextDouble() * 80f;
                Vector3 p = new Vector3(Mathf.Sin(a * Mathf.Deg2Rad) * d, 0f, 40f + Mathf.Cos(a * Mathf.Deg2Rad) * d);
                float s = 6f + (float)r.NextDouble() * 6f;
                k.Ellipsoid(p + Vector3.up * s * 0.9f, new Vector3(s, s * 1.1f, s), Quaternion.identity, Pal.Gloss(trees[r.Next(trees.Length)], 0.1f), 5, 8);
            }
            Add("Skyline", k.ToMesh("BB_Skyline"), M.prop, false, true);
        }

        void BuildTubeMen(TeamData home, TeamData away)
        {
            for (int sd = -1; sd <= 1; sd += 2)
            {
                const float s = 20f;
                Vector3 p = Geo.FromLine(sd, s, Geo.WallD(s) + 1.6f) + Vector3.up * 2.6f;
                TeamData t = sd < 0 ? home : away;
                tubeMen.Add(new TubeMan(root, M, p, t.main, t.accent, sd * 1.7f));
            }
        }

        public void Excite(float amount, float secs = 2.5f)
        {
            excite = Mathf.Max(excite, amount);
            exciteT = Mathf.Max(exciteT, secs);
            foreach (var tm in tubeMen) tm.Hype(amount);
        }

        public void Tick(float dt)
        {
            exciteT -= dt;
            if (exciteT <= 0f) excite = MathX.Damp(excite, 0.12f, 1.5f, dt);
            Shader.SetGlobalFloat(ExciteId, excite);
            foreach (var tm in tubeMen) tm.Tick(dt);
            if (msgT > 0f && sbMsg != null)
            {
                msgT -= dt;
                sbMsg.enabled = Mathf.Repeat(Time.time * 4f, 1f) > 0.18f || msgT < 0.3f;
                if (msgT <= 0f) { sbMsg.enabled = true; sbMsg.text = "WACKY BASEBALL"; sbMsg.color = Pal.Gold; }
            }
        }
    }

    /// <summary>The wacky waving inflatable arm-flailing tube man: a wobbly chain of segments with two noodle arms.</summary>
    public class TubeMan
    {
        readonly Transform[] seg = new Transform[7];
        readonly Transform armL, armR;
        readonly float seed;
        float hype, t;

        public TubeMan(Transform parent, Mats m, Vector3 pos, Color body, Color trim, float seedValue)
        {
            seed = seedValue;
            var root = new GameObject("TubeMan").transform;
            root.SetParent(parent, false);
            root.position = pos;
            var bk = new MeshKit();
            bk.Tube(u => new Vector3(0f, u * 0.9f, 0f), u => 0.34f, 0f, 1f, 2, 12, Pal.Gloss(body, 0.6f));
            Mesh segMesh = bk.ToMesh("BB_TubeSeg", true);
            var hk = new MeshKit();
            hk.Tube(u => new Vector3(0f, u * 0.9f, 0f), u => 0.34f, 0f, 1f, 2, 12, Pal.Gloss(body, 0.6f));
            hk.Sphere(new Vector3(0f, 0.95f, 0f), 0.36f, Pal.Gloss(body, 0.6f), 8, 12);
            for (int e = -1; e <= 1; e += 2)
            {
                hk.outline = 0.6f;
                hk.Sphere(new Vector3(e * 0.13f, 1.0f, 0.3f), 0.1f, Pal.Gloss(Color.white, 0.6f), 5, 8);
                hk.outline = 0f;
                hk.Sphere(new Vector3(e * 0.13f, 1.0f, 0.39f), 0.045f, Pal.Gloss(Pal.Ink, 0.8f), 4, 6);
                hk.Ellipsoid(new Vector3(e * 0.14f, 1.13f, 0.3f), new Vector3(0.1f, 0.025f, 0.03f), Quaternion.Euler(0f, 0f, e * -15f), Pal.Gloss(Pal.Ink, 0.2f), 3, 6);
            }
            hk.outline = 0.5f;
            hk.Ellipsoid(new Vector3(0f, 0.8f, 0.31f), new Vector3(0.16f, 0.09f, 0.06f), Quaternion.identity, Pal.Gloss(Pal.Hex(0x4A0E14), 0.4f), 5, 10);
            hk.outline = 1f;
            for (int i = 0; i < 9; i++)
            {
                float a = (i / 8f - 0.5f) * 2.4f;
                hk.Ellipsoid(new Vector3(Mathf.Sin(a) * 0.3f, 1.25f + Mathf.Cos(a) * 0.08f, Mathf.Cos(a) * 0.05f), new Vector3(0.05f, 0.25f, 0.05f), Quaternion.Euler(0f, 0f, -a * 30f), Pal.Gloss(trim, 0.4f), 4, 6);
            }
            Mesh headMesh = hk.ToMesh("BB_TubeHead", true);
            var ak = new MeshKit();
            ak.Tube(u => new Vector3(0f, -u * 2.2f, 0f), u => 0.13f, 0f, 1f, 6, 8, Pal.Gloss(trim, 0.6f));
            Mesh armMesh = ak.ToMesh("BB_TubeArm", true);

            Transform prev = root;
            for (int i = 0; i < seg.Length; i++)
            {
                var go = new GameObject("Seg" + i);
                go.transform.SetParent(prev, false);
                go.transform.localPosition = i == 0 ? Vector3.zero : new Vector3(0f, 0.9f, 0f);
                go.AddComponent<MeshFilter>().sharedMesh = i == seg.Length - 1 ? headMesh : segMesh;
                go.AddComponent<MeshRenderer>().sharedMaterial = m.mascot;
                seg[i] = go.transform;
                prev = go.transform;
            }
            armL = Arm(seg[seg.Length - 3], armMesh, m, -1f);
            armR = Arm(seg[seg.Length - 3], armMesh, m, 1f);
            var bs = new MeshKit();
            bs.Box(new Vector3(0f, -0.3f, 0f), new Vector3(1.2f, 0.6f, 1.2f), Quaternion.identity, Pal.Gloss(Pal.Steel, 0.5f));
            var baseGo = new GameObject("Blower");
            baseGo.transform.SetParent(root, false);
            baseGo.AddComponent<MeshFilter>().sharedMesh = bs.ToMesh("BB_Blower");
            baseGo.AddComponent<MeshRenderer>().sharedMaterial = m.metal;
        }

        static Transform Arm(Transform parent, Mesh mesh, Mats m, float side)
        {
            var go = new GameObject(side < 0 ? "ArmL" : "ArmR");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(side * 0.32f, 0.7f, 0f);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = m.mascot;
            return go.transform;
        }

        public void Hype(float a) { hype = Mathf.Max(hype, a); }

        public void Tick(float dt)
        {
            t += dt * (1.6f + 2.2f * hype);
            hype = Mathf.Max(0f, hype - dt * 0.25f);
            float amp = 9f + 22f * hype;
            float collapse = Mathf.Max(0f, Mathf.Sin(t * 0.21f + seed * 3f) - 0.93f) * 14f;
            for (int i = 0; i < seg.Length; i++)
            {
                float k = (i + 1f) / seg.Length;
                float x = Mathf.Sin(t * 1.9f + seed + i * 0.8f) * amp * k + (Mathf.PerlinNoise(t * 0.7f + i, seed) - 0.5f) * 20f * k;
                float z = Mathf.Cos(t * 1.4f + seed * 2f + i * 0.6f) * amp * 0.7f * k;
                if (i > 0) x += collapse * 25f * k;
                seg[i].localRotation = Quaternion.Euler(z, 0f, x);
            }
            armL.localRotation = Quaternion.Euler(Mathf.Sin(t * 3.1f + seed) * 50f, 0f, -110f + Mathf.Sin(t * 2.3f) * 70f);
            armR.localRotation = Quaternion.Euler(Mathf.Cos(t * 2.7f + seed) * 50f, 0f, 110f + Mathf.Cos(t * 2.1f) * 70f);
        }
    }
}
