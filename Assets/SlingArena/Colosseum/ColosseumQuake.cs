using System.Collections.Generic;
using UnityEngine;

namespace SlingArena.Colosseum
{
    /// <summary>
    /// On the third turn the ground shakes. Stretches of the side walls tremble and come down
    /// one after another, and the ground they stood on drops away into a ditch. Whoever goes out through a gap is
    /// out of the fight. Which stretches go is different every match: how many, where along the walls, and how long
    /// each is, and never where the wall came down in the last earthquake. The two teams always get the same, each
    /// as it sees the arena from its own end.
    /// </summary>
    public sealed class ColosseumQuake
    {
        // Seconds of trembling before the first stretch goes, between one stretch and the next, and for the dust to
        // settle after the last.
        const float ShakeFor = 1.15f, Stagger = .24f, SettleFor = .5f;
        // How far past the line of the wall a hero's middle has to be to count as out. A hero against a standing
        // wall is well inside the line, so anyone past it has gone through a gap.
        const float OutLine = .05f;
        const int ChunkCount = 60;
        // In each team's half no more than this many battlements' worth of wall comes down, and on one wall at least
        // this many are left standing between one gap and the next.
        const int Between = 2;
        // How many battlements may come down in one team's half, and how many stretches that may be at most.
        // Both are set from the balance sheet at the start of a match (see Tune).
        static int HalfBudget = 7, MostGaps = 3;

        /// <summary>How much of the walls an earthquake brings down in the sets to come.</summary>
        public static void Tune(int mostGaps, int halfBudget)
        {
            MostGaps = Mathf.Clamp(mostGaps, 1, 3);
            HalfBudget = Mathf.Clamp(halfBudget, 2, 7);
        }

        /// <summary>One stretch of wall that falls, and what it leaves behind.</summary>
        sealed class Gap
        {
            public ColosseumArena.WallCut Cut;
            public GameObject Wreck;        // shown once the stretch has gone: the ditch, the rubble, the warnings
            public Transform Cracks;        // cracks running into the court, opening while the wall trembles
            public MeshRenderer Glow;
            public MeshRenderer[] Arrows;   // two rows of warning chevrons, lit one after the other
            public float BreakAt, DustClock, Seed;
            public bool Down;
        }

        /// <summary>A lump of stone thrown up as a stretch comes down.</summary>
        sealed class Chunk
        {
            public Transform Item;
            public Vector3 Velocity, Spin;
            public float Age, Size;
            public bool Active, Landed;
        }

        readonly ColosseumArena arena;
        readonly Transform root;
        readonly float hw, hd;
        readonly Material stroke;
        readonly MaterialPropertyBlock block = new MaterialPropertyBlock();
        readonly List<Chunk> chunks = new List<Chunk>();
        // Its own dice, so that where the walls break owes nothing to whatever else in the game is drawing lots.
        readonly System.Random dice = new System.Random(System.Guid.NewGuid().GetHashCode());
        // The stretches picked for the south half this set (which wall, and the breaks each runs from and to), and
        // the ones that actually came down in the last earthquake and in the one before it. A set's stretches are
        // picked clear of where the wall last went, so that nobody sees the same piece of it go twice running.
        readonly List<Vector3Int> picked = new List<Vector3Int>(), fallen = new List<Vector3Int>(), fallenBefore = new List<Vector3Int>();
        Gap[] gaps = new Gap[0];   // the gaps for the way the walls are going to break up this set
        int chunkLook;             // the look of the arena the lumps of rubble were last made for
        int chunkCursor;
        float clock = -1f;       // seconds since the ground began to shake; negative while it is still

        /// <summary>A stretch of wall has just come down: the middle of the gap on the line of the wall, and which side.</summary>
        public System.Action<Vector3, int> Crumbled;

