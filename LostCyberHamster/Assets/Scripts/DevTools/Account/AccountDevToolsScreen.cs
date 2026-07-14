#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using Assets.Scripts.Account;
using Assets.Scripts.DevTools.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Scripts.DevTools.Account
{
    /// <summary>
    /// Предоставляет локальный и полный сброс тестового состояния аккаунта.
    /// </summary>
    internal sealed class AccountDevToolsScreen : IDevToolsScreen
    {
        private readonly AccountService _accountService;
        private readonly Action _returnToRoot;
        private readonly Action<string> _setTitle;
        private readonly RectTransform _rootRect;
        private readonly Button _localResetButton;
        private readonly Button _fullResetButton;
        private readonly Text _resultText;

        private bool _isResetInProgress;

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

            uiFactory.CreateSectionHeading("LocalResetHeading", content, "Local Account Reset");
            uiFactory.CreateBodyText(
                "LocalResetDescription",
                content,
                "Clears local Unity Authentication and Player Accounts sessions. Server links remain untouched.");
            _localResetButton = uiFactory.CreateButton(
                "ResetLocalAccountStateButton",
                content,
                "RESET LOCAL ACCOUNT STATE",
                new Color(1f, 0.78f, 0.78f),
                ResetLocalAccountState);

            uiFactory.CreateSectionHeading("FullResetHeading", content, "Full Linked Account Reset");
            uiFactory.CreateBodyText(
                "FullResetDescription",
                content,
                "Signs in to the linked server account, removes its Unity Player Account link, then clears local sessions.");
            _fullResetButton = uiFactory.CreateButton(
                "FullResetTestAccountButton",
                content,
                "FULL RESET LINKED ACCOUNT",
                new Color(1f, 0.58f, 0.58f),
                FullResetTestAccount);
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

        private void ResetLocalAccountState()
        {
            if (_isResetInProgress)
                return;

            try
            {
                _accountService.ResetLocalAccountStateForTesting();
                _resultText.text = "Success. Local account state cleared. The next Account Start will select CreateGuest.";
            }
            catch (Exception exception)
            {
                if (IsAlive())
                    _resultText.text = "Error. Local account state was not reset.";
                Debug.LogError($"[Account] Local reset UI action failed. Error type: {exception.GetType().Name}.");
            }
        }

        private async void FullResetTestAccount()
        {
            if (_isResetInProgress)
                return;

            SetBusy(true);
            _resultText.text = "Full reset in progress…";

            try
            {
                await _accountService.FullResetTestAccountAsync();
                if (IsAlive())
                    _resultText.text = "Success. Server link and local account state cleared.";
            }
            catch (OperationCanceledException)
            {
                if (IsAlive())
                    _resultText.text = "Full reset was cancelled.";
            }
            catch (Exception exception)
            {
                if (IsAlive())
                    _resultText.text = "Error. Full reset was not completed.";
                Debug.LogError($"[Account] Full reset UI action failed. Error type: {exception.GetType().Name}.");
            }
            finally
            {
                if (IsAlive())
                    SetBusy(false);
            }
        }

        private void SetBusy(bool isBusy)
        {
            _isResetInProgress = isBusy;
            _localResetButton.interactable = !isBusy;
            _fullResetButton.interactable = !isBusy;
        }

        private bool IsAlive()
        {
            return RootObject != null &&
                   _resultText != null &&
                   _localResetButton != null &&
                   _fullResetButton != null;
        }
    }
}
#endif
