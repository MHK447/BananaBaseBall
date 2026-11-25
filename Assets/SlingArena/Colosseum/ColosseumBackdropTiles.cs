using UnityEngine;
using UnityEngine.UI;

namespace SlingArena.Colosseum
{
    /// <summary>A single stamped layer, padded by one repeat so the edges stay filled as it scrolls.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class ColosseumBackdropTiles : MaskableGraphic
    {
        public enum Motif { Stars, SwordsAndHelmets, ChestsAndCards }
        const float CellWidth = 172f, CellHeight = 148f;
        public const float TravelPerSecond = 22f;
        Motif motif;

        // Horizontal sections from top to bottom: height, outer edge, edge of the cutout, central nose guard.
        static readonly Vector4[] Helmet = {
            new(.64f,.09f,0,0), new(.61f,.31f,0,0), new(.52f,.49f,0,0),
            new(.34f,.64f,0,0), new(.13f,.72f,0,0), new(.035f,.73f,.46f,.46f),
            new(-.045f,.73f,.54f,.44f), new(-.18f,.72f,.52f,.13f),
            new(-.30f,.71f,.49f,.115f), new(-.44f,.68f,.32f,.098f),
            new(-.57f,.62f,.32f,.073f), new(-.66f,.56f,.32f,0),
            new(-.79f,.47f,.32f,0), new(-.93f,.32f,.31f,0)
        };
        // The blade has a narrow fuller. The crossguard and pommel flare at the bottom.
        static readonly Vector4[] Sword = {
            new(1f,0,0,0), new(.65f,.16f,0,0), new(.45f,.157f,.035f,0),
            new(-.10f,.137f,.034f,0), new(-.17f,.134f,0,0), new(-.20f,.14f,0,0),
            new(-.24f,.39f,0,0), new(-.32f,.42f,0,0), new(-.37f,.35f,0,0),
            new(-.39f,.09f,0,0), new(-.71f,.085f,0,0), new(-.74f,.145f,0,0),
            new(-.82f,.16f,0,0), new(-.90f,.08f,0,0)
        };
        // Plume slices, left to right: horizontal position, underside, top edge.
        static readonly Vector3[] Plume = {
            new(-.88f,.44f,.46f), new(-.76f,.53f,.69f), new(-.54f,.66f,.91f),
            new(-.26f,.72f,1.03f), new(.02f,.74f,1.065f), new(.30f,.69f,1.01f),
            new(.51f,.61f,.88f), new(.67f,.57f,.72f), new(.70f,.58f,.63f)
        };
        // Rounded lid and front panel with two recessed bands; only the clasp crosses the lid seam.
        static readonly Vector4[] Chest = {
            new(.61f,.49f,.30f,.30f), new(.58f,.63f,.35f,.27f), new(.48f,.74f,.38f,.26f),
            new(.30f,.80f,.40f,.27f), new(.16f,.80f,.40f,.27f), new(.16f,.80f,0,0),
            new(.09f,.80f,0,0), new(.09f,.13f,0,0), new(-.035f,.13f,0,0),
            new(-.035f,.76f,.40f,.27f), new(-.44f,.76f,.40f,.27f), new(-.49f,.74f,.40f,.27f),
            new(-.52f,.69f,.37f,.29f), new(-.55f,.63f,.32f,.32f)
        };
        // A tall card with a diamond cutout. A second tilted card peeks out behind its left edge.
        static readonly Vector4[] Card = {
            new(.76f,.40f,0,0), new(.73f,.48f,0,0), new(.65f,.51f,0,0),
            new(.35f,.51f,0,0), new(.04f,.51f,.24f,0), new(-.27f,.51f,0,0),
            new(-.56f,.51f,0,0), new(-.64f,.48f,0,0), new(-.67f,.40f,0,0)
        };
        // Top to bottom: height, exposed left edge, exposed right edge.
        static readonly Vector3[] BackCard = {
            new(.57f,-.62f,-.61f), new(.53f,-.82f,-.59f), new(.47f,-.87f,-.59f),
            new(.39f,-.86f,-.59f), new(-.49f,-.66f,-.59f), new(-.58f,-.62f,-.59f)
        };

        public static void Add(RectTransform background, Motif motif, Color32 ink)
        {
            // The team panels have a rotated dividing edge and require a stencil; the other panels are axis-aligned.
            float angle = Mathf.DeltaAngle(0f, background.eulerAngles.z);
            if (Mathf.Abs(angle) > .01f) background.gameObject.AddComponent<Mask>();
            else background.gameObject.AddComponent<RectMask2D>();
            var field = new GameObject("Scrolling " + motif, typeof(RectTransform), typeof(ColosseumBackdropTiles));
            var layer = field.GetComponent<ColosseumBackdropTiles>();
            layer.rectTransform.SetParent(background, false);
            layer.rectTransform.anchorMin = Vector2.zero;
            layer.rectTransform.anchorMax = Vector2.one;
            layer.rectTransform.offsetMin = layer.rectTransform.offsetMax = Vector2.zero;
            layer.motif = motif;
            layer.color = ink;
            layer.raycastTarget = false;
            var direction = motif == Motif.Stars ? UITileComponent.Direction.LeftBottom_To_RightTop
                : UITileComponent.Direction.RightBottom_To_LeftTop;
            field.AddComponent<UITileComponent>().ScrollRepeatingGeometry(Vector3.right * (2f * CellWidth),
                Vector3.up * (2f * CellHeight), TravelPerSecond, -angle, direction);
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            Rect rect = rectTransform.rect;
            int rows = Mathf.CeilToInt(rect.height / CellHeight), columns = Mathf.CeilToInt(rect.width / CellWidth);
            for (int row = -2; row <= rows + 2; row++)
                for (int column = -2; column <= columns + 2; column++)
                {
                    var p = new Vector2(rect.xMin + (column + (row & 1) * .5f) * CellWidth, rect.yMin + row * CellHeight);
                    int variation = (column & 1) + (row & 1) * 2;
                    if (motif == Motif.Stars) Star(mesh, p, 25f + variation * 4f, (variation - 1) * .19f);
                    else if (motif == Motif.ChestsAndCards)
                    {
                        bool chest = ((row + column) & 1) == 0;
                        Pictogram(mesh, p, 44f + variation * 2f, chest ? -10f : 13f, chest ? Chest : Card);
                    }
                    else
                    {
                        bool helmet = ((row + column) & 1) != 0;
                        Pictogram(mesh, p, 52f + variation * 3f, helmet ? -4f + (row & 1) * 8f : -34f, helmet ? Helmet : Sword);
                    }
                }
        }

        void Star(VertexHelper mesh, Vector2 center, float radius, float spin)
        {
            int first = mesh.currentVertCount;
            for (int i = 0; i < 10; i++)
            {
                float angle = Mathf.PI * 2f * i / 10f + spin;
                var dir = new Vector2(Mathf.Sin(angle), Mathf.Cos(angle));
                mesh.AddVert(center + dir * (radius * (i % 2 == 0 ? 1f : .46f)), color, Vector2.zero);
            }
            mesh.AddVert(center, color, Vector2.zero);
            for (int i = 0; i < 10; i++) mesh.AddTriangle(first + 10, first + i, first + (i + 1) % 10);
        }

        void Pictogram(VertexHelper mesh, Vector2 center, float radius, float angle, Vector4[] shape)
        {
            var xAxis = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad)) * radius;
            var yAxis = new Vector2(-xAxis.y, xAxis.x);
            void Vertex(float x, float y) => mesh.AddVert(center + xAxis * x + yAxis * y, color, Vector2.zero);
            void Join(int i, int stride)
            {
                mesh.AddTriangle(i - stride, i - stride + 1, i);
                mesh.AddTriangle(i, i - stride + 1, i + 1);
            }