        public ColosseumQuake(ColosseumGame owner, ColosseumArena arena, float halfWidth, float halfDepth)
        {
            this.arena = arena;
            hw = halfWidth;
            hd = halfDepth;
            root = new GameObject("Colosseum earthquake").transform;
            root.SetParent(owner.World, false);
            stroke = Kit.StrokeMaterial(Color.white);
            Mesh[] shapes = ChunkMeshes();
            chunkLook = arena.LookIndex;
            for (int i = 0; i < ChunkCount; i++)
            {
                GameObject item = Kit.Spawn("Rubble chunk", root, shapes[i % shapes.Length]);
                item.SetActive(false);
                chunks.Add(new Chunk { Item = item.transform });
            }
            Reset();
        }

        /// <summary>The ground is shaking, or stretches of wall are still coming down.</summary>
        public bool Shaking
        {
            get { return clock >= 0f && clock < ShakeFor + (gaps.Length - 1) * Stagger + SettleFor; }
        }

        /// <summary>At least one stretch of wall is down.</summary>
        public bool Open { get; private set; }

        /// <summary>How hard the camera should be shaking right now.</summary>
        public float Rumble
        {
            get { return Shaking ? .07f + .09f * Mathf.Clamp01(clock / ShakeFor) : 0f; }
        }

        /// <summary>
        /// A new set: the walls are built whole again, and where they will break up this time is picked afresh.
        /// </summary>
        public void Reset()
        {
            clock = -1f;
            Open = false;
            foreach (Gap gap in gaps) Scrap(gap);
            foreach (Chunk chunk in chunks)
            {
                chunk.Active = false;
                chunk.Item.gameObject.SetActive(false);
            }
            if (chunkLook != arena.LookIndex)
            {
                // The arena has been dressed for another map: the rubble is of its stone now.
                chunkLook = arena.LookIndex;
                Mesh[] shapes = ChunkMeshes();
                for (int i = 0; i < chunks.Count; i++)
                {
                    MeshFilter filter = chunks[i].Item.GetComponent<MeshFilter>();
                    if (i < shapes.Length) Object.Destroy(filter.sharedMesh);
                    filter.sharedMesh = Kit.Own(shapes[i % shapes.Length]);
                }
            }
            arena.RaiseWalls(PickCuts());
            IList<ColosseumArena.WallCut> cuts = arena.WallCuts;
            gaps = new Gap[cuts.Count];
            for (int c = 0; c < gaps.Length; c++)
                gaps[c] = BuildGap(cuts[c], dice.Next(100000));
        }

        /// <summary>Everything that was made for a gap, and is not wanted now the walls are to break up another way.</summary>
        static void Scrap(Gap gap)
        {
            foreach (MeshFilter filter in gap.Wreck.GetComponentsInChildren<MeshFilter>(true))
                Object.Destroy(filter.sharedMesh);
            Object.Destroy(gap.Cracks.GetComponent<MeshFilter>().sharedMesh);
            gap.Wreck.SetActive(false);
            gap.Cracks.gameObject.SetActive(false);
            Object.Destroy(gap.Wreck);
            Object.Destroy(gap.Cracks.gameObject);
        }

        /// <summary>
        /// Where the walls will break up this set. In the south team's half one, two or three stretches are picked,
        /// on either wall, anywhere between the corner and the half-way post, two to four battlements long, never
        /// so close together on one wall that what is left between them would be a stump, and never where a stretch
        /// came down in the last earthquake. The north half gets the same stretches turned about the middle of the
        /// court, so that neither team has the better of it.
        /// </summary>
        // An end wall only breaks clear of the gate in its middle (and the posts that flank the blue one).
        const float EndGateClear = 2.4f;

        /// <summary>
        /// Half the time the earthquake takes the end walls instead: two battlements' worth of the south wall, on
        /// one side of its gate, and the same stretch of the north wall turned about the middle of the court.
        /// </summary>
        List<ColosseumArena.WallCut> PickEndCuts()
        {
            int last = arena.EndBreaks - 1;
            const int length = 2;
            var options = new List<Vector3Int>();
            for (int from = 0; from + length <= last; from++)
            {
                int to = from + length;
                float a = arena.EndBreak(from), b = arena.EndBreak(to);
                if (a * b < 0f || Mathf.Min(Mathf.Abs(a), Mathf.Abs(b)) < EndGateClear) continue;
                var pick = new Vector3Int(-2, from, to);
                if (!fallen.Contains(pick)) options.Add(pick);
            }
            if (options.Count == 0) return null;
            Vector3Int chosen = options[dice.Next(options.Count)];
            picked.Clear();
            picked.Add(chosen);
            return new List<ColosseumArena.WallCut>
            {
                new ColosseumArena.WallCut { End = true, Side = -1, From = arena.EndBreak(chosen.y), To = arena.EndBreak(chosen.z) },
                new ColosseumArena.WallCut { End = true, Side = 1, From = arena.EndBreak(last - chosen.z), To = arena.EndBreak(last - chosen.y) }
            };
        }

