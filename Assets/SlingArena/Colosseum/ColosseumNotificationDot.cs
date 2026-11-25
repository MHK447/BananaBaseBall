using UnityEngine;

namespace SlingArena.Colosseum
{
    internal static class ColosseumNotificationDot
    {
        static GameObject prefab;

        internal static GameObject Create(Transform parent, Vector2 anchor, Vector2 offset, float diameter)
        {
            if (prefab == null) prefab = Resources.Load<GameObject>("ColosseumPrefabs/NotificationDot");
            var dot = Object.Instantiate(prefab, parent, false);
            dot.name = "Notification dot";
            var rect = (RectTransform)dot.transform;
            rect.anchorMin = rect.anchorMax = anchor;
            rect.anchoredPosition = offset;
            rect.sizeDelta = Vector2.one * diameter;
            return dot;
        }
    }
}
