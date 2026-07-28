#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using Assets.Scripts.DevTools.Core;
using UnityEngine;

namespace Assets.Scripts.DevTools.GameProgressTesting
{
    /// <summary>Связывает runtime DEV-представление с единым shared runner.</summary>
    internal sealed class GameProgressTestingScreen
    {
        private readonly GameProgressTestRunner _runner;
        private readonly GameProgressTestingView _view;
        private readonly Action<string> _setTitle;
        private readonly RectTransform _rootRect;

        public GameProgressTestingScreen(
            Transform parent,
            Font font,
            Action<string> setTitle)
        {
            _setTitle = setTitle;
            _runner = GameProgressTestRunner.Shared;

            var uiFactory = new DevToolsUiFactory(font);
            _view = new GameProgressTestingView(parent, uiFactory);
            _view.PrepareLevelUpRequested += _runner.PrepareLevelUp;
            _view.PrimaryRequested += _runner.RunPrimaryAction;
            _view.CancelRequested += _runner.Cancel;
            _view.ResetProgressRequested += _runner.ResetProgress;
            _runner.Changed += RefreshPresentation;

            RootObject = _view.RootObject;
            _rootRect = RootObject.GetComponent<RectTransform>();
            RootObject.SetActive(false);
        }

        public GameObject RootObject { get; }

        public void Show()
        {
            RootObject.SetActive(true);
            _setTitle?.Invoke("Game Progress Testing");
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
            _runner.Tick();
            _view.Render(_runner);
        }
    }
}
#endif
