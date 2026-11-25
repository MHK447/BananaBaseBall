using UnityEngine;
using UnityEngine.UI;

namespace SlingArena.Colosseum
{
    public sealed partial class ColosseumHud
    {
        DecreeParchment decreeParchment;

        static float DecreeRollCenter(float spread) => Mathf.Lerp(16f, 315f, spread);

        void BuildDecreeScroll()
        {
            var page = (RectTransform)Group("Royal parchment", decreeBand).transform;
            Place(page, decreeBand, DecreeCenter, Vector2.zero, new Vector2(672f, 330f));
            decreeParchment = page.gameObject.AddComponent<DecreeParchment>();
            decreeParchment.raycastTarget = false;
        }

        void SetDecreeScroll(float spread, float resultReveal)
        {
            decreeParchment.SetSheet(DecreeRollCenter(spread), resultReveal);
        }

        [RequireComponent(typeof(CanvasRenderer))]
        sealed class DecreeParchment : MaskableGraphic
        {
            const float OutlineWidth = 3.3f;
            const float RollBottom = -68f, RollTop = 86f;
            static readonly Color32 OutlineInk = new Color32(36, 29, 30, 255);
            static readonly Color32 GoldShade = new Color32(206, 152, 72, 255);
            static readonly Color32 Gold = new Color32(244, 177, 66, 255);
            static readonly Color32 Gilt = new Color32(255, 227, 131, 255);
            static readonly Color32 Cream = new Color32(255, 232, 184, 255);
            static readonly Color32 Paper = new Color32(247, 207, 139, 255);
            static readonly Color32 Fold = new Color32(232, 187, 122, 255);
            static readonly Color32 Tea = new Color32(218, 159, 88, 255);
            static readonly Color32 Rule = new Color32(216, 162, 87, 255);
            static readonly Color32 Wax = new Color32(181, 50, 60, 255);
            static readonly Color32 WaxEdge = new Color32(104, 41, 48, 255);
            static readonly Vector2[] CapCircle = CapDirections();

