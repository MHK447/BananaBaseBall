using System.Collections.Generic;
using UnityEngine;

namespace SlingArena.Colosseum
{
    /// <summary>
    /// The emperor's supply crate. He has a crate dropped onto the court, with a sign over it showing what is
    /// inside: health, shields or another turn. The first good knock breaks it open (or the last of several, by
    /// the sheet): a hero slung or thrown into it, a slam, a star, a bomb or the boomerang's edge. What is in it
    /// goes to the side whose turn it is when it breaks. Health goes to every hero of that side still standing; so
    /// does a shield, a bubble that takes the next blows for its hero until it is used up or its rounds run out;
    /// another turn is taken straight after the turn in hand. A crate nobody breaks is taken away again.
    /// </summary>
    sealed class MapCrate : MapFeature
    {
        enum Gift { Heal, Shield, Turn }
        enum Stage { None, Falling, Standing, Broken }

        const float FallFrom = 11f, FallFor = .72f, Half = .5f, Tall = .9f, SignHigh = 1.78f;
        const int Planks = 10, Bubbles = 8;
        static readonly Color Green = new Color(.5f, 1f, .52f), Blue = new Color(.6f, .88f, 1f), Gold = new Color(1f, .82f, .23f);

        sealed class Plank
        {
            public Transform Item;
            public Vector3 Velocity, Spin;
            public float Age;
            public bool Active;
        }

        sealed class Bubble
        {
            public Transform Root;
            public MeshRenderer Ball, Rim;
            public ColosseumDoll Doll;
            public float Age, Flash, Burst;
            public bool Active;
        }

        readonly List<Plank> planks = new List<Plank>();
        readonly List<Bubble> bubbles = new List<Bubble>();
        readonly GameObject[] sign = new GameObject[3];
        Transform crate, signs;
        GameObject body;
        MeshRenderer shadow;
        Vector3 at;
        Stage stage;
        Gift gift;
        int hitsLeft, healAmount, shieldAmount, shieldRounds, shieldEnds;
        float clock, wobble;

        public override string Key { get { return ColosseumEventTable.Crate; } }
        public override string Description => "Break the crate to claim a gift for your team.";
        public override bool Busy { get { return stage == Stage.Falling; } }
        public override Color Theme { get { return Gold; } }
        /// <summary>It is on its way down from the moment it is called out: the show runs to its landing.</summary>
        public override float Lead { get { return FallFor; } }
        public override int Mood { get { return 3; } }
        /// <summary>Where the crate is, and whether it is standing there to be broken.</summary>
        public Vector3 At { get { return at; } }
        public bool Standing { get { return stage == Stage.Standing; } }

        protected override void Build()
        {
            // A stout wooden crate bound with gold, under a lid of the emperor's purple.
            var kit = new MeshKit();
            Color32 wood = MeshKit.Rgb(0xb5763f, .15f), woodLight = MeshKit.Rgb(0xd9a064, .2f), gold = MeshKit.Rgb(Kit.Gold, .9f), goldLight = MeshKit.Rgb(0xffd76a, .9f);
            Color32 purple = MeshKit.Rgb(0x8d52da, .35f), purpleLight = MeshKit.Rgb(0xb685f2, .4f);
            kit.Chamfer(new Vector3(0f, .43f, 0f), new Vector3(Half * 2f, .82f, Half * 2f), Quaternion.identity, .06f, wood, woodLight);
            kit.Box(new Vector3(0f, .43f, 0f), new Vector3(Half * 2f + .04f, .84f, .18f), Quaternion.identity, gold, goldLight);
            kit.Box(new Vector3(0f, .43f, 0f), new Vector3(.18f, .84f, Half * 2f + .04f), Quaternion.identity, gold, goldLight);
            kit.Chamfer(new Vector3(0f, .88f, 0f), new Vector3(Half * 2f + .1f, .12f, Half * 2f + .1f), Quaternion.identity, .04f, purple, purpleLight);
            kit.Blob(new Vector3(0f, .97f, 0f), new Vector3(.17f, .08f, .17f), Quaternion.identity, gold, 1f, 1f, 10);
            crate = Kit.Spawn("Supply crate", root, kit.ToMesh("Colosseum supply crate"), true).transform;
            crate.gameObject.SetActive(false);

            body = new GameObject("Supply crate body");
            body.transform.SetParent(root, false);
            var box = body.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, Tall * .5f, 0f);
            box.size = new Vector3(Half * 2f, Tall, Half * 2f);
            box.sharedMaterial = host.WallSurface;
            body.SetActive(false);

