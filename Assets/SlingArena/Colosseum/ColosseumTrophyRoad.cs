using UnityEngine;
using UnityEngine.UI;

namespace SlingArena.Colosseum
{
    /// <summary>A single gold-edged trophy gauge with two graduations per win.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class ColosseumTrophyRoad : MaskableGraphic
    {
        const float DesignWidth = 266f;
        const int CellsPerWin = 2;
        static readonly Color Ink = new Color32(40, 64, 82, 255);
        static readonly Color Rim = new Color32(255, 219, 150, 255);
        static readonly Color EmptySeam = new Color32(70, 114, 133, 255);
        static readonly Color EmptyBase = new Color32(93, 141, 154, 255);
        static readonly Color EmptyFace = new Color32(125, 168, 179, 255);
        static readonly Color EmptyTop = new Color32(168, 204, 207, 255);
        static readonly Color GoldSeam = new Color32(197, 125, 51, 255);
        static readonly Color Apricot = new Color32(255, 150, 123, 255);
        static readonly Color Honey = new Color32(255, 198, 67, 255);
        static readonly Color Lemon = new Color32(255, 237, 132, 255);
        static readonly Color Gleam = new Color32(255, 248, 199, 255);

        float progress, receiptIntensity, receiptSweep;

        public static float HalfWidth(float fraction)
        {
            float near = 1f - Mathf.Clamp01(fraction);
            return 44f + 89f * near * (.58f + .42f * near);
        }

        public void SetProgress(float fraction)
        {
            float next = Mathf.Clamp01(fraction);
            if (Mathf.Approximately(progress, next)) return;
            progress = next;
            SetVerticesDirty();
        }

        /// <summary>Lights the gauge while trophies arrive. Sweep runs from the first cup to the last.</summary>
        public void SetReceiptCharge(float intensity, float sweep)
        {
            intensity = Mathf.Clamp01(intensity);
            sweep = intensity > 0f ? Mathf.Clamp01(sweep) : 0f;
            if (Mathf.Approximately(receiptIntensity, intensity) && Mathf.Approximately(receiptSweep, sweep)) return;
            receiptIntensity = intensity;
            receiptSweep = sweep;
            SetVerticesDirty();
        }

        protected override void Awake()
        {
            base.Awake();
            raycastTarget = false;
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect rect = GetPixelAdjustedRect();
            if (rect.width <= 0f || rect.height <= 0f) return;

            var mesh = new RailMesh(vh, rect, color, .7f / Mathf.Max(.01f, canvas.scaleFactor));
            mesh.DrawBacking(0f, 1f, Ink);
            mesh.DrawBacking(2.5f, 1f, Rim);
            mesh.DrawBacking(5f, 1f, EmptySeam);
            mesh.DrawBacking(5f, progress, GoldSeam);

            float trophiesPerCell = (float)ColosseumSave.WinTrophies / CellsPerWin;
            int count = Mathf.CeilToInt(ColosseumSave.StageGoal / trophiesPerCell);
            for (int i = 0; i < count; i++)
            {
                float start = i * trophiesPerCell / ColosseumSave.StageGoal;
                float end = Mathf.Min(1f, (i + 1) * trophiesPerCell / ColosseumSave.StageGoal);
                mesh.DrawCell(start, end, progress);
            }
            if (receiptIntensity > 0f) mesh.DrawReceipt(progress, receiptIntensity, receiptSweep);
        }

        readonly struct RailMesh
        {
            readonly VertexHelper vertices;
            readonly Rect rect;
            readonly Color tint;
            readonly float halfPixel;

            public RailMesh(VertexHelper vertices, Rect rect, Color tint, float halfPixel)
            {
                this.vertices = vertices;
                this.rect = rect;
                this.tint = tint;
                this.halfPixel = halfPixel;
            }

            // Every cell shares this curved border. The thin space between faces exposes the recessed seam.
            public void DrawBacking(float inset, float fill, Color color)
            {
                float baseY = rect.yMin + inset;
                float topY = Mathf.Min(rect.yMax - inset, rect.yMin + rect.height * fill);
                if (topY <= baseY) return;
                int first = vertices.currentVertCount;
                const int sections = 24;
                Row(baseY, inset, color);
                for (int i = 1; i < sections; i++)
                {
                    float y = Mathf.Lerp(rect.yMin, rect.yMax, (float)i / sections);
                    if (y > baseY && y < topY) Row(y, inset, color);
                }
                Row(topY, inset, color);
                ConnectRows(first);
            }

            public void DrawCell(float start, float end, float fill)
            {
                const float halfSeam = 1.15f;
                float low = Mathf.Max(rect.yMin + 5f, rect.yMin + start * rect.height + halfSeam);
                float high = Mathf.Min(rect.yMax - 5f, rect.yMin + end * rect.height - halfSeam);
                if (high <= low) return;
                float frontier = Mathf.Min(rect.yMin + fill * rect.height, high);
                DrawFace(low, high, high, EmptyBase, EmptyFace, EmptyTop);
                if (frontier <= low) return;

                // Anchor the colour to the road so already earned cells keep their hue while the next one fills.
                float level = (start + end) * .5f;
                Color warm = Color.Lerp(Apricot, Honey, Mathf.InverseLerp(0f, .45f, level));
                Color face = Color.Lerp(warm, Lemon, Mathf.InverseLerp(.45f, 1f, level));
                DrawFace(low, high, frontier, Color.Lerp(face, GoldSeam, .42f), face, Color.Lerp(face, Gleam, .48f));
            }

            void DrawFace(float low, float high, float frontier, Color baseColor, Color face, Color top)
            {
                float bevel = Mathf.Min(2.4f, (high - low) * .12f);
                int first = vertices.currentVertCount;
                Row(low, 5f, baseColor);
                Band(low, low + bevel, baseColor, face, frontier);
                Band(low + bevel, high - bevel, face, face, frontier);
                Band(high - bevel, high, face, top, frontier);
                ConnectRows(first);
            }

            void Band(float low, float high, Color below, Color above, float frontier)
            {
                if (frontier <= low) return;
                float y = Mathf.Min(frontier, high);
                Row(y, 5f, Color.Lerp(below, above, (y - low) / (high - low)));
            }

            float Edge(float y, float inset)
            {
                float t = (y - rect.yMin) / rect.height;
                float endDistance = Mathf.Min(y - rect.yMin, rect.yMax - y);
                float corner = Mathf.Max(0f, 5f - endDistance);
                return (HalfWidth(t) - inset - corner) * rect.width / DesignWidth;
            }

            float EdgeNormal(float y)
            {
                float t = (y - rect.yMin) / rect.height;
                float slope = 89f * (1.42f - .84f * t) * rect.width / (DesignWidth * rect.height);
                return Mathf.Sqrt(1f + slope * slope);
            }

            void Row(float y, float inset, Color color)
            {
                float half = Edge(y, inset);
                float feather = Mathf.Min(half, halfPixel * EdgeNormal(y));
                Color32 c = color * tint;
                Color32 clear = c;
                clear.a = 0;
                float left = rect.center.x - half, right = rect.center.x + half;
                vertices.AddVert(new Vector2(left - feather, y), clear, Vector2.zero);
                vertices.AddVert(new Vector2(left + feather, y), c, Vector2.zero);
                vertices.AddVert(new Vector2(right - feather, y), c, Vector2.zero);
                vertices.AddVert(new Vector2(right + feather, y), clear, Vector2.zero);
            }

            public void DrawReceipt(float fill, float intensity, float sweep)
            {
                // Sample the same curved edges as the recessed track, including its clipped corners.
                for (int side = -1; side <= 1; side += 2)
                {
                    int first = vertices.currentVertCount;
                    const int sections = 24;
                    for (int i = 0; i <= sections; i++)
                    {
                        float t = i / (float)sections;
                        float y = Mathf.Lerp(rect.yMin + 1f, rect.yMax - 1f, t);
                        float nearFront = 1f - Mathf.Clamp01(Mathf.Abs(t - fill) * rect.height / 30f);
                        float wave = Mathf.Clamp01(1f - Mathf.Abs(t - sweep * fill) * 8f);
                        float brightness = (.26f + .28f * Mathf.Clamp01((fill - t) * 20f)
                            + .28f * nearFront + .39f * wave * wave) * intensity;
                        float x = rect.center.x + side * Edge(y, 2.3f);
                        float normal = EdgeNormal(y);
                        float core = normal * Mathf.Max(.85f, 1.45f * intensity);
                        float halo = normal * (halfPixel + 4.7f + 2f * wave);
                        AddLightVertex(x - halo, y, 0f);
                        AddLightVertex(x - core - halfPixel, y, brightness * .24f);
                        AddLightVertex(x - core, y, brightness);
                        AddLightVertex(x + core, y, brightness);
                        AddLightVertex(x + core + halfPixel, y, brightness * .24f);
                        AddLightVertex(x + halo, y, 0f);
                    }
                    ConnectRows(first, 6);
                }

                if (fill <= 0f) return;
                float front = Mathf.Clamp(rect.yMin + fill * rect.height, rect.yMin + 2f, rect.yMax - 2f);
                int crest = vertices.currentVertCount;
                var white = new Color(1f, 1f, 1f, intensity * .95f);
                var clear = new Color(1f, 1f, 1f, 0f);
                Row(Mathf.Max(rect.yMin, front - 8f), 3f, clear);
                Row(front - 1.3f, 3f, white);
                Row(front + 1.3f, 3f, white);
                Row(Mathf.Min(rect.yMax, front + 6f), 3f, clear);
                ConnectRows(crest);
            }

            void AddLightVertex(float x, float y, float alpha)
            {
                var tone = tint;
                tone.a *= Mathf.Clamp01(alpha);
                vertices.AddVert(new Vector2(x, y), tone, Vector2.zero);
            }

            void ConnectRows(int first, int stride = 4)
            {
                for (int row = first; row + stride < vertices.currentVertCount; row += stride)
                {
                    for (int i = row; i < row + stride - 1; i++)
                    {
                        vertices.AddTriangle(i, i + stride, i + stride + 1);
                        vertices.AddTriangle(i, i + stride + 1, i + 1);
                    }
                }
            }
        }
    }
}
