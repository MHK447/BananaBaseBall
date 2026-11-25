using System.Collections.Generic;
using UnityEngine;

namespace SlingArena.Colosseum
{
    /// <summary>
    /// Builds the colosseum: tiled sand court, stone walls with towers and braziers, stepped stands with an arcade,
    /// a cheering crowd, the emperor's box and the gates. Also animates everything that moves in the background.
    /// </summary>
    public sealed partial class ColosseumArena
    {
        const float EmperorScale = .784f;
        struct Fan
        {
            public Transform Item;
            public Vector3 Home;
            public Quaternion Facing;
            public float Phase, Rate, Size, Wave;
        }

        struct Sway
        {
            public Transform Item;
            public Quaternion Rest;
            public float Phase, Amount, Rate;
            public Vector3 Axis;
        }

        internal const float WallWide = .7f, WallHigh = .62f;
        // The stands begin this far outside the court: between them and the court lies the ground the walls stand on.
        internal const float TierStart = 1.3f;
        const float TierStep = 1.05f;
        const int Tiers = 5;

        /// <summary>A stretch of a wall that an earthquake can bring down.</summary>
        public struct WallCut
        {
            /// <summary>True for the south and north end walls, false for the west and east side walls.</summary>
            public bool End;
            /// <summary>-1 for the west (or south) wall, +1 for the east (or north) wall.</summary>
            public int Side;
            /// <summary>Where the stretch starts and ends along the wall.</summary>
            public float From, To;
            public float Middle { get { return (From + To) * .5f; } }
        }

        /// <summary>
        /// The two side walls, built afresh for the stretches an earthquake is going to bring down this set: the
        /// stretches that stay up in one mesh, and every stretch that can fall as an object of its own, with a
        /// collider of its own plugging the hole it leaves.
        /// </summary>
        sealed class SideWalls
        {
            public GameObject Root;
            public WallCut[] Cuts;
            public Transform[] Sections;
            public Vector3[] Homes;
            public BoxCollider[] Plugs;
            public readonly List<Mesh> Meshes = new List<Mesh>();
        }

        /// <summary>A pennant pole that stands on a side wall, and goes with the wall if the stretch under it falls.</summary>
        sealed class SideFlag
        {
            public Transform Pole;
            public Vector3 Home;
            public int Side;
        }

        SideWalls sideWalls;
        readonly List<SideFlag> sideFlags = new List<SideFlag>();
        readonly Transform wallRoot;
        readonly float wallHw, wallHd;
        readonly PhysicsMaterial wallSurface;

        readonly List<Fan> fans = new List<Fan>();
        readonly List<Sway> sways = new List<Sway>();
        readonly List<Transform> flames = new List<Transform>();
        const int KingIdle = 0, KingJump = 1, KingRage = 2, KingClap = 3, KingLaugh = 4, KingGasp = 5, KingMoods = 6;
        readonly float[] kingWeight = new float[KingMoods];
        readonly Transform[] kingBrows = new Transform[2], kingEyes = new Transform[2], kingPupils = new Transform[2], kingSteam = new Transform[2];
        Transform emperor, kingBody, kingHead, kingMouth, kingArmL, kingArmR, kingSceptre, kingSceptreRest;
        readonly Transform[] kingVeins = new Transform[2];
        Vector3 emperorHome, kingFocus;
        float kingMoodLeft, kingPower, kingYaw, kingPump, kingStroke;
        int kingMood, kingNext, kingLastKo;
        float clock;

        public ColosseumArena(Transform world, float halfWidth, float halfDepth, PhysicsMaterial floorSurface, PhysicsMaterial wallSurface,
            int stageNumber = 1)
        {
            Transform root = new GameObject("Colosseum").transform;
            root.SetParent(world, false);
            BuildColliders(root, halfWidth, halfDepth, floorSurface, wallSurface);
            wallRoot = root;
            wallHw = halfWidth;
            wallHd = halfDepth;
            this.wallSurface = wallSurface;
            // Everything that is only there to be seen is built in the look of the map (ColosseumArena.Looks.cs),
            // and built again whenever the arena is dressed for another one.
            look = Look.For(stageNumber);
            BuildScenery();
            RaiseWalls(new WallCut[0]);
            BuildCrowd(root, halfWidth, halfDepth);
            BuildEmperor(root, new Vector3(0f, 2.08f, halfDepth + 2.35f - .7f - .02f));
        }

        static Color32 C(int hex, float gloss = 0f) { return MeshKit.Rgb(hex, gloss); }

        const float SolidThick = 3f, SolidTall = 14f;

        static void BuildColliders(Transform root, float hw, float hd, PhysicsMaterial floorSurface, PhysicsMaterial wallSurface)
        {
            var solid = new GameObject("Arena colliders");
            solid.transform.SetParent(root, false);
            var floor = solid.AddComponent<BoxCollider>();
            floor.center = new Vector3(0f, -1f, 0f);
            floor.size = new Vector3(hw * 2f + 8f, 2f, hd * 2f + 8f);
            floor.sharedMaterial = floorSurface;
            // The four walls bring their own colliders (BuildSideWalls), because stretches of any of them can fall.
            var roof = solid.AddComponent<BoxCollider>();
            roof.center = new Vector3(0f, SolidTall, 0f);
            roof.size = new Vector3(hw * 2f + 8f, 2f, hd * 2f + 8f);
            roof.sharedMaterial = floorSurface;
        }

        // ------------------------------------------------------------------ court

        static void BuildCourt(Transform root, float hw, float hd)
        {
            Look l = look;
            var kit = new MeshKit { Gradient = 0f };
            kit.Floor(-70f, -70f, 70f, 70f, -.04f, l.Void);
            kit.Floor(-hw - 1.6f, -hd - 1.6f, hw + 1.6f, hd + 1.6f, -.02f, l.Apron);

            int columns = Mathf.RoundToInt(hw * 2f / 1.06f), rows = Mathf.RoundToInt(hd * 2f / 1.06f);
            float stepX = hw * 2f / columns, stepZ = hd * 2f / rows;
            Color32 grout = l.Grout, light = l.TileLight, dark = l.TileDark, worn = l.TileWorn;
            Color32 blueWash = l.WashBlue, redWash = l.WashRed;
            kit.Floor(-hw, -hd, hw, hd, 0f, grout);
            for (int x = 0; x < columns; x++)
            {
                for (int z = 0; z < rows; z++)
                {
                    float x0 = -hw + x * stepX, z0 = -hd + z * stepZ;
                    Color32 tone = (x + z) % 2 == 0 ? light : dark;
                    if (Random.value < .09f) tone = worn;
                    float mid = z0 + stepZ * .5f;
                    tone = MeshKit.Mix(tone, mid < 0f ? blueWash : redWash, l.Wash * Mathf.Clamp01(Mathf.Abs(mid) / hd));
                    float shade = Random.Range(l.ShadeLow, l.ShadeHigh);
                    tone = new Color32((byte)Mathf.Min(255f, tone.r * shade), (byte)Mathf.Min(255f, tone.g * shade), (byte)Mathf.Min(255f, tone.b * shade), 0);
                    kit.Floor(x0 + .035f, z0 + .035f, x0 + stepX - .035f, z0 + stepZ - .035f, .006f, tone);
                    if (l.Index == Look.Frost && Random.value < .2f) IceShine(kit, x0, z0, stepX, stepZ, .008f);
                }
            }

            const float lift = .014f;
            kit.Floor(-hw, -.07f, hw, .07f, lift, l.Line);
            PaintCourt(kit, hw, hd, lift);
            PaintSeal(kit, lift);

            for (int i = 0; i < l.Stains; i++)
            {
                var spot = new Vector3(Random.Range(-hw + .6f, hw - .6f), .009f, Random.Range(-hd + .6f, hd - .6f));
                if (spot.magnitude < (l.Index == Look.Sandpit ? 2.1f : 2.6f)) continue;
                float size = Random.Range(.16f, .42f);
                kit.Disc(spot, size, size * Random.Range(.5f, .9f), l.Stain, l.StainEdge, 14);
            }
            Kit.Spawn("Court", root, kit.ToMesh("Colosseum court"), false, false);

            var stones = new MeshKit();
            for (int i = 0; i < 16; i++)
            {
                bool alongX = Random.value < .45f;
                float edge = Random.Range(.25f, .8f);
                var spot = alongX
                    ? new Vector3(Random.Range(-hw + .3f, hw - .3f), 0f, (Random.value < .5f ? -1f : 1f) * (hd - edge))
                    : new Vector3((Random.value < .5f ? -1f : 1f) * (hw - edge), 0f, Random.Range(-hd + .3f, hd - .3f));
                float size = Random.Range(.045f, .095f);
                stones.Blob(spot + Vector3.up * size * .5f, new Vector3(size * 1.3f, size * .7f, size), Quaternion.Euler(0f, Random.value * 360f, 0f),
                    Random.value < .5f ? l.PebbleA : l.PebbleB, .8f, .8f, 6);
            }
            Kit.Spawn("Pebbles", root, stones.ToMesh("Colosseum pebbles"));

            // Soft darkening where the sand meets the walls, so the court sits inside the stone.
            var soft = new MeshKit { Gradient = 0f };
            Color32 deep = l.Shade;
            var none = new Color32(deep.r, deep.g, deep.b, 0);
            const float fade = 1.05f, y = .02f;
            for (int i = 0; i < 2; i++)
            {
                float s = i == 0 ? -1f : 1f;
                soft.Quad(new Vector3(-hw, y, s * hd), new Vector3(hw, y, s * hd), new Vector3(hw, y, s * (hd - fade)), new Vector3(-hw, y, s * (hd - fade)), deep, deep, none, none);
                soft.Quad(new Vector3(s * hw, y, -hd), new Vector3(s * hw, y, hd), new Vector3(s * (hw - fade), y, hd), new Vector3(s * (hw - fade), y, -hd), deep, deep, none, none);
            }
            // Drawn before every other mark on the sand (power gates, cracks, warnings), so none of them is dimmed by it.
            Kit.SpawnStroke("Court shading", root, soft.ToMesh("Colosseum court shading"), Kit.StrokeMaterial(Color.white))
                .GetComponent<MeshRenderer>().sortingOrder = -10;
        }

