using System.Collections.Generic;
using UnityEngine;

namespace SlingArena.Colosseum
{
    /// <summary>
    /// Lava vents. Holes open in the sand, each with a ring round it showing how far it reaches. Every time a turn
    /// has been played out (or every few turns, by the sheet) they go off: the ground rumbles for a moment, and
    /// then every hero standing or lying inside a ring, of either side, is burnt and thrown into the air. So a
    /// hero is best not left beside a vent at the end of its own turn, and an enemy is well knocked onto one.
    /// When nobody is in reach the vents only spit, and the match does not wait for them.
    /// </summary>
    sealed class MapLava : MapFeature
    {
        const int Most = 8, DropsEach = 8;
        const float OpenFor = .7f, WarnFor = .62f, BurstFor = .5f, CloseFor = .45f, Hole = .5f;
        static readonly Color Ember = new Color(1f, .42f, .1f), Hot = new Color(1f, .82f, .3f);

        enum Stage { Calm, Warning, Bursting }

        sealed class Vent
        {
            public Transform Root, Rocks, Column;
            public MeshRenderer Reach, Glow, Pool, Fire;
            public Vector3 At;
            public float Age, Seed, Burst, Size, Spit;
            public bool Up, Hits;
        }

        sealed class Drop
        {
            public Transform Item;
            public Vector3 Velocity;
            public float Age, Life, Size;
            public bool Active;
        }

        readonly List<Vent> vents = new List<Vent>();
        readonly List<Drop> drops = new List<Drop>();
        readonly List<ColosseumDoll> victims = new List<ColosseumDoll>();
        float radius = 1.25f, knockUp = 9f, knockOut = 4f;
        int damage = 18, period = 1, count, turns, dropCursor;
        float clock, stageClock, closing = -1f;
        Stage stage;

        public override string Key { get { return ColosseumEventTable.Lava; } }
        public override string Description => "Watch the vents. Eruptions hit heroes nearby.";
        public override bool Busy { get { return count > 0 && (clock < ColosseumEvents.Herald + count * .09f + OpenFor || stage != Stage.Calm); } }
        public override Color Theme { get { return Ember; } }
        public override int Mood { get { return 4; } }