        List<ColosseumArena.WallCut> PickCuts()
        {
            if (dice.Next(2) == 0)
            {
                List<ColosseumArena.WallCut> ends = PickEndCuts();
                if (ends != null) return ends;
            }
            int last = arena.WallBreaks - 1, post = last / 2;
            double chance = dice.NextDouble();
            // No more stretches than the sheet allows, nor than there are battlements to make them of.
            // One stretch only: with the north half turned about the middle, that is one gap in each side wall,
            // somewhere along it at random.
            int want = 1;
            picked.Clear();
            int left = HalfBudget;
            for (int tries = 0; picked.Count < want && tries < 120; tries++)
            {
                // Where there is no room left clear of the last earthquake's gaps, fewer come down this time.
                if (tries >= 90 && picked.Count > 0) break;
                int still = want - picked.Count;
                // One gap alone is a big one; three are small ones.
                int shortest = Mathf.Min(2, left), longest = Mathf.Min(2, left - (still - 1) * 2);
                if (longest < shortest) break;
                // Two on one wall can only be at its two ends, so a second gap is more often than not put on the
                // other wall from the first, where it can be anywhere.
                int side = dice.Next(2) == 0 ? -1 : 1, length = dice.Next(shortest, longest + 1);
                if (picked.Count == 1 && dice.Next(4) != 0) side = -picked[0].x;
                // The battlement by the corner and the one by the post always stand.
                int from = dice.Next(0, post - 1 - length + 1), to = from + length;
                bool crowded = false;
                foreach (Vector3Int other in picked)
                {
                    if (other.x == side && from < other.z + Between && other.y < to + Between) crowded = true;
                }
                // Not where the wall went in the last earthquake: nothing of an old gap may be part of a new one.
                // Only if that leaves nowhere at all is it enough for the stretch not to be the very same one.
                foreach (Vector3Int old in fallen)
                {
                    if (old.x != side) continue;
                    if (tries < 90 ? from < old.z && old.y < to : from == old.y && to == old.z) crowded = true;
                }
                // Nor the very same stretch as in the earthquake before that.
                foreach (Vector3Int old in fallenBefore)
                {
                    if (tries < 90 && old.x == side && from == old.y && to == old.z) crowded = true;
                }
                if (crowded) continue;
                picked.Add(new Vector3Int(side, from, to));
                left -= length;
            }
            var cuts = new List<ColosseumArena.WallCut>();
            foreach (Vector3Int pick in picked)
            {
                cuts.Add(new ColosseumArena.WallCut { Side = pick.x, From = arena.WallBreak(pick.y), To = arena.WallBreak(pick.z) });
                cuts.Add(new ColosseumArena.WallCut { Side = -pick.x, From = arena.WallBreak(last - pick.z), To = arena.WallBreak(last - pick.y) });
            }
            return cuts;
        }

        /// <summary>These stretches are coming down where everyone can see: the next sets will break up elsewhere.</summary>
        void Remember()
        {
            fallenBefore.Clear();
            fallenBefore.AddRange(fallen);
            fallen.Clear();
            fallen.AddRange(picked);
        }

        /// <summary>The ground starts to shake. The stretches go one after another, in no particular order.</summary>
        public void Begin()
        {
            if (clock >= 0f) return;
            clock = 0f;
            Remember();
            var order = new List<int>();
            for (int i = 0; i < gaps.Length; i++) order.Insert(Random.Range(0, order.Count + 1), i);
            for (int i = 0; i < gaps.Length; i++)
            {
                Gap gap = gaps[order[i]];
                gap.BreakAt = ShakeFor + i * Stagger;
                gap.DustClock = Random.value * .12f;
                gap.Cracks.localScale = new Vector3(.01f, 1f, 1f);
                gap.Cracks.gameObject.SetActive(true);
            }
        }

