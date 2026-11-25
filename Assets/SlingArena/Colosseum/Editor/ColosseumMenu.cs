using UnityEditor;
using UnityEditor.SceneManagement;

namespace SlingArena.Colosseum.EditorTools
{
    /// <summary>Menu shortcuts for the Colosseum PvP scene.</summary>
    public static class ColosseumMenu
    {
        const string ScenePath = "Assets/SlingArena/Colosseum/ColosseumPvP.unity";

        [MenuItem("SlingArena/Open Colosseum PvP")]
        public static void Open()
        {
            if (EditorApplication.isPlaying)
                return;
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                EditorSceneManager.OpenScene(ScenePath);
        }

        [MenuItem("SlingArena/Play Colosseum PvP")]
        public static void Play()
        {
            if (EditorApplication.isPlaying)
                return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;
            EditorSceneManager.OpenScene(ScenePath);
            EditorApplication.isPlaying = true;
        }
    }
}
