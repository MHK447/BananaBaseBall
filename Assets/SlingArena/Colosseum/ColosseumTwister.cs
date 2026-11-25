using System.Collections.Generic;
using UnityEngine;

namespace SlingArena.Colosseum
{
    /// <summary>
    /// The viking's whirlwind, as it is seen. A funnel of turning air stands where he does, narrow at the sand and
    /// wide open at the top: six storeys of it, one on another, each going round at its own pace and swaying a
    /// little out of line with the rest, striped with wind. Under it the sand is drawn in: arms of dust going
    /// round, and rings closing on the middle from as far out as it reaches. Chaff goes up inside it. When it
    /// bursts the funnel flies apart upwards and gusts go up after whoever it has thrown at the sky; where each of
    /// those is going to come down again a ring is marked on the sand, closing in as they fall. One it has
    /// finished does not come down: there is a twinkle at the top of the view where it went.
    /// What the whirlwind does to anyone is the game's business (see ColosseumGame.TickWhirl); this only draws it.
    /// </summary>
    public sealed class ColosseumTwister
    {
        const int Storeys = 6, Rings = 3, Bits = 20, Gusts = 14, Marks = 8, Glints = 4;
        /// <summary>The funnel at full size, which is the size for a whirlwind that reaches FullReach: how tall, and how wide at the sand and at the top.</summary>
        const float Tall = 5.2f, Foot = .38f, Crown = 2.13f, FullReach = 3f;
        const float GrowFor = .3f, BurstFor = .4f, StopFor = .3f, GlintFor = .8f;

        sealed class Bit
        {
            public Transform Body;
            public float Turn, Height, Wide, Pace, Rise, Size;
        }

        sealed class Gust
        {
            public Transform Body;
            public MeshRenderer Skin;
            public Vector3 Speed;
            public float Age, Life, Wide, Long;
            public bool Live;
        }

        sealed class Spot
        {
            public Transform Body;
            public MeshRenderer Skin;
            public Color Tint;
            public float Age, Due, Reach, Seed;
            public bool Live;
        }

        sealed class Glint
        {
            public Transform Body, Halo;
            public MeshRenderer Skin, HaloSkin;
            public float Age;
            public bool Live;
        }

        readonly Transform funnel;
        readonly Transform[] storeys = new Transform[Storeys];
        readonly MeshRenderer[] storeySkins = new MeshRenderer[Storeys];
        readonly float[] storeyFoot = new float[Storeys];
        readonly MeshRenderer shadow, arms;
        readonly MeshRenderer[] rings = new MeshRenderer[Rings];
        readonly List<Bit> bits = new List<Bit>();
        readonly List<Gust> gusts = new List<Gust>();
        readonly List<Spot> marks = new List<Spot>();
        readonly List<Glint> glints = new List<Glint>();
        readonly MaterialPropertyBlock block = new MaterialPropertyBlock();
        Color ringTint = Color.white;
        float clock, age, burstAge = -1f, stopAge = -1f;
        bool turning;
        int nextGust;

        /// <summary>True while the funnel is standing, or flying apart.</summary>
        public bool Turning { get { return turning; } }

