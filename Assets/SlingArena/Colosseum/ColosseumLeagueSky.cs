using UnityEngine;
using UnityEngine.UI;

namespace SlingArena.Colosseum
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class ColosseumLeagueSky : MaskableGraphic
    {
        Color32 top = new Color32(238, 178, 137, 255);
        Color32 bottom = new Color32(191, 124, 127, 255);

        internal void SetGradient(Color32 upper, Color32 lower)
        {
            top = upper;
            bottom = lower;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            Rect r = rectTransform.rect;
            mesh.AddVert(new Vector2(r.xMin, r.yMin), bottom, Vector2.zero);
            mesh.AddVert(new Vector2(r.xMin, r.yMax), top, Vector2.zero);
            mesh.AddVert(new Vector2(r.xMax, r.yMax), top, Vector2.zero);
            mesh.AddVert(new Vector2(r.xMax, r.yMin), bottom, Vector2.zero);
            mesh.AddTriangle(0, 1, 2);
            mesh.AddTriangle(2, 3, 0);
        }
    }
}
