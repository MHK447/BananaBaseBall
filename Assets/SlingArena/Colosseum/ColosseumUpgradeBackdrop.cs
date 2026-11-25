using UnityEngine;
using UnityEngine.UI;

namespace SlingArena.Colosseum
{
    /// <summary>A daylight sky with warm light behind the face of the upgrade card.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class ColosseumUpgradeBackdrop : MaskableGraphic
    {
        const int Columns = 9;

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            Rect rect = rectTransform.rect;
            SkyLine(mesh, rect, 0f,   new Color32(126, 155, 207, 255), new Color32(167, 198, 227, 255));
            SkyLine(mesh, rect, .30f, new Color32(157, 196, 226, 255), new Color32(211, 230, 237, 255));
            SkyLine(mesh, rect, .59f, new Color32(174, 216, 234, 255), new Color32(255, 237, 205, 255));
            SkyLine(mesh, rect, .82f, new Color32(128, 187, 226, 255), new Color32(197, 230, 242, 255));
            SkyLine(mesh, rect, 1f,   new Color32(105, 166, 218, 255), new Color32(160, 208, 239, 255));
            for (int row = 0; row < 4; row++)
            {
                for (int x = 0; x < Columns - 1; x++)
                {
                    int i = row * Columns + x;
                    mesh.AddTriangle(i, i + Columns, i + Columns + 1);
                    mesh.AddTriangle(i, i + Columns + 1, i + 1);
                }
            }
        }

        static void SkyLine(VertexHelper mesh, Rect rect, float height, Color edge, Color middle)
        {
            for (int x = 0; x < Columns; x++)
            {
                float u = x / (Columns - 1f);
                float light = Mathf.SmoothStep(0f, 1f, 1f - Mathf.Abs(u * 2f - 1f));
                Vector2 point = new Vector2(Mathf.Lerp(rect.xMin, rect.xMax, u), Mathf.Lerp(rect.yMin, rect.yMax, height));
                mesh.AddVert(point, Color.Lerp(edge, middle, light), Vector2.zero);
            }
        }
    }
}
