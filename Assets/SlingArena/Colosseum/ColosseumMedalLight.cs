using UnityEngine;
using UnityEngine.UI;

namespace SlingArena.Colosseum
{
    /// <summary>Soft light and rotating shafts behind a league emblem.</summary>
    public sealed class ColosseumMedalLight : MonoBehaviour
    {
        static Sprite haloMask, rayMask;
        Image halo, rays;
        float elapsed;

        internal static (Sprite Halo, Sprite Rays) GetMasks()
        {
            if (haloMask != null && rayMask != null) return (haloMask, rayMask);
            // Two masks in one texture let the glow breathe independently of the rotating beams.
            const int size = 160;
            var atlas = Kit.Own(new Texture2D(size * 2, size, TextureFormat.RGBA32, false)
                { name = "League light", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear });
            var pixels = new Color32[atlas.width * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = (x + .5f) * 2f / size - 1f, dy = (y + .5f) * 2f / size - 1f;
                float radius = Mathf.Sqrt(dx * dx + dy * dy);
                float angle = Mathf.Atan2(dy, dx);
                float halo = Mathf.SmoothStep(1f, 0f, Mathf.InverseLerp(.30f, 1f, radius));
                float beam = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.06f, .88f, Mathf.Cos(angle * 10f)));
                float tip = .93f + .065f * Mathf.Cos(angle * 5f);
                float fade = Mathf.SmoothStep(1f, 0f, Mathf.InverseLerp(.57f, tip, radius));
                int index = y * atlas.width + x;
                pixels[index] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(255f * halo));
                pixels[index + size] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(255f * beam * fade));
            }
            atlas.SetPixels32(pixels);
            atlas.Apply(false, true);
            haloMask = Kit.Own(Sprite.Create(atlas, new Rect(0, 0, size, size), new Vector2(.5f, .5f), 100f));
            rayMask = Kit.Own(Sprite.Create(atlas, new Rect(size, 0, size, size), new Vector2(.5f, .5f), 100f));
            return (haloMask, rayMask);
        }

        internal static ColosseumMedalLight Attach(Image halo, Image rays)
        {
            var light = halo.gameObject.AddComponent<ColosseumMedalLight>();
            light.halo = halo;
            light.rays = rays;
            var masks = GetMasks();
            halo.sprite = masks.Halo;
            rays.sprite = masks.Rays;
            halo.rectTransform.sizeDelta = Vector2.one * 240f;
            rays.rectTransform.sizeDelta = Vector2.one * 274f;
            return light;
        }

        internal void SetTint(Color glow, Color beams)
        {
            glow.a = .82f;
            beams.a = .75f;
            halo.color = glow;
            rays.color = beams;
        }

        void Update()
        {
            elapsed += Time.unscaledDeltaTime;
            float pulse = Mathf.Sin(elapsed * (2f * Mathf.PI / 3.2f));
            halo.rectTransform.localScale = Vector3.one * (1f + .055f * pulse);
            rays.rectTransform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Repeat(elapsed * 9f, 360f));
        }
    }
}
