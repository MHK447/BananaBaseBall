using System.Collections.Generic;
using UnityEngine;

namespace SlingArena.Colosseum
{
    /// <summary>
    /// The goblin sapper's bombs. Every enemy the goblin runs into while it is slung gets a bomb stuck on its head.
    /// A moment after the last of them is stuck on, and so before the turn passes to the other side, the bombs
    /// go off one after another, in the order they were stuck on. Each gives a moment's warning: all at once it
    /// starts to shake, and the hero under it with it, harder and harder, blinking red-hot, and then it bursts. The
    /// hero carrying it is hurt and thrown into the air; every team-mate of theirs inside the ring on the sand is
    /// hurt less and thrown clear.
    /// </summary>
    public sealed class ColosseumMines
    {
        /// <summary>How far a bomb reaches.</summary>
        public const float BlastRadius = 2f;
        // How long a bomb shakes before it bursts (the first to go, and each one after it), and the pause between
        // one bursting and the next starting to shake.
        const float FirstFuse = .7f, NextFuse = .44f, Pause = .08f;
        // How much room a bomb takes up on a head: the health plate floats that much higher over it.
        const float PlateRoom = .82f;
        const int Pool = 10;
        static readonly Vector3 BallMiddle = new Vector3(0f, .2f, 0f), FuseTip = new Vector3(.2f, .68f, 0f);
        const float BallRadius = .27f;

        sealed class Bomb
        {
            public Transform Root, Body, Spark;
            public MeshRenderer Ring, Heat;
            public ColosseumDoll Carrier;
            public int Team, Damage;
            public float Age, Fuse, Burn, Beat, Seed, Fizz;
            public bool Live, Lit;
        }

        readonly List<Bomb> bombs = new List<Bomb>();
        readonly List<Bomb> order = new List<Bomb>();   // the ones stuck on somebody, first stuck first
        readonly MaterialPropertyBlock block = new MaterialPropertyBlock();
        bool burning, first;
        float wait;

        /// <summary>A bomb has started to shake: who carries it.</summary>
        public System.Action<ColosseumDoll> Lit;
        /// <summary>Sparks fly off the fuse of a shaking bomb: where.</summary>
        public System.Action<Vector3> Fizzes;
        /// <summary>A bomb goes off with the team and damage it received when it was planted.</summary>
        public System.Action<ColosseumDoll, int, int> WentOff;

        public ColosseumMines(ColosseumGame owner)
        {
            Transform root = new GameObject("Colosseum bombs").transform;
            root.SetParent(owner.World, false);
            Material stroke = Kit.StrokeMaterial(Color.white);

            // The bomb: a round iron shell with a band about its middle, a brass neck and a short fuse curling up
            // out of it. It is drawn standing on its own underside, so that it can be set down on a head.
            var shell = new MeshKit();
            var fittings = new MeshKit();
            Color32 iron = MeshKit.Rgb(0x3a4160, .95f), band = MeshKit.Rgb(0x596288, .8f), gold = MeshKit.Rgb(Kit.Gold, .9f);
            Color32 goldDark = MeshKit.Rgb(Kit.GoldDark, .7f), rope = MeshKit.Rgb(0xead9ac, .1f), ropeDark = MeshKit.Rgb(0xb89a68, .1f);
            shell.Ball(BallMiddle, BallRadius, iron, 18);
            shell.Blob(BallMiddle, new Vector3(BallRadius + .012f, .05f, BallRadius + .012f), Quaternion.identity, band, 1f, 1f, 18);
            shell.Capsule(BallMiddle + Vector3.up * (BallRadius - .05f), BallMiddle + Vector3.up * (BallRadius + .075f), .105f, .09f, gold, 12);
            fittings.Blob(BallMiddle + Vector3.up * (BallRadius + .078f), new Vector3(.1f, .022f, .1f), Quaternion.identity, goldDark, 1f, 1f, 10);
            Vector3 from = BallMiddle + Vector3.up * (BallRadius + .06f);
            Vector3[] curl = { from, from + new Vector3(.02f, .09f, 0f), from + new Vector3(.08f, .15f, 0f), FuseTip };
            for (int i = 0; i + 1 < curl.Length; i++)
                shell.Capsule(curl[i], curl[i + 1], .032f, .03f, i == curl.Length - 2 ? ropeDark : rope, 8);
            Mesh shellMesh = shell.ToMesh("Colosseum bomb"), fittingMesh = fittings.ToMesh("Colosseum bomb fittings");
            Mesh sparkMesh = Kit.UnitBall(0xffd24a, 1f), heatMesh = Kit.UnitBall(0xffffff, 1f);

            // The ring on the sand: a faint wash out to the edge of the blast, a firm line at the edge and ticks
            // inside it. White, so that it can be tinted.
            var reach = new MeshKit { Gradient = 0f };
            reach.Disc(Vector3.zero, 1f, 1f, new Color32(255, 255, 255, 18), new Color32(255, 255, 255, 58), 48);
            reach.Ring(new Vector3(0f, .003f, 0f), .955f, 1f, new Color32(255, 255, 255, 235), 56);
            for (int i = 0; i < 24; i++)
            {
                float a = i * 15f * Mathf.Deg2Rad, b = a + 5f * Mathf.Deg2Rad;
                Vector3 da = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)), db = new Vector3(Mathf.Cos(b), 0f, Mathf.Sin(b)), up = Vector3.up * .005f;
                reach.Quad(da * .86f + up, da * .93f + up, db * .93f + up, db * .86f + up, new Color32(255, 255, 255, 170));
            }
            Mesh reachMesh = reach.ToMesh("Colosseum bomb reach");

