using UnityEngine;
using UnityEngine.UI;

namespace SlingArena.Colosseum
{
    /// <summary>Lights the top of a HUD surface and shades its base while retaining the sprite's alpha.</summary>
    [AddComponentMenu("")]
    public sealed class ColosseumHudShade : BaseMeshEffect
    {
        public float highlight;
        public float shadow;

        public override void ModifyMesh(VertexHelper helper)
        {
            if (!IsActive()) return;

            Rect rect = graphic.rectTransform.rect;
            var vertex = new UIVertex();
            for (int i = 0; i < helper.currentVertCount; i++)
            {
                helper.PopulateUIVertex(ref vertex, i);
                Color tint = vertex.color;
                float height = Mathf.InverseLerp(rect.yMin, rect.yMax, vertex.position.y);
                Color lower = Color.Lerp(tint, Color.black, shadow);
                Color upper = Color.Lerp(tint, Color.white, highlight);
                Color shaded = Color.Lerp(lower, upper, height);
                shaded.a = tint.a;
                vertex.color = shaded;
                helper.SetUIVertex(vertex, i);
            }
        }
    }
}
