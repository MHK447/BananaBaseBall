using UnityEngine;
using UnityEngine.UI;

namespace SlingArena.Colosseum
{
    /// <summary>The team-blue deck and warm sand collection share a moving sword and helmet pattern.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class ColosseumDeckBackground : MaskableGraphic
    {
        public const float CollectionHeaderDepth = 464f;

        protected override void Awake()
        {
            base.Awake();
            if (Application.isPlaying)
                ColosseumBackdropTiles.Add(rectTransform, ColosseumBackdropTiles.Motif.SwordsAndHelmets, new Color32(255, 255, 249, 78));
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            Rect r = rectTransform.rect;
            float divider = Mathf.Max(r.yMin, r.yMax - CollectionHeaderDepth);
            Band(mesh, r, r.yMin, divider, new Color32(247, 213, 174, 255), new Color32(255, 239, 209, 255));
            Band(mesh, r, divider, r.yMax, new Color32(157, 206, 236, 255), new Color32(217, 242, 255, 255));
        }

        static void Band(VertexHelper v, Rect r, float y0, float y1, Color32 bottom, Color32 top)
        {
            int k = v.currentVertCount;
            v.AddVert(new Vector2(r.xMin,y0), bottom, Vector2.zero);
            v.AddVert(new Vector2(r.xMin,y1), top, Vector2.zero);
            v.AddVert(new Vector2(r.xMax,y1), top, Vector2.zero);
            v.AddVert(new Vector2(r.xMax,y0), bottom, Vector2.zero);
            v.AddTriangle(k,k+1,k+2);
            v.AddTriangle(k+2,k+3,k);
        }
    }
}
