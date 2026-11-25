using UnityEngine;
using UnityEngine.UI;

namespace SlingArena.Colosseum
{
    /// <summary>Screen-print highlights and halftone ink in the corners of a collectible card.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class ColosseumCardFoil : MaskableGraphic
    {
        public static void AddTo(Transform parent)
        {
            var foil = new GameObject("Foil print", typeof(RectTransform), typeof(ColosseumCardFoil))
                .GetComponent<ColosseumCardFoil>();
            var rect = foil.rectTransform;
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            foil.raycastTarget = false;
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            Rect r = rectTransform.rect;
            Color32 glint = new Color32(255, 255, 255, 45);
            Strip(mesh, r, .08f, .16f, .34f, glint);
            Strip(mesh, r, .185f, .207f, .0935f, glint);

            Color32 ink = new Color32(17, 35, 88, 53);
            float spacing = Mathf.Min(r.width / 25f, r.height / 22f);
            for (int y = 0; y < 5; y++)
            {
                for (int x = 0; x < 11 - y * 2; x++)
                {
                    float radius = spacing * (.34f - .019f * x - .03f * y);
                    var centre = new Vector2(r.xMax - (x + .95f + .5f * (y % 2)) * spacing,
                        r.yMin + (y + .9f) * spacing);
                    int first = mesh.currentVertCount;
                    for (int i = 0; i < 6; i++)
                    {
                        float angle = i * Mathf.PI / 3f;
                        var offset = radius * new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                        mesh.AddVert(centre + offset, ink, Vector2.zero);
                    }
                    for (int i = 1; i < 5; i++) mesh.AddTriangle(first, first + i, first + i + 1);
                }
            }
        }

        static void Strip(VertexHelper mesh, Rect r, float inner, float outer, float drop, Color32 color)
        {
            int first = mesh.currentVertCount;
            mesh.AddVert(new Vector2(r.xMin + inner * r.width, r.yMax), color, Vector2.zero);
            mesh.AddVert(new Vector2(r.xMin + outer * r.width, r.yMax), color, Vector2.zero);
            mesh.AddVert(new Vector2(r.xMin, r.yMax - drop * r.height), color, Vector2.zero);
            mesh.AddVert(new Vector2(r.xMin, r.yMax - drop * (inner / outer) * r.height), color, Vector2.zero);
            mesh.AddTriangle(first, first + 1, first + 2);
            mesh.AddTriangle(first, first + 2, first + 3);
        }
    }
}
