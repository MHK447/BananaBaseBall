using System.Collections.Generic;
using UnityEngine;

namespace SlingArena.Colosseum
{
    /// <summary>
    /// Twin portals. They come in pairs, one in each team's half, at the same spot turned about the middle of
    /// the court. A hero that slides or tumbles into one at any speed worth the name comes out of its twin at
    /// once, as fast as it went in and going the same way. Having come out, it has to leave that portal before
    /// it can go through one again. Heroes in the air are not taken, nor heroes being carried.
    /// </summary>
    sealed class MapPortals : MapFeature
    {
        const int Most = 4;
        const float OpenFor = .7f, CloseFor = .4f;
        static readonly int[] PairHex = { 0xb46bff, 0x30e6c8 };

        sealed class Portal
        {
            public Transform Root, Arms, Counter;
            public GameObject[] Rims;
            public MeshRenderer[] Pools;
            public MeshRenderer Glow, Beam, Flare;
            public Vector3 At;
            public int Pair;
            public float Age, Flash, Seed;
            public bool Up;
        }

        readonly List<Portal> portals = new List<Portal>();
        readonly Dictionary<ColosseumDoll, int> mouth = new Dictionary<ColosseumDoll, int>();   // the portal a hero has just come out of, and is still standing in
        readonly Dictionary<ColosseumDoll, float> ready = new Dictionary<ColosseumDoll, float>(); // when a hero may next go through one
        // How often each hero has gone through this turn. After so many the pools are closed to it: with the sheet
        // set to send heroes out faster than they went in, one could otherwise go round and round for ever.
        readonly Dictionary<ColosseumDoll, int> passedThisTurn = new Dictionary<ColosseumDoll, int>();
        const int MostPasses = 6;
        float radius = .85f, exitScale = 1f, minSpeed = 2.5f, cooldown = .5f;
        int count;
        float clock, closing = -1f;

        public override string Key { get { return ColosseumEventTable.Portal; } }
        public override string Description => "Enter one portal to exit through its twin.";
        public override bool Busy { get { return count > 0 && clock < ColosseumEvents.Herald + .35f + OpenFor; } }
        public override Color Theme { get { return MapMesh.Tone(PairHex[0], 255); } }
        public override int Mood { get { return 5; } }

