using System.Collections.Generic;
using UnityEngine;

namespace SlingArena.Colosseum
{
    /// <summary>
    /// Ice. A sheet of it creeps out over the sand. On the ice the ground holds nobody: a hero slung across it
    /// keeps nearly all its speed, and one knocked over on it slides on until it is off the ice or up against
    /// something. Standing still is safe enough. A single sheet lies in the very middle of the court, whatever
    /// else is there (it is only ground, and other things can stand on it); more than one come in pairs, one in
    /// each team's half, turned about the middle.
    /// </summary>
    sealed class MapIce : MapFeature
    {
        const int Most = 4, Rim = 32, Twinkles = 10;
        const float SpreadFor = .75f, MeltFor = .6f;
        static readonly Color Frost = new Color(.62f, .9f, 1f);

        sealed class Sheet
        {
            public Transform Root;
            public MeshRenderer Face;
            public Mesh Mesh;
            public Vector3 At;
            public float Radius, Age;
            /// <summary>How far the edge is from the middle in each direction round it, as a share of the radius.</summary>
            public readonly float[] Reach = new float[Rim];
        }

        sealed class Twinkle
        {
            public Transform Item;
            public MeshRenderer Face;
            public float Age, Life, Size;
            public bool Active;
        }

        readonly List<Sheet> sheets = new List<Sheet>();
        readonly List<Twinkle> twinkles = new List<Twinkle>();
        float grip = .15f, clock, melting = -1f, twinkleClock;
        bool middle;   // a sheet has been laid in the very middle

        public override string Key { get { return ColosseumEventTable.Ice; } }
        public override string Description => "Heroes slide farther across the ice.";
        public override bool Busy { get { return sheets.Count > 0 && clock < ColosseumEvents.Herald + sheets.Count * .1f + SpreadFor; } }
        public override Color Theme { get { return Frost; } }
        public override int Mood { get { return 5; } }

        protected override void Build()
        {
            // A glint: a thin four-pointed star.
            var star = new MeshKit { Gradient = 0f };
            var white = new Color32(255, 255, 255, 255);
            var clear = new Color32(255, 255, 255, 0);
            for (int arm = 0; arm < 4; arm++)
            {
                Quaternion turn = Quaternion.Euler(0f, 0f, arm * 90f);
                MapMesh.Sheet(star, Vector3.zero, turn * new Vector3(.09f, .09f, 0f), turn * new Vector3(0f, 1f, 0f), turn * new Vector3(-.09f, .09f, 0f), white, white, clear, white);
            }
            Mesh starMesh = star.ToMesh("Colosseum ice glint");
            for (int i = 0; i < Twinkles; i++)
            {
                var twinkle = new Twinkle { Item = Kit.SpawnStroke("Ice glint", root, starMesh, host.Stroke).transform };
                twinkle.Face = twinkle.Item.GetComponent<MeshRenderer>();
                twinkle.Face.sortingOrder = 1;
                twinkle.Item.rotation = ColosseumEvents.TowardLens;
                twinkle.Item.gameObject.SetActive(false);
                twinkles.Add(twinkle);
            }
        }

        protected override bool Arrive()
        {
            grip = Value("grip");
            float radius = Value("radius");
            int wanted = Mathf.Min(Count("patch_count"), Most);
            // No wider than fits between the side walls.
            radius = Mathf.Min(radius, (ColosseumGame.HalfWidth - .35f) / .96f);
            float keep = radius * .82f;
            middle = false;
            while (sheets.Count < wanted)
            {
                bool paired = wanted - sheets.Count >= 2;
                Vector3 spot = Vector3.zero;
                // One alone lies in the very middle.
                bool found = !paired && !middle;
                if (found) middle = true;
                else found = host.Place(Mathf.Min(keep, ColosseumGame.HalfWidth - 1.2f), Mathf.Max(.2f, ColosseumGame.HalfWidth - radius * .96f - .3f), paired ? radius * .5f : 0f, 6.3f,
                    false, paired, out spot, false, this);
                if (!found) break;
                // Heralded first, then the frost runs out over the sand from the middle of each.
                float wait = ColosseumEvents.Herald + sheets.Count * .1f;
                Lay(spot, radius, wait);
                if (paired) Lay(-spot, radius, wait + .1f);
            }
            clock = 0f;
            melting = -1f;
            return sheets.Count > 0;
        }

