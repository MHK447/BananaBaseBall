using UnityEngine;
using UnityEngine.UI;

namespace SlingArena.Colosseum
{
    /// <summary>The reward chest painting, hinged along its back rail. All pieces share one UI draw.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class ColosseumChestProp : MaskableGraphic
    {
        static Material lacquer;
        Texture artwork;
        float opening, unit, lidSin, lidCos = 1f;
        Vector2 centre;
        const float LidAngle = 76f, ChestDepth = 68f, DomeHeight = 49f;
        const float ChestFront = 123.1f, SideRun = 40f, SideRise = 21f;
        const int DomeRows = 20;
        static readonly float ArchRadius = Mathf.Sqrt(DomeHeight * DomeHeight + SideRise * SideRise * .25f);
        static readonly float ArchOffset = Mathf.Atan2(DomeHeight, SideRise * .5f);
        // The outer ends of the visible lip lie just inside the painting's transparent margin.
        const float MouthLeft = 44.8f, MouthRight = 210f;
        static readonly float[] Cuts = { 32, 41, MouthLeft, 45.7f, 49, 54, 62, 76, 88, 92, 97, 107, 118, 123, 127, 144, 162, 168, 175, 182, 189, 196, 202, 208, MouthRight, 215, 224 };

        static Vector2 P(float x, float y) => new Vector2(x, y);
        public override Texture mainTexture => artwork != null ? artwork : Texture2D.whiteTexture;

        public static ColosseumChestProp Create(Transform parent)
        {
            var item = new GameObject("Hinged arena chest", typeof(RectTransform), typeof(CanvasRenderer));
            var rect = (RectTransform)item.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.sizeDelta = new Vector2(300f, 240f);
            var prop = item.AddComponent<ColosseumChestProp>();
            prop.artwork = Resources.Load<Texture2D>("ColosseumUI/reward-chest-shell");
            if (!lacquer)
            {
                Shader shader = Resources.Load<Shader>("ColosseumChestLacquer");
                if (shader && shader.isSupported) lacquer = Kit.Own(new Material(shader));
            }
            if (lacquer) prop.material = lacquer;
            prop.raycastTarget = false;
            return prop;
        }

        public void SetOpening(float opening)
        {
            opening = Mathf.Clamp01(opening);
            if (this.opening == opening) return;
            this.opening = opening;
            float angle = opening * (LidAngle * Mathf.Deg2Rad);
            lidSin = Mathf.Sin(angle);
            lidCos = Mathf.Cos(angle);
            SetVerticesDirty();
        }

        // Follow the painted upper rail, including the raised centre clasp, in source pixels.
        static float Seam(float x)
        {
            float clasp = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(89f, 98f, x)) *
                (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(118f, 126f, x)));
            return 123.1f - 3.6f * clasp - Mathf.Max(0f, x - 168f) * .50f - Mathf.Max(0f, 61f - x) * .32f;
        }

        // Recover the visible skin of the barrel from its painted orthographic projection.
        // A horizontal scanline hits the curved front first, except on the flat right end cap.
        static float LidDepth(Vector2 source)
        {
            float archY = (source.y - ChestFront + .5f * SideRise) / ArchRadius;
            float phi = Mathf.Max(0f, Mathf.Acos(Mathf.Clamp(archY, -1f, 1f)) - ArchOffset);
            float curved = .5f * (1f - Mathf.Cos(phi));
            float cap = Mathf.Clamp01((source.x - 168f) / SideRun);
            return Mathf.Max(curved, cap);
        }

        Vector2 RotateAboutRail(Vector2 paint, float depthFraction)
        {
            float forward = ChestDepth * (1f - depthFraction);
            float height = ChestFront - SideRise * depthFraction - paint.y;
            float back = forward * (1f - lidCos) + height * lidSin;
            float up = forward * lidSin - height * (1f - lidCos);
            return paint + P(back * (SideRun / ChestDepth), -up - back * (SideRise / ChestDepth));
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            Rect rect = rectTransform.rect;
            unit = Mathf.Min(rect.width / 300f, rect.height / 240f) * 1.64f;
            centre = rect.center;
            if (unit <= 0f) return;

            if (opening > 0f) DrawOpeningLight(mesh, false);
            float vault = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.32f, .55f, opening));
            if (vault > 0f) DrawVault(mesh, vault);
            if (vault < 1f) DrawShell(mesh, true, 1f - vault);
            // The underside becomes visible when its near edge passes the rear rail in projection.
            if (ChestDepth * lidSin > SideRise * lidCos) DrawOpeningLight(mesh, true);
            DrawShell(mesh, false);
        }

        void DrawShell(VertexHelper mesh, bool lid, float opacity = 1f)
        {
            // Only the animated dome needs depth subdivisions. Body and closed stamps use one row.
            int rows = lid && opening > 0f ? DomeRows : 1;
            int start = mesh.currentVertCount;
            Color tint = color;
            tint.a *= opacity;
            for (int i = 0; i < Cuts.Length; i++)
            {
                float x = Cuts[i], seam = Seam(x);
                for (int row = 0; row <= rows; row++)
                {
                    float y = Mathf.Lerp(lid ? 48f : seam, lid ? seam : 211f, (float)row / rows);
                    Vector2 source = P(x, y);
                    Vector2 p = lid && opening > 0f ? RotateAboutRail(source, LidDepth(source)) : source;
                    mesh.AddVert(Position(p), tint, P(x / 256f, 1f - y / 256f));
                }
                if (i == 0) continue;
                for (int row = 0; row < rows; row++)
                {
                    int a = start + (i - 1) * (rows + 1) + row, b = a + rows + 1;
                    mesh.AddTriangle(a, b, b + 1);
                    mesh.AddTriangle(a, b + 1, a + 1);
                }
            }
        }

        const int ArchSlices = 32, AcrossLid = 14;
        const float VaultHeight = 45f;

        // A wide swing exposes the back of the barrel, which is absent from a closed painting.
        // Give it a complete curved roof and a rounded right end, sharing the same hinge and rim.
        void DrawVault(VertexHelper mesh, float opacity)
        {
            Color tint = color;
            tint.a *= opacity;
            int start = mesh.currentVertCount;
            for (int j = 0; j <= ArchSlices; j++)
            {
                float phi = Mathf.PI * (1f - (float)j / ArchSlices);
                float depth = .5f - .5f * Mathf.Cos(phi), arch = Mathf.Sin(phi);
                // The straps wrap the barrel. Sample the visible half of the original lacquer
                // on the freshly exposed half, keeping the clasp exclusively on the box.
                float sampleDepth = Mathf.Min(depth, 1f - depth);
                for (int i = 0; i <= AcrossLid; i++)
                {
                    float width = (float)i / AcrossLid;
                    Vector2 pose = VaultPoint(width, depth, VaultHeight * arch);
                    float x = Mathf.Lerp(55.5f, 167f, width) + 39f * sampleDepth;
                    float y = Mathf.Min(ChestFront - SideRise * sampleDepth - 42f * arch, Seam(x) - 5.5f);
                    float minY = 73f + 40f * Mathf.Pow(Mathf.Clamp01((82f - x) / 29f), 1.65f)
                        + 30f * Mathf.Pow(Mathf.Clamp01((x - 181f) / 28f), 1.5f);
                    mesh.AddVert(Position(pose), tint, P(x / 256f, 1f - Mathf.Max(y, minY) / 256f));
                }
                if (j == 0) continue;
                for (int i = 0; i < AcrossLid; i++)
                {
                    int a = start + (j - 1) * (AcrossLid + 1) + i, b = a + AcrossLid + 1;
                    mesh.AddTriangle(a, a + 1, b);
                    mesh.AddTriangle(b, a + 1, b + 1);
                }
            }

            // The end is a half-disc. Its highlight and rolled edge continue the emerald enamel.
            int centre = mesh.currentVertCount;
            AddEndPoint(mesh, .50f, .36f * VaultHeight, new Color32(34, 183, 153, 255), opacity);
            for (int ring = 0; ring < 3; ring++)
            {
                float q = ring == 0 ? .65f : ring == 1 ? .964f : 1f;
                for (int i = 0; i <= ArchSlices; i++)
                {
                    float phi = i * Mathf.PI / ArchSlices;
                    float depth = Mathf.Lerp(.5f, .5f - .5f * Mathf.Cos(phi), q);
                    float h = Mathf.Lerp(.36f, Mathf.Sin(phi), q);
                    Color c = Color.Lerp(new Color32(11, 105, 96, 255), new Color32(69, 211, 173, 255), .25f + .75f * h);
                    if (ring == 2) c = Color.Lerp(c, new Color32(14, 71, 71, 255), .75f);
                    AddEndPoint(mesh, depth, VaultHeight * h, c, opacity);
                }
                int first = centre + 1 + ring * (ArchSlices + 1);
                for (int i = 0; i <= ArchSlices; i++)
                {
                    int a = first + i, b = first + (i + 1) % (ArchSlices + 1);
                    if (ring == 0) mesh.AddTriangle(centre, a, b);
                    else
                    {
                        mesh.AddTriangle(a - ArchSlices - 1, a, b);
                        mesh.AddTriangle(a - ArchSlices - 1, b, b - ArchSlices - 1);
                    }
                }
            }
        }

        Vector2 VaultPoint(float width, float depth, float height)
        {
            Vector2 p = P(Mathf.Lerp(47.4f, 167.6f, width) + SideRun * depth, ChestFront - SideRise * depth - height);
            return RotateAboutRail(p, depth);
        }

        void AddEndPoint(VertexHelper mesh, float depth, float height, Color tint, float opacity)
        {
            tint.a *= opacity;
            Solid(mesh, VaultPoint(1f, depth, height), tint);
        }

        // Both luminous surfaces follow the rounded lip in depth. The fixed mouth is the source;
        // the gilded lining rotates with the lid and reflects that light more softly.
        void DrawOpeningLight(VertexHelper mesh, bool insideLid)
        {
            const int corners = 4, arcSteps = 6, ringPoints = corners * (arcSteps + 1);
            int center = mesh.currentVertCount;
            AddLightPoint(mesh, P(.5f, .5f), 0, insideLid);
            for (int ring = 0; ring < LightRings.Length; ring++)
            {
                float radius = LightRings[ring];
                for (int corner = 0; corner < corners; corner++)
                {
                    float cx = corner == 0 || corner == 3 ? .95f : .05f;
                    float cy = corner < 2 ? .90f : .10f;
                    for (int j = 0; j <= arcSteps; j++)
                    {
                        float a = (corner + (float)j / arcSteps) * .5f * Mathf.PI;
                        Vector2 edge = P(cx + .05f * Mathf.Cos(a), cy + .10f * Mathf.Sin(a));
                        AddLightPoint(mesh, Vector2.LerpUnclamped(P(.5f, .5f), edge, radius), ring + 1, insideLid);
                    }
                }
                int first = center + 1 + ring * ringPoints;
                for (int j = 0; j < ringPoints; j++)
                {
                    int a = first + j, b = first + (j + 1) % ringPoints;
                    if (ring == 0) mesh.AddTriangle(center, a, b);
                    else
                    {
                        mesh.AddTriangle(a - ringPoints, a, b);
                        mesh.AddTriangle(a - ringPoints, b, b - ringPoints);
                    }
                }
            }
        }

        static readonly float[] LightRings = { .32f, .72f, .89f, .95f, 1f };
        static readonly Color32[] MouthColors = {
            new Color32(255, 255, 250, 255), new Color32(255, 254, 223, 255),
            new Color32(255, 234, 145, 255), new Color32(255, 192, 71, 255),
            new Color32(255, 247, 191, 255), new Color32(176, 107, 48, 255)
        };
        static readonly Color32[] LiningColors = {
            new Color32(255, 224, 146, 255), new Color32(255, 218, 130, 255),
            new Color32(244, 186, 87, 255), new Color32(215, 149, 62, 255),
            new Color32(255, 231, 161, 255), new Color32(143, 91, 49, 255)
        };

        void AddLightPoint(VertexHelper mesh, Vector2 uv, int band, bool insideLid)
        {
            float depth = uv.y;
            Vector2 p = P(Mathf.Lerp(47.4f, 167.6f, uv.x) + SideRun * depth, ChestFront - SideRise * depth);
            if (insideLid) p = RotateAboutRail(p, depth);
            Color tint = insideLid ? LiningColors[band] : MouthColors[band];
            Solid(mesh, p, tint);
        }

        Vector2 Position(Vector2 point)
        {
            return centre + P(point.x - 128f, 129f - point.y) * unit;
        }

        void Solid(VertexHelper mesh, Vector2 point, Color tint)
        {
            // The standard UI material can reuse a pale highlight texel if custom shaders are unavailable.
            Vector2 solidUV = lacquer ? new Vector2(-1f, 0f) : new Vector2(.283f, .676f);
            mesh.AddVert(Position(point), tint * color, solidUV);
        }
    }
}
