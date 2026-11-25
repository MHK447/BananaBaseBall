using System.Collections.Generic;
using UnityEngine;

namespace SlingArena.Colosseum
{
    public enum DollState { Idle, Aim, Fly, Tumble, Down, GetUp, Ko }
    public enum WeaponKind { Axe, Sword, Hammer, Bow, Slam, Mine, Bounce, Hook, Boomerang, Star }
    enum Mood { Calm, Nervous, Scream, Hurt, Dizzy, Ko, Grin, Gasp, Panic }

    /// <summary>One toy gladiator: a physics ball with a floppy, procedurally animated body and face.</summary>
    [DisallowMultipleComponent]
    public sealed class ColosseumDoll : MonoBehaviour
    {
        /// <summary>Overall hero size; 1 is the original chunky scale.</summary>
        public const float Scale = .7f;
        public const float Radius = .56f * Scale;
        internal const float FirstSideVolleyDelay = .06f, SideArrowOffset = Radius + .05f;
        internal static Vector3 SideArrowVelocity(Vector3 travelVelocity, Vector3 outward) => outward * 15f + travelVelocity * .18f;
        /// <summary>Visual scale of the whole toy; the model is authored at 1.0.</summary>
        const float Size = 1.3f * Scale;
        const float StandHeight = .75f * Size;
        const float LieHeight = .27f * Size;
        const float IdleDamping = 9f;
        const int ArmL = 0, ArmR = 1, LegL = 2, LegR = 3, Head = 4;
        const float EyeWide = .14f, EyeTall = .155f, PupilWide = .064f, PupilTall = .07f;

        static readonly Vector3[] Anchors =
        {
            new Vector3(-.40f, .17f, 0f), new Vector3(.40f, .17f, 0f),
            new Vector3(-.15f, -.27f, 0f), new Vector3(.15f, -.27f, 0f), new Vector3(0f, .30f, 0f)
        };
        static readonly float[] Lengths = { .44f, .44f, .46f, .46f, .41f };
        // The wrestler is built wider: shoulders and hips set further apart, the head sitting lower between them.
        static readonly Vector3[] WideAnchors =
        {
            new Vector3(-.50f, .20f, 0f), new Vector3(.50f, .20f, 0f),
            new Vector3(-.19f, -.27f, 0f), new Vector3(.19f, -.27f, 0f), new Vector3(0f, .33f, 0f)
        };
        static readonly float[] Floors = { .10f, .10f, .05f, .05f, .38f };

        public int Id { get; private set; }
        public int Team { get; private set; }
        public int Slot { get; private set; }
        public int HeroIndex { get; private set; }
        public WeaponKind Kind { get; private set; }
        public int Attack { get; private set; }
        public int Health { get; private set; }
        public int MaxHealth { get; private set; }
        public int Level { get; private set; }
        public bool Alive { get; private set; }
        public DollState State { get; private set; }
        public float StateTime { get; private set; }
        public Rigidbody Body { get; private set; }
        public SphereCollider Shape { get; private set; }
        public Vector3 PreVelocity { get; private set; }
        public Vector3 Facing { get; set; }
        public Vector3 LookTarget { get; set; }
        public bool Cheering { get; set; }
        bool showcase;
        bool leaping, fixedMouth, touchAndGo, curled, bounced;
        // Per-hero size: the big ones are the same toy drawn larger, with a larger body to bump into.
        float bulk = 1f, scale = Scale, size = Size, standHeight = StandHeight, lieHeight = LieHeight;
        // The size the hero was made at, and how much larger than that it is just now.
        float build = 1f, growth = 1f;
        float rattle;
        bool powered;
        float eyeSize = 1f, pupilSize = 1f;
        // One eye and its pupil against the other: even for everyone but the witch, whose stare is lopsided.
        readonly float[] eyeSkew = { 1f, 1f }, pupilSkew = { 1f, 1f };
        // The witch's long hair: the fall of it behind her, the far end of it now and a frame ago.
        // The witch's long hair: three hanks, one down the middle and one flung out to each side, each of which
        // swings on its own from the back of her head.
        readonly Transform[] manes = new Transform[3];
        readonly Vector3[] maneTip = new Vector3[3], maneWas = new Vector3[3];
        static readonly float[] ManeLength = { 1.2f, 1.32f, 1.03f };
        Quaternion maneYaw = Quaternion.identity;
        bool maneReady;
        // Where the mouth sits, how big it opens and how far it drops as it opens. An animal's mouth is small, sits on
        // the front of its muzzle and opens downwards from under the nose.
        Vector3 mouthHome = new Vector3(0f, -.19f, .345f);
        float mouthSize = 1f, mouthDrop;
        Vector3[] anchors = Anchors;
        public float Bulk { get { return bulk; } }
        /// <summary>How well the ground holds the hero: 1 on sand, down towards 0 on ice (see ColosseumEvents).</summary>
        public float Grip { get; set; } = 1f;
        /// <summary>How much damage a shield will still take for the hero before any reaches its health.</summary>
        public int Shield { get; set; }
        /// <summary>One unit of the model's own measurements, in the world.</summary>
        public float Span { get { return size; } }

        /// <summary>
        /// How much larger than life the hero is just now: the power gate swells it. Everything follows: the model,
        /// the body that others bump into, and how far its weapon reaches.
        /// </summary>
        public float Growth
        {
            get { return growth; }
            set
            {
                float wanted = Mathf.Max(.2f, value);
                if (Mathf.Approximately(wanted, growth)) return;
                growth = wanted;
                Resize();
            }
        }

        void Resize()
        {
            bulk = build * growth;
            scale = Scale * bulk;
            size = Size * bulk;
            standHeight = .75f * size;
            lieHeight = .27f * size;
            Shape.radius = Radius * bulk;
            Shape.center = new Vector3(0f, Radius * bulk, 0f);
            for (int i = 0; i < limbs.Length; i++)
            {
                if (limbs[i] != null) limbs[i].localScale = Vector3.one * size;
            }
            if (head != null) head.localScale = Vector3.one * (size * (fixedMouth ? 1f : 1.1f));
            foreach (Transform hank in manes)
            {
                if (hank != null) hank.localScale = Vector3.one * size;
            }
        }

        /// <summary>
        /// Lights, or puts out, the light round the hero's whole silhouette that shows it is powered up (see
        /// Kit.Powered).
        /// </summary>
        public void SetPowered(bool on)
        {
            if (on == powered) return;
            powered = on;
            foreach (MeshRenderer part in GetComponentsInChildren<MeshRenderer>(true))
            {
                Material[] worn = part.sharedMaterials;
                if (worn.Length == 0 || (worn[0] != Kit.Surface && worn[0] != Kit.PoweredPlain[0])) continue;
                bool inked = worn.Length > 1;
                part.sharedMaterials = on ? (inked ? Kit.Powered : Kit.PoweredPlain) : (inked ? Kit.Inked : Kit.Plain);
            }
        }

        /// <summary>Shakes the hero where it is, for as long as this keeps being called: 1 is as hard as it gets.</summary>
        public void Rattle(float amount) { rattle = Mathf.Max(rattle, Mathf.Clamp01(amount)); }

        /// <summary>
        /// Which way the head stands on the neck, and the top of it that way, for anything stuck on it. This is not
        /// the way the face is turned: a hero looking up at the sky still wears its hat on top.
        /// </summary>
        public Vector3 HeadAxis
        {
            get
            {
                if (curled) return Vector3.up;
                Vector3 axis = tips[Head] - (pivot.position + pivot.rotation * (anchors[Head] * size));
                return axis.sqrMagnitude > 1e-6f ? axis.normalized : Vector3.up;
            }
        }
        public Vector3 Crown { get { return curled ? ball.position + Vector3.up * (.62f * size) : tips[Head] + HeadAxis * (.43f * size); } }
        /// <summary>How much higher than usual the health plate floats: room for whatever is sitting on the head.</summary>
        public float PlateLift { get; set; }
        Vector3 leapFrom, leapTo;
        float leapTime, leapSpan, leapPeak;

        /// <summary>
        /// Look for heroes shown large in the lobby and in portraits: a finer outline that skips the stray inner
        /// lines, no cast shadows, and the weapon held upright at the side instead of out in front.
        /// </summary>
        public void EnterShowcase()
        {
            showcase = true;
            foreach (MeshRenderer part in GetComponentsInChildren<MeshRenderer>(true))
            {
                part.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                // Eyes and other face parts keep the bold outline that makes the face read.
                if (part.sharedMaterials.Length == 2 && part.transform.parent != head)
                    part.sharedMaterials = new[] { Kit.Surface, Kit.Showcase };
            }
        }

        public void ShowWeapon(bool visible) { weapon.gameObject.SetActive(visible); }
        public bool Startled { get; set; }
        /// <summary>True while an opponent's aim line runs through this hero: it sweats, shivers and leans away.</summary>
        public bool Targeted { get; set; }
        public Vector3 AimPull { get { return aimPull; } }
        public int RingStyle { get; set; }
        public bool Grounded { get { return transform.position.y < .07f; } }
        /// <summary>Middle of the head; for a hero curled into a ball, the same height above the ball.</summary>
        public Vector3 HeadPosition { get { return curled ? ball.position + Vector3.up * (.2f * size) : tips[Head]; } }
        /// <summary>Gone out through a gap in the wall: out of the fight, and soon out of sight.</summary>
        public bool Lost { get; private set; }
        /// <summary>Still dropping out of the arena, where it can be seen.</summary>
        public bool Falling { get { return Lost && !gone; } }
        public bool Resting
        {
            get { return Lost || State == DollState.Idle || State == DollState.Down || State == DollState.Ko || State == DollState.GetUp; }
        }

        readonly Dictionary<int, float> cooldowns = new Dictionary<int, float>();
        readonly List<int> cooldownKeys = new List<int>();
        readonly Vector3[] tips = new Vector3[5];
        readonly Vector3[] previousTips = new Vector3[5];
        readonly Vector3[] sprawl = new Vector3[4];
        readonly Transform[] limbs = new Transform[4];
        readonly Transform[] eyes = new Transform[2];
        readonly Transform[] pupils = new Transform[2];
        readonly Transform[] glints = new Transform[2];
        readonly Transform[] brows = new Transform[2];
        readonly Transform[] crosses = new Transform[2];
        readonly Transform[] stars = new Transform[3];
        readonly Transform[] sweat = new Transform[4];
        static Mesh sweatMesh, starMesh;
        float trailLeft;
        float dread;
        readonly Vector3[] eyeHome = new Vector3[2];
        readonly Vector3[] browHome = new Vector3[2];

        ColosseumGame game;
        Transform pivot, head, weapon, mouth, teeth, tongue, smile, ring, shade, ball, ballSpin;
        MeshRenderer ringRenderer;
        Quaternion tumble = Quaternion.identity, lie = Quaternion.identity, headTurn = Quaternion.identity;
        Vector3 spinAxis = Vector3.right, aimPull, lastHead, pupilDrift, dart;
        float spinSpeed, flatSpin, flatAngle, roll, squash, aimPower, arrowTimer, blink, dartTimer, slowTime, wobble;
        float perchTime;   // how long it has lain still up on something that is not the ground
        bool faceUp = true, tipsReady, tipsLooked;
        // Falling out of the arena: placed by hand, like a leap, until the ground hides the hero.
        const float FallSpan = 1.1f;
        Vector3 fallSpeed;
        float fallTime;
        bool gone;

        public void Setup(ColosseumGame owner, int id, int team, int slot, int heroIndex, int level, PhysicsMaterial surface)
        {
            game = owner;
            HeroIndex = heroIndex;
            WeaponKind kind = ColosseumRoster.Heroes[heroIndex].Weapon;
            Id = id; Team = team; Slot = slot; Kind = kind;
            Level = Mathf.Clamp(level, 1, ColosseumRoster.MaxLevel);
            var stats = ColosseumUnitInfo.For(kind, Level);
            Attack = stats.Attack;
            Health = MaxHealth = stats.Health;
            Alive = true;
            Facing = team == 0 ? Vector3.forward : Vector3.back;
            wobble = Random.value * 10f;
            blink = Random.Range(1f, 4f);

            Body = gameObject.AddComponent<Rigidbody>();
            Body.mass = kind == WeaponKind.Slam ? 1.6f : kind == WeaponKind.Hammer ? 1.35f : kind == WeaponKind.Bow ? .85f : kind == WeaponKind.Mine ? .8f
                : kind == WeaponKind.Bounce ? 1.25f : kind == WeaponKind.Hook ? 1.2f : kind == WeaponKind.Boomerang ? 1.15f : kind == WeaponKind.Star ? .9f : 1f;
            Body.constraints = RigidbodyConstraints.FreezeRotation;
            Body.interpolation = RigidbodyInterpolation.Interpolate;
            Body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            Body.linearDamping = IdleDamping;
            Shape = gameObject.AddComponent<SphereCollider>();
            // The orc is big, the goblin small, the porcupine a little stout, the pirate burly, the gladiator brawny,
            // the witch slight.
            build = kind == WeaponKind.Slam ? 1.3f : kind == WeaponKind.Mine ? .88f : kind == WeaponKind.Bounce ? 1.06f : kind == WeaponKind.Hook ? 1.1f
                : kind == WeaponKind.Boomerang ? 1.12f : kind == WeaponKind.Star ? .96f : 1f;
            anchors = kind == WeaponKind.Slam ? WideAnchors : Anchors;
            growth = 1f;
            Resize();
            Shape.sharedMaterial = surface;
            BuildBody();
            EnterState(DollState.Idle);
        }

        // ------------------------------------------------------------------ model

        static Color32 C(int hex, float gloss) { return MeshKit.Rgb(hex, gloss); }

        /// <summary>Puffy five-point star for the dizzy halo, with shared edges so the ink outline wraps it cleanly.</summary>
        static Mesh StarMesh()
        {
            if (starMesh != null)
                return starMesh;
            const float thick = .2f, bulge = .42f;
            var points = new List<Vector3>();
            var normals = new List<Vector3>();
            var tints = new List<Color32>();
            var faces = new List<int>();
            Color32 heart = C(0xfff2a0, 1f), rim = C(0xffc928, 1f);
            points.Add(new Vector3(0f, 0f, -bulge)); normals.Add(Vector3.back); tints.Add(heart);
            points.Add(new Vector3(0f, 0f, bulge)); normals.Add(Vector3.forward); tints.Add(heart);
            for (int i = 0; i < 10; i++)
            {
                float angle = Mathf.PI * .5f + i * Mathf.PI / 5f, reach = (i & 1) == 0 ? 1f : .5f;
                var spoke = new Vector3(Mathf.Cos(angle) * reach, Mathf.Sin(angle) * reach, 0f);
                points.Add(spoke + Vector3.back * thick); normals.Add((spoke.normalized + Vector3.back * .7f).normalized); tints.Add(rim);
                points.Add(spoke + Vector3.forward * thick); normals.Add((spoke.normalized + Vector3.forward * .7f).normalized); tints.Add(rim);
            }
            for (int i = 0; i < 10; i++)
            {
                int front = 2 + i * 2, back = front + 1, nextFront = 2 + ((i + 1) % 10) * 2, nextBack = nextFront + 1;
                Facet(points, faces, 0, front, nextFront, Vector3.back);
                Facet(points, faces, 1, back, nextBack, Vector3.forward);
                Vector3 outward = points[front] + points[nextFront];
                outward.z = 0f;
                Facet(points, faces, front, back, nextBack, outward);
                Facet(points, faces, front, nextBack, nextFront, outward);
            }
            starMesh = Kit.Own(new Mesh { name = "Colosseum dizzy star", hideFlags = HideFlags.DontSave });
            starMesh.SetVertices(points);
            starMesh.SetNormals(normals);
            starMesh.SetColors(tints);
            starMesh.SetTriangles(faces, 0);
            starMesh.RecalculateBounds();
            return starMesh;
        }

        static void Facet(List<Vector3> points, List<int> faces, int a, int b, int c, Vector3 outward)
        {
            bool flip = Vector3.Dot(Vector3.Cross(points[b] - points[a], points[c] - points[a]), outward) < 0f;
            faces.Add(a); faces.Add(flip ? c : b); faces.Add(flip ? b : c);
        }

        /// <summary>Cartoon bead of sweat: pointed at the top, fat at the bottom, with a white glint on the bulb.</summary>
        static Mesh SweatMesh()
        {
            if (sweatMesh != null)
                return sweatMesh;
            var kit = new MeshKit { Gradient = .22f };
            Color32 water = C(0x6fd0ff, 1f), glint = C(0xffffff, 1f);
            kit.Ball(new Vector3(0f, -.22f, 0f), .5f, water, 12);
            kit.Capsule(new Vector3(0f, -.08f, 0f), new Vector3(0f, .78f, 0f), .44f, .035f, water, 12);
            kit.Blob(new Vector3(-.17f, -.2f, .4f), new Vector3(.12f, .2f, .09f), Quaternion.Euler(0f, 0f, 18f), glint, 1f, 1f, 6);
            kit.Ball(new Vector3(.14f, -.42f, .4f), .06f, glint, 5);
            sweatMesh = Kit.Own(kit.ToMesh("Colosseum sweat drop"));
            return sweatMesh;
        }

        static Vector2[] Oval(float wide, float tall, int steps, float square = 1f)
        {
            var rim = new Vector2[steps];
            for (int i = 0; i < steps; i++)
            {
                float a = (float)i / steps * Mathf.PI * 2f, c = Mathf.Cos(a), s = Mathf.Sin(a);
                rim[i] = new Vector2(Mathf.Sign(c) * Mathf.Pow(Mathf.Abs(c), square) * wide, Mathf.Sign(s) * Mathf.Pow(Mathf.Abs(s), square) * tall);
            }
            return rim;
        }

        /// <summary>Leaf or feather outline, pointed at both ends and fullest a little above the middle.</summary>
        static Vector2[] Leaf(float wide, float tall, int steps = 7)
        {
            var rim = new List<Vector2>();
            for (int i = 0; i <= steps; i++)
                rim.Add(new Vector2(wide * Mathf.Pow(Mathf.Max(0f, Mathf.Sin((float)i / steps * Mathf.PI)), .7f), Mathf.Lerp(tall, -tall, (float)i / steps)));
            for (int i = steps - 1; i >= 1; i--)
                rim.Add(new Vector2(-rim[i].x, rim[i].y));
            return rim.ToArray();
        }

        /// <summary>Brow with a heavy inner end tapering to a raised tail; side is -1 for the left eye.</summary>
        static Mesh BrowMesh(float side, int hex)
        {
            Color32 hair = C(hex, 0f);
            var kit = new MeshKit { Gradient = .1f };
            kit.Blob(new Vector3(-side * .22f, 0f, 0f), new Vector3(.8f, 1f, 1f), Quaternion.identity, hair, 1f, 1f, 12);
            kit.Blob(new Vector3(side * .42f, .22f, 0f), new Vector3(.62f, .62f, .9f), Quaternion.identity, hair, 1f, 1f, 10);
            return kit.ToMesh("Doll brow");
        }

        /// <summary>
        /// Turned helmet shell: an egg-shaped dome with crisp steps, a flared band at brow height that stands in
        /// for the brim, and a small cap on the crown.
        /// </summary>
        static void Helm(MeshKit kit, Vector3 center, Vector3 radii, Color32 shell, Color32 shade, Color32 band, Color32 bandLight)
        {
            const float brow = .37f;
            var profile = new List<Vector2>();
            var colors = new List<Color32>();
            void Add(float r, float h, Color32 c) { profile.Add(new Vector2(r * radii.x, h * radii.y)); colors.Add(c); }
            float Round(float h) { return Mathf.Sqrt(Mathf.Max(0f, 1f - h * h)); }
            for (int i = 0; i <= 5; i++)
            {
                float h = Mathf.Lerp(-.86f, brow - .08f, i / 5f);
                Add(Round(h), h, shade);
            }
            Add(Round(brow - .08f), brow - .08f, band);
            Add(Round(brow - .08f) + .05f, brow - .06f, band);
            Add(Round(brow) + .08f, brow + .02f, bandLight);
            Add(Round(brow) + .08f, brow + .02f, band);
            Add(Round(brow + .08f) + .035f, brow + .09f, band);
            Add(Round(brow + .08f), brow + .09f, band);
            for (int i = 0; i <= 5; i++)
            {
                float h = Mathf.Lerp(brow + .09f, .955f, i / 5f);
                Add(Round(h), h, shell);
            }
            Add(Round(.955f), .955f, band);
            Add(Round(.955f) + .02f, 1.0f, bandLight);
            Add(.12f, 1.05f, bandLight);
            Add(0f, 1.06f, bandLight);
            kit.Lathe(center, Quaternion.identity, profile.ToArray(), colors.ToArray(), 28, radii.z / radii.x);
        }

        /// <summary>Shoulder guard of three overlapping lames with a bright rim, leaning out over the arm.</summary>
        static void Pauldron(MeshKit kit, float side, float size, Color32 plate, Color32 shade, Color32 edge)
        {
            Vector2[] shape =
            {
                new Vector2(1f, 0f), new Vector2(1.05f, .05f), new Vector2(1.05f, .15f), new Vector2(.97f, .2f), new Vector2(.97f, .2f),
                new Vector2(.9f, .46f), new Vector2(.9f, .46f), new Vector2(.79f, .5f), new Vector2(.79f, .5f), new Vector2(.62f, .78f),
                new Vector2(.62f, .78f), new Vector2(.5f, .82f), new Vector2(.5f, .82f), new Vector2(.28f, .98f), new Vector2(0f, 1.02f)
            };
            for (int i = 0; i < shape.Length; i++) shape[i] *= size;
            kit.Lathe(new Vector3(side * .37f, .14f, 0f), Quaternion.Euler(0f, 0f, -side * 32f), shape,
                new[] { edge, edge, edge, edge, plate, plate, shade, shade, plate, plate, shade, shade, plate, plate, plate }, 20);
        }

        /// <summary>Guard around a limb (which runs along Z) with a flange standing out at each end.</summary>
        static void Cuff(MeshKit kit, Vector3 center, float radius, float length, Color32 body, Color32 edge)
        {
            float half = length * .5f;
            kit.Lathe(center, Quaternion.Euler(90f, 0f, 0f), new[]
            {
                new Vector2(radius - .012f, -half), new Vector2(radius + .012f, -half + .006f), new Vector2(radius + .012f, -half + .03f),
                new Vector2(radius, -half + .036f), new Vector2(radius, -half + .036f), new Vector2(radius - .002f, half - .036f),
                new Vector2(radius - .002f, half - .036f), new Vector2(radius + .012f, half - .03f), new Vector2(radius + .012f, half - .006f),
                new Vector2(radius - .012f, half)
            }, new[] { edge, edge, edge, edge, body, body, edge, edge, edge, edge }, 16);
        }

        /// <summary>Cheek piece hanging from a helmet rim: a cut plate, wide at the top and drawn to a point by the jaw.</summary>
        static void CheekPiece(MeshKit kit, float side, Color32 face, Color32 rim, Color32 edge)
        {
            kit.Cut(new Vector3(side * .415f, -.06f, .06f), Quaternion.Euler(0f, 90f, 0f), new[]
            {
                new Vector2(.12f, .2f), new Vector2(-.14f, .2f), new Vector2(-.17f, 0f), new Vector2(-.07f, -.2f), new Vector2(.1f, -.13f)
            }, .07f, .03f, face, rim, edge);
        }

        /// <summary>
        /// Orc wrestler's trunk: a broad bare chest over a big belly, trunks in the team colour, a
        /// champion's belt with a jewelled plate and one spiked shoulder guard. He carries no weapon.
        /// </summary>
        static void OrcTorso(MeshKit kit, MeshKit trim, Color32 skin, Color32 main, Color32 dark, Color32 light, Color32 gold, Color32 goldLight, Color32 gem)
        {
            Quaternion none = Quaternion.identity;
            Color32 belly = MeshKit.Mix(skin, C(0xc8e48a, .15f), .45f), hide = C(0x4a3424, .15f), iron = C(0x7f8fa8, .8f), ironLight = C(0xdfe7f2, 1f);
            kit.Blob(new Vector3(0f, .04f, 0f), new Vector3(.45f, .36f, .33f), none, skin, .85f, .9f, 26);
            kit.Blob(new Vector3(0f, -.1f, .06f), new Vector3(.41f, .3f, .33f), none, belly, 1f, 1f, 22);
            for (int s = -1; s <= 1; s += 2)
                kit.Blob(new Vector3(s * .19f, .17f, .2f), new Vector3(.2f, .13f, .13f), none, skin, 1f, 1f, 14);
            // Wrestling trunks in the team colour.
            kit.Blob(new Vector3(0f, -.27f, 0f), new Vector3(.44f, .2f, .35f), none, main, .8f, .9f, 22);
            // Champion's belt.
            kit.Blob(new Vector3(0f, -.2f, .01f), new Vector3(.462f, .075f, .366f), none, hide, .4f, .9f, 24);
            trim.Chamfer(new Vector3(0f, -.2f, .375f), new Vector3(.34f, .23f, .06f), none, .035f, gold, goldLight);
            trim.Chamfer(new Vector3(0f, -.2f, .4f), new Vector3(.2f, .14f, .04f), Quaternion.Euler(0f, 0f, 45f), .02f, goldLight, C(0xfff2b8, 1f));
            trim.Gem(new Vector3(0f, -.2f, .42f), Quaternion.Euler(90f, 0f, 0f), new Vector3(.06f, .035f, .06f), gem, C(0xffffff, 1f));
            for (int s = -1; s <= 1; s += 2)
            {
                trim.Chamfer(new Vector3(s * .27f, -.2f, .3f), new Vector3(.13f, .15f, .05f), Quaternion.Euler(0f, s * 38f, 0f), .02f, gold, goldLight);
                trim.Ball(new Vector3(s * .4f, -.2f, .16f), .03f, goldLight, 6);
            }
            // One spiked shoulder guard on the right.
            Pauldron(kit, 1f, .25f, iron, C(0x5e6c84, .7f), ironLight);
            for (int i = 0; i < 3; i++)
            {
                float turn = (i - 1) * 62f * Mathf.Deg2Rad;
                var foot = new Vector3(.45f + Mathf.Cos(turn) * .1f, .3f, Mathf.Sin(turn) * .16f);
                kit.Spike(foot, foot + new Vector3(.13f + Mathf.Cos(turn) * .05f, .17f, Mathf.Sin(turn) * .1f), .06f, ironLight, C(0xffffff, 1f));
            }
        }

