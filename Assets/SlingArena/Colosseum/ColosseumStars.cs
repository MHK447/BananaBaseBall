using System.Collections.Generic;
using UnityEngine;

namespace SlingArena.Colosseum
{
    /// <summary>
    /// The dawn witch's falling stars. Each one is shot straight up off her wand, climbs out of sight over the arena,
    /// crosses far overhead and comes down on its mark as steeply as it went up, turning as it goes, with a tail of
    /// light behind it and glitter falling away all the way. A ring on the sand shows where it is going to land from
    /// the moment it is loosed, and closes in as it falls.
    /// </summary>
    public sealed class ColosseumStars
    {
        const int Pool = 12, Glitter = 140;
        // The tail is drawn afresh every frame along the way the star has just come: this many lengths of it,
        // covering this much of the whole flight.
        const int TailSteps = 18;
        const float TailSpan = .22f;

        sealed class Shot
        {
            public Transform Star, Halo;
            public MeshRenderer Mark;
            public GameObject Tail;
            public Mesh TailMesh;
            public ColosseumDoll Owner;
            public Vector3 From, To, Bow, Was;
            public float Age, Span, Peak, Turn, Shed, Reach, Seed;
            public int Number;
            public bool Live, Whistled;
        }

        sealed class Mote
        {
            public Transform Body;
            public MeshRenderer Skin;
            public Vector3 Speed;
            public Color Tint;
            public float Age, Life, Size, Turn;
            public bool Live;
        }

        readonly List<Shot> shots = new List<Shot>();
        readonly List<Mote> motes = new List<Mote>();
        readonly MaterialPropertyBlock block = new MaterialPropertyBlock();
        readonly List<Vector3> tailVerts = new List<Vector3>();
        readonly List<Color32> tailColors = new List<Color32>();
        readonly List<int> tailTris = new List<int>();
        static readonly Color[] Tints =
        {
            new Color(1f, .93f, .5f), new Color(1f, .78f, .28f), new Color(1f, .68f, .62f), new Color(1f, 1f, .92f)
        };
        int nextMote;

        /// <summary>How many stars are in the air.</summary>
        public int Flying { get; private set; }

        /// <summary>A star has come down: whose it is, where, and which of the barrage it was (from 0).</summary>
        public System.Action<ColosseumDoll, Vector3, int> Landed;

        /// <summary>A star is over the top and on its way down: where it is going to land.</summary>
        public System.Action<Vector3> Falls;

        public ColosseumStars(ColosseumGame owner)
        {
            Transform root = new GameObject("Colosseum stars").transform;
            root.SetParent(owner.World, false);
            Material stroke = Kit.StrokeMaterial(Color.white);

            // The star itself: a fat gold five-pointer, paler along its edges.
            var body = new MeshKit();
            body.Plate(Vector3.zero, Quaternion.identity, MeshKit.StarRim(5, .44f, .2f), .2f, .08f, MeshKit.Rgb(0xffd648, 1f), MeshKit.Rgb(0xfff4b4, 1f));
            Mesh starMesh = body.ToMesh("Colosseum star");

            // The light round it: a disc that is bright in the middle and gone at the rim. White, to be tinted.
            var glow = new MeshKit { Gradient = 0f };
            glow.Disc(Vector3.zero, 1f, 1f, new Color32(255, 255, 255, 200), new Color32(255, 255, 255, 0), 28);
            Mesh haloMesh = glow.ToMesh("Colosseum star halo");

            // Glitter: a little four-pointed twinkle, drawn on both faces.
            var twinkle = new MeshKit { Gradient = 0f };
            var white = new Color32(255, 255, 255, 255);
            for (int arm = 0; arm < 4; arm++)
            {
                float a = arm * 90f * Mathf.Deg2Rad, b = a + 45f * Mathf.Deg2Rad, c = a - 45f * Mathf.Deg2Rad;
                Vector3 point = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
                Vector3 left = new Vector3(Mathf.Cos(b), Mathf.Sin(b), 0f) * .24f, right = new Vector3(Mathf.Cos(c), Mathf.Sin(c), 0f) * .24f;
                twinkle.Quad(Vector3.zero, left, point, right, white);
                twinkle.Quad(Vector3.zero, right, point, left, white);
            }
            Mesh moteMesh = twinkle.ToMesh("Colosseum star glitter");

            // The mark on the sand: a faint wash, a firm ring at its edge and four ticks pointing in.
            var ring = new MeshKit { Gradient = 0f };
            ring.Disc(Vector3.zero, 1f, 1f, new Color32(255, 255, 255, 30), new Color32(255, 255, 255, 80), 36);
            ring.Ring(new Vector3(0f, .003f, 0f), .9f, 1f, new Color32(255, 255, 255, 240), 40);
            for (int i = 0; i < 4; i++)
            {
                float a = (i * 90f + 45f) * Mathf.Deg2Rad;
                Vector3 way = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)), side = new Vector3(-way.z, 0f, way.x) * .07f, up = Vector3.up * .005f;
                ring.Quad(way * .5f - side + up, way * .5f + side + up, way * .84f + side + up, way * .84f - side + up, new Color32(255, 255, 255, 220));
            }
            Mesh markMesh = ring.ToMesh("Colosseum star mark");

