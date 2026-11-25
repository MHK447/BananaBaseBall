using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace SlingArena.Colosseum
{
    /// <summary>Dust, sparks, confetti, shock rings, flying arrows and the sling aim arrow.</summary>
    public sealed class ColosseumFx
    {
        const int Dust = 0, Spark = 1, Confetti = 2, Flash = 3, Wave = 4, Pow = 5;
        internal const float ArrowLifetime = .9f, ArrowHitRadius = ColosseumDoll.Radius + .14f, ArrowHitHeight = 1.6f;

        sealed class Bit
        {
            public Transform Item;
            public MeshRenderer Renderer;
            public Vector3 Velocity;
            public float Age, Life, Size, Spin;
            public bool Active;
        }

        sealed class Arrow
        {
            public Transform Item;
            public Vector3 Velocity;
            public ColosseumDoll Owner;
            public float Age;
            public bool Flying, Stuck;
        }

        readonly ColosseumGame game;
        readonly List<Bit>[] pools = { new List<Bit>(), new List<Bit>(), new List<Bit>(), new List<Bit>(), new List<Bit>(), new List<Bit>() };
        readonly int[] cursors = new int[6];
        readonly List<Arrow> arrows = new List<Arrow>();
        readonly Mesh[] arrowMeshes = new Mesh[2];
        readonly MaterialPropertyBlock block = new MaterialPropertyBlock();
        readonly Transform root;
        readonly Material flashMaterial;
        int arrowCursor;
        readonly Transform[] craters = new Transform[6];
        readonly Material[] craterTint = new Material[6];
        readonly float[] craterAge = new float[6];
        int craterCursor;

        readonly Mesh aimMesh;
        readonly GameObject aimObject;
        readonly List<Vector3> aimVerts = new List<Vector3>();
        readonly List<Color32> aimColors = new List<Color32>();
        readonly List<int> aimTris = new List<int>();

        public int FlyingArrows { get; private set; }

        public ColosseumFx(ColosseumGame owner)
        {
            game = owner;
            root = new GameObject("Colosseum effects").transform;
            root.SetParent(owner.World, false);
            flashMaterial = Kit.StrokeMaterial(Color.white);

            // Dust is a lumpy little cloud rather than a single ball, so puffs read as kicked-up sand.
            var cloud = new MeshKit { Gradient = .2f };
            Color32 sand = MeshKit.Rgb(0xf7ead0, 0f);
            cloud.Ball(Vector3.zero, .62f, sand, 10);
            cloud.Ball(new Vector3(.5f, .08f, .12f), .42f, sand, 8);
            cloud.Ball(new Vector3(-.46f, .02f, -.14f), .46f, sand, 8);
            cloud.Ball(new Vector3(.04f, .4f, -.05f), .4f, sand, 8);
            cloud.Ball(new Vector3(-.1f, -.05f, .46f), .36f, sand, 8);
            Fill(Dust, 72, cloud.ToMesh("Colosseum dust"), null);
            Fill(Spark, 64, Kit.UnitBall(0xffe14a, 1f), null);
            int[] paper = { Kit.TeamMain[0], Kit.TeamMain[1], Kit.Gold, 0xffffff, Kit.TeamLight[0], Kit.TeamLight[1] };
            for (int i = 0; i < 90; i++)
                Add(Confetti, Kit.UnitBall(paper[i % paper.Length], .4f), null);

            var star = new MeshKit();
            Color32 white = new Color32(255, 255, 255, 255);
            for (int i = 0; i < 8; i++)
            {
                float a0 = i * Mathf.PI / 4f, a1 = a0 + Mathf.PI / 8f, a2 = a0 + Mathf.PI / 4f;
                Vector3 tip = new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1));
                Vector3 left = new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)) * .42f;
                Vector3 right = new Vector3(Mathf.Cos(a2), 0f, Mathf.Sin(a2)) * .42f;
                star.Tri(Vector3.zero, tip, left, white);
                star.Tri(Vector3.zero, right, tip, white);
            }
            Fill(Flash, 6, star.ToMesh("Colosseum flash"), flashMaterial);
            var wave = new MeshKit();
            wave.Ring(Vector3.zero, .86f, 1f, white, 44);
            Fill(Wave, 4, wave.ToMesh("Colosseum wave"), flashMaterial);

            // Comic impact burst: dark rim, yellow spikes, white heart. Drawn back to front inside one mesh.
            var pow = new MeshKit();
            Spikes(pow, 1.1f, .5f, .012f, new Color32(22, 16, 34, 255));
            Spikes(pow, 1f, .43f, .006f, new Color32(255, 206, 46, 255));
            Spikes(pow, .62f, .27f, 0f, white);
            Fill(Pow, 14, pow.ToMesh("Colosseum impact"), flashMaterial);

            for (int team = 0; team < 2; team++)
            {
                var kit = new MeshKit();
                kit.Capsule(new Vector3(0f, 0f, -.34f), new Vector3(0f, 0f, .26f), .02f, .02f, MeshKit.Rgb(0xf6ead2, 0f), 6);
                kit.Capsule(new Vector3(0f, 0f, .26f), new Vector3(0f, 0f, .42f), .055f, .004f, MeshKit.Rgb(Kit.Steel, 1f), 8);
                kit.Blob(new Vector3(0f, 0f, -.32f), new Vector3(.075f, .014f, .09f), Quaternion.identity, MeshKit.Rgb(Kit.TeamLight[team], .2f), 1f, 1f, 8);
                kit.Blob(new Vector3(0f, 0f, -.32f), new Vector3(.014f, .075f, .09f), Quaternion.identity, MeshKit.Rgb(Kit.TeamLight[team], .2f), 1f, 1f, 8);
                arrowMeshes[team] = kit.ToMesh("Colosseum arrow");
            }
            for (int i = 0; i < 56; i++)
            {
                var item = Kit.Spawn("Arrow", root, arrowMeshes[0], true).transform;
                item.gameObject.SetActive(false);
                arrows.Add(new Arrow { Item = item });
            }

            BuildCraters();
            aimMesh = Kit.Own(new Mesh { name = "Colosseum aim" });
            aimMesh.MarkDynamic();
            aimObject = new GameObject("Aim arrow");
            aimObject.transform.SetParent(root, false);
            aimObject.AddComponent<MeshFilter>().sharedMesh = aimMesh;
            var aimRenderer = aimObject.AddComponent<MeshRenderer>();
            aimRenderer.sharedMaterial = flashMaterial;
            aimRenderer.shadowCastingMode = ShadowCastingMode.Off;
            aimRenderer.receiveShadows = false;
            aimObject.SetActive(false);
        }

        /// <summary>Uneven twelve-point starburst lying in the XY plane, one layer of the impact burst.</summary>
        static void Spikes(MeshKit kit, float outer, float inner, float depth, Color32 color)
        {
            const int points = 12;
            for (int i = 0; i < points; i++)
            {
                float a0 = i * Mathf.PI * 2f / points, half = Mathf.PI / points;
                float reach = outer * ((i & 1) == 0 ? 1f : .72f);
                var tip = new Vector3(Mathf.Cos(a0) * reach, Mathf.Sin(a0) * reach, depth);
                var left = new Vector3(Mathf.Cos(a0 - half) * inner, Mathf.Sin(a0 - half) * inner, depth);
                var right = new Vector3(Mathf.Cos(a0 + half) * inner, Mathf.Sin(a0 + half) * inner, depth);
                var centre = new Vector3(0f, 0f, depth);
                kit.Tri(centre, left, tip, color);
                kit.Tri(centre, tip, right, color);
            }
        }

        void Fill(int kind, int count, Mesh mesh, Material stroke)
        {
            for (int i = 0; i < count; i++)
                Add(kind, mesh, stroke);
        }

        void Add(int kind, Mesh mesh, Material stroke)
        {
            GameObject item = stroke != null ? Kit.SpawnStroke("Bit", root, mesh, stroke) : Kit.Spawn("Bit", root, mesh, false, false);
            item.SetActive(false);
            pools[kind].Add(new Bit { Item = item.transform, Renderer = item.GetComponent<MeshRenderer>() });
        }

        Bit Take(int kind, Vector3 position, Vector3 velocity, float life, float size)
        {
            List<Bit> pool = pools[kind];
            Bit bit = pool[cursors[kind]];
            cursors[kind] = (cursors[kind] + 1) % pool.Count;
            bit.Active = true;
            bit.Age = 0f;
            bit.Life = life;
            bit.Size = size;
            bit.Velocity = velocity;
            bit.Spin = Random.Range(-420f, 420f);
            bit.Item.position = position;
            bit.Item.rotation = Quaternion.identity;
            bit.Item.localScale = Vector3.one * .001f;
            bit.Item.gameObject.SetActive(true);
            return bit;
        }

        public void Puff(Vector3 at, int count, float speed = 2.4f)
        {
            for (int i = 0; i < count; i++)
            {
                Vector2 ring = Random.insideUnitCircle.normalized * Random.Range(.3f, 1f) * speed;
                Bit bit = Take(Dust, new Vector3(at.x, Mathf.Max(.12f, at.y), at.z), new Vector3(ring.x, Random.Range(.4f, 1.4f), ring.y),
                    Random.Range(.35f, .65f), Random.Range(.12f, .22f));
                bit.Item.rotation = Random.rotation;
            }
        }

        /// <summary>Dust kicked up behind something sliding along the sand: low, drifting back and fading fast.</summary>
        public void Trail(Vector3 at, Vector3 direction, float speed)
        {
            Vector3 side = Vector3.Cross(Vector3.up, direction);
            int count = speed > 9f ? 2 : 1;
            for (int i = 0; i < count; i++)
            {
                float lean = Random.Range(-1f, 1f);
                Bit bit = Take(Dust, new Vector3(at.x, .09f, at.z) - direction * .12f + side * (lean * .14f),
                    -direction * Random.Range(.5f, 1.3f) + side * (lean * .9f) + Vector3.up * Random.Range(.5f, 1.2f),
                    Random.Range(.36f, .58f), Random.Range(.15f, .24f) * Mathf.Lerp(.8f, 1.25f, Mathf.Clamp01(speed / 22f)));
                bit.Item.rotation = Random.rotation;
            }
        }

        /// <summary>Spiky comic burst that snaps open at a point of contact, turned to face the camera.</summary>
        public void Impact(Vector3 at, float size)
        {
            Bit bit = Take(Pow, new Vector3(at.x, Mathf.Max(at.y, .4f) + .15f, at.z - .1f), Vector3.zero, .24f, size);
            bit.Item.rotation = Quaternion.Euler(ColosseumGame.CameraPitch, 0f, 0f) * Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
        }

        public void Sparks(Vector3 at, int count)
        {
            for (int i = 0; i < count; i++)
                Take(Spark, at, Random.onUnitSphere * Random.Range(4f, 10f) + Vector3.up * 3f, Random.Range(.2f, .42f), Random.Range(.05f, .10f));
        }

        public void Burst(Vector3 at, float size)
        {
            Take(Flash, new Vector3(at.x, Mathf.Max(at.y, 1.2f), at.z), Vector3.zero, .26f, size);
        }

        public void Shockwave(Vector3 at, float radius)
        {
            Take(Wave, new Vector3(at.x, .06f, at.z), Vector3.zero, .42f, radius);
        }

        public void Celebrate(float halfWidth, float halfDepth)
        {
            for (int i = 0; i < 90; i++)
            {
                Take(Confetti, new Vector3(Random.Range(-halfWidth, halfWidth), Random.Range(5f, 9f), Random.Range(-halfDepth, halfDepth)),
                    new Vector3(Random.Range(-1f, 1f), Random.Range(-4.5f, -2f), Random.Range(-1f, 1f)), Random.Range(1.6f, 2.8f), Random.Range(.07f, .12f));
            }
        }

        public void FireArrow(ColosseumDoll owner, Vector3 from, Vector3 velocity)
        {
            Arrow arrow = arrows[arrowCursor];
            arrowCursor = (arrowCursor + 1) % arrows.Count;
            arrow.Owner = owner;
            arrow.Velocity = velocity;
            arrow.Age = 0f;
            arrow.Flying = true;
            arrow.Stuck = false;
            arrow.Item.GetComponent<MeshFilter>().sharedMesh = arrowMeshes[owner.Team];
            arrow.Item.SetPositionAndRotation(from, Quaternion.LookRotation(velocity.normalized));
            arrow.Item.localScale = Vector3.one * Mathf.Lerp(1f, ColosseumDoll.Scale, .65f);
            arrow.Item.gameObject.SetActive(true);
        }

        public void ClearArrows()
        {
            foreach (Arrow arrow in arrows)
            {
                arrow.Flying = arrow.Stuck = false;
                arrow.Item.gameObject.SetActive(false);
            }
            FlyingArrows = 0;
        }

        public void Tick(float delta, IList<ColosseumDoll> dolls, float halfWidth, float halfDepth)
        {
            TickBits(delta);
            TickCraters(delta);
            int flying = 0;
            foreach (Arrow arrow in arrows)
            {
                if (!arrow.Flying) continue;
                arrow.Age += delta;
                Vector3 position = arrow.Item.position + arrow.Velocity * delta;
                bool done = false;
                for (int i = 0; i < dolls.Count && !done; i++)
                {
                    ColosseumDoll doll = dolls[i];
                    if (!doll.Alive || arrow.Owner == null || doll.Team == arrow.Owner.Team) continue;
                    Vector3 gap = Kit.Flat(doll.transform.position - position);
                    if (gap.sqrMagnitude < ArrowHitRadius * ArrowHitRadius && doll.transform.position.y < ArrowHitHeight)
                    {
                        game.OnArrowHit(arrow.Owner, doll, arrow.Velocity, position);
                        arrow.Flying = false;
                        arrow.Item.gameObject.SetActive(false);
                        done = true;
                    }
                }
                if (done) continue;

                bool wall = Mathf.Abs(position.x) > halfWidth - .05f || Mathf.Abs(position.z) > halfDepth - .05f;
                if (wall && game.GapAt(position))
                {
                    // Out through a gap in the wall: there is nothing there for it to stick in.
                    arrow.Flying = false;
                    arrow.Item.gameObject.SetActive(false);
                    continue;
                }
                if (wall || arrow.Age > ArrowLifetime)
                {
                    position.x = Mathf.Clamp(position.x, -halfWidth + .05f, halfWidth - .05f);
                    position.z = Mathf.Clamp(position.z, -halfDepth + .05f, halfDepth - .05f);
                    arrow.Flying = false;
                    arrow.Stuck = true;
                    Vector3 heading = arrow.Velocity.normalized;
                    if (!wall)
                    {
                        position.y = .16f;
                        heading = (heading + Vector3.down * .7f).normalized;
                    }
                    arrow.Item.SetPositionAndRotation(position, Quaternion.LookRotation(heading));
                    continue;
                }
                arrow.Item.position = position;
                flying++;
            }
            FlyingArrows = flying;
        }

        void TickBits(float delta)
        {
            for (int kind = 0; kind < pools.Length; kind++)
            {
                foreach (Bit bit in pools[kind])
                {
                    if (!bit.Active) continue;
                    bit.Age += delta;
                    float k = bit.Age / bit.Life;
                    if (k >= 1f)
                    {
                        bit.Active = false;
                        bit.Item.gameObject.SetActive(false);
                        continue;
                    }
                    bit.Item.position += bit.Velocity * delta;
                    switch (kind)
                    {
                        case Dust:
                            bit.Velocity *= Mathf.Exp(-4f * delta);
                            bit.Item.localScale = Vector3.one * (bit.Size * (.5f + k * 1.1f) * (1f - k * k));
                            break;
                        case Spark:
                            bit.Velocity += Vector3.down * (18f * delta);
                            bit.Item.localScale = Vector3.one * (bit.Size * (1f - k));
                            break;
                        case Confetti:
                            bit.Velocity = new Vector3(bit.Velocity.x, Mathf.Max(bit.Velocity.y, -3.2f), bit.Velocity.z);
                            if (bit.Item.position.y < .05f) bit.Velocity = Vector3.zero;
                            bit.Item.Rotate(bit.Spin * delta, bit.Spin * .6f * delta, 0f);
                            bit.Item.localScale = new Vector3(1f, .12f, .6f) * (bit.Size * Mathf.Clamp01((1f - k) * 4f));
                            break;
                        case Flash:
                        {
                            float grow = k * (2f - k);
                            bit.Item.localScale = Vector3.one * (bit.Size * (.35f + grow * .75f));
                            bit.Item.rotation = Quaternion.Euler(-30f, k * 70f, 0f);
                            block.SetColor("_Color", new Color(1f, .9f, .3f, 1f - k * k));
                            bit.Renderer.SetPropertyBlock(block);
                            break;
                        }
                        case Pow:
                        {
                            float snap = k < .3f ? Mathf.Lerp(.3f, 1.18f, k / .3f) : Mathf.Lerp(1.18f, .92f, (k - .3f) / .7f);
                            bit.Item.localScale = Vector3.one * (bit.Size * snap);
                            block.SetColor("_Color", new Color(1f, 1f, 1f, 1f - k * k * k));
                            bit.Renderer.SetPropertyBlock(block);
                            break;
                        }
                        default:
                        {
                            float grow = k * (2f - k);
                            bit.Item.localScale = Vector3.one * (bit.Size * (.15f + grow * .85f));
                            block.SetColor("_Color", new Color(1f, 1f, 1f, (1f - k) * .9f));
                            bit.Renderer.SetPropertyBlock(block);
                            break;
                        }
                    }
                }
            }
        }

        // ------------------------------------------------------------------ aim arrow

        /// <summary>
        /// Guide for a leaping hero: the sling bands, dots along the arc he will fly (with their shadows on the
        /// sand) and a ring on the ground showing everything the landing will hit.
        /// </summary>
        /// <summary>
        /// The witch's guide: a wide broken ring round the ground her stars will fall on, with as many little stars
        /// hopping about inside it as she has to throw, since no one can say where in it each will land; dots that go
        /// straight up from her, over and down into the middle of it; and behind her, arrowheads the way the kick of
        /// the wand will send her.
        /// </summary>
        public void ShowStarfall(Vector3 home, Vector3 pulled, Vector3 target, float scatter, float burst, int count, float kick, int team)
        {
            aimVerts.Clear(); aimColors.Clear(); aimTris.Clear();
            Vector3 from = new Vector3(home.x, .05f, home.z), to = new Vector3(target.x, .05f, target.z);
            Vector3 direction = to - from;
            float span = direction.magnitude;
            direction = span > .01f ? direction / span : Vector3.forward;
            Vector3 side = Vector3.Cross(Vector3.up, direction).normalized;
            Color32 main = MeshKit.Rgb(Kit.TeamMain[team], 1f);
            var white = new Color32(255, 255, 255, 235);
            var gold = new Color32(255, 222, 80, 255);
            var backing = new Color32(58, 40, 52, 215);

            // The ring: a soft wash and a rim in dashes, because nothing about it is exact.
            Ring(to, 0f, scatter, new Color32(main.r, main.g, main.b, 54));
            Ring(to + Vector3.up * .002f, scatter - .5f, scatter, new Color32(main.r, main.g, main.b, 40));
            const int dashes = 26, pieces = 3;
            for (int i = 0; i < dashes; i += 2)
            {
                for (int k = 0; k < pieces; k++)
                {
                    float a0 = (i + (float)k / pieces) * Mathf.PI * 2f / dashes, a1 = (i + (k + 1f) / pieces) * Mathf.PI * 2f / dashes;
                    Vector3 d0 = new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)), d1 = new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1));
                    Vector3 rim = to + Vector3.up * .004f, line = to + Vector3.up * .006f;
                    Quad(rim + d0 * (scatter - .16f), rim + d0 * scatter, rim + d1 * scatter, rim + d1 * (scatter - .16f), white);
                    Quad(line + d0 * (scatter - .12f), line + d0 * (scatter - .04f), line + d1 * (scatter - .04f), line + d1 * (scatter - .12f), main);
                }
            }

            // The little stars: thrown down afresh four times a second, anywhere in the ring.
            float tick = Mathf.Floor(Time.unscaledTime * 4f);
            for (int i = 0; i < count; i++)
            {
                float first = Mathf.Sin(i * 12.9898f + tick * 78.233f) * 43758.5453f, second = Mathf.Sin(i * 39.346f + tick * 11.135f) * 24634.6345f;
                first -= Mathf.Floor(first);
                second -= Mathf.Floor(second);
                float turn = first * Mathf.PI * 2f, reach = (scatter - .35f) * Mathf.Sqrt(second);
                Vector3 at = to + new Vector3(Mathf.Cos(turn), 0f, Mathf.Sin(turn)) * reach;
                Ring(at + Vector3.up * .007f, 0f, burst * .5f, new Color32(255, 255, 255, 34), 16);
                StarShape(at + Vector3.up * .009f, .31f, .15f, first * 72f, backing);
                StarShape(at + Vector3.up * .012f, .23f, .105f, first * 72f, gold);
            }

            // The way the stars go, in little: dots straight up from her, across overhead, and straight down into the
            // ring, growing as they come.
            int dots = Mathf.Clamp(Mathf.RoundToInt(span / .6f) + 8, 12, 28);
            float peak = Mathf.Lerp(6.5f, 9.5f, Mathf.InverseLerp(3f, 14f, span));
            for (int i = 1; i < dots; i++)
            {
                float t = (float)i / dots, size = Mathf.Lerp(.13f, .25f, t);
                float across = t * t * (3f - 2f * t), climb = 1f - Mathf.Pow(Mathf.Abs(2f * t - 1f), 2.4f);
                Vector3 air = Vector3.Lerp(from, to, across) + Vector3.up * (peak * climb);
                Ring(air, 0f, size, white, 12);
                Ring(air + Vector3.up * .004f, 0f, size * .62f, gold, 12);
            }

            // The kick: three arrowheads behind her, pointing away from where the stars are going, fainter as they go.
            for (int k = 0; k < 3; k++)
            {
                Vector3 centre = from - direction * (ColosseumDoll.Radius + .5f + k * kick * .3f) + Vector3.up * .006f;
                var tint = new Color32(main.r, main.g, main.b, (byte)(235 - k * 70));
                var edge = new Color32(255, 255, 255, (byte)(235 - k * 70));
                Vector3 back = -direction;
                Quad(centre - side * .3f - back * .22f, centre - side * .3f - back * .05f, centre + back * .17f, centre - back * .0f, edge);
                Quad(centre + side * .3f - back * .22f, centre - back * .0f, centre + back * .17f, centre + side * .3f - back * .05f, edge);
                Vector3 raise = Vector3.up * .003f;
                Quad(centre - side * .25f - back * .17f + raise, centre - side * .25f - back * .08f + raise, centre + back * .11f + raise, centre + back * .03f + raise, tint);
                Quad(centre + side * .25f - back * .17f + raise, centre + back * .03f + raise, centre + back * .11f + raise, centre + side * .25f - back * .08f + raise, tint);
            }

            var band = new Color32(255, 255, 255, 200);
            Vector3 grip = new Vector3(pulled.x, .05f, pulled.z);
            for (int i = -1; i <= 1; i += 2)
            {
                Vector3 post = from + side * ((ColosseumDoll.Radius + .06f) * i);
                Vector3 across = Vector3.Cross(Vector3.up, (grip - post).normalized) * .04f;
                Quad(post - across, post + across, grip + across, grip - across, band);
            }

            SubmitAimMesh();
        }

        /// <summary>A five-pointed star lying on the sand.</summary>
        void StarShape(Vector3 centre, float outer, float inner, float turned, Color32 color)
        {
            for (int i = 0; i < 10; i++)
            {
                float a0 = (turned + i * 36f) * Mathf.Deg2Rad, a1 = (turned + (i + 1) * 36f) * Mathf.Deg2Rad;
                float r0 = i % 2 == 0 ? outer : inner, r1 = i % 2 == 0 ? inner : outer;
                Quad(centre, centre + new Vector3(Mathf.Sin(a0), 0f, Mathf.Cos(a0)) * r0, centre + new Vector3(Mathf.Sin(a1), 0f, Mathf.Cos(a1)) * r1, centre, color);
            }
        }

        public void ShowLanding(Vector3 home, Vector3 pulled, Vector3 target, float radius, float innerRadius, float peak, int team)
        {
            aimVerts.Clear(); aimColors.Clear(); aimTris.Clear();
            Vector3 from = new Vector3(home.x, .05f, home.z), to = new Vector3(target.x, .05f, target.z);
            Vector3 direction = to - from;
            float span = direction.magnitude;
            direction = span > .01f ? direction / span : Vector3.forward;
            Vector3 side = Vector3.Cross(Vector3.up, direction).normalized;
            Color32 main = MeshKit.Rgb(Kit.TeamMain[team], 1f);
            var white = new Color32(255, 255, 255, 235);

            // Landing circle: soft fill, a bold rim, an inner ring and a cross at the centre.
            Ring(to, 0f, radius, new Color32(main.r, main.g, main.b, 70));
            Ring(to + Vector3.up * .004f, radius - .16f, radius, white);
            Ring(to + Vector3.up * .006f, radius - .12f, radius - .04f, new Color32(main.r, main.g, main.b, 255));
            Ring(to + Vector3.up * .004f, innerRadius - .04f, innerRadius, new Color32(255, 255, 255, 150));
            Quad(to - side * .3f - direction * .05f, to - side * .3f + direction * .05f, to + side * .3f + direction * .05f, to + side * .3f - direction * .05f, white);
            Quad(to - direction * .3f - side * .05f, to - direction * .3f + side * .05f, to + direction * .3f + side * .05f, to + direction * .3f - side * .05f, white);

            // The arc, as dots that grow towards the landing, each with a faint shadow underneath.
            int dots = Mathf.Clamp(Mathf.RoundToInt(span / .62f), 6, 18);
            for (int i = 1; i < dots; i++)
            {
                float t = (float)i / dots, size = Mathf.Lerp(.15f, .27f, t);
                Vector3 ground = Vector3.Lerp(from, to, t), air = ground + Vector3.up * (peak * 4f * t * (1f - t));
                Ring(ground, 0f, size * .8f, new Color32(60, 36, 24, 60), 10);
                Ring(air, 0f, size, white, 12);
                Ring(air + Vector3.up * .004f, 0f, size * .62f, main, 12);
            }

            var band = new Color32(255, 255, 255, 200);
            Vector3 grip = new Vector3(pulled.x, .05f, pulled.z);
            for (int i = -1; i <= 1; i += 2)
            {
                Vector3 post = from + side * ((ColosseumDoll.Radius + .06f) * i);
                Vector3 across = Vector3.Cross(Vector3.up, (grip - post).normalized) * .04f;
                Quad(post - across, post + across, grip + across, grip - across, band);
            }

            SubmitAimMesh();
        }

        /// <summary>
        /// Guide for the bouncing hero: the sling bands, a small ring at each spot it will touch down on the way, a
        /// big ring where its last great leap comes down, and dots along every arc. The last spot is the big one.
        /// </summary>
        public void ShowBounce(Vector3 home, Vector3 pulled, Vector3[] spots, float hopRadius, float radius, float hopPeak, float peak, int team)
        {
            aimVerts.Clear(); aimColors.Clear(); aimTris.Clear();
            Color32 main = MeshKit.Rgb(Kit.TeamMain[team], 1f);
            var white = new Color32(255, 255, 255, 235);
            Vector3 from = new Vector3(home.x, .05f, home.z), last = from;
            Vector3 end = new Vector3(spots[spots.Length - 1].x, .05f, spots[spots.Length - 1].z);
            Vector3 direction = end - from;
            direction = direction.sqrMagnitude > .0001f ? direction.normalized : Vector3.forward;
            Vector3 side = Vector3.Cross(Vector3.up, direction).normalized;
            for (int k = 0; k < spots.Length; k++)
            {
                bool great = k == spots.Length - 1;
                Vector3 to = new Vector3(spots[k].x, .05f, spots[k].z);
                float reach = great ? radius : hopRadius, rise = great ? peak : hopPeak;
                Ring(to, 0f, reach, new Color32(main.r, main.g, main.b, (byte)(great ? 70 : 40)));
                Ring(to + Vector3.up * .004f, reach - (great ? .16f : .08f), reach, great ? white : new Color32(255, 255, 255, 190));
                if (great)
                {
                    Ring(to + Vector3.up * .006f, reach - .12f, reach - .04f, new Color32(main.r, main.g, main.b, 255));
                    Ring(to + Vector3.up * .004f, reach * .5f - .04f, reach * .5f, new Color32(255, 255, 255, 150));
                    Quad(to - side * .3f - direction * .05f, to - side * .3f + direction * .05f, to + side * .3f + direction * .05f, to + side * .3f - direction * .05f, white);
                    Quad(to - direction * .3f - side * .05f, to - direction * .3f + side * .05f, to + direction * .3f + side * .05f, to + direction * .3f - side * .05f, white);
                }
                // The arc to this spot, as dots, each with a faint shadow underneath.
                int dots = Mathf.Clamp(Mathf.RoundToInt((to - last).magnitude / (great ? .62f : .5f)), great ? 5 : 3, 16);
                for (int i = 1; i < dots; i++)
                {
                    float t = (float)i / dots, size = great ? Mathf.Lerp(.15f, .27f, t) : .12f;
                    Vector3 ground = Vector3.Lerp(last, to, t), air = ground + Vector3.up * (rise * 4f * t * (1f - t));
                    Ring(ground, 0f, size * .8f, new Color32(60, 36, 24, 60), 10);
                    Ring(air, 0f, size, white, 12);
                    Ring(air + Vector3.up * .004f, 0f, size * .62f, main, 12);
                }
                last = to;
            }

            var band = new Color32(255, 255, 255, 200);
            Vector3 grip = new Vector3(pulled.x, .05f, pulled.z);
            for (int i = -1; i <= 1; i += 2)
            {
                Vector3 post = from + side * ((ColosseumDoll.Radius + .06f) * i);
                Vector3 across = Vector3.Cross(Vector3.up, (grip - post).normalized) * .04f;
                Quad(post - across, post + across, grip + across, grip - across, band);
            }

            SubmitAimMesh();
        }

        /// <summary>
        /// Guide for the boomerang rider: the sling bands, the whole loop he will fly drawn as a lane as wide as his
        /// blade cuts, with a line of arrowheads down the middle of it, and a ring where he comes down.
        /// </summary>
        public void ShowLoop(Vector3 home, Vector3 pulled, List<Vector3> path, List<Vector3> hint, float reach, int team)
        {
            aimVerts.Clear(); aimColors.Clear(); aimTris.Clear();
            Color32 main = MeshKit.Rgb(Kit.TeamMain[team], 1f);
            var white = new Color32(255, 255, 255, 235);
            var lane = new Color32(main.r, main.g, main.b, 44);
            var body = new Color32(255, 255, 255, 125);
            var chevron = new Color32(176, 132, 80, 230);
            int count = path.Count;
            Vector3 lift = Vector3.up * .05f;
            float along = 0f, mark = .5f;
            for (int i = 0; i + 1 < count; i++)
            {
                Vector3 a = path[i] + lift, b = path[i + 1] + lift;
                Vector3 da = path[Mathf.Min(i + 1, count - 1)] - path[Mathf.Max(i - 1, 0)], db = path[Mathf.Min(i + 2, count - 1)] - path[i];
                da = da.sqrMagnitude > .0001f ? da.normalized : Vector3.forward;
                db = db.sqrMagnitude > .0001f ? db.normalized : da;
                Vector3 sa = Vector3.Cross(Vector3.up, da), sb = Vector3.Cross(Vector3.up, db);
                Quad(a - sa * reach, a + sa * reach, b + sb * reach, b - sb * reach, lane);
                Vector3 raise = Vector3.up * .003f;
                Quad(a - sa * .2f + raise, a + sa * .2f + raise, b + sb * .2f + raise, b - sb * .2f + raise, body);
                // Arrowheads at even steps along the way, pointing the way he goes.
                float span = (b - a).magnitude;
                while (span > .0001f && mark <= along + span)
                {
                    Vector3 direction = (b - a) / span, side = Vector3.Cross(Vector3.up, direction);
                    Vector3 centre = a + direction * (mark - along) + Vector3.up * .006f;
                    Quad(centre - side * .19f - direction * .17f, centre - side * .19f - direction * .06f, centre + direction * .09f, centre - direction * .02f, chevron);
                    Quad(centre + side * .19f - direction * .17f, centre - direction * .02f, centre + direction * .09f, centre + side * .19f - direction * .06f, chevron);
                    mark += .72f;
                }
                along += span;
            }
            // Past the turn the way home is anybody's guess: a swaying line of dashes that thins out and fades, and
            // a question mark where it gives up.
            int dashes = hint.Count;
            for (int i = 0; i + 1 < dashes; i++)
            {
                if (i % 3 == 2) continue;
                float fade = 1f - (float)i / (dashes - 1);
                Vector3 a = hint[i] + lift, b = hint[i + 1] + lift;
                Vector3 way = b - a;
                way = way.sqrMagnitude > .0001f ? way.normalized : Vector3.forward;
                Vector3 side = Vector3.Cross(Vector3.up, way), raise = Vector3.up * .003f;
                float wide = reach * Mathf.Lerp(.45f, .8f, fade);
                Quad(a - side * wide, a + side * wide, b + side * wide, b - side * wide, new Color32(main.r, main.g, main.b, (byte)Mathf.RoundToInt(14f + 30f * fade)));
                Quad(a - side * .13f + raise, a + side * .13f + raise, b + side * .13f + raise, b - side * .13f + raise,
                    new Color32(255, 255, 255, (byte)Mathf.RoundToInt(120f + 100f * fade)));
            }
            if (dashes > 1)
            {
                Vector3 last = hint[dashes - 1] + lift, before = hint[dashes - 2] + lift;
                Query(last + (last - before).normalized * .95f + Vector3.up * .006f, 1.6f, new Color32(58, 40, 52, 235), new Color32(255, 222, 80, 255));
            }

            var band = new Color32(255, 255, 255, 200);
            Vector3 from = new Vector3(home.x, .05f, home.z), grip = new Vector3(pulled.x, .05f, pulled.z);
            Vector3 pullWay = from - grip;
            pullWay = pullWay.sqrMagnitude > .0001f ? pullWay.normalized : Vector3.forward;
            Vector3 across = Vector3.Cross(Vector3.up, pullWay);
            for (int i = -1; i <= 1; i += 2)
            {
                Vector3 post = from + across * ((ColosseumDoll.Radius + .06f) * i);
                Vector3 thick = Vector3.Cross(Vector3.up, (grip - post).normalized) * .04f;
                Quad(post - thick, post + thick, grip + thick, grip - thick, band);
            }

            SubmitAimMesh();
        }

        void SubmitAimMesh()
        {
            aimMesh.Clear();
            aimMesh.SetVertices(aimVerts);
            aimMesh.SetColors(aimColors);
            aimMesh.SetTriangles(aimTris, 0);
            aimMesh.RecalculateBounds();
            if (!aimObject.activeSelf)
                aimObject.SetActive(true);
        }

        /// <summary>A question mark lying on the sand, its head towards the far end of the arena, in one colour on a darker backing.</summary>
        void Query(Vector3 centre, float size, Color32 backing, Color32 face)
        {
            for (int layer = 0; layer < 2; layer++)
            {
                Color32 color = layer == 0 ? backing : face;
                float bowl = .2f * size, thick = (layer == 0 ? .095f : .055f) * size;
                Vector3 middle = centre + Vector3.forward * (.2f * size) + Vector3.up * (layer * .003f);
                const int steps = 10;
                for (int i = 0; i < steps; i++)
                {
                    // From the upper left, over the top and round to the lower right.
                    float a0 = Mathf.Lerp(165f, -62f, (float)i / steps) * Mathf.Deg2Rad, a1 = Mathf.Lerp(165f, -62f, (float)(i + 1) / steps) * Mathf.Deg2Rad;
                    Vector3 d0 = new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)), d1 = new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1));
                    Quad(middle + d0 * (bowl - thick), middle + d0 * (bowl + thick), middle + d1 * (bowl + thick), middle + d1 * (bowl - thick), color);
                }
                float end = -62f * Mathf.Deg2Rad;
                Vector3 hook = middle + new Vector3(Mathf.Cos(end), 0f, Mathf.Sin(end)) * bowl;
                Vector3 stem = middle + Vector3.back * (.29f * size), across = Vector3.right * thick;
                Quad(hook - across, hook + across, stem + across, stem - across, color);
                Vector3 dot = middle + Vector3.back * (.45f * size);
                float half = thick * 1.2f;
                Quad(dot + new Vector3(-half, 0f, -half), dot + new Vector3(-half, 0f, half), dot + new Vector3(half, 0f, half), dot + new Vector3(half, 0f, -half), color);
            }
        }

        void Ring(Vector3 centre, float inner, float outer, Color32 color, int steps = 40)
        {
            for (int i = 0; i < steps; i++)
            {
                float a0 = (float)i / steps * Mathf.PI * 2f, a1 = (float)(i + 1) / steps * Mathf.PI * 2f;
                var d0 = new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0));
                var d1 = new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1));
                Quad(centre + d0 * inner, centre + d0 * outer, centre + d1 * outer, centre + d1 * inner, color);
            }
        }

        /// <summary>Smashed ground where a leaper landed: a dark pit with a pale thrown-up rim and cracks, fading out over a few seconds.</summary>
        public void Crater(Vector3 at, float radius)
        {
            int pick = craterCursor++ % craters.Length;
            craters[pick].position = new Vector3(at.x, .03f + pick * .002f, at.z);
            craters[pick].rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            craters[pick].localScale = Vector3.one * radius;
            craters[pick].gameObject.SetActive(true);
            craterAge[pick] = 0f;
        }

        void BuildCraters()
        {
            Mesh mesh = CraterMesh();
            for (int i = 0; i < craters.Length; i++)
            {
                craterTint[i] = Kit.StrokeMaterial(Color.white);
                craters[i] = Kit.SpawnStroke("Crater", root, mesh, craterTint[i]).transform;
                craters[i].gameObject.SetActive(false);
            }
        }

        /// <summary>The arena has been dressed for another map: what a landing breaks open is that map's ground now.</summary>
        public void Dress()
        {
            Mesh mesh = Kit.Own(CraterMesh());
            Object.Destroy(craters[0].GetComponent<MeshFilter>().sharedMesh);
            foreach (Transform crater in craters) crater.GetComponent<MeshFilter>().sharedMesh = mesh;
        }

        static Mesh CraterMesh()
        {
            ColosseumArena.Look skin = ColosseumArena.Skin;
            var kit = new MeshKit { Gradient = 0f };
            Color32 soot = skin.Soot;
            kit.Disc(Vector3.zero, 1.05f, 1.05f, soot, new Color32(soot.r, soot.g, soot.b, 0), 28);
            kit.Ring(new Vector3(0f, .002f, 0f), .5f, .68f, skin.CraterRim, 28);
            kit.Disc(new Vector3(0f, .004f, 0f), .56f, .56f, skin.CraterDeep, skin.CraterMid, 28);
            Color32 crack = skin.CraterCrack;
            for (int i = 0; i < 11; i++)
            {
                float a = (i * 32.7f + (i % 3) * 9f) * Mathf.Deg2Rad, reach = .95f + (i * 37 % 10) * .045f, kink = a + ((i & 1) == 0 ? .16f : -.16f);
                var d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                var k = new Vector3(Mathf.Cos(kink), 0f, Mathf.Sin(kink));
                Vector3 across = new Vector3(-d.z, 0f, d.x) * .045f, up = Vector3.up * .006f;
                Vector3 elbow = k * (reach * .62f) + up;
                kit.Quad(d * .5f - across + up, d * .5f + across + up, elbow + across * .6f, elbow - across * .6f, crack);
                kit.Quad(elbow - across * .6f, elbow + across * .6f, d * reach + up, d * reach + up, crack);
            }
            // Clods thrown up around the rim.
            Color32 clod = skin.Clod;
            for (int i = 0; i < 9; i++)
            {
                float a = (i * 40f + 13f) * Mathf.Deg2Rad, r = .78f + (i % 3) * .1f, size = .05f + (i % 4) * .016f;
                var c = new Vector3(Mathf.Cos(a) * r, .008f, Mathf.Sin(a) * r);
                kit.Quad(c + new Vector3(-size, 0f, -size * .6f), c + new Vector3(-size * .4f, 0f, size), c + new Vector3(size, 0f, size * .5f), c + new Vector3(size * .6f, 0f, -size), clod);
            }
            return kit.ToMesh("Colosseum crater");
        }

        void TickCraters(float delta)
        {
            for (int i = 0; i < craters.Length; i++)
            {
                if (!craters[i].gameObject.activeSelf) continue;
                craterAge[i] += delta;
                float fade = 1f - Mathf.Clamp01((craterAge[i] - 4.5f) / 2.5f);
                craterTint[i].color = new Color(1f, 1f, 1f, fade);
                if (fade <= 0f) craters[i].gameObject.SetActive(false);
            }
        }

        public void HideAim()
        {
            if (aimObject.activeSelf)
                aimObject.SetActive(false);
        }

        /// <summary>
        /// The plain sling guide: the bands and an arrow the way the hero will go. For the viking it can also mark
        /// the ground his whirlwind will reach: a broken ring round the place where he will pull up, with the wind
        /// drawn turning inside it.
        /// </summary>
        public void ShowAim(Vector3 home, Vector3 pulled, Vector3 direction, float power, int team, bool marked = false, Vector3 mark = default, float reach = 0f, float bowSpeed = 0f)
        {
            aimVerts.Clear(); aimColors.Clear(); aimTris.Clear();
            if (marked) Whirlpool(new Vector3(mark.x, .05f, mark.z), reach, team);
            Vector3 side = Vector3.Cross(Vector3.up, direction).normalized;
            float length = 1.8f + power * 5.4f;
            Vector3 start = new Vector3(home.x, .05f, home.z) + direction * (ColosseumDoll.Radius + .22f);
            Vector3 end = start + direction * length;
            var body = new Color32(255, 255, 255, 120);
            Quad(start - side * .27f, start + side * .27f, end + side * .27f, end - side * .27f, body);
            var chevron = new Color32(176, 132, 80, 230);
            for (float along = .55f; along < length - .25f; along += .62f)
            {
                Vector3 centre = start + direction * along + Vector3.up * .004f;
                Quad(centre - side * .19f - direction * .17f, centre - side * .19f - direction * .06f, centre + direction * .09f, centre - direction * .02f, chevron);
                Quad(centre + side * .19f - direction * .17f, centre - direction * .02f, centre + direction * .09f, centre + side * .19f - direction * .06f, chevron);
            }
            Color32 main = MeshKit.Rgb(Kit.TeamMain[team], 1f);
            var rim = new Color32(255, 255, 255, 255);
            Quad(end - side * .52f, end + side * .52f, end + direction * .62f, end + direction * .62f, rim);
            Vector3 inner = end + direction * .05f + Vector3.up * .004f;
            Quad(inner - side * .40f, inner + side * .40f, inner + direction * .48f, inner + direction * .48f, main);

            var band = new Color32(255, 255, 255, 200);
            Vector3 grip = new Vector3(pulled.x, .05f, pulled.z);
            for (int i = -1; i <= 1; i += 2)
            {
                Vector3 post = new Vector3(home.x, .05f, home.z) + side * ((ColosseumDoll.Radius + .06f) * i);
                Vector3 across = Vector3.Cross(Vector3.up, (grip - post).normalized) * .04f;
                Quad(post - across, post + across, grip + across, grip - across, band);
            }

            if (bowSpeed > 0f)
            {
                for (int sign = -1; sign <= 1; sign += 2)
                {
                    if (!SideArrowGuide(home, direction, bowSpeed, sign, out Vector3 tail, out Vector3 tip)) continue;
                    Vector3 heading = (tip - tail).normalized;
                    float wing = Mathf.Min(.25f, (tip - tail).magnitude * .36f);
                    DrawArrowGlyph(tail, tip, wing, new Color32(91, 51, 34, 240));
                    Vector3 lift = Vector3.up * .004f;
                    DrawArrowGlyph(tail + heading * .05f + lift, tip - heading * .09f + lift, wing * .70f, new Color32(255, 215, 77, 255));
                }
            }

            SubmitAimMesh();
        }

        // Show where the first pair leaves the moving bow, with the same sideways speed and forward drift as a fired arrow.
        internal static bool SideArrowGuide(Vector3 home, Vector3 direction, float launchSpeed, int sideSign, out Vector3 muzzle, out Vector3 tip)
        {
            Vector3 outward = Vector3.Cross(Vector3.up, direction) * sideSign;
            Vector3 travel = direction * launchSpeed;
            muzzle = Kit.Flat(home) + travel * ColosseumDoll.FirstSideVolleyDelay + outward * ColosseumDoll.SideArrowOffset + Vector3.up * .06f;
            tip = muzzle;
            const float edgeX = ColosseumGame.HalfWidth - .13f, edgeZ = ColosseumGame.HalfDepth - .13f;
            if (Mathf.Abs(muzzle.x) > edgeX || Mathf.Abs(muzzle.z) > edgeZ) return false;

            Vector3 heading = ColosseumDoll.SideArrowVelocity(travel, outward).normalized;
            float length = 1.45f;
            if (Mathf.Abs(heading.x) > .001f) length = Mathf.Min(length, (Mathf.Sign(heading.x) * edgeX - muzzle.x) / heading.x);
            if (Mathf.Abs(heading.z) > .001f) length = Mathf.Min(length, (Mathf.Sign(heading.z) * edgeZ - muzzle.z) / heading.z);
            if (length < .32f) return false;
            tip = muzzle + heading * length;
            return true;
        }

        void DrawArrowGlyph(Vector3 tail, Vector3 tip, float wingWidth, Color32 tint)
        {
            Vector3 heading = (tip - tail).normalized;
            Vector3 wing = Vector3.Cross(Vector3.up, heading) * wingWidth;
            Vector3 shaft = wing * .40f, neck = tip - heading * (1.6f * wingWidth);
            Quad(tail - shaft, tail + shaft, neck + shaft, neck - shaft, tint);
            Quad(neck - wing, neck + wing, tip, tip, tint);
        }

        /// <summary>The reach of the viking's whirlwind on the sling guide: a wash, a rim in dashes, and arms of wind turning inwards.</summary>
        void Whirlpool(Vector3 at, float reach, int team)
        {
            Color32 main = MeshKit.Rgb(Kit.TeamMain[team], 1f);
            var white = new Color32(255, 255, 255, 235);
            Ring(at, 0f, reach, new Color32(main.r, main.g, main.b, 46));
            const int dashes = 28, pieces = 3;
            for (int i = 0; i < dashes; i += 2)
            {
                for (int k = 0; k < pieces; k++)
                {
                    float a0 = (i + (float)k / pieces) * Mathf.PI * 2f / dashes, a1 = (i + (k + 1f) / pieces) * Mathf.PI * 2f / dashes;
                    Vector3 d0 = new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)), d1 = new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1));
                    Vector3 rim = at + Vector3.up * .004f, line = at + Vector3.up * .006f;
                    Quad(rim + d0 * (reach - .16f), rim + d0 * reach, rim + d1 * reach, rim + d1 * (reach - .16f), white);
                    Quad(line + d0 * (reach - .12f), line + d0 * (reach - .04f), line + d1 * (reach - .04f), line + d1 * (reach - .12f), main);
                }
            }
            // Three arms winding in to the middle, going round as the pull is held.
            float turned = Time.unscaledTime * 2.4f;
            const int steps = 12;
            for (int arm = 0; arm < 3; arm++)
            {
                for (int s = 0; s < steps; s++)
                {
                    float t0 = (float)s / steps, t1 = (s + 1f) / steps;
                    // Drawn the way he turns: round to the right as seen from above, and inwards as it goes.
                    float a0 = arm * Mathf.PI * 2f / 3f - turned - t0 * 2.6f, a1 = arm * Mathf.PI * 2f / 3f - turned - t1 * 2.6f;
                    float r0 = Mathf.Lerp(reach * .86f, reach * .2f, t0), r1 = Mathf.Lerp(reach * .86f, reach * .2f, t1);
                    float w0 = .03f + .11f * Mathf.Sin(t0 * Mathf.PI), w1 = .03f + .11f * Mathf.Sin(t1 * Mathf.PI);
                    Vector3 d0 = new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)), d1 = new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1));
                    Vector3 lift = at + Vector3.up * .008f;
                    Quad(lift + d0 * (r0 - w0), lift + d0 * (r0 + w0), lift + d1 * (r1 + w1), lift + d1 * (r1 - w1), new Color32(255, 255, 255, 150));
                }
            }
        }

        void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color32 color)
        {
            int start = aimVerts.Count;
            aimVerts.Add(a); aimVerts.Add(b); aimVerts.Add(c); aimVerts.Add(d);
            for (int i = 0; i < 4; i++) aimColors.Add(color);
            aimTris.Add(start); aimTris.Add(start + 1); aimTris.Add(start + 2);
            aimTris.Add(start); aimTris.Add(start + 2); aimTris.Add(start + 3);
        }
    }
}