        /// <summary>True for a hero that has gone out through a gap.</summary>
        public bool IsOut(Vector3 point)
        {
            return Open && (Mathf.Abs(point.x) > hw + OutLine || Mathf.Abs(point.z) > hd + OutLine);
        }

        /// <summary>True where a stretch of wall has come down: at or past the line of the wall, between the ends of a gap.</summary>
        public bool GapAt(Vector3 point)
        {
            if (!Open) return false;
            bool nearSide = Mathf.Abs(point.x) >= hw - .1f, nearEnd = Mathf.Abs(point.z) >= hd - .1f;
            if (!nearSide && !nearEnd) return false;
            int sideX = point.x > 0f ? 1 : -1, sideZ = point.z > 0f ? 1 : -1;
            foreach (Gap gap in gaps)
            {
                if (!gap.Down) continue;
                ColosseumArena.WallCut cut = gap.Cut;
                if (!cut.End && nearSide && cut.Side == sideX && point.z > cut.From && point.z < cut.To) return true;
                if (cut.End && nearEnd && cut.Side == sideZ && point.x > cut.From && point.x < cut.To) return true;
            }
            return false;
        }

        /// <summary>
        /// True if something slung from here in this direction would come to an open gap before a standing wall;
        /// reach is how far away the gap is.
        /// </summary>
        public bool LeadsOut(Vector3 from, Vector3 direction, out float reach)
        {
            reach = 0f;
            if (!Open) return false;
            float best = float.PositiveInfinity;
            if (Mathf.Abs(direction.x) > .001f)
            {
                int side = direction.x > 0f ? 1 : -1;
                float r = (side * hw - from.x) / direction.x, z = from.z + direction.z * r;
                if (Mathf.Abs(z) <= hd)
                    foreach (Gap gap in gaps)
                        if (gap.Down && !gap.Cut.End && gap.Cut.Side == side && z > gap.Cut.From - .15f && z < gap.Cut.To + .15f) best = Mathf.Min(best, r);
            }
            if (Mathf.Abs(direction.z) > .001f)
            {
                int side = direction.z > 0f ? 1 : -1;
                float r = (side * hd - from.z) / direction.z, x = from.x + direction.x * r;
                if (Mathf.Abs(x) <= hw)
                    foreach (Gap gap in gaps)
                        if (gap.Down && gap.Cut.End && gap.Cut.Side == side && x > gap.Cut.From - .15f && x < gap.Cut.To + .15f) best = Mathf.Min(best, r);
            }
            if (float.IsPositiveInfinity(best)) return false;
            reach = best;
            return true;
        }

        Vector3 At(ColosseumArena.WallCut cut, float outward, float height, float along)
        {
            return cut.End ? new Vector3(along, height, cut.Side * (hd + outward)) : new Vector3(cut.Side * (hw + outward), height, along);
        }

        // ------------------------------------------------------------------ per frame

