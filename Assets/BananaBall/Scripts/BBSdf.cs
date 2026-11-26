using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace BananaBall
{
    /// <summary>
    /// Signed-distance modelling for the cartoon players: round cones and ellipsoids melted together with smooth blends,
    /// turned into one seamless, soft mesh with surface nets. Colour regions are cut into the mesh along their exact
    /// borders, so a sleeve, a belt or a sock stripe ends on a clean line instead of a blurry band.
    /// </summary>
    public sealed class SdfShape
    {
        public const float Big = 1e4f;

        public struct Prim
        {
            public int kind;                  // 0 round cone, 1 ellipsoid
            public Vector3 a, b, ba;          // cone axis (ellipsoid: its local y axis, for bone blending)
            public float r1, r2, l2, rr, a2, il2;
            public Vector3 c, r;
            public Quaternion inv;
            public bool rot;
            public float k;                   // smooth blend into its group
            public int label;                 // colour region
            public int bone0, bone1;          // skinning: bone0 -> bone1 along the axis (t0..t1); bone0 = -2: torso, by height
            public float t0, t1;
            public Vector3 bc;                // bounding sphere
            public float br;
        }

        public enum ClipKind { Plane, Ellipsoid }

        public struct Clip
        {
            public ClipKind kind;
            public Vector3 n; public float o;
            public Vector3 c, r; public Quaternion inv;
            public bool subtract;
            public float k;
        }

        /// <summary>Prims of a group melt together in order; each child group melts into its parent but not into its siblings
        /// (so two legs never web together). Clips then cut the group (intersection, or subtraction).</summary>
        public sealed class Group
        {
            public readonly List<int> prims = new List<int>();
            public readonly List<Group> kids = new List<Group>();
            public readonly List<Clip> clips = new List<Clip>();
            public float k;
            /// <summary>&gt; 0: the children also melt into each other with this radius (instead of meeting in a crease).</summary>
            public float kidK;
            internal int[] pa = new int[0];
            internal Group[] ka = new Group[0];
            internal Clip[] ca = new Clip[0];
        }

        readonly List<Prim> list = new List<Prim>();
        public Prim[] P = new Prim[0];
        public readonly Group Root = new Group();
        public float kMax;
        bool dirty = true;

        public Group Branch(Group parent, float k)
        {
            var g = new Group { k = k };
            parent.kids.Add(g);
            dirty = true;
            return g;
        }

        public int Cone(Group g, Vector3 a, Vector3 b, float r1, float r2, float k, int label, int bone0, int bone1 = -1, float t0 = 0f, float t1 = 1f)
        {
            Vector3 ba = b - a;
            float L = ba.magnitude;
            if (L < 1e-4f) { b = a + Vector3.up * 1e-4f; ba = b - a; L = 1e-4f; }
            float maxDr = L * 0.97f;
            if (Mathf.Abs(r1 - r2) > maxDr) { if (r1 > r2) r2 = r1 - maxDr; else r1 = r2 - maxDr; }
            var q = new Prim
            {
                kind = 0, a = a, b = b, ba = ba, r1 = r1, r2 = r2, k = k, label = label,
                bone0 = bone0, bone1 = bone1 < 0 ? bone0 : bone1, t0 = t0, t1 = t1,
                bc = (a + b) * 0.5f, br = L * 0.5f + Mathf.Max(r1, r2),
            };
            q.l2 = Vector3.Dot(ba, ba);
            q.rr = r1 - r2;
            q.a2 = q.l2 - q.rr * q.rr;
            q.il2 = 1f / q.l2;
            return Add(g, q);
        }

        public int Ell(Group g, Vector3 c, Vector3 r, Quaternion q, float k, int label, int bone0, int bone1 = -1, float t0 = 0f, float t1 = 1f)
        {
            r = Vector3.Max(r, Vector3.one * 1e-4f);
            var p = new Prim
            {
                kind = 1, c = c, r = r, inv = Quaternion.Inverse(q), rot = Quaternion.Angle(q, Quaternion.identity) > 0.01f, k = k, label = label,
                bone0 = bone0, bone1 = bone1 < 0 ? bone0 : bone1, t0 = t0, t1 = t1,
                bc = c, br = Mathf.Max(r.x, Mathf.Max(r.y, r.z)),
                a = c - q * new Vector3(0f, r.y, 0f), b = c + q * new Vector3(0f, r.y, 0f),
            };
            p.ba = p.b - p.a;
            p.l2 = Mathf.Max(Vector3.Dot(p.ba, p.ba), 1e-10f);
            p.il2 = 1f / p.l2;
            return Add(g, p);
        }

        int Add(Group g, Prim p)
        {
            list.Add(p);
            g.prims.Add(list.Count - 1);
            dirty = true;
            return list.Count - 1;
        }

        /// <summary>Keep the side of the plane the normal points away from (subtract = keep the other side).</summary>
        public void ClipPlane(Group g, Vector3 n, Vector3 through, float k = 0f)
        {
            n.Normalize();
            g.clips.Add(new Clip { kind = ClipKind.Plane, n = n, o = Vector3.Dot(n, through), k = k });
            dirty = true;
        }

        public void CarveEll(Group g, Vector3 c, Vector3 r, Quaternion q, float k = 0f)
        {
            g.clips.Add(new Clip { kind = ClipKind.Ellipsoid, c = c, r = r, inv = Quaternion.Inverse(q), k = k, subtract = true });
            dirty = true;
        }

        public void Bake()
        {
            if (!dirty) return;
            P = list.ToArray();
            kMax = 0f;
            foreach (var p in P) kMax = Mathf.Max(kMax, p.k);
            BakeG(Root);
            dirty = false;
        }

        void BakeG(Group g)
        {
            g.pa = g.prims.ToArray();
            g.ka = g.kids.ToArray();
            g.ca = g.clips.ToArray();
            kMax = Mathf.Max(kMax, Mathf.Max(g.k, g.kidK));
            foreach (var c in g.ca) kMax = Mathf.Max(kMax, c.k);
            foreach (var kid in g.ka) BakeG(kid);
        }

        public float Eval(Vector3 p, bool[] mask = null) => EvalG(Root, p, mask);

        float EvalG(Group g, Vector3 p, bool[] mask)
        {
            float d = Big;
            int[] pa = g.pa;
            for (int i = 0; i < pa.Length; i++)
            {
                int id = pa[i];
                if (mask != null && !mask[id]) continue;
                d = SMin(d, Dist(ref P[id], p), P[id].k);
            }
            Group[] ka = g.ka;
            if (ka.Length > 0)
            {
                float res = d;
                for (int i = 0; i < ka.Length; i++)
                {
                    float dk = EvalG(ka[i], p, mask);
                    if (dk >= Big) continue;
                    float v = SMin(d, dk, ka[i].k);
                    res = g.kidK > 0f ? SMin(res, v, g.kidK) : (v < res ? v : res);
                }
                d = res;
            }
            if (d < Big)
            {
                Clip[] ca = g.ca;
                for (int i = 0; i < ca.Length; i++)
                {
                    float c = ClipDist(ref ca[i], p);
                    d = ca[i].subtract ? SMax(d, -c, ca[i].k) : SMax(d, c, ca[i].k);
                }
            }
            return d;
        }

        public static float SMin(float a, float b, float k)
        {
            float m = a < b ? a : b;
            if (k <= 0f) return m;
            float h = k - (a > b ? a - b : b - a);
            if (h <= 0f) return m;
            h /= k;
            return m - h * h * k * 0.25f;
        }

        public static float SMax(float a, float b, float k) => -SMin(-a, -b, k);

        static float ClipDist(ref Clip c, Vector3 p)
        {
            if (c.kind == ClipKind.Plane) return c.n.x * p.x + c.n.y * p.y + c.n.z * p.z - c.o;
            return EllD(c.inv * (p - c.c), c.r);
        }

        static float EllD(Vector3 l, Vector3 r)
        {
            float x = l.x / r.x, y = l.y / r.y, z = l.z / r.z;
            float k0 = Mathf.Sqrt(x * x + y * y + z * z);
            x /= r.x; y /= r.y; z /= r.z;
            float k1 = Mathf.Sqrt(x * x + y * y + z * z);
            if (k1 < 1e-12f) return -Mathf.Min(r.x, Mathf.Min(r.y, r.z));
            return k0 * (k0 - 1f) / k1;
        }

        /// <summary>Distance to one primitive (round cone: exact; ellipsoid: a close bound).</summary>
        public static float Dist(ref Prim q, Vector3 p)
        {
            if (q.kind == 0)
            {
                float pax = p.x - q.a.x, pay = p.y - q.a.y, paz = p.z - q.a.z;
                float y = pax * q.ba.x + pay * q.ba.y + paz * q.ba.z;
                float z = y - q.l2;
                float xx = pax * q.l2 - q.ba.x * y, xy = pay * q.l2 - q.ba.y * y, xz = paz * q.l2 - q.ba.z * y;
                float x2 = xx * xx + xy * xy + xz * xz;
                float y2 = y * y * q.l2, z2 = z * z * q.l2;
                float kk = (q.rr >= 0f ? 1f : -1f) * q.rr * q.rr * x2;
                if ((z >= 0f ? 1f : -1f) * q.a2 * z2 > kk) return Mathf.Sqrt(x2 + z2) * q.il2 - q.r2;
                if ((y >= 0f ? 1f : -1f) * q.a2 * y2 < kk) return Mathf.Sqrt(x2 + y2) * q.il2 - q.r1;
                return (Mathf.Sqrt(x2 * q.a2 * q.il2) + y * q.rr) * q.il2 - q.r1;
            }
            Vector3 l = new Vector3(p.x - q.c.x, p.y - q.c.y, p.z - q.c.z);
            if (q.rot) l = q.inv * l;
            return EllD(l, q.r);
        }

        /// <summary>Colour region of the nearest primitive.</summary>
        public int Label(Vector3 p, bool[] mask)
        {
            float best = Big;
            int lab = 0;
            for (int i = 0; i < P.Length; i++)
            {
                if (mask != null && !mask[i]) continue;
                float d = Dist(ref P[i], p);
                if (d < best) { best = d; lab = P[i].label; }
            }
            return lab;
        }

        public float LabelDist(Vector3 p, int label, bool[] mask)
        {
            float best = Big;
            for (int i = 0; i < P.Length; i++)
            {
                if (P[i].label != label || (mask != null && !mask[i])) continue;
                float d = Dist(ref P[i], p);
                if (d < best) best = d;
            }
            return best;
        }

        public Vector3 Grad(Vector3 p, bool[] mask, float e)
        {
            float a = Eval(new Vector3(p.x + e, p.y - e, p.z - e), mask);
            float b = Eval(new Vector3(p.x - e, p.y - e, p.z + e), mask);
            float c = Eval(new Vector3(p.x - e, p.y + e, p.z - e), mask);
            float d = Eval(new Vector3(p.x + e, p.y + e, p.z + e), mask);
            return new Vector3(a - b - c + d, -a - b + c + d, -a + b - c + d) * (0.25f / e);
        }

        /// <summary>Sphere-trace from 'from' along 'dir' to the surface.</summary>
        public Vector3 March(Vector3 from, Vector3 dir, float maxDist = 2f)
        {
            Bake();
            dir.Normalize();
            float t = 0f;
            for (int i = 0; i < 160; i++)
            {
                Vector3 p = from + dir * t;
                float d = Eval(p);
                if (d < 1e-4f) return p;
                t += Mathf.Max(d * 0.8f, 1e-4f);
                if (t > maxDist) break;
            }
            return from + dir * Mathf.Min(t, maxDist);
        }
    }

    /// <summary>Surface-nets polygonisation of an SdfShape plus the clean-border colour cuts.</summary>
    public sealed class SdfMesh
    {
        public readonly List<Vector3> P = new List<Vector3>(16384);
        public readonly List<Vector3> N = new List<Vector3>(16384);
        public readonly List<Vector3> Bias = new List<Vector3>(16384);
        public readonly List<int> M = new List<int>(16384);
        public List<int> L = new List<int>(16384);
        public List<int> T = new List<int>(65536);
        public bool[][] masks;
        public SdfShape shape;
        public float h;

        static readonly int[] E0 = { 0, 2, 4, 6, 0, 1, 4, 5, 0, 1, 2, 3 };
        static readonly int[] E1 = { 1, 3, 5, 7, 2, 3, 6, 7, 4, 5, 6, 7 };

        public bool[] MaskOf(int v) => masks[M[v]];

        public static SdfMesh Build(SdfShape sh, float h)
        {
            sh.Bake();
            var m = new SdfMesh { shape = sh, h = h };
            int np = sh.P.Length;
            if (np == 0) { m.masks = new bool[0][]; return m; }
            Vector3 mn = Vector3.one * 1e9f, mx = -Vector3.one * 1e9f;
            for (int i = 0; i < np; i++)
            {
                Vector3 c = sh.P[i].bc;
                float r = sh.P[i].br;
                mn = Vector3.Min(mn, c - Vector3.one * r);
                mx = Vector3.Max(mx, c + Vector3.one * r);
            }
            float pad = sh.kMax + 3f * h;
            mn -= Vector3.one * pad;
            mx += Vector3.one * pad;
            const int B = 8;
            int nx = Mathf.CeilToInt((mx.x - mn.x) / h) + 1, ny = Mathf.CeilToInt((mx.y - mn.y) / h) + 1, nz = Mathf.CeilToInt((mx.z - mn.z) / h) + 1;
            int bx = (nx + B - 1) / B, by = (ny + B - 1) / B, bz = (nz + B - 1) / B;
            int nb = bx * by * bz, sxy = nx * ny;
            var F = new float[nx * ny * nz];
            var cls = new sbyte[nb];
            var masks = new bool[nb][];
            float rb = Mathf.Sqrt(3f) * 0.5f * B * h;
            float lim = 1.5f * rb + sh.kMax;

            // 1. classify bricks: far outside / deep inside need no evaluation
            Parallel.For(0, nb, bi =>
            {
                int ix = bi % bx, iy = (bi / bx) % by, iz = bi / (bx * by);
                Vector3 c = mn + new Vector3((ix * B + B * 0.5f) * h, (iy * B + B * 0.5f) * h, (iz * B + B * 0.5f) * h);
                float dc = sh.Eval(c);
                if (dc > lim || dc < -lim)
                {
                    cls[bi] = dc > 0f ? (sbyte)1 : (sbyte)-1;
                    float v = dc > 0f ? SdfShape.Big : -SdfShape.Big;
                    int x1 = Mathf.Min(ix * B + B, nx), y1 = Mathf.Min(iy * B + B, ny), z1 = Mathf.Min(iz * B + B, nz);
                    for (int z = iz * B; z < z1; z++)
                        for (int y = iy * B; y < y1; y++)
                        {
                            int row = nx * y + sxy * z;
                            for (int x = ix * B; x < x1; x++) F[x + row] = v;
                        }
                    return;
                }
                var mk = new bool[np];
                float thr = dc + lim;
                for (int i = 0; i < np; i++) mk[i] = (c - sh.P[i].bc).magnitude - sh.P[i].br - rb <= thr;
                masks[bi] = mk;
            });

            // 2. evaluate the active bricks (their far faces included)
            Parallel.For(0, nb, bi =>
            {
                if (cls[bi] != 0) return;
                var mk = masks[bi];
                int ix = bi % bx, iy = (bi / bx) % by, iz = bi / (bx * by);
                int x1 = Mathf.Min(ix * B + B, nx - 1), y1 = Mathf.Min(iy * B + B, ny - 1), z1 = Mathf.Min(iz * B + B, nz - 1);
                for (int z = iz * B; z <= z1; z++)
                    for (int y = iy * B; y <= y1; y++)
                    {
                        int row = nx * y + sxy * z;
                        for (int x = ix * B; x <= x1; x++)
                            F[x + row] = sh.Eval(new Vector3(mn.x + x * h, mn.y + y * h, mn.z + z * h), mk);
                    }
            });

            // 3. one vertex per cell the surface crosses
            int cx = nx - 1, cy = ny - 1, cz = nz - 1;
            var cellV = new int[cx * cy * cz];
            var bv = new List<Vector3>[nb];
            var bcl = new List<int>[nb];
            Parallel.For(0, nb, bi =>
            {
                if (cls[bi] != 0) return;
                int ix = bi % bx, iy = (bi / bx) % by, iz = bi / (bx * by);
                int x1 = Mathf.Min(ix * B + B, cx), y1 = Mathf.Min(iy * B + B, cy), z1 = Mathf.Min(iz * B + B, cz);
                var lv = new List<Vector3>(128);
                var lc = new List<int>(128);
                var v = new float[8];
                for (int z = iz * B; z < z1; z++)
                    for (int y = iy * B; y < y1; y++)
                        for (int x = ix * B; x < x1; x++)
                        {
                            int msk = 0;
                            for (int c = 0; c < 8; c++)
                            {
                                float f = F[(x + (c & 1)) + nx * (y + ((c >> 1) & 1)) + sxy * (z + ((c >> 2) & 1))];
                                v[c] = f;
                                if (f < 0f) msk |= 1 << c;
                            }
                            if (msk == 0 || msk == 255) continue;
                            float sx = 0f, sy = 0f, sz = 0f;
                            int cnt = 0;
                            for (int e = 0; e < 12; e++)
                            {
                                int c0 = E0[e], c1 = E1[e];
                                if (((msk >> c0) & 1) == ((msk >> c1) & 1)) continue;
                                float t = v[c0] / (v[c0] - v[c1]);
                                float x0 = c0 & 1, y0 = (c0 >> 1) & 1, z0 = (c0 >> 2) & 1;
                                float xa = c1 & 1, ya = (c1 >> 1) & 1, za = (c1 >> 2) & 1;
                                sx += x0 + (xa - x0) * t; sy += y0 + (ya - y0) * t; sz += z0 + (za - z0) * t;
                                cnt++;
                            }
                            lv.Add(new Vector3(mn.x + (x + sx / cnt) * h, mn.y + (y + sy / cnt) * h, mn.z + (z + sz / cnt) * h));
                            lc.Add(x + cx * (y + cy * z));
                        }
                bv[bi] = lv;
                bcl[bi] = lc;
            });
            for (int bi = 0; bi < nb; bi++)
            {
                var lv = bv[bi];
                if (lv == null) continue;
                var lc = bcl[bi];
                for (int i = 0; i < lv.Count; i++)
                {
                    m.P.Add(lv[i]);
                    m.M.Add(bi);
                    cellV[lc[i]] = m.P.Count;
                }
            }

            // 4. pull each vertex onto the true surface, smooth normals from the field gradient
            int nv = m.P.Count;
            var PA = m.P.ToArray();
            var NA = new Vector3[nv];
            var MA = m.M.ToArray();
            float ge = 0.3f * h;
            Parallel.For(0, nv, vi =>
            {
                var mk = masks[MA[vi]];
                Vector3 p0 = PA[vi], p = p0;
                for (int it = 0; it < 2; it++)
                {
                    float d = sh.Eval(p, mk);
                    Vector3 g = sh.Grad(p, mk, ge);
                    float g2 = g.sqrMagnitude;
                    if (g2 < 1e-10f) break;
                    p -= g * (d / g2);
                }
                Vector3 off = p - p0;
                float om = off.magnitude;
                if (om > h) p = p0 + off * (h / om);
                Vector3 n = sh.Grad(p, mk, ge);
                NA[vi] = n.sqrMagnitude > 1e-14f ? n.normalized : Vector3.up;
                PA[vi] = p;
            });
            m.P.Clear();
            m.P.AddRange(PA);
            m.N.AddRange(NA);
            for (int i = 0; i < nv; i++) m.Bias.Add(Vector3.zero);

            // 5. a quad across every grid edge with a sign change
            for (int bi = 0; bi < nb; bi++)
            {
                if (cls[bi] != 0) continue;
                int ix = bi % bx, iy = (bi / bx) % by, iz = bi / (bx * by);
                int x1 = Mathf.Min(ix * B + B, nx), y1 = Mathf.Min(iy * B + B, ny), z1 = Mathf.Min(iz * B + B, nz);
                for (int z = iz * B; z < z1; z++)
                    for (int y = iy * B; y < y1; y++)
                        for (int x = ix * B; x < x1; x++)
                        {
                            int idx = x + nx * y + sxy * z;
                            bool in0 = F[idx] < 0f;
                            if (x + 1 < nx && y >= 1 && z >= 1 && y < cy && z < cz && in0 != (F[idx + 1] < 0f))
                                m.Quad(cellV, x + cx * (y - 1 + cy * (z - 1)), x + cx * (y + cy * (z - 1)), x + cx * (y + cy * z), x + cx * (y - 1 + cy * z), in0);
                            if (y + 1 < ny && x >= 1 && z >= 1 && x < cx && z < cz && in0 != (F[idx + nx] < 0f))
                                m.Quad(cellV, x - 1 + cx * (y + cy * (z - 1)), x - 1 + cx * (y + cy * z), x + cx * (y + cy * z), x + cx * (y + cy * (z - 1)), in0);
                            if (z + 1 < nz && x >= 1 && y >= 1 && x < cx && y < cy && in0 != (F[idx + sxy] < 0f))
                                m.Quad(cellV, x - 1 + cx * (y - 1 + cy * z), x + cx * (y - 1 + cy * z), x + cx * (y + cy * z), x - 1 + cx * (y + cy * z), in0);
                        }
            }
            m.masks = masks;
            return m;
        }

        void Quad(int[] cellV, int ca, int cb, int cc, int cd, bool outPositive)
        {
            int a = cellV[ca] - 1, b = cellV[cb] - 1, c = cellV[cc] - 1, d = cellV[cd] - 1;
            if (a < 0 || b < 0 || c < 0 || d < 0) return;
            bool ac = (P[a] - P[c]).sqrMagnitude <= (P[b] - P[d]).sqrMagnitude;
            if (outPositive)
            {
                if (ac) { Tri(a, b, c); Tri(a, c, d); } else { Tri(a, b, d); Tri(b, c, d); }
            }
            else
            {
                if (ac) { Tri(a, c, b); Tri(a, d, c); } else { Tri(a, d, b); Tri(b, d, c); }
            }
        }

        void Tri(int a, int b, int c) { T.Add(a); T.Add(b); T.Add(c); }

        public void ComputeLabels()
        {
            L = new List<int>(P.Count + 1024);
            for (int i = 0; i < P.Count; i++) L.Add(shape.Label(P[i] + Bias[i], masks[M[i]]));
        }

        int NewVert(int src, Vector3 p, Vector3 n, int label)
        {
            P.Add(p);
            N.Add(n);
            M.Add(M[src]);
            L.Add(label);
            Vector3 d = P[src] + Bias[src] - p;
            float dm = d.magnitude;
            Bias.Add(dm > 1e-7f ? d * (Mathf.Min(0.0015f, dm * 0.45f) / dm) : Vector3.zero);
            return P.Count - 1;
        }

        static long Key(int a, int b) => a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;

        /// <summary>long keys hash as (hi ^ lo) by default, which collides badly for index pairs; mix them properly.</summary>
        sealed class LongMix : IEqualityComparer<long>
        {
            public static readonly LongMix I = new LongMix();
            public bool Equals(long a, long b) => a == b;
            public int GetHashCode(long x)
            {
                ulong z = (ulong)x;
                z = (z ^ (z >> 30)) * 0xbf58476d1ce4e5b9UL;
                z = (z ^ (z >> 27)) * 0x94d049bb133111ebUL;
                z ^= z >> 31;
                return (int)z ^ (int)(z >> 32);
            }
        }

        static long PosKey(Vector3 p, float scale)
        {
            long x = Mathf.RoundToInt(p.x * scale) + (1 << 20), y = Mathf.RoundToInt(p.y * scale) + (1 << 20), z = Mathf.RoundToInt(p.z * scale) + (1 << 20);
            return (x << 42) | (y << 21) | z;
        }

        /// <summary>Split triangles whose corners lie in different colour regions along the exact border.</summary>
        public void CutLabels(Func<int, int, bool> need = null)
        {
            var cache = new Dictionary<long, long>(LongMix.I);
            var NT = new List<int>(T.Count + T.Count / 6);
            for (int t = 0; t < T.Count; t += 3)
            {
                int i0 = T[t], i1 = T[t + 1], i2 = T[t + 2];
                int l0 = L[i0], l1 = L[i1], l2 = L[i2];
                if ((l0 == l1 && l1 == l2) || (l0 != l1 && l1 != l2 && l0 != l2)) { NT.Add(i0); NT.Add(i1); NT.Add(i2); continue; }
                int a, b, c;
                if (l1 == l2) { a = i0; b = i1; c = i2; }
                else if (l0 == l2) { a = i1; b = i2; c = i0; }
                else { a = i2; b = i0; c = i1; }
                int la = L[a], lb = L[b];
                if (need != null && !need(la, lb)) { NT.Add(i0); NT.Add(i1); NT.Add(i2); continue; }
                SplitBy(cache, a, b, p => shape.LabelDist(p, la, masks[M[a]]) - shape.LabelDist(p, lb, masks[M[a]]), out int abA, out int abB);
                SplitBy(cache, a, c, p => shape.LabelDist(p, la, masks[M[a]]) - shape.LabelDist(p, lb, masks[M[a]]), out int acA, out int acB);
                NT.Add(a); NT.Add(abA); NT.Add(acA);
                NT.Add(abB); NT.Add(b); NT.Add(c);
                NT.Add(abB); NT.Add(c); NT.Add(acB);
            }
            T = NT;
        }

        /// <summary>Split the triangles of the given regions along the zero line of g (a paint border).</summary>
        public void Cut(Func<Vector3, float> g, Func<int, bool> regionOk)
        {
            var cache = new Dictionary<long, long>(LongMix.I);
            int nv = P.Count;
            var gv = new float[nv];
            var have = new bool[nv];
            Func<int, float> G = i =>
            {
                if (!have[i]) { gv[i] = g(P[i]); have[i] = true; }
                return gv[i];
            };
            var NT = new List<int>(T.Count + T.Count / 8);
            for (int t = 0; t < T.Count; t += 3)
            {
                int i0 = T[t], i1 = T[t + 1], i2 = T[t + 2];
                int lab = L[i0];
                if (L[i1] != lab || L[i2] != lab || !regionOk(lab)) { NT.Add(i0); NT.Add(i1); NT.Add(i2); continue; }
                bool s0 = G(i0) < 0f, s1 = G(i1) < 0f, s2 = G(i2) < 0f;
                if (s0 == s1 && s1 == s2) { NT.Add(i0); NT.Add(i1); NT.Add(i2); continue; }
                int a, b, c;
                if (s1 == s2) { a = i0; b = i1; c = i2; }
                else if (s0 == s2) { a = i1; b = i2; c = i0; }
                else { a = i2; b = i0; c = i1; }
                SplitBy(cache, a, b, g, out int abA, out int abB, G(a), G(b));
                SplitBy(cache, a, c, g, out int acA, out int acB, G(a), G(c));
                NT.Add(a); NT.Add(abA); NT.Add(acA);
                NT.Add(abB); NT.Add(b); NT.Add(c);
                NT.Add(abB); NT.Add(c); NT.Add(acB);
            }
            T = NT;
        }

        /// <summary>A border cut in one region ends on a vertex in the middle of the neighbouring region's edge (a T-junction,
        /// which opens a hairline crack once skinned). Split those edges at the stray vertices so the surface stays closed.</summary>
        public int FixTJunctions()
        {
            int fixedTotal = 0;
            for (int pass = 0; pass < 4; pass++)
            {
                int nv = P.Count;
                var map = new Dictionary<long, int>(nv, LongMix.I);
                var wid = new int[nv];
                var wpos = new List<Vector3>(nv);
                for (int i = 0; i < nv; i++)
                {
                    Vector3 p = P[i];
                    long key = PosKey(p, 50000f);
                    if (!map.TryGetValue(key, out int id)) { id = wpos.Count; map[key] = id; wpos.Add(p); }
                    wid[i] = id;
                }
                var count = new Dictionary<long, int>(T.Count, LongMix.I);
                var owner = new Dictionary<long, int>(T.Count, LongMix.I);
                for (int t = 0; t < T.Count; t += 3)
                    for (int e = 0; e < 3; e++)
                    {
                        int a = wid[T[t + e]], b = wid[T[t + (e + 1) % 3]];
                        if (a == b) continue;
                        long key = Key(a, b);
                        count.TryGetValue(key, out int c);
                        count[key] = c + 1;
                        owner[key] = t + e;
                    }
                var nbr = new Dictionary<int, List<int>>();
                foreach (var kv in count)
                {
                    if (kv.Value != 1) continue;
                    int a = (int)(kv.Key >> 32), b = (int)(kv.Key & 0xffffffffL);
                    if (!nbr.TryGetValue(a, out var la)) nbr[a] = la = new List<int>(4);
                    if (!nbr.TryGetValue(b, out var lb)) nbr[b] = lb = new List<int>(4);
                    la.Add(b); lb.Add(a);
                }
                // per triangle edge: the stray vertices that lie on it, walking along open edges from one end to the other
                var splits = new Dictionary<int, List<int>>();
                foreach (var kv in count)
                {
                    if (kv.Value != 1) continue;
                    int a = (int)(kv.Key >> 32), b = (int)(kv.Key & 0xffffffffL);
                    int slot = owner[kv.Key];
                    int t0 = slot - slot % 3, e = slot % 3;
                    int ea = wid[T[t0 + e]], eb = wid[T[t0 + (e + 1) % 3]];
                    Vector3 pa = wpos[ea], pb = wpos[eb], ab = pb - pa;
                    float L2 = ab.sqrMagnitude;
                    if (L2 < 1e-12f) continue;
                    var chain = new List<int>();
                    int cur = ea; float tc = 0f;
                    for (int guard = 0; guard < 16; guard++)
                    {
                        if (!nbr.TryGetValue(cur, out var nl)) break;
                        int best = -1; float bt = 2f;
                        foreach (int x in nl)
                        {
                            if (x == eb) { if (chain.Count > 0 && bt > 1f) { best = eb; bt = 1f; } continue; }
                            Vector3 px = wpos[x];
                            float tx = Vector3.Dot(px - pa, ab) / L2;
                            if (tx <= tc + 1e-5f || tx >= 1f - 1e-5f) continue;
                            if ((px - (pa + ab * tx)).sqrMagnitude > 1e-9f + L2 * 1e-6f) continue;
                            if (tx < bt) { bt = tx; best = x; }
                        }
                        if (best < 0) { chain.Clear(); break; }
                        if (best == eb) break;
                        chain.Add(best);
                        cur = best; tc = bt;
                    }
                    if (chain.Count > 0 && !splits.ContainsKey(t0)) { chain.Insert(0, e); splits[t0] = chain; }
                }
                if (splits.Count == 0) break;
                var NT = new List<int>(T.Count + splits.Count * 6);
                for (int t = 0; t < T.Count; t += 3)
                {
                    if (!splits.TryGetValue(t, out var sp)) { NT.Add(T[t]); NT.Add(T[t + 1]); NT.Add(T[t + 2]); continue; }
                    int e = sp[0];
                    int va = T[t + e], vb = T[t + (e + 1) % 3], vo = T[t + (e + 2) % 3];
                    Vector3 cen = (P[va] + P[vb] + P[vo]) / 3f;
                    int prev = va;
                    for (int i = 1; i < sp.Count; i++)
                    {
                        Vector3 px = wpos[sp[i]];
                        float tt = Mathf.Clamp01(Vector3.Dot(px - P[va], P[vb] - P[va]) / Mathf.Max(1e-12f, (P[vb] - P[va]).sqrMagnitude));
                        Vector3 n = Vector3.LerpUnclamped(N[va], N[vb], tt);
                        P.Add(px); N.Add(n.sqrMagnitude > 1e-12f ? n.normalized : N[va]); M.Add(M[va]); L.Add(L[va]);
                        Vector3 d = cen - px; float dm = d.magnitude;
                        Bias.Add(dm > 1e-7f ? d * (Mathf.Min(0.0015f, dm * 0.45f) / dm) : Vector3.zero);
                        int nx = P.Count - 1;
                        NT.Add(prev); NT.Add(nx); NT.Add(vo);
                        prev = nx;
                    }
                    NT.Add(prev); NT.Add(vb); NT.Add(vo);
                    fixedTotal++;
                }
                T = NT;
            }
            return fixedTotal;
        }

        void SplitBy(Dictionary<long, long> cache, int a, int b, Func<Vector3, float> g, out int nearA, out int nearB, float ga = float.NaN, float gb = float.NaN)
        {
            long key = Key(a, b);
            if (cache.TryGetValue(key, out long pair))
            {
                int lo = (int)(pair >> 32), hi = (int)(pair & 0xffffffffL);
                if (a < b) { nearA = lo; nearB = hi; } else { nearA = hi; nearB = lo; }
                return;
            }
            Vector3 pa = P[a], pb = P[b];
            if (float.IsNaN(ga)) ga = g(pa);
            if (float.IsNaN(gb)) gb = g(pb);
            // regula falsi on the chord
            float t0 = 0f, t1 = 1f, f0 = ga, f1 = gb, t = 0.5f;
            for (int it = 0; it < 3; it++)
            {
                float den = f0 - f1;
                t = Mathf.Abs(den) > 1e-12f ? t0 + (t1 - t0) * (f0 / den) : (t0 + t1) * 0.5f;
                if (it == 2) break;
                float ft = g(Vector3.LerpUnclamped(pa, pb, t));
                if ((ft < 0f) == (f0 < 0f)) { t0 = t; f0 = ft; } else { t1 = t; f1 = ft; }
            }
            t = Mathf.Clamp(t, 0.04f, 0.96f);
            Vector3 p = Vector3.LerpUnclamped(P[a], P[b], t);
            Vector3 n = Vector3.LerpUnclamped(N[a], N[b], t);
            n = n.sqrMagnitude > 1e-12f ? n.normalized : N[a];
            nearA = NewVert(a, p, n, L[a]);
            nearB = NewVert(b, p, n, L[b]);
            cache[key] = a < b ? ((long)nearA << 32) | (uint)nearB : ((long)nearB << 32) | (uint)nearA;
        }
    }

    /// <summary>The player body as one soft blob of cones and ellipsoids, plus hair and the wacky mohawk crest.</summary>
    public static class PlayerSdf
    {
        public const int LTorso = 0, LNeck = 1, LHead = 2, LArmL = 3, LArmR = 4, LHandL = 5, LHandR = 6, LLegL = 7, LLegR = 8, LShoeL = 9, LShoeR = 10, LMuzzle = 11;
        /// <summary>Modelling pose: arms swung out from the sides so they do not melt into the torso. The skin is bound in this
        /// pose (PlayerMesh.BindPose), so it is never baked back to arms-down.</summary>
        const float ArmOut = PlayerMesh.ArmOut;
        public static float Res = 0.015f;
        public static readonly System.Text.StringBuilder Stats = new System.Text.StringBuilder();
        public static Action<SdfMesh, string> Probe;
        static void Stat(SdfMesh m, string what) { var pr = Probe; if (pr == null) return; lock (Stats) { Stats.Append(what).Append('=').Append(m.P.Count).Append(' '); } pr(m, what); }

        public sealed class Info
        {
            public float backZ, frontZ;
            public readonly List<Vector3> buttons = new List<Vector3>();
            /// <summary>Costume buttons and clasps: position (xyz) and radius (w), with their colours.</summary>
            public readonly List<Vector4> studs = new List<Vector4>();
            public readonly List<Color> studC = new List<Color>();
            /// <summary>The middle of the seat of the pants, on the surface (overalls wear the club name there).</summary>
            public Vector3 seat;
            public SdfShape shape;
            public Vector3 headCenter;
            public float ms;
            public int verts;
            /// <summary>Grid scale of this build (1 = full detail, 2+ = far-away level of detail).</summary>
            public float coarse = 1f;
            /// <summary>The body's vertices in the modelling pose, their regions and final skin weights (for gear that rides on it).</summary>
            public IList<Vector3> skinP;
            public IList<int> skinL;
            public BoneWeight[] skinW;
        }

        static Color Cloth(Color c) => Pal.Gloss(c, 0.14f);
        static Color SkinC(Color c) => Pal.Gloss(c, 0.36f);
        static Color HairC(Color c) => Pal.Gloss(c, 0.22f);
        static float Smooth01(float x) { x = Mathf.Clamp01(x); return x * x * (3f - 2f * x); }

        public static Info MakeBody(MeshKit k, Body b, TeamData t, Vector3[] rp, float coarse = 1f)
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();
            float s = b.height / 1.85f, w = PlayerMesh.WidthOf(b), be = b.belly, mu = b.muscle;
            float TK = PlayerMesh.TorsoK, hs = s * PlayerMesh.HeadOf(b), Lk = PlayerMesh.LimbK * s, Hk = PlayerMesh.HandK * s, Fk = PlayerMesh.FootK * s;
            bool ape = b.ape;
            // ---- costume (Banana Ball dress-up nights); the defaults are the plain uniform
            bool aloha = b.top == Top.Aloha, tank = b.top == Top.Tank, tux = b.top == Top.Tux, coat = b.top == Top.FireCoat, jerseyTop = b.top == Top.Jersey;
            bool shorts = b.bottom == Bottom.Shorts, kilt = b.bottom == Bottom.Kilt, overalls = b.bottom == Bottom.Overalls, pantsBot = b.bottom == Bottom.Pants;
            bool flip = b.feet == Feet.FlipFlops && !ape, boots = b.feet == Feet.Boots && !ape;
            bool untucked = aloha || tank || tux || coat;
            bool blouse = b.highSocks && pantsBot && !tux && !coat && !boots && !flip && !ape;   // baseball pants bloused over high socks
            bool socked = !flip && !ape;
            bool lowSock = socked && !boots && !kilt && !blouse;
            float aLk = ape ? Lk * 1.3f : Lk;          // gorilla arms: huge
            if (ape) { Hk *= 1.28f; Fk *= 1.08f; }
            float zk = (0.9f + 0.1f * w) * (ape ? 1.16f : 1f);
            Vector3 hips = rp[0];
            Vector3 Tu(float y) => hips + new Vector3(0f, y * s * TK, 0f);
            Vector3 Hd(float x, float y, float z) => rp[4] + new Vector3(x, y, z) * hs;
            Quaternion rotL = Quaternion.Euler(0f, 0f, -ArmOut), rotR = Quaternion.Euler(0f, 0f, ArmOut);
            Vector3 SL = rp[8], SR = rp[11];
            Vector3 A(Vector3 p, int side) => side == 0 ? SL + rotL * (p - SL) : SR + rotR * (p - SR);

            var sh = new SdfShape();
            var core = sh.Root;
            // ---- torso: a soft bean from crotch to shoulders
            sh.Ell(core, Tu(0.0f) + new Vector3(0f, 0f, -0.008f * s), new Vector3(0.158f * w * s, 0.088f * s * TK, 0.112f * zk * s), Quaternion.identity, 0f, LTorso, -2);
            if (!ape) sh.Ell(core, Tu(-0.02f) + new Vector3(0f, 0f, -0.05f * s), new Vector3(0.132f * w * s, 0.07f * s * TK, 0.085f * zk * s), Quaternion.identity, 0.05f * s, LTorso, -2);
            sh.Ell(core, Tu(0.115f) + new Vector3(0f, 0f, 0.028f * be * s), new Vector3(0.150f * w * s, 0.085f * s * TK, (0.104f + 0.05f * be) * zk * s), Quaternion.identity, 0.07f * s, LTorso, -2);
            sh.Ell(core, Tu(0.235f) + new Vector3(0f, 0f, 0.052f * be * s), new Vector3((0.155f + 0.02f * be) * w * s, 0.1f * s * TK, (0.108f + 0.074f * be) * zk * s), Quaternion.identity, 0.07f * s, LTorso, -2);
            sh.Ell(core, Tu(0.345f) + new Vector3(0f, 0f, (0.012f + 0.014f * be) * s), new Vector3((0.172f + 0.016f * mu) * w * s, 0.105f * s * TK, (0.114f + 0.034f * be + 0.008f * mu) * zk * s), Quaternion.identity, 0.07f * s, LTorso, -2);
            sh.Ell(core, Tu(0.44f), new Vector3((0.178f + 0.022f * mu) * w * s, 0.075f * s * TK, (0.1f + 0.008f * mu) * zk * s), Quaternion.identity, 0.06f * s, LTorso, -2);
            if (ape)
                for (int e = -1; e <= 1; e += 2)   // the mountains of shoulder a gorilla has
                    sh.Ell(core, Tu(0.47f) + new Vector3(e * 0.14f * w * s, 0f, -0.012f * s), new Vector3(0.095f, 0.075f, 0.1f) * s, Quaternion.identity, 0.06f * s, LTorso, -2);
            sh.Cone(core, Tu(0.47f), rp[4] + new Vector3(0f, 0.02f * hs, -0.004f * hs), (ape ? 0.1f : 0.074f) * s, (ape ? 0.09f : 0.068f) * s, 0.035f * s, LNeck, 2, 3, 0.05f, 0.55f);

            // ---- head: round cranium, chubby cheeks, button nose, ears
            var head = sh.Branch(core, 0.03f * s);
            float ck = Mathf.Lerp(1f, b.chin, 0.5f);
            sh.Ell(head, Hd(0f, 0.1f, 0f), new Vector3(0.085f, 0.092f, 0.088f) * hs, Quaternion.identity, 0f, LHead, 4);
            sh.Ell(head, Hd(0f, 0.062f, 0.012f), new Vector3(0.08f, 0.06f * ck, 0.08f) * hs, Quaternion.identity, 0.014f * hs, LHead, 4);
            float nz = b.nose * 0.85f;
            if (!ape)
                sh.Ell(head, Hd(0f, 0.063f, 0.088f + 0.008f * (nz - 1f)), new Vector3(0.0185f, 0.0165f, 0.0165f) * hs * nz, Quaternion.identity, 0.01f * hs, LHead, 4);
            else
            {
                // heavy brow ridge, big round muzzle
                sh.Ell(head, Hd(0f, 0.116f, 0.064f), new Vector3(0.078f, 0.024f, 0.034f) * hs, Quaternion.Euler(-10f, 0f, 0f), 0.012f * hs, LHead, 4);
                sh.Ell(head, Hd(0f, 0.043f, 0.074f), new Vector3(0.066f, 0.05f, 0.054f) * hs, Quaternion.Euler(8f, 0f, 0f), 0.016f * hs, LMuzzle, 4);
            }
            // ears are fine parts on the head bone (PlayerSdf.Ears): the body grid is too coarse for their folds

            // ---- arms (modelled swung out); the hands are fine parts on the hand bones
            for (int side = 0; side < 2; side++)
            {
                int shb = side == 0 ? 8 : 11, el = shb + 1, ha = shb + 2;
                float sg = side == 0 ? -1f : 1f;
                int la = side == 0 ? LArmL : LArmR;
                var arm = sh.Branch(core, 0.045f * s);
                Vector3 S0 = rp[shb] + new Vector3(-sg * 0.012f, 0.012f, 0f) * s, E = rp[el], W = rp[ha];
                float rTop = (0.062f + 0.016f * mu) * aLk, rMid = (0.055f + 0.018f * mu) * aLk, rEl = (0.047f + 0.004f * mu) * aLk;
                Vector3 Mid = Vector3.Lerp(S0, E, 0.4f) + new Vector3(0f, 0f, 0.004f * s);
                sh.Cone(arm, A(S0, side), A(Mid, side), rTop, rMid, 0f, la, shb);
                sh.Cone(arm, A(Mid, side), A(E, side), rMid, rEl, 0.04f * s, la, shb, el, 0.6f, 1.15f);
                float rF = (0.044f + 0.01f * mu) * aLk * (ape ? 1.15f : 1f), rW = 0.032f * aLk;
                Vector3 Fm = Vector3.Lerp(E, W, 0.3f) + new Vector3(0f, 0f, 0.003f * s);
                sh.Cone(arm, A(E, side), A(Fm, side), rEl, rF, 0.06f * s, la, el);
                sh.Cone(arm, A(Fm, side), A(W, side), rF, rW, 0.04f * s, la, el, ha, 0.8f, 1.15f);
            }

            // ---- legs (the thighs touch with a soft fold, not a sharp crease)
            var legs = sh.Branch(core, 0.04f * s);
            legs.kidK = 0.014f * s;
            float yShort = Mathf.Lerp(rp[14].y, rp[15].y, 0.72f);   // board shorts end a hand above the knee
            for (int side = 0; side < 2; side++)
            {
                int th = side == 0 ? 14 : 17, kn = th + 1, ft = th + 2;
                int ll = side == 0 ? LLegL : LLegR, ls = side == 0 ? LShoeL : LShoeR;
                var leg = sh.Branch(legs, 0f);
                Vector3 a = rp[th] + new Vector3(0f, 0.05f * s, 0f), K = rp[kn], c = rp[ft];
                float rTh = (0.084f + 0.01f * be) * w * Lk, rKn = 0.066f * Lk;
                sh.Cone(leg, a, K, rTh, rKn, 0f, ll, 0, th, -0.05f, 0.3f);
                Vector3 calf = Vector3.Lerp(K, c, 0.32f) + new Vector3(0f, 0f, -0.007f * s);
                sh.Cone(leg, K, calf, 0.062f * Lk, (0.06f + 0.007f * mu) * Lk, 0.03f * s, ll, th, kn, -0.3f, 0.3f);
                sh.Cone(leg, calf, c + new Vector3(0f, -0.02f * s, 0f), (0.06f + 0.007f * mu) * Lk, 0.044f * Lk, 0.03f * s, ll, kn, ft, 0.85f, 1.1f);
                if (lowSock)
                    sh.Ell(leg, Vector3.Lerp(K, c, 0.86f), new Vector3(0.056f, 0.032f, 0.06f) * Lk, Quaternion.identity, 0.02f * s, ll, kn);
                if (blouse)
                {
                    // classic baseball pants: bloused just below the knee, over the high socks
                    float yHem0 = Mathf.Lerp(K.y, c.y, 0.36f);
                    sh.Ell(leg, new Vector3(K.x, yHem0 + 0.045f * s, Mathf.Lerp(K.z, c.z, 0.3f) - 0.004f * s), new Vector3(0.071f * Lk, 0.04f * s, 0.073f * Lk), Quaternion.identity, 0.03f * s, ll, kn);
                }
                if (shorts)
                {
                    // wide board-shorts legs: a soft lip round the hem
                    float tt = Mathf.InverseLerp(a.y, K.y, yShort);
                    Vector3 cc = Vector3.Lerp(a, K, tt);
                    float rr = Mathf.Lerp(rTh, rKn, tt) + 0.012f * s;
                    sh.Ell(leg, new Vector3(cc.x, yShort + 0.016f * s, cc.z), new Vector3(rr, 0.03f * s, rr), Quaternion.identity, 0.018f * s, ll, th);
                }
                if (ape)
                {
                    // big flat bare feet, the big toe sticking out to the inside
                    float sgi = side == 0 ? 1f : -1f;
                    sh.Ell(leg, c + new Vector3(0f, -0.05f, 0.045f) * Fk, new Vector3(0.062f, 0.036f, 0.112f) * Fk, Quaternion.identity, 0.02f * s, ls, ft);
                    sh.Ell(leg, c + new Vector3(sgi * 0.05f, -0.062f, 0.095f) * Fk, new Vector3(0.026f, 0.022f, 0.04f) * Fk, Quaternion.Euler(0f, sgi * 28f, 0f), 0.012f * s, ls, ft);
                }
            }

            // ---- costume shapes on the torso: a loose hem over the waistband, the aloha shirt's camp collar, a fire coat's high collar
            float yBL = hips.y + 0.082f * s * TK, yBH = hips.y + 0.118f * s * TK, yBM = (yBL + yBH) * 0.5f;
            float yHem = aloha || tank ? Tu(0.03f).y : tux ? Tu(0.0f).y : coat ? Tu(-0.055f).y : yBL;
            // (measuring rays see the torso and legs only: no arm in the way)
            bool[] Trunk() { sh.Bake(); var mk = new bool[sh.P.Length]; for (int i = 0; i < mk.Length; i++) { int l0 = sh.P[i].label; mk[i] = l0 == LTorso || l0 == LLegL || l0 == LLegR; } return mk; }
            if (aloha || tux || coat)
            {
                var trunk = Trunk();
                float yh = yHem + 0.011f * s;
                float zf = MarchMask(sh, trunk, new Vector3(0f, yh, 0.8f), Vector3.back, 1.6f).z;
                float zb = MarchMask(sh, trunk, new Vector3(0f, yh, -0.8f), Vector3.forward, 1.6f).z;
                float xs = MarchMask(sh, trunk, new Vector3(0.8f, yh, (zf + zb) * 0.5f), Vector3.left, 1.6f).x;
                float lip = (coat ? 0.014f : 0.009f) * s;
                sh.Ell(core, new Vector3(0f, yh, (zf + zb) * 0.5f), new Vector3(xs + lip, 0.026f * s, (zf - zb) * 0.5f + lip), Quaternion.identity, 0.02f * s, LTorso, -2);
            }
            if (aloha)
                for (int e = -1; e <= 1; e += 2)
                {
                    // camp collar: two soft flaps lying open on the upper chest, either side of the open neck
                    var trunk = Trunk();
                    float cy = Tu(0.425f).y, cx = e * 0.074f * s;
                    float cz = MarchMask(sh, trunk, new Vector3(cx, cy, 0.8f), Vector3.back, 1.6f).z;
                    sh.Ell(core, new Vector3(cx, cy, cz - 0.003f * s), new Vector3(0.046f, 0.024f, 0.009f) * s, Quaternion.Euler(-26f, 0f, -e * 40f), 0.005f * s, LTorso, -2);
                }
            if (coat)
                sh.Cone(core, Tu(0.42f), Tu(0.505f) + new Vector3(0f, 0f, 0.004f * s), (ape ? 0.12f : 0.095f) * s, (ape ? 0.115f : 0.089f) * s, 0.012f * s, LTorso, -2);

            sh.Bake();
            var m = SdfMesh.Build(sh, Res * s * coarse);
            if (Probe != null) lock (Stats) Stats.Clear();
            Stat(m, "nets");
            m.ComputeLabels();
            bool shirtStripes = t.pinstripes && jerseyTop, pantStripes = t.pinstripes && pantsBot && !tux && !coat;
            bool spotted = t.spots && jerseyTop;
            bool printed = aloha || spotted;
            bool bandW0 = (b.gear == Gear.Headband || b.muscle > 0.6f) && (jerseyTop || tank);
            m.CutLabels((la, lb) =>
            {
                if (la > lb) { int tmp = la; la = lb; lb = tmp; }
                if (la == LNeck && lb == LHead) return false;                            // skin meets skin
                if (la == LTorso && (lb == LArmL || lb == LArmR)) return shirtStripes || printed;   // shirt meets sleeve
                if (la == LTorso && (lb == LLegL || lb == LLegR)) return pantStripes || printed;   // pants meet pants
                if ((la == LArmL && lb == LHandL) || (la == LArmR && lb == LHandR)) return b.sleeves || bandW0 || ape;
                return true;
            });
            Stat(m, "labels");

            // ---- colours
            Color jersey = Cloth(t.jersey), pants = Cloth(t.pants), belt = Pal.Gloss(t.dark, 0.35f), trim = Cloth(t.main), pipe = Cloth(t.accent);
            Color skin = SkinC(b.skin), sock = Cloth(t.main), sockBand = Cloth(Color.Lerp(t.accent, Color.white, t.accent.grayscale < 0.3f ? 0f : 0.2f));
            Color under = Cloth(t.dark.grayscale < 0.2f ? t.main : t.dark), buckle = Pal.Gloss(new Color(0.85f, 0.8f, 0.62f), 0.85f);
            Color shoe = Pal.Gloss(Pal.Cleat, 0.55f), sole = Pal.Gloss(new Color(0.93f, 0.93f, 0.95f), 0.3f), swoosh = Cloth(t.main), wrist = Cloth(t.main);
            Color blushC = Pal.Gloss(Color.Lerp(b.skin, new Color(1f, 0.42f, 0.45f), 0.5f), 0.36f);
            Color stubbleC = Pal.Gloss(Color.Lerp(b.skin, Color.Lerp(b.hairColor, Color.black, 0.2f), 0.3f), 0.36f);
            bool band = bandW0;
            Color fur = Pal.Gloss(new Color(0.2f, 0.19f, 0.21f), 0.1f), apeFace = Pal.Gloss(new Color(0.33f, 0.29f, 0.29f), 0.3f);
            Color muzzle = Pal.Gloss(new Color(0.6f, 0.54f, 0.5f), 0.3f);
            if (ape) { sock = fur; shoe = apeFace; }
            Color bare = ape ? fur : skin;
            var cw = Wardrobe.Of(b, t);
            Color dark = Pal.Gloss(new Color(0.11f, 0.11f, 0.13f), 0.1f);   // what hides under a kilt

            float neckZ = -0.003f * s, rCol = 0.088f * s, colW = 0.013f * s;
            float yV = hips.y + 0.45f * s * TK;
            float VHalf(float y) => Mathf.Max(0f, y - yV) * 0.85f;
            float yColMin = hips.y + 0.4f * s * TK;
            float CollarOuter(Vector3 p) => Mathf.Max(Mathf.Min(new Vector2(p.x, p.z - neckZ).magnitude - rCol, Mathf.Max(Mathf.Abs(p.x) - VHalf(p.y), -p.z)), yColMin - p.y);
            float CollarInner(Vector3 p) => Mathf.Max(Mathf.Min(new Vector2(p.x, p.z - neckZ).magnitude - (rCol - colW), Mathf.Max(Mathf.Abs(p.x) - (VHalf(p.y) - colW), -p.z)), yColMin - p.y);
            float Placket(Vector3 p) => Mathf.Max(Mathf.Max(Mathf.Abs(p.x) - 0.0045f * s, -p.z), Mathf.Max(yBH + 0.006f * s - p.y, p.y - yV));
            float Buckle(Vector3 p) => Mathf.Max(Mathf.Max(Mathf.Abs(p.x) - 0.024f * s, Mathf.Abs(p.y - yBM) - 0.017f * s), -p.z);
            // aloha shirt: an open V at the throat
            float yVa = Tu(0.31f).y;
            float AlohaV(Vector3 p) => Mathf.Max(Mathf.Abs(p.x) - (p.y - yVa) * 0.55f, -p.z);
            // tank top: a scoop neck and deep armholes (ellipses in the body's front plane), bound with a trim
            float yTop = Tu(0.47f).y, shx = Mathf.Abs(rp[11].x - hips.x);
            float armXc = shx + 0.03f * s, armAx = Mathf.Max(0.06f * s, armXc - 0.125f * s), armYc = yTop + 0.012f * s, armAy = 0.155f * s;
            float Armhole(Vector3 p) { float qx = (Mathf.Abs(p.x) - armXc) / armAx, qy = (p.y - armYc) / armAy; return (Mathf.Sqrt(qx * qx + qy * qy) - 1f) * armAx; }
            float scoopYc = Tu(0.5f).y;
            float Scoop(Vector3 p) { float rx = (p.z > 0f ? 0.09f : 0.08f) * s, ry = (p.z > 0f ? 0.118f : 0.058f) * s; float qx = p.x / rx, qy = (p.y - scoopYc) / ry; return (Mathf.Sqrt(qx * qx + qy * qy) - 1f) * rx; }
            float tankTrim = 0.011f * s;
            // tuxedo: the jacket's V over a white shirt, lapels round it (a collar round the back of the neck), a pocket square
            float yBtn = Tu(0.17f).y, yPocket = Tu(0.335f).y;
            float TuxV(Vector3 p) => Mathf.Max(Mathf.Abs(p.x) - (p.y - yBtn) * 0.42f, -p.z);
            float Lapel(Vector3 p)
            {
                if (p.z > 0f) return Mathf.Max(Mathf.Abs(p.x) - (p.y - yBtn) * 0.42f - 0.042f * s * Mathf.Clamp01((p.y - yBtn) / (0.07f * s)), -p.z);
                return Mathf.Max(Tu(0.445f).y - p.y, Mathf.Abs(p.x) - 0.11f * s);
            }
            float Pocket(Vector3 p) => Mathf.Max(Mathf.Max(Mathf.Abs(p.x + 0.088f * s) - 0.021f * s, Mathf.Abs(p.y - yPocket) - 0.01f * s), -p.z);
            // fire coat: a dark closure flap down the front, two lime-and-silver reflective bands round the body
            float yBand1 = Tu(0.25f).y, yBand2 = Tu(-0.005f).y, bandH = 0.018f * s, bandS = 0.0065f * s;
            float Closure(Vector3 p) => Mathf.Max(Mathf.Abs(p.x) - 0.013f * s, -p.z);
            // overalls: a denim bib, straps over the shoulders and straight down the back, a bib pocket
            float yBib = Tu(0.36f).y, bibW = 0.098f * s * Mathf.Lerp(1f, w, 0.5f);
            float Bib(Vector3 p) => Mathf.Max(Mathf.Max(Mathf.Abs(p.x) - bibW, p.y - yBib), -p.z);
            Vector2 sF0 = new Vector2(0.074f * s, yBib), sTop = new Vector2(0.108f * s, yTop + 0.012f * s);
            Vector2 sBR0 = new Vector2(0.108f * s, yTop + 0.012f * s), sBR1 = new Vector2(0.088f * s, yBL), sBL0 = new Vector2(-0.108f * s, yTop + 0.012f * s), sBL1 = new Vector2(-0.088f * s, yBL);
            float Seg2(Vector2 q, Vector2 a0, Vector2 b0) { Vector2 ab = b0 - a0; float h = Mathf.Clamp01(Vector2.Dot(q - a0, ab) / ab.sqrMagnitude); return (q - (a0 + ab * h)).magnitude; }
            float Strap(Vector3 p)
            {
                float d = p.z > 0f ? Seg2(new Vector2(Mathf.Abs(p.x), p.y), sF0, sTop)
                                   : Mathf.Min(Seg2(new Vector2(p.x, p.y), sBR0, sBR1), Seg2(new Vector2(p.x, p.y), sBL0, sBL1));
                return d - 0.017f * s;
            }
            float BibPocket(Vector3 p) => Mathf.Max(Mathf.Max(Mathf.Abs(p.x) - 0.046f * s, Mathf.Abs(p.y - (yBib - 0.058f * s)) - 0.026f * s), -p.z);
            // prints: big plumerias on an aloha shirt, dalmatian spots on the fire crew's jerseys
            Print print = null;
            if (printed)
                print = Print.Scatter(sh, rp, s, TK, aloha ? 0 : 1, b.style * 131 + t.index * 17 + 3, aloha ? yHem : yBL, aloha ? 0.47f : 0.42f, A);
            Func<int, bool> isTorso = l => l == LTorso;
            Func<int, bool> isShirt = l => l == LTorso || l == LArmL || l == LArmR || l == LLegL || l == LLegR;
            // the top of the thighs pokes above the waist at the hips: there it wears whatever the torso wears
            float waist = untucked ? yHem : yBL;

            // gorilla face: a bare patch around the eyes over the muzzle
            Vector3 faceC = Hd(0f, 0.086f, 0f);
            float FaceF(Vector3 p)
            {
                Vector3 q = (p - faceC) / hs;
                return Mathf.Max(new Vector2(q.x / 0.064f, q.y / 0.05f).magnitude - 1f, (0.045f - q.z) * 30f);
            }
            if (ape) m.Cut(FaceF, l => l == LHead);
            // the waist (an untucked top hangs over it; overalls have a waistband all round)
            if (untucked) m.Cut(p => p.y - yHem, isTorso);
            if (!untucked || overalls)
            {
                if (pantsBot || shorts) m.Cut(p => p.y - yBL, isTorso);
                m.Cut(p => p.y - yBH, isTorso);
                if (pantsBot && !untucked) m.Cut(Buckle, isTorso);
            }
            // the top
            if (jerseyTop)
            {
                m.Cut(CollarOuter, isTorso);
                m.Cut(CollarInner, isTorso);
                m.Cut(Placket, isTorso);
            }
            else if (aloha) m.Cut(AlohaV, isTorso);
            else if (tank)
            {
                m.Cut(Scoop, isTorso); m.Cut(p => Scoop(p) - tankTrim, isTorso);
                m.Cut(Armhole, isTorso); m.Cut(p => Armhole(p) - tankTrim, isTorso);
            }
            else if (tux) { m.Cut(TuxV, isTorso); m.Cut(Lapel, isTorso); m.Cut(Pocket, isTorso); }
            else if (coat)
            {
                m.Cut(Closure, isTorso);
                foreach (float yb in new[] { yBand1, yBand2 }) { m.Cut(p => Mathf.Abs(p.y - yb) - bandH, isTorso); m.Cut(p => Mathf.Abs(p.y - yb) - bandS, isTorso); }
            }
            if (overalls) { m.Cut(Bib, isTorso); m.Cut(Strap, isTorso); m.Cut(BibPocket, isTorso); }
            Stat(m, "torso cuts");

            // arms: sleeve ends, trims, cuffs, bands
            var armS = new Vector3[2]; var armE = new Vector3[2]; var armW = new Vector3[2];
            for (int side = 0; side < 2; side++)
            {
                int shb = side == 0 ? 8 : 11;
                float sg = side == 0 ? -1f : 1f;
                armS[side] = A(rp[shb] + new Vector3(-sg * 0.012f, 0.012f, 0f) * s, side);
                armE[side] = A(rp[shb + 1], side);
                armW[side] = A(rp[shb + 2], side);
            }
            float upLen = (armE[0] - armS[0]).magnitude, foreLen = (armW[0] - armE[0]).magnitude;
            float ArmT(Vector3 p, int side) { Vector3 d = armE[side] - armS[side]; return Vector3.Dot(p - armS[side], d) / d.sqrMagnitude; }
            float ForeT(Vector3 p, int side) { Vector3 d = armW[side] - armE[side]; return Vector3.Dot(p - armE[side], d) / d.sqrMagnitude; }
            const float tSleeve = 0.42f, tTrim = 0.47f, tAloha = 0.5f, tCuff = 0.84f, tCuffEnd = 0.93f;
            const float tCoatUp = 0.62f, tCoatFore = 0.5f, tCoatCuff = 0.88f;
            for (int side = 0; side < 2; side++)
            {
                int sd = side, lab = side == 0 ? LArmL : LArmR;
                Func<int, bool> isArm = l => l == lab;
                if (jerseyTop)
                {
                    m.Cut(p => ArmT(p, sd) - tSleeve, isArm);
                    if (!b.sleeves) m.Cut(p => ArmT(p, sd) - tTrim, isArm);
                    else m.Cut(p => ForeT(p, sd) - 0.9f, isArm);
                }
                else if (aloha) m.Cut(p => ArmT(p, sd) - tAloha, isArm);
                else if (tux) { m.Cut(p => ForeT(p, sd) - tCuff, isArm); m.Cut(p => ForeT(p, sd) - tCuffEnd, isArm); }
                else if (coat)
                {
                    m.Cut(p => Mathf.Abs(ArmT(p, sd) - tCoatUp) * upLen - bandH, isArm); m.Cut(p => Mathf.Abs(ArmT(p, sd) - tCoatUp) * upLen - bandS, isArm);
                    m.Cut(p => Mathf.Abs(ForeT(p, sd) - tCoatFore) * foreLen - bandH, isArm); m.Cut(p => Mathf.Abs(ForeT(p, sd) - tCoatFore) * foreLen - bandS, isArm);
                    m.Cut(p => ForeT(p, sd) - tCoatCuff, isArm);
                }
                if (band) m.Cut(p => ForeT(p, sd) - 0.82f, isArm);
            }
            Stat(m, "arm cuts");

            // legs: socks, sock stripes, side piping, hems, hose, cuffs, boot tops
            float ySock(int side)
            {
                int kn = side == 0 ? 15 : 18, ft = kn + 1;
                float yk = rp[kn].y, ya = rp[ft].y - 0.02f * s;
                return Mathf.Lerp(yk, ya, blouse ? 0.36f : 0.9f);
            }
            float BandY(int side, int i) { int kn = side == 0 ? 15 : 18; float yk = rp[kn].y, ya = rp[kn + 1].y - 0.02f * s; return Mathf.Lerp(yk, ya, i == 0 ? 0.53f : 0.65f); }
            float bandW = 0.03f * 0.5f * (rp[15].y - rp[16].y);
            float LegAng(Vector3 p, int side) { int th = side == 0 ? 14 : 17; float sg = side == 0 ? -1f : 1f; return Mathf.Atan2(p.z, (p.x - rp[th].x) * sg); }
            float yAnkle = rp[16].y, yKnee = rp[15].y;
            float yAnkleSock = yAnkle + 0.055f * s;                        // ankle socks under shorts
            float yKiltHide = yKnee + 0.07f * s;                           // the kilt shell's hem hides everything above this
            float yHose = yKnee - 0.035f * s, yHoseFold = yHose - 0.032f * s;   // kilt hose: top, turned-down cuff
            float yCuff = yAnkle + 0.075f * s, yCuffTop = yCuff + 0.035f * s;     // overalls: rolled cuffs over the socks
            float yBoot = yAnkle + 0.17f * s, bootRim = 0.022f * s;
            float yShin = Mathf.Lerp(yKnee, yAnkle, 0.45f);                 // fire coat trousers: a band round the shins
            float Flash(Vector3 p, int side) => Mathf.Max(Mathf.Abs(LegAng(p, side)) - 0.26f, Mathf.Abs(p.y - (yHoseFold - 0.028f * s)) - 0.024f * s);
            for (int side = 0; side < 2; side++)
            {
                int sd = side, lab = side == 0 ? LLegL : LLegR;
                Func<int, bool> isLeg = l => l == lab;
                m.Cut(p => p.y - waist, isLeg);
                if (!untucked || overalls) m.Cut(p => p.y - yBH, isLeg);
                if (boots) { m.Cut(p => p.y - yBoot, isLeg); m.Cut(p => p.y - (yBoot - bootRim), isLeg); }
                if (pantsBot)
                {
                    float ys = ySock(side);
                    if (!boots) m.Cut(p => p.y - ys, isLeg);
                    if (blouse)
                    {
                        float y0 = BandY(side, 0), y1 = BandY(side, 1);
                        m.Cut(p => Mathf.Abs(p.y - y0) - bandW, isLeg);
                        m.Cut(p => Mathf.Abs(p.y - y1) - bandW, isLeg);
                    }
                    if (coat) { m.Cut(p => Mathf.Abs(p.y - yShin) - bandH, isLeg); m.Cut(p => Mathf.Abs(p.y - yShin) - bandS, isLeg); }
                    else m.Cut(p => Mathf.Max(Mathf.Abs(LegAng(p, sd)) - (tux ? 0.11f : 0.07f), ys - p.y), isLeg);
                }
                else if (shorts)
                {
                    m.Cut(p => p.y - yShort, isLeg);
                    m.Cut(p => p.y - (yShort + 0.03f * s), isLeg);
                    m.Cut(p => Mathf.Max(Mathf.Abs(LegAng(p, sd)) - 0.17f, yShort - p.y), isLeg);
                    if (socked && !boots) m.Cut(p => p.y - yAnkleSock, isLeg);
                }
                else if (kilt)
                {
                    m.Cut(p => p.y - yKiltHide, isLeg);
                    if (!boots) { m.Cut(p => p.y - yHose, isLeg); m.Cut(p => p.y - yHoseFold, isLeg); m.Cut(p => Flash(p, sd), isLeg); }
                }
                else if (overalls)
                {
                    m.Cut(p => p.y - yCuffTop, isLeg);
                    if (!boots) m.Cut(p => p.y - yCuff, isLeg);
                }
            }
            Stat(m, "leg cuts");
            // prints last, on the cut-up surface
            if (print != null)
            {
                if (print.kind == 0) m.Cut(print.Leaf, isShirt);
                m.Cut(print.Outer, isShirt);
                if (print.kind == 0) m.Cut(print.Inner, isShirt);
                Stat(m, "print");
            }

            int tj = m.FixTJunctions();
            Stat(m, "tjunctions" + tj);

            // what the hips and legs wear above the hem (torso: sd = -1)
            Color BottomCloth(Vector3 p, int sd, ref float R)
            {
                if (kilt) return dark;
                if (overalls) return cw.denim;
                if (shorts) return cw.shorts;
                if (tux) return cw.tux;
                if (coat) return cw.coat;
                if (pantStripes) R = sd < 0 ? 46f : 28f;
                return pants;
            }
            Color PrintAt(Vector3 p, Color ground)
            {
                int layer = print.Layer(p, out int fi);
                if (layer == 0) return ground;
                if (print.kind == 1) return cw.spot;
                if (layer == 2) return cw.pistil;
                if (layer == 3) return cw.leaf;
                return (fi & 1) == 0 ? cw.petalA : cw.petalB;
            }
            Color TorsoCol(Vector3 p, Vector3 n, ref float R)
            {
                if (p.y < (untucked ? yHem : yBL)) return BottomCloth(p, -1, ref R);
                if (overalls && (p.y < yBH || Bib(p) < 0f || Strap(p) < 0f)) return BibPocket(p) < 0f ? cw.denimDark : cw.denim;
                if (!untucked && p.y < yBH)
                {
                    if (kilt) return dark;
                    if (shorts) return cw.shortsBand;
                    return Buckle(p) < 0f ? buckle : belt;
                }
                if (aloha) return AlohaV(p) < 0f ? bare : PrintAt(p, cw.aloha);
                if (tank)
                {
                    float sc = Scoop(p), ah = Armhole(p);
                    if (sc < 0f || ah < 0f) return bare;
                    return sc < tankTrim || ah < tankTrim ? cw.tankTrim : cw.tank;
                }
                if (tux)
                {
                    if (TuxV(p) < 0f) return cw.shirt;
                    if (Lapel(p) < 0f) return cw.lapel;
                    return Pocket(p) < 0f ? cw.shirt : cw.tux;
                }
                if (coat)
                {
                    float d1 = Mathf.Abs(p.y - yBand1), d2 = Mathf.Abs(p.y - yBand2), d = Mathf.Min(d1, d2);
                    if (d < bandS) return cw.silver;
                    if (d < bandH) return cw.lime;
                    return Closure(p) < 0f ? cw.coatDark : cw.coat;
                }
                // the jersey
                if (CollarInner(p) < 0f) return under;
                if (CollarOuter(p) < 0f) return trim;
                if (Placket(p) < 0f) return pipe;
                if (shirtStripes && n.y < 0.5f) R = 46f;
                return spotted ? PrintAt(p, jersey) : jersey;
            }
            Color ArmCol(Vector3 p, int sd, ref float R)
            {
                float ta = ArmT(p, sd), tf = ForeT(p, sd);
                if (aloha) return ta < tAloha ? PrintAt(p, cw.aloha) : bare;
                if (tux) return tf < tCuff ? cw.tux : tf < tCuffEnd ? cw.shirt : bare;
                if (coat)
                {
                    float d = Mathf.Min(Mathf.Abs(ta - tCoatUp) * upLen, Mathf.Abs(tf - tCoatFore) * foreLen);
                    if (d < bandS) return cw.silver;
                    if (d < bandH) return cw.lime;
                    return tf > tCoatCuff ? cw.coatDark : cw.coat;
                }
                if (tank) return band && tf > 0.82f ? wrist : bare;
                if (ta < tSleeve) { if (shirtStripes) R = 18f; return spotted ? PrintAt(p, jersey) : jersey; }
                if (!b.sleeves && ta < tTrim) return trim;
                if (band && tf > 0.82f) return wrist;
                if (b.sleeves && tf < 0.9f) return under;
                return bare;
            }
            Color LegCol(Vector3 p, int sd, ref float R)
            {
                if (boots && p.y < yBoot) return p.y > yBoot - bootRim ? cw.bootRim : cw.boot;
                if (shorts)
                {
                    if (p.y > yShort)
                    {
                        if (Mathf.Abs(LegAng(p, sd)) < 0.17f || p.y < yShort + 0.03f * s) return cw.shortsStripe;
                        return cw.shorts;
                    }
                    return socked && p.y < yAnkleSock ? cw.sock : bare;
                }
                if (kilt)
                {
                    if (p.y > yKiltHide) return dark;
                    if (p.y > yHose) return bare;
                    if (p.y > yHoseFold) return cw.hoseFold;
                    return Flash(p, sd) < 0f ? cw.flash : cw.hose;
                }
                if (overalls)
                {
                    if (p.y > yCuffTop) return cw.denim;
                    if (p.y > yCuff) return cw.denimLight;
                    return socked ? sock : bare;
                }
                // trousers
                float ys = ySock(sd);
                if (p.y < ys)
                {
                    if (!socked && !ape) return bare;
                    if (blouse && (Mathf.Abs(p.y - BandY(sd, 0)) < bandW || Mathf.Abs(p.y - BandY(sd, 1)) < bandW)) return sockBand;
                    return tux ? cw.lapel : coat ? cw.coatDark : sock;
                }
                if (coat)
                {
                    float d = Mathf.Abs(p.y - yShin);
                    if (d < bandS) return cw.silver;
                    return d < bandH ? cw.lime : cw.coat;
                }
                if (Mathf.Abs(LegAng(p, sd)) < (tux ? 0.11f : 0.07f)) return tux ? cw.lapel : pipe;
                return BottomCloth(p, sd, ref R);
            }

            // ---- final colours, stripes, skin weights
            int nv = m.P.Count;
            var col = new Color[nv];
            var uv = new Vector2[nv];
            var rep = new float[nv];
            var inkW = new float[nv];
            var bw = new BoneWeight[nv];
            var pos = new Vector3[nv];
            var nrm = new Vector3[nv];
            Vector3 blushL = Hd(-0.05f, 0.056f, 0.074f), blushR = Hd(0.05f, 0.056f, 0.074f);
            float blushR0 = 0.02f * hs;
            Vector3 chinC = Hd(0f, 0.035f, 0.07f);
            var armAxis = new Vector3[2]; var armRef = new Vector3[2];
            for (int side = 0; side < 2; side++)
            {
                armAxis[side] = (armE[side] - armS[side]).normalized;
                Vector3 r0 = Vector3.forward - armAxis[side] * Vector3.Dot(Vector3.forward, armAxis[side]);
                armRef[side] = r0.normalized;
            }
            var P = sh.P;
            var wacc = new float[nv * 20];
            Parallel.For(0, nv, vi =>
            {
                Vector3 p = m.P[vi] + m.Bias[vi];
                int lab = m.L[vi];
                Color c = skin;
                Vector2 u = MeshKit.Plain;
                float R = 0f;
                switch (lab)
                {
                    case LTorso:
                        c = TorsoCol(p, m.N[vi], ref R);
                        if (R > 0f) u = new Vector2((Mathf.Atan2(p.x, p.z - 0.01f * s) / (2f * Mathf.PI) + 0.5f) * R, 0f);
                        break;
                    case LArmL:
                    case LArmR:
                    {
                        int sd = lab == LArmL ? 0 : 1;
                        c = ArmCol(p, sd, ref R);
                        if (R > 0f)
                        {
                            Vector3 v = p - armS[sd];
                            Vector3 e2 = Vector3.Cross(armAxis[sd], armRef[sd]);
                            u = new Vector2((Mathf.Atan2(Vector3.Dot(v, e2), Vector3.Dot(v, armRef[sd])) / (2f * Mathf.PI) + 0.5f) * R, 0f);
                        }
                        break;
                    }
                    case LLegL:
                    case LLegR:
                    {
                        int sd = lab == LLegL ? 0 : 1;
                        if (p.y > waist)
                        {
                            c = TorsoCol(p, m.N[vi], ref R);
                            if (R > 0f) u = new Vector2((Mathf.Atan2(p.x, p.z - 0.01f * s) / (2f * Mathf.PI) + 0.5f) * R, 0f);
                            break;
                        }
                        c = LegCol(p, sd, ref R);
                        if (R > 0f)
                        {
                            int th = sd == 0 ? 14 : 17;
                            u = new Vector2((Mathf.Atan2(p.x - rp[th].x, p.z) / (2f * Mathf.PI) + 0.5f) * R, 0f);
                        }
                        break;
                    }
                    case LShoeL:
                    case LShoeR:
                        c = ape ? apeFace : shoe;
                        break;
                    case LMuzzle:
                        c = muzzle;
                        break;
                    case LHead when ape:
                        c = FaceF(p) < 0f ? apeFace : fur;
                        break;
                    case LNeck when ape:
                        c = fur;
                        break;
                    case LHandL when ape:
                    case LHandR when ape:
                        c = apeFace;
                        break;
                    case LHead:
                    {
                        float bl = Mathf.Min((p - blushL).magnitude, (p - blushR).magnitude);
                        float wb = 1f - Smooth01(bl / blushR0);
                        c = Color.Lerp(skin, blushC, wb * 0.75f);
                        if (b.beard == Beard.Stubble)
                        {
                            Vector3 q = p - chinC;
                            float sdist = new Vector3(q.x / (0.07f * hs), q.y / (0.045f * hs), Mathf.Max(0f, q.z) / (0.08f * hs)).magnitude;
                            float ws = (1f - Smooth01((sdist - 0.7f) / 0.3f)) * Smooth01((rp[4].y + 0.058f * hs - p.y) / (0.01f * hs));
                            c = Color.Lerp(c, stubbleC, ws);
                        }
                        c.a = skin.a;
                        break;
                    }
                    default:
                        c = skin;
                        break;
                }
                // ambient occlusion straight from the distance field: creases (collar, armpits, crotch, under the chin) darken
                float occl = Occlusion(sh, p, m.N[vi], 0.016f * s, 0.42f);
                c.r *= occl; c.g *= occl; c.b *= occl;
                // no ink hull in creases (neck into shoulders, armpits, crotch): there it would poke through the surface as black
                // cracks; silhouettes are convex, so they keep their full line
                inkW[vi] = 1f - Smooth01(((1f - occl) / 0.42f - 0.12f) / 0.3f);
                // nor round the base of the neck: its hull would cut into the tops of the shoulders
                if (lab == LNeck) inkW[vi] *= Smooth01((p.y - Tu(0.43f).y) / (0.06f * s));
                col[vi] = c;
                uv[vi] = u;
                rep[vi] = R;

                // skin weights: the nearest primitives of the same limb share the vertex, each spreads along its own bones
                var mk = m.MaskOf(vi);
                int grp = SkinGroup(lab);
                float dmin = SdfShape.Big;
                for (int i = 0; i < P.Length; i++) { if (!mk[i] || SkinGroup(P[i].label) != grp) continue; float d = SdfShape.Dist(ref P[i], p); if (d < dmin) dmin = d; }
                float sig = 0.013f * s;
                int ao = vi * 20;
                for (int i = 0; i < P.Length; i++)
                {
                    if (!mk[i] || SkinGroup(P[i].label) != grp) continue;
                    float x = (SdfShape.Dist(ref P[i], p) - dmin) / sig;
                    if (x > 6f) continue;
                    float wt = Mathf.Exp(-x);
                    if (P[i].bone0 == -2) TorsoBones((p.y - hips.y) / (s * TK), wt, wacc, ao);
                    else if (P[i].bone0 == P[i].bone1) wacc[ao + P[i].bone0] += wt;
                    else
                    {
                        float tt = Vector3.Dot(p - P[i].a, P[i].ba) * P[i].il2;
                        float f = Smooth01((tt - P[i].t0) / Mathf.Max(1e-4f, P[i].t1 - P[i].t0));
                        wacc[ao + P[i].bone0] += wt * (1f - f);
                        wacc[ao + P[i].bone1] += wt * f;
                    }
                }
                float tot = 0f;
                for (int j = 0; j < 20; j++) tot += wacc[ao + j];
                if (tot > 0f) for (int j = 0; j < 20; j++) wacc[ao + j] /= tot;
                else wacc[ao] = 1f;
            });

            // soften the weights over the surface (welded across colour borders): joints bend without creases or folds
            SmoothWeights(m, wacc, 10);

            // shoulders, elbows and knees: re-blend across a band about as wide as the limb is thick, centred on the joint, so they
            // bend in a curve instead of folding like paper (the primitives' own blends are only a few centimetres wide)
            float elW = 0.045f * s * (ape ? 1.25f : 1f), knW = 0.058f * s;
            var foreDir = new Vector3[2];
            for (int side = 0; side < 2; side++) foreDir[side] = (armW[side] - armE[side]).normalized;
            Vector3 shinL = (rp[16] - rp[15]).normalized, shinR = (rp[19] - rp[18]).normalized;
            Parallel.For(0, nv, vi =>
            {
                var acc = new float[PlayerMesh.BoneCount];
                System.Array.Copy(wacc, vi * 20, acc, 0, 20);
                int lb = m.L[vi];
                Vector3 p0 = m.P[vi];
                if (lb == LArmL || lb == LArmR)
                {
                    // (the shoulder keeps its narrow blend: the sleeve turns on it like a ball joint, which stays clean even with
                    // the arm thrown back or straight overhead, where a wide blend smears the sleeve)
                    int sd = lb == LArmL ? 0 : 1, shb = sd == 0 ? 8 : 11;
                    Rebalance(acc, shb, shb + 1, Vector3.Dot(p0 - armE[sd], foreDir[sd]), elW);
                }
                else if (lb == LLegL) Rebalance(acc, 14, 15, Vector3.Dot(p0 - rp[15], shinL), knW);
                else if (lb == LLegR) Rebalance(acc, 17, 18, Vector3.Dot(p0 - rp[18], shinR), knW);
                SplitHelpers(acc);
                bw[vi] = Top4(acc);
                pos[vi] = m.P[vi];
                nrm[vi] = m.N[vi];
            });

            // ---- emit (pinstripe seams get their own copies so a triangle never wraps the whole stripe range)
            int baseIdx = k.V.Count;
            for (int i = 0; i < nv; i++)
            {
                k.V.Add(pos[i]); k.N.Add(nrm[i]); k.C.Add(col[i]); k.UV.Add(uv[i]); k.UV2.Add(Vector2.zero); k.OL.Add(inkW[i]); k.W.Add(bw[i]);
            }
            var T = m.T;
            var hidden = UnderCap(b, rp, pos);
            for (int i = 0; i < T.Count; i += 3)
            {
                int a = T[i], bb = T[i + 1], c = T[i + 2];
                if (hidden[a] && hidden[bb] && hidden[c]) continue;
                float R = rep[a];
                if (R > 0f && rep[bb] == R && rep[c] == R)
                {
                    float u0 = uv[a].x, u1 = uv[bb].x, u2 = uv[c].x;
                    float lo = Mathf.Min(u0, Mathf.Min(u1, u2)), hi = Mathf.Max(u0, Mathf.Max(u1, u2));
                    if (hi - lo > R * 0.5f)
                    {
                        k.T.Add(Wrap(k, baseIdx, a, R)); k.T.Add(Wrap(k, baseIdx, bb, R)); k.T.Add(Wrap(k, baseIdx, c, R));
                        continue;
                    }
                }
                k.T.Add(baseIdx + a); k.T.Add(baseIdx + bb); k.T.Add(baseIdx + c);
            }

            // ---- where the jersey text, buttons and costume studs sit on this body
            var info = new Info { shape = sh, headCenter = Hd(0f, 0.1f, 0f) };
            float yNum = rp[2].y + 0.09f * s * TK, yNick = rp[2].y + 0.075f * s * TK;
            float back = 1f, front = -1f;
            for (int i = -1; i <= 1; i++)
            {
                back = Mathf.Min(back, sh.March(new Vector3(i * 0.07f * s, yNum, -0.8f), Vector3.forward, 1.6f).z);
                front = Mathf.Max(front, sh.March(new Vector3(i * 0.065f * s, yNick, 0.8f), Vector3.back, 1.6f).z);
            }
            info.backZ = back - rp[2].z - 0.004f * s;
            info.frontZ = front - rp[2].z + 0.004f * s;
            Vector3 OnFront(float x, float y) => sh.March(new Vector3(x, y, 0.8f), Vector3.back, 1.6f);
            void Stud(Vector3 p, Color c, float r) { info.studs.Add(new Vector4(p.x, p.y, p.z, r)); info.studC.Add(c); }
            if (jerseyTop)
                for (int i = 0; i < 4; i++)
                {
                    float y = Mathf.Lerp(yBH + 0.035f * s, yV - 0.012f * s, i / 3f);
                    if (overalls && y < yBib + 0.01f * s) continue;   // under the bib
                    info.buttons.Add(OnFront(0f, y));
                }
            if (aloha)
                for (int i = 0; i < 3; i++) Stud(OnFront(0f, Mathf.Lerp(yHem + 0.05f * s, yVa - 0.012f * s, i / 2f)), cw.button, 0.0072f * s);
            if (tux)
            {
                Stud(OnFront(0f, yBtn - 0.014f * s), cw.lapel, 0.009f * s);
                for (int i = 0; i < 2; i++) Stud(OnFront(0f, Mathf.Lerp(yBtn + 0.04f * s, Tu(0.39f).y, i)), Pal.Gloss(new Color(0.08f, 0.08f, 0.1f), 0.6f), 0.0058f * s);
            }
            if (coat)
                for (int i = 0; i < 3; i++) Stud(OnFront(0.024f * s, Mathf.Lerp(yHem + 0.06f * s, Tu(0.4f).y, i / 2f)), Pal.Gloss(new Color(0.2f, 0.2f, 0.22f), 0.7f), 0.0085f * s);
            if (overalls)
                for (int e = -1; e <= 1; e += 2) Stud(OnFront(e * 0.074f * s, yBib - 0.012f * s), Pal.Gloss(new Color(0.86f, 0.68f, 0.28f), 0.85f), 0.011f * s);
            if (shorts && !untucked)
                for (int e = -1; e <= 1; e += 2) Stud(OnFront(e * 0.014f * s, yBM - 0.004f * s), Pal.Gloss(Color.white, 0.3f), 0.0065f * s);
            info.seat = sh.March(new Vector3(0f, hips.y - 0.012f * s, -0.8f), Vector3.forward, 1.6f);
            info.verts = nv;
            info.coarse = coarse;
            info.skinP = m.P;
            info.skinL = m.L;
            info.skinW = bw;
            info.ms = (float)watch.Elapsed.TotalMilliseconds;
            return info;
        }

        /// <summary>Sphere-trace a sub-shape (only the primitives in the mask).</summary>
        static Vector3 MarchMask(SdfShape sh, bool[] mask, Vector3 from, Vector3 dir, float maxDist)
        {
            sh.Bake();
            dir.Normalize();
            float t = 0f;
            for (int i = 0; i < 160; i++)
            {
                Vector3 p = from + dir * t;
                float d = sh.Eval(p, mask);
                if (d < 1e-4f) return p;
                t += Mathf.Max(d * 0.8f, 1e-4f);
                if (t > maxDist) break;
            }
            return from + dir * Mathf.Min(t, maxDist);
        }

        static bool[] Mask(SdfShape sh, int label)
        {
            sh.Bake();
            var mk = new bool[sh.P.Length];
            for (int i = 0; i < mk.Length; i++) mk[i] = sh.P[i].label == label;
            return mk;
        }

        // ------------------------------------------------------------------ costumes

        /// <summary>Costume colours for one player: the club's colours turned by his style number into a colourway.</summary>
        public sealed class Wardrobe
        {
            public Color aloha, petalA, petalB, pistil, leaf, button, spot;
            public Color tank, tankTrim;
            public Color tux, lapel, shirt, bowtie;
            public Color coat, coatDark, lime, silver;
            public Color shorts, shortsStripe, shortsBand, sock;
            public Color hose, hoseFold, flash;
            public Color denim, denimDark, denimLight;
            public Color boot, bootRim, bootSole;
            public Color kilt, kiltDark, kiltLine, kiltBelt, buckle;
            public Color ring, ringStripe, duck, beak, wing, wingBand;
            public Color bandana, bandanaDot, snorkel, flipSole, flipStrap;
            public Color[] lei;
            public bool fireBoots, duckRing;

            public static float Lum(Color c) => 0.3f * c.r + 0.55f * c.g + 0.15f * c.b;
            static Color C(Color c) => Pal.Gloss(c, 0.14f);

            public static Wardrobe Of(Body b, TeamData t)
            {
                int st = Mathf.Abs(b.style);
                var w = new Wardrobe();
                // aloha shirt: a team colour ground under big plumerias and leaves
                Color ground = st % 2 == 0 ? t.main : (Lum(t.dark) < 0.12f ? Color.Lerp(t.dark, t.accent, 0.3f) : t.dark);
                bool light = Lum(ground) > 0.5f;
                Color cream = new Color(0.98f, 0.97f, 0.93f);
                w.aloha = C(ground);
                w.petalA = C(light ? Pal.Hex(0xE5413A) : cream);
                bool warmMain = Lum(t.main) > 0.5f && t.main.r > t.main.b + 0.2f;
                w.petalB = C(light ? cream : warmMain ? t.main : Pal.Hex(0xFF8576));
                w.pistil = C(light ? Pal.Hex(0xFF9F1C) : Pal.Hex(0xFFC83A));
                w.leaf = C(Lum(ground) > 0.35f ? Pal.Hex(0x1E7A47) : Pal.Hex(0x3CC07A));
                w.button = Pal.Gloss(light ? new Color(0.45f, 0.28f, 0.16f) : cream, 0.5f);
                w.spot = Pal.Gloss(new Color(0.07f, 0.07f, 0.08f), 0.1f);
                // tank top in a team colour with a contrasting trim
                Color tk = st % 2 == 0 ? t.main : t.accent, tkT = st % 2 == 0 ? t.accent : t.main;
                if (Mathf.Abs(Lum(tk) - Lum(tkT)) < 0.15f) tkT = Lum(tk) > 0.5f ? t.dark : Color.white;
                w.tank = C(tk);
                w.tankTrim = C(tkT);
                // the tuxedo in the club colour (a yellow tux for the Bananas), satin lapels, a white shirt
                w.tux = Pal.Gloss(t.main, 0.3f);
                w.lapel = Pal.Gloss(Lum(t.main) < 0.2f ? t.accent : new Color(0.08f, 0.08f, 0.1f), 0.6f);
                w.shirt = C(new Color(0.97f, 0.97f, 0.95f));
                w.bowtie = Pal.Gloss(st % 2 == 0 ? new Color(0.08f, 0.08f, 0.1f) : t.accent, 0.6f);
                // fire coat: tan or black turnout cloth, lime and silver reflective bands
                bool tan = st % 3 != 2;
                w.coat = C(tan ? Pal.Hex(0xC9A55E) : Pal.Hex(0x2D2E33));
                w.coatDark = C(tan ? Pal.Hex(0x8A6B35) : Pal.Hex(0x18191C));
                w.lime = Pal.Gloss(Pal.Hex(0xD8F02A), 0.3f);
                w.silver = Pal.Gloss(Pal.Hex(0xDDE2E7), 0.85f);
                // board shorts: the team colour that stands off the top best, a stripe down the side
                Color topC = b.top == Top.Aloha ? ground : b.top == Top.Tank ? tk : t.jersey;
                Color[] cands = { t.accent, t.dark, t.main, Color.white };
                Color sc = cands[0]; float best = -1f;
                for (int i = 0; i < cands.Length; i++)
                {
                    Color c = cands[(i + st) % cands.Length];
                    float d = Mathf.Abs(Lum(c) - Lum(topC));
                    if (d > 0.25f) { sc = c; best = d; break; }
                    if (d > best) { best = d; sc = c; }
                }
                w.shorts = C(sc);
                w.shortsStripe = C(Lum(sc) > 0.5f ? (Lum(t.dark) < 0.3f ? t.dark : new Color(0.1f, 0.1f, 0.12f)) : (Lum(t.main) > 0.5f ? t.main : Color.white));
                w.shortsBand = C(Color.Lerp(sc, Color.black, 0.22f));
                w.sock = C(new Color(0.95f, 0.95f, 0.93f));
                // kilt hose: cream, folded down in the team colour, with garter flashes
                w.hose = C(new Color(0.94f, 0.91f, 0.83f));
                w.hoseFold = C(t.main);
                w.flash = C(Lum(t.accent) < 0.4f || Mathf.Abs(Lum(t.accent) - 0.9f) > 0.2f ? t.accent : t.dark);
                // denim
                w.denim = C(Pal.Hex(0x3B62A6));
                w.denimDark = C(Pal.Hex(0x2B4C88));
                w.denimLight = C(Pal.Hex(0x7898CF));
                // boots: firefighter black with a band, or tan cowboy leather
                w.fireBoots = b.top == Top.FireCoat || b.hat == Hat.Fire;
                w.boot = Pal.Gloss(w.fireBoots ? Pal.Hex(0x1E1F24) : (st % 2 == 0 ? Pal.Hex(0x80502A) : Pal.Hex(0x3A2416)), 0.55f);
                w.bootRim = Pal.Gloss(w.fireBoots ? Pal.Hex(0xE5C21E) : Pal.Hex(0xC08A4E), 0.4f);
                w.bootSole = Pal.Gloss(Pal.Hex(0x2A1A10), 0.3f);
                // tartan: the team colour crossed by dark bands and a thin bright line
                w.kilt = C(t.main);
                w.kiltDark = C(Lum(t.dark) < 0.25f ? t.dark : Color.Lerp(t.dark, Color.black, 0.5f));
                w.kiltLine = C(Lum(t.main) > 0.6f ? Pal.Hex(0xD9302B) : new Color(0.95f, 0.95f, 0.92f));
                w.kiltBelt = Pal.Gloss(Pal.Hex(0x3A2414), 0.45f);
                w.buckle = Pal.Gloss(Pal.Hex(0xE0C060), 0.9f);
                // pool toys
                w.duckRing = st % 2 == 1;
                w.ring = Pal.Gloss(w.duckRing ? Pal.Hex(0xFFD21F) : Pal.Hex(0xE8433A), 0.7f);
                w.ringStripe = Pal.Gloss(w.duckRing ? Pal.Hex(0xFFE873) : new Color(0.98f, 0.97f, 0.95f), 0.7f);
                w.duck = Pal.Gloss(Pal.Hex(0xFFD21F), 0.6f);
                w.beak = Pal.Gloss(Pal.Hex(0xFF8A1E), 0.6f);
                w.wing = Pal.Gloss(Pal.Hex(0xFF8A1E), 0.7f);
                w.wingBand = Pal.Gloss(Lum(t.main) > 0.5f ? t.main : Pal.Hex(0xFFE36B), 0.7f);
                // hot pink, white, purple and orange: they stand off a yellow, navy or teal shirt alike
                w.lei = new[] { Pal.Gloss(Pal.Hex(0xFF3D8E), 0.25f), Pal.Gloss(Color.white, 0.25f), Pal.Gloss(Pal.Hex(0xA552E8), 0.25f), Pal.Gloss(Pal.Hex(0xFF7A1F), 0.25f), Pal.Gloss(Pal.Hex(0xFFD23F), 0.25f) };
                w.bandana = C(st % 2 == 0 ? Pal.Hex(0xD7322E) : Pal.Hex(0x23407A));
                w.bandanaDot = C(cream);
                w.snorkel = Pal.Gloss(Lum(t.main) > 0.45f ? t.main : Pal.Hex(0xFFD400), 0.6f);
                w.flipSole = Pal.Gloss(st % 2 == 0 ? t.main : Pal.Hex(0x2DB4E6), 0.35f);
                w.flipStrap = Pal.Gloss(Lum(t.main) > 0.5f ? t.dark : Color.white, 0.4f);
                return w;
            }
        }

        /// <summary>A printed pattern on the shirt: motifs scattered over the torso and the short sleeves, each a small shape in its
        /// own tangent plane (kind 0: plumerias with a dot in the middle and two leaves, kind 1: dalmatian spots). Its borders are cut
        /// into the mesh like every other paint line, so the print stays crisp at any distance, and the layout comes from the shape
        /// alone, so the near and far bodies wear the same print.</summary>
        public sealed class Print
        {
            struct Motif { public Vector3 c, n, u, v; public float r, ph; }
            public readonly int kind;
            readonly List<Motif> list = new List<Motif>();
            readonly Dictionary<long, List<int>> grid = new Dictionary<long, List<int>>();
            readonly float cell;
            Print(int kind, float cell) { this.kind = kind; this.cell = cell; }

            static long Key(int x, int y, int z) => ((long)(x + 8192) << 28) | ((long)(y + 8192) << 14) | (long)(z + 8192);

            void Add(Motif mo)
            {
                int id = list.Count;
                list.Add(mo);
                float reach = mo.r * (kind == 0 ? 1.62f : 1.35f);
                int x0 = Mathf.FloorToInt((mo.c.x - reach) / cell), x1 = Mathf.FloorToInt((mo.c.x + reach) / cell);
                int y0 = Mathf.FloorToInt((mo.c.y - reach) / cell), y1 = Mathf.FloorToInt((mo.c.y + reach) / cell);
                int z0 = Mathf.FloorToInt((mo.c.z - reach) / cell), z1 = Mathf.FloorToInt((mo.c.z + reach) / cell);
                for (int z = z0; z <= z1; z++)
                    for (int y = y0; y <= y1; y++)
                        for (int x = x0; x <= x1; x++)
                        {
                            long key = Key(x, y, z);
                            if (!grid.TryGetValue(key, out var l)) grid[key] = l = new List<int>(4);
                            l.Add(id);
                        }
            }

            List<int> Near(Vector3 p)
            {
                grid.TryGetValue(Key(Mathf.FloorToInt(p.x / cell), Mathf.FloorToInt(p.y / cell), Mathf.FloorToInt(p.z / cell)), out var l);
                return l;
            }

            void Local(int i, Vector3 p, out Vector2 q, out float z)
            {
                Motif mo = list[i];
                Vector3 d = p - mo.c;
                q = new Vector2(Vector3.Dot(d, mo.u), Vector3.Dot(d, mo.v));
                z = Mathf.Abs(Vector3.Dot(d, mo.n)) - 0.45f * mo.r;
            }

            /// <summary>Five round petals round a full middle (a plumeria), turned by ph.</summary>
            static float PetalD(Vector2 q, float r, float ph)
            {
                float d = q.magnitude - 0.42f * r;
                for (int i = 0; i < 5; i++)
                {
                    float a = ph + i * 1.2566371f;
                    d = Mathf.Min(d, (q - new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (0.55f * r)).magnitude - 0.4f * r);
                }
                return d;
            }

            /// <summary>Two pointed leaves behind the flower, between its petals.</summary>
            static float LeafD(Vector2 q, float r, float ph)
            {
                float d = 1f;
                float L = 1.02f * r, W = 0.46f * r, rho = (L * L * 0.25f + W * W * 0.25f) / W, off = rho - W * 0.5f;
                for (int i = 0; i < 2; i++)
                {
                    float a = ph + 0.6283185f + i * Mathf.PI * 0.94f;
                    Vector2 dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a)), nor = new Vector2(-dir.y, dir.x);
                    float x = Vector2.Dot(q, dir) - 0.48f * r - L * 0.5f, y = Vector2.Dot(q, nor);
                    float d1 = new Vector2(x, y + off).magnitude - rho, d2 = new Vector2(x, y - off).magnitude - rho;
                    d = Mathf.Min(d, Mathf.Max(d1, d2));
                }
                return d;
            }

            /// <summary>A lumpy round spot.</summary>
            static float SpotD(Vector2 q, float r, float ph)
            {
                float a = Mathf.Atan2(q.y, q.x);
                return q.magnitude - r * (1f + 0.16f * Mathf.Sin(3f * a + ph) + 0.07f * Mathf.Sin(5f * a + 2.3f * ph));
            }

            float Field(Vector3 p, int which)
            {
                var l = Near(p);
                if (l == null) return 1f;
                float d = 1f;
                foreach (int i in l)
                {
                    Local(i, p, out Vector2 q, out float z);
                    Motif mo = list[i];
                    float f = which == 0 ? (kind == 0 ? PetalD(q, mo.r, mo.ph) : SpotD(q, mo.r, mo.ph)) : which == 1 ? q.magnitude - 0.16f * mo.r : LeafD(q, mo.r, mo.ph);
                    d = Mathf.Min(d, Mathf.Max(f, z));
                }
                return d;
            }

            public float Outer(Vector3 p) => Field(p, 0);
            public float Inner(Vector3 p) => Field(p, 1);
            public float Leaf(Vector3 p) => Field(p, 2);

            /// <summary>0: ground, 1: petal (or spot), 2: the flower's middle, 3: leaf. which: the motif (petal colours alternate).</summary>
            public int Layer(Vector3 p, out int which)
            {
                which = -1;
                var l = Near(p);
                if (l == null) return 0;
                int layer = 0;
                foreach (int i in l)
                {
                    Local(i, p, out Vector2 q, out float z);
                    if (z >= 0f) continue;
                    Motif mo = list[i];
                    if (kind == 1) { if (SpotD(q, mo.r, mo.ph) < 0f) { which = i; return 1; } continue; }
                    if (q.magnitude < 0.16f * mo.r) { which = i; return 2; }
                    if (PetalD(q, mo.r, mo.ph) < 0f) { which = i; return 1; }
                    if (layer == 0 && LeafD(q, mo.r, mo.ph) < 0f) { layer = 3; which = i; }
                }
                return layer;
            }

            /// <summary>Scatter motifs over the shirt: candidate spots from rays at the torso (and down onto the shoulders) and round
            /// each upper arm, then in a shuffled order keep every one that clears those already kept.</summary>
            public static Print Scatter(SdfShape sh, Vector3[] rp, float s, float TK, int kind, int seed, float yLow, float tSleeve, Func<Vector3, int, Vector3> A)
            {
                sh.Bake();
                float r = (kind == 0 ? 0.045f : 0.027f) * s, gap = (kind == 0 ? 2.75f : 2.6f) * r;
                var pr = new Print(kind, gap);
                var rnd = new System.Random(seed);
                float Rnd() => (float)rnd.NextDouble();
                var cand = new List<Motif>();
                var torso = Mask(sh, LTorso);
                Motif Frame(bool[] mk, Vector3 p, float rr)
                {
                    Vector3 n = sh.Grad(p, mk, 0.002f * s).normalized;
                    Vector3 u = Vector3.Cross(Vector3.up, n);
                    if (u.sqrMagnitude < 0.05f) u = Vector3.Cross(Vector3.forward, n);
                    u.Normalize();
                    return new Motif { c = p, n = n, u = u, v = Vector3.Cross(n, u), r = rr, ph = Rnd() * 6.2831853f };
                }
                float Size() => kind == 0 ? r * (0.88f + 0.24f * Rnd()) : r * (0.6f + 0.8f * Rnd());
                Vector3 hips = rp[0];
                float yHigh = hips.y + 0.5f * s * TK, step = gap * 0.6f;
                for (float y = yLow + r * 0.3f; y < yHigh; y += step)
                {
                    int na = Mathf.Max(6, Mathf.RoundToInt(2f * Mathf.PI * 0.18f * s / step));
                    float off = Rnd();
                    for (int i = 0; i < na; i++)
                    {
                        float ang = (i + off + (Rnd() - 0.5f) * 0.6f) / na * Mathf.PI * 2f;
                        Vector3 dir = new Vector3(Mathf.Sin(ang), 0f, Mathf.Cos(ang));
                        Vector3 o = new Vector3(0f, y + (Rnd() - 0.5f) * step * 0.6f, -0.01f * s) + dir * 0.7f;
                        Vector3 hit = MarchMask(sh, torso, o, -dir, 0.75f);
                        if (sh.Eval(hit, torso) > 0.002f) continue;   // missed
                        cand.Add(Frame(torso, hit, Size()));
                    }
                }
                // the tops of the shoulders, from above
                for (int ix = -2; ix <= 2; ix++)
                    for (int iz = -1; iz <= 1; iz++)
                    {
                        Vector3 o = new Vector3(ix * 0.075f * s + (Rnd() - 0.5f) * 0.03f * s, hips.y + 0.9f * s, iz * 0.045f * s);
                        Vector3 hit = MarchMask(sh, torso, o, Vector3.down, 0.6f * s);
                        if (sh.Eval(hit, torso) > 0.002f || hit.y < yHigh - 0.08f * s) continue;
                        cand.Add(Frame(torso, hit, Size()));
                    }
                // short sleeves
                for (int side = 0; side < 2 && tSleeve > 0f; side++)
                {
                    int shb = side == 0 ? 8 : 11;
                    float sg = side == 0 ? -1f : 1f;
                    var arm = Mask(sh, side == 0 ? LArmL : LArmR);
                    Vector3 aS = A(rp[shb] + new Vector3(-sg * 0.012f, 0.012f, 0f) * s, side), aE = A(rp[shb + 1], side);
                    Vector3 ax = (aE - aS).normalized;
                    Vector3 e1 = Vector3.Cross(ax, Vector3.forward).normalized, e2 = Vector3.Cross(ax, e1);
                    float len = (aE - aS).magnitude;
                    for (float tt = 0.1f; tt < tSleeve; tt += step * 0.8f / len)
                    {
                        float off = Rnd();
                        for (int i = 0; i < 5; i++)
                        {
                            float ang = (i + off) / 5f * Mathf.PI * 2f;
                            Vector3 dir = e1 * Mathf.Cos(ang) + e2 * Mathf.Sin(ang);
                            Vector3 o = aS + ax * (tt * len) + dir * 0.3f;
                            Vector3 hit = MarchMask(sh, arm, o, -dir, 0.32f);
                            if (sh.Eval(hit, arm) > 0.002f) continue;
                            cand.Add(Frame(arm, hit, Size() * 0.85f));
                        }
                    }
                }
                for (int i = cand.Count - 1; i > 0; i--) { int j = rnd.Next(i + 1); var tmp = cand[i]; cand[i] = cand[j]; cand[j] = tmp; }
                foreach (var c in cand)
                {
                    bool ok = true;
                    foreach (var q in pr.list) if ((q.c - c.c).magnitude < (c.r + q.r) * 0.5f * (gap / r)) { ok = false; break; }
                    if (ok) pr.Add(c);
                }
                return pr;
            }
        }

        // ------------------------------------------------------------------ costume parts

        /// <summary>Wrap one optional part so a failure costs only that part.</summary>
        static void TryPart(string what, Action a)
        {
            try { a(); }
            catch (Exception e) { Debug.LogWarning("BananaBall: costume part '" + what + "' failed. " + e); }
        }

        /// <summary>Append a part already in model space (bind pose), skinned 100% to one bone, coloured by label.</summary>
        static void EmitModel(MeshKit k, SdfMesh m, int bone, Func<int, Vector3, Color> colorOf, float aoStep, float aoStrength, float ink = 1f)
        {
            EmitOnBone(k, m, bone, Vector3.zero, Quaternion.identity, colorOf, aoStep, aoStrength, ink);
        }

        /// <summary>A torus round an axis (frame: centre, up = axis, fwd = phi 0); the ring may be elliptic (Rx, Rz); colour by
        /// band index round the ring (each band gets its own vertices, so stripes stay crisp). Vertices go through xf (model space).</summary>
        static void Torus(MeshKit k, Vector3 c, Vector3 up, Vector3 fwd, float Rx, float Rz, float ra, float rb, int segs, int sides, Func<int, Color> bandColor, int bands, int bone, Func<Vector3, Vector3> xf, Func<Vector3, Vector3> xn, float ink = 1f)
        {
            up.Normalize();
            fwd = (fwd - up * Vector3.Dot(fwd, up)).normalized;
            Vector3 right = Vector3.Cross(up, fwd);
            int perBand = Mathf.Max(2, segs / Mathf.Max(1, bands));
            int nb = Mathf.Max(1, bands);
            for (int bi = 0; bi < nb; bi++)
            {
                Color col = bandColor(bi);
                int s0 = k.V.Count;
                for (int i = 0; i <= perBand; i++)
                {
                    float phi = (bi + i / (float)perBand) / nb * Mathf.PI * 2f;
                    float sp = Mathf.Sin(phi), cp = Mathf.Cos(phi);
                    Vector3 ringP = c + right * (Rx * sp) + fwd * (Rz * cp);
                    // outward direction of the (elliptic) ring at this point
                    Vector3 outD = (right * (sp / Mathf.Max(Rx, 1e-4f)) + fwd * (cp / Mathf.Max(Rz, 1e-4f))).normalized;
                    for (int j = 0; j <= sides; j++)
                    {
                        float psi = j / (float)sides * Mathf.PI * 2f;
                        float cs = Mathf.Cos(psi), sn = Mathf.Sin(psi);
                        Vector3 p = ringP + outD * (ra * cs) + up * (rb * sn);
                        Vector3 n = (outD * (cs / ra) + up * (sn / rb)).normalized;
                        k.V.Add(xf(p)); k.N.Add(xn(n)); k.C.Add(col); k.UV.Add(MeshKit.Plain); k.UV2.Add(Vector2.zero); k.OL.Add(ink);
                        if (k.skin) k.W.Add(new BoneWeight { boneIndex0 = bone, weight0 = 1f });
                    }
                }
                for (int i = 0; i < perBand; i++)
                    for (int j = 0; j < sides; j++)
                    {
                        int a = s0 + i * (sides + 1) + j, b = a + sides + 1;
                        k.T.Add(a); k.T.Add(b); k.T.Add(a + 1);
                        k.T.Add(a + 1); k.T.Add(b); k.T.Add(b + 1);
                    }
            }
        }

        /// <summary>Body hull at a height: for each of n directions round (0 = front, +x right), how far the torso and legs reach
        /// (the support of their outline, so the gap between the thighs is bridged). Measured from the given centre.</summary>
        static float[] Hull(SdfShape sh, Vector3 centre, int n)
        {
            sh.Bake();
            var mk = new bool[sh.P.Length];
            for (int i = 0; i < mk.Length; i++) { int l = sh.P[i].label; mk[i] = l == LTorso || l == LLegL || l == LLegR; }
            const int NS = 72;
            var pts = new Vector2[NS];
            for (int i = 0; i < NS; i++)
            {
                float a = i / (float)NS * Mathf.PI * 2f;
                Vector3 d = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
                Vector3 hit = MarchMask(sh, mk, centre + d * 0.9f, -d, 0.9f);
                pts[i] = new Vector2(hit.x - centre.x, hit.z - centre.z);
            }
            var h = new float[n];
            for (int j = 0; j < n; j++)
            {
                float a = j / (float)n * Mathf.PI * 2f;
                Vector2 d = new Vector2(Mathf.Sin(a), Mathf.Cos(a));
                float best = 0f;
                foreach (var p in pts) best = Mathf.Max(best, Vector2.Dot(p, d));
                h[j] = best;
            }
            return h;
        }

        static float HullAt(float[] h, float phi)
        {
            int n = h.Length;
            float f = Mathf.Repeat(phi / (Mathf.PI * 2f), 1f) * n;
            int i0 = Mathf.FloorToInt(f) % n, i1 = (i0 + 1) % n;
            return Mathf.Lerp(h[i0], h[i1], f - Mathf.Floor(f));
        }

        /// <summary>A pleated tartan kilt from the belt to just above the knee: a loft round the body's hull (straight across the gap
        /// between the legs) that flares toward the hem; a flat apron in front, a fan of pleats behind; a leather belt with a buckle.
        /// Tartan by faces, so the checks stay crisp. Skinned to the hips, the hem partly to each thigh, so it swings with the legs.</summary>
        static void Kilt(MeshKit k, Body b, Wardrobe cw, Vector3[] rp, float coarse, Info body)
        {
            float s = b.height / 1.85f, TK = PlayerMesh.TorsoK;
            Vector3 hips = rp[0];
            float yTop = hips.y + 0.13f * s * TK, yHem = rp[15].y + 0.05f * s;
            float zc = -0.006f * s;
            // rows: the belt, then tartan lines down to the hem
            float rep = 0.1f * s, beltH = 0.032f * s;
            float[] fr = { 0f, 0.2f, 0.46f, 0.52f };   // tartan sett: dark band, ground, bright line, ground (fractions of a repeat)
            var ys = new List<float> { yTop + 0.012f * s, yTop, yTop - beltH };
            for (float y0 = yTop - beltH; y0 > yHem - rep; y0 -= rep)
                foreach (float f in fr) { float y = y0 - f * rep; if (y < yTop - beltH - 0.002f * s && y > yHem + 0.002f * s) ys.Add(y); }
            ys.Add(yHem);
            ys.Sort((p, q) => q.CompareTo(p));
            bool far = coarse > 1.5f;
            for (int i = ys.Count - 2; i >= 1; i--)   // no row taller than 2.6 cm (far away: 6 cm)
            {
                float gap = ys[i] - ys[i + 1];
                int extra = Mathf.FloorToInt(gap / ((far ? 0.06f : 0.026f) * s));
                for (int e = 1; e <= extra; e++) ys.Insert(i + e, ys[i] - gap * e / (extra + 1));
            }
            // columns: 8 tartan repeats round; pleat ridges and valleys at quarter repeats
            const int Reps = 8;
            float[] cf = far ? new[] { 0f, 0.2f, 0.25f, 0.46f, 0.52f, 0.75f } : new[] { 0f, 0.1f, 0.2f, 0.25f, 0.33f, 0.46f, 0.5f, 0.52f, 0.62f, 0.75f, 0.87f };
            var phis = new List<float>();
            for (int r = 0; r < Reps; r++) foreach (float f in cf) phis.Add((r + f) / Reps * Mathf.PI * 2f);
            int nc = phis.Count, nr = ys.Count;
            // radius per row: the body's hull plus room, growing toward the hem
            var R = new float[nr, nc];
            var hulls = new float[nr][];
            for (int i = 1; i < nr; i++) hulls[i] = Hull(body.shape, new Vector3(0f, ys[i], zc), 48);
            hulls[0] = hulls[1];
            for (int j = 0; j < nc; j++)
            {
                float phi = phis[j], cph = Mathf.Cos(phi);
                bool back = cph < -0.25f;
                float prev = 0f;
                for (int i = 0; i < nr; i++)
                {
                    float u = Mathf.Clamp01((yTop - ys[i]) / (yTop - yHem));
                    float r = HullAt(hulls[i], phi) + (i == 0 ? -0.012f * s : 0.006f * s + 0.04f * s * Mathf.Pow(u, 1.5f));
                    if (back && i > 2)
                    {
                        // knife pleats: a sawtooth-ish zigzag across the back
                        float g = Mathf.Repeat(phi / (Mathf.PI * 2f) * Reps * 2f, 1f);
                        float tri = 1f - Mathf.Abs(g * 2f - 1f);
                        r += 0.009f * s * (0.3f + 0.7f * u) * tri * Mathf.Clamp01((-cph - 0.25f) / 0.3f);
                    }
                    if (i > 1) r = Mathf.Max(r, prev + 0.0015f * s);
                    R[i, j] = r;
                    prev = r;
                }
            }
            Vector3 Pt(int i, int j) => new Vector3(Mathf.Sin(phis[j % nc]) * R[i, j % nc], ys[i], zc + Mathf.Cos(phis[j % nc]) * R[i, j % nc]);
            var P = new Vector3[nr, nc];
            var N = new Vector3[nr, nc];
            for (int i = 0; i < nr; i++) for (int j = 0; j < nc; j++) P[i, j] = Pt(i, j);
            for (int i = 0; i < nr; i++)
                for (int j = 0; j < nc; j++)
                {
                    Vector3 du = P[i, (j + 1) % nc] - P[i, (j - 1 + nc) % nc];
                    Vector3 dv = P[Mathf.Max(i - 1, 0), j] - P[Mathf.Min(i + 1, nr - 1), j];
                    Vector3 n = Vector3.Cross(dv, du).normalized;
                    Vector3 radial = new Vector3(P[i, j].x, 0f, P[i, j].z - zc);
                    if (Vector3.Dot(n, radial) < 0f) n = -n;
                    N[i, j] = n;
                }
            BoneWeight W(int i, int j)
            {
                float u = Mathf.Clamp01((yTop - ys[i]) / (yTop - yHem));
                float phi = phis[j % nc];
                float leg = Mathf.Clamp01(Mathf.Pow(u, 1.35f) * (0.42f + 0.36f * Mathf.Max(0f, Mathf.Cos(phi))));
                float fRight = Smooth01((Mathf.Sin(phi) + 0.35f) / 0.7f);
                float wl = leg * (1f - fRight), wr = leg * fRight, wh = 1f - leg;
                return new BoneWeight { boneIndex0 = 0, weight0 = wh, boneIndex1 = 14, weight1 = wl, boneIndex2 = 17, weight2 = wr };
            }
            // tartan colour of a face
            int Band(float f) { f = Mathf.Repeat(f, 1f); return f < 0.2f ? 1 : (f >= 0.46f && f < 0.52f) ? 2 : 0; }
            Color darker = Pal.Gloss(Color.Lerp(cw.kiltDark, Color.black, 0.35f), 0.12f);
            Color Face(int i, int j)
            {
                float ym = (ys[i] + ys[i + 1]) * 0.5f;
                if (ym > yTop - beltH)
                {
                    float pm = (phis[j] + (j + 1 < nc ? phis[j + 1] : Mathf.PI * 2f)) * 0.5f;
                    bool front = Mathf.Abs(Mathf.DeltaAngle(0f, pm * Mathf.Rad2Deg)) < 6f;
                    return front ? cw.buckle : cw.kiltBelt;
                }
                float fv = phis[j] / (Mathf.PI * 2f) * Reps + 0.001f;
                float fh = (yTop - beltH - ym) / rep;
                int bv = Band(fv), bh = Band(fh);
                if (bv == 2 || bh == 2) return cw.kiltLine;
                if (bv == 1 && bh == 1) return darker;
                if (bv == 1 || bh == 1) return cw.kiltDark;
                return cw.kilt;
            }
            // outer faces (own vertices: crisp checks)
            for (int i = 0; i < nr - 1; i++)
                for (int j = 0; j < nc; j++)
                {
                    int j1 = (j + 1) % nc;
                    Color c = Face(i, j);
                    int a0 = KiltV(k, P[i, j], N[i, j], c, W(i, j)), a1 = KiltV(k, P[i, j1], N[i, j1], c, W(i, j1));
                    int b0 = KiltV(k, P[i + 1, j], N[i + 1, j], c, W(i + 1, j)), b1 = KiltV(k, P[i + 1, j1], N[i + 1, j1], c, W(i + 1, j1));
                    k.T.Add(a0); k.T.Add(b0); k.T.Add(a1);
                    k.T.Add(a1); k.T.Add(b0); k.T.Add(b1);
                }
            // inside (one dark colour) and the turned edge at the hem
            float th = 0.006f * s;
            Color inside = Pal.Gloss(Color.Lerp(cw.kiltDark, Color.black, 0.3f), 0.1f);
            int[,] inner = new int[nr, nc];
            for (int i = 1; i < nr; i++) for (int j = 0; j < nc; j++) inner[i, j] = KiltV(k, P[i, j] - N[i, j] * th, -N[i, j], inside, W(i, j));
            for (int i = 1; i < nr - 1; i++)
                for (int j = 0; j < nc; j++)
                {
                    int j1 = (j + 1) % nc;
                    k.T.Add(inner[i, j]); k.T.Add(inner[i, j1]); k.T.Add(inner[i + 1, j]);
                    k.T.Add(inner[i, j1]); k.T.Add(inner[i + 1, j1]); k.T.Add(inner[i + 1, j]);
                }
            int last = nr - 1;
            for (int j = 0; j < nc; j++)
            {
                int j1 = (j + 1) % nc;
                Vector3 dn = Vector3.down;
                int o0 = KiltV(k, P[last, j], dn, inside, W(last, j)), o1 = KiltV(k, P[last, j1], dn, inside, W(last, j1));
                int i0 = KiltV(k, P[last, j] - N[last, j] * th, dn, inside, W(last, j)), i1 = KiltV(k, P[last, j1] - N[last, j1] * th, dn, inside, W(last, j1));
                k.T.Add(o0); k.T.Add(i0); k.T.Add(o1);
                k.T.Add(o1); k.T.Add(i0); k.T.Add(i1);
            }
        }

        static int KiltV(MeshKit k, Vector3 p, Vector3 n, Color c, BoneWeight w)
        {
            k.V.Add(p); k.N.Add(n); k.C.Add(c); k.UV.Add(MeshKit.Plain); k.UV2.Add(Vector2.zero); k.OL.Add(1f); k.W.Add(w);
            return k.V.Count - 1;
        }

        /// <summary>An inflatable swim ring round the belly (striped, or a yellow duck ring with a head), rigid on the spine.</summary>
        static void Floatie(MeshKit k, Body b, Wardrobe cw, Vector3[] rp, float coarse, Info body)
        {
            float s = b.height / 1.85f, TK = PlayerMesh.TorsoK;
            float y = rp[0].y + 0.16f * s * TK, zc = -0.004f * s;
            float tube = 0.07f * s, flat = 0.062f * s;
            var h = Hull(body.shape, new Vector3(0f, y, zc), 36);
            float side = Mathf.Max(HullAt(h, Mathf.PI * 0.5f), HullAt(h, Mathf.PI * 1.5f));
            float front = HullAt(h, 0f), backR = HullAt(h, Mathf.PI);
            float rz = (front + backR) * 0.5f, cz = zc + (front - backR) * 0.5f;
            int segs = coarse > 1.2f ? 28 : 56, sides = coarse > 1.2f ? 9 : 16;
            Vector3 c = new Vector3(0f, y, cz);
            Torus(k, c, Vector3.up, Vector3.forward, side + tube * 0.78f, rz + tube * 0.78f, tube, flat, segs, sides,
                  bi => cw.duckRing ? cw.ring : (bi % 2 == 0 ? cw.ring : cw.ringStripe), cw.duckRing ? 1 : 8, 1, p => p, n => n, 0.9f);
            if (!cw.duckRing) return;
            // the duck: a head with a flat orange bill on the front of the ring, looking ahead
            var sh = new SdfShape();
            var g = sh.Root;
            Vector3 hc = c + new Vector3(0f, flat * 0.9f + 0.045f * s, rz + tube * 0.78f + 0.012f * s);
            sh.Cone(g, hc + new Vector3(0f, -0.06f * s, -0.006f * s), hc, 0.038f * s, 0.034f * s, 0f, 0, 1);
            sh.Ell(g, hc + new Vector3(0f, 0.016f * s, 0f), new Vector3(0.05f, 0.047f, 0.05f) * s, Quaternion.identity, 0.02f * s, 0, 1);
            sh.Ell(g, hc + new Vector3(0f, 0.004f * s, 0.052f * s), new Vector3(0.034f, 0.011f, 0.03f) * s, Quaternion.Euler(-8f, 0f, 0f), 0.006f * s, 1, 1);
            for (int e = -1; e <= 1; e += 2)
                sh.Ell(g, hc + new Vector3(e * 0.023f * s, 0.032f * s, 0.038f * s), new Vector3(0.0085f, 0.011f, 0.006f) * s, Quaternion.Euler(0f, e * 25f, 0f), 0f, 2, 1);
            sh.Bake();
            var m = SdfMesh.Build(sh, 0.0042f * s * Mathf.Min(coarse, 2.5f));
            m.ComputeLabels();
            m.CutLabels();
            m.FixTJunctions();
            Color eye = Pal.Gloss(new Color(0.06f, 0.06f, 0.07f), 0.9f);
            EmitModel(k, m, 1, (l, p) => l == 1 ? cw.beak : l == 2 ? eye : cw.duck, 0.006f * s, 0.4f, 0.8f);
        }

        /// <summary>Water wings: two puffy orange rings round each upper arm, rigid on the shoulder bone.</summary>
        static void Wing(MeshKit k, Body b, Wardrobe cw, int side, float coarse, Vector3[] rp, Vector3 bp, Quaternion bq)
        {
            float s = b.height / 1.85f, Lk = PlayerMesh.LimbK * s * (b.ape ? 1.3f : 1f), mu = b.muscle;
            int shb = side == 0 ? 8 : 11;
            float upLen = (rp[shb + 1] - rp[shb]).magnitude;
            float rArm = (0.057f + 0.017f * mu) * Lk;
            int segs = coarse > 1.2f ? 18 : 32, sides = coarse > 1.2f ? 8 : 12;
            for (int i = 0; i < 2; i++)
            {
                // bone frame: the upper arm runs down local -y from the shoulder joint
                Vector3 c = new Vector3(0f, -upLen * (0.38f + 0.2f * i), 0.003f * s);
                float tube = 0.031f * s;
                Torus(k, c, Vector3.up, Vector3.forward, rArm + tube * 0.62f, rArm + tube * 0.62f, tube, tube * 0.92f, segs, sides,
                      bi => bi == 0 ? cw.wing : cw.wingBand, 1, shb, p => bp + bq * p, n => bq * n, 0.85f);
            }
            // a valve on the outside
            Vector3 vc = new Vector3((side == 0 ? -1f : 1f) * (rArm + 0.05f * s), -upLen * 0.48f, 0.01f * s);
            Torus(k, vc, side == 0 ? Vector3.left : Vector3.right, Vector3.forward, 0.006f * s, 0.006f * s, 0.006f * s, 0.006f * s, 8, 6, bi => cw.wingBand, 1, shb, p => bp + bq * p, n => bq * n, 0f);
        }

        /// <summary>A flower lei round the neck, hanging lower in front: plumerias in four colours with a few leaves, rigid on the chest.</summary>
        static void Lei(MeshKit k, Body b, Wardrobe cw, Vector3[] rp, float coarse, Info body)
        {
            float s = b.height / 1.85f, TK = PlayerMesh.TorsoK;
            float yN = rp[0].y + 0.47f * s * TK;
            var sh = new SdfShape();
            const int Nf = 17;
            float fr = 0.027f * s;
            var full = body.shape;
            for (int i = 0; i < Nf; i++)
            {
                float phi = (i + 0.5f) / Nf * Mathf.PI * 2f;
                float sn = Mathf.Sin(phi), cs = Mathf.Cos(phi);
                float dip = 0.12f * s * Mathf.Pow(Mathf.Max(0f, cs), 1.5f);
                Vector3 guide = new Vector3(0.122f * s * sn * Mathf.Lerp(1f, 0.85f, Mathf.Max(0f, cs)), yN + 0.004f * s - dip, 0.092f * s * cs - 0.008f * s);
                Vector3 outD = new Vector3(sn, 0.9f - 0.7f * Mathf.Max(0f, cs), cs).normalized;
                Vector3 hit = full.March(guide + outD * 0.3f, -outD, 0.45f);
                Vector3 n = full.Grad(hit, null, 0.003f * s).normalized;
                Vector3 c = hit + n * fr * 0.62f;
                var g = sh.Branch(sh.Root, 0.004f * s);
                Quaternion q = Quaternion.LookRotation(n, Vector3.up);
                int lab = i % 4;
                for (int pi = 0; pi < 5; pi++)
                {
                    float a = i * 0.9f + pi * 1.2566371f;
                    Vector3 pc = c + q * new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * (fr * 0.52f);
                    sh.Ell(g, pc, new Vector3(0.46f, 0.46f, 0.27f) * fr, q, 0.003f * s, lab, 2);
                }
                sh.Ell(g, c + n * fr * 0.16f, new Vector3(0.24f, 0.24f, 0.2f) * fr, q, 0.002f * s, 4, 2);
                if (i % 2 == 1)
                {
                    // a leaf peeking out between this flower and the next
                    Vector3 side = q * new Vector3(1f, -0.4f, 0f);
                    sh.Ell(sh.Root, c + side * fr * 0.95f - n * fr * 0.18f, new Vector3(0.55f, 0.24f, 0.14f) * fr, q * Quaternion.Euler(0f, 0f, -20f), 0f, 5, 2);
                }
            }
            sh.Bake();
            var m = SdfMesh.Build(sh, 0.0042f * s * Mathf.Min(coarse, 2.4f));
            m.ComputeLabels();
            m.CutLabels();
            m.FixTJunctions();
            Color leaf = Pal.Gloss(Pal.Hex(0x2C9A57), 0.25f);
            EmitModel(k, m, 2, (l, p) => l < 4 ? cw.lei[l] : l == 4 ? cw.lei[4] : leaf, 0.005f * s, 0.35f, 0.6f);
        }

        /// <summary>A polka-dot neckerchief: the neck and chest blown up a little, cut to a band round the neck with a triangle hanging
        /// in front, skinned like the body under it.</summary>
        static void Bandana(MeshKit k, Body b, Wardrobe cw, Vector3[] rp, float coarse, Info body)
        {
            float s = b.height / 1.85f, TK = PlayerMesh.TorsoK;
            Vector3 hips = rp[0];
            float Tuy(float y) => hips.y + y * s * TK;
            var sh = new SdfShape();
            float th = 0.008f * s;
            void Blown(SdfShape.Group g, bool neckOnly)
            {
                foreach (var q in body.shape.P)
                {
                    if (q.label != LNeck && (neckOnly || q.label != LTorso)) continue;
                    if (q.kind == 1) sh.Ell(g, q.c, q.r + Vector3.one * th, Quaternion.Inverse(q.inv), q.k, 0, -2);
                    else sh.Cone(g, q.a, q.b, q.r1 + th, q.r2 + th, q.k, 0, -2);
                }
            }
            float yTip = Tuy(0.35f), yTop = Tuy(0.53f), slope = 0.72f, ek = 0.004f * s;
            // the triangle hanging in front
            var front = sh.Branch(sh.Root, 0f);
            Blown(front, false);
            sh.ClipPlane(front, Vector3.up, new Vector3(0f, yTop, 0f), ek);
            sh.ClipPlane(front, new Vector3(1f, -slope, 0f), new Vector3(0f, yTip, 0f), ek);
            sh.ClipPlane(front, new Vector3(-1f, -slope, 0f), new Vector3(0f, yTip, 0f), ek);
            sh.ClipPlane(front, Vector3.back, new Vector3(0f, 0f, 0.01f * s), ek);
            // and a narrow band round the neck, knotted at the back
            var band = sh.Branch(sh.Root, 0f);
            Blown(band, true);
            sh.ClipPlane(band, Vector3.up, new Vector3(0f, yTop, 0f), ek);
            sh.ClipPlane(band, Vector3.down, new Vector3(0f, Tuy(0.475f), 0f), ek);
            Vector3 knot = body.shape.March(new Vector3(0f, Tuy(0.5f), -0.8f), Vector3.forward, 1.6f);
            sh.Ell(sh.Root, knot + new Vector3(0f, 0f, -0.006f * s), new Vector3(0.022f, 0.016f, 0.014f) * s, Quaternion.identity, 0f, 0, -2);
            sh.Bake();
            var m = SdfMesh.Build(sh, 0.0062f * s * Mathf.Min(coarse, 2.2f));
            m.ComputeLabels();
            float sp = 0.034f * s, dr = 0.0085f * s;
            float Dot(Vector3 p)
            {
                float u = Mathf.Repeat(p.x / sp + (p.z > 0f ? 0f : 0.5f), 1f) - 0.5f, v = Mathf.Repeat(p.y / sp + Mathf.Floor(p.x / sp) * 0.5f, 1f) - 0.5f;
                return new Vector2(u, v).magnitude * sp - dr;
            }
            m.Cut(Dot, l => true);
            m.FixTJunctions();
            var skinOf = new NearestSkin(body, l => l == LTorso || l == LNeck, 0.03f * s);
            EmitShell(k, m, body, -0.006f * s, p => Dot(p) < 0f ? cw.bandanaDot : cw.bandana, skinOf.Weight);
        }

        /// <summary>A bow tie at the throat, rigid on the chest.</summary>
        static void BowTie(MeshKit k, Body b, Wardrobe cw, Vector3[] rp, float coarse, Info body)
        {
            float s = b.height / 1.85f, TK = PlayerMesh.TorsoK;
            float y = rp[0].y + 0.462f * s * TK;
            Vector3 c = body.shape.March(new Vector3(0f, y, 0.8f), Vector3.back, 1.6f) + new Vector3(0f, 0f, 0.008f * s);
            var sh = new SdfShape();
            var g = sh.Root;
            for (int e = -1; e <= 1; e += 2)
                sh.Ell(g, c + new Vector3(e * 0.026f * s, 0f, -0.002f * s), new Vector3(0.028f, 0.019f, 0.01f) * s, Quaternion.Euler(0f, e * -12f, e * 6f), 0.004f * s, 0, 2);
            sh.Ell(g, c + new Vector3(0f, 0f, 0.003f * s), new Vector3(0.011f, 0.013f, 0.011f) * s, Quaternion.identity, 0.004f * s, 1, 2);
            sh.Bake();
            var m = SdfMesh.Build(sh, 0.0026f * s * Mathf.Min(coarse, 2.4f));
            m.ComputeLabels();
            m.CutLabels();
            m.FixTJunctions();
            Color knot = Pal.Gloss(Color.Lerp(cw.bowtie, Color.black, 0.25f), 0.6f);
            EmitModel(k, m, 2, (l, p) => l == 1 ? knot : cw.bowtie, 0.004f * s, 0.3f, 0.6f);
        }

        /// <summary>A snorkel clipped to the goggle strap: a tube from the corner of the mouth up the side of the head.</summary>
        static void Snorkel(MeshKit k, Body b, Wardrobe cw, Vector3[] rp, Info body)
        {
            float s = b.height / 1.85f, hs = s * PlayerMesh.HeadOf(b);
            Vector3 H(float x, float y, float z) => rp[4] + new Vector3(x, y, z) * hs;
            Vector3[] path = { H(-0.012f, 0.03f, 0.1f), H(-0.05f, 0.036f, 0.092f), H(-0.088f, 0.058f, 0.058f), H(-0.112f, 0.1f, 0.012f), H(-0.112f, 0.16f, -0.01f), H(-0.104f, 0.215f, -0.02f) };
            Vector3 Path(float t)
            {
                float f = Mathf.Clamp01(t) * (path.Length - 1);
                int i = Mathf.Min(Mathf.FloorToInt(f), path.Length - 2);
                float u = f - i;
                Vector3 p0 = path[Mathf.Max(i - 1, 0)], p1 = path[i], p2 = path[i + 1], p3 = path[Mathf.Min(i + 2, path.Length - 1)];
                return 0.5f * (2f * p1 + (-p0 + p2) * u + (2f * p0 - 5f * p1 + 4f * p2 - p3) * u * u + (-p0 + 3f * p1 - 3f * p2 + p3) * u * u * u);
            }
            int save = k.bone;
            float saveOl = k.outline;
            k.bone = 4;
            k.outline = 0.6f;
            k.Tube(t => Path(t), t => (t < 0.08f ? 0.013f : 0.0088f) * hs, 0f, 1f, 22, 8, cw.snorkel);
            k.Sphere(path[path.Length - 1] + new Vector3(0f, 0.004f, 0f) * hs, 0.0125f * hs, cw.wingBand, 4, 8);
            k.Ellipsoid(path[0] + new Vector3(0.004f, 0f, 0.004f) * hs, new Vector3(0.018f, 0.012f, 0.008f) * hs, Quaternion.identity, Pal.Gloss(new Color(0.1f, 0.1f, 0.12f), 0.5f), 4, 8);
            k.bone = save;
            k.outline = saveOl;
        }

        /// <summary>A bare cartoon foot on a flip-flop (foot bone frame like the cleat: ankle at the origin, toes toward +z,
        /// the ground at y = -AnkleY): heel, arch, the ball of the foot and five toes, the big one on the inside; a team-colour sole
        /// and a thong strap from between the first two toes to both sides of the arch.</summary>
        static void FlipFlop(MeshKit k, Body b, Wardrobe cw, int side, float coarse, int bone, Vector3 bp, Quaternion bq)
        {
            float f = PlayerMesh.FootK * b.height / 1.85f;
            float inX = side == 0 ? 1f : -1f;   // toward the midline: the big toe's side
            const float lift = 0.012f;          // the foot stands on a thick sole, clear of the dirt
            var sh = new SdfShape();
            var foot = sh.Branch(sh.Root, 0f);
            sh.Cone(foot, new Vector3(0f, 0.02f, -0.006f) * f, new Vector3(0f, -0.032f, 0f) * f, 0.03f * f, 0.033f * f, 0f, 0, 0);
            sh.Ell(foot, new Vector3(0f, -0.046f + lift, -0.014f) * f, new Vector3(0.033f, 0.024f, 0.036f) * f, Quaternion.identity, 0.012f * f, 0, 0);
            sh.Ell(foot, new Vector3(-inX * 0.002f, -0.048f + lift, 0.046f) * f, new Vector3(0.039f, 0.02f, 0.058f) * f, Quaternion.Euler(4f, 0f, 0f), 0.02f * f, 0, 0);
            sh.Ell(foot, new Vector3(inX * 0.004f, -0.053f + lift, 0.094f) * f, new Vector3(0.045f, 0.017f, 0.034f) * f, Quaternion.identity, 0.016f * f, 0, 0);
            var toes = sh.Branch(foot, 0.005f * f);
            float[] tx = { 0.026f, 0.007f, -0.009f, -0.023f, -0.035f }, tz = { 0.137f, 0.134f, 0.128f, 0.12f, 0.11f }, tr = { 0.0155f, 0.0108f, 0.0102f, 0.0093f, 0.0085f };
            for (int i = 0; i < 5; i++)
                sh.Ell(toes, new Vector3(inX * tx[i], -0.057f + lift, tz[i]) * f, new Vector3(tr[i], tr[i] * 0.85f, tr[i] * 1.15f) * f, Quaternion.identity, 0f, 0, 0);
            sh.Bake();
            // the thong strap rides on top of the foot: from between the first two toes up to the instep, then down both sides
            Vector3 OnTop(float x, float z) => sh.March(new Vector3(x, 0.05f, z) * f, Vector3.down, 0.3f * f) + new Vector3(0f, 0.003f, 0f) * f;
            Vector3 toe = OnTop(inX * 0.017f, 0.112f), mid = OnTop(inX * 0.01f, 0.085f), top = OnTop(inX * 0.005f, 0.062f);
            var sole = sh.Branch(sh.Root, 0f);
            sh.Ell(sole, new Vector3(inX * 0.002f, -0.067f, 0.052f) * f, new Vector3(0.054f, 0.0135f, 0.12f) * f, Quaternion.identity, 0f, 1, 0);
            sh.ClipPlane(sole, Vector3.down, new Vector3(0f, -0.081f, 0f) * f);
            sh.ClipPlane(sole, Vector3.up, new Vector3(0f, -0.058f, 0f) * f, 0.002f * f);
            var strap = sh.Branch(sh.Root, 0.002f * f);
            float sr = 0.0062f * f;
            sh.Cone(strap, new Vector3(toe.x, -0.06f * f, toe.z), toe, sr * 0.8f, sr, 0f, 2, 0);
            sh.Cone(strap, toe, mid, sr, sr, 0.002f * f, 2, 0);
            sh.Cone(strap, mid, top, sr, sr, 0.002f * f, 2, 0);
            for (int e = -1; e <= 1; e += 2)
            {
                Vector3 sideP = OnTop(e * 0.03f + inX * 0.002f, 0.046f);
                Vector3 low = new Vector3(e * 0.044f + inX * 0.002f, -0.056f, 0.038f) * f;
                sh.Cone(strap, top, sideP, sr, sr, 0.002f * f, 2, 0);
                sh.Cone(strap, sideP, low, sr, sr * 0.9f, 0.002f * f, 2, 0);
            }
            sh.Bake();
            var m = SdfMesh.Build(sh, 0.0032f * f * coarse);
            m.ComputeLabels();
            m.CutLabels();
            m.FixTJunctions();
            Color skin = SkinC(b.skin);
            EmitOnBone(k, m, bone, bp, bq, (l, p) => l == 1 ? cw.flipSole : l == 2 ? cw.flipStrap : skin, 0.006f * f, 0.4f, 0.7f);
        }

        // ------------------------------------------------------------------ hats (rigid, on the head bone; head space, scale hs)

        /// <summary>A hat brim (or a band, tilted 90): a flattened ring of elliptic section round the crown; the section's outer edge
        /// drops by tilt (degrees, negative curls it up); radii, height and tilt may vary round the hat (phi 0 = front, +x right).</summary>
        public static void Brim(MeshKit k, Vector3 c, Func<float, float> rIn, Func<float, float> rOut, Func<float, float> yOff, Func<float, float> tiltDeg, float thick, Color col, int segs = 64, int sides = 12)
        {
            int s0 = k.V.Count;
            for (int i = 0; i <= segs; i++)
            {
                float phi = i / (float)segs * Mathf.PI * 2f;
                Vector3 radial = new Vector3(Mathf.Sin(phi), 0f, Mathf.Cos(phi));
                float ri = rIn(phi), ro = rOut(phi), rc = (ri + ro) * 0.5f, a = Mathf.Max(1e-4f, (ro - ri) * 0.5f), bb = Mathf.Max(1e-4f, thick * 0.5f);
                float tl = tiltDeg(phi) * Mathf.Deg2Rad, ct = Mathf.Cos(tl), st = Mathf.Sin(tl);
                float yo = yOff(phi);
                for (int j = 0; j <= sides; j++)
                {
                    float psi = j / (float)sides * Mathf.PI * 2f;
                    float lr = a * Mathf.Cos(psi), ly = bb * Mathf.Sin(psi);
                    float pr = lr * ct + ly * st, py = -lr * st + ly * ct;
                    float nr0 = Mathf.Cos(psi) / a, ny0 = Mathf.Sin(psi) / bb;
                    float nr = nr0 * ct + ny0 * st, ny = -nr0 * st + ny0 * ct;
                    Vector3 p = c + radial * (rc + pr) + Vector3.up * (yo + py);
                    Vector3 n = (radial * nr + Vector3.up * ny).normalized;
                    k.Vert(p, n, col, MeshKit.Plain);
                }
            }
            for (int i = 0; i < segs; i++)
                for (int j = 0; j < sides; j++)
                {
                    int a = s0 + i * (sides + 1) + j, b = a + sides + 1;
                    k.T.Add(a); k.T.Add(b); k.T.Add(a + 1);
                    k.T.Add(a + 1); k.T.Add(b); k.T.Add(b + 1);
                }
        }

        /// <summary>Radius of an ellipsoid crown's cross-section at height y, in direction phi.</summary>
        static float CrownR(Vector3 cc, Vector3 cr, float y, float phi)
        {
            float f = Mathf.Sqrt(Mathf.Max(0.02f, 1f - ((y - cc.y) / cr.y) * ((y - cc.y) / cr.y)));
            float rx = cr.x * f, rz = cr.z * f, sp = Mathf.Sin(phi), cp = Mathf.Cos(phi);
            return 1f / Mathf.Sqrt(sp * sp / (rx * rx) + cp * cp / (rz * rz));
        }

        /// <summary>The costume hats: bucket, straw, cowboy, fire helmet, bowler. Each crown covers the cap's crown (so the hidden
        /// top of the head stays hidden) and its brim clears the ears and brows.</summary>
        public static void HatShape(MeshKit k, Hat hat, float hs, TeamData t, int style, out Vector3 badge)
        {
            int st = Mathf.Abs(style);
            badge = Vector3.zero;
            var sh = new SdfShape();
            var g = sh.Root;
            Vector3 H(float x, float y, float z) => new Vector3(x, y, z) * hs;
            Vector3 cc = H(0f, 0.14f, -0.004f), cr = H(0.096f, 0.088f, 0.108f);
            float yBrim = 0.118f, bandY = 0.13f;
            Color crown, band, brim;
            Color Cl(Color c, float gl = 0.15f) => Pal.Gloss(c, gl);
            float L(Color c) => Wardrobe.Lum(c);
            switch (hat)
            {
                case Hat.Bucket:
                {
                    Color c0 = st % 3 == 0 ? t.main : st % 3 == 1 ? t.accent : Color.white;
                    crown = brim = Cl(c0, 0.1f);
                    band = Cl(L(c0) > 0.5f ? t.dark : t.main, 0.2f);
                    cr = H(0.1f, 0.092f, 0.112f); cc = H(0f, 0.136f, -0.004f);
                    sh.Ell(g, cc, cr, Quaternion.identity, 0f, 0, 0);
                    sh.ClipPlane(g, Vector3.up, H(0f, 0.208f, 0f), 0.016f * hs);
                    break;
                }
                case Hat.Straw:
                    crown = brim = Cl(new Color(0.93f, 0.8f, 0.5f), 0.2f);
                    band = Cl(st % 2 == 0 ? t.main : Pal.Hex(0xE8433A), 0.3f);
                    cr = H(0.097f, 0.084f, 0.108f); cc = H(0f, 0.146f, -0.004f);
                    sh.Ell(g, cc, cr, Quaternion.identity, 0f, 0, 0);
                    sh.ClipPlane(g, Vector3.up, H(0f, 0.212f, 0f), 0.012f * hs);
                    break;
                case Hat.Cowboy:
                {
                    Color[] hc = { new Color(0.78f, 0.6f, 0.38f), new Color(0.47f, 0.3f, 0.17f), new Color(0.13f, 0.12f, 0.13f), new Color(0.93f, 0.91f, 0.86f) };
                    crown = brim = Cl(hc[st % 4], 0.15f);
                    band = Cl(st % 4 == 1 ? new Color(0.15f, 0.1f, 0.07f) : new Color(0.32f, 0.2f, 0.12f), 0.35f);
                    cr = H(0.094f, 0.096f, 0.116f); cc = H(0f, 0.15f, -0.006f);
                    sh.Ell(g, cc, cr, Quaternion.identity, 0f, 0, 0);
                    sh.CarveEll(g, H(0f, 0.254f, -0.012f), H(0.017f, 0.05f, 0.088f), Quaternion.identity, 0.012f * hs);
                    for (int e = -1; e <= 1; e += 2) sh.CarveEll(g, H(e * 0.06f, 0.222f, 0.068f), H(0.026f, 0.05f, 0.04f), Quaternion.Euler(0f, e * 30f, 0f), 0.01f * hs);
                    break;
                }
                case Hat.Fire:
                {
                    Color[] hc = { t.main, new Color(0.1f, 0.1f, 0.12f), new Color(0.98f, 0.8f, 0.14f) };
                    crown = brim = Cl(hc[st % 3], 0.85f);
                    band = crown;
                    cr = H(0.103f, 0.104f, 0.122f); cc = H(0f, 0.13f, -0.012f);
                    sh.Ell(g, cc, cr, Quaternion.identity, 0f, 0, 0);
                    sh.Ell(g, H(0f, 0.226f, -0.012f), H(0.014f, 0.016f, 0.108f), Quaternion.identity, 0.012f * hs, 0, 0);
                    for (int e = -1; e <= 1; e += 2) sh.Ell(g, H(e * 0.042f, 0.214f, -0.012f), H(0.011f, 0.013f, 0.1f), Quaternion.Euler(0f, 0f, e * -24f), 0.01f * hs, 0, 0);
                    // a leather front shield in a brass rim, a brass eagle on top of it
                    var front = sh.Branch(g, 0.003f * hs);
                    sh.Ell(front, H(0f, 0.166f, 0.114f), H(0.048f, 0.056f, 0.011f), Quaternion.Euler(-24f, 0f, 0f), 0f, 2, 0);
                    sh.Ell(front, H(0f, 0.166f, 0.12f), H(0.039f, 0.046f, 0.011f), Quaternion.Euler(-24f, 0f, 0f), 0.002f * hs, 1, 0);
                    sh.Ell(g, H(0f, 0.238f, 0.086f), H(0.017f, 0.015f, 0.03f), Quaternion.Euler(-20f, 0f, 0f), 0.006f * hs, 2, 0);
                    badge = H(0f, 0.166f, 0.134f);
                    yBrim = 0.1f;
                    break;
                }
                default:   // bowler
                {
                    Color c0 = st % 2 == 0 ? t.main : new Color(0.1f, 0.1f, 0.12f);
                    crown = brim = Cl(c0, 0.35f);
                    band = Cl(L(c0) < 0.2f ? t.main : new Color(0.08f, 0.08f, 0.1f), 0.5f);
                    cr = H(0.095f, 0.096f, 0.107f); cc = H(0f, 0.14f, -0.004f);
                    sh.Ell(g, cc, cr, Quaternion.identity, 0f, 0, 0);
                    break;
                }
            }
            sh.ClipPlane(g, Vector3.down, H(0f, 0.1f, 0f));
            sh.Bake();
            var m = SdfMesh.Build(sh, 0.0068f * hs);
            m.ComputeLabels();
            m.CutLabels();
            m.FixTJunctions();
            Color shield = Pal.Gloss(st % 2 == 0 ? new Color(0.08f, 0.08f, 0.1f) : new Color(0.96f, 0.95f, 0.92f), 0.6f), brass = Pal.Gloss(new Color(0.88f, 0.7f, 0.3f), 0.9f);
            Color cCrown = crown;
            // the crown's flat underside sits inside the head: leave it out
            float yCut = 0.1f * hs + 0.004f * hs;
            int baseIdx = k.V.Count;
            var remap = new int[m.P.Count];
            for (int i = 0; i < remap.Length; i++) remap[i] = -1;
            for (int ti = 0; ti < m.T.Count; ti += 3)
            {
                int a = m.T[ti], b0 = m.T[ti + 1], c0 = m.T[ti + 2];
                if (m.P[a].y < yCut && m.P[b0].y < yCut && m.P[c0].y < yCut && m.N[a].y < -0.6f && m.N[b0].y < -0.6f && m.N[c0].y < -0.6f) continue;
                foreach (int vi in new[] { a, b0, c0 })
                {
                    if (remap[vi] < 0)
                    {
                        int l = m.L != null && vi < m.L.Count ? m.L[vi] : 0;
                        Color col = l == 1 ? shield : l == 2 ? brass : cCrown;
                        remap[vi] = k.Vert(m.P[vi], m.N[vi], col, MeshKit.Plain);
                    }
                    k.T.Add(remap[vi]);
                }
            }
            // band round the crown, then the brim
            Vector3 c0v = new Vector3(0f, 0f, cc.z);
            float bh = (hat == Hat.Cowboy ? 0.022f : hat == Hat.Fire ? 0f : 0.026f) * hs;
            if (bh > 0f)
                Brim(k, c0v, ph => CrownR(cc, cr, bandY * hs, ph) - bh * 0.5f + 0.002f * hs, ph => CrownR(cc, cr, bandY * hs, ph) + bh * 0.5f + 0.002f * hs,
                     ph => bandY * hs, ph => 90f, 0.009f * hs, band, 48, 6);
            switch (hat)
            {
                case Hat.Bucket:
                    Brim(k, c0v, ph => CrownR(cc, cr, yBrim * hs, ph) - 0.01f * hs, ph => CrownR(cc, cr, yBrim * hs, ph) + 0.066f * hs,
                         ph => yBrim * hs, ph => 26f, 0.012f * hs, brim, 56, 10);
                    break;
                case Hat.Straw:
                    Brim(k, c0v, ph => CrownR(cc, cr, yBrim * hs, ph) - 0.01f * hs, ph => CrownR(cc, cr, yBrim * hs, ph) + 0.105f * hs,
                         ph => yBrim * hs, ph => 6f + 9f * Mathf.Cos(ph) * Mathf.Cos(ph), 0.009f * hs, brim, 60, 10);
                    break;
                case Hat.Cowboy:
                    Brim(k, c0v, ph => CrownR(cc, cr, yBrim * hs, ph) - 0.01f * hs, ph => CrownR(cc, cr, yBrim * hs, ph) + 0.105f * hs,
                         ph => (yBrim + 0.012f * Mathf.Sin(ph) * Mathf.Sin(ph)) * hs, ph => 10f * Mathf.Cos(ph) * Mathf.Cos(ph) - 34f * Mathf.Sin(ph) * Mathf.Sin(ph), 0.011f * hs, brim, 60, 10);
                    break;
                case Hat.Fire:
                    Brim(k, c0v, ph => CrownR(cc, cr, yBrim * hs, ph) - 0.012f * hs,
                         ph => CrownR(cc, cr, yBrim * hs, ph) + (0.04f + 0.08f * Smooth01((-Mathf.Cos(ph) - 0.1f) / 0.9f)) * hs,
                         ph => yBrim * hs, ph => Mathf.Lerp(-6f, 30f, Smooth01((1f - Mathf.Cos(ph)) * 0.5f)), 0.013f * hs, brim, 60, 10);
                    break;
                default:
                    Brim(k, c0v, ph => CrownR(cc, cr, yBrim * hs, ph) - 0.01f * hs, ph => CrownR(cc, cr, yBrim * hs, ph) + 0.038f * hs,
                         ph => (yBrim + 0.01f * Mathf.Sin(ph) * Mathf.Sin(ph)) * hs, ph => 8f * Mathf.Cos(ph) * Mathf.Cos(ph) - 38f * Mathf.Sin(ph) * Mathf.Sin(ph), 0.01f * hs, brim, 52, 10);
                    break;
            }
        }

        /// <summary>Which points sit inside the cap's crown (a cap or a helmet is always worn, so nothing there ever shows).</summary>
        static bool[] UnderCap(Body b, Vector3[] rp, IList<Vector3> pts)
        {
            float s = b.height / 1.85f, hs = s * PlayerMesh.HeadOf(b) * (b.hair == BananaBall.Hair.Afro ? 1.22f : 1f);
            Vector3 c = rp[4] + new Vector3(0f, 0.122f, -0.004f) * hs, r = new Vector3(0.087f, 0.085f, 0.1f) * hs;
            float rim = c.y + 0.012f * hs;
            var res = new bool[pts.Count];
            for (int i = 0; i < pts.Count; i++)
            {
                Vector3 p = pts[i];
                if (p.y < rim) continue;
                Vector3 q = p - c;
                q.x /= r.x; q.y /= r.y; q.z /= r.z;
                res[i] = q.sqrMagnitude < 0.93f;
            }
            return res;
        }

        static int Wrap(MeshKit k, int baseIdx, int i, float R)
        {
            int src = baseIdx + i;
            if (k.UV[src].x >= R * 0.5f) return src;
            k.V.Add(k.V[src]); k.N.Add(k.N[src]); k.C.Add(k.C[src]); k.UV.Add(new Vector2(k.UV[src].x + R, k.UV[src].y));
            k.UV2.Add(k.UV2[src]); k.OL.Add(k.OL[src]); k.W.Add(k.W[src]);
            return k.V.Count - 1;
        }

        static void Acc(int bone, float w, Vector3 p, Vector3 n, ref Vector3 pp, ref Vector3 nn, Vector3 SL, Vector3 SR, Quaternion invL, Quaternion invR)
        {
            if (w <= 0f) return;
            if (bone >= 8 && bone <= 10) { pp += (SL + invL * (p - SL)) * w; nn += invL * n * w; }
            else if (bone >= 11 && bone <= 13) { pp += (SR + invR * (p - SR)) * w; nn += invR * n * w; }
            else { pp += p * w; nn += n * w; }
        }

        /// <summary>Re-split the weight a vertex has on bones a and b: all on a well before the joint (signed distance d &lt; -w along
        /// the limb), all on b well after it, a smooth blend across the band.</summary>
        static void Rebalance(float[] acc, int a, int b, float d, float w)
        {
            float tot = acc[a] + acc[b];
            if (tot <= 1e-5f) return;
            float f = Smooth01((d + w) / (2f * w));
            acc[a] = tot * (1f - f);
            acc[b] = tot * f;
        }

        /// <summary>Move the skin across each joint from parent - child onto parent - helper - child (see PlayerMesh.Helpers):
        /// the first half of the blend goes parent to helper, the second half helper to child, so the average turn stays the same
        /// while no vertex mixes two transforms more than half the joint's angle apart.</summary>
        static void SplitHelpers(float[] acc)
        {
            for (int h = 0; h < PlayerMesh.Helpers; h++)
            {
                int pa = PlayerMesh.HelperParent[h], ch = PlayerMesh.HelperChild[h];
                if (pa == 2) continue;   // shoulders stay a ball joint (see the arm re-blend in MakeBody)
                float a = acc[pa], c = acc[ch];
                if (a <= 1e-5f || c <= 1e-5f) continue;
                float tot = a + c, f = c / tot;
                if (f < 0.5f) { float u = 2f * f; acc[pa] = tot * (1f - u); acc[20 + h] += tot * u; acc[ch] = 0f; }
                else { float u = 2f * f - 1f; acc[pa] = 0f; acc[20 + h] += tot * (1f - u); acc[ch] = tot * u; }
            }
        }

        /// <summary>Ambient occlusion of a surface point from its own distance field: how far the field falls short of open
        /// space a few steps out along the normal (1 = open, lower = in a crease).</summary>
        public static float Occlusion(SdfShape sh, Vector3 p, Vector3 n, float step, float strength)
        {
            float occ = 0f, tot = 0f, wt = 1f;
            for (int i = 1; i <= 4; i++)
            {
                float hh = step * i;
                float d = sh.Eval(p + n * hh);
                occ += wt * Mathf.Clamp01((hh - d) / hh);
                tot += wt;
                wt *= 0.65f;
            }
            return 1f - strength * occ / tot;
        }

        static void TorsoBones(float y, float w, float[] acc, int o)
        {
            if (y < 0.04f) acc[o] += w;
            else if (y < 0.14f) { float f = Smooth01((y - 0.04f) / 0.10f); acc[o] += w * (1f - f); acc[o + 1] += w * f; }
            else if (y < 0.24f) acc[o + 1] += w;
            else if (y < 0.33f) { float f = Smooth01((y - 0.24f) / 0.09f); acc[o + 1] += w * (1f - f); acc[o + 2] += w * f; }
            else if (y < 0.53f) acc[o + 2] += w;
            else { float f = Smooth01((y - 0.53f) / 0.04f); acc[o + 2] += w * (1f - f); acc[o + 3] += w * f; }
        }

        static int SkinGroup(int lab)
        {
            switch (lab)
            {
                case LTorso: return 0;
                case LNeck: case LHead: case LMuzzle: return 1;
                case LArmL: case LHandL: return 2;
                case LArmR: case LHandR: return 3;
                case LLegL: case LShoeL: return 4;
                default: return 5;
            }
        }

        /// <summary>Laplacian smoothing of per-vertex bone weights (20 per vertex) on the welded surface.</summary>
        static void SmoothWeights(SdfMesh m, float[] w, int iterations)
        {
            int nv = m.P.Count;
            var weld = new int[nv];
            var map = new Dictionary<Vector3Int, int>(nv);
            for (int i = 0; i < nv; i++)
            {
                Vector3 p = m.P[i];
                var key = new Vector3Int(Mathf.RoundToInt(p.x * 20000f), Mathf.RoundToInt(p.y * 20000f), Mathf.RoundToInt(p.z * 20000f));
                if (!map.TryGetValue(key, out int id)) { id = map.Count; map[key] = id; }
                weld[i] = id;
            }
            int nw = map.Count;
            var cur = new float[nw * 20];
            var cnt = new int[nw];
            for (int i = 0; i < nv; i++)
            {
                int o = weld[i] * 20, so = i * 20;
                for (int j = 0; j < 20; j++) cur[o + j] += w[so + j];
                cnt[weld[i]]++;
            }
            for (int i = 0; i < nw; i++) { float inv = 1f / Mathf.Max(1, cnt[i]); for (int j = 0; j < 20; j++) cur[i * 20 + j] *= inv; }
            // adjacency (CSR)
            var deg = new int[nw + 1];
            var T = m.T;
            for (int t = 0; t < T.Count; t += 3)
                for (int e = 0; e < 3; e++) { deg[weld[T[t + e]]] += 2; }
            var start = new int[nw + 1];
            for (int i = 0; i < nw; i++) start[i + 1] = start[i] + deg[i];
            var adj = new int[start[nw]];
            var fill = new int[nw];
            for (int t = 0; t < T.Count; t += 3)
                for (int e = 0; e < 3; e++)
                {
                    int a = weld[T[t + e]], b = weld[T[t + (e + 1) % 3]], c = weld[T[t + (e + 2) % 3]];
                    adj[start[a] + fill[a]++] = b;
                    adj[start[a] + fill[a]++] = c;
                }
            var nxt = new float[nw * 20];
            for (int it = 0; it < iterations; it++)
            {
                var src = cur;
                var dst = nxt;
                Parallel.For(0, nw, i =>
                {
                    int s0 = start[i], s1 = start[i] + fill[i];
                    int o = i * 20;
                    if (s1 <= s0) { for (int j = 0; j < 20; j++) dst[o + j] = src[o + j]; return; }
                    float inv = 1f / (s1 - s0);
                    for (int j = 0; j < 20; j++)
                    {
                        float avg = 0f;
                        for (int q = s0; q < s1; q++) avg += src[adj[q] * 20 + j];
                        dst[o + j] = 0.5f * src[o + j] + 0.5f * avg * inv;
                    }
                });
                nxt = cur;
                cur = dst;
            }
            for (int i = 0; i < nv; i++) System.Array.Copy(cur, weld[i] * 20, w, i * 20, 20);
        }

        static BoneWeight Top4(float[] acc)
        {
            int i0 = -1, i1 = -1, i2 = -1, i3 = -1;
            for (int i = 0; i < acc.Length; i++)
            {
                float v = acc[i];
                if (v <= 0f) continue;
                if (i0 < 0 || v > acc[i0]) { i3 = i2; i2 = i1; i1 = i0; i0 = i; }
                else if (i1 < 0 || v > acc[i1]) { i3 = i2; i2 = i1; i1 = i; }
                else if (i2 < 0 || v > acc[i2]) { i3 = i2; i2 = i; }
                else if (i3 < 0 || v > acc[i3]) i3 = i;
            }
            if (i0 < 0) return new BoneWeight { boneIndex0 = 0, weight0 = 1f };
            float w0 = acc[i0], w1 = i1 >= 0 ? acc[i1] : 0f, w2 = i2 >= 0 ? acc[i2] : 0f, w3 = i3 >= 0 ? acc[i3] : 0f;
            // drop crumbs so far-off bones never tug at a vertex
            float tot = w0 + w1 + w2 + w3;
            if (w3 < tot * 0.02f) w3 = 0f;
            if (w2 < tot * 0.02f) w2 = 0f;
            if (w1 < tot * 0.02f) w1 = 0f;
            tot = w0 + w1 + w2 + w3;
            return new BoneWeight
            {
                boneIndex0 = i0, weight0 = w0 / tot,
                boneIndex1 = Mathf.Max(i1, 0), weight1 = w1 / tot,
                boneIndex2 = Mathf.Max(i2, 0), weight2 = w2 / tot,
                boneIndex3 = Mathf.Max(i3, 0), weight3 = w3 / tot,
            };
        }

        /// <summary>Point on the head surface along the ray from the cranium centre through 'nominal'.</summary>
        public static Vector3 Snap(Info info, Vector3 nominal)
        {
            Vector3 c = info.headCenter, d = nominal - c;
            float L = d.magnitude;
            if (L < 1e-5f) return nominal;
            d /= L;
            Vector3 from = c + d * (L + 0.12f);
            return info.shape.March(from, -d, L + 0.2f);
        }

        // ------------------------------------------------------------------ fine parts (hands, cleats, ears)

        /// <summary>Hands, cleats and ears: small parts with folds the body's grid cannot hold. Each is built on its own fine grid in
        /// its bone's frame and skinned rigidly to that bone in the bind pose (PlayerMesh.BindPose).</summary>
        public static void Parts(MeshKit k, Body b, TeamData t, Vector3[] rp, float coarse, Info body)
        {
            var bpP = new Vector3[PlayerMesh.BoneCount];
            var bpQ = new Quaternion[PlayerMesh.BoneCount];
            PlayerMesh.BindPose(rp, bpP, bpQ);
            float partCoarse = coarse;
            if (coarse > 1.2f) coarse *= 1.45f;   // the far body: small parts get coarser still (they are a few pixels there)
            var cw = Wardrobe.Of(b, t);
            for (int side = 0; side < 2; side++)
            {
                int hb = side == 0 ? 10 : 13;
                Hand(k, b, side, coarse, hb, bpP[hb], bpQ[hb]);
                if (!b.ape)
                {
                    int fb = side == 0 ? 16 : 19;
                    int sd = side;
                    if (b.feet == Feet.FlipFlops) TryPart("flip-flop", () => FlipFlop(k, b, cw, sd, coarse, fb, bpP[fb], bpQ[fb]));
                    else Shoe(k, b, t, side, coarse, fb, bpP[fb], bpQ[fb], b.feet == Feet.Boots ? cw : null);
                }
                if (b.Has(Extra.Wings))
                {
                    int sb = side == 0 ? 8 : 11, sd = side;
                    TryPart("water wings", () => Wing(k, b, cw, sd, coarse, rp, bpP[sb], bpQ[sb]));
                }
            }
            if (body != null) Ears(k, b, rp, coarse, body);
            if (body == null) return;
            if (b.bottom == Bottom.Kilt) TryPart("kilt", () => Kilt(k, b, cw, rp, partCoarse, body));
            if (b.Has(Extra.Floatie)) TryPart("swim ring", () => Floatie(k, b, cw, rp, partCoarse, body));
            if (b.Has(Extra.Lei)) TryPart("lei", () => Lei(k, b, cw, rp, partCoarse, body));
            if (b.Has(Extra.Bandana)) TryPart("bandana", () => Bandana(k, b, cw, rp, partCoarse, body));
            if (b.Has(Extra.BowTie)) TryPart("bow tie", () => BowTie(k, b, cw, rp, partCoarse, body));
            if (b.Has(Extra.Snorkel)) TryPart("snorkel", () => Snorkel(k, b, cw, rp, body));
        }

        /// <summary>Append a part skinned 100% to one bone. The part's mesh is in the bone's frame; (bp, bq) is the bone in the bind
        /// pose. Colours get ambient occlusion from the part's own distance field.</summary>
        static void EmitOnBone(MeshKit k, SdfMesh m, int bone, Vector3 bp, Quaternion bq, Func<int, Vector3, Color> colorOf, float aoStep, float aoStrength, float ink = 1f)
        {
            int nv = m.P.Count;
            var col = new Color[nv];
            Parallel.For(0, nv, i =>
            {
                Vector3 p = m.P[i];
                Color c = colorOf(m.L != null && i < m.L.Count ? m.L[i] : 0, p + m.Bias[i]);
                float ao = aoStrength > 0f ? Occlusion(m.shape, p, m.N[i], aoStep, aoStrength) : 1f;
                c.r *= ao; c.g *= ao; c.b *= ao;
                col[i] = c;
            });
            int baseIdx = k.V.Count;
            for (int i = 0; i < nv; i++)
            {
                k.V.Add(bp + bq * m.P[i]); k.N.Add(bq * m.N[i]); k.C.Add(col[i]);
                k.UV.Add(MeshKit.Plain); k.UV2.Add(Vector2.zero); k.OL.Add(ink);
                k.W.Add(new BoneWeight { boneIndex0 = bone, weight0 = 1f });
            }
            foreach (int i in m.T) k.T.Add(baseIdx + i);
        }

        /// <summary>A cartoon hand on the hand bone: a soft palm, four fingers fanned a little and curled toward the palm, a thumb in
        /// front. Fingers point down the arm (-y), the palm faces the body's midline and the thumb points forward (+z), as a hand hangs
        /// at rest. The wrist stub hides inside the forearm's end.</summary>
        static void Hand(MeshKit k, Body b, int side, float coarse, int bone, Vector3 bp, Quaternion bq)
        {
            float s = b.height / 1.85f, u = PlayerMesh.HandK * s * (b.ape ? 1.28f : 1f);
            float fat = b.ape ? 1.18f : 1f;
            float inX = side == 0 ? 1f : -1f;
            var sh = new SdfShape();
            var palm = sh.Root;
            sh.Cone(palm, new Vector3(0f, 0.018f, 0f) * u, new Vector3(0f, -0.022f, 0.002f) * u, 0.02f * u, 0.023f * u, 0f, 0, 0);
            sh.Ell(palm, new Vector3(-inX * 0.0015f, -0.047f, 0.004f) * u, new Vector3(0.0185f * fat, 0.032f, 0.0295f) * u, Quaternion.identity, 0.012f * u, 0, 0);
            // the ball of the thumb
            sh.Ell(palm, new Vector3(inX * 0.004f, -0.034f, 0.017f) * u, new Vector3(0.012f, 0.017f, 0.011f) * u * fat, Quaternion.identity, 0.008f * u, 0, 0);
            float[] zs = { 0.0235f, 0.0079f, -0.0079f, -0.0235f };
            float[] fan = { 0.12f, 0.04f, -0.04f, -0.13f };
            float[] l1 = { 0.019f, 0.021f, 0.019f, 0.0155f }, l2 = { 0.016f, 0.0175f, 0.016f, 0.013f };
            float[] rf = { 0.0074f, 0.0076f, 0.0073f, 0.0066f };
            float fk = (coarse > 1.2f ? 0.009f : 0.0028f) * u;   // far away the fingers melt into a mitten
            for (int f = 0; f < 4; f++)
            {
                var fg = sh.Branch(palm, fk);
                Vector3 kn = new Vector3(-inX * 0.0015f, -0.072f, zs[f] + 0.002f) * u;
                Vector3 d0 = new Vector3(0f, -1f, fan[f]).normalized;
                Vector3 d1 = Quaternion.Euler(0f, 0f, inX * 16f) * d0;
                Vector3 d2 = Quaternion.Euler(0f, 0f, inX * 52f) * d0;
                Vector3 m1 = kn + d1 * (l1[f] * u), tip = m1 + d2 * (l2[f] * u);
                float r = rf[f] * u * fat;
                sh.Cone(fg, kn - d1 * (0.006f * u), m1, r, r * 0.97f, 0f, 1, 0);
                sh.Cone(fg, m1, tip, r * 0.97f, r * 0.9f, 0.003f * u, 1, 0);
            }
            var th = sh.Branch(palm, 0.006f * u);
            Vector3 t0 = new Vector3(inX * 0.006f, -0.03f, 0.022f) * u;
            Vector3 t1 = t0 + new Vector3(inX * 0.35f, -0.6f, 0.72f).normalized * (0.021f * u);
            Vector3 t2 = t1 + new Vector3(inX * 0.62f, -0.6f, 0.5f).normalized * (0.018f * u);
            sh.Cone(th, t0, t1, 0.0098f * u * fat, 0.009f * u * fat, 0f, 1, 0);
            sh.Cone(th, t1, t2, 0.009f * u * fat, 0.0082f * u * fat, 0.003f * u, 1, 0);
            sh.Bake();
            var m = SdfMesh.Build(sh, 0.0028f * u * coarse);
            Color skin = b.ape ? Pal.Gloss(new Color(0.33f, 0.29f, 0.29f), 0.3f) : SkinC(b.skin);
            // a lighter line round the hand: the bold body ink would turn the fingers into a black mitten up close
            EmitOnBone(k, m, bone, bp, bq, (l, p) => skin, 0.007f * u, 0.38f, 0.5f);
        }

        /// <summary>A baseball cleat on the foot bone (origin at the ankle, toes toward +z, the ground at y = -AnkleY): a heel cup,
        /// an instep and a round toe box over a white sole with a little toe spring, a padded collar round the sock, a tongue, laces,
        /// a team swoosh on the outside and a team heel tab. Studs underneath show on slides and leg kicks.</summary>
        static void Shoe(MeshKit k, Body b, TeamData t, int side, float coarse, int bone, Vector3 bp, Quaternion bq, Wardrobe boot = null)
        {
            float f = PlayerMesh.FootK * b.height / 1.85f;
            float sg = side == 0 ? -1f : 1f;        // the outer side of this foot
            const int LUp = 0, LSole = 1, LCollar = 2, LTongue = 3, LStud = 4, LLace = 5;
            var sh = new SdfShape();
            var all = sh.Root;
            var shoe = sh.Branch(all, 0f);
            sh.Ell(shoe, new Vector3(0f, -0.036f, -0.028f) * f, new Vector3(0.047f, 0.041f, 0.046f) * f, Quaternion.identity, 0f, LUp, 0);
            sh.Ell(shoe, new Vector3(0f, -0.037f, 0.05f) * f, new Vector3(0.053f, 0.038f, 0.072f) * f, Quaternion.Euler(6f, 0f, 0f), 0.022f * f, LUp, 0);
            sh.Ell(shoe, new Vector3(0f, -0.049f, 0.124f) * f, new Vector3(0.05f, 0.03f, 0.054f) * f, Quaternion.identity, 0.024f * f, LUp, 0);
            sh.Ell(shoe, new Vector3(0f, -0.006f, 0.032f) * f, new Vector3(0.03f, 0.014f, 0.034f) * f, Quaternion.Euler(-28f, 0f, 0f), 0.01f * f, LTongue, 0);
            const int NC = 16;
            for (int i = 0; i < NC; i++)
            {
                float a0 = Mathf.PI * 2f * i / NC, a1 = Mathf.PI * 2f * (i + 1) / NC;
                Vector3 p0 = new Vector3(0.043f * Mathf.Sin(a0), -0.006f - 0.008f * Mathf.Cos(a0), -0.008f + 0.044f * Mathf.Cos(a0)) * f;
                Vector3 p1 = new Vector3(0.043f * Mathf.Sin(a1), -0.006f - 0.008f * Mathf.Cos(a1), -0.008f + 0.044f * Mathf.Cos(a1)) * f;
                sh.Cone(shoe, p0, p1, 0.0105f * f, 0.0105f * f, 0.006f * f, LCollar, 0);
            }
            sh.Ell(shoe, new Vector3(0f, -0.073f, -0.026f) * f, new Vector3(0.047f, 0.013f, 0.05f) * f, Quaternion.identity, 0.006f * f, LSole, 0);
            sh.Ell(shoe, new Vector3(0f, -0.074f, 0.054f) * f, new Vector3(0.055f, 0.013f, 0.082f) * f, Quaternion.identity, 0.02f * f, LSole, 0);
            sh.Ell(shoe, new Vector3(0f, -0.069f, 0.131f) * f, new Vector3(0.05f, 0.014f, 0.055f) * f, Quaternion.Euler(-9f, 0f, 0f), 0.02f * f, LSole, 0);
            sh.ClipPlane(shoe, Vector3.down, new Vector3(0f, -0.084f, 0f) * f);
            sh.CarveEll(shoe, new Vector3(0f, 0.012f, -0.008f) * f, new Vector3(0.034f, 0.032f, 0.036f) * f, Quaternion.identity, 0.004f * f);
            if (boot == null)
            {
                var studs = sh.Branch(all, 0f);
                float[] sx = { -0.027f, 0.027f, 0f, -0.031f, 0.031f, -0.024f, 0.024f };
                float[] sz = { 0.13f, 0.13f, 0.158f, 0.072f, 0.072f, -0.036f, -0.036f };
                for (int i = 0; i < sx.Length; i++)
                    sh.Cone(studs, new Vector3(sx[i], -0.081f, sz[i]) * f, new Vector3(sx[i], -0.093f, sz[i]) * f, 0.0085f * f, 0.0055f * f, 0f, LStud, 0);
            }
            else if (!boot.fireBoots)
            {
                // a cowboy boot's stacked heel
                var heel = sh.Branch(all, 0f);
                sh.Ell(heel, new Vector3(0f, -0.084f, -0.03f) * f, new Vector3(0.034f, 0.016f, 0.03f) * f, Quaternion.identity, 0f, LSole, 0);
                sh.ClipPlane(heel, Vector3.down, new Vector3(0f, -0.094f, 0f) * f);
            }
            sh.Bake();
            // laces: three white bars painted across the top of the instep
            var lz = new float[3]; var ly = new float[3]; var lw = new float[3];
            for (int i = 0; i < 3; i++)
            {
                lz[i] = 0.044f + 0.024f * i;
                ly[i] = sh.March(new Vector3(0f, 0.1f * f, lz[i] * f), Vector3.down, 0.3f * f).y / f;
                lw[i] = 0.021f - 0.002f * i;
            }
            float Lace(Vector3 p)
            {
                float d = 1f;
                for (int i = 0; i < 3; i++)
                    d = Mathf.Min(d, Mathf.Max(Mathf.Max(Mathf.Abs(p.z / f - lz[i]) - 0.0042f, Mathf.Abs(p.x / f) - lw[i]), ly[i] - 0.012f - p.y / f));
                return d * f;
            }
            var m = SdfMesh.Build(sh, 0.0042f * f * coarse);
            m.ComputeLabels();
            m.CutLabels();
            m.Cut(Lace, l => l == LUp || l == LTongue);
            float Swoosh(Vector3 p) => Mathf.Max(Mathf.Max(Mathf.Abs((p.y / f + 0.033f) - 0.26f * (p.z / f - 0.05f)) - 0.0085f, 0.022f - p.x / f * sg), Mathf.Abs(p.z / f - 0.058f) - 0.072f) * f;
            float Tab(Vector3 p) => Mathf.Max(Mathf.Abs(p.x / f) - 0.014f, Mathf.Max(-0.058f - p.z / f, -0.03f - p.y / f)) * f;
            m.Cut(Swoosh, l => l == LUp);
            m.Cut(Tab, l => l == LUp || l == LCollar);
            m.FixTJunctions();
            Color upper = Pal.Gloss(Pal.Cleat, 0.55f), sole = Pal.Gloss(new Color(0.93f, 0.93f, 0.95f), 0.3f), team = Cloth(t.main);
            Color lace = Pal.Gloss(new Color(0.95f, 0.95f, 0.96f), 0.25f), stud = Pal.Gloss(new Color(0.22f, 0.22f, 0.25f), 0.4f);
            Color collar = Pal.Gloss(Color.Lerp(Pal.Cleat, Color.white, 0.08f), 0.3f);
            if (boot != null)
            {
                // a plain leather boot foot (its shaft is the leg's lower end): no laces, no swoosh
                upper = collar = lace = team = boot.boot;
                sole = stud = boot.bootSole;
            }
            EmitOnBone(k, m, bone, bp, bq, (l, p) =>
            {
                switch (l)
                {
                    case LSole: return sole;
                    case LStud: return stud;
                    case LLace: return lace;
                    case LCollar: return Tab(p) < 0f ? team : collar;
                    case LTongue: return Lace(p) < 0f ? lace : upper;
                    default: return Lace(p) < 0f ? lace : Swoosh(p) < 0f || Tab(p) < 0f ? team : upper;
                }
            }, 0.008f * f, 0.4f);
        }

        /// <summary>Ears on the head bone: a rim, a lobe and a bowl carved in from the outside, pressed onto the side of the head.</summary>
        static void Ears(MeshKit k, Body b, Vector3[] rp, float coarse, Info body)
        {
            float s = b.height / 1.85f, hs = s * PlayerMesh.HeadOf(b);
            float ek = b.ears * (b.ape ? 0.78f : 1f);
            var sh = new SdfShape();
            for (int e = -1; e <= 1; e += 2)
            {
                var g = sh.Branch(sh.Root, 0f);
                Vector3 nominal = rp[4] + new Vector3(0f, 0.082f, -0.006f) * hs;
                float xs = body.shape.March(nominal + new Vector3(e * 0.3f * hs, 0f, 0f), new Vector3(-e, 0f, 0f), 0.6f * hs).x;
                Vector3 c = new Vector3(xs + e * 0.0035f * hs, nominal.y, nominal.z);
                Quaternion q = Quaternion.Euler(0f, e * -20f, e * -5f);   // turned a little forward
                sh.Ell(g, c, new Vector3(0.0105f, 0.025f, 0.0175f) * hs * ek, q, 0f, 0, 4);
                sh.Ell(g, c + new Vector3(e * 0.001f, -0.018f, 0.004f) * hs * ek, new Vector3(0.008f, 0.009f, 0.009f) * hs * ek, q, 0.006f * hs, 0, 4);
                sh.CarveEll(g, c + q * new Vector3(e * 0.0105f, 0.002f, 0.002f) * hs * ek, new Vector3(0.0062f, 0.0155f, 0.0105f) * hs * ek, q, 0.0025f * hs);
            }
            sh.Bake();
            var m = SdfMesh.Build(sh, 0.0024f * hs * coarse);
            Color skin = b.ape ? Pal.Gloss(new Color(0.24f, 0.22f, 0.24f), 0.15f) : SkinC(b.skin);
            EmitOnBone(k, m, 4, Vector3.zero, Quaternion.identity, (l, p) => skin, 0.006f * hs, 0.55f, 0.7f);
        }

        /// <summary>Hair that shows below the cap: sideburns, the back of the head, and the long styles. Skinned to the head.</summary>
        public static void MakeHair(MeshKit k, Body b, Vector3[] rp, Info body = null)
        {
            if (b.hair == BananaBall.Hair.Bald) return;
            float s = b.height / 1.85f, hs = s * PlayerMesh.HeadOf(b);
            Vector3 Hd(float x, float y, float z) => rp[4] + new Vector3(x, y, z) * hs;
            var sh = new SdfShape();
            var g = sh.Root;
            BananaBall.Hair st = b.hair;
            float th = st == BananaBall.Hair.Buzz || st == BananaBall.Hair.Mohawk ? 0.0032f : st == BananaBall.Hair.Afro ? 0.036f : 0.0065f;
            Vector3 cr = new Vector3(0.085f, 0.092f, 0.088f) * hs;
            sh.Ell(g, Hd(0f, 0.1f, 0f), cr + Vector3.one * th * hs, Quaternion.identity, 0f, 0, 4);
            float nape = 0.045f;
            switch (st)
            {
                case BananaBall.Hair.Mullet:
                    sh.Ell(g, Hd(0f, 0.035f, -0.074f), new Vector3(0.07f, 0.074f, 0.034f) * hs, Quaternion.Euler(14f, 0f, 0f), 0.022f * hs, 0, 4);
                    nape = -0.05f;
                    break;
                case BananaBall.Hair.Long:
                    sh.Ell(g, Hd(0f, -0.012f, -0.08f), new Vector3(0.082f, 0.115f, 0.034f) * hs, Quaternion.Euler(8f, 0f, 0f), 0.022f * hs, 0, 4);
                    nape = -0.135f;
                    break;
                case BananaBall.Hair.Ponytail:
                    sh.Cone(g, Hd(0f, 0.112f, -0.09f), Hd(0f, 0.0f, -0.15f), 0.026f * hs, 0.012f * hs, 0.014f * hs, 0, 4);
                    nape = -0.03f;
                    break;
                case BananaBall.Hair.Afro:
                    nape = 0.02f;
                    break;
            }
            // hairline: nothing in front of the ears or on the forehead (the cap covers the top), nothing below the nape
            float front = st == BananaBall.Hair.Afro ? 0.045f : 0.022f;
            sh.ClipPlane(g, new Vector3(0f, -0.6f, 1f), Hd(0f, 0.08f, front), 0.004f * hs);
            sh.ClipPlane(g, new Vector3(0f, -1f, 0.34f), Hd(0f, nape, -0.06f), 0.006f * hs);
            for (int e = -1; e <= 1; e += 2)
                sh.CarveEll(g, Hd(e * 0.085f, 0.084f, -0.004f), new Vector3(0.0135f, 0.026f, 0.019f) * hs * b.ears + Vector3.one * 0.006f * hs, Quaternion.Euler(0f, e * 18f, 0f), 0.004f * hs);
            sh.Bake();
            var m = SdfMesh.Build(sh, 0.0068f * s * (body != null ? Mathf.Min(body.coarse, 2.2f) : 1f));
            Color hc = st == BananaBall.Hair.Buzz ? Color.Lerp(b.hairColor, b.skin, 0.35f) : st == BananaBall.Hair.Mohawk ? Color.Lerp(b.hairColor, b.skin, 0.55f) : b.hairColor;
            hc = HairC(hc);
            // faces buried in the head never show: keep only what is outside the skin
            int nv = m.P.Count;
            var inside = UnderCap(b, rp, m.P);
            if (body != null)
            {
                float lim = -0.0012f * hs;
                Parallel.For(0, nv, i => { if (!inside[i]) inside[i] = body.shape.Eval(m.P[i]) < lim; });
            }
            var remap = new int[nv];
            for (int i = 0; i < nv; i++) remap[i] = -1;
            for (int i = 0; i < m.T.Count; i += 3)
            {
                int a = m.T[i], bb = m.T[i + 1], c = m.T[i + 2];
                if (inside[a] && inside[bb] && inside[c]) continue;
                k.T.Add(Remap(k, m, remap, a, hc)); k.T.Add(Remap(k, m, remap, bb, hc)); k.T.Add(Remap(k, m, remap, c, hc));
            }
        }

        static int Remap(MeshKit k, SdfMesh m, int[] remap, int i, Color hc)
        {
            if (remap[i] >= 0) return remap[i];
            k.V.Add(m.P[i]); k.N.Add(m.N[i]); k.C.Add(hc); k.UV.Add(MeshKit.Plain); k.UV2.Add(Vector2.zero); k.OL.Add(1f);
            k.W.Add(new BoneWeight { boneIndex0 = 4, weight0 = 1f });
            remap[i] = k.V.Count - 1;
            return remap[i];
        }

        /// <summary>Facial hair as soft shapes that hug the face (mustaches curve back along the cheeks, beards are shells
        /// over the jaw). Skinned to the head.</summary>
        public static void MakeBeard(MeshKit k, Body b, Vector3[] rp, Info body)
        {
            if (b.beard == Beard.None || b.beard == Beard.Stubble || body == null) return;
            float s = b.height / 1.85f, hs = s * PlayerMesh.HeadOf(b);
            Vector3 Hd(float x, float y, float z) => rp[4] + new Vector3(x, y, z) * hs;
            var sh = new SdfShape();
            var g = sh.Root;
            Vector3 lip = Snap(body, Hd(0f, 0.043f, 0.097f));   // under the nose, on the upper lip
            Vector3 L(float x, float y, float z) => lip + new Vector3(x, y, z) * hs;
            float ck = Mathf.Lerp(1f, b.chin, 0.5f);
            void Stache(float reach, float droop, float r0, float r1)
            {
                sh.Ell(g, L(0f, -0.001f, 0.001f), new Vector3(0.012f, 0.0075f, 0.008f) * hs * (r0 / 0.0085f), Quaternion.identity, 0f, 0, 4);
                for (int e = -1; e <= 1; e += 2)
                    sh.Cone(g, L(e * 0.006f, 0f, 0f), L(e * reach, -droop, -0.45f * reach), r0 * hs, r1 * hs, 0.008f * hs, 0, 4);
            }
            void Shell(float th, float yTop, float yBot, float frontCut)
            {
                var sg = sh.Branch(g, 0.006f * hs);
                sh.Ell(sg, Hd(0f, 0.062f, 0.012f), new Vector3(0.08f + th, 0.06f * ck + th, 0.08f + th) * hs, Quaternion.identity, 0f, 0, 4);
                sh.ClipPlane(sg, Vector3.up, Hd(0f, yTop, 0f), 0.006f * hs);
                sh.ClipPlane(sg, Vector3.down, Hd(0f, yBot, 0f), 0.006f * hs);
                sh.ClipPlane(sg, Vector3.back, Hd(0f, 0f, frontCut), 0.006f * hs);
                // leave the lips free
                sh.CarveEll(sg, Hd(0f, 0.033f, 0.09f), new Vector3(0.03f, 0.016f, 0.05f) * hs, Quaternion.identity, 0.005f * hs);
            }
            switch (b.beard)
            {
                case Beard.Mustache: Stache(0.03f, 0.007f, 0.0085f, 0.006f); break;
                case Beard.Handlebar:
                    Stache(0.032f, 0.004f, 0.0085f, 0.0055f);
                    for (int e = -1; e <= 1; e += 2)
                    {
                        sh.Cone(g, L(e * 0.032f, -0.004f, -0.0145f), L(e * 0.046f, 0.012f, -0.022f), 0.0055f * hs, 0.0035f * hs, 0.006f * hs, 0, 4);
                        sh.Ell(g, L(e * 0.047f, 0.014f, -0.022f), Vector3.one * 0.0042f * hs, Quaternion.identity, 0.004f * hs, 0, 4);
                    }
                    break;
                case Beard.Walrus: Stache(0.034f, 0.026f, 0.013f, 0.0095f); break;
                case Beard.Goatee:
                    Stache(0.026f, 0.008f, 0.0065f, 0.0045f);
                    {
                        Vector3 c = Snap(body, Hd(0f, 0.008f, 0.084f));
                        sh.Ell(g, c + new Vector3(0f, -0.006f, 0.002f) * hs, new Vector3(0.017f, 0.024f, 0.012f) * hs, Quaternion.Euler(-25f, 0f, 0f), 0.006f * hs, 0, 4);
                    }
                    break;
                case Beard.Full:
                    Stache(0.03f, 0.01f, 0.009f, 0.0075f);
                    Shell(0.011f, 0.044f, -0.02f, -0.01f);
                    break;
                case Beard.Viking:
                    Stache(0.032f, 0.014f, 0.0095f, 0.008f);
                    Shell(0.013f, 0.046f, -0.03f, -0.01f);
                    sh.Ell(g, Hd(0f, -0.03f, 0.072f), new Vector3(0.05f, 0.06f, 0.03f) * hs, Quaternion.Euler(-12f, 0f, 0f), 0.02f * hs, 0, 4);
                    for (int e = -1; e <= 1; e += 2)
                        sh.Cone(g, Hd(e * 0.024f, -0.06f, 0.08f), Hd(e * 0.026f, -0.19f, 0.07f), 0.012f * hs, 0.0075f * hs, 0.01f * hs, 0, 4);
                    break;
                case Beard.Chops:
                    for (int e = -1; e <= 1; e += 2)
                    {
                        var cg = sh.Branch(g, 0f);
                        sh.Ell(cg, Hd(0f, 0.062f, 0.012f), new Vector3(0.092f, 0.07f * ck, 0.092f) * hs, Quaternion.identity, 0f, 0, 4);
                        sh.ClipPlane(cg, Vector3.up, Hd(0f, 0.095f, 0f), 0.006f * hs);
                        sh.ClipPlane(cg, Vector3.down, Hd(0f, 0.03f, 0f), 0.008f * hs);
                        sh.ClipPlane(cg, new Vector3(-e, 0f, 0f), Hd(e * 0.05f, 0f, 0f), 0.008f * hs);
                        sh.ClipPlane(cg, Vector3.back, Hd(0f, 0f, -0.015f), 0.006f * hs);
                        sh.ClipPlane(cg, Vector3.forward, Hd(0f, 0f, 0.075f), 0.008f * hs);
                    }
                    break;
            }
            sh.Bake();
            var m = SdfMesh.Build(sh, 0.0052f * s * Mathf.Min(body.coarse, 2.2f));
            Color bc = HairC(Color.Lerp(b.hairColor, Color.black, 0.1f));
            int nv = m.P.Count;
            var inside = new bool[nv];
            float lim = -0.001f * hs;
            Parallel.For(0, nv, i => { inside[i] = body.shape.Eval(m.P[i]) < lim; });
            var remap = new int[nv];
            for (int i = 0; i < nv; i++) remap[i] = -1;
            for (int i = 0; i < m.T.Count; i += 3)
            {
                int a = m.T[i], bb = m.T[i + 1], c = m.T[i + 2];
                if (inside[a] && inside[bb] && inside[c]) continue;
                k.T.Add(Remap(k, m, remap, a, bc)); k.T.Add(Remap(k, m, remap, bb, bc)); k.T.Add(Remap(k, m, remap, c, bc));
            }
        }

        // ------------------------------------------------------------------ catcher's gear

        /// <summary>Plate colour of a club's catcher gear: dark on a light jersey, the accent colour on a dark one.</summary>
        public static Color GearPlate(TeamData t) =>
            t.jersey.grayscale < 0.4f ? Color.Lerp(t.accent, Color.black, 0.12f) : Color.Lerp(t.dark, t.accent, 0.5f);

        /// <summary>Catcher's gear as soft shells over the body: a padded chest protector and two shin guards with knee caps.
        /// Each shell is the body's own blobs blown up by the pad thickness and cut to shape, skinned like the body under it,
        /// so it hugs any belly and bends with the squat.</summary>
        public static void MakeCatcherGear(MeshKit k, Body b, TeamData t, Vector3[] rp, Info body)
        {
            if (body == null) return;
            float s = b.height / 1.85f, TK = PlayerMesh.TorsoK, Lk = PlayerMesh.LimbK * s, w = PlayerMesh.WidthOf(b), mu = b.muscle;
            float coarse = Mathf.Min(body.coarse, 2.2f);
            Vector3 hips = rp[0];
            Color plate = Pal.Gloss(GearPlate(t), 0.34f);
            Color trim = Pal.Gloss(t.main, 0.45f);
            Color groove = Pal.Gloss(Color.Lerp(GearPlate(t), Color.black, 0.45f), 0.2f);
            Color shell = Pal.Gloss(t.main, 0.7f);
            Color strap = Pal.Gloss(Color.Lerp(GearPlate(t), Color.black, 0.2f), 0.3f);
            float lim = -0.008f * s;
            var torsoSkin = new NearestSkin(body, l => l == LTorso, 0.03f * s);

            // ---- chest protector: the torso blown up by the pad, cut to a plate on the front
            {
                var sh = new SdfShape();
                var g = sh.Root;
                float th = 0.024f * s;
                foreach (var q in body.shape.P)
                {
                    if (q.label != LTorso || q.kind != 1) continue;
                    sh.Ell(g, q.c, q.r + Vector3.one * th, Quaternion.Inverse(q.inv), q.k, 0, -2);
                }
                // a plate that narrows toward the shoulders (so it stays clear of the arms) and widens over the belly
                float yTop = hips.y + 0.5f * s * TK, yBot = hips.y + 0.13f * s * TK, yMid = (yTop + yBot) * 0.5f, xw = 0.112f * w * s, zc = 0.055f * s;
                const float taper = 0.42f;
                float tn = Mathf.Sqrt(1f + taper * taper);
                float ek = 0.01f * s;
                sh.ClipPlane(g, Vector3.up, new Vector3(0f, yTop, 0f), ek);
                sh.ClipPlane(g, Vector3.down, new Vector3(0f, yBot, 0f), ek);
                sh.ClipPlane(g, new Vector3(1f, taper, 0f), new Vector3(xw, yMid, 0f), ek);
                sh.ClipPlane(g, new Vector3(-1f, taper, 0f), new Vector3(-xw, yMid, 0f), ek);
                sh.ClipPlane(g, Vector3.back, new Vector3(0f, 0f, zc), ek);
                // a dip at the throat so the chin and the mask's throat guard sit in it
                sh.CarveEll(g, new Vector3(0f, yTop + 0.01f * s, 0.08f * s), new Vector3(0.06f, 0.05f, 0.12f) * s, Quaternion.identity, 0.012f * s);
                sh.Bake();
                var m = SdfMesh.Build(sh, 0.0085f * s * coarse);
                m.ComputeLabels();
                float trimW = 0.012f * s, gw = 0.0042f * s;
                Vector3 dipC = new Vector3(0f, yTop + 0.01f * s, 0.08f * s), dipR = new Vector3(0.06f, 0.05f, 0.12f) * s + Vector3.one * trimW;
                float Edge(Vector3 p)
                {
                    Vector3 q = p - dipC;
                    float dip = 1f - new Vector3(q.x / dipR.x, q.y / dipR.y, q.z / dipR.z).magnitude;   // > 0 inside the throat dip band
                    float sideD = (Mathf.Abs(p.x) + taper * (p.y - yMid) - xw) / tn;
                    return Mathf.Max(Mathf.Max(p.y - yTop, yBot - p.y) + trimW, Mathf.Max(sideD + trimW, dip * 0.04f * s));
                }
                float y1 = Mathf.Lerp(yBot, yTop, 0.34f), y2 = Mathf.Lerp(yBot, yTop, 0.64f);
                m.Cut(Edge, l => true);
                m.Cut(p => Mathf.Abs(p.y - y1) - gw, l => true);
                m.Cut(p => Mathf.Abs(p.y - y2) - gw, l => true);
                m.Cut(p => Mathf.Abs(p.x) - gw, l => true);
                m.FixTJunctions();
                EmitShell(k, m, body, lim, p =>
                {
                    if (Edge(p) > 0f) return trim;
                    if (Mathf.Abs(p.y - y1) < gw || Mathf.Abs(p.y - y2) < gw || (Mathf.Abs(p.x) < gw && p.y > y1)) return groove;
                    return plate;
                }, torsoSkin.Weight);
            }

            // ---- shin guards with round knee caps (they ride on the shin bone)
            for (int side = 0; side < 2; side++)
            {
                int thb = side == 0 ? 14 : 17, kn = thb + 1, ft = thb + 2;
                Vector3 K = rp[kn], c = rp[ft];
                Vector3 calf = Vector3.Lerp(K, c, 0.32f) + new Vector3(0f, 0f, -0.007f * s);
                float t2 = 0.017f * s;
                var sh = new SdfShape();
                var g = sh.Root;
                sh.Cone(g, K + new Vector3(0f, 0.01f * s, 0.004f * s), calf, 0.064f * Lk + t2, (0.06f + 0.007f * mu) * Lk + t2, 0f, 0, kn);
                sh.Cone(g, calf, c + new Vector3(0f, 0.012f * s, 0.008f * s), (0.06f + 0.007f * mu) * Lk + t2, 0.047f * Lk + t2, 0.03f * s, 0, kn);
                sh.Ell(g, K + new Vector3(0f, 0.016f * s, 0.05f * Lk), new Vector3(0.056f, 0.062f, 0.036f) * Lk, Quaternion.Euler(-8f, 0f, 0f), 0.024f * s, 0, kn);
                sh.ClipPlane(g, Vector3.back, new Vector3(0f, 0f, K.z - 0.018f * s), 0.008f * s);
                sh.ClipPlane(g, Vector3.down, new Vector3(0f, c.y + 0.018f * s, 0f), 0.008f * s);
                sh.ClipPlane(g, Vector3.up, new Vector3(0f, K.y + 0.085f * s, 0f), 0.01f * s);
                sh.Bake();
                var m = SdfMesh.Build(sh, 0.0068f * s * coarse);
                m.ComputeLabels();
                float sy0 = Mathf.Lerp(K.y, c.y, 0.42f), sy1 = Mathf.Lerp(K.y, c.y, 0.78f), sw = 0.011f * s;
                m.Cut(p => Mathf.Abs(p.y - sy0) - sw, l => true);
                m.Cut(p => Mathf.Abs(p.y - sy1) - sw, l => true);
                m.FixTJunctions();
                int legL = side == 0 ? LLegL : LLegR;
                var legSkin = new NearestSkin(body, l => l == legL, 0.03f * s);
                EmitShell(k, m, body, lim, p => Mathf.Abs(p.y - sy0) < sw || Mathf.Abs(p.y - sy1) < sw ? strap : shell, legSkin.Weight);
            }
        }

        /// <summary>Skin weights of the nearest body vertex in the given regions (gear rides on the body exactly as it bends).</summary>
        sealed class NearestSkin
        {
            readonly Dictionary<long, List<int>> cells = new Dictionary<long, List<int>>();
            readonly IList<Vector3> P;
            readonly BoneWeight[] W;
            readonly float inv;

            public NearestSkin(Info body, Func<int, bool> ok, float cell)
            {
                P = body.skinP;
                W = body.skinW;
                inv = 1f / cell;
                for (int i = 0; i < P.Count; i++)
                {
                    if (!ok(body.skinL[i])) continue;
                    long key = Key(P[i]);
                    if (!cells.TryGetValue(key, out var l)) cells[key] = l = new List<int>(8);
                    l.Add(i);
                }
            }

            long Key(int x, int y, int z) => ((long)(x + (1 << 20)) << 42) | ((long)(y + (1 << 20)) << 21) | (long)(z + (1 << 20));
            long Key(Vector3 p) => Key(Mathf.FloorToInt(p.x * inv), Mathf.FloorToInt(p.y * inv), Mathf.FloorToInt(p.z * inv));

            public BoneWeight Weight(Vector3 q)
            {
                int cx = Mathf.FloorToInt(q.x * inv), cy = Mathf.FloorToInt(q.y * inv), cz = Mathf.FloorToInt(q.z * inv);
                int best = -1;
                float bd = float.MaxValue;
                for (int r = 1; r <= 4 && best < 0; r++)
                    for (int z = cz - r; z <= cz + r; z++)
                        for (int y = cy - r; y <= cy + r; y++)
                            for (int x = cx - r; x <= cx + r; x++)
                            {
                                if (!cells.TryGetValue(Key(x, y, z), out var l)) continue;
                                foreach (int i in l)
                                {
                                    float d = (P[i] - q).sqrMagnitude;
                                    if (d < bd) { bd = d; best = i; }
                                }
                            }
                return best >= 0 ? W[best] : new BoneWeight { boneIndex0 = 0, weight0 = 1f };
            }
        }

        /// <summary>Append a skinned shell, leaving out the faces buried in the body.</summary>
        static void EmitShell(MeshKit k, SdfMesh m, Info body, float lim, Func<Vector3, Color> colorOf, Func<Vector3, BoneWeight> weightOf)
        {
            int nv = m.P.Count;
            var inside = new bool[nv];
            Parallel.For(0, nv, i => { inside[i] = body.shape.Eval(m.P[i]) < lim; });
            var remap = new int[nv];
            for (int i = 0; i < nv; i++) remap[i] = -1;
            for (int t = 0; t < m.T.Count; t += 3)
            {
                int a = m.T[t], b = m.T[t + 1], c = m.T[t + 2];
                if (inside[a] && inside[b] && inside[c]) continue;
                for (int e = 0; e < 3; e++)
                {
                    int i = m.T[t + e];
                    if (remap[i] < 0)
                    {
                        Vector3 pb = m.P[i] + m.Bias[i];
                        k.V.Add(m.P[i]); k.N.Add(m.N[i]); k.C.Add(colorOf(pb)); k.UV.Add(MeshKit.Plain); k.UV2.Add(Vector2.zero); k.OL.Add(1f);
                        k.W.Add(weightOf(m.P[i]));
                        remap[i] = k.V.Count - 1;
                    }
                    k.T.Add(remap[i]);
                }
            }
        }

        /// <summary>Append a rigid part (no skinning) coloured per vertex.</summary>
        static void EmitRigid(MeshKit k, SdfMesh m, Func<int, Vector3, Color> colorOf)
        {
            int baseIdx = k.V.Count;
            for (int i = 0; i < m.P.Count; i++)
            {
                k.V.Add(m.P[i]); k.N.Add(m.N[i]); k.C.Add(colorOf(m.L != null && i < m.L.Count ? m.L[i] : 0, m.P[i] + m.Bias[i]));
                k.UV.Add(MeshKit.Plain); k.UV2.Add(Vector2.zero); k.OL.Add(1f);
                if (k.skin) k.W.Add(new BoneWeight { boneIndex0 = k.bone, weight0 = 1f });
            }
            foreach (int i in m.T) k.T.Add(baseIdx + i);
        }

        /// <summary>The catcher's face mask in head space (scale hs): a rounded frame, bars that leave the eyes and the mouth
        /// visible, a throat guard and padded temples. One soft cage: bars melt into the frame.</summary>
        public static void CatcherMask(MeshKit k, float hs, Color metal, Color pad, float coarse = 1f)
        {
            var sh = new SdfShape();
            var g = sh.Root;
            const float zF = 0.13f, bend = 0.062f, rx = 0.097f, cy = 0.078f, ry = 0.092f;
            Vector3 Fr(float x, float y) { float u = x / rx; return new Vector3(x, y, zF - bend * u * u) * hs; }
            float Half(float y) { float v = (y - cy) / ry; return rx * Mathf.Sqrt(Mathf.Max(0f, 1f - v * v)); }
            // the far version's bars are a touch thicker so the coarse grid never breaks them
            float thick = 1f + 0.35f * Mathf.Clamp01(coarse - 1f);
            float rb = 0.0066f * hs * thick, rf = 0.0088f * hs * thick, kb = 0.0028f * hs;
            // frame: a loop around the face
            const int N = 28;
            for (int i = 0; i < N; i++)
            {
                float a0 = Mathf.PI * 2f * i / N, a1 = Mathf.PI * 2f * (i + 1) / N;
                Vector3 p0 = Fr(rx * Mathf.Sin(a0), cy + ry * Mathf.Cos(a0)), p1 = Fr(rx * Mathf.Sin(a1), cy + ry * Mathf.Cos(a1));
                sh.Cone(g, p0, p1, rf, rf, kb, 0, 4);
            }
            // bars across: over the brows, under the nose, at the chin
            float[] bars = { 0.142f, 0.05f, 0.012f };
            foreach (float y in bars)
            {
                float hw = Half(y) * 0.98f;
                for (int i = 0; i < 6; i++)
                {
                    float x0 = Mathf.Lerp(-hw, hw, i / 6f), x1 = Mathf.Lerp(-hw, hw, (i + 1) / 6f);
                    sh.Cone(g, Fr(x0, y), Fr(x1, y), rb, rb, kb, 0, 4);
                }
            }
            // throat guard
            sh.Ell(g, new Vector3(0f, -0.034f, 0.112f) * hs, new Vector3(0.036f, 0.03f, 0.008f) * hs, Quaternion.Euler(20f, 0f, 0f), 0.01f * hs, 0, 4);
            // padded temples where the frame meets the helmet, and a pad across the forehead
            for (int e = -1; e <= 1; e += 2)
                sh.Ell(g, new Vector3(e * 0.091f, 0.098f, 0.058f) * hs, new Vector3(0.016f, 0.05f, 0.034f) * hs, Quaternion.Euler(0f, e * 22f, 0f), 0.008f * hs, 1, 4);
            sh.Ell(g, new Vector3(0f, 0.163f, 0.096f) * hs, new Vector3(0.062f, 0.016f, 0.022f) * hs, Quaternion.Euler(-30f, 0f, 0f), 0.008f * hs, 1, 4);
            sh.Bake();
            var m = SdfMesh.Build(sh, 0.0034f * hs * coarse);
            m.ComputeLabels();
            m.CutLabels();
            EmitRigid(k, m, (l, p) => l == 1 ? pad : metal);
        }

        /// <summary>A round, thick catcher's mitt on the hand bone (fingers point down the arm in the bind pose, the pocket faces +z):
        /// a fat padded disc with a rolled rim, a deep pocket, a thumb lobe on the inner side and a cuff at the wrist.</summary>
        public static void Mitt(MeshKit k, bool rightHand, float coarse = 1f)
        {
            var sh = new SdfShape();
            var g = sh.Root;
            float tx = rightHand ? 1f : -1f;   // thumb on the inner side once the mitt is held up, fingers to the sky
            // the padded disc and the back of the hand
            sh.Ell(g, new Vector3(0f, -0.112f, 0.012f), new Vector3(0.088f, 0.104f, 0.046f), Quaternion.identity, 0f, 0, 0);
            // rolled rim around the face of the mitt
            const int N = 26;
            for (int i = 0; i < N; i++)
            {
                float a0 = Mathf.PI * 2f * i / N, a1 = Mathf.PI * 2f * (i + 1) / N;
                Vector3 p0 = new Vector3(0.079f * Mathf.Cos(a0), -0.112f + 0.095f * Mathf.Sin(a0), 0.04f);
                Vector3 p1 = new Vector3(0.079f * Mathf.Cos(a1), -0.112f + 0.095f * Mathf.Sin(a1), 0.04f);
                sh.Cone(g, p0, p1, 0.021f, 0.021f, 0.004f, 1, 0);
            }
            // thumb lobe and the web next to it
            sh.Ell(g, new Vector3(tx * 0.088f, -0.088f, 0.028f), new Vector3(0.032f, 0.064f, 0.034f), Quaternion.Euler(0f, 0f, tx * 14f), 0.02f, 0, 0);
            sh.Ell(g, new Vector3(tx * 0.055f, -0.188f, 0.044f), new Vector3(0.03f, 0.024f, 0.02f), Quaternion.Euler(0f, 0f, tx * -30f), 0.012f, 1, 0);
            // cuff at the wrist
            sh.Ell(g, new Vector3(0f, -0.012f, 0.004f), new Vector3(0.052f, 0.026f, 0.042f), Quaternion.identity, 0.012f, 1, 0);
            // the deep pocket
            sh.CarveEll(g, new Vector3(0f, -0.115f, 0.088f), new Vector3(0.06f, 0.068f, 0.04f), Quaternion.identity, 0.012f);
            sh.Bake();
            var m = SdfMesh.Build(sh, 0.0058f * coarse);
            m.ComputeLabels();
            m.CutLabels();
            Color lea = Pal.Gloss(Pal.Leather, 0.42f), rim = Pal.Gloss(Pal.LeatherDark, 0.38f), pocket = Pal.Gloss(Color.Lerp(Pal.Leather, Pal.Lace, 0.45f), 0.3f);
            Vector3 pc = new Vector3(0f, -0.115f, 0.06f);
            m.Cut(p => { Vector3 q = p - pc; return Mathf.Max(new Vector2(q.x / 0.05f, q.y / 0.058f).magnitude - 1f, (0.035f - p.z) * 20f); }, l => l == 0);
            m.FixTJunctions();
            EmitRigid(k, m, (l, p) =>
            {
                if (l == 1) return rim;
                Vector3 q = p - pc;
                return new Vector2(q.x / 0.05f, q.y / 0.058f).magnitude < 1f && p.z > 0.035f ? pocket : lea;
            });
        }

        /// <summary>The wacky mohawk: one soft crest of spikes growing out of a cap or helmet dome (rigid part mesh).</summary>
        public static void Crest(MeshKit k, Vector3 domeC, Vector3 domeR, float hs, Color c, float a0 = -12f, float a1 = 112f)
        {
            var sh = new SdfShape();
            var g = sh.Root;
            for (int i = 0; i < 5; i++)
            {
                float a = Mathf.Lerp(a0, a1, i / 4f) * Mathf.Deg2Rad;
                Vector3 u = new Vector3(0f, Mathf.Cos(a), -Mathf.Sin(a));
                Vector3 basep = domeC + new Vector3(0f, u.y * domeR.y, u.z * domeR.z);
                Vector3 n = new Vector3(0f, u.y / domeR.y, u.z / domeR.z).normalized;
                float hgt = (0.078f - 0.012f * Mathf.Abs(i - 1.6f)) * hs;
                Vector3 tip = basep + (n * 1f + new Vector3(0f, 0f, -0.45f)).normalized * hgt;
                sh.Cone(g, basep - n * 0.012f * hs, tip, 0.021f * hs, 0.004f * hs, 0.02f * hs, 0, 0);
            }
            sh.Bake();
            var m = SdfMesh.Build(sh, 0.0045f * Mathf.Max(0.6f, hs / 1.78f));
            Color hc = HairC(c);
            int baseIdx = k.V.Count;
            for (int i = 0; i < m.P.Count; i++) { k.V.Add(m.P[i]); k.N.Add(m.N[i]); k.C.Add(hc); k.UV.Add(MeshKit.Plain); k.UV2.Add(Vector2.zero); k.OL.Add(1f); }
            foreach (int i in m.T) k.T.Add(baseIdx + i);
        }
    }
}