            for (int i = 0; i < Pool; i++)
            {
                var bomb = new Bomb { Root = new GameObject("Bomb").transform, Seed = i * 1.7f };
                bomb.Root.SetParent(root, false);
                bomb.Body = Kit.Spawn("Bomb body", bomb.Root, shellMesh, true).transform;
                Kit.Spawn("Bomb fittings", bomb.Body, fittingMesh, false, false);
                bomb.Spark = Kit.Spawn("Bomb spark", bomb.Body, sparkMesh, false, false).transform;
                bomb.Spark.localPosition = FuseTip;
                // A shell of light over the iron: clear until the bomb starts to shake, then red-hot, then white.
                bomb.Heat = Kit.SpawnStroke("Bomb heat", bomb.Body, heatMesh, stroke).GetComponent<MeshRenderer>();
                bomb.Heat.transform.localPosition = BallMiddle;
                bomb.Heat.transform.localScale = Vector3.one * (BallRadius * 1.07f);
                bomb.Heat.sortingOrder = 2;
                bomb.Ring = Kit.SpawnStroke("Bomb reach", bomb.Root, reachMesh, stroke).GetComponent<MeshRenderer>();
                bomb.Ring.sortingOrder = -7;
                bomb.Ring.transform.localPosition = new Vector3(0f, .026f, 0f);
                bomb.Root.gameObject.SetActive(false);
                bombs.Add(bomb);
            }
        }

        /// <summary>No bombs left anywhere (a new set, or back to the lobby).</summary>
        public void Clear()
        {
            for (int i = order.Count - 1; i >= 0; i--) Drop(order[i]);
            burning = false;
        }

        /// <summary>True for a hero with a bomb on its head.</summary>
        public bool Carries(ColosseumDoll doll)
        {
            foreach (Bomb bomb in order)
            {
                if (bomb.Carrier == doll) return true;
            }
            return false;
        }

        /// <summary>True while there is a bomb still to go off.</summary>
        public bool Any { get { return order.Count > 0; } }

        /// <summary>True from the moment the bombs are set going until the last of them has burst.</summary>
        public bool Burning { get { return burning; } }

        /// <summary>
        /// Sticks a bomb on an enemy's head. Its damage includes the sapper's attack and any boost at planting time.
        /// </summary>
        public bool Plant(int team, ColosseumDoll carrier, int damage)
        {
            if (carrier == null || Carries(carrier)) return false;
            Bomb bomb = bombs.Find(spare => !spare.Live);
            if (bomb == null) return false;
            bomb.Carrier = carrier;
            bomb.Team = team;
            bomb.Damage = damage;
            bomb.Age = bomb.Burn = bomb.Beat = bomb.Fizz = 0f;
            bomb.Live = true;
            bomb.Lit = false;
            order.Add(bomb);
            carrier.PlateLift = PlateRoom;
            bomb.Root.gameObject.SetActive(true);
            Animate(bomb, 0f, Time.time);
            return true;
        }

        /// <summary>Sets the bombs going: from now on they burst one after another, first stuck first.</summary>
        public void Light()
        {
            if (burning || order.Count == 0) return;
            burning = first = true;
            wait = 0f;
        }

        void Drop(Bomb bomb)
        {
            bomb.Live = bomb.Lit = false;
            bomb.Root.gameObject.SetActive(false);
            if (bomb.Carrier != null) bomb.Carrier.PlateLift = 0f;
            bomb.Carrier = null;
            order.Remove(bomb);
        }

        public void Tick(float delta)
        {
            float time = Time.time;
            for (int i = order.Count - 1; i >= 0; i--)
            {
                Bomb bomb = order[i];
                // Whoever goes out through a gap in the wall takes their bomb with them.
                if (bomb.Carrier == null || bomb.Carrier.Lost) Drop(bomb);
                else Animate(bomb, delta, time);
            }
            if (!burning) return;
            if (order.Count == 0) { burning = false; return; }

            Bomb next = order[0];
            if (!next.Lit)
            {
                wait -= delta;
                if (wait > 0f) return;
                next.Lit = true;
                next.Burn = 0f;
                next.Fuse = first ? FirstFuse : NextFuse;
                first = false;
                if (Lit != null) Lit(next.Carrier);
                return;
            }
            next.Burn += delta / next.Fuse;
            next.Carrier.Rattle(Mathf.Lerp(.45f, 1f, next.Burn));
            if (next.Burn < 1f) return;

            ColosseumDoll carrier = next.Carrier;
            int team = next.Team, damage = next.Damage;
            Drop(next);
            wait = Pause;
            if (order.Count == 0) burning = false;
            WentOff?.Invoke(carrier, team, damage);
        }

        /// <summary>
        /// The bomb sits on its carrier's head and goes where the head goes. Until its turn comes it only rocks a
        /// little, its fuse smouldering. Once it is lit it shakes harder and harder, swells, and glows red and then
        /// white, beating faster all the time, while the ring on the sand closes in to the edge of the blast.
        /// </summary>
        void Animate(Bomb bomb, float delta, float time)
        {
            ColosseumDoll carrier = bomb.Carrier;
            bomb.Age += delta;
            bomb.Root.position = Kit.Flat(carrier.transform.position);
            float grow = Mathf.Clamp01(bomb.Age / .2f);
            float size = (1f + .15f * carrier.Bulk) * grow * (1f + Mathf.Sin(grow * Mathf.PI) * .45f);
            Vector3 seat = carrier.Crown;
            Quaternion turn = Quaternion.FromToRotation(Vector3.up, carrier.HeadAxis)
                * Quaternion.Euler(Mathf.Sin(time * 2.3f + bomb.Seed) * 5f, 0f, -9f + Mathf.Cos(time * 1.9f + bomb.Seed) * 5f);
            float burn = bomb.Lit ? Mathf.Clamp01(bomb.Burn) : 0f;
            float ember = .05f + .012f * Mathf.Sin(time * 31f + bomb.Seed);
            float heat = 0f;
            if (bomb.Lit)
            {
                bomb.Beat += delta * (16f + 52f * burn);
                float pulse = .5f + .5f * Mathf.Sin(bomb.Beat);
                float shake = Mathf.Lerp(.022f, .085f, burn), twist = Mathf.Lerp(9f, 26f, burn);
                seat += new Vector3(Mathf.Sin(time * 97f + bomb.Seed), Mathf.Sin(time * 113f) * .5f, Mathf.Cos(time * 89f + bomb.Seed)) * shake;
                turn *= Quaternion.Euler(Mathf.Sin(time * 71f) * twist, Mathf.Sin(time * 53f + bomb.Seed) * twist, Mathf.Cos(time * 61f) * twist);
                size *= 1f + .12f * burn + .3f * burn * burn * burn + .06f * pulse;
                // It blinks, iron and red-hot by turns, quicker and quicker.
                heat = Mathf.Lerp(.62f, .9f, burn) * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((pulse - .35f) / .3f));
                ember = (.1f + .09f * burn) * (.75f + .5f * Random.value);
                bomb.Fizz -= delta;
                if (bomb.Fizz <= 0f)
                {
                    bomb.Fizz = Mathf.Lerp(.07f, .03f, burn);
                    if (Fizzes != null) Fizzes(bomb.Spark.position);
                }
            }
            bomb.Body.SetPositionAndRotation(seat, turn);
            bomb.Body.localScale = Vector3.one * Mathf.Max(.001f, size);
            bomb.Spark.localScale = Vector3.one * ember;

            Color glow = Color.Lerp(new Color(1f, .17f, .06f), new Color(1f, .9f, .55f), burn * burn * burn);
            glow.a = heat;
            block.SetColor("_Color", glow);
            bomb.Heat.SetPropertyBlock(block);
            bomb.Heat.enabled = heat > .01f;

            // The ring shows only for the bomb whose turn it is: it snaps in wide and closes on the blast's edge.
            bomb.Ring.enabled = bomb.Lit;
            if (!bomb.Lit) return;
            float close = 1f - Mathf.Pow(1f - burn, 2f);
            float reach = BlastRadius * Mathf.Lerp(1.3f, 1f, close) * (1f + .03f * Mathf.Sin(bomb.Beat));
            bomb.Ring.transform.localScale = new Vector3(reach, 1f, reach);
            var tint = new Color(1f, Mathf.Lerp(.55f, .25f, burn), .12f, Mathf.Clamp01(.35f + .5f * burn) * Mathf.Clamp01(burn * 6f + .25f));
            block.SetColor("_Color", tint);
            bomb.Ring.SetPropertyBlock(block);
        }
    }
}
