using System.Collections.Generic;
using UnityEngine;

namespace BananaBall
{
    /// <summary>Small procedural mesh builder: ellipsoids, tubes, boxes and flat shapes with vertex colours and optional rigid skinning.</summary>
    public class MeshKit
    {
        public readonly List<Vector3> V = new List<Vector3>(2048);
        public readonly List<Vector3> N = new List<Vector3>(2048);
        public readonly List<Color> C = new List<Color>(2048);
        public readonly List<Vector2> UV = new List<Vector2>(2048);
        public readonly List<Vector2> UV2 = new List<Vector2>(2048);
        public readonly List<BoneWeight> W = new List<BoneWeight>(2048);
        public readonly List<float> OL = new List<float>(2048);
        public readonly List<int> T = new List<int>(8192);
        public bool skin;
        public int bone;
        public Vector2 extra;
        /// <summary>Ink-line width factor for the vertices added next (1 = full outline, 0 = none). Only used by ToMesh(outline: true).</summary>
        public float outline = 1f;
        /// <summary>Tessellation multiplier for ellipsoids, tubes and lofts (characters use more for smooth silhouettes).</summary>
        public float detail = 1f;
        int D(int n, int min) => Mathf.Max(min, Mathf.RoundToInt(n * detail));
        public static readonly Vector2 Plain = new Vector2(0.5f, 0.5f);

        public delegate Vector3 PathFn(float t);
        public delegate float RadFn(float t);
        public delegate float BlendFn(float t, out int b0, out int b1);

        public int Vert(Vector3 p, Vector3 n, Color c, Vector2 uv)
        {
            V.Add(p); N.Add(n); C.Add(c); UV.Add(uv); UV2.Add(extra); OL.Add(outline);
            if (skin) W.Add(new BoneWeight { boneIndex0 = bone, weight0 = 1f });
            return V.Count - 1;
        }

        int VertBlend(Vector3 p, Vector3 n, Color c, Vector2 uv, int b0, int b1, float w1)
        {
            V.Add(p); N.Add(n); C.Add(c); UV.Add(uv); UV2.Add(extra); OL.Add(outline);
            if (skin)
            {
                if (w1 <= 0.002f || b0 == b1) W.Add(new BoneWeight { boneIndex0 = b0, weight0 = 1f });
                else if (w1 >= 0.998f) W.Add(new BoneWeight { boneIndex0 = b1, weight0 = 1f });
                else if (w1 <= 0.5f) W.Add(new BoneWeight { boneIndex0 = b0, weight0 = 1f - w1, boneIndex1 = b1, weight1 = w1 });
                else W.Add(new BoneWeight { boneIndex0 = b1, weight0 = w1, boneIndex1 = b0, weight1 = 1f - w1 });
            }
            return V.Count - 1;
        }

        public void Ellipsoid(Vector3 c, Vector3 r, Quaternion q, Color col, int lat = 8, int lon = 12)
        {
            lat = D(lat, 2); lon = D(lon, 3);
            int s0 = V.Count;
            for (int i = 0; i <= lat; i++)
            {
                float th = Mathf.PI * i / lat;
                float y = Mathf.Cos(th), sr = Mathf.Sin(th);
                for (int j = 0; j <= lon; j++)
                {
                    float ph = Mathf.PI * 2f * j / lon;
                    Vector3 u = new Vector3(sr * Mathf.Cos(ph), y, sr * Mathf.Sin(ph));
                    Vector3 p = new Vector3(u.x * r.x, u.y * r.y, u.z * r.z);
                    Vector3 n = new Vector3(u.x / Mathf.Max(r.x, 1e-4f), u.y / Mathf.Max(r.y, 1e-4f), u.z / Mathf.Max(r.z, 1e-4f)).normalized;
                    Vert(c + q * p, q * n, col, Plain);
                }
            }
            for (int i = 0; i < lat; i++)
                for (int j = 0; j < lon; j++)
                {
                    int a = s0 + i * (lon + 1) + j, b = a + lon + 1;
                    T.Add(a); T.Add(a + 1); T.Add(b);
                    T.Add(a + 1); T.Add(b + 1); T.Add(b);
                }
        }

        public void Sphere(Vector3 c, float r, Color col, int lat = 8, int lon = 12) =>
            Ellipsoid(c, new Vector3(r, r, r), Quaternion.identity, col, lat, lon);

