using System.Collections.Generic;
using UnityEngine;

namespace SlingArena.Colosseum
{
    /// <summary>
    /// Pillars of fire, on the Ember Crown only. The court splits open in a few places picked by the dice (in
    /// pairs, one in each team's half, turned about the middle, and clear of where anyone is standing), and a
    /// pillar of fire stands in each. Whoever touches one, slung into it or knocked into it, is burnt, thrown
    /// back off it, and set alight: for the next turns the flames go on costing them a little at every turn's
    /// end. A pillar burns a hero once in a turn, however often it comes back to it. Each time a turn has been
    /// played out the fire moves: the pillars die down and come up somewhere else.
    /// </summary>
    sealed class MapFirePillars : MapFeature
    {
        const int Most = 8, Flames = 8, MostBurns = 3, MostThrows = 6;
        const float RiseFor = .32f, DieFor = .34f, Drawn = .5f, Tall = 2.8f;
        static readonly Color Ember = new Color(1f, .45f, .1f), Hot = new Color(1f, .84f, .32f);

        sealed class Pillar
        {
            public Transform Root, Column, Heart;
            public MeshRenderer Scorch, Glow, Reach, Fire, Core;
            public Vector3 At;
            public float Age, Seed, Flare, Spit;
        }

        /// <summary>The fire a burning hero carries about.</summary>
        sealed class Flame
        {
            public Transform Item;
            public MeshRenderer Face;
        }

        readonly List<Pillar> pillars = new List<Pillar>();
        readonly List<Flame> flames = new List<Flame>();
        readonly Dictionary<ColosseumDoll, int> alight = new Dictionary<ColosseumDoll, int>();      // the turns each hero still burns for
        readonly Dictionary<ColosseumDoll, float> again = new Dictionary<ColosseumDoll, float>();   // when a hero can next be thrown by the fire
        readonly Dictionary<ColosseumDoll, int> burns = new Dictionary<ColosseumDoll, int>();       // how often the fire has thrown each this turn
        readonly Dictionary<ColosseumDoll, int> scorched = new Dictionary<ColosseumDoll, int>();    // the pillars that have burnt each this turn, a bit apiece
        readonly List<ColosseumDoll> roll = new List<ColosseumDoll>();
        float radius = .55f, knockOut = 9f, knockUp = 5f;
        int damage = 14, burnDamage = 5, burnTurns = 2, wanted = 4, period = 1, count, turns;
        float clock, dying = -1f, moving = -1f;
        bool raised;

        public override string Key { get { return ColosseumEventTable.Fire; } }
        public override string Description => "Fire erupts at random. Touch it and you burn.";
        public override bool Busy { get { return Live && dying < 0f && (moving >= 0f || Rising()); } }
        public override Color Theme { get { return Ember; } }
        public override int Mood { get { return 4; } }

        bool Rising()
        {
            for (int i = 0; i < count; i++)
            {
                if (pillars[i].Age < RiseFor) return true;
            }
            return false;
        }

        MeshRenderer Layer(string name, Transform parent, Mesh mesh, int order, float height)
        {
            var face = Kit.SpawnStroke(name, parent, mesh, host.Stroke).GetComponent<MeshRenderer>();
            face.sortingOrder = order;
            face.transform.localPosition = new Vector3(0f, height, 0f);
            return face;
        }

