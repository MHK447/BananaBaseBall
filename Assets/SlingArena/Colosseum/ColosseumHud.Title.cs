using System;
using UnityEngine;
using UnityEngine.UI;

namespace SlingArena.Colosseum
{
    public sealed partial class ColosseumHud
    {
        GameObject launchTitle;
        Action enteredTitle;
        int launchFrame;
        bool titlePresented;

        public bool IsLaunchTitleOpen => launchTitle != null;

        public void ShowLaunchTitle(Action onEntered)
        {
            if (titlePresented) return;
            titlePresented = true;
            launchTitle = Group("Launch loading", canvas, true);
            var background = launchTitle.AddComponent<Image>();
            background.color = new Color32(30, 26, 47, 255);
            enteredTitle = onEntered;
            launchFrame = Time.frameCount;
            if (!ColosseumPreloadPoster.Show((RectTransform)launchTitle.transform, font))
                FinishLaunch();
        }

        void TickLaunchTitle()
        {
            // Let the first game frame render behind the artwork before revealing the lobby or tutorial.
            if (Time.frameCount > launchFrame) FinishLaunch();
        }

        void FinishLaunch()
        {
            launchTitle.SetActive(false);
            UnityEngine.Object.Destroy(launchTitle);
            launchTitle = null;
            var ready = enteredTitle;
            enteredTitle = null;
            ready?.Invoke();
        }
    }
}