        /// <summary>Tube along path(t) for t in [t0,t1]. stripes: wraps the pinstripe UV around the tube.</summary>
        public void Tube(PathFn path, RadFn rad, float t0, float t1, int rings, int segs, Color col,
                         bool stripes = false, float stripeRepeat = 6f, float lobes = 0f, BlendFn blend = null)
        {
            rings = D(rings, 1); segs = D(segs, 3);
            int s0 = V.Count;
            for (int i = 0; i <= rings; i++)
            {
                float t = Mathf.Lerp(t0, t1, i / (float)rings);
                Vector3 c = path(t);
                Vector3 tan = path(t + 0.003f) - path(t - 0.003f);
                if (tan.sqrMagnitude < 1e-10f) tan = Vector3.up;
                tan.Normalize();
                Vector3 refAx = Mathf.Abs(tan.z) < 0.9f ? Vector3.forward : Vector3.right;
                Vector3 nAx = Vector3.Cross(tan, refAx).normalized;
                Vector3 bAx = Vector3.Cross(tan, nAx);
                float r = rad(t);
                int b0 = bone, b1 = bone;
                float w = 0f;
                if (blend != null) w = blend(t, out b0, out b1);
                for (int j = 0; j <= segs; j++)
                {
                    float ph = j / (float)segs * Mathf.PI * 2f;
                    Vector3 dir = nAx * Mathf.Cos(ph) + bAx * Mathf.Sin(ph);
                    float rr = r * (1f + lobes * Mathf.Cos(5f * ph));
                    Vector2 uv = stripes ? new Vector2(j / (float)segs * stripeRepeat, 0.5f) : Plain;
                    if (blend != null) VertBlend(c + dir * rr, dir, col, uv, b0, b1, w);
                    else Vert(c + dir * rr, dir, col, uv);
                }
            }
            for (int i = 0; i < rings; i++)
                for (int j = 0; j < segs; j++)
                {
                    int a = s0 + i * (segs + 1) + j, b = a + segs + 1;
                    T.Add(a); T.Add(a + 1); T.Add(b);
                    T.Add(a + 1); T.Add(b + 1); T.Add(b);
                }
        }

        /// <summary>Straight tube between two points with linear radius.</summary>
        public void Limb(Vector3 a, Vector3 b, float ra, float rb, Color col, int rings = 2, int segs = 10, bool stripes = false, float t0 = 0f, float t1 = 1f)
        {
            Tube(t => Vector3.LerpUnclamped(a, b, t), t => Mathf.Lerp(ra, rb, Mathf.Clamp01(t)), t0, t1, rings, segs, col, stripes, 4f);
        }

        public void Quad(Vector3 c, Vector3 n, Vector3 vAx, float hu, float hv, Color col)
        {
            n.Normalize();
            Vector3 u = Vector3.Cross(n, vAx).normalized;
            Vector3 v = Vector3.Cross(u, n).normalized;
            int i0 = Vert(c - u * hu - v * hv, n, col, Plain);
            int i1 = Vert(c - u * hu + v * hv, n, col, Plain);
            int i2 = Vert(c + u * hu + v * hv, n, col, Plain);
            int i3 = Vert(c + u * hu - v * hv, n, col, Plain);
            T.Add(i0); T.Add(i1); T.Add(i2);
            T.Add(i0); T.Add(i2); T.Add(i3);
        }

        public void Quad4(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color col, bool twoSided = false)
        {
            Vector3 n = Vector3.Cross(b - a, c - a).normalized;
            int i0 = Vert(a, n, col, Plain), i1 = Vert(b, n, col, Plain), i2 = Vert(c, n, col, Plain), i3 = Vert(d, n, col, Plain);
            T.Add(i0); T.Add(i1); T.Add(i2);
            T.Add(i0); T.Add(i2); T.Add(i3);
            if (twoSided) { T.Add(i0); T.Add(i2); T.Add(i1); T.Add(i0); T.Add(i3); T.Add(i2); }
        }

        public void Tri(Vector3 a, Vector3 b, Vector3 c, Color col, bool twoSided = false)
        {
            Vector3 n = Vector3.Cross(b - a, c - a).normalized;
            int i0 = Vert(a, n, col, Plain), i1 = Vert(b, n, col, Plain), i2 = Vert(c, n, col, Plain);
            T.Add(i0); T.Add(i1); T.Add(i2);
            if (twoSided) { T.Add(i0); T.Add(i2); T.Add(i1); }
        }

