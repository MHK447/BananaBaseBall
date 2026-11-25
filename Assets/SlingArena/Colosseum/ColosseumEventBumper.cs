using System.Collections.Generic;
using UnityEngine;

namespace SlingArena.Colosseum
{
    /// <summary>
    /// Bumper pillars. Stout sprung pillars push up out of the sand. Whoever runs into one is thrown off it faster
    /// than they came, at no less than a fair speed: a hero in the middle of its attack goes on attacking, on its
    /// new line, and a hero knocked into one goes tumbling off across the court. They come in pairs, one in each
    /// team's half, turned about the middle of the court, and keep clear of the lane the power gate runs in.
    /// </summary>
    sealed class MapBumpers : MapFeature
    {
        const int Most = 8;
        // Drawn half a metre across; what a hero runs into is this much wider than the sheet's radius.
        const float Drawn = .5f, Girth = 1.18f, Under = 1.45f;
        const float RiseFor = .5f, SinkFor = .4f;
        static readonly Color Gold = new Color(1f, .82f, .23f);

        sealed class Pillar
        {
            public Transform Root, Model;
            public CapsuleCollider Shape;
            public MeshRenderer Band, Pad;
            public Vector3 At;
            public float Age, Bump, Seed;
        }

        readonly List<Pillar> pillars = new List<Pillar>();
        readonly Dictionary<long, float> again = new Dictionary<long, float>();   // when a hero can next be thrown by a pillar: one bounce to a touch
        // How often each hero has been thrown this turn. After so many the pillars are only posts to it: caught
        // between two of them, or one and a wall, it would otherwise be thrown to and fro and never come to rest.
        readonly Dictionary<ColosseumDoll, int> thrownThisTurn = new Dictionary<ColosseumDoll, int>();
        const int MostThrows = 5;
        PhysicsMaterial spring;
        float radius = .5f, bounce = 1.25f, least = 9f;
        int damage, count;
        float clock, sinking = -1f;

        public override string Key { get { return ColosseumEventTable.Bumper; } }
        public override string Description => "Bounce off a pillar for a burst of speed.";
        public override bool Busy { get { return count > 0 && clock < ColosseumEvents.Herald + count * .11f + RiseFor + .15f; } }
        public override Color Theme { get { return Gold; } }
        public override int Mood { get { return 3; } }