        public void Tick(float delta, ColosseumFx fx)
        {
            if (clock >= 0f)
            {
                clock += delta;
                for (int i = 0; i < gaps.Length; i++)
                {
                    Gap gap = gaps[i];
                    if (gap.Down) continue;
                    // The stretch trembles harder and harder, shedding dust, while cracks open in the sand below it.
                    float k = Mathf.Clamp01(clock / gap.BreakAt);
                    float wild = .012f + .055f * k * k;
                    arena.ShakeWall(i, new Vector3(Random.Range(-1f, 1f) * wild, Random.value * wild * .6f, Random.Range(-1f, 1f) * wild),
                        Quaternion.Euler(Random.Range(-1f, 1f) * wild * 45f, 0f, Random.Range(-1f, 1f) * wild * 70f));
                    gap.Cracks.localScale = new Vector3(Mathf.Max(.01f, .75f * k), 1f, 1f);
                    gap.DustClock -= delta;
                    if (gap.DustClock <= 0f)
                    {
                        gap.DustClock = Mathf.Lerp(.16f, .06f, k);
                        fx.Puff(At(gap.Cut, Random.Range(-.1f, .5f), ColosseumArena.WallHigh, Random.Range(gap.Cut.From, gap.Cut.To)), 1, 1.3f);
                    }
                    if (clock >= gap.BreakAt) Break(i, fx);
                }
            }

            float time = Time.time;
            foreach (Gap gap in gaps)
            {
                if (!gap.Down) continue;
                // The chevrons light up one row after the other, running towards the gap.
                float beat = time * 5.2f + gap.Seed;
                block.SetColor("_Color", new Color(1f, 1f, 1f, .68f + .32f * Mathf.Max(0f, Mathf.Sin(beat))));
                gap.Arrows[0].SetPropertyBlock(block);
                block.SetColor("_Color", new Color(1f, 1f, 1f, .68f + .32f * Mathf.Max(0f, Mathf.Sin(beat - 1.3f))));
                gap.Arrows[1].SetPropertyBlock(block);
                block.SetColor("_Color", new Color(1f, 1f, 1f, .64f + .36f * Mathf.Sin(time * 3.1f + gap.Seed)));
                gap.Glow.SetPropertyBlock(block);
            }

            foreach (Chunk chunk in chunks)
            {
                if (!chunk.Active) continue;
                chunk.Age += delta;
                if (chunk.Landed)
                {
                    // Lies where it fell for a moment, then is gone.
                    float k = Mathf.Clamp01((chunk.Age - .3f) / .35f);
                    chunk.Item.localScale = Vector3.one * (chunk.Size * (1f - k));
                    if (k >= 1f) Rest(chunk);
                    continue;
                }
                chunk.Velocity.y -= 24f * delta;
                Vector3 next = chunk.Item.position + chunk.Velocity * delta;
                chunk.Item.Rotate(chunk.Spin * delta, Space.World);
                if (Mathf.Abs(next.x) < hw && Mathf.Abs(next.z) < hd && next.y < chunk.Size * .16f && chunk.Velocity.y < 0f)
                {
                    // Came down inside the court.
                    next.y = chunk.Size * .16f;
                    chunk.Landed = true;
                    chunk.Age = 0f;
                }
                else if (next.y < -1.4f)
                {
                    // Went down into the ditch.
                    Rest(chunk);
                    continue;
                }
                chunk.Item.position = next;
            }
        }

        static void Rest(Chunk chunk)
        {
            chunk.Active = false;
            chunk.Item.gameObject.SetActive(false);
        }

        void Break(int index, ColosseumFx fx)
        {
            Gap gap = gaps[index];
            ColosseumArena.WallCut cut = gap.Cut;
            gap.Down = true;
            Open = true;
            arena.DropWall(index);
            gap.Wreck.SetActive(true);
            gap.Cracks.localScale = Vector3.one;
            for (int n = 0; n < 14; n++)
            {
                Chunk chunk = chunks[chunkCursor];
                chunkCursor = (chunkCursor + 1) % chunks.Count;
                chunk.Active = true;
                chunk.Landed = false;
                chunk.Age = 0f;
                chunk.Size = Random.Range(.5f, 1.05f);
                // Mostly outwards, into the ditch; a few come down in the court.
                float outwards = cut.Side * Random.Range(-2.4f, 3.2f), up = Random.Range(3.2f, 7.6f), along = Random.Range(-1.8f, 1.8f);
                chunk.Velocity = cut.End ? new Vector3(along, up, outwards) : new Vector3(outwards, up, along);
                chunk.Spin = Random.insideUnitSphere * 620f;
                chunk.Item.SetPositionAndRotation(At(cut, Random.Range(.05f, .65f), Random.Range(.12f, .75f), Random.Range(cut.From + .2f, cut.To - .2f)), Random.rotation);
                chunk.Item.localScale = Vector3.one * chunk.Size;
                chunk.Item.gameObject.SetActive(true);
            }
            for (int n = 0; n < 4; n++)
                fx.Puff(At(cut, .3f, .35f, Mathf.Lerp(cut.From + .3f, cut.To - .3f, n / 3f)), 4, 3.6f);
            if (Crumbled != null) Crumbled(At(cut, 0f, 0f, cut.Middle), cut.Side);
        }