        protected override void Build()
        {
            // Scorched rocks heaped round the hole.
            var rocks = new MeshKit();
            Color32 basalt = MeshKit.Rgb(0x3d3442, .15f), basaltLight = MeshKit.Rgb(0x5a4c58, .15f), scorched = MeshKit.Rgb(0x6b3a2c, .1f);
            for (int i = 0; i < 10; i++)
            {
                float angle = i * Mathf.PI * 2f / 10f + (i % 2) * .12f, size = i % 3 == 0 ? .2f : i % 3 == 1 ? .15f : .17f;
                var at = new Vector3(Mathf.Cos(angle) * (Hole + .08f), size * .55f, Mathf.Sin(angle) * (Hole + .08f));
                rocks.Blob(at, new Vector3(size * 1.25f, size * .85f, size), Quaternion.Euler(0f, -angle * Mathf.Rad2Deg + 90f + i * 23f, i * 7f - 20f),
                    i % 4 == 0 ? scorched : i % 2 == 0 ? basalt : basaltLight, .7f, .75f, 8);
            }
            Mesh rockMesh = rocks.ToMesh("Colosseum lava rocks");

            var pool = new MeshKit { Gradient = 0f };
            MapMesh.Band(pool, Vector3.zero, 0f, Hole * .55f, new Color32(255, 244, 170, 255), new Color32(255, 196, 70, 255), 24);
            MapMesh.Band(pool, Vector3.zero, Hole * .55f, Hole + .06f, new Color32(255, 196, 70, 255), new Color32(255, 96, 24, 255), 24);
            Mesh poolMesh = pool.ToMesh("Colosseum lava pool");
            var glow = new MeshKit { Gradient = 0f };
            MapMesh.Band(glow, Vector3.zero, Hole * .9f, Hole + .75f, new Color32(255, 120, 30, 150), new Color32(255, 90, 20, 0), 30);
            Mesh glowMesh = glow.ToMesh("Colosseum lava glow");

            // How far a vent reaches, drawn one unit across: a faint wash, a broken line at the edge.
            var reach = new MeshKit { Gradient = 0f };
            MapMesh.Band(reach, Vector3.zero, 0f, 1f, new Color32(255, 255, 255, 14), new Color32(255, 255, 255, 46), 40);
            for (int i = 0; i < 20; i++)
            {
                float from = i * Mathf.PI * 2f / 20f, to = from + Mathf.PI * 2f / 20f * .62f;
                reach.Ring(new Vector3(0f, .003f, 0f), .94f, 1f, new Color32(255, 255, 255, 235), 3, from, to);
            }
            Mesh reachMesh = reach.ToMesh("Colosseum lava reach");

            // The jet: white-hot at the foot, red and thinning out at the top.
            var fire = new MeshKit { Gradient = 0f };
            fire.Lathe(Vector3.zero, Quaternion.identity, new[]
            {
                new Vector2(.02f, 0f), new Vector2(.5f, .12f), new Vector2(.44f, .8f), new Vector2(.34f, 1.5f), new Vector2(.16f, 2.25f), new Vector2(.01f, 2.8f)
            }, new[]
            {
                new Color32(255, 250, 200, 255), new Color32(255, 236, 140, 250), new Color32(255, 176, 50, 235), new Color32(255, 104, 26, 200),
                new Color32(240, 60, 16, 120), new Color32(220, 40, 10, 0)
            }, 18);
            Mesh fireMesh = fire.ToMesh("Colosseum lava jet");
            Mesh dropMesh = Kit.UnitBall(0xff8a1e, 1f);

            for (int i = 0; i < Most; i++)
            {
                var vent = new Vent { Root = new GameObject("Lava vent").transform, Seed = i * 1.9f + .4f };
                vent.Root.SetParent(root, false);
                vent.Reach = Layer("Lava reach", vent.Root, reachMesh, -7, .02f);
                vent.Glow = Layer("Lava glow", vent.Root, glowMesh, -6, .024f);
                vent.Pool = Layer("Lava pool", vent.Root, poolMesh, -5, .03f);
                vent.Rocks = Kit.Spawn("Lava rocks", vent.Root, rockMesh, true).transform;
                vent.Fire = Layer("Lava jet", vent.Root, fireMesh, 2, .04f);
                vent.Column = vent.Fire.transform;
                vent.Root.gameObject.SetActive(false);
                vents.Add(vent);
            }
            for (int i = 0; i < Most * DropsEach; i++)
            {
                var drop = new Drop { Item = Kit.Spawn("Lava drop", root, dropMesh, false, false).transform };
                drop.Item.gameObject.SetActive(false);
                drops.Add(drop);
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
            damage = Count("damage");
            knockUp = Value("knock_up");
            knockOut = Value("knock_out");
            period = Mathf.Max(1, Count("period_turn"));
            int wanted = Mathf.Min(Count("vent_count"), Most);
            // A vent opens clear of everyone: nobody is burnt at the next turn's end for standing where they stood.
            while (count < wanted)
            {
                bool paired = wanted - count >= 2;
                if (!host.Place(radius + .1f, ColosseumGame.HalfWidth - 1.25f, paired ? 1.6f : 0f, 6.7f, false, paired, out Vector3 spot)) break;
                // Heralded first, then they break through the sand one after another.
                float wait = ColosseumEvents.Herald + count * .09f;
                Open(vents[count++], spot, wait);
                host.Take(spot, Hole + .35f, this);
                if (!paired) continue;
                Open(vents[count++], -spot, wait + .09f);
                host.Take(-spot, Hole + .35f, this);
            }
            clock = stageClock = 0f;
            closing = -1f;
            turns = 0;
            stage = Stage.Calm;
            return count > 0;
        }

        protected override void OnReveal() => host.Sound.Play("lava-rumble", .7f);

        void Open(Vent vent, Vector3 at, float wait)
        {
            vent.At = at;
            vent.Age = -wait;
            vent.Burst = -1f;
            vent.Spit = Random.Range(.6f, 1.6f);
            vent.Up = true;
            vent.Hits = false;
            vent.Root.position = at;
            vent.Root.localScale = Vector3.one;
            vent.Rocks.localScale = Vector3.one * .01f;
            vent.Fire.enabled = false;
            vent.Root.gameObject.SetActive(false);
            // The mark is the reach of its fire: where not to be standing when a turn ends.
            host.Foretell(at, radius, Ember, wait);
        }

        /// <summary>Its moment has come: it breaks up through the sand with a spit of fire that burns nobody.</summary>
        void Reveal(Vent vent)
        {
            vent.Root.gameObject.SetActive(true);
            host.Strike(vent.At, Hole * 1.5f, .26f, "thud");
            Jet(vent, .62f, 4);
        }

        public override void Leave()
        {
            if (count > 0 && closing < 0f) closing = 0f;
        }

        public override void Clear()
        {
            foreach (Vent vent in vents)
            {
                vent.Up = false;
                vent.Root.gameObject.SetActive(false);
            }
            foreach (Drop drop in drops)
            {
                drop.Active = false;
                drop.Item.gameObject.SetActive(false);
            }
            victims.Clear();
            count = 0;
            closing = -1f;
            stage = Stage.Calm;
            Live = false;
            host.GiveBack(this);
        }

        bool InReach(Vent vent, ColosseumDoll doll)
        {
            return Kit.Flat(doll.transform.position - vent.At).magnitude < radius + ColosseumDoll.Radius * doll.Bulk * .5f;
        }

        public override bool TurnEnds()
        {
            if (count == 0 || closing >= 0f) return false;
            turns++;
            if (turns % period != 0) return false;
            victims.Clear();
            for (int i = 0; i < count; i++) vents[i].Hits = false;
            foreach (ColosseumDoll doll in host.Dolls)
            {
                if (!ColosseumEvents.InPlay(doll) || !doll.Alive) continue;
                for (int i = 0; i < count; i++)
                {
                    if (!InReach(vents[i], doll)) continue;
                    vents[i].Hits = true;
                    if (!victims.Contains(doll)) victims.Add(doll);
                }
            }
            if (victims.Count == 0)
            {
                // Nobody in reach: the vents only spit, and the match goes straight on.
                for (int i = 0; i < count; i++) Jet(vents[i], .42f, 3);
                host.Sound.Play("lava", .22f);
                return false;
            }
            stage = Stage.Warning;
            stageClock = 0f;
            host.Sound.Play("lava-rumble");
            host.Shake(.12f);
            return true;
        }

        void Erupt()
        {
            stage = Stage.Bursting;
            stageClock = 0f;
            for (int i = 0; i < count; i++)
            {
                Vent vent = vents[i];
                Jet(vent, vent.Hits ? 1f : .5f, vent.Hits ? DropsEach : 3);
                if (!vent.Hits) continue;
                host.Fx.Shockwave(vent.At, radius * 1.2f);
                host.Fx.Puff(vent.At, 12, 5f);
                host.Hud.Pop(vent.At + Vector3.forward * .9f, "ERUPT!", Hot, 1.25f);
            }
            foreach (ColosseumDoll doll in victims)
            {
                if (!ColosseumEvents.InPlay(doll)) continue;
                // Thrown up and away from the nearest vent that caught it.
                Vent nearest = null;
                float best = float.MaxValue;
                for (int i = 0; i < count; i++)
                {
                    float gap = Kit.Flat(doll.transform.position - vents[i].At).sqrMagnitude;
                    if (gap < best) { best = gap; nearest = vents[i]; }
                }
                Vector3 away = Kit.Flat(doll.transform.position - nearest.At);
                away = away.sqrMagnitude > .0004f ? away.normalized : Quaternion.Euler(0f, Random.value * 360f, 0f) * Vector3.forward;
                doll.Knock(away * knockOut + Vector3.up * knockUp);
                host.Hurt(doll, damage, "BURN", Ember);
                host.Fx.Sparks(doll.transform.position + Vector3.up * .5f, 8);
            }
            victims.Clear();
            host.Sound.Play("lava");
            host.Shake(.4f, .28f);
            host.Thrill(.55f);
        }

        /// <summary>A vent goes off: the jet of fire, and drops of lava thrown out of it.</summary>
        void Jet(Vent vent, float size, int thrown)
        {
            vent.Burst = 0f;
            vent.Size = size;
            for (int i = 0; i < thrown; i++)
            {
                Drop drop = drops[dropCursor];
                dropCursor = (dropCursor + 1) % drops.Count;
                Vector2 out2 = Random.insideUnitCircle * (1.6f + 2.4f * size);
                drop.Active = true;
                drop.Age = 0f;
                drop.Life = Random.Range(.55f, .9f);
                drop.Size = Random.Range(.07f, .14f) * (.6f + .6f * size);
                drop.Velocity = new Vector3(out2.x, Random.Range(4.5f, 8.5f) * (.5f + .5f * size), out2.y);
                drop.Item.position = vent.At + Vector3.up * .25f;
                drop.Item.localScale = Vector3.one * drop.Size;
                drop.Item.gameObject.SetActive(true);
            }
        }

        public override void Tick(float delta, float time)
        {
            clock += delta;
            if (closing >= 0f)
            {
                closing += delta;
                if (closing >= CloseFor) { Clear(); return; }
            }
            float shut = closing < 0f ? 1f : 1f - Mathf.SmoothStep(0f, 1f, closing / CloseFor);
            float warn = 0f;
            if (stage == Stage.Warning)
            {
                stageClock += delta;
                warn = Mathf.Clamp01(stageClock / WarnFor);
                host.Shake(.03f + .06f * warn);
                if (stageClock >= WarnFor) Erupt();
            }
            else if (stage == Stage.Bursting)
            {
                stageClock += delta;
                if (stageClock >= BurstFor) stage = Stage.Calm;
            }

            for (int i = 0; i < count; i++)
            {
                Vent vent = vents[i];
                bool unseen = vent.Age < 0f;
                vent.Age += delta;
                if (vent.Age < 0f) continue;
                if (unseen) Reveal(vent);
                float grow = Pop(vent.Age / OpenFor) * shut;
                float heat = vent.Hits ? warn : warn * .35f;
                vent.Rocks.localScale = Vector3.one * Mathf.Max(.01f, grow);
                float bubble = 1f + .07f * Mathf.Sin(time * 6.3f + vent.Seed) + .05f * Mathf.Sin(time * 11.1f + vent.Seed * 2f) + .3f * heat;
                vent.Pool.transform.localScale = new Vector3(bubble, 1f, bubble) * Mathf.Max(.01f, grow);
                Tint(vent.Glow, new Color(1f, 1f, 1f, (.6f + .25f * Mathf.Sin(time * 4.4f + vent.Seed) + heat) * shut));
                vent.Glow.transform.localScale = Vector3.one * (grow * (1f + .5f * heat));

                // The ring: steady while all is quiet, beating fast and bright while the ground rumbles.
                float beat = stage == Stage.Warning ? .5f + .5f * Mathf.Sin(stageClock * (26f + 30f * warn)) : 0f;
                float span = radius * Mathf.Min(1f, grow) * (1f + .05f * beat * heat);
                vent.Reach.transform.localScale = new Vector3(span, 1f, span);
                vent.Reach.transform.localRotation = Quaternion.Euler(0f, time * 14f + vent.Seed * 30f, 0f);
                Color ring = Color.Lerp(Ember, Hot, heat * beat);
                ring.a = Mathf.Lerp(.5f, 1f, heat) * shut;
                Tint(vent.Reach, ring);

                if (vent.Burst >= 0f)
                {
                    vent.Burst += delta;
                    float up = Mathf.Clamp01(vent.Burst / .1f), fade = 1f - Mathf.SmoothStep(0f, 1f, (vent.Burst - .2f) / .32f);
                    vent.Fire.enabled = fade > .01f;
                    vent.Column.localScale = new Vector3(.75f + .45f * fade, up * (.85f + .25f * Mathf.Sin(vent.Burst * 40f) * fade), .75f + .45f * fade) * vent.Size;
                    Tint(vent.Fire, new Color(1f, 1f, 1f, fade));
                    if (fade <= .01f) vent.Burst = -1f;
                }
                else if (stage == Stage.Calm && closing < 0f && vent.Age > OpenFor)
                {
                    // An ember now and then, so that it never looks dead.
                    vent.Spit -= delta;
                    if (vent.Spit <= 0f)
                    {
                        vent.Spit = Random.Range(.9f, 2.1f);
                        Jet(vent, .16f, 1);
                    }
                }
            }

            foreach (Drop drop in drops)
            {
                if (!drop.Active) continue;
                drop.Age += delta;
                drop.Velocity += Vector3.down * (19f * delta);
                Vector3 at = drop.Item.position + drop.Velocity * delta;
                if (drop.Age >= drop.Life || at.y < .03f)
                {
                    drop.Active = false;
                    drop.Item.gameObject.SetActive(false);
                    continue;
                }
                drop.Item.position = at;
                drop.Item.localScale = Vector3.one * (drop.Size * (1f - .6f * drop.Age / drop.Life));
            }
        }
    }
}