        protected override void Build()
        {
            spring = Kit.Own(new PhysicsMaterial("Colosseum bumper")
            {
                dynamicFriction = 0f,
                staticFriction = 0f,
                bounciness = 1f,
                frictionCombine = PhysicsMaterialCombine.Minimum,
                bounceCombine = PhysicsMaterialCombine.Maximum
            });

            // A stone foot, a fat sprung body with two pale hoops round it, and a gold cap with a star on it.
            var kit = new MeshKit();
            Color32 stone = MeshKit.Rgb(0xc4a67c, .12f), stoneLight = MeshKit.Rgb(0xe9dcc0, .15f);
            Color32 red = MeshKit.Rgb(0xe8473a, .55f), redLight = MeshKit.Rgb(0xff7a55, .6f), redDark = MeshKit.Rgb(0xb42c2c, .5f);
            Color32 cream = MeshKit.Rgb(0xfff3d6, .5f), gold = MeshKit.Rgb(Kit.Gold, .9f), goldDark = MeshKit.Rgb(Kit.GoldDark, .8f);
            kit.Lathe(Vector3.zero, Quaternion.identity, new[] { new Vector2(.64f, 0f), new Vector2(.66f, .1f), new Vector2(.6f, .2f), new Vector2(.48f, .24f) },
                new[] { stone, stoneLight, stoneLight, stone }, 20);
            kit.Lathe(Vector3.zero, Quaternion.identity, new[]
            {
                new Vector2(.48f, .24f), new Vector2(.56f, .4f), new Vector2(.6f, .62f), new Vector2(.56f, .84f), new Vector2(.48f, 1f)
            }, new[] { redDark, red, redLight, red, redDark }, 20);
            for (int hoop = 0; hoop < 2; hoop++)
            {
                float y = hoop == 0 ? .47f : .77f, wide = hoop == 0 ? .585f : .585f;
                kit.Lathe(Vector3.zero, Quaternion.identity, new[] { new Vector2(wide, y - .035f), new Vector2(wide + .025f, y), new Vector2(wide, y + .035f) },
                    new[] { cream, cream, cream }, 20);
            }
            kit.Lathe(Vector3.zero, Quaternion.identity, new[]
            {
                new Vector2(.48f, 1f), new Vector2(.58f, 1.03f), new Vector2(.58f, 1.11f), new Vector2(.5f, 1.17f), new Vector2(.3f, 1.23f), new Vector2(.01f, 1.25f)
            }, new[] { goldDark, gold, gold, gold, gold, gold }, 20);
            kit.Plate(new Vector3(0f, 1.262f, 0f), Quaternion.Euler(90f, 0f, 0f), MeshKit.StarRim(5, .27f, .125f), .05f, .015f,
                MeshKit.Rgb(0xfff2a0, 1f), MeshKit.Rgb(0xffc928, 1f));
            Mesh pillarMesh = kit.ToMesh("Colosseum bumper");

            // A hoop of light round the body that flashes when it is hit, and a dark ring on the sand round its foot.
            var band = new MeshKit { Gradient = 0f };
            MapMesh.Wall(band, new Vector3(0f, .3f, 0f), .7f, .62f, new Color32(255, 255, 255, 0), new Color32(255, 255, 255, 230), 24);
            MapMesh.Wall(band, new Vector3(0f, .92f, 0f), .7f, .3f, new Color32(255, 255, 255, 230), new Color32(255, 255, 255, 0), 24);
            Mesh bandMesh = band.ToMesh("Colosseum bumper flash");
            var pad = new MeshKit { Gradient = 0f };
            MapMesh.Band(pad, Vector3.zero, .6f, .82f, new Color32(60, 32, 30, 120), new Color32(60, 32, 30, 0), 28);
            pad.Ring(new Vector3(0f, .002f, 0f), .8f, .86f, new Color32(255, 240, 200, 150), 28);
            Mesh padMesh = pad.ToMesh("Colosseum bumper pad");

            for (int i = 0; i < Most; i++)
            {
                var pillar = new Pillar { Root = new GameObject("Bumper").transform, Seed = i * 1.3f };
                pillar.Root.SetParent(root, false);
                pillar.Model = Kit.Spawn("Bumper pillar", pillar.Root, pillarMesh, true).transform;
                pillar.Band = Kit.SpawnStroke("Bumper flash", pillar.Model, bandMesh, host.Stroke).GetComponent<MeshRenderer>();
                pillar.Band.sortingOrder = 2;
                pillar.Pad = Kit.SpawnStroke("Bumper pad", pillar.Root, padMesh, host.Stroke).GetComponent<MeshRenderer>();
                pillar.Pad.sortingOrder = -6;
                pillar.Pad.transform.localPosition = new Vector3(0f, .022f, 0f);
                pillar.Shape = pillar.Root.gameObject.AddComponent<CapsuleCollider>();
                pillar.Shape.direction = 1;
                pillar.Shape.sharedMaterial = spring;
                pillar.Shape.enabled = false;
                pillar.Root.gameObject.SetActive(false);
                pillars.Add(pillar);
            }
        }

        protected override bool Arrive()
        {
            radius = Value("radius");
            bounce = Value("bounce");
            least = Value("min_bounce_speed");
            damage = Count("damage");
            int wanted = Mathf.Min(Count("pillar_count"), Most);
            float room = radius * Girth + .2f;
            while (count < wanted)
            {
                bool paired = wanted - count >= 2;
                if (!host.Place(room, ColosseumGame.HalfWidth - room - .9f, paired ? 1.7f : 0f, 6.1f, true, paired, out Vector3 spot)) break;
                // Heralded first, then up they come out of the sand, one after another.
                float wait = ColosseumEvents.Herald + count * .11f;
                Raise(pillars[count++], spot, wait);
                host.Take(spot, room, this);
                if (!paired) continue;
                Raise(pillars[count++], -spot, wait + .11f);
                host.Take(-spot, room, this);
            }
            clock = 0f;
            sinking = -1f;
            return count > 0;
        }

        void Raise(Pillar pillar, Vector3 at, float wait)
        {
            float size = radius / Drawn;
            pillar.At = at;
            pillar.Age = -wait;
            pillar.Bump = 9f;
            pillar.Root.position = at;
            pillar.Root.localScale = new Vector3(size, 1f, size);
            pillar.Model.localPosition = Vector3.down * Under;
            pillar.Model.localScale = Vector3.one;
            // In the root's own measure: the root is as much wider as the sheet's radius asks.
            pillar.Shape.radius = Drawn * Girth;
            pillar.Shape.height = 3.2f;
            pillar.Shape.center = new Vector3(0f, 1.3f, 0f);
            pillar.Shape.enabled = false;
            Tint(pillar.Band, new Color(1f, 1f, 1f, 0f));
            pillar.Root.gameObject.SetActive(false);
            host.Foretell(at, radius * Girth + .25f, Gold, wait);
        }

        /// <summary>Its moment has come: it bursts up out of the sand, ringing.</summary>
        void Reveal(Pillar pillar)
        {
            pillar.Root.gameObject.SetActive(true);
            pillar.Bump = 0f;
            host.Strike(pillar.At, radius * 1.3f, .26f, "thud");
            host.Sound.Play("boing", .45f);
        }