        /// <summary>Orc wrestler's limbs: thick bare arms with taped wrists and big fists, laced boots and knee pads.</summary>
        static void OrcLimb(MeshKit kit, MeshKit trim, bool arm, Color32 skin, Color32 main, Color32 dark, Color32 light, Color32 cream)
        {
            Quaternion none = Quaternion.identity;
            if (arm)
            {
                kit.Ball(Vector3.zero, .17f, skin, 14);
                kit.Capsule(Vector3.zero, new Vector3(0f, 0f, .34f), .135f, .12f, skin, 14);
                kit.Blob(new Vector3(0f, .03f, .14f), new Vector3(.13f, .13f, .13f), none, skin, 1f, 1f, 12);
                Cuff(kit, new Vector3(0f, 0f, .3f), .125f, .13f, cream, C(0xffffff, .2f));
                kit.Ball(new Vector3(0f, 0f, .46f), .16f, skin, 14);
                kit.Ball(new Vector3(0f, .13f, .47f), .065f, skin, 8);
                for (int k = -1; k <= 1; k++)
                    trim.Ball(new Vector3(k * .07f, .02f, .6f), .045f, MeshKit.Mix(skin, C(0x2f6a2c, .1f), .25f), 6);
            }
            else
            {
                kit.Ball(Vector3.zero, .15f, dark, 12);
                kit.Capsule(Vector3.zero, new Vector3(0f, 0f, .28f), .14f, .12f, skin, 12);
                Cuff(kit, new Vector3(0f, .012f, .17f), .135f, .13f, dark, light);
                kit.Blob(new Vector3(0f, .02f, .31f), new Vector3(.135f, .14f, .09f), none, main, .8f, .9f, 12);
                kit.Blob(new Vector3(0f, .05f, .385f), new Vector3(.132f, .178f, .1f), none, main, .7f, .7f, 14);
                kit.Blob(new Vector3(0f, .056f, .455f), new Vector3(.138f, .185f, .032f), none, C(0x2a2030, .1f), .7f, .7f, 12);
                for (int k = 0; k < 3; k++)
                    trim.Capsule(new Vector3(-.05f, .15f + k * .012f, .28f + k * .05f), new Vector3(.05f, .15f + k * .012f, .28f + k * .05f), .013f, .013f, cream, 6);
            }
        }

        /// <summary>Orc head: heavy jaw with tusks, pointed ears, a flat nose and a crest of hair in the team colour.</summary>
        /// <summary>
        /// A porcupine's quill: a long, sharp spike standing out from a round body, dark with a pale tip. Yaw is
        /// measured round from straight back, pitch up from level; the quill starts `from` out from the centre.
        /// </summary>
        static void Quill(MeshKit kit, MeshKit detail, Vector3 centre, float yaw, float pitch, float from, float length, float radius,
            Color32 dark, Color32 mid, Color32 pale)
        {
            float turn = yaw * Mathf.Deg2Rad, rise = pitch * Mathf.Deg2Rad;
            var away = new Vector3(Mathf.Sin(turn) * Mathf.Cos(rise), Mathf.Sin(rise), -Mathf.Cos(turn) * Mathf.Cos(rise));
            Quill(kit, detail, centre, away, from, length, radius, dark, mid, pale);
        }

        static void Quill(MeshKit kit, MeshKit detail, Vector3 centre, Vector3 away, float from, float length, float radius,
            Color32 dark, Color32 mid, Color32 pale)
        {
            Vector3 foot = centre + away * from, tip = foot + away * length;
            kit.Spike(foot, tip, radius, dark, mid, 6);
            detail.Spike(Vector3.Lerp(foot, tip, .6f), tip + away * .012f, radius * .46f, pale, pale, 6);
        }

        /// <summary>
        /// The porcupine curled up for a leap: a ball of fur with quills standing out all the way round. Its face
        /// peeps out at the front between its ears, above the neckerchief, so the spin can be read.
        /// </summary>
        static void QuillBall(MeshKit kit, MeshKit detail, Color32 main, Color32 light)
        {
            Quaternion none = Quaternion.identity;
            Color32 fur = C(FurHex, .12f), hide = C(HideHex, .15f), hideLight = C(HideLightHex, .2f), pale = C(QuillTipHex, .3f);
            Color32 ink = C(0x2e1e22, .25f), white = C(0xffffff, .5f), black = C(0x18121a, .9f), pink = C(PinkHex, .3f);
            kit.Blob(Vector3.zero, new Vector3(.47f, .47f, .47f), none, fur, 1f, 1f, 24);
            kit.Blob(new Vector3(0f, .03f, .27f), new Vector3(.28f, .3f, .23f), none, C(FaceHex, .15f), 1f, 1f, 16);
            kit.Blob(new Vector3(0f, -.23f, .3f), new Vector3(.25f, .075f, .2f), none, main, 1f, 1f, 14);
            detail.Ball(new Vector3(-.08f, -.235f, .485f), .026f, light, 6);
            detail.Ball(new Vector3(.08f, -.235f, .485f), .026f, light, 6);
            for (int side = -1; side <= 1; side += 2)
            {
                // Round ears, wide eyes with big pupils (white with black, like every hero's) and rosy cheeks.
                kit.Ball(new Vector3(side * .215f, .3f, .3f), .085f, fur, 10);
                detail.Blob(new Vector3(side * .215f, .3f, .375f), new Vector3(.05f, .05f, .022f), none, pink, 1f, 1f, 8);
                detail.Blob(new Vector3(side * .112f, .12f, .452f), new Vector3(.094f, .104f, .03f), none, ink, 1f, 1f, 12);
                detail.Blob(new Vector3(side * .112f, .12f, .467f), new Vector3(.08f, .09f, .03f), none, white, 1f, 1f, 12);
                detail.Blob(new Vector3(side * .1f, .112f, .494f), new Vector3(.046f, .053f, .012f), none, black, 1f, 1f, 8);
                detail.Ball(new Vector3(side * .1f + .016f, .132f, .507f), .012f, white, 6);
                detail.Blob(new Vector3(side * .205f, -.02f, .405f), new Vector3(.05f, .032f, .02f), none, pink, 1f, 1f, 8);
            }
            detail.Blob(new Vector3(0f, -.04f, .465f), new Vector3(.1f, .075f, .075f), none, C(MuzzleHex, .25f), 1f, 1f, 10);
            detail.Blob(new Vector3(0f, -.015f, .54f), new Vector3(.045f, .035f, .035f), none, C(NoseHex, .9f), 1f, 1f, 8);
            const int count = 34;
            for (int i = 0; i < count; i++)
            {
                // Evenly spread over the ball, leaving the face clear.
                float y = 1f - (i + .5f) * 2f / count, ring = Mathf.Sqrt(1f - y * y), turn = i * 2.39996f;
                var away = new Vector3(Mathf.Cos(turn) * ring, y, Mathf.Sin(turn) * ring);
                if (away.z > .6f) continue;
                Quill(kit, detail, Vector3.zero, away, .36f, .52f, .11f, hide, hideLight, pale);
            }
        }

        // The porcupine is an animal, not a person in a costume: brown fur, a cream face and belly, pink ears and
        // paws, and nothing on but a neckerchief in the team colour.
        const int FurHex = 0x7a5236, HideHex = 0x5a3d2b, HideLightHex = 0x7d573a, QuillTipHex = 0xfff0d0, PinkHex = 0xffa9a0;
        const int FaceHex = 0xfff1d8, MuzzleHex = 0xfffaf0, NoseHex = 0x3b2630;

        /// <summary>
        /// The porcupine's body: a plump pear of fur with a cream belly and a stub of a tail, its back bristling with
        /// long quills, and a neckerchief knotted at the side.
        /// </summary>
        static void PorcupineTorso(MeshKit kit, MeshKit trim, Color32 main, Color32 light)
        {
            Quaternion none = Quaternion.identity;
            Color32 fur = C(FurHex, .12f), hide = C(HideHex, .15f), hideLight = C(HideLightHex, .2f), pale = C(QuillTipHex, .3f);
            kit.Blob(new Vector3(0f, -.08f, -.02f), new Vector3(.40f, .44f, .34f), none, fur, 1f, 1f, 24);
            kit.Blob(new Vector3(0f, -.11f, .1f), new Vector3(.31f, .37f, .27f), none, C(FaceHex, .15f), 1f, 1f, 20);
            trim.Blob(new Vector3(0f, -.14f, .285f), new Vector3(.19f, .22f, .1f), none, C(MuzzleHex, .2f), 1f, 1f, 14);
            kit.Spike(new Vector3(0f, -.38f, -.27f), new Vector3(0f, -.45f, -.5f), .085f, fur, hideLight, 6);

            var back = new Vector3(0f, -.02f, -.06f);
            float[][] rows =
            {
                new[] { 62f, -40f, 0f, 40f }, new[] { 30f, -72f, -36f, 0f, 36f, 72f },
                new[] { 0f, -54f, -18f, 18f, 54f }, new[] { -30f, -36f, 0f, 36f }
            };
            foreach (float[] row in rows)
            {
                for (int i = 1; i < row.Length; i++)
                    Quill(kit, trim, back, row[i], row[0], .31f, .44f, .095f, hide, hideLight, pale);
            }

            // Neckerchief: a band round the neck, a point hanging down the chest with pale spots, and a knot at
            // the side with its two ends standing out.
            kit.Blob(new Vector3(0f, .3f, 0f), new Vector3(.3f, .085f, .27f), none, main, 1f, 1f, 18);
            kit.Capsule(new Vector3(0f, .25f, .19f), new Vector3(0f, 0f, .375f), .17f, .035f, main, 12);
            trim.Ball(new Vector3(-.099f, .224f, .312f), .03f, light, 8);
            trim.Ball(new Vector3(.099f, .224f, .312f), .03f, light, 8);
            trim.Ball(new Vector3(0f, .153f, .373f), .028f, light, 8);
            kit.Ball(new Vector3(-.27f, .29f, .1f), .075f, main, 10);
            kit.Capsule(new Vector3(-.3f, .29f, .09f), new Vector3(-.52f, .24f, 0f), .052f, .02f, main, 8);
            kit.Capsule(new Vector3(-.3f, .27f, .1f), new Vector3(-.47f, .12f, .12f), .052f, .02f, main, 8);
        }

        /// <summary>The porcupine's limbs: short furry arms and legs ending in round cream paws with three toes.</summary>
        static void PorcupineLimb(MeshKit kit, bool arm)
        {
            Color32 fur = C(FurHex, .12f), paw = C(FaceHex, .15f);
            if (arm)
            {
                kit.Ball(Vector3.zero, .118f, fur, 12);
                kit.Capsule(Vector3.zero, new Vector3(0f, 0f, .25f), .108f, .1f, fur, 12);
                kit.Blob(new Vector3(0f, 0f, .33f), new Vector3(.112f, .112f, .1f), Quaternion.identity, paw, 1f, 1f, 12);
                for (int toe = -1; toe <= 1; toe++)
                    kit.Ball(new Vector3(toe * .062f, .025f, .405f), .042f, paw, 8);
                return;
            }
            // The leg is mostly hidden in the fur of the belly; what shows is a long flat foot, toes forward.
            kit.Ball(Vector3.zero, .125f, fur, 10);
            kit.Capsule(Vector3.zero, new Vector3(0f, 0f, .3f), .125f, .115f, fur, 12);
            kit.Blob(new Vector3(0f, .07f, .4f), new Vector3(.128f, .19f, .07f), Quaternion.identity, paw, 1f, 1f, 14);
            for (int toe = -1; toe <= 1; toe++)
                kit.Ball(new Vector3(toe * .075f, .225f, .4f), .05f, paw, 8);
        }

        /// <summary>How far forward the front of the porcupine's muzzle is at a point on the face.</summary>
        static float MuzzleFront(float x, float y)
        {
            float inside = 1f - x * x / (.135f * .135f) - (y + .15f) * (y + .15f) / (.1f * .1f);
            return .385f + .125f * Mathf.Sqrt(Mathf.Max(0f, inside));
        }

        /// <summary>
        /// The porcupine's head: a hood of fur round a cream, heart-shaped face, round ears with pink insides, a short
        /// muzzle with a shiny button nose, and long quills standing up from the hood with a tuft over the brow.
        /// </summary>
        static void PorcupineHead(MeshKit kit, MeshKit detail)
        {
            Quaternion none = Quaternion.identity;
            Color32 fur = C(FurHex, .12f), hide = C(HideHex, .15f), hideLight = C(HideLightHex, .2f), pale = C(QuillTipHex, .3f);
            kit.Blob(new Vector3(0f, .1f, -.09f), new Vector3(.45f, .385f, .41f), none, fur, 1f, 1f, 24);
            for (int s = -1; s <= 1; s += 2)
            {
                // Fur down the temples makes the face a heart; the ears sit high and wide in front of the quills.
                kit.Blob(new Vector3(s * .365f, .15f, 0f), new Vector3(.1f, .2f, .19f), none, fur, 1f, 1f, 12);
                kit.Ball(new Vector3(s * .31f, .31f, .1f), .115f, fur, 12);
                detail.Blob(new Vector3(s * .305f, .305f, .195f), new Vector3(.068f, .068f, .03f), none, C(PinkHex, .3f), 1f, 1f, 10);
            }
            kit.Blob(new Vector3(0f, .35f, .17f), new Vector3(.085f, .075f, .1f), none, fur, 1f, 1f, 10);
            detail.Blob(new Vector3(0f, -.15f, .385f), new Vector3(.135f, .1f, .125f), none, C(MuzzleHex, .25f), 1f, 1f, 14);
            detail.Blob(new Vector3(0f, -.108f, .502f), new Vector3(.066f, .05f, .052f), none, C(NoseHex, .9f), 1f, 1f, 10);
            detail.Ball(new Vector3(-.022f, -.086f, .546f), .015f, C(0xffffff, 1f), 6);

            var crown = new Vector3(0f, .08f, -.06f);
            float[][] rows = { new[] { 74f, -50f, 0f, 50f }, new[] { 46f, -78f, -40f, 0f, 40f, 78f }, new[] { 14f, -62f, -22f, 22f, 62f } };
            foreach (float[] row in rows)
            {
                for (int i = 1; i < row.Length; i++)
                    Quill(kit, detail, crown, row[i], row[0], .34f, .5f, .105f, hide, hideLight, pale);
            }
            Quill(kit, detail, crown, 180f, 62f, .34f, .36f, .09f, hide, hideLight, pale);
            Quill(kit, detail, crown, 148f, 56f, .34f, .27f, .08f, hide, hideLight, pale);
            Quill(kit, detail, crown, 212f, 56f, .34f, .27f, .08f, hide, hideLight, pale);
        }

        /// <summary>
        /// The pirate's body: a long coat in the team colour, open over a pale shirt, with gold buttons down the
        /// lapels and fringed epaulettes; a broad belt with a square buckle; the skirts of the coat hanging below it.
        /// </summary>
        static void PirateTorso(MeshKit kit, MeshKit trim, Color32 main, Color32 dark, Color32 light, Color32 gold, Color32 goldLight)
        {
            Quaternion none = Quaternion.identity;
            Color32 shirt = C(0xf6ead2, .1f), belt = C(0x3a2a24, .25f), beltLight = C(0x5e4638, .25f);
            kit.Blob(Vector3.zero, new Vector3(.34f, .35f, .262f), none, main, .8f, .8f, 24);
            kit.Blob(new Vector3(0f, .07f, .12f), new Vector3(.19f, .27f, .165f), none, shirt, .9f, .9f, 16);
            for (int s = -1; s <= 1; s += 2)
            {
                kit.Chamfer(new Vector3(s * .185f, .09f, .222f), new Vector3(.1f, .4f, .06f), Quaternion.Euler(0f, s * 24f, s * -13f), .024f, dark, main);
                for (int i = 0; i < 3; i++)
                    trim.Ball(new Vector3(s * (.215f - i * .024f), .2f - i * .115f, .268f + i * .006f), .027f, goldLight, 8);
                kit.Blob(new Vector3(s * .355f, .3f, 0f), new Vector3(.16f, .05f, .17f), Quaternion.Euler(0f, 0f, s * -12f), gold, .8f, .9f, 14);
                for (int i = 0; i < 5; i++)
                {
                    float a = Mathf.Lerp(-70f, 70f, i / 4f) * Mathf.Deg2Rad;
                    trim.Ball(new Vector3(s * (.39f + Mathf.Cos(a) * .12f), .255f, Mathf.Sin(a) * .15f), .03f, goldLight, 6);
                }
                kit.Blob(new Vector3(s * .2f, -.38f, -.04f), new Vector3(.18f, .2f, .24f), none, main, .8f, .8f, 14);
            }
            kit.Blob(new Vector3(0f, -.38f, -.2f), new Vector3(.27f, .2f, .12f), none, dark, .8f, .8f, 14);
            kit.Blob(new Vector3(0f, .3f, 0f), new Vector3(.215f, .08f, .195f), none, light, 1f, 1f, 16);
            trim.Blob(new Vector3(0f, .235f, .255f), new Vector3(.085f, .07f, .05f), none, shirt, 1f, 1f, 10);
            kit.Blob(new Vector3(0f, -.17f, 0f), new Vector3(.355f, .072f, .275f), none, belt, .4f, .8f, 20);
            trim.Chamfer(new Vector3(0f, -.17f, .278f), new Vector3(.2f, .16f, .055f), none, .024f, gold, goldLight);
            trim.Chamfer(new Vector3(0f, -.17f, .3f), new Vector3(.105f, .075f, .03f), none, .012f, belt, beltLight);
        }

        /// <summary>
        /// The pirate's limbs. Coat sleeves with deep cuffs: the right ends in a brass socket for the hook, the left in
        /// a hand holding a blunderbuss, brass-barrelled with a bell mouth. One sea boot, and one wooden peg.
        /// </summary>
        static void PirateLimb(MeshKit kit, MeshKit trim, int limb, Color32 skin, Color32 main, Color32 light, Color32 gold, Color32 goldLight)
        {
            Quaternion none = Quaternion.identity;
            Color32 wood = C(Kit.Wood, .15f), woodDark = C(0x5e3a22, .15f), iron = C(0x66728a, .8f), boot = C(0x4a3226, .2f), sole = C(0x2e1e16, .1f);
            Color32 trousers = C(0x463c4e, .1f), bore = C(0x241c22, .1f);
            if (limb < 2)
            {
                kit.Ball(Vector3.zero, .13f, main, 12);
                kit.Capsule(Vector3.zero, new Vector3(0f, 0f, .3f), .108f, .104f, main, 12);
                kit.Blob(new Vector3(0f, 0f, .31f), new Vector3(.135f, .135f, .075f), none, light, .7f, .9f, 14);
                trim.Ball(new Vector3(0f, .13f, .31f), .026f, goldLight, 6);
                if (limb == ArmR)
                {
                    kit.Blob(new Vector3(0f, 0f, .41f), new Vector3(.105f, .105f, .07f), none, gold, .7f, 1f, 12);
                    return;
                }
                kit.Ball(new Vector3(0f, 0f, .45f), .125f, skin, 12);
                kit.Ball(new Vector3(0f, .1f, .47f), .054f, skin, 8);
                kit.Capsule(new Vector3(0f, -.03f, .3f), new Vector3(0f, .02f, .52f), .05f, .06f, wood, 10);
                kit.Capsule(new Vector3(0f, .03f, .5f), new Vector3(0f, .03f, .9f), .058f, .062f, gold, 12);
                kit.Lathe(new Vector3(0f, .03f, .88f), Quaternion.Euler(90f, 0f, 0f), new[]
                {
                    new Vector2(.062f, 0f), new Vector2(.068f, .045f), new Vector2(.088f, .095f), new Vector2(.128f, .134f), new Vector2(.134f, .14f),
                    new Vector2(.112f, .142f), new Vector2(.112f, .142f), new Vector2(0f, .12f)
                }, new[] { gold, gold, gold, goldLight, goldLight, goldLight, bore, bore }, 16);
                trim.Blob(new Vector3(0f, .095f, .52f), new Vector3(.03f, .045f, .05f), none, iron, 1f, 1f, 8);
                trim.Blob(new Vector3(0f, .03f, .62f), new Vector3(.066f, .066f, .02f), none, goldLight, 1f, 1f, 10);
                return;
            }
            kit.Ball(Vector3.zero, .13f, trousers, 10);
            if (limb == LegL)
            {
                // A wooden peg from the knee down, shod with iron.
                kit.Capsule(Vector3.zero, new Vector3(0f, 0f, .2f), .122f, .112f, trousers, 12);
                kit.Blob(new Vector3(0f, 0f, .215f), new Vector3(.122f, .122f, .04f), none, woodDark, 1f, 1f, 12);
                kit.Capsule(new Vector3(0f, 0f, .23f), new Vector3(0f, 0f, .43f), .075f, .048f, wood, 10);
                kit.Blob(new Vector3(0f, 0f, .445f), new Vector3(.058f, .058f, .022f), none, iron, 1f, 1f, 10);
                return;
            }
            // A sea boot with its top turned down.
            kit.Capsule(Vector3.zero, new Vector3(0f, 0f, .3f), .122f, .108f, trousers, 12);
            kit.Blob(new Vector3(0f, .01f, .24f), new Vector3(.15f, .15f, .06f), none, boot, .8f, .9f, 14);
            kit.Capsule(new Vector3(0f, .01f, .25f), new Vector3(0f, .01f, .36f), .118f, .118f, boot, 12);
            kit.Blob(new Vector3(0f, .05f, .375f), new Vector3(.127f, .172f, .105f), none, boot, .7f, .7f, 14);
            kit.Blob(new Vector3(0f, .056f, .452f), new Vector3(.133f, .18f, .032f), none, sole, .7f, .7f, 12);
            trim.Ball(new Vector3(0f, .15f, .25f), .026f, goldLight, 6);
        }

        /// <summary>
        /// The pirate's head: a bandana in the team colour knotted behind one ear, black hair under it, a patch over
        /// one eye, a gold ring in the other ear, a curled moustache and a great black beard with a gold bead in it.
        /// </summary>
        static void PirateHead(MeshKit kit, MeshKit detail, Color32 main, Color32 dark, Color32 light, Color32 gold)
        {
            Quaternion none = Quaternion.identity;
            Color32 black = C(0x2f2a36, .15f), blackDeep = C(0x1f1b25, .15f), patch = C(0x221c26, .3f);
            kit.Blob(new Vector3(0f, .17f, -.06f), new Vector3(.447f, .3f, .41f), none, main, 1f, 1f, 24);
            kit.Blob(new Vector3(0f, .285f, -.01f), new Vector3(.452f, .045f, .41f), none, light, 1f, 1f, 20);
            kit.Ball(new Vector3(.34f, .24f, -.28f), .095f, main, 10);
            kit.Capsule(new Vector3(.37f, .22f, -.32f), new Vector3(.56f, .04f, -.5f), .065f, .02f, main, 8);
            kit.Capsule(new Vector3(.35f, .2f, -.35f), new Vector3(.42f, -.1f, -.56f), .065f, .02f, dark, 8);
            for (int i = 0; i < 9; i++)
            {
                float a = Mathf.Lerp(62f, 298f, i / 8f) * Mathf.Deg2Rad;
                kit.Ball(new Vector3(Mathf.Sin(a) * .43f, .03f, Mathf.Cos(a) * .4f - .05f), .09f, i % 2 == 0 ? black : blackDeep, 8);
            }
            detail.Ball(new Vector3(-.47f, -.13f, .02f), .045f, gold, 8);
            // The patch sits over the right eye, on a cord that runs up under the bandana and back past the ear.
            detail.Blob(new Vector3(.168f, .05f, .402f), new Vector3(.152f, .157f, .045f), Quaternion.Euler(-6f, 20f, 0f), patch, 1f, 1f, 14);
            detail.Capsule(new Vector3(.07f, .17f, .388f), new Vector3(-.2f, .285f, .3f), .017f, .017f, patch, 6);
            detail.Capsule(new Vector3(.29f, .03f, .335f), new Vector3(.43f, .09f, .1f), .017f, .017f, patch, 6);
            for (int s = -1; s <= 1; s += 2)
            {
                kit.Capsule(new Vector3(s * .035f, -.135f, .405f), new Vector3(s * .2f, -.185f, .365f), .062f, .052f, black, 10);
                kit.Capsule(new Vector3(s * .2f, -.185f, .365f), new Vector3(s * .33f, -.115f, .29f), .052f, .016f, black, 8);
            }
            kit.Blob(new Vector3(0f, -.3f, .22f), new Vector3(.32f, .215f, .18f), none, black, 1f, 1f, 16);
            kit.Blob(new Vector3(0f, -.47f, .25f), new Vector3(.15f, .12f, .11f), none, blackDeep, 1f, 1f, 10);
            kit.Capsule(new Vector3(0f, -.52f, .27f), new Vector3(0f, -.66f, .27f), .06f, .04f, black, 8);
            kit.Blob(new Vector3(0f, -.62f, .27f), new Vector3(.055f, .03f, .055f), none, gold, 1f, 1f, 10);
            kit.Ball(new Vector3(0f, -.7f, .27f), .042f, black, 8);
        }

