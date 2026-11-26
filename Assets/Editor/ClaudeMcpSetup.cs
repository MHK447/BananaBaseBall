#if UNITY_EDITOR
using System;
using System.IO;
using MCPForUnity.Editor.Clients.Configurators;
using MCPForUnity.Editor.Services;
using MCPForUnity.Editor.Services.Transport.Transports;
using UnityEditor;
using UnityEngine;

namespace BananaBall.EditorTools
{
    /// <summary>
    /// One-time hookup of MCP for Unity to the Claude desktop app: switch the package to the stdio bridge
    /// (the transport Claude Desktop uses), start that bridge, and register the server in Claude's config —
    /// the same as pressing Start and Configure in Window > MCP for Unity. The result is written to
    /// Library/ClaudeMcpSetup.txt.
    /// </summary>
    [InitializeOnLoad]
    static class ClaudeMcpSetup
    {
        const string DoneKey = "BananaBall.ClaudeMcpSetup.Done";
        // Key written by the earlier copy of this script; honour it so Claude's config is not rewritten again.
        const string LegacyDoneKey = "SlingArena.ClaudeMcpSetup.Done";

        static ClaudeMcpSetup()
        {
            if (Application.isBatchMode) return;
            if (EditorPrefs.GetBool(LegacyDoneKey, false)) EditorPrefs.SetBool(DoneKey, true);
            EditorApplication.delayCall += Run;
        }

        [MenuItem("Tools/Claude/Connect Claude (MCP)")]
        static void RunFromMenu()
        {
            EditorPrefs.DeleteKey(DoneKey);
            Run();
        }

        static void Run()
        {
            string report = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Library", "ClaudeMcpSetup.txt"));
            var log = new System.Text.StringBuilder();
            log.AppendLine(DateTime.UtcNow.ToString("o"));
            try
            {
                EditorConfigurationCache.Instance.SetUseHttpTransport(false);
                log.AppendLine("transport: stdio");
                if (!StdioBridgeHost.IsRunning) StdioBridgeHost.StartAutoConnect();
                log.AppendLine("bridge running: " + StdioBridgeHost.IsRunning + " port " + StdioBridgeHost.GetCurrentPort());
                if (!EditorPrefs.GetBool(DoneKey, false))
                {
                    var claude = new ClaudeDesktopConfigurator();
                    claude.Configure();
                    log.AppendLine("claude desktop: " + claude.Status + " at " + claude.GetConfigPath());
                    EditorPrefs.SetBool(DoneKey, true);
                }
                else
                {
                    log.AppendLine("claude desktop: already configured");
                }
            }
            catch (Exception error)
            {
                log.AppendLine("error: " + error);
            }
            File.WriteAllText(report, log.ToString());
            Debug.Log("[Claude] MCP setup\n" + log);
        }
    }
}
#endif
