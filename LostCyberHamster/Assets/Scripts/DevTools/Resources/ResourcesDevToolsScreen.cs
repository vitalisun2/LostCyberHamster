#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using Assets.Scripts.DevTools.Core;
using GameManagement;
using UnityEngine;
using UnityEngine.UI;
using Vues.GameCore;

namespace Assets.Scripts.DevTools.Resources
{
    /// <summary>Показывает точное DEV-начисление Money внутри runtime DEV-shell.</summary>
    internal sealed class ResourcesDevToolsScreen : IDevToolsScreen
    {
        private const int _defaultAmount = 100;

        private readonly Action _returnToRoot;
        private readonly Action<string> _setTitle;
        private readonly RectTransform _rootRect;
        private readonly InputField _amountField;
        private readonly Button _addMoneyButton;
        private readonly Text _statusText;
        private string _resultStatus;

        public ResourcesDevToolsScreen(
            Transform parent,
            Font font,
            Action returnToRoot,
            Action<string> setTitle)
        {
            _returnToRoot = returnToRoot;
            _setTitle = setTitle;

            var ui = new DevToolsUiFactory(font);
            RootObject = ui.CreateStaticPage(
                "ResourcesScreen",
                parent,
                out Transform content);
            _rootRect = RootObject.GetComponent<RectTransform>();
            ui.CreateSectionHeading("ResourcesHeading", content, "RESOURCES");
            ui.CreateBodyText("AmountLabel", content, "Amount");
            _amountField = ui.CreateInputField(
                "AmountField",
                content,
                _defaultAmount.ToString());
            _amountField.onValueChanged.AddListener(OnAmountChanged);
            _addMoneyButton = ui.CreateButton(
                "AddMoneyButton",
                content,
                "Add Money",
                DevToolsTheme.Primary,
                AddMoney,
                DevToolsTheme.PrimaryButtonHeight);
            Transform statusCard = ui.CreateCard(
                "StatusCard",
                content,
                DevToolsTheme.StatusCard);
            _statusText = ui.CreateBodyText(
                "StatusText",
                statusCard,
                string.Empty);
            RootObject.SetActive(false);
        }

        public GameObject RootObject { get; }

        public void Show()
        {
            RootObject.SetActive(true);
            _setTitle?.Invoke("Resources");
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
            bool isReady = ResourceManager.IsReady;
            _amountField.interactable = isReady;
            bool amountParsed = int.TryParse(_amountField.text, out int amount);
            int balance = isReady
                ? ResourceManager.GetCurrentBalance(ResourceType.Coins)
                : 0;
            bool amountValid =
                amountParsed && amount > 0 && balance <= int.MaxValue - amount;
            _addMoneyButton.interactable = isReady && amountValid;

            if (!isReady)
                _statusText.text = "PlayerData/services ещё не готовы.";
            else if (!amountParsed || amount <= 0)
                _statusText.text = "Amount должен быть больше 0.";
            else if (!amountValid)
                _statusText.text = "Amount переполняет Money balance.";
            else
                _statusText.text = string.IsNullOrWhiteSpace(_resultStatus)
                    ? $"Ready. Balance={balance}."
                    : _resultStatus;
        }

        private void AddMoney()
        {
            if (!int.TryParse(_amountField.text, out int amount))
                return;

            bool added = ResourceManager.TryAddMoneyForDevelopment(
                amount,
                out int newBalance);
            _resultStatus = added
                ? $"PASS: добавлено {amount} Money. Balance={newBalance}."
                : "FAIL: Money не добавлены.";
            RefreshPresentation();
        }

        private void OnAmountChanged(string _)
        {
            _resultStatus = string.Empty;
            RefreshPresentation();
        }
    }
}
#endif