        void Lay(Vector3 at, float radius, float wait)
        {
            var sheet = new Sheet { At = at, Radius = radius, Age = -wait };
            // An uneven edge: three slow waves round it, never quite out to the full radius nor far inside it.
            float a = host.Between(0f, 6.28f), b = host.Between(0f, 6.28f), c = host.Between(0f, 6.28f);
            for (int i = 0; i < Rim; i++)
            {
                float angle = i * Mathf.PI * 2f / Rim;
                sheet.Reach[i] = .87f + .07f * Mathf.Sin(angle * 2f + a) + .04f * Mathf.Sin(angle * 3f + b) + .02f * Mathf.Sin(angle * 5f + c);
            }

            var kit = new MeshKit { Gradient = 0f };
            Color32 heart = new Color32(226, 247, 255, 205), body = new Color32(184, 231, 255, 215), edge = new Color32(142, 210, 255, 225);
            Color32 frost = new Color32(255, 255, 255, 235), none = new Color32(255, 255, 255, 0);
            for (int i = 0; i < Rim; i++)
            {
                int next = (i + 1) % Rim;
                float a0 = i * Mathf.PI * 2f / Rim, a1 = (i + 1) * Mathf.PI * 2f / Rim;
                Vector3 d0 = new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)) * (radius * sheet.Reach[i]);
                Vector3 d1 = new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1)) * (radius * sheet.Reach[next]);
                MapMesh.Sheet(kit, Vector3.zero, Vector3.zero, d1 * .55f, d0 * .55f, heart, heart, body, body);
                MapMesh.Sheet(kit, d0 * .55f, d1 * .55f, d1, d0, body, body, edge, edge);
                // Hoar frost along the edge, thinning away onto the sand.
                MapMesh.Sheet(kit, d0 * .93f, d1 * .93f, d1, d0, none, none, frost, frost);
                MapMesh.Sheet(kit, d0, d1, d1 * 1.07f, d0 * 1.07f, frost, frost, none, none);
            }
            // The shine on it: bright streaks, all lying the same way.
            var along = new Vector3(.86f, 0f, .5f);
            Vector3 lift = Vector3.up * .004f;
            for (int i = 0; i < 7; i++)
            {
                Vector2 spot = Random.insideUnitCircle * (radius * .62f);
                float reach = Random.Range(.35f, .95f) * Mathf.Min(1f, radius / 2f);
                MapMesh.Stroke(kit, new Vector3(spot.x, 0f, spot.y) - along * reach * .5f + lift, new Vector3(spot.x, 0f, spot.y) + along * reach * .5f + lift,
                    Random.Range(.05f, .11f), new Color32(255, 255, 255, 150));
            }
            // And cracks: fine dark lines wandering out from somewhere near the middle.
            var crack = new Color32(86, 160, 214, 190);
            for (int i = 0; i < 4; i++)
            {
                Vector3 from = Quaternion.Euler(0f, Random.value * 360f, 0f) * Vector3.forward * (radius * Random.Range(.05f, .3f));
                Vector3 way = Quaternion.Euler(0f, Random.value * 360f, 0f) * Vector3.forward;
                for (int s = 0; s < 4; s++)
                {
                    Vector3 to = from + Quaternion.Euler(0f, Random.Range(-38f, 38f), 0f) * way * (radius * Random.Range(.1f, .2f));
                    if (to.magnitude > radius * .78f) break;
                    MapMesh.Stroke(kit, from + lift * 1.5f, to + lift * 1.5f, .035f, crack);
                    from = to;
                }
            }
            sheet.Mesh = kit.ToMesh("Colosseum ice");
            sheet.Face = Kit.SpawnStroke("Ice sheet", root, sheet.Mesh, host.Stroke).GetComponent<MeshRenderer>();
            sheet.Face.sortingOrder = -9;
            sheet.Root = sheet.Face.transform;
            sheet.Root.position = at + Vector3.up * .018f;
            sheet.Root.localScale = new Vector3(.01f, 1f, .01f);
            sheet.Root.gameObject.SetActive(false);
            host.Foretell(at, radius * .9f, Frost, wait);
            sheets.Add(sheet);
            host.Take(at, radius * .85f, this, true);
        }

        public override void Leave()
        {
            if (sheets.Count > 0 && melting < 0f) melting = 0f;
        }

        public override void Clear()
        {
            foreach (Sheet sheet in sheets)
            {
                Object.Destroy(sheet.Root.gameObject);
                Object.Destroy(sheet.Mesh);
            }
            sheets.Clear();
            foreach (Twinkle twinkle in twinkles)
            {
                twinkle.Active = false;
                twinkle.Item.gameObject.SetActive(false);
            }
            if (host != null)
            {
                foreach (ColosseumDoll doll in host.Dolls)
                {
                    if (doll != null) doll.Grip = 1f;
                }
                host.GiveBack(this);
            }
            melting = -1f;
            Live = false;
        }

        /// <summary>True for a point on the ice.</summary>
        bool On(Sheet sheet, Vector3 point)
        {
            Vector3 gap = Kit.Flat(point - sheet.At);
            float far = gap.magnitude;
            if (far > sheet.Radius) return false;
            if (far < sheet.Radius * .7f) return true;
            float turn = Mathf.Atan2(gap.z, gap.x) / (Mathf.PI * 2f);
            float place = (turn - Mathf.Floor(turn)) * Rim;
            int index = (int)place % Rim;
            return far < sheet.Radius * Mathf.Lerp(sheet.Reach[index], sheet.Reach[(index + 1) % Rim], place - Mathf.Floor(place));
        }

        /// <summary>Its moment has come: the sand freezes over with a crack, out from the middle.</summary>
        void Reveal(Sheet sheet)
        {
            sheet.Root.gameObject.SetActive(true);
            host.Strike(sheet.At, sheet.Radius * .45f, .36f, "freeze", .14f);
            for (int i = 0; i < 6; i++)
            {
                float turn = i * Mathf.PI / 3f;
                host.Fx.Sparks(sheet.At + new Vector3(Mathf.Cos(turn), 0f, Mathf.Sin(turn)) * (sheet.Radius * .5f) + Vector3.up * .3f, 3);
            }
        }

        public override void Step(float delta)
        {
            bool slick = melting < 0f;
            foreach (ColosseumDoll doll in host.Dolls)
            {
                if (doll == null) continue;
                float hold = 1f;
                if (slick && !doll.Lost && doll.Body != null && doll.Body.position.y < .6f)
                {
                    foreach (Sheet sheet in sheets)
                    {
                        // Slippery once the frost has run most of the way out.
                        if (sheet.Age > SpreadFor * .5f && On(sheet, doll.Body.position)) { hold = grip; break; }
                    }
                }
                doll.Grip = hold;
            }
        }

        public override void Tick(float delta, float time)
        {
            clock += delta;
            if (melting >= 0f)
            {
                melting += delta;
                if (melting >= MeltFor) { Clear(); return; }
            }
            float left = melting < 0f ? 1f : 1f - Mathf.SmoothStep(0f, 1f, melting / MeltFor);
            foreach (Sheet sheet in sheets)
            {
                bool unseen = sheet.Age < 0f;
                sheet.Age += delta;
                if (sheet.Age < 0f) continue;
                if (unseen) Reveal(sheet);
                float t = Mathf.Clamp01(sheet.Age / SpreadFor), spread = 1f - Mathf.Pow(1f - t, 3f);
                sheet.Root.localScale = new Vector3(Mathf.Max(.01f, spread), 1f, Mathf.Max(.01f, spread));
                Tint(sheet.Face, new Color(1f, 1f, 1f, left));
            }

            twinkleClock -= delta;
            if (twinkleClock <= 0f && melting < 0f && sheets.Count > 0)
            {
                twinkleClock = Random.Range(.12f, .32f);
                Twinkle spare = twinkles.Find(one => !one.Active);
                Sheet sheet = sheets[Random.Range(0, sheets.Count)];
                // Only on ice that is there to glint.
                if (spare != null && sheet.Age >= 0f)
                {
                    Vector2 spot = Random.insideUnitCircle * (sheet.Radius * .72f * Mathf.Clamp01(sheet.Age / SpreadFor));
                    spare.Active = true;
                    spare.Age = 0f;
                    spare.Life = Random.Range(.4f, .7f);
                    spare.Size = Random.Range(.16f, .3f);
                    spare.Item.position = sheet.At + new Vector3(spot.x, .1f, spot.y);
                    spare.Item.gameObject.SetActive(true);
                }
            }
            foreach (Twinkle twinkle in twinkles)
            {
                if (!twinkle.Active) continue;
                twinkle.Age += delta;
                float t = twinkle.Age / twinkle.Life;
                if (t >= 1f)
                {
                    twinkle.Active = false;
                    twinkle.Item.gameObject.SetActive(false);
                    continue;
                }
                twinkle.Item.localScale = Vector3.one * (twinkle.Size * Mathf.Sin(t * Mathf.PI));
                twinkle.Item.rotation = ColosseumEvents.TowardLens * Quaternion.Euler(0f, 0f, t * 70f);
                Tint(twinkle.Face, new Color(1f, 1f, 1f, Mathf.Sin(t * Mathf.PI) * left));
            }
        }
    }
}