        // ------------------------------------------------------------------ walls

        void BuildWalls(Transform root, float hw, float hd)
        {
            Look l = look;
            var kit = new MeshKit();
            Color32 stone = l.Stone, cap = l.Cap;
            Color32 block = l.Block, gold = l.Trim;
            // The four walls themselves are built in BuildSideWalls, every set; here only the corner towers and posts.

            for (int i = 0; i < 4; i++)
            {
                var corner = new Vector3((i % 2 == 0 ? -1f : 1f) * (hw + WallWide * .5f), 0f, (i < 2 ? -1f : 1f) * (hd + WallWide * .5f));
                if (l.Index != Look.Sandpit)
                {
                    BuildTower(kit, root, corner, i >= 2);
                    continue;
                }
                kit.Blob(corner + Vector3.up * .62f, new Vector3(.62f, .64f, .62f), Quaternion.identity, stone, .35f, .8f, 14);
                kit.Blob(corner + Vector3.up * 1.26f, new Vector3(.7f, .1f, .7f), Quaternion.identity, cap, .5f, .9f, 14);
                for (int m = 0; m < 8; m++)
                {
                    float a = m * Mathf.PI / 4f;
                    kit.Blob(corner + new Vector3(Mathf.Cos(a) * .56f, 1.44f, Mathf.Sin(a) * .56f), new Vector3(.13f, .12f, .13f), Quaternion.identity, block, .5f, .5f, 6);
                }
                kit.Blob(corner + Vector3.up * 1.46f, new Vector3(.34f, .14f, .34f), Quaternion.identity, l.Bowl, .8f, 1f, 10);
                AddFlame(root, corner + Vector3.up * 1.62f, 1f);
            }
            for (int i = 0; i < 2; i++)
            {
                float sign = i == 0 ? -1f : 1f;
                var post = new Vector3(sign * (hw + WallWide * .5f), 0f, 0f);
                if (l.Index != Look.Sandpit)
                {
                    BuildPost(kit, root, post);
                    continue;
                }
                kit.Blob(post + Vector3.up * .5f, new Vector3(.46f, .52f, .46f), Quaternion.identity, stone, .35f, .5f, 10);
                kit.Blob(post + Vector3.up * 1.05f, new Vector3(.5f, .07f, .5f), Quaternion.identity, cap, .5f, .5f, 10);
                kit.Ball(post + Vector3.up * 1.26f, .2f, gold, 10);
            }
            Kit.Spawn("Walls", root, kit.ToMesh("Colosseum walls"));
        }

        /// <summary>
        /// One stretch of a wall, from one point along it to another: the stone, the plinth outside it, the pale
        /// cap along its inside edge, and the battlements and team shields that fall within it. The pads lengthen
        /// the stone at either end, where a wall runs on into a corner. Walls 0 and 1 are the south and north ends,
        /// 2 and 3 the west and east sides.
        /// </summary>
        static void WallStretch(MeshKit kit, int wall, float hw, float hd, float from, float to, float padFrom, float padTo, Vector3 origin)
        {
            Look l = look;
            Color32 stone = l.Stone, stoneTop = l.StoneTop, plinth = l.Plinth, cap = l.Cap;
            bool alongX = wall < 2;
            float sign = wall % 2 == 0 ? -1f : 1f;
            float span = alongX ? hw : hd;
            Vector3 Along(float t, float height, float outward)
            {
                return (alongX ? new Vector3(t, height, sign * (hd + outward)) : new Vector3(sign * (hw + outward), height, t)) - origin;
            }
            Vector3 Size(float length, float height, float wide)
            {
                return alongX ? new Vector3(length, height, wide) : new Vector3(wide, height, length);
            }
            kit.Box(Along((from - padFrom + to + padTo) * .5f, WallHigh * .5f, WallWide * .5f), Size(to - from + padFrom + padTo, WallHigh, WallWide),
                Quaternion.identity, stone, stoneTop);
            float skirtFrom = padFrom > 0f ? padFrom + .28f : 0f, skirtTo = padTo > 0f ? padTo + .28f : 0f;
            kit.Box(Along((from - skirtFrom + to + skirtTo) * .5f, .19f, WallWide + .14f), Size(to - from + skirtFrom + skirtTo, .38f, .28f),
                Quaternion.identity, plinth, l.PlinthTop);
            kit.Box(Along((from + to) * .5f, WallHigh + .035f, .09f), Size(to - from, .07f, .18f), Quaternion.identity, cap, cap);

            int count = Mathf.FloorToInt(span * 2f / 1.06f);
            for (int s = 0; s <= count; s++)
            {
                float t = -span + .32f + s * ((span * 2f - .64f) / count);
                if (t >= from && t < to)
                    Merlon(kit, Along(t, WallHigh + .12f, WallWide * .56f), Size(.2f, .13f, .2f), s);
                if (s % 3 != 1) continue;
                float hang = t + (alongX ? 0f : .53f);
                if (hang < from - padFrom || hang >= to + padTo) continue;
                // A shield that would hang across the end of a stretch is moved in from the end.
                if (padFrom <= 0f) hang = Mathf.Max(hang, from + .32f);
                if (padTo <= 0f) hang = Mathf.Min(hang, to - .32f);
                int team = (alongX ? sign : t) >= 0f ? 1 : 0;
                Vector3 lean = alongX ? new Vector3(-sign * 28f, 0f, 0f) : new Vector3(0f, 0f, sign * 28f);
                Shield(kit, Along(hang, WallHigh + .2f, .2f), lean, team);
            }
        }

        /// <summary>
        /// The side walls, for the stretches that are going to fall this set (in order along each wall): what stays
        /// up as one mesh, and each stretch that falls as an object of its own with its own collider.
        /// </summary>
        SideWalls BuildSideWalls(WallCut[] cuts)
        {
            float hw = wallHw, hd = wallHd;
            var walls = new SideWalls { Root = new GameObject("Side walls"), Cuts = cuts };
            walls.Root.transform.SetParent(wallRoot, false);
            walls.Sections = new Transform[cuts.Length];
            walls.Homes = new Vector3[cuts.Length];
            walls.Plugs = new BoxCollider[cuts.Length];
            var solid = new GameObject("Side wall colliders");
            solid.transform.SetParent(walls.Root.transform, false);
            var whole = new MeshKit();
            // Walls 0 and 1 are the south and north ends, 2 and 3 the west and east sides.
            for (int wall = 0; wall < 4; wall++)
            {
                bool end = wall < 2;
                int side = wall % 2 == 0 ? -1 : 1;
                float span = end ? hw : hd;
                float at = -span;
                for (int c = 0; c < cuts.Length; c++)
                {
                    WallCut cut = cuts[c];
                    if (cut.End != end || cut.Side != side) continue;
                    bool first = at <= -span;
                    WallStretch(whole, wall, hw, hd, at, cut.From, first ? WallWide : 0f, 0f, Vector3.zero);
                    Solid(solid, end, side, first ? at - SolidThick : at, cut.From, wallSurface);
                    var home = end ? new Vector3(cut.Middle, 0f, side * (hd + WallWide * .5f)) : new Vector3(side * (hw + WallWide * .5f), 0f, cut.Middle);
                    var loose = new MeshKit();
                    WallStretch(loose, wall, hw, hd, cut.From, cut.To, 0f, 0f, home);
                    Mesh section = loose.ToMesh("Colosseum wall section");
                    walls.Meshes.Add(section);
                    walls.Sections[c] = Kit.Spawn("Wall section", walls.Root.transform, section).transform;
                    walls.Sections[c].position = home;
                    walls.Homes[c] = home;
                    walls.Plugs[c] = Solid(solid, end, side, cut.From, cut.To, wallSurface);
                    at = cut.To;
                }
                // A wall nothing falls from runs from corner to corner in one.
                bool unbroken = at <= -span;
                WallStretch(whole, wall, hw, hd, at, span, unbroken ? WallWide : 0f, WallWide, Vector3.zero);
                Solid(solid, end, side, unbroken ? at - SolidThick : at, span + SolidThick, wallSurface);
            }
            Mesh standing = whole.ToMesh("Colosseum side walls");
            walls.Meshes.Add(standing);
            Kit.Spawn("Side walls", walls.Root.transform, standing);
            return walls;
        }

        BoxCollider Solid(GameObject host, bool end, int side, float from, float to, PhysicsMaterial surface)
        {
            var box = host.AddComponent<BoxCollider>();
            box.center = end
                ? new Vector3((from + to) * .5f, SolidTall * .5f - 1f, side * (wallHd + SolidThick * .5f))
                : new Vector3(side * (wallHw + SolidThick * .5f), SolidTall * .5f - 1f, (from + to) * .5f);
            box.size = end ? new Vector3(to - from, SolidTall, SolidThick) : new Vector3(SolidThick, SolidTall, to - from);
            box.sharedMaterial = surface;
            return box;
        }

        /// <summary>How many places there are along an end wall where a stretch can break off, numbered from the west.</summary>
        public int EndBreaks { get { return Mathf.FloorToInt(wallHw * 2f / 1.06f); } }