        /// <summary>
        /// The gladiator's body: a broad bare chest over a narrow waist, a strap across it with a medallion, one
        /// great shoulder plate in the team colour, a champion's belt and a skirt of leather strips.
        /// </summary>
        static void GladiatorTorso(MeshKit kit, MeshKit trim, Color32 skin, Color32 main, Color32 dark, Color32 light, Color32 gold, Color32 goldLight)
        {
            Quaternion none = Quaternion.identity;
            Color32 leather = C(0x8a5730, .15f), leatherDark = C(0x54331e, .15f), leatherLight = C(0xb07a48, .15f);
            Color32 line = MeshKit.Mix(skin, C(0x7a4028, .1f), .5f), goldDark = C(Kit.GoldDark, .7f);
            kit.Blob(new Vector3(0f, .1f, 0f), new Vector3(.385f, .27f, .265f), none, skin, .85f, .75f, 24);
            kit.Blob(new Vector3(0f, -.1f, 0f), new Vector3(.3f, .22f, .225f), none, skin, .8f, .8f, 20);
            kit.Blob(new Vector3(0f, .3f, 0f), new Vector3(.2f, .09f, .185f), none, skin, 1f, 1f, 16);
            for (int s = -1; s <= 1; s += 2)
            {
                // The slabs of his chest and the slope from neck to shoulder.
                kit.Blob(new Vector3(s * .145f, .135f, .185f), new Vector3(.158f, .118f, .095f), Quaternion.Euler(0f, s * 12f, 0f), skin, .9f, .9f, 14);
                kit.Blob(new Vector3(s * .21f, .265f, -.02f), new Vector3(.17f, .09f, .15f), Quaternion.Euler(0f, 0f, s * -18f), skin, .9f, .9f, 12);
                for (int row = 0; row < 2; row++)
                    trim.Capsule(new Vector3(s * .02f, -.005f - row * .085f, .226f - row * .006f), new Vector3(s * .12f, -.005f - row * .085f, .208f - row * .006f), .009f, .009f, line, 5);
            }
            trim.Capsule(new Vector3(0f, .2f, .266f), new Vector3(0f, -.11f, .228f), .01f, .01f, line, 5);
            // The strap of his harness, over the plated shoulder and down to the far hip, front and back.
            kit.Capsule(new Vector3(-.31f, .3f, .14f), new Vector3(.25f, -.14f, .21f), .042f, .042f, leatherDark, 8);
            kit.Capsule(new Vector3(-.31f, .3f, -.14f), new Vector3(.25f, -.14f, -.2f), .042f, .042f, leatherDark, 8);
            trim.Lathe(new Vector3(-.035f, .085f, .25f), Quaternion.Euler(90f, 0f, 0f), new[]
            {
                new Vector2(.092f, 0f), new Vector2(.092f, .03f), new Vector2(.074f, .046f), new Vector2(.054f, .04f), new Vector2(0f, .04f)
            }, new[] { goldDark, gold, goldLight, gold, gold }, 16);
            trim.Ball(new Vector3(-.035f, .085f, .292f), .032f, main, 8);
            Pauldron(kit, -1f, .25f, main, dark, goldLight);
            for (int i = 0; i < 12; i++)
            {
                float a = i * 30f * Mathf.Deg2Rad;
                Quaternion turn = Quaternion.Euler(0f, i * 30f, 0f);
                var at = new Vector3(Mathf.Sin(a) * .29f, -.34f, Mathf.Cos(a) * .22f);
                kit.Chamfer(at, new Vector3(.14f, .25f, .055f), turn * Quaternion.Euler(-12f, 0f, 0f), .022f, i % 2 == 0 ? leather : leatherDark, i % 2 == 0 ? leatherLight : leather);
                trim.Ball(at + new Vector3(0f, -.105f, 0f) + turn * new Vector3(0f, 0f, .03f), .03f, goldLight, 8);
            }
            kit.Chamfer(new Vector3(0f, -.37f, .24f), new Vector3(.19f, .3f, .05f), Quaternion.Euler(-10f, 0f, 0f), .022f, main, light);
            kit.Chamfer(new Vector3(0f, -.37f, -.24f), new Vector3(.19f, .3f, .05f), Quaternion.Euler(10f, 0f, 0f), .022f, dark, main);
            kit.Blob(new Vector3(0f, -.2f, 0f), new Vector3(.33f, .08f, .255f), none, leatherDark, .4f, .8f, 20);
            trim.Chamfer(new Vector3(0f, -.2f, .262f), new Vector3(.21f, .17f, .055f), none, .028f, gold, goldLight);
            trim.Gem(new Vector3(0f, -.2f, .292f), Quaternion.Euler(90f, 0f, 0f), new Vector3(.06f, .034f, .05f), main, light);
        }

        /// <summary>
        /// The gladiator's limbs. Thick bare arms: the left in a sleeve of banded plate from the elbow down, the
        /// throwing arm with a gold band above the elbow and its wrist bound in linen. Bare thighs, a bronze
        /// greave on each shin and strapped sandals.
        /// </summary>
        static void GladiatorLimb(MeshKit kit, MeshKit trim, int limb, Color32 skin, Color32 main, Color32 dark, Color32 light, Color32 gold, Color32 goldLight)
        {
            Quaternion none = Quaternion.identity;
            Color32 leatherDark = C(0x54331e, .15f), linen = C(0xf0e2c4, .1f), sole = C(0x3a2418, .1f);
            if (limb < 2)
            {
                kit.Ball(Vector3.zero, .155f, skin, 12);
                kit.Capsule(Vector3.zero, new Vector3(0f, 0f, .2f), .128f, .118f, skin, 12);
                kit.Blob(new Vector3(0f, .03f, .11f), new Vector3(.118f, .125f, .11f), none, skin, 1f, 1f, 12);
                kit.Capsule(new Vector3(0f, 0f, .2f), new Vector3(0f, 0f, .38f), .112f, .1f, skin, 12);
                if (limb == ArmL)
                {
                    for (int band = 0; band < 3; band++)
                    {
                        float girth = .13f - band * .007f;
                        kit.Blob(new Vector3(0f, 0f, .21f + band * .068f), new Vector3(girth, girth, .044f), none, band % 2 == 0 ? main : dark, .6f, .9f, 14);
                    }
                    trim.Blob(new Vector3(0f, 0f, .172f), new Vector3(.132f, .132f, .018f), none, goldLight, 1f, 1f, 12);
                }
                else
                {
                    kit.Blob(new Vector3(0f, 0f, .1f), new Vector3(.134f, .134f, .028f), none, gold, .8f, 1f, 14);
                    for (int i = 0; i < 3; i++)
                        kit.Blob(new Vector3(0f, 0f, .29f + i * .036f), new Vector3(.109f - i * .003f, .109f - i * .003f, .022f), none, linen, 1f, 1f, 12);
                }
                kit.Ball(new Vector3(0f, 0f, .45f), .135f, skin, 12);
                kit.Ball(new Vector3(0f, .105f, .47f), .056f, skin, 8);
                return;
            }
            kit.Ball(Vector3.zero, .135f, skin, 10);
            kit.Capsule(Vector3.zero, new Vector3(0f, 0f, .3f), .125f, .108f, skin, 12);
            Cuff(kit, new Vector3(0f, .012f, .2f), .124f, .2f, gold, goldLight);
            trim.Gem(new Vector3(0f, .132f, .17f), none, new Vector3(.045f, .028f, .055f), main, light, 6);
            kit.Blob(new Vector3(0f, 0f, .305f), new Vector3(.128f, .128f, .035f), none, leatherDark, 1f, 1f, 12);
            kit.Blob(new Vector3(0f, .05f, .375f), new Vector3(.122f, .168f, .1f), none, skin, .7f, .7f, 14);
            kit.Blob(new Vector3(0f, .056f, .452f), new Vector3(.13f, .178f, .03f), none, sole, .7f, .7f, 12);
            for (int i = 0; i < 2; i++)
                kit.Blob(new Vector3(0f, .02f + i * .095f, .372f), new Vector3(.131f, .03f, .108f), none, leatherDark, 1f, 1f, 10);
        }

        /// <summary>
        /// The gladiator's head: dark hair cropped close, a heavy jaw dark with stubble, an old scar through one
        /// brow, and a victor's wreath of gold leaves tied behind with a ribbon in the team colour.
        /// </summary>
        static void GladiatorHead(MeshKit kit, MeshKit detail, Color32 skin, Color32 main, Color32 light, Color32 gold, Color32 goldLight)
        {
            Quaternion none = Quaternion.identity;
            Color32 hair = C(0x3a2e2c, .15f), shadow = MeshKit.Mix(skin, C(0x4a3830, .1f), .2f), scar = MeshKit.Mix(skin, C(0xffe2d6, .1f), .65f);
            kit.Blob(new Vector3(0f, .13f, -.075f), new Vector3(.432f, .33f, .395f), none, hair, 1f, 1f, 24);
            kit.Blob(new Vector3(0f, -.215f, .05f), new Vector3(.374f, .185f, .343f), none, shadow, 1f, 1f, 18);
            for (int s = -1; s <= 1; s += 2)
            {
                // Seven leaves a side, from the knot at the back round to the brow, each leaning out and forward.
                for (int i = 0; i < 7; i++)
                {
                    float degrees = Mathf.Lerp(158f, 52f, i / 6f), a = degrees * Mathf.Deg2Rad;
                    var at = new Vector3(s * Mathf.Sin(a) * .445f, .245f + Mathf.Cos(a) * .03f, Mathf.Cos(a) * .41f - .06f);
                    kit.Blob(at, new Vector3(.04f, .1f, .068f), Quaternion.Euler(0f, s * degrees, 0f) * Quaternion.Euler(-34f, 0f, s * -22f), i % 2 == 0 ? gold : goldLight, 1f, 1f, 8);
                }
                kit.Capsule(new Vector3(s * .04f, .2f, -.47f), new Vector3(s * .13f, -.1f, -.56f), .05f, .018f, s > 0 ? main : light, 8);
            }
            kit.Ball(new Vector3(0f, .215f, -.465f), .07f, main, 10);
            detail.Capsule(new Vector3(-.215f, .215f, .325f), new Vector3(-.125f, -.03f, .388f), .013f, .013f, scar, 6);
        }

        /// <summary>
        /// The dawn witch's body: a short dress in the team colour that flares into a ragged hem of points, a wide
        /// pale collar pinned with a star, a rose sash knotted on one hip, the sun coming up in gold on her front
        /// and little stars scattered over the skirt.
        /// </summary>
        static void WitchTorso(MeshKit kit, MeshKit trim, Color32 main, Color32 dark, Color32 light, Color32 gold, Color32 goldLight)
        {
            Quaternion none = Quaternion.identity;
            Color32 cream = C(0xfff4e2, .1f), rose = C(0xff9f8e, .25f), roseDark = C(0xe2776c, .2f);
            kit.Blob(new Vector3(0f, .07f, 0f), new Vector3(.285f, .285f, .22f), none, main, .85f, .8f, 22);
            // The skirt: a bell under the sash, and a hem of ten points, long and short by turns.
            kit.Blob(new Vector3(0f, -.25f, 0f), new Vector3(.385f, .17f, .32f), none, main, .9f, .7f, 22);
            for (int i = 0; i < 10; i++)
            {
                float a = (i * 36f + 18f) * Mathf.Deg2Rad, reach = i % 2 == 0 ? 1.34f : 1.16f, drop = i % 2 == 0 ? -.53f : -.44f;
                var from = new Vector3(Mathf.Sin(a) * .31f, -.3f, Mathf.Cos(a) * .255f);
                var tip = new Vector3(Mathf.Sin(a) * .385f * reach, drop, Mathf.Cos(a) * .32f * reach);
                kit.Spike(from, tip, .135f, i % 2 == 0 ? main : dark, i % 2 == 0 ? light : main, 5);
            }
            // The sash, with its knot and two tails on her left hip.
            kit.Blob(new Vector3(0f, -.13f, 0f), new Vector3(.305f, .055f, .24f), none, rose, .5f, .85f, 20);
            kit.Ball(new Vector3(-.27f, -.14f, .1f), .07f, rose, 10);
            kit.Capsule(new Vector3(-.28f, -.16f, .11f), new Vector3(-.37f, -.39f, .17f), .045f, .02f, roseDark, 8);
            kit.Capsule(new Vector3(-.27f, -.16f, .1f), new Vector3(-.3f, -.43f, .07f), .045f, .02f, rose, 8);
            // The collar and the star that pins it.
            kit.Blob(new Vector3(0f, .28f, .01f), new Vector3(.27f, .065f, .225f), none, cream, 1f, .9f, 18);
            trim.Plate(new Vector3(0f, .245f, .228f), Quaternion.Euler(-14f, 0f, 0f), MeshKit.StarRim(5, .075f, .034f), .035f, .012f, goldLight, gold);
            // The sun coming up over the sash: half a disc and five rays.
            var sun = new Vector2[9];
            for (int i = 0; i < sun.Length; i++)
            {
                float a = i * Mathf.PI / (sun.Length - 1);
                sun[i] = new Vector2(Mathf.Cos(a) * .1f, Mathf.Sin(a) * .1f);
            }
            var rise = new Vector3(0f, -.075f, .207f);
            trim.Plate(rise, none, sun, .034f, .012f, gold, goldLight);
            for (int i = 1; i <= 5; i++)
            {
                float a = i * 30f * Mathf.Deg2Rad;
                var ray = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
                trim.Capsule(rise + ray * .125f, rise + ray * .178f + Vector3.back * .012f, .014f, .008f, goldLight, 5);
            }
            // Stars on the skirt, wherever they fell.
            float[] round = { -66f, -27f, 31f, 70f, 148f, 212f };
            float[] high = { -.27f, -.31f, -.3f, -.26f, -.29f, -.27f };
            for (int i = 0; i < round.Length; i++)
            {
                float a = round[i] * Mathf.Deg2Rad;
                float swell = Mathf.Sqrt(Mathf.Max(0f, 1f - Mathf.Pow((high[i] + .25f) / .17f, 2f)));
                var at = new Vector3(Mathf.Sin(a) * .392f * swell, high[i], Mathf.Cos(a) * .327f * swell);
                trim.Plate(at, Quaternion.Euler(0f, round[i], 0f) * Quaternion.Euler(0f, 0f, i * 23f), MeshKit.StarRim(5, .05f, .022f), .03f, .01f, goldLight, gold);
            }
        }

        /// <summary>
        /// The dawn witch's limbs. Sleeves like bells, a thin bare forearm and a gold bangle; odd stockings, hooped
        /// on one leg and plain with a star at the knee on the other; and boots whose toes turn up to a little bell.
        /// </summary>
        static void WitchLimb(MeshKit kit, MeshKit trim, int limb, Color32 skin, Color32 main, Color32 dark, Color32 light, Color32 gold, Color32 goldLight)
        {
            Quaternion none = Quaternion.identity;
            Color32 cream = C(0xfff4e2, .1f), boot = C(0x553650, .3f), bootDark = C(0x33202f, .2f);
            if (limb < 2)
            {
                kit.Ball(Vector3.zero, .12f, main, 12);
                kit.Capsule(Vector3.zero, new Vector3(0f, 0f, .27f), .105f, .165f, main, 14);
                kit.Blob(new Vector3(0f, 0f, .286f), new Vector3(.174f, .174f, .03f), none, light, 1f, 1f, 14);
                kit.Blob(new Vector3(0f, 0f, .302f), new Vector3(.14f, .14f, .022f), none, cream, 1f, 1f, 12);
                kit.Capsule(new Vector3(0f, 0f, .26f), new Vector3(0f, 0f, .4f), .075f, .07f, skin, 10);
                trim.Blob(new Vector3(0f, 0f, .376f), new Vector3(.086f, .086f, .02f), none, goldLight, 1f, 1f, 10);
                kit.Ball(new Vector3(0f, 0f, .45f), .115f, skin, 12);
                kit.Ball(new Vector3(0f, .09f, .47f), .048f, skin, 8);
                return;
            }
            kit.Ball(Vector3.zero, .125f, dark, 10);
            if (limb == LegL)
            {
                // Hoops, pale and coloured by turns. One even shade to each, so the joins between them stay clean.
                kit.Gradient = 0f;
                for (int hoop = 0; hoop < 5; hoop++)
                {
                    float z0 = hoop * .062f, z1 = z0 + .062f;
                    kit.Capsule(new Vector3(0f, 0f, z0), new Vector3(0f, 0f, z1), Mathf.Lerp(.117f, .1f, z0 / .31f), Mathf.Lerp(.117f, .1f, z1 / .31f), hoop % 2 == 0 ? cream : main, 12);
                }
                kit.Gradient = .16f;
            }
            else
            {
                kit.Capsule(Vector3.zero, new Vector3(0f, 0f, .31f), .117f, .1f, cream, 12);
                trim.Plate(new Vector3(0f, .112f, .15f), Quaternion.Euler(-90f, 0f, 0f), MeshKit.StarRim(5, .052f, .023f), .03f, .01f, goldLight, gold);
            }
            kit.Blob(new Vector3(0f, 0f, .302f), new Vector3(.127f, .127f, .04f), none, light, 1f, 1f, 12);
            kit.Blob(new Vector3(0f, .045f, .375f), new Vector3(.118f, .165f, .1f), none, boot, .7f, .7f, 14);
            kit.Blob(new Vector3(0f, .05f, .452f), new Vector3(.124f, .172f, .03f), none, bootDark, .7f, .7f, 12);
            kit.Spike(new Vector3(0f, .16f, .39f), new Vector3(0f, .35f, .27f), .08f, boot, boot, 5);
            trim.Ball(new Vector3(0f, .355f, .262f), .032f, goldLight, 6);
        }

        /// <summary>
        /// The dawn witch's head: a wild mass of white hair with a ragged fringe, a lock down each side of her face
        /// and tufts sticking out wherever they like; and a crooked pointed hat in the team colour, far too small,
        /// perched on the back of it. The long fall of hair behind is a part of its own (see WitchMane).
        /// </summary>
        static void WitchHead(MeshKit kit, MeshKit detail, Color32 main, Color32 dark, Color32 light, Color32 gold, Color32 goldLight, Color32 hair, Color32 hairShade)
        {
            Quaternion none = Quaternion.identity;
            kit.Blob(new Vector3(0f, .12f, -.085f), new Vector3(.458f, .36f, .425f), none, hair, 1f, 1f, 26);
            // The fringe: five ragged points over her brow, the middle one long enough to hang between her eyes.
            float[] across = { -.27f, -.14f, -.01f, .12f, .25f };
            float[] down = { .26f, .31f, .12f, .3f, .25f };
            for (int i = 0; i < across.Length; i++)
            {
                float x = across[i], off = Mathf.Abs(x);
                kit.Spike(new Vector3(x * .9f, .37f - off * .25f, .24f - off * .3f), new Vector3(x * 1.08f + .02f, down[i], .41f - off * .42f), .12f, hair, hair, 5);
            }
            for (int s = -1; s <= 1; s += 2)
            {
                // A lock down each side of her face as far as her cheek, not quite a pair (the long hair below
                // them is the hanks that swing: see WitchHank)...
                float longer = s < 0 ? 1.14f : 1f;
                var bend = new Vector3(s * .455f, -.16f * longer, .17f);
                kit.Capsule(new Vector3(s * .39f, .12f, .12f), bend, .12f, .1f, hair, 10);
                kit.Capsule(bend, new Vector3(s * .42f, -.36f * longer, .23f), .1f, .025f, hair, 10);
                // ...and tufts that stick out behind her ears and at her nape.
                kit.Spike(new Vector3(s * .36f, .22f, -.2f), new Vector3(s * .74f, .36f, -.3f), .13f, hair, hair, 5);
                kit.Spike(new Vector3(s * .4f, -.02f, -.22f), new Vector3(s * .76f, -.1f, -.36f), .12f, hairShade, hair, 5);
                kit.Spike(new Vector3(s * .3f, -.2f, -.3f), new Vector3(s * .58f, -.42f, -.44f), .12f, hair, hair, 5);
            }
            // The hat: a brim, a cone in three bent lengths, a gold band with a star on it and a bell at the tip.
            Quaternion tilt = Quaternion.Euler(-24f, 0f, 9f);
            var seat = new Vector3(.03f, .37f, -.2f);
            kit.Blob(seat, new Vector3(.4f, .04f, .38f), tilt, main, .8f, .9f, 22);
            Vector3 first = seat + tilt * new Vector3(0f, .24f, 0f), second = first + tilt * new Vector3(.06f, .2f, -.05f);
            Vector3 tip = second + tilt * new Vector3(.17f, .1f, -.12f);
            kit.Gradient = 0f;
            kit.Capsule(seat, first, .255f, .17f, main, 14);
            kit.Capsule(first, second, .17f, .09f, main, 12);
            kit.Capsule(second, tip, .09f, .025f, main, 10);
            kit.Gradient = .16f;
            kit.Blob(seat + tilt * new Vector3(0f, .075f, 0f), new Vector3(.264f, .05f, .264f), tilt, gold, .6f, 1f, 16);
            detail.Plate(seat + tilt * new Vector3(0f, .085f, .268f), tilt, MeshKit.StarRim(5, .078f, .035f), .035f, .012f, goldLight, gold);
            kit.Ball(tip, .052f, goldLight, 8);
        }

        /// <summary>
        /// One hank of the witch's long hair, drawn hanging along its own Z from its root with its flat side to her
        /// back: 1 is the heavy fall down the middle, which ends in four ragged points; 0 and 2 the thinner ones that
        /// hang down past her shoulders to her right and left, which end in three and are not the same length.
        /// </summary>
        static void WitchHank(MeshKit kit, int which, Color32 hair, Color32 hairShade)
        {
            Quaternion none = Quaternion.identity;
            if (which == 1)
            {
                Color32 deep = MeshKit.Mix(hair, hairShade, .55f);
                kit.Blob(new Vector3(0f, 0f, .2f), new Vector3(.32f, .16f, .3f), none, deep, 1f, 1f, 18);
                kit.Blob(new Vector3(0f, .01f, .54f), new Vector3(.33f, .14f, .32f), none, deep, 1f, 1f, 18);
                kit.Blob(new Vector3(0f, .02f, .86f), new Vector3(.29f, .12f, .27f), none, deep, 1f, 1f, 16);
                float[] spread = { -.25f, -.08f, .09f, .26f };
                float[] far = { 1.24f, 1.45f, 1.33f, 1.19f };
                for (int i = 0; i < spread.Length; i++)
                    kit.Spike(new Vector3(spread[i] * .8f, .02f, .82f), new Vector3(spread[i] * 1.3f, .03f, far[i]), .135f, i % 2 == 0 ? deep : hair, hair, 5);
                return;
            }
            // The one on her right is the longer of the two: nothing about her is a pair. Thick where it comes out
            // from under the rest of her hair, thinner all the way down, and ragged at the end.
            float s = which - 1, lank = which == 0 ? 1f : .86f;
            kit.Blob(new Vector3(0f, 0f, .15f * lank), new Vector3(.2f, .15f, .24f * lank), none, hair, 1f, 1f, 16);
            kit.Blob(new Vector3(s * .035f, .01f, .43f * lank), new Vector3(.185f, .13f, .27f * lank), none, hair, 1f, 1f, 16);
            kit.Blob(new Vector3(s * .075f, .02f, .7f * lank), new Vector3(.15f, .105f, .24f * lank), none, hair, 1f, 1f, 14);
            float[] out3 = { -.11f, .0f, .12f };
            float[] far3 = { 1.02f, 1.2f, .95f };
            for (int i = 0; i < out3.Length; i++)
                kit.Spike(new Vector3(s * .075f + out3[i] * .8f, .02f, .72f * lank), new Vector3(s * .13f + out3[i] * 1.5f, .03f, far3[i] * lank), .1f, i == 1 ? hairShade : hair, hair, 5);
        }

        static void OrcHead(MeshKit kit, MeshKit detail, Color32 skin, Color32 main, Color32 light, Color32 cream, Color32 gold)
        {
            Quaternion none = Quaternion.identity;
            Color32 deep = MeshKit.Mix(skin, C(0x2f6a2c, .1f), .4f);
            kit.Blob(new Vector3(0f, -.2f, .03f), new Vector3(.41f, .24f, .31f), none, skin, .9f, .9f, 20);
            kit.Blob(new Vector3(0f, -.31f, .25f), new Vector3(.24f, .09f, .12f), none, MeshKit.Mix(skin, C(0xc8e48a, .15f), .3f), 1f, 1f, 14);
            kit.Blob(new Vector3(0f, .2f, .3f), new Vector3(.33f, .07f, .1f), none, skin, 1f, 1f, 14);
            for (int s = -1; s <= 1; s += 2)
            {
                kit.Capsule(new Vector3(s * .17f, -.3f, .33f), new Vector3(s * .215f, -.17f, .385f), .05f, .012f, cream, 10);
                kit.Capsule(new Vector3(s * .4f, -.01f, -.02f), new Vector3(s * .66f, .12f, -.08f), .085f, .012f, skin, 10);
                detail.Capsule(new Vector3(s * .44f, -.01f, .03f), new Vector3(s * .58f, .06f, 0f), .035f, .008f, deep, 6);
                detail.Ball(new Vector3(s * .5f, -.11f, -.02f), .035f, gold, 8);
                detail.Blob(new Vector3(s * .035f, -.07f, .455f), new Vector3(.022f, .03f, .02f), none, deep, 1f, 1f, 6);
            }
            detail.Blob(new Vector3(0f, -.06f, .40f), new Vector3(.11f, .07f, .075f), none, MeshKit.Mix(skin, C(0x3f8a3a, .6f), .35f), 1f, 1f, 12);
            // Crest of hair from the brow to the nape.
            for (int i = 0; i < 7; i++)
            {
                float degrees = Mathf.Lerp(38f, 150f, i / 6f), a = degrees * Mathf.Deg2Rad;
                var radial = new Vector3(0f, Mathf.Sin(a), Mathf.Cos(a));
                kit.Blob(radial * .4f + radial * .1f, new Vector3(.07f, .15f - Mathf.Abs(i - 3) * .012f, .085f), Quaternion.Euler(90f - degrees, 0f, 0f), i % 2 == 0 ? main : light, 1f, 1f, 10);
            }
        }