        // ------------------------------------------------------------------ what a fallen stretch leaves behind

        MeshRenderer Layer(string name, Transform parent, Mesh mesh, int order)
        {
            var face = Kit.SpawnStroke(name, parent, mesh, stroke).GetComponent<MeshRenderer>();
            face.sortingOrder = order;
            return face;
        }

        static Color32 Fade(Color32 color, float share)
        {
            return new Color32(color.r, color.g, color.b, (byte)Mathf.RoundToInt(color.a * share));
        }

        /// <summary>A size or offset given across the wall, up and along it, turned to the wall the stretch is in.</summary>
        static Vector3 Across(ColosseumArena.WallCut cut, float outward, float up, float along)
        {
            return cut.End ? new Vector3(along, up, outward) : new Vector3(outward, up, along);
        }

        Gap BuildGap(ColosseumArena.WallCut cut, int seed)
        {
            var random = new System.Random(seed * 7919 + 17);
            float Next(float from, float to) { return Mathf.Lerp(from, to, (float)random.NextDouble()); }
            Vector3 P(float outward, float up, float where) { return At(cut, outward, up, where); }
            float z0 = cut.From, z1 = cut.To, length = z1 - z0;
            // A chevron and a crack for about every battlement that is gone.
            int marksAlong = Mathf.Max(2, Mathf.RoundToInt(length / 1.04f));
            var gap = new Gap { Cut = cut, Seed = Next(0f, 6f), Wreck = new GameObject("Wall gap") };
            ColosseumArena.Look skin = ColosseumArena.Skin;
            Transform wreck = gap.Wreck.transform;
            wreck.SetParent(root, false);

            // ---- the ditch: the ground the wall stood on has dropped away, from the court out to the stands
            var ditch = new MeshKit { Gradient = 0f };
            const int steps = 12;
            const float ground = .022f;
            float far = ColosseumArena.TierStart + .02f;
            // The edge of the court is broken off unevenly.
            var edge = new float[steps + 1];
            for (int k = 0; k <= steps; k++)
                edge[k] = k == 0 || k == steps ? .02f : k % 2 == 0 ? -Next(.07f, .17f) : Next(-.02f, .06f);
            for (int k = 0; k < steps; k++)
            {
                float a = z0 + length * k / steps, b = z0 + length * (k + 1) / steps;
                ditch.Quad(P(edge[k], ground, a), P(far, ground, a), P(far, ground, b), P(edge[k + 1], ground, b), skin.Pit[7]);
            }
            // From the camera the far end of the ditch shows as a face of earth going down into the dark, with
            // a couple of darker seams across it.
            float[] back = { 0f, .22f, .24f, .28f, .55f, .57f, .61f, 1f };
            Color32[] earth = skin.Pit;
            for (int k = 0; k < back.Length - 1; k++)
            {
                float a = z1 - back[k], b = z1 - back[k + 1];
                ditch.Quad(P(.02f, ground + .002f, a), P(far, ground + .002f, a), P(far, ground + .002f, b), P(.02f, ground + .002f, b),
                    earth[k], earth[k], earth[k + 1], earth[k + 1]);
            }
            // A pale line where the broken edge of the court catches the light.
            Color32 lip = skin.Lip;
            for (int k = 0; k < steps; k++)
            {
                float a = z0 + length * k / steps, b = z0 + length * (k + 1) / steps;
                ditch.Quad(P(edge[k] - .045f, ground + .004f, a), P(edge[k], ground + .004f, a), P(edge[k + 1], ground + .004f, b),
                    P(edge[k + 1] - .045f, ground + .004f, b), lip);
            }
            Layer("Ditch", wreck, ditch.ToMesh("Colosseum quake ditch"), -5);

            // ---- rubble: broken stone left sticking out of the wall at both ends of the gap, and stones in the court
            var rubble = new MeshKit();
            Color32 stone = skin.Stone, pale = skin.Block, shadowed = skin.StoneShade;
            for (int end = 0; end < 2; end++)
            {
                float z = end == 0 ? z0 : z1, into = end == 0 ? 1f : -1f;
                for (int n = 0; n < 4; n++)
                {
                    float reach = Next(.06f, .2f);
                    rubble.Box(P(Next(.14f, .56f), Next(.1f, .5f), z + into * reach * .6f), Across(cut, Next(.2f, .32f), Next(.12f, .2f), reach * 2f),
                        Quaternion.Euler(Next(-12f, 12f), Next(-14f, 14f), Next(-10f, 10f)), n % 2 == 0 ? stone : shadowed, pale);
                }
            }
            for (int n = 0; n < 5; n++)
            {
                float size = Next(.07f, .13f);
                rubble.Blob(P(-Next(.14f, .6f), size * .55f, Next(z0 - .2f, z1 + .2f)), new Vector3(size * 1.25f, size * .75f, size),
                    Quaternion.Euler(0f, Next(0f, 360f), 0f), n % 2 == 0 ? pale : stone, .6f, .6f, 6);
            }
            Kit.Spawn("Rubble", wreck, rubble.ToMesh("Colosseum quake rubble"));

            // ---- warnings on the sand in front of the gap: a red glow at the brink, and chevrons pointing out
            var glow = new MeshKit { Gradient = 0f };
            var hot = new Color32(255, 58, 44, 135);
            var none = new Color32(255, 58, 44, 0);
            float[] spans = { z0 - .45f, z0 + .15f, z1 - .15f, z1 + .45f };
            float[] show = { 0f, 1f, 1f, 0f };
            for (int k = 0; k < 3; k++)
            {
                glow.Quad(P(-.02f, .028f, spans[k]), P(-1.35f, .028f, spans[k]), P(-1.35f, .028f, spans[k + 1]), P(-.02f, .028f, spans[k + 1]),
                    Fade(hot, show[k]), none, none, Fade(hot, show[k + 1]));
            }
            gap.Glow = Layer("Danger glow", wreck, glow.ToMesh("Colosseum quake glow"), -4);

            gap.Arrows = new MeshRenderer[2];
            for (int row = 0; row < 2; row++)
            {
                var marks = new MeshKit { Gradient = 0f };
                float middle = row == 0 ? -1.08f : -.52f;
                const float reach = .3f, rise = .16f;
                // Each chevron is painted twice: a dark one underneath, set back a little, so the red reads.
                for (int layer = 0; layer < 2; layer++)
                {
                    float height = .034f + layer * .003f, thick = layer == 0 ? .25f : .18f, back0 = layer == 0 ? -.055f : 0f;
                    Color32 paint = layer == 0 ? new Color32(112, 12, 18, 235) : new Color32(255, 66, 46, 255);
                    var lift = Across(cut, cut.Side * thick, 0f, 0f);
                    for (int n = 0; n < marksAlong; n++)
                    {
                        float z = z0 + length * (n + .5f) / marksAlong;
                        for (int side = -1; side <= 1; side += 2)
                        {
                            Vector3 foot = P(middle - rise + back0, height, z + side * reach), tip = P(middle + rise + back0, height, z);
                            marks.Quad(foot, foot + lift, tip + lift, tip, paint);
                        }
                    }
                }
                gap.Arrows[row] = Layer("Danger arrows", wreck, marks.ToMesh("Colosseum quake arrows"), -2);
            }

            // ---- cracks running in from the wall. Their object sits on the line of the wall and is scaled along
            // x, so the cracks open from the wall inwards.
            var cracks = new MeshKit { Gradient = 0f };
            var veins = new List<Vein>();
            for (int n = 0; n < marksAlong; n++)
                Grow(veins, random, 0f, (cut.End ? -1f : 1f) * (z0 + length * (n + .5f) / marksAlong + Next(-.15f, .15f)), Next(1.6f, 2.6f), .2f, 0f, true);
            // Every crack is drawn twice: a pale edge set a little further up the arena, where the broken sand
            // catches the light, and the dark of the crack itself on top.
            foreach (Vein vein in veins) Draw(cracks, vein, cut.Side, .045f, 1f, skin.CrackEdge);
            foreach (Vein vein in veins) Draw(cracks, vein, cut.Side, 0f, 1f, skin.CrackDark);
            gap.Cracks = Layer("Quake cracks", root, cracks.ToMesh("Colosseum quake cracks"), -6).transform;
            // On an end wall the cracks are turned a quarter round, so that they still open from the wall inwards.
            if (cut.End) gap.Cracks.SetPositionAndRotation(new Vector3(0f, 0f, cut.Side * hd), Quaternion.Euler(0f, -90f, 0f));
            else gap.Cracks.position = new Vector3(cut.Side * hw, 0f, 0f);
            gap.Cracks.gameObject.SetActive(false);
            gap.Wreck.SetActive(false);
            return gap;
        }