        /// <summary>Where along an end wall (its x) the given break is: half-way between one battlement and the next.</summary>
        public float EndBreak(int index)
        {
            int count = EndBreaks;
            return -wallHw + .32f + (index + .5f) * ((wallHw * 2f - .64f) / count);
        }

        /// <summary>
        /// How many places there are along a side wall where a stretch can break off: one between every battlement
        /// and the next, numbered from the south end. The middle one is where the post stands on the half-way line.
        /// </summary>
        public int WallBreaks { get { return Mathf.FloorToInt(wallHd * 2f / 1.06f); } }

        /// <summary>Where along a side wall the given break is: half-way between one battlement and the next.</summary>
        public float WallBreak(int index)
        {
            int count = WallBreaks;
            return -wallHd + .32f + (index + .5f) * ((wallHd * 2f - .64f) / count);
        }

        /// <summary>The stretches of side wall that can fall, the way the walls stand now, in order along each wall.</summary>
        public IList<WallCut> WallCuts { get { return sideWalls.Cuts; } }

        /// <summary>
        /// Both side walls built whole again, ready to break up at the given stretches. Stretches on one wall must
        /// not touch or overlap.
        /// </summary>
        public void RaiseWalls(IList<WallCut> cuts)
        {
            if (sideWalls != null)
            {
                sideWalls.Root.SetActive(false);
                foreach (Mesh mesh in sideWalls.Meshes) Object.Destroy(mesh);
                Object.Destroy(sideWalls.Root);
            }
            var order = new List<WallCut>(cuts);
            order.Sort((a, b) => a.End != b.End ? a.End.CompareTo(b.End) : a.Side != b.Side ? a.Side.CompareTo(b.Side) : a.From.CompareTo(b.From));
            sideWalls = BuildSideWalls(order.ToArray());
            foreach (SideFlag flag in sideFlags)
            {
                flag.Pole.gameObject.SetActive(true);
                flag.Pole.SetPositionAndRotation(flag.Home, Quaternion.identity);
            }
        }

        /// <summary>True for a pole that stands on the given stretch.</summary>
        static bool StandsOn(SideFlag flag, WallCut cut)
        {
            return !cut.End && flag.Side == cut.Side && flag.Home.z > cut.From && flag.Home.z < cut.To;
        }

        /// <summary>A stretch of wall trembles before it goes: moved and tipped a little off where it stands.</summary>
        public void ShakeWall(int cut, Vector3 offset, Quaternion tilt)
        {
            SideWalls walls = sideWalls;
            walls.Sections[cut].SetPositionAndRotation(walls.Homes[cut] + offset, tilt);
            foreach (SideFlag flag in sideFlags)
            {
                if (StandsOn(flag, walls.Cuts[cut])) flag.Pole.SetPositionAndRotation(flag.Home + offset, tilt);
            }
        }

        /// <summary>A stretch of wall is gone: nothing stands there any more and the way out is open.</summary>
        public void DropWall(int cut)
        {
            SideWalls walls = sideWalls;
            walls.Sections[cut].gameObject.SetActive(false);
            walls.Plugs[cut].enabled = false;
            foreach (SideFlag flag in sideFlags)
            {
                if (StandsOn(flag, walls.Cuts[cut])) flag.Pole.gameObject.SetActive(false);
            }
        }

        /// <summary>The ground shakes: the emperor is shocked for as long as it lasts.</summary>
        public void ReactToQuake(float seconds)
        {
            SetKingMood(KingGasp, seconds, 1f);
            kingNext = KingIdle;
        }

        /// <summary>A fire that flickers, or (kind 1) the steadier glow of a lamp.</summary>
        void AddFlame(Transform root, Vector3 at, float size, int kind = 0)
        {
            var kit = new MeshKit { Gradient = 0f };
            if (kind == 1)
            {
                kit.Ball(new Vector3(0f, .1f, 0f), .2f, C(0xffcf45, 1f), 8);
                kit.Ball(new Vector3(0f, .1f, .03f), .13f, C(0xfff6c0, 1f), 6);
            }
            else
            {
                kit.Blob(new Vector3(0f, .16f, 0f), new Vector3(.2f, .3f, .2f), Quaternion.identity, C(0xff7a1a, 1f), 1.3f, 1f, 8);
                kit.Blob(new Vector3(0f, .14f, .02f), new Vector3(.13f, .22f, .13f), Quaternion.identity, C(0xffc21f, 1f), 1.3f, 1f, 8);
                kit.Blob(new Vector3(0f, .1f, .04f), new Vector3(.07f, .12f, .07f), Quaternion.identity, C(0xfff3b0, 1f), 1.2f, 1f, 6);
            }
            Transform flame = Kit.Spawn("Flame", root, kit.ToMesh("Colosseum flame"), false, false).transform;
            flame.position = at;
            flame.localScale = Vector3.one * size;
            flames.Add(flame);
        }

        // ------------------------------------------------------------------ stands

        static Color32 StandColor(float z, int part)
        {
            return (z >= 0f ? look.StandRed : look.StandBlue)[part];
        }

        static void BuildStands(Transform root, float hw, float hd)
        {
            var kit = new MeshKit { Gradient = .2f };
            for (int tier = 0; tier < Tiers; tier++)
            {
                float inner = TierStart + tier * TierStep, height = .6f + tier * .52f;
                for (int half = 0; half < 2; half++)
                {
                    float z = half == 0 ? -1f : 1f;
                    float wide = (hw + inner + TierStep) * 2f;
                    kit.Box(new Vector3(0f, height * .5f, z * (hd + inner + TierStep * .5f)), new Vector3(wide, height, TierStep), Quaternion.identity,
                        StandColor(z, 1), StandColor(z, 0));
                    kit.Box(new Vector3(0f, height + .02f, z * (hd + inner + .09f)), new Vector3(wide, .05f, .18f), Quaternion.identity,
                        StandColor(z, 2), StandColor(z, 2));
                    for (int side = 0; side < 2; side++)
                    {
                        float x = side == 0 ? -1f : 1f;
                        float length = hd + inner;
                        kit.Box(new Vector3(x * (hw + inner + TierStep * .5f), height * .5f, z * length * .5f), new Vector3(TierStep, height, length),
                            Quaternion.identity, StandColor(z, 1), StandColor(z, 0));
                        kit.Box(new Vector3(x * (hw + inner + .09f), height + .02f, z * length * .5f), new Vector3(.18f, .05f, length),
                            Quaternion.identity, StandColor(z, 2), StandColor(z, 2));
                    }
                }
            }

            // Stair aisles cut through the seating.
            Look l = look;
            Color32 stair = l.Stair, stairEdge = l.StairEdge;
            float[] aisles = { -hw - .2f, -hw * .42f, hw * .42f, hw + .2f };
            for (int tier = 0; tier < Tiers; tier++)
            {
                float inner = TierStart + tier * TierStep, height = .6f + tier * .52f;
                foreach (float aisle in aisles)
                {
                    for (int half = 0; half < 2; half++)
                    {
                        float z = (half == 0 ? -1f : 1f) * (hd + inner + TierStep * .5f);
                        kit.Box(new Vector3(aisle, height + .03f, z), new Vector3(.62f, .06f, TierStep * .98f), Quaternion.identity, stairEdge, stair);
                    }
                }
            }

            // Arcade behind the far stands: the classic ring of arches.
            float back = hd + TierStart + Tiers * TierStep, top = 5.2f, wideAll = hw + TierStart + Tiers * TierStep + 1f;
            // The front stands meet a paved concourse, visible below the crowd on tall phones.
            const float concourseDepth = 5.25f;
            float concourseY = .6f + (Tiers - 1) * .52f;
            float concourseZ = -back - concourseDepth * .5f;
            kit.Box(new Vector3(0f, concourseY * .5f, concourseZ), new Vector3(wideAll * 2f, concourseY, concourseDepth),
                Quaternion.identity, StandColor(-1f, 1), StandColor(-1f, 0));
            foreach (float aisle in aisles)
                kit.Box(new Vector3(aisle, concourseY + .03f, concourseZ), new Vector3(.62f, .06f, concourseDepth),
                    Quaternion.identity, stairEdge, stair);
            for (int row = 0; row < 5; row++)
                kit.Box(new Vector3(0f, concourseY + .012f, -back - row * TierStep), new Vector3(wideAll * 2f, .024f, .045f),
                    Quaternion.identity, StandColor(-1f, 1), StandColor(-1f, 1));

            Color32 wall = l.Arcade, wallTop = l.ArcadeTop, arch = l.Arch, column = l.Column;
            kit.Box(new Vector3(0f, top * .5f, back + .7f), new Vector3(wideAll * 2f, top, 1.4f), Quaternion.identity, wall, wallTop);
            kit.Box(new Vector3(0f, top + .14f, back + .6f), new Vector3(wideAll * 2f + .3f, .28f, 1.7f), Quaternion.identity, l.Cornice, l.CorniceTop);
            for (float x = -wideAll + 1.1f; x <= wideAll - 1.1f; x += 2.15f)
            {
                kit.Blob(new Vector3(x, 3.45f, back - .02f), new Vector3(.62f, 1.25f, .1f), Quaternion.identity, arch, 1f, 1f, 12);
                kit.Box(new Vector3(x, 2.5f, back - .02f), new Vector3(1.24f, 1.4f, .1f), Quaternion.identity, arch, arch);
                kit.Capsule(new Vector3(x + 1.075f, 2.3f, back - .12f), new Vector3(x + 1.075f, top - .25f, back - .12f), .17f, .15f, column, 8);
                kit.Blob(new Vector3(x + 1.075f, top - .16f, back - .12f), new Vector3(.27f, .12f, .27f), Quaternion.identity, l.Capital, .5f, .5f, 8);
            }
            Kit.Spawn("Stands", root, kit.ToMesh("Colosseum stands"));
        }

