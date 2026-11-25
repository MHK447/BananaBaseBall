using UnityEngine;
using UnityEngine.UI;

namespace SlingArena.Colosseum
{
    /// <summary>Five faceted stars, one upgrade per facet color and position.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class ColosseumLevelStars : MaskableGraphic
    {
        const int Slots = 5;
        [SerializeField] int level = 1;

        // Socket, gold, emerald, sapphire, ruby. Each has an independent light and shadow.
        static readonly Color32[] Face = {
            new Color32(71, 87, 114, 255), new Color32(255, 194, 44, 255), new Color32(62, 218, 121, 255),
            new Color32(57, 171, 255, 255), new Color32(255, 88, 98, 255)
        };
        static readonly Color32[] Light = {
            new Color32(88, 105, 132, 255), new Color32(255, 248, 169, 255), new Color32(186, 255, 198, 255),
            new Color32(185, 239, 255, 255), new Color32(255, 209, 192, 255)
        };
        static readonly Color32[] Shade = {
            new Color32(62, 77, 102, 255), new Color32(237, 132, 22, 255), new Color32(16, 152, 92, 255),
            new Color32(42, 99, 208, 255), new Color32(204, 46, 74, 255)
        };
        static readonly Vector2[] Points = MakePoints();
        static readonly Color32 Edge = new Color32(31, 39, 63, 255);
        static readonly Color32 ClearEdge = new Color32(31, 39, 63, 0);

        public static ColosseumLevelStars Create(Transform parent, Vector2 position, Vector2 size)
        {
            var row = new GameObject("Level stars", typeof(RectTransform), typeof(ColosseumLevelStars))
                .GetComponent<ColosseumLevelStars>();
            row.rectTransform.SetParent(parent, false);
            row.rectTransform.sizeDelta = size;
            row.rectTransform.anchoredPosition = position;
            row.raycastTarget = false;
            return row;
        }

        public void SetLevel(int value)
        {
            value = Mathf.Clamp(value, 0, ColosseumRoster.MaxLevel);
            if (level == value) return;
            level = value;
            SetVerticesDirty();
        }

        static Vector2[] MakePoints()
        {
            var points = new Vector2[Slots * 2];
            for (int i = 0; i < points.Length; i++)
            {
                float angle = i * Mathf.PI / Slots;
                float radius = i % 2 == 0 ? 1f : .55f;
                points[i] = new Vector2(Mathf.Sin(angle), Mathf.Cos(angle)) * radius;
            }
            return points;
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            Rect rect = rectTransform.rect;
            float step = rect.width / Slots;
            float radius = Mathf.Min(step * .51f, rect.height * .5f);
            // The scale of a detail card differs from a deck card. Convert the screen pixel
            // into a radial offset: the distance from the center to a star edge is .5033 radii.
            float cardScale = Mathf.Abs(rectTransform.lossyScale.x / canvas.rootCanvas.transform.lossyScale.x);
            float feather = Mathf.Min(radius * .30f, .65f / Mathf.Max(.01f, .5033f * cardScale * canvas.scaleFactor));
            for (int slot = 0; slot < Slots; slot++)
            {
                int tier = Mathf.Clamp((level + Slots - 1 - slot) / Slots, 0, Face.Length - 1);
                var center = new Vector2(rect.xMin + (slot + .5f) * step, rect.center.y - radius * .07f);
                Star(mesh, center, radius, tier, feather);
            }
        }

        static void Star(VertexHelper mesh, Vector2 center, float radius, int tier, float feather)
        {
            float outline = radius * .965f;
            float enamel = radius * .76f;
            int start = mesh.currentVertCount;
            for (int i = 0; i < Points.Length; i++)
            {
                mesh.AddVert(center + Points[i] * (outline + feather), ClearEdge, Vector2.zero);
                mesh.AddVert(center + Points[i] * (outline - feather), Edge, Vector2.zero);
                mesh.AddVert(center + Points[i] * (enamel - feather), Edge, Vector2.zero);
            }
            for (int i = 0; i < Points.Length; i++)
            {
                int next = (i + 1) % Points.Length;
                int a = start + 3 * i, b = start + 3 * next;
                for (int ring = 0; ring < 2; ring++)
                {
                    mesh.AddTriangle(a + ring, b + ring, a + ring + 1);
                    mesh.AddTriangle(b + ring, b + ring + 1, a + ring + 1);
                }
                // Alternating light planes meet just above the center of the enamel face.
                Color32 plane = i < 2 || i >= 7 ? Light[tier] : Shade[tier];
                if (i % 2 == 0) plane = Face[tier];
                Color32 clearPlane = plane;
                clearPlane.a = 0;
                int first = mesh.currentVertCount;
                mesh.AddVert(center + new Vector2(-.08f, .08f) * radius, plane, Vector2.zero);
                mesh.AddVert(center + Points[i] * (enamel - feather), plane, Vector2.zero);
                mesh.AddVert(center + Points[next] * (enamel - feather), plane, Vector2.zero);
                mesh.AddVert(center + Points[i] * (enamel + feather), clearPlane, Vector2.zero);
                mesh.AddVert(center + Points[next] * (enamel + feather), clearPlane, Vector2.zero);
                mesh.AddTriangle(first, first + 1, first + 2);
                mesh.AddTriangle(first + 1, first + 3, first + 4);
                mesh.AddTriangle(first + 1, first + 4, first + 2);
            }
        }
    }
}
