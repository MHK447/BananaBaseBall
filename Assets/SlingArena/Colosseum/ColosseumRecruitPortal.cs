using UnityEngine;
using UnityEngine.UI;

namespace SlingArena.Colosseum
{
    /// <summary>The night sky, stone dais and luminous rings of the hero reveal, drawn in one UI mesh.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class ColosseumRecruitPortal : MaskableGraphic
    {
        const int Segments = 64;
        static readonly Color32 Sky = new Color32(28, 20, 72, 255);
        static readonly Color32 Horizon = new Color32(54, 53, 127, 255);
        static readonly Color32 Ground = new Color32(16, 36, 76, 255);
        static readonly Color32 Aqua = new Color32(113, 255, 239, 255);
        static readonly Color32 Violet = new Color32(151, 110, 255, 255);
        static readonly Color Clear = new Color(0f, 0f, 0f, 0f);
        float clock;

        public void SetTime(float seconds)
        {
            // The glow and drifting sparks update at thirty fps, independently of the card's smooth entrance.
            if ((int)(seconds * 30f) == (int)(clock * 30f)) return;
            clock = seconds;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect rect = rectTransform.rect;
            float w = rect.width * .5f, h = rect.height * .5f;
            Strip(vh, -w, w, -h, -70f, Ground, Horizon);
            Strip(vh, -w, w, -70f, h, Horizon, Sky);
            var portal = new Vector2(0f, -179f);
            // The lantern-like violet wash behind the card fades naturally into the sky.
            Disc(vh, new Vector2(0f, 65f), new Vector2(365f, 355f), new Color32(131, 82, 211, 183), Clear);
            Disc(vh, portal + Vector2.down * 84f, new Vector2(316f, 52f), new Color32(6, 17, 55, 153), Clear);
            Disc(vh, portal + Vector2.down * 57f, new Vector2(221f, 46f), new Color32(36, 57, 102, 255), new Color32(28, 42, 85, 255));
            Disc(vh, portal + Vector2.down * 43f, new Vector2(221f, 44f), new Color32(73, 93, 149, 255), new Color32(55, 74, 129, 255));
            Disc(vh, portal + Vector2.down * 27f, new Vector2(185f, 41f), new Color32(37, 45, 100, 255), new Color32(35, 43, 99, 255));
            Disc(vh, portal + Vector2.down * 13f, new Vector2(186f, 42f), new Color32(106, 95, 178, 255), new Color32(91, 88, 176, 255));
            float flare = Mathf.Clamp01(1f - Mathf.Abs(clock - .46f) / .42f);
            Disc(vh, portal, new Vector2(233f, 67f), new Color32(94, 247, 236, 155), Clear);
            Disc(vh, portal, new Vector2(157f, 37f), new Color32(204, 255, 253, 255), new Color32(73, 135, 218, 255));
            for (int i = 0; i < 3; i++)
            {
                float phase = Mathf.Repeat(clock * .43f + i / 3f, 1f);
                float rad = Mathf.Lerp(30f, 159f, phase);
                Color ring = Color.Lerp(Violet, Aqua, phase);
                ring.a = Mathf.Sin(Mathf.PI * phase);
                Ring(vh, portal, rad, 3f + 3f * flare, ring);
            }
            // Ascending motes spiral outwards from the opening, leaving a quiet center behind the portrait.
            for (int i = 0; i < 24; i++)
            {
                float t = Mathf.Repeat(i * .381966f + clock * (.19f + .015f * (i % 3)), 1f);
                float angle = i * 2.39996f + clock * .61f + 2f * t;
                float span = Mathf.Lerp(85f, 270f, t);
                var point = portal + new Vector2(Mathf.Cos(angle) * span, t * 515f + 16f * Mathf.Sin(angle));
                float visibility = Mathf.Sin(t * Mathf.PI);
                float size = (i % 3 == 0 ? 7f : 3.5f) * visibility;
                Color mote = i % 3 == 0 ? (Color)Aqua : Color.white;
                mote.a = visibility * .8f;
                Quad(vh, point + Vector2.left * size * .7f, point + Vector2.up * size, point + Vector2.right * size * .7f,
                    point + Vector2.down * size, mote, mote);
            }
        }

        static void Strip(VertexHelper vh, float left, float right, float bottom, float top, Color low, Color high)
        {
            Quad(vh, new Vector2(left, bottom), new Vector2(left, top), new Vector2(right, top), new Vector2(right, bottom), low, high);
        }

        static void Quad(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color low, Color high)
        {
            int first = vh.currentVertCount;
            vh.AddVert(a, low, Vector2.zero); vh.AddVert(b, high, Vector2.zero);
            vh.AddVert(c, high, Vector2.zero); vh.AddVert(d, low, Vector2.zero);
            vh.AddTriangle(first, first + 1, first + 2); vh.AddTriangle(first + 2, first + 3, first);
        }

        static void Disc(VertexHelper vh, Vector2 center, Vector2 radius, Color core, Color edge)
        {
            int first = vh.currentVertCount;
            vh.AddVert(center, core, Vector2.zero);
            for (int i = 0; i < Segments; i++)
            {
                float angle = 2f * Mathf.PI * i / Segments;
                vh.AddVert(center + new Vector2(radius.x * Mathf.Cos(angle), radius.y * Mathf.Sin(angle)), edge, Vector2.zero);
                vh.AddTriangle(first, first + 1 + (i + 1) % Segments, first + 1 + i);
            }
        }

        static void Ring(VertexHelper vh, Vector2 center, float radius, float width, Color color)
        {
            int first = vh.currentVertCount;
            for (int i = 0; i < Segments; i++)
            {
                float angle = 2f * Mathf.PI * i / Segments;
                var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle) * .23f);
                vh.AddVert(center + direction * (radius - width), color, Vector2.zero);
                vh.AddVert(center + direction * radius, color, Vector2.zero);
                int a = first + i * 2, b = first + (i + 1) % Segments * 2;
                vh.AddTriangle(a, b + 1, a + 1); vh.AddTriangle(a, b, b + 1);
            }
        }
    }
}
