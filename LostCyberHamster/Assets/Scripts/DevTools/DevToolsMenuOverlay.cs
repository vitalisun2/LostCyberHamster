#if UNITY_EDITOR || DEVELOPMENT_BUILD
using Assets.Scripts.DevTools.Core;
using UnityEngine;

namespace Assets.Scripts.DevTools
{
    /// <summary>
    /// Связывает Unity lifecycle с общей runtime-оболочкой DEV-меню.
    /// </summary>
    public sealed class DevToolsMenuOverlay : MonoBehaviour
    {
        private DevToolsOverlayShell _shell;

        private void Awake()
        {
            if (!DevToolsMenuBootstrap.TryRegister(this))
            {
                Destroy(gameObject);
                return;
            }

            if (Application.isPlaying)
            {
                DontDestroyOnLoad(gameObject);
            }

            _shell ??= new DevToolsOverlayShell(gameObject);
        }

        private void OnDestroy()
        {
            DevToolsMenuBootstrap.Unregister(this);
        }

        private void Update()
        {
            _shell?.Tick();
        }
    }
}
#endif