            static Vector2[] CapDirections()
            {
                var directions = new Vector2[40];
                for (int i = 0; i < directions.Length; i++)
                {
                    float angle = i * (2f * Mathf.PI / directions.Length);
                    directions[i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                }
                return directions;
            }

            float halfWidth = 16f;
            float drop;
            float halfFeather = .7f;

            public void SetSheet(float width, float reveal)
            {
                // The overlay can be drawn at less than one pixel per canvas unit.
                float feather = Mathf.Min(OutlineWidth * .48f, .72f / Mathf.Max(.01f, canvas.scaleFactor));
                if (halfWidth == width && drop == reveal && halfFeather == feather) return;
                halfWidth = width;
                drop = reveal;
                halfFeather = feather;
                SetVerticesDirty();
            }

            protected override void OnPopulateMesh(VertexHelper mesh)
            {
                mesh.Clear();
                DrawRollOutline(mesh, -halfWidth);
                DrawRollOutline(mesh, halfWidth);
                DrawPage(mesh);
                DrawRoll(mesh, -halfWidth);
                DrawRoll(mesh, halfWidth);
                if (halfWidth < 91f) DrawOpeningSeal(mesh);
            }

            float Top(float x)
            {
                // One shallow bow meets both rollers below their gold collars.
                float u = x / halfWidth;
                return RollTop - 2f - halfWidth * .017f * (1f - u * u);
            }

            float Bottom(float x)
            {
                // The folded lower leaf falls free between the two spindles.
                float hanging = Mathf.SmoothStep(0f, 1f, (halfWidth - Mathf.Abs(x)) / 58f);
                return RollBottom + 1f - 78f * drop * hanging;
            }

            void DrawPage(VertexHelper mesh)
            {
                int strips = Mathf.Max(8, Mathf.CeilToInt(2f * halfWidth / 6f));
                Color32 clearInk = Faded(OutlineInk, 0f);
                float curlWidth = Mathf.Min(33f, halfWidth);
                var curlTint = new Color32(181, 133, 77, 60);
                for (int i = 0; i < strips; i++)
                {
                    float l = Mathf.Lerp(-halfWidth, halfWidth, (float)i / strips);
                    float r = Mathf.Lerp(-halfWidth, halfWidth, (float)(i + 1) / strips);
                    float tl = Top(l), tr = Top(r), bl = Bottom(l), br = Bottom(r);

                    // Both edges of the ink have a pixel-wide transition, including where it meets the paper.
                    float nl = LowerNormalScale(l), nr = LowerNormalScale(r);
                    float e = halfFeather;
                    float outerL = bl - OutlineWidth * nl, outerR = br - OutlineWidth * nr;
                    float el = e * nl, er = e * nr;
                    Band(mesh, l, r, outerL - el, outerR - er, outerL + el, outerR + er, clearInk, OutlineInk);
                    Band(mesh, l, r, outerL + el, outerR + er, bl - el, br - er, OutlineInk, OutlineInk);
                    Band(mesh, l, r, bl - el, br - er, bl + el, br + er, OutlineInk, Fold);
                    Band(mesh, l, r, bl + el, br + er, bl + 5f, br + 5f, Fold, Paper);
                    Band(mesh, l, r, bl + 5f, br + 5f, tl - e, tr - e, Paper, Cream);
                    Band(mesh, l, r, tl - e, tr - e, tl + e, tr + e, Cream, OutlineInk);
                    Band(mesh, l, r, tl + e, tr + e, tl + OutlineWidth - e, tr + OutlineWidth - e, OutlineInk, OutlineInk);
                    Band(mesh, l, r, tl + OutlineWidth - e, tr + OutlineWidth - e, tl + OutlineWidth + e, tr + OutlineWidth + e, OutlineInk, clearInk);

                    // Follow the fold with each strip so its shadow cannot spill outside the ink.
                    float leftCurl = Mathf.Clamp01(1f - (halfWidth - Mathf.Abs(l)) / curlWidth);
                    float rightCurl = Mathf.Clamp01(1f - (halfWidth - Mathf.Abs(r)) / curlWidth);
                    if (leftCurl > 0f || rightCurl > 0f)
                    {
                        Color32 left = Faded(curlTint, leftCurl), right = Faded(curlTint, rightCurl);
                        Quad(mesh, new Vector2(l, bl + el), new Vector2(r, br + er),
                            new Vector2(r, tr - e), new Vector2(l, tl - e), left, right, right, left);
                    }
                }

                if (halfWidth > 75f)
                {
                    DrawCorner(mesh, -1);
                    DrawCorner(mesh, 1);
                }
            }

            float LowerNormalScale(float x)
            {
                float slope = (Bottom(x + 1f) - Bottom(x - 1f)) * .5f;
                return Mathf.Sqrt(1f + slope * slope);
            }

            void DrawCorner(VertexHelper mesh, int side)
            {
                float x = side * (halfWidth - 39f);
                float y = Top(x) - 18f;
                var ink = Faded(Rule, Mathf.InverseLerp(75f, 115f, halfWidth));
                for (int i = 0; i < 3; i++)
                {
                    float cx = x - side * i * 13f, cy = y + i * 1.8f;
                    float rx = 7f - i * 1.8f, ry = rx * .54f;
                    Quad(mesh, new Vector2(cx - rx, cy), new Vector2(cx, cy - ry),
                        new Vector2(cx + rx, cy), new Vector2(cx, cy + ry), ink, ink, ink, ink);
                }
            }

            void DrawOpeningSeal(VertexHelper mesh)
            {
                float release = Mathf.InverseLerp(16f, 91f, halfWidth);
                float free = Mathf.SmoothStep(0f, 1f, release);
                float opacity = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.20f, .96f, release));
                for (int side = -1; side <= 1; side += 2)
                {
                    float outer = side * (halfWidth + 14f);
                    float end = side * (halfWidth - 3f) * free;
                    float bend = Mathf.Lerp(end, outer, .6f);
                    // The severed lengths stay wound on their own rolls and slacken at their free ends.
                    for (int wrap = -1; wrap <= 1; wrap += 2)
                    {
                        float outerY = 9f + wrap * 9f + side * 4f;
                        float endY = 9f + wrap * 3.4f - free * 19f;
                        float middleY = Mathf.Lerp(endY, outerY, .6f) - free * 5f;
                        DrawCord(mesh, end, endY, bend, middleY, opacity);
                        DrawCord(mesh, bend, middleY, outer, outerY, opacity);
                    }
                }

                // The seal lifts away from the knot before the first event tiles pass the pointer.
                float y = 8f + 39f * free;
                float radius = 26f * (1f + .20f * Mathf.Sin(free * Mathf.PI) - .45f * free);
                Ellipse(mesh, 0f, y - 2.8f, radius + 1f, radius * .95f, Faded(WaxEdge, opacity), true);
                Ellipse(mesh, 0f, y, radius, radius * .94f, Faded(new Color32(216, 64, 65, 255), opacity), true);
                Ellipse(mesh, 0f, y + 1f, radius * .79f, radius * .73f, Faded(new Color32(251, 127, 111, 255), opacity));
                Ellipse(mesh, 0f, y + .4f, radius * .72f, radius * .66f, Faded(Wax, opacity));
                DrawCrownStamp(mesh, y, radius, Faded(Gilt, opacity));
            }

