using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SlingArena.Colosseum.EditorTools
{
    /// <summary>
    /// File-driven remote control for the editor, so the Colosseum scene can be refreshed, played, stopped and
    /// inspected without anyone touching the Unity window. Commands are read from Library/ClaudeBridge/cmd.txt
    /// (one per line: refresh, open, play, stop, selftest on, selftest off, clearlog); status, compiler messages
    /// and runtime errors are written back into the same folder.
    /// </summary>
    [InitializeOnLoad]
    public static class ColosseumBridge
    {
        const string ScenePath = "Assets/SlingArena/Colosseum/ColosseumPvP.unity";
        static readonly string Folder = Path.GetFullPath(Path.Combine(Application.dataPath, "../Library/ClaudeBridge"));
        static double nextPoll, nextStatus;
        static string lastCommand = "none";

        static ColosseumBridge()
        {
            try
            {
                Directory.CreateDirectory(Folder);
                File.WriteAllText(Path.Combine(Folder, "alive.txt"), DateTime.UtcNow.ToString("o"));
            }
            catch (Exception)
            {
                return;
            }
            EditorApplication.update += Tick;
            Application.logMessageReceived += OnLog;
            CompilationPipeline.assemblyCompilationFinished += OnCompiled;
        }

        static void Tick()
        {
            double now = EditorApplication.timeSinceStartup;
            if (now >= nextStatus)
            {
                nextStatus = now + 1.0;
                WriteStatus();
            }
            if (now < nextPoll)
                return;
            nextPoll = now + 0.4;

            string path = Path.Combine(Folder, "cmd.txt");
            if (!File.Exists(path))
                return;
            string[] lines;
            try
            {
                lines = File.ReadAllLines(path);
                File.Delete(path);
            }
            catch (Exception)
            {
                return;
            }
            foreach (string raw in lines)
            {
                string line = raw.Trim();
                if (line.Length > 0)
                    Run(line);
            }
            WriteStatus();
        }

        static void Run(string line)
        {
            lastCommand = line;
            try
            {
                switch (line)
                {
                    case "refresh":
                        AssetDatabase.Refresh();
                        break;
                    case "open":
                        if (!EditorApplication.isPlaying && SceneManager.GetActiveScene().path != ScenePath && !SceneManager.GetActiveScene().isDirty)
                            EditorSceneManager.OpenScene(ScenePath);
                        break;
                    case "play":
                        if (!EditorApplication.isPlaying && SceneManager.GetActiveScene().path == ScenePath)
                            EditorApplication.isPlaying = true;
                        break;
                    case "stop":
                        if (EditorApplication.isPlaying)
                            EditorApplication.isPlaying = false;
                        break;
                    case "selftest on":
                        File.WriteAllText(Path.Combine(Folder, "selftest"), "on");
                        break;
                    case "selftest off":
                        File.Delete(Path.Combine(Folder, "selftest"));
                        break;
                    case "clearlog":
                        File.Delete(Path.Combine(Folder, "log.txt"));
                        File.Delete(Path.Combine(Folder, "compile.txt"));
                        break;
                }
            }
            catch (Exception error)
            {
                Append("log.txt", "[bridge] " + line + " failed: " + error.Message);
            }
        }

        static void WriteStatus()
        {
            try
            {
                Scene scene = SceneManager.GetActiveScene();
                var text = new StringBuilder();
                text.AppendLine("time " + DateTime.UtcNow.ToString("HH:mm:ss"));
                text.AppendLine("playing " + EditorApplication.isPlaying);
                text.AppendLine("compiling " + EditorApplication.isCompiling);
                text.AppendLine("scene " + scene.path);
                text.AppendLine("dirty " + scene.isDirty);
                text.AppendLine("last " + lastCommand);
                File.WriteAllText(Path.Combine(Folder, "status.txt"), text.ToString());
            }
            catch (Exception)
            {
            }
        }

        static void OnLog(string condition, string stackTrace, LogType type)
        {
            if (type == LogType.Log || type == LogType.Warning)
                return;
            string first = stackTrace ?? string.Empty;
            int cut = first.IndexOf('\n', Math.Min(first.Length, 1));
            Append("log.txt", DateTime.UtcNow.ToString("HH:mm:ss") + " [" + type + "] " + condition + "\n    " + (cut > 0 ? first.Substring(0, Math.Min(first.Length, 600)) : first));
        }

        static void OnCompiled(string assembly, CompilerMessage[] messages)
        {
            foreach (CompilerMessage message in messages)
            {
                if (message.type == CompilerMessageType.Error)
                    Append("compile.txt", DateTime.UtcNow.ToString("HH:mm:ss") + " " + message.message);
            }
        }

        static void Append(string file, string text)
        {
            try
            {
                File.AppendAllText(Path.Combine(Folder, file), text + "\n");
            }
            catch (Exception)
            {
            }
        }
    }
}
