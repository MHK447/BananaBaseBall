using System.Collections.Generic;
using UnityEngine;

namespace SlingArena.Colosseum
{
    /// <summary>
    /// High tide, on the Tidal Bastion only. The sea comes in over the court and stands knee-deep on the whole of
    /// it for a few rounds: deep enough to be seen at a glance, sea-blue over the pale flags, with rollers going
    /// down the court, foam round everyone's legs and spray off anyone who wades or lands in it. Wading takes it
    /// out of everyone: while the water is up, a sling has a tenth less force than the same pull has on dry flags
    /// (the sheet says how much), for both sides alike. Nothing else changes, and when its rounds are up the water
    /// runs off again.
    /// </summary>
    sealed class MapTide : MapFeature
    {
        const int Crests = 48, Rings = 16, Collars = 12, Rollers = 5, Drops = 44;
        const float RiseFor = 1.5f, EbbFor = 1.1f, Depth = .34f, Fall = 13f;
        // How far through the rise the two fronts meet in the middle: the water goes on coming up after that.
        const float Meet = .6f;
        static readonly Color Sea = new Color(.32f, .76f, 1f);

        sealed class Ring
        {
            public Transform Item;
            public MeshRenderer Face;
            public float Age, Life, Size;
            public bool Active;
        }

        /// <summary>A drop of spray: thrown up, and gone when it falls back into the water.</summary>
        sealed class Drop
        {
            public Transform Item;
            public MeshRenderer Face;
            public Vector3 Velocity;
            public float Age, Life, Size;
            public bool Active;
        }

        Transform sheet;
        MeshRenderer water, foam;
        readonly Transform[] swell = new Transform[2], surge = new Transform[2], flood = new Transform[2], roller = new Transform[Rollers];
        readonly MeshRenderer[] swellFace = new MeshRenderer[2], surgeFace = new MeshRenderer[2], floodFace = new MeshRenderer[2], rollerFace = new MeshRenderer[Rollers];
        readonly List<Ring> rings = new List<Ring>(), collars = new List<Ring>();
        readonly List<Drop> drops = new List<Drop>();
        readonly Dictionary<ColosseumDoll, float> wake = new Dictionary<ColosseumDoll, float>();   // when each hero next sends out a ripple
        readonly Dictionary<ColosseumDoll, bool> wet = new Dictionary<ColosseumDoll, bool>();      // who was in the water a moment ago
        float scale = .9f, clock, ebbing = -1f, level, top, sprayDue;
        float dry;      // while it is coming in: how far either side of the half-way line is still dry
        int ringCursor, dropCursor;
        bool met, told;

        public override string Key { get { return ColosseumEventTable.Tide; } }
        public override string Description
        {
            get { return "The court floods. Slings lose " + Mathf.RoundToInt((1f - scale) * 100f) + "% of their power."; }
        }
        public override bool Busy { get { return Live && ebbing < 0f && clock < ColosseumEvents.Herald + RiseFor; } }
        public override Color Theme { get { return Sea; } }
        public override int Mood { get { return 5; } }

        MeshRenderer Layer(string name, Transform parent, Mesh mesh, int order)
        {
            var face = Kit.SpawnStroke(name, parent, mesh, host.Stroke).GetComponent<MeshRenderer>();
            face.sortingOrder = order;
            return face;
        }

