#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using Assets.Scripts.DevTools.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Scripts.DevTools.Account
{
    /// <summary>
    /// Строит account-страницы DEV-меню, отображает presentation state и публикует пользовательский input.
    /// </summary>
    internal sealed class AccountDevToolsView
    {
        private readonly Action<string> _setTitle;
        private readonly DevToolsUiFactory _ui;
        private readonly List<Button> _actionButtons = new List<Button>();
        private readonly GameObject _rootObject;
        private readonly RectTransform _rootRect;
        private readonly Dictionary<AccountDevToolsPage, GameObject> _pages =
            new Dictionary<AccountDevToolsPage, GameObject>();

        private Text _humanStatusText;
        private Text _readinessSummaryText;
        private Text _diagnosticsText;
        private Text _lastResultText;
        private Text _sessionResultText;
        private Text _helpDetailText;
        private Text _confirmationText;
        private Button _linkButton;
        private Text _linkButtonText;

        /// <summary>
        /// Создаёт все account-страницы и размещает их внутри общей DEV-панели.
        /// </summary>
        public AccountDevToolsView(Transform parent, Font font, Action<string> setTitle)
        {
            _setTitle = setTitle;
            _ui = new DevToolsUiFactory(font);
            _rootObject = _ui.CreateUiObject("AccountScreen", parent);
            _rootRect = _rootObject.GetComponent<RectTransform>();
            _pages.Add(AccountDevToolsPage.Account, CreateAccountPage());
            _pages.Add(AccountDevToolsPage.Sessions, CreateSessionPage());
            _pages.Add(AccountDevToolsPage.Diagnostics, CreateDiagnosticsPage());
            _pages.Add(AccountDevToolsPage.HelpIndex, CreateHelpIndexPage());
            _pages.Add(AccountDevToolsPage.HelpDetail, CreateHelpDetailPage());
            _pages.Add(AccountDevToolsPage.Confirmation, CreateConfirmationPage());
            SetPage(AccountDevToolsPage.Account);
            SetVisible(false);
        }

        public event Action LinkRequested;
        public event Action RefreshRequested;
        public event Action SessionsRequested;
        public event Action DiagnosticsRequested;
        public event Action HelpRequested;
        public event Action EnsureSessionRequested;
        public event Action SignOutUpaRequested;
        public event Action SignOutUgsRequested;
        public event Action UnlinkRequested;
        public event Action ClearIdentityRequested;
        public event Action DashboardRequested;
        public event Action<int> HelpSectionRequested;
        public event Action ConfirmRequested;
        public event Action CancelRequested;

        public GameObject RootObject => _rootObject;

        /// <summary>
        /// Показывает или скрывает корневой объект account view.
        /// </summary>
        public void SetVisible(bool visible)
        {
            _rootObject.SetActive(visible);
        }

        /// <summary>
        /// Применяет отступы account view внутри feature-панели.
        /// </summary>
        public void ApplyLayout(float left, float top, float right, float bottom)
        {
            _rootRect.anchorMin = Vector2.zero;
            _rootRect.anchorMax = Vector2.one;
            _rootRect.offsetMin = new Vector2(left, bottom);
            _rootRect.offsetMax = new Vector2(-right, -top);
        }

        /// <summary>
        /// Активирует выбранную account-страницу и обновляет заголовок общей панели.
        /// </summary>
        public void SetPage(AccountDevToolsPage page, string detailTitle = null)
        {
            foreach (KeyValuePair<AccountDevToolsPage, GameObject> item in _pages)
            {
                item.Value.SetActive(item.Key == page);
            }

            _setTitle(GetPageTitle(page, detailTitle));
        }

        /// <summary>
        /// Заполняет текст выбранного раздела справки.
        /// </summary>
        public void SetHelpDetail(string text)
        {
            _helpDetailText.text = text ?? string.Empty;
        }

        /// <summary>
        /// Заполняет предупреждение на странице подтверждения опасного действия.
        /// </summary>
        public void SetConfirmationText(string text)
        {
            _confirmationText.text = text ?? string.Empty;
        }

        /// <summary>
        /// Отображает account-state, результаты операций и доступность действий.
        /// </summary>
        public void Render(AccountDevToolsViewState state)
        {
            _humanStatusText.text = $"Аккаунт: {state.HumanStatus}";
            _readinessSummaryText.text = state.IsLocallyReady
                ? "Готовность: Локально готово"
                : "Готовность: Нужна настройка — откройте Диагностику";
            _diagnosticsText.text = state.Diagnostics;

            string result = state.IsBusy ? "Выполняется…" : CompactResult(state.LastResult);
            _lastResultText.text = $"Последнее действие: {result}";
            _lastResultText.gameObject.SetActive(state.HasResult);
            _sessionResultText.text = $"Результат: {result}";
            _sessionResultText.gameObject.SetActive(state.HasResult);

            foreach (Button button in _actionButtons)
            {
                if (button != null)
                {
                    button.interactable = !state.IsBusy;
                }
            }

            _linkButton.interactable = !state.IsBusy && !state.IsLinked && state.IsLocallyReady;
            _linkButtonText.text = state.IsLinked ? "АККАУНТ ПРИВЯЗАН" : "ПРИВЯЗАТЬ АККАУНТ";
        }

        private GameObject CreateAccountPage()
        {
            GameObject page = _ui.CreateStaticPage("AccountPage", _rootObject.transform, out Transform content);
            _ui.CreateSectionHeading("StateHeading", content, "СОСТОЯНИЕ");

            Transform stateCard = _ui.CreateCard("StateCard", content, DevToolsTheme.StatusCard);
            _humanStatusText = _ui.CreateBodyText("HumanStatus", stateCard, string.Empty, FontStyle.Bold);
            _humanStatusText.fontSize = 18;
            _readinessSummaryText = _ui.CreateBodyText("ReadinessSummary", stateCard, string.Empty);
            _lastResultText = _ui.CreateBodyText("LastResult", stateCard, string.Empty);

            _linkButton = AddActionButton(
                content,
                "LinkAccountButton",
                "ПРИВЯЗАТЬ АККАУНТ",
                () => LinkRequested?.Invoke(),
                DevToolsTheme.Primary,
                DevToolsTheme.PrimaryButtonHeight);
            _linkButtonText = _linkButton.GetComponentInChildren<Text>();
            AddActionButton(content, "RefreshAccountButton", "ОБНОВИТЬ СТАТУС", () => RefreshRequested?.Invoke());
            CreateNavigationButton(content, "SessionsButton", "УПРАВЛЕНИЕ СЕССИЯМИ", () => SessionsRequested?.Invoke());
            CreateNavigationButton(content, "DiagnosticsButton", "ДИАГНОСТИКА", () => DiagnosticsRequested?.Invoke());
            CreateNavigationButton(content, "HelpButton", "СПРАВКА", () => HelpRequested?.Invoke());
            return page;
        }

        private GameObject CreateSessionPage()
        {
            GameObject page = _ui.CreateScrollPage("SessionPage", _rootObject.transform, out Transform content);
            _ui.CreateSectionHeading("NormalActionsHeading", content, "РАСШИРЕННЫЕ ДЕЙСТВИЯ");
            _ui.CreateBodyText(
                "AdvancedActionsHint",
                content,
                "Технические команды для ручной проверки сессий. Для обычного игрового flow они не нужны.");
            _sessionResultText = _ui.CreateBodyText("SessionResult", content, string.Empty);
            AddActionButton(
                content,
                "EnsureSessionButton",
                "СОЗДАТЬ / ВОССТАНОВИТЬ ГОСТЕВУЮ СЕССИЮ",
                () => EnsureSessionRequested?.Invoke());
            AddActionButton(
                content,
                "SignOutUpaButton",
                "ВЫЙТИ ИЗ UNITY PLAYER ACCOUNT",
                () => SignOutUpaRequested?.Invoke());
            AddActionButton(
                content,
                "SignOutUgsButton",
                "ЗАВЕРШИТЬ UGS-СЕССИЮ (КЭШ ОСТАВИТЬ)",
                () => SignOutUgsRequested?.Invoke());

            Transform dangerCard = _ui.CreateCard("DangerousActionsCard", content, DevToolsTheme.DangerCard);
            _ui.CreateSectionHeading("DangerousActionsHeading", dangerCard, "ОПАСНЫЕ ДЕЙСТВИЯ");
            _ui.CreateBodyText(
                "DangerousActionsHint",
                dangerCard,
                "Эти действия могут лишить доступа к identity. Перед выполнением будет отдельное подтверждение.");
            AddActionButton(
                dangerCard,
                "UnlinkAccountButton",
                "ОТВЯЗАТЬ UNITY PLAYER ACCOUNT",
                () => UnlinkRequested?.Invoke(),
                DevToolsTheme.Danger);
            AddActionButton(
                dangerCard,
                "ClearIdentityButton",
                "ОЧИСТИТЬ ДАННЫЕ ВХОДА НА УСТРОЙСТВЕ",
                () => ClearIdentityRequested?.Invoke(),
                DevToolsTheme.Danger);
            return page;
        }

        private GameObject CreateDiagnosticsPage()
        {
            GameObject page = _ui.CreateScrollPage("DiagnosticsPage", _rootObject.transform, out Transform content);
            _ui.CreateSectionHeading("ReadinessHeading", content, "ГОТОВНОСТЬ");
            _diagnosticsText = _ui.CreateBodyText("DiagnosticsText", content, string.Empty);
            AddActionButton(
                content,
                "RefreshDiagnosticsButton",
                "ОБНОВИТЬ ДИАГНОСТИКУ",
                () => RefreshRequested?.Invoke());
            CreateNavigationButton(
                content,
                "DashboardButton",
                "ОТКРЫТЬ UNITY DASHBOARD",
                () => DashboardRequested?.Invoke());
            return page;
        }

        private GameObject CreateHelpIndexPage()
        {
            GameObject page = _ui.CreateScrollPage("HelpIndexPage", _rootObject.transform, out Transform content);
            _ui.CreateBodyText(
                "HelpIntro",
                content,
                "Выбери тему. Каждый раздел открывается внутри этой же панели.",
                FontStyle.Bold);

            for (int index = 0; index < AccountDevToolsHelpContent.SectionCount; index++)
            {
                int capturedIndex = index;
                CreateNavigationButton(
                    content,
                    $"HelpSection{index}",
                    AccountDevToolsHelpContent.GetTitle(index).ToUpperInvariant(),
                    () => HelpSectionRequested?.Invoke(capturedIndex));
            }

            return page;
        }

        private GameObject CreateHelpDetailPage()
        {
            GameObject page = _ui.CreateScrollPage("HelpDetailPage", _rootObject.transform, out Transform content);
            _helpDetailText = _ui.CreateBodyText("HelpDetail", content, string.Empty);
            return page;
        }

        private GameObject CreateConfirmationPage()
        {
            GameObject page = _ui.CreateStaticPage("ConfirmationPage", _rootObject.transform, out Transform content);
            _ui.CreateSectionHeading("ConfirmationHeading", content, "ПОДТВЕРЖДЕНИЕ");
            _confirmationText = _ui.CreateBodyText("ConfirmationText", content, string.Empty, FontStyle.Bold);

            GameObject actionsRow = new GameObject(
                "ConfirmationActions",
                typeof(RectTransform),
                typeof(HorizontalLayoutGroup),
                typeof(LayoutElement));
            actionsRow.transform.SetParent(content, false);
            HorizontalLayoutGroup actionsLayout = actionsRow.GetComponent<HorizontalLayoutGroup>();
            actionsLayout.spacing = DevToolsTheme.ContentSpacing;
            actionsLayout.childControlWidth = true;
            actionsLayout.childControlHeight = true;
            actionsLayout.childForceExpandWidth = true;
            actionsLayout.childForceExpandHeight = false;
            actionsRow.GetComponent<LayoutElement>().preferredHeight = DevToolsTheme.PrimaryButtonHeight;

            _ui.CreateButton(
                "CancelDangerousActionButton",
                actionsRow.transform,
                "ОТМЕНА",
                DevToolsTheme.Button,
                () => CancelRequested?.Invoke());
            _ui.CreateButton(
                "ConfirmDangerousActionButton",
                actionsRow.transform,
                "ПРОДОЛЖИТЬ",
                DevToolsTheme.Danger,
                () => ConfirmRequested?.Invoke());
            return page;
        }

        private Button AddActionButton(
            Transform parent,
            string name,
            string label,
            UnityEngine.Events.UnityAction action,
            Color? color = null,
            float height = DevToolsTheme.ButtonHeight)
        {
            Button button = _ui.CreateButton(name, parent, label, color ?? DevToolsTheme.Button, action, height);
            _actionButtons.Add(button);
            return button;
        }

        private Button CreateNavigationButton(
            Transform parent,
            string name,
            string label,
            UnityEngine.Events.UnityAction action)
        {
            return _ui.CreateButton(name, parent, label, DevToolsTheme.Navigation, action);
        }

        private static string GetPageTitle(AccountDevToolsPage page, string detailTitle)
        {
            switch (page)
            {
                case AccountDevToolsPage.Account:
                    return "Аккаунт";
                case AccountDevToolsPage.Sessions:
                    return "Управление сессиями";
                case AccountDevToolsPage.Diagnostics:
                    return "Диагностика";
                case AccountDevToolsPage.HelpIndex:
                    return "Справка";
                case AccountDevToolsPage.Confirmation:
                    return "Подтверждение";
                default:
                    return detailTitle ?? "Аккаунт";
            }
        }

        private static string CompactResult(string result)
        {
            if (string.IsNullOrWhiteSpace(result))
            {
                return "—";
            }

            string compact = result.Replace('\r', ' ').Replace('\n', ' ').Trim();
            return compact.Length <= 120 ? compact : $"{compact.Substring(0, 117)}…";
        }
    }
}
#endif