        // ------------------------------------------------------------------ crowd

        // The lobby spectator atlas is baked from these same match meshes.
        public static Mesh FanMesh(int variant, int team)
        {
            int[][] shirts =
            {
                new[] { 0x3f66b8, 0x5b80cc, 0x2c4c94, 0x8aa6dd, 0xf2ead8, 0xc9b28e, 0x4a78d0, 0xe9e2cf, 0x7a5a44, 0x6f93e0 },
                new[] { 0xb0463c, 0xcf5a4a, 0x8e3030, 0xe08a70, 0xf2ead8, 0xc9b28e, 0xd4503e, 0xe9e2cf, 0x7a5a44, 0xe86a52 }
            };
            int[] skins = { 0xf0c3a0, 0xd9a27a, 0xb97d55, 0xf6d2b4, 0x8f5c3c };
            int[] hairs = { 0x3f2f2a, 0x6b4a2e, 0x1f1a1c, 0xb98a4a, 0xd8d2c6, 0x8a3b22 };
            var kit = new MeshKit { Gradient = .22f };
            Color32 haze = C(team == 0 ? 0x333f6b : 0x5a3238);
            Color32 cloth = MeshKit.Mix(C(shirts[team][variant % 10], .05f), haze, .26f), skin = MeshKit.Mix(C(skins[variant % skins.Length], .05f), haze, .26f);
            Color32 hair = MeshKit.Mix(C(hairs[(variant * 3 + team) % hairs.Length], .1f), haze, .26f), trim = MeshKit.Mix(C(Kit.TeamLight[team], .1f), haze, .2f);
            float wide = variant % 3 == 0 ? .25f : .21f;
            kit.Blob(new Vector3(0f, .25f, 0f), new Vector3(wide, .29f, .18f), Quaternion.identity, cloth, .85f, .9f, 8);
            if (variant % 4 == 1)
                kit.Blob(new Vector3(0f, .46f, 0f), new Vector3(wide * .85f, .06f, .16f), Quaternion.identity, trim, 1f, 1f, 8);
            kit.Ball(new Vector3(0f, .66f, 0f), .18f, skin, 10);
            switch (variant % 6)
            {
                case 0: kit.Blob(new Vector3(0f, .73f, -.03f), new Vector3(.19f, .13f, .18f), Quaternion.identity, hair, 1f, 1f, 8); break;
                case 1:
                    kit.Blob(new Vector3(0f, .72f, -.04f), new Vector3(.2f, .14f, .19f), Quaternion.identity, hair, 1f, 1f, 8);
                    kit.Ball(new Vector3(0f, .86f, -.08f), .08f, hair, 6);
                    break;
                case 2: kit.Blob(new Vector3(0f, .70f, -.07f), new Vector3(.21f, .19f, .19f), Quaternion.identity, hair, 1f, 1f, 8); break;
                case 3:
                    kit.Blob(new Vector3(0f, .76f, -.02f), new Vector3(.2f, .12f, .19f), Quaternion.identity, C(Kit.Steel, .8f), 1f, 1f, 8);
                    kit.Blob(new Vector3(0f, .9f, -.02f), new Vector3(.04f, .08f, .15f), Quaternion.identity, trim, 1f, 1f, 6);
                    break;
                case 4:
                    for (int i = 0; i < 7; i++)
                    {
                        float a = Mathf.Lerp(-30f, 210f, i / 6f) * Mathf.Deg2Rad;
                        kit.Ball(new Vector3(Mathf.Cos(a) * .17f, .76f, -Mathf.Sin(a) * .17f), .05f, C(0x6f9a3a, .2f), 5);
                    }
                    break;
                default: kit.Blob(new Vector3(0f, .74f, -.02f), new Vector3(.23f, .06f, .23f), Quaternion.identity, C(0xd8b46a, .1f), 1f, 1f, 8); break;
            }
            Color32 ink = C(Kit.Ink);
            kit.Ball(new Vector3(-.07f, .68f, .16f), .028f, ink, 5);
            kit.Ball(new Vector3(.07f, .68f, .16f), .028f, ink, 5);
            kit.Blob(new Vector3(0f, .59f, .165f), new Vector3(.05f, .04f, .02f), Quaternion.identity, C(0x5a1a22), 1f, 1f, 6);

            bool oneArm = variant % 5 == 2;
            Vector3 leftHand = oneArm ? new Vector3(-.26f, .2f, .1f) : new Vector3(-.36f, .8f, .05f);
            Vector3 rightHand = variant % 5 == 4 ? new Vector3(.2f, .62f, .26f) : new Vector3(.36f, .8f, .05f);
            kit.Capsule(new Vector3(-.2f, .4f, 0f), leftHand, .06f, .052f, cloth, 6);
            kit.Capsule(new Vector3(.2f, .4f, 0f), rightHand, .06f, .052f, cloth, 6);
            kit.Ball(leftHand, .068f, skin, 6);
            kit.Ball(rightHand, .068f, skin, 6);
            if (variant % 5 == 0)
            {
                Color32 flag = C(Kit.TeamMain[team], .1f);
                Vector3 grip = rightHand, tip = rightHand + new Vector3(.06f, .5f, 0f);
                kit.Capsule(grip, tip, .018f, .018f, C(0x6f4529), 5);
                kit.Tri(tip, tip + new Vector3(.34f, -.1f, 0f), tip + new Vector3(0f, -.22f, 0f), flag);
                kit.Tri(tip, tip + new Vector3(0f, -.22f, 0f), tip + new Vector3(.34f, -.1f, 0f), flag);
            }
            return kit.ToMesh("Colosseum fan");
        }

        void BuildCrowd(Transform root, float hw, float hd)
        {
            var meshes = new Mesh[2, 15];
            for (int team = 0; team < 2; team++)
                for (int i = 0; i < 15; i++)
                    meshes[team, i] = FanMesh(i, team);

            Transform crowd = new GameObject("Crowd").transform;
            crowd.SetParent(root, false);
            int count = 0;
            for (int tier = 0; tier < Tiers; tier++)
            {
                float inner = TierStart + tier * TierStep + .55f, height = .6f + tier * .52f;
                float reach = hw + inner;
                for (float x = -reach; x <= reach; x += .62f)
                {
                    bool aisle = Mathf.Abs(Mathf.Abs(x) - (hw + .2f)) < .42f || Mathf.Abs(Mathf.Abs(x) - hw * .42f) < .42f;
                    if (aisle) continue;
                    for (int half = 0; half < 2; half++)
                    {
                        float z = (half == 0 ? -1f : 1f) * (hd + inner);
                        if (Mathf.Abs(x) < 2.6f && tier < 3 && half == 1) continue;
                        if (Mathf.Abs(x) < 2.1f && tier < 1 && half == 0) continue;
                        AddFan(crowd, meshes, new Vector3(x, height, z), ref count);
                    }
                }
                if (tier > 3) continue;
                for (float z = -hd - inner + .62f; z <= hd + inner - .62f; z += .62f)
                {
                    if (Mathf.Abs(z) < .45f) continue;
                    AddFan(crowd, meshes, new Vector3(-reach, height, z), ref count);
                    AddFan(crowd, meshes, new Vector3(reach, height, z), ref count);
                }
            }
        }

        void AddFan(Transform crowd, Mesh[,] meshes, Vector3 home, ref int count)
        {
            int team = home.z >= 0f ? 1 : 0;
            home += new Vector3(Random.Range(-.1f, .1f), 0f, Random.Range(-.1f, .1f));
            Transform item = Kit.Spawn("Fan", crowd, meshes[team, Random.Range(0, 15)], false, false).transform;
            Vector3 toCourt = Kit.Flat(-home);
            Quaternion facing = Quaternion.LookRotation(toCourt.sqrMagnitude > .01f ? toCourt.normalized : Vector3.forward)
                * Quaternion.Euler(0f, Random.Range(-18f, 18f), 0f);
            float size = Random.Range(.98f, 1.24f);
            item.SetPositionAndRotation(home, facing);
            item.localScale = Vector3.one * size;
            fans.Add(new Fan
            {
                Item = item, Home = home, Facing = facing, Phase = Random.value * 6.28f, Rate = Random.Range(5f, 9f), Size = size,
                Wave = Mathf.Atan2(home.z, home.x)
            });
            count++;
        }

        // ------------------------------------------------------------------ emperor box, gates, banners