        protected override void Build()
        {
            // The ground it comes out of: scorched black, split, and glowing.
            var scorch = new MeshKit { Gradient = 0f };
            MapMesh.Band(scorch, Vector3.zero, 0f, .62f, new Color32(30, 12, 14, 215), new Color32(46, 18, 16, 190), 28);
            MapMesh.Band(scorch, Vector3.zero, .62f, 1.05f, new Color32(46, 18, 16, 190), new Color32(46, 18, 16, 0), 28);
            for (int i = 0; i < 7; i++)
            {
                float turn = i * Mathf.PI * 2f / 7f + .3f, reach = i % 2 == 0 ? 1.02f : .8f;
                var way = new Vector3(Mathf.Cos(turn), 0f, Mathf.Sin(turn));
                var bend = new Vector3(Mathf.Cos(turn + .22f), 0f, Mathf.Sin(turn + .22f));
                MapMesh.Stroke(scorch, way * .3f + Vector3.up * .002f, bend * (reach * .62f) + Vector3.up * .002f, .07f, new Color32(255, 150, 40, 235));
                MapMesh.Stroke(scorch, bend * (reach * .62f) + Vector3.up * .002f, way * reach + Vector3.up * .002f, .04f, new Color32(255, 110, 30, 200));
            }
            Mesh scorchMesh = scorch.ToMesh("Colosseum fire scorch");
            var glow = new MeshKit { Gradient = 0f };
            MapMesh.Band(glow, Vector3.zero, 0f, .5f, new Color32(255, 236, 150, 235), new Color32(255, 150, 40, 200), 26);
            MapMesh.Band(glow, Vector3.zero, .5f, 1.5f, new Color32(255, 120, 30, 150), new Color32(255, 90, 20, 0), 30);
            Mesh glowMesh = glow.ToMesh("Colosseum fire glow");

            // How near is too near, drawn one unit across: a broken line that turns slowly.
            var reachKit = new MeshKit { Gradient = 0f };
            for (int i = 0; i < 14; i++)
            {
                float from = i * Mathf.PI * 2f / 14f, to = from + Mathf.PI * 2f / 14f * .58f;
                reachKit.Ring(Vector3.zero, .93f, 1f, new Color32(255, 255, 255, 235), 3, from, to);
            }
            Mesh reachMesh = reachKit.ToMesh("Colosseum fire reach");

            // The pillar: a broad red-gold body thinning out upwards, and a white-hot heart inside it.
            var body = new MeshKit { Gradient = 0f };
            body.Lathe(Vector3.zero, Quaternion.identity, new[]
            {
                new Vector2(.06f, 0f), new Vector2(.58f, .1f), new Vector2(.54f, .8f), new Vector2(.5f, 1.5f), new Vector2(.42f, 2.2f), new Vector2(.28f, 2.8f),
                new Vector2(.12f, 3.25f), new Vector2(.01f, 3.6f)
            }, new[]
            {
                new Color32(255, 214, 110, 245), new Color32(255, 160, 44, 240), new Color32(255, 124, 30, 232), new Color32(252, 96, 22, 215),
                new Color32(244, 72, 18, 180), new Color32(232, 52, 14, 120), new Color32(220, 40, 10, 55), new Color32(210, 30, 8, 0)
            }, 18);
            Mesh bodyMesh = body.ToMesh("Colosseum fire pillar");
            var heart = new MeshKit { Gradient = 0f };
            heart.Lathe(Vector3.zero, Quaternion.identity, new[]
            {
                new Vector2(.02f, 0f), new Vector2(.34f, .1f), new Vector2(.3f, .9f), new Vector2(.24f, 1.6f), new Vector2(.12f, 2.2f), new Vector2(.01f, 2.6f)
            }, new[]
            {
                new Color32(255, 252, 214, 255), new Color32(255, 240, 160, 250), new Color32(255, 222, 110, 235), new Color32(255, 196, 70, 180),
                new Color32(255, 174, 48, 90), new Color32(255, 160, 40, 0)
            }, 14);
            Mesh heartMesh = heart.ToMesh("Colosseum fire heart");

            for (int i = 0; i < Most; i++)
            {
                var pillar = new Pillar { Root = new GameObject("Fire pillar").transform, Seed = i * 1.7f + .6f };
                pillar.Root.SetParent(root, false);
                pillar.Scorch = Layer("Fire scorch", pillar.Root, scorchMesh, -7, .02f);
                pillar.Glow = Layer("Fire glow", pillar.Root, glowMesh, -6, .026f);
                pillar.Reach = Layer("Fire reach", pillar.Root, reachMesh, -5, .03f);
                pillar.Fire = Layer("Fire body", pillar.Root, bodyMesh, 2, .03f);
                pillar.Column = pillar.Fire.transform;
                pillar.Core = Layer("Fire heart", pillar.Root, heartMesh, 3, .04f);
                pillar.Heart = pillar.Core.transform;
                pillar.Root.gameObject.SetActive(false);
                pillars.Add(pillar);
            }
            for (int i = 0; i < Flames; i++)
            {
                var flame = new Flame { Face = Layer("Burning", root, heartMesh, 4, 0f) };
                flame.Item = flame.Face.transform;
                flame.Item.gameObject.SetActive(false);
                flames.Add(flame);
            }
        }