        public void Box(Vector3 c, Vector3 size, Quaternion q, Color col)
        {
            Vector3 h = size * 0.5f;
            Vector3 X = q * Vector3.right, Y = q * Vector3.up, Z = q * Vector3.forward;
            Quad(c + X * h.x, X, Y, h.z, h.y, col);
            Quad(c - X * h.x, -X, Y, h.z, h.y, col);
            Quad(c + Y * h.y, Y, Z, h.x, h.z, col);
            Quad(c - Y * h.y, -Y, Z, h.x, h.z, col);
            Quad(c + Z * h.z, Z, Y, h.x, h.y, col);
            Quad(c - Z * h.z, -Z, Y, h.x, h.y, col);
        }

        /// <summary>Flat disc facing +Y. uvWorld maps UV to world XZ (for tiled ground textures).</summary>
        public void Disc(Vector3 c, float r, Color col, int seg = 32, float uvWorld = 0f)
        {
            int ci = Vert(c, Vector3.up, col, uvWorld > 0f ? new Vector2(c.x * uvWorld, c.z * uvWorld) : Plain);
            int s0 = V.Count;
            for (int k = 0; k <= seg; k++)
            {
                float a = Mathf.PI * 2f * k / seg;
                Vector3 p = c + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
                Vert(p, Vector3.up, col, uvWorld > 0f ? new Vector2(p.x * uvWorld, p.z * uvWorld) : Plain);
            }
            for (int k = 0; k < seg; k++) { T.Add(ci); T.Add(s0 + k + 1); T.Add(s0 + k); }
        }

        public void Ring(Vector3 c, float r0, float r1, Color col, int seg = 40, float a0 = 0f, float a1 = 360f)
        {
            int s0 = V.Count;
            for (int k = 0; k <= seg; k++)
            {
                float a = Mathf.Lerp(a0, a1, k / (float)seg) * Mathf.Deg2Rad;
                Vector3 d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                Vert(c + d * r0, Vector3.up, col, new Vector2(0f, k / (float)seg));
                Vert(c + d * r1, Vector3.up, col, new Vector2(1f, k / (float)seg));
            }
            for (int k = 0; k < seg; k++)
            {
                int i0 = s0 + k * 2, o0 = i0 + 1, i1 = i0 + 2, o1 = i0 + 3;
                T.Add(i0); T.Add(i1); T.Add(o0);
                T.Add(i1); T.Add(o1); T.Add(o0);
            }
        }

        /// <summary>Star-shaped polygon (fan from centre) facing +Y.</summary>
        public void Fan(Vector3 c, List<Vector3> boundary, Color col, float uvWorld = 0f)
        {
            int ci = Vert(c, Vector3.up, col, uvWorld > 0f ? new Vector2(c.x * uvWorld, c.z * uvWorld) : Plain);
            int s0 = V.Count;
            foreach (var p in boundary) Vert(p, Vector3.up, col, uvWorld > 0f ? new Vector2(p.x * uvWorld, p.z * uvWorld) : Plain);
            int n = boundary.Count;
            for (int k = 0; k < n; k++)
            {
                int a = s0 + k, b = s0 + (k + 1) % n;
                Vector3 pa = boundary[k] - c, pb = boundary[(k + 1) % n] - c;
                float cross = pa.x * pb.z - pa.z * pb.x;   // >0: counter-clockwise seen from above
                if (cross > 0f) { T.Add(ci); T.Add(b); T.Add(a); }
                else { T.Add(ci); T.Add(a); T.Add(b); }
            }
        }

        /// <summary>Flat strip along a polyline on the ground (for chalk lines).</summary>
        public void Strip(Vector3 a, Vector3 b, float width, Color col)
        {
            Vector3 d = (b - a); d.y = 0f; d.Normalize();
            Vector3 s = new Vector3(d.z, 0f, -d.x) * (width * 0.5f);
            Quad4(a - s, b - s, b + s, a + s, col);
            // make sure it faces up
            int last = T.Count;
            Vector3 n = Vector3.Cross((b - s) - (a - s), (b + s) - (a - s));
            if (n.y < 0f)
            {
                for (int i = last - 6; i < last; i += 3) { int tmp = T[i + 1]; T[i + 1] = T[i + 2]; T[i + 2] = tmp; }
                for (int i = V.Count - 4; i < V.Count; i++) N[i] = Vector3.up;
            }
        }

