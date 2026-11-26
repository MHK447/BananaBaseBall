using System.Collections.Generic;
using UnityEngine;

namespace BananaBall
{
    /// <summary>What the ragdolls land on: one big box under the whole park and the pitcher's mound. Built on first use.</summary>
    public static class RagdollWorld
    {
        static GameObject root;
        static PhysicsMaterial mat;

        public static PhysicsMaterial Mat { get { Ensure(); return mat; } }

        public static void Ensure()
        {
            if (root != null) return;
            root = new GameObject("RagdollGround");
            mat = new PhysicsMaterial("BB_Ragdoll")
            {
                dynamicFriction = 0.55f,
                staticFriction = 0.7f,
                bounciness = 0.22f,
                frictionCombine = PhysicsMaterialCombine.Average,
                bounceCombine = PhysicsMaterialCombine.Maximum,
            };
            var box = root.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, -1f, 40f);
            box.size = new Vector3(700f, 2f, 700f);
            box.sharedMaterial = mat;
            var mound = new GameObject("Mound");
            mound.transform.SetParent(root.transform, false);
            var mc = mound.AddComponent<MeshCollider>();
            mc.sharedMesh = MoundMesh();
            mc.sharedMaterial = mat;
        }

        /// <summary>The mound's shallow paraboloid (same shape as Geo.GroundY).</summary>
        static Mesh MoundMesh()
        {
            const int rings = 8, segs = 28;
            var v = new List<Vector3>();
            var t = new List<int>();
            Vector3 c = Geo.Rubber;
            v.Add(new Vector3(c.x, Geo.MoundH, c.z));
            for (int i = 1; i <= rings; i++)
            {
                float r = Geo.MoundR * 1.03f * i / rings;
                for (int j = 0; j < segs; j++)
                {
                    float a = Mathf.PI * 2f * j / segs;
                    var p = new Vector3(c.x + Mathf.Sin(a) * r, 0f, c.z + Mathf.Cos(a) * r);
                    p.y = Geo.GroundY(p) - (i == rings ? 0.01f : 0f);
                    v.Add(p);
                }
            }
            for (int j = 0; j < segs; j++) { t.Add(0); t.Add(1 + j); t.Add(1 + (j + 1) % segs); }
            for (int i = 1; i < rings; i++)
            {
                int a0 = 1 + (i - 1) * segs, b0 = 1 + i * segs;
                for (int j = 0; j < segs; j++)
                {
                    int j1 = (j + 1) % segs;
                    t.Add(a0 + j); t.Add(b0 + j); t.Add(b0 + j1);
                    t.Add(a0 + j); t.Add(b0 + j1); t.Add(a0 + j1);
                }
            }
            var m = new Mesh { name = "BB_MoundCollider" };
            m.SetVertices(v);
            m.SetTriangles(t, 0);
            m.RecalculateNormals();
            return m;
        }
    }

    /// <summary>
    /// Physics ragdoll for one ballplayer, put on his bones only while he is limp: 12 bodies (pelvis, belly, chest, head,
    /// upper arms, forearms with hands, thighs, shins with shoes) on character joints with body-like limits. Ragdolls
    /// bounce off the ground and pile on each other but never on themselves.
    /// </summary>
    public sealed class RagdollRig
    {
        // rig bones of the bodies, their parent body and mass (kg)
        static readonly int[] SegBone = { 0, 1, 2, 4, 8, 9, 11, 12, 14, 15, 17, 18 };
        static readonly int[] SegParent = { -1, 0, 1, 2, 2, 4, 2, 6, 0, 8, 0, 10 };
        static readonly float[] SegMass = { 12f, 10f, 14f, 7f, 2.6f, 2.1f, 2.6f, 2.1f, 7.5f, 4.3f, 7.5f, 4.3f };
        public const int SegHead = 3, SegChest = 2, SegHips = 0;

        readonly Transform[] bones;
        readonly Vector3[] rest;
        readonly Body body;
        readonly Rigidbody[] rbs = new Rigidbody[12];
        readonly CharacterJoint[] joints = new CharacterJoint[12];
        readonly List<Collider> cols = new List<Collider>();
        public bool On { get; private set; }

        public RagdollRig(Transform[] bones, Vector3[] rest, Body body)
        {
            this.bones = bones;
            this.rest = rest;
            this.body = body;
        }

        public float HipsSpeed => On && rbs[0] != null ? rbs[0].linearVelocity.magnitude : 0f;

        /// <summary>Go limp from the current pose. vel: launch velocity, spin: angular velocity (rad/s, world) of the whole body;
        /// hitSeg/hitImpulse: an extra knock on one body (a ball to the head).</summary>
        public void Begin(Vector3 vel, Vector3 spin, int hitSeg, Vector3 hitImpulse)
        {
            if (On) return;
            RagdollWorld.Ensure();
            int n = bones.Length;
            var save = new Quaternion[n];
            for (int i = 0; i < n; i++) save[i] = bones[i].localRotation;
            // build in the bind pose so the joint limits read like a body's, then put the pose back
            for (int i = 1; i < n; i++) bones[i].localRotation = Quaternion.identity;
            for (int k = 0; k < 12; k++)
            {
                var go = bones[SegBone[k]].gameObject;
                var rb = go.AddComponent<Rigidbody>();
                rb.mass = SegMass[k] * Mathf.Lerp(0.85f, 1.3f, body.belly * 0.5f + (body.ape ? 0.6f : 0f));
                rb.interpolation = RigidbodyInterpolation.Interpolate;
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
                rb.maxAngularVelocity = 40f;
                rb.maxDepenetrationVelocity = 4f;
                rb.linearDamping = 0.02f;
                rb.angularDamping = 0.35f;
                rb.solverIterations = 14;
                rb.solverVelocityIterations = 6;
                rbs[k] = rb;
                AddColliders(k, go);
            }
            for (int k = 1; k < 12; k++) AddJoint(k);
            for (int i = 1; i < n; i++) bones[i].localRotation = save[i];
            Physics.SyncTransforms();
            for (int a = 0; a < cols.Count; a++)
                for (int b = a + 1; b < cols.Count; b++)
                    Physics.IgnoreCollision(cols[a], cols[b], true);
            On = true;
            Push(vel, spin);
            if (hitSeg >= 0 && hitSeg < 12 && hitImpulse.sqrMagnitude > 0f) rbs[hitSeg].AddForce(hitImpulse, ForceMode.Impulse);
        }

        /// <summary>Add a whole-body launch (and tumble) to a limp body.</summary>
        public void Push(Vector3 vel, Vector3 spin)
        {
            if (!On) return;
            Vector3 c = Vector3.zero;
            float m = 0f;
            for (int k = 0; k < 12; k++) { c += rbs[k].worldCenterOfMass * rbs[k].mass; m += rbs[k].mass; }
            c /= Mathf.Max(m, 1e-3f);
            for (int k = 0; k < 12; k++)
            {
                var rb = rbs[k];
                rb.linearVelocity += vel + Vector3.Cross(spin, rb.worldCenterOfMass - c);
                rb.angularVelocity += spin;
            }
        }

        /// <summary>Cartoon gravity on top of the real one (called from FixedUpdate).</summary>
        public void ExtraGravity(float k)
        {
            if (!On) return;
            Vector3 g = Physics.gravity * k;
            for (int i = 0; i < 12; i++) if (rbs[i] != null) rbs[i].AddForce(g, ForceMode.Acceleration);
        }

        public void End()
        {
            if (!On) return;
            On = false;
            for (int k = 11; k >= 1; k--) if (joints[k] != null) Object.DestroyImmediate(joints[k]);
            foreach (var c in cols) if (c != null) Object.DestroyImmediate(c);
            cols.Clear();
            for (int k = 11; k >= 0; k--) if (rbs[k] != null) Object.DestroyImmediate(rbs[k]);
            // joints drift a hair: put every bone back in its socket
            for (int i = 1; i < bones.Length; i++) bones[i].localPosition = rest[i] - rest[PlayerMesh.Parent[i]];
        }

        void AddColliders(int k, GameObject go)
        {
            float s = body.height / 1.85f, w = PlayerMesh.WidthOf(body), hs = s * PlayerMesh.HeadOf(body), Lk = PlayerMesh.LimbK * s;
            float TK = PlayerMesh.TorsoK, Fk = PlayerMesh.FootK * s, ak = body.ape ? 1.3f : 1f;
            int b = SegBone[k];
            switch (k)
            {
                case 0: Capsule(go, new Vector3(0f, -0.02f * s, -0.01f * s), 0.1f * w * s, 0.34f * w * s, 0); break;
                case 1: Capsule(go, new Vector3(0f, 0.09f * TK * s, 0.025f * body.belly * s), (0.125f + 0.04f * body.belly) * w * s, 0.3f * s, 1); break;
                case 2: Capsule(go, new Vector3(0f, 0.1f * TK * s, -0.01f * s), 0.11f * s * ak, 0.38f * w * s, 0); break;
                case 3: Sphere(go, new Vector3(0f, 0.095f * hs, 0.01f * hs), 0.092f * hs); break;
                case 4:
                case 6:
                {
                    float L = (rest[b + 1] - rest[b]).magnitude, r = 0.058f * Lk * ak;
                    Capsule(go, new Vector3(0f, -L * 0.5f, 0f), r, L + 2f * r, 1);
                    break;
                }
                case 5:
                case 7:
                {
                    float L = (rest[b + 1] - rest[b]).magnitude + 0.09f * PlayerMesh.HandK * s * ak, r = 0.05f * Lk * ak;
                    Capsule(go, new Vector3(0f, -L * 0.5f, 0f), r, L + 2f * r, 1);
                    break;
                }
                case 8:
                case 10:
                {
                    float L = (rest[b + 1] - rest[b]).magnitude, r = 0.08f * Lk;
                    Capsule(go, new Vector3(0f, -L * 0.5f, 0f), r, L + 2f * r, 1);
                    break;
                }
                default:
                {
                    float L = (rest[b + 1] - rest[b]).magnitude, r = 0.062f * Lk;
                    Capsule(go, new Vector3(0f, -L * 0.45f, 0f), r, L, 1);
                    // the big shoe rides with the shin
                    Box(bones[b + 1].gameObject, new Vector3(0f, -0.045f * Fk, 0.06f * Fk), new Vector3(0.11f, 0.09f, 0.27f) * Fk);
                    break;
                }
            }
        }

        void Capsule(GameObject go, Vector3 c, float r, float h, int dir)
        {
            var col = go.AddComponent<CapsuleCollider>();
            col.center = c;
            col.radius = r;
            col.height = Mathf.Max(h, 2f * r);
            col.direction = dir;
            col.sharedMaterial = RagdollWorld.Mat;
            cols.Add(col);
        }

        void Sphere(GameObject go, Vector3 c, float r)
        {
            var col = go.AddComponent<SphereCollider>();
            col.center = c;
            col.radius = r;
            col.sharedMaterial = RagdollWorld.Mat;
            cols.Add(col);
        }

        void Box(GameObject go, Vector3 c, Vector3 size)
        {
            var col = go.AddComponent<BoxCollider>();
            col.center = c;
            col.size = size;
            col.sharedMaterial = RagdollWorld.Mat;
            cols.Add(col);
        }

        void AddJoint(int k)
        {
            var j = bones[SegBone[k]].gameObject.AddComponent<CharacterJoint>();
            j.connectedBody = rbs[SegParent[k]];
            j.enableProjection = true;
            j.projectionDistance = 0.03f;
            j.projectionAngle = 8f;
            j.enablePreprocessing = false;
            Vector3 axis, swing;
            float lo, hi, s1, s2;
            switch (k)
            {
                case 1: case 2: axis = Vector3.up; swing = Vector3.right; lo = -25f; hi = 25f; s1 = 35f; s2 = 25f; break;          // belly, chest
                case 3: axis = Vector3.up; swing = Vector3.right; lo = -60f; hi = 60f; s1 = 45f; s2 = 35f; break;                  // head
                case 4: case 6: axis = Vector3.down; swing = Vector3.right; lo = -70f; hi = 70f; s1 = 120f; s2 = 110f; break;     // shoulders
                case 5: case 7: axis = Vector3.right; swing = Vector3.forward; lo = -145f; hi = 5f; s1 = 10f; s2 = 10f; break;    // elbows bend forward
                case 8: case 10: axis = Vector3.down; swing = Vector3.right; lo = -35f; hi = 35f; s1 = 110f; s2 = 55f; break;     // hips
                default: axis = Vector3.right; swing = Vector3.forward; lo = -5f; hi = 150f; s1 = 6f; s2 = 6f; break;             // knees bend back
            }
            j.axis = axis;
            j.swingAxis = swing;
            j.lowTwistLimit = new SoftJointLimit { limit = lo };
            j.highTwistLimit = new SoftJointLimit { limit = hi };
            j.swing1Limit = new SoftJointLimit { limit = s1 };
            j.swing2Limit = new SoftJointLimit { limit = s2 };
            joints[k] = j;
        }
    }
}