        protected override bool Arrive()
        {
            radius = Value("radius");
            damage = Count("damage");
            burnDamage = Count("burn_damage");
            burnTurns = Count("burn_turn");
            knockOut = Value("knock_out");
            knockUp = Value("knock_up");
            period = Mathf.Max(1, Count("period_turn"));
            wanted = Mathf.Min(Count("pillar_count"), Most);
            clock = 0f;
            dying = moving = -1f;
            raised = false;
            turns = 0;
            Raise(true);
            return count > 0;
        }

        protected override void OnReveal() => host.Sound.Play("fire-warn", .7f);

        /// <summary>True if a pillar here would come up under somebody, or right beside them.</summary>
        bool Crowded(Vector3 at)
        {
            foreach (ColosseumDoll doll in host.Dolls)
            {
                if (doll == null || doll.Lost || !doll.Alive) continue;
                if (Kit.Flat(doll.transform.position - at).magnitude < radius + ColosseumDoll.Radius * doll.Bulk + .55f) return true;
            }
            return false;
        }

        /// <summary>
        /// New places for the fire, and a pillar set to come up in each, one after another. The first time they
        /// are marked when the scroll has been read; after that, at once.
        /// </summary>
        void Raise(bool heralded)
        {
            count = 0;
            host.GiveBack(this);
            // Room round each for a hero to get by, between it and a wall and between it and the next one: one
            // wedged in a gap too narrow for it would be thrown against the wall over and over.
            float room = radius + .3f;
            int tries = 0;
            while (count < wanted && tries++ < 24)
            {
                bool paired = wanted - count >= 2;
                if (!host.Place(room, ColosseumGame.HalfWidth - radius - 1.05f, paired ? 1.3f : 0f, ColosseumGame.HalfDepth - 2.6f, true, paired, out Vector3 spot)) break;
                // Never under anyone: a pillar burns only those who go to it.
                if (Crowded(spot) || (paired && Crowded(-spot))) continue;
                // Heralded the first time; when they move, they are marked only a moment before they are up again:
                // it is the end of a turn, and nobody is going anywhere.
                float wait = heralded ? ColosseumEvents.Herald + count * .08f : .28f + count * .06f;
                Light(pillars[count++], spot, wait, heralded);
                host.Take(spot, room, this);
                if (!paired) continue;
                Light(pillars[count++], -spot, wait + (heralded ? .08f : .06f), heralded);
                host.Take(-spot, room, this);
            }
        }

        void Light(Pillar pillar, Vector3 at, float wait, bool heralded)
        {
            pillar.At = at;
            pillar.Age = -wait;
            pillar.Flare = 9f;
            pillar.Spit = Random.Range(.2f, .6f);
            pillar.Root.position = at;
            pillar.Root.gameObject.SetActive(false);
            if (heralded) host.Foretell(at, radius + .25f, Ember, wait);
            else host.Mark(at, radius + .25f, Ember, wait);
        }

