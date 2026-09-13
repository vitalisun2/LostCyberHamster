#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using Assets.Scripts.Account;
using Assets.Scripts.DevTools.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Scripts.DevTools.Account
{
    /// <summary>
    /// Предоставляет локальный сброс сессии и чистый старт с новым гостевым прогрессом.
    /// </summary>
    internal sealed class AccountDevToolsScreen : IDevToolsScreen
    {
        private readonly AccountService _accountService;
        private readonly Action _returnToRoot;
        private readonly Action<string> _setTitle;
        private readonly RectTransform _rootRect;
        private readonly Button _localResetButton;
        private readonly Button _fullResetButton;
        private readonly Button _unlinkButton;
        private readonly Text _resultText;
        private readonly Text _stateText;

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
            RootObject = uiFactory.CreateScrollPage("AccountScreen", parent, out Transform content);
            _rootRect = RootObject.GetComponent<RectTransform>();

            uiFactory.CreateSectionHeading("AccountHeading", content, "АККАУНТ");
            uiFactory.CreateBodyText(
                "AccountDescription",
                content,
                "Общие production-команды аккаунта, выровненные по той же структуре, что и в Tools/Testing.");

            Transform localResetCard = uiFactory.CreateCard("LocalResetCard", content, DevToolsTheme.Surface);
            uiFactory.CreateSectionHeading("LocalResetHeading", localResetCard, "LOCAL RESET");
            uiFactory.CreateBodyText(
                "LocalResetDescription",
                localResetCard,
                "Очищает локальные Unity Authentication и Player Accounts сессии. Серверная привязка остаётся нетронутой.");
            _localResetButton = uiFactory.CreateButton(
                "ResetLocalAccountStateButton",
                localResetCard,
                "RESET LOCAL ACCOUNT STATE",
                new Color(1f, 0.78f, 0.78f),
                ResetLocalAccountState);

            Transform fullResetCard = uiFactory.CreateCard("FullResetCard", content, DevToolsTheme.DangerCard);
            uiFactory.CreateSectionHeading("FullResetHeading", fullResetCard, "ЧИСТЫЙ СТАРТ");
            uiFactory.CreateBodyText(
                "FullResetDescription",
                fullResetCard,
                "Создаёт нового гостя с нулевым прогрессом. Локальный прогресс заменяется; настройки сохраняются. Без сети вход завершится позже.");
            _fullResetButton = uiFactory.CreateButton(
                "StartFreshGuestButton",
                fullResetCard,
                "ЧИСТЫЙ СТАРТ — НОВЫЙ ГОСТЬ",
                new Color(1f, 0.58f, 0.58f),
                StartFreshGuest);

            Transform unlinkCard = uiFactory.CreateCard("UnlinkCard", content, DevToolsTheme.DangerCard);
            uiFactory.CreateSectionHeading("UnlinkHeading", unlinkCard, "ОТВЯЗКА ДЛЯ ТЕСТОВ");
            uiFactory.CreateBodyText("UnlinkDescription", unlinkCard,
                "Удаляет серверную привязку и локальную сессию прежнего аккаунта. Для новой игры используйте чистый старт.");
            _unlinkButton = uiFactory.CreateButton("FullResetTestAccountButton", unlinkCard,
                "ОТВЯЗАТЬ АККАУНТ И ОЧИСТИТЬ СЕССИЮ", new Color(1f, 0.58f, 0.58f), FullResetTestAccount);

            Transform stateCard = uiFactory.CreateCard("AccountStateCard", content, DevToolsTheme.StatusCard);
            uiFactory.CreateSectionHeading("AccountStateHeading", stateCard, "СОСТОЯНИЕ");
            _stateText = uiFactory.CreateBodyText("AccountState", stateCard, string.Empty);

            Transform resultCard = uiFactory.CreateCard("ResetResultCard", content, DevToolsTheme.StatusCard);
            uiFactory.CreateSectionHeading("ResetResultHeading", resultCard, "РЕЗУЛЬТАТ");
            _resultText = uiFactory.CreateBodyText("ResetResult", resultCard, string.Empty);

            RootObject.SetActive(false);
        }

        public GameObject RootObject { get; }

        public void Show()
        {
            RootObject.SetActive(true);
            _setTitle?.Invoke("Аккаунт");
            RefreshPresentation();
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
            _localResetButton.interactable = !_isResetInProgress && _accountService.CanStartFreshGuestForTesting;
            _fullResetButton.interactable = !_isResetInProgress && _accountService.CanStartFreshGuestForTesting;
            _unlinkButton.interactable = !_isResetInProgress && _accountService.CanStartFreshGuestForTesting &&
                _accountService.TryGetLinkedPlayerId(out _);
            _stateText.text = "Состояние аккаунта: " + _accountService.State;
        }

        private void ResetLocalAccountState()
        {
            if (_isResetInProgress)
                return;

            try
            {
                _accountService.ResetLocalAccountStateForTesting();
                _resultText.text = "Local session cleared. Existing progress still belongs to its previous owner.";
            }
            catch (Exception exception)
            {
                if (IsAlive())
                    _resultText.text = "Error. Local account state was not reset.";
                Debug.LogError($"[Account] Local reset UI action failed. Error type: {exception.GetType().Name}.");
            }
        }

        private void StartFreshGuest()
        {
            if (_isResetInProgress)
                return;

            SetBusy(true);
            _resultText.text = "Создаём чистый профиль…";

            try
            {
                _accountService.StartFreshGuestForTesting();
                if (IsAlive())
                    _resultText.text = "Новый прогресс готов. Гостевой аккаунт подключится при доступной сети.";
            }
            catch (Exception exception)
            {
                if (IsAlive())
                    _resultText.text = "Чистый старт не завершён. " + exception.Message;
                Debug.LogError($"[Account] Fresh start failed: {exception}");
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
            RefreshPresentation();
        }

        /// <summary>Сохраняет отдельный сценарий серверной отвязки для существующих E2E-инструментов.</summary>
        private async void FullResetTestAccount()
        {
            if (_isResetInProgress) return;
            SetBusy(true);
            try
            {
                await _accountService.FullResetTestAccountAsync();
                if (IsAlive()) _resultText.text = "Server link and local session cleared. Use fresh start for a new game.";
            }
            catch (OperationCanceledException)
            {
                if (IsAlive()) _resultText.text = "Full reset was cancelled.";
            }
            catch (Exception exception)
            {
                if (IsAlive()) _resultText.text = "Error. Full reset was not completed.";
                Debug.LogError($"[Account] Full reset failed: {exception}");
            }
            finally
            {
                if (IsAlive()) SetBusy(false);
            }
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
