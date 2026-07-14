#if UNITY_EDITOR || DEVELOPMENT_BUILD
using Assets.Scripts.Account;
using Assets.Scripts.DevTools.Core;
using UnityEngine;
using Zenject;

namespace Assets.Scripts.DevTools
{
    /// <summary>
    /// Связывает Unity lifecycle с общей runtime-оболочкой DEV-меню.
    /// </summary>
    public sealed class DevToolsMenuOverlay : MonoBehaviour
    {
        private DevToolsOverlayShell _shell;

        [Inject]
        private void Construct(AccountService accountService)
        {
            _shell ??= new DevToolsOverlayShell(gameObject, accountService);
        }

        private void Update()
        {
            _shell?.Tick();
        }
    }
}
#endif
