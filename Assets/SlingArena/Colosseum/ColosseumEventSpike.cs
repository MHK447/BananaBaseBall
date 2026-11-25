using System.Collections.Generic;
using UnityEngine;

namespace SlingArena.Colosseum
{
    /// <summary>
    /// Spike walls. Rows of iron spikes snap out of stretches of the side walls, with a red warning on the sand
    /// under them. Whoever is knocked or slung into a spiked stretch at any speed is hurt, the harder the faster,
    /// and thrown back off it tumbling: so a hero slung into one has thrown its own turn away. The stretches come
    /// in pairs, one in each team's half, turned about the middle of the court, and never where an earthquake is
    /// going to bring the wall down (or has): where that leaves too little wall for a full stretch, a shorter
    /// one is put out, so that spike walls which are due always come.
    /// </summary>
    sealed class MapSpikes : MapFeature
    {
        // How far the spikes stand out of the wall, and how far out a hero is stopped by them.
        const float Long = .64f, Deep = .44f;
        const float OutFor = .32f, InFor = .3f, Sore = .6f;
        // The shortest stretch worth putting out, and how far a stretch keeps from one that an earthquake takes.
        const float Shortest = 1.5f, CutGap = .25f;
        // How high the slab a hero runs into stands (as high as the walls), and how high up the points are felt.
        const float GuardTall = 13f, Reach = 1.7f;
        static readonly Color Red = new Color(1f, .27f, .2f);

        sealed class Run
        {
            public Transform Root;
            public GameObject Guard;
            public MeshRenderer Strip;
            public Mesh Spikes, Warning;
            public Vector3 Home;
            public int Side;
            public float From, To, Flash;
            /// <summary>How long after the event began this stretch springs out, and whether it has.</summary>
            public float Wait, Crackle;
            public bool Out;
        }

        readonly List<Run> runs = new List<Run>();
        readonly Dictionary<ColosseumDoll, float> sore = new Dictionary<ColosseumDoll, float>();   // when each hero can next be hurt by them
        // How often each hero has been thrown back off the spikes this turn. After so many they are only wall to
        // it: with a strong rebound set in the sheet it could otherwise be thrown from one wall to the other for ever.
        readonly Dictionary<ColosseumDoll, int> prickedThisTurn = new Dictionary<ColosseumDoll, int>();
        const int MostPricks = 3;
        float minSpeed = 4f, bounce = .7f;
        int damage = 16;
        float clock, closing = -1f, longest;
        bool mindCuts;

        public override string Key { get { return ColosseumEventTable.Spike; } }
        public override string Description => "Heroes knocked into the spikes take damage.";
        public override bool Busy { get { return runs.Count > 0 && clock < longest + OutFor + .25f; } }
        public override Color Theme { get { return Red; } }
        public override int Mood { get { return 4; } }

        protected override void Build() { }

