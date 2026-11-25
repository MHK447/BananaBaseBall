using UnityEngine;
using UnityEngine.UI;

namespace SlingArena.Colosseum
{
    /// <summary>Leans a UI graphic sideways into a parallelogram, for the slanted plates and buttons of the lobby.</summary>
    public sealed class ColosseumSlant : BaseMeshEffect
    {
        /// <summary>Sideways shift per unit of height; positive leans the top edge to the right.</summary>
        public float lean = .14f;

        public override void ModifyMesh(VertexHelper helper)
        {
            if (!IsActive() || (TryGetComponent<ColosseumUiContour>(out var contour) && contour.isActiveAndEnabled)) return;
            Apply(helper);
        }

        internal void Apply(VertexHelper helper)
        {
            float middle = graphic.rectTransform.rect.center.y;
            var vertex = new UIVertex();
            for (int i = 0; i < helper.currentVertCount; i++)
            {
                helper.PopulateUIVertex(ref vertex, i);
                vertex.position.x += (vertex.position.y - middle) * lean;
                helper.SetUIVertex(vertex, i);
            }
        }
    }
}