        public ColosseumTwister(ColosseumGame owner)
        {
            Transform root = new GameObject("Colosseum whirlwind").transform;
            root.SetParent(owner.World, false);
            Material stroke = Kit.StrokeMaterial(Color.white);
            funnel = new GameObject("Funnel").transform;
            funnel.SetParent(root, false);

            // What it does to the sand: a shadow under it, arms of dust going round, rings closing on the middle.
            var dark = new MeshKit { Gradient = 0f };
            MapMesh.Band(dark, Vector3.zero, 0f, .8f, new Color32(58, 42, 36, 76), new Color32(58, 42, 36, 54), 30);
            MapMesh.Band(dark, Vector3.zero, .8f, 1.9f, new Color32(58, 42, 36, 54), new Color32(58, 42, 36, 0), 30);
            shadow = Layer("Whirlwind shadow", funnel, dark.ToMesh("Colosseum whirlwind shadow"), stroke, -5, .02f);

            var dust = new MeshKit { Gradient = 0f };
            const int armSteps = 18;
            for (int arm = 0; arm < 5; arm++)
            {
                Vector3 innerWas = Vector3.zero, outerWas = Vector3.zero;
                byte alphaWas = 0;
                for (int s = 0; s <= armSteps; s++)
                {
                    // Out at the edge it has hardly begun; it winds in round the way the wind turns.
                    float t = (float)s / armSteps, turn = arm * Mathf.PI * 2f / 5f + t * 2.9f;
                    float swell = Mathf.Max(0f, Mathf.Sin(t * Mathf.PI));
                    float reach = FullReach * (.95f - .72f * t), wide = .04f + .2f * swell;
                    var way = new Vector3(Mathf.Sin(turn), 0f, Mathf.Cos(turn));
                    Vector3 inner = way * (reach - wide), outer = way * (reach + wide);
                    byte alpha = (byte)(swell * 185f);
                    if (s > 0)
                    {
                        MapMesh.Sheet(dust, innerWas, inner, outer, outerWas, new Color32(255, 248, 230, alphaWas), new Color32(255, 248, 230, alpha),
                            new Color32(255, 248, 230, alpha), new Color32(255, 248, 230, alphaWas));
                    }
                    innerWas = inner; outerWas = outer; alphaWas = alpha;
                }
            }
            arms = Layer("Whirlwind dust", funnel, dust.ToMesh("Colosseum whirlwind dust"), stroke, -4, .03f);

            var hoop = new MeshKit { Gradient = 0f };
            MapMesh.Band(hoop, Vector3.zero, .955f, 1f, new Color32(255, 255, 255, 0), new Color32(255, 255, 255, 255), 44);
            MapMesh.Band(hoop, Vector3.zero, 1f, 1.03f, new Color32(255, 255, 255, 255), new Color32(255, 255, 255, 0), 44);
            Mesh hoopMesh = hoop.ToMesh("Colosseum whirlwind ring");
            for (int i = 0; i < Rings; i++)
                rings[i] = Layer("Whirlwind ring", funnel, hoopMesh, stroke, -3, .036f);

            // The funnel itself.
            var dice = new System.Random(7);
            for (int i = 0; i < Storeys; i++)
            {
                storeyFoot[i] = (float)i / Storeys * Tall;
                storeySkins[i] = Layer("Whirlwind storey", funnel, Storey(i, dice), stroke, 1, storeyFoot[i]);
                storeys[i] = storeySkins[i].transform;
            }

            // Chaff: grains and clods of the court going up inside it.
            int[] tones = { 0xf7ead0, 0xd9c090, 0xb8925a, 0x8e6c48, 0xffffff };
            for (int i = 0; i < Bits; i++)
            {
                var bit = new Bit { Body = Kit.Spawn("Whirlwind chaff", funnel, Kit.UnitBall(tones[i % tones.Length], 0f), false, false).transform };
                bit.Body.gameObject.SetActive(false);
                bits.Add(bit);
            }
            funnel.gameObject.SetActive(false);

            // A gust: a streak of air going straight up, bright at its head and gone at its tail. Stood up to face the lens.
            var streak = new MeshKit { Gradient = 0f };
            streak.Quad(new Vector3(-.5f, -1f, 0f), new Vector3(.5f, -1f, 0f), new Vector3(.5f, -.1f, 0f), new Vector3(-.5f, -.1f, 0f),
                new Color32(255, 255, 255, 0), new Color32(255, 255, 255, 0), new Color32(255, 255, 255, 230), new Color32(255, 255, 255, 230));
            streak.Quad(new Vector3(-.5f, -.1f, 0f), new Vector3(.5f, -.1f, 0f), new Vector3(.12f, 0f, 0f), new Vector3(-.12f, 0f, 0f),
                new Color32(255, 255, 255, 230), new Color32(255, 255, 255, 230), new Color32(255, 255, 255, 120), new Color32(255, 255, 255, 120));
            Mesh streakMesh = streak.ToMesh("Colosseum whirlwind gust");
            for (int i = 0; i < Gusts; i++)
            {
                var gust = new Gust { Skin = Layer("Whirlwind gust", root, streakMesh, stroke, 2, 0f) };
                gust.Body = gust.Skin.transform;
                gust.Body.gameObject.SetActive(false);
                gusts.Add(gust);
            }

            // Where one of them is coming down: a faint wash, a firm ring at its edge, four ticks pointing in.
            var ring = new MeshKit { Gradient = 0f };
            ring.Disc(Vector3.zero, 1f, 1f, new Color32(255, 255, 255, 34), new Color32(255, 255, 255, 86), 36);
            ring.Ring(new Vector3(0f, .003f, 0f), .88f, 1f, new Color32(255, 255, 255, 240), 40);
            for (int i = 0; i < 4; i++)
            {
                float a = (i * 90f + 45f) * Mathf.Deg2Rad;
                Vector3 way = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)), side = new Vector3(-way.z, 0f, way.x) * .08f, up = Vector3.up * .005f;
                ring.Quad(way * .42f - side + up, way * .42f + side + up, way * .82f + side + up, way * .82f - side + up, new Color32(255, 255, 255, 225));
            }
            Mesh markMesh = ring.ToMesh("Colosseum whirlwind mark");
            for (int i = 0; i < Marks; i++)
            {
                var mark = new Spot { Skin = Layer("Whirlwind mark", root, markMesh, stroke, -6, 0f), Seed = i * 2.3f };
                mark.Body = mark.Skin.transform;
                mark.Body.gameObject.SetActive(false);
                marks.Add(mark);
            }

