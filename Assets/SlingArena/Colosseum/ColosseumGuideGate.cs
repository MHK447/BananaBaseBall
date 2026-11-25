using UnityEngine;

namespace SlingArena.Colosseum
{
    /// <summary>
    /// A clear sheet over the whole screen that takes every tap except those on one thing: while the lobby guide
    /// is pointing at something, that is the only thing that can be pressed.
    /// </summary>
    public sealed class ColosseumGuideGate : MonoBehaviour, ICanvasRaycastFilter
    {
        public RectTransform Hole;

        public bool IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera)
        {
            return Hole == null || !Hole.gameObject.activeInHierarchy ||
                !RectTransformUtility.RectangleContainsScreenPoint(Hole, screenPoint, eventCamera);
        }
    }
}
