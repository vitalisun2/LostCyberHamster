#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using Assets.Scripts.Account;
using Assets.Scripts.DevTools.GameProgressTesting;
using Assets.Scripts.DevTools.QuestTesting;
using Assets.Scripts.DevTools.SkateboardTesting;
using Assets.Scripts.DevTools.SkinTesting;
using Assets.Scripts.GameManagerLogic;
using Assets.Scripts.Online;
using Assets.Scripts.System;
using LostCyberHamster.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Assets.Scripts.DevTools.UiToolkit
{
    internal sealed class DevToolsUiToolkitOverlayShell : IDisposable
    {
        private readonly DevToolsUiToolkitHost _host;
        private UIDocument _document;
        private GameManager _pausedGameManager;

        public DevToolsUiToolkitOverlayShell(AccountService accountService)
        {
            _host = new DevToolsUiToolkitHost(showLauncher: true, useSafeArea: true);
            _host.Opened += PauseGameplayIfNeeded;
            _host.Closed += ResumeGameplayIfNeeded;

            List<DevToolsNavigationLink> links = new()
            {
                new DevToolsNavigationLink(DevToolsUiToolkitPageIds.Account, "Аккаунт", "Сессия, чистый старт и отвязка."),
                new DevToolsNavigationLink(DevToolsUiToolkitPageIds.Gameplay, "Переключатели", "Бот и временное открытие уровней."),
                new DevToolsNavigationLink(DevToolsUiToolkitPageIds.GameProgress, "Прогресс забега", "Level up и прохождение target-уровней."),
                new DevToolsNavigationLink(DevToolsUiToolkitPageIds.ExperienceProgress, "XP и уровни", "Рекорд, первая сессия и return."),
                new DevToolsNavigationLink(DevToolsUiToolkitPageIds.Quests, "Квесты", "Выбор карточки и команды жизненного цикла."),
                new DevToolsNavigationLink(DevToolsUiToolkitPageIds.Skateboard, "Скейтборд", "Подготовка, сценарии и guided checks."),
                new DevToolsNavigationLink(DevToolsUiToolkitPageIds.Skin, "Скины", "Покупка и экипировка следующего скина."),
                new DevToolsNavigationLink(DevToolsUiToolkitPageIds.Resources, "Ресурсы", "Точное начисление Money."),
                new DevToolsNavigationLink(DevToolsUiToolkitPageIds.Networking, "Сеть", "Forced offline и сетевой режим."),
            };

            _host.RegisterRootPage(
                DevToolsUiToolkitPageIds.Root,
                new DevToolsNavigationPage(
                    _host.Factory,
                    "DEV-инструменты",
                    "Мобильный operator UI внутри игры: короткие сценарии, статусы и быстрые действия без хаотичного скролла.",
                    links,
                    Navigate));
            _host.RegisterPage(DevToolsUiToolkitPageIds.Account, new AccountDevToolsUiPage(_host.Factory, () => accountService));
            _host.RegisterPage(DevToolsUiToolkitPageIds.Gameplay, new GameplayDevToolsUiPage(_host.Factory));
            _host.RegisterPage(DevToolsUiToolkitPageIds.GameProgress, new GameProgressTestingUiPage(_host.Factory, _host.Close));
            _host.RegisterPage(DevToolsUiToolkitPageIds.ExperienceProgress, new ExperienceProgressTestingUiPage(_host.Factory));
            _host.RegisterPage(DevToolsUiToolkitPageIds.Quests, new QuestTestingUiPage(_host.Factory));
            _host.RegisterPage(DevToolsUiToolkitPageIds.Skateboard, new SkateboardTestingUiPage(_host.Factory, _host.Close));
            _host.RegisterPage(DevToolsUiToolkitPageIds.Skin, new SkinTestingUiPage(_host.Factory));
            _host.RegisterPage(DevToolsUiToolkitPageIds.Resources, new ResourcesDevToolsUiPage(_host.Factory));
            _host.RegisterPage(DevToolsUiToolkitPageIds.Networking, new NetworkingDevToolsUiPage(_host.Factory, () => GameNetworkFacade.Instance));
        }

        public VisualElement PanelElement => _host.Root.Q<VisualElement>("dev-overlay-panel");

        public void Tick()
        {
            AttachToActiveDocument();
            bool offline = GameNetworkFacade.Instance.IsForcedOffline;
            _host.SetLauncherState(offline ? "DEV OFF" : "DEV", offline);
            GameProgressTestRunner.Shared.Tick();
            SkateboardTestingRunner.Shared.Tick();
            if (_host.IsOpen)
                _host.Refresh();
        }

        public void Dispose()
        {
            _host.Opened -= PauseGameplayIfNeeded;
            _host.Closed -= ResumeGameplayIfNeeded;
            _host.Dispose();
        }

        public void OpenPanel() => _host.Open();
        public void ClosePanel() => _host.Close();
        public void ShowRootScreen() => Navigate(DevToolsUiToolkitPageIds.Root);
        public void ShowAccountScreen() => Navigate(DevToolsUiToolkitPageIds.Account);
        public void ShowGameplayScreen() => Navigate(DevToolsUiToolkitPageIds.Gameplay);
        public void ShowResourcesScreen() => Navigate(DevToolsUiToolkitPageIds.Resources);
        public void ShowNetworkingScreen() => Navigate(DevToolsUiToolkitPageIds.Networking);
        public void ShowGameProgressTestingScreen() => Navigate(DevToolsUiToolkitPageIds.GameProgress);
        public void ShowExperienceProgressTestingScreen() => Navigate(DevToolsUiToolkitPageIds.ExperienceProgress);
        public void ShowQuestTestingScreen() => Navigate(DevToolsUiToolkitPageIds.Quests);
        public void ShowSkateboardTestingScreen() => Navigate(DevToolsUiToolkitPageIds.Skateboard);
        public void ShowSkinTestingScreen() => Navigate(DevToolsUiToolkitPageIds.Skin);

        private void Navigate(string pageId)
        {
            _host.Open();
            _host.Navigate(pageId);
        }

        private void AttachToActiveDocument()
        {
            UIDocument activeDocument = FindActiveDocument();
            if (activeDocument == null || activeDocument.rootVisualElement == null || ReferenceEquals(_document, activeDocument))
                return;

            UiDocumentInputGuard.EnsureAttached(activeDocument);
            _document = activeDocument;
            _host.AttachTo(activeDocument.rootVisualElement);
        }

        private static UIDocument FindActiveDocument()
        {
            Scene activeScene = SceneManager.GetActiveScene();
            UIDocument fallback = null;
            foreach (UIDocument document in UnityEngine.Object.FindObjectsByType<UIDocument>(
                         FindObjectsInactive.Include,
                         FindObjectsSortMode.None))
            {
                if (document == null || !document.isActiveAndEnabled)
                    continue;

                fallback ??= document;
                if (document.gameObject.scene == activeScene)
                    return document;
            }

            return fallback;
        }

        private void PauseGameplayIfNeeded()
        {
            GameManager gameManager = LevelController.Instance?.LevelData?.GameManager;
            if (gameManager?.State == GameState.PLAYING)
            {
                gameManager.Pause();
                _pausedGameManager = gameManager;
            }
        }

        private void ResumeGameplayIfNeeded()
        {
            GameManager gameManager = _pausedGameManager;
            _pausedGameManager = null;
            if (gameManager?.State == GameState.PAUSED)
                gameManager.Resume();
        }
    }
}
#endif