using UnityEngine;
using UnityEngine.UI;

namespace SlingArena.Colosseum
{
    /// <summary>Two banks of tapered foil strokes rush into the card seams and rise on the chime.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class ColosseumUpgradeStreaks : MaskableGraphic
    {
        static readonly Vector2[] Lanes = {
            new Vector2(-163f, -123f), new Vector2(-197f, -103f), new Vector2(-151f, 15f),
            new Vector2(-162f, 104f), new Vector2(168f, -68f), new Vector2(157f, 63f),
            new Vector2(195f, 96f), new Vector2(146f, 153f)
        };
        static readonly Vector2 Direction = new Vector2(.80f, .60f);
        static readonly Vector2 Across = new Vector2(-.60f, .80f);
        static readonly Color Cool = new Color32(108, 205, 247, 255);
        static readonly Color Warm = new Color32(255, 190, 75, 255);
        static readonly Color Pearl = new Color32(255, 250, 198, 255);

        float charge, releaseAge;
        bool released;

        public void SetPhase(float gather, float afterChime, bool isReleased)
        {
            charge = gather;
            releaseAge = afterChime;
            released = isReleased;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            for (int i = 0; i < Lanes.Length; i++)
            {
                float t = Mathf.SmoothStep(0f, 1f, released
                    ? Mathf.InverseLerp(-.015f * (i % 3), .39f + .027f * (i % 4), releaseAge)
                    : Mathf.InverseLerp(.03f * (i % 4), .88f + .025f * (i % 4), charge));
                float light = released ? 1f - t : t * .62f;
                if (light <= .001f) continue;
                float side = Mathf.Sign(Lanes[i].x);
                Vector2 origin = Lanes[i] + (released ? Direction * (t * 160f)
                    : new Vector2(side * 37f, -65f) * (1f - t));
                float length = (91f + i * 43 % 83) * (released ? 1.48f - .47f * t : .68f + .32f * t);
                float width = (i % 3 == 0 ? 8.5f : 4.5f) * (released ? 1.18f : 1f);
                Color tint = Color.Lerp(Cool, Warm, released ? 1f : charge * .63f);
                // A translucent broad facet, its lit bevel, and a fine offset hairline.
                Stroke(mesh, origin, length, width * 2.3f, tint, light * .17f);
                Stroke(mesh, origin, length, width, tint, light * .79f);
                Stroke(mesh, origin + Across * width * .38f, length * .78f, width * .26f,
                    Color.Lerp(tint, Pearl, .73f), light);
                if (i % 3 == 0)
                    Stroke(mesh, origin - Across * width * 2.5f + Direction * 14f, length * .50f,
                        1.6f, tint, light * .79f);
            }
        }

        static void Stroke(VertexHelper mesh, Vector2 origin, float length, float width, Color tint, float opacity)
        {
            int first = mesh.currentVertCount;
            Vector2 tail = origin - Direction * length * .55f, tip = origin + Direction * length * .62f;
            Color fade = tint;
            fade.a = 0f;
            tint.a = opacity;
            mesh.AddVert(tail, fade, Vector2.zero);
            mesh.AddVert(origin + Across * width, tint, Vector2.zero);
            mesh.AddVert(tip, fade, Vector2.zero);
            mesh.AddVert(origin + Direction * length * .17f - Across * width * .7f, tint, Vector2.zero);
            mesh.AddTriangle(first, first + 1, first + 3);
            mesh.AddTriangle(first + 1, first + 2, first + 3);
        }
    }
}