        protected override void Build()
        {
            float hw = ColosseumGame.HalfWidth, hd = ColosseumGame.HalfDepth;
            sheet = new GameObject("Tide").transform;
            sheet.SetParent(root, false);

            // The water: sea-blue over the pale flags, so that nobody takes a flooded court for a dry one. The flags
            // show through it dimly; out towards the walls it is deeper and darker.
            var kit = new MeshKit { Gradient = 0f };
            Color32 heart = new Color32(38, 140, 236, 158), deep = new Color32(14, 88, 204, 194);
            const float shelf = 1.7f;
            Vector3 P(float x, float z) { return new Vector3(x, 0f, z); }
            float ix = hw - shelf, iz = hd - shelf;
            MapMesh.Sheet(kit, P(-ix, -iz), P(-ix, iz), P(ix, iz), P(ix, -iz), heart);
            MapMesh.Sheet(kit, P(-hw, -hd), P(-hw, hd), P(-ix, iz), P(-ix, -iz), deep, deep, heart, heart);
            MapMesh.Sheet(kit, P(hw, hd), P(hw, -hd), P(ix, -iz), P(ix, iz), deep, deep, heart, heart);
            MapMesh.Sheet(kit, P(-hw, hd), P(hw, hd), P(ix, iz), P(-ix, iz), deep, deep, heart, heart);
            MapMesh.Sheet(kit, P(hw, -hd), P(-hw, -hd), P(-ix, -iz), P(ix, -iz), deep, deep, heart, heart);
            water = Layer("Tide water", sheet, kit.ToMesh("Colosseum tide water"), -8);

            // Foam where it laps against the walls.
            kit = new MeshKit { Gradient = 0f };
            Color32 white = new Color32(255, 255, 255, 215), none = new Color32(255, 255, 255, 0);
            const float lap = .5f;
            MapMesh.Sheet(kit, P(-hw, -hd), P(-hw, hd), P(-hw + lap, hd - lap), P(-hw + lap, -hd + lap), white, white, none, none);
            MapMesh.Sheet(kit, P(hw, hd), P(hw, -hd), P(hw - lap, -hd + lap), P(hw - lap, hd - lap), white, white, none, none);
            MapMesh.Sheet(kit, P(-hw, hd), P(hw, hd), P(hw - lap, hd - lap), P(-hw + lap, hd - lap), white, white, none, none);
            MapMesh.Sheet(kit, P(hw, -hd), P(-hw, -hd), P(-hw + lap, -hd + lap), P(hw - lap, -hd + lap), white, white, none, none);
            foam = Layer("Tide foam", sheet, kit.ToMesh("Colosseum tide foam"), -7);
            foam.transform.localPosition = new Vector3(0f, .002f, 0f);

            // The swell: two sheets of pale crests that drift against each other, so the water never lies still.
            var crests = new[] { new MeshKit { Gradient = 0f }, new MeshKit { Gradient = 0f } };
            const int across = 4;
            int down = Crests / across;
            for (int i = 0; i < Crests; i++)
            {
                int column = i % across, row = i / across;
                var at = new Vector3(-hw + (column + .5f + Random.Range(-.3f, .3f)) * (hw * 2f / across), 0f,
                    -hd + (row + .5f + Random.Range(-.3f, .3f)) * (hd * 2f / down));
                at.x = Mathf.Clamp(at.x, -hw + 1.1f, hw - 1.1f);
                at.z = Mathf.Clamp(at.z, -hd + .5f, hd - .5f);
                float reach = Random.Range(.42f, .78f), bow = Random.Range(.06f, .12f);
                MeshKit layer = crests[(column + row) % 2];
                // A shallow bow of four strokes, thin at its ends.
                Vector3 before = at + new Vector3(-reach, 0f, 0f);
                for (int s = 1; s <= 4; s++)
                {
                    float t = s / 4f;
                    Vector3 here = at + new Vector3(Mathf.Lerp(-reach, reach, t), 0f, Mathf.Sin(t * Mathf.PI) * bow);
                    MapMesh.Stroke(layer, before, here, s == 1 || s == 4 ? .045f : .08f, new Color32(255, 255, 255, (byte)(s == 1 || s == 4 ? 120 : 190)));
                    before = here;
                }
            }
            for (int k = 0; k < 2; k++)
            {
                swellFace[k] = Layer("Tide swell", sheet, crests[k].ToMesh("Colosseum tide swell"), -7);
                swell[k] = swellFace[k].transform;
            }

            // Rollers: long waves going down the court one behind another, each a wavy line of foam with a pale
            // wash trailing behind it. It is these, more than anything, that say the water is moving.
            kit = new MeshKit { Gradient = 0f };
            Color32 wash = new Color32(214, 240, 255, 66), clear = new Color32(214, 240, 255, 0);
            const int bends = 18;
            float span = hw - .3f;
            var trail = new Vector3(0f, 0f, .85f);
            Vector3 Bend(int i)
            {
                float x = Mathf.Lerp(-span, span, i / (float)bends);
                return new Vector3(x, 0f, Mathf.Sin(x * 1.5f) * .13f);
            }
            for (int i = 0; i < bends; i++)
            {
                Vector3 a = Bend(i), b = Bend(i + 1);
                bool end = i == 0 || i == bends - 1;
                MapMesh.Sheet(kit, a, b, b + trail, a + trail, wash, wash, clear, clear);
                MapMesh.Stroke(kit, a + Vector3.up * .002f, b + Vector3.up * .002f, end ? .05f : .08f, new Color32(255, 255, 255, (byte)(end ? 90 : 180)));
            }
            Mesh rollerMesh = kit.ToMesh("Colosseum tide roller");
            for (int i = 0; i < Rollers; i++)
            {
                rollerFace[i] = Layer("Tide roller", sheet, rollerMesh, -7);
                roller[i] = rollerFace[i].transform;
                // Every other one the other way round, so that no two in a row are alike.
                roller[i].localScale = new Vector3(i % 2 == 0 ? 1f : -1f, 1f, 1f);
            }

            // The front of the water as it comes in from either end: a bar of foam, bright at its leading edge.
            kit = new MeshKit { Gradient = 0f };
            Color32 crest = new Color32(255, 255, 255, 230), tail = new Color32(190, 232, 255, 0);
            MapMesh.Sheet(kit, P(-hw, 0f), P(hw, 0f), P(hw, -.26f), P(-hw, -.26f), crest, crest, crest, crest);
            MapMesh.Sheet(kit, P(-hw, -.26f), P(hw, -.26f), P(hw, -2.2f), P(-hw, -2.2f), crest, crest, tail, tail);
            MapMesh.Sheet(kit, P(-hw, 0f), P(hw, 0f), P(hw, .16f), P(-hw, .16f), crest, crest, tail, tail);
            Mesh bar = kit.ToMesh("Colosseum tide surge");
            for (int k = 0; k < 2; k++)
            {
                surgeFace[k] = Layer("Tide surge", root, bar, -6);
                surge[k] = surgeFace[k].transform;
                surge[k].localRotation = Quaternion.Euler(0f, k == 0 ? 0f : 180f, 0f);
            }

            // The water behind each front while it is on its way in: one unit long, and stretched from its end wall
            // as far as the front has got, so that the middle of the court stays dry until the sea reaches it.
            kit = new MeshKit { Gradient = 0f };
            MapMesh.Sheet(kit, P(-hw, 0f), P(-ix, 0f), P(-ix, 1f), P(-hw, 1f), deep, heart, heart, deep);
            MapMesh.Sheet(kit, P(-ix, 0f), P(ix, 0f), P(ix, 1f), P(-ix, 1f), heart);
            MapMesh.Sheet(kit, P(ix, 0f), P(hw, 0f), P(hw, 1f), P(ix, 1f), heart, deep, deep, heart);
            Mesh floodMesh = kit.ToMesh("Colosseum tide flood");
            for (int k = 0; k < 2; k++)
            {
                floodFace[k] = Layer("Tide flood", root, floodMesh, -8);
                flood[k] = floodFace[k].transform;
                flood[k].localRotation = Quaternion.Euler(0f, k == 0 ? 0f : 180f, 0f);
            }

            // Ripples going out from whoever stands or wades in it.
            kit = new MeshKit { Gradient = 0f };
            MapMesh.Band(kit, Vector3.zero, .8f, .93f, none, new Color32(255, 255, 255, 170), 28);
            MapMesh.Band(kit, Vector3.zero, .93f, 1f, new Color32(255, 255, 255, 170), none, 28);
            Mesh ripple = kit.ToMesh("Colosseum tide ripple");
            for (int i = 0; i < Rings; i++)
            {
                var ring = new Ring { Face = Layer("Tide ripple", root, ripple, -6) };
                ring.Item = ring.Face.transform;
                ring.Item.gameObject.SetActive(false);
                rings.Add(ring);
            }

            // The line of foam where the water closes round each hero's legs: it is this that shows how deep they stand.
            kit = new MeshKit { Gradient = 0f };
            Color32 lather = new Color32(255, 255, 255, 225);
            MapMesh.Band(kit, Vector3.zero, .7f, .86f, none, lather, 28);
            MapMesh.Band(kit, Vector3.zero, .86f, .94f, lather, lather, 28);
            MapMesh.Band(kit, Vector3.zero, .94f, 1.12f, lather, none, 28);
            Mesh collarMesh = kit.ToMesh("Colosseum tide collar");
            for (int i = 0; i < Collars; i++)
            {
                var collar = new Ring { Face = Layer("Tide collar", root, collarMesh, -5) };
                collar.Item = collar.Face.transform;
                collar.Item.gameObject.SetActive(false);
                collars.Add(collar);
            }

            // Spray.
            kit = new MeshKit { Gradient = 0f };
            kit.Ball(Vector3.zero, 1f, new Color32(236, 248, 255, 240), 6);
            Mesh dropMesh = kit.ToMesh("Colosseum tide drop");
            for (int i = 0; i < Drops; i++)
            {
                var drop = new Drop { Face = Layer("Tide spray", root, dropMesh, 5) };
                drop.Item = drop.Face.transform;
                drop.Item.gameObject.SetActive(false);
                drops.Add(drop);
            }
        }

