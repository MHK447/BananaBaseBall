using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace SlingArena.Colosseum
{
    /// <summary>Vertex-coloured procedural mesh builder used by every Colosseum model.</summary>
    public sealed class MeshKit
    {
        readonly List<Vector3> verts = new List<Vector3>();
        readonly List<Vector3> norms = new List<Vector3>();
        readonly List<Color32> cols = new List<Color32>();
        readonly List<int> tris = new List<int>();

        /// <summary>Painted top-to-bottom shading baked into every rounded shape (0 = flat colour).</summary>
        public float Gradient = .16f;

        static Color32 Tint(Color32 color, float factor)
        {
            return new Color32((byte)Mathf.Clamp(Mathf.RoundToInt(color.r * factor), 0, 255),
                (byte)Mathf.Clamp(Mathf.RoundToInt(color.g * factor), 0, 255),
                (byte)Mathf.Clamp(Mathf.RoundToInt(color.b * factor), 0, 255), color.a);
        }

        /// <summary>Lighter towards the top of a shape, darker towards its underside; height is -1..1.</summary>
        Color32 Painted(Color32 color, float height)
        {
            if (Gradient <= 0f) return color;
            return Tint(color, 1f + Gradient * (height > 0f ? .55f : 1.25f) * Mathf.Clamp(height, -1f, 1f));
        }

        /// <summary>
        /// The outline of a star for Plate or Cut: so many points, the first of them straight up, out to one radius
        /// at the points and in to another between them.
        /// </summary>
        public static Vector2[] StarRim(int points, float outer, float inner)
        {
            var rim = new Vector2[points * 2];
            for (int i = 0; i < rim.Length; i++)
            {
                float a = i * Mathf.PI / points, reach = i % 2 == 0 ? outer : inner;
                rim[i] = new Vector2(Mathf.Sin(a) * reach, Mathf.Cos(a) * reach);
            }
            return rim;
        }

        public static Color32 Mix(Color32 a, Color32 b, float t)
        {
            return Color32.Lerp(a, b, t);
        }

        /// <summary>Hex colour; alpha carries the gloss amount read by the toy shader.</summary>
        public static Color32 Rgb(int hex, float gloss = .22f)
        {
            return new Color32((byte)((hex >> 16) & 255), (byte)((hex >> 8) & 255), (byte)(hex & 255),
                (byte)Mathf.RoundToInt(Mathf.Clamp01(gloss) * 255f));
        }

        static float SignedPow(float value, float exponent)
        {
            return Mathf.Sign(value) * Mathf.Pow(Mathf.Abs(value), exponent);
        }

        void Stitch(int start, int rows, int seg)
        {
            int width = seg + 1;
            for (int i = 0; i < rows; i++)
            {
                for (int j = 0; j < seg; j++)
                {
                    int a = start + i * width + j, b = a + 1, d = a + width, e = d + 1;
                    tris.Add(a); tris.Add(d); tris.Add(b);
                    tris.Add(b); tris.Add(d); tris.Add(e);
                }
            }
        }

        /// <summary>Superellipsoid: e = 1 is a sphere, smaller values give a soft rounded box.</summary>
        public void Blob(Vector3 center, Vector3 radii, Quaternion rotation, Color32 color,
            float e1 = 1f, float e2 = 1f, int seg = 14)
        {
            int rows = Mathf.Max(4, seg / 2 + 1), start = verts.Count;
            float tallest = Mathf.Max(radii.x, Mathf.Max(radii.y, radii.z));
            for (int i = 0; i <= rows; i++)
            {
                float v = Mathf.Lerp(-Mathf.PI * .5f, Mathf.PI * .5f, (float)i / rows);
                float cv = Mathf.Cos(v), sv = Mathf.Sin(v);
                for (int j = 0; j <= seg; j++)
                {
                    float u = (float)j / seg * Mathf.PI * 2f;
                    float cu = Mathf.Cos(u), su = Mathf.Sin(u);
                    var p = new Vector3(radii.x * SignedPow(cv, e1) * SignedPow(cu, e2), radii.y * SignedPow(sv, e1),
                        radii.z * SignedPow(cv, e1) * SignedPow(su, e2));
                    var n = new Vector3(SignedPow(cv, 2f - e1) * SignedPow(cu, 2f - e2) / radii.x,
                        SignedPow(sv, 2f - e1) / radii.y, SignedPow(cv, 2f - e1) * SignedPow(su, 2f - e2) / radii.z);
                    if (n.sqrMagnitude < 1e-12f)
                        n = p;
                    Vector3 turned = rotation * p;
                    verts.Add(center + turned);
                    norms.Add((rotation * n).normalized);
                    cols.Add(Painted(color, turned.y / tallest));
                }
            }
            Stitch(start, rows, seg);
        }

        public void Ball(Vector3 center, float radius, Color32 color, int seg = 12)
        {
            Blob(center, Vector3.one * radius, Quaternion.identity, color, 1f, 1f, seg);
        }

        /// <summary>Capsule between two points with its own radius at each end.</summary>
        public void Capsule(Vector3 p0, Vector3 p1, float r0, float r1, Color32 color, int seg = 10)
        {
            Vector3 axis = p1 - p0;
            float length = axis.magnitude;
            Quaternion q = length > 1e-5f ? Quaternion.FromToRotation(Vector3.up, axis / length) : Quaternion.identity;
            const int half = 4;
            int start = verts.Count, rows = half * 2 + 1;
            float middle = (p0.y + p1.y) * .5f, reach = Mathf.Abs(p1.y - p0.y) * .5f + Mathf.Max(r0, r1);
            for (int i = 0; i <= rows; i++)
            {
                bool top = i > half;
                int k = top ? i - 1 : i;
                float v = Mathf.Lerp(-Mathf.PI * .5f, Mathf.PI * .5f, (float)k / (half * 2));
                float cv = Mathf.Cos(v), sv = Mathf.Sin(v);
                float radius = top ? r1 : r0;
                Vector3 center = top ? p1 : p0;
                for (int j = 0; j <= seg; j++)
                {
                    float u = (float)j / seg * Mathf.PI * 2f;
                    var d = new Vector3(cv * Mathf.Cos(u), sv, cv * Mathf.Sin(u));
                    Vector3 point = center + q * (d * radius);
                    verts.Add(point);
                    norms.Add(q * d);
                    cols.Add(Painted(color, (point.y - middle) / reach));
                }
            }
            Stitch(start, rows, seg);
        }

        /// <summary>Flat-shaded quad; the corner order decides the facing (normal = (b-a) x (c-a)).</summary>
        public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color32 color)
        {
            Vector3 n = Vector3.Cross(b - a, c - a).normalized;
            int start = verts.Count;
            verts.Add(a); verts.Add(b); verts.Add(c); verts.Add(d);
            for (int i = 0; i < 4; i++) { norms.Add(n); cols.Add(color); }
            tris.Add(start); tris.Add(start + 1); tris.Add(start + 2);
            tris.Add(start); tris.Add(start + 2); tris.Add(start + 3);
        }

        /// <summary>Flat quad with a colour per corner, for gradients and soft ground shadows.</summary>
        public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color32 ca, Color32 cb, Color32 cc, Color32 cd)
        {
            Vector3 n = Vector3.Cross(b - a, c - a).normalized;
            int start = verts.Count;
            verts.Add(a); verts.Add(b); verts.Add(c); verts.Add(d);
            cols.Add(ca); cols.Add(cb); cols.Add(cc); cols.Add(cd);
            for (int i = 0; i < 4; i++) norms.Add(n);
            tris.Add(start); tris.Add(start + 1); tris.Add(start + 2);
            tris.Add(start); tris.Add(start + 2); tris.Add(start + 3);
        }

        /// <summary>Upward-facing disc on the ground blending from a centre colour to an edge colour.</summary>
        public void Disc(Vector3 center, float radiusX, float radiusZ, Color32 middle, Color32 edge, int seg = 24)
        {
            int start = verts.Count;
            verts.Add(center); norms.Add(Vector3.up); cols.Add(middle);
            for (int i = 0; i <= seg; i++)
            {
                float a = (float)i / seg * Mathf.PI * 2f;
                verts.Add(center + new Vector3(Mathf.Cos(a) * radiusX, 0f, Mathf.Sin(a) * radiusZ));
                norms.Add(Vector3.up);
                cols.Add(edge);
            }
            for (int i = 0; i < seg; i++)
            {
                tris.Add(start); tris.Add(start + i + 2); tris.Add(start + i + 1);
            }
        }

        public void Tri(Vector3 a, Vector3 b, Vector3 c, Color32 color)
        {
            Vector3 n = Vector3.Cross(b - a, c - a).normalized;
            int start = verts.Count;
            verts.Add(a); verts.Add(b); verts.Add(c);
            for (int i = 0; i < 3; i++) { norms.Add(n); cols.Add(color); }
            tris.Add(start); tris.Add(start + 1); tris.Add(start + 2);
        }

        /// <summary>Hard-edged box with a separate colour for the top face.</summary>
        public void Box(Vector3 center, Vector3 size, Quaternion q, Color32 side, Color32 top)
        {
            Vector3 h = size * .5f;
            Vector3 P(float x, float y, float z) { return center + q * new Vector3(x * h.x, y * h.y, z * h.z); }
            Color32 low = Gradient > 0f ? Tint(side, 1f - Gradient * 1.5f) : side;
            Quad(P(-1, 1, -1), P(-1, 1, 1), P(1, 1, 1), P(1, 1, -1), top);
            Quad(P(-1, -1, -1), P(1, -1, -1), P(1, -1, 1), P(-1, -1, 1), low);
            Quad(P(1, -1, -1), P(1, 1, -1), P(1, 1, 1), P(1, -1, 1), low, side, side, low);
            Quad(P(-1, -1, -1), P(-1, -1, 1), P(-1, 1, 1), P(-1, 1, -1), low, low, side, side);
            Quad(P(-1, -1, 1), P(1, -1, 1), P(1, 1, 1), P(-1, 1, 1), low, low, side, side);
            Quad(P(-1, -1, -1), P(-1, 1, -1), P(1, 1, -1), P(1, -1, -1), low, side, side, low);
        }

        public void Box(Vector3 center, Vector3 size, Color32 color)
        {
            Box(center, size, Quaternion.identity, color, color);
        }

        /// <summary>Upward-facing rectangle lying on the ground.</summary>
        public void Floor(float x0, float z0, float x1, float z1, float y, Color32 color)
        {
            Quad(new Vector3(x0, y, z0), new Vector3(x0, y, z1), new Vector3(x1, y, z1), new Vector3(x1, y, z0), color);
        }

        /// <summary>Upward-facing ring (or arc) lying on the ground.</summary>
        public void Ring(Vector3 center, float inner, float outer, Color32 color, int seg = 40,
            float from = 0f, float to = Mathf.PI * 2f)
        {
            for (int i = 0; i < seg; i++)
            {
                float t0 = Mathf.Lerp(from, to, (float)i / seg), t1 = Mathf.Lerp(from, to, (float)(i + 1) / seg);
                var d0 = new Vector3(Mathf.Cos(t0), 0f, Mathf.Sin(t0));
                var d1 = new Vector3(Mathf.Cos(t1), 0f, Mathf.Sin(t1));
                Quad(center + d0 * inner, center + d1 * inner, center + d1 * outer, center + d0 * outer, color);
            }
        }

        /// <summary>
        /// Surface of revolution around the shape's own Y axis. Profile points are (radius, height) listed along the
        /// outside from the bottom up; repeat a point to put a hard crease there.
        /// </summary>
        public void Lathe(Vector3 center, Quaternion rotation, Vector2[] profile, Color32[] colors, int seg = 24, float deep = 1f)
        {
            int start = verts.Count, rows = profile.Length - 1;
            for (int i = 0; i <= rows; i++)
            {
                Vector2 before = profile[Mathf.Max(0, i - 1)], here = profile[i], after = profile[Mathf.Min(rows, i + 1)];
                Vector2 along = (here - before).sqrMagnitude < 1e-10f ? after - here
                    : (after - here).sqrMagnitude < 1e-10f ? here - before : after - before;
                if (along.sqrMagnitude < 1e-10f) along = Vector2.up;
                along.Normalize();
                Color32 color = colors[Mathf.Min(i, colors.Length - 1)];
                for (int j = 0; j <= seg; j++)
                {
                    float u = (float)j / seg * Mathf.PI * 2f;
                    float cu = Mathf.Cos(u), su = Mathf.Sin(u);
                    verts.Add(center + rotation * new Vector3(here.x * cu, here.y, here.x * su * deep));
                    norms.Add(rotation * new Vector3(along.y * cu, -along.x, along.y * su / deep).normalized);
                    cols.Add(color);
                }
            }
            Stitch(start, rows, seg);
        }

        /// <summary>Flat triangle wound so that it faces the given direction.</summary>
        public void Facet(Vector3 a, Vector3 b, Vector3 c, Vector3 outward, Color32 ca, Color32 cb, Color32 cc)
        {
            Vector3 n = Vector3.Cross(b - a, c - a);
            if (Vector3.Dot(n, outward) < 0f)
            {
                Vector3 point = b; b = c; c = point;
                Color32 color = cb; cb = cc; cc = color;
                n = -n;
            }
            n.Normalize();
            int start = verts.Count;
            verts.Add(a); verts.Add(b); verts.Add(c);
            cols.Add(ca); cols.Add(cb); cols.Add(cc);
            for (int i = 0; i < 3; i++) norms.Add(n);
            tris.Add(start); tris.Add(start + 1); tris.Add(start + 2);
        }

        /// <summary>
        /// Cut-out plate with a bevelled rim, facing the shape's own -Z. The outline lies in the XY plane and has to be
        /// star-shaped around its middle (every rim point visible from it); it is painted lighter towards the top.
        /// </summary>
        public void Slab(Vector3 center, Quaternion rotation, Vector2[] outline, float depth, float bevel,
            Color32 face, Color32 rim, Color32 side)
        {
            int count = outline.Length;
            Vector2 middle = Vector2.zero;
            float low = float.MaxValue, high = float.MinValue;
            for (int i = 0; i < count; i++)
            {
                middle += outline[i];
                low = Mathf.Min(low, outline[i].y);
                high = Mathf.Max(high, outline[i].y);
            }
            middle /= count;
            float half = Mathf.Max(1e-4f, (high - low) * .5f), level = (high + low) * .5f;
            float front = -depth * .5f, back = depth * .5f;
            Vector3 toward = rotation * Vector3.back;
            for (int i = 0; i < count; i++)
            {
                Vector2 a = outline[i], b = outline[(i + 1) % count];
                Vector2 ia = Inset(a, middle, bevel), ib = Inset(b, middle, bevel);
                Vector2 away = (a + b) * .5f - middle;
                Vector3 outward = rotation * new Vector3(away.x, away.y, 0f).normalized;
                Vector3 pa = center + rotation * new Vector3(a.x, a.y, front + bevel), pb = center + rotation * new Vector3(b.x, b.y, front + bevel);
                Vector3 qa = center + rotation * new Vector3(ia.x, ia.y, front), qb = center + rotation * new Vector3(ib.x, ib.y, front);
                Vector3 ba = center + rotation * new Vector3(a.x, a.y, back), bb = center + rotation * new Vector3(b.x, b.y, back);
                float ta = (a.y - level) / half, tb = (b.y - level) / half, tia = (ia.y - level) / half, tib = (ib.y - level) / half;
                Facet(center + rotation * new Vector3(middle.x, middle.y, front), qa, qb, toward,
                    Painted(face, (middle.y - level) / half), Painted(face, tia), Painted(face, tib));
                Facet(qa, qb, pb, outward + toward, Painted(rim, tia), Painted(rim, tib), Painted(rim, tb));
                Facet(qa, pb, pa, outward + toward, Painted(rim, tia), Painted(rim, tb), Painted(rim, ta));
                Facet(pa, pb, bb, outward, Painted(side, ta), Painted(side, tb), Painted(side, tb));
                Facet(pa, bb, ba, outward, Painted(side, ta), Painted(side, tb), Painted(side, ta));
            }
        }

        /// <summary>
        /// Closed plate with a soft pillow edge: both faces and a rim whose normals turn smoothly from the faces to
        /// the edge, so it takes the outline shell cleanly. The outline lies in the shape's own XY plane (thickness
        /// along Z) and has to be star-shaped around its middle. The rim colour runs along the edge, which makes a
        /// sharpened blade or a polished border when it is lighter than the face.
        /// </summary>
        public void Plate(Vector3 center, Quaternion rotation, Vector2[] outline, float thickness, float bevel, Color32 face, Color32 rim)
        {
            int count = outline.Length, start = verts.Count;
            Vector2 middle = Vector2.zero;
            float area = 0f;
            for (int i = 0; i < count; i++)
            {
                Vector2 a = outline[i], b = outline[(i + 1) % count];
                area += a.x * b.y - b.x * a.y;
                middle += a;
            }
            middle /= count;
            float half = thickness * .5f, turn = area > 0f ? 1f : -1f;

            verts.Add(center + rotation * new Vector3(middle.x, middle.y, -half)); norms.Add(rotation * Vector3.back); cols.Add(face);
            for (int ring = 0; ring < 3; ring++)
            {
                for (int i = 0; i < count; i++)
                {
                    Vector2 point = outline[i], along = (outline[(i + 1) % count] - outline[(i - 1 + count) % count]).normalized;
                    var lateral = new Vector2(along.y * turn, -along.x * turn);
                    Vector2 at = ring == 1 ? point : Inset(point, middle, bevel);
                    Vector3 normal = ring == 1 ? new Vector3(lateral.x, lateral.y, 0f)
                        : new Vector3(lateral.x * .6f, lateral.y * .6f, ring == 0 ? -1f : 1f).normalized;
                    verts.Add(center + rotation * new Vector3(at.x, at.y, ring == 0 ? -half : ring == 1 ? 0f : half));
                    norms.Add(rotation * normal);
                    cols.Add(ring == 1 ? rim : face);
                }
            }
            verts.Add(center + rotation * new Vector3(middle.x, middle.y, half)); norms.Add(rotation * Vector3.forward); cols.Add(face);

            int front = start, ring0 = start + 1, ring1 = ring0 + count, ring2 = ring1 + count, back = ring2 + count;
            for (int i = 0; i < count; i++)
            {
                int j = (i + 1) % count;
                Wind(front, ring0 + i, ring0 + j);
                Wind(ring0 + i, ring1 + i, ring1 + j);
                Wind(ring0 + i, ring1 + j, ring0 + j);
                Wind(ring1 + i, ring2 + i, ring2 + j);
                Wind(ring1 + i, ring2 + j, ring1 + j);
                Wind(back, ring2 + j, ring2 + i);
            }
        }

        /// <summary>Adds a triangle between existing vertices, wound to agree with the normals they already carry.</summary>
        void Wind(int a, int b, int c)
        {
            Vector3 n = Vector3.Cross(verts[b] - verts[a], verts[c] - verts[a]);
            if (Vector3.Dot(n, norms[a] + norms[b] + norms[c]) < 0f)
            {
                int swap = b; b = c; c = swap;
            }
            tris.Add(a); tris.Add(b); tris.Add(c);
        }

        /// <summary>
        /// Cut plate: flat faces on both sides with flat chamfers running to a crisp edge, like a ground blade or a
        /// stamped piece of armour. The outline lies in the shape's own XY plane, thickness along Z.
        /// </summary>
        public void Cut(Vector3 center, Quaternion rotation, Vector2[] outline, float depth, float bevel, Color32 face, Color32 rim, Color32 side)
        {
            int count = outline.Length;
            Vector2 middle = Vector2.zero;
            float area = 0f, low = float.MaxValue, high = float.MinValue;
            for (int i = 0; i < count; i++)
            {
                Vector2 a = outline[i], b = outline[(i + 1) % count];
                area += a.x * b.y - b.x * a.y;
                middle += a;
                low = Mathf.Min(low, a.y);
                high = Mathf.Max(high, a.y);
            }
            middle /= count;
            float turn = area > 0f ? 1f : -1f, span = Mathf.Max(1e-4f, (high - low) * .5f), level = (high + low) * .5f;
            var inner = new Vector2[count];
            for (int i = 0; i < count; i++)
            {
                Vector2 along = (outline[(i + 1) % count] - outline[(i - 1 + count) % count]).normalized;
                inner[i] = outline[i] - new Vector2(along.y * turn, -along.x * turn) * bevel;
            }
            float half = depth * .5f, edge = Mathf.Max(half * .15f, half - bevel * .55f);
            for (int i = 0; i < count; i++)
            {
                int j = (i + 1) % count;
                Vector2 away = (outline[i] + outline[j]) * .5f - middle;
                Vector3 outward = rotation * new Vector3(away.x, away.y, 0f).normalized;
                float ti = (outline[i].y - level) / span, tj = (outline[j].y - level) / span;
                for (int sign = -1; sign <= 1; sign += 2)
                {
                    Vector3 toward = rotation * new Vector3(0f, 0f, sign);
                    Vector3 m = center + rotation * new Vector3(middle.x, middle.y, sign * half);
                    Vector3 qa = center + rotation * new Vector3(inner[i].x, inner[i].y, sign * half), qb = center + rotation * new Vector3(inner[j].x, inner[j].y, sign * half);
                    Vector3 pa = center + rotation * new Vector3(outline[i].x, outline[i].y, sign * edge), pb = center + rotation * new Vector3(outline[j].x, outline[j].y, sign * edge);
                    Facet(m, qa, qb, toward, Painted(face, 0f), Painted(face, ti), Painted(face, tj));
                    Facet(qa, qb, pb, outward + toward, Painted(rim, ti), Painted(rim, tj), Painted(rim, tj));
                    Facet(qa, pb, pa, outward + toward, Painted(rim, ti), Painted(rim, tj), Painted(rim, ti));
                }
                Vector3 fa = center + rotation * new Vector3(outline[i].x, outline[i].y, -edge), fb = center + rotation * new Vector3(outline[j].x, outline[j].y, -edge);
                Vector3 ba = center + rotation * new Vector3(outline[i].x, outline[i].y, edge), bb = center + rotation * new Vector3(outline[j].x, outline[j].y, edge);
                Facet(fa, fb, bb, outward, side, side, side);
                Facet(fa, bb, ba, outward, side, side, side);
            }
        }

        /// <summary>Box with every edge and corner cut off flat; the cuts take the edge colour so they catch the light.</summary>
        public void Chamfer(Vector3 center, Vector3 size, Quaternion q, float bevel, Color32 color, Color32 edge)
        {
            Vector3 h = size * .5f;
            float b = Mathf.Min(bevel, Mathf.Min(h.x, Mathf.Min(h.y, h.z)) * .9f);
            Vector3 P(int axis, float along, int u, float su, int v, float sv)
            {
                Vector3 point = Vector3.zero;
                point[axis] = along; point[u] = su; point[v] = sv;
                return center + q * point;
            }
            Vector3 Axis(int axis, float sign) { Vector3 d = Vector3.zero; d[axis] = sign; return q * d; }
            Color32 lowTone = Painted(color, -1f), highTone = Painted(color, 1f);
            for (int axis = 0; axis < 3; axis++)
            {
                int u = (axis + 1) % 3, v = (axis + 2) % 3;
                for (int sign = -1; sign <= 1; sign += 2)
                {
                    // Flat face.
                    Vector3 a = P(axis, sign * h[axis], u, -(h[u] - b), v, -(h[v] - b)), c = P(axis, sign * h[axis], u, h[u] - b, v, h[v] - b);
                    Vector3 d = P(axis, sign * h[axis], u, h[u] - b, v, -(h[v] - b)), e = P(axis, sign * h[axis], u, -(h[u] - b), v, h[v] - b);
                    Color32 tone = axis == 1 ? (sign > 0 ? highTone : lowTone) : color;
                    Facet(a, d, c, Axis(axis, sign), tone, tone, tone);
                    Facet(a, c, e, Axis(axis, sign), tone, tone, tone);
                    // Cut edges between this face and the next axis's two faces.
                    for (int other = -1; other <= 1; other += 2)
                    {
                        Vector3 outward = Axis(axis, sign) + Axis(u, other);
                        Vector3 p0 = P(axis, sign * h[axis], u, other * (h[u] - b), v, -(h[v] - b)), p1 = P(axis, sign * h[axis], u, other * (h[u] - b), v, h[v] - b);
                        Vector3 r0 = P(axis, sign * (h[axis] - b), u, other * h[u], v, -(h[v] - b)), r1 = P(axis, sign * (h[axis] - b), u, other * h[u], v, h[v] - b);
                        Facet(p0, p1, r1, outward, edge, edge, edge);
                        Facet(p0, r1, r0, outward, edge, edge, edge);
                    }
                }
            }
            for (int sx = -1; sx <= 1; sx += 2)
                for (int sy = -1; sy <= 1; sy += 2)
                    for (int sz = -1; sz <= 1; sz += 2)
                        Facet(center + q * new Vector3(sx * h.x, sy * (h.y - b), sz * (h.z - b)), center + q * new Vector3(sx * (h.x - b), sy * h.y, sz * (h.z - b)),
                            center + q * new Vector3(sx * (h.x - b), sy * (h.y - b), sz * h.z), q * new Vector3(sx, sy, sz), edge, edge, edge);
        }

        /// <summary>Cut jewel sitting on a surface: a flat table on top of a ring of facets. Its own +Y points out of the surface.</summary>
        public void Gem(Vector3 center, Quaternion rotation, Vector3 radii, Color32 color, Color32 light, int sides = 8)
        {
            Vector3 top = center + rotation * new Vector3(0f, radii.y, 0f);
            for (int i = 0; i < sides; i++)
            {
                float a0 = (float)i / sides * Mathf.PI * 2f, a1 = (float)(i + 1) / sides * Mathf.PI * 2f;
                Vector3 d0 = new Vector3(Mathf.Cos(a0) * radii.x, 0f, Mathf.Sin(a0) * radii.z), d1 = new Vector3(Mathf.Cos(a1) * radii.x, 0f, Mathf.Sin(a1) * radii.z);
                Vector3 g0 = center + rotation * d0, g1 = center + rotation * d1;
                Vector3 t0 = top + rotation * (d0 * .55f), t1 = top + rotation * (d1 * .55f);
                Vector3 outward = rotation * (d0 + d1 + Vector3.up * radii.y);
                Color32 tone = (i & 1) == 0 ? color : light;
                Facet(g0, g1, t1, outward, tone, tone, tone);
                Facet(g0, t1, t0, outward, tone, tone, tone);
                Facet(top, t0, t1, rotation * Vector3.up, light, light, light);
            }
        }

        /// <summary>Flat-sided spike from a base point to a tip.</summary>
        public void Spike(Vector3 from, Vector3 tip, float radius, Color32 color, Color32 light, int sides = 4)
        {
            Vector3 axis = (tip - from).normalized;
            Quaternion q = Quaternion.FromToRotation(Vector3.up, axis);
            for (int i = 0; i < sides; i++)
            {
                float a0 = (i + .5f) / sides * Mathf.PI * 2f, a1 = (i + 1.5f) / sides * Mathf.PI * 2f;
                Vector3 d0 = q * new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)), d1 = q * new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1));
                Color32 tone = (i & 1) == 0 ? color : light;
                Facet(from + d0 * radius, from + d1 * radius, tip, d0 + d1, tone, tone, tone);
            }
        }

        static Vector2 Inset(Vector2 point, Vector2 middle, float amount)
        {
            Vector2 to = middle - point;
            float reach = to.magnitude;
            return reach <= amount * 2f ? Vector2.Lerp(point, middle, .5f) : point + to / reach * amount;
        }

        /// <summary>
        /// Direction each vertex of the outline shell is pushed in: the average of the distinct normals meeting at
        /// that point. On rounded shapes it equals the normal; on cut and boxed shapes it keeps the shell in one piece.
        /// </summary>
        List<Vector3> OutlineNormals() { return OutlineNormals(verts, norms); }

        public static List<Vector3> OutlineNormals(IList<Vector3> verts, IList<Vector3> norms)
        {
            var meeting = new Dictionary<Vector3Int, List<Vector3>>();
            var keys = new Vector3Int[verts.Count];
            for (int i = 0; i < verts.Count; i++)
            {
                Vector3 p = verts[i] * 4000f;
                keys[i] = new Vector3Int(Mathf.RoundToInt(p.x), Mathf.RoundToInt(p.y), Mathf.RoundToInt(p.z));
                if (!meeting.TryGetValue(keys[i], out List<Vector3> list))
                    meeting[keys[i]] = list = new List<Vector3>(2);
                bool known = false;
                for (int k = 0; k < list.Count && !known; k++)
                    known = Vector3.Dot(list[k], norms[i]) > .999f;
                if (!known) list.Add(norms[i]);
            }
            var smooth = new List<Vector3>(verts.Count);
            for (int i = 0; i < verts.Count; i++)
            {
                List<Vector3> list = meeting[keys[i]];
                Vector3 sum = Vector3.zero;
                for (int k = 0; k < list.Count; k++) sum += list[k];
                smooth.Add(sum.sqrMagnitude > 1e-8f ? sum.normalized : norms[i]);
            }
            return smooth;
        }

        public Mesh ToMesh(string name)
        {
            var mesh = new Mesh { name = name, hideFlags = HideFlags.DontSave };
            if (verts.Count > 65000)
                mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(verts);
            mesh.SetNormals(norms);
            mesh.SetColors(cols);
            mesh.SetUVs(3, OutlineNormals());
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            return mesh;
        }
    }

    /// <summary>Shared materials, palette and object helpers.</summary>
    public static class Kit
    {
        public const int Ink = 0x33202b;
        public const int Steel = 0xc6d2e2, SteelDark = 0x8493ab, Gold = 0xf7b836, GoldDark = 0xd48a1c, Wood = 0xa96b3d;
        public static readonly int[] TeamMain = { 0x2f7df2, 0xe83c30 };
        public static readonly int[] TeamDark = { 0x1b4db6, 0xa6202a };
        public static readonly int[] TeamLight = { 0x86ccff, 0xff9a80 };
        public static readonly int[] Skins = { 0xf6c7a4, 0xeeb48c, 0xdb9c70, 0xb5774e };

        public static Material Surface { get; private set; }
        public static Material Outline { get; private set; }
        /// <summary>Finer outline for heroes shown large (lobby, portraits): it sinks a little so that parts lying
        /// flush against each other no longer draw stray inner lines.</summary>
        public static Material Showcase { get; private set; }
        /// <summary>The two materials every inked part is drawn with, and the one a part without ink has.</summary>
        public static Material[] Inked { get; private set; }
        public static Material[] Plain { get; private set; }
        /// <summary>
        /// What a powered-up hero is drawn with instead: its own surface and ink, which also leave a mark on the
        /// screen wherever they land, and then three shells of light, each drawn only where neither the hero nor a
        /// shell inside it has been drawn yet. So the light rings the whole silhouette and never runs between the
        /// hero's own parts: pale gold against the ink, deep gold outside that, and a soft glow outermost.
        /// </summary>
        public static Material[] Powered { get; private set; }
        /// <summary>The same for a part without ink: its surface only, leaving the mark.</summary>
        public static Material[] PoweredPlain { get; private set; }
        const float InkWidth = .026f, AuraMark = 64f;
        static readonly float[] AuraReach = { .07f, .135f, .235f };
        static readonly Material[] aura = new Material[3];
        static Shader strokeShader;
        static readonly List<Object> owned = new List<Object>();
        static readonly Dictionary<long, Mesh> balls = new Dictionary<long, Mesh>();

        /// <summary>Shared unit sphere in one colour, scaled per use for eyes, sparks and other small bits.</summary>
        public static Mesh UnitBall(int hex, float gloss)
        {
            long key = ((long)hex << 8) | (long)Mathf.RoundToInt(Mathf.Clamp01(gloss) * 255f);
            if (!balls.TryGetValue(key, out Mesh mesh) || mesh == null)
            {
                var kit = new MeshKit();
                kit.Ball(Vector3.zero, 1f, MeshKit.Rgb(hex, gloss), 12);
                mesh = kit.ToMesh("Colosseum unit ball");
                balls[key] = mesh;
            }
            return mesh;
        }

        public static void Init(Shader stroke)
        {
            Dispose();
            Shader toy = Resources.Load<Shader>("ColosseumToon");
            if (toy == null || !toy.isSupported) toy = Resources.Load<Shader>("SlingArenaToySurface");
            if (toy == null) toy = Shader.Find("SlingArena/Toy Surface");
            if (toy == null) toy = Shader.Find("Universal Render Pipeline/Particles/Lit");
            if (toy == null) toy = Shader.Find("Sprites/Default");
            Surface = Own(new Material(toy) { name = "Colosseum toy surface" });
            Outline = Own(new Material(toy) { name = "Colosseum toy ink" });
            Outline.SetColor("_InkColor", (Color)MeshKit.Rgb(Ink, 1f));
            Outline.SetFloat("_InkWidth", InkWidth);
            Outline.SetFloat("_InkMode", 1f);
            Outline.SetFloat("_Cull", (float)CullMode.Front);
            Showcase = Own(new Material(Outline) { name = "Colosseum showcase ink" });
            Showcase.SetFloat("_InkWidth", .019f);
            Showcase.SetFloat("_InkPush", .035f);
            Inked = new[] { Surface, Outline };
            Plain = new[] { Surface };
            Material markedSurface = Own(new Material(Surface) { name = "Colosseum toy surface (powered)" });
            Material markedInk = Own(new Material(Outline) { name = "Colosseum toy ink (powered)" });
            foreach (Material marked in new[] { markedSurface, markedInk })
            {
                marked.SetFloat("_StencilRef", AuraMark);
                marked.SetFloat("_StencilPass", (float)StencilOp.Replace);
            }
            Color[] light = { new Color(1f, .98f, .7f, 1f), new Color(1f, .8f, .1f, 1f), new Color(1f, .74f, .08f, .5f) };
            for (int k = 0; k < aura.Length; k++)
            {
                aura[k] = Own(new Material(Outline) { name = "Colosseum power light " + k });
                aura[k].SetColor("_InkColor", light[k]);
                aura[k].SetFloat("_InkWidth", AuraReach[k]);
                aura[k].SetFloat("_StencilRef", AuraMark);
                aura[k].SetFloat("_StencilComp", (float)CompareFunction.NotEqual);
                aura[k].SetFloat("_StencilPass", (float)StencilOp.Replace);
                // After everything solid, innermost first, so that each shell finds the marks of those inside it.
                aura[k].renderQueue = (int)RenderQueue.Geometry + 10 + k;
            }
            // The outermost is see-through, and leaves nothing in the depth buffer for the sand's shadows to trip on.
            aura[2].SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            aura[2].SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            aura[2].SetFloat("_ZWrite", 0f);
            Powered = new[] { markedSurface, markedInk, aura[0], aura[1], aura[2] };
            PoweredPlain = new[] { markedSurface };
            strokeShader = stroke != null ? stroke : Shader.Find("SlingArena/Stroke");
            if (strokeShader == null) strokeShader = Shader.Find("Sprites/Default");
        }

        /// <summary>
        /// How far the light stands out round a powered-up hero just now: at 0 it is folded away inside the ink, at
        /// 1 it is out in full (more flares it out further still), and the two outer shells breathe in and out a
        /// little as time passes.
        /// </summary>
        public static void SetAura(float strength, float time)
        {
            float breath = 1f + .15f * Mathf.Sin(time * 10f);
            for (int k = 0; k < aura.Length; k++)
            {
                if (aura[k] != null)
                    aura[k].SetFloat("_InkWidth", Mathf.LerpUnclamped(InkWidth, AuraReach[k] * (k == 0 ? 1f : breath), Mathf.Clamp(strength, 0f, 1.6f)));
            }
        }

        public static T Own<T>(T asset) where T : Object
        {
            asset.hideFlags = HideFlags.DontSave;
            owned.Add(asset);
            return asset;
        }

        public static void Dispose()
        {
            foreach (Object asset in owned)
            {
                if (asset != null)
                    Object.Destroy(asset);
            }
            owned.Clear();
            balls.Clear();
        }

        /// <summary>Unlit transparent material for ground decals and flashes.</summary>
        public static Material StrokeMaterial(Color tint)
        {
            var material = Own(new Material(strokeShader) { name = "Colosseum stroke" });
            material.color = tint;
            if (material.HasProperty("_Color"))
                material.SetColor("_Color", tint);
            return material;
        }

        public static GameObject Spawn(string name, Transform parent, Mesh mesh, bool ink = false, bool shadows = true)
        {
            var item = new GameObject(name);
            item.transform.SetParent(parent, false);
            item.AddComponent<MeshFilter>().sharedMesh = Own(mesh);
            var renderer = item.AddComponent<MeshRenderer>();
            if (ink) renderer.sharedMaterials = Inked;
            else renderer.sharedMaterial = Surface;
            renderer.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            return item;
        }

        public static GameObject SpawnStroke(string name, Transform parent, Mesh mesh, Material material)
        {
            var item = new GameObject(name);
            item.transform.SetParent(parent, false);
            item.AddComponent<MeshFilter>().sharedMesh = Own(mesh);
            var renderer = item.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return item;
        }

        public static Vector3 Flat(Vector3 v) { return new Vector3(v.x, 0f, v.z); }
    }

    /// <summary>Tiny synthesised sound effects so the scene needs no audio assets.</summary>
    public sealed class Sfx
    {
        const int Rate = 22050;
        readonly AudioSource source;
        readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
        // Recorded sounds, the files in Resources/ColosseumAudio. One named like a synthesised sound takes its place;
        // "hit", "hit_2", "hit_3" are takes of one sound and are played in turn.
        readonly Dictionary<string, List<AudioClip>> recorded = new Dictionary<string, List<AudioClip>>();
        readonly Dictionary<string, int> lastTake = new Dictionary<string, int>();
        readonly AudioSource[] musicPlayers = new AudioSource[2];
        AudioSource crowdPlayer, trophyChargePlayer;
        float trophyChargeVolume, trophyChargePitch;
        bool trophyChargePlaying;
        int musicFront;
        string musicName;
        float duckLeft, crowdWanted;
        /// <summary>How loud the music sits under everything else, how far it drops under a fanfare, and how long a change of piece takes.</summary>
        const float MusicLevel = .5f, MusicDucked = .15f, MusicFade = 1.1f;
        /// <summary>What to play for a recorded sound whose file is missing, so the game is never left silent.</summary>
        static readonly Dictionary<string, string> StandIn = new Dictionary<string, string>
        {
            { "ui-tap", "click" }, { "ui-tab", "click" }, { "ui-open", "click" }, { "ui-close", "click" }, { "card-pick", "click" },
            { "card-equip", "match-ding" }, { "upgrade-charge", "hero-charge" }, { "upgrade-burst", "hero-upgrade" },
            { "recruit-reveal", "match-ding" }, { "match-found", "match-impact" }, { "battle-start", "click" }, { "pull", "click" },
            { "set-win", "win" }, { "set-lose", "win" }, { "match-win", "win" }, { "match-lose", "win" },
            { "twister", "whirl" }, { "skyward", "launch" }, { "twinkle", "match-ding" },
            { "chest-shake", "hero-charge" }, { "chest-open", "hero-upgrade" }, { "chest-coins", "match-ding" },
            { "reward-show", "match-ding" }, { "arena-unlock", "win" }, { "trophy-tick", "click" }, { "card-fly", "click" },
            { "tide", "whirl" }, { "fire-pillar", "lava" }, { "fire-warn", "lava-rumble" }, { "scald", "lava" },
        };

        public Sfx(GameObject host)
        {
            source = host.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            Make("launch", .30f, t => Noise(t) * .55f + Tone(t, 260f, 620f, .30f) * .3f);
            Make("bounce", .10f, t => Square(t, 190f, 90f, .10f) * .25f + Noise(t) * .25f);
            Make("hit", .24f, t => Saw(t, 150f, 45f, .24f) * .5f + Noise(t) * .5f);
            Make("smash", .42f, t => Tone(t, 90f, 35f, .42f) * .9f + Noise(t) * .45f);
            Make("chain", .13f, t => Square(t, 520f, 260f, .13f) * .3f);
            Make("arrow", .06f, t => Tone(t, 1300f, 700f, .06f) * .2f);
            Make("ko", .5f, t => Tone(t, 440f, 80f, .5f) * .5f);
            Make("click", .06f, t => Square(t, 700f, 500f, .06f) * .2f);
            Make("match-impact", .24f, t => Tone(t, 97f, 43f, .24f) * .60f
                + Tone(t, 231f, 99f, .24f) * Mathf.Exp(-t * 24f) * .25f);
            Make("match-tick", .045f, t => Tone(t, 1140f, 760f, .045f) * .14f
                + Tone(t, 380f, 260f, .045f) * .09f);
            Make("match-ding", .42f, t => Tone(t, 880f, 0f, 0f) * .30f
                + Tone(t, 1320f, 0f, 0f) * Mathf.Exp(-t * 7f) * .16f
                + Tone(t, 2640f, 0f, 0f) * Mathf.Exp(-t * 15f) * .06f);
            Make("match-reveal", .13f, t => Tone(t, 410f, 530f, .13f) * .20f
                + Tone(t, 820f, 1060f, .13f) * Mathf.Exp(-t * 18f) * .08f);
            Make("cheer", .9f, t => Noise(t) * .35f * Mathf.Sin(Mathf.Clamp01(t / .9f) * Mathf.PI));
            Make("win", .62f, t => Square(t, t < .14f ? 523f : t < .28f ? 659f : 784f, 0f, 0f) * .28f);
            // Power strips: a chime when they appear, and a quick rising run of notes when a hero powers up.
            Make("strip", .34f, t => Tone(t, 660f, 990f, .34f) * .2f + Tone(t, 1320f, 1980f, .34f) * .08f);
            Make("powerup", .44f, t => Square(t, PowerNotes[Mathf.Min(PowerNotes.Length - 1, (int)(t / .066f))], 0f, 0f) * .2f);
            // A soft rising harmonic pulls into a short rest before the card turns face up.
            Make("hero-charge", 1.13f, t => Mathf.SmoothStep(0f, 1f, t / .42f)
                * (.72f + .28f * Mathf.Sin(t * (19f + t * 12f)))
                * (.15f * Tone(t, 330f, 785f, 1.13f) + .055f * Tone(t, 660f, 1570f, 1.13f)));
            // One bright mallet strike with a higher bell overtone and a long, softer tail.
            Make("hero-upgrade", .90f, t => .41f * Tone(t, 1046.5f, 0f, 0f) * Mathf.Exp(-t * 4.5f)
                + .23f * Tone(t, 1568f, 0f, 0f) * Mathf.Exp(-t * 5.7f)
                + .14f * Tone(t, 2657f, 0f, 0f) * Mathf.Exp(-t * 15f));
            // Earthquake: a long low rumble while the walls tremble, a crash as each stretch comes down, and a
            // falling whistle for a hero going out through a gap.
            Make("quake", 2.1f, t => (Tone(t, 47f, 39f, 2.1f) * .6f + Tone(t, 73f, 55f, 2.1f) * .3f + Noise(t) * .2f) * (.6f + .4f * Mathf.Sin(t * 33f)));
            Make("crumble", .6f, t => Noise(t) * .6f * Mathf.Exp(-t * 4f) + Tone(t, 72f, 30f, .6f) * .75f * Mathf.Exp(-t * 6f));
            Make("fall", .5f, t => Tone(t, 940f, 170f, .5f) * .26f);
            // The goblin's bombs: two rising beeps as one is stuck on, the fuse fizzing up as it shakes, and the blast.
            Make("plant", .2f, t => Square(t, t < .09f ? 880f : 1320f, 0f, 0f) * .16f);
            Make("tick", .09f, t => Square(t, 1500f, 0f, 0f) * .14f);
            Make("fuse", .5f, t => Noise(t) * .2f * Mathf.Clamp01(t * 12f) * (.55f + .45f * Mathf.Sin(t * (60f + t * 240f)))
                + Square(t, 620f, 1750f, .5f) * .075f * (.5f + .5f * Mathf.Sin(t * (50f + t * 260f))));
            Make("boom", .7f, t => Noise(t) * .7f * Mathf.Exp(-t * 3.2f) + Tone(t, 64f, 26f, .7f) * .9f * Mathf.Exp(-t * 4.5f));
            // The pirate: iron biting as the hook catches, the lock of his gun drawn back, and the shot.
            Make("hook", .18f, t => Square(t, 1250f, 620f, .18f) * .16f * Mathf.Exp(-t * 14f) + Noise(t) * .3f * Mathf.Exp(-t * 22f));
            Make("cock", .17f, t => (t < .04f || (t > .1f && t < .14f) ? Square(t, 940f, 0f, 0f) * .2f : 0f) + Noise(t) * .1f * Mathf.Exp(-t * 30f));
            Make("blast", .55f, t => Noise(t) * .95f * Mathf.Exp(-t * 9f) + Tone(t, 120f, 38f, .55f) * .8f * Mathf.Exp(-t * 7f));
            // The gladiator: the beat of the great boomerang turning, its edge finding someone, and his feet coming down.
            Make("whirl", 1.7f, t => Noise(t) * .3f * (.55f + .45f * Mathf.Sin(t * 62f)) * Mathf.Clamp01(t * 8f) * Mathf.Clamp01((1.7f - t) * 3f)
                + Tone(t, 150f, 110f, 1.7f) * .1f * (.5f + .5f * Mathf.Sin(t * 62f)) * Mathf.Clamp01((1.7f - t) * 3f));
            Make("slice", .17f, t => Noise(t) * .5f * Mathf.Exp(-t * 20f) + Tone(t, 1900f, 700f, .17f) * .2f * Mathf.Exp(-t * 15f));
            Make("thud", .3f, t => Tone(t, 150f, 55f, .3f) * .8f * Mathf.Exp(-t * 10f) + Noise(t) * .35f * Mathf.Exp(-t * 18f));
            // ...and the boomerang getting away from him on the way home: a lurch in its beat, and iron ringing off a wall.
            Make("lurch", .22f, t => Tone(t, 320f, 150f, .22f) * .28f * Mathf.Exp(-t * 9f) + Noise(t) * .22f * Mathf.Exp(-t * 16f));
            Make("clang", .3f, t => Square(t, 980f, 760f, .3f) * .13f * Mathf.Exp(-t * 11f) + Tone(t, 1960f, 1500f, .3f) * .12f * Mathf.Exp(-t * 8f)
                + Noise(t) * .3f * Mathf.Exp(-t * 30f));
            // The dawn witch: her giggle as she starts, the zip of a star leaving her wand, and a star coming down:
            // a bright ring over a thump.
            Make("cackle", .52f, t =>
            {
                float beat = t * 9.5f, part = beat - Mathf.Floor(beat);
                return (Square(t, 760f + 130f * Mathf.Floor(beat), 0f, 0f) * .06f + Tone(t, 1520f + 260f * Mathf.Floor(beat), 0f, 0f) * .1f) * Mathf.Exp(-part * 5f);
            });
            Make("cast", .24f, t => Tone(t, 620f, 2300f, .24f) * .2f * Mathf.Exp(-t * 6f) + Square(t, 1240f, 3300f, .24f) * .04f * Mathf.Exp(-t * 11f)
                + Noise(t) * .16f * Mathf.Exp(-t * 28f));
            // A star on its way down: a thin whistle that drops as it comes.
            Make("starfall", .5f, t => Tone(t, 2500f, 640f, .5f) * .085f * Mathf.Min(1f, t * 16f) * Mathf.Exp(-t * 1.6f)
                + Noise(t) * .028f * Mathf.Min(1f, t * 5f));
            Make("starhit", .36f, t => Tone(t, 1568f, 0f, 0f) * .17f * Mathf.Exp(-t * 9f) + Tone(t, 2349f, 0f, 0f) * .1f * Mathf.Exp(-t * 13f)
                + Tone(t, 180f, 62f, .36f) * .5f * Mathf.Exp(-t * 11f) + Noise(t) * .32f * Mathf.Exp(-t * 24f));
            // Map events. Three rising notes as one arrives; the zip of a portal; ice creeping over the sand; lava
            // rumbling up and bursting out; spikes snapping out of a wall, and biting; the twang of a bumper; the
            // supply crate splitting open, and what was in it: health, a shield (and a blow stopped by one).
            Make("event", .62f, t => Notes(t, EventNotes, .1f, 7f) * .17f);
            // ...and under them war drums, faster and harder up to the moment the event lands.
            // (Twice as long as the roll itself: a sound is faded out over its last three fifths, and the last
            // beats are the ones that have to land.)
            Make("event-drum", 1.4f, t => Drums(t, DrumBeats) * .5f);
            Make("warp", .32f, t => Tone(t, 300f, 1900f, .32f) * .22f * Mathf.Sin(Mathf.Clamp01(t / .32f) * Mathf.PI)
                + Tone(t, 2400f, 900f, .32f) * .08f * Mathf.Exp(-t * 8f));
            Make("freeze", .7f, t => Noise(t) * .2f * Mathf.Exp(-t * 3f) * (Mathf.Sin(t * 180f) > .2f ? 1f : .35f)
                + Tone(t, 2100f, 3200f, .7f) * .07f * Mathf.Exp(-t * 4f));
            Make("lava-rumble", .6f, t => (Tone(t, 58f, 84f, .6f) * .55f + Noise(t) * .16f) * Mathf.Clamp01(t * 5f));
            Make("lava", .7f, t => Noise(t) * .7f * Mathf.Exp(-t * 4f) + Tone(t, 96f, 40f, .7f) * .7f * Mathf.Exp(-t * 5f)
                + Tone(t, 420f, 160f, .7f) * .12f * Mathf.Exp(-t * 7f));
            Make("spike-out", .22f, t => Square(t, 1500f, 900f, .22f) * .12f * Mathf.Exp(-t * 18f) + Noise(t) * .35f * Mathf.Exp(-t * 26f)
                + Tone(t, 2900f, 0f, 0f) * .07f * Mathf.Exp(-t * 12f));
            Make("spike", .28f, t => Noise(t) * .5f * Mathf.Exp(-t * 16f) + Saw(t, 520f, 180f, .28f) * .22f * Mathf.Exp(-t * 11f)
                + Tone(t, 2200f, 1200f, .28f) * .1f * Mathf.Exp(-t * 14f));
            Make("boing", .34f, t => (Tone(t, 150f, 520f, .09f) * .4f * Mathf.Exp(-t * 7f) + Tone(t, 300f, 1040f, .09f) * .14f * Mathf.Exp(-t * 10f))
                * (1f + .25f * Mathf.Sin(t * 70f)));
            Make("crate", .5f, t => Noise(t) * .7f * Mathf.Exp(-t * 11f) + Square(t, 240f, 90f, .5f) * .22f * Mathf.Exp(-t * 9f)
                + Tone(t, 1300f, 600f, .5f) * .06f * Mathf.Exp(-t * 20f));
            Make("heal", .56f, t => Notes(t, HealNotes, .09f, 6f) * .15f);
            Make("shield", .4f, t => Tone(t, 440f, 880f, .4f) * .16f * Mathf.Exp(-t * 4f) + Tone(t, 1320f, 1760f, .4f) * .07f * Mathf.Exp(-t * 6f));
            Make("block", .16f, t => Square(t, 990f, 660f, .16f) * .12f * Mathf.Exp(-t * 16f) + Noise(t) * .2f * Mathf.Exp(-t * 30f));
            LoadRecorded(host);
        }

        void LoadRecorded(GameObject host)
        {
            foreach (AudioClip clip in Resources.LoadAll<AudioClip>("ColosseumAudio"))
            {
                string name = clip.name;
                int cut = name.LastIndexOf('_');
                if (cut > 0 && int.TryParse(name.Substring(cut + 1), out int _)) name = name.Substring(0, cut);
                if (!recorded.TryGetValue(name, out List<AudioClip> takes)) recorded[name] = takes = new List<AudioClip>();
                takes.Add(clip);
            }
            for (int i = 0; i < musicPlayers.Length; i++) musicPlayers[i] = Looping(host);
            crowdPlayer = Looping(host);
            if (recorded.TryGetValue("crowd-loop", out List<AudioClip> murmur)) crowdPlayer.clip = murmur[0];
        }

        static AudioSource Looping(GameObject host)
        {
            AudioSource player = host.AddComponent<AudioSource>();
            player.playOnAwake = false;
            player.loop = true;
            player.spatialBlend = 0f;
            player.volume = 0f;
            return player;
        }

        /// <summary>The piece of music to have playing: it fades in over whatever was. Null fades to silence.</summary>
        public void Music(string name)
        {
            if (name == musicName) return;
            musicName = name;
            if (name == null || !recorded.TryGetValue(name, out List<AudioClip> pieces)) return;   // what was playing just fades away
            musicFront = 1 - musicFront;
            AudioSource front = musicPlayers[musicFront];
            front.clip = pieces[0];
            front.volume = 0f;
            front.Play();
        }

        /// <summary>Holds the music down for a while, under a fanfare.</summary>
        public void Duck(float seconds) { duckLeft = Mathf.Max(duckLeft, seconds); }

        /// <summary>How loud the crowd's murmur should be, from 0 (the lobby) to 1.</summary>
        public void Crowd(float level) { crowdWanted = Mathf.Clamp01(level); }

        /// <summary>One continuous, pitched shimmer for the whole train of trophies entering the gauge.</summary>
        public void SetTrophyCharge(float intensity, float sweep)
        {
            intensity = Mathf.Clamp01(intensity);
            if (intensity <= 0f)
            {
                trophyChargeVolume = 0f;
                return;
            }
            sweep = Mathf.Clamp01(sweep);
            trophyChargePitch = Mathf.Pow(2f, Mathf.Lerp(-4f, 14f, sweep) / 12f);
            trophyChargeVolume = Mathf.Lerp(.45f, .59f, sweep) * Mathf.Sqrt(intensity);
            if (trophyChargePlaying || source == null) return;
            if (trophyChargePlayer == null)
            {
                if (!recorded.TryGetValue("trophy-charge", out var charge)) return;
                trophyChargePlayer = Looping(source.gameObject);
                trophyChargePlayer.clip = charge[0];
            }
            trophyChargePlayer.pitch = trophyChargePitch;
            trophyChargePlayer.volume = 0f;
            trophyChargePlayer.Play();
            trophyChargePlaying = true;
            Heard?.Invoke("trophy-charge", trophyChargeVolume);
        }

        /// <summary>Moves the music and the crowd towards where they should be. Called once a frame, in real time.</summary>
        public void Tick(float delta)
        {
            if (trophyChargePlaying && trophyChargePlayer != null)
            {
                trophyChargePlayer.pitch = Mathf.Lerp(trophyChargePlayer.pitch, trophyChargePitch, 1f - Mathf.Exp(-20f * delta));
                trophyChargePlayer.volume = Mathf.MoveTowards(trophyChargePlayer.volume, trophyChargeVolume, delta * 3.5f);
                if (trophyChargeVolume <= 0f && trophyChargePlayer.volume <= 0f)
                {
                    trophyChargePlayer.Stop();
                    trophyChargePlaying = false;
                }
            }
            duckLeft -= delta;
            float level = duckLeft > 0f ? MusicDucked : MusicLevel;
            bool wanted = musicName != null && recorded.ContainsKey(musicName);
            for (int i = 0; i < musicPlayers.Length; i++)
            {
                AudioSource player = musicPlayers[i];
                if (player == null) continue;
                float target = wanted && i == musicFront ? level : 0f;
                player.volume = Mathf.MoveTowards(player.volume, target, delta / MusicFade * MusicLevel);
                if (target <= 0f && player.volume <= 0f && player.isPlaying) player.Stop();
            }
            if (crowdPlayer == null || crowdPlayer.clip == null) return;
            crowdPlayer.volume = Mathf.MoveTowards(crowdPlayer.volume, crowdWanted, delta * .6f);
            if (crowdPlayer.volume > 0f && !crowdPlayer.isPlaying) crowdPlayer.Play();
            else if (crowdPlayer.volume <= 0f && crowdWanted <= 0f && crowdPlayer.isPlaying) crowdPlayer.Stop();
        }

        static readonly float[] EventNotes = { 523f, 659f, 880f }, HealNotes = { 659f, 784f, 1047f, 1319f };
        static readonly float[] DrumBeats = { 0f, .19f, .33f, .44f, .52f };

        /// <summary>Low drums struck at these moments, each a little harder than the one before.</summary>
        static float Drums(float t, float[] beats)
        {
            float sum = 0f;
            for (int n = 0; n < beats.Length; n++)
            {
                float since = t - beats[n];
                if (since < 0f) break;
                float hard = .5f + .5f * n / (beats.Length - 1);
                // The skin's own note, a deeper one under it for speakers that can carry it, and the slap of the stick.
                sum += hard * (Tone(since, 196f, 98f, .12f) * .7f * Mathf.Exp(-since * 13f) + Tone(since, 104f, 50f, .16f) * .6f * Mathf.Exp(-since * 14f)
                    + Noise(since) * .4f * Mathf.Exp(-since * 45f));
            }
            return sum;
        }

        /// <summary>A run of notes, one starting every so often, each ringing on and dying away.</summary>
        static float Notes(float t, float[] notes, float apart, float fade)
        {
            float sum = 0f;
            for (int n = 0; n < notes.Length; n++)
            {
                float since = t - n * apart;
                if (since >= 0f) sum += Mathf.Sin(notes[n] * since * Mathf.PI * 2f) * Mathf.Exp(-since * fade);
            }
            return sum;
        }

        static readonly float[] PowerNotes = { 392f, 523f, 659f, 784f, 1047f, 1319f };

        /// <summary>Told about every sound as it plays (the self-test video uses it to rebuild the soundtrack).</summary>
        public System.Action<string, float> Heard;

        public void Play(string name, float volume = 1f)
        {
            if (Heard != null) Heard(name, volume);
            ColosseumHaptics.OnSound(name, volume);
            if (source == null) return;
            if (!recorded.ContainsKey(name) && !clips.ContainsKey(name) && StandIn.TryGetValue(name, out string other)) name = other;
            if (recorded.TryGetValue(name, out List<AudioClip> takes))
            {
                lastTake.TryGetValue(name, out int last);
                int pick = (last + 1) % takes.Count;
                lastTake[name] = pick;
                source.PlayOneShot(takes[pick], volume);
            }
            else if (clips.TryGetValue(name, out AudioClip clip))
                source.PlayOneShot(clip, volume);
        }

        void Make(string name, float seconds, System.Func<float, float> wave)
        {
            int count = Mathf.CeilToInt(seconds * Rate);
            var data = new float[count];
            for (int i = 0; i < count; i++)
            {
                float t = (float)i / Rate;
                float fade = Mathf.Clamp01((seconds - t) / (seconds * .6f)) * Mathf.Clamp01(t / .004f);
                data[i] = Mathf.Clamp(wave(t) * fade, -1f, 1f);
            }
            AudioClip clip = AudioClip.Create("Colosseum " + name, count, 1, Rate, false);
            clip.SetData(data, 0);
            clips[name] = Kit.Own(clip);
        }

        static float Phase(float t, float from, float to, float length)
        {
            if (to <= 0f || length <= 0f)
                return from * t;
            float k = Mathf.Clamp01(t / length);
            return (from + (to - from) * k * .5f) * t;
        }

        static float Tone(float t, float from, float to, float length) { return Mathf.Sin(Phase(t, from, to, length) * Mathf.PI * 2f); }
        static float Square(float t, float from, float to, float length) { return Tone(t, from, to, length) >= 0f ? 1f : -1f; }
        static float Saw(float t, float from, float to, float length) { return Mathf.Repeat(Phase(t, from, to, length), 1f) * 2f - 1f; }
        static float Noise(float t) { return Random.value * 2f - 1f; }
    }
}