        /// <summary>Its moment has come: the ground splits and the fire stands up out of it.</summary>
        void Reveal(Pillar pillar, bool first)
        {
            pillar.Root.gameObject.SetActive(true);
            pillar.Flare = 0f;
            if (first) host.Strike(pillar.At, radius * 1.3f, moving >= 0f ? .14f : .24f, "fire-pillar");
            else
            {
                host.Fx.Puff(pillar.At, 5, 2.6f);
                host.Fx.Sparks(pillar.At + Vector3.up * .5f, 6);
            }
        }

        public override void Leave()
        {
            if (!Live || dying >= 0f) return;
            dying = 0f;
            moving = -1f;
        }

        public override void Clear()
        {
            foreach (Pillar pillar in pillars) pillar.Root.gameObject.SetActive(false);
            foreach (Flame flame in flames) flame.Item.gameObject.SetActive(false);
            alight.Clear();
            again.Clear();
            burns.Clear();
            scorched.Clear();
            count = 0;
            dying = moving = -1f;
            raised = false;
            Live = false;
            if (host != null) host.GiveBack(this);
        }

        public override void TurnBegins()
        {
            scorched.Clear();
            // Whoever the fire gave up throwing, and is still standing in it, is left alone until they are out of it.
            roll.Clear();
            foreach (KeyValuePair<ColosseumDoll, int> thrown in burns)
            {
                if (thrown.Value >= MostThrows && thrown.Key != null && Within(thrown.Key) >= 0) roll.Add(thrown.Key);
            }
            burns.Clear();
            foreach (ColosseumDoll doll in roll) burns[doll] = MostThrows;
        }

        /// <summary>The pillar this hero is in the fire of, or -1.</summary>
        int Within(ColosseumDoll doll)
        {
            Vector3 where = doll.Body.position;
            if (where.y > Tall) return -1;
            float reach = radius + ColosseumDoll.Radius * doll.Bulk * .85f;
            for (int i = 0; i < count; i++)
            {
                if (pillars[i].Age < RiseFor * .6f) continue;
                if (Kit.Flat(where - pillars[i].At).sqrMagnitude <= reach * reach) return i;
            }
            return -1;
        }

        static int Bits(int mask)
        {
            int bits = 0;
            for (; mask != 0; mask >>= 1) bits += mask & 1;
            return bits;
        }

        public override bool TurnEnds()
        {
            if (dying >= 0f) return false;
            // Whoever is alight is burnt a little more as the turn ends.
            bool burnt = false;
            roll.Clear();
            roll.AddRange(alight.Keys);
            foreach (ColosseumDoll doll in roll)
            {
                int left = alight[doll] - 1;
                if (doll == null || doll.Lost || !doll.Alive) { alight.Remove(doll); continue; }
                host.Hurt(doll, burnDamage, "BURN", Ember);
                host.Fx.Sparks(doll.transform.position + Vector3.up * .8f, 6);
                burnt = true;
                if (left <= 0 || !doll.Alive) alight.Remove(doll);
                else alight[doll] = left;
            }
            if (burnt) host.Sound.Play("scald", .55f);
            turns++;
            if (turns % period != 0) return false;
            // The fire moves: these die down, and others are marked and come up somewhere else.
            moving = 0f;
            raised = false;
            return true;
        }

        public override void Step(float delta)
        {
            // Dying down, they burn nobody.
            if (dying >= 0f || (moving >= 0f && !raised)) return;
            float now = Time.time;
            foreach (ColosseumDoll doll in host.Dolls)
            {
                if (!ColosseumEvents.InPlay(doll) || !doll.Alive) continue;
                int index = Within(doll);
                if (index >= 0) Touch(doll, index, now);
            }
        }