        protected override bool Arrive()
        {
            scale = Mathf.Clamp(Value("sling_scale"), .5f, 1f);
            clock = 0f;
            ebbing = -1f;
            level = 0f;
            top = .016f;
            sprayDue = 0f;
            dry = ColosseumGame.HalfDepth;
            met = told = false;
            Show(0f, 0f, 0f, 0f);
            // Marked in the middle of the court, the way everything that is coming is marked.
            host.Foretell(Vector3.zero, 2.6f, Sea, ColosseumEvents.Herald);
            return true;
        }

        protected override void OnReveal()
        {
            host.Sound.Play("tide");
            host.Shake(.12f);
        }

        public override void Leave()
        {
            if (!Live || ebbing >= 0f) return;
            ebbing = 0f;
            host.Sound.Play("tide", .5f);
        }

        public override void Clear()
        {
            foreach (Ring ring in rings)
            {
                ring.Active = false;
                ring.Item.gameObject.SetActive(false);
            }
            foreach (Ring collar in collars) collar.Item.gameObject.SetActive(false);
            foreach (Drop drop in drops)
            {
                drop.Active = false;
                drop.Item.gameObject.SetActive(false);
            }
            wake.Clear();
            wet.Clear();
            ebbing = -1f;
            level = 0f;
            dry = 0f;
            Live = false;
            if (host != null) host.SlingScale = 1f;
            // Nothing of it is left lying on the court.
            if (root != null) root.gameObject.SetActive(false);
        }