        /// <summary>One crack in the sand: its points, as how far in from the wall and where along it, and its width at each.</summary>
        sealed class Vein
        {
            public readonly List<Vector2> Points = new List<Vector2>();
            public readonly List<float> Wide = new List<float>();
        }

        /// <summary>
        /// Grows a crack from `inward` of the wall at `along`: it runs `length` further in, zigzagging and thinning
        /// to nothing, and may throw off a branch to either side.
        /// </summary>
        static void Grow(List<Vein> veins, System.Random random, float inward, float along, float length, float wide, float lean, bool branches)
        {
            float Next(float from, float to) { return Mathf.Lerp(from, to, (float)random.NextDouble()); }
            var vein = new Vein();
            veins.Add(vein);
            int pieces = Mathf.Max(3, Mathf.RoundToInt(length / .34f));
            float step = length / pieces, z = along;
            vein.Points.Add(new Vector2(inward, z));
            vein.Wide.Add(wide);
            for (int i = 1; i <= pieces; i++)
            {
                z += lean * step + (i % 2 == 0 ? 1f : -1f) * Next(.07f, .26f);
                vein.Points.Add(new Vector2(inward + step * i, z));
                vein.Wide.Add(wide * Mathf.Pow(1f - (float)i / pieces, .7f));
                if (!branches || (i != pieces / 3 && i != pieces * 2 / 3)) continue;
                Grow(veins, random, inward + step * i, z, length * Next(.28f, .42f), vein.Wide[i] * .8f, (i == pieces / 3 ? 1f : -1f) * Next(.7f, 1.1f), false);
            }
        }