        void BuildEmperorBox(Transform root, float hd)
        {
            Look l = look;
            var kit = new MeshKit();
            Color32 stone = l.BoxStone, stoneTop = l.BoxTop, purple = l.Drape, purpleDark = l.DrapeDeep;
            Color32 gold = C(Kit.Gold, .9f), column = C(0xfff3d6, .2f);
            float z = hd + 2.35f;
            kit.Box(new Vector3(0f, .85f, z), new Vector3(4.6f, 1.7f, 2.4f), Quaternion.identity, stone, stoneTop);
            kit.Box(new Vector3(0f, 1.74f, z - 1.12f), new Vector3(4.7f, .12f, .22f), Quaternion.identity, gold, gold);
            kit.Box(new Vector3(0f, 1.1f, z - 1.22f), new Vector3(1.9f, 1.15f, .06f), Quaternion.identity, purpleDark, purple);
            kit.Blob(new Vector3(0f, 1.15f, z - 1.27f), new Vector3(.36f, .36f, .04f), Quaternion.identity, gold, 1f, 1f, 14);
            kit.Blob(new Vector3(0f, 1.15f, z - 1.3f), new Vector3(.2f, .2f, .04f), Quaternion.identity, purple, 1f, 1f, 12);
            for (int i = 0; i < 4; i++)
            {
                var post = new Vector3((i % 2 == 0 ? -1f : 1f) * 2.05f, 0f, z + (i < 2 ? -.35f : .95f));
                kit.Capsule(post + Vector3.up * 1.7f, post + Vector3.up * 3.5f, .12f, .11f, column, 10);
                kit.Blob(post + Vector3.up * 3.55f, new Vector3(.2f, .09f, .2f), Quaternion.identity, gold, .5f, .5f, 8);
                kit.Blob(post + Vector3.up * 1.76f, new Vector3(.19f, .07f, .19f), Quaternion.identity, gold, .5f, .5f, 8);
            }
            for (int i = 0; i < 9; i++)
            {
                float x = -2.2f + i * .55f;
                kit.Box(new Vector3(x, 3.72f, z + .3f), new Vector3(.56f, .2f, 1.75f), Quaternion.Euler(-9f, 0f, 0f), i % 2 == 0 ? l.StripeA : l.StripeB, i % 2 == 0 ? l.StripeATop : l.StripeBTop);
                kit.Ball(new Vector3(x, 3.44f, z - .58f), .09f, i % 2 == 0 ? gold : l.StripeATop, 6);
            }
            // Eagle standard beside the throne.
            kit.Capsule(new Vector3(1.8f, 1.7f, z - .7f), new Vector3(1.8f, 3.0f, z - .7f), .035f, .035f, C(0x6f4529), 6);
            kit.Blob(new Vector3(1.8f, 3.12f, z - .7f), new Vector3(.3f, .1f, .05f), Quaternion.identity, gold, 1f, 1f, 8);
            kit.Ball(new Vector3(1.8f, 3.22f, z - .68f), .1f, gold, 8);
            kit.Box(new Vector3(1.8f, 2.78f, z - .7f), new Vector3(.36f, .3f, .04f), Quaternion.identity, C(Kit.TeamMain[1], .2f), C(Kit.TeamMain[1], .2f));
            // Two guards.
            for (int i = 0; i < 2; i++)
            {
                var foot = new Vector3(i == 0 ? -1.5f : 1.2f, 1.7f, z - .55f);
                kit.Blob(foot + Vector3.up * .34f, new Vector3(.24f, .34f, .2f), Quaternion.identity, C(0x7a2a2a, .1f), .85f, .9f, 8);
                kit.Blob(foot + Vector3.up * .4f, new Vector3(.25f, .17f, .21f), Quaternion.identity, C(Kit.Steel, .8f), .85f, .9f, 8);
                kit.Ball(foot + Vector3.up * .8f, .2f, C(0xe6b08a, .1f), 10);
                kit.Blob(foot + new Vector3(0f, .88f, -.03f), new Vector3(.215f, .15f, .2f), Quaternion.identity, C(Kit.Steel, .9f), 1f, 1f, 8);
                kit.Blob(foot + new Vector3(0f, 1.04f, -.03f), new Vector3(.04f, .09f, .17f), Quaternion.identity, C(Kit.TeamMain[1], .2f), 1f, 1f, 6);
                kit.Capsule(foot + new Vector3(.3f, 0f, .05f), foot + new Vector3(.3f, 1.5f, .05f), .03f, .03f, C(0x6f4529), 6);
                kit.Capsule(foot + new Vector3(.3f, 1.5f, .05f), foot + new Vector3(.3f, 1.78f, .05f), .06f, .005f, C(Kit.Steel, 1f), 6);
            }
            // Throne: tall gilded back with a purple cushion, lion-headed armrests and a stepped plinth.
            float seat = z - .7f;
            Color32 goldDeep = C(Kit.GoldDark, .8f), velvet = C(0x7a3ccf, .25f), velvetDeep = C(0x57259c, .25f), crimson = C(0xb3262e, .2f);
            kit.Box(new Vector3(0f, 1.76f, seat + .1f), new Vector3(2.2f, .12f, 1.5f), Quaternion.identity, crimson, C(0xd2393f, .2f));
            kit.Box(new Vector3(0f, 1.92f, seat + .16f), new Vector3(1.55f, .3f, 1.05f), Quaternion.identity, goldDeep, gold);
            kit.Box(new Vector3(0f, 2.1f, seat + .1f), new Vector3(1.2f, .1f, .8f), Quaternion.identity, velvetDeep, velvet);
            kit.Box(new Vector3(0f, 2.75f, seat + .62f), new Vector3(1.6f, 1.75f, .2f), Quaternion.identity, goldDeep, gold);
            kit.Box(new Vector3(0f, 2.72f, seat + .5f), new Vector3(1.2f, 1.4f, .06f), Quaternion.identity, velvetDeep, velvet);
            for (int i = -1; i <= 1; i += 2)
            {
                kit.Capsule(new Vector3(i * .8f, 1.9f, seat + .62f), new Vector3(i * .8f, 3.6f, seat + .62f), .1f, .08f, gold, 8);
                kit.Box(new Vector3(i * .72f, 2.28f, seat + .12f), new Vector3(.2f, .42f, .95f), Quaternion.identity, goldDeep, gold);
                kit.Ball(new Vector3(i * .72f, 2.52f, seat - .36f), .16f, gold, 10);
                kit.Ball(new Vector3(i * .72f, 2.5f, seat - .5f), .07f, goldDeep, 6);
                // Drapes gathered against the front columns.
                kit.Blob(new Vector3(i * 2.05f, 2.75f, z - .32f), new Vector3(.2f, .7f, .16f), Quaternion.identity, l.CurtainDeep, .8f, .8f, 8);
                kit.Blob(new Vector3(i * 2.05f, 2.02f, z - .32f), new Vector3(.26f, .3f, .2f), Quaternion.identity, l.Curtain, .8f, .8f, 8);
                kit.Blob(new Vector3(i * 2.05f, 2.36f, z - .34f), new Vector3(.17f, .06f, .17f), Quaternion.identity, gold, .6f, .6f, 8);
            }
            BuildBoxOrnaments(kit, root, z);
            Kit.Spawn("Emperor box", root, kit.ToMesh("Colosseum emperor box"));
        }