        protected override void Build()
        {
            // The pool of light: bright in the middle, the pair's own colour at the edge. One for each pair.
            var pools = new Mesh[PairHex.Length];
            var rims = new Mesh[PairHex.Length];
            for (int pair = 0; pair < PairHex.Length; pair++)
            {
                int hex = PairHex[pair];
                Color32 edge = MapMesh.Tone(hex, 235);
                var bright = MeshKit.Mix(edge, new Color32(255, 255, 255, 255), .8f);
                bright.a = 245;
                var pool = new MeshKit { Gradient = 0f };
                MapMesh.Band(pool, Vector3.zero, 0f, .5f, bright, MeshKit.Mix(bright, edge, .5f), 30);
                MapMesh.Band(pool, Vector3.zero, .5f, .98f, MeshKit.Mix(bright, edge, .5f), edge, 30);
                pools[pair] = pool.ToMesh("Colosseum portal pool");

                // The ring of carved stone it stands in, set with stones of the pair's colour.
                var rim = new MeshKit();
                Color32 stone = MeshKit.Rgb(0x4d4470, .25f), stoneLight = MeshKit.Rgb(0x7468a8, .3f), stoneDark = MeshKit.Rgb(0x352e52, .2f);
                rim.Lathe(Vector3.zero, Quaternion.identity, new[]
                {
                    new Vector2(1.2f, 0f), new Vector2(1.22f, .1f), new Vector2(1.14f, .19f), new Vector2(1.02f, .19f), new Vector2(.97f, .1f), new Vector2(.97f, 0f)
                }, new[] { stoneDark, stone, stoneLight, stoneLight, stone, stoneDark }, 30);
                for (int i = 0; i < 6; i++)
                {
                    float angle = (i + .5f) * Mathf.PI / 3f;
                    rim.Gem(new Vector3(Mathf.Cos(angle) * 1.08f, .19f, Mathf.Sin(angle) * 1.08f), Quaternion.Euler(0f, -angle * Mathf.Rad2Deg, 0f),
                        new Vector3(.1f, .07f, .07f), MeshKit.Rgb(hex, .9f), MeshKit.Mix(MeshKit.Rgb(hex, .9f), new Color32(255, 255, 255, 230), .55f), 6);
                }
                rims[pair] = rim.ToMesh("Colosseum portal rim");
            }

            // Arms of light turning in the pool: thin at both ends, and fading out there.
            var arms = new MeshKit { Gradient = 0f };
            const int steps = 14;
            for (int arm = 0; arm < 3; arm++)
            {
                Vector3 innerWas = Vector3.zero, outerWas = Vector3.zero;
                byte alphaWas = 0;
                for (int s = 0; s <= steps; s++)
                {
                    float t = (float)s / steps, angle = arm * Mathf.PI * 2f / 3f + t * 3.9f, reach = Mathf.Lerp(.12f, .92f, t);
                    // The sine of pi comes out a hair below nothing, and a power of that is not a number at all:
                    // it is held at nothing, or the last piece of each arm has no place to be.
                    float swell = Mathf.Max(0f, Mathf.Sin(t * Mathf.PI));
                    float wide = .02f + .17f * Mathf.Pow(swell, .8f);
                    var way = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                    Vector3 inner = way * (reach - wide * .5f), outer = way * (reach + wide * .5f);
                    byte alpha = (byte)(swell * 165f);
                    if (s > 0)
                    {
                        MapMesh.Sheet(arms, innerWas, inner, outer, outerWas, new Color32(255, 255, 255, alphaWas), new Color32(255, 255, 255, alpha),
                            new Color32(255, 255, 255, alpha), new Color32(255, 255, 255, alphaWas));
                    }
                    innerWas = inner; outerWas = outer; alphaWas = alpha;
                }
            }
            Mesh armMesh = arms.ToMesh("Colosseum portal arms");

            var halo = new MeshKit { Gradient = 0f };
            MapMesh.Band(halo, Vector3.zero, 1.15f, 2.05f, new Color32(255, 255, 255, 105), new Color32(255, 255, 255, 0), 36);
            Mesh haloMesh = halo.ToMesh("Colosseum portal halo");
            var beam = new MeshKit { Gradient = 0f };
            MapMesh.Wall(beam, Vector3.zero, .94f, 1.7f, new Color32(255, 255, 255, 120), new Color32(255, 255, 255, 0), 30);
            Mesh beamMesh = beam.ToMesh("Colosseum portal beam");
            var flare = new MeshKit { Gradient = 0f };
            MapMesh.Band(flare, Vector3.zero, 0f, 1.3f, new Color32(255, 255, 255, 255), new Color32(255, 255, 255, 120), 30);
            Mesh flareMesh = flare.ToMesh("Colosseum portal flare");

            for (int i = 0; i < Most; i++)
            {
                var portal = new Portal { Root = new GameObject("Portal").transform, Seed = i * 2.3f };
                portal.Root.SetParent(root, false);
                portal.Glow = Layer("Portal halo", portal.Root, haloMesh, -6, .024f);
                portal.Rims = new GameObject[PairHex.Length];
                portal.Pools = new MeshRenderer[PairHex.Length];
                for (int pair = 0; pair < PairHex.Length; pair++)
                {
                    portal.Pools[pair] = Layer("Portal pool", portal.Root, pools[pair], -5, .03f);
                    portal.Rims[pair] = Kit.Spawn("Portal rim", portal.Root, rims[pair], true);
                }
                portal.Arms = Layer("Portal arms", portal.Root, armMesh, -4, .038f).transform;
                portal.Counter = Layer("Portal arms", portal.Root, armMesh, -4, .044f).transform;
                portal.Counter.localScale = new Vector3(.62f, 1f, -.62f);   // the same arms, smaller and turning the other way
                portal.Beam = Layer("Portal beam", portal.Root, beamMesh, 1, .05f);
                portal.Flare = Layer("Portal flare", portal.Root, flareMesh, 2, .06f);
                portal.Root.gameObject.SetActive(false);
                portals.Add(portal);
            }
        }

