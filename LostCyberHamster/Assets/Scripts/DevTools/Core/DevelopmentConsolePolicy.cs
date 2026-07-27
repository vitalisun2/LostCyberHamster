#if DEVELOPMENT_BUILD && !UNITY_EDITOR
using UnityEngine;

namespace Assets.Scripts.DevTools.Core
{
    /// <summary>
    /// Скрывает встроенную Unity Development Console, если диагностическая сборка явно не включила её.
    /// </summary>
    internal static class DevelopmentConsolePolicy
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSplashScreen)]
        private static void Apply()
        {
#if LCH_SHOW_DEVELOPMENT_CONSOLE
            Debug.developerConsoleEnabled = true;
            Debug.developerConsoleVisible = true;
#else
            Debug.developerConsoleVisible = false;
            Debug.developerConsoleEnabled = false;
#endif
        }
    }
}
#endif