        void Touch(ColosseumDoll doll, int index, float now)
        {
            if (again.TryGetValue(doll, out float until) && now < until) return;
            // Thrown about enough for one turn: the fire lets it be, or the turn would never come to an end.
            burns.TryGetValue(doll, out int had);
            if (had >= MostThrows) return;
            again[doll] = now + .3f;
            burns[doll] = had + 1;
            Pillar pillar = pillars[index];
            Vector3 away = Kit.Flat(doll.Body.position - pillar.At);
            Vector3 before = Kit.Flat(doll.Body.linearVelocity);
            float gap = away.magnitude, speed = before.magnitude;
            away = gap > .01f ? away / gap : speed > .1f ? -before / speed : Quaternion.Euler(0f, host.Between(0f, 360f), 0f) * Vector3.forward;
            // Thrown back the way a ball comes off a post, and straight out from the fire besides.
            float into = Vector3.Dot(before, away);
            Vector3 line = into < 0f ? before - 2f * into * away : before;
            line = line.sqrMagnitude > .01f ? line.normalized : away;
            line = (line * .45f + away * .55f).normalized;
            // The first touch of a turn throws it hardest. After that the fire has less and less to give, and in
            // the end only nudges it clear: caught between two pillars, or a pillar and a wall, it would otherwise
            // be thrown to and fro and never come to rest.
            float force = had == 0 ? 1f : had == 1 ? .6f : had == 2 ? .38f : .22f;
            if (had >= 2)
            {
                // Still coming back: something is in its way. It is sent round the fire, towards the open court.
                var round = new Vector3(-away.z, 0f, away.x);
                if (Vector3.Dot(round, Kit.Flat(doll.Body.position)) > 0f) round = -round;
                line = (away * .5f + round * .87f).normalized;
            }
            float thrown = Mathf.Min(ColosseumEvents.TopSpeed, Mathf.Max(knockOut * force, speed * (had == 0 ? .7f : .45f)));
            doll.Squash(.6f);
            doll.Knock(line * thrown + Vector3.up * (knockUp * force));

            // Each pillar burns a hero once in a turn, however often it comes back to it, and no hero is burnt by
            // more than a few of them: nobody is burnt away to nothing in one go.
            scorched.TryGetValue(doll, out int marks);
            bool burnt = (marks & 1 << index) == 0 && Bits(marks) < MostBurns;
            Vector3 point = pillar.At + away * radius + Vector3.up * .7f;
            pillar.Flare = 0f;
            if (!burnt)
            {
                host.Fx.Sparks(point, 5);
                host.Sound.Play("scald", .35f);
                return;
            }
            scorched[doll] = marks | 1 << index;
            host.Hurt(doll, damage, "BURN!", Ember);
            if (burnTurns > 0 && burnDamage > 0 && doll.Alive) alight[doll] = burnTurns;
            host.Fx.Impact(point, .55f);
            host.Fx.Sparks(point, 10);
            host.Fx.Puff(point, 4, 3f);
            host.Sound.Play("scald");
            host.Shake(.18f, .12f);
            host.Thrill(.4f);
        }