        /// <summary>
        /// The water this deep (0 dry, 1 full) and the whole sheet of it this plain to see; the bars of foam this far on
        /// their way in (0 at the end walls, 1 met in the middle), with the water behind them this plain to see.
        /// </summary>
        void Show(float deep, float shown, float run, float flooding)
        {
            float time = Time.time, hd = ColosseumGame.HalfDepth;
            // It rises and falls a little all the time, the way the sea breathes.
            float lift = .012f + Depth * deep + Mathf.Sin(time * 1.7f) * .014f * deep;
            top = lift + .004f;
            sheet.localPosition = new Vector3(0f, lift, 0f);
            Tint(water, new Color(1f, 1f, 1f, shown));
            Tint(foam, new Color(1f, 1f, 1f, shown * (.72f + .28f * Mathf.Sin(time * 2.3f))));
            bool running = run > 0f && run < 1f, flooded = flooding > 0f && run > 0f;
            for (int k = 0; k < 2; k++)
            {
                float side = k == 0 ? 1f : -1f;
                swell[k].localPosition = new Vector3(Mathf.Sin(time * .6f + k * 2.1f) * .45f * side, .003f, Mathf.Sin(time * .85f + k * 1.3f) * .16f);
                Tint(swellFace[k], new Color(1f, 1f, 1f, shown * (.55f + .45f * Mathf.Sin(time * 1.25f + k * Mathf.PI))));
                // The front runs in from each end wall, bright all the way, and they meet on the half-way line.
                if (surge[k].gameObject.activeSelf != running) surge[k].gameObject.SetActive(running);
                if (running)
                {
                    surge[k].localPosition = new Vector3(0f, lift + .008f, side * -hd * (1f - run));
                    Tint(surgeFace[k], new Color(1f, 1f, 1f, Mathf.Clamp01(run / .1f) * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.93f, 1f, run)))));
                }
                if (flood[k].gameObject.activeSelf != flooded) flood[k].gameObject.SetActive(flooded);
                if (!flooded) continue;
                flood[k].localPosition = new Vector3(0f, lift, side * -hd);
                flood[k].localScale = new Vector3(1f, 1f, Mathf.Max(.01f, hd * run));
                Tint(floodFace[k], new Color(1f, 1f, 1f, flooding));
            }
            // The rollers go down the court from the far end to the near one, and come round again.
            float length = hd * 2f - 1.6f;
            for (int i = 0; i < Rollers; i++)
            {
                float gone = Mathf.Repeat(time * (.62f + .11f * (i % 3)) + length * i / Rollers, length);
                roller[i].localPosition = new Vector3(0f, .004f, hd - 1.2f - gone);
                float seen = Mathf.Clamp01(Mathf.Min(gone, length - gone) / 1.5f);
                Tint(rollerFace[i], new Color(1f, 1f, 1f, shown * seen * (.8f + .2f * Mathf.Sin(time * 2.1f + i * 1.9f))));
            }
        }

        /// <summary>True for a hero who is standing, wading or lying in the water: not up in the air over it, and not where it has yet to come.</summary>
        bool InWater(ColosseumDoll doll)
        {
            if (!ColosseumEvents.InPlay(doll)) return false;
            Vector3 at = doll.Body.position;
            return at.y - ColosseumDoll.Radius * doll.Bulk < top && Mathf.Abs(at.z) >= dry;
        }

        public override void Tick(float delta, float time)
        {
            clock += delta;
            float run = 0f, hw = ColosseumGame.HalfWidth;
            if (ebbing >= 0f)
            {
                ebbing += delta;
                if (ebbing >= EbbFor) { Clear(); return; }
                level = 1f - Mathf.SmoothStep(0f, 1f, ebbing / EbbFor);
                dry = 0f;
                Show(level, level, 0f, 0f);
            }
            else
            {
                float t = Mathf.Clamp01((clock - ColosseumEvents.Herald) / RiseFor);
                level = Mathf.SmoothStep(0f, 1f, t);
                // The fronts are across the court a little past half-way through the rise. Until they meet there is
                // water only behind them; once they have, the one sheet of it lies over the whole court.
                float along = Mathf.Clamp01(t / Meet);
                run = clock < ColosseumEvents.Herald ? 0f : 1f - (1f - along) * (1f - along);
                float whole = Mathf.Clamp01((t - Meet) / .14f);
                dry = ColosseumGame.HalfDepth * (1f - run);
                Show(level, whole, run, 1f - whole);
                if (t > 0f && t < 1f) host.Shake(.03f + .05f * Mathf.Sin(t * Mathf.PI));
                if (run > 0f && run < 1f)
                {
                    // Spray off the two fronts as they come in.
                    sprayDue -= delta;
                    if (sprayDue <= 0f)
                    {
                        sprayDue = .03f;
                        Spray(new Vector3(Random.Range(-hw + .4f, hw - .4f), top, -dry), 1, 2.8f, new Vector3(0f, 0f, 2.2f));
                        Spray(new Vector3(Random.Range(-hw + .4f, hw - .4f), top, dry), 1, 2.8f, new Vector3(0f, 0f, -2.2f));
                    }
                }
                if (run >= 1f && !met)
                {
                    met = true;
                    // The two fronts meet on the half-way line, and throw up a wall of spray along the whole of it.
                    for (int i = 0; i < 14; i++)
                        Spray(new Vector3(Mathf.Lerp(-hw + .5f, hw - .5f, (i + Random.value) / 14f), top, Random.Range(-.2f, .2f)), 1, 4f, Vector3.zero);
                    for (int i = 0; i < 4; i++) Ripple(new Vector3(Mathf.Lerp(-hw + 1.4f, hw - 1.4f, i / 3f), 0f, 0f), 1f, .9f);
                    host.Shake(.16f);
                }
                if (t >= 1f && !told)
                {
                    told = true;
                    host.Hud.Pop(new Vector3(0f, .4f, 1.4f), "SLING -" + Mathf.RoundToInt((1f - scale) * 100f) + "%", Sea, 1.2f);
                }
            }
            // Once it is most of the way up, it is in everyone's way.
            host.SlingScale = level > .6f ? scale : 1f;

            int used = 0;
            foreach (ColosseumDoll doll in host.Dolls)
            {
                if (doll == null) continue;
                bool inside = InWater(doll);
                wet.TryGetValue(doll, out bool was);
                wet[doll] = inside;
                if (!inside) continue;
                Vector3 at = doll.Body.position;
                float girth = ColosseumDoll.Radius * doll.Bulk, speed = Kit.Flat(doll.Body.linearVelocity).magnitude;
                if (!was && (dry > 0f || level > .5f))
                {
                    // The front has just reached it, or it has come back down into the water out of the air: a splash,
                    // and a ring going out from where it stands.
                    Spray(new Vector3(at.x, top, at.z), dry > 0f ? 5 : 7, dry > 0f ? 3f : 3.6f, Kit.Flat(doll.Body.linearVelocity) * .12f);
                    Ripple(at, girth * 1.7f, .7f);
                }
                if (level > .06f && used < collars.Count)
                {
                    // The foam round its legs, stirred up the faster it goes.
                    Ring collar = collars[used++];
                    if (!collar.Item.gameObject.activeSelf) collar.Item.gameObject.SetActive(true);
                    float wide = girth * (1.16f + .06f * Mathf.Sin(time * 3.1f + used * 1.7f) + Mathf.Min(.2f, speed * .02f));
                    collar.Item.position = new Vector3(at.x, top + .003f, at.z);
                    collar.Item.localScale = new Vector3(wide, 1f, wide);
                    Tint(collar.Face, new Color(1f, 1f, 1f, Mathf.Clamp01((level - .06f) / .2f) * (.82f + .18f * Mathf.Sin(time * 4.3f + used))));
                }
                if (level <= .3f) continue;
                wake.TryGetValue(doll, out float due);
                due -= delta;
                if (due <= 0f)
                {
                    // Slow rings round someone standing; a quick string of them, and spray, behind someone on the move.
                    bool wading = speed > 2.5f;
                    due = wading ? .09f : Random.Range(.75f, 1.15f);
                    Ripple(at, girth * (wading ? 1.15f : 1.45f), wading ? .45f : 1f);
                    if (speed > 6f) Spray(new Vector3(at.x, top, at.z), speed > 14f ? 2 : 1, 2.4f, Kit.Flat(doll.Body.linearVelocity) * -.07f);
                }
                wake[doll] = due;
            }
            for (int i = used; i < collars.Count; i++)
            {
                if (collars[i].Item.gameObject.activeSelf) collars[i].Item.gameObject.SetActive(false);
            }

            foreach (Ring ring in rings)
            {
                if (!ring.Active) continue;
                ring.Age += delta;
                float t = ring.Age / ring.Life;
                if (t >= 1f)
                {
                    ring.Active = false;
                    ring.Item.gameObject.SetActive(false);
                    continue;
                }
                float wide = ring.Size * (1f + 1.1f * (1f - (1f - t) * (1f - t)));
                ring.Item.position = new Vector3(ring.Item.position.x, top + .002f, ring.Item.position.z);
                ring.Item.localScale = new Vector3(wide, 1f, wide);
                Tint(ring.Face, new Color(1f, 1f, 1f, (1f - t) * .85f * level));
            }
            foreach (Drop drop in drops)
            {
                if (!drop.Active) continue;
                drop.Age += delta;
                drop.Velocity += Vector3.down * (Fall * delta);
                Vector3 at = drop.Item.position + drop.Velocity * delta;
                float t = drop.Age / drop.Life;
                if (t >= 1f || (drop.Velocity.y < 0f && at.y < top - .03f))
                {
                    drop.Active = false;
                    drop.Item.gameObject.SetActive(false);
                    continue;
                }
                drop.Item.position = at;
                drop.Item.localScale = Vector3.one * (drop.Size * (1f - .45f * t));
                Tint(drop.Face, new Color(1f, 1f, 1f, 1f - t * t));
            }
        }

        /// <summary>A ring going out over the water from here.</summary>
        void Ripple(Vector3 at, float size, float life)
        {
            Ring ring = rings[ringCursor];
            ringCursor = (ringCursor + 1) % rings.Count;
            ring.Active = true;
            ring.Age = 0f;
            ring.Life = life;
            ring.Size = size;
            ring.Item.position = new Vector3(at.x, top + .002f, at.z);
            ring.Item.localScale = new Vector3(size, 1f, size);
            ring.Item.gameObject.SetActive(true);
        }

        /// <summary>Drops thrown up from here, this hard, and carried a little this way.</summary>
        void Spray(Vector3 at, int count, float speed, Vector3 carried)
        {
            for (int i = 0; i < count; i++)
            {
                Drop drop = drops[dropCursor];
                dropCursor = (dropCursor + 1) % drops.Count;
                Vector2 wide = Random.insideUnitCircle * (speed * .45f);
                drop.Active = true;
                drop.Age = 0f;
                drop.Life = Random.Range(.42f, .7f);
                drop.Size = Random.Range(.05f, .095f);
                drop.Velocity = new Vector3(wide.x, Random.Range(.7f, 1f) * speed, wide.y) + carried;
                drop.Item.position = at + new Vector3(wide.x, 0f, wide.y) * .06f;
                drop.Item.localScale = Vector3.one * drop.Size;
                drop.Item.gameObject.SetActive(true);
                Tint(drop.Face, Color.white);
            }
        }
    }
}
