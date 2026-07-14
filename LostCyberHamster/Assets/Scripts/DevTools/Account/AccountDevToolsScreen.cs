#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using Assets.Scripts.Account;
using Assets.Scripts.DevTools.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Scripts.DevTools.Account
{
    /// <summary>
    /// Предоставляет безопасный сброс локального тестового состояния аккаунта.
    /// </summary>
    internal sealed class AccountDevToolsScreen : IDevToolsScreen
    {
        private const string _resetButtonLabel = "RESET ACCOUNT TEST STATE";
        private const string _confirmButtonLabel = "CONFIRM RESET";

        private readonly AccountService _accountService;
        private readonly Action _returnToRoot;
        private readonly Action<string> _setTitle;
        private readonly RectTransform _rootRect;
        private readonly Text _resetButtonText;
        private readonly Text _resultText;

        private bool _isConfirmationPending;

        public AccountDevToolsScreen(
            Transform parent,
            Font font,
            AccountService accountService,
            Action returnToRoot,
            Action<string> setTitle)
        {
            _accountService = accountService;
            _returnToRoot = returnToRoot;
            _setTitle = setTitle;

            var uiFactory = new DevToolsUiFactory(font);
            RootObject = uiFactory.CreateStaticPage("AccountScreen", parent, out Transform content);
            _rootRect = RootObject.GetComponent<RectTransform>();

            uiFactory.CreateSectionHeading("ResetHeading", content, "Reset Account Test State");
            uiFactory.CreateBodyText(
                "ResetDescription",
                content,
                "Clears only local Unity Authentication credentials. " +
                "Server account, progress, Cloud Save and Analytics remain untouched.");
            Button resetButton = uiFactory.CreateButton(
                "ResetAccountTestStateButton",
                content,
                _resetButtonLabel,
                new Color(1f, 0.78f, 0.78f),
                RequestReset);
            _resetButtonText = resetButton.GetComponentInChildren<Text>();
            _resultText = uiFactory.CreateBodyText("ResetResult", content, string.Empty);

            RootObject.SetActive(false);
        }

        public GameObject RootObject { get; }

        public void Show()
        {
            RootObject.SetActive(true);
            _setTitle?.Invoke("Аккаунт");
        }

        public void Hide()
        {
            CancelConfirmation();
            RootObject.SetActive(false);
        }

        public void GoBack()
        {
            _returnToRoot?.Invoke();
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
        }

        private void RequestReset()
        {
            if (!_isConfirmationPending)
            {
                _isConfirmationPending = true;
                _resetButtonText.text = _confirmButtonLabel;
                _resultText.text = "Press CONFIRM RESET to clear local account credentials.";
                return;
            }

            ConfirmReset();
        }

        private void ConfirmReset()
        {
            CancelConfirmation();

            try
            {
                _accountService.ResetForTesting();
                _resultText.text = "Success. Local account credentials cleared. " +
                                   "The next Account Start will select CreateGuest.";
            }
            catch (Exception exception)
            {
                _resultText.text = $"Error. Account test state was not reset: {exception.Message}";
                Debug.LogError($"[Account] Test state reset failed: {exception.Message}");
            }
        }

        private void CancelConfirmation()
        {
            _isConfirmationPending = false;
            _resetButtonText.text = _resetButtonLabel;
        }
    }
}
#endif
