using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace BananaBall.EditorTools
{
    /// <summary>Creates the Wacky Baseball scene, wires its assets, and puts it right after the TinySauce preload scene.</summary>
    public static class SceneBuilder
    {
        public const string ScenePath = "Assets/BananaBall/Scenes/BananaBall.unity";
        const string Preload = "Assets/VoodooPackages/TinySauce/Preload/Scene/TSPreloadScene.unity";

        [MenuItem("BananaBall/Build Scene")]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var go = new GameObject("WACKY BASEBALL");
            var g = go.AddComponent<BananaBallGame>();
            Wire(g);
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.Refresh();
            UpdateBuildSettings();
            Debug.Log("[Wacky Baseball] scene built at " + ScenePath + " (renderer index " + g.rendererIndex + ")");
        }

        /// <summary>Shaders and fonts the game needs (also used to refresh an existing scene).</summary>
        public static void Wire(BananaBallGame g)
        {
            g.litShader = Shader.Find("BananaBall/Lit");
            g.unlitShader = Shader.Find("BananaBall/Unlit");
            g.skyShader = Shader.Find("BananaBall/Sky");
            g.fontFile = AssetDatabase.LoadAssetAtPath<Font>("Assets/Arts/Fonts/Outfit-Black.ttf");
            g.fontAsset = AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>("Assets/Arts/Fonts/Outfit-Black SDF.asset");
            g.fontKrFile = AssetDatabase.LoadAssetAtPath<Font>("Assets/Arts/Fonts/ONE-Mobile-POP.otf");
            g.fontKrAsset = AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>("Assets/Arts/Fonts/ONE-Mobile-POP SDF.asset");
            g.rendererIndex = UniversalRendererIndex();
            WireFx(g.fxPrefabs);
        }

        const string Cfx = "Assets/JMO Assets/Cartoon FX Remaster/CFXR Prefabs/";

        static GameObject Cfxr(string path)
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(Cfx + path + ".prefab");
            if (go == null) Debug.LogWarning("[Wacky Baseball] Cartoon FX prefab not found: " + path);
            return go;
        }

        /// <summary>Cartoon FX Remaster prefabs (already in the project) for contact, dust, catches and fireworks.</summary>
        static void WireFx(FxPrefabs f)
        {
            f.hit = Cfxr("Impacts/CFXR2 Hit");
            f.hitSparks = Cfxr("Impacts/CFXR Hit Sparks HDR");
            f.bigImpact = Cfxr("Impacts/CFXR Big Impact HDR");
            f.woodDebris = Cfxr("Impacts/CFXR2 Debris Wood Hit (Unlit)");
            f.groundHit = Cfxr("Impacts/CFXR2 Ground Hit");
            f.groundHitBig = Cfxr("Impacts/CFXR2 Ground Hit (Alt)");
            f.smokePoof = Cfxr("Misc/CFXR Smoke Poof");
            f.catchPop = Cfxr("Impacts/CFXR Hit C");
            f.stun = Cfxr("Expressions/CFXR2 Expression Stun (Loop)");
            f.firework = Cfxr("Explosions/CFXR4 Firework with Glows HDR Shoot Single (Random Color)");
            f.fireworkBig = Cfxr("Explosions/CFXR4 Firework HDR Shoot Multiple Loop (Random Color)");
            f.fireTrail = Cfxr("Fire/CFXR Fire Trail");
            f.textBoom = Cfxr("Texts/CFXR2 _BOOM_");
            f.textWham = Cfxr("Texts/CFXR _WHAM_");
            f.textPow = Cfxr("Texts/CFXR _POW_");
            f.textBoing = Cfxr("Texts/CFXR _BOING_");
            f.textWow = Cfxr("Texts/CFXR3 _WOW_");
            f.textSmash = Cfxr("Texts/CFXR _SMASH_");
        }

        [MenuItem("BananaBall/Refresh Scene Assets")]
        public static void Refresh()
        {
            var g = Object.FindFirstObjectByType<BananaBallGame>();
            if (g == null) { Debug.LogWarning("[Wacky Baseball] open the game scene first (BananaBall > Open Scene)."); return; }
            Wire(g);
            EditorUtility.SetDirty(g);
            EditorSceneManager.MarkSceneDirty(g.gameObject.scene);
            EditorSceneManager.SaveScene(g.gameObject.scene);
            Debug.Log("[Wacky Baseball] scene assets refreshed.");
        }

        public static int UniversalRendererIndex()
        {
            var asset = GraphicsSettings.defaultRenderPipeline as UniversalRenderPipelineAsset;
            if (asset == null) return -1;
            var so = new SerializedObject(asset);
            var list = so.FindProperty("m_RendererDataList");
            if (list == null) return -1;
            for (int i = 0; i < list.arraySize; i++)
                if (list.GetArrayElementAtIndex(i).objectReferenceValue is UniversalRendererData) return i;
            return -1;
        }

        public static void UpdateBuildSettings()
        {
            var scenes = new List<EditorBuildSettingsScene>();
            if (File.Exists(Preload)) scenes.Add(new EditorBuildSettingsScene(Preload, true));
            scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
            foreach (var s in EditorBuildSettings.scenes)
            {
                if (s.path == Preload || s.path == ScenePath) continue;
                if (!File.Exists(s.path)) continue;
                scenes.Add(new EditorBuildSettingsScene(s.path, false));
            }
            EditorBuildSettings.scenes = scenes.ToArray();
            AssetDatabase.SaveAssets();   // write ProjectSettings/EditorBuildSettings.asset now, not at editor exit
        }

        [MenuItem("BananaBall/Open Scene")]
        public static void Open()
        {
            // ask about unsaved changes in the open scene before replacing it
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (!File.Exists(ScenePath)) Build();
            EditorSceneManager.OpenScene(ScenePath);
        }

        [MenuItem("BananaBall/Reset Save Data")]
        public static void ResetSave()
        {
            Save.ResetAll();
            Debug.Log("[Wacky Baseball] progress in Master.dat cleared (batting order, fans, matches, match log, hints). Other save data is kept.");
        }

        /// <summary>Adds and selects a 1080x1920 portrait size in the Game view.</summary>
        [MenuItem("BananaBall/Game View 1080x1920")]
        public static void PortraitGameView()
        {
            try
            {
                var sizesType = System.Type.GetType("UnityEditor.GameViewSizes,UnityEditor");
                var singleType = typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
                var instance = singleType.GetProperty("instance").GetValue(null, null);
                var group = sizesType.GetMethod("GetGroup").Invoke(instance, new object[] { (int)GameViewSizeGroupType.Standalone });
                var groupType = group.GetType();
                int total = (int)groupType.GetMethod("GetTotalCount").Invoke(group, null);
                int found = -1;
                for (int i = 0; i < total; i++)
                {
                    var size = groupType.GetMethod("GetGameViewSize").Invoke(group, new object[] { i });
                    var st = size.GetType();
                    if ((int)st.GetProperty("width").GetValue(size, null) == 1080 && (int)st.GetProperty("height").GetValue(size, null) == 1920) { found = i; break; }
                }
                if (found < 0)
                {
                    var sizeType = System.Type.GetType("UnityEditor.GameViewSize,UnityEditor");
                    var typeEnum = System.Type.GetType("UnityEditor.GameViewSizeType,UnityEditor");
                    var ctor = sizeType.GetConstructor(new[] { typeEnum, typeof(int), typeof(int), typeof(string) });
                    var newSize = ctor.Invoke(new object[] { System.Enum.Parse(typeEnum, "FixedResolution"), 1080, 1920, "Wacky Baseball 9:16" });
                    groupType.GetMethod("AddCustomSize").Invoke(group, new[] { newSize });
                    found = (int)groupType.GetMethod("GetTotalCount").Invoke(group, null) - 1;
                }
                var gvType = System.Type.GetType("UnityEditor.GameView,UnityEditor");
                var gv = EditorWindow.GetWindow(gvType);
                var prop = gvType.GetProperty("selectedSizeIndex", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                if (prop != null) prop.SetValue(gv, found, null);
                else gvType.GetMethod("SizeSelectionCallback", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)?.Invoke(gv, new object[] { found, null });
                gv.Repaint();
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[Wacky Baseball] could not set the Game view size: " + e.Message);
            }
        }
    }
}
