#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Assets.EditorTools
{
    /// <summary>
    /// Editor tool to view and manage diagnostic logs.
    /// Logs are automatically written to EditorLogs/diagnostic_log.txt by DebugManager.DiagLog()
    /// </summary>
    public static class DiagnosticLogViewer
    {
        [MenuItem("Tools/Diagnostics/View Diagnostic Log", priority = 600)]
        public static void ViewDiagnosticLog()
        {
            var logPath = DebugManager.GetDiagLogPath();
            
            if (!File.Exists(logPath))
            {
                Debug.LogWarning($"[DiagnosticLogViewer] Log file not found at: {logPath}");
                return;
            }
            
            var content = DebugManager.ReadDiagLog();
            
            // Open in external editor
            if (EditorUtility.DisplayDialog("Diagnostic Log", 
                $"Log file location:\n{logPath}\n\nTotal lines: {content.Split('\n').Length}", 
                "Open in External Editor", "Close"))
            {
                System.Diagnostics.Process.Start(logPath);
            }
        }

        [MenuItem("Tools/Diagnostics/Clear Diagnostic Log", priority = 601)]
        public static void ClearDiagnosticLog()
        {
            if (EditorUtility.DisplayDialog("Clear Diagnostic Log", 
                "Are you sure you want to clear the diagnostic log?", 
                "Clear", "Cancel"))
            {
                DebugManager.ClearDiagLog();
            }
        }

        [MenuItem("Tools/Diagnostics/Open Log Folder", priority = 602)]
        public static void OpenLogFolder()
        {
            var logPath = DebugManager.GetDiagLogPath();
            var folder = Path.GetDirectoryName(logPath);
            
            if (Directory.Exists(folder))
            {
                EditorUtility.RevealInFinder(folder);
            }
            else
            {
                Debug.LogWarning($"[DiagnosticLogViewer] Log folder not found: {folder}");
            }
        }
    }
}
#endif
