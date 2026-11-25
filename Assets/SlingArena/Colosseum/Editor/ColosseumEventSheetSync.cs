using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace SlingArena.Colosseum.EditorTools
{
    /// <summary>
    /// Imports the map event tabs and SmashUnitInfo into Resources/ColosseumTables. Play mode downloads in the
    /// background. Manual imports and pre-build imports wait for the result. Offline builds use the bundled CSVs.
    /// </summary>
    [InitializeOnLoad]
    public static class ColosseumEventSheetSync
    {
        const string SheetId = "1d8aU3eZJcqTEbw6JSPPz_tM3WgxiEtf5NuOLLo1dE-I";
        const string Folder = "Assets/SlingArena/Colosseum/Resources/" + ColosseumEventTable.Folder;
        const string AutoKey = "SlingArena.MapEvents.AutoSync";
        const int UnitsGid = 472619118;
        const string MenuRoot = "SlingArena/맵 이벤트/";
        const string SyncItem = MenuRoot + "시트에서 가져오기", AutoItem = MenuRoot + "플레이할 때마다 자동으로 가져오기", OpenItem = MenuRoot + "시트 열기";

        struct Tab
        {
            public string Name, Needs, SheetName;
            public int Gid;
            public Tab(string name, int gid, string needs, string sheetName = null)
            {
                Name = name;
                Gid = gid;
                Needs = needs;
                SheetName = sheetName ?? name;
            }
        }

        // Each tab, its number in the sheet's address (it stays the same when the tab is renamed or moved), and a
        // column it has to have for what came back to be taken for the tab and not for an error page.
        static readonly Tab[] Tabs =
        {
            new Tab(ColosseumEventTable.InfoName, 16031101, "unlock_arena"),
            new Tab(ColosseumEventTable.ValueName, 16031102, "value_key"),
            new Tab(ColosseumEventTable.RuleName, 16031103, "force_events"),
            new Tab(ColosseumUnitInfo.TableName, UnitsGid, "unit_key", "SmashUnitInfo"),
        };

        static readonly HttpClient Client = new HttpClient { Timeout = TimeSpan.FromSeconds(6) };

        static Task<string>[] waiting;   // a download begun on entering play mode, until it is in

        static ColosseumEventSheetSync()
        {
            EditorApplication.playModeStateChanged += change =>
            {
                if (change != PlayModeStateChange.EnteredPlayMode || !Auto || waiting != null) return;
                waiting = Begin();
                EditorApplication.update += Poll;
            };
        }

        static void Poll()
        {
            if (waiting != null)
            {
                foreach (Task<string> download in waiting)
                {
                    if (!download.IsCompleted) return;
                }
                Task<string>[] done = waiting;
                waiting = null;
                // Update the unit lookup in memory and the event tables on disk without reimporting during play.
                Finish(done, false, false);
            }
            EditorApplication.update -= Poll;
        }

        public static bool Auto
        {
            get { return EditorPrefs.GetBool(AutoKey, true); }
            set { EditorPrefs.SetBool(AutoKey, value); }
        }

        [MenuItem(SyncItem, priority = 100)]
        [MenuItem("SlingArena/유닛/밸런스 시트 동기화", priority = 100)]
        public static void SyncNow() { Sync(true); }

        [MenuItem(AutoItem, priority = 101)]
        static void ToggleAuto() { Auto = !Auto; }

        [MenuItem(AutoItem, true)]
        static bool ToggleAutoShown()
        {
            Menu.SetChecked(AutoItem, Auto);
            return true;
        }

        [MenuItem(OpenItem, priority = 120)]
        static void OpenSheet() { Application.OpenURL("https://docs.google.com/spreadsheets/d/" + SheetId + "/edit#gid=" + Tabs[2].Gid); }

        [MenuItem("SlingArena/유닛/UnitInfo 시트 열기", priority = 120)]
        static void OpenUnitSheet() { Application.OpenURL("https://docs.google.com/spreadsheets/d/" + SheetId + "/edit#gid=" + UnitsGid); }

        /// <summary>
        /// Downloads the configured tabs, returning true if all were accepted.
        /// </summary>
        public static bool Sync(bool loud)
        {
            // A manual or build import supersedes any older download still pending from entering play mode.
            EditorApplication.update -= Poll;
            waiting = null;
            Task<string>[] downloads = Begin();
            try { Task.WaitAll(downloads, 9000); }
            catch (AggregateException) { }
            return Finish(downloads, loud, true);
        }

        static Task<string>[] Begin()
        {
            var downloads = new Task<string>[Tabs.Length];
            for (int i = 0; i < Tabs.Length; i++)
            {
                Tab tab = Tabs[i];
                downloads[i] = Task.Run(() => Download(tab));
            }
            return downloads;
        }

        static bool Finish(Task<string>[] downloads, bool loud, bool import)
        {
            var texts = new string[Tabs.Length];
            var changed = new List<string>();
            var failed = new List<string>();
            Directory.CreateDirectory(Folder);
            for (int i = 0; i < Tabs.Length; i++)
                texts[i] = downloads[i].Status == TaskStatus.RanToCompletion ? downloads[i].Result : null;
            // Info gates the pool and Rule chooses from it. Import the pair from the same
            // tier-aware schema so an older online table cannot re-enable locked events.
            if (texts[0] == null || texts[2] == null)
                texts[0] = texts[2] = null;
            for (int i = 0; i < Tabs.Length; i++)
            {
                string path = Folder + "/" + Tabs[i].Name + ".csv";
                if (Tabs[i].Name == ColosseumUnitInfo.TableName && texts[i] != null
                    && !ColosseumUnitInfo.TryApply(texts[i], out string error))
                {
                    Debug.LogWarning("[UnitInfo] " + error);
                    failed.Add(Tabs[i].SheetName);
                    continue;
                }
                if (texts[i] == null)
                {
                    failed.Add(Tabs[i].SheetName);
                    texts[i] = File.Exists(path) ? File.ReadAllText(path) : null;
                    continue;
                }
                if (File.Exists(path) && File.ReadAllText(path) == texts[i]) continue;
                File.WriteAllText(path, texts[i]);
                changed.Add(Tabs[i].Name);
            }
            if (changed.Count > 0 && import)
            {
                // Only these files are brought in: nothing else that happens to be waiting on the disk is touched.
                AssetDatabase.ImportAsset(Folder, ImportAssetOptions.ForceSynchronousImport);
                foreach (string name in changed) AssetDatabase.ImportAsset(Folder + "/" + name + ".csv", ImportAssetOptions.ForceSynchronousImport);
            }

            ColosseumEventTable table = ColosseumEventTable.Parse(texts[0], texts[1], texts[2]);
            foreach (string problem in table.Problems) Debug.LogWarning("[MapEvent] 시트 확인 필요: " + problem);
            if (failed.Count > 0)
            {
                Debug.LogWarning("[Colosseum] 시트에서 다음 탭을 적용할 수 없어 마지막 정상 데이터를 사용합니다: " + string.Join(", ", failed));
            }
            if (changed.Count > 0) Debug.Log("[MapEvent] 시트에서 가져옴: " + string.Join(", ", changed) + " 변경됨.  " + table.Summary());
            else if (loud && failed.Count == 0) Debug.Log("[MapEvent] 시트와 같습니다 (바뀐 것 없음).  " + table.Summary());
            return failed.Count == 0;
        }

        /// <summary>One tab as CSV, or null if what came back is not the tab (no connection, a sign-in page, a tab that has been deleted).</summary>
        static string Download(Tab tab)
        {
            string byNumber = "https://docs.google.com/spreadsheets/d/" + SheetId + "/export?format=csv&gid=" + tab.Gid;
            string byName = "https://docs.google.com/spreadsheets/d/" + SheetId + "/gviz/tq?tqx=out:csv&headers=1&sheet=" + Uri.EscapeDataString(tab.SheetName);
            return Fetch(byNumber, tab.Needs) ?? Fetch(byName, tab.Needs);
        }

        static string Fetch(string address, string needs)
        {
            try
            {
                using (HttpResponseMessage answer = Client.GetAsync(address).GetAwaiter().GetResult())
                {
                    if (!answer.IsSuccessStatusCode) return null;
                    string text = answer.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                    if (string.IsNullOrEmpty(text)) return null;
                    int line = text.IndexOf('\n');
                    string header = line < 0 ? text : text.Substring(0, line);
                    return header.IndexOf(needs, StringComparison.OrdinalIgnoreCase) >= 0 && header.IndexOf('<') < 0 ? text : null;
                }
            }
            catch (Exception)
            {
                return null;
            }
        }
    }

    /// <summary>The sheet is brought in before a build too, so that a build never ships with numbers older than the sheet's.</summary>
    sealed class ColosseumEventSheetBuildSync : IPreprocessBuildWithReport
    {
        public int callbackOrder { get { return 0; } }

        public void OnPreprocessBuild(BuildReport report)
        {
            if (ColosseumEventSheetSync.Auto) ColosseumEventSheetSync.Sync(false);
        }
    }
}