        protected override bool Arrive()
        {
            float full = Mathf.Min(Value("length"), ColosseumGame.HalfDepth - 2.2f);
            damage = Count("damage");
            minSpeed = Value("min_speed");
            bounce = Value("bounce");
            int wanted = Count("wall_count");
            // Only walls that an earthquake is to bring down in this set (or has) have stretches to keep off.
            mindCuts = host.Planned(ColosseumEventTable.Quake);
            while (runs.Count < wanted)
            {
                bool paired = wanted - runs.Count >= 2, found = false;
                // At full length where there is room for it; shorter where the earthquake's stretches leave less
                // wall than that; and right beside somebody only when nothing else will do.
                for (int pass = 0; pass < 6 && !found; pass++)
                {
                    bool mindHeroes = pass < 3;
                    float span = Mathf.Max(full * (pass % 3 == 0 ? 1f : pass % 3 == 1 ? .75f : .5f), Mathf.Min(full, Shortest));
                    // A stretch is picked in the south team's half; its twin is the same stretch turned about the middle.
                    float low = -ColosseumGame.HalfDepth + 1f, high = Mathf.Max(low, -1f - span);
                    for (int tries = 0; tries < 30 && !found; tries++)
                    {
                        int side = host.Pick(2) == 0 ? -1 : 1;
                        float from = host.Between(low, high), to = from + span;
                        if (!paired && host.Pick(2) == 0)
                        {
                            side = -side;
                            float turned = -to;
                            to = -from;
                            from = turned;
                        }
                        if (!Open(side, from, to, mindHeroes) || (paired && !Open(-side, -to, -from, mindHeroes))) continue;
                        // Heralded first (the wall's foot glows and spits sparks), then out they spring, a pair at a time.
                        float wait = ColosseumEvents.Herald + (runs.Count / 2) * .16f;
                        Raise(side, from, to, wait);
                        if (paired) Raise(-side, -to, -from, wait + .08f);
                        found = true;
                    }
                }
                if (!found) break;
            }
            clock = 0f;
            closing = -1f;
            longest = 0f;
            foreach (Run run in runs) longest = Mathf.Max(longest, run.Wait);
            return runs.Count > 0;
        }

        /// <summary>True if this stretch of wall can take spikes: it will not fall, has none already, and (if that is asked) nobody is standing against it.</summary>
        bool Open(int side, float from, float to, bool mindHeroes)
        {
            if (mindCuts)
            {
                foreach (ColosseumArena.WallCut cut in host.Arena.WallCuts)
                {
                    if (cut.Side == side && from < cut.To + CutGap && cut.From - CutGap < to) return false;
                }
            }
            foreach (Run run in runs)
            {
                if (run.Side == side && from < run.To + .5f && run.From - .5f < to) return false;
            }
            if (!mindHeroes) return true;
            foreach (ColosseumDoll doll in host.Dolls)
            {
                if (doll == null || doll.Lost) continue;
                Vector3 at = doll.transform.position;
                if (Mathf.Abs(side * ColosseumGame.HalfWidth - at.x) < 1.25f && at.z > from - .5f && at.z < to + .5f) return false;
            }
            return true;
        }

