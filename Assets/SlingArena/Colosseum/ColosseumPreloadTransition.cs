using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using Voodoo.Tiny.Sauce.Privacy;

namespace SlingArena.Colosseum
{
    internal sealed class ColosseumPreloadTransition : MonoBehaviour
    {
        internal const string SdkScenePath = "Assets/VoodooPackages/TinySauce/Preload/Scene/TSPreloadScene.unity";
        const float StartupWaitSeconds = 8f;
        bool loading;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void ObserveFirstScene()
        {
            SceneManager.sceneLoaded -= InstallOnPreloadScene;
            SceneManager.sceneLoaded += InstallOnPreloadScene;
        }

        static void InstallOnPreloadScene(Scene scene, LoadSceneMode mode)
        {
            SceneManager.sceneLoaded -= InstallOnPreloadScene;
            if (scene.path != SdkScenePath) return;

            var sdkLoader = FindFirstObjectByType<TSSceneLoader>();
            if (sdkLoader == null || sdkLoader.gameObject.scene != scene) return;

            // Replace the scene transition before the SDK loader subscribes to consent in Start.
            sdkLoader.enabled = false;
            sdkLoader.gameObject.AddComponent<ColosseumPreloadTransition>();
        }

        void Start()
        {
            TinySauce.SubscribeOnInitFinishedEvent(OnConsentReady);
            if (!loading) StartCoroutine(WaitForSdk());
        }

        void OnDestroy()
        {
            TinySauce.UnsubscribeOnInitFinishedEvent(OnConsentReady);
        }

        IEnumerator WaitForSdk()
        {
            yield return new WaitForSecondsRealtime(StartupWaitSeconds);
            while (!loading && FindAnyObjectByType<PrivacyScreenBehaviour>() != null)
                yield return null;
            if (loading) yield break;

            Debug.LogWarning("TinySauce is still waiting for privacy initialization. Opening Smash Rumble.");
            BeginGameScene();
        }

        void OnConsentReady(bool attribution, bool analytics)
        {
            BeginGameScene();
        }

        void BeginGameScene()
        {
            if (loading) return;
            loading = true;
            StartCoroutine(LoadGameScene());
        }

        static IEnumerator LoadGameScene()
        {
            yield return null;
            SceneManager.LoadSceneAsync(1);
        }
    }
}
