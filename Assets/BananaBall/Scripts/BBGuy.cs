using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace BananaBall
{
    public enum Act { Idle, Ready, Run, Jog, Stance, Bunt, PitchSet, PitchWind, PitchThrow, Throw, Scoop, Jump, Dive, Down, Slide, Celebrate, Dance, Sad, Dizzy, Flop, Tag, Wave, Squat, GetUp, Tantrum, ChestBeat }

    /// <summary>
    /// One ballplayer: realistic proportions (from Body), a 20-bone skinned mesh built in code, cap or batting helmet,
    /// glove, back number. Procedural poses on spring joints plus two-bone IK for bat and glove. Left-handed actions are mirrored.
    /// The logic owns pos/yaw; animation never moves the logic point.
    /// </summary>
    public class Ballplayer : MonoBehaviour
    {
        public const int HIPS = 0, SPINE = 1, CHEST = 2, NECK = 3, HEAD = 4, SHL = 5, ELL = 6, HAL = 7, SHR = 8, ELR = 9, HAR = 10,
                         THL = 11, KNL = 12, FTL = 13, THR = 14, KNR = 15, FTR = 16, JN = 17;
        public const float PitchRelease = 0.36f, ThrowRelease = 0.17f;
        static readonly int[] RigOf = { 0, 1, 2, 3, 4, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19 };
        static readonly int[] Mirror = { HIPS, SPINE, CHEST, NECK, HEAD, SHR, ELR, HAR, SHL, ELL, HAL, THR, KNR, FTR, THL, KNL, FTL };
        static readonly float[] Kst = { 320, 280, 260, 150, 120, 190, 190, 230, 190, 190, 230, 260, 260, 130, 260, 260, 130 };
        static readonly float[] Dmp = { 32, 28, 26, 13, 11, 15, 15, 20, 15, 15, 20, 24, 24, 9, 24, 24, 9 };
        static readonly Dictionary<string, Mesh> MeshCache = new Dictionary<string, Mesh>();

        public PlayerData data;
        public TeamData team;
        public readonly Transform[] J = new Transform[JN];
        Transform jaw, eyeL, eyeR, capT, helmetT, gloveT, numT;
        Transform capFarT, gloveFarT;      // catcher: light mask and mitt for the far-away level of detail
        readonly Vector3[] cur = new Vector3[JN], vel = new Vector3[JN], tgt = new Vector3[JN], mtmp = new Vector3[JN];
        Vector3 hipsBind;
        float shoulderY, chestY, armLen, s;
        Vector3[] rest;

        // --- driven by game logic ---
        public Vector3 pos;
        public float yaw;
        public Act act { get; private set; }
        public float actT;
        public Vector3 lookAt;
        public bool looking;
        public float mouth = 0.1f;
        public float gloveW;
        public Vector3 gloveAt;
        public Transform bat;
        public float swing = -1f;          // 0..1 through the swing, -1 = not swinging
        /// <summary>Batter: the stride while the pitch is on its way (set by Match): 0 set in the stance, 1 front foot down and hands loaded.</summary>
        public float load;
        public Vector3 swingAim;           // contact point in world space
        public bool megaBat;
        public float speedHint = -1f;
        public bool hasBall;               // show the ball in the throwing hand
        public float bigKick;              // pitcher leg-kick height 0..1 (wacky)
        /// <summary>Catcher: mask, chest protector, shin guards and a big round mitt. Squats behind the plate (Act.Squat).</summary>
        public bool isCatcher { get; private set; }
        /// <summary>Glove pocket in the glove hand's space (where a caught ball sits).</summary>
        public static readonly Vector3 GloveTip = new Vector3(0f, -0.11f, 0.05f) * PlayerMesh.GloveK;
        float squatRise, squatLow;
        /// <summary>A gorilla in uniform: hunched idle, chest beats, knuckle gallop.</summary>
        public bool isApe { get; private set; }
        bool floatie;
        /// <summary>Wacky pitching windup for the next pitch (0 normal, 1 backflip, 2 tornado, 3 windmill, 4 cartwheel,
        /// 5 bunny hops, 6 chest beat) and a gag after the release (1 face-plant ragdoll, 2 spin-out).</summary>
        public int windStyle, throwGag;
        int runStyle;
        float beatT = 3f, beatW, beatTarget, beatClock, chestFrontZ = 0.12f;

        // ragdoll: limp physics bodies on the bones, then a springy get-up blended from the limp pose
        Transform[] bonesAll;
        Transform[] helpers;
        Vector3[] helperRest;
        float[] helperPush;
        RagdollRig rag;
        float ragT, ragMin, ragSettle;
        bool ragGetUp = true, getUpFaceUp;
        readonly Vector3[] blendPos = new Vector3[20];
        readonly Quaternion[] blendRot = new Quaternion[20], animRot = new Quaternion[20];
        float blendT = -1f;
        const float BlendDur = 0.32f;
        float lockT;
        bool hasQueued;
        Act queued;

        // acrobatics: a whole-body rotation about the hips (flips, spins, cartwheels) on top of the spring pose
        public const int AcroBackflip = 1, AcroTornado = 2, AcroCartwheel = 3, AcroCorkscrew = 4, AcroPlank = 5, AcroFrontflip = 6, AcroSpinOut = 7;
        int acroKind;
        float acroT, acroDur, acroSign = 1f;
        bool acroThenRagdoll;

        Vector3 rootOff, rootOffV, rootRot, rootRotV;
        float hipY, hipYV;
        Vector3 lastPos, velS, accL;
        float runPhase, tt, mouthCur, gloveCur, seed;
        float ikWL, ikWR;
        bool mirror;
        Transform shadowT, heldBall;

        public float Height => data.body.height;
        public Vector3 GloveWorld => gloveT != null ? gloveT.TransformPoint(GloveTip) : J[GloveHand].position;
        public Vector3 HandWorld => J[ThrowHand].TransformPoint(new Vector3(0f, -0.07f * s * PlayerMesh.HandK, 0.01f));
        public Vector3 HeadTop => J[HEAD].TransformPoint(new Vector3(0f, 0.24f * s * PlayerMesh.HeadOf(data.body), 0f));
        public Vector3 Chest => J[CHEST].position;
        int GloveHand => data.throws == Hand.Right ? HAL : HAR;
        int ThrowHand => data.throws == Hand.Right ? HAR : HAL;

        static string MeshKey(PlayerData d, TeamData t) => d.body.Key() + "|" + t.abbr + (t.pinstripes ? "P" : "") + (d.pos == Pos.C ? "|C" : "");

        /// <summary>Build every body mesh these players still need, all at once on worker threads (one soft body costs about
        /// 0.2 s on a single core), so a new line-up appears without a long hitch.</summary>
        public static void Prewarm(IList<PlayerData> players, TeamData t)
        {
            var todo = new List<PlayerData>();
            var keys = new List<string>();
            foreach (var d in players)
            {
                if (d == null) continue;
                string key = MeshKey(d, t);
                if (keys.Contains(key) || (MeshCache.TryGetValue(key, out Mesh m) && m != null)) continue;
                todo.Add(d);
                keys.Add(key);
            }
            if (todo.Count == 0) return;
            int n = todo.Count;
            var kits = new PlayerMesh.BodyKit[n * 2];
            System.Threading.Tasks.Parallel.For(0, n * 2, i =>
            {
                var d = todo[i % n];
                kits[i] = PlayerMesh.BuildBodyKit(d.body, t, PlayerMesh.Rig(d.body), i / n, d.pos == Pos.C);
            });
            for (int i = 0; i < n; i++)
            {
                MeshCache[keys[i]] = kits[i] != null ? PlayerMesh.FinishBody(kits[i]) : PlayerMesh.BuildBodyLoft(todo[i].body, t, PlayerMesh.Rig(todo[i].body));
                if (kits[n + i] != null) MeshCache[keys[i] + FarKey] = PlayerMesh.FinishBody(kits[n + i]);
            }
        }

        const string FarKey = "|far";
        /// <summary>Below this share of the screen height a player switches to the light far-away body.</summary>
        public static float LodSwitch = 0.16f;

        public static Ballplayer Create(PlayerData d, TeamData t, Transform parent, Mats m, Mesh shadowMesh, Mesh ballMesh)
        {
            var go = new GameObject(d.name);
            go.transform.SetParent(parent, false);
            var g = go.AddComponent<Ballplayer>();
            g.Build(d, t, m, shadowMesh, ballMesh);
            return g;
        }

        void Build(PlayerData d, TeamData t, Mats m, Mesh shadowMesh, Mesh ballMesh)
        {
            data = d;
            team = t;
            isCatcher = d.pos == Pos.C;
            seed = (d.number * 7.3f + d.name.Length * 1.9f) % 13f;
            s = d.body.height / 1.85f;
            rest = PlayerMesh.Rig(d.body);
            var bones = new Transform[20];
            string[] names = { "Hips", "Spine", "Chest", "Neck", "Head", "Jaw", "EyeL", "EyeR", "ShoulderL", "ElbowL", "HandL", "ShoulderR", "ElbowR", "HandR", "ThighL", "KneeL", "FootL", "ThighR", "KneeR", "FootR" };
            for (int i = 0; i < 20; i++)
            {
                var tr = new GameObject(names[i]).transform;
                int par = PlayerMesh.Parent[i];
                tr.SetParent(par < 0 ? transform : bones[par], false);
                tr.localPosition = par < 0 ? rest[i] : rest[i] - rest[par];
                bones[i] = tr;
            }
            for (int j = 0; j < JN; j++) J[j] = bones[RigOf[j]];
            bonesAll = bones;
            // helper bones on the joints (PlayerMesh.Helpers): skin only, they follow the joint below them halfway
            helpers = new Transform[PlayerMesh.Helpers];
            helperRest = new Vector3[PlayerMesh.Helpers];
            helperPush = new float[PlayerMesh.Helpers];
            var skinBones = new Transform[PlayerMesh.BoneCount];
            for (int i = 0; i < 20; i++) skinBones[i] = bones[i];
            for (int h = 0; h < PlayerMesh.Helpers; h++)
            {
                int par = PlayerMesh.HelperParent[h], ch = PlayerMesh.HelperChild[h];
                var tr = new GameObject(names[ch] + "Mid").transform;
                tr.SetParent(bones[par], false);
                tr.localPosition = rest[ch] - rest[par];
                helpers[h] = tr;
                helperRest[h] = tr.localPosition;
                skinBones[20 + h] = tr;
                bool elbow = ch == 9 || ch == 12, knee = ch == 15 || ch == 18;
                helperPush[h] = elbow ? 0.009f * s * (d.body.ape ? 1.3f : 1f) : knee ? 0.013f * s : 0f;
            }
            isApe = d.body.ape;
            floatie = d.body.Has(Extra.Floatie);
            // everybody runs a little differently (some of them very differently)
            int rs = Mathf.Abs(d.number * 7 + d.name.Length * 3) % 10;
            runStyle = isApe ? 10 : rs < 4 ? 0 : rs < 6 ? 1 : rs < 8 ? 2 : rs < 9 ? 3 : 4;
            jaw = bones[5];
            eyeL = bones[6];
            eyeR = bones[7];
            hipsBind = J[HIPS].localPosition;
            shoulderY = rest[11].y;
            chestY = rest[2].y;
            armLen = (rest[8] - rest[9]).magnitude + (rest[9] - rest[10]).magnitude;

            Material mat = m.Team(t);
            string key = MeshKey(d, t);
            if (!MeshCache.TryGetValue(key, out Mesh mesh) || mesh == null)
            {
                mesh = PlayerMesh.BuildBody(d.body, t, rest, isCatcher);
                MeshCache[key] = mesh;
            }
            var body = new GameObject("Body");
            body.transform.SetParent(transform, false);
            var smr = body.AddComponent<SkinnedMeshRenderer>();
            smr.sharedMesh = mesh;
            smr.sharedMaterial = mat;
            smr.bones = skinBones;
            smr.rootBone = bones[0];
            smr.localBounds = new Bounds(new Vector3(0f, 0.1f, 0f), new Vector3(3f, 3.2f, 3f));
            smr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            smr.receiveShadows = true;
            smr.updateWhenOffscreen = false;

            // light far-away body (fielders across the park, the batter seen from the mound)
            if (!MeshCache.TryGetValue(key + FarKey, out Mesh far) || far == null)
            {
                var kit1 = PlayerMesh.BuildBodyKit(d.body, t, rest, 1, isCatcher);
                far = kit1 != null ? PlayerMesh.FinishBody(kit1) : null;
                if (far != null) MeshCache[key + FarKey] = far;
            }
            if (far != null)
            {
                var body1 = new GameObject("BodyFar");
                body1.transform.SetParent(transform, false);
                var smr1 = body1.AddComponent<SkinnedMeshRenderer>();
                smr1.sharedMesh = far;
                smr1.sharedMaterial = mat;
                smr1.bones = skinBones;
                smr1.rootBone = bones[0];
                smr1.localBounds = smr.localBounds;
                smr1.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                smr1.receiveShadows = true;
                smr1.updateWhenOffscreen = false;
                var lg = gameObject.AddComponent<LODGroup>();
                lg.SetLODs(new[] { new LOD(LodSwitch, new Renderer[] { smr }), new LOD(0f, new Renderer[] { smr1 }) });
                lg.localReferencePoint = new Vector3(0f, 0.7f * s, 0f);
                lg.size = 1.5f * s;
                lodNear = smr;
                lodFar = smr1;
            }

            // a catcher wears his helmet and mask where the others wear a cap (or a costume hat), and a big round mitt;
            // a firefighter bats in his fire helmet
            Vector3 badge = Vector3.zero;
            Mesh headwear = isCatcher ? PlayerMesh.CatcherHelmet(d.body, t) : PlayerMesh.Headwear(d.body, t, out badge);
            bool fireHelmet = !isCatcher && d.body.hat == Hat.Fire;
            capT = Part("Cap", headwear, J[HEAD], m.body);
            helmetT = Part("Helmet", fireHelmet ? headwear : PlayerMesh.Helmet(d.body, t, d.bats == Hand.Left), J[HEAD], m.body);
            gloveT = Part("Glove", isCatcher ? PlayerMesh.Mitt(d.throws == Hand.Left) : PlayerMesh.Glove(d.throws == Hand.Left), J[GloveHand], m.body);
            float hk = s * PlayerMesh.HeadOf(d.body) * CapScale(d.body);
            if (!isCatcher && d.body.hat == Hat.Cap) AddLetter(capT, t, new Vector3(0f, 0.152f, 0.092f) * hk, -18f, 0.042f * hk);
            if (fireHelmet && badge != Vector3.zero)
            {
                // his number on the helmet's front shield
                foreach (var tr in new[] { capT, helmetT })
                {
                    Color nc = (Mathf.Abs(d.body.style) % 2 == 0) ? Color.white : new Color(0.08f, 0.08f, 0.1f);
                    var nt = WorldText.Make(tr, d.number.ToString(), m.font, nc, 0.038f * hk, true);
                    nt.localPosition = badge + new Vector3(0f, 0f, 0.004f * hk);
                    nt.localRotation = Quaternion.Euler(-24f, 180f, 0f);
                }
            }
            // the catcher's mask and mitt are detailed soft parts: they get light far versions that switch with the body
            var lgroup = GetComponent<LODGroup>();
            if (isCatcher && lgroup != null && lodNear != null && lodFar != null)
            {
                capFarT = Part("CapFar", PlayerMesh.CatcherHelmet(d.body, t, true), J[HEAD], m.body);
                gloveFarT = Part("GloveFar", PlayerMesh.Mitt(d.throws == Hand.Left, true), J[GloveHand], m.body);
                lgroup.SetLODs(new[]
                {
                    new LOD(LodSwitch, new Renderer[] { lodNear, capT.GetComponent<Renderer>(), gloveT.GetComponent<Renderer>() }),
                    new LOD(0f, new Renderer[] { lodFar, capFarT.GetComponent<Renderer>(), gloveFarT.GetComponent<Renderer>() }),
                });
            }
            if (!fireHelmet) AddLetter(helmetT, t, new Vector3(0f, 0.15f, 0.104f) * hk, -20f, 0.04f * hk);

            Color numC = t.jersey.grayscale < 0.45f ? (t.accent.grayscale > 0.5f ? t.accent : Color.white) : (t.accent.grayscale < 0.5f ? t.accent : t.main);
            if (d.body.top != Top.Jersey)
            {
                // a costume top: a number that stands off its back
                var cwn = PlayerSdf.Wardrobe.Of(d.body, t);
                Color backC = d.body.top == Top.Aloha ? cwn.aloha : d.body.top == Top.Tank ? cwn.tank : d.body.top == Top.Tux ? cwn.tux : cwn.coat;
                numC = PlayerSdf.Wardrobe.Lum(backC) > 0.5f ? (PlayerSdf.Wardrobe.Lum(t.accent) < 0.35f ? t.accent : new Color(0.1f, 0.1f, 0.12f)) : Color.white;
                if (d.body.top == Top.FireCoat) numC = PlayerSdf.Wardrobe.Lum(backC) > 0.5f ? new Color(0.12f, 0.12f, 0.14f) : cwn.lime;
            }
            numT = WorldText.Make(J[CHEST], d.number.ToString(), m.font, numC, 0.17f * s, true);
            float backZ = -PlayerMesh.BackDepth(d.body) - 0.006f * s;
            float frontZ = PlayerMesh.BackDepth(d.body) + (0.026f + 0.034f * d.body.belly) * s;
            if (PlayerMesh.TextZ.TryGetValue(mesh, out Vector2 tz)) { backZ = tz.x; frontZ = tz.y; }
            chestFrontZ = frontZ;
            numT.localPosition = new Vector3(0f, 0.09f * s * PlayerMesh.TorsoK, backZ);
            numT.localRotation = Quaternion.identity;
            // club nickname across the chest, like a real jersey (a catcher's chest protector covers it, so do most costumes)
            string nick = t.name.Substring(t.name.LastIndexOf(' ') + 1);
            var body0 = d.body;
            if (!isCatcher && (body0.top == Top.Jersey || body0.top == Top.Tank) && body0.bottom != Bottom.Overalls && !body0.Has(Extra.Lei) && !body0.Has(Extra.Bandana))
            {
                Color cc = numC;
                if (body0.top == Top.Tank)
                {
                    var cw = PlayerSdf.Wardrobe.Of(body0, t);
                    cc = cw.tankTrim;
                }
                var chestT = WorldText.Make(J[CHEST], nick, m.font, cc, 0.048f * s, true);
                chestT.localPosition = new Vector3(0f, 0.075f * s * PlayerMesh.TorsoK, frontZ);
                chestT.localRotation = Quaternion.Euler(-6f, 180f, 0f);
            }
            // overalls: the club name across the seat (Banana Ball's B-A-N-A-N-A-S)
            if (body0.bottom == Bottom.Overalls && PlayerMesh.SeatOf.TryGetValue(mesh, out Vector3 seat))
            {
                var seatT = WorldText.Make(J[HIPS], nick.ToUpperInvariant(), m.font, new Color(0.98f, 0.82f, 0.3f), 0.04f * s, true);
                seatT.localPosition = seat - rest[0] + new Vector3(0f, 0f, -0.003f * s);
                seatT.localRotation = Quaternion.Euler(10f, 0f, 0f);
            }

            var hb = new GameObject("HeldBall");
            hb.transform.SetParent(J[ThrowHand], false);
            hb.transform.localPosition = new Vector3(0f, -0.075f * s * PlayerMesh.HandK, 0.02f * s * PlayerMesh.HandK);
            hb.AddComponent<MeshFilter>().sharedMesh = ballMesh;
            hb.AddComponent<MeshRenderer>().sharedMaterial = m.ball;
            heldBall = hb.transform;
            heldBall.gameObject.SetActive(false);

            var sh = new GameObject("ContactShadow");
            sh.transform.SetParent(transform.parent, false);
            sh.AddComponent<MeshFilter>().sharedMesh = shadowMesh;
            var mr = sh.AddComponent<MeshRenderer>();
            mr.sharedMaterial = m.shadow;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            shadowT = sh.transform;
            Gear(false, true);
            lastPos = pos;
        }

        static float CapScale(Body b) => b.hair == Hair.Afro ? 1.22f : 1f;
        Renderer lodNear, lodFar;

        Transform Part(string n, Mesh mesh, Transform parent, Material mat)
        {
            var go = new GameObject(n);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            return go.transform;
        }

        void AddLetter(Transform parent, TeamData t, Vector3 local, float tilt, float size)
        {
            var tr = WorldText.Make(parent, t.abbr.Substring(0, 1), null, t.accent, size, false);
            tr.localPosition = local;
            tr.localRotation = Quaternion.Euler(tilt, 180f, 0f);
        }

        void OnEnable() { if (shadowT) shadowT.gameObject.SetActive(true); }
        void OnDisable() { if (shadowT) shadowT.gameObject.SetActive(false); }
        void OnDestroy() { if (shadowT) Destroy(shadowT.gameObject); }

        public void Show(bool on)
        {
            if (!on) ClearSlapstick();
            if (gameObject.activeSelf != on) gameObject.SetActive(on);
        }

        /// <summary>helmet: batting helmet instead of cap. glove: wear the fielding glove.</summary>
        public void Gear(bool helmet, bool glove)
        {
            capT.gameObject.SetActive(!helmet);
            helmetT.gameObject.SetActive(helmet);
            gloveT.gameObject.SetActive(glove);
            if (capFarT != null) capFarT.gameObject.SetActive(!helmet);
            if (gloveFarT != null) gloveFarT.gameObject.SetActive(glove);
        }

        public void Set(Act a)
        {
            if (Floored) { queued = a; hasQueued = true; return; }
            if (act == a) return;
            act = a;
            actT = 0f;
        }

        public void Play(Act a)
        {
            if (Floored) { queued = a; hasQueued = true; return; }
            act = a;
            actT = 0f;
        }

        public void Place(Vector3 p, float yawDeg)
        {
            ClearSlapstick();
            rootRot = rootRotV = rootOff = rootOffV = Vector3.zero;
            pos = p;
            lastPos = p;
            yaw = yawDeg;
            velS = Vector3.zero;
            accL = Vector3.zero;
            transform.SetPositionAndRotation(p, Quaternion.Euler(0f, yawDeg, 0f));
        }

        /// <summary>A slapstick jolt through the spring joints.</summary>
        public void Kick(float amount)
        {
            vel[NECK] += new Vector3(-110f, 0f, 70f) * amount;
            vel[HEAD] += new Vector3(-80f, 0f, -60f) * amount;
            vel[SHR] += new Vector3(140f, 0f, 90f) * amount;
            vel[SHL] += new Vector3(120f, 0f, -90f) * amount;
            vel[FTL] += new Vector3(170f, 0f, 0f) * amount;
            vel[FTR] += new Vector3(-160f, 0f, 0f) * amount;
        }

        // ------------------------------------------------------------------ animation

        void S(int j, float x, float y, float z) { tgt[j] = new Vector3(x, y, z); }
        static float Smooth01(float x) { x = Mathf.Clamp01(x); return x * x * (3f - 2f * x); }
        static float Seg(float p, float a, float b) => Mathf.Clamp01((p - a) / (b - a));

        void K4(int j, Vector3 k0, Vector3 k1, Vector3 k2, Vector3 k3, float a, float b, float c)
        {
            tgt[j] = Vector3.Lerp(Vector3.Lerp(Vector3.Lerp(k0, k1, a), k2, b), k3, c);
        }

        public void Tick(float dt)
        {
            if (dt <= 0f || !gameObject.activeInHierarchy) return;
            tt += dt;
            actT += dt;
            if (Ragdolled)
            {
                ragT += dt;
                ragSettle = rag.HipsSpeed < 0.6f ? ragSettle + dt : 0f;
                if (ragGetUp && ragT >= ragMin && (ragSettle > 0.2f || ragT > ragMin + 1.8f)) GetUp();
                else
                {
                    // limp: physics owns the bones; only the face and the shadow are ours
                    mouthCur = MathX.Damp(mouthCur, 1f, 10f, dt);
                    jaw.localScale = new Vector3(1f, 0.35f + 1.4f * mouthCur, 1f);
                    lastPos = pos;
                    if (heldBall != null && heldBall.gameObject.activeSelf != hasBall) heldBall.gameObject.SetActive(hasBall);
                    UpdateHelpers();
                    UpdateShadow();
                    return;
                }
            }
            if (lockT > 0f)
            {
                lockT -= dt;
                if (lockT <= 0f) { lockT = 0f; if (hasQueued) { act = queued; actT = 0f; hasQueued = false; } else if (act == Act.GetUp) { act = Act.Idle; actT = 0f; } }
            }
            Vector3 v = (pos - lastPos) / dt;
            lastPos = pos;
            v.y = 0f;
            if (v.sqrMagnitude > 1600f) v = velS;
            Vector3 acc = (v - velS) / dt;
            velS = Vector3.Lerp(velS, v, 1f - Mathf.Exp(-16f * dt));
            Quaternion inv = Quaternion.Euler(0f, -yaw, 0f);
            accL = Vector3.Lerp(accL, inv * Vector3.ClampMagnitude(acc, 60f), 1f - Mathf.Exp(-9f * dt));
            float speed = speedHint >= 0f ? speedHint : velS.magnitude;

            mirror = act == Act.Stance || act == Act.Bunt ? data.bats == Hand.Left
                   : act == Act.PitchSet || act == Act.PitchWind || act == Act.PitchThrow || act == Act.Throw ? data.throws == Hand.Left : false;

            Vector3 tOff = Vector3.zero, tRot = Vector3.zero;
            float tHip = 0f;
            Pose(speed, dt, ref tOff, ref tRot, ref tHip);
            if (mirror)
            {
                var tmp = mtmp;
                for (int i = 0; i < JN; i++) { Vector3 e = tgt[Mirror[i]]; tmp[i] = new Vector3(e.x, -e.y, -e.z); }
                for (int i = 0; i < JN; i++) tgt[i] = tmp[i];
                tOff.x = -tOff.x;
                tRot.y = -tRot.y;
                tRot.z = -tRot.z;
            }

            float lag = Mathf.Clamp(accL.z * 0.45f, -22f, 22f);
            tgt[SHL].x += lag; tgt[SHR].x += lag;
            tgt[NECK].x += lag * 0.25f;
            float side = Mathf.Clamp(accL.x * 0.35f, -14f, 14f);
            tgt[NECK].z -= side * 0.5f;

            int steps = dt > 0.02f ? 2 : 1;
            float h = dt / steps;
            if (rigidPose)
            {
                // fast moves (the swing): the joints follow their keys almost exactly; springs would leave the body behind the bat
                float a = 1f - Mathf.Exp(-60f * dt);
                for (int i = 0; i < JN; i++)
                {
                    Vector3 d = new Vector3(Mathf.DeltaAngle(cur[i].x, tgt[i].x), Mathf.DeltaAngle(cur[i].y, tgt[i].y), Mathf.DeltaAngle(cur[i].z, tgt[i].z));
                    cur[i] += d * a;
                    vel[i] = Vector3.zero;
                }
            }
            for (int st = 0; st < steps; st++)
            {
                if (!rigidPose)
                    for (int i = 0; i < JN; i++)
                    {
                        Vector3 d = new Vector3(Mathf.DeltaAngle(cur[i].x, tgt[i].x), Mathf.DeltaAngle(cur[i].y, tgt[i].y), Mathf.DeltaAngle(cur[i].z, tgt[i].z));
                        vel[i] += (d * Kst[i] - vel[i] * Dmp[i]) * h;
                        cur[i] += vel[i] * h;
                    }
                rootOffV += ((tOff - rootOff) * 260f - rootOffV * 30f) * h;
                rootOff += rootOffV * h;
                rootRotV += ((tRot - rootRot) * 200f - rootRotV * 26f) * h;
                rootRot += rootRotV * h;
                hipYV += ((tHip - hipY) * 320f - hipYV * 36f) * h;
                hipY += hipYV * h;
            }
            for (int i = 0; i < JN; i++) J[i].localRotation = Quaternion.Euler(cur[i]);
            J[HIPS].localPosition = hipsBind + new Vector3(0f, hipY * s, 0f);

            Quaternion yr = Quaternion.Euler(0f, yaw, 0f);
            Quaternion baseRot = yr * Quaternion.Euler(rootRot);
            Vector3 basePos = pos + yr * (rootOff * s);
            if (acroKind != 0)
            {
                acroT += dt;
                EvalAcro(out Quaternion ar, out float lift, out float piv);
                Vector3 pv = new Vector3(0f, piv, 0f);
                transform.SetPositionAndRotation(basePos + Vector3.up * lift + baseRot * pv - baseRot * (ar * pv), baseRot * ar);
            }
            else transform.SetPositionAndRotation(basePos, baseRot);

            ApplyIK(dt);
            Face(dt);
            BlendFromLimp(dt);
            UpdateHelpers();
            if (heldBall != null && heldBall.gameObject.activeSelf != hasBall) heldBall.gameObject.SetActive(hasBall);
            UpdateShadow();

            // slapstick that starts at a moment of an action
            if (acroKind != 0 && acroT >= acroDur) EndAcro();
            if (act == Act.PitchThrow && throwGag != 0 && actT > (throwGag == 1 ? 0.5f : 0.4f))
            {
                int g = throwGag;
                throwGag = 0;
                if (g == 1) Ragdoll(transform.forward * 3.4f + Vector3.up * 1.2f, transform.right * 7f, 0.9f);
                else Acro(AcroSpinOut, 0f, data.throws == Hand.Left ? -1f : 1f);
            }
        }

        /// <summary>Helper bones turn halfway with the joint below them; elbows and knees slide out to the convex side as they fold.</summary>
        void UpdateHelpers()
        {
            if (helpers == null) return;
            for (int h = 0; h < helpers.Length; h++)
            {
                Quaternion lr = bonesAll[PlayerMesh.HelperChild[h]].localRotation;
                helpers[h].localRotation = Quaternion.Slerp(Quaternion.identity, lr, 0.5f);
                Vector3 p = helperRest[h];
                if (helperPush[h] > 0f)
                {
                    // straight: up + down = 0; folded: points into the bend, so the bulge goes the other way
                    Vector3 into = Vector3.up + lr * Vector3.down;
                    p -= into * helperPush[h];
                }
                helpers[h].localPosition = p;
            }
        }

        void UpdateShadow()
        {
            if (!shadowT) return;
            Vector3 hp = J[HIPS].position;
            float lift = Mathf.Clamp01((hp.y - rest[0].y) / 1.5f);
            shadowT.position = new Vector3(hp.x, Geo.GroundY(hp) + 0.02f, hp.z);
            bool limp = Ragdolled;
            bool flat = limp || act == Act.Dive || act == Act.Down || act == Act.Slide || act == Act.Flop || act == Act.GetUp;
            float sz = (flat ? 1.5f : 0.9f) * s * (1f - lift * 0.5f);
            shadowT.localScale = new Vector3(sz, 1f, sz * (flat ? 1.7f : 1f));
            float sy = yaw;
            if (limp) { Vector3 d = J[HEAD].position - hp; d.y = 0f; if (d.sqrMagnitude > 1e-4f) sy = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg; }
            shadowT.rotation = Quaternion.Euler(0f, sy, 0f);
        }

        // ------------------------------------------------------------------ ragdoll

        public bool Ragdolled => rag != null && rag.On;
        /// <summary>Limp, or still getting back up: game logic should let him be (acts asked for now are played after).</summary>
        public bool Floored => Ragdolled || lockT > 0f;
        /// <summary>Ground point under the limp body's hips.</summary>
        public Vector3 RagdollGround { get { Vector3 h = J[HIPS].position; return new Vector3(h.x, Geo.GroundY(h), h.z); } }

        /// <summary>Go limp. vel: launch velocity (m/s), spin: tumble (rad/s, world axis), minTime: how long he stays down before
        /// getting up (infinity: stays limp); hitSeg/hitImpulse: an extra knock on one body part (RagdollRig.SegHead...).</summary>
        public void Ragdoll(Vector3 vel, Vector3 spin, float minTime = 1.1f, int hitSeg = -1, Vector3 hitImpulse = default)
        {
            if (!gameObject.activeInHierarchy || bonesAll == null) return;
            if (rag == null) rag = new RagdollRig(bonesAll, rest, data.body);
            if (rag.On) { rag.Push(vel, spin); ragMin = Mathf.Max(ragMin, ragT + minTime * 0.5f); return; }
            EndAcroSilently();
            blendT = -1f;
            rag.Begin(vel + velS, spin, hitSeg, hitImpulse);
            ragT = 0f;
            ragMin = minTime;
            ragSettle = 0f;
            ragGetUp = !float.IsInfinity(minTime);
            lockT = 0f;
        }

        void FixedUpdate()
        {
            if (rag != null && rag.On) rag.ExtraGravity(0.6f);
        }

        /// <summary>Back on his feet from wherever the ragdoll left him: a springy push-up or sit-up, blended from the limp pose.</summary>
        void GetUp()
        {
            Transform hips = bonesAll[0], chest = bonesAll[2], head = bonesAll[4];
            Vector3 hp = hips.position;
            Vector3 hd = head.position - hp;
            hd.y = 0f;
            bool upright = Vector3.Dot(chest.up, Vector3.up) > 0.6f;
            bool faceUp = Vector3.Dot(chest.forward, Vector3.up) > 0f;
            for (int i = 0; i < 20; i++) { blendPos[i] = bonesAll[i].position; blendRot[i] = bonesAll[i].rotation; }
            for (int j = 0; j < JN; j++) { cur[j] = J[j].localRotation.eulerAngles; vel[j] = Vector3.zero; }
            rag.End();
            Vector3 flat = hd.sqrMagnitude > 1e-4f ? hd.normalized : transform.forward;
            float L = hipsBind.y;
            rootOff = rootOffV = rootRotV = Vector3.zero;
            hipY = hipYV = 0f;
            if (upright)
            {
                Vector3 f = chest.forward; f.y = 0f;
                if (f.sqrMagnitude > 1e-4f) yaw = Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg;
                pos = new Vector3(hp.x, 0f, hp.z);
                rootRot = Vector3.zero;
                act = Act.Idle;
            }
            else
            {
                getUpFaceUp = faceUp;
                yaw = Mathf.Atan2(faceUp ? -flat.x : flat.x, faceUp ? -flat.z : flat.z) * Mathf.Rad2Deg;
                pos = hp - flat * (L * 0.9f);
                rootRot = new Vector3(faceUp ? -84f : 84f, 0f, 0f);
                act = Act.GetUp;
            }
            pos.y = Geo.GroundY(pos);
            lastPos = pos;
            velS = Vector3.zero;
            actT = 0f;
            lockT = upright ? 0.25f : 0.78f;
            blendT = 0f;
        }

        void BlendFromLimp(float dt)
        {
            if (blendT < 0f) return;
            blendT += dt;
            float u = Mathf.Clamp01(blendT / BlendDur);
            u = u * u * (3f - 2f * u);
            for (int i = 0; i < 20; i++) animRot[i] = bonesAll[i].rotation;
            Vector3 hp = bonesAll[0].position;
            bonesAll[0].SetPositionAndRotation(Vector3.Lerp(blendPos[0], hp, u), Quaternion.Slerp(blendRot[0], animRot[0], u));
            for (int i = 1; i < 20; i++) bonesAll[i].rotation = Quaternion.Slerp(blendRot[i], animRot[i], u);
            if (blendT >= BlendDur) blendT = -1f;
        }

        /// <summary>Drop every slapstick state at once (teleports, hiding).</summary>
        void ClearSlapstick()
        {
            if (rag != null && rag.On) rag.End();
            blendT = -1f;
            lockT = 0f;
            hasQueued = false;
            acroKind = 0;
            throwGag = 0;
        }

        // ------------------------------------------------------------------ acrobatics

        public bool InAcro => acroKind != 0;

        /// <summary>Whole-body flips and spins: backflip, tornado, cartwheel, corkscrew (then limp), plank faint, frontflip, spin-out.</summary>
        public void Acro(int kind, float delay = 0f, float sign = 1f, bool thenRagdoll = false)
        {
            if (Ragdolled) return;
            acroKind = kind;
            acroT = -delay;
            acroSign = sign;
            acroThenRagdoll = thenRagdoll;
            acroDur = kind == AcroBackflip ? 0.52f : kind == AcroTornado ? 0.62f : kind == AcroCartwheel ? 0.55f : kind == AcroCorkscrew ? 0.5f
                    : kind == AcroPlank ? 1.75f : kind == AcroFrontflip ? 0.55f : 0.5f;
        }

        static float Flip01(float u) => u * u * (3f - 2f * u);

        void EvalAcro(out Quaternion r, out float lift, out float pivot)
        {
            r = Quaternion.identity;
            lift = 0f;
            pivot = hipsBind.y + hipY * s;
            if (acroT < 0f) return;
            float u = Mathf.Clamp01(acroT / acroDur);
            switch (acroKind)
            {
                case AcroBackflip: r = Quaternion.Euler(-360f * Flip01(u), 0f, 0f); lift = 4f * 0.95f * s * u * (1f - u); break;
                case AcroFrontflip: r = Quaternion.Euler(360f * Flip01(u), 0f, 0f); lift = 4f * 0.85f * s * u * (1f - u); break;
                case AcroTornado: r = Quaternion.Euler(0f, 720f * MathX.Ease(u) * acroSign, 0f); lift = 0.14f * s * Mathf.Abs(Mathf.Sin(u * Mathf.PI * 2f)); break;
                case AcroCartwheel: r = Quaternion.Euler(0f, 0f, 360f * Flip01(u) * acroSign); lift = 4f * 0.5f * s * u * (1f - u); break;
                case AcroCorkscrew:
                {
                    float e = 1f - (1f - u) * (1f - u);
                    r = Quaternion.Euler(0f, 560f * e * acroSign, 22f * u * acroSign);
                    lift = 0.3f * s * Mathf.Sin(Mathf.PI * u);
                    break;
                }
                case AcroSpinOut: { float e = 1f - (1f - u) * (1f - u); r = Quaternion.Euler(0f, 360f * e * acroSign, 0f); break; }
                case AcroPlank:
                {
                    // stiff as a board, straight back onto the dirt, one bounce
                    float t = acroT, fall = Mathf.Clamp01(t / 0.42f);
                    float ang = -88f * fall * fall;
                    if (t > 0.42f) ang += 9f * Mathf.Exp(-(t - 0.42f) * 8f) * Mathf.Abs(Mathf.Sin((t - 0.42f) * 20f));
                    r = Quaternion.Euler(ang, 0f, 0f);
                    pivot = 0f;
                    break;
                }
            }
        }

        void EndAcro()
        {
            int k = acroKind;
            acroKind = 0;
            if (k == AcroPlank) { rootRot = new Vector3(-88f, 0f, 0f); rootRotV = Vector3.zero; }   // the springs boing him back up
            if (k == AcroCorkscrew && acroThenRagdoll)
            {
                // the swing spun him round and round and down he goes
                Ragdoll(transform.right * (1.2f * acroSign) + Vector3.up * 0.4f, new Vector3(0f, 9f * acroSign, 0f) + transform.forward * (2.5f * acroSign), 1.0f);
            }
        }

        void EndAcroSilently() { acroKind = 0; }

        public void WackyWindup(int style)
        {
            windStyle = style;
            float hand = data.throws == Hand.Left ? -1f : 1f;
            if (style == 1) Acro(AcroBackflip, 0.12f);
            else if (style == 2) Acro(AcroTornado, 0.05f, hand);
            else if (style == 4) Acro(AcroCartwheel, 0.08f, hand);
        }

        void Pose(float speed, float dt, ref Vector3 tOff, ref Vector3 tRot, ref float tHip)
        {
            rigidPose = false;
            float bs = Mathf.Sin(tt * 1.9f + seed);
            S(HIPS, 0, 0, 0);
            S(SPINE, 3f, 0, 0);
            S(CHEST, -1f + bs * 1.2f, 0, 0);
            S(NECK, -2f, 0, Mathf.Sin(tt * 1.1f + seed) * 2f);
            S(HEAD, -2f, 0, 0);
            S(SHL, 3, 0, -11); S(ELL, -12, 0, 0); S(HAL, 0, 0, 0);
            S(SHR, 3, 0, 11); S(ELR, -12, 0, 0); S(HAR, 0, 0, 0);
            S(THL, 0, 0, -3); S(KNL, 2, 0, 0); S(FTL, -2, 0, 0);
            S(THR, 0, 0, 3); S(KNR, 2, 0, 0); S(FTR, -2, 0, 0);
            float targetMouth = mouth;
            float glove = gloveW;
            beatTarget = 0f;
            if (isApe)
            {
                // the gorilla stands hunched, knuckles low, legs bowed
                S(SPINE, 18f, 0, 0); S(CHEST, 10f + bs * 2f, 0, 0); S(NECK, -24f, 0, 0); S(HEAD, -10f, 0, 0);
                S(SHL, -8, 0, -16); S(ELL, -20, 0, 0); S(SHR, -8, 0, 16); S(ELR, -20, 0, 0);
                S(THL, -6, 0, -11); S(KNL, 16, 0, 0); S(THR, -6, 0, 11); S(KNR, 16, 0, 0);
                tHip = -0.04f;
            }

            switch (act)
            {
                case Act.Idle:
                    if (speed > 0.6f) RunPose(speed, dt, ref tHip, 1f);
                    else if (isApe)
                    {
                        // every few seconds he pounds his chest
                        beatT -= dt;
                        if (beatT < -1.4f) beatT = 2.5f + (seed % 3f);
                        if (beatT < 0f) ChestBeatPose(-beatT, ref tOff, ref targetMouth);
                    }
                    break;

                case Act.Run:
                    RunPose(Mathf.Max(speed, 4f), dt, ref tHip, 1f);
                    targetMouth = Mathf.Max(mouth, 0.35f);
                    break;

                case Act.Jog:
                    RunPose(Mathf.Max(speed, 2.5f), dt, ref tHip, 0.6f);
                    S(SHL, -150 + 20 * Mathf.Sin(tt * 6f), 0, -20); S(ELL, -30, 0, 0);
                    targetMouth = 0.7f;
                    break;

                case Act.Ready:
                    if (speed > 0.8f) { RunPose(speed, dt, ref tHip, 1f); break; }
                    tHip = -0.12f + 0.01f * Mathf.Sin(tt * 6f + seed);
                    S(SPINE, 28f, 0, 0); S(CHEST, 8, 0, 0);
                    S(NECK, -20, 0, 0); S(HEAD, -14, 0, 0);
                    S(THL, -42, 0, -12); S(KNL, 62, 0, 0); S(FTL, -20, 0, 0);
                    S(THR, -42, 0, 12); S(KNR, 62, 0, 0); S(FTR, -20, 0, 0);
                    S(SHL, -55, 0, -14); S(ELL, -32, 0, 0);
                    S(SHR, -48, 0, 14); S(ELR, -40, 0, 0);
                    break;

                case Act.Stance:
                    BatterPose(ref tOff, ref tHip, ref targetMouth);
                    break;

                case Act.Bunt:
                    tHip = -0.12f;
                    S(HIPS, 0, -40, 0); S(SPINE, 18, -18, 0); S(CHEST, 6, -14, 0);
                    S(NECK, 0, -8, 0); S(HEAD, -6, 0, 0);
                    S(THL, -24, 0, -14); S(KNL, 40, 0, 0); S(FTL, -16, 0, 0);
                    S(THR, -18, 0, 16); S(KNR, 36, 0, 0); S(FTR, -16, 0, 0);
                    S(SHL, -70, 0, -10); S(SHR, -70, 0, 10);
                    targetMouth = 0.3f;
                    break;

                case Act.PitchSet:
                    S(SPINE, 6, 0, 0); S(CHEST, 2, 0, 0); S(NECK, -6, 0, 0);
                    S(THL, -4, 0, -4); S(KNL, 10, 0, 0); S(THR, 0, 0, 4); S(KNR, 8, 0, 0);
                    S(SHL, -40, 0, -12); S(ELL, -85, 0, 0);
                    S(SHR, -38, 0, 14); S(ELR, -85, 0, 0);
                    glove = 0f;
                    break;

                case Act.PitchWind:
                {
                    float p = MathX.Ease(Seg(actT, 0f, 0.5f));
                    float k = Mathf.Lerp(1f, 1.45f, bigKick);
                    float wob = Mathf.Sin(tt * 5f + seed) * 2.5f * p;
                    S(SPINE, Mathf.Lerp(5f, -4f, p), Mathf.Lerp(0f, 62f, p), wob);
                    S(CHEST, Mathf.Lerp(0f, -8f, p), Mathf.Lerp(0f, 22f, p), 0);
                    S(NECK, -6, -Mathf.Lerp(0f, 70f, p), 0);
                    S(THL, Mathf.Lerp(-4f, -82f * k, p), 0, -6); S(KNL, Mathf.Lerp(10f, 96f + 20f * bigKick, p), 0, 0); S(FTL, Mathf.Lerp(-2f, 25f, p), 0, 0);
                    S(THR, 0, 0, 4); S(KNR, Mathf.Lerp(8f, 14f, p), 0, 0);
                    S(SHL, -42, 0, -14); S(ELL, -88, 0, 0);
                    S(SHR, -40, 0, 16); S(ELR, -88, 0, 0);
                    tHip = 0.02f * p;
                    glove = 0f;
                    targetMouth = 0.2f + 0.3f * bigKick * p;
                    if (windStyle != 0) WindOverlay(ref tOff, ref tHip, ref targetMouth);
                    break;
                }

                case Act.PitchThrow:
                {
                    float p = Mathf.Clamp01(actT / 0.9f);
                    float rel = PitchRelease / 0.9f;
                    float a = MathX.Ease(Seg(p, 0f, rel * 0.7f)), b = MathX.Ease(Seg(p, rel * 0.7f, rel * 1.08f)), c = MathX.Ease(Seg(p, rel * 1.08f, 1f));
                    K4(SPINE, new Vector3(-4, 62, 0), new Vector3(8, 40, 0), new Vector3(32, -25, 0), new Vector3(40, -32, 0), a, b, c);
                    K4(CHEST, new Vector3(-8, 22, 0), new Vector3(-10, 12, 0), new Vector3(16, -32, 0), new Vector3(14, -20, 0), a, b, c);
                    K4(THL, new Vector3(-82, 0, -6), new Vector3(-40, 0, -6), new Vector3(-30, 0, -6), new Vector3(-22, 0, -4), a, b, c);
                    K4(KNL, new Vector3(96, 0, 0), new Vector3(30, 0, 0), new Vector3(28, 0, 0), new Vector3(24, 0, 0), a, b, c);
                    K4(THR, new Vector3(0, 0, 4), new Vector3(14, 0, 6), new Vector3(28, 0, 6), new Vector3(52, 0, 8), a, b, c);
                    K4(KNR, new Vector3(14, 0, 0), new Vector3(30, 0, 0), new Vector3(36, 0, 0), new Vector3(78, 0, 0), a, b, c);
                    K4(SHR, new Vector3(-40, 0, 16), new Vector3(35, 0, 100), new Vector3(-120, 0, 36), new Vector3(-55, 0, -18), a, b, c);
                    K4(ELR, new Vector3(-88, 0, 0), new Vector3(-100, 0, 0), new Vector3(-12, 0, 0), new Vector3(-45, 0, 0), a, b, c);
                    K4(SHL, new Vector3(-42, 0, -14), new Vector3(-85, 0, -35), new Vector3(-20, 0, -50), new Vector3(8, 0, -25), a, b, c);
                    K4(ELL, new Vector3(-88, 0, 0), new Vector3(-30, 0, 0), new Vector3(-75, 0, 0), new Vector3(-65, 0, 0), a, b, c);
                    S(NECK, -4, -tgt[SPINE].y * 0.7f, 0);
                    tOff = new Vector3(0f, 0f, 0.62f * Mathf.Max(b, c * 0.92f));
                    tHip = -0.08f * b;
                    targetMouth = p > rel * 0.8f && p < rel * 1.6f ? 0.9f : 0.3f;
                    glove = 0f;
                    break;
                }

                case Act.Throw:
                {
                    float p = Mathf.Clamp01(actT / 0.42f);
                    float rel = ThrowRelease / 0.42f;
                    float a = MathX.Ease(Seg(p, 0f, rel * 0.8f)), b = MathX.Ease(Seg(p, rel * 0.8f, rel * 1.15f)), c = MathX.Ease(Seg(p, rel * 1.15f, 1f));
                    K4(SPINE, new Vector3(10, 0, 0), new Vector3(5, 45, 0), new Vector3(28, -30, 0), new Vector3(10, 0, 0), a, b, c);
                    K4(SHR, new Vector3(-30, 0, 10), new Vector3(35, 0, 100), new Vector3(-110, 0, 30), new Vector3(-12, 0, 10), a, b, c);
                    K4(ELR, new Vector3(-60, 0, 0), new Vector3(-95, 0, 0), new Vector3(-15, 0, 0), new Vector3(-20, 0, 0), a, b, c);
                    K4(SHL, new Vector3(-40, 0, -10), new Vector3(-85, 0, -25), new Vector3(-20, 0, -40), new Vector3(0, 0, -10), a, b, c);
                    K4(THL, new Vector3(-10, 0, 0), new Vector3(-38, 0, -4), new Vector3(-16, 0, -4), new Vector3(0, 0, 0), a, b, c);
                    K4(THR, new Vector3(5, 0, 0), new Vector3(5, 0, 4), new Vector3(25, 0, 4), new Vector3(0, 0, 0), a, b, c);
                    S(NECK, -4, -tgt[SPINE].y * 0.7f, 0);
                    targetMouth = 0.8f;
                    glove = 0f;
                    break;
                }

                case Act.Scoop:
                    tHip = -0.3f;
                    S(SPINE, 48, 0, 0); S(CHEST, 12, 0, 0); S(NECK, -40, 0, 0); S(HEAD, -18, 0, 0);
                    S(THL, -70, 0, -22); S(KNL, 92, 0, 0); S(FTL, -24, 0, 0);
                    S(THR, -50, 0, 22); S(KNR, 82, 0, 0); S(FTR, -30, 0, 0);
                    S(SHR, -60, 0, 18); S(ELR, -40, 0, 0);
                    glove = 1f;
                    break;

                case Act.Jump:
                {
                    float p = Seg(actT, 0f, 0.62f);
                    tOff = new Vector3(0f, 0.75f * Mathf.Sin(Mathf.PI * p), 0f);
                    S(THL, -40 * Mathf.Sin(Mathf.PI * p), 0, -6); S(KNL, 70 * Mathf.Sin(Mathf.PI * p), 0, 0);
                    S(THR, -10, 0, 6); S(KNR, 50 * Mathf.Sin(Mathf.PI * p), 0, 0);
                    S(SHR, -150, 0, 30); S(ELR, -20, 0, 0);
                    S(NECK, -25, 0, 0);
                    glove = 1f;
                    targetMouth = 0.9f;
                    break;
                }

                case Act.Dive:
                {
                    float p = Mathf.Clamp01(actT / 0.36f);
                    float fl = Mathf.Sin(tt * 26f);
                    tRot = new Vector3(84f * MathX.Ease(Mathf.Min(1f, p * 1.7f)), 0f, 0f);
                    tOff = new Vector3(0f, 0.18f + 0.45f * Mathf.Sin(Mathf.PI * p), 0f);
                    S(SPINE, -8, 0, 0); S(CHEST, -6, 0, 0); S(NECK, -40, 0, 0); S(HEAD, -22, 0, 0);
                    S(SHL, -172, 0, -12); S(ELL, -5, 0, 0);
                    S(SHR, -158, 0, 26); S(ELR, -18, 0, 0);
                    S(THL, 20 + 25 * fl, 0, -9); S(KNL, 38 + 28 * fl, 0, 0); S(FTL, 30, 0, 0);
                    S(THR, 26 - 25 * fl, 0, 9); S(KNR, 48 - 20 * fl, 0, 0); S(FTR, 30, 0, 0);
                    targetMouth = 1f;
                    glove = 1f;
                    break;
                }

                case Act.Down:
                {
                    float up = Seg(actT, 0.7f, 1.0f);
                    float f = Mathf.Sin(tt * 18f) * (1f - Seg(actT, 0f, 0.5f));
                    tRot = new Vector3(Mathf.Lerp(88f, 0f, up), 0f, 0f);
                    tOff = new Vector3(0f, Mathf.Lerp(0.12f, 0f, up), 0f);
                    S(NECK, -30 * (1f - up), 0, 0); S(HEAD, -15 * (1f - up), 0, 0);
                    S(SHL, -150 * (1f - up), 0, -30); S(ELL, -20, 0, 0);
                    S(SHR, -120 * (1f - up), 0, 40 + 20 * f); S(ELR, -30, 0, 0);
                    S(THL, 25 * f, 0, -18); S(KNL, 40 + 40 * f, 0, 0); S(FTL, 40, 0, 0);
                    S(THR, -25 * f, 0, 18); S(KNR, 60 - 30 * f, 0, 0); S(FTR, 40, 0, 0);
                    targetMouth = 0.8f;
                    break;
                }

                case Act.Slide:
                    tRot = new Vector3(-62f, 0f, 0f);
                    tOff = new Vector3(0f, -0.1f, 0f);
                    S(THL, -80, 0, -6); S(KNL, 10, 0, 0); S(FTL, -20, 0, 0);
                    S(THR, -52, 0, 8); S(KNR, 62, 0, 0); S(FTR, 10, 0, 0);
                    S(SHL, -128, 0, -38); S(SHR, -128, 0, 38);
                    S(NECK, 20, 0, 0);
                    targetMouth = 1f;
                    break;

                case Act.Celebrate:
                {
                    float hop = Mathf.Abs(Mathf.Sin(actT * 7.5f + seed));
                    tOff = new Vector3(0f, 0.22f * hop, 0f);
                    S(SHL, -165, 0, -26); S(ELL, -22, 0, 0);
                    S(SHR, -165, 0, 26); S(ELR, -22, 0, 0);
                    S(THL, -20 * hop, 0, -6); S(KNL, 40 * hop, 0, 0);
                    S(THR, -20 * hop, 0, 6); S(KNR, 40 * hop, 0, 0);
                    S(NECK, -18, 0, 0); S(HEAD, -10, 0, 0);
                    targetMouth = 1f;
                    break;
                }

                case Act.Dance:
                {
                    // the noodle dance
                    float w = Mathf.Sin(actT * 9f + seed), w2 = Mathf.Sin(actT * 4.5f + seed);
                    S(HIPS, 0, 25 * w2, 12 * w);
                    S(SPINE, 4, -20 * w2, -14 * w); S(CHEST, 0, -10 * w2, -10 * w);
                    S(NECK, 0, 15 * w, 18 * w2); S(HEAD, 0, 0, 10 * w);
                    S(SHL, -90 + 60 * w, 0, -70 - 30 * w2); S(ELL, -60 - 40 * w2, 0, 0);
                    S(SHR, -90 - 60 * w, 0, 70 + 30 * w2); S(ELR, -60 + 40 * w2, 0, 0);
                    S(THL, -20 * Mathf.Max(0f, w), 0, -8); S(KNL, 40 * Mathf.Max(0f, w), 0, 0);
                    S(THR, -20 * Mathf.Max(0f, -w), 0, 8); S(KNR, 40 * Mathf.Max(0f, -w), 0, 0);
                    tOff = new Vector3(0.05f * w2, 0.05f * Mathf.Abs(w), 0f);
                    targetMouth = 0.6f + 0.4f * Mathf.Abs(w);
                    break;
                }

                case Act.Sad:
                    S(SHL, -150, 0, -48); S(ELL, -125, 0, 0);
                    S(SHR, -150, 0, 48); S(ELR, -125, 0, 0);
                    S(NECK, 26, 0, Mathf.Sin(tt * 3f) * 8f); S(HEAD, 8, 0, 0);
                    S(SPINE, 16, 0, 0);
                    S(KNL, 12, 0, 0); S(KNR, 12, 0, 0);
                    targetMouth = 0.1f;
                    break;

                case Act.Dizzy:
                {
                    float w = Mathf.Sin(tt * 3.2f), w2 = Mathf.Cos(tt * 2.3f);
                    S(SPINE, 6, 0, 10 * w); S(NECK, 10 * w2, 0, 22 * w); S(HEAD, 0, 0, 12 * w);
                    S(SHL, -20, 0, -70 - 10 * w); S(ELL, -10, 0, 0);
                    S(SHR, -20, 0, 70 + 10 * w2); S(ELR, -10, 0, 0);
                    S(THL, -8 * w, 0, -10); S(THR, 8 * w, 0, 10);
                    tRot = new Vector3(5f * w2, 0f, 8f * w);
                    targetMouth = 0.55f;
                    break;
                }

                case Act.Flop:
                {
                    float p = Seg(actT, 0f, 0.42f);
                    float bounce = actT > 0.42f ? Mathf.Abs(Mathf.Sin((actT - 0.42f) * 12f)) * Mathf.Exp(-(actT - 0.42f) * 6f) : 0f;
                    tRot = new Vector3(-84f * MathX.Ease(p), 0f, 0f);
                    tOff = new Vector3(0f, 0.12f * Mathf.Sin(Mathf.PI * p) + 0.08f * bounce, -0.3f * p);
                    S(SHL, -160, 0, -60); S(ELL, -20, 0, 0);
                    S(SHR, -160, 0, 60); S(ELR, -20, 0, 0);
                    S(THL, -60 * p, 0, -12); S(KNL, 20, 0, 0);
                    S(THR, -80 * p, 0, 12); S(KNR, 10, 0, 0);
                    S(NECK, 20, 0, 0);
                    targetMouth = 1f;
                    break;
                }

                case Act.Tag:
                    tHip = -0.25f;
                    S(SPINE, 40, 0, 0); S(NECK, -30, 0, 0);
                    S(THL, -50, 0, -20); S(KNL, 70, 0, 0);
                    S(THR, -45, 0, 20); S(KNR, 66, 0, 0);
                    glove = 1f;
                    break;

                case Act.GetUp:
                {
                    // push up off the face, or sit up off the back, tuck the legs and pop up
                    float up = MathX.Ease(Seg(actT, 0.1f, 0.62f));
                    float mid = Mathf.Sin(Mathf.PI * up);
                    tRot = new Vector3(Mathf.Lerp(getUpFaceUp ? -84f : 84f, 0f, up), 0f, 0f);
                    tOff = new Vector3(0f, 0.1f * (1f - up) + 0.1f * mid, 0f);
                    if (getUpFaceUp)
                    {
                        S(SPINE, 34f * mid, 0, 0); S(NECK, 20f * mid, 0, 0);
                        S(SHL, -30f * mid, 0, -30); S(ELL, -20, 0, 0); S(SHR, -30f * mid, 0, 30); S(ELR, -20, 0, 0);
                    }
                    else
                    {
                        S(SHL, Mathf.Lerp(-95f, 0f, up), 0, -22); S(ELL, -35f * (1f - up), 0, 0);
                        S(SHR, Mathf.Lerp(-95f, 0f, up), 0, 22); S(ELR, -35f * (1f - up), 0, 0);
                        S(NECK, -30f * (1f - up), 0, 0);
                    }
                    S(THL, -75f * mid, 0, -10); S(KNL, 115f * mid, 0, 0);
                    S(THR, -75f * mid, 0, 10); S(KNR, 115f * mid, 0, 0);
                    targetMouth = 0.6f;
                    break;
                }

                case Act.Tantrum:
                {
                    // stomp, stomp, stomp, fists in the air
                    float ph = Mathf.Sin(actT * 9f), f = Mathf.Sin(actT * 18f);
                    bool lft = ph > 0f;
                    tOff = new Vector3(0f, 0.14f * Mathf.Abs(ph), 0f);
                    S(THL, lft ? -62f : -4f, 0, -8); S(KNL, lft ? 95f : 12f, 0, 0);
                    S(THR, lft ? -4f : -62f, 0, 8); S(KNR, lft ? 12f : 95f, 0, 0);
                    S(SHL, -150f + 35f * f, 0, -35); S(ELL, -60f - 35f * f, 0, 0);
                    S(SHR, -150f - 35f * f, 0, 35); S(ELR, -60f + 35f * f, 0, 0);
                    S(SPINE, 8, 0, 8f * f); S(NECK, -22, 0, 0); S(HEAD, -14, 18f * f, 0);
                    targetMouth = 1f;
                    glove = 0f;
                    break;
                }

                case Act.ChestBeat:
                    ChestBeatPose(actT, ref tOff, ref targetMouth);
                    glove = 0f;
                    break;

                case Act.Squat:
                {
                    // the catcher's crouch: a deep squat on flat feet, knees wide, back fairly upright, bare hand tucked
                    // behind the thigh; the mitt is held up as the target by the IK. High targets lift him, low ones bend him over.
                    float rise = 0f, low = 0f;
                    if (gloveW > 0.01f)
                    {
                        Vector3 lt = transform.InverseTransformPoint(gloveAt);
                        rise = Smooth01((lt.y - 0.66f * s) / (0.42f * s));
                        low = Smooth01((0.52f * s - lt.y) / (0.2f * s));
                    }
                    squatRise = MathX.Damp(squatRise, rise, 7f, dt);
                    squatLow = MathX.Damp(squatLow, low, 7f, dt);
                    float r = squatRise, lo = squatLow;
                    float a = Mathf.Lerp(100f, 70f, r), kb = Mathf.Lerp(128f, 92f, r), sp = Mathf.Lerp(26f, 20f, r);
                    float fx = Mathf.Lerp(-28f, -22f, r), fy = Mathf.Lerp(12f, 8f, r), fz = Mathf.Lerp(-10f, 4f, r);
                    tHip = Mathf.Lerp(-0.309f, -0.185f, r) + 0.006f * Mathf.Sin(tt * 2.2f + seed);
                    S(HIPS, 0, 0, 0);
                    S(SPINE, Mathf.Lerp(17f, 20f, r) + 20f * lo, 0, 0); S(CHEST, 5f + 8f * lo, 0, 0);
                    S(NECK, -16f - 14f * lo, 0, 0); S(HEAD, -10f - 8f * lo, 0, 0);
                    S(THL, -a, 0, -sp); S(KNL, kb, 0, 0); S(FTL, fx, fy, fz);
                    S(THR, -a, 0, sp); S(KNR, kb, 0, 0); S(FTR, fx, -fy, -fz);
                    S(SHR, 30, -40, 30); S(ELR, -110, 0, 0);   // bare hand tucked behind the hip
                    S(SHL, -60, 0, -20); S(ELL, -40, 0, 0);
                    tOff = new Vector3(0f, 0f, -0.03f);
                    targetMouth = 0.12f;
                    break;
                }

                case Act.Wave:
                {
                    float w = Mathf.Sin(tt * 8f);
                    S(SHR, -170, 0, 30 + 25 * w); S(ELR, -30, 0, 0);
                    S(SHL, -170, 0, -30 - 25 * w); S(ELL, -30, 0, 0);
                    targetMouth = 0.9f;
                    break;
                }
            }
            // a swim ring round the belly: standing about, he rests his forearms on it
            if (floatie && act == Act.Idle && speed <= 0.6f && beatTarget <= 0f)
            {
                tgt[SHL].z -= 30f; tgt[SHR].z += 30f;
                tgt[SHL].x -= 10f; tgt[SHR].x -= 10f;
                tgt[ELL].x -= 38f; tgt[ELR].x -= 38f;
            }

            mouthCur = MathX.Damp(mouthCur, targetMouth, 14f, dt);
            gloveCur = MathX.Damp(gloveCur, glove, 14f, dt);
        }

        void RunPose(float speed, float dt, ref float tHip, float amp0)
        {
            runPhase += dt * speed / (1.7f * s * Mathf.Lerp(1f, PlayerMesh.LegK, 0.8f)) * Mathf.PI;
            float sn = Mathf.Sin(runPhase), c = Mathf.Cos(runPhase);
            float amp = (0.5f + 0.5f * Mathf.Clamp01(speed / 8f)) * amp0;
            tHip = -0.04f + 0.05f * Mathf.Abs(c) * amp;
            S(HIPS, 0, 7 * sn * amp, 0);
            S(SPINE, 12f + 6f * amp, -9 * sn * amp, 0); S(CHEST, 4, -7 * sn * amp, 0);
            S(NECK, -10, 0, 0); S(HEAD, -4, 9 * sn * amp, 0);
            S(THL, -58 * sn * amp, 0, -3); S(THR, 58 * sn * amp, 0, 3);
            S(KNL, 14f + 80f * amp * Mathf.Max(0f, c), 0, 0);
            S(KNR, 14f + 80f * amp * Mathf.Max(0f, -c), 0, 0);
            S(FTL, -14, 0, 0); S(FTR, -14, 0, 0);
            S(SHL, 50 * sn * amp, 0, -10); S(ELL, -88, 0, 0);
            S(SHR, -50 * sn * amp, 0, 10); S(ELR, -88, 0, 0);
            if (amp0 < 0.9f && runStyle != 10) return;   // the home-run trot keeps its own swagger
            switch (runStyle)
            {
                case 1:   // noodle: arms whirl over the head, head wobbles
                    S(SHL, -150f + 60f * sn, 0, -40f - 30f * c); S(ELL, -40f + 30f * c, 0, 0);
                    S(SHR, -150f - 60f * sn, 0, 40f + 30f * c); S(ELR, -40f - 30f * c, 0, 0);
                    S(HEAD, -4, 25f * sn * amp, 12f * c);
                    break;
                case 2:   // ninja: leaning way forward, arms straight back
                    S(SPINE, 34f, -6f * sn * amp, 0); S(CHEST, 8, 0, 0); S(NECK, -34, 0, 0);
                    S(SHL, 72f, 0, -14); S(ELL, -4, 0, 0); S(SHR, 72f, 0, 14); S(ELR, -4, 0, 0);
                    break;
                case 3:   // prancing horse: knees to the chin
                    S(THL, -95f * Mathf.Max(0f, sn) + 20f * Mathf.Max(0f, -sn), 0, -3); S(THR, -95f * Mathf.Max(0f, -sn) + 20f * Mathf.Max(0f, sn), 0, 3);
                    S(KNL, 20f + 110f * Mathf.Max(0f, sn), 0, 0); S(KNR, 20f + 110f * Mathf.Max(0f, -sn), 0, 0);
                    S(SHL, -60, 0, -40); S(SHR, -60, 0, 40); S(SPINE, -8, 0, 0);
                    break;
                case 4:   // windmill arms
                {
                    float wm = runPhase * Mathf.Rad2Deg * 1.2f;
                    S(SHL, -(wm % 360f), 0, -14); S(ELL, -8, 0, 0);
                    S(SHR, -((wm + 180f) % 360f), 0, 14); S(ELR, -8, 0, 0);
                    break;
                }
                case 10:  // gorilla gallop: bent over, knuckles reaching for the dirt
                {
                    float g = Mathf.Clamp01(speed / 5f);
                    S(SPINE, 30f + 28f * g, -6f * sn * amp, 0); S(CHEST, 14f + 8f * g, 0, 0);
                    S(NECK, -40f - 15f * g, 0, 0); S(HEAD, -12, 0, 0);
                    S(SHL, -30f - 55f * sn * amp * g, 0, -22); S(ELL, -10, 0, 0);
                    S(SHR, -30f + 55f * sn * amp * g, 0, 22); S(ELR, -10, 0, 0);
                    S(THL, -40f * sn * amp, 0, -12); S(THR, 40f * sn * amp, 0, 12);
                    tHip = -0.06f + 0.06f * Mathf.Abs(c) * amp;
                    break;
                }
            }
        }

        /// <summary>Fists pound the chest, left, right, left, right; head back, mouth wide.</summary>
        void ChestBeatPose(float t, ref Vector3 tOff, ref float mouthT)
        {
            beatTarget = 1f;
            beatClock = t;
            float beat = Mathf.Sin(t * 15f);
            float l = Mathf.Max(0f, beat), r = Mathf.Max(0f, -beat);
            S(SPINE, -6, 0, 0); S(CHEST, -12, 0, 0); S(NECK, -26, 0, 0); S(HEAD, -16, 0, 0);
            S(SHL, -62f - 22f * l, -28, -34f + 16f * l); S(ELL, -118f - 18f * l, 0, 0);
            S(SHR, -62f - 22f * r, 28, 34f - 16f * r); S(ELR, -118f - 18f * r, 0, 0);
            S(THL, -12, 0, -16); S(KNL, 28, 0, 0); S(THR, -12, 0, 16); S(KNR, 28, 0, 0);
            tOff = new Vector3(0f, 0.025f * (l + r), 0f);
            mouthT = 1f;
        }

        /// <summary>Wacky windups on top of the leg kick (right-handed; the mirror flips them for lefties).</summary>
        void WindOverlay(ref Vector3 tOff, ref float tHip, ref float mouthT)
        {
            bool acro = acroKind != 0;
            switch (windStyle)
            {
                case 1:   // backflip: crouch, tuck through the flip, land into the kick
                    if (acro && acroT < 0f)
                    {
                        tHip = -0.16f;
                        S(SPINE, 22, 0, 0); S(THL, -32, 0, -6); S(KNL, 60, 0, 0); S(THR, -32, 0, 6); S(KNR, 60, 0, 0);
                        S(SHL, 45, 0, -20); S(ELL, -15, 0, 0); S(SHR, 45, 0, 20); S(ELR, -15, 0, 0);
                    }
                    else if (acro)
                    {
                        S(SPINE, 30, 0, 0); S(CHEST, 10, 0, 0); S(NECK, 25, 0, 0);
                        S(THL, -118, 0, -12); S(KNL, 135, 0, 0); S(FTL, 25, 0, 0);
                        S(THR, -118, 0, 12); S(KNR, 135, 0, 0); S(FTR, 25, 0, 0);
                        S(SHL, -95, 0, -25); S(ELL, -70, 0, 0); S(SHR, -95, 0, 25); S(ELR, -70, 0, 0);
                        mouthT = 1f;
                    }
                    break;
                case 2:   // tornado: arms out, one leg up, spinning
                    if (acro && acroT >= 0f)
                    {
                        S(SHL, 0, 0, -88); S(ELL, 0, 0, 0); S(SHR, 0, 0, 88); S(ELR, 0, 0, 0);
                        S(THL, -75, 0, -8); S(KNL, 105, 0, 0); S(NECK, -10, 0, 0);
                        mouthT = 1f;
                    }
                    break;
                case 3:   // windmill: the throwing arm whirls through the whole windup
                {
                    float wm = (actT * 1150f) % 360f;
                    S(SHR, -wm, 0, 22); S(ELR, -8, 0, 0);
                    mouthT = 0.9f;
                    break;
                }
                case 4:   // cartwheel: arms up, legs wide
                    if (acro && acroT >= 0f)
                    {
                        S(SHL, -175, 0, -25); S(ELL, 0, 0, 0); S(SHR, -175, 0, 25); S(ELR, 0, 0, 0);
                        S(THL, 0, 0, -42); S(KNL, 5, 0, 0); S(THR, 0, 0, 42); S(KNR, 5, 0, 0);
                        S(SPINE, 0, 0, 0); S(NECK, 0, 0, 0);
                        mouthT = 1f;
                    }
                    break;
                case 5:   // bunny hops toward the plate on the plant leg, arms flapping
                    if (actT < 0.75f)
                    {
                        float ph = actT / 0.25f;
                        float hop = Mathf.Abs(Mathf.Sin(Mathf.PI * ph));
                        tOff = new Vector3(0f, 0.16f * hop, 0.12f * ph);
                        float fl = Mathf.Sin(actT * 25f);
                        S(SHL, -60f + 50f * fl, 0, -55); S(SHR, -60f - 50f * fl, 0, 55);
                        mouthT = 0.8f;
                    }
                    else tOff = new Vector3(0f, 0f, 0.36f);
                    break;
                case 6:   // gorilla: a chest-beating war cry before the kick
                    if (actT < 0.75f) ChestBeatPose(actT, ref tOff, ref mouthT);
                    break;
            }
        }

        // ------------------------------------------------------------------ IK

        void ApplyIK(float dt)
        {
            // the batter's feet stay planted (front foot striding, back foot turning on its toes) while his hips turn over them
            // (only in the box: once he sets off, or flips out of a whiff, his legs are his own again at once, or they would trail back to the box)
            legW = act == Act.Stance && acroKind == 0 ? MathX.Damp(legW, 1f, 40f, dt) : 0f;
            if (legW > 0.01f) BatterLegs(legW);
            beatW = MathX.Damp(beatW, beatTarget, 16f, dt);
            if (beatW > 0.01f)
            {
                // fists drum on the chest, left, right, left, right
                float ph = Mathf.Sin(beatClock * 15f);
                Transform ch = J[CHEST];
                Vector3 front = ch.TransformPoint(new Vector3(0f, 0.04f * s * PlayerMesh.TorsoK, chestFrontZ));
                // elbows flared out wide and level, forearms across the chest: the wrist sits outboard so the fist lands on the pec
                float side = (isApe ? 0.1f : 0.08f) * s, fist = 0.07f * s * PlayerMesh.HandK * (isApe ? 1.28f : 1f);
                Vector3 tl = front + ch.right * -(side + fist) + ch.forward * (0.035f * s + 0.15f * s * Mathf.Max(0f, ph));
                Vector3 tr = front + ch.right * (side + fist) + ch.forward * (0.035f * s + 0.15f * s * Mathf.Max(0f, -ph));
                Solve(J[SHL], J[ELL], J[HAL], tl, ch.TransformDirection(new Vector3(-1f, 0.2f, 0.25f)), beatW);
                Solve(J[SHR], J[ELR], J[HAR], tr, ch.TransformDirection(new Vector3(1f, 0.2f, 0.25f)), beatW);
                // Solve keeps the hand's old world rotation; for the drumming the fists follow the forearms across the chest
                J[HAL].localRotation = Quaternion.Slerp(J[HAL].localRotation, Quaternion.identity, beatW);
                J[HAR].localRotation = Quaternion.Slerp(J[HAR].localRotation, Quaternion.identity, beatW);
                if (beatTarget > 0f) return;
            }
            bool batIK = (act == Act.Stance || act == Act.Bunt) && bat != null;
            bool setIK = act == Act.PitchSet || act == Act.PitchWind;
            bool gloveLeft = data.throws == Hand.Right;
            float wl, wr;
            if (batIK || setIK) { wl = 1f; wr = 1f; }
            else { wl = gloveLeft ? gloveCur : 0f; wr = gloveLeft ? 0f : gloveCur; }
            ikWL = MathX.Damp(ikWL, wl, 20f, dt);
            ikWR = MathX.Damp(ikWR, wr, 20f, dt);
            float mx = mirror ? -1f : 1f;

            if (batIK)
            {
                Vector3 handle, dir;
                if (act == Act.Bunt) BuntPose(out handle, out dir); else BatPose(out handle, out dir);
                handle.x *= mx;
                dir.x *= mx;
                bat.localPosition = handle;
                bat.localRotation = Quaternion.LookRotation(dir, Vector3.up);
                float g0 = 0.035f * s, g1 = act == Act.Bunt ? 0.30f * s : 0.125f * s;
                Vector3 dW = transform.TransformDirection(dir);
                Vector3 top = transform.TransformPoint(handle + dir * g1);
                Vector3 bottom = transform.TransformPoint(handle + dir * g0);
                // the hand nearer the knob is the bottom hand: left for a right-handed batter
                Vector3 hl = mirror ? top : bottom, hr = mirror ? bottom : top;
                Vector3 pl, pr;
                if (act == Act.Bunt) { pl = transform.TransformDirection(new Vector3(-0.4f, -1f, -0.1f)); pr = transform.TransformDirection(new Vector3(0.4f, -1f, -0.1f)); }
                else BatElbows(mx, out pl, out pr);
                // the wrists sit back from the handle so the fists close round it
                float palm = 0.05f * s * PlayerMesh.HandK;
                Vector3 fl = Fingers(J[SHL].position, hl, dW), fr = Fingers(J[SHR].position, hr, dW);
                Solve(J[SHL], J[ELL], J[HAL], hl - fl * palm, pl, ikWL);
                Solve(J[SHR], J[ELR], J[HAR], hr - fr * palm, pr, ikWR);
                fl = Fingers(J[ELL].position, J[HAL].position + fl * palm, dW);
                fr = Fingers(J[ELR].position, J[HAR].position + fr * palm, dW);
                J[HAL].rotation = Quaternion.Slerp(J[HAL].rotation, GripRot(fl, dW, false), ikWL);
                J[HAR].rotation = Quaternion.Slerp(J[HAR].rotation, GripRot(fr, dW, true), ikWR);
                // where an arm comes up short, the bat goes to the hands rather than float off them
                Vector3 errL = J[HAL].position + fl * palm - hl, errR = J[HAR].position + fr * palm - hr;
                bat.position += (errL + errR) * 0.5f * Mathf.Min(ikWL, ikWR);
                return;
            }
            if (setIK)
            {
                Vector3 c = transform.TransformPoint(new Vector3(0f, chestY - 0.06f * s, 0.24f * s));
                Solve(J[SHL], J[ELL], J[HAL], c + transform.right * -0.02f * s, transform.TransformDirection(new Vector3(-0.6f, -1f, 0f)), ikWL);
                Solve(J[SHR], J[ELR], J[HAR], c + transform.right * 0.03f * s, transform.TransformDirection(new Vector3(0.6f, -1f, 0f)), ikWR);
                return;
            }
            int sh = gloveLeft ? SHL : SHR, el = sh + 1, ha = sh + 2;
            float w = gloveLeft ? ikWL : ikWR;
            if (act == Act.Squat)
            {
                if (w <= 0.01f) return;
                // the catcher holds the pocket on the target, facing the mound, fingers up (and leaning out to the glove side);
                // he turns the mitt over, fingers down, for low pitches
                float side = gloveLeft ? 1f : -1f;
                float roll = 180f + side * (14f + 166f * squatLow);
                Quaternion want = transform.rotation * Quaternion.Euler(0f, 0f, roll);
                Vector3 wrist = gloveAt - want * GloveTip;
                Vector3 shp = J[sh].position;
                Vector3 to = wrist - shp;
                float reach = armLen * 0.985f;
                if (to.magnitude > reach) to = to.normalized * reach;
                Solve(J[sh], J[el], J[ha], shp + to, transform.TransformDirection(new Vector3(-side, -0.8f, -0.25f)), w);
                J[ha].rotation = Quaternion.Slerp(J[ha].rotation, want, w);
                return;
            }
            if (w > 0.01f)
            {
                Vector3 shp = J[sh].position;
                Vector3 to = gloveAt - shp;
                float reach = armLen * 0.97f;
                if (to.magnitude > reach) to = to.normalized * reach;
                Solve(J[sh], J[el], J[ha], shp + to, transform.TransformDirection(new Vector3(gloveLeft ? -0.7f : 0.7f, -1f, -0.3f)), w);
                Vector3 face = gloveAt - J[ha].position;
                if (face.sqrMagnitude > 1e-4f)
                {
                    Vector3 up = J[el].position - J[ha].position;
                    Quaternion want = Quaternion.LookRotation(face.normalized, up.sqrMagnitude > 1e-6f ? up.normalized : Vector3.up);
                    J[ha].rotation = Quaternion.Slerp(J[ha].rotation, want, w);
                }
            }
        }

        // ------------------------------------------------------------------ batting
        // The swing in the batter's own frame for a right-hander (mirrored for lefties): +z toward the plate, -x toward the
        // pitcher, +y up. 'load' is the stride while the pitch comes in (set by Match); 'swing' runs 0..1 over Cfg.SwingTime
        // with bat on ball at Cfg.SwingToContact. Keys: set -> loaded (front foot down, hands back) -> slot (hips open first,
        // hands drop inside, barrel still back) -> contact (bat flat across the plate, front leg braced, back foot turning on
        // its toes) -> extension (both arms out toward the pitcher) -> a two-handed finish over the front shoulder.

        /// <summary>One key of the swing. Body yaws in degrees (+ = coiled toward the catcher), forward lean, hip drop (x s),
        /// back heel lift and back-foot turn, front toe turn, weight shift (x s, + = back), bat knob (x s, y from the shoulders),
        /// bat yaw and pitch, back elbow (0 up and out, 1 tucked to the hip, 2 out front at the finish).</summary>
        struct BK
        {
            public float hip, spine, chest, lean, drop, heel, pivot, toe, shift, yaw, pitch, elbow;
            public Vector3 hand;
            public BK(float hip, float spine, float chest, float lean, float drop, float heel, float pivot, float toe, float shift, Vector3 hand, float yaw, float pitch, float elbow)
            {
                this.hip = hip; this.spine = spine; this.chest = chest; this.lean = lean; this.drop = drop; this.heel = heel; this.pivot = pivot;
                this.toe = toe; this.shift = shift; this.hand = hand; this.yaw = yaw; this.pitch = pitch; this.elbow = elbow;
            }
            public static BK Lerp(BK a, BK b, float u) => new BK(
                Mathf.LerpUnclamped(a.hip, b.hip, u), Mathf.LerpUnclamped(a.spine, b.spine, u), Mathf.LerpUnclamped(a.chest, b.chest, u),
                Mathf.LerpUnclamped(a.lean, b.lean, u), Mathf.LerpUnclamped(a.drop, b.drop, u), Mathf.LerpUnclamped(a.heel, b.heel, u),
                Mathf.LerpUnclamped(a.pivot, b.pivot, u), Mathf.LerpUnclamped(a.toe, b.toe, u), Mathf.LerpUnclamped(a.shift, b.shift, u),
                Vector3.LerpUnclamped(a.hand, b.hand, u), Mathf.LerpUnclamped(a.yaw, b.yaw, u), Mathf.LerpUnclamped(a.pitch, b.pitch, u),
                Mathf.LerpUnclamped(a.elbow, b.elbow, u));
        }

        static readonly BK KSet = new BK(14f, 4f, 6f, 15f, -0.06f, 0f, 0f, 0f, 0f, new Vector3(0.18f, -0.10f, 0.09f), 145f, 48f, 0f);
        static readonly BK KLoad = new BK(22f, 6f, 9f, 16f, -0.075f, 0f, 0f, -10f, 0.025f, new Vector3(0.23f, -0.07f, 0.05f), 150f, 46f, 0f);
        static readonly BK KSlot = new BK(-8f, 11f, 10f, 18f, -0.09f, 14f, -28f, -16f, 0f, new Vector3(0.11f, -0.22f, 0.19f), 104f, 14f, 0.85f);
        static readonly BK KHit = new BK(-44f, 9f, 4f, 18f, -0.085f, 32f, -58f, -22f, -0.03f, Vector3.zero, -14f, 0f, 1f);
        static readonly BK KExt = new BK(-64f, -8f, -14f, 13f, -0.075f, 44f, -72f, -24f, -0.04f, new Vector3(-0.30f, -0.16f, 0.10f), -95f, 3f, 1.3f);
        static readonly BK KFin = new BK(-76f, -18f, -26f, 7f, -0.06f, 54f, -82f, -24f, -0.045f, new Vector3(-0.15f, -0.02f, -0.22f), -282f, 4f, 2f);
        readonly BK[] batKeys = new BK[5];
        BK bk = KSet;                      // this frame's key: the body (Pose), the bat and the arms and legs (IK) all read it
        float bkG, bkStride, bkLift;
        bool rigidPose;
        float legW;

        void BatterPose(ref Vector3 tOff, ref float tHip, ref float targetMouth)
        {
            float sw = swing;
            float cu = Cfg.SwingToContact / Cfg.SwingTime;
            float ld = MathX.Ease(load);
            // where along the keys: 0 launch, 1 slot, 2 contact, 3 extension, 4 finish. The swing speeds up all the way to the
            // ball and slows down after it, so the bat is quickest right at contact.
            float g;
            if (sw < 0f) g = 0f;
            else if (sw < cu)
            {
                float w = Mathf.Pow(sw / cu, 1.6f);
                g = w < 0.4f ? w / 0.4f : 1f + (w - 0.4f) / 0.6f;
            }
            else
            {
                float v = Mathf.Clamp01((sw - cu) / (1f - cu));
                float w = 1f - Mathf.Pow(1f - v, 2.2f);
                g = 2f + (w < 0.42f ? w / 0.42f : 1f + (w - 0.42f) / 0.58f);
            }
            var keys = batKeys;
            keys[0] = BK.Lerp(KSet, KLoad, ld);
            keys[1] = KSlot;
            keys[2] = ContactKey();
            keys[3] = KExt;
            keys[4] = KFin;
            // long arms and broad shoulders (the gorilla) hold the bat further out, or the elbows fold up over the helmet
            float reach = armLen / (0.415f * s) - 1f, wide = (rest[11].x - 0.19f * s) / s;
            if (reach > 0.02f || wide > 0.01f)
            {
                Vector3 o = new Vector3(Mathf.Max(0f, wide) * 0.8f, -0.12f * Mathf.Max(0f, reach), 0.2f * Mathf.Max(0f, reach));
                // (out along where the chest faces: the catcher side while loaded, the pitcher side through the extension, behind at the finish)
                keys[0].hand += o; keys[1].hand += o * 0.7f; keys[3].hand += new Vector3(-o.z * 0.8f, 0f, 0f); keys[4].hand += new Vector3(0f, 0f, -o.z * 0.3f);
            }
            int i = Mathf.Min(3, Mathf.FloorToInt(g));
            float u = g - i;
            BK k = BK.Lerp(keys[i], keys[i + 1], u);
            // the knob runs on a smooth curve through the keys
            k.hand = CatmullRom(keys[Mathf.Max(i - 1, 0)].hand, keys[i].hand, keys[i + 1].hand, keys[Mathf.Min(i + 2, 4)].hand, u);
            float wait = sw < 0f ? 1f - ld : 0f;
            if (wait > 0f)
            {
                // waiting on the pitch: the bat waggles, the body breathes
                float wg = Mathf.Sin(tt * 2.3f + seed);
                k.yaw += 7f * wg * wait;
                k.pitch += 3f * Mathf.Sin(tt * 2.3f + seed + 1.2f) * wait;
                k.hand.y += 0.012f * wg * wait;
                k.drop += 0.006f * Mathf.Sin(tt * 1.6f + seed) * wait;
            }
            bk = k;
            bkG = g;
            // the stride: the front foot comes up and steps toward the pitcher as the pitch comes, and is down for the swing
            bkStride = Mathf.Max(ld, Mathf.Clamp01(g));
            bkLift = 0.09f * Mathf.Sin(Mathf.PI * Mathf.Clamp01(load / 0.9f)) * (1f - Mathf.Clamp01(g));

            tHip = k.drop;
            tOff = new Vector3(k.shift, 0f, 0.008f * Mathf.Sin(tt * 3f) * wait);
            S(HIPS, 0f, k.hip, 0f);
            S(SPINE, k.lean, k.spine, 0f);
            S(CHEST, 4f, k.chest, 0f);
            // eyes on the pitcher while waiting, on the ball through contact, then after it out into the field
            float body = k.hip + k.spine + k.chest;
            Vector3 c = ContactLocal();
            Vector3 hd = c - new Vector3(0f, shoulderY + 0.22f * s, 0.05f * s);
            float y1 = Mathf.Atan2(hd.x, hd.z) * Mathf.Rad2Deg, p1 = Mathf.Atan2(-hd.y, new Vector2(hd.x, hd.z).magnitude) * Mathf.Rad2Deg;
            float hy, hp;
            if (g <= 2f) { float e = MathX.Ease(g / 1.6f); hy = Mathf.Lerp(-86f, y1, e); hp = Mathf.Lerp(-6f, p1, e); }
            else { float e = MathX.Ease((g - 2.6f) / 1.2f); hy = Mathf.Lerp(y1, -78f, e); hp = Mathf.Lerp(p1, -14f, e); }
            float ry = hy - body, rp = hp - (k.lean + 4f);
            S(NECK, rp * 0.45f, ry * 0.45f, 0f);
            S(HEAD, rp * 0.55f, ry * 0.55f, 0f);
            // arms and legs are set by the IK; these keep the springs close so it blends in and out cleanly
            S(SHL, -60, 0, -20); S(ELL, -60, 0, 0);
            S(SHR, -60, 0, 30); S(ELR, -70, 0, 0);
            S(THL, -14, 0, -12); S(KNL, 26, 0, 0); S(FTL, -12, 0, 0);
            S(THR, -12, 0, 12); S(KNR, 26, 0, 0); S(FTR, -12, 0, 0);
            targetMouth = sw > 0.25f && sw < 0.8f ? 0.9f : 0.2f;
            rigidPose = sw >= 0f || load > 0.01f;
        }

        /// <summary>Where the barrel meets the ball, in the batter's frame (right-handed): a little out in front of the plate.</summary>
        Vector3 ContactLocal()
        {
            Vector3 aim = transform.InverseTransformPoint(swingAim);
            if (mirror) aim.x = -aim.x;
            return new Vector3(-0.12f * s, Mathf.Clamp(aim.y, 0.3f, 1.4f * s), Mathf.Clamp(aim.z, 0.55f, 1.2f));
        }

        /// <summary>The contact key: the sweet spot on the ball, the bat flat across the plate, hands between the ball and the chest.</summary>
        BK ContactKey()
        {
            BK k = KHit;
            Vector3 c = ContactLocal();
            float barrel = megaBat ? 0.9f : 0.56f;
            float handY = Mathf.Lerp(c.y + 0.04f, shoulderY - 0.2f * s, 0.55f);
            float p = Mathf.Asin(Mathf.Clamp((c.y - handY) / barrel, -0.85f, 0.45f)) * Mathf.Rad2Deg;
            float yr = k.yaw * Mathf.Deg2Rad, pr = p * Mathf.Deg2Rad;
            Vector3 d = new Vector3(Mathf.Sin(yr) * Mathf.Cos(pr), Mathf.Sin(pr), Mathf.Cos(yr) * Mathf.Cos(pr));
            Vector3 h = c - d * barrel;
            k.hand = new Vector3(h.x / s, (h.y - shoulderY) / s, h.z / s);
            k.pitch = p;
            return k;
        }

        /// <summary>Bat knob and direction in the batter's local frame (right-handed; mirrored for lefties).</summary>
        void BatPose(out Vector3 handle, out Vector3 dir)
        {
            BK k = bk;
            handle = new Vector3(k.hand.x * s, shoulderY + k.hand.y * s, k.hand.z * s);
            float yr = k.yaw * Mathf.Deg2Rad, pr = k.pitch * Mathf.Deg2Rad;
            dir = new Vector3(Mathf.Sin(yr) * Mathf.Cos(pr), Mathf.Sin(pr), Mathf.Cos(yr) * Mathf.Cos(pr));
        }

        /// <summary>Elbow directions on the bat: the back elbow up and out in the stance, tucked to the hip in the slot, out front
        /// at the finish; the lead elbow down, then out front.</summary>
        void BatElbows(float mx, out Vector3 poleL, out Vector3 poleR)
        {
            float e = bk.elbow;
            Vector3 b0 = new Vector3(0.85f, -0.25f, -0.4f), b1 = new Vector3(0.2f, -1f, 0.25f), b2 = new Vector3(0.1f, -0.7f, 0.7f);
            Vector3 bp = e < 1f ? Vector3.Lerp(b0, b1, e) : Vector3.Lerp(b1, b2, e - 1f);
            Vector3 lp = Vector3.Lerp(new Vector3(-0.3f, -1f, -0.15f), new Vector3(-0.5f, -0.7f, 0.4f), Mathf.Clamp01(e - 1f));
            bp.x *= mx; lp.x *= mx;
            Quaternion ch = J[CHEST].rotation;
            Vector3 back = ch * bp, lead = ch * lp;
            poleL = mirror ? back : lead;
            poleR = mirror ? lead : back;
        }

        /// <summary>Which way the fingers of a hand on the bat point: from the arm toward the grip, square to the bat.</summary>
        static Vector3 Fingers(Vector3 from, Vector3 grip, Vector3 batDir)
        {
            Vector3 f = grip - from;
            f -= batDir * Vector3.Dot(f, batDir);
            return f.sqrMagnitude > 1e-6f ? f.normalized : Vector3.down;
        }

        /// <summary>A fist round the handle: knuckles along the bat, fingers (the hand's -y) wrapping it.</summary>
        static Quaternion GripRot(Vector3 fingers, Vector3 batDir, bool flip)
        {
            Vector3 y = -fingers, x = flip ? -batDir : batDir;
            Vector3 z = Vector3.Cross(x, y);
            return z.sqrMagnitude > 1e-6f ? Quaternion.LookRotation(z, y) : Quaternion.LookRotation(batDir, Vector3.up);
        }

        /// <summary>The batter's feet, planted in the box: the front foot strides toward the pitcher and opens a little, the back
        /// foot turns on the ball of the foot and comes up on its toes as the hips fire.</summary>
        void BatterLegs(float w)
        {
            BK k = bk;
            float mx = mirror ? -1f : 1f;
            Quaternion box = Quaternion.Euler(0f, yaw, 0f);
            float ay = rest[16].y, c = 0.085f * s;
            Vector3 front = new Vector3((-0.22f - 0.10f * bkStride) * s * mx, ay + bkLift * s, 0.03f * s);
            Quaternion fRot = box * Quaternion.Euler(0f, k.toe * mx, 0f);
            Vector3 ball = new Vector3(0.19f * s * mx, 0f, -0.03f * s + c);
            Quaternion bRot = box * Quaternion.Euler(0f, k.pivot * mx, 0f) * Quaternion.Euler(k.heel, 0f, 0f);
            Vector3 fW = pos + box * front;
            Vector3 bW = pos + box * ball + bRot * new Vector3(0f, ay, -c);
            Vector3 fPole = box * new Vector3(-0.1f * mx, 0.1f, 1f);
            Vector3 bPole = box * (Quaternion.Euler(0f, k.pivot * mx, 0f) * new Vector3(0.05f * mx, 0.1f, 1f));
            int fl = mirror ? THR : THL, bl = mirror ? THL : THR;
            Solve(J[fl], J[fl + 1], J[fl + 2], fW, fPole, w);
            Solve(J[bl], J[bl + 1], J[bl + 2], bW, bPole, w);
            J[fl + 2].rotation = Quaternion.Slerp(J[fl + 2].rotation, fRot, w);
            J[bl + 2].rotation = Quaternion.Slerp(J[bl + 2].rotation, bRot, w);
        }

        static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            float t2 = t * t, t3 = t2 * t;
            return 0.5f * (2f * p1 + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
        }

        void BuntPose(out Vector3 handle, out Vector3 dir)
        {
            Vector3 aim = transform.InverseTransformPoint(swingAim);
            if (mirror) aim.x = -aim.x;
            float cy = Mathf.Clamp(aim.y, 0.4f, 1.3f * s);
            handle = new Vector3(-0.1f * s, cy + 0.02f, 0.30f * s);
            dir = new Vector3(-0.08f, 0.03f, 1f).normalized;
        }

        static void Solve(Transform upper, Transform lower, Transform end, Vector3 target, Vector3 pole, float w)
        {
            if (w <= 0.001f) return;
            Vector3 a = upper.position;
            float la = (lower.position - a).magnitude, lb = (end.position - lower.position).magnitude;
            if (la < 1e-4f || lb < 1e-4f) return;
            Vector3 at = target - a;
            float d = Mathf.Clamp(at.magnitude, 0.02f, la + lb - 0.002f);
            Vector3 dir = at.sqrMagnitude > 1e-8f ? at.normalized : Vector3.down;
            float cosA = Mathf.Clamp((la * la + d * d - lb * lb) / (2f * la * d), -1f, 1f);
            float sinA = Mathf.Sqrt(Mathf.Max(0f, 1f - cosA * cosA));
            Vector3 pp = pole - dir * Vector3.Dot(pole, dir);
            if (pp.sqrMagnitude < 1e-6f) pp = Vector3.Cross(dir, Vector3.right);
            pp.Normalize();
            Vector3 elbow = a + dir * (la * cosA) + pp * (la * sinA);
            Vector3 hand = a + dir * d;
            Vector3 side = Vector3.Cross(dir, pp).normalized;

            Vector3 yU = -(elbow - a).normalized;
            Vector3 xU = (side - yU * Vector3.Dot(side, yU)).normalized;
            Quaternion qU = Quaternion.LookRotation(Vector3.Cross(xU, yU), yU);
            Vector3 yL = -(hand - elbow).normalized;
            Vector3 xL = (side - yL * Vector3.Dot(side, yL)).normalized;
            Quaternion qL = Quaternion.LookRotation(Vector3.Cross(xL, yL), yL);

            Quaternion endRot = end.rotation;
            upper.rotation = Quaternion.Slerp(upper.rotation, qU, w);
            lower.rotation = Quaternion.Slerp(lower.rotation, qL, w);
            end.rotation = endRot;
        }

        void Face(float dt)
        {
            jaw.localScale = new Vector3(1f, 0.35f + 1.4f * mouthCur, 1f);
            if (!looking) { eyeL.localRotation = Quaternion.identity; eyeR.localRotation = Quaternion.identity; return; }
            LookEye(eyeL);
            LookEye(eyeR);
            // turn the head a little toward what we watch
            Vector3 d = J[NECK].InverseTransformDirection(lookAt - J[HEAD].position);
            if (d.sqrMagnitude > 1e-4f && act != Act.Stance && act != Act.PitchThrow && act != Act.Dive && act != Act.Down && act != Act.Flop)
            {
                Quaternion want = Quaternion.LookRotation(d.normalized, Vector3.up);
                J[HEAD].localRotation = Quaternion.Slerp(J[HEAD].localRotation, Quaternion.RotateTowards(Quaternion.identity, want, 55f), 0.8f);
            }
        }

        void LookEye(Transform e)
        {
            Vector3 d = e.parent.InverseTransformDirection(lookAt - e.position);
            if (d.sqrMagnitude < 1e-6f) return;
            Quaternion want = Quaternion.LookRotation(d.normalized, Vector3.up);
            e.localRotation = Quaternion.RotateTowards(Quaternion.identity, want, 25f);
        }
    }

    /// <summary>World-space TextMeshPro helper (numbers on jerseys, ad boards, the scoreboard).</summary>
    public static class WorldText
    {
        public static TMP_FontAsset Font;

        public static Transform Make(Transform parent, string text, TMP_FontAsset font, Color c, float capHeight, bool bold)
        {
            var go = new GameObject("Text_" + text);
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<TextMeshPro>();
            var f = font != null ? font : Font;
            if (f != null) t.font = f;
            t.text = text;
            t.fontSize = 10f;
            t.color = c;
            t.alignment = TextAlignmentOptions.Center;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.overflowMode = TextOverflowModes.Overflow;
            t.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
            t.rectTransform.sizeDelta = new Vector2(20f, 5f);
            t.ForceMeshUpdate();
            float h = t.textBounds.size.y;
            float k = h > 1e-4f ? capHeight / h : capHeight * 0.14f;
            go.transform.localScale = Vector3.one * k;
            var r = go.GetComponent<MeshRenderer>();
            if (r != null) { r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false; }
            return go.transform;
        }

        public static TextMeshPro Board(Transform parent, string text, TMP_FontAsset font, Color c, Vector2 box, float fontSize)
        {
            var go = new GameObject("Board_" + text);
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<TextMeshPro>();
            var f = font != null ? font : Font;
            if (f != null) t.font = f;
            t.text = text;
            t.color = c;
            t.alignment = TextAlignmentOptions.Center;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.overflowMode = TextOverflowModes.Overflow;
            t.rectTransform.sizeDelta = box;
            t.enableAutoSizing = true;
            t.fontSizeMin = 1f;
            t.fontSizeMax = fontSize;
            t.fontSize = fontSize;
            var r = go.GetComponent<MeshRenderer>();
            if (r != null) { r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false; }
            return t;
        }
    }

    /// <summary>Builds the meshes for a player: skinned body, cap, helmet, glove. All in the character's local frame (y up, z forward).</summary>
    public static class PlayerMesh
    {
        public static readonly int[] Parent = { -1, 0, 1, 2, 3, 4, 4, 4, 2, 8, 9, 2, 11, 12, 0, 14, 15, 0, 17, 18 };

        /// <summary>Volume-keeping helper bones (20..29). Each sits on a joint, is a child of the bone above it and turns half as far
        /// as the bone below it; the skin across the joint blends parent - helper - child instead of parent - child, so linear
        /// skinning pinches and loses volume far less at shoulders, elbows, wrists, hips and knees. Elbow and knee helpers also
        /// slide out a little as the joint folds, so a bent knee keeps its kneecap.</summary>
        public const int Helpers = 10, BoneCount = 20 + Helpers;
        public static readonly int[] HelperParent = { 2, 8, 9, 2, 11, 12, 0, 14, 0, 17 };
        public static readonly int[] HelperChild = { 8, 9, 10, 11, 12, 13, 14, 15, 17, 18 };
        /// <summary>The skin is modelled and bound with the arms swung out this far from the sides: arms never melt into the torso,
        /// and the shoulders deform from their middle position (arms down and arms up are both only part of the way from here).</summary>
        public const float ArmOut = 40f;

        /// <summary>Model-space position and rotation of every bone and helper in the bind pose (the rest skeleton, arms out).</summary>
        public static void BindPose(Vector3[] rp, Vector3[] pos, Quaternion[] rot)
        {
            for (int i = 0; i < 20; i++)
            {
                int par = Parent[i];
                Quaternion local = i == 8 ? Quaternion.Euler(0f, 0f, -ArmOut) : i == 11 ? Quaternion.Euler(0f, 0f, ArmOut) : Quaternion.identity;
                if (par < 0) { rot[i] = local; pos[i] = rp[i]; }
                else { rot[i] = rot[par] * local; pos[i] = pos[par] + rot[par] * (rp[i] - rp[par]); }
            }
            for (int h = 0; h < Helpers; h++)
            {
                int par = HelperParent[h], ch = HelperChild[h];
                rot[20 + h] = rot[par] * Quaternion.Slerp(Quaternion.identity, Quaternion.Inverse(rot[par]) * rot[ch], 0.5f);
                pos[20 + h] = pos[ch];
            }
        }

        public static Matrix4x4[] BindPoses(Vector3[] rp)
        {
            var pos = new Vector3[BoneCount];
            var rot = new Quaternion[BoneCount];
            BindPose(rp, pos, rot);
            var bp = new Matrix4x4[BoneCount];
            for (int i = 0; i < BoneCount; i++) bp[i] = Matrix4x4.TRS(pos[i], rot[i], Vector3.one).inverse;
            return bp;
        }

        // Cute, casual cartoon proportions on top of each Body: a big head, short legs and arms, chunky limbs,
        // mitten hands, big shoes and a big glove (about 4 heads tall instead of 7.5).
        public const float LegK = 0.52f, TorsoK = 0.8f, ArmK = 0.74f, HeadK = 1.78f, HandK = 1.45f, FootK = 1.36f, LimbK = 1.24f, GloveK = 1.45f;
        public const float AnkleY = 0.082f * FootK;
        /// <summary>Head scale of a body with the cartoon proportions applied.</summary>
        public static float HeadOf(Body b) => b.head * HeadK;
        /// <summary>Shoulder and hip width of a body with the cartoon proportions: everyone a little stockier, extremes softened.</summary>
        public static float WidthOf(Body b) => Mathf.Lerp(1f, b.width, 0.75f) * 1.06f;

        public struct Key
        {
            public float u, rx, rz, ox, oz;
            public Key(float u, float rx, float rz, float ox = 0f, float oz = 0f) { this.u = u; this.rx = rx; this.rz = rz; this.ox = ox; this.oz = oz; }
        }

        public delegate Color ColFn(float u, float ang);
        public delegate void BoneFn(float u, out int b0, out int b1, out float w);

        public static Vector3[] Rig(Body b)
        {
            float s = b.height / 1.85f, w = WidthOf(b), hd = HeadOf(b);
            // a gorilla: short bowed legs, arms down to the knees, the head sunk between huge shoulders
            float lk = b.ape ? LegK * 0.84f : LegK, ak = b.ape ? ArmK * 1.42f : ArmK;
            var p = new Vector3[20];
            Vector3 hips = new Vector3(0f, (AnkleY + 0.865f * lk + 0.045f) * s, 0f);
            p[0] = hips;
            p[1] = hips + new Vector3(0f, 0.12f * TorsoK, 0f) * s;
            p[2] = hips + new Vector3(0f, 0.30f * TorsoK, 0f) * s;
            p[3] = hips + new Vector3(0f, (b.ape ? 0.47f : 0.505f) * TorsoK, b.ape ? 0.03f : 0f) * s;
            p[4] = p[3] + new Vector3(0f, b.ape ? 0.03f : 0.036f, b.ape ? 0.03f : 0.012f) * s * Mathf.Lerp(1f, hd, 0.5f);   // the big head sits low: almost no neck
            float hs = s * hd;
            p[5] = p[4] + (b.ape ? new Vector3(0f, 0.02f, 0.105f) : new Vector3(0f, 0.034f, 0.052f)) * hs;
            p[6] = p[4] + new Vector3(b.ape ? -0.03f : -0.033f, b.ape ? 0.078f : 0.09f, 0.08f) * hs;
            p[7] = p[4] + new Vector3(b.ape ? 0.03f : 0.033f, b.ape ? 0.078f : 0.09f, 0.08f) * hs;
            float shx = (0.185f + 0.012f * b.muscle) * w * (b.ape ? 1.1f : 1f);
            float shy = b.ape ? 0.475f : 0.462f;
            p[8] = hips + new Vector3(-shx, shy * TorsoK, -0.012f) * s;
            p[9] = p[8] + new Vector3(0f, -0.30f * ak, 0f) * s;
            p[10] = p[9] + new Vector3(0f, -0.26f * ak, 0f) * s;
            p[11] = hips + new Vector3(shx, shy * TorsoK, -0.012f) * s;
            p[12] = p[11] + new Vector3(0f, -0.30f * ak, 0f) * s;
            p[13] = p[12] + new Vector3(0f, -0.26f * ak, 0f) * s;
            float hx = 0.092f * w;
            p[14] = hips + new Vector3(-hx, -0.045f, 0f) * s;
            p[15] = p[14] + new Vector3(0f, -0.445f * lk, 0.01f) * s;
            p[16] = p[15] + new Vector3(0f, -0.42f * lk, -0.01f) * s;
            p[17] = hips + new Vector3(hx, -0.045f, 0f) * s;
            p[18] = p[17] + new Vector3(0f, -0.445f * lk, 0.01f) * s;
            p[19] = p[18] + new Vector3(0f, -0.42f * lk, -0.01f) * s;
            return p;
        }

        static float Spline(Key[] k, float u, int f)
        {
            int n = k.Length;
            if (u <= k[0].u) return F(k[0], f);
            if (u >= k[n - 1].u) return F(k[n - 1], f);
            int i = 0;
            while (i < n - 2 && u > k[i + 1].u) i++;
            float u0 = k[i].u, u1 = k[i + 1].u, h = u1 - u0, t = (u - u0) / h;
            float p0 = F(k[i], f), p1 = F(k[i + 1], f);
            float m0 = i > 0 ? (p1 - F(k[i - 1], f)) / (u1 - k[i - 1].u) : (p1 - p0) / h;
            float m1 = i < n - 2 ? (F(k[i + 2], f) - p0) / (k[i + 2].u - u0) : (p1 - p0) / h;
            float t2 = t * t, t3 = t2 * t;
            return (2f * t3 - 3f * t2 + 1f) * p0 + (t3 - 2f * t2 + t) * h * m0 + (-2f * t3 + 3f * t2) * p1 + (t3 - t2) * h * m1;
        }

        static float F(Key k, int f) => f == 0 ? k.rx : f == 1 ? k.rz : f == 2 ? k.ox : k.oz;

        /// <summary>Elliptic-section loft from A to B. ang 0 = front (+Z), +pi/2 = right (+X).</summary>
        public static void Loft(MeshKit k, Vector3 A, Vector3 B, Key[] keys, int rings, int segs, ColFn col, BoneFn bones, float[] edges, float stripeRepeat, System.Func<float, float, bool> striped)
        {
            rings = Mathf.Max(2, Mathf.RoundToInt(rings * k.detail));
            segs = Mathf.Max(4, Mathf.RoundToInt(segs * k.detail));
            Vector3 D = B - A;
            float L = D.magnitude;
            D /= L;
            Vector3 Z = Vector3.forward - D * Vector3.Dot(Vector3.forward, D);
            if (Z.sqrMagnitude < 1e-4f) Z = Vector3.up - D * Vector3.Dot(Vector3.up, D);
            Z.Normalize();
            Vector3 X = Vector3.Cross(D, Z);
            if (Vector3.Dot(X, Vector3.right) < 0f) X = -X;

            var us = new List<float>();
            for (int i = 0; i <= rings; i++) us.Add(i / (float)rings);
            if (edges != null) foreach (float e in edges) { if (e > 0.002f && e < 0.998f) { us.Add(e - 0.0006f); us.Add(e + 0.0006f); } }
            us.Sort();

            System.Func<float, float, Vector3> P = (u, a) =>
            {
                float rx = Spline(keys, u, 0), rz = Spline(keys, u, 1), ox = Spline(keys, u, 2), oz = Spline(keys, u, 3);
                return A + D * (u * L) + X * (ox + rx * Mathf.Sin(a)) + Z * (oz + rz * Mathf.Cos(a));
            };

            int s0 = k.V.Count;
            int cols = segs + 1;
            for (int i = 0; i < us.Count; i++)
            {
                float u = us[i];
                bones(u, out int b0, out int b1, out float bw);
                for (int j = 0; j <= segs; j++)
                {
                    float a = j / (float)segs * Mathf.PI * 2f;
                    Vector3 p = P(u, a);
                    Vector3 da = P(u, a + 0.01f) - P(u, a - 0.01f);
                    float du0 = Mathf.Max(0f, u - 0.01f), du1 = Mathf.Min(1f, u + 0.01f);
                    Vector3 du = P(du1, a) - P(du0, a);
                    // normalise the two tangents first: on thin limbs their raw cross product falls under Vector3.Normalize's
                    // 1e-5 cut-off and would come out as a zero normal (black, NaN-lit vertices)
                    da /= Mathf.Max(da.magnitude, 1e-12f);
                    du /= Mathf.Max(du.magnitude, 1e-12f);
                    Vector3 n = Vector3.Cross(da, du);
                    Vector3 radial = X * Mathf.Sin(a) + Z * Mathf.Cos(a);
                    float nm = n.magnitude;
                    n = nm > 1e-6f ? n / nm : radial;
                    if (Vector3.Dot(n, radial) < 0f) n = -n;
                    Color c = col(u, a > Mathf.PI ? a - Mathf.PI * 2f : a);
                    bool st = striped != null && stripeRepeat > 0f && striped(u, a);
                    Vector2 uv = st ? new Vector2(a / (Mathf.PI * 2f) * stripeRepeat, u) : MeshKit.Plain;
                    k.V.Add(p); k.N.Add(n); k.C.Add(c); k.UV.Add(uv); k.UV2.Add(k.extra); k.OL.Add(k.outline);
                    if (bw <= 0.002f || b0 == b1) k.W.Add(new BoneWeight { boneIndex0 = b0, weight0 = 1f });
                    else if (bw >= 0.998f) k.W.Add(new BoneWeight { boneIndex0 = b1, weight0 = 1f });
                    else if (bw <= 0.5f) k.W.Add(new BoneWeight { boneIndex0 = b0, weight0 = 1f - bw, boneIndex1 = b1, weight1 = bw });
                    else k.W.Add(new BoneWeight { boneIndex0 = b1, weight0 = bw, boneIndex1 = b0, weight1 = 1f - bw });
                }
            }
            // winding: make the first quad face outward
            bool flip = false;
            if (us.Count > 1)
            {
                Vector3 a0 = k.V[s0], a1 = k.V[s0 + 1], b0v = k.V[s0 + cols];
                Vector3 fn = Vector3.Cross(a1 - a0, b0v - a0);
                flip = Vector3.Dot(fn, k.N[s0]) < 0f;
            }
            for (int i = 0; i < us.Count - 1; i++)
                for (int j = 0; j < segs; j++)
                {
                    int a = s0 + i * cols + j, b = a + cols;
                    if (!flip) { k.T.Add(a); k.T.Add(a + 1); k.T.Add(b); k.T.Add(a + 1); k.T.Add(b + 1); k.T.Add(b); }
                    else { k.T.Add(a); k.T.Add(b); k.T.Add(a + 1); k.T.Add(a + 1); k.T.Add(b); k.T.Add(b + 1); }
                }
        }

        static BoneFn One(int b) => (float u, out int b0, out int b1, out float w) => { b0 = b; b1 = b; w = 0f; };

        static BoneFn Blend(int a, int b, float from, float to) => (float u, out int b0, out int b1, out float w) =>
        {
            b0 = a; b1 = b; w = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(from, to, u));
        };

        // material helpers: alpha = 1 - smoothness
        static Color Cloth(Color c) => Pal.Gloss(c, 0.14f);
        static Color SkinC(Color c) => Pal.Gloss(c, 0.36f);
        static Color HairC(Color c) => Pal.Gloss(c, 0.22f);

        public static float BackDepth(Body b)
        {
            float s = b.height / 1.85f;
            return (0.112f + 0.01f * b.muscle) * (0.9f + 0.1f * WidthOf(b)) * s - 0.008f * s;
        }

        /// <summary>Back-number and chest-text depth (local to the chest bone) for each body mesh built.</summary>
        public static readonly Dictionary<Mesh, Vector2> TextZ = new Dictionary<Mesh, Vector2>();
        /// <summary>The seat of the pants (model space) of each body mesh built: overalls wear the club name there.</summary>
        public static readonly Dictionary<Mesh, Vector3> SeatOf = new Dictionary<Mesh, Vector3>();
        public static float LastBuildMs;

        /// <summary>One seamless soft body (signed-distance blobs, see PlayerSdf), hair under the cap, face on top.
        /// Falls back to the older lofted body if anything goes wrong.</summary>
        public static Mesh BuildBody(Body b, TeamData t, Vector3[] rp, bool catcher = false)
        {
            var kit = BuildBodyKit(b, t, rp, 0, catcher);
            return kit != null ? FinishBody(kit) : BuildBodyLoft(b, t, rp);
        }

        /// <summary>Grid scale of the far-away body (about a fifth of the vertices).</summary>
        public const float LodCoarse = 2.3f;

        /// <summary>Geometry of a soft body, ready to become a Mesh. Pure maths: safe on a worker thread.</summary>
        public sealed class BodyKit { public MeshKit k; public PlayerSdf.Info info; public Vector3[] rp; }

        public static BodyKit BuildBodyKit(Body b, TeamData t, Vector3[] rp, int lod = 0, bool catcher = false)
        {
            var k = new MeshKit { skin = true, detail = lod == 0 ? 1.6f : 0.85f };
            PlayerSdf.Info info;
            try { info = PlayerSdf.MakeBody(k, b, t, rp, lod == 0 ? 1f : LodCoarse); }
            catch (System.Exception e)
            {
                Debug.LogWarning("BananaBall: soft body build failed, using the lofted body. " + e);
                return null;
            }
            float s = b.height / 1.85f;
            k.outline = 0f;
            foreach (var bp in info.buttons)
            {
                k.bone = bp.y < rp[0].y + 0.24f * s * TorsoK ? 1 : 2;
                k.Sphere(bp + new Vector3(0f, 0f, 0.001f * s), 0.0068f * s, Pal.Gloss(Color.white, 0.6f), 3, 6);
            }
            // costume buttons, clasps and drawstrings
            for (int i = 0; i < info.studs.Count; i++)
            {
                Vector4 sp = info.studs[i];
                Vector3 p = new Vector3(sp.x, sp.y, sp.z);
                k.bone = p.y < rp[0].y + 0.24f * s * TorsoK ? 1 : 2;
                k.Sphere(p + new Vector3(0f, 0f, sp.w * 0.25f), sp.w, info.studC[i], 3, 6);
            }
            k.outline = 1f;
            try { PlayerSdf.Parts(k, b, t, rp, lod == 0 ? 1f : LodCoarse, info); }
            catch (System.Exception e) { Debug.LogWarning("BananaBall: hand/cleat/ear parts failed. " + e); }
            PlayerSdf.MakeHair(k, b, rp, info);
            HeadParts(k, b, t, rp, SkinC(b.skin), info);
            if (catcher)
            {
                try { PlayerSdf.MakeCatcherGear(k, b, t, rp, info); }
                catch (System.Exception e) { Debug.LogWarning("BananaBall: catcher gear build failed. " + e); }
            }
            return new BodyKit { k = k, info = info, rp = rp };
        }

        /// <summary>Main thread: turn a body kit into the skinned Mesh.</summary>
        public static Mesh FinishBody(BodyKit kit)
        {
            var mesh = kit.k.ToMesh("BB_Player", true);
            mesh.bindposes = BindPoses(kit.rp);
            mesh.RecalculateBounds();
            mesh.UploadMeshData(true);   // nothing reads a body back: keep it on the GPU only
            TextZ[mesh] = new Vector2(kit.info.backZ, kit.info.frontZ);
            SeatOf[mesh] = kit.info.seat;
            LastBuildMs = kit.info.ms;
            return mesh;
        }

        public static Mesh BuildBodyLoft(Body b, TeamData t, Vector3[] rp)
        {
            var k = new MeshKit { skin = true, detail = 1.6f };
            float s = b.height / 1.85f, w = WidthOf(b), be = b.belly, mu = b.muscle;
            Vector3 hips = rp[0];
            Color jersey = Cloth(t.jersey), pants = Cloth(t.pants), belt = Pal.Gloss(t.dark, 0.35f), trim = Cloth(t.main), pipe = Cloth(t.accent);
            Color skin = SkinC(b.skin), sock = Cloth(t.main), sockBand = Cloth(Color.Lerp(t.accent, Color.white, t.accent.grayscale < 0.3f ? 0f : 0.2f));
            Color under = Cloth(t.dark.grayscale < 0.2f ? t.main : t.dark);
            bool stripes = t.pinstripes;

            // ---------- torso: crotch to neck
            float y0 = -0.075f, y1 = 0.62f, L = y1 - y0;
            System.Func<float, float> U = y => (y - y0) / L;
            float zk = 0.9f + 0.1f * w;
            var keys = new[]
            {
                new Key(U(-0.075f), 0.118f * w * s, 0.092f * s, 0f, -0.004f * s),
                new Key(U(-0.02f), 0.157f * w * s, 0.112f * zk * s, 0f, -0.012f * s),
                new Key(U(0.035f), 0.163f * w * s, 0.114f * zk * s, 0f, -0.008f * s),
                new Key(U(0.095f), 0.151f * w * s, (0.104f + 0.045f * be) * zk * s, 0f, 0.022f * be * s),
                new Key(U(0.165f), (0.150f + 0.01f * be) * w * s, (0.106f + 0.072f * be) * zk * s, 0f, 0.046f * be * s),
                new Key(U(0.245f), (0.158f + 0.02f * be) * w * s, (0.110f + 0.076f * be) * zk * s, 0f, 0.056f * be * s),
                new Key(U(0.335f), (0.172f + 0.016f * mu) * w * s, (0.116f + 0.036f * be + 0.008f * mu) * zk * s, 0f, (0.012f + 0.016f * be) * s),
                new Key(U(0.415f), (0.186f + 0.022f * mu) * w * s, (0.112f + 0.01f * mu) * zk * s, 0f, 0.006f * s),
                new Key(U(0.47f), (0.176f + 0.022f * mu) * w * s, 0.097f * zk * s, 0f, -0.006f * s),
                new Key(U(0.515f), (0.118f + 0.01f * mu) * w * s, 0.079f * s, 0f, -0.008f * s),
                new Key(U(0.548f), 0.066f * s * (1f + 0.15f * mu), 0.061f * s, 0f, 0f),
                new Key(U(0.62f), 0.053f * s * (1f + 0.12f * mu), 0.055f * s, 0f, 0.008f * s),
            };
            float beltLo = U(0.082f), beltHi = U(0.118f), collarLo = U(0.522f), collarHi = U(0.548f);
            ColFn torsoCol = (u, a) =>
            {
                if (u < beltLo) return Mathf.Abs(Mathf.Abs(a) - Mathf.PI * 0.5f) < 0.06f ? pipe : pants;
                if (u < beltHi) return Mathf.Abs(a) < 0.12f ? Pal.Gloss(new Color(0.75f, 0.72f, 0.6f), 0.8f) : belt;
                if (u < collarLo) return Mathf.Abs(a) < 0.035f && u > beltHi + 0.01f ? pipe : jersey;
                if (u < collarHi) return trim;
                return skin;
            };
            BoneFn torsoBones = (float u, out int b0, out int b1, out float bw) =>
            {
                float y = y0 + u * L;
                if (y < 0.04f) { b0 = 0; b1 = 0; bw = 0f; }
                else if (y < 0.14f) { b0 = 0; b1 = 1; bw = Mathf.SmoothStep(0f, 1f, (y - 0.04f) / 0.10f); }
                else if (y < 0.24f) { b0 = 1; b1 = 1; bw = 0f; }
                else if (y < 0.33f) { b0 = 1; b1 = 2; bw = Mathf.SmoothStep(0f, 1f, (y - 0.24f) / 0.09f); }
                else if (y < 0.52f) { b0 = 2; b1 = 2; bw = 0f; }
                else if (y < 0.565f) { b0 = 2; b1 = 3; bw = Mathf.SmoothStep(0f, 1f, (y - 0.52f) / 0.045f); }
                else { b0 = 3; b1 = 3; bw = 0f; }
            };
            Loft(k, hips + new Vector3(0f, y0 * s * TorsoK, 0f), hips + new Vector3(0f, y1 * s * TorsoK, 0f), keys, 26, 22, torsoCol, torsoBones,
                 new[] { beltLo, beltHi, collarLo, collarHi }, stripes ? 46f : 0f, (u, a) => u < beltLo || (u > beltHi && u < collarLo));

            // buttons down the placket
            k.bone = 2;
            k.outline = 0f;
            for (int i = 0; i < 5; i++)
            {
                float y = 0.16f + i * 0.075f;
                float u = U(y);
                float rz = Spline(keys, u, 1), oz = Spline(keys, u, 3);
                k.bone = y < 0.24f ? 1 : 2;
                k.Sphere(hips + new Vector3(0f, y * s * TorsoK, oz + rz + 0.003f * s), 0.0065f * s, Pal.Gloss(Color.white, 0.6f), 3, 6);
            }
            k.outline = 1f;

            // ---------- legs
            for (int side = 0; side < 2; side++)
            {
                int th = side == 0 ? 14 : 17, kn = th + 1, ft = th + 2;
                float sg = side == 0 ? -1f : 1f;
                Vector3 a = rp[th] + new Vector3(0f, 0.05f * s, 0f), bk = rp[kn], c = rp[ft];
                float Lk = LimbK * s;
                var tk = new[]
                {
                    new Key(0f, (0.092f + 0.01f * be) * w * Lk, 0.098f * Lk, 0f, 0f),
                    new Key(0.35f, (0.084f + 0.006f * mu) * Lk, (0.088f + 0.006f * mu) * Lk, -sg * 0.004f * s, 0.006f * s),
                    new Key(0.8f, 0.066f * Lk, 0.068f * Lk, 0f, 0.004f * s),
                    new Key(1.02f, 0.06f * Lk, 0.062f * Lk, 0f, 0.008f * s),
                };
                float pipeAng = sg * Mathf.PI * 0.5f;
                ColFn tcol = (u, ang) => Mathf.Abs(Mathf.DeltaAngle(ang * Mathf.Rad2Deg, pipeAng * Mathf.Rad2Deg)) < 3.6f ? pipe : pants;
                Loft(k, a, bk + (bk - a).normalized * 0.02f * s, tk, 10, 16, tcol, Blend(0, th, 0f, 0.22f), null, stripes ? 30f : 0f, (u, ang) => true);
                float cut = b.highSocks ? 0.36f : 0.9f;
                var sk = new[]
                {
                    new Key(0f, 0.06f * Lk, 0.062f * Lk, 0f, 0.004f * s),
                    new Key(0.3f, (0.059f + 0.006f * mu) * Lk, (0.064f + 0.006f * mu) * Lk, 0f, -0.006f * s),
                    new Key(0.75f, 0.046f * Lk, 0.048f * Lk, 0f, 0f),
                    new Key(1.0f, 0.042f * Lk, 0.046f * Lk, 0f, 0.004f * s),
                };
                ColFn scol = (u, ang) =>
                {
                    if (u < cut) return Mathf.Abs(Mathf.DeltaAngle(ang * Mathf.Rad2Deg, pipeAng * Mathf.Rad2Deg)) < 4f ? pipe : pants;
                    if (b.highSocks && ((u > 0.5f && u < 0.56f) || (u > 0.62f && u < 0.68f))) return sockBand;
                    return sock;
                };
                Loft(k, bk, c + new Vector3(0f, -0.02f * s, 0f), sk, 10, 16, scol, Blend(th, kn, 0f, 0.16f), b.highSocks ? new[] { cut, 0.5f, 0.56f, 0.62f, 0.68f } : new[] { cut }, stripes ? 24f : 0f, (u, ang) => u < cut);
                if (!b.highSocks)
                {
                    // pant cuff bulge over the shoe
                    k.bone = kn;
                    k.Ellipsoid(Vector3.Lerp(bk, c, 0.9f), new Vector3(0.05f, 0.03f, 0.055f) * Lk, Quaternion.identity, pants, 4, 12);
                }
                k.bone = th;
                k.Sphere(Vector3.Lerp(a, bk, 0.98f), 0.064f * Lk, pants, 6, 10);
                // cleat
                k.bone = ft;
                Color shoe = Pal.Gloss(Pal.Cleat, 0.55f), sole = Pal.Gloss(new Color(0.06f, 0.06f, 0.07f), 0.3f), swoosh = Cloth(t.main);
                float Fk = FootK * s;
                k.Ellipsoid(c + new Vector3(0f, -0.035f, 0.05f) * Fk, new Vector3(0.052f, 0.044f, 0.115f) * Fk, Quaternion.Euler(4f, 0f, 0f), shoe, 7, 12);
                k.Ellipsoid(c + new Vector3(0f, -0.068f, 0.053f) * Fk, new Vector3(0.054f, 0.014f, 0.12f) * Fk, Quaternion.identity, sole, 3, 12);
                k.outline = 0f;
                k.Ellipsoid(c + new Vector3(sg * 0.05f, -0.03f, 0.045f) * Fk, new Vector3(0.004f, 0.014f, 0.065f) * Fk, Quaternion.Euler(-14f, 0f, 0f), swoosh, 3, 8);
                k.Ellipsoid(c + new Vector3(0f, -0.004f, 0.035f) * Fk, new Vector3(0.038f, 0.012f, 0.046f) * Fk, Quaternion.Euler(-20f, 0f, 0f), Pal.Gloss(Color.white, 0.2f), 3, 8);
                k.outline = 1f;
            }

            // ---------- arms
            for (int side = 0; side < 2; side++)
            {
                int sh = side == 0 ? 8 : 11, el = sh + 1, ha = sh + 2;
                float sg = side == 0 ? -1f : 1f;
                // the sleeve root is tucked into the torso and capped with a dome of the same size, so the shoulder reads as one
                // rounded jersey shape instead of a ball stuck on the side
                Vector3 a = rp[sh] + new Vector3(-sg * 0.03f, 0.03f, 0f) * s, bE = rp[el], c = rp[ha];
                float m = mu, Lk = LimbK * s;
                float rTop = (0.062f + 0.016f * m) * Lk;
                var uk = new[]
                {
                    new Key(0f, rTop * 0.94f, rTop * 0.9f, 0f, 0f),
                    new Key(0.16f, rTop, rTop * 0.96f, 0f, 0.002f * s),
                    new Key(0.4f, (0.055f + 0.022f * m) * Lk, (0.053f + 0.024f * m) * Lk, 0f, 0.004f * s),
                    new Key(1.02f, 0.042f * Lk, 0.041f * Lk, 0f, 0f),
                };
                Color lower = b.sleeves ? under : skin;
                ColFn ucol = (u, ang) => u < 0.42f ? jersey : (u < 0.47f && !b.sleeves ? trim : lower);
                Loft(k, a, bE, uk, 10, 16, ucol, Blend(2, sh, 0.05f, 0.3f), new[] { 0.42f, 0.47f }, stripes ? 18f : 0f, (u, ang) => u < 0.42f);
                k.bone = sh;
                k.Ellipsoid(a, new Vector3(rTop * 0.97f, rTop * 0.78f, rTop * 0.92f), Quaternion.identity, jersey, 8, 14);
                var fk = new[]
                {
                    new Key(0f, 0.042f * Lk, 0.041f * Lk, 0f, 0f),
                    new Key(0.28f, (0.042f + 0.014f * m) * Lk, (0.044f + 0.012f * m) * Lk, 0f, 0.003f * s),
                    new Key(1.0f, 0.032f * Lk, 0.027f * Lk, 0f, 0f),
                };
                Color wrist = Cloth(t.main);
                bool band = (b.gear == Gear.Headband) || (b.muscle > 0.6f);
                ColFn fcol = (u, ang) => band && u > 0.84f ? wrist : (b.sleeves && u < 0.9f ? under : skin);
                Loft(k, bE, c, fk, 8, 12, fcol, Blend(sh, el, 0f, 0.14f), band ? new[] { 0.84f } : (b.sleeves ? new[] { 0.9f } : null), 0f, null);
                k.bone = el;
                k.Sphere(bE, 0.043f * Lk, b.sleeves ? under : skin, 5, 10);
                // hand: a chubby palm, curled fingers and thumb (mitten-like at this size)
                k.bone = ha;
                float Hk = HandK * s;
                k.Ellipsoid(c + new Vector3(0f, -0.045f, 0.004f) * Hk, new Vector3(0.025f, 0.045f, 0.018f) * Hk, Quaternion.identity, skin, 6, 10);
                k.Ellipsoid(c + new Vector3(0f, -0.086f, 0.012f) * Hk, new Vector3(0.024f, 0.026f, 0.019f) * Hk, Quaternion.Euler(20f, 0f, 0f), skin, 5, 8);
                k.Ellipsoid(c + new Vector3(sg * -0.024f, -0.04f, 0.016f) * Hk, new Vector3(0.011f, 0.028f, 0.012f) * Hk, Quaternion.Euler(10f, 0f, sg * 25f), skin, 4, 6);
            }

            HeadParts(k, b, t, rp, skin);

            var mesh = k.ToMesh("BB_Player", true);
            var bp = new Matrix4x4[BoneCount];
            for (int i = 0; i < 20; i++) bp[i] = Matrix4x4.Translate(-rp[i]);
            for (int h = 0; h < Helpers; h++) bp[20 + h] = Matrix4x4.Translate(-rp[HelperChild[h]]);
            mesh.bindposes = bp;
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>The cartoon mouth on the jaw bone: a D (flat top with the corners turned up, round bottom). The jaw scales it from the
        /// top edge, so shut it is a thin smile and open it drops like a real jaw; upper teeth run along the top. Every vertex is
        /// pressed onto the face, so the mouth follows the curve of the cheeks.</summary>
        static void Mouth(MeshKit k, Vector3 pivot, float hs, PlayerSdf.Info sdf)
        {
            float w = 0.025f * hs, hgt = 0.018f * hs, th = 0.0042f * hs, smile = 0.0035f * hs;
            Vector3 top = pivot + new Vector3(0f, 0.0005f * hs, 0f);
            System.Func<float, float> TopY = x => smile * (x / w) * (x / w);
            var pts = new List<Vector2>();
            const int N = 16;
            for (int i = 0; i <= N; i++)   // top edge, left to right
            {
                float x = Mathf.Lerp(-w, w, i / (float)N);
                pts.Add(new Vector2(x, TopY(x)));
            }
            for (int i = 1; i < N; i++)    // bottom arc, right to left
            {
                float a = Mathf.PI * i / N;
                float x = w * Mathf.Cos(a);
                pts.Add(new Vector2(x, TopY(x) * 0.6f - hgt * Mathf.Pow(Mathf.Sin(a), 0.85f)));
            }
            Prism(k, pts, top, th, sdf, Pal.Gloss(new Color(0.34f, 0.07f, 0.1f), 0.45f), 0.35f, 0f);
            // upper teeth: a strip under the top edge, a hair in front of the mouth
            var tp = new List<Vector2>();
            float tw = w * 0.78f, tt = 0.0046f * hs;
            for (int i = 0; i <= 10; i++) { float x = Mathf.Lerp(-tw, tw, i / 10f); tp.Add(new Vector2(x, TopY(x) - 0.0004f * hs)); }
            for (int i = 10; i >= 0; i--) { float x = Mathf.Lerp(-tw, tw, i / 10f); tp.Add(new Vector2(x, TopY(x) - tt * (1f - 0.35f * (x / tw) * (x / tw)))); }
            Prism(k, tp, top, th * 0.6f, sdf, Pal.Gloss(new Color(0.98f, 0.97f, 0.94f), 0.7f), 0f, 0.0012f * hs);
        }

        /// <summary>A thin closed slab with the given outline (in the face's x/y around 'origin'), pressed onto the head's surface:
        /// front face, back face inside the head and side walls, so the ink hull closes around it.</summary>
        static void Prism(MeshKit k, List<Vector2> outline, Vector3 origin, float thick, PlayerSdf.Info sdf, Color c, float ink, float lift)
        {
            int n = outline.Count;
            var front = new Vector3[n];
            var back = new Vector3[n];
            Vector2 cen = Vector2.zero;
            foreach (var p in outline) cen += p;
            cen /= n;
            Vector3 Surf(Vector2 p)
            {
                Vector3 from = origin + new Vector3(p.x, p.y, 0.12f);
                return sdf.shape.March(from, Vector3.back, 0.4f);
            }
            for (int i = 0; i < n; i++)
            {
                Vector3 sp = Surf(outline[i]);
                front[i] = sp + new Vector3(0f, 0f, thick * 0.45f + lift);
                back[i] = sp - new Vector3(0f, 0f, thick * 0.9f);
            }
            Vector3 fc = Surf(cen) + new Vector3(0f, 0f, thick * 0.45f + lift);
            Vector3 bc = Surf(cen) - new Vector3(0f, 0f, thick * 0.9f);
            float saveOl = k.outline;
            k.outline = ink;
            void Tri(int a, int b, int d, Vector3 want)
            {
                Vector3 pa = k.V[a], pb = k.V[b], pd = k.V[d];
                if (Vector3.Dot(Vector3.Cross(pb - pa, pd - pa), want) >= 0f) { k.T.Add(a); k.T.Add(b); k.T.Add(d); }
                else { k.T.Add(a); k.T.Add(d); k.T.Add(b); }
            }
            int f0 = k.Vert(fc, Vector3.forward, c, MeshKit.Plain);
            int fs = k.V.Count;
            for (int i = 0; i < n; i++) k.Vert(front[i], Vector3.forward, c, MeshKit.Plain);
            for (int i = 0; i < n; i++) Tri(f0, fs + i, fs + (i + 1) % n, Vector3.forward);
            int b0 = k.Vert(bc, Vector3.back, c, MeshKit.Plain);
            int bs = k.V.Count;
            for (int i = 0; i < n; i++) k.Vert(back[i], Vector3.back, c, MeshKit.Plain);
            for (int i = 0; i < n; i++) Tri(b0, bs + i, bs + (i + 1) % n, Vector3.back);
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                Vector2 e = outline[j] - outline[i];
                Vector2 mid = (outline[i] + outline[j]) * 0.5f;
                Vector3 nrm = new Vector3(e.y, -e.x, 0f).normalized;
                if (Vector2.Dot(new Vector2(nrm.x, nrm.y), mid - cen) < 0f) nrm = -nrm;
                int a = k.Vert(front[i], nrm, c, MeshKit.Plain), bb = k.Vert(front[j], nrm, c, MeshKit.Plain);
                int cc = k.Vert(back[j], nrm, c, MeshKit.Plain), d = k.Vert(back[i], nrm, c, MeshKit.Plain);
                Tri(a, bb, cc, nrm);
                Tri(a, cc, d, nrm);
            }
            k.outline = saveOl;
        }

        static void HeadParts(MeshKit k, Body b, TeamData t, Vector3[] rp, Color skin, PlayerSdf.Info sdf = null)
        {
            float s = b.height / 1.85f, hs = s * HeadOf(b);
            Vector3 H = rp[4];
            System.Func<float, float, float, Vector3> P = (x, y, z) => H + new Vector3(x, y, z) * hs;
            // shift that moves a feature from the old skull onto the soft head's surface
            System.Func<Vector3, Vector3> Off = q => sdf == null ? Vector3.zero : PlayerSdf.Snap(sdf, q) - q;
            Color hair = HairC(b.hairColor);
            Color darkSkin = Pal.Gloss(Color.Lerp(b.skin, Color.black, 0.12f), 0.3f);
            k.bone = 4;
            if (sdf == null) {
            // cartoon head: a round cranium over chubby jowls, a button nose, big shiny eyes, stubby brows, pink cheeks
            k.Ellipsoid(P(0f, 0.1f, 0f), new Vector3(0.085f, 0.092f, 0.088f) * hs, Quaternion.identity, skin, 12, 16);
            k.Ellipsoid(P(0f, 0.062f, 0.012f), new Vector3(0.08f, 0.06f, 0.08f) * hs, Quaternion.identity, skin, 10, 14);
            float nz = b.nose * 0.85f;
            k.outline = 0.55f;
            k.Sphere(P(0f, 0.066f, 0.094f + 0.006f * (nz - 1f)), 0.0135f * hs * nz, skin, 6, 8);
            // ears
            for (int e = -1; e <= 1; e += 2)
            {
                k.outline = 0.8f;
                k.Ellipsoid(P(e * 0.086f, 0.084f, -0.004f), new Vector3(0.012f, 0.026f, 0.02f) * hs * b.ears, Quaternion.Euler(0f, e * 18f, 0f), skin, 5, 8);
                k.outline = 0f;
                k.Ellipsoid(P(e * 0.089f, 0.084f, -0.001f), new Vector3(0.005f, 0.016f, 0.011f) * hs * b.ears, Quaternion.Euler(0f, e * 18f, 0f), darkSkin, 3, 6);
            }
            // rosy cheeks
            k.outline = 0f;
            Color blush = Pal.Gloss(Color.Lerp(b.skin, new Color(1f, 0.42f, 0.45f), 0.45f), 0.2f);
            for (int e = -1; e <= 1; e += 2)
                k.Ellipsoid(P(e * 0.05f, 0.056f, 0.071f), new Vector3(0.016f, 0.009f, 0.008f) * hs, Quaternion.Euler(0f, e * 32f, 0f), blush, 4, 8);
            }
            // eyebrows: short and thick, they carry the expression
            Color browC = HairC(Color.Lerp(b.hairColor, Color.black, 0.3f));
            float bw = b.brow;
            k.outline = 0.3f;
            for (int e = -1; e <= 1 && !b.ape; e += 2)
                k.Ellipsoid(P(e * 0.034f, 0.121f, 0.079f) + Off(P(e * 0.034f, 0.121f, 0.079f)), new Vector3(0.017f * Mathf.Lerp(1f, bw, 0.5f), 0.0062f * Mathf.Lerp(1f, bw, 0.7f), 0.007f) * hs, Quaternion.Euler(-12f, e * 14f, e * -10f), browC, 4, 8);
            // eye-black
            k.outline = 0f;
            if (b.gear == Gear.EyeBlack)
                for (int e = -1; e <= 1; e += 2)
                    k.Ellipsoid(P(e * 0.036f, 0.066f, 0.078f) + Off(P(e * 0.036f, 0.066f, 0.078f)), new Vector3(0.016f, 0.005f, 0.006f) * hs, Quaternion.Euler(0f, e * 22f, 0f), Pal.Gloss(Color.black, 0.3f), 3, 6);
            // eyes (on the eye bones, so they can look around): tall white ovals, big dark irises, a shine
            for (int e = 0; e < 2; e++)
            {
                k.bone = 6 + e;
                Vector3 ep = rp[6 + e] + Off(rp[6 + e]);
                k.outline = 0.45f;
                k.Ellipsoid(ep, new Vector3(0.0165f, 0.0205f, 0.0095f) * hs, Quaternion.identity, Pal.Gloss(new Color(0.99f, 0.98f, 0.96f), 0.8f), 8, 12);
                k.outline = 0f;
                k.Ellipsoid(ep + new Vector3(0f, 0.0128f, 0.0034f) * hs, new Vector3(0.0176f, 0.0062f, 0.0082f) * hs, Quaternion.Euler(-10f, 0f, 0f), Pal.Gloss(new Color(0.16f, 0.09f, 0.07f), 0.4f), 5, 10);
                k.Ellipsoid(ep + new Vector3(0f, -0.002f, 0.0062f) * hs, new Vector3(0.0105f, 0.0135f, 0.0055f) * hs, Quaternion.identity, Pal.Gloss(new Color(0.12f, 0.08f, 0.06f), 0.85f), 6, 10);
                k.Sphere(ep + new Vector3(0.0035f, 0.0045f, 0.0112f) * hs, 0.0032f * hs, Pal.Gloss(Color.white, 0.9f), 3, 6);
            }
            // mouth on the jaw bone (scales open): a little smile (a gorilla's is wide and dark, with nostrils above)
            k.bone = 5;
            Vector3 jp = rp[5] + Off(rp[5] + new Vector3(0f, -0.002f, 0.031f) * hs);
            k.outline = 0.35f;
            if (b.ape)
            {
                k.Ellipsoid(jp + new Vector3(0f, -0.002f, 0.031f) * hs, new Vector3(0.034f, 0.0075f, 0.009f) * hs, Quaternion.identity, Pal.Gloss(new Color(0.13f, 0.08f, 0.09f), 0.4f), 4, 12);
                k.outline = 0f;
                k.Ellipsoid(jp + new Vector3(0f, 0.0035f, 0.031f) * hs, new Vector3(0.024f, 0.0035f, 0.007f) * hs, Quaternion.identity, Pal.Gloss(new Color(0.95f, 0.93f, 0.85f), 0.6f), 3, 10);
                k.bone = 4;
                for (int e = -1; e <= 1; e += 2)
                {
                    Vector3 nn = P(e * 0.016f, 0.05f, 0.13f);
                    k.Ellipsoid(nn + Off(nn), new Vector3(0.012f, 0.0085f, 0.006f) * hs, Quaternion.Euler(-10f, e * -25f, e * 20f), Pal.Gloss(new Color(0.12f, 0.1f, 0.11f), 0.3f), 3, 8);
                }
            }
            else if (sdf != null)
            {
                Mouth(k, rp[5], hs, sdf);
            }
            else
            {
                k.Ellipsoid(jp + new Vector3(0f, -0.002f, 0.031f) * hs, new Vector3(0.022f, 0.0065f, 0.0075f) * hs, Quaternion.identity, Pal.Gloss(new Color(0.42f, 0.1f, 0.12f), 0.5f), 4, 10);
                k.outline = 0f;
                k.Ellipsoid(jp + new Vector3(0f, 0.0025f, 0.029f) * hs, new Vector3(0.016f, 0.0035f, 0.0065f) * hs, Quaternion.identity, Pal.Gloss(Color.white, 0.6f), 3, 8);
            }
            k.bone = 4;
            k.outline = 1f;

            // hair (what shows below a cap): the soft body builds its own
            if (sdf == null)
            switch (b.hair)
            {
                case Hair.Short:
                    k.Ellipsoid(P(0f, 0.107f, -0.01f), new Vector3(0.09f, 0.093f, 0.092f) * hs, Quaternion.Euler(-8f, 0f, 0f), hair, 8, 14);
                    break;
                case Hair.Buzz:
                    k.Ellipsoid(P(0f, 0.104f, -0.006f), new Vector3(0.087f, 0.094f, 0.09f) * hs, Quaternion.Euler(-8f, 0f, 0f), Pal.Gloss(Color.Lerp(b.hairColor, b.skin, 0.4f), 0.2f), 8, 14);
                    break;
                case Hair.Mullet:
                    k.Ellipsoid(P(0f, 0.107f, -0.01f), new Vector3(0.09f, 0.093f, 0.092f) * hs, Quaternion.Euler(-8f, 0f, 0f), hair, 8, 14);
                    k.Ellipsoid(P(0f, 0.03f, -0.074f), new Vector3(0.074f, 0.09f, 0.036f) * hs, Quaternion.Euler(12f, 0f, 0f), hair, 8, 12);
                    break;
                case Hair.Afro:
                    k.Ellipsoid(P(0f, 0.118f, -0.012f), new Vector3(0.13f, 0.114f, 0.126f) * hs, Quaternion.identity, Pal.Gloss(b.hairColor, 0.08f), 10, 16);
                    break;
                case Hair.Mohawk:
                    k.Ellipsoid(P(0f, 0.103f, -0.006f), new Vector3(0.087f, 0.093f, 0.09f) * hs, Quaternion.Euler(-8f, 0f, 0f), Pal.Gloss(Color.Lerp(b.hairColor, b.skin, 0.55f), 0.2f), 8, 14);
                    for (int i = 0; i < 6; i++)
                    {
                        float a = Mathf.Lerp(-40f, 120f, i / 5f) * Mathf.Deg2Rad;
                        Vector3 c = P(0f, 0.1f + Mathf.Cos(a) * 0.1f, Mathf.Sin(a) * -0.1f);
                        k.Ellipsoid(c, new Vector3(0.012f, 0.05f, 0.02f) * hs, Quaternion.Euler(-a * Mathf.Rad2Deg, 0f, 0f), Pal.Gloss(t.main, 0.2f), 5, 8);
                    }
                    break;
                case Hair.Long:
                    k.Ellipsoid(P(0f, 0.107f, -0.01f), new Vector3(0.092f, 0.095f, 0.094f) * hs, Quaternion.Euler(-8f, 0f, 0f), hair, 8, 14);
                    k.Ellipsoid(P(0f, -0.02f, -0.07f), new Vector3(0.085f, 0.13f, 0.04f) * hs, Quaternion.Euler(8f, 0f, 0f), hair, 8, 12);
                    break;
                case Hair.Ponytail:
                    k.Ellipsoid(P(0f, 0.107f, -0.01f), new Vector3(0.09f, 0.093f, 0.092f) * hs, Quaternion.Euler(-8f, 0f, 0f), hair, 8, 14);
                    k.Ellipsoid(P(0f, 0.03f, -0.115f), new Vector3(0.024f, 0.08f, 0.024f) * hs, Quaternion.Euler(30f, 0f, 0f), hair, 6, 8);
                    break;
            }
            if (b.gear == Gear.Headband)
            {
                k.outline = 0.5f;
                k.Ellipsoid(P(0f, 0.13f, -0.004f), new Vector3(0.085f, 0.014f, 0.1f) * hs * (sdf != null ? 1.09f : 1f), Quaternion.identity, Cloth(Color.white), 3, 16);
                k.outline = 1f;
            }

            // facial hair (stubble is a tint, not a shape); the soft body grows its own
            Color bc = HairC(Color.Lerp(b.hairColor, Color.black, 0.1f));
            Vector3 fo = Off(P(0f, 0.046f, 0.097f));
            System.Func<float, float, float, Vector3> P0 = P;
            P = (x, y, z) => P0(x, y, z) + fo;
            k.outline = b.beard == Beard.Stubble ? 0f : 0.7f;
            if (sdf != null) PlayerSdf.MakeBeard(k, b, rp, sdf);
            else
            switch (b.beard)
            {
                case Beard.Stubble:
                    break;   // a stubble shell reads as a muzzle on the cartoon face: clean-shaven instead
                case Beard.Mustache:
                    k.Ellipsoid(P(0f, 0.046f, 0.097f), new Vector3(0.026f, 0.008f, 0.009f) * hs, Quaternion.identity, bc, 4, 8);
                    break;
                case Beard.Handlebar:
                    k.Ellipsoid(P(0f, 0.046f, 0.098f), new Vector3(0.03f, 0.008f, 0.01f) * hs, Quaternion.identity, bc, 4, 8);
                    for (int e = -1; e <= 1; e += 2)
                    {
                        int ee = e;
                        k.Tube(u => P(ee * (0.028f + 0.03f * u), 0.046f + 0.03f * u * u, 0.094f - 0.012f * u) + new Vector3(ee * Mathf.Sin(u * 5f) * 0.006f, Mathf.Sin(u * 7f) * 0.004f, 0f) * hs,
                               u => (0.0075f - 0.005f * u) * hs, 0f, 1f, 6, 6, bc);
                    }
                    break;
                case Beard.Walrus:
                    k.Ellipsoid(P(0f, 0.038f, 0.096f), new Vector3(0.038f, 0.02f, 0.016f) * hs, Quaternion.Euler(10f, 0f, 0f), bc, 6, 10);
                    break;
                case Beard.Goatee:
                    k.Ellipsoid(P(0f, 0.046f, 0.097f), new Vector3(0.024f, 0.007f, 0.009f) * hs, Quaternion.identity, bc, 4, 8);
                    k.Ellipsoid(P(0f, -0.004f, 0.08f), new Vector3(0.018f, 0.03f, 0.016f) * hs, Quaternion.identity, bc, 5, 8);
                    break;
                case Beard.Full:
                    k.Ellipsoid(P(0f, 0.012f, 0.05f), new Vector3(0.072f, 0.06f, 0.07f) * hs, Quaternion.identity, bc, 8, 12);
                    k.Ellipsoid(P(0f, 0.046f, 0.098f), new Vector3(0.028f, 0.008f, 0.01f) * hs, Quaternion.identity, bc, 4, 8);
                    break;
                case Beard.Viking:
                    k.Ellipsoid(P(0f, 0.0f, 0.06f), new Vector3(0.07f, 0.07f, 0.065f) * hs, Quaternion.identity, bc, 8, 12);
                    k.Ellipsoid(P(0f, -0.07f, 0.08f), new Vector3(0.05f, 0.07f, 0.035f) * hs, Quaternion.Euler(-10f, 0f, 0f), bc, 8, 10);
                    k.Ellipsoid(P(0f, 0.046f, 0.098f), new Vector3(0.032f, 0.01f, 0.012f) * hs, Quaternion.identity, bc, 4, 8);
                    for (int e = -1; e <= 1; e += 2)
                    {
                        int ee = e;
                        k.Tube(u => P(ee * 0.025f, -0.08f - 0.13f * u, 0.095f - 0.02f * u), u => 0.011f * hs * (1f - 0.3f * u), 0f, 1f, 6, 6, bc);
                        k.Sphere(P(ee * 0.025f, -0.215f, 0.075f), 0.012f * hs, Cloth(t.main), 3, 6);
                    }
                    break;
                case Beard.Chops:
                    for (int e = -1; e <= 1; e += 2)
                        k.Ellipsoid(P(e * 0.066f, 0.05f, 0.03f), new Vector3(0.018f, 0.05f, 0.04f) * hs, Quaternion.Euler(0f, 0f, e * -8f), bc, 6, 8);
                    break;
            }

            // eyewear: lenses inked lightly, thin frames not at all
            // (a gorilla's eyes sit lower, under his brow ridge)
            float gy = b.ape ? -0.018f : 0f;
            Vector3 go = Off(P0(0f, 0.096f + gy, 0.098f));
            P = (x, y, z) => P0(x, y + gy, z) + go;
            k.outline = 0.5f;
            switch (b.gear)
            {
                case Gear.Shades:
                {
                    Color lens = Pal.Gloss(new Color(0.05f, 0.06f, 0.08f), 0.95f), frame = Pal.Gloss(new Color(0.85f, 0.72f, 0.3f), 0.9f);
                    for (int e = -1; e <= 1; e += 2) k.Ellipsoid(P(e * 0.032f, 0.096f, 0.098f), new Vector3(0.022f, 0.015f, 0.005f) * hs, Quaternion.identity, lens, 4, 10);
                    k.outline = 0f;
                    k.Ellipsoid(P(0f, 0.106f, 0.1f), new Vector3(0.062f, 0.0025f, 0.004f) * hs, Quaternion.identity, frame, 2, 8);
                    break;
                }
                case Gear.Goggles:
                {
                    Color lens = Pal.Gloss(new Color(0.2f, 0.75f, 0.95f), 0.95f), strap = Pal.Gloss(t.main, 0.3f);
                    for (int e = -1; e <= 1; e += 2) k.Ellipsoid(P(e * 0.034f, 0.097f, 0.096f), new Vector3(0.024f, 0.02f, 0.012f) * hs, Quaternion.identity, lens, 5, 10);
                    k.outline = 0.3f;
                    k.Ellipsoid(P(0f, 0.1f, 0f), new Vector3(0.088f, 0.008f, 0.102f) * hs, Quaternion.identity, strap, 2, 16);
                    break;
                }
                case Gear.Monocle:
                {
                    Color gold = Pal.Gloss(new Color(0.9f, 0.75f, 0.3f), 0.92f);
                    Vector3 c = P(0.033f, 0.096f, 0.095f);
                    k.outline = 0.25f;
                    k.Tube(u => c + new Vector3(Mathf.Cos(u * Mathf.PI * 2f) * 0.018f, Mathf.Sin(u * Mathf.PI * 2f) * 0.018f, 0f) * hs, u => 0.0022f * hs, 0f, 1f, 16, 4, gold);
                    k.Tube(u => Vector3.Lerp(c + new Vector3(0.014f, -0.012f, 0f) * hs, P(0.075f, -0.03f, 0.04f), u) + Vector3.down * Mathf.Sin(u * Mathf.PI) * 0.02f * hs, u => 0.0012f * hs, 0f, 1f, 10, 3, gold);
                    break;
                }
            }
            k.outline = 1f;
        }

        /// <summary>Curve a flat brim down at its sides like a real cap (vertices from 'start' on).</summary>
        static void BendBrim(MeshKit k, int start, float halfWidth, float droop)
        {
            for (int i = start; i < k.V.Count; i++)
            {
                Vector3 v = k.V[i];
                float x = v.x / halfWidth;
                v.y -= droop * x * x;
                k.V[i] = v;
                Vector3 n = k.N[i];
                n.x += 2f * droop * x / halfWidth * Mathf.Sign(n.y) * 0.5f;
                k.N[i] = n.normalized;
            }
        }

        static void Dome(MeshKit k, Vector3 c, Vector3 r, Color col, float latMax, int lat, int lon, float backDrop = 0f)
        {
            int s0 = k.V.Count;
            for (int i = 0; i <= lat; i++)
            {
                float th = latMax * Mathf.Deg2Rad * i / lat;
                for (int j = 0; j <= lon; j++)
                {
                    float ph = Mathf.PI * 2f * j / lon;
                    float back = Mathf.Max(0f, -Mathf.Sin(ph));   // 1 at the back (-z)
                    float thj = th * (1f + backDrop * back * i / lat);
                    Vector3 u = new Vector3(Mathf.Sin(thj) * Mathf.Cos(ph), Mathf.Cos(thj), Mathf.Sin(thj) * Mathf.Sin(ph));
                    Vector3 p = new Vector3(u.x * r.x, u.y * r.y, u.z * r.z);
                    Vector3 n = new Vector3(u.x / r.x, u.y / r.y, u.z / r.z).normalized;
                    k.Vert(c + p, n, col, MeshKit.Plain);
                }
            }
            // same winding as MeshKit.Ellipsoid: outward-facing (an inward dome is culled and the ink hull covers it)
            for (int i = 0; i < lat; i++)
                for (int j = 0; j < lon; j++)
                {
                    int a = s0 + i * (lon + 1) + j, b = a + lon + 1;
                    k.T.Add(a); k.T.Add(a + 1); k.T.Add(b);
                    k.T.Add(a + 1); k.T.Add(b + 1); k.T.Add(b);
                }
        }

        public static Mesh Cap(Body b, TeamData t)
        {
            string key = "cap" + t.abbr + b.head.ToString("F2") + b.height.ToString("F2") + (int)b.hair + (b.hair == Hair.Mohawk ? ColorUtility.ToHtmlStringRGB(b.hairColor) : "");
            if (CacheTry(key, out Mesh m)) return m;
            var k = new MeshKit { detail = 1.6f };
            float hs = b.height / 1.85f * HeadOf(b) * (b.hair == Hair.Afro ? 1.22f : 1f);
            Color cap = Pal.Gloss(t.main, 0.18f), brim = Pal.Gloss(t.dark.grayscale < 0.15f ? t.main : t.dark, 0.2f);
            Dome(k, new Vector3(0f, 0.122f, -0.004f) * hs, new Vector3(0.087f, 0.085f, 0.1f) * hs, cap, 92f, 12, 32);
            int brim0 = k.V.Count;
            k.Ellipsoid(new Vector3(0f, 0.128f, 0.112f) * hs, new Vector3(0.082f, 0.008f, 0.072f) * hs, Quaternion.Euler(-12f, 0f, 0f), brim, 3, 18);
            BendBrim(k, brim0, 0.082f * hs, 0.024f * hs);
            k.outline = 0.4f;
            k.Sphere(new Vector3(0f, 0.207f, -0.004f) * hs, 0.008f * hs, brim, 3, 8);
            k.outline = 1f;
            if (b.hair == Hair.Mohawk)
                PlayerSdf.Crest(k, new Vector3(0f, 0.122f, -0.004f) * hs, new Vector3(0.087f, 0.085f, 0.1f) * hs, hs, b.hairColor, 8f, 118f);
            return CacheSet(key, k.ToMesh("BB_Cap", true));
        }

        /// <summary>What a player wears on his head in the field: the club cap, or his costume hat (bucket, straw, cowboy, fire helmet,
        /// bowler). badge: where a fire helmet's front shield is (head space), for his number; zero for other hats.</summary>
        public static Mesh Headwear(Body b, TeamData t, out Vector3 badge)
        {
            badge = Vector3.zero;
            if (b.hat == Hat.Cap) return Cap(b, t);
            float hs = b.height / 1.85f * HeadOf(b) * (b.hair == Hair.Afro ? 1.22f : 1f);
            string key = "hat" + (int)b.hat + "_" + t.abbr + "_" + b.style + "_" + hs.ToString("F3");
            if (HatBadge.TryGetValue(key, out Vector3 bd)) badge = bd;
            if (CacheTry(key, out Mesh m)) return m;
            var k = new MeshKit { detail = 1.4f };
            PlayerSdf.HatShape(k, b.hat, hs, t, b.style, out badge);
            HatBadge[key] = badge;
            return CacheSet(key, k.ToMesh("BB_Hat", true));
        }

        static readonly Dictionary<string, Vector3> HatBadge = new Dictionary<string, Vector3>();

        public static Mesh Helmet(Body b, TeamData t, bool leftyBatter)
        {
            string key = "helm" + t.abbr + b.head.ToString("F2") + b.height.ToString("F2") + (leftyBatter ? "L" : "R") + (int)b.hair + (b.hair == Hair.Mohawk ? ColorUtility.ToHtmlStringRGB(b.hairColor) : "");
            if (CacheTry(key, out Mesh m)) return m;
            var k = new MeshKit { detail = 1.6f };
            float hs = b.height / 1.85f * HeadOf(b) * (b.hair == Hair.Afro ? 1.2f : 1f);
            Color shell = Pal.Gloss(t.main, 0.88f), pad = Pal.Gloss(t.dark, 0.3f);
            Dome(k, new Vector3(0f, 0.112f, -0.006f) * hs, new Vector3(0.094f, 0.096f, 0.11f) * hs, shell, 100f, 12, 32, 0.25f);
            int brim0 = k.V.Count;
            k.Ellipsoid(new Vector3(0f, 0.122f, 0.108f) * hs, new Vector3(0.084f, 0.008f, 0.044f) * hs, Quaternion.Euler(-14f, 0f, 0f), shell, 3, 18);
            BendBrim(k, brim0, 0.084f * hs, 0.018f * hs);
            // ear flap on the side that faces the pitcher (left ear for a right-handed batter)
            float fx = leftyBatter ? 1f : -1f;
            k.Ellipsoid(new Vector3(fx * 0.084f, 0.07f, 0.012f) * hs, new Vector3(0.02f, 0.05f, 0.052f) * hs, Quaternion.Euler(0f, 0f, fx * 6f), shell, 6, 10);
            k.outline = 0.3f;
            k.Ellipsoid(new Vector3(fx * 0.09f, 0.072f, 0.008f) * hs, new Vector3(0.012f, 0.018f, 0.018f) * hs, Quaternion.identity, pad, 3, 8);
            k.outline = 0f;
            for (int i = 0; i < 3; i++)
                k.Ellipsoid(new Vector3((i - 1) * 0.024f, 0.205f, 0.03f - 0.03f * Mathf.Abs(i - 1)) * hs, new Vector3(0.006f, 0.004f, 0.012f) * hs, Quaternion.identity, pad, 2, 6);
            k.outline = 1f;
            if (b.hair == Hair.Mohawk)
                PlayerSdf.Crest(k, new Vector3(0f, 0.112f, -0.006f) * hs, new Vector3(0.094f, 0.096f, 0.11f) * hs, hs, b.hairColor, 0f, 112f);
            return CacheSet(key, k.ToMesh("BB_Helmet", true));
        }

        /// <summary>Fielding glove on the hand bone (fingers point down the arm in the bind pose).</summary>
        public static Mesh Glove(bool rightHand)
        {
            string key = "glove" + (rightHand ? "R" : "L");
            if (CacheTry(key, out Mesh m)) return m;
            var k = new MeshKit { detail = 1.5f };
            Color lea = Pal.Gloss(Pal.Leather, 0.42f), dark = Pal.Gloss(Pal.LeatherDark, 0.35f), lace = Pal.Gloss(Pal.Lace, 0.3f);
            float sg = rightHand ? -1f : 1f;
            k.Ellipsoid(new Vector3(0f, -0.1f, 0.018f), new Vector3(0.07f, 0.12f, 0.042f), Quaternion.identity, lea, 10, 14);
            k.outline = 0.4f;
            k.Ellipsoid(new Vector3(0f, -0.11f, 0.05f), new Vector3(0.05f, 0.085f, 0.016f), Quaternion.identity, dark, 6, 12);
            k.outline = 1f;
            k.Ellipsoid(new Vector3(sg * 0.07f, -0.075f, 0.02f), new Vector3(0.026f, 0.07f, 0.03f), Quaternion.Euler(0f, 0f, sg * -22f), lea, 6, 10);
            for (int f = 0; f < 4; f++)
                k.Ellipsoid(new Vector3(-sg * 0.045f + sg * f * 0.028f, -0.205f, 0.016f), new Vector3(0.018f, 0.04f, 0.026f), Quaternion.identity, lea, 5, 8);
            k.outline = 0f;
            k.Ellipsoid(new Vector3(sg * 0.035f, -0.19f, 0.042f), new Vector3(0.03f, 0.03f, 0.008f), Quaternion.identity, lace, 3, 8);
            k.outline = 1f;
            k.Ellipsoid(new Vector3(0f, -0.008f, 0f), new Vector3(0.045f, 0.022f, 0.038f), Quaternion.identity, dark, 4, 10);
            for (int i = 0; i < k.V.Count; i++) k.V[i] *= GloveK;
            return CacheSet(key, k.ToMesh("BB_Glove", true));
        }

        /// <summary>Catcher's helmet: a skull cap with ear cups (no brim) and the face mask in front (see PlayerSdf.CatcherMask).</summary>
        public static Mesh CatcherHelmet(Body b, TeamData t, bool far = false)
        {
            string key = "chelm" + t.abbr + b.head.ToString("F2") + b.height.ToString("F2") + (int)b.hair + (b.hair == Hair.Mohawk ? ColorUtility.ToHtmlStringRGB(b.hairColor) : "") + (far ? "F" : "");
            if (CacheTry(key, out Mesh m)) return m;
            var k = new MeshKit { detail = far ? 0.8f : 1.6f };
            float hs0 = b.height / 1.85f * HeadOf(b), hs = hs0 * (b.hair == Hair.Afro ? 1.22f : 1f);
            Color shell = Pal.Gloss(t.main, 0.88f), pad = Pal.Gloss(PlayerSdf.GearPlate(t), 0.3f), metal = Pal.Gloss(new Color(0.13f, 0.14f, 0.17f), 0.72f);
            Vector3 dc = new Vector3(0f, 0.122f, -0.004f) * hs, dr = new Vector3(0.089f, 0.087f, 0.102f) * hs;
            Dome(k, dc, dr, shell, 86f, 12, 32, 0.5f);
            for (int e = -1; e <= 1; e += 2)
                k.Ellipsoid(new Vector3(e * 0.087f, 0.084f, -0.008f) * hs, new Vector3(0.02f, 0.038f, 0.04f) * hs, Quaternion.Euler(0f, e * 10f, 0f), shell, 8, 14);
            PlayerSdf.CatcherMask(k, hs0, metal, pad, far ? 1.7f : 1f);
            if (b.hair == Hair.Mohawk)
                PlayerSdf.Crest(k, dc, dr, hs, b.hairColor, 8f, 118f);
            return CacheSet(key, k.ToMesh("BB_CatcherHelmet", true));
        }

        /// <summary>Catcher's mitt on the hand bone, same frame and pocket point as the fielding glove.</summary>
        public static Mesh Mitt(bool rightHand, bool far = false)
        {
            string key = "mitt" + (rightHand ? "R" : "L") + (far ? "F" : "");
            if (CacheTry(key, out Mesh m)) return m;
            var k = new MeshKit { detail = 1.5f };
            PlayerSdf.Mitt(k, rightHand, far ? 2.0f : 1f);
            for (int i = 0; i < k.V.Count; i++) k.V[i] *= GloveK;
            return CacheSet(key, k.ToMesh("BB_Mitt", true));
        }

        static bool CacheTry(string key, out Mesh m) => (PartCacheRef.TryGetValue(key, out m) && m != null);
        static Mesh CacheSet(string key, Mesh m) { PartCacheRef[key] = m; return m; }
        static readonly Dictionary<string, Mesh> PartCacheRef = new Dictionary<string, Mesh>();
    }
}