        void BuildBody()
        {
            Color32 main = C(Kit.TeamMain[Team], .3f), dark = C(Kit.TeamDark[Team], .2f), light = C(Kit.TeamLight[Team], .2f);
            Color32 skin = C(Kit.Skins[(HeroIndex + Team * 2) % Kit.Skins.Length], .1f);
            bool orc = Kind == WeaponKind.Slam, goblin = Kind == WeaponKind.Mine, spiky = Kind == WeaponKind.Bounce, pirate = Kind == WeaponKind.Hook;
            bool brute = Kind == WeaponKind.Boomerang, witch = Kind == WeaponKind.Star;
            if (brute) skin = C(0xdb9a66, .12f);   // the gladiator is bronzed by the sun, whichever side he fights for
            if (witch) skin = C(0xfbdcc8, .1f);    // and the witch has not seen it in years
            Color32 witchHair = C(0xf8f3ec, .3f), witchHairShade = C(0xd6cde4, .25f);
            if (spiky) skin = C(FaceHex, .2f);   // the porcupine's cream face
            if (orc) skin = C(0x7fc24e, .2f);
            if (goblin) skin = C(0xa9db58, .2f);   // a paler, yellower green than the orc
            Color32 steel = C(0xbccadc, .95f), steelDark = C(0x7f8fa8, .8f);
            Color32 gold = C(Kit.Gold, .9f), goldDark = C(Kit.GoldDark, .7f), goldLight = C(0xffe07a, 1f);
            Color32 leather = C(0x9a6538, .15f), leatherDark = C(0x5e3a22, .15f), cream = C(0xf6ead2, .1f);
            Color32 fur = C(0xc29562, 0f), furDark = C(0x8a6238, 0f), gem = C(Kit.TeamMain[Team], 1f);
            Quaternion none = Quaternion.identity, faceOn = Quaternion.Euler(0f, 180f, 0f);
            Transform root = transform;
            bool plated = Kind == WeaponKind.Sword || Kind == WeaponKind.Hammer;

            // ---- torso: tunic, skirt strips, belt, scarf and the armour that marks the class. Each part has a
            // second mesh for studs, buckles and gems, which carry no outline so they stay crisp at any size.
            var kit = new MeshKit();
            var trim = new MeshKit();
            // The orc is sculpted in Blender; the code-built version only stands in if that file is missing.
            Mesh sculpt = orc ? ColosseumModels.Get("orc_torso", Team) : null;
            if (orc)
            {
                if (sculpt == null) OrcTorso(kit, trim, skin, main, dark, light, gold, goldLight, gem);
            }
            else if (spiky)
            {
                PorcupineTorso(kit, trim, main, light);
            }
            else if (pirate)
            {
                PirateTorso(kit, trim, main, dark, light, gold, goldLight);
            }
            else if (brute)
            {
                GladiatorTorso(kit, trim, skin, main, dark, light, gold, goldLight);
            }
            else if (witch)
            {
                WitchTorso(kit, trim, main, dark, light, gold, goldLight);
            }
            else
            {
            kit.Blob(Vector3.zero, new Vector3(.33f, .34f, .25f), none, main, .8f, .8f, 24);
            for (int i = 0; i < 12; i++)
            {
                float a = i * 30f * Mathf.Deg2Rad;
                Quaternion turn = Quaternion.Euler(0f, i * 30f, 0f);
                var at = new Vector3(Mathf.Sin(a) * .30f, -.31f, Mathf.Cos(a) * .225f);
                kit.Chamfer(at, new Vector3(.145f, .25f, .058f), turn * Quaternion.Euler(-12f, 0f, 0f), .022f, i % 2 == 0 ? dark : main, i % 2 == 0 ? main : light);
                trim.Ball(at + new Vector3(0f, -.108f, 0f) + turn * new Vector3(0f, 0f, .032f), .03f, goldLight, 8);
            }
            kit.Blob(new Vector3(0f, -.17f, 0f), new Vector3(.348f, .062f, .267f), none, leatherDark, .4f, .8f, 20);
            trim.Chamfer(new Vector3(0f, -.17f, .272f), new Vector3(.18f, .135f, .055f), none, .022f, gold, goldLight);
            trim.Gem(new Vector3(0f, -.17f, .298f), Quaternion.Euler(90f, 0f, 0f), new Vector3(.05f, .03f, .042f), gem, light);
            kit.Blob(new Vector3(0f, .3f, 0f), new Vector3(.215f, .08f, .195f), none, light, 1f, 1f, 16);
            switch (Kind)
            {
                case WeaponKind.Sword:
                    // Riveted bands of plate, round pauldrons edged in gold, a jewelled collar and a short cape.
                    for (int band = 0; band < 3; band++)
                    {
                        float y = .22f - band * .115f;
                        kit.Blob(new Vector3(0f, y, 0f), new Vector3(.356f, .062f, .272f), none, band % 2 == 0 ? steel : steelDark, .4f, .85f, 22);
                        for (int s = -1; s <= 1; s += 2)
                            trim.Ball(new Vector3(s * .2f, y, .236f), .024f, goldLight, 6);
                    }
                    for (int s = -1; s <= 1; s += 2)
                        Pauldron(kit, s, .2f, steel, steelDark, goldLight);
                    trim.Chamfer(new Vector3(0f, .285f, .222f), new Vector3(.12f, .1f, .05f), none, .02f, gold, goldLight);
                    trim.Gem(new Vector3(0f, .285f, .246f), Quaternion.Euler(90f, 0f, 0f), new Vector3(.04f, .028f, .034f), gem, light);
                    kit.Blob(new Vector3(0f, -.1f, -.275f), new Vector3(.3f, .4f, .05f), Quaternion.Euler(10f, 0f, 0f), dark, .8f, .8f, 14);
                    break;
                case WeaponKind.Axe:
                    // Leather vest with crossed straps, a bronze medallion and a thick two-row fur collar.
                    kit.Blob(new Vector3(0f, .08f, 0f), new Vector3(.346f, .21f, .263f), none, leather, .85f, .85f, 22);
                    kit.Capsule(new Vector3(-.26f, .27f, .2f), new Vector3(.24f, -.12f, .235f), .034f, .034f, leatherDark, 8);
                    kit.Capsule(new Vector3(.26f, .27f, .2f), new Vector3(-.24f, -.12f, .235f), .034f, .034f, leatherDark, 8);
                    trim.Lathe(new Vector3(0f, .08f, .25f), Quaternion.Euler(90f, 0f, 0f), new[]
                    {
                        new Vector2(.088f, 0f), new Vector2(.088f, .03f), new Vector2(.072f, .046f), new Vector2(.052f, .04f), new Vector2(0f, .04f)
                    }, new[] { goldDark, gold, goldLight, gold, gold }, 16);
                    trim.Ball(new Vector3(0f, .08f, .29f), .03f, gem, 8);
                    for (int i = 0; i < 12; i++)
                    {
                        float a = i * 30f * Mathf.Deg2Rad;
                        kit.Ball(new Vector3(Mathf.Sin(a) * .31f, .25f + (i % 2) * .035f, Mathf.Cos(a) * .22f), .105f, i % 2 == 0 ? fur : furDark, 8);
                    }
                    for (int i = 0; i < 8; i++)
                    {
                        float a = (i * 45f + 15f) * Mathf.Deg2Rad;
                        kit.Ball(new Vector3(Mathf.Sin(a) * .25f, .335f, Mathf.Cos(a) * .18f), .075f, i % 2 == 0 ? furDark : fur, 8);
                    }
                    break;
                case WeaponKind.Hammer:
                    // Golden cuirass with a jewelled medallion, layered spiked pauldrons and a cape with a gold hem.
                    kit.Lathe(Vector3.zero, none, new[]
                    {
                        new Vector2(.35f, -.17f), new Vector2(.378f, -.15f), new Vector2(.378f, -.1f), new Vector2(.362f, -.09f), new Vector2(.362f, -.09f),
                        new Vector2(.37f, .02f), new Vector2(.372f, .09f), new Vector2(.372f, .09f), new Vector2(.395f, .1f), new Vector2(.39f, .2f),
                        new Vector2(.34f, .275f), new Vector2(.25f, .31f), new Vector2(.25f, .31f), new Vector2(.27f, .32f), new Vector2(.27f, .36f),
                        new Vector2(.2f, .37f)
                    }, new[]
                    {
                        goldDark, goldDark, goldDark, goldDark, gold, gold, gold, goldLight, goldLight, gold, gold, gold, goldDark, goldLight, goldLight,
                        goldLight
                    }, 26, .765f);
                    trim.Lathe(new Vector3(0f, .1f, .262f), Quaternion.Euler(90f, 0f, 0f), new[]
                    {
                        new Vector2(.135f, 0f), new Vector2(.135f, .028f), new Vector2(.115f, .045f), new Vector2(.095f, .03f), new Vector2(.095f, .03f),
                        new Vector2(.07f, .06f), new Vector2(0f, .075f)
                    }, new[] { goldDark, goldDark, goldLight, goldDark, gem, gem, gem }, 20);
                    for (int s = -1; s <= 1; s += 2)
                    {
                        Pauldron(kit, s, .235f, gold, goldDark, goldLight);
                        kit.Spike(new Vector3(s * .47f, .32f, 0f), new Vector3(s * .66f, .52f, 0f), .075f, steel, C(0xf2f6fb, 1f));
                        trim.Ball(new Vector3(s * .29f, .375f, .1f), .03f, goldLight, 6);
                    }
                    kit.Blob(new Vector3(0f, -.1f, -.295f), new Vector3(.33f, .42f, .05f), Quaternion.Euler(10f, 0f, 0f), main, .8f, .8f, 14);
                    kit.Blob(new Vector3(0f, -.48f, -.365f), new Vector3(.33f, .04f, .055f), none, gold, .8f, .8f, 10);
                    break;
                case WeaponKind.Mine:
                {
                    // Sapper's kit: a padded leather vest, a bandolier of little bombs across the chest and a
                    // spare mine strapped to the back.
                    Color32 iron = C(0x4a4d5c, .7f), ironLight = C(0x80859a, .8f), button = C(0xff4a3c, 1f);
                    kit.Blob(new Vector3(0f, .08f, 0f), new Vector3(.346f, .2f, .263f), none, leather, .85f, .85f, 22);
                    kit.Capsule(new Vector3(-.26f, .3f, .19f), new Vector3(.24f, -.12f, .24f), .04f, .04f, leatherDark, 8);
                    for (int i = 0; i < 3; i++)
                    {
                        Vector3 at = Vector3.Lerp(new Vector3(-.2f, .25f, .25f), new Vector3(.18f, -.07f, .29f), i / 2f);
                        kit.Ball(at, .074f, iron, 10);
                        trim.Blob(at + new Vector3(.02f, .072f, .01f), new Vector3(.024f, .03f, .024f), none, gold, 1f, 1f, 6);
                        trim.Ball(at + new Vector3(-.026f, .026f, .058f), .016f, ironLight, 5);
                    }
                    kit.Blob(new Vector3(0f, .04f, -.29f), new Vector3(.215f, .215f, .085f), none, iron, .7f, 1f, 16);
                    trim.Blob(new Vector3(0f, .04f, -.375f), new Vector3(.075f, .075f, .03f), none, button, 1f, 1f, 10);
                    for (int i = 0; i < 4; i++)
                    {
                        float a = (i * 90f + 45f) * Mathf.Deg2Rad;
                        trim.Ball(new Vector3(Mathf.Cos(a) * .15f, .04f + Mathf.Sin(a) * .15f, -.362f), .028f, gold, 6);
                    }
                    kit.Ball(new Vector3(-.36f, .2f, 0f), .125f, leatherDark, 12);
                    kit.Ball(new Vector3(.36f, .2f, 0f), .125f, leatherDark, 12);
                    break;
                }
                default:
                    // Laced leather jerkin, a buckled quiver strap and a quiver of fletched arrows on the back.
                    kit.Blob(new Vector3(0f, .08f, 0f), new Vector3(.346f, .2f, .263f), none, leather, .85f, .85f, 22);
                    kit.Capsule(new Vector3(-.25f, .28f, .2f), new Vector3(.22f, -.12f, .24f), .034f, .034f, leatherDark, 8);
                    trim.Slab(new Vector3(-.03f, .09f, .262f), faceOn * Quaternion.Euler(0f, 0f, 40f), Oval(.058f, .046f, 14, .5f), .05f, .016f, gold, goldLight, goldDark);
                    for (int i = 0; i < 3; i++)
                        trim.Capsule(new Vector3(.08f, .18f - i * .075f, .258f), new Vector3(.18f, .18f - i * .075f, .244f), .013f, .013f, cream, 6);
                    kit.Capsule(new Vector3(.14f, -.22f, -.3f), new Vector3(-.12f, .34f, -.3f), .09f, .1f, leatherDark, 10);
                    kit.Blob(new Vector3(-.115f, .33f, -.3f), new Vector3(.112f, .032f, .112f), Quaternion.Euler(0f, 0f, 24f), gold, 1f, 1f, 12);
                    for (int i = 0; i < 3; i++)
                    {
                        var nock = new Vector3(-.14f + i * .07f, .38f, -.3f + (i - 1) * .03f);
                        Vector3 tip = nock + new Vector3(-.05f, .2f, 0f);
                        kit.Capsule(nock, tip, .014f, .014f, cream, 6);
                        kit.Blob(tip + new Vector3(.02f, -.02f, 0f), new Vector3(.03f, .07f, .012f), Quaternion.Euler(0f, 0f, 14f), i == 1 ? main : light, 1f, 1f, 8);
                        kit.Blob(tip + new Vector3(-.024f, -.02f, 0f), new Vector3(.03f, .07f, .012f), Quaternion.Euler(0f, 0f, 14f), i == 1 ? light : main, 1f, 1f, 8);
                    }
                    kit.Ball(new Vector3(-.36f, .2f, 0f), .135f, dark, 12);
                    kit.Ball(new Vector3(.36f, .2f, 0f), .135f, dark, 12);
                    break;
            }
            }
            pivot = Kit.Spawn("Torso", root, sculpt != null ? sculpt : kit.ToMesh("Doll torso"), true).transform;
            Kit.Spawn("Torso trim", pivot, sculpt != null ? ColosseumModels.Get("orc_torso_trim", Team) : trim.ToMesh("Doll torso trim"), false, false);

            // ---- limbs: sleeve, bare arm, bracer and mitt; thigh, greave and boot
            Color32 guard = Kind == WeaponKind.Sword ? steel : Kind == WeaponKind.Hammer ? gold : leatherDark;
            Color32 sole = C(0x3a2418, .1f);
            for (int i = 0; i < 4; i++)
            {
                kit = new MeshKit();
                trim = new MeshKit();
                sculpt = orc ? ColosseumModels.Get(i < 2 ? "orc_arm" : "orc_leg", Team) : null;
                if (orc)
                {
                    if (sculpt == null) OrcLimb(kit, trim, i < 2, skin, main, dark, light, cream);
                }
                else if (spiky)
                {
                    PorcupineLimb(kit, i < 2);
                }
                else if (pirate)
                {
                    PirateLimb(kit, trim, i, skin, main, light, gold, goldLight);
                }
                else if (brute)
                {
                    GladiatorLimb(kit, trim, i, skin, main, dark, light, gold, goldLight);
                }
                else if (witch)
                {
                    WitchLimb(kit, trim, i, skin, main, dark, light, gold, goldLight);
                }
                else if (i < 2)
                {
                    kit.Ball(Vector3.zero, .125f, main, 12);
                    // Sleeve cuff, kept clear of the shoulder so the two never cut into each other.
                    kit.Blob(new Vector3(0f, 0f, .128f), new Vector3(.118f, .118f, .028f), none, light, 1f, 1f, 14);
                    kit.Capsule(Vector3.zero, new Vector3(0f, 0f, .36f), .097f, .09f, skin, 12);
                    Cuff(kit, new Vector3(0f, 0f, .25f), .104f, .16f, guard, plated ? goldLight : leather);
                    if (!plated)
                    {
                        // Leather bracers are studded instead.
                        for (int stud = 0; stud < 6; stud++)
                        {
                            float a = stud * 60f * Mathf.Deg2Rad;
                            trim.Ball(new Vector3(Mathf.Cos(a) * .104f, Mathf.Sin(a) * .104f, .25f), .022f, gold, 6);
                        }
                    }
                    kit.Ball(new Vector3(0f, 0f, .45f), .128f, skin, 12);
                    kit.Ball(new Vector3(0f, .1f, .47f), .054f, skin, 8);
                    if (i == ArmL && Kind == WeaponKind.Sword)
                    {
                        // Oval shield: polished gold rim, a field in the team colour with a pale stripe, boss and rivets.
                        var centre = new Vector3(0f, .15f, .27f);
                        Quaternion outward = Quaternion.Euler(90f, 0f, 0f);
                        kit.Cut(centre, Quaternion.Euler(-90f, 0f, 0f), Oval(.25f, .31f, 28), .075f, .04f, gold, goldLight, goldDark);
                        trim.Slab(centre + new Vector3(0f, .036f, 0f), outward, Oval(.19f, .25f, 28), .03f, .022f, main, light, dark);
                        trim.Slab(centre + new Vector3(0f, .046f, 0f), outward, new[]
                        {
                            new Vector2(-.168f, -.04f), new Vector2(.168f, -.04f), new Vector2(.168f, .04f), new Vector2(-.168f, .04f)
                        }, .024f, .012f, light, C(0xffffff, .5f), main);
                        trim.Ball(centre + new Vector3(0f, .062f, 0f), .078f, gold, 12);
                        trim.Ball(centre + new Vector3(.022f, .125f, .02f), .022f, goldLight, 6);
                        for (int k = 0; k < 4; k++)
                        {
                            float a = (k * 90f + 45f) * Mathf.Deg2Rad;
                            trim.Ball(centre + new Vector3(Mathf.Cos(a) * .215f, .036f, Mathf.Sin(a) * .27f), .022f, goldDark, 6);
                        }
                    }
                }
                else
                {
                    kit.Ball(Vector3.zero, .13f, dark, 10);
                    kit.Capsule(Vector3.zero, new Vector3(0f, 0f, .30f), .12f, .105f, skin, 12);
                    Cuff(kit, new Vector3(0f, .012f, .19f), .122f, .22f, guard, plated ? goldLight : leather);
                    if (plated) trim.Gem(new Vector3(0f, .13f, .16f), none, new Vector3(.05f, .03f, .06f), guard, goldLight, 6);
                    kit.Blob(new Vector3(0f, 0f, .3f), new Vector3(.13f, .13f, .042f), none, leather, 1f, 1f, 12);
                    kit.Blob(new Vector3(0f, .05f, .375f), new Vector3(.127f, .172f, .105f), none, leatherDark, .7f, .7f, 14);
                    kit.Blob(new Vector3(0f, .056f, .452f), new Vector3(.133f, .18f, .032f), none, sole, .7f, .7f, 12);
                    kit.Blob(new Vector3(0f, .05f, .352f), new Vector3(.132f, .177f, .024f), none, leather, .7f, .7f, 12);
                    trim.Ball(new Vector3(0f, .13f, .3f), .026f, gold, 6);
                }
                limbs[i] = Kit.Spawn(i < 2 ? "Arm" : "Leg", root, sculpt != null ? sculpt : kit.ToMesh("Doll limb"), true).transform;
                limbs[i].localScale = Vector3.one * size;
                Mesh extras = sculpt == null ? trim.ToMesh("Doll limb trim") : i < 2 ? null : ColosseumModels.Get("orc_leg_trim", Team);
                if (extras != null) Kit.Spawn("Limb trim", limbs[i], extras, false, false);
            }

            // ---- head: big readable face under a helmet, hood or horns. Nose, blush, rivets and gems go into the
            // detail mesh, which has no outline of its own.
            int browHex = 0x5a3a22;
            kit = new MeshKit();
            var detail = new MeshKit();
            kit.Blob(Vector3.zero, new Vector3(.42f, .40f, .40f), none, skin, 1f, 1f, 28);
            Color32 inner = MeshKit.Mix(skin, C(0xa85a40, .1f), .4f);
            for (int s = -1; s <= 1; s += 2)
            {
                if (!spiky)
                {
                    // A person's ears, at the sides of the head; the porcupine's are its own.
                    kit.Ball(new Vector3(s * .41f, -.03f, 0f), .085f, skin, 10);
                    detail.Blob(new Vector3(s * .468f, -.03f, .035f), new Vector3(.03f, .045f, .04f), none, inner, 1f, 1f, 8);
                }
                if (!orc && !goblin) detail.Blob(new Vector3(s * .25f, -.13f, .29f), new Vector3(.08f, .048f, .03f), Quaternion.Euler(0f, s * 32f, 0f), MeshKit.Mix(skin, C(0xf07a6a, .1f), .5f), 1f, 1f, 8);
                // The goblin's ears are long and pointed, and stand out sideways.
                if (!goblin) continue;
                kit.Spike(new Vector3(s * .34f, 0f, -.03f), new Vector3(s * .84f, .19f, -.12f), .17f, skin, skin, 6);
                detail.Spike(new Vector3(s * .43f, .005f, .04f), new Vector3(s * .73f, .14f, -.04f), .08f, inner, inner, 4);
            }
            if (!orc && !goblin && !spiky) detail.Blob(new Vector3(0f, -.06f, .40f), new Vector3(.078f, .068f, .085f), none, MeshKit.Mix(skin, C(0xc56a48, .7f), .45f), 1f, 1f, 12);
            if (goblin) detail.Blob(new Vector3(0f, -.075f, .44f), new Vector3(.076f, .066f, .15f), Quaternion.Euler(-8f, 0f, 0f), MeshKit.Mix(skin, C(0x5a9a2e, .3f), .5f), 1f, 1f, 12);
            switch (Kind)
            {
                case WeaponKind.Slam:
                    browHex = 0x24341c;
                    OrcHead(kit, detail, skin, main, light, cream, gold);
                    break;
                case WeaponKind.Bounce:
                    browHex = 0x6a4630;
                    PorcupineHead(kit, detail);
                    break;
                case WeaponKind.Hook:
                    browHex = 0x2a2530;
                    PirateHead(kit, detail, main, dark, light, gold);
                    break;
                case WeaponKind.Boomerang:
                    browHex = 0x2e2420;
                    GladiatorHead(kit, detail, skin, main, light, gold, goldLight);
                    break;
                case WeaponKind.Star:
                    browHex = 0x6f6384;
                    WitchHead(kit, detail, main, dark, light, gold, goldLight, witchHair, witchHairShade);
                    break;
                case WeaponKind.Mine:
                {
                    // Leather flying cap pulled down over the back of the head, with brass goggles pushed up on
                    // the brow; their lenses are in the team colour.
                    browHex = 0x39481c;
                    kit.Blob(new Vector3(0f, .16f, -.1f), new Vector3(.44f, .34f, .39f), none, leather, 1f, 1f, 24);
                    kit.Blob(new Vector3(0f, .3f, -.01f), new Vector3(.45f, .045f, .405f), none, leatherDark, 1f, 1f, 20);
                    for (int s = -1; s <= 1; s += 2)
                    {
                        Quaternion tip = Quaternion.Euler(-18f, s * 12f, 0f);
                        kit.Blob(new Vector3(s * .15f, .33f, .3f), new Vector3(.115f, .115f, .06f), tip, gold, .8f, 1f, 14);
                        detail.Blob(new Vector3(s * .153f, .342f, .345f), new Vector3(.082f, .082f, .03f), tip, gem, .8f, 1f, 12);
                        detail.Ball(new Vector3(s * .153f - .026f, .368f, .372f), .02f, C(0xffffff, 1f), 5);
                    }
                    detail.Blob(new Vector3(0f, .33f, .33f), new Vector3(.04f, .03f, .03f), none, goldLight, 1f, 1f, 6);
                    kit.Ball(new Vector3(0f, .49f, -.1f), .06f, leatherDark, 8);
                    break;
                }
                case WeaponKind.Sword:
                    // Steel helm with a gold brim and ridge, riveted cheek guards and a crest of feathers front to back.
                    Helm(kit, new Vector3(0f, .2f, -.11f), new Vector3(.447f, .30f, .39f), steel, steelDark, gold, goldLight);
                    kit.Chamfer(new Vector3(0f, .45f, -.11f), new Vector3(.11f, .16f, .6f), none, .03f, gold, goldLight);
                    for (int s = -1; s <= 1; s += 2)
                    {
                        CheekPiece(kit, s, steel, C(0xe4ebf5, 1f), steelDark);
                        detail.Ball(new Vector3(s * .452f, 0f, .1f), .028f, goldLight, 6);
                        detail.Ball(new Vector3(s * .452f, -.16f, .1f), .028f, goldLight, 6);
                    }
                    for (int i = 0; i < 10; i++)
                    {
                        float degrees = Mathf.Lerp(26f, 154f, i / 9f), a = degrees * Mathf.Deg2Rad;
                        var radial = new Vector3(0f, Mathf.Sin(a), Mathf.Cos(a));
                        var at = new Vector3(0f, .2f + .30f * Mathf.Sin(a), -.11f + .39f * Mathf.Cos(a)) + radial * .15f;
                        kit.Blob(at, new Vector3(.078f, .17f, .085f), Quaternion.Euler(90f - degrees, 0f, 0f), i % 2 == 0 ? main : light, 1f, 1f, 10);
                    }
                    break;
                case WeaponKind.Axe:
                {
                    // Riveted iron cap with a ridge, long curved horns in gold sockets, a handlebar moustache and a
                    // big ginger beard with two braids.
                    Color32 ginger = C(0xe9852e, .1f), gingerDeep = C(0xcf6f1e, .1f), hornTone = C(0xf3e6c8, .35f);
                    browHex = 0xb85a1c;
                    Helm(kit, new Vector3(0f, .21f, -.09f), new Vector3(.44f, .27f, .39f), steelDark, C(0x66758e, .7f), steel, C(0xe6edf6, 1f));
                    kit.Chamfer(new Vector3(0f, .425f, -.09f), new Vector3(.075f, .14f, .62f), none, .024f, C(0x93a2ba, .8f), steel);
                    for (int i = -2; i <= 2; i++)
                        detail.Ball(new Vector3(i * .14f, .315f, .322f - i * i * .021f), .026f, goldLight, 6);
                    for (int s = -1; s <= 1; s += 2)
                    {
                        Vector3[] horn =
                        {
                            new Vector3(s * .36f, .29f, -.06f), new Vector3(s * .5f, .34f, -.05f), new Vector3(s * .6f, .44f, -.04f),
                            new Vector3(s * .655f, .57f, -.03f), new Vector3(s * .65f, .7f, -.02f), new Vector3(s * .61f, .81f, -.01f)
                        };
                        float[] thick = { .108f, .098f, .082f, .062f, .04f, .012f };
                        // One even colour without per-piece shading, so the joints between the pieces do not show.
                        kit.Gradient = 0f;
                        for (int j = 0; j < 5; j++)
                            kit.Capsule(horn[j], horn[j + 1], thick[j], thick[j + 1], hornTone, 10);
                        kit.Gradient = .16f;
                        kit.Ball(horn[0] + new Vector3(s * .03f, .01f, 0f), .122f, gold, 12);
                        kit.Capsule(new Vector3(s * .035f, -.135f, .405f), new Vector3(s * .2f, -.185f, .365f), .062f, .052f, ginger, 10);
                        kit.Capsule(new Vector3(s * .2f, -.185f, .365f), new Vector3(s * .32f, -.11f, .29f), .052f, .016f, ginger, 8);
                        kit.Capsule(new Vector3(s * .14f, -.42f, .29f), new Vector3(s * .165f, -.6f, .29f), .062f, .04f, gingerDeep, 8);
                        kit.Blob(new Vector3(s * .16f, -.555f, .29f), new Vector3(.058f, .03f, .058f), none, gold, 1f, 1f, 10);
                        kit.Ball(new Vector3(s * .17f, -.645f, .29f), .045f, ginger, 8);
                    }
                    for (int i = 0; i < 9; i++)
                    {
                        float a = Mathf.Lerp(62f, 298f, i / 8f) * Mathf.Deg2Rad;
                        kit.Ball(new Vector3(Mathf.Sin(a) * .43f, .11f, Mathf.Cos(a) * .4f - .05f), .088f, i % 2 == 0 ? fur : furDark, 8);
                    }
                    kit.Blob(new Vector3(0f, -.3f, .22f), new Vector3(.31f, .21f, .175f), none, ginger, 1f, 1f, 16);
                    kit.Blob(new Vector3(0f, -.46f, .25f), new Vector3(.13f, .11f, .1f), none, ginger, 1f, 1f, 10);
                    break;
                }
                case WeaponKind.Hammer:
                {
                    // Golden helm with a jewel on the brow, a fan of feathers from ear to ear, a silver handlebar
                    // moustache and a pointed beard.
                    Color32 silver = C(0xece7dc, .1f);
                    browHex = 0xbdb5a8;
                    Helm(kit, new Vector3(0f, .2f, -.11f), new Vector3(.45f, .31f, .40f), gold, goldDark, goldDark, goldLight);
                    detail.Chamfer(new Vector3(0f, .325f, .31f), new Vector3(.15f, .12f, .05f), none, .022f, goldLight, C(0xfff2b8, 1f));
                    detail.Gem(new Vector3(0f, .325f, .334f), Quaternion.Euler(90f, 0f, 0f), new Vector3(.05f, .03f, .042f), gem, light);
                    for (int i = 0; i < 11; i++)
                    {
                        float degrees = Mathf.Lerp(-72f, 72f, i / 10f), b = degrees * Mathf.Deg2Rad;
                        var radial = new Vector3(Mathf.Sin(b), Mathf.Cos(b), 0f);
                        var at = new Vector3(.45f * Mathf.Sin(b), .2f + .31f * Mathf.Cos(b), -.08f) + radial * .13f;
                        kit.Blob(at, new Vector3(.085f, .16f, .08f), Quaternion.Euler(0f, 0f, -degrees), i % 2 == 0 ? cream : main, 1f, 1f, 10);
                    }
                    for (int s = -1; s <= 1; s += 2)
                    {
                        CheekPiece(kit, s, gold, goldLight, goldDark);
                        detail.Ball(new Vector3(s * .452f, -.02f, .1f), .028f, goldLight, 6);
                        kit.Capsule(new Vector3(s * .035f, -.14f, .405f), new Vector3(s * .2f, -.2f, .37f), .064f, .056f, silver, 10);
                        kit.Capsule(new Vector3(s * .2f, -.2f, .37f), new Vector3(s * .33f, -.125f, .3f), .056f, .018f, silver, 8);
                    }
                    kit.Blob(new Vector3(0f, -.32f, .24f), new Vector3(.23f, .16f, .14f), none, silver, 1f, 1f, 14);
                    kit.Capsule(new Vector3(0f, -.36f, .27f), new Vector3(0f, -.53f, .25f), .1f, .028f, silver, 10);
                    break;
                }
                default:
                {
                    // Hood with a tail and a dark brim, a row of curls underneath and a long feather pinned to the side.
                    Color32 hair = C(0x6b3d24, .15f);
                    kit.Blob(new Vector3(0f, .1f, -.14f), new Vector3(.46f, .42f, .40f), none, main, 1f, 1f, 28);
                    kit.Capsule(new Vector3(0f, .42f, -.3f), new Vector3(0f, .36f, -.66f), .13f, .03f, main, 10);
                    kit.Blob(new Vector3(0f, .37f, .2f), new Vector3(.3f, .05f, .14f), none, dark, .8f, .8f, 14);
                    float[] curls = { -.2f, -.1f, 0f, .1f, .2f };
                    for (int i = 0; i < curls.Length; i++)
                    {
                        float off = Mathf.Abs(curls[i]);
                        kit.Ball(new Vector3(curls[i], .31f - off * .2f, .285f - off * .18f), .1f - off * .08f, hair, 10);
                    }
                    var pin = new Vector3(.4f, .4f, .02f);
                    Quaternion lean = Quaternion.Euler(-12f, 0f, -22f);
                    kit.Plate(pin, lean, Leaf(.085f, .27f), .05f, .03f, gold, goldLight);
                    detail.Capsule(pin + lean * new Vector3(0f, -.2f, .03f), pin + lean * new Vector3(0f, .24f, .03f), .012f, .006f, cream, 6);
                    break;
                }
            }
            sculpt = orc ? ColosseumModels.Get("orc_head", Team) : null;
            fixedMouth = sculpt != null;
            head = Kit.Spawn("Head", root, sculpt != null ? sculpt : kit.ToMesh("Doll head"), true).transform;
            head.localScale = Vector3.one * (size * (fixedMouth ? 1f : 1.1f));
            // The sculpted orc has small, deep-set eyes under heavy brows.
            if (fixedMouth) { eyeSize = .64f; pupilSize = .8f; browHex = 0x3d2418; }
            Vector3 eyeSpot = fixedMouth ? new Vector3(.145f, .07f, .368f) : new Vector3(.158f, .045f, .352f);
            Vector3 browSpot = fixedMouth ? new Vector3(.15f, .2f, .392f) : new Vector3(.158f, .232f, .335f);
            Vector3 browSize = fixedMouth ? new Vector3(.125f, .058f, .045f) : new Vector3(.13f, .05f, .045f);
            if (spiky)
            {
                // Round eyes set wide apart, with large pupils and only a dash of a brow; the mouth is on the muzzle.
                eyeSize = .9f; pupilSize = 1.25f;
                eyeSpot = new Vector3(.186f, .03f, .348f);
                browSpot = new Vector3(.188f, .204f, .33f);
                browSize = new Vector3(.07f, .028f, .04f);
                mouthHome = new Vector3(0f, -.185f, .5f);
                mouthSize = .66f;
                mouthDrop = .7f;
            }
            if (brute)
            {
                // His mouth sits out on the heavy jaw.
                mouthHome = new Vector3(0f, -.2f, .388f);
                mouthSize = 1.1f;
            }
            if (pirate)
            {
                // His mouth opens in the beard, under the moustache.
                mouthHome = new Vector3(0f, -.283f, .388f);
                mouthSize = 1.16f;
            }
            if (witch)
            {
                // One eye wide with a pinprick of a pupil, the other narrower and all pupil: she is not quite looking
                // at anything. And a mouth made for laughing.
                eyeSkew[0] = 1.16f; pupilSkew[0] = .56f;
                eyeSkew[1] = .94f; pupilSkew[1] = 1.14f;
                mouthSize = 1.12f;
            }
            Kit.Spawn("Face detail", head, sculpt != null ? ColosseumModels.Get("orc_head_trim", Team) : detail.ToMesh("Doll face detail"), false, false);

            for (int i = 0; i < 2; i++)
            {
                float side = i == 0 ? -1f : 1f;
                Quaternion turn = Quaternion.Euler(-6f, side * 20f, 0f);
                eyeHome[i] = new Vector3(side * eyeSpot.x, eyeSpot.y, eyeSpot.z);
                eyes[i] = Face("Eye", Kit.UnitBall(0xffffff, .5f), eyeHome[i], new Vector3(EyeWide * eyeSize, EyeTall * eyeSize, .06f), false);
                eyes[i].localRotation = turn;
                // A dark lens behind the white is the eye's outline. Unlike an outline shell it tapers away cleanly
                // where the eye sinks into the face, and it blinks along with the eye.
                Transform rim = Kit.Spawn("Eye rim", eyes[i], Kit.UnitBall(Kit.Ink, 0f), false, false).transform;
                rim.localPosition = new Vector3(0f, 0f, -.3f);
                rim.localScale = new Vector3(1.17f, 1.15f, .72f);
                pupils[i] = Face("Pupil", Kit.UnitBall(0x241a22, .9f), eyeHome[i], new Vector3(PupilWide, PupilTall, .03f) * pupilSize, false);
                pupils[i].localRotation = turn;
                glints[i] = Face("Glint", Kit.UnitBall(0xffffff, 1f), eyeHome[i], new Vector3(.02f, .022f, .012f) * pupilSize, false);
                browHome[i] = new Vector3(side * browSpot.x, browSpot.y, browSpot.z);
                brows[i] = Face("Brow", BrowMesh(side, browHex), browHome[i], browSize, false);
                var cross = new MeshKit { Gradient = 0f };
                Color32 inkColor = MeshKit.Rgb(Kit.Ink, 0f);
                cross.Blob(Vector3.zero, new Vector3(1f, .22f, .3f), Quaternion.Euler(0f, 0f, 45f), inkColor, .5f, .5f, 8);
                cross.Blob(Vector3.zero, new Vector3(1f, .22f, .3f), Quaternion.Euler(0f, 0f, -45f), inkColor, .5f, .5f, 8);
                crosses[i] = Face("Cross", cross.ToMesh("Doll cross eye"), eyeHome[i] + turn * new Vector3(0f, 0f, .05f), Vector3.one * (.1f * Mathf.Lerp(eyeSize, 1f, .4f)), false);
                crosses[i].localRotation = turn;
                crosses[i].gameObject.SetActive(false);
            }
            if (pirate)
            {
                // One eye is under the patch for good.
                patchedEye = 1;
                eyes[1].gameObject.SetActive(false);
            }
            mouth = Face("Mouth", Kit.UnitBall(0x5a1a22, 0f), mouthHome, new Vector3(.09f, .022f, .05f) * mouthSize, false);
            teeth = Face("Teeth", Kit.UnitBall(0xffffff, .4f), mouthHome + new Vector3(0f, .02f, .02f), new Vector3(.1f, .03f, .03f) * mouthSize, false);
            tongue = Face("Tongue", Kit.UnitBall(0xff7f8f, .5f), mouthHome + new Vector3(0f, -.06f, .035f), new Vector3(.06f, .09f, .03f) * mouthSize, false);
            if (Kind == WeaponKind.Sword || Kind == WeaponKind.Bow)
            {
                // Clean-shaven heroes rest with a small confident smile instead of a flat line.
                var curve = new MeshKit { Gradient = 0f };
                Color32 lip = C(0x6a2a22, 0f);
                Vector3 last = Vector3.zero;
                for (int k = 0; k <= 8; k++)
                {
                    float u = k / 4f - 1f;
                    var point = new Vector3(.088f * u, -.186f - .024f * (1f - u * u), .358f - 1.3f * (.088f * u) * (.088f * u));
                    if (k > 0) curve.Capsule(last, point, .013f, .013f, lip, 6);
                    last = point;
                }
                curve.Ball(new Vector3(-.094f, -.181f, .345f), .017f, lip, 6);
                curve.Ball(new Vector3(.094f, -.181f, .345f), .017f, lip, 6);
                smile = Kit.Spawn("Smile", head, curve.ToMesh("Doll smile"), false, false).transform;
            }
            else if (spiky)
            {
                // At rest the porcupine's mouth is a little "w" under the nose, drawn on the front of the muzzle.
                var curve = new MeshKit { Gradient = 0f };
                Color32 lip = C(0x5a3024, 0f);
                curve.Capsule(new Vector3(0f, -.158f, MuzzleFront(0f, -.158f) + .004f), new Vector3(0f, -.19f, MuzzleFront(0f, -.19f) + .006f), .01f, .01f, lip, 6);
                for (int side = -1; side <= 1; side += 2)
                {
                    Vector3 last = Vector3.zero;
                    for (int k = 0; k <= 6; k++)
                    {
                        float u = k / 6f, x = side * .068f * u, y = -.19f - .03f * Mathf.Sin(u * Mathf.PI) + .014f * u;
                        var point = new Vector3(x, y, MuzzleFront(x, y) + .006f);
                        if (k > 0) curve.Capsule(last, point, .011f, .011f, lip, 6);
                        last = point;
                    }
                    curve.Ball(last, .014f, lip, 6);
                }
                smile = Kit.Spawn("Smile", head, curve.ToMesh("Doll smile"), false, false).transform;
            }

            if (spiky)
            {
                // In the air the porcupine is a ball of quills. The ball stands in for the whole body; it turns
                // inside a holder that stays upright, so a bounce flattens it against the ground and not along
                // whichever way it happens to be facing.
                var curl = new MeshKit();
                var points = new MeshKit();
                QuillBall(curl, points, main, light);
                ball = new GameObject("Quill ball").transform;
                ball.SetParent(root, false);
                ballSpin = Kit.Spawn("Quills", ball, curl.ToMesh("Doll quill ball"), true).transform;
                Kit.Spawn("Quill tips", ballSpin, points.ToMesh("Doll quill tips"), false, false);
                ball.gameObject.SetActive(false);
            }

            if (witch)
            {
                for (int i = 0; i < manes.Length; i++)
                {
                    var fall = new MeshKit();
                    WitchHank(fall, i, witchHair, witchHairShade);
                    manes[i] = Kit.Spawn("Mane", root, fall.ToMesh("Doll mane"), true).transform;
                    manes[i].localScale = Vector3.one * size;
                }
            }

            var forged = new MeshKit();
            var fittings = new MeshKit();
            BuildWeapon(Kind, Team, forged, fittings);
            weapon = Kit.Spawn("Weapon", limbs[ArmR], forged.ToMesh("Doll " + Kind), true).transform;
            weapon.localPosition = new Vector3(0f, 0f, .45f);
            Kit.Spawn("Weapon trim", weapon, fittings.ToMesh("Doll weapon trim"), false, false);

            if (brute)
            {
                // The blur of the turning blade: three pale streaks that chase one another round under him as he
                // rides. Drawn on both faces, brightest at the leading end.
                var streaks = new MeshKit { Gradient = 0f };
                const int steps = 11;
                var haze = new Color32(255, 255, 255, 30);
                for (int i = 0; i < 36; i++)
                {
                    // A faint disc the size of its sweep, so the reach of the blade can be read at a glance.
                    float a0 = i * 10f * Mathf.Deg2Rad, a1 = (i + 1) * 10f * Mathf.Deg2Rad;
                    Vector3 d0 = new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)), d1 = new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1));
                    streaks.Quad(Vector3.zero, d0, d1, Vector3.zero, haze, haze, haze, haze);
                    streaks.Quad(Vector3.zero, d1, d0, Vector3.zero, haze, haze, haze, haze);
                }
                for (int k = 0; k < 3; k++)
                {
                    for (int i = 0; i < steps; i++)
                    {
                        float a0 = (k * 120f + i * 9f) * Mathf.Deg2Rad, a1 = (k * 120f + (i + 1) * 9f) * Mathf.Deg2Rad;
                        float t0 = 1f - (float)i / steps, t1 = 1f - (float)(i + 1) / steps;
                        Vector3 d0 = new Vector3(Mathf.Cos(a0), .004f, Mathf.Sin(a0)), d1 = new Vector3(Mathf.Cos(a1), .004f, Mathf.Sin(a1));
                        var c0 = new Color32(255, 255, 255, (byte)(t0 * t0 * 215f));
                        var c1 = new Color32(255, 255, 255, (byte)(t1 * t1 * 215f));
                        Vector3 i0 = new Vector3(d0.x * (1f - .34f * t0), .004f, d0.z * (1f - .34f * t0)), i1 = new Vector3(d1.x * (1f - .34f * t1), .004f, d1.z * (1f - .34f * t1));
                        streaks.Quad(i0, d0, d1, i1, c0, c0, c1, c1);
                        streaks.Quad(i1, d1, d0, i0, c1, c1, c0, c0);
                    }
                }
                whirl = Kit.SpawnStroke("Whirl", root, streaks.ToMesh("Doll whirl"), Kit.StrokeMaterial(Color.white)).transform;
                whirl.gameObject.SetActive(false);
            }

            for (int i = 0; i < 3; i++)
            {
                stars[i] = Kit.Spawn("Dizzy star", root, StarMesh(), true, false).transform;
                stars[i].localScale = Vector3.one * (.16f * size);
                stars[i].gameObject.SetActive(false);
            }
            for (int i = 0; i < sweat.Length; i++)
            {
                sweat[i] = Face("Sweat drop", SweatMesh(), Vector3.zero, Vector3.one, true);
                sweat[i].gameObject.SetActive(false);
            }

            var ringMesh = new MeshKit { Gradient = 0f };
            ringMesh.Ring(Vector3.zero, .50f, .62f, new Color32(255, 255, 255, 255), 36);
            ring = Kit.SpawnStroke("Ring", game.World, ringMesh.ToMesh("Doll ring"), game.RingMaterial(Team, 0)).transform;
            ringRenderer = ring.GetComponent<MeshRenderer>();

            var blob = new MeshKit { Gradient = 0f };
            blob.Disc(Vector3.zero, .78f, .62f, new Color32(60, 30, 26, 120), new Color32(60, 30, 26, 0), 24);
            shade = Kit.SpawnStroke("Contact shadow", game.World, blob.ToMesh("Doll contact shadow"), game.DecalMaterial).transform;
        }

        Transform Face(string label, Mesh mesh, Vector3 position, Vector3 scale, bool ink)
        {
            Transform part = Kit.Spawn(label, head, mesh, ink, false).transform;
            part.localPosition = position;
            part.localScale = scale;
            return part;
        }

        /// <summary>
        /// Weapons point along +Z from the hand. The first mesh takes the outline; the second holds the gems, bindings
        /// and other fittings, which are drawn without one.
        /// </summary>
        static void BuildWeapon(WeaponKind kind, int team, MeshKit kit, MeshKit trim)
        {
            Color32 steel = C(0xbfcde0, 1f), steelLight = C(0xffffff, 1f), steelDark = C(Kit.SteelDark, .8f);
            Color32 gold = C(Kit.Gold, .9f), goldLight = C(0xffe07a, 1f);
            Color32 wood = C(Kit.Wood, .15f), wrap = C(0x5e3a22, .1f), binding = C(0x3e2616, .1f), gem = C(Kit.TeamMain[team], 1f), cream = C(0xf6ead2, 0f);
            Color32 pale = C(Kit.TeamLight[team], 1f);
            Quaternion none = Quaternion.identity, flat = Quaternion.Euler(90f, 0f, 0f);
            switch (kind)
            {
                case WeaponKind.Slam:
                case WeaponKind.Bounce:
                    break;
                case WeaponKind.Star:
                {
                    // A wand: a dark twisted stick with a gold collar, and on the end of it a fat five-pointed star
                    // with a rose stone on either face.
                    Color32 stick = C(0x5d3f52, .2f), stickDark = C(0x3c2838, .15f), rose = C(0xff8f86, 1f);
                    kit.Capsule(new Vector3(0f, 0f, -.14f), new Vector3(0f, 0f, .5f), .04f, .034f, stick, 8);
                    kit.Ball(new Vector3(0f, 0f, -.15f), .06f, gold, 8);
                    for (int i = 0; i < 3; i++)
                        trim.Blob(new Vector3(0f, 0f, .06f + i * .13f), new Vector3(.048f, .048f, .016f), none, stickDark, 1f, 1f, 8);
                    kit.Blob(new Vector3(0f, 0f, .5f), new Vector3(.062f, .062f, .04f), none, gold, .8f, 1f, 10);
                    kit.Plate(new Vector3(0f, 0f, .71f), flat, MeshKit.StarRim(5, .23f, .105f), .09f, .035f, goldLight, gold);
                    for (int s = -1; s <= 1; s += 2)
                        trim.Blob(new Vector3(0f, s * .05f, .71f), new Vector3(.055f, .022f, .055f), none, rose, 1f, 1f, 8);
                    break;
                }
                case WeaponKind.Hook:
                {
                    // A boarding hook longer than its owner is tall: an iron shaft standing in a brass socket, a great
                    // curve at the end of it opening to one side, and a point filed sharp.
                    Color32 iron = C(0x9aa8bd, .95f), ironLight = C(0xf0f4fa, 1f), ironDark = C(0x66728a, .8f);
                    const float shaft = HookShaft, r = HookCurve, sweep = 235f;
                    const int steps = 12;
                    kit.Blob(new Vector3(0f, 0f, .02f), new Vector3(.12f, .12f, .09f), none, gold, .7f, 1f, 14);
                    kit.Blob(new Vector3(0f, 0f, .115f), new Vector3(.098f, .098f, .03f), none, goldLight, 1f, 1f, 12);
                    // One even colour without per-piece shading, so the joints between the pieces do not show.
                    kit.Gradient = 0f;
                    kit.Capsule(new Vector3(0f, 0f, .1f), new Vector3(0f, 0f, shaft), .075f, .068f, iron, 12);
                    var previous = new Vector3(0f, 0f, shaft);
                    for (int i = 1; i <= steps; i++)
                    {
                        float a = (180f - sweep * i / steps) * Mathf.Deg2Rad;
                        var point = new Vector3(r + r * Mathf.Cos(a), 0f, shaft + r * Mathf.Sin(a));
                        kit.Capsule(previous, point, Mathf.Lerp(.068f, .05f, (i - 1f) / steps), Mathf.Lerp(.068f, .05f, (float)i / steps), iron, 10);
                        previous = point;
                    }
                    float end = (180f - sweep) * Mathf.Deg2Rad;
                    var tangent = new Vector3(Mathf.Sin(end), 0f, -Mathf.Cos(end));
                    kit.Gradient = .16f;
                    kit.Spike(previous - tangent * .02f, previous + tangent * .24f, .062f, iron, ironLight);
                    for (int i = 0; i < 2; i++)
                        trim.Blob(new Vector3(0f, 0f, .3f + i * .17f), new Vector3(.084f, .084f, .022f), none, ironDark, 1f, 1f, 10);
                    break;
                }
                case WeaponKind.Boomerang:
                {
                    // A war boomerang as long as its owner is tall: two broad steel arms ground to an edge, meeting
                    // at a gold boss set with a stone in the team colour. He holds it by the bound end of one arm,
                    // and the other stands out to his left; lying flat, it is all in the X-Z plane.
                    var elbow = new Vector3(0f, 0f, BoomerangArm);
                    Color32 blade = C(0x8fa1bd, .9f), bladeDark = C(0x5f6f8c, .8f);
                    Quaternion[] lies = { Quaternion.Euler(0f, 180f, 0f) * flat, Quaternion.Euler(0f, -BoomerangBend, 0f) * flat };
                    float[] spans = { BoomerangArm - .14f, BoomerangArm };
                    for (int i = 0; i < 2; i++)
                    {
                        float span = spans[i];
                        kit.Cut(elbow, lies[i], new[]
                        {
                            new Vector2(.15f, -.13f), new Vector2(.172f, span * .16f), new Vector2(.158f, span * .55f), new Vector2(.118f, span * .86f),
                            new Vector2(.05f, span * 1.01f), new Vector2(-.05f, span * 1.01f), new Vector2(-.118f, span * .86f), new Vector2(-.158f, span * .55f),
                            new Vector2(-.172f, span * .16f), new Vector2(-.15f, -.13f)
                        }, .075f, .07f, blade, steelLight, bladeDark);
                        Quaternion turn = i == 0 ? Quaternion.Euler(0f, 180f, 0f) : Quaternion.Euler(0f, -BoomerangBend, 0f);
                        trim.Chamfer(elbow + turn * new Vector3(0f, 0f, span * .5f), new Vector3(.335f, .092f, .06f), turn, .014f, gold, goldLight);
                        trim.Chamfer(elbow + turn * new Vector3(0f, 0f, span * .8f), new Vector3(.26f, .088f, .04f), turn, .012f, gold, goldLight);
                    }
                    kit.Blob(elbow, new Vector3(.215f, .07f, .215f), none, gold, .8f, 1f, 18);
                    trim.Blob(elbow, new Vector3(.1f, .092f, .1f), none, gem, 1f, 1f, 12);
                    kit.Capsule(new Vector3(0f, 0f, -.15f), new Vector3(0f, 0f, .2f), .047f, .047f, wrap, 10);
                    for (int i = 0; i < 5; i++)
                        trim.Blob(new Vector3(0f, 0f, -.1f + i * .062f), new Vector3(.053f, .053f, .014f), none, i % 2 == 0 ? gem : binding, 1f, 1f, 8);
                    kit.Chamfer(new Vector3(0f, 0f, -.2f), Vector3.one * .12f, Quaternion.Euler(45f, 45f, 0f), .026f, gold, goldLight);
                    break;
                }
                case WeaponKind.Mine:
                {
                    // A landmine carried like a plate: a fat iron disc with a paler rim, brass prongs round the
                    // face and a red button in the middle.
                    Color32 iron = C(0x4a4d5c, .7f), ironLight = C(0x80859a, .8f), button = C(0xff4a3c, 1f);
                    kit.Blob(new Vector3(0f, 0f, .2f), new Vector3(.25f, .25f, .095f), none, iron, .7f, 1f, 18);
                    kit.Blob(new Vector3(0f, 0f, .2f), new Vector3(.272f, .272f, .04f), none, ironLight, .5f, 1f, 18);
                    trim.Blob(new Vector3(0f, 0f, .3f), new Vector3(.095f, .095f, .045f), none, button, 1f, 1f, 12);
                    trim.Ball(new Vector3(-.03f, .035f, .336f), .022f, C(0xffffff, 1f), 5);
                    for (int i = 0; i < 6; i++)
                    {
                        float a = i * 60f * Mathf.Deg2Rad;
                        trim.Blob(new Vector3(Mathf.Cos(a) * .19f, Mathf.Sin(a) * .19f, .285f), new Vector3(.032f, .032f, .03f), none, gold, 1f, 1f, 6);
                    }
                    kit.Blob(new Vector3(0f, 0f, .06f), new Vector3(.05f, .05f, .09f), none, wrap, 1f, 1f, 8);
                    break;
                }
                case WeaponKind.Sword:
                    // Leaf-bladed short sword: bright edges, a dark fuller, a curled guard set with a gem, bound grip.
                    kit.Cut(new Vector3(0f, 0f, .56f), flat, new[]
                    {
                        new Vector2(.06f, -.5f), new Vector2(.098f, -.4f), new Vector2(.108f, -.1f), new Vector2(.098f, .2f), new Vector2(.07f, .37f),
                        new Vector2(0f, .5f), new Vector2(-.07f, .37f), new Vector2(-.098f, .2f), new Vector2(-.108f, -.1f), new Vector2(-.098f, -.4f),
                        new Vector2(-.06f, -.5f)
                    }, .075f, .074f, steel, steelLight, steelDark);
                    kit.Chamfer(new Vector3(0f, 0f, .06f), new Vector3(.4f, .088f, .095f), none, .026f, gold, goldLight);
                    for (int s = -1; s <= 1; s += 2)
                    {
                        kit.Chamfer(new Vector3(s * .215f, 0f, .09f), new Vector3(.1f, .09f, .1f), Quaternion.Euler(0f, 45f, 0f), .022f, gold, goldLight);
                        trim.Gem(new Vector3(0f, s * .042f, .06f), Quaternion.Euler(s > 0 ? 0f : 180f, 0f, 0f), new Vector3(.046f, .03f, .04f), gem, pale);
                    }
                    kit.Capsule(new Vector3(0f, 0f, -.13f), new Vector3(0f, 0f, .03f), .04f, .04f, wrap, 10);
                    for (int i = 0; i < 3; i++)
                        trim.Blob(new Vector3(0f, 0f, -.1f + i * .045f), new Vector3(.046f, .046f, .012f), none, binding, 1f, 1f, 8);
                    kit.Chamfer(new Vector3(0f, 0f, -.18f), Vector3.one * .115f, Quaternion.Euler(45f, 45f, 0f), .026f, gold, goldLight);
                    break;
                case WeaponKind.Axe:
                    // Double-bitted war axe: two crescent blades with bright edges on a banded socket, a spike on top.
                    kit.Capsule(new Vector3(0f, 0f, -.16f), new Vector3(0f, 0f, .74f), .04f, .034f, wood, 10);
                    kit.Ball(new Vector3(0f, 0f, -.18f), .056f, gold, 10);
                    for (int i = 0; i < 3; i++)
                        trim.Blob(new Vector3(0f, 0f, -.08f + i * .08f), new Vector3(.047f, .047f, .02f), none, wrap, 1f, 1f, 8);
                    kit.Chamfer(new Vector3(0f, 0f, .55f), new Vector3(.16f, .125f, .34f), none, .028f, steelDark, steel);
                    for (int s = -1; s <= 1; s += 2)
                    {
                        kit.Cut(new Vector3(0f, 0f, .55f), flat, new[]
                        {
                            new Vector2(s * .05f, .1f), new Vector2(s * .18f, .13f), new Vector2(s * .33f, .27f), new Vector2(s * .40f, .14f),
                            new Vector2(s * .425f, 0f), new Vector2(s * .40f, -.14f), new Vector2(s * .33f, -.27f), new Vector2(s * .18f, -.13f),
                            new Vector2(s * .05f, -.1f)
                        }, .07f, .06f, steel, steelLight, steelDark);
                        trim.Gem(new Vector3(0f, s * .06f, .55f), Quaternion.Euler(s > 0 ? 0f : 180f, 0f, 0f), new Vector3(.055f, .03f, .055f), gold, goldLight);
                        kit.Chamfer(new Vector3(0f, 0f, .55f + s * .12f), new Vector3(.185f, .15f, .05f), none, .014f, gold, goldLight);
                    }
                    kit.Spike(new Vector3(0f, 0f, .72f), new Vector3(0f, 0f, .93f), .055f, steel, steelLight);
                    break;
                case WeaponKind.Hammer:
                    // War hammer: a rounded steel head between gold bands, dark striking faces, gems and a finial.
                    kit.Capsule(new Vector3(0f, 0f, -.16f), new Vector3(0f, 0f, .6f), .042f, .038f, wood, 10);
                    kit.Ball(new Vector3(0f, 0f, -.18f), .058f, gold, 10);
                    for (int i = 0; i < 3; i++)
                        trim.Blob(new Vector3(0f, 0f, -.08f + i * .08f), new Vector3(.049f, .049f, .02f), none, wrap, 1f, 1f, 8);
                    kit.Chamfer(new Vector3(0f, 0f, .49f), new Vector3(.13f, .13f, .07f), none, .02f, gold, goldLight);
                    kit.Chamfer(new Vector3(0f, 0f, .68f), new Vector3(.58f, .33f, .32f), none, .05f, C(0xc6d3e4, .9f), C(0xf4f8fc, 1f));
                    for (int s = -1; s <= 1; s += 2)
                    {
                        kit.Chamfer(new Vector3(s * .3f, 0f, .68f), new Vector3(.07f, .26f, .25f), none, .022f, steelDark, steel);
                        kit.Chamfer(new Vector3(s * .185f, 0f, .68f), new Vector3(.085f, .365f, .355f), none, .02f, gold, goldLight);
                        trim.Chamfer(new Vector3(0f, s * .165f, .68f), new Vector3(.17f, .03f, .17f), Quaternion.Euler(0f, 45f, 0f), .012f, goldLight, C(0xfff2b8, 1f));
                        trim.Gem(new Vector3(0f, s * .178f, .68f), Quaternion.Euler(s > 0 ? 0f : 180f, 0f, 0f), new Vector3(.07f, .04f, .07f), gem, pale);
                    }
                    kit.Spike(new Vector3(0f, 0f, .83f), new Vector3(0f, 0f, .97f), .06f, gold, goldLight);
                    break;
                default:
                {
                    // Recurve bow with gold tips and a bound grip, strung, with an arrow on the string.
                    const int steps = 16;
                    Vector3 previous = Vector3.zero, bottom = Vector3.zero, top = Vector3.zero;
                    kit.Gradient = 0f;
                    for (int i = 0; i <= steps; i++)
                    {
                        float t = (float)i / steps * 2f - 1f, a = t * 70f * Mathf.Deg2Rad, curl = Mathf.Max(0f, Mathf.Abs(t) - .72f);
                        var point = new Vector3(.47f * Mathf.Sin(a), 0f, .47f * Mathf.Cos(a) - .35f + curl * curl * 2.2f);
                        if (i > 0)
                            kit.Capsule(previous, point, Mathf.Lerp(.042f, .022f, Mathf.Abs(t - 2f / steps)), Mathf.Lerp(.042f, .022f, Mathf.Abs(t)), wood, 8);
                        if (i == 0) bottom = point;
                        if (i == steps) top = point;
                        previous = point;
                    }
                    kit.Gradient = .16f;
                    kit.Ball(bottom, .042f, gold, 8);
                    kit.Ball(top, .042f, gold, 8);
                    kit.Capsule(bottom, top, .009f, .009f, cream, 6);
                    kit.Blob(new Vector3(0f, 0f, .12f), new Vector3(.054f, .054f, .085f), none, wrap, 1f, 1f, 10);
                    trim.Blob(new Vector3(0f, 0f, .05f), new Vector3(.058f, .058f, .014f), none, gold, 1f, 1f, 8);
                    trim.Blob(new Vector3(0f, 0f, .19f), new Vector3(.058f, .058f, .014f), none, gold, 1f, 1f, 8);
                    kit.Capsule(new Vector3(0f, 0f, -.18f), new Vector3(0f, 0f, .4f), .016f, .016f, cream, 6);
                    kit.Plate(new Vector3(0f, 0f, .46f), flat, new[]
                    {
                        new Vector2(0f, .1f), new Vector2(.055f, -.05f), new Vector2(0f, -.02f), new Vector2(-.055f, -.05f)
                    }, .03f, .02f, steel, steelLight);
                    for (int s = -1; s <= 1; s += 2)
                        kit.Blob(new Vector3(s * .03f, 0f, -.12f), new Vector3(.03f, .009f, .06f), none, s < 0 ? gem : C(Kit.TeamLight[team], .3f), 1f, 1f, 8);
                    break;
                }
            }
        }

        // ------------------------------------------------------------------ state

        void EnterState(DollState next)
        {
            State = next;
            StateTime = 0f;
            slowTime = 0f;
            if (Body != null && !Body.isKinematic)
                Body.linearDamping = next == DollState.Fly ? .5f : next == DollState.Tumble ? .75f : IdleDamping;
        }

        public void BeginAim()
        {
            if (!Alive) return;
            aimPull = Vector3.zero;
            aimPower = 0f;
            EnterState(DollState.Aim);
        }

        public void SetAim(Vector3 pull, float power)
        {
            aimPull = pull;
            aimPower = power;
            if (pull.sqrMagnitude > .0004f)
                Facing = -pull.normalized;
        }

        public void CancelAim()
        {
            if (State == DollState.Aim)
                EnterState(DollState.Idle);
        }

        public bool Leaping { get { return leaping; } }

        // ---- the gladiator's boomerang
        /// <summary>How long each arm of the boomerang is, and how far the second is bent round from the first.</summary>
        const float BoomerangArm = 1f, BoomerangBend = 75f;
        /// <summary>The middle of the boomerang, which it turns about in flight, in its own measure.</summary>
        static readonly Vector3 BladeMiddle = new Vector3(-.24f, 0f, .84f);
        /// <summary>In flight it is drawn this much larger, turns this many degrees a second, and its blur reaches this far out.</summary>
        const float RideGrow = 1.25f, RideWhirl = 880f, WhirlReach = 1.06f;
        /// <summary>Up on his boomerang, out of the physics, carried round its loop by the game.</summary>
        public bool Riding { get { return riding; } }
        /// <summary>
        /// How far the boomerang has got away from him, 0 to 1: it rocks and dips under him as it turns, and he
        /// throws his arms about to stay on.
        /// </summary>
        public float RideWobble { get; set; }
        bool riding;
        float rideSpin, rideLift;
        Transform whirl;

        /// <summary>How the boomerang, and he on it, is tipped over just now: level while he has it in hand.</summary>
        Quaternion RideTilt()
        {
            float lurch = RideWobble;
            return lurch > 0f
                ? Quaternion.Euler(Mathf.Sin(wobble * 12.5f) * 26f * lurch, 0f, Mathf.Cos(wobble * 9.5f) * 26f * lurch)
                : Quaternion.identity;
        }

        // ---- the dawn witch's barrage
        /// <summary>True while she is loosing her stars: out of the physics, and thrown about by her own wand.</summary>
        public bool Casting { get { return casting; } }
        bool casting;
        float castLift, castLean, castFlick;
        /// <summary>The star on the end of her wand, where the falling ones set out from.</summary>
        public Vector3 WandTip { get { return weapon.position + weapon.forward * (.7f * size); } }

        /// <summary>Feet planted and wand up: she starts her barrage, facing where she means it to fall.</summary>
        public void BeginCast(Vector3 facing)
        {
            if (facing.sqrMagnitude > .001f) Facing = Kit.Flat(facing).normalized;
            cooldowns.Clear();
            squash = .35f;
            EnterState(DollState.Fly);
            if (!Body.isKinematic) Body.linearVelocity = Vector3.zero;
            Body.isKinematic = true;
            Body.interpolation = RigidbodyInterpolation.None;
            Shape.enabled = false;
            leaping = false;
            casting = true;
            castLift = castLean = castFlick = 0f;
        }

        /// <summary>
        /// Where the kick of her wand has thrown her this frame: the spot under her, how far off the sand she is,
        /// how far she is tipped over backwards (in degrees), and which way she is facing.
        /// </summary>
        public void Cast(Vector3 point, Vector3 facing, float lift, float lean)
        {
            if (!casting) return;
            var ground = new Vector3(point.x, 0f, point.z);
            transform.position = ground;
            Body.position = ground;
            if (facing.sqrMagnitude > .001f) Facing = Kit.Flat(facing).normalized;
            castLift = lift;
            castLean = lean;
        }

        /// <summary>A star leaves the wand: she throws it up at the sky, on tiptoe, and jolts.</summary>
        public void Flick()
        {
            castFlick = 1f;
            squash = -.3f;
        }

        /// <summary>The barrage is over: back into the physics wherever it has left her, on her feet.</summary>
        public void EndCast()
        {
            if (!casting) return;
            casting = false;
            castLift = castLean = castFlick = 0f;
            Body.isKinematic = false;
            Body.interpolation = RigidbodyInterpolation.Interpolate;
            Shape.enabled = true;
            Body.linearVelocity = Vector3.zero;
            squash = .5f;
            EnterState(DollState.Idle);
        }

        /// <summary>Up onto the great boomerang and away.</summary>
        public void BeginRide(Vector3 heading)
        {
            if (heading.sqrMagnitude > .001f) Facing = heading.normalized;
            cooldowns.Clear();
            squash = -.45f;
            EnterState(DollState.Fly);
            if (!Body.isKinematic) Body.linearVelocity = Vector3.zero;
            Body.isKinematic = true;
            Body.interpolation = RigidbodyInterpolation.None;
            Shape.enabled = false;
            leaping = false;
            riding = true;
            RideWobble = 0f;
            rideLift = 0f;
            // It starts out flat in front of him, as he held it to throw.
            rideSpin = Quaternion.LookRotation(Kit.Flat(Facing).normalized).eulerAngles.y;
        }

        /// <summary>Where the boomerang has carried him this frame, how high it flies, and which way it is heading.</summary>
        public void Ride(Vector3 point, Vector3 heading, float lift)
        {
            if (!riding) return;
            var ground = new Vector3(point.x, 0f, point.z);
            transform.position = ground;
            Body.position = ground;
            if (heading.sqrMagnitude > .001f) Facing = heading.normalized;
            rideLift = lift;
        }

        /// <summary>Off the boomerang and down on his feet where it has brought him, the blade back in his hand.</summary>
        public void EndRide(Vector3 spot, Vector3 facing)
        {
            if (!riding) return;
            riding = false;
            RideWobble = 0f;
            spot.y = 0f;
            transform.position = spot;
            Body.position = spot;
            Body.isKinematic = false;
            Body.interpolation = RigidbodyInterpolation.Interpolate;
            Shape.enabled = true;
            Body.linearVelocity = Vector3.zero;
            if (facing.sqrMagnitude > .001f) Facing = Kit.Flat(facing).normalized;
            weapon.localPosition = new Vector3(0f, 0f, .45f);
            weapon.localScale = Vector3.one;
            squash = .65f;
            EnterState(DollState.Idle);
        }

        // ---- the pirate's hook
        /// <summary>How far along the hook, from the pirate's wrist, its straight shaft runs before the curve.</summary>
        public const float HookShaft = 1.9f;
        const float HookCurve = .4f, GunReach = 1.02f;
        /// <summary>Caught on the pirate's hook: out of the physics, and carried wherever the hook goes.</summary>
        public bool Caught { get; private set; }
        /// <summary>The pirate standing braced with his catch, hook held out level and gun coming up.</summary>
        public bool Gunning { get; private set; }
        /// <summary>The pirate's wrist, where the hook begins, and the way the hook points.</summary>
        public Vector3 HookRoot { get { return weapon.position; } }
        public Vector3 HookAlong { get { return weapon.forward; } }
        /// <summary>The mouth of the pirate's gun.</summary>
        public Vector3 Muzzle { get { return limbs[ArmL].position + limbs[ArmL].forward * (GunReach * size); } }
        Vector3 hangAlong = Vector3.forward;
        float hangLift;
        int hangSeat, patchedEye = -1;

        /// <summary>Caught on the hook: the hero goes limp and leaves the physics until it is let go.</summary>
        public void Catch(int seat)
        {
            Caught = true;
            hangSeat = seat;
            hangLift = .55f * size;   // where it is, until the hook has placed it
            leaping = false;
            cooldowns.Clear();
            if (State != DollState.Tumble)
            {
                tumble = pivot.rotation;
                EnterState(DollState.Tumble);
            }
            spinSpeed = 0f;
            if (!Body.isKinematic) Body.linearVelocity = Vector3.zero;
            Body.isKinematic = true;
            Body.interpolation = RigidbodyInterpolation.None;
            Shape.enabled = false;
        }

        /// <summary>Where on the hook the hero hangs this frame, and which way the hook is travelling.</summary>
        public void Hang(Vector3 point, Vector3 along)
        {
            if (!Caught) return;
            hangAlong = along;
            hangLift = point.y - .3f * size;
            var ground = new Vector3(point.x, 0f, point.z);
            transform.position = ground;
            Body.position = ground;
        }

        /// <summary>Off the hook and back into the physics, at the height it was hanging.</summary>
        public void LetGo()
        {
            if (!Caught) return;
            Caught = false;
            Vector3 at = transform.position + Vector3.up * Mathf.Max(0f, hangLift - .55f * size);
            transform.position = at;
            Body.position = at;
            Body.isKinematic = false;
            Body.interpolation = RigidbodyInterpolation.Interpolate;
            Shape.enabled = true;
            StateTime = 0f;
            slowTime = 0f;
        }

        /// <summary>The pirate pulls up on his feet with his catch still hooked, levels the hook and raises his gun.</summary>
        void Brace()
        {
            Gunning = true;
            squash = .45f;
            EnterState(DollState.Idle);
            game.OnHookStops(this);
        }

        public void LowerGun() { Gunning = false; }

        // ---- the viking's whirlwind
        /// <summary>The viking turning on the spot at the heart of his whirlwind: out of the physics until it lets him down.</summary>
        public bool Whirling { get { return whirling; } }
        bool whirling;
        float whirlTurn, whirlRate, whirlLift, whirlRise;

        /// <summary>He pulls up where he is and begins to turn, axe out.</summary>
        public void BeginWhirl()
        {
            Vector3 flat = Kit.Flat(Facing);
            whirlTurn = flat.sqrMagnitude > .001f ? Quaternion.LookRotation(flat.normalized).eulerAngles.y : 0f;
            cooldowns.Clear();
            squash = .4f;
            EnterState(DollState.Fly);
            if (!Body.isKinematic) Body.linearVelocity = Vector3.zero;
            Body.isKinematic = true;
            Body.interpolation = RigidbodyInterpolation.None;
            Shape.enabled = false;
            leaping = false;
            whirling = true;
            whirlRate = 0f;
            // However far off the sand his flight had him, that is where he starts from.
            whirlRise = whirlLift = Mathf.Max(0f, transform.position.y);
            Vector3 ground = Kit.Flat(transform.position);
            transform.position = ground;
            Body.position = ground;
        }

        /// <summary>Where he stands this frame, how fast he is turning (in degrees a second) and how far off the sand the wind has him.</summary>
        public void Whirl(Vector3 point, float rate, float lift)
        {
            if (!whirling) return;
            var ground = new Vector3(point.x, 0f, point.z);
            transform.position = ground;
            Body.position = ground;
            whirlRate = rate;
            whirlLift = lift;
        }

        /// <summary>It has blown itself out: back into the physics where he stands, and down he goes with his head still turning.</summary>
        public void EndWhirl()
        {
            if (!whirling) return;
            whirling = false;
            whirlRate = whirlLift = whirlRise = 0f;
            Facing = Quaternion.Euler(0f, whirlTurn, 0f) * Vector3.forward;
            Body.isKinematic = false;
            Body.interpolation = RigidbodyInterpolation.Interpolate;
            Shape.enabled = true;
            Body.linearVelocity = Vector3.zero;
            Collapse();
        }

        // ---- taken by the whirlwind
        /// <summary>Taken up by the viking's whirlwind, or thrown out of the top of it: out of the physics, and placed by hand.</summary>
        public bool Swept { get { return swept; } }
        /// <summary>Thrown out of the whirlwind and not yet back on the sand.</summary>
        public bool Hurled { get { return hurled; } }
        bool swept, hurled;
        Vector3 hurlFrom, hurlTo;
        float hurlTime, hurlSpan, hurlPeak;
        /// <summary>How much of a flight out of the whirlwind is the way up; the rest is the fall.</summary>
        public const float HurlClimb = .4f;
        /// <summary>How far above the sand a hero falling out of the sky is given back to the physics, and how fast it is going down by then.</summary>
        const float DropHeight = 1.2f, DropSpeed = 13f;

        /// <summary>The wind has it: limp, head over heels, and out of the physics until it is thrown clear or let fall.</summary>
        public void Sweep()
        {
            if (swept) return;
            Caught = true;
            swept = true;
            hurled = false;
            leaping = false;
            cooldowns.Clear();
            if (State != DollState.Tumble)
            {
                tumble = pivot.rotation;
                EnterState(DollState.Tumble);
            }
            spinAxis = (Vector3.up + Random.insideUnitSphere * .8f).normalized;
            spinSpeed = Random.Range(520f, 760f) * (Random.value < .5f ? -1f : 1f);
            if (!Body.isKinematic) Body.linearVelocity = Vector3.zero;
            Body.isKinematic = true;
            Body.interpolation = RigidbodyInterpolation.None;
            Shape.enabled = false;
        }

        /// <summary>Where the wind has carried it this frame: its feet, wherever in the air they are.</summary>
        public void Carry(Vector3 point)
        {
            if (!swept || hurled) return;
            transform.position = point;
            Body.position = point;
        }

        /// <summary>
        /// Thrown out of the top of the whirlwind: up to this height, out of sight, and after so many seconds down
        /// out of the sky on to this spot. One the whirlwind has finished goes up and does not come down.
        /// </summary>
        public void Hurl(Vector3 landing, float seconds, float peak)
        {
            if (!swept || hurled) return;
            hurled = true;
            hurlFrom = transform.position;
            hurlTo = new Vector3(landing.x, 0f, landing.z);
            hurlTime = 0f;
            hurlSpan = Mathf.Max(.4f, seconds);
            hurlPeak = Mathf.Max(peak, hurlFrom.y + 2f);
            spinAxis = Random.onUnitSphere;
            spinSpeed = Random.Range(980f, 1420f) * (Random.value < .5f ? -1f : 1f);
            squash = -.55f;
        }

        /// <summary>
        /// Where one thrown out of the whirlwind is, so far through its flight: all but straight up, fastest as it
        /// leaves; across far overhead; and down on its spot as steeply, fastest as it arrives.
        /// </summary>
        Vector3 HurlPlace(float t)
        {
            Vector3 at = Vector3.Lerp(hurlFrom, hurlTo, t * t * (3f - 2f * t));
            if (t < HurlClimb)
            {
                at.y = Mathf.Lerp(hurlPeak, hurlFrom.y, Mathf.Pow(1f - t / HurlClimb, 2.2f));
            }
            else
            {
                float fallen = (t - HurlClimb) / (1f - HurlClimb);
                at.y = Mathf.Lerp(hurlPeak, DropHeight, fallen * fallen);
            }
            return at;
        }

        /// <summary>Out of the wind and back into the physics where it is, moving as given.</summary>
        public void Release(Vector3 velocity)
        {
            if (!swept) return;
            swept = hurled = false;
            Caught = false;
            Body.position = transform.position;
            Body.isKinematic = false;
            Body.interpolation = RigidbodyInterpolation.Interpolate;
            Shape.enabled = true;
            Body.linearVelocity = velocity;
            StateTime = 0f;
            slowTime = 0f;
        }

        /// <summary>Set down on the sand at once, wherever the wind had it or was sending it, and left to lie.</summary>
        public void SetDown()
        {
            if (!swept) return;
            Vector3 spot = Kit.Flat(hurled ? hurlTo : transform.position) + Vector3.up * .05f;
            transform.position = spot;
            Release(Vector3.zero);
        }

        /// <summary>Gone up into the sky for good: finished, and not seen again.</summary>
        void Vanish()
        {
            swept = hurled = false;
            Caught = false;
            Lost = true;
            gone = true;
            Alive = false;
            Health = 0;
            foreach (Transform part in transform) part.gameObject.SetActive(false);
            shade.gameObject.SetActive(false);
            ring.gameObject.SetActive(false);
        }

        /// <summary>Where a leaper is aiming to come down; the game sets it while the sling is drawn.</summary>
        public Vector3 LandingAim { get; set; }

        /// <summary>
        /// Jump in a high arc and come down on a spot instead of sliding along the ground. In the air the body is
        /// out of the physics, so it sails over everyone in between; the game is told when it lands.
        /// </summary>
        public void Leap(Vector3 target, float seconds, float peak, bool hop = false)
        {
            // A hop only touches the ground: the hero stays out of the physics, and the game sends it up again.
            touchAndGo = hop;
            Vector3 toward = Kit.Flat(target - transform.position);
            if (toward.sqrMagnitude > .001f) Facing = toward.normalized;
            cooldowns.Clear();
            // Off the ground with a stretch; straight out of a bounce, still flat from hitting the sand.
            squash = bounced ? .68f : -.5f;
            bounced = false;
            EnterState(DollState.Fly);
            if (!Body.isKinematic) Body.linearVelocity = Vector3.zero;   // a hero between hops is out of the physics already
            Body.isKinematic = true;
            // The flight is placed by hand every frame. Interpolation would keep pulling the transform back to the
            // body's last physics pose, so the hero (and the plate above him) shook all the way through the arc.
            Body.interpolation = RigidbodyInterpolation.None;
            Shape.enabled = false;
            leaping = true;
            leapFrom = transform.position;
            leapTo = new Vector3(target.x, leapFrom.y, target.z);
            leapTime = 0f;
            leapSpan = Mathf.Max(.2f, seconds);
            leapPeak = peak;
        }

        void Land()
        {
            leaping = false;
            transform.position = leapTo;
            Body.position = leapTo;
            if (touchAndGo)
            {
                bounced = true;
                game.OnDollHops(this, leapTo);
                return;
            }
            Body.isKinematic = false;
            Body.interpolation = RigidbodyInterpolation.Interpolate;
            Shape.enabled = true;
            Body.linearVelocity = Vector3.zero;
            Collapse();
            // Flat on the belly, facing the way he jumped.
            faceUp = false;
            lie = Quaternion.LookRotation(Vector3.down, Kit.Flat(Facing).normalized);
            squash = .7f;
            game.OnDollSlams(this, leapTo);
        }

        public void Launch(Vector3 direction, float speed)
        {
            Facing = direction;
            cooldowns.Clear();
            arrowTimer = FirstSideVolleyDelay;
            roll = 0f;
            flatSpin = 0f;
            squash = -.5f;
            EnterState(DollState.Fly);
            Body.linearVelocity = direction * speed + Vector3.up * 2.2f;
        }

        /// <summary>Send the whole body flying: sets its velocity and starts the tumble.</summary>
        public void Knock(Vector3 velocity)
        {
            if (Body.isKinematic) return;
            Body.linearVelocity = velocity;
            StartTumble(velocity);
        }

        /// <summary>Start tumbling with whatever velocity physics already gave the body.</summary>
        public void StartTumble(Vector3 along)
        {
            Vector3 flat = Kit.Flat(along);
            if (State != DollState.Tumble)
            {
                tumble = pivot.rotation;
                EnterState(DollState.Tumble);
            }
            Vector3 axis = flat.sqrMagnitude > .01f ? Vector3.Cross(Vector3.up, flat.normalized) : Random.onUnitSphere;
            spinAxis = (axis + Random.insideUnitSphere * .45f).normalized;
            spinSpeed = Random.Range(560f, 980f) * (Random.value < .5f ? -1f : 1f);
            if (flat.sqrMagnitude > .01f)
                spinSpeed = Mathf.Abs(spinSpeed);
            StateTime = 0f;
            slowTime = 0f;
        }

        public void SpinFlat(float degreesPerSecond) { flatSpin = degreesPerSecond; }
        public void Squash(float amount) { squash = Mathf.Max(squash, amount); }
        public void FlipSpin() { spinSpeed *= -.8f; }

        public int TakeDamage(int amount)
        {
            if (!Alive) return 0;
            if (Shield > 0 && amount > 0)
            {
                // The shield takes what it can; only what is left over reaches the hero.
                int held = Mathf.Min(Shield, amount);
                Shield -= held;
                amount -= held;
                if (game != null) game.OnShieldHolds(this, held);
                if (amount <= 0) return 0;
            }
            int dealt = Mathf.Min(Health, amount);
            Health -= dealt;
            if (Health <= 0)
                Alive = false;
            return dealt;
        }

        /// <summary>Gives health back, up to what the hero started with; returns how much it took.</summary>
        public int Heal(int amount)
        {
            if (!Alive || amount <= 0) return 0;
            int gained = Mathf.Min(MaxHealth - Health, amount);
            Health += gained;
            return gained;
        }

        /// <summary>
        /// Somewhere else in an instant, speed and all (a portal). Everything that trails after the body goes
        /// with it, so that nothing is left stretched across the arena.
        /// </summary>
        public void Warp(Vector3 to)
        {
            Vector3 shift = to - transform.position;
            transform.position = to;
            Body.position = to;
            lastHead += shift;
            for (int i = 0; i < tips.Length; i++)
            {
                tips[i] += shift;
                previousTips[i] += shift;
            }
            for (int i = 0; i < maneTip.Length; i++)
            {
                maneTip[i] += shift;
                maneWas[i] += shift;
            }
        }

        public bool OnCooldown(ColosseumDoll other)
        {
            return cooldowns.TryGetValue(other.Id, out float left) && left > 0f;
        }

        public void SetCooldown(ColosseumDoll other, float seconds) { cooldowns[other.Id] = seconds; }

        public void StandUp()
        {
            if (State == DollState.Down && Alive)
                EnterState(DollState.GetUp);
        }

        /// <summary>
        /// Out through a gap in the wall. The hero is finished at once, whatever health it had: it tips over the
        /// edge where the wall stood, drops into the ditch and is gone.
        /// </summary>
        public void FallOut(Vector3 outward)
        {
            if (Lost) return;
            Vector3 speed = Body.isKinematic ? Vector3.zero : Body.linearVelocity;
            Lost = true;
            Alive = false;
            Health = 0;
            leaping = false;
            riding = false;
            casting = false;
            whirling = false;
            RideWobble = 0f;
            StartTumble(speed.sqrMagnitude > .01f ? speed : outward);
            spinSpeed = Mathf.Max(Mathf.Abs(spinSpeed), 620f) * (spinSpeed < 0f ? -1f : 1f);
            if (!Body.isKinematic)
            {
                Body.linearVelocity = Vector3.zero;
                Body.isKinematic = true;
            }
            Body.interpolation = RigidbodyInterpolation.None;
            Shape.enabled = false;
            // Over the edge at no more than a trot, so the drop is seen before the stands hide it.
            float pace = Mathf.Clamp(Vector3.Dot(Kit.Flat(speed), outward), 1.6f, 3.6f);
            fallSpeed = outward * pace + Vector3.forward * (Mathf.Clamp(speed.z, -3f, 3f) * .4f) + Vector3.up * 3.2f;
            fallTime = 0f;
            shade.gameObject.SetActive(false);
            ring.gameObject.SetActive(false);
        }

        /// <summary>A knocked-out toy stays where it fell and stops blocking the arena.</summary>
        public void Retire()
        {
            if (Body.isKinematic) return;
            Body.linearVelocity = Vector3.zero;
            Body.isKinematic = true;
            Shape.enabled = false;
        }

        void Collapse()
        {
            Vector3 along = Kit.Flat(pivot.up);
            if (along.sqrMagnitude < .09f)
                along = Kit.Flat(Random.onUnitSphere);
            if (along.sqrMagnitude < .001f)
                along = Vector3.forward;
            faceUp = Random.value < .72f;
            lie = Quaternion.LookRotation(faceUp ? Vector3.up : Vector3.down, along.normalized);
            for (int i = 0; i < 4; i++)
            {
                float side = i % 2 == 0 ? -1f : 1f;
                sprawl[i] = i < 2 ? new Vector3(side, Random.Range(-.6f, .95f), Random.Range(-.1f, .1f))
                    : new Vector3(side * Random.Range(.25f, 1f), -1f, Random.Range(-.1f, .1f));
            }
            squash = .55f;
            EnterState(Alive ? DollState.Down : DollState.Ko);
            game.OnDollCollapsed(this);
        }

        void FixedUpdate()
        {
            if (Body == null || Body.isKinematic)
                return;

            float delta = Time.fixedDeltaTime;
            PreVelocity = Body.linearVelocity;
            cooldownKeys.Clear();
            cooldownKeys.AddRange(cooldowns.Keys);
            foreach (int key in cooldownKeys)
                cooldowns[key] -= delta;

            Body.AddForce(Vector3.down * 15f, ForceMode.Acceleration);
            Vector3 flat = Kit.Flat(PreVelocity);
            float speed = flat.magnitude;
            bool moving = State == DollState.Fly || State == DollState.Tumble;
            if (!moving)
                return;

            // On ice the ground and the air close to it hold a sliding hero far less.
            Body.linearDamping = (State == DollState.Fly ? .5f : .75f) * (Grounded ? Mathf.Lerp(.1f, 1f, Grip) : 1f);
            if (Grounded && speed > .01f)
            {
                float drag = (State == DollState.Tumble ? 5.5f : 1.6f) * Grip;
                Vector3 slowed = flat.normalized * Mathf.Max(0f, speed - drag * delta);
                Body.linearVelocity = new Vector3(slowed.x, Body.linearVelocity.y, slowed.z);
            }

            if (Grounded && speed > 2.2f)
            {
                trailLeft -= speed * delta;
                if (trailLeft <= 0f)
                {
                    trailLeft = Random.Range(.32f, .5f);
                    game.OnDollSkids(this, flat / speed, speed);
                }
            }

            if (State == DollState.Fly && Kind == WeaponKind.Bow && speed > 3.2f)
            {
                arrowTimer -= delta;
                if (arrowTimer <= 0f)
                {
                    arrowTimer = .085f;
                    Vector3 direction = flat / speed;
                    Vector3 side = Vector3.Cross(Vector3.up, direction);
                    game.FireArrow(this, transform.position + side * SideArrowOffset, SideArrowVelocity(flat, side));
                    game.FireArrow(this, transform.position - side * SideArrowOffset, SideArrowVelocity(flat, -side));
                }
            }

            // Out of the arena altogether, or down through the sand: only something gone badly wrong can do that,
            // but the turn would wait for ever for it to come to rest. It is set down inside, at the nearest place.
            Vector3 where = Body.position;
            if (where.y < -1.5f || Mathf.Abs(where.x) > ColosseumGame.HalfWidth + 4f || Mathf.Abs(where.z) > ColosseumGame.HalfDepth + 4f)
            {
                Warp(new Vector3(Mathf.Clamp(where.x, 1f - ColosseumGame.HalfWidth, ColosseumGame.HalfWidth - 1f), .3f,
                    Mathf.Clamp(where.z, 1f - ColosseumGame.HalfDepth, ColosseumGame.HalfDepth - 1f)));
                Body.linearVelocity = Vector3.zero;
            }

            // Come to rest up on something (a supply crate, a ledge): nobody gets to their feet up there, and the
            // turn would wait for it for ever. It is tipped off, towards the middle of the court.
            perchTime = !Grounded && Body.linearVelocity.sqrMagnitude < .09f ? perchTime + delta : 0f;
            if (perchTime > .3f)
            {
                perchTime = 0f;
                Vector3 inward = Kit.Flat(-transform.position);
                Body.linearVelocity = (inward.sqrMagnitude > .01f ? inward.normalized : Vector3.forward) * 3.2f + Vector3.up * 2.6f;
            }

            float limit = State == DollState.Fly ? 1.3f : .9f;
            slowTime = Grounded && speed < limit && Mathf.Abs(PreVelocity.y) < 1.2f ? slowTime + delta : 0f;
            if (slowTime > .12f)
            {
                Body.linearVelocity = Vector3.zero;
                // With heroes on his hook the pirate does not flop over like the rest: he has shooting to do.
                if (Kind == WeaponKind.Hook && game.HookLoaded(this)) Brace();
                else Collapse();
            }
        }

        void OnCollisionEnter(Collision collision)
        {
            if (game == null) return;
            var other = collision.collider.GetComponent<ColosseumDoll>();
            Vector3 point = collision.contactCount > 0 ? collision.GetContact(0).point : transform.position;
            if (other != null)
            {
                if (Id < other.Id)
                    game.OnDollsCollide(this, other, collision.relativeVelocity, point);
                return;
            }
            Vector3 normal = collision.contactCount > 0 ? collision.GetContact(0).normal : Vector3.up;
            game.OnDollHitsArena(this, point, normal, collision.relativeVelocity);
        }

        // ------------------------------------------------------------------ animation

        void LateUpdate()
        {
            float delta = Time.deltaTime;
            if (game == null || delta <= 0f || gone)
                return;

            if (Lost)
            {
                fallTime += delta;
                fallSpeed.y -= 30f * delta;
                transform.position += fallSpeed * delta;
                Body.position = transform.position;
                if (fallTime >= FallSpan)
                {
                    gone = true;
                    foreach (Transform part in transform) part.gameObject.SetActive(false);
                    return;
                }
            }

            if (leaping)
            {
                leapTime += delta;
                float t = Mathf.Clamp01(leapTime / leapSpan);
                Vector3 along = Vector3.Lerp(leapFrom, leapTo, t);
                along.y = leapFrom.y + leapPeak * 4f * t * (1f - t);
                transform.position = along;
                Body.position = along;
                if (t >= 1f) Land();
            }

            if (hurled)
            {
                hurlTime += delta;
                float t = Mathf.Clamp01(hurlTime / hurlSpan);
                Vector3 at = HurlPlace(t);
                transform.position = at;
                Body.position = at;
                if (!Alive && t >= HurlClimb)
                {
                    // Finished by the whirlwind: it goes on up, and is not seen again.
                    Vanish();
                    game.OnDollGoneSkyward(this, at);
                    return;
                }
                if (t >= 1f)
                {
                    // It comes straight down: what little it has left of the way it was thrown will not carry it to a wall.
                    Vector3 drift = Kit.Flat(hurlTo - hurlFrom);
                    Release((drift.sqrMagnitude > .01f ? drift.normalized * .5f : Vector3.zero) + Vector3.down * DropSpeed);
                    game.OnDollComesDown(this, hurlTo);
                }
            }

            StateTime += delta;
            wobble += delta;
            dread = Mathf.MoveTowards(dread, Targeted && Alive && State == DollState.Idle ? 1f : 0f, delta * 7f);
            squash = Mathf.MoveTowards(squash, 0f, delta * 3.2f);
            Vector3 feet = transform.position;
            Vector3 flatFacing = Kit.Flat(Facing);
            if (flatFacing.sqrMagnitude < .001f) flatFacing = Vector3.forward;
            Quaternion yaw = Quaternion.LookRotation(flatFacing.normalized);
            Quaternion rotation = yaw;
            Vector3 position = feet + Vector3.up * standHeight;
            Vector3 stretch = Vector3.one;

            switch (State)
            {
                case DollState.Idle:
                    position.y += (Mathf.Sin(wobble * 3.1f) * .012f + (Cheering ? Mathf.Abs(Mathf.Sin(wobble * 9f)) * .14f : 0f)) * scale;
                    Quaternion pose = dread > 0f ? Quaternion.AngleAxis(-15f * dread + Mathf.Sin(wobble * 47f) * 4f * dread, yaw * Vector3.right) * yaw : yaw;
                    rotation = Quaternion.Slerp(pivot.rotation, pose, 1f - Mathf.Exp(-12f * delta));
                    if (dread > 0f)
                    {
                        // Knees knocking: a fast shiver, shrinking down and leaning back from whoever is aiming.
                        position += new Vector3(Mathf.Sin(wobble * 61f), 0f, Mathf.Cos(wobble * 53f)) * (.03f * scale * dread);
                        position.y -= .06f * scale * dread;
                        stretch = new Vector3(1f + .05f * dread, 1f - .07f * dread, 1f + .05f * dread);
                    }
                    break;
                case DollState.Aim:
                {
                    Vector3 right = yaw * Vector3.right;
                    Vector3 tremble = Random.insideUnitSphere * (.02f * aimPower * scale);
                    position = feet + aimPull * .42f + Vector3.up * (standHeight - .1f * aimPower * scale) + tremble;
                    rotation = Quaternion.AngleAxis(-40f * aimPower, right) * yaw;
                    stretch = new Vector3(1f - .1f * aimPower, 1f + .22f * aimPower, 1f - .1f * aimPower);
                    break;
                }
                case DollState.Fly:
                {
                    if (whirling)
                    {
                        // Bolt upright and going round like a top, leaning out against the weight of the axe, and
                        // up on his toes as the wind gets under him.
                        whirlTurn += whirlRate * delta;
                        whirlRise = Mathf.Lerp(whirlRise, whirlLift, 1f - Mathf.Exp(-9f * delta));
                        rotation = Quaternion.Euler(0f, whirlTurn, 0f) * Quaternion.Euler(Mathf.Sin(wobble * 11f) * 4f, 0f, 9f);
                        position = feet + Vector3.up * (whirlRise + standHeight);
                        stretch = new Vector3(1.05f, .95f, 1.05f);
                        break;
                    }
                    if (casting)
                    {
                        // On her toes with the wand over her head. Each star kicks her up off the sand and over
                        // backwards, and she comes down still facing where they are falling.
                        castFlick = Mathf.MoveTowards(castFlick, 0f, delta * 3.6f);
                        rotation = yaw * Quaternion.Euler(-castLean, 0f, Mathf.Sin(wobble * 19f) * 5f);
                        position = feet + Vector3.up * (castLift + standHeight);
                        stretch = new Vector3(.97f, 1.06f, .97f);
                        break;
                    }
                    if (riding)
                    {
                        // Feet planted on the turning boomerang, knees bent, leaning into it: he goes round with it.
                        rideSpin += RideWhirl * delta;
                        rotation = RideTilt() * Quaternion.Euler(0f, rideSpin, 0f) * Quaternion.Euler(12f, 0f, 0f);
                        position = feet + Vector3.up * (rideLift + standHeight * .9f);
                        stretch = new Vector3(1.05f, .92f, 1.05f);
                        break;
                    }
                    if (leaping && Kind == WeaponKind.Bounce)
                    {
                        // Curled up and rolling forward through the air, quills outermost. The last, high leap
                        // swells towards the top of its arc, as if it were coming up at the crowd.
                        roll += (touchAndGo ? 820f : 1150f) * delta;
                        float air = Mathf.Sin(Mathf.Clamp01(leapTime / leapSpan) * Mathf.PI);
                        rotation = yaw;
                        position = feet + Vector3.up * (.8f * size);
                        stretch = Vector3.one * (touchAndGo ? 1f : 1f + .3f * air);
                        break;
                    }
                    if (leaping)
                    {
                        // Lean back on the way up, then spread out belly-down for the drop.
                        float t = Mathf.Clamp01(leapTime / leapSpan);
                        float tip = Mathf.Lerp(-30f, 92f, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t * 1.25f)));
                        rotation = yaw * Quaternion.Euler(tip, 0f, Mathf.Sin(wobble * 17f) * 5f);
                        position = feet + Vector3.up * (.55f * size);
                        stretch = new Vector3(1.06f, .96f, 1.06f);
                        break;
                    }
                    Vector3 travel = Kit.Flat(Body.linearVelocity);
                    if (travel.sqrMagnitude > .2f) Facing = travel.normalized;
                    if (Kind == WeaponKind.Hook)
                    {
                        // The pirate does not spin like the others: he rides low behind his great hook, leaning into it.
                        rotation = Quaternion.LookRotation(Kit.Flat(Facing).normalized)
                            * Quaternion.Euler(58f + Mathf.Sin(wobble * 21f) * 4f, 0f, Mathf.Cos(wobble * 17f) * 5f);
                        position = feet + Vector3.up * (.56f * size);
                        stretch = new Vector3(.96f, 1.08f, .96f);
                        break;
                    }
                    roll += 640f * delta;
                    flatAngle += flatSpin * delta;
                    flatSpin = Mathf.MoveTowards(flatSpin, 0f, 900f * delta);
                    rotation = Quaternion.AngleAxis(flatAngle, Vector3.up) * Quaternion.LookRotation(Kit.Flat(Facing).normalized)
                        * Quaternion.Euler(78f, 0f, 0f) * Quaternion.Euler(0f, roll, 0f)
                        * Quaternion.Euler(Mathf.Sin(wobble * 23f) * 15f, 0f, Mathf.Cos(wobble * 19f) * 15f);
                    position = feet + Vector3.up * (.5f * size);
                    stretch = new Vector3(.92f, 1.14f, .92f);
                    break;
                }
                case DollState.Tumble:
                    if (swept)
                    {
                        // In the whirlwind, or thrown out of the top of it: head over heels, with nothing to slow it.
                        tumble = Quaternion.AngleAxis(spinSpeed * delta, spinAxis) * tumble;
                        rotation = tumble;
                        position = feet + Vector3.up * (.55f * size);
                        if (hurled) stretch = new Vector3(.93f, 1.14f, .93f);
                        break;
                    }
                    if (Caught)
                    {
                        // Strung on the pirate's hook like meat on a skewer: on its back across the iron, belly up,
                        // head out to one side and legs to the other, the next one along the other way about, and
                        // all of them jolting as he goes.
                        float swing = wobble * 9f + hangSeat * 1.9f;
                        Vector3 across = Vector3.Cross(Vector3.up, hangAlong) * (hangSeat % 2 == 0 ? 1f : -1f);
                        rotation = Quaternion.LookRotation(Vector3.up, across)
                            * Quaternion.Euler(Mathf.Sin(swing) * 9f, Mathf.Sin(swing * .7f) * 12f, Mathf.Cos(swing * 1.3f) * 7f);
                        position = feet + Vector3.up * hangLift;
                        tumble = rotation;
                        break;
                    }
                    tumble = Quaternion.AngleAxis(spinSpeed * delta, spinAxis) * tumble;
                    spinSpeed *= Mathf.Exp(-(Grounded ? 2.4f : .5f) * delta);
                    rotation = tumble;
                    position = feet + Vector3.up * (.55f * size);
                    break;
                case DollState.Down:
                case DollState.Ko:
                    rotation = Quaternion.Slerp(pivot.rotation, lie, 1f - Mathf.Exp(-14f * delta));
                    position = feet + Vector3.up * Mathf.Lerp(pivot.position.y - feet.y, lieHeight, 1f - Mathf.Exp(-14f * delta));
                    break;
                case DollState.GetUp:
                {
                    float k = Mathf.Clamp01(StateTime / .42f);
                    float eased = k * k * (3f - 2f * k);
                    rotation = Quaternion.Slerp(lie, yaw, eased);
                    position = feet + Vector3.up * (Mathf.Lerp(lieHeight, standHeight, eased) + Mathf.Sin(k * Mathf.PI) * .55f * scale);
                    if (k >= 1f)
                    {
                        squash = .4f;
                        EnterState(DollState.Idle);
                        game.OnDollStood(this);
                    }
                    break;
                }
            }

            if (rattle > 0f)
            {
                // Something on it is about to go off: the whole body judders, faster than the eye can follow.
                position += new Vector3(Mathf.Sin(wobble * 91f), Mathf.Abs(Mathf.Sin(wobble * 67f)) * .6f, Mathf.Cos(wobble * 79f)) * (.085f * scale * rattle);
                rotation = Quaternion.Euler(Mathf.Sin(wobble * 83f) * 7f * rattle, 0f, Mathf.Cos(wobble * 71f) * 9f * rattle) * rotation;
                rattle = Mathf.MoveTowards(rattle, 0f, delta * 5f);
            }
            float squeeze = Mathf.Clamp(squash, -.6f, .7f);
            pivot.SetPositionAndRotation(position, rotation);
            pivot.localScale = new Vector3(stretch.x * (1f + squeeze * .32f), stretch.y * (1f - squeeze * .36f), stretch.z * (1f + squeeze * .32f)) * size;
            if (ball != null)
            {
                bool tucked = leaping && State == DollState.Fly;
                if (tucked != curled) Curl(tucked);
                if (curled)
                {
                    ball.SetPositionAndRotation(position, rotation);
                    ball.localScale = pivot.localScale;
                    ballSpin.localRotation = Quaternion.Euler(roll, 0f, 0f);
                }
            }
            if (whirl != null)
            {
                if (whirl.gameObject.activeSelf != riding) whirl.gameObject.SetActive(riding);
                if (riding)
                {
                    whirl.SetPositionAndRotation(feet + Vector3.up * (rideLift + .05f), Quaternion.Euler(0f, rideSpin + 20f, 0f));
                    whirl.localScale = Vector3.one * (WhirlReach * size * RideGrow);
                }
            }
            MoveLimbs(delta, feet, yaw);
            MoveHead(delta);
            MoveMane(delta);
            MoveFace(delta);
            MoveExtras(feet);
        }

        /// <summary>
        /// The witch's long hair hangs from her nape down and out behind her. Its far end is a weight on a string:
        /// it is left behind when she is thrown one way and comes swinging after her, so that every kick of the
        /// wand sends it flying over her head.
        /// </summary>
        void MoveMane(float delta)
        {
            if (manes[0] == null) return;
            // Her hair grows out of her head, so it goes wherever that turns, and falls behind whichever way she is
            // looking: down her back when she looks where she is going, and behind her face all the same when she
            // looks round at the crowd.
            Vector3 gaze = Kit.Flat(headTurn * Vector3.forward);
            if (gaze.sqrMagnitude > .04f) maneYaw = Quaternion.LookRotation(gaze.normalized, Vector3.up);
            float headSize = head.localScale.x, step = Mathf.Min(delta, .05f);
            for (int i = 0; i < manes.Length; i++)
            {
                int s = i - 1;
                float length = ManeLength[i] * size;
                // The middle one grows from the back of her head and falls behind her; the other two from over
                // her ears, and hang down outside her arms where they can be seen from in front.
                Vector3 root = tips[Head] + headTurn * (new Vector3(s * .39f, s == 0 ? -.05f : -.13f, s == 0 ? -.35f : -.04f) * headSize);
                Vector3 rest = root + maneYaw * new Vector3(s * .27f, -.94f, s == 0 ? -.5f : -.13f).normalized * length;
                if (!maneReady || (maneTip[i] - rest).sqrMagnitude > 16f * length * length)
                    maneTip[i] = maneWas[i] = rest;
                // Each hank is one weight on a string: it keeps going the way it was, sinks, and is drawn back to
                // where it hangs. No two of them are quite as heavy, so they do not swing together.
                Vector3 carried = (maneTip[i] - maneWas[i]) * Mathf.Exp(-(3f + i * .7f) * step);
                maneWas[i] = maneTip[i];
                maneTip[i] += carried + Vector3.down * (9f * step * step);
                maneTip[i] = Vector3.Lerp(maneTip[i], rest, 1f - Mathf.Exp(-(6.6f - i * .9f) * step));
                Vector3 reach = maneTip[i] - root;
                maneTip[i] = root + (reach.sqrMagnitude > 1e-6f ? reach.normalized : Vector3.down) * length;
                if (maneTip[i].y < .06f) maneTip[i].y = .06f;
                Vector3 along = maneTip[i] - root;
                along = along.sqrMagnitude > 1e-6f ? along.normalized : Vector3.down;
                Vector3 hint = maneYaw * new Vector3(s * .3f, 0f, -1f).normalized;
                if (Mathf.Abs(Vector3.Dot(along, hint)) > .96f) hint = Vector3.up;
                manes[i].SetPositionAndRotation(root, Quaternion.LookRotation(along, hint));
            }
            maneReady = true;
        }

        /// <summary>Swap the body for the ball of quills, or back.</summary>
        void Curl(bool on)
        {
            curled = on;
            ball.gameObject.SetActive(on);
            pivot.gameObject.SetActive(!on);
            head.gameObject.SetActive(!on);
            for (int i = 0; i < limbs.Length; i++) limbs[i].gameObject.SetActive(!on);
        }

        void MoveLimbs(float delta, Vector3 feet, Quaternion yaw)
        {
            float frames = Mathf.Clamp(delta * 60f, 0f, 3f);
            bool floppy = State == DollState.Fly || State == DollState.Tumble || State == DollState.Down || State == DollState.Ko;
            bool lying = State == DollState.Down || State == DollState.Ko;
            Quaternion rotation = pivot.rotation;
            Vector3 centre = pivot.position;
            for (int i = 0; i < 5; i++)
            {
                Vector3 anchor = centre + rotation * (anchors[i] * size);
                float length = Lengths[i] * size;
                Vector3 rest;
                float pull, keep, gravity = 0f, noise = 0f;
                if (i == Head)
                {
                    rest = Vector3.up;
                    pull = floppy ? .14f : .32f;
                    keep = floppy ? .86f : .80f;
                    gravity = lying ? 9f : 0f;
                }
                else if (lying)
                {
                    rest = sprawl[i]; pull = .07f; keep = .80f; gravity = 22f;
                }
                else if (State == DollState.Tumble)
                {
                    float side = i % 2 == 0 ? -1f : 1f;
                    rest = i < 2 ? new Vector3(side, .35f, 0f) : new Vector3(side * .55f, -1f, 0f);
                    pull = .035f; keep = .90f; gravity = 6f; noise = .085f;
                    if (swept)
                    {
                        // Arms and legs thrown out every way by the wind.
                        rest = i < 2 ? new Vector3(side, .7f, 0f) : new Vector3(side * .75f, -1f, 0f);
                        pull = .06f; keep = .90f; gravity = 0f; noise = .12f;
                    }
                    else if (Caught)
                    {
                        // Limp across the hook: arms flung out past the head, legs trailing the other way, all drooping.
                        rest = i < 2 ? new Vector3(side * .5f, 1f, -.35f) : new Vector3(side * .4f, -1f, -.4f);
                        pull = .12f; keep = .86f; gravity = 9f; noise = .05f;
                    }
                }
                else if (State == DollState.Fly && casting)
                {
                    // The wand held ready at her shoulder, and thrown up at the sky as far as she can reach with
                    // each star; the other arm flung out; legs trailing under her as she goes over backwards.
                    float side = i % 2 == 0 ? -1f : 1f;
                    if (i == ArmR) rest = Vector3.Lerp(new Vector3(.85f, .3f, .32f), new Vector3(.1f, 1f, .04f), castFlick);
                    else if (i == ArmL) rest = new Vector3(-1f, .45f + Mathf.Sin(wobble * 21f) * .25f, .15f);
                    else rest = new Vector3(side * .28f, -1f, castLift > .05f ? .45f : .04f);
                    pull = .5f; keep = .62f;
                    noise = i == ArmL ? .04f : 0f;
                }
                else if (State == DollState.Fly && whirling)
                {
                    // Both arms flung straight out by the turning, the axe at the end of one of them, and his legs
                    // trailing together under him.
                    float side = i % 2 == 0 ? -1f : 1f;
                    rest = i < 2 ? new Vector3(side, .1f, side * .15f) : new Vector3(side * .2f, -1f, -.1f);
                    pull = .7f; keep = .4f;
                }
                else if (State == DollState.Fly && riding)
                {
                    // Arms flung wide for balance, feet apart on the blade.
                    float side = i % 2 == 0 ? -1f : 1f;
                    rest = i < 2 ? new Vector3(side, .14f, .12f) : new Vector3(side * .5f, -1f, .06f);
                    pull = .45f; keep = .68f;
                    if (RideWobble > 0f && i < 2)
                    {
                        // It has got away from him: both arms go up and thrash about.
                        rest = Vector3.Lerp(rest, new Vector3(side * .55f, 1f, Mathf.Sin(wobble * 15f + i * 2.4f) * .5f), RideWobble);
                        noise = .05f * RideWobble;
                    }
                }
                else if (State == DollState.Fly)
                {
                    float side = i % 2 == 0 ? -1f : 1f;
                    if (i == ArmR)
                    {
                        // Weapon arm thrust out ahead; the pirate, who leans less, holds his level.
                        rest = Kind == WeaponKind.Hook ? new Vector3(.12f, .85f, .53f) : new Vector3(.22f, 1f, .12f);
                        pull = .5f; keep = .6f;
                    }
                    else
                    {
                        rest = i < 2 ? new Vector3(side * .6f, -1f, 0f) : new Vector3(side * .3f, -1f, 0f);
                        pull = .06f; keep = .88f; noise = .08f;
                    }
                }
                else if (State == DollState.Aim)
                {
                    float side = i % 2 == 0 ? -1f : 1f;
                    rest = i < 2 ? new Vector3(side * .25f, .15f, 1f) : new Vector3(side * .1f, -1f, .5f);
                    pull = .3f; keep = .75f; noise = .012f * aimPower;
                }
                else
                {
                    rest = i == ArmL ? new Vector3(-.35f, -1f, .15f) : i == ArmR ? new Vector3(.45f, -.3f, .8f) : Vector3.down;
                    // The porcupine carries nothing: it holds both paws up in front of its belly.
                    if (Kind == WeaponKind.Bounce && i < 2) rest = new Vector3(i == ArmL ? -.3f : .3f, -.62f, .7f);
                    // The gladiator holds his boomerang out at his side, clear of his face.
                    if (Kind == WeaponKind.Boomerang && i == ArmR && !Cheering) rest = new Vector3(.66f, -.42f, .42f);
                    // The pirate carries his gun at the hip, muzzle down; braced to fire, both arms come up level.
                    if (Kind == WeaponKind.Hook && i == ArmL) rest = new Vector3(-.3f, -.55f, .78f);
                    if (Gunning && i < 2) rest = i == ArmL ? new Vector3(-.16f, .14f, 1f) : new Vector3(.3f, .06f, 1f);
                    if (showcase && i == ArmR && Kind != WeaponKind.Bow) rest = new Vector3(.52f, -.74f, .40f);
                    if (showcase && i == ArmL && Kind == WeaponKind.Sword) rest = new Vector3(-.6f, -.8f, .2f);
                    if (Cheering && i < 2) rest = new Vector3(i == ArmL ? -.5f : .5f, 1f, .1f);
                    pull = .34f; keep = .76f;
                }

                Vector3 target = anchor + rotation * rest.normalized * length;
                bool planted = i >= LegL && i <= LegR && (State == DollState.Idle || State == DollState.Aim);
                if (planted)
                {
                    float side = i == LegL ? -1f : 1f;
                    target = feet + yaw * new Vector3(side * .15f * size, .02f, 0f);
                    if (State == DollState.Idle) { pull = 1f; keep = 0f; }
                    else { pull = .5f; keep = .5f; }
                }

                if (!tipsReady)
                    tips[i] = previousTips[i] = target;
                Vector3 velocity = (tips[i] - previousTips[i]) * Mathf.Pow(keep, frames);
                previousTips[i] = tips[i];
                Vector3 tip = tips[i] + velocity + (target - tips[i]) * (1f - Mathf.Pow(1f - Mathf.Clamp01(pull), frames));
                tip += Vector3.down * (gravity * delta * delta);
                if (noise > 0f)
                    tip += Random.insideUnitSphere * (noise * frames);

                Vector3 reach = tip - anchor;
                if (reach.sqrMagnitude < 1e-6f) reach = Vector3.down;
                reach = reach.normalized * length;
                tip = anchor + reach;
                float floor = Floors[i] * size;
                if (tip.y < floor)
                {
                    float drop = floor - anchor.y;
                    if (Mathf.Abs(drop) < length)
                    {
                        Vector3 level = Kit.Flat(reach);
                        if (level.sqrMagnitude < 1e-6f)
                            level = rotation * Vector3.right * (i % 2 == 0 ? -1f : 1f);
                        level = Kit.Flat(level);
                        if (level.sqrMagnitude < 1e-6f) level = Vector3.right;
                        tip = anchor + level.normalized * Mathf.Sqrt(length * length - drop * drop) + Vector3.up * drop;
                    }
                    else
                    {
                        tip.y = floor;
                    }
                }
                tips[i] = tip;

                if (i == Head) continue;
                Vector3 pointing = (tip - anchor).normalized;
                // The shield arm keeps its outer side turned away from the body; legs keep toes forward.
                Vector3 hint = i == ArmL ? rotation * Vector3.left : i == ArmR ? rotation * Vector3.up : rotation * Vector3.forward;
                // On show the knight turns the face of the shield to the viewer instead of holding it edge-on.
                if (showcase && i == ArmL && Kind == WeaponKind.Sword) hint = rotation * Vector3.forward;
                if (Mathf.Abs(Vector3.Dot(pointing, hint)) > .98f)
                    hint = i == ArmL ? rotation * Vector3.up : rotation * Vector3.right;
                limbs[i].SetPositionAndRotation(anchor, Quaternion.LookRotation(pointing, hint));
            }
            tipsReady = true;

            if (riding)
            {
                // The boomerang lies flat under his feet, grown to its flying size, and whirls about its own middle.
                Quaternion round = RideTilt() * Quaternion.Euler(0f, rideSpin, 0f);
                weapon.localScale = Vector3.one * RideGrow;
                weapon.rotation = round;
                weapon.position = feet + Vector3.up * rideLift - round * (BladeMiddle * (size * RideGrow));
                return;
            }
            // Carried along the arm when flying or aiming; held up and out when standing so its shape reads from above.
            float thrust = State == DollState.Fly ? 0f : State == DollState.Aim ? -25f : -72f;
            Quaternion wanted = limbs[ArmR].rotation * Quaternion.Euler(thrust, 0f, Kind == WeaponKind.Bow ? 90f : 0f);
            bool standing = State == DollState.Idle || State == DollState.GetUp;
            if (standing && Kind != WeaponKind.Bow)
            {
                Vector3 raised = rotation * (showcase ? new Vector3(.13f, 1f, .08f) : new Vector3(.42f, 1f, Cheering ? .1f : .22f));
                wanted = Quaternion.LookRotation(raised.normalized, rotation * Vector3.back);
                if (Kind == WeaponKind.Boomerang && !showcase && !Cheering)
                {
                    // The great boomerang stands on end at his side, leaning out, so that its second arm climbs
                    // away over his head and not across his face: held so, the whole shape of it faces whoever is
                    // looking down on him.
                    raised = rotation * new Vector3(.47f, 1f, -.06f);
                    wanted = Quaternion.LookRotation(raised.normalized, rotation * Vector3.back);
                }
                if (Kind == WeaponKind.Hook && !showcase)
                {
                    // The great hook is too long to lean out: it stands upright like a staff, and its curve comes
                    // back in over his own head instead of reaching across his neighbour or the wall.
                    raised = rotation * new Vector3(.08f, 1f, Cheering ? .03f : .08f);
                    wanted = Quaternion.LookRotation(raised.normalized, rotation * Vector3.forward);
                }
            }
            if (Kind == WeaponKind.Hook && (State == DollState.Fly || State == DollState.Aim || Gunning))
            {
                // The great hook is carried level and pointing the way he faces, its curve lying flat and opening
                // across his front, whatever the arm behind it is doing.
                wanted = Quaternion.LookRotation(yaw * Vector3.forward, Vector3.up) * Quaternion.Euler(State == DollState.Aim ? -8f : 0f, 0f, 180f);
            }
            if (casting)
            {
                // The wand is held tipped out and up, and points straight at the sky as a star leaves it.
                Vector3 aloft = Vector3.Lerp(new Vector3(.55f, .75f, .3f), new Vector3(.02f, 1f, .03f), castFlick);
                wanted = Quaternion.LookRotation((rotation * aloft).normalized, rotation * Vector3.back);
            }
            // The hook comes down from his shoulder smartly as he is let fly: it has catching to do at once.
            float brisk = casting ? 34f : whirling ? 70f : Kind == WeaponKind.Hook && State == DollState.Fly ? 30f : 16f;
            weapon.rotation = Quaternion.Slerp(weapon.rotation, wanted, 1f - Mathf.Exp(-brisk * delta));
        }

        void MoveHead(float delta)
        {
            Quaternion rotation = pivot.rotation;
            Vector3 neck = pivot.position + rotation * (anchors[Head] * size);
            Vector3 up = tips[Head] - neck;
            up = up.sqrMagnitude > 1e-6f ? up.normalized : Vector3.up;
            Vector3 gaze;
            // The witch does not so much as look where her stars are going: she grins round at the crowd all through.
            bool upright = State == DollState.Idle || State == DollState.Aim || State == DollState.GetUp || casting;
            if (upright)
            {
                gaze = LookTarget - tips[Head];
                if (gaze.sqrMagnitude < .01f) gaze = rotation * Vector3.forward + Vector3.up * .5f;
            }
            else
            {
                gaze = rotation * Vector3.forward;
            }
            gaze.Normalize();
            if (Mathf.Abs(Vector3.Dot(gaze, up)) > .97f)
                up = rotation * Vector3.back;
            Quaternion wanted = Quaternion.LookRotation(gaze, up);
            headTurn = tipsLooked ? Quaternion.Slerp(headTurn, wanted, 1f - Mathf.Exp(-(upright ? 10f : 22f) * delta)) : wanted;
            tipsLooked = true;
            head.SetPositionAndRotation(tips[Head], headTurn);
        }

        Mood CurrentMood()
        {
            switch (State)
            {
                case DollState.Aim: return Mood.Nervous;
                case DollState.Fly: return casting || whirling || (riding && RideWobble < .3f) ? Mood.Grin : Mood.Scream;
                case DollState.Tumble: return Lost ? Mood.Scream : !Alive ? Mood.Ko : swept ? Mood.Scream : Mood.Hurt;
                case DollState.Down: return rattle > .05f ? Mood.Panic : Mood.Dizzy;
                case DollState.Ko: return Mood.Ko;
                default:
                    return rattle > .05f || (dread > .3f && !Cheering) ? Mood.Panic : Cheering ? Mood.Grin : Startled ? Mood.Gasp
                        : Kind == WeaponKind.Star ? Mood.Grin : Mood.Calm;
            }
        }

        void MoveFace(float delta)
        {
            Mood mood = CurrentMood();
            float eye = 1f, pupil = 1f, browTilt = 16f, browLift = 0f, mouthWide = .09f, mouthTall = .022f;
            bool showTeeth = false, showTongue = false, hangTongue = false, spin = false, darting = false, crossed = false;
            switch (mood)
            {
                case Mood.Nervous: eye = 1.22f; pupil = .62f; browTilt = -22f; browLift = .035f; mouthWide = .17f; mouthTall = .075f; showTeeth = true; darting = true; break;
                case Mood.Scream: eye = 1.48f; pupil = .42f; browTilt = -26f; browLift = .06f; mouthWide = .135f; mouthTall = .2f; showTongue = true; break;
                case Mood.Hurt: eye = 1.32f; pupil = .7f; browTilt = -16f; browLift = .03f; mouthWide = .125f; mouthTall = .15f; showTongue = true; spin = true; break;
                case Mood.Dizzy: eye = 1.16f; pupil = .8f; browTilt = -22f; browLift = .01f; mouthWide = .1f + Mathf.Sin(wobble * 5f) * .02f; mouthTall = .06f; showTongue = true; hangTongue = true; spin = true; break;
                case Mood.Ko: eye = 1.05f; browTilt = -12f; mouthWide = .09f; mouthTall = .055f; showTongue = true; hangTongue = true; crossed = true; break;
                case Mood.Grin: eye = 1.08f; browTilt = -8f; browLift = .03f; mouthWide = .18f; mouthTall = .095f; showTeeth = true; break;
                case Mood.Panic: eye = 1.36f; pupil = .52f; browTilt = -28f; browLift = .07f; mouthWide = .16f; mouthTall = .045f + Mathf.Abs(Mathf.Sin(wobble * 24f)) * .05f; showTeeth = true; darting = true; break;
                case Mood.Gasp: eye = 1.34f; pupil = .55f; browTilt = -20f; browLift = .05f; mouthWide = .075f; mouthTall = .1f; break;
            }

            // At rest the heroes frown; the porcupine only looks on, wide-eyed.
            if (mood == Mood.Calm && Kind == WeaponKind.Bounce) browTilt = -5f;
            // The witch never stops grinning, and her eyes never keep still.
            if (mood == Mood.Grin && Kind == WeaponKind.Star) { darting = true; mouthWide = .2f; mouthTall = .11f; }

            blink -= delta;
            float lid = 1f;
            if (mood == Mood.Calm && blink < .1f)
            {
                lid = .12f;
                if (blink < 0f) blink = Random.Range(1.4f, 4.2f);
            }

            dartTimer -= delta;
            if (dartTimer <= 0f)
            {
                dartTimer = Random.Range(.1f, .26f);
                dart = new Vector3(Random.Range(-.04f, .04f), Random.Range(-.03f, .03f), 0f);
            }
            Vector3 headMotion = (head.position - lastHead) / Mathf.Max(delta, .0001f);
            lastHead = head.position;
            Vector3 local = Quaternion.Inverse(head.rotation) * headMotion;
            pupilDrift = Vector3.Lerp(pupilDrift, Vector3.ClampMagnitude(new Vector3(-local.x, -local.y, 0f) * .006f, .05f), 1f - Mathf.Exp(-14f * delta));

            float follow = 1f - Mathf.Exp(-18f * delta);
            for (int i = 0; i < 2; i++)
            {
                float side = i == 0 ? -1f : 1f;
                Quaternion turn = eyes[i].localRotation;
                float wide = eye * eyeSize * eyeSkew[i], dot = pupil * pupilSize * pupilSkew[i];
                eyes[i].localScale = Vector3.Lerp(eyes[i].localScale, new Vector3(EyeWide * wide, EyeTall * wide * lid, .06f), follow);
                Vector3 offset = pupilDrift;
                if (spin)
                {
                    float rate = mood == Mood.Dizzy ? 5.5f : 17f;
                    float angle = wobble * rate * side + i * 1.7f;
                    offset = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * (.05f * eye);
                }
                else if (darting)
                {
                    offset += dart;
                }
                pupils[i].localPosition = eyeHome[i] + turn * (offset * eyeSize + new Vector3(0f, 0f, .05f));
                pupils[i].localScale = Vector3.Lerp(pupils[i].localScale, new Vector3(PupilWide, PupilTall, .03f) * dot * (lid < 1f ? 0f : 1f), follow);
                pupils[i].gameObject.SetActive(!crossed);
                glints[i].gameObject.SetActive(!crossed && lid >= 1f);
                glints[i].localPosition = pupils[i].localPosition + turn * new Vector3(.022f * dot, .026f * dot, .022f);
                crosses[i].gameObject.SetActive(crossed);
                if (i == patchedEye)
                {
                    // Nothing shows from under the pirate's patch.
                    pupils[i].gameObject.SetActive(false);
                    glints[i].gameObject.SetActive(false);
                    crosses[i].gameObject.SetActive(false);
                }
                brows[i].localPosition = browHome[i] + Vector3.up * browLift;
                brows[i].localRotation = Quaternion.Slerp(brows[i].localRotation, Quaternion.Euler(0f, side * 20f, side * browTilt), follow);
            }

            mouthWide *= mouthSize;
            mouthTall *= mouthSize;
            mouth.localScale = Vector3.Lerp(mouth.localScale, new Vector3(mouthWide, mouthTall, .05f), follow);
            // A mouth on a muzzle opens downwards from under the nose; a person's opens about its middle.
            Vector3 mouthAt = mouthHome + Vector3.down * (mouth.localScale.y * mouthDrop);
            mouth.localPosition = mouthAt;
            if (smile != null)
            {
                smile.gameObject.SetActive(mood == Mood.Calm);
                mouth.gameObject.SetActive(mood != Mood.Calm);
            }
            teeth.gameObject.SetActive(showTeeth);
            if (showTeeth)
            {
                teeth.localPosition = new Vector3(0f, mouthAt.y + mouthTall * .42f, mouthAt.z + .027f);
                teeth.localScale = new Vector3(mouthWide * .82f, mouthTall * .42f, .028f);
            }
            tongue.gameObject.SetActive(showTongue);
            if (fixedMouth)
            {
                // A sculpted head has its own mouth and teeth.
                mouth.gameObject.SetActive(false);
                teeth.gameObject.SetActive(false);
                tongue.gameObject.SetActive(false);
            }
            if (showTongue)
            {
                float flap = hangTongue ? Mathf.Sin(wobble * 3f) * 8f + 22f : Mathf.Sin(wobble * 38f) * 30f;
                tongue.localPosition = new Vector3(hangTongue ? .035f * mouthSize : 0f, mouthAt.y - mouthTall * .55f, mouthAt.z + .035f);
                tongue.localRotation = Quaternion.Euler(hangTongue ? 25f : -20f, 0f, flap);
                tongue.localScale = new Vector3(.062f, hangTongue ? .1f : .085f, .03f) * mouthSize;
            }
        }

        void MoveExtras(Vector3 feet)
        {
            bool dizzy = State == DollState.Down && Alive;
            for (int i = 0; i < stars.Length; i++)
            {
                stars[i].gameObject.SetActive(dizzy);
                if (!dizzy) continue;
                float angle = wobble * 5f + i * Mathf.PI * 2f / 3f;
                stars[i].position = tips[Head] + new Vector3(Mathf.Cos(angle) * .56f, .6f + Mathf.Sin(wobble * 9f + i) * .06f, Mathf.Sin(angle) * .3f) * size;
                stars[i].rotation = Quaternion.Euler(ColosseumGame.CameraPitch, 0f, 0f) * Quaternion.Euler(0f, Mathf.Sin(wobble * 4f + i * 2f) * 35f, wobble * 150f + i * 50f);
                stars[i].localScale = Vector3.one * (size * (.19f + Mathf.Sin(wobble * 7f + i * 2.1f) * .03f));
            }

            bool sweating = dread > .05f;
            for (int i = 0; i < sweat.Length; i++)
            {
                sweat[i].gameObject.SetActive(sweating);
                if (!sweating) continue;
                // Each bead wells up beside an eye, runs down the cheek picking up speed, then drips off and is gone.
                float side = (i & 1) == 0 ? 1f : -1f;
                bool brow = i >= 2;
                float life = Mathf.Repeat(wobble * (brow ? .78f : .92f) + i * .29f, 1f);
                float swell = Mathf.Clamp01(life / .18f);
                float run = Mathf.Clamp01((life - .18f) / .57f);
                float drip = Mathf.Clamp01((life - .75f) / .25f);
                Vector3 from = brow ? new Vector3(side * .33f, .17f, .30f) : new Vector3(side * .295f, .02f, .35f);
                Vector3 to = brow ? new Vector3(side * .37f, -.12f, .29f) : new Vector3(side * .32f, -.24f, .33f);
                Vector3 spot = Vector3.Lerp(from, to, run * run);
                spot += new Vector3(side * .03f * drip, -.24f * drip * drip, .02f * drip);
                float size = (swell * (1.18f - .18f * swell) + Mathf.Sin(swell * Mathf.PI) * .2f) * (1f - drip * drip) * dread * (brow ? .8f : 1f);
                float stretch = 1f + run * .22f + drip * .35f;
                sweat[i].localPosition = spot;
                sweat[i].localRotation = Quaternion.Euler(0f, 0f, side * (brow ? 9f : 5f));
                sweat[i].localScale = new Vector3(.098f / Mathf.Sqrt(stretch), .125f * stretch, .062f) * Mathf.Max(.001f, size);
            }

            float height = Mathf.Max(0f, feet.y);
            float spread = size / (1f + height * .45f);
            shade.position = new Vector3(feet.x, .024f, feet.z);
            shade.localScale = new Vector3(spread, 1f, spread);

            bool showRing = Alive && (State == DollState.Idle || State == DollState.Aim) && RingStyle >= 0;
            ring.gameObject.SetActive(showRing);
            if (showRing)
            {
                float pulse = RingStyle == 1 ? 1f + Mathf.Sin(wobble * 6f) * .05f : 1f;
                ring.position = new Vector3(feet.x, .03f, feet.z);
                ring.localScale = new Vector3(pulse * size, 1f, pulse * size);
                ringRenderer.sharedMaterial = game.RingMaterial(Team, RingStyle);
            }
        }

        void OnDestroy()
        {
            if (ring != null)
                Destroy(ring.gameObject);
            if (shade != null)
                Destroy(shade.gameObject);
        }
    }
}