        void Raise(int side, float from, float to, float wait)
        {
            float span = to - from, middle = (from + to) * .5f;
            var run = new Run { Side = side, From = from, To = to, Home = new Vector3(side * ColosseumGame.HalfWidth, 0f, middle), Wait = wait };
            // Marked on the sand in front of the wall, like everything else that is on its way.
            host.Foretell(run.Home - Vector3.right * (side * .7f), Mathf.Min(1.5f, span * .42f), Red, wait);

            // A plate of iron lying against the wall, and the spikes standing out of it in two rows.
            var kit = new MeshKit();
            Color32 steel = MeshKit.Rgb(Kit.Steel, .85f), bright = MeshKit.Rgb(0xf3f7ff, .9f), iron = MeshKit.Rgb(0x565b73, .6f), ironTop = MeshKit.Rgb(0x767c98, .6f);
            kit.Box(new Vector3(-side * .04f, .33f, 0f), new Vector3(.08f, .54f, span - .06f), Quaternion.identity, iron, ironTop);
            int gaps = Mathf.Max(2, Mathf.FloorToInt((span - .36f) / .44f));
            float step = (span - .36f) / gaps;
            for (int i = 0; i <= gaps; i++)
            {
                float z = -span * .5f + .18f + i * step;
                kit.Spike(new Vector3(-side * .03f, .2f, z), new Vector3(-side * Long, .2f, z), .16f, steel, bright, 4);
                if (i < gaps)
                    kit.Spike(new Vector3(-side * .03f, .47f, z + step * .5f), new Vector3(-side * Long * .8f, .47f, z + step * .5f), .13f, steel, bright, 4);
            }
            run.Spikes = kit.ToMesh("Colosseum spikes");
            run.Root = Kit.Spawn("Spike run", root, run.Spikes, true).transform;
            run.Root.position = run.Home + Vector3.right * (side * (Long + .1f));

            // On the sand under them: a red wash fading into the court, and teeth along it pointing the same way.
            var mark = new MeshKit { Gradient = 0f };
            var deep = new Color32(255, 255, 255, 185);
            var none = new Color32(255, 255, 255, 0);
            float half = span * .5f;
            MapMesh.Sheet(mark, new Vector3(0f, 0f, -half), new Vector3(0f, 0f, half), new Vector3(-side * 1.35f, 0f, half), new Vector3(-side * 1.35f, 0f, -half),
                deep, deep, none, none);
            int teeth = Mathf.Max(2, Mathf.RoundToInt(span / .7f));
            float pitch = span / teeth;
            for (int i = 0; i < teeth; i++)
            {
                float z = -half + i * pitch;
                mark.Tri(new Vector3(-side * .72f, .002f, z + .09f), new Vector3(-side * .72f, .002f, z + pitch - .09f), new Vector3(-side * 1.16f, .002f, z + pitch * .5f),
                    new Color32(255, 255, 255, 230));
            }
            run.Warning = mark.ToMesh("Colosseum spike warning");
            run.Strip = Kit.SpawnStroke("Spike warning", root, run.Warning, host.Stroke).GetComponent<MeshRenderer>();
            run.Strip.sortingOrder = -8;
            run.Strip.transform.position = run.Home + Vector3.up * .021f;

            // What a hero actually runs into: a slab in front of the wall, as deep as the spikes are felt. It goes
            // all the way up, as the walls do, so that nobody thrown high comes down to lie along the top of it.
            run.Guard = new GameObject("Spike guard");
            run.Guard.transform.SetParent(root, false);
            run.Guard.transform.position = new Vector3(side * (ColosseumGame.HalfWidth - Deep * .5f), GuardTall * .5f, middle);
            var slab = run.Guard.AddComponent<BoxCollider>();
            slab.size = new Vector3(Deep, GuardTall, span - .1f);
            slab.sharedMaterial = host.WallSurface;
            run.Guard.SetActive(false);
            runs.Add(run);
        }

        protected override void OnReveal()
        {
            foreach (Run run in runs)
            {
                float half = (run.To - run.From) * .5f;
                for (int i = 0; i < 3; i++)
                    host.Fx.Puff(run.Home + new Vector3(-run.Side * .2f, .3f, Mathf.Lerp(-half, half, (i + .5f) / 3f)), 3, 2.4f);
            }
        }

        public override void Leave()
        {
            if (runs.Count > 0 && closing < 0f)
            {
                closing = 0f;
                host.Sound.Play("spike-out", .6f);
            }
        }

        public override void Clear()
        {
            foreach (Run run in runs)
            {
                Object.Destroy(run.Root.gameObject);
                Object.Destroy(run.Strip.gameObject);
                Object.Destroy(run.Guard);
                Object.Destroy(run.Spikes);
                Object.Destroy(run.Warning);
            }
            runs.Clear();
            sore.Clear();
            prickedThisTurn.Clear();
            closing = -1f;
            Live = false;
        }

        public override void TurnBegins() { prickedThisTurn.Clear(); }