        /// <summary>Paints a crack, relative to the line of the wall on the given side, shifted up the arena by `shift`.</summary>
        static void Draw(MeshKit kit, Vein vein, int side, float shift, float swell, Color32 paint)
        {
            const float height = .024f;
            for (int i = 0; i < vein.Points.Count - 1; i++)
            {
                Vector2 a = vein.Points[i], b = vein.Points[i + 1];
                float wa = vein.Wide[i] * swell * .5f, wb = vein.Wide[i + 1] * swell * .5f;
                kit.Quad(new Vector3(-side * a.x, height, a.y + shift - wa), new Vector3(-side * a.x, height, a.y + shift + wa),
                    new Vector3(-side * b.x, height, b.y + shift + wb), new Vector3(-side * b.x, height, b.y + shift - wb), paint);
            }
        }

        static Mesh[] ChunkMeshes()
        {
            ColosseumArena.Look skin = ColosseumArena.Skin;
            Color32 stone = skin.Stone, top = skin.StoneTop, pale = skin.Block, dark = skin.BlockDark;
            var slab = new MeshKit();
            slab.Box(Vector3.zero, new Vector3(.46f, .3f, .36f), Quaternion.identity, stone, top);
            var lump = new MeshKit();
            lump.Blob(Vector3.zero, new Vector3(.22f, .16f, .2f), Quaternion.identity, pale, .5f, .5f, 8);
            var block = new MeshKit();
            block.Box(Vector3.zero, new Vector3(.3f, .26f, .28f), Quaternion.Euler(18f, 30f, 0f), dark, pale);
            return new[] { slab.ToMesh("Colosseum rubble slab"), lump.ToMesh("Colosseum rubble lump"), block.ToMesh("Colosseum rubble block") };
        }
    }
}