        /// <summary>
        /// The emperor is built in pieces (body, head, brows, eyes, mouth, arms, sceptre) so he can follow the fight,
        /// and react to it with a whole range of moods: leaping for joy, stomping in a rage, applauding, belly-laughing
        /// or throwing his hands up in shock.
        /// </summary>
        void BuildEmperor(Transform root, Vector3 home)
        {
            Color32 toga = C(0xfbf3e0, .1f), skin = C(0xeeb48c, .1f), skinDark = C(0xd99a78, .2f), white = C(0xffffff, .3f);
            Color32 gold = C(Kit.Gold, .9f), velvet = C(0x7a3ccf, .25f), velvetDeep = C(0x57259c, .25f), crimson = C(0xb3262e, .2f);
            Color32 ruby = C(0xe2334a, 1f), sapphire = C(0x3f8cff, 1f), ink = C(Kit.Ink, 1f);
            Quaternion none = Quaternion.identity;

            emperorHome = home;
            emperor = new GameObject("Emperor").transform;
            emperor.SetParent(root, false);
            emperor.SetPositionAndRotation(home, Quaternion.LookRotation(Vector3.back));
            emperor.localScale = Vector3.one * EmperorScale;

            var kit = new MeshKit();
            kit.Blob(new Vector3(0f, .62f, -.2f), new Vector3(.56f, .62f, .2f), none, crimson, .8f, .8f, 12);          // cape
            kit.Blob(new Vector3(0f, .3f, .12f), new Vector3(.47f, .32f, .42f), none, velvetDeep, .9f, .9f, 12);        // lap of the robe
            kit.Blob(new Vector3(0f, .68f, 0f), new Vector3(.43f, .44f, .34f), none, toga, .9f, .9f, 12);               // chest
            kit.Capsule(new Vector3(-.3f, .98f, .2f), new Vector3(.3f, .46f, .27f), .095f, .095f, velvet, 8);           // sash
            kit.Blob(new Vector3(0f, .47f, .03f), new Vector3(.45f, .06f, .37f), none, gold, .7f, .7f, 12);             // belt
            kit.Ball(new Vector3(0f, .49f, .39f), .08f, ruby, 8);
            kit.Blob(new Vector3(0f, 1.0f, -.02f), new Vector3(.47f, .13f, .36f), none, white, .8f, .8f, 12);           // ermine collar
            for (int i = 0; i < 7; i++)
            {
                float a = Mathf.Lerp(-70f, 70f, i / 6f) * Mathf.Deg2Rad;
                kit.Ball(new Vector3(Mathf.Sin(a) * .45f, 1.0f, Mathf.Cos(a) * .34f - .02f), .035f, ink, 5);
            }
            kit.Blob(new Vector3(0f, .8f, .34f), new Vector3(.1f, .1f, .035f), none, gold, 1f, 1f, 10);                  // medallion
            kit.Ball(new Vector3(0f, .8f, .37f), .045f, sapphire, 6);
            kingBody = Kit.Spawn("Emperor body", emperor, kit.ToMesh("Colosseum emperor body"), true).transform;

            kit = new MeshKit();
            kit.Blob(new Vector3(0f, .05f, -.1f), new Vector3(.37f, .32f, .31f), none, white, 1f, 1f, 12);              // hair
            kit.Ball(Vector3.zero, .33f, skin, 14);
            kit.Blob(new Vector3(0f, -.22f, .15f), new Vector3(.31f, .28f, .21f), none, white, .9f, .9f, 12);           // beard
            kit.Blob(new Vector3(0f, -.44f, .2f), new Vector3(.16f, .16f, .12f), none, white, .9f, .9f, 8);
            for (int i = -1; i <= 1; i += 2)
                kit.Blob(new Vector3(i * .13f, -.07f, .31f), new Vector3(.15f, .065f, .07f), Quaternion.Euler(0f, 0f, -i * 16f), white, 1f, 1f, 8);   // moustache
            kit.Ball(new Vector3(0f, -.01f, .34f), .075f, skinDark, 8);
            // Crown: jewelled band with seven points.
            kit.Blob(new Vector3(0f, .27f, 0f), new Vector3(.31f, .085f, .31f), none, gold, .45f, .45f, 14);
            kit.Blob(new Vector3(0f, .2f, 0f), new Vector3(.325f, .035f, .325f), none, white, .5f, .5f, 14);
            for (int i = 0; i < 7; i++)
            {
                float a = i * Mathf.PI * 2f / 7f;
                var foot = new Vector3(Mathf.Sin(a) * .27f, .3f, Mathf.Cos(a) * .27f);
                kit.Capsule(foot, foot + new Vector3(Mathf.Sin(a) * .05f, .26f, Mathf.Cos(a) * .05f), .075f, .012f, gold, 6);
                kit.Ball(foot + new Vector3(Mathf.Sin(a) * .06f, .29f, Mathf.Cos(a) * .06f), .04f, i % 2 == 0 ? ruby : sapphire, 5);
            }
            kit.Ball(new Vector3(0f, .28f, .31f), .06f, ruby, 8);
            kit.Blob(new Vector3(0f, .3f, 0f), new Vector3(.2f, .09f, .2f), none, velvet, 1f, 1f, 10);
            kingHead = Kit.Spawn("Emperor head", emperor, kit.ToMesh("Colosseum emperor head"), true).transform;
            kingHead.localPosition = new Vector3(0f, 1.3f, .02f);

            // Face parts live on their own so every mood can reshape them.
            Mesh ball(Color32 color)
            {
                var part = new MeshKit();
                part.Ball(Vector3.zero, 1f, color, 10);
                return part.ToMesh("Colosseum emperor face");
            }
            Mesh eyeMesh = ball(C(0xffffff, .5f)), pupilMesh = ball(ink), browMesh = ball(white), puffMesh = ball(C(0xffffff, .2f));
            for (int k = 0; k < 2; k++)
            {
                float side = k == 0 ? -1f : 1f;
                kingEyes[k] = Kit.Spawn("Emperor eye", kingHead, eyeMesh, true, false).transform;
                kingEyes[k].localPosition = new Vector3(side * .12f, .07f, .275f);
                kingPupils[k] = Kit.Spawn("Emperor pupil", kingHead, pupilMesh, false, false).transform;
                kingPupils[k].localPosition = new Vector3(side * .12f, .07f, .335f);
                kingBrows[k] = Kit.Spawn("Emperor brow", kingHead, browMesh, true, false).transform;
                kingSteam[k] = Kit.Spawn("Emperor steam", kingHead, puffMesh, false, false).transform;
                kingSteam[k].gameObject.SetActive(false);
            }
            kingMouth = Kit.Spawn("Emperor mouth", kingHead, ball(C(0x5a1a22, 0f)), false, false).transform;
            kingMouth.localPosition = new Vector3(0f, -.17f, .345f);
            kingMouth.localScale = new Vector3(.08f, .02f, .04f);

            // Comic anger marks: four bent red strokes around a gap, popping beside his crown while he rages.
            kit = new MeshKit();
            Color32 fury = C(0xff2e3a, .7f);
            for (int i = 0; i < 4; i++)
            {
                Quaternion turn = Quaternion.Euler(0f, 0f, i * 90f);
                Vector3 corner = turn * new Vector3(.24f, .24f, 0f);
                kit.Capsule(corner, turn * new Vector3(.9f, .34f, 0f), .12f, .1f, fury, 6);
                kit.Capsule(corner, turn * new Vector3(.34f, .9f, 0f), .12f, .1f, fury, 6);
                kit.Ball(corner, .12f, fury, 6);
            }
            Mesh anger = kit.ToMesh("Colosseum emperor anger");
            for (int k = 0; k < kingVeins.Length; k++)
            {
                kingVeins[k] = Kit.Spawn("Emperor anger", root, anger, true, false).transform;
                kingVeins[k].gameObject.SetActive(false);
            }

            for (int side = -1; side <= 1; side += 2)
            {
                kit = new MeshKit();
                kit.Capsule(Vector3.zero, new Vector3(0f, -.42f, .04f), .135f, .115f, velvet, 8);
                kit.Ball(Vector3.zero, .15f, gold, 8);
                kit.Blob(new Vector3(0f, -.4f, .04f), new Vector3(.13f, .05f, .13f), none, gold, .7f, .7f, 8);
                kit.Ball(new Vector3(0f, -.52f, .05f), .115f, skin, 8);
                Transform arm = Kit.Spawn("Emperor arm", emperor, kit.ToMesh("Colosseum emperor arm"), true).transform;
                arm.localPosition = new Vector3(side * .46f, 1.0f, .02f);
                if (side > 0) kingArmR = arm; else kingArmL = arm;
            }

            // Sceptre: one copy in his right hand, one propped against the throne for when he needs both hands.
            kit = new MeshKit();
            kit.Capsule(Vector3.zero, new Vector3(0f, 1.1f, 0f), .038f, .034f, gold, 6);
            kit.Blob(new Vector3(0f, 1.12f, 0f), new Vector3(.1f, .05f, .1f), none, gold, 1f, 1f, 8);
            kit.Ball(new Vector3(0f, 1.24f, 0f), .11f, ruby, 10);
            kit.Capsule(new Vector3(0f, 1.32f, 0f), new Vector3(0f, 1.46f, 0f), .05f, .008f, gold, 6);
            Mesh sceptre = kit.ToMesh("Colosseum emperor sceptre");
            kingSceptre = Kit.Spawn("Emperor sceptre", kingArmR, sceptre, true).transform;
            kingSceptre.localPosition = new Vector3(0f, -.52f, -.3f);
            kingSceptre.localRotation = Quaternion.Euler(90f, 0f, 0f);
            kingSceptreRest = Kit.Spawn("Emperor sceptre at rest", root, sceptre, true).transform;
            kingSceptreRest.SetPositionAndRotation(home + new Vector3(-.98f, -.12f, -.3f), Quaternion.Euler(-8f, 0f, 9f));
            kingSceptreRest.localScale = Vector3.one * EmperorScale;
            kingSceptreRest.gameObject.SetActive(false);
        }

