#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Voodoo.Tiny.Sauce.Internal;

namespace SlingArena.Colosseum
{
    // Direct scene play uses the same consent-aware prefab as the mobile preload scene.
    internal static class ColosseumEditorSdkBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void InitializeForDirectScenePlay()
        {
            if (SceneManager.GetActiveScene().path != "Assets/SlingArena/Colosseum/ColosseumPvP.unity"
                || TinySauceBehaviour.Instance != null)
                return;

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/VoodooPackages/TinySauce/Prefabs/TinySauce.prefab");
            if (prefab == null)
            {
                Debug.LogError("Smash Rumble: TinySauce prefab is missing.");
                return;
            }

            Object.Instantiate(prefab);
        }
    }
}
#endif