        MeshRenderer Layer(string name, Transform parent, Mesh mesh, int order, float height)
        {
            var face = Kit.SpawnStroke(name, parent, mesh, host.Stroke).GetComponent<MeshRenderer>();
            face.sortingOrder = order;
            face.transform.localPosition = new Vector3(0f, height, 0f);
            return face;
        }

        protected override bool Arrive()
        {
            radius = Value("radius");
            exitScale = Value("exit_speed_scale");
            minSpeed = Value("min_speed");
            cooldown = Value("cooldown");
            int pairs = Mathf.Min(Count("pair_count"), Most / 2);
            float room = radius * 1.25f + .1f;
            for (int pair = 0; pair < pairs; pair++)
            {
                if (!host.Place(room, ColosseumGame.HalfWidth - room - .5f, 2.1f, 6.3f, false, true, out Vector3 spot)) break;
                // Heralded first, then torn open one after the other.
                float wait = ColosseumEvents.Herald + pair * .2f;
                Open(portals[count++], spot, pair, wait);
                Open(portals[count++], -spot, pair, wait + .1f);
                host.Take(spot, room, this);
                host.Take(-spot, room, this);
            }
            clock = 0f;
            closing = -1f;
            return count > 0;
        }

        void Open(Portal portal, Vector3 at, int pair, float wait)
        {
            portal.At = at;
            portal.Pair = pair;
            portal.Age = -wait;
            portal.Flash = 0f;
            portal.Up = true;
            portal.Root.position = at;
            portal.Root.localScale = Vector3.one * .01f;
            for (int i = 0; i < PairHex.Length; i++)
            {
                portal.Rims[i].SetActive(i == pair);
                portal.Pools[i].gameObject.SetActive(i == pair);
            }
            portal.Root.gameObject.SetActive(false);
            host.Foretell(at, radius * 1.2f, MapMesh.Tone(PairHex[pair], 255), wait);
        }

        /// <summary>Its moment has come: it is torn open in a flash.</summary>
        void Reveal(Portal portal)
        {
            portal.Root.gameObject.SetActive(true);
            portal.Flash = 1f;
            host.Strike(portal.At, radius, .3f, "warp");
        }

        public override void Leave()
        {
            if (count > 0 && closing < 0f) closing = 0f;
        }

        public override void Clear()
        {
            foreach (Portal portal in portals)
            {
                portal.Up = false;
                portal.Root.gameObject.SetActive(false);
            }
            mouth.Clear();
            passedThisTurn.Clear();
            ready.Clear();
            count = 0;
            closing = -1f;
            Live = false;
            host.GiveBack(this);
        }

        public override void TurnBegins() { passedThisTurn.Clear(); }