        /// <summary>outline: bake ink-line data for BananaBall/Lit's outline pass: tangent.xyz = normal averaged over every vertex
        /// at the same spot (so boxes and seams extrude as one closed hull; skinning moves it with the bones), uv2.x = 1 - width factor.</summary>
        public Mesh ToMesh(string name, bool outline = false)
        {
            var m = new Mesh { name = name };
            if (V.Count > 65000) m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            m.SetVertices(V);
            m.SetNormals(N);
            // 8-bit colours (albedo + gloss in alpha) are plenty, and a quarter of the size of float colours
            var c32 = new List<Color32>(C.Count);
            for (int i = 0; i < C.Count; i++) c32.Add(C[i]);
            m.SetColors(c32);
            m.SetUVs(0, UV);
            if (outline)
            {
                var sum = new Dictionary<Vector3Int, Vector3>(V.Count);
                for (int i = 0; i < V.Count; i++)
                {
                    var key = Weld(V[i]);
                    sum.TryGetValue(key, out Vector3 acc);
                    sum[key] = acc + N[i];
                }
                var tan = new List<Vector4>(V.Count);
                var uv2 = new List<Vector2>(V.Count);
                for (int i = 0; i < V.Count; i++)
                {
                    Vector3 n = sum[Weld(V[i])];
                    n = n.sqrMagnitude > 1e-8f ? n.normalized : N[i];
                    if (Vector3.Dot(n, N[i]) < 0.2f) n = N[i];   // knife-thin or two-sided: keep its own normal
                    tan.Add(new Vector4(n.x, n.y, n.z, 1f));
                    float f = i < OL.Count ? Mathf.Clamp01(OL[i]) : 1f;
                    uv2.Add(new Vector2(1f - f, UV2[i].y));
                }
                m.SetTangents(tan);
                m.SetUVs(1, uv2);
            }
            else m.SetUVs(1, UV2);
            m.SetTriangles(T, 0);
            if (skin) m.boneWeights = W.ToArray();
            m.RecalculateBounds();
            return m;
        }

        static Vector3Int Weld(Vector3 p) => new Vector3Int(Mathf.RoundToInt(p.x * 5000f), Mathf.RoundToInt(p.y * 5000f), Mathf.RoundToInt(p.z * 5000f));

        public void Clear()
        {
            V.Clear(); N.Clear(); C.Clear(); UV.Clear(); UV2.Clear(); W.Clear(); T.Clear(); OL.Clear();
            bone = 0; extra = Vector2.zero; outline = 1f; detail = 1f;
        }
    }

    /// <summary>Runtime-generated textures and sprites (no imported art needed).</summary>
    public static partial class Tex
    {
        public static Texture2D Pinstripe()
        {
            var t = new Texture2D(32, 4, TextureFormat.RGBA32, true) { name = "BB_Pinstripe", wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear, anisoLevel = 2 };
            var stripe = new Color(0.62f, 0.70f, 0.80f);
            var px = new Color[32 * 4];
            for (int y = 0; y < 4; y++)
                for (int x = 0; x < 32; x++)
                    px[y * 32 + x] = (x <= 1) ? stripe : Color.white;
            t.SetPixels(px);
            t.Apply(true, true);
            return t;
        }

        public static Texture2D Checker(Color a, Color b, int size = 64)
        {
            var t = new Texture2D(size, size, TextureFormat.RGBA32, true) { name = "BB_Checker", wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear, anisoLevel = 4 };
            var px = new Color[size * size];
            int h = size / 2;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    px[y * size + x] = ((x < h) ^ (y < h)) ? a : b;
            t.SetPixels(px);
            t.Apply(true, true);
            return t;
        }

        static Texture2D Alpha(string name, int size, System.Func<float, float, float> f, bool mips = true)
        {
            var t = new Texture2D(size, size, TextureFormat.RGBA32, mips) { name = name, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f) / size * 2f - 1f, v = (y + 0.5f) / size * 2f - 1f;
                    px[y * size + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(f(u, v)));
                }
            t.SetPixels(px);
            t.Apply(mips, true);
            return t;
        }

        public static Texture2D SoftCircle(int size = 64) =>
            Alpha("BB_Soft", size, (u, v) => { float r = Mathf.Sqrt(u * u + v * v); return Mathf.SmoothStep(1f, 0f, Mathf.InverseLerp(0.35f, 1f, r)); });

