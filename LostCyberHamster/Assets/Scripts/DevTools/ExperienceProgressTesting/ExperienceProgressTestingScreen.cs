#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using Assets.Scripts.DevTools.Core;
using UnityEngine;

namespace Assets.Scripts.DevTools.ExperienceProgressTesting
{
    /// <summary>Связывает runtime DEV-представление с единым shared XP runner.</summary>
    internal sealed class ExperienceProgressTestingScreen
    {
        private readonly ExperienceProgressTestRunner _runner;
        private readonly ExperienceProgressTestingView _view;
        private readonly Action<string> _setTitle;
        private readonly RectTransform _rootRect;

        public ExperienceProgressTestingScreen(
            Transform parent,
            Font font,
            Action<string> setTitle)
        {
            _setTitle = setTitle;
            _runner = ExperienceProgressTestRunner.Shared;

            // Представление вызывает только публичные команды shared runner.
            var uiFactory = new DevToolsUiFactory(font);
            _view = new ExperienceProgressTestingView(parent, uiFactory);
            _view.PrepareNewRecordRequested += _runner.PrepareNewRecord;
            _view.CompleteNextLevelRequested += _runner.CompleteNextLevel;
            _runner.Changed += RefreshPresentation;

            // Экран использует общий responsive viewport DEV-shell.
            RootObject = _view.RootObject;
            _rootRect = RootObject.GetComponent<RectTransform>();
            RootObject.SetActive(false);
        }

        public GameObject RootObject { get; }

        public void Show()
        {
            RootObject.SetActive(true);
            _setTitle?.Invoke("XP/Level Progress Testing");
            RefreshPresentation();
        }

        public void Hide()
        {
            RootObject.SetActive(false);
        }

        public void ApplyLayout(float left, float top, float right, float bottom)
        {
            _rootRect.anchorMin = Vector2.zero;
            _rootRect.anchorMax = Vector2.one;
            _rootRect.offsetMin = new Vector2(left, bottom);
            _rootRect.offsetMax = new Vector2(-right, -top);
        }

        public void RefreshPresentation()
        {
            _view.Render(_runner);
        }
    }
}
#endif