            static void DrawCord(VertexHelper mesh, float x0, float y0, float x1, float y1, float opacity)
            {
                if (x0 > x1) { (x0, x1) = (x1, x0); (y0, y1) = (y1, y0); }
                var edge = Faded(WaxEdge, opacity);
                Band(mesh, x0, x1, y0 - 3.4f, y1 - 3.4f, y0 + 3.4f, y1 + 3.4f, edge, edge);
                Band(mesh, x0, x1, y0 - 1.4f, y1 - 1.4f, y0 + 2.4f, y1 + 2.4f,
                    Faded(Wax, opacity), Faded(new Color32(253, 133, 102, 255), opacity));
            }

            static void DrawCrownStamp(VertexHelper mesh, float y, float radius, Color32 foil)
            {
                float w = .49f * radius;
                var a = new Vector2(-w * .8f, y - w * .39f);
                var b = new Vector2(w * .8f, y - w * .39f);
                Triangle(mesh, a, new Vector2(-w, y + w * .48f), new Vector2(-w * .35f, y + w * .05f), foil);
                Triangle(mesh, a, new Vector2(0f, y + w * .86f), b, foil);
                Triangle(mesh, new Vector2(w * .35f, y + w * .05f), new Vector2(w, y + w * .48f), b, foil);
                Band(mesh, a.x, b.x, y - w * .65f, y - w * .65f, y - w * .48f, y - w * .48f, foil, foil);
            }

            static Color32 Faded(Color32 color, float opacity)
            {
                color.a = (byte)Mathf.RoundToInt(color.a * opacity);
                return color;
            }

            void DrawRollOutline(VertexHelper mesh, float x)
            {
                // Lay down one joined silhouette before drawing the sheet, rollers and their hardware.
                DrawShaft(mesh, x, 14f + OutlineWidth, OutlineInk, OutlineInk);
                for (int dir = -1; dir <= 1; dir += 2)
                {
                    float y = dir < 0 ? RollBottom : RollTop;
                    SoftEllipse(mesh, x, y + dir * 10f - 1f, 9f + OutlineWidth, 8.1f + OutlineWidth, OutlineInk);
                    SoftEllipse(mesh, x, y + dir * 2.5f - 1f, 16.3f + OutlineWidth, 7.1f + OutlineWidth, OutlineInk);
                }
            }

            void DrawShaft(VertexHelper mesh, float x, float radius, Color32 bottom, Color32 top)
            {
                float l = x - radius, r = x + radius, e = halfFeather;
                Color32 clearBottom = Faded(bottom, 0f), clearTop = Faded(top, 0f);
                Quad(mesh, new Vector2(l - e, RollBottom), new Vector2(l + e, RollBottom),
                    new Vector2(l + e, RollTop), new Vector2(l - e, RollTop), clearBottom, bottom, top, clearTop);
                Band(mesh, l + e, r - e, RollBottom, RollBottom, RollTop, RollTop, bottom, top);
                Quad(mesh, new Vector2(r - e, RollBottom), new Vector2(r + e, RollBottom),
                    new Vector2(r + e, RollTop), new Vector2(r - e, RollTop), bottom, clearBottom, clearTop, top);
            }

            void DrawRoll(VertexHelper mesh, float x)
            {
                // Fixed radius. The left and right cylinders translate as the sheet unfurls.
                const float low = RollBottom, high = RollTop;
                DrawShaft(mesh, x, 14f, Tea, Fold);
                RodStripe(mesh, x - 12f, x - 8f, low, high, Tea, Paper);
                RodStripe(mesh, x - 8f, x - 3f, low, high, Paper, Cream);
                RodStripe(mesh, x - 3f, x + .5f, low, high, Cream, new Color32(255, 249, 222, 255));
                RodStripe(mesh, x + .5f, x + 8f, low, high, Cream, new Color32(246, 202, 132, 255));
                RodStripe(mesh, x + 8f, x + 12f, low, high, new Color32(246, 202, 132, 255), Tea);

                // A pair of curved paper seams mark the outer winding.
                for (int k = 0; k < 2; k++)
                {
                    float seam = k == 0 ? low + 9f : high - 11f;
                    Band(mesh, x - 12f, x, seam + 1.5f, seam - 2f, seam + 3f, seam - .4f, Rule, Rule);
                    Band(mesh, x, x + 12f, seam - 2f, seam + .5f, seam - .4f, seam + 2f, Rule, Rule);
                }

                DrawFinial(mesh, x, high, 1f);
                DrawFinial(mesh, x, low, -1f);
            }