        public static Texture2D Circle(int size = 64) =>
            Alpha("BB_Circle", size, (u, v) => { float r = Mathf.Sqrt(u * u + v * v); return (1f - r) * size * 0.5f; });

        public static Texture2D RingTex(int size = 128, float inner = 0.72f) =>
            Alpha("BB_Ring", size, (u, v) =>
            {
                float r = Mathf.Sqrt(u * u + v * v);
                float e = 2f / size;
                return Mathf.Min((1f - r) / e, (r - inner) / e);
            });

        public static Texture2D Star(int size = 128) =>
            Alpha("BB_Star", size, (u, v) =>
            {
                float a = Mathf.Atan2(v, u), r = Mathf.Sqrt(u * u + v * v);
                float k = Mathf.Abs(Mathf.Cos(a * 4f));
                float edge = Mathf.Lerp(0.38f, 1f, Mathf.Pow(k, 3f));
                return (edge - r) * size * 0.25f;
            });

        public static Texture2D Hand(int size = 128)
        {
            return Alpha("BB_Hand", size, (u, v) =>
            {
                float d = Mathf.Sqrt(u * u + (v + 0.28f) * (v + 0.28f)) - 0.42f; // palm
                for (int i = 0; i < 4; i++)
                {
                    float fx = -0.33f + i * 0.22f;
                    float top = 0.62f + (i == 1 || i == 2 ? 0.14f : 0f);
                    float cy = Mathf.Clamp(v, -0.1f, top);
                    float fd = Mathf.Sqrt((u - fx) * (u - fx) + (v - cy) * (v - cy)) - 0.085f;
                    d = Mathf.Min(d, fd);
                }
                // thumb
                Vector2 a = new Vector2(-0.38f, -0.2f), b = new Vector2(-0.72f, 0.18f), p = new Vector2(u, v);
                float h = Mathf.Clamp01(Vector2.Dot(p - a, b - a) / (b - a).sqrMagnitude);
                d = Mathf.Min(d, (p - (a + (b - a) * h)).magnitude - 0.09f);
                return -d * size * 0.5f;
            });
        }

