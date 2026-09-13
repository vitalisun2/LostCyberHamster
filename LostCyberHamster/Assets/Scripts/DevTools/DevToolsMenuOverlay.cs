#if UNITY_EDITOR || DEVELOPMENT_BUILD
using Assets.Scripts.Account;
using Assets.Scripts.DevTools.ExperienceProgressTesting;
using Assets.Scripts.DevTools.GameProgressTesting;
using Assets.Scripts.DevTools.QuestTesting;
using Assets.Scripts.DevTools.SkateboardTesting;
using Assets.Scripts.DevTools.SkinTesting;
using Assets.Scripts.DevTools.UiToolkit;
using UnityEngine;
using Zenject;

namespace Assets.Scripts.DevTools
{
    /// <summary>
    /// Связывает Unity lifecycle с общей runtime-оболочкой DEV-меню.
    /// </summary>
    public sealed class DevToolsMenuOverlay : MonoBehaviour
    {
        private DevToolsUiToolkitOverlayShell _shell;

        [Inject]
        private void Construct(AccountService accountService)
        {
            _shell ??= new DevToolsUiToolkitOverlayShell(accountService);
        }

        private void Update()
        {
            _shell?.Tick();
        }

        private void OnDestroy()
        {
            _shell?.Dispose();
            ExperienceProgressTestRunner.Shared.HandlePlayModeStopped();
            GameProgressTestRunner.Shared.HandlePlayModeStopped();
            QuestTestRunner.Shared.HandlePlayModeStopped();
            SkateboardTestingRunner.Shared.HandlePlayModeStopped();
            SkinTestingRunner.Shared.ResetStatus();
            AbilityProgressTestingRunner.Shared.EndSession();
        }
    }
}
#endif
