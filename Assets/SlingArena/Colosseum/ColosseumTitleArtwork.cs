using UnityEngine;
using UnityEngine.UI;

namespace SlingArena.Colosseum
{
    /// <summary>The same framing carries the arena poster from preload into the title screen.</summary>
    public sealed class ColosseumTitleArtwork : RawImage
    {
        const float MinimumVisible = .92f;
        Material edgeMaterial;

        public static bool Show(RectTransform parent)
        {
            var art = Resources.Load<Texture2D>("ColosseumTitle/ArenaArrival");
            if (!art) return false;
            var go = new GameObject("Arena arrival poster", typeof(RectTransform), typeof(CanvasRenderer));
            go.layer = parent.gameObject.layer;
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            rect.SetAsFirstSibling();
            var poster = go.AddComponent<ColosseumTitleArtwork>();
            poster.raycastTarget = false;
            poster.texture = art;
            var shader = Resources.Load<Shader>("ColosseumTitleEdge");
            if (shader && shader.isSupported)
            {
                poster.edgeMaterial = new Material(shader) { hideFlags = HideFlags.DontSave };
                poster.material = poster.edgeMaterial;
            }
            poster.FrameArt();
            return true;
        }

        protected override void OnRectTransformDimensionsChange()
        {
            base.OnRectTransformDimensionsChange();
            FrameArt();
        }

        void FrameArt()
        {
            if (!texture) return;
            Rect view = rectTransform.rect;
            if (view.width <= 0f || view.height <= 0f) return;
            float sx = view.width / texture.width, sy = view.height / texture.height;
            // On wider tablets use the space below the duel as crop room so the
            // scene can fill the whole width while the lettering remains visible.
            float wide = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(1.12f, 1.42f, sx / sy));
            float keepHeight = Mathf.Lerp(MinimumVisible, .69f, wide);
            float scale = Mathf.Min(Mathf.Max(sx, sy), Mathf.Min(sx / MinimumVisible, sy / keepHeight));
            var visible = new Vector2(sx, sy) / scale;
            var origin = (Vector2.one - visible) * .5f;
            if (visible.y < 1f) origin.y = (1f - visible.y) * Mathf.Lerp(.5f, .85f, wide);
            uvRect = new Rect(origin, visible);
        }

        protected override void OnDestroy()
        {
            if (edgeMaterial)
            {
                if (Application.isPlaying) Destroy(edgeMaterial);
                else DestroyImmediate(edgeMaterial);
            }
            base.OnDestroy();
        }
    }
}