            void DrawFinial(VertexHelper mesh, float x, float y, float dir)
            {
                SoftEllipse(mesh, x, y + dir * 10f - 1f, 9f, 8.1f, GoldShade);
                SoftEllipse(mesh, x, y + dir * 10f, 7.6f, 6.4f, Gold);
                SoftEllipse(mesh, x - 1.8f, y + dir * 10f + 2f, 4.2f, 2.8f, Gilt);
                SoftEllipse(mesh, x, y + dir * 2.5f - 1f, 16.3f, 7.1f, GoldShade);
                SoftEllipse(mesh, x, y + dir * 2.5f + .2f, 14.7f, 5.3f, Gold);
                SoftEllipse(mesh, x - 1f, y + dir * 2.5f + 1.8f, 11.7f, 2.6f, Gilt);
            }

            void SoftEllipse(VertexHelper mesh, float x, float y, float rx, float ry, Color32 color)
            {
                int center = mesh.currentVertCount;
                mesh.AddVert(new Vector2(x, y), color, Vector2.zero);
                Color32 clear = Faded(color, 0f);
                for (int i = 0; i < CapCircle.Length; i++)
                {
                    Vector2 direction = CapCircle[i];
                    var edge = new Vector2(x + rx * direction.x, y + ry * direction.y);
                    Vector2 offset = new Vector2(ry * direction.x, rx * direction.y).normalized * halfFeather;
                    mesh.AddVert(edge - offset, color, Vector2.zero);
                    mesh.AddVert(edge + offset, clear, Vector2.zero);
                    int inner = center + 1 + i * 2;
                    int next = center + 1 + ((i + 1) % CapCircle.Length) * 2;
                    mesh.AddTriangle(center, next, inner);
                    mesh.AddTriangle(inner, next, next + 1);
                    mesh.AddTriangle(inner, next + 1, inner + 1);
                }
            }

            static void RodStripe(VertexHelper mesh, float l, float r, float low, float high, Color32 left, Color32 right)
            {
                Quad(mesh, new Vector2(l, low), new Vector2(r, low), new Vector2(r, high), new Vector2(l, high),
                    left, right, right, left);
            }

            static void Band(VertexHelper mesh, float l, float r, float bl, float br, float tl, float tr, Color32 bottom, Color32 top)
            {
                Quad(mesh, new Vector2(l, bl), new Vector2(r, br), new Vector2(r, tr), new Vector2(l, tl),
                    bottom, bottom, top, top);
            }

            static void Quad(VertexHelper mesh, Vector2 a, Vector2 b, Vector2 c, Vector2 d,
                Color32 ca, Color32 cb, Color32 cc, Color32 cd)
            {
                int start = mesh.currentVertCount;
                mesh.AddVert(a, ca, Vector2.zero);
                mesh.AddVert(b, cb, Vector2.zero);
                mesh.AddVert(c, cc, Vector2.zero);
                mesh.AddVert(d, cd, Vector2.zero);
                mesh.AddTriangle(start, start + 2, start + 1);
                mesh.AddTriangle(start, start + 3, start + 2);
            }

            static void Triangle(VertexHelper mesh, Vector2 a, Vector2 b, Vector2 c, Color32 color)
            {
                int start = mesh.currentVertCount;
                mesh.AddVert(a, color, Vector2.zero);
                mesh.AddVert(b, color, Vector2.zero);
                mesh.AddVert(c, color, Vector2.zero);
                mesh.AddTriangle(start, start + 1, start + 2);
            }

            static void Ellipse(VertexHelper mesh, float x, float y, float rx, float ry, Color32 color, bool waxEdge = false)
            {
                int edges = waxEdge ? 48 : 24;
                int start = mesh.currentVertCount;
                mesh.AddVert(new Vector2(x, y), color, Vector2.zero);
                for (int i = 0; i < edges; i++)
                {
                    float angle = 2f * Mathf.PI * i / edges;
                    float radius = waxEdge ? 1f + .046f * Mathf.Sin(7f * angle) + .028f * Mathf.Cos(3f * angle) : 1f;
                    mesh.AddVert(new Vector2(x + rx * radius * Mathf.Cos(angle), y + ry * radius * Mathf.Sin(angle)), color, Vector2.zero);
                    mesh.AddTriangle(start, start + 1 + ((i + 1) % edges), start + 1 + i);
                }
            }
        }
    }
}