        public override void Leave()
        {
            if (count > 0 && sinking < 0f) sinking = 0f;
        }

        public override void Clear()
        {
            foreach (Pillar pillar in pillars)
            {
                pillar.Shape.enabled = false;
                pillar.Root.gameObject.SetActive(false);
            }
            again.Clear();
            thrownThisTurn.Clear();
            count = 0;
            sinking = -1f;
            Live = false;
            host.GiveBack(this);
        }

        public override void TurnBegins() { thrownThisTurn.Clear(); }

        public override void Tick(float delta, float time)
        {
            clock += delta;
            if (sinking >= 0f)
            {
                sinking += delta;
                if (sinking >= SinkFor) { Clear(); return; }
            }
            for (int i = 0; i < count; i++)
            {
                Pillar pillar = pillars[i];
                bool unseen = pillar.Age < 0f;
                pillar.Age += delta;
                if (pillar.Age < 0f) continue;
                if (unseen) Reveal(pillar);
                pillar.Bump += delta;
                // Up out of the sand, a little too far, and back down onto its foot.
                float t = Mathf.Clamp01(pillar.Age / RiseFor);
                float up = sinking < 0f ? 1f - Mathf.Pow(1f - t, 3f) + Mathf.Sin(t * Mathf.PI) * .14f : 1f - Mathf.SmoothStep(0f, 1f, sinking / SinkFor);
                pillar.Model.localPosition = Vector3.down * (Under * (1f - up));
                pillar.Shape.enabled = sinking < 0f && t > .55f;
                // Hit, it rings like a bell: fat and short, thin and tall, and so on, dying away.
                float ring = Mathf.Exp(-pillar.Bump * 8f) * Mathf.Cos(pillar.Bump * 34f);
                pillar.Model.localScale = new Vector3(1f + .24f * ring, 1f - .2f * ring, 1f + .24f * ring);
                Tint(pillar.Band, new Color(1f, .95f, .7f, Mathf.Exp(-pillar.Bump * 6f)));
                pillar.Band.enabled = pillar.Bump < .9f;
                Tint(pillar.Pad, new Color(1f, 1f, 1f, Mathf.Clamp01(up)));
            }
        }

        public override bool Struck(ColosseumDoll doll, Vector3 point, Vector3 normal, Vector3 relative)
        {
            if (sinking >= 0f || normal.y > .6f) return false;
            for (int i = 0; i < count; i++)
            {
                Pillar pillar = pillars[i];
                if (!pillar.Shape.enabled) continue;
                Vector3 away = Kit.Flat(doll.transform.position - pillar.At);
                float gap = away.magnitude;
                if (gap > radius * Girth + ColosseumDoll.Radius * doll.Bulk + .35f) continue;

                Vector3 before = Kit.Flat(doll.PreVelocity);
                float speed = Mathf.Max(before.magnitude, Kit.Flat(relative).magnitude);
                if (speed < 1.5f) return false;
                long key = i * 100000L + doll.Id;
                float now = Time.time;
                if (again.TryGetValue(key, out float until) && now < until) return true;
                thrownThisTurn.TryGetValue(doll, out int throws);
                if (throws >= MostThrows) return false;
                thrownThisTurn[doll] = throws + 1;
                again[key] = now + .14f;

                away = gap > .01f ? away / gap : (before.sqrMagnitude > .01f ? -before.normalized : Vector3.forward);
                // Off it the way a ball comes off a post, and then some: mostly its own line turned back, partly straight out.
                float into = Vector3.Dot(before, away);
                Vector3 line = into < 0f ? before - 2f * into * away : before;
                line = line.sqrMagnitude > .01f ? line.normalized : away;
                line = (line * .65f + away * .35f).normalized;
                float thrown = Mathf.Min(ColosseumEvents.TopSpeed, Mathf.Max(least, speed * bounce));
                if (doll.State == DollState.Fly)
                {
                    // Still on its attack: it carries on down the new line.
                    doll.Body.linearVelocity = line * thrown + Vector3.up * Mathf.Max(doll.Body.linearVelocity.y, 1.4f);
                    doll.Facing = line;
                    doll.Squash(.55f);
                }
                else
                {
                    doll.Knock(line * thrown + Vector3.up * 4.4f);
                }
                if (damage > 0) host.Hurt(doll, damage, "BUMP", Gold);

                pillar.Bump = 0f;
                point.y = Mathf.Max(point.y, .6f);
                host.Fx.Impact(point, .5f);
                host.Fx.Sparks(point, 7);
                host.Fx.Shockwave(pillar.At, radius * 2.4f);
                host.Hud.Pop(pillar.At + Vector3.up * 1.9f, "BOING!", Gold, .95f);
                host.Sound.Play("boing");
                host.Shake(.1f);
                host.Thrill(.25f);
                return true;
            }
            return false;
        }
    }
}