        public static Sprite RoundRect(int size = 64, int radius = 26)
        {
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "BB_Round", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float cx = Mathf.Clamp(x + 0.5f, radius, size - radius);
                    float cy = Mathf.Clamp(y + 0.5f, radius, size - radius);
                    float d = Mathf.Sqrt((x + 0.5f - cx) * (x + 0.5f - cx) + (y + 0.5f - cy) * (y + 0.5f - cy));
                    px[y * size + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(radius - d + 0.5f));
                }
            t.SetPixels(px);
            t.Apply(false, true);
            return Sprite.Create(t, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(radius + 2, radius + 2, radius + 2, radius + 2));
        }

        public static Sprite SpriteOf(Texture2D t) =>
            Sprite.Create(t, new Rect(0, 0, t.width, t.height), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
    }

    public static class Shapes
    {
        public static Mesh Quad(string name = "BB_Quad")
        {
            var m = new Mesh { name = name };
            m.vertices = new[] { new Vector3(-0.5f, 0, -0.5f), new Vector3(-0.5f, 0, 0.5f), new Vector3(0.5f, 0, 0.5f), new Vector3(0.5f, 0, -0.5f) };
            m.uv = new[] { new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0) };
            m.colors = new[] { Color.white, Color.white, Color.white, Color.white };
            m.normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
            m.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            m.RecalculateBounds();
            return m;
        }

        /// <summary>Upright quad in XY facing -Z (toward the camera behind home).</summary>
        public static Mesh Billboard(string name = "BB_Board")
        {
            var m = new Mesh { name = name };
            m.vertices = new[] { new Vector3(-0.5f, -0.5f, 0), new Vector3(-0.5f, 0.5f, 0), new Vector3(0.5f, 0.5f, 0), new Vector3(0.5f, -0.5f, 0) };
            m.uv = new[] { new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0) };
            m.colors = new[] { Color.white, Color.white, Color.white, Color.white };
            m.normals = new[] { Vector3.back, Vector3.back, Vector3.back, Vector3.back };
            m.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            m.RecalculateBounds();
            return m;
        }

        /// <summary>Flat arrow on XZ pointing +Z, length 1.</summary>
        public static Mesh Arrow()
        {
            var k = new MeshKit();
            Color c = Color.white;
            k.Quad4(new Vector3(-0.06f, 0, 0), new Vector3(-0.06f, 0, 0.7f), new Vector3(0.06f, 0, 0.7f), new Vector3(0.06f, 0, 0), c);
            k.Tri(new Vector3(-0.2f, 0, 0.66f), new Vector3(0, 0, 1f), new Vector3(0.2f, 0, 0.66f), c);
            for (int i = 0; i < k.N.Count; i++) k.N[i] = Vector3.up;
            // ensure upward winding
            var m = k.ToMesh("BB_Arrow");
            FixUp(m);
            return m;
        }

        public static Mesh RingMesh(float r0, float r1, int seg = 48)
        {
            var k = new MeshKit();
            k.Ring(Vector3.zero, r0, r1, Color.white, seg);
            return k.ToMesh("BB_RingMesh");
        }

        static void FixUp(Mesh m)
        {
            var v = m.vertices;
            var t = m.triangles;
            for (int i = 0; i < t.Length; i += 3)
            {
                Vector3 n = Vector3.Cross(v[t[i + 1]] - v[t[i]], v[t[i + 2]] - v[t[i]]);
                if (n.y < 0f) { int tmp = t[i + 1]; t[i + 1] = t[i + 2]; t[i + 2] = tmp; }
            }
            m.triangles = t;
        }

        /// <summary>A chunky cartoon bat along +Z from the knob (handle at the origin). scale enlarges the barrel for the Mega Bat.</summary>
        public static Mesh Bat(float scale = 1f, string name = "BB_Bat")
        {
            var k = new MeshKit { detail = 1.6f };
            Color wood = Pal.Gloss(Pal.Wood, 0.55f), grip = Pal.Gloss(new Color(0.08f, 0.08f, 0.09f), 0.2f), cup = Pal.Gloss(new Color(0.35f, 0.2f, 0.1f), 0.5f);
            float L = 0.74f * scale;
            MeshKit.RadFn rad = t =>
            {
                float handle = 0.0155f * Mathf.Lerp(1f, 1.6f, Mathf.Clamp01((scale - 1f) * 0.5f));
                float barrel = 0.041f * scale;
                float u = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.32f, 0.72f, t));
                float r = Mathf.Lerp(handle, barrel, u);
                if (t > 0.97f) r *= Mathf.Lerp(1f, 0.82f, (t - 0.97f) / 0.03f);
                return r;
            };
            k.outline = 0.75f;
            k.Tube(t => new Vector3(0, 0, t * L), rad, 0f, 0.28f, 4, 12, grip);
            k.Tube(t => new Vector3(0, 0, t * L), rad, 0.28f, 1f, 14, 12, wood);
            k.Ellipsoid(new Vector3(0, 0, L), new Vector3(rad(1f), rad(1f), 0.006f * scale), Quaternion.identity, cup, 4, 12);
            k.Ellipsoid(new Vector3(0, 0, -0.004f), new Vector3(0.021f, 0.021f, 0.009f) * Mathf.Max(1f, scale * 0.7f), Quaternion.identity, grip, 4, 10);
            return k.ToMesh(name, true);
        }

        /// <summary>Baseball with red stitching.</summary>
        public static Mesh BallSeamed()
        {
            var k = new MeshKit { detail = 1.4f };
            float r = Cfg.BallRadius;
            k.Sphere(Vector3.zero, r, Pal.Gloss(new Color(0.96f, 0.95f, 0.92f), 0.35f), 12, 18);
            Color red = Pal.Gloss(new Color(0.78f, 0.1f, 0.1f), 0.2f);
            const float a = 0.7f, b = 0.3f;
            float c = 2f * Mathf.Sqrt(a * b);
            MeshKit.PathFn seam = t =>
            {
                float u = t * Mathf.PI * 2f;
                var q = new Vector3(a * Mathf.Cos(u) + b * Mathf.Cos(3f * u), a * Mathf.Sin(u) - b * Mathf.Sin(3f * u), c * Mathf.Sin(2f * u));
                return q.normalized * (r * 1.01f);
            };
            k.outline = 0f;
            k.Tube(seam, t => r * 0.07f, 0f, 1f, 64, 4, red);
            return k.ToMesh("BB_Ball", true);
        }

        public static Mesh Ball()
        {
            var k = new MeshKit();
            k.Sphere(Vector3.zero, Cfg.BallRadius, Color.white, 10, 14);
            return k.ToMesh("BB_Ball", true);
        }
    }
}
