using UnityEditor;

namespace SlingArena.Colosseum.EditorTools
{
    /// <summary>
    /// Editor only: fights the next battles in the map of a chosen arena, whatever arena the save has reached, so
    /// that every map can be looked at without winning through to it. The choice lasts until the editor is closed.
    /// </summary>
    public static class ColosseumArenaPreview
    {
        const string Key = "SlingArena.Colosseum.PreviewArena";
        const string Root = "SlingArena/맵 미리보기/";
        const string Off = Root + "끄기 (실제 진행도대로)";
        const string One = Root + "아레나 1 - Royal Sandpit", Two = Root + "아레나 2 - Tidal Bastion";
        const string Three = Root + "아레나 3 - Ember Crown", Four = Root + "아레나 4 - Frostspire";
        const string Five = Root + "아레나 5 - Pharaoh Sands", Six = Root + "아레나 6 - Jade Temple", Seven = Root + "아레나 7 - Gridiron Bowl";

        [InitializeOnLoadMethod]
        static void Hook()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.EnteredPlayMode) Apply();
            };
        }

        static int Chosen { get { return SessionState.GetInt(Key, 0); } }

        // The game dresses the arena for this stage when the next battle starts (ColosseumGame.StartMatch).
        static void Apply() { ColosseumEvents.TestStage = Chosen; }

        static void Pick(int stage)
        {
            SessionState.SetInt(Key, stage);
            if (EditorApplication.isPlaying) Apply();
        }

        static bool Mark(string item, int stage)
        {
            Menu.SetChecked(item, Chosen == stage);
            return true;
        }

        [MenuItem(Off, priority = 300)] static void PickOff() { Pick(0); }
        [MenuItem(Off, true)] static bool MarkOff() { return Mark(Off, 0); }
        [MenuItem(One, priority = 311)] static void PickOne() { Pick(1); }
        [MenuItem(One, true)] static bool MarkOne() { return Mark(One, 1); }
        [MenuItem(Two, priority = 312)] static void PickTwo() { Pick(2); }
        [MenuItem(Two, true)] static bool MarkTwo() { return Mark(Two, 2); }
        [MenuItem(Three, priority = 313)] static void PickThree() { Pick(3); }
        [MenuItem(Three, true)] static bool MarkThree() { return Mark(Three, 3); }
        [MenuItem(Four, priority = 314)] static void PickFour() { Pick(4); }
        [MenuItem(Four, true)] static bool MarkFour() { return Mark(Four, 4); }
        [MenuItem(Five, priority = 315)] static void PickFive() { Pick(5); }
        [MenuItem(Five, true)] static bool MarkFive() { return Mark(Five, 5); }
        [MenuItem(Six, priority = 316)] static void PickSix() { Pick(6); }
        [MenuItem(Six, true)] static bool MarkSix() { return Mark(Six, 6); }
        [MenuItem(Seven, priority = 317)] static void PickSeven() { Pick(7); }
        [MenuItem(Seven, true)] static bool MarkSeven() { return Mark(Seven, 7); }
    }
}
