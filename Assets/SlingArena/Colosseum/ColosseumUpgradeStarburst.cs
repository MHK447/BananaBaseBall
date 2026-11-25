using UnityEngine;
using UnityEngine.UI;

namespace SlingArena.Colosseum
{
    /// <summary>A volley of faceted stars, with two smaller stars following each flight, in one UI mesh.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class ColosseumUpgradeStarburst : MaskableGraphic
    {
        // Screen-relative destination and head radius. Uneven spacing and scale give the burst a few hero stars.
        static readonly Vector3[] Flights = {
            new Vector3(-.84f, .62f, 42f), new Vector3(.77f, .47f, 46f), new Vector3(-.51f, -.36f, 30f),
            new Vector3(.34f, .91f, 35f), new Vector3(-.96f, -.05f, 28f), new Vector3(.88f, -.24f, 36f),
            new Vector3(-.38f, .81f, 26f), new Vector3(.53f, -.53f, 22f), new Vector3(-.93f, .29f, 34f),
            new Vector3(.96f, .11f, 25f), new Vector3(-.17f, .99f, 29f), new Vector3(.65f, .77f, 23f),
            new Vector3(-.85f, -.43f, 23f), new Vector3(.09f, -.59f, 17f), new Vector3(-.56f, .54f, 18f),
            new Vector3(.91f, .63f, 21f), new Vector3(-.71f, .07f, 20f), new Vector3(.75f, -.48f, 21f),
            new Vector3(.10f, .75f, 18f), new Vector3(-.95f, .77f, 18f), new Vector3(.89f, .31f, 18f)
        };

        static readonly Color Gold = new Color32(255, 204, 53, 255);
        static readonly Color Apricot = new Color32(255, 160, 83, 255);
        static readonly Color Ice = new Color32(130, 228, 255, 255);
        static readonly Color Pearl = new Color32(255, 252, 205, 255);
        static readonly Vector2[] Corners = BuildCorners();
        public const float Duration = .83f;
        float age = -1f;

        public void SetAge(float seconds)
        {
            float next = seconds >= 0f && seconds < Duration ? seconds : -1f;
            if (age == next) return;
            age = next;
            SetVerticesDirty();
        }

        static Vector2[] BuildCorners()
        {
            var points = new Vector2[10];
            for (int i = 0; i < points.Length; i++)
            {
                float angle = i * Mathf.PI / 5f;
                points[i] = new Vector2(Mathf.Sin(angle), Mathf.Cos(angle)) * (i % 2 == 0 ? 1f : .48f);
            }
            return points;
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            if (age < 0f) return;
            Rect frame = rectTransform.rect;
            var reach = new Vector2(Mathf.Min(530f, frame.width * .49f), Mathf.Min(570f, frame.height * .42f));
            var origin = new Vector2(frame.center.x, frame.center.y + 34f);
            for (int i = 0; i < Flights.Length; i++)
            {
                float delay = .008f * (i % 5) + (i / 7) * .018f;
                float t = (age - delay) / (.58f + (i % 4) * .053f);
                if (t <= 0f || t >= 1f) continue;
                Vector3 spec = Flights[i];
                Color tint = i % 7 == 2 ? Ice : i % 5 == 4 ? Apricot : Gold;
                Vector2 destination = new Vector2(spec.x * reach.x, spec.y * reach.y);
                float spin = (i % 2 == 0 ? -1f : 1f) * (.58f + (i % 4) * .38f);
                float rotation = i * .73f + spin * t;
                float size = spec.z * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t * 10f));
                float alpha = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.54f, .99f, t));
                if (alpha <= .001f) continue;
                size *= .68f + .32f * alpha;
                Vector2 head = Flight(origin, destination, t, i);
                Vector2 mid = Flight(origin, destination, Mathf.Max(0f, t - .080f), i);
                Vector2 tail = Flight(origin, destination, Mathf.Max(0f, t - .16f), i);
                Streak(mesh, tail, head, size * .17f, tint, alpha * .65f);
                DrawStar(mesh, tail, size * .22f, rotation - .55f, tint, alpha * .52f, false);
                DrawStar(mesh, mid, size * .35f, rotation - .24f, Pearl, alpha * .83f, false);
                DrawStar(mesh, head, size, rotation, tint, alpha, true);
            }
        }

        static Vector2 Flight(Vector2 origin, Vector2 destination, float t, int index)
        {
            float travel = 1f - Mathf.Pow(1f - t, 2.8f);
            float side = Mathf.Sign(destination.x);
            // Initial high velocity opens the centre immediately. Late gravity breaks up the silhouettes.
            Vector2 start = new Vector2(destination.x * .15f, destination.y * .12f);
            Vector2 arc = new Vector2(side * (index % 3 - 1) * 17f * travel, 49f * Mathf.Sin(travel * Mathf.PI) - 53f * t * t);
            return origin + Vector2.LerpUnclamped(start, destination, travel) + arc;
        }

        static void Streak(VertexHelper mesh, Vector2 tail, Vector2 head, float width, Color color, float alpha)
        {
            Vector2 axis = head - tail;
            Vector2 normal = new Vector2(-axis.y, axis.x).normalized * width;
            int k = mesh.currentVertCount;
            color.a = 0f;
            mesh.AddVert(tail, color, Vector2.zero);
            color.a = alpha;
            mesh.AddVert(head + normal, color, Vector2.zero);
            mesh.AddVert(head - normal, color, Vector2.zero);
            mesh.AddTriangle(k, k + 1, k + 2);
        }

        static void DrawStar(VertexHelper mesh, Vector2 center, float radius, float rotation, Color fill, float alpha, bool faceted)
        {
            Color light = Color.Lerp(fill, Pearl, .77f);
            Color edge = Color.Lerp(fill, new Color32(87, 47, 95, 255), .65f);
            Color shadow = Color.Lerp(fill, new Color32(194, 74, 41, 255), .44f);
            fill.a = light.a = edge.a = shadow.a = alpha;
            float c = Mathf.Cos(rotation) * radius, s = Mathf.Sin(rotation) * radius;
            int start = mesh.currentVertCount;
            mesh.AddVert(center + new Vector2(-.10f, .12f) * radius, light, Vector2.zero);
            for (int i = 0; i < Corners.Length; i++)
            {
                var p = Corners[i];
                Vector2 ray = new Vector2(c * p.x - s * p.y, s * p.x + c * p.y);
                var face = faceted ? (ray.y > 0f ? light : i % 2 == 0 ? fill : shadow) : fill;
                mesh.AddVert(center + ray, faceted ? edge : face, Vector2.zero);
                if (faceted) mesh.AddVert(center + ray * .82f, face, Vector2.zero);
            }
            for (int i = 0; i < Corners.Length; i++)
            {
                int next = (i + 1) % Corners.Length;
                if (faceted)
                {
                    int a = start + 1 + 2 * i, b = start + 1 + 2 * next;
                    mesh.AddTriangle(a, b, a + 1);
                    mesh.AddTriangle(b, b + 1, a + 1);
                    mesh.AddTriangle(start, a + 1, b + 1);
                }
                else mesh.AddTriangle(start, start + 1 + i, start + 1 + next);
            }
        }
    }
}