            // The twinkle: a long four-pointed star with a shorter one across it, and a soft light behind.
            var star = new MeshKit { Gradient = 0f };
            var white = new Color32(255, 255, 255, 255);
            for (int arm = 0; arm < 8; arm++)
            {
                float a = arm * 45f * Mathf.Deg2Rad, b = a + 22.5f * Mathf.Deg2Rad, c = a - 22.5f * Mathf.Deg2Rad;
                float reach = arm % 2 == 0 ? 1f : .42f, waist = arm % 2 == 0 ? .13f : .1f;
                Vector3 point = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * reach;
                Vector3 left = new Vector3(Mathf.Cos(b), Mathf.Sin(b), 0f) * waist, right = new Vector3(Mathf.Cos(c), Mathf.Sin(c), 0f) * waist;
                star.Quad(Vector3.zero, right, point, left, white);
            }
            Mesh starMesh = star.ToMesh("Colosseum whirlwind twinkle");
            var glow = new MeshKit { Gradient = 0f };
            glow.Disc(Vector3.zero, 1f, 1f, new Color32(255, 255, 255, 190), new Color32(255, 255, 255, 0), 28);
            Mesh glowMesh = glow.ToMesh("Colosseum whirlwind glow");
            for (int i = 0; i < Glints; i++)
            {
                var glint = new Glint { Skin = Layer("Whirlwind twinkle", root, starMesh, stroke, 5, 0f) };
                glint.Body = glint.Skin.transform;
                glint.HaloSkin = Layer("Whirlwind glow", glint.Body, glowMesh, stroke, 4, 0f);
                glint.Halo = glint.HaloSkin.transform;
                // The disc is drawn lying down; stood up behind the star it faces whoever the star is facing.
                glint.Halo.localRotation = Quaternion.Euler(90f, 0f, 0f);
                glint.Halo.localPosition = new Vector3(0f, 0f, -.05f);
                glint.Body.gameObject.SetActive(false);
                glints.Add(glint);
            }
        }

        static MeshRenderer Layer(string name, Transform parent, Mesh mesh, Material stroke, int order, float height)
        {
            var face = Kit.SpawnStroke(name, parent, mesh, stroke).GetComponent<MeshRenderer>();
            face.sortingOrder = order;
            face.transform.localPosition = new Vector3(0f, height, 0f);
            return face;
        }

        /// <summary>How wide the funnel is, so far up it (0 at the sand, 1 at the top).</summary>
        static float Girth(float up) { return Foot + (Crown - Foot) * Mathf.Pow(Mathf.Clamp01(up), 1.45f); }

        /// <summary>
        /// One storey of the funnel, in its own measure from its own foot: a wall of air, streaky round its girth
        /// and thinner towards the top; three broad stripes of wind that climb the whole of it as they go round,
        /// thick in the middle and gone at both ends; and two thin dark ones between them. Each storey stands a
        /// little into the one above, so that no gap opens between them as they sway. The top one has a lip.
        /// </summary>
        static Mesh Storey(int index, System.Random dice)
        {
            var kit = new MeshKit { Gradient = 0f };
            float from = (float)index / Storeys, to = Mathf.Min(1f, (index + 1.22f) / Storeys), tall = (to - from) * Tall;
            float phase = (float)dice.NextDouble() * Mathf.PI * 2f;
            Vector3 At(float turn, float up, float proud)
            {
                float reach = Girth(from + (to - from) * up) + proud;
                return new Vector3(Mathf.Sin(turn) * reach, up * tall, Mathf.Cos(turn) * reach);
            }
            float Between(float low, float high) { return low + (high - low) * (float)dice.NextDouble(); }

            const int around = 28;
            for (int s = 0; s < around; s++)
            {
                float t0 = (float)s / around * Mathf.PI * 2f, t1 = (s + 1f) / around * Mathf.PI * 2f;
                float thick0 = .5f + .25f * (1f + Mathf.Sin(2f * t0 + phase)), thick1 = .5f + .25f * (1f + Mathf.Sin(2f * t1 + phase));
                kit.Quad(At(t0, 0f, 0f), At(t1, 0f, 0f), At(t1, 1f, 0f), At(t0, 1f, 0f),
                    new Color32(118, 146, 190, (byte)(118f * thick0)), new Color32(118, 146, 190, (byte)(118f * thick1)),
                    new Color32(118, 146, 190, (byte)(58f * thick1)), new Color32(118, 146, 190, (byte)(58f * thick0)));
            }

            for (int stripe = 0; stripe < 3; stripe++)
            {
                // It trails back against the way the wind turns, climbing as it goes.
                float start = phase + stripe * 2.09f + Between(-.25f, .25f), sweep = Between(170f, 230f) * Mathf.Deg2Rad;
                const int steps = 16;
                Vector3 lowWas = Vector3.zero, highWas = Vector3.zero;
                byte alphaWas = 0;
                for (int s = 0; s <= steps; s++)
                {
                    float along = (float)s / steps, swell = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(along * Mathf.PI)), .7f);
                    float turn = start - sweep * along, up = .04f + .92f * along, wide = .06f + .3f * swell;
                    Vector3 low = At(turn, Mathf.Max(0f, up - wide * .5f), .02f), high = At(turn, Mathf.Min(1f, up + wide * .5f), .02f);
                    byte alpha = (byte)(215f * swell);
                    if (s > 0)
                    {
                        kit.Quad(lowWas, low, high, highWas, new Color32(255, 255, 255, alphaWas), new Color32(255, 255, 255, alpha),
                            new Color32(255, 255, 255, alpha), new Color32(255, 255, 255, alphaWas));
                    }
                    lowWas = low; highWas = high; alphaWas = alpha;
                }
            }

            for (int stripe = 0; stripe < 2; stripe++)
            {
                float start = phase + 1f + stripe * 3.1f, sweep = Between(120f, 180f) * Mathf.Deg2Rad;
                const int steps = 12;
                Vector3 lowWas = Vector3.zero, highWas = Vector3.zero;
                byte alphaWas = 0;
                for (int s = 0; s <= steps; s++)
                {
                    float along = (float)s / steps, swell = Mathf.Max(0f, Mathf.Sin(along * Mathf.PI));
                    float turn = start - sweep * along, up = .12f + .76f * along, wide = .03f + .11f * swell;
                    Vector3 low = At(turn, up - wide * .5f, .035f), high = At(turn, up + wide * .5f, .035f);
                    byte alpha = (byte)(150f * swell);
                    if (s > 0)
                    {
                        kit.Quad(lowWas, low, high, highWas, new Color32(70, 96, 150, alphaWas), new Color32(70, 96, 150, alpha),
                            new Color32(70, 96, 150, alpha), new Color32(70, 96, 150, alphaWas));
                    }
                    lowWas = low; highWas = high; alphaWas = alpha;
                }
            }

            if (index == Storeys - 1)
            {
                for (int s = 0; s < around; s++)
                {
                    float t0 = (float)s / around * Mathf.PI * 2f, t1 = (s + 1f) / around * Mathf.PI * 2f;
                    kit.Quad(At(t0, .9f, .01f), At(t1, .9f, .01f), At(t1, 1f, .01f), At(t0, 1f, .01f),
                        new Color32(255, 255, 255, 60), new Color32(255, 255, 255, 60), new Color32(255, 255, 255, 190), new Color32(255, 255, 255, 190));
                }
            }
            return kit.ToMesh("Colosseum whirlwind storey");
        }

        void Tint(Renderer part, Color color)
        {
            block.SetColor("_Color", color);
            part.SetPropertyBlock(block);
        }

        // ------------------------------------------------------------------ what the game asks

        /// <summary>It begins: where it stands, how far it reaches across the sand, and whose it is.</summary>
        public void Begin(Vector3 eye, float reach, int team)
        {
            funnel.position = new Vector3(eye.x, 0f, eye.z);
            funnel.localScale = Vector3.one * (Mathf.Max(.5f, reach) / FullReach);
            ringTint = MeshKit.Rgb(Kit.TeamLight[Mathf.Clamp(team, 0, 1)], 1f);
            age = 0f;
            burstAge = stopAge = -1f;
            turning = true;
            for (int i = 0; i < bits.Count; i++)
            {
                Bit bit = bits[i];
                bit.Turn = Random.Range(0f, 360f);
                bit.Height = Random.value;
                bit.Wide = Random.Range(.8f, 1.3f);
                bit.Pace = Random.Range(380f, 660f);
                bit.Rise = Random.Range(.4f, .9f);
                bit.Size = Random.Range(.05f, .11f);
                bit.Body.gameObject.SetActive(true);
            }
            funnel.gameObject.SetActive(true);
            Pose(0f);
        }

        /// <summary>It bursts, throwing whoever it had at the sky: the funnel flies apart upwards, and gusts go up after them.</summary>
        public void Burst()
        {
            if (!turning || burstAge >= 0f) return;
            burstAge = 0f;
            stopAge = -1f;
            Vector3 eye = funnel.position;
            float size = funnel.localScale.x;
            for (int i = 0; i < Gusts; i++)
            {
                Gust gust = gusts[nextGust];
                nextGust = (nextGust + 1) % gusts.Count;
                Vector2 off = Random.insideUnitCircle * (1.5f * size);
                gust.Live = true;
                gust.Age = -Random.Range(0f, .1f);
                gust.Life = Random.Range(.3f, .5f);
                gust.Wide = Random.Range(.07f, .15f) * size;
                gust.Long = Random.Range(1.6f, 3.4f) * size;
                gust.Speed = Vector3.up * Random.Range(22f, 40f);
                gust.Body.position = eye + new Vector3(off.x, Random.Range(.6f, 2.4f) * size, off.y);
                gust.Body.localScale = Vector3.one * .001f;
                gust.Body.gameObject.SetActive(true);
            }
        }

        /// <summary>It dies away where it stands, having thrown nobody anywhere.</summary>
        public void Stop()
        {
            if (!turning || burstAge >= 0f || stopAge >= 0f) return;
            stopAge = 0f;
        }

        /// <summary>The place where one it threw out is coming down: how long from now, how wide the mark, and whose side they are on.</summary>
        public void Mark(Vector3 landing, float due, float reach, int team)
        {
            Spot pick = null;
            foreach (Spot mark in marks)
            {
                if (!mark.Live) { pick = mark; break; }
                if (pick == null || mark.Age / mark.Due > pick.Age / pick.Due) pick = mark;
            }
            pick.Live = true;
            pick.Age = 0f;
            pick.Due = Mathf.Max(.1f, due);
            pick.Reach = Mathf.Max(.3f, reach);
            pick.Tint = MeshKit.Rgb(Kit.TeamLight[Mathf.Clamp(team, 0, 1)], 1f);
            pick.Body.position = new Vector3(landing.x, .03f, landing.z);
            pick.Body.localScale = Vector3.one * .001f;
            pick.Body.gameObject.SetActive(true);
        }

        /// <summary>One it finished has gone for good: a twinkle here.</summary>
        public void Twinkle(Vector3 at)
        {
            Glint pick = glints[0];
            foreach (Glint glint in glints)
            {
                if (!glint.Live) { pick = glint; break; }
                if (glint.Age > pick.Age) pick = glint;
            }
            pick.Live = true;
            pick.Age = 0f;
            pick.Body.position = at;
            pick.Body.localScale = Vector3.one * .001f;
            pick.Body.gameObject.SetActive(true);
        }

        /// <summary>Nothing of it left anywhere (a new set, the lobby, a turn cut short).</summary>
        public void Clear()
        {
            turning = false;
            burstAge = stopAge = -1f;
            funnel.gameObject.SetActive(false);
            foreach (Gust gust in gusts)
            {
                gust.Live = false;
                gust.Body.gameObject.SetActive(false);
            }
            foreach (Spot mark in marks)
            {
                mark.Live = false;
                mark.Body.gameObject.SetActive(false);
            }
            foreach (Glint glint in glints)
            {
                glint.Live = false;
                glint.Body.gameObject.SetActive(false);
            }
        }

        // ------------------------------------------------------------------ every frame

        public void Tick(float delta, Vector3 towardLens)
        {
            clock += delta;
            if (turning)
            {
                age += delta;
                if (burstAge >= 0f) burstAge += delta;
                if (stopAge >= 0f) stopAge += delta;
                if (burstAge >= BurstFor || stopAge >= StopFor)
                {
                    turning = false;
                    funnel.gameObject.SetActive(false);
                }
                else
                {
                    Pose(delta);
                }
            }

            Quaternion facing = Quaternion.LookRotation(towardLens, Vector3.up);
            foreach (Gust gust in gusts)
            {
                if (!gust.Live) continue;
                gust.Age += delta;
                if (gust.Age < 0f) continue;
                float k = gust.Age / gust.Life;
                if (k >= 1f)
                {
                    gust.Live = false;
                    gust.Body.gameObject.SetActive(false);
                    continue;
                }
                gust.Body.position += gust.Speed * delta;
                gust.Body.rotation = facing;
                gust.Body.localScale = new Vector3(gust.Wide, gust.Long * (.4f + .6f * Mathf.Clamp01(k * 3f)), 1f);
                Tint(gust.Skin, new Color(1f, 1f, 1f, (1f - k * k) * .8f));
            }

            foreach (Spot mark in marks)
            {
                if (!mark.Live) continue;
                mark.Age += delta;
                float t = mark.Age / mark.Due;
                if (t >= 1f)
                {
                    mark.Live = false;
                    mark.Body.gameObject.SetActive(false);
                    continue;
                }
                // It snaps in wide and closes on the place, quickening with the fall.
                float close = t * t, reach = mark.Reach * Mathf.Lerp(2f, 1f, close) * (1f + .04f * Mathf.Sin(clock * 22f + mark.Seed));
                mark.Body.localScale = new Vector3(reach, 1f, reach);
                mark.Body.rotation = Quaternion.Euler(0f, clock * 90f + mark.Seed * 40f, 0f);
                Color tint = Color.Lerp(mark.Tint, Color.white, .25f);
                tint.a = Mathf.Clamp01(mark.Age / .15f) * Mathf.Lerp(.6f, .95f, close);
                Tint(mark.Skin, tint);
            }

            foreach (Glint glint in glints)
            {
                if (!glint.Live) continue;
                glint.Age += delta;
                float k = glint.Age / GlintFor;
                if (k >= 1f)
                {
                    glint.Live = false;
                    glint.Body.gameObject.SetActive(false);
                    continue;
                }
                // Out in a flash, and dwindling with a flicker.
                float size = (k < .18f ? Mathf.Lerp(.2f, 1.7f, k / .18f) : Mathf.Lerp(1.7f, 0f, (k - .18f) / .82f)) * (1f + .16f * Mathf.Sin(glint.Age * 46f));
                glint.Body.rotation = facing * Quaternion.Euler(0f, 0f, glint.Age * 70f);
                glint.Body.localScale = Vector3.one * Mathf.Max(.001f, size);
                Tint(glint.Skin, new Color(1f, .98f, .82f, 1f - k * k * k));
                glint.Halo.localScale = Vector3.one * 1.5f;
                Tint(glint.HaloSkin, new Color(1f, .9f, .5f, .5f * (1f - k)));
            }
        }

        /// <summary>From nothing to full size, running a little past it on the way.</summary>
        static float Pop(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t) + Mathf.Sin(t * Mathf.PI) * .14f;
        }

        /// <summary>Everything of the funnel and of the sand under it, as it stands at this moment.</summary>
        void Pose(float delta)
        {
            float grow = Pop(age / GrowFor), shown = Mathf.Clamp01(age / .1f);
            float burst = burstAge >= 0f ? Mathf.Clamp01(burstAge / BurstFor) : 0f;
            float stop = stopAge >= 0f ? Mathf.Clamp01(stopAge / StopFor) : 0f;
            // Dying away it sinks and thins; bursting, every storey goes up and out and is gone.
            float tall = grow * (1f - .5f * stop), wide = (.45f + .55f * grow) * (1f - .35f * stop);
            float solid = shown * (1f - stop) * (1f - burst) * (1f - burst);
            for (int i = 0; i < Storeys; i++)
            {
                float sway = .1f * i * grow;
                var lean = new Vector3(Mathf.Sin(clock * 2.3f + i * .9f) * sway, 0f, Mathf.Cos(clock * 1.9f + i * .7f) * sway);
                float spread = wide * (1f + 1.6f * burst);
                storeys[i].localPosition = lean + Vector3.up * (storeyFoot[i] * tall + 9f * burst * (1f + .3f * i));
                storeys[i].localRotation = Quaternion.Euler(0f, clock * (640f - 56f * i) + i * 37f, 0f);
                storeys[i].localScale = new Vector3(spread, Mathf.Max(.01f, tall), spread);
                Tint(storeySkins[i], new Color(1f, 1f, 1f, solid));
            }

            float ground = shown * (1f - stop) * (1f - Mathf.Clamp01(burst * 1.4f));
            Tint(shadow, new Color(1f, 1f, 1f, ground));
            arms.transform.localRotation = Quaternion.Euler(0f, clock * 220f, 0f);
            Tint(arms, new Color(1f, 1f, 1f, ground));
            for (int i = 0; i < Rings; i++)
            {
                // Each closes on the middle from as far out as the whirlwind reaches, one after another, for as long as it stands.
                float k = Mathf.Repeat(clock / .7f + (float)i / Rings, 1f), reach = FullReach * (1f - .82f * k);
                rings[i].transform.localScale = new Vector3(reach, 1f, reach);
                Tint(rings[i], new Color(ringTint.r, ringTint.g, ringTint.b, .62f * Mathf.Sin(k * Mathf.PI) * ground));
            }

            foreach (Bit bit in bits)
            {
                bit.Turn += bit.Pace * delta;
                bit.Height += bit.Rise * delta * (1f + 3f * burst);
                if (bit.Height > 1f)
                {
                    // Out of the top and gone; another is picked up off the sand.
                    bit.Height -= 1f;
                    bit.Turn = Random.Range(0f, 360f);
                    bit.Wide = Random.Range(.8f, 1.3f);
                }
                float reach = Girth(bit.Height) * bit.Wide * wide * (1f + 1.2f * burst), turn = bit.Turn * Mathf.Deg2Rad;
                bit.Body.localPosition = new Vector3(Mathf.Sin(turn) * reach, bit.Height * Tall * tall + 4f * burst, Mathf.Cos(turn) * reach);
                bit.Body.localScale = Vector3.one * (bit.Size * (.6f + .9f * bit.Height) * shown * (1f - stop) * (1f - burst));
            }
        }
    }
}
