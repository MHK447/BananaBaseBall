using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace SlingArena.Colosseum
{
    [DisallowMultipleComponent, RequireComponent(typeof(RectTransform))]
    public sealed class ColosseumPreloadPoster : MonoBehaviour
    {
        RectTransform caption;
        TextMeshProUGUI loading;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void ShowDuringPreload()
        {
            if (SceneManager.GetActiveScene().path != ColosseumPreloadTransition.SdkScenePath)
                return;

            // This canvas belongs to the game. Required SDK dialogs render above it and keep their input.
            var root = new GameObject("Smash Rumble loading", typeof(Canvas), typeof(CanvasScaler));
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(720f, 1280f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            var assets = Resources.Load<ColosseumAssets>("ColosseumAssets");
            if (!Show((RectTransform)root.transform, assets ? assets.brawlFont : null))
                Destroy(root);
        }

        internal static bool Show(RectTransform parent, TMP_FontAsset font)
        {
            if (!ColosseumTitleArtwork.Show(parent)) return false;

            var footer = new GameObject("Loading contrast", typeof(RectTransform), typeof(ColosseumLeagueSky));
            var gradient = footer.GetComponent<ColosseumLeagueSky>();
            gradient.rectTransform.SetParent(parent, false);
            gradient.rectTransform.anchorMin = Vector2.zero;
            gradient.rectTransform.anchorMax = new Vector2(1f, .2f);
            gradient.rectTransform.offsetMin = gradient.rectTransform.offsetMax = Vector2.zero;
            gradient.SetGradient(new Color32(29, 19, 36, 0), new Color32(29, 19, 36, 195));
            gradient.raycastTarget = false;

            var view = parent.gameObject.AddComponent<ColosseumPreloadPoster>();
            var label = new GameObject("Loading", typeof(RectTransform), typeof(TextMeshProUGUI));
            view.loading = label.GetComponent<TextMeshProUGUI>();
            view.caption = view.loading.rectTransform;
            view.caption.SetParent(parent, false);
            if (font) view.loading.font = font;
            view.loading.text = "LOADING...";
            view.loading.color = new Color32(255, 248, 228, 255);
            view.loading.alignment = TextAlignmentOptions.Center;
            view.loading.fontSize = 29f;
            view.loading.characterSpacing = 4f;
            view.loading.raycastTarget = false;
            view.Update();
            return true;
        }

        void Update()
        {
            Rect safe = Screen.safeArea;
            int w = Screen.width, h = Screen.height;
            if (!loading || w <= 0 || h <= 0) return;
            if (safe.width <= 0f || safe.height <= 0f) safe = new Rect(0f, 0f, w, h);
            float bottom = safe.yMin / h;
            caption.anchorMin = new Vector2(safe.xMin / w, bottom);
            caption.anchorMax = new Vector2(safe.xMax / w, bottom);
            caption.offsetMin = new Vector2(0f, 24f);
            caption.offsetMax = new Vector2(0f, 76f);
            loading.maxVisibleCharacters = 7 + (Mathf.FloorToInt(Time.unscaledTime * 3f) % 4);
        }
    }
}