            for (int i = 0; i < shape.Length; i++)
            {
                Vector4 row = shape[i];
                int first = mesh.currentVertCount;
                Vertex(-row.y,row.x); Vertex(-row.z,row.x);
                Vertex(-row.w,row.x); Vertex(row.w,row.x);
                Vertex(row.z,row.x); Vertex(row.y,row.x);
                if (i == 0) continue;
                for (int strip = 0; strip < 3; strip++) Join(first + strip * 2, 6);
            }
            if (shape == Card)
            {
                for (int i = 0; i < BackCard.Length; i++)
                {
                    var row = BackCard[i];
                    int first = mesh.currentVertCount;
                    Vertex(row.y, row.x); Vertex(row.z, row.x);
                    if (i > 0) Join(first, 2);
                }
            }
            if (shape != Helmet) return;
            int stem = mesh.currentVertCount;
            Vertex(-.085f,.64f); Vertex(.085f,.64f); Vertex(-.085f,.73f); Vertex(.085f,.73f);
            Join(stem + 2, 2);
            for (int i = 0; i < Plume.Length; i++)
            {
                Vector3 edge = Plume[i];
                int first = mesh.currentVertCount;
                Vertex(edge.x,edge.y); Vertex(edge.x,edge.z);
                if (i > 0) Join(first, 2);
            }
        }
    }
}