            for (int i = 0; i < Pool; i++)
            {
                var shot = new Shot { Seed = i * 2.3f };
                shot.Star = Kit.Spawn("Star", root, starMesh, true, false).transform;
                shot.Halo = Kit.SpawnStroke("Star halo", shot.Star, haloMesh, stroke).transform;
                // The disc is drawn lying down; stood up behind the star it faces whoever the star is facing.
                shot.Halo.localRotation = Quaternion.Euler(90f, 0f, 0f);
                shot.Halo.localPosition = new Vector3(0f, 0f, -.12f);
                shot.Halo.localScale = Vector3.one * .95f;
                block.SetColor("_Color", new Color(1f, .9f, .45f, .55f));
                shot.Halo.GetComponent<MeshRenderer>().SetPropertyBlock(block);
                shot.Mark = Kit.SpawnStroke("Star mark", root, markMesh, stroke).GetComponent<MeshRenderer>();
                shot.Mark.sortingOrder = -6;
                shot.TailMesh = new Mesh { name = "Colosseum star tail" };
                shot.TailMesh.MarkDynamic();
                shot.Tail = Kit.SpawnStroke("Star tail", root, shot.TailMesh, stroke);
                shot.Tail.GetComponent<MeshRenderer>().sortingOrder = 2;
                shot.Star.gameObject.SetActive(false);
                shot.Mark.gameObject.SetActive(false);
                shot.Tail.SetActive(false);
                shots.Add(shot);
            }
            for (int i = 0; i < Glitter; i++)
            {
                var mote = new Mote();
                GameObject item = Kit.SpawnStroke("Star glitter", root, moteMesh, stroke);
                mote.Body = item.transform;
                mote.Skin = item.GetComponent<MeshRenderer>();
                mote.Skin.sortingOrder = 3;
                item.SetActive(false);
                motes.Add(mote);
            }
        }

        /// <summary>Nothing left in the air or on the sand (a new set, or back to the lobby).</summary>
        public void Clear()
        {
            foreach (Shot shot in shots)
            {
                shot.Live = false;
                shot.Star.gameObject.SetActive(false);
                shot.Mark.gameObject.SetActive(false);
                shot.Tail.SetActive(false);
            }
            foreach (Mote mote in motes)
            {
                mote.Live = false;
                mote.Body.gameObject.SetActive(false);
            }
            Flying = 0;
        }

        /// <summary>
        /// Looses a star from one point to come down on another: how long it takes, how high it climbs on the way,
        /// how far it swings out to one side, how wide its mark on the sand is, and which of the barrage it is.
        /// </summary>
        public void Fire(ColosseumDoll owner, Vector3 from, Vector3 to, float seconds, float peak, Vector3 bow, float reach, int number)
        {
            Shot shot = shots.Find(spare => !spare.Live);
            if (shot == null) return;
            shot.Owner = owner;
            shot.From = shot.Was = from;
            shot.To = new Vector3(to.x, 0f, to.z);
            shot.Bow = bow;
            shot.Span = Mathf.Max(.3f, seconds);
            shot.Peak = peak;
            shot.Reach = reach;
            shot.Number = number;
            shot.Age = shot.Shed = 0f;
            shot.Turn = Random.Range(0f, 360f);
            shot.Live = true;
            shot.Whistled = false;
            shot.Star.position = from;
            shot.Star.localScale = Vector3.one * .01f;
            shot.Star.gameObject.SetActive(true);
            shot.Mark.transform.position = shot.To + Vector3.up * .03f;
            shot.Mark.gameObject.SetActive(true);
            shot.TailMesh.Clear();
            shot.Tail.SetActive(true);
            Flying++;
        }

        /// <summary>
        /// Where a star is, so far through its flight. It goes up off the wand almost straight, and is most of the way
        /// up before it has gone any distance across; it crosses far overhead; and it comes down on the mark as
        /// steeply as it went up: out of the sky, not along the ground.
        /// </summary>
        static Vector3 Along(Shot shot, float t)
        {
            float across = t * t * (3f - 2f * t);
            float climb = 1f - Mathf.Pow(Mathf.Abs(2f * t - 1f), 2.4f);
            Vector3 at = Vector3.Lerp(shot.From, shot.To, across);
            at.y = Mathf.Lerp(shot.From.y, 0f, t) + shot.Peak * climb;
            return at + shot.Bow * Mathf.Sin(across * Mathf.PI);
        }

        /// <summary>
        /// The tail: two ribbons of light along the way the star has just come, a wide faint one and a narrow bright
        /// one inside it, both turned to face whoever is looking and both thinning away to nothing.
        /// </summary>
        void DrawTail(Shot shot, float t, float size, Vector3 towardLens)
        {
            tailVerts.Clear(); tailColors.Clear(); tailTris.Clear();
            for (int ribbon = 0; ribbon < 2; ribbon++)
            {
                float broad = ribbon == 0 ? .42f : .2f;
                Color32 head = ribbon == 0 ? new Color32(255, 214, 110, 96) : new Color32(255, 250, 214, 240);
                Color32 end = ribbon == 0 ? new Color32(255, 140, 110, 0) : new Color32(255, 196, 90, 0);
                int first = tailVerts.Count;
                for (int i = 0; i <= TailSteps; i++)
                {
                    float k = (float)i / TailSteps, then = Mathf.Max(0f, t - k * TailSpan);
                    Vector3 at = Along(shot, then);
                    Vector3 going = Along(shot, Mathf.Min(1f, then + .012f)) - Along(shot, Mathf.Max(0f, then - .012f));
                    Vector3 side = Vector3.Cross(going, towardLens);
                    side = side.sqrMagnitude > 1e-8f ? side.normalized : Vector3.right;
                    float wide = size * broad * (1f - k) * (1f - .35f * k);
                    // A little behind the star, so that it is drawn over the root of its own tail.
                    at -= towardLens * (.06f + ribbon * .01f);
                    tailVerts.Add(at - side * wide);
                    tailVerts.Add(at + side * wide);
                    Color32 tint = Color32.Lerp(head, end, k);
                    tailColors.Add(tint);
                    tailColors.Add(tint);
                    if (i == 0) continue;
                    int b = first + (i - 1) * 2;
                    tailTris.Add(b); tailTris.Add(b + 1); tailTris.Add(b + 3);
                    tailTris.Add(b); tailTris.Add(b + 3); tailTris.Add(b + 2);
                    tailTris.Add(b); tailTris.Add(b + 3); tailTris.Add(b + 1);
                    tailTris.Add(b); tailTris.Add(b + 2); tailTris.Add(b + 3);
                }
            }
            shot.TailMesh.Clear();
            shot.TailMesh.SetVertices(tailVerts);
            shot.TailMesh.SetColors(tailColors);
            shot.TailMesh.SetTriangles(tailTris, 0);
            shot.TailMesh.RecalculateBounds();
        }

        public void Tick(float delta, Vector3 towardLens)
        {
            float time = Time.time;
            foreach (Shot shot in shots)
            {
                if (!shot.Live) continue;
                shot.Age += delta;
                float t = Mathf.Clamp01(shot.Age / shot.Span);
                Vector3 at = Along(shot, t);
                Vector3 way = at - shot.Was;
                shot.Was = at;
                // It swells as it leaves the wand, so that it is as big as she is while it can still be seen going
                // up, and it is bigger again as it comes down at the sand.
                float size = Mathf.Lerp(.7f, 1.75f, Mathf.SmoothStep(0f, 1f, t / .09f)) * (1f + .3f * Mathf.Clamp01((t - .62f) / .38f));
                if (!shot.Whistled && t > .66f)
                {
                    shot.Whistled = true;
                    if (Falls != null) Falls(shot.To);
                }
                shot.Turn += delta * 540f;
                shot.Star.SetPositionAndRotation(at, Quaternion.LookRotation(towardLens, Vector3.up) * Quaternion.Euler(0f, 0f, shot.Turn));
                shot.Star.localScale = Vector3.one * size;
                shot.Halo.localScale = Vector3.one * (.95f + .12f * Mathf.Sin(time * 26f + shot.Seed));
                DrawTail(shot, t, size, towardLens);

                // Glitter falls away behind it, thicker the faster it is going.
                shot.Shed -= delta;
                if (shot.Shed <= 0f)
                {
                    shot.Shed = .022f;
                    Vector3 back = way.sqrMagnitude > 1e-6f ? -way.normalized : Vector3.up;
                    Shed(at + back * .25f + Random.insideUnitSphere * .16f, back * Random.Range(1f, 3f) + Random.insideUnitSphere * .9f, Random.Range(.12f, .26f), Random.Range(.3f, .55f));
                }

                // The mark snaps in wide and closes on the size of the burst, quickening with the fall.
                float close = t * t;
                float reach = shot.Reach * Mathf.Lerp(1.9f, 1f, close) * (1f + .04f * Mathf.Sin(time * 22f + shot.Seed));
                shot.Mark.transform.localScale = new Vector3(reach, 1f, reach);
                shot.Mark.transform.rotation = Quaternion.Euler(0f, time * 90f + shot.Seed * 40f, 0f);
                block.SetColor("_Color", new Color(1f, Mathf.Lerp(.9f, .62f, close), Mathf.Lerp(.5f, .2f, close), Mathf.Clamp01(shot.Age / .12f) * Mathf.Lerp(.5f, .95f, close)));
                shot.Mark.SetPropertyBlock(block);

                if (t < 1f) continue;
                shot.Live = false;
                shot.Star.gameObject.SetActive(false);
                shot.Mark.gameObject.SetActive(false);
                shot.Tail.SetActive(false);
                Flying = Mathf.Max(0, Flying - 1);
                for (int i = 0; i < 14; i++)
                {
                    Vector2 out2 = Random.insideUnitCircle.normalized * Random.Range(2.5f, 7f);
                    Shed(shot.To + Vector3.up * .3f, new Vector3(out2.x, Random.Range(2.5f, 7f), out2.y), Random.Range(.16f, .32f), Random.Range(.35f, .7f));
                }
                if (Landed != null) Landed(shot.Owner, shot.To, shot.Number);
            }

            foreach (Mote mote in motes)
            {
                if (!mote.Live) continue;
                mote.Age += delta;
                float k = mote.Age / mote.Life;
                if (k >= 1f)
                {
                    mote.Live = false;
                    mote.Body.gameObject.SetActive(false);
                    continue;
                }
                mote.Speed += Vector3.down * (9f * delta);
                mote.Speed *= Mathf.Exp(-2.2f * delta);
                Vector3 place = mote.Body.position + mote.Speed * delta;
                if (place.y < .05f) { place.y = .05f; mote.Speed.y = Mathf.Abs(mote.Speed.y) * .3f; }
                mote.Turn += delta * 300f;
                // It twinkles: swelling and shrinking as it goes out.
                float size = mote.Size * (1f - k * k) * (.7f + .3f * Mathf.Sin(mote.Age * 34f + mote.Turn));
                mote.Body.SetPositionAndRotation(place, Quaternion.LookRotation(towardLens, Vector3.up) * Quaternion.Euler(0f, 0f, mote.Turn));
                mote.Body.localScale = Vector3.one * Mathf.Max(.001f, size);
                Color tint = mote.Tint;
                tint.a = 1f - k * k * k;
                block.SetColor("_Color", tint);
                mote.Skin.SetPropertyBlock(block);
            }
        }

        /// <summary>One twinkle of glitter: where, how it is moving, how big and for how long.</summary>
        public void Shed(Vector3 at, Vector3 speed, float size, float life)
        {
            Mote mote = motes[nextMote];
            nextMote = (nextMote + 1) % motes.Count;
            mote.Live = true;
            mote.Age = 0f;
            mote.Life = life;
            mote.Size = size;
            mote.Speed = speed;
            mote.Turn = Random.Range(0f, 360f);
            mote.Tint = Tints[Random.Range(0, Tints.Length)];
            mote.Body.position = at;
            mote.Body.localScale = Vector3.one * .001f;
            mote.Body.gameObject.SetActive(true);
        }
    }
}
