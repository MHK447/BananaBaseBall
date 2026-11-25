using UnityEngine;
using UnityEngine.UI;

namespace SlingArena.Colosseum
{
    /// <summary>Describes a UI silhouette in its own coordinates so the shader can filter both sides at screen resolution.</summary>
    [DisallowMultipleComponent]
    public sealed class ColosseumUiContour : BaseMeshEffect
    {
        static Material fillMaterial, artMaterial;
        RectTransform window;
        float radius;
        bool ellipse;
        bool grayscale;
        Matrix4x4 lastTransform;
        Rect lastWindow;

        // These two shape sprites are used by the deck, hero-card and standings prefabs.
        public static void PrefabShapes(GameObject root)
        {
            foreach (Image image in root.GetComponentsInChildren<Image>(true))
            {
                if (!image.sprite || image.TryGetComponent<ColosseumUiContour>(out _)) continue;
                if (image.sprite.name == "ui-round" && image.type == Image.Type.Sliced)
                    Fill(image, 30f / (image.pixelsPerUnitMultiplier * image.pixelsPerUnit));
                else if (image.sprite.name == "ui-circle")
                    Fill(image, 0f, true);
            }
        }

        public void SetGrayscale(bool value)
        {
            if (grayscale == value) return;
            grayscale = value;
            graphic.SetVerticesDirty();
        }

        public static void Fill(Image image, float cornerRadius, bool oval = false)
        {
            var contour = Configure(image, false);
            contour.SetWindow(null);
            contour.radius = cornerRadius;
            contour.ellipse = oval;
            image.SetVerticesDirty();
        }

        public static void Artwork(Graphic art, RectTransform clipRect, float radius)
        {
            var contour = Configure(art, true);
            contour.SetWindow(clipRect);
            contour.radius = radius;
            contour.ellipse = false;
            art.SetVerticesDirty();
        }

        static ColosseumUiContour Configure(Graphic target, bool textured)
        {
            var contour = target.GetComponent<ColosseumUiContour>();
            if (!contour) contour = target.gameObject.AddComponent<ColosseumUiContour>();
            ref Material shared = ref (textured ? ref artMaterial : ref fillMaterial);
            if (!shared)
            {
                shared = Kit.Own(new Material(Resources.Load<Shader>("ColosseumUiContour")));
                shared.SetFloat("_Artwork", textured ? 1f : 0f);
            }
            target.material = shared;
            return contour;
        }

        void SetWindow(RectTransform value)
        {
            if (window == value) return;
            if (window && isActiveAndEnabled) Canvas.preWillRenderCanvases -= CheckWindow;
            window = value;
            if (window && isActiveAndEnabled) Canvas.preWillRenderCanvases += CheckWindow;
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            if (window) Canvas.preWillRenderCanvases += CheckWindow;
        }

        protected override void OnDisable()
        {
            Canvas.preWillRenderCanvases -= CheckWindow;
            base.OnDisable();
        }

        void CheckWindow()
        {
            // A portrait can move within a stationary frame without Unity rebuilding its Image mesh.
            if (!window || !graphic.isActiveAndEnabled) return;
            var relative = window.worldToLocalMatrix * transform.localToWorldMatrix;
            if (lastTransform != relative || lastWindow != window.rect) graphic.SetVerticesDirty();
        }

        public override void ModifyMesh(VertexHelper mesh)
        {
            if (!IsActive() || mesh.currentVertCount == 0) return;
            Rect rect = window ? window.rect : graphic.GetPixelAdjustedRect();
            if (rect.width <= 0f || rect.height <= 0f) return;
            Vector2 half = rect.size * .5f;
            Vector2 center = rect.center;
            float curve = Mathf.Clamp(radius, .01f, Mathf.Min(half.x, half.y));
            Vector2 unit = ellipse ? half : new Vector2(curve, curve);
            Vector2 extent = new Vector2(half.x / unit.x, half.y / unit.y);
            lastTransform = window ? window.worldToLocalMatrix * transform.localToWorldMatrix : Matrix4x4.identity;
            lastWindow = rect;
            float gutter = 2f / Mathf.Max(.01f, graphic.canvas.scaleFactor);
            UIVertex vertex = default;
            for (int i = 0; i < mesh.currentVertCount; i++)
            {
                mesh.PopulateUIVertex(ref vertex, i);
                Vector3 p = vertex.position;
                if (window)
                {
                    p = lastTransform.MultiplyPoint3x4(p);
                }
                else
                {
                    // Give the outer half of the transition actual geometry without changing layout or hit bounds.
                    if (Mathf.Approximately(p.x, rect.xMin)) p.x -= gutter;
                    else if (Mathf.Approximately(p.x, rect.xMax)) p.x += gutter;
                    if (Mathf.Approximately(p.y, rect.yMin)) p.y -= gutter;
                    else if (Mathf.Approximately(p.y, rect.yMax)) p.y += gutter;
                    vertex.position = p;
                }
                vertex.uv1 = new Vector4((p.x - center.x) / unit.x, (p.y - center.y) / unit.y, extent.x, extent.y);
                if (grayscale)
                {
                    Color32 tint = vertex.color;
                    byte grey = (byte)Mathf.RoundToInt(.299f * tint.r + .587f * tint.g + .114f * tint.b);
                    vertex.color = new Color32(grey, grey, grey, tint.a);
                }
                mesh.SetUIVertex(vertex, i);
            }
            // Prefab effects and runtime effects can be attached in either order.
            // Shear the geometry after recording the unsheared silhouette.
            if (TryGetComponent<ColosseumSlant>(out var slant) && slant.isActiveAndEnabled) slant.Apply(mesh);
        }
    }
}