            // The sign over it: a dark roundel facing the camera, with what is inside drawn on it.
            signs = new GameObject("Supply crate sign").transform;
            signs.SetParent(root, false);
            signs.rotation = ColosseumEvents.TowardLens;
            for (int i = 0; i < sign.Length; i++)
            {
                sign[i] = Kit.Spawn("Supply sign", signs, SignMesh((Gift)i), true, false);
                sign[i].SetActive(false);
            }
            signs.gameObject.SetActive(false);

            var shade = new MeshKit { Gradient = 0f };
            MapMesh.Band(shade, Vector3.zero, 0f, .85f, new Color32(40, 22, 30, 150), new Color32(40, 22, 30, 0), 24);
            shadow = Kit.SpawnStroke("Supply crate shadow", root, shade.ToMesh("Colosseum crate shadow"), host.Stroke).GetComponent<MeshRenderer>();
            shadow.sortingOrder = -6;
            shadow.gameObject.SetActive(false);

            var board = new MeshKit();
            board.Chamfer(Vector3.zero, new Vector3(.52f, .07f, .17f), Quaternion.identity, .02f, wood, woodLight);
            Mesh boardMesh = board.ToMesh("Colosseum crate plank");
            for (int i = 0; i < Planks; i++)
            {
                var plank = new Plank { Item = Kit.Spawn("Crate plank", root, boardMesh, true).transform };
                plank.Item.gameObject.SetActive(false);
                planks.Add(plank);
            }

            // A shield: a bubble round its hero, with a bright rim that always faces the camera.
            Mesh ball = Kit.UnitBall(0xffffff, 1f);
            var hoop = new MeshKit { Gradient = 0f };
            hoop.Ring(Vector3.zero, .93f, 1f, new Color32(255, 255, 255, 255), 40);
            hoop.Ring(Vector3.zero, .7f, .8f, new Color32(255, 255, 255, 150), 10, 2.2f, 3.1f);
            Mesh hoopMesh = hoop.ToMesh("Colosseum shield rim");
            for (int i = 0; i < Bubbles; i++)
            {
                var bubble = new Bubble { Root = new GameObject("Shield").transform };
                bubble.Root.SetParent(root, false);
                bubble.Ball = Kit.SpawnStroke("Shield bubble", bubble.Root, ball, host.Stroke).GetComponent<MeshRenderer>();
                bubble.Ball.sortingOrder = 1;
                bubble.Rim = Kit.SpawnStroke("Shield rim", bubble.Root, hoopMesh, host.Stroke).GetComponent<MeshRenderer>();
                bubble.Rim.sortingOrder = 2;
                bubble.Rim.transform.rotation = ColosseumEvents.TowardLens * Quaternion.Euler(90f, 0f, 0f);
                bubble.Root.gameObject.SetActive(false);
                bubbles.Add(bubble);
            }
        }

