using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace SlingArena.Colosseum
{
    /// <summary>
    /// On the fourth turn a wide gate of golden light rises from a pad in the middle of the
    /// arena and slides from side to side across it. A hero slung through it (or over it in the air) powers up for
    /// the rest of the turn: it flickers and springs out to 1.4 times its size, twinkles, hits harder, and for as
    /// long as that lasts its whole silhouette is ringed with golden light.
    /// </summary>
    public sealed class ColosseumBoost
    {
        /// <summary>
        /// How much harder a powered-up hero hits, and how much bigger it gets. These, and the size of the gate
        /// below, are set from the balance sheet at the start of a match (see Tune).
        /// </summary>
        public static float DamageScale { get; private set; } = 1.5f;
        public static float GrowScale { get; private set; } = 1.4f;
        const float Flick = .07f, Shrink = .24f;
        const int TwinkleCount = 56, WispCount = 56;
        // The gate that stands on a strip: a wall of light whose top edge blazes, with a taller flame at each end.
        const float GateWide = 5.1f, GateBase = 2.15f, GateReach = 1.95f, PostRise = 2.35f;
        // Its parts are built to those measurements, and the whole gate is then shown, and counts, at this share
        // of them: a narrower, lower gate that takes more aiming to go through.
        static float GateSize = .7f;

        /// <summary>The gate's numbers for the matches to come: how much harder, how much bigger, and how large the gate itself is.</summary>
        public static void Tune(float damageScale, float growScale, float gateSize)
        {
            DamageScale = Mathf.Max(1f, damageScale);
            GrowScale = Mathf.Clamp(growScale, 1f, 2.2f);
            GateSize = Mathf.Clamp(gateSize, .3f, 1.4f);
        }
        // What a gate stands on: a pad of light shaped like a stadium, rounded off all the way round and, end to
        // end, as long as the wall above it is wide.
        const float PadRound = .58f, PadHalf = GateWide * .5f + .1f - PadRound;
        // A hero powers up on coming within PassReach of the line the wall stands on (PassHalf is half that line's
        // length, by the gate's own measurements; PassReach is a distance on the sand).
        const float PassHalf = GateWide * .5f - .03f, PassReach = .8f;
        // The sign on a gate, against the size its parts were drawn at.
        const float SignSize = .76f;
        // The gate waits in the middle while it rises, then swings from side to side: SlideRate is how fast (in
        // radians a second, a full swing there and back being one turn), and EdgeGap how far short of the side
        // walls its ends stop.
        const float SlideWait = 1.1f, SlideRamp = .9f, SlideRate = .98f, EdgeGap = .35f;
        const int PadSide = 8, PadCap = 10, PadLoop = (PadSide + 1) * 2 + (PadCap - 1) * 2;
        const int Columns = 56, Rows = 3, Layers = 3, PerLayer = (Columns + 1) * Rows;
        static readonly Bounds GateBounds = new Bounds(new Vector3(0f, 4.5f, 0f), new Vector3(GateWide + 2f, 10f, 1f));

        sealed class Strip
        {
            public Transform Root, Icon;
            public MeshRenderer Glow, Pad, Bar, Wall;
            public Mesh Curtain;   // the wall of light, reshaped every frame
            public Vector3 Home, Centre;   // the middle of its swing, and where it is now
            public Quaternion Turn;
            public float Age, TwinkleClock, WispClock, Seed, Flare;
            public float Swing, Phase, Speed;   // which way it sets off, how far through its swing it is, how fast it is moving
        }

        /// <summary>One powered-up hero.</summary>
        sealed class Charge
        {
            public ColosseumDoll Doll;
            public float Age, Gone, TwinkleClock;
            public bool Leaving;
        }

        /// <summary>A pooled sparkle, or a streak of light rising off a gate.</summary>
        sealed class Mote
        {
            public Transform Item;
            public MeshRenderer Face;
            public Vector3 Velocity;
            public float Age, Life, Size, Tall, Spin;
            public bool Active;
        }

        readonly Transform root;
        readonly List<Strip> strips = new List<Strip>();
        readonly List<Charge> charges = new List<Charge>();
        readonly List<Mote> twinkles = new List<Mote>();
        readonly List<Mote> wisps = new List<Mote>();
        readonly HashSet<ColosseumDoll> boosted = new HashSet<ColosseumDoll>();
        readonly Dictionary<ColosseumDoll, Vector3> lastSeen = new Dictionary<ColosseumDoll, Vector3>();
        readonly MaterialPropertyBlock block = new MaterialPropertyBlock();
        readonly Vector3[] curtainPoints = new Vector3[PerLayer * Layers];
        readonly Color32[] curtainTints;
        readonly int[] curtainTriangles;
        readonly Mesh glowMesh, padMesh, barMesh, twinkleMesh, wispMesh, arrowMesh;
        readonly Material glow;
        readonly Sprite icon;
        int twinkleCursor, wispCursor;
        float travel;   // how far either side of the middle the gate slides

        public ColosseumBoost(ColosseumGame owner)
        {
            root = new GameObject("Colosseum power gates").transform;
            root.SetParent(owner.World, false);
            glow = Kit.StrokeMaterial(Color.white);
            icon = Resources.Load<Sprite>("ColosseumUI/swords");
            glowMesh = GlowMesh();
            padMesh = PadMesh();
            barMesh = BarMesh();
            twinkleMesh = TwinkleMesh();
            wispMesh = WispMesh();
            arrowMesh = ArrowMesh();
            curtainTints = CurtainTints();
            curtainTriangles = CurtainTriangles();
            for (int i = 0; i < TwinkleCount; i++) twinkles.Add(MakeMote("Twinkle", twinkleMesh, 2));
            for (int i = 0; i < WispCount; i++) wisps.Add(MakeMote("Wisp", wispMesh, 1));
        }

        Mote MakeMote(string name, Mesh mesh, int order)
        {
            GameObject item = Kit.SpawnStroke(name, root, mesh, glow);
            item.SetActive(false);
            var face = item.GetComponent<MeshRenderer>();
            face.sortingOrder = order;
            return new Mote { Item = item.transform, Face = face };
        }

        // ------------------------------------------------------------------ meshes

        /// <summary>Soft pool of warm light on the sand under a gate: a disc of radius 1, stretched to fit.</summary>
        static Mesh GlowMesh()
        {
            var kit = new MeshKit();
            var heart = new Color32(255, 190, 64, 120);
            var rim = new Color32(255, 168, 40, 0);
            const int steps = 28;
            for (int i = 0; i < steps; i++)
            {
                float a = Mathf.PI * 2f * i / steps, b = Mathf.PI * 2f * (i + 1) / steps;
                kit.Quad(Vector3.zero, new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)), new Vector3(Mathf.Sin(b), 0f, Mathf.Cos(b)), Vector3.zero,
                    heart, rim, rim, heart);
            }
            return kit.ToMesh("Colosseum power glow");
        }

        /// <summary>
        /// One loop round the pad, `reach` away from its middle line: along the far side, round the right end, back
        /// along the near side, round the left end. Every loop has its points in the same places, so two of them
        /// can be joined point to point.
        /// </summary>
        static void Loop(float half, float reach, float y, Vector3[] into)
        {
            int at = 0;
            for (int i = 0; i <= PadSide; i++) into[at++] = new Vector3(Mathf.Lerp(-half, half, (float)i / PadSide), y, reach);
            for (int j = 1; j < PadCap; j++)
            {
                float angle = Mathf.PI * (.5f - (float)j / PadCap);
                into[at++] = new Vector3(half + Mathf.Cos(angle) * reach, y, Mathf.Sin(angle) * reach);
            }
            for (int i = 0; i <= PadSide; i++) into[at++] = new Vector3(Mathf.Lerp(half, -half, (float)i / PadSide), y, -reach);
            for (int j = 1; j < PadCap; j++)
            {
                float angle = Mathf.PI * (-.5f - (float)j / PadCap);
                into[at++] = new Vector3(-half + Mathf.Cos(angle) * reach, y, Mathf.Sin(angle) * reach);
            }
        }

        /// <summary>
        /// Bands of colour round a middle line: reach[k] is how far out band k's edge lies and tint[k] its colour
        /// there. Stops dimFrom to dimTo are a line that should not show through the wall standing on the middle
        /// line, so along the far side they melt into the colour just inside them, except close to the ends.
        /// </summary>
        static void Stadium(MeshKit kit, float half, float y, float[] reach, Color32[] tint, int dimFrom = -1, int dimTo = -1)
        {
            var inner = new Vector3[PadLoop];
            var outer = new Vector3[PadLoop];
            var innerTint = new Color32[PadLoop];
            var outerTint = new Color32[PadLoop];
            for (int k = 0; k < reach.Length; k++)
            {
                Loop(half, reach[k], y, outer);
                for (int i = 0; i < PadLoop; i++)
                {
                    outerTint[i] = tint[k];
                    if (k < dimFrom || k > dimTo || i > PadSide) continue;
                    float show = Mathf.Lerp(.3f, 1f, Mathf.InverseLerp(half - .45f, half, Mathf.Abs(outer[i].x)));
                    outerTint[i] = MeshKit.Mix(tint[dimFrom - 1], tint[k], show);
                }
                if (k > 0)
                {
                    for (int i = 0; i < PadLoop; i++)
                    {
                        int next = (i + 1) % PadLoop;
                        kit.Quad(inner[i], outer[i], outer[next], inner[next], innerTint[i], outerTint[i], outerTint[next], innerTint[next]);
                    }
                }
                System.Array.Copy(outer, inner, PadLoop);
                System.Array.Copy(outerTint, innerTint, PadLoop);
            }
        }

        /// <summary>
        /// The pad a gate stands on: light spilling out from the foot of the wall, gold and then orange, closed off
        /// by a pale rim all the way round, with a soft edge outside the rim.
        /// </summary>
        static Mesh PadMesh()
        {
            var kit = new MeshKit();
            const float r = PadRound;
            float[] reach = { 0f, .07f, .18f, .3f, r - .065f, r - .06f, r, r + .005f, r + .3f };
            var rim = new Color32(255, 246, 196, 245);
            Color32[] tint =
            {
                new Color32(255, 248, 200, 255), new Color32(255, 236, 140, 250), new Color32(255, 206, 70, 240),
                new Color32(255, 176, 34, 225), new Color32(255, 152, 22, 212), rim, rim,
                new Color32(255, 170, 40, 120), new Color32(255, 150, 30, 0)
            };
            Stadium(kit, PadHalf, .03f, reach, tint, 5, 6);
            return kit.ToMesh("Colosseum power gate pad");
        }

        /// <summary>The white-hot bar along the foot of the wall, as long as the wall, with rounded ends.</summary>
        static Mesh BarMesh()
        {
            var kit = new MeshKit();
            float[] reach = { 0f, .045f, .05f, .11f };
            Color32[] tint =
            {
                new Color32(255, 255, 236, 255), new Color32(255, 252, 214, 255), new Color32(255, 240, 150, 150),
                new Color32(255, 230, 120, 0)
            };
            Stadium(kit, GateWide * .5f - .12f, .046f, reach, tint);
            return kit.ToMesh("Colosseum power gate bar");
        }

        /// <summary>Four-pointed sparkle in the XY plane, white at the heart and gold at the tips.</summary>
        static Mesh TwinkleMesh()
        {
            var kit = new MeshKit();
            var heart = new Color32(255, 255, 240, 255);
            var tip = new Color32(255, 214, 64, 255);
            const float waist = .13f;
            var points = new Vector3[8];
            for (int i = 0; i < 8; i++)
            {
                float angle = Mathf.PI * .25f * i, reach = i % 2 == 0 ? .5f : waist;
                points[i] = new Vector3(Mathf.Sin(angle) * reach, Mathf.Cos(angle) * reach, 0f);
            }
            for (int i = 0; i < 8; i++)
                kit.Quad(Vector3.zero, points[i], points[(i + 1) % 8], Vector3.zero, heart, i % 2 == 0 ? tip : heart, i % 2 == 0 ? heart : tip, heart);
            return kit.ToMesh("Colosseum twinkle");
        }

        /// <summary>A lick of light, one unit tall in the XY plane: pointed at both ends, brightest low down.</summary>
        static Mesh WispMesh()
        {
            var kit = new MeshKit();
            var foot = new Color32(255, 244, 190, 0);
            var belly = new Color32(255, 222, 104, 210);
            var tip = new Color32(255, 140, 28, 0);
            var low = new Vector3(0f, 0f, 0f);
            var left = new Vector3(-.5f, .3f, 0f);
            var right = new Vector3(.5f, .3f, 0f);
            var core = new Vector3(0f, .34f, 0f);
            var high = new Vector3(0f, 1f, 0f);
            var heart = new Color32(255, 250, 214, 230);
            kit.Quad(low, left, core, low, foot, belly, heart, foot);
            kit.Quad(low, core, right, low, foot, heart, belly, foot);
            kit.Quad(left, high, core, left, belly, tip, heart, belly);
            kit.Quad(core, high, right, core, heart, tip, belly, heart);
            return kit.ToMesh("Colosseum power wisp");
        }

        /// <summary>Chunky green up-arrow on a dark backing, shown beside the swords on a gate.</summary>
        static Mesh ArrowMesh()
        {
            var kit = new MeshKit();
            ArrowLayer(kit, 1.28f, .004f, new Color32(22, 16, 34, 255));
            ArrowLayer(kit, 1f, 0f, new Color32(112, 240, 84, 255));
            return kit.ToMesh("Colosseum power arrow");
        }

        static void ArrowLayer(MeshKit kit, float grow, float depth, Color32 color)
        {
            float w = .5f * grow, shaft = .2f * grow, top = .5f + (grow - 1f) * .42f, neck = -.02f, foot = -.5f - (grow - 1f) * .42f;
            kit.Tri(new Vector3(0f, top, depth), new Vector3(w, neck - (grow - 1f) * .3f, depth), new Vector3(-w, neck - (grow - 1f) * .3f, depth), color);
            kit.Quad(new Vector3(-shaft, foot, depth), new Vector3(-shaft, neck, depth), new Vector3(shaft, neck, depth), new Vector3(shaft, foot, depth), color, color, color, color);
        }

        // ------------------------------------------------------------------ the wall of light

        /// <summary>
        /// Colours of the wall, which is three sheets one in front of the other: burning orange-gold whose tips thin
        /// out, a shorter yellow one inside it, and a low white-hot one at the foot. Each column of a sheet has three
        /// points: foot, waist and tip. The tall flames at the two ends are a little more solid.
        /// </summary>
        static Color32[] CurtainTints()
        {
            var tints = new Color32[PerLayer * Layers];
            for (int i = 0; i <= Columns; i++)
            {
                float post = Post((float)i / Columns);
                int at = i * Rows;
                tints[at] = new Color32(255, 160, 28, (byte)Mathf.RoundToInt(205f + 35f * post));
                tints[at + 1] = new Color32(255, 126, 20, (byte)Mathf.RoundToInt(175f + 28f * post));
                tints[at + 2] = new Color32(255, 96, 12, 55);
                at += PerLayer;
                tints[at] = new Color32(255, 222, 80, (byte)Mathf.RoundToInt(215f + 35f * post));
                tints[at + 1] = new Color32(255, 198, 52, (byte)Mathf.RoundToInt(175f + 28f * post));
                tints[at + 2] = new Color32(255, 170, 36, 45);
                at += PerLayer;
                tints[at] = new Color32(255, 252, 214, 230);
                tints[at + 1] = new Color32(255, 240, 150, 160);
                tints[at + 2] = new Color32(255, 226, 110, 0);
            }
            return tints;
        }

        static int[] CurtainTriangles()
        {
            var list = new List<int>();
            for (int layer = 0; layer < Layers; layer++)
            {
                for (int i = 0; i < Columns; i++)
                {
                    for (int row = 0; row < Rows - 1; row++)
                    {
                        int a = layer * PerLayer + i * Rows + row, b = a + 1, c = b + Rows, d = a + Rows;
                        list.Add(a); list.Add(b); list.Add(c);
                        list.Add(a); list.Add(c); list.Add(d);
                    }
                }
            }
            return list.ToArray();
        }

        /// <summary>Triangle wave, 0 to 1 and back once per unit.</summary>
        static float Tri(float u)
        {
            u -= Mathf.Floor(u);
            return Mathf.Clamp01(1f - Mathf.Abs(u * 2f - 1f));
        }

        /// <summary>The wall drops to the ground at its two ends, flaring out a little where it meets the pad.</summary>
        static float Ends(float s) { return Ease(Mathf.Clamp01(s / .05f)) * Ease(Mathf.Clamp01((1f - s) / .05f)); }

        static float Ease(float k) { return k * k * (3f - 2f * k); }

        /// <summary>How far a point (in a gate's own space, on the ground) is from the line its wall stands on.</summary>
        static float Away(Vector3 local)
        {
            float past = Mathf.Max(Mathf.Abs(local.x) - PassHalf, 0f);
            return Mathf.Sqrt(past * past + local.z * local.z);
        }

        /// <summary>1 where the tall flame at either end of the gate stands, 0 in between.</summary>
        static float Post(float s)
        {
            float near = Mathf.Min(s, 1f - s);
            return Mathf.Pow(Mathf.Clamp01(1f - Mathf.Abs(near - .055f) / .06f), 1.4f);
        }

        /// <summary>
        /// How far the tongues of light reach above the wall at one point along it (s from 0 to 1): big tongues
        /// drifting one way, small ones the other, each licking up and down on its own.
        /// </summary>
        static float Blaze(float s, float time, float seed)
        {
            float big = Mathf.Pow(Tri(s * 6.4f + seed + time * .16f), 1.6f);
            float small = Mathf.Pow(Tri(s * 13.5f + seed * 1.7f - time * .23f), 1.5f);
            float lick = Mathf.PerlinNoise(s * 11.5f + seed * 3.1f, time * 1.9f);
            float flick = Mathf.PerlinNoise(s * 21.5f + seed * 2.3f + 40f, time * 3.1f);
            return big * (.45f + .75f * lick) + small * .42f * (.35f + flick);
        }

        /// <summary>Which way, and how far (-1 to 1), the wall leans at a point along it: two slow waves crossing each other.</summary>
        static float Roll(float s, float time, float seed)
        {
            return Mathf.Sin(time * 1.5f + s * 5.2f + seed) * .6f + Mathf.Sin(time * 2.3f - s * 8.5f + seed * 1.7f) * .4f;
        }

        /// <summary>Reshapes a gate's wall for this moment.</summary>
        void Blow(Strip strip, float time)
        {
            float surge = 1f + .3f * strip.Flare;
            // On the move, the light streams out behind the gate: the faster it slides, the further its tips trail.
            float drag = -strip.Speed * .12f;
            for (int i = 0; i <= Columns; i++)
            {
                float s = (float)i / Columns, x = (s - .5f) * GateWide;
                float ends = Ends(s);
                // The whole sheet of light rolls gently: a slow swell runs along it, raising and lowering it, and
                // its upper part leans one way and then the other, the tips most, the foot not at all.
                float swell = 1f + .085f * Mathf.Sin(time * 1.15f - s * 3.6f + strip.Seed * 2.3f);
                float lean = (Roll(s, time, strip.Seed) * .13f + drag) * ends, lag = (Roll(s, time - .28f, strip.Seed) * .13f + drag * 1.3f) * ends;
                float post = PostRise * Post(s) * (.8f + .4f * Mathf.PerlinNoise(time * 2.4f + (s < .5f ? 3f : 11f), strip.Seed));
                float outer = ends * (GateBase + GateReach * Blaze(s, time, strip.Seed) + post) * surge * swell;
                float inner = ends * (GateBase + GateReach * Blaze(s, time * 1.2f + 5f, strip.Seed + 3.7f) + post) * .6f * surge * swell;
                float core = ends * (GateBase + GateReach * Blaze(s, time * 1.5f + 9f, strip.Seed + 7.1f) * .6f + post * .5f) * .3f * surge;
                int at = i * Rows;
                curtainPoints[at] = new Vector3(x, .04f, 0f);
                curtainPoints[at + 1] = new Vector3(x + lean * .4f, .04f + outer * .42f, 0f);
                curtainPoints[at + 2] = new Vector3(x + lean, .04f + outer, 0f);
                at += PerLayer;
                curtainPoints[at] = new Vector3(x, .04f, -.02f);
                curtainPoints[at + 1] = new Vector3(x + lag * .3f, .04f + inner * .4f, -.02f);
                curtainPoints[at + 2] = new Vector3(x + lag * .75f, .04f + inner, -.02f);
                at += PerLayer;
                curtainPoints[at] = new Vector3(x, .04f, -.04f);
                curtainPoints[at + 1] = new Vector3(x + lag * .1f, .04f + core * .45f, -.04f);
                curtainPoints[at + 2] = new Vector3(x + lag * .3f, .04f + core, -.04f);
            }
            strip.Curtain.SetVertices(curtainPoints);
            strip.Curtain.bounds = GateBounds;
        }

        // ------------------------------------------------------------------ gates

        /// <summary>
        /// One gate in the middle of the arena, standing square across it. Once it is up it slides from side to
        /// side (see Tick), as far as the side walls allow.
        /// </summary>
        public void Open(float halfWidth)
        {
            Clear();
            travel = Mathf.Max(0f, halfWidth - GateWide * GateSize * .5f - EdgeGap);
            strips.Add(BuildStrip(Vector3.zero, 0f));
        }

        Strip BuildStrip(Vector3 centre, float yaw)
        {
            var strip = new Strip
            {
                Home = centre, Centre = centre, Turn = Quaternion.Euler(0f, yaw, 0f), Seed = Random.Range(0f, 20f),
                Swing = Random.value < .5f ? -1f : 1f
            };
            strip.Root = new GameObject("Power gate").transform;
            strip.Root.SetParent(root, false);
            strip.Root.SetPositionAndRotation(centre, strip.Turn);
            strip.Root.localScale = Vector3.one * .01f;
            strip.Glow = Layer("Gate glow", strip.Root, glowMesh, -4);
            strip.Glow.transform.localPosition = new Vector3(0f, .02f, 0f);
            strip.Glow.transform.localScale = new Vector3(PadHalf + PadRound + .6f, 1f, PadRound + 1.05f);
            strip.Pad = Layer("Gate pad", strip.Root, padMesh, -3);
            strip.Bar = Layer("Gate bar", strip.Root, barMesh, -1);

            strip.Curtain = new Mesh { name = "Colosseum power gate" };
            strip.Curtain.MarkDynamic();
            strip.Curtain.SetVertices(curtainPoints);
            strip.Curtain.colors32 = curtainTints;
            strip.Curtain.triangles = curtainTriangles;
            Blow(strip, Time.time);
            strip.Wall = Layer("Gate light", strip.Root, strip.Curtain, 0);

            // Sign on the gate: crossed swords with a green up-arrow.
            strip.Icon = new GameObject("Power sign").transform;
            strip.Icon.SetParent(root, false);
            if (icon != null)
            {
                var badge = new GameObject("Swords", typeof(SpriteRenderer));
                badge.transform.SetParent(strip.Icon, false);
                badge.transform.localScale = Vector3.one * .54f;
                var picture = badge.GetComponent<SpriteRenderer>();
                picture.sprite = icon;
                picture.sortingOrder = 3;
                picture.shadowCastingMode = ShadowCastingMode.Off;
                picture.receiveShadows = false;
            }
            MeshRenderer arrow = Layer("Up arrow", strip.Icon, arrowMesh, 4);
            arrow.transform.localPosition = new Vector3(.58f, -.26f, -.02f);
            arrow.transform.localScale = Vector3.one * .54f;
            strip.Icon.position = centre + Vector3.up * (1.7f * GateSize);
            strip.Icon.localScale = Vector3.zero;
            return strip;
        }

        MeshRenderer Layer(string name, Transform parent, Mesh mesh, int order)
        {
            var face = Kit.SpawnStroke(name, parent, mesh, glow).GetComponent<MeshRenderer>();
            face.sortingOrder = order;
            return face;
        }

        /// <summary>Gates gone and everyone back to normal size (a new set, or back to the lobby).</summary>
        public void Clear()
        {
            foreach (Strip strip in strips)
            {
                if (strip.Root != null) Object.Destroy(strip.Root.gameObject);
                if (strip.Icon != null) Object.Destroy(strip.Icon.gameObject);
                if (strip.Curtain != null) Object.Destroy(strip.Curtain);
            }
            strips.Clear();
            foreach (Charge charge in charges)
            {
                if (charge.Doll == null) continue;
                charge.Doll.Growth = 1f;
                charge.Doll.SetPowered(false);
            }
            charges.Clear();
            boosted.Clear();
            lastSeen.Clear();
            foreach (Mote twinkle in twinkles) Rest(twinkle);
            foreach (Mote wisp in wisps) Rest(wisp);
        }

        static void Rest(Mote mote)
        {
            mote.Active = false;
            mote.Item.gameObject.SetActive(false);
        }

        /// <summary>The power-up lasts only for the turn it was picked up in: heroes shrink back.</summary>
        public void EndTurn()
        {
            foreach (Charge charge in charges) charge.Leaving = true;
            boosted.Clear();
            lastSeen.Clear();
        }

        // ------------------------------------------------------------------ powering up

        /// <summary>Starts following a hero from where it is slung, so a hitch in the frame rate cannot skip a gate.</summary>
        public void Watch(ColosseumDoll doll)
        {
            if (doll != null) lastSeen[doll] = doll.transform.position;
        }

        /// <summary>
        /// Follows the hero in flight; true the moment it first crosses a gate's strip this turn. Height does not
        /// matter, so the leaper powers up as it sails over; the gate blazes up towards whoever went through.
        /// </summary>
        public bool Passes(ColosseumDoll doll)
        {
            if (doll == null) return false;
            Vector3 now = doll.transform.position;
            bool known = lastSeen.TryGetValue(doll, out Vector3 before);
            lastSeen[doll] = now;
            if (strips.Count == 0 || boosted.Contains(doll)) return false;
            if (!known) before = now;
            int steps = Mathf.Clamp(Mathf.CeilToInt(Kit.Flat(now - before).magnitude / .2f), 1, 16);
            for (int s = 0; s <= steps; s++)
            {
                Vector3 point = Vector3.Lerp(before, now, (float)s / steps);
                foreach (Strip strip in strips)
                {
                    if (strip.Age < .05f) continue;
                    Vector3 local = Quaternion.Inverse(strip.Turn) * Kit.Flat(point - strip.Centre) / GateSize;
                    if (Away(local) * GateSize > PassReach) continue;
                    boosted.Add(doll);
                    charges.Add(new Charge { Doll = doll });
                    doll.SetPowered(true);
                    // The higher the hero is flying, the higher the gate leaps after it.
                    strip.Flare = 1f + Mathf.Clamp01(now.y / 4f);
                    for (int i = 0; i < 14; i++) Sparkle(Around(doll, 1f), 1.5f);
                    for (int i = 0; i < 12; i++)
                        Rise(strip.Centre + strip.Turn * (new Vector3(local.x + Random.Range(-.7f, .7f), Random.Range(.4f, 2.4f), Random.Range(-.15f, .15f)) * GateSize), 1.5f);
                    return true;
                }
            }
            return false;
        }

        public bool IsBoosted(ColosseumDoll doll) { return doll != null && boosted.Contains(doll); }

        /// <summary>
        /// Size of a hero this long after powering up: it flickers between small and big a few times, the way a
        /// plumber does on eating a mushroom, then springs out past full size and settles.
        /// </summary>
        static float Grown(float time)
        {
            if (time < Flick * 5f) return (int)(time / Flick) % 2 == 0 ? GrowScale : 1f;
            float after = time - Flick * 5f;
            return GrowScale + .22f * Mathf.Exp(-after * 6.5f) * Mathf.Cos(after * 21f);
        }

        static Vector3 Around(ColosseumDoll doll, float spread)
        {
            Vector3 feet = doll.transform.position;
            float tall = 1.45f * doll.Bulk;
            return feet + new Vector3(Random.Range(-.55f, .55f) * spread, Random.Range(.15f, 1f) * tall, Random.Range(-.3f, .3f) * spread);
        }

        // ------------------------------------------------------------------ per frame

        public void Tick(float delta, Camera camera)
        {
            Quaternion face = camera != null ? camera.transform.rotation : Quaternion.identity;
            Vector3 toLens = camera != null ? -camera.transform.forward : Vector3.up;
            float time = Time.time;

            foreach (Strip strip in strips)
            {
                strip.Age += delta;
                // It rises in the middle, waits a moment, then swings from side to side for as long as it stands,
                // slowing to turn at each end.
                float was = Mathf.Sin(strip.Phase) * travel * strip.Swing;
                strip.Phase += delta * SlideRate * Ease(Mathf.Clamp01((strip.Age - SlideWait) / SlideRamp));
                float across = Mathf.Sin(strip.Phase) * travel * strip.Swing;
                strip.Speed = delta > 1e-5f ? (across - was) / delta : strip.Speed;
                strip.Centre = strip.Home + strip.Turn * new Vector3(across, 0f, 0f);
                strip.Root.position = strip.Centre;
                strip.Flare = Mathf.Max(0f, strip.Flare - delta * 2.4f);
                float grow = Mathf.Clamp01(strip.Age / .45f);
                float pop = 1f + Mathf.Sin(grow * Mathf.PI) * .18f;
                strip.Root.localScale = Vector3.one * Mathf.Max(.01f, grow * pop * GateSize);
                Blow(strip, time);
                float flare = Mathf.Min(1f, strip.Flare);
                block.SetColor("_Color", new Color(1f, 1f, 1f, .86f + Mathf.Sin(time * 3.3f + strip.Seed) * .05f + flare * .09f));
                strip.Wall.SetPropertyBlock(block);
                block.SetColor("_Color", new Color(1f, 1f, 1f, .8f + Mathf.Sin(time * 2.6f + strip.Seed) * .15f + flare * .05f));
                strip.Glow.SetPropertyBlock(block);
                // The light on the sand laps in and out a little, in time with the wall above it.
                float lap = Mathf.Sin(time * 1.15f + strip.Seed * 2.3f);
                block.SetColor("_Color", new Color(1f, 1f, 1f, .9f + lap * .08f));
                strip.Pad.SetPropertyBlock(block);
                strip.Pad.transform.localScale = new Vector3(1f + lap * .012f, 1f, 1f + lap * .07f);
                strip.Glow.transform.localScale = new Vector3(PadHalf + PadRound + .6f, 1f, PadRound + 1.05f) * (1f + lap * .05f);
                block.SetColor("_Color", new Color(1f, 1f, 1f, .92f + Mathf.Sin(time * 3.3f + strip.Seed) * .06f + flare * .02f));
                strip.Bar.SetPropertyBlock(block);
                // The sign hangs in front of the wall of light, on the camera's side of it.
                strip.Icon.position = strip.Centre + Vector3.up * ((1.7f + Mathf.Sin(time * 2.4f + strip.Seed) * .07f) * GateSize) + toLens * .45f;
                strip.Icon.rotation = face;
                strip.Icon.localScale = Vector3.one * (SignSize * grow * pop * (1f + Mathf.Sin(time * 5f) * .04f + flare * .22f));

                strip.TwinkleClock += delta;
                while (strip.TwinkleClock > .3f)
                {
                    strip.TwinkleClock -= .3f;
                    Sparkle(strip.Centre + strip.Turn * (new Vector3(Random.Range(-.5f, .5f) * GateWide, Random.Range(.4f, 3.4f), Random.Range(-.3f, .3f)) * GateSize), .8f);
                }
                // Licks of light keep breaking away and rising: this is what makes the gate look alight.
                strip.WispClock += delta * grow;
                while (strip.WispClock > .055f)
                {
                    strip.WispClock -= .055f;
                    Rise(strip.Centre + strip.Turn * (new Vector3(Random.Range(-.46f, .46f) * GateWide, Random.Range(.5f, 2.9f), Random.Range(-.12f, .12f)) * GateSize), .9f);
                }
            }

            // The light round a powered-up hero comes out as it grows and is drawn back in as it shrinks.
            float light = 0f;
            for (int i = charges.Count - 1; i >= 0; i--)
            {
                Charge charge = charges[i];
                if (charge.Doll == null) { charges.RemoveAt(i); continue; }
                charge.Age += delta;
                if (charge.Leaving)
                {
                    // Back to normal size, with a small dip on the way down.
                    charge.Gone += delta;
                    float k = Mathf.Clamp01(charge.Gone / Shrink);
                    float size = Mathf.Lerp(GrowScale, 1f, k * k * (3f - 2f * k)) - Mathf.Sin(k * Mathf.PI) * .05f;
                    charge.Doll.Growth = k >= 1f ? 1f : size;
                    light = Mathf.Max(light, 1f - k);
                    if (k >= 1f)
                    {
                        charge.Doll.SetPowered(false);
                        charges.RemoveAt(i);
                    }
                    continue;
                }
                charge.Doll.Growth = Grown(charge.Age);
                // It flares past its full reach as the hero springs out, then settles.
                float lit = Mathf.Clamp01(charge.Age / (Flick * 5f));
                light = Mathf.Max(light, lit + .45f * Mathf.Exp(-Mathf.Max(0f, charge.Age - Flick * 5f) * 5f) * lit);
                charge.TwinkleClock += delta;
                while (charge.TwinkleClock > .1f)
                {
                    charge.TwinkleClock -= .1f;
                    Sparkle(Around(charge.Doll, 1f), 1f);
                }
            }

            if (charges.Count > 0) Kit.SetAura(light, time);

            foreach (Mote twinkle in twinkles)
            {
                if (!twinkle.Active) continue;
                twinkle.Age += delta;
                float k = twinkle.Age / twinkle.Life;
                if (k >= 1f) { Rest(twinkle); continue; }
                twinkle.Item.position += twinkle.Velocity * delta;
                twinkle.Item.rotation = face * Quaternion.Euler(0f, 0f, twinkle.Spin * twinkle.Age);
                // Blinks open and shut once.
                twinkle.Item.localScale = Vector3.one * (twinkle.Size * Mathf.Sin(k * Mathf.PI));
            }

            foreach (Mote wisp in wisps)
            {
                if (!wisp.Active) continue;
                wisp.Age += delta;
                float k = wisp.Age / wisp.Life;
                if (k >= 1f) { Rest(wisp); continue; }
                wisp.Item.position += wisp.Velocity * delta;
                wisp.Item.rotation = face * Quaternion.Euler(0f, 0f, wisp.Spin);
                // Swells as it breaks away, then thins out and fades as it climbs.
                float swell = Mathf.Sin(Mathf.Sqrt(k) * Mathf.PI);
                wisp.Item.localScale = new Vector3(wisp.Size * (.45f + .55f * swell), wisp.Tall * (.55f + .45f * swell), 1f);
                block.SetColor("_Color", new Color(1f, 1f, 1f, swell));
                wisp.Face.SetPropertyBlock(block);
            }
        }

        void Sparkle(Vector3 at, float strength)
        {
            Mote twinkle = twinkles[twinkleCursor];
            twinkleCursor = (twinkleCursor + 1) % twinkles.Count;
            twinkle.Active = true;
            twinkle.Age = 0f;
            twinkle.Life = Random.Range(.34f, .56f);
            twinkle.Size = Random.Range(.32f, .52f) * strength;
            twinkle.Spin = Random.Range(-90f, 90f);
            twinkle.Velocity = new Vector3(Random.Range(-.3f, .3f), Random.Range(.4f, 1f), Random.Range(-.2f, .2f)) * strength;
            twinkle.Item.position = at;
            twinkle.Item.localScale = Vector3.zero;
            twinkle.Item.gameObject.SetActive(true);
        }

        /// <summary>A lick of light breaks away from a gate and climbs.</summary>
        void Rise(Vector3 at, float strength)
        {
            Mote wisp = wisps[wispCursor];
            wispCursor = (wispCursor + 1) % wisps.Count;
            wisp.Active = true;
            wisp.Age = 0f;
            wisp.Life = Random.Range(.6f, 1f);
            wisp.Size = Random.Range(.17f, .28f) * strength;
            wisp.Tall = Random.Range(.65f, 1.1f) * strength;
            wisp.Spin = Random.Range(-9f, 9f);
            wisp.Velocity = new Vector3(Random.Range(-.18f, .18f), Random.Range(1.3f, 2.3f) * strength, 0f);
            wisp.Item.position = at;
            wisp.Item.localScale = Vector3.zero;
            wisp.Item.gameObject.SetActive(true);
        }
    }
}
