using UnityEngine;

namespace Assets.Scripts.Debugging
{
    /// <summary>
    /// Provides a centralised hook for development-only diagnostics emitted on Android builds.
    /// </summary>
    public static class AndroidDiagnosticsHelper
    {
    private const string _defaultCategory = "General";
    private const string _rootTag = "AndroidDiag";

        public static bool ShouldLog()
        {
            return Debug.isDebugBuild && Application.platform == RuntimePlatform.Android;
        }

        public static void Log(string category, string message)
        {
            if (!ShouldLog())
            {
                return;
            }
        }

        public static void LogWarning(string category, string message)
        {
            if (!ShouldLog())
            {
                return;
            }

            var safeCategory = string.IsNullOrWhiteSpace(category) ? _defaultCategory : category;
            Debug.LogWarning($"[{_rootTag}][{safeCategory}] {message}");
        }

        public static void LogError(string category, string message)
        {
            if (!ShouldLog())
            {
                return;
            }

            var safeCategory = string.IsNullOrWhiteSpace(category) ? _defaultCategory : category;
            Debug.LogError($"[{_rootTag}][{safeCategory}] {message}");
        }
    }
}