        void BuildGatesAndBanners(Transform root, float hw, float hd)
        {
            var kit = new MeshKit();
            Look l = look;
            Color32 iron = l.Iron, dark = l.GateDark, gold = C(Kit.Gold, .9f);
            // North gate under the emperor.
            kit.Box(new Vector3(0f, .58f, hd + .05f), new Vector3(2.3f, 1.16f, .14f), Quaternion.identity, dark, dark);
            kit.Blob(new Vector3(0f, 1.16f, hd + .05f), new Vector3(1.15f, .5f, .08f), Quaternion.identity, dark, 1f, 1f, 12);
            for (int i = -4; i <= 4; i++)
                kit.Capsule(new Vector3(i * .26f, .04f, hd - .05f), new Vector3(i * .26f, 1.3f, hd - .05f), .04f, .04f, iron, 6);
            for (int i = 0; i < 3; i++)
                kit.Capsule(new Vector3(-1.08f, .35f + i * .4f, hd - .05f), new Vector3(1.08f, .35f + i * .4f, hd - .05f), .035f, .035f, iron, 6);
            // South gate for the blue side.
            Color32 blue = C(Kit.TeamMain[0], .3f), blueLight = C(Kit.TeamLight[0], .3f), blueDark = C(Kit.TeamDark[0], .3f);
            kit.Box(new Vector3(0f, .55f, -hd - 1.02f), new Vector3(3.9f, 1.1f, .8f), Quaternion.identity, blueDark, blue);
            for (int i = -6; i <= 6; i++)
                kit.Box(new Vector3(i * .3f, 1.13f, -hd - 1.02f), new Vector3(.14f, .07f, .72f), Quaternion.identity, blueLight, blueLight);
            kit.Blob(new Vector3(0f, 1.24f, -hd - 1.02f), new Vector3(.36f, .08f, .36f), Quaternion.identity, gold, 1f, 1f, 12);
            for (int i = 0; i < 2; i++)
            {
                var post = new Vector3((i == 0 ? -1f : 1f) * 2.2f, 0f, -hd - 1.02f);
                kit.Blob(post + Vector3.up * .8f, new Vector3(.34f, .82f, .34f), Quaternion.identity, l.Stone, .4f, .7f, 10);
                kit.Ball(post + Vector3.up * 1.76f, .2f, gold, 10);
            }
            Kit.Spawn("Gates", root, kit.ToMesh("Colosseum gates"));

            // Pennants on poles around the court, and long team banners on the stands.
            float edgeX = hw + .36f, edgeZ = hd + .36f;
            Vector3[] spots =
            {
                new Vector3(-edgeX, 0f, -hd * .5f), new Vector3(edgeX, 0f, -hd * .5f), new Vector3(-edgeX, 0f, hd * .5f), new Vector3(edgeX, 0f, hd * .5f),
                new Vector3(-hw * .55f, 0f, -edgeZ), new Vector3(hw * .55f, 0f, -edgeZ), new Vector3(-hw * .55f, 0f, edgeZ), new Vector3(hw * .55f, 0f, edgeZ)
            };
            var poles = new MeshKit();
            var cloth = new Mesh[2];
            for (int team = 0; team < 2; team++)
            {
                var flag = new MeshKit { Gradient = 0f };
                Color32 main = C(Kit.TeamMain[team], .2f), tip = C(Kit.TeamLight[team], .2f);
                for (int s = 0; s < 4; s++)
                {
                    float x0 = s * .26f, x1 = (s + 1) * .26f, h0 = .3f * (1f - s / 4f), h1 = .3f * (1f - (s + 1) / 4f);
                    Color32 tone = s % 2 == 0 ? main : tip;
                    flag.Quad(new Vector3(x0, h0, 0f), new Vector3(x1, h1, 0f), new Vector3(x1, -h1, 0f), new Vector3(x0, -h0, 0f), tone);
                    flag.Quad(new Vector3(x0, h0, 0f), new Vector3(x0, -h0, 0f), new Vector3(x1, -h1, 0f), new Vector3(x1, h1, 0f), tone);
                }
                cloth[team] = flag.ToMesh("Colosseum pennant");
            }
            foreach (Vector3 spot in spots)
            {
                Transform host = root;
                if (Mathf.Abs(spot.x) > hw)
                {
                    // A pole on a side wall is an object of its own, pennant and all, because an earthquake may
                    // take the wall from under it.
                    var own = new MeshKit();
                    own.Capsule(Vector3.up * .5f, Vector3.up * 2.7f, .055f, .045f, l.Pole, 6);
                    own.Ball(Vector3.up * 2.78f, .1f, C(Kit.Gold, .9f), 8);
                    host = Kit.Spawn("Side flag pole", root, own.ToMesh("Colosseum side pole")).transform;
                    host.position = spot;
                    sideFlags.Add(new SideFlag { Pole = host, Home = spot, Side = spot.x > 0f ? 1 : -1 });
                }
                else
                {
                    poles.Capsule(spot + Vector3.up * .5f, spot + Vector3.up * 2.7f, .055f, .045f, l.Pole, 6);
                    poles.Ball(spot + Vector3.up * 2.78f, .1f, C(Kit.Gold, .9f), 8);
                }
                Transform pennant = Kit.Spawn("Pennant", host, cloth[spot.z >= 0f ? 1 : 0], false, false).transform;
                pennant.position = spot + Vector3.up * 2.42f;
                sways.Add(new Sway { Item = pennant, Rest = Quaternion.identity, Phase = Random.value * 6.28f, Amount = 24f, Rate = 5f, Axis = Vector3.up });
            }
            Kit.Spawn("Flag poles", root, poles.ToMesh("Colosseum poles"));

            // Long banners hanging in every other arch of the far arcade.
            var banner = new MeshKit { Gradient = 0f };
            Color32 red = C(Kit.TeamMain[1], .15f), deep = C(Kit.TeamDark[1], .15f), trim = C(Kit.Gold, .7f);
            banner.Box(new Vector3(0f, -1f, 0f), new Vector3(1f, 2f, .05f), Quaternion.identity, deep, red);
            banner.Box(new Vector3(0f, -1f, .03f), new Vector3(.74f, 1.7f, .03f), Quaternion.identity, red, red);
            banner.Box(new Vector3(0f, -.02f, 0f), new Vector3(1.2f, .1f, .1f), Quaternion.identity, trim, trim);
            banner.Blob(new Vector3(0f, -.95f, .06f), new Vector3(.25f, .25f, .03f), Quaternion.identity, trim, 1f, 1f, 12);
            banner.Blob(new Vector3(0f, -.95f, .08f), new Vector3(.14f, .14f, .03f), Quaternion.identity, red, 1f, 1f, 10);
            Mesh bannerMesh = banner.ToMesh("Colosseum banner");
            float back = hd + TierStart + Tiers * TierStep, wideAll = hw + TierStart + Tiers * TierStep + 1f;
            int arch = 0;
            for (float x = -wideAll + 1.1f; x <= wideAll - 1.1f; x += 2.15f, arch++)
            {
                if (arch % 2 == 0) continue;
                Transform item = Kit.Spawn("Banner", root, bannerMesh, false, false).transform;
                Quaternion rest = Quaternion.LookRotation(Vector3.back);
                item.SetPositionAndRotation(new Vector3(x, 4.75f, back - .2f), rest);
                sways.Add(new Sway { Item = item, Rest = rest, Phase = Random.value * 6.28f, Amount = 5f, Rate = 2.2f, Axis = Vector3.right });
            }
        }

        /// <summary>Crowd hops, sways and does the wave harder the more exciting the fight gets (0..1.6).</summary>
        public void Tick(float delta, float excitement)
        {
            clock += delta;
            float lift = .06f + excitement * .3f;
            for (int i = 0; i < fans.Count; i++)
            {
                Fan fan = fans[i];
                float beat = Mathf.Sin(clock * fan.Rate + fan.Phase);
                float wave = Mathf.Max(0f, Mathf.Sin(clock * 2.6f - fan.Wave * 2f)) * excitement * .22f;
                float hop = (Mathf.Max(0f, beat) * lift + wave) * fan.Size;
                fan.Item.SetPositionAndRotation(fan.Home + Vector3.up * hop,
                    fan.Facing * Quaternion.Euler(0f, 0f, Mathf.Sin(clock * fan.Rate * .5f + fan.Phase) * (4f + excitement * 7f)));
            }
            for (int i = 0; i < sways.Count; i++)
            {
                Sway sway = sways[i];
                sway.Item.localRotation = sway.Rest * Quaternion.AngleAxis(Mathf.Sin(clock * sway.Rate + sway.Phase) * sway.Amount, sway.Axis);
            }
            for (int i = 0; i < flames.Count; i++)
            {
                float flicker = 1f + Mathf.Sin(clock * 17f + i * 2.1f) * .12f + Mathf.Sin(clock * 29f + i) * .07f;
                Vector3 scale = flames[i].localScale;
                flames[i].localScale = new Vector3(scale.x, Mathf.Abs(scale.x) * flicker * 1.15f, scale.z);
                flames[i].localRotation = Quaternion.Euler(Mathf.Sin(clock * 9f + i) * 6f, clock * 90f, Mathf.Cos(clock * 11f + i) * 6f);
            }
            TickEmperor(delta, excitement);
        }

        /// <summary>Tell the emperor where the action is so he can follow it with his head.</summary>
        public void Watch(Vector3 point) { kingFocus = point; }

        /// <summary>Send the emperor leaping to his feet; when the cheering ends he settles into applause.</summary>
        public void Cheer(float seconds, float power)
        {
            SetKingMood(KingJump, seconds, power);
            kingNext = KingClap;
        }

        /// <summary>A hero went down: he either rages, leaps about or gasps (then claps), never the same twice running.</summary>
        public void ReactToKnockout()
        {
            if (kingMood == KingJump && kingMoodLeft > 2f) return;
            int[] picks = { KingRage, KingJump, KingGasp, KingRage, KingLaugh };
            int pick;
            do pick = picks[Random.Range(0, picks.Length)];
            while (pick == kingLastKo);
            kingLastKo = pick;
            SetKingMood(pick, pick == KingGasp ? 1.1f : 2.1f, .7f);
            kingNext = pick == KingGasp ? KingClap : KingIdle;
        }

        /// <summary>A solid hit landed: a round of applause or a belly laugh, unless he is already busy reacting.</summary>
        public void ReactToHit()
        {
            if (kingMood != KingIdle) return;
            SetKingMood(Random.value < .6f ? KingClap : KingLaugh, 1.5f, .5f);
            kingNext = KingIdle;
        }

        /// <summary>Force one mood for a while (1 leap, 2 rage, 3 applause, 4 laugh, 5 shock); used by the editor self-test.</summary>
        public void ShowMood(int mood, float seconds)
        {
            SetKingMood(Mathf.Clamp(mood, KingIdle, KingMoods - 1), seconds, 1f);
            kingNext = KingIdle;
        }

        void SetKingMood(int mood, float seconds, float power)
        {
            kingMood = mood;
            kingMoodLeft = seconds;
            kingPower = Mathf.Clamp01(power);
        }