        public override void Tick(float delta, float time)
        {
            clock += delta;
            if (dying >= 0f)
            {
                dying += delta;
                if (dying >= DieFor) { Clear(); return; }
            }
            if (moving >= 0f)
            {
                moving += delta;
                if (!raised && moving >= DieFor)
                {
                    // The old ones are out: the new places are marked, and the fire comes up in them.
                    for (int i = 0; i < count; i++) pillars[i].Root.gameObject.SetActive(false);
                    raised = true;
                    Raise(false);
                    host.Sound.Play("fire-warn", .45f);
                    host.Shake(.06f);
                }
                else if (raised && !Rising()) moving = -1f;
            }
            float shut = dying >= 0f ? 1f - Mathf.SmoothStep(0f, 1f, dying / DieFor)
                : moving >= 0f && !raised ? 1f - Mathf.SmoothStep(0f, 1f, moving / DieFor) : 1f;
            float size = radius / Drawn;
            for (int i = 0; i < count; i++)
            {
                Pillar pillar = pillars[i];
                bool unseen = pillar.Age < 0f;
                pillar.Age += delta;
                if (pillar.Age < 0f) continue;
                if (unseen) Reveal(pillar, i == 0);
                pillar.Flare += delta;
                float up = Pop(pillar.Age / RiseFor) * shut, flare = Mathf.Exp(-pillar.Flare * 5f);
                float lick = Mathf.Sin(time * 13f + pillar.Seed) * .07f + Mathf.Sin(time * 23f + pillar.Seed * 2f) * .05f;
                float sway = 1f + Mathf.Sin(time * 9f + pillar.Seed * 3f) * .06f + .25f * flare;
                pillar.Column.localScale = new Vector3(size * sway, Mathf.Max(.01f, up * (1f + lick + .2f * flare)), size * sway);
                pillar.Column.localRotation = Quaternion.Euler(0f, time * 160f + pillar.Seed * 40f, 0f);
                pillar.Heart.localScale = new Vector3(size * (2f - sway), Mathf.Max(.01f, up * (1f - lick + .15f * flare)), size * (2f - sway));
                pillar.Heart.localRotation = Quaternion.Euler(0f, -time * 220f + pillar.Seed * 70f, 0f);
                Tint(pillar.Fire, new Color(1f, 1f, 1f, Mathf.Clamp01(up) * (.86f + .14f * Mathf.Sin(time * 17f + pillar.Seed))));
                Tint(pillar.Core, new Color(1f, 1f, 1f, Mathf.Clamp01(up)));
                float show = Mathf.Clamp01(pillar.Age / .12f) * shut;
                Tint(pillar.Scorch, new Color(1f, 1f, 1f, show));
                pillar.Glow.transform.localScale = Vector3.one * (size * (1f + .08f * Mathf.Sin(time * 6f + pillar.Seed) + .5f * flare));
                Tint(pillar.Glow, new Color(1f, 1f, 1f, (.62f + .2f * Mathf.Sin(time * 4.7f + pillar.Seed) + .4f * flare) * show));
                float span = (radius + .1f) * Mathf.Min(1f, Mathf.Max(.01f, up));
                pillar.Reach.transform.localScale = new Vector3(span, 1f, span);
                pillar.Reach.transform.localRotation = Quaternion.Euler(0f, time * 22f + pillar.Seed * 30f, 0f);
                Color ring = Color.Lerp(Ember, Hot, .5f + .5f * Mathf.Sin(time * 8f + pillar.Seed));
                ring.a = .85f * show;
                Tint(pillar.Reach, ring);
                // Sparks climbing off it.
                pillar.Spit -= delta;
                if (pillar.Spit <= 0f && shut >= 1f && up > .8f)
                {
                    pillar.Spit = Random.Range(.35f, .8f);
                    host.Fx.Sparks(pillar.At + Vector3.up * Random.Range(1.2f, 2.1f), 2);
                }
            }

            // The flames on those who are alight: a little fire dancing over each of them.
            int used = 0;
            foreach (KeyValuePair<ColosseumDoll, int> burning in alight)
            {
                if (used >= flames.Count) break;
                ColosseumDoll doll = burning.Key;
                if (doll == null || doll.Lost || !doll.Alive) continue;
                Flame flame = flames[used++];
                if (!flame.Item.gameObject.activeSelf) flame.Item.gameObject.SetActive(true);
                float beat = 1f + Mathf.Sin(time * 15f + used * 1.9f) * .16f;
                flame.Item.position = doll.transform.position + new Vector3(Mathf.Sin(time * 7f + used) * .06f, 1.05f * doll.Bulk, 0f);
                flame.Item.localScale = new Vector3(.6f, .4f * beat, .6f) * doll.Bulk;
                flame.Item.localRotation = Quaternion.Euler(0f, time * 240f, 0f);
                Tint(flame.Face, new Color(1f, 1f, 1f, .95f));
            }
            for (int i = used; i < flames.Count; i++)
            {
                if (flames[i].Item.gameObject.activeSelf) flames[i].Item.gameObject.SetActive(false);
            }
        }
    }
}
