#if UNITY_ANDROID && !UNITY_EDITOR
using com.adjust.sdk;
using UnityEngine;

namespace BananaBall
{
    internal sealed class AdjustLifecycle : MonoBehaviour
    {
        private bool ready;
        private bool paused;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ObserveStartupConsent()
        {
            TinySauce.SubscribeOnInitFinishedEvent(OnStartupConsent);
        }

        private static void OnStartupConsent(bool attribution, bool analytics)
        {
            TinySauce.UnsubscribeOnInitFinishedEvent(OnStartupConsent);
            if (!attribution) return;

            var lifecycle = new GameObject("Adjust lifecycle", typeof(AdjustLifecycle));
            DontDestroyOnLoad(lifecycle);
        }

        private void Start()
        {
            // TinySauce starts Adjust after the consent callback. Adjust starts in the resumed state.
            // The SDK can carry a prior opt-out on disk after attribution has been granted again.
            Adjust.setEnabled(true);
            ready = true;
            if (paused) AdjustAndroid.OnPause();
        }

        private void OnApplicationPause(bool isPaused)
        {
            if (paused == isPaused) return;
            paused = isPaused;
            if (!ready) return;

            if (paused)
                AdjustAndroid.OnPause();
            else
                AdjustAndroid.OnResume();
        }
    }
}
#endif