        public override void Tick(float delta, float time)
        {
            clock += delta;
            if (closing >= 0f)
            {
                closing += delta;
                if (closing >= InFor) { Clear(); return; }
            }
            float inward = closing < 0f ? 1f : 1f - Mathf.SmoothStep(0f, 1f, closing / InFor);
            foreach (Run run in runs)
            {
                run.Flash = Mathf.MoveTowards(run.Flash, 0f, delta * 3f);
                float since = clock - run.Wait;
                if (since < 0f && closing < 0f)
                {
                    // Not yet: the sand at the wall's foot glows redder and redder, beating, and sparks jump along it.
                    float near = Mathf.Clamp01(clock / run.Wait);
                    Color glow = Red;
                    glow.a = near * (.3f + .5f * (.5f + .5f * Mathf.Sin(clock * (22f + 30f * near))));
                    Tint(run.Strip, glow);
                    run.Crackle -= delta;
                    if (run.Crackle <= 0f)
                    {
                        run.Crackle = .07f;
                        host.Fx.Sparks(run.Home + new Vector3(-run.Side * .12f, .3f, Random.Range(run.From, run.To) - (run.From + run.To) * .5f), 2);
                    }
                    continue;
                }
                if (!run.Out && closing < 0f) Spring(run);
                // Out with a snap that runs a little past and settles; back in smoothly.
                float t = Mathf.Clamp01(since / OutFor);
                float shown = Mathf.Min(since < 0f ? 0f : 1f - Mathf.Pow(1f - t, 3f) + Mathf.Sin(t * Mathf.PI) * .12f, inward);
                bool solid = closing < 0f && t > .7f;
                run.Root.position = run.Home + Vector3.right * (run.Side * (Long + .1f) * (1f - shown));
                if (run.Guard.activeSelf != solid) run.Guard.SetActive(solid);
                Color tint = Color.Lerp(Red, Color.white, run.Flash * .7f);
                tint.a = Mathf.Clamp01((.62f + .2f * Mathf.Sin(time * 5f + run.From) + run.Flash) * Mathf.Clamp01(shown));
                Tint(run.Strip, tint);
            }
        }

        /// <summary>Its moment has come: the spikes spring out of the wall, striking sparks all along it.</summary>
        void Spring(Run run)
        {
            run.Out = true;
            run.Flash = 1f;
            float span = run.To - run.From;
            host.Strike(run.Home - Vector3.right * (run.Side * .45f), span * .3f, .32f, "spike-out");
            for (int i = -1; i <= 1; i++)
                host.Fx.Sparks(run.Home + new Vector3(-run.Side * .35f, .4f, i * span * .34f), 4);
        }

        public override bool Struck(ColosseumDoll doll, Vector3 point, Vector3 normal, Vector3 relative)
        {
            if (closing >= 0f || Mathf.Abs(normal.x) < .5f) return false;
            foreach (Run run in runs)
            {
                if (!run.Guard.activeSelf || (point.x > 0f ? 1 : -1) != run.Side) continue;
                if (Mathf.Abs(point.x) < ColosseumGame.HalfWidth - Deep - .3f || point.z < run.From - .15f || point.z > run.To + .15f) continue;
                // Over the points there is only wall.
                if (point.y > Reach) return false;
                Vector3 before = doll.PreVelocity;
                float speed = Mathf.Max(Mathf.Abs(before.x), Mathf.Abs(relative.x));
                // Leaning on the points does nothing: it takes a real knock.
                if (speed < minSpeed) return false;
                float now = Time.time;
                if (sore.TryGetValue(doll, out float until) && now < until) return true;
                prickedThisTurn.TryGetValue(doll, out int pricks);
                if (pricks >= MostPricks) return false;
                prickedThisTurn[doll] = pricks + 1;
                sore[doll] = now + Sore;
                doll.Squash(.7f);
                float most = ColosseumEvents.TopSpeed;
                doll.Knock(new Vector3(-run.Side * Mathf.Clamp(Mathf.Abs(before.x) * bounce, 5f, most), 5.2f, Mathf.Clamp(before.z * .6f, -most, most)));
                host.Hurt(doll, Mathf.RoundToInt(damage * Mathf.Clamp(speed / 14f, .5f, 1f)), "SPIKED", Red);
                point.y = Mathf.Max(point.y, .5f);
                host.Fx.Impact(point, .62f);
                host.Fx.Sparks(point, 10);
                host.Fx.Puff(point, 5, 3f);
                host.Sound.Play("spike");
                host.Shake(.24f, .22f);
                host.Thrill(.45f);
                run.Flash = 1f;
                return true;
            }
            return false;
        }
    }
}
