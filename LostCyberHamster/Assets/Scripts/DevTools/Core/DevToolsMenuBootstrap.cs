#if UNITY_EDITOR || DEVELOPMENT_BUILD
using Assets.Scripts.DevTools;
using UnityEngine;

namespace Assets.Scripts.DevTools.Core
{
    /// <summary>
    /// Создаёт единственный runtime-host DEV-меню и регистрирует его Unity lifecycle adapter.
    /// </summary>
    internal static class DevToolsMenuBootstrap
    {
        private const string _hostObjectName = "[DevToolsMenu]";
        private static DevToolsMenuOverlay _instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            GameObject host = GameObject.Find(_hostObjectName) ?? new GameObject(_hostObjectName);
            if (!host.TryGetComponent(out DevToolsMenuOverlay overlay))
            {
                overlay = host.AddComponent<DevToolsMenuOverlay>();
            }

            Object.DontDestroyOnLoad(host);
        }

        public static bool TryRegister(DevToolsMenuOverlay overlay)
        {
            if (_instance != null && _instance != overlay)
            {
                return false;
            }

            _instance = overlay;
            return true;
        }

        public static void Unregister(DevToolsMenuOverlay overlay)
        {
            if (_instance == overlay)
            {
                _instance = null;
            }
        }
    }
}
#endif