        void TickEmperor(float delta, float excitement)
        {
            if (emperor == null) return;
            if (kingMood != KingIdle)
            {
                kingMoodLeft -= delta;
                if (kingMoodLeft <= 0f)
                {
                    int next = kingNext;
                    kingNext = KingIdle;
                    if (next != KingIdle) SetKingMood(next, 1.7f, .5f); else kingMood = KingIdle;
                }
            }
            for (int i = 0; i < KingMoods; i++)
                kingWeight[i] = Mathf.MoveTowards(kingWeight[i], i == kingMood ? 1f : 0f, delta * 6f);
            float jump = kingWeight[KingJump], rage = kingWeight[KingRage], clap = kingWeight[KingClap], laugh = kingWeight[KingLaugh], gasp = kingWeight[KingGasp];
            float seated = Mathf.Clamp01(1f - jump - rage - gasp * .6f);
            float calm = Mathf.Clamp01(1f - jump - rage - clap - laugh - gasp);

            float thrill = Mathf.Clamp01((excitement - .35f) / .9f) * calm;
            kingPump = Mathf.MoveTowards(kingPump, thrill, delta * 4f);
            kingStroke -= delta;
            if (kingStroke < -1.6f) kingStroke = Random.Range(4f, 8f);
            float stroke = kingStroke < 0f ? Mathf.Sin(-kingStroke / 1.6f * Mathf.PI) * calm * (1f - kingPump) : 0f;
            float power = Mathf.Lerp(.55f, 1f, kingPower);
            float t = clock;

            // Whole body: leaping, stomping, a little bounce while clapping, sitting back to laugh, recoiling in shock.
            float rise = jump * .2f + rage * .2f + gasp * .12f + clap * .04f;
            float hop = jump * Mathf.Abs(Mathf.Sin(t * 9.5f)) * .22f * power + rage * Mathf.Abs(Mathf.Sin(t * 14f)) * .1f + clap * Mathf.Abs(Mathf.Sin(t * 13f)) * .03f;
            emperor.position = emperorHome + Vector3.up * (rise + hop);
            float lean = calm * (-4f + kingPump * 12f + Mathf.Sin(t * 1.7f) * 1.2f) + jump * -10f + rage * 3f + clap * 5f
                + laugh * (-15f + Mathf.Sin(t * 11f) * 3f) + gasp * -13f;
            float rock = jump * Mathf.Sin(t * 9.5f) * 5f + rage * Mathf.Sin(t * 7f) * 9f + laugh * Mathf.Sin(t * 5.5f) * 3f;
            float twist = jump * Mathf.Sin(t * 4.75f) * 7f + rage * Mathf.Sin(t * 7f + 1f) * 6f;
            emperor.rotation = Quaternion.LookRotation(Vector3.back) * Quaternion.Euler(lean, twist, rock);
            float breath = 1f + Mathf.Sin(t * 2.1f) * .018f + laugh * Mathf.Sin(t * 22f) * .05f;
            float stretch = 1f + (hop - .12f * (jump + rage)) * .22f;
            kingBody.localScale = new Vector3(2f - stretch, breath * stretch * (1f + (jump + rage) * .12f), 2f - stretch);

            // Head: follows the action when calm; thrown back, shaking or bobbing with each mood.
            Vector3 toward = Quaternion.Inverse(emperor.rotation) * (kingFocus - kingHead.position);
            float yaw = Mathf.Clamp(Mathf.Atan2(toward.x, Mathf.Max(.5f, toward.z)) * Mathf.Rad2Deg, -42f, 42f);
            kingYaw = Mathf.Lerp(kingYaw, yaw, 1f - Mathf.Exp(-5f * delta));
            float nod = calm * (-24f + kingPump * 5f + stroke * 5f + Mathf.Sin(t * .9f) * 2f) + jump * (-30f + Mathf.Sin(t * 9.5f) * 8f)
                + rage * (-34f + Mathf.Sin(t * 14f) * 5f) + clap * (-24f + Mathf.Sin(t * 13f) * 3f) + laugh * (-38f + Mathf.Sin(t * 11f) * 8f) + gasp * -22f;
            float turn = kingYaw * (calm + clap * .6f + gasp) + jump * Mathf.Sin(t * 6f) * 14f + rage * Mathf.Sin(t * 19f) * 15f + laugh * Mathf.Sin(t * 3f) * 8f;
            float tilt = jump * Mathf.Sin(t * 4.75f) * 6f + laugh * Mathf.Sin(t * 5.5f) * 6f + clap * Mathf.Sin(t * 6.5f) * 5f;
            kingHead.localRotation = Quaternion.Euler(nod, turn, tilt);
            kingHead.localPosition = new Vector3(0f, 1.3f + (kingBody.localScale.y - 1f) * 1.1f, .02f);

            // Face: brows, eyes and mouth are blended from one target shape per mood.
            float browTilt = calm * (12f + kingPump * 8f) + jump * -12f + rage * 34f + clap * -10f + laugh * -16f + gasp * -6f;
            float browLift = jump * .045f + rage * -.035f + clap * .03f + laugh * .05f + gasp * .09f + calm * kingPump * -.01f;
            float eyeOpen = calm * 1f + jump * .5f + rage * .78f + clap * .62f + laugh * .22f + gasp * 1.4f;
            float pupil = calm * 1f + jump * 1f + rage * .62f + clap * 1f + laugh * 1f + gasp * .5f;
            float mouthWide = calm * (.08f + kingPump * .04f) + jump * .135f + rage * .15f + clap * .13f + laugh * .16f + gasp * .07f;
            float mouthTall = calm * (.02f + kingPump * (.5f + Mathf.Sin(t * 15f) * .2f) * .09f) + jump * (.1f + Mathf.Sin(t * 12f) * .025f)
                + rage * (.105f + Mathf.Sin(t * 20f) * .03f) + clap * .045f + laugh * (.115f + Mathf.Sin(t * 22f) * .035f) + gasp * .1f;
            for (int k = 0; k < 2; k++)
            {
                float side = k == 0 ? -1f : 1f;
                kingBrows[k].localPosition = new Vector3(side * .13f, .2f + browLift, .29f);
                kingBrows[k].localRotation = Quaternion.Euler(0f, 0f, side * browTilt);
                kingBrows[k].localScale = new Vector3(.115f, .042f, .045f);
                kingEyes[k].localScale = new Vector3(.078f, .078f * Mathf.Max(.12f, eyeOpen), .078f);
                kingPupils[k].localScale = Vector3.one * (.04f * Mathf.Max(.3f, pupil)) * Mathf.Clamp01(eyeOpen * 2.2f);
                // Steam whistling out of his ears while he rages.
                bool fuming = rage > .3f;
                if (kingSteam[k].gameObject.activeSelf != fuming) kingSteam[k].gameObject.SetActive(fuming);
                if (fuming)
                {
                    float life = Mathf.Repeat(t * 1.9f + k * .5f, 1f);
                    kingSteam[k].localPosition = new Vector3(side * (.36f + life * .22f), .04f + life * .5f, -.02f);
                    kingSteam[k].localScale = Vector3.one * (Mathf.Sin(life * Mathf.PI) * .13f * rage);
                }
            }
            kingMouth.localScale = new Vector3(mouthWide, mouthTall, .04f);
            bool seething = rage > .25f;
            for (int k = 0; k < kingVeins.Length; k++)
            {
                if (kingVeins[k].gameObject.activeSelf != seething) kingVeins[k].gameObject.SetActive(seething);
                if (!seething) continue;
                // Each mark snaps open on its own beat, overshoots, then throbs until the next one.
                float beat = Mathf.Repeat(t * 2.6f + k * .5f, 1f);
                float pop = beat < .16f ? Mathf.Lerp(.35f, 1.35f, beat / .16f) : Mathf.Lerp(1.35f, 1f, Mathf.Clamp01((beat - .16f) / .2f));
                kingVeins[k].position = kingHead.position + (k == 0 ? new Vector3(.6f, .42f, 0f) : new Vector3(-.54f, .62f, 0f));
                kingVeins[k].rotation = Quaternion.Euler(ColosseumGame.CameraPitch, 0f, 0f)
                    * Quaternion.Euler(0f, 0f, (k == 0 ? 12f : -20f) + Mathf.Sin(t * 17f + k * 2f) * 9f);
                kingVeins[k].localScale = Vector3.one * ((k == 0 ? .27f : .17f) * pop * rage);
            }

            // Arms. Angles are (swing forward/up, 0, out to the side): for the right arm a positive last value is outward.
            Quaternion right = Quaternion.Euler(-78f + Mathf.Sin(t * 1.3f) * 2.5f - Mathf.Abs(Mathf.Sin(t * 10f)) * 14f * kingPump, 0f, -10f);
            Quaternion left = Quaternion.Slerp(Quaternion.Slerp(Quaternion.Euler(-34f, 0f, -12f), Quaternion.Euler(-128f + Mathf.Sin(t * 7f) * 6f, 0f, 44f), stroke),
                Quaternion.Euler(-150f - Mathf.Sin(t * 15f) * 16f, 0f, -14f), kingPump);
            // Leaping: both arms thrown up in a V.
            right = Quaternion.Slerp(right, Quaternion.Euler(-160f + Mathf.Sin(t * 11f) * 18f, 0f, 38f + Mathf.Sin(t * 11f) * 14f), jump);
            left = Quaternion.Slerp(left, Quaternion.Euler(-160f + Mathf.Sin(t * 11f + 1.7f) * 18f, 0f, -38f - Mathf.Sin(t * 11f + 1.7f) * 14f), jump);
            // Raging: hammering the sceptre down and shaking a fist overhead.
            right = Quaternion.Slerp(right, Quaternion.Euler(-112f + Mathf.Sin(t * 12f) * 46f, 0f, 16f), rage);
            left = Quaternion.Slerp(left, Quaternion.Euler(-158f + Mathf.Sin(t * 19f) * 13f, 0f, -26f), rage);
            // Applauding: hands meet in front of the chest.
            float apart = Mathf.Abs(Mathf.Sin(t * 13f));
            right = Quaternion.Slerp(right, Quaternion.Euler(-100f, 0f, -50f + apart * 24f), clap);
            left = Quaternion.Slerp(left, Quaternion.Euler(-100f, 0f, 50f - apart * 24f), clap);
            // Laughing: one hand on the belly.
            left = Quaternion.Slerp(left, Quaternion.Euler(-58f + Mathf.Sin(t * 22f) * 4f, 0f, 40f), laugh);
            // Shock: arms flung wide, trembling.
            right = Quaternion.Slerp(right, Quaternion.Euler(-118f + Mathf.Sin(t * 40f) * 3f, 0f, 64f), gasp);
            left = Quaternion.Slerp(left, Quaternion.Euler(-118f + Mathf.Sin(t * 40f + 1f) * 3f, 0f, -64f), gasp);
            kingArmR.localRotation = right;
            kingArmL.localRotation = left;
            float shoulder = 1.0f + (kingBody.localScale.y - 1f) * .8f;
            kingArmR.localPosition = new Vector3(.46f, shoulder, .02f);
            kingArmL.localPosition = new Vector3(-.46f, shoulder, .02f);

            // He props the sceptre against the throne whenever he needs both hands free.
            bool handsFree = clap + gasp > .5f;
            if (kingSceptre.gameObject.activeSelf == handsFree) kingSceptre.gameObject.SetActive(!handsFree);
            if (kingSceptreRest.gameObject.activeSelf != handsFree) kingSceptreRest.gameObject.SetActive(handsFree);
        }
    }
}