        public override void Tick(float delta, float time)
        {
            clock += delta;
            if (closing >= 0f)
            {
                closing += delta;
                if (closing >= CloseFor) { Clear(); return; }
            }
            float shut = closing < 0f ? 1f : 1f - Mathf.SmoothStep(0f, 1f, closing / CloseFor);
            for (int i = 0; i < count; i++)
            {
                Portal portal = portals[i];
                bool unseen = portal.Age < 0f;
                portal.Age += delta;
                if (portal.Age < 0f) continue;
                if (unseen) Reveal(portal);
                portal.Flash = Mathf.MoveTowards(portal.Flash, 0f, delta * 2.6f);
                float grow = Pop(portal.Age / OpenFor) * shut;
                float beat = 1f + .035f * Mathf.Sin(time * 5.2f + portal.Seed) + .28f * portal.Flash * portal.Flash;
                portal.Root.localScale = Vector3.one * Mathf.Max(.01f, radius * grow);
                portal.Arms.localRotation = Quaternion.Euler(0f, -time * 170f + portal.Seed * 40f, 0f);
                portal.Counter.localRotation = Quaternion.Euler(0f, -time * 250f - portal.Seed * 70f, 0f);
                portal.Pools[portal.Pair].transform.localScale = new Vector3(beat, 1f, beat);
                Color tint = MapMesh.Tone(PairHex[portal.Pair], 255);
                tint.a = (.55f + .2f * Mathf.Sin(time * 3.1f + portal.Seed)) * shut;
                Tint(portal.Glow, tint);
                tint.a = (.5f + .25f * Mathf.Sin(time * 7.3f + portal.Seed) + portal.Flash) * shut;
                Tint(portal.Beam, tint);
                portal.Beam.transform.localScale = new Vector3(1f, (1f + .12f * Mathf.Sin(time * 4.1f + portal.Seed) + .9f * portal.Flash) / Mathf.Max(.2f, radius), 1f);
                Tint(portal.Flare, new Color(1f, 1f, 1f, portal.Flash * portal.Flash * .85f));
                portal.Flare.enabled = portal.Flash > .02f;
            }
        }

        public override void Step(float delta)
        {
            if (closing >= 0f) return;
            float now = Time.time;
            foreach (ColosseumDoll doll in host.Dolls)
            {
                if (!ColosseumEvents.InPlay(doll))
                {
                    if (doll != null) mouth.Remove(doll);
                    continue;
                }
                Vector3 feet = doll.Body.position;
                int at = -1;
                for (int i = 0; i < count && at < 0; i++)
                {
                    // Not until it and its twin are well open.
                    if (portals[i].Age < OpenFor * .6f || portals[i ^ 1].Age < OpenFor * .6f) continue;
                    if (Kit.Flat(feet - portals[i].At).sqrMagnitude < radius * radius) at = i;
                }
                if (at < 0) { mouth.Remove(doll); continue; }
                if (mouth.TryGetValue(doll, out int stood) && stood == at) continue;
                if (doll.State != DollState.Fly && doll.State != DollState.Tumble) continue;
                Vector3 velocity = doll.Body.linearVelocity;
                if (feet.y > 1.3f || Kit.Flat(velocity).magnitude < minSpeed) continue;
                if (ready.TryGetValue(doll, out float when) && now < when) continue;
                passedThisTurn.TryGetValue(doll, out int passes);
                if (passes >= MostPasses) continue;
                passedThisTurn[doll] = passes + 1;

                // In at one, out at its twin: the same place in the pool, the same way, the same speed.
                Portal from = portals[at], to = portals[at ^ 1];
                Vector3 exit = to.At + (feet - from.At);
                doll.Warp(exit);
                // Out as fast as the sheet says, but no faster than the hardest sling (unless it came in faster still).
                Vector3 sent = velocity * exitScale;
                float most = Mathf.Max(ColosseumEvents.TopSpeed, velocity.magnitude);
                if (sent.sqrMagnitude > most * most) sent = sent.normalized * most;
                doll.Body.linearVelocity = sent;
                mouth[doll] = at ^ 1;
                ready[doll] = now + cooldown;
                from.Flash = to.Flash = 1f;
                host.Warped(doll);
                host.Fx.Shockwave(from.At, radius * 1.5f);
                host.Fx.Shockwave(to.At, radius * 1.5f);
                host.Fx.Sparks(from.At + Vector3.up * .5f, 6);
                host.Fx.Burst(exit + Vector3.up * .7f, 1.15f);
                host.Hud.Pop(to.At + Vector3.up * 1.5f, "WARP!", Color.Lerp(MapMesh.Tone(PairHex[to.Pair], 255), Color.white, .35f), 1.1f);
                host.Sound.Play("warp");
                host.Shake(.08f);
                host.Thrill(.3f);
            }
        }
    }
}