        static Mesh SignMesh(Gift gift)
        {
            var kit = new MeshKit();
            var round = new Vector2[22];
            for (int i = 0; i < round.Length; i++)
            {
                float angle = i * Mathf.PI * 2f / round.Length;
                round[i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * .5f;
            }
            kit.Plate(Vector3.zero, Quaternion.identity, round, .08f, .03f, MeshKit.Rgb(0x2c2338, .3f), MeshKit.Rgb(0xfff3d6, .5f));
            var front = new Vector3(0f, 0f, -.07f);
            switch (gift)
            {
                case Gift.Heal:
                {
                    var heart = new Vector2[26];
                    for (int i = 0; i < heart.Length; i++)
                    {
                        float t = i * Mathf.PI * 2f / heart.Length, sine = Mathf.Sin(t);
                        heart[i] = new Vector2(16f * sine * sine * sine, 13f * Mathf.Cos(t) - 5f * Mathf.Cos(2f * t) - 2f * Mathf.Cos(3f * t) - Mathf.Cos(4f * t)) * (.3f / 16f)
                            + new Vector2(0f, .03f);
                    }
                    kit.Plate(front, Quaternion.identity, heart, .07f, .02f, MeshKit.Rgb(0xff4d5e, .7f), MeshKit.Rgb(0xff9aa6, .8f));
                    break;
                }
                case Gift.Shield:
                    kit.Plate(front, Quaternion.identity, new[]
                    {
                        new Vector2(-.27f, .28f), new Vector2(0f, .34f), new Vector2(.27f, .28f), new Vector2(.27f, .02f), new Vector2(.18f, -.2f),
                        new Vector2(0f, -.35f), new Vector2(-.18f, -.2f), new Vector2(-.27f, .02f)
                    }, .07f, .02f, MeshKit.Rgb(0x4aa3ff, .7f), MeshKit.Rgb(0xd4ecff, .8f));
                    kit.Plate(front + new Vector3(0f, 0f, -.04f), Quaternion.identity, new[]
                    {
                        new Vector2(-.05f, .2f), new Vector2(.05f, .2f), new Vector2(.05f, .05f), new Vector2(.17f, .05f), new Vector2(.17f, -.04f),
                        new Vector2(.05f, -.04f), new Vector2(.05f, -.2f), new Vector2(-.05f, -.2f), new Vector2(-.05f, -.04f), new Vector2(-.17f, -.04f),
                        new Vector2(-.17f, .05f), new Vector2(-.05f, .05f)
                    }, .04f, .012f, MeshKit.Rgb(0xffffff, .8f), MeshKit.Rgb(0xffffff, .8f));
                    break;
                default:
                {
                    // A bolt of lightning, in two pieces: the upper stroke, and the point below it.
                    Color32 yellow = MeshKit.Rgb(0xffd83a, .8f), pale = MeshKit.Rgb(0xfff7b0, .9f);
                    kit.Plate(front, Quaternion.identity, new[]
                    {
                        new Vector2(.02f, .38f), new Vector2(.24f, .38f), new Vector2(.08f, .02f), new Vector2(-.2f, .02f)
                    }, .07f, .02f, yellow, pale);
                    kit.Plate(front, Quaternion.identity, new[]
                    {
                        new Vector2(-.08f, .1f), new Vector2(.2f, .1f), new Vector2(-.1f, -.38f)
                    }, .07f, .02f, yellow, pale);
                    break;
                }
            }
            return kit.ToMesh("Colosseum supply sign");
        }

        protected override bool Arrive()
        {
            hitsLeft = Mathf.Max(1, Count("hits"));
            healAmount = Count("heal_amount");
            shieldAmount = Count("shield_amount");
            shieldRounds = Count("shield_round");
            float heal = Value("heal_weight"), guard = Value("shield_weight"), turn = Value("extra_turn_weight");
            float pick = host.Chance() * (heal + guard + turn);
            gift = heal + guard + turn <= 0f || pick < heal ? Gift.Heal : pick < heal + guard ? Gift.Shield : Gift.Turn;
            if (!host.Place(.9f, ColosseumGame.HalfWidth - 1.7f, 0f, 3.6f, true, false, out at)) return false;
            host.Take(at, .95f, this);

            stage = Stage.Falling;
            clock = wobble = 0f;
            crate.SetPositionAndRotation(at + Vector3.up * FallFrom, Quaternion.Euler(0f, 14f, 0f));
            crate.localScale = Vector3.one;
            crate.gameObject.SetActive(true);
            for (int i = 0; i < sign.Length; i++) sign[i].SetActive(i == (int)gift);
            signs.gameObject.SetActive(false);
            shadow.transform.position = at + Vector3.up * .022f;
            shadow.gameObject.SetActive(true);
            body.transform.position = at;
            body.SetActive(false);
            // Where it is coming down is marked on the sand all the way.
            host.Foretell(at, 1.05f, Gold, FallFor);
            return true;
        }

        protected override void OnReveal() => host.Sound.Play("fall", .8f);

        void Land()
        {
            stage = Stage.Standing;
            clock = 0f;
            crate.position = at;
            body.SetActive(true);
            signs.gameObject.SetActive(true);
            // It comes down hard: the sand cracks under it, and the whole view jumps.
            host.Fx.Puff(at, 10, 4.6f);
            host.Fx.Crater(at, .95f);
            host.Strike(at, .9f, .5f, "smash", .2f);
            // Anyone it comes down on is shouldered aside.
            foreach (ColosseumDoll doll in host.Dolls)
            {
                if (!ColosseumEvents.InPlay(doll)) continue;
                Vector3 away = Kit.Flat(doll.transform.position - at);
                if (away.magnitude > Half + ColosseumDoll.Radius * doll.Bulk + .15f) continue;
                away = away.sqrMagnitude > .0004f ? away.normalized : Vector3.forward;
                doll.Knock(away * 4.2f + Vector3.up * 3.6f);
            }
        }

        public override void Leave()
        {
            if (stage != Stage.Falling && stage != Stage.Standing) return;
            // Nobody wanted it: it is whisked away.
            host.Fx.Puff(at + Vector3.up * .4f, 14, 4f);
            Hide();
            stage = Stage.None;
            host.GiveBack(this);
        }

        void Hide()
        {
            crate.gameObject.SetActive(false);
            signs.gameObject.SetActive(false);
            shadow.gameObject.SetActive(false);
            body.SetActive(false);
        }

        public override void Clear()
        {
            if (crate != null) Hide();
            foreach (Plank plank in planks)
            {
                plank.Active = false;
                plank.Item.gameObject.SetActive(false);
            }
            foreach (Bubble bubble in bubbles) Release(bubble);
            stage = Stage.None;
            shieldEnds = 0;
            Live = false;
            host.GiveBack(this);
        }

        public override void RoundBegins(int round)
        {
            if (shieldEnds <= 0 || round < shieldEnds) return;
            // The shields' rounds have run out: they go, used up or not.
            shieldEnds = 0;
            foreach (Bubble bubble in bubbles)
            {
                if (!bubble.Active || bubble.Doll == null) continue;
                bubble.Doll.Shield = 0;
            }
        }

        public override bool Struck(ColosseumDoll doll, Vector3 point, Vector3 normal, Vector3 relative)
        {
            if (stage != Stage.Standing) return false;
            if (Mathf.Abs(point.x - at.x) > Half + .3f || Mathf.Abs(point.z - at.z) > Half + .3f) return false;
            if (normal.y > .6f && point.y < Tall * .6f) return false;   // the sand beside it, not the crate
            Vector3 before = doll.PreVelocity;
            if (Mathf.Max(before.magnitude, relative.magnitude) < 2.2f) return false;
            point.y = Mathf.Max(point.y, .45f);
            if (Hit(host.TurnSide, point) && doll.State == DollState.Fly)
            {
                // A hero on its attack goes on through the wreck.
                doll.Body.linearVelocity = new Vector3(before.x * .82f, Mathf.Max(doll.Body.linearVelocity.y, 1.2f), before.z * .82f);
            }
            doll.Squash(.5f);
            return true;
        }

        public override void Blast(Vector3 where, float radius, int team)
        {
            if (stage == Stage.Standing && Kit.Flat(where - at).magnitude < radius + Half + .1f) Hit(team, at + Vector3.up * .5f);
        }

        /// <summary>The crate takes a knock for this side; true if that was the one that broke it.</summary>
        bool Hit(int team, Vector3 point)
        {
            hitsLeft--;
            wobble = 1f;
            host.Fx.Impact(point, .5f);
            host.Fx.Sparks(point, 6);
            if (hitsLeft > 0)
            {
                host.Sound.Play("crate", .55f);
                host.Hud.Pop(at + Vector3.up * 1.2f, "CRACK!", Gold, 1f);
                host.Shake(.1f);
                return false;
            }
            Break(team);
            return true;
        }

        void Break(int team)
        {
            stage = Stage.Broken;
            clock = 0f;
            Hide();
            host.GiveBack(this);
            foreach (Plank plank in planks)
            {
                Vector2 out2 = Random.insideUnitCircle.normalized * Random.Range(2.5f, 6.5f);
                plank.Active = true;
                plank.Age = 0f;
                plank.Velocity = new Vector3(out2.x, Random.Range(4f, 8.5f), out2.y);
                plank.Spin = Random.onUnitSphere * Random.Range(300f, 800f);
                plank.Item.SetPositionAndRotation(at + new Vector3(Random.Range(-.3f, .3f), Random.Range(.25f, .8f), Random.Range(-.3f, .3f)), Random.rotation);
                plank.Item.localScale = Vector3.one;
                plank.Item.gameObject.SetActive(true);
            }
            host.Fx.Burst(at + Vector3.up * .8f, 1.7f);
            host.Fx.Sparks(at + Vector3.up * .6f, 18);
            host.Fx.Puff(at, 14, 5f);
            host.Fx.Shockwave(at, 1.8f);
            host.Sound.Play("crate");
            host.Shake(.26f, .25f);
            host.Thrill(.6f);
            Give(team);
        }

        void Give(int team)
        {
            string side = team == 0 ? "BLUE" : "RED";
            Vector3 above = at + Vector3.up * 1.3f;
            switch (gift)
            {
                case Gift.Heal:
                    foreach (ColosseumDoll doll in host.Dolls)
                    {
                        if (doll == null || doll.Team != team || !doll.Alive || doll.Lost) continue;
                        int got = doll.Heal(healAmount);
                        if (got <= 0) continue;
                        host.Hud.Pop(doll.transform.position + Vector3.up * .5f, "+" + got, Green, 1.15f);
                        host.Fx.Burst(doll.transform.position + Vector3.up, .9f);
                    }
                    host.Hud.Pop(above, "HEAL!", Green, 1.5f);
                    host.Hud.Banner("HEALED!", side + " TEAM  +" + healAmount + " HP EACH", 1.8f, team);
                    host.Sound.Play("heal");
                    break;
                case Gift.Shield:
                    foreach (ColosseumDoll doll in host.Dolls)
                    {
                        if (doll == null || doll.Team != team || !doll.Alive || doll.Lost) continue;
                        doll.Shield = shieldAmount;
                        Wrap(doll);
                    }
                    shieldEnds = shieldRounds > 0 ? host.Round + shieldRounds : 0;
                    host.Hud.Pop(above, "SHIELD!", Blue, 1.5f);
                    host.Hud.Banner("SHIELDS UP!", side + " TEAM BLOCKS " + shieldAmount + " DAMAGE EACH", 1.8f, team);
                    host.Sound.Play("shield");
                    break;
                default:
                    host.GrantExtraTurn(team);
                    host.Hud.Pop(above, "EXTRA TURN!", Gold, 1.5f);
                    host.Hud.Banner("EXTRA TURN!", side + " GOES AGAIN AFTER THIS", 1.8f, team);
                    host.Sound.Play("powerup");
                    break;
            }
        }

        void Wrap(ColosseumDoll doll)
        {
            Bubble bubble = bubbles.Find(one => one.Active && one.Doll == doll) ?? bubbles.Find(one => !one.Active);
            if (bubble == null) return;
            bubble.Doll = doll;
            bubble.Active = true;
            bubble.Age = 0f;
            bubble.Flash = 1f;
            bubble.Burst = -1f;
            bubble.Root.gameObject.SetActive(true);
        }

        static void Release(Bubble bubble)
        {
            bubble.Active = false;
            bubble.Doll = null;
            if (bubble.Root != null) bubble.Root.gameObject.SetActive(false);
        }

        public override void ShieldHeld(ColosseumDoll doll, int held)
        {
            Bubble bubble = bubbles.Find(one => one.Active && one.Doll == doll);
            if (bubble != null) bubble.Flash = 1f;
            Vector3 chest = doll.transform.position + Vector3.up * .7f;
            host.Hud.Pop(doll.transform.position + Vector3.up * .3f, "BLOCK -" + held, Blue, 1f);
            host.Fx.Sparks(chest, 6);
            host.Sound.Play("block");
        }

        public override void Tick(float delta, float time)
        {
            clock += delta;
            if (stage == Stage.Falling)
            {
                float t = Mathf.Clamp01(clock / FallFor);
                crate.SetPositionAndRotation(at + Vector3.up * (FallFrom * (1f - t * t)), Quaternion.Euler(Mathf.Sin(time * 9f) * 8f * (1f - t), 14f, Mathf.Cos(time * 7f) * 8f * (1f - t)));
                float size = Mathf.Lerp(.35f, 1f, t);
                shadow.transform.localScale = new Vector3(size, 1f, size);
                Tint(shadow, new Color(1f, 1f, 1f, t));
                if (t >= 1f) Land();
            }
            else if (stage == Stage.Standing)
            {
                wobble = Mathf.MoveTowards(wobble, 0f, delta * 2.6f);
                // Flat for a moment as it lands, then square; a knock that does not break it sets it rocking.
                float flat = Mathf.Exp(-clock * 9f) * Mathf.Cos(clock * 26f) * .22f;
                crate.localScale = new Vector3(1f + flat, 1f - flat, 1f + flat);
                crate.rotation = Quaternion.Euler(Mathf.Sin(time * 41f) * 7f * wobble, 14f, Mathf.Cos(time * 37f) * 7f * wobble);
                signs.position = at + Vector3.up * (SignHigh + .08f * Mathf.Sin(time * 3.2f));
                signs.localScale = Vector3.one * (Pop(clock / .35f) * (1f + .05f * Mathf.Sin(time * 6.4f)));
                Tint(shadow, new Color(1f, 1f, 1f, 1f));
                // The boomerang's edge opens it in passing.
                foreach (ColosseumDoll doll in host.Dolls)
                {
                    if (doll == null || doll.Lost || !doll.Riding) continue;
                    if (Kit.Flat(doll.transform.position - at).magnitude > Half + 1.05f) continue;
                    Hit(doll.Team, at + Vector3.up * .5f);
                    break;
                }
            }
            else if (stage == Stage.Broken && clock > 1.2f)
            {
                stage = Stage.None;
            }

            bool busy = stage != Stage.None;
            foreach (Plank plank in planks)
            {
                if (!plank.Active) continue;
                plank.Age += delta;
                plank.Velocity += Vector3.down * (20f * delta);
                Vector3 to = plank.Item.position + plank.Velocity * delta;
                if (to.y < .05f)
                {
                    // One bounce off the sand, losing most of its go.
                    to.y = .05f;
                    plank.Velocity = new Vector3(plank.Velocity.x * .45f, -plank.Velocity.y * .3f, plank.Velocity.z * .45f);
                    plank.Spin *= .4f;
                }
                plank.Item.position = to;
                plank.Item.Rotate(plank.Spin * delta, Space.World);
                float fade = 1f - Mathf.Clamp01((plank.Age - .7f) / .4f);
                plank.Item.localScale = Vector3.one * Mathf.Max(.01f, fade);
                if (fade <= 0f)
                {
                    plank.Active = false;
                    plank.Item.gameObject.SetActive(false);
                }
                busy = true;
            }

            foreach (Bubble bubble in bubbles)
            {
                if (!bubble.Active) continue;
                busy = true;
                ColosseumDoll doll = bubble.Doll;
                bubble.Age += delta;
                bubble.Flash = Mathf.MoveTowards(bubble.Flash, 0f, delta * 3f);
                bool held = doll != null && doll.Alive && !doll.Lost && doll.Shield > 0;
                if (!held && bubble.Burst < 0f)
                {
                    bubble.Burst = 0f;
                    if (doll != null && !doll.Lost) host.Fx.Sparks(doll.transform.position + Vector3.up * .7f, 8);
                }
                if (doll == null) { Release(bubble); continue; }
                float size = ColosseumDoll.Radius * doll.Bulk * 1.75f * Pop(bubble.Age / .3f), see = 1f;
                if (bubble.Burst >= 0f)
                {
                    // Used up: it swells and is gone.
                    bubble.Burst += delta;
                    float t = bubble.Burst / .22f;
                    if (t >= 1f) { Release(bubble); continue; }
                    size *= 1f + .5f * t;
                    see = 1f - t;
                }
                bubble.Root.position = doll.transform.position + Vector3.up * (ColosseumDoll.Radius * doll.Bulk * 1.2f);
                bubble.Root.localScale = Vector3.one * Mathf.Max(.01f, size * (1f + .03f * Mathf.Sin(time * 5f + bubble.Age)));
                Color light = MeshKit.Rgb(Kit.TeamLight[doll.Team], 1f);
                Color tint = Color.Lerp(light, Color.white, .35f + .5f * bubble.Flash);
                tint.a = (.16f + .3f * bubble.Flash) * see;
                Tint(bubble.Ball, tint);
                tint.a = (.8f + .2f * bubble.Flash) * see;
                Tint(bubble.Rim, tint);
            }
            if (!busy) Live = false;
        }
    }
}
