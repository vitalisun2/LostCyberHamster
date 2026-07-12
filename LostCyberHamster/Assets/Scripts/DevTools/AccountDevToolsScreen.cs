#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using LostCyberHamster.Account;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Assets.Scripts.DevTools
{
    /// <summary>
    /// Строит account-раздел DEV-меню и управляет его навигацией, состоянием представления и диагностическими действиями.
    /// </summary>
    internal sealed class AccountDevToolsScreen
    {
        private const int _accountPage = 0;
        private const int _sessionPage = 1;
        private const int _diagnosticsPage = 2;
        private const int _helpIndexPage = 3;
        private const int _helpDetailPage = 4;
        private const int _confirmationPage = 5;

        private static readonly Color _buttonColor = Color.white;
        private static readonly Color _primaryColor = new Color(0.48f, 0.82f, 1f, 1f);
        private static readonly Color _navigationColor = new Color(0.86f, 0.93f, 1f, 1f);
        private static readonly Color _statusCardColor = new Color(0.93f, 0.96f, 1f, 1f);
        private static readonly Color _dangerColor = new Color(1f, 0.78f, 0.74f, 1f);

        private readonly AccountDevToolsService _service;
        private readonly Action _returnToRoot;
        private readonly Action<string> _setTitle;
        private readonly Font _font;
        private readonly List<Button> _actionButtons = new List<Button>();

        private readonly GameObject _rootObject;
        private readonly RectTransform _rootRect;
        private readonly GameObject _accountPageObject;
        private readonly GameObject _sessionPageObject;
        private readonly GameObject _diagnosticsPageObject;
        private readonly GameObject _helpIndexPageObject;
        private readonly GameObject _helpDetailPageObject;
        private readonly GameObject _confirmationPageObject;

        private Text _humanStatusText;
        private Text _readinessSummaryText;
        private Text _diagnosticsText;
        private Text _lastResultText;
        private Text _sessionResultText;
        private Text _helpDetailText;
        private Text _confirmationText;
        private Button _confirmButton;
        private Button _linkButton;
        private Text _linkButtonText;
        private Func<Task<string>> _pendingConfirmation;
        private int _confirmationReturnPage = _accountPage;
        private int _currentPage;
        private bool _operationInProgress;
        private string _lastResult = string.Empty;

        public AccountDevToolsScreen(
            Transform parent,
            Font font,
            Action returnToRoot,
            Action<string> setTitle)
        {
            _font = font;
            _returnToRoot = returnToRoot;
            _setTitle = setTitle;
            _service = new AccountDevToolsService(AccountServiceProvider.Current);

            _rootObject = CreateUiObject("AccountScreen", parent);
            _rootRect = _rootObject.GetComponent<RectTransform>();
            _accountPageObject = CreateAccountPage(_rootObject.transform);
            _sessionPageObject = CreateSessionPage(_rootObject.transform);
            _diagnosticsPageObject = CreateDiagnosticsPage(_rootObject.transform);
            _helpIndexPageObject = CreateHelpIndexPage(_rootObject.transform);
            _helpDetailPageObject = CreateHelpDetailPage(_rootObject.transform);
            _confirmationPageObject = CreateConfirmationPage(_rootObject.transform);

            ShowPage(_accountPage);
            _rootObject.SetActive(false);
        }

        public GameObject RootObject => _rootObject;

        /// <summary>
        /// Открывает account-экран и сбрасывает вложенную навигацию.
        /// </summary>
        public void Show()
        {
            _rootObject.SetActive(true);
            ShowPage(_accountPage);
            RefreshPresentation();
        }

        /// <summary>
        /// Скрывает все account/help-экраны без прерывания запущенного OAuth flow.
        /// </summary>
        public void Hide()
        {
            _rootObject.SetActive(false);
        }

        /// <summary>
        /// Возвращает на предыдущий смысловой экран: detail → help → account → root.
        /// </summary>
        public void GoBack()
        {
            if (_currentPage == _helpDetailPage)
            {
                ShowPage(_helpIndexPage);
                return;
            }

            if (_currentPage == _helpIndexPage || _currentPage == _confirmationPage)
            {
                if (_currentPage == _confirmationPage)
                {
                    int returnPage = _confirmationReturnPage;
                    CancelConfirmation();
                    ShowPage(returnPage);
                }
                else
                {
                    ShowPage(_accountPage);
                }
                return;
            }

            if (_currentPage == _sessionPage || _currentPage == _diagnosticsPage)
            {
                ShowPage(_accountPage);
                return;
            }

            Hide();
            _returnToRoot();
        }

        /// <summary>
        /// Подгоняет вложенный экран под доступную область общей DEV-панели.
        /// </summary>
        public void ApplyLayout(float left, float top, float right, float bottom)
        {
            _rootRect.anchorMin = Vector2.zero;
            _rootRect.anchorMax = Vector2.one;
            _rootRect.offsetMin = new Vector2(left, bottom);
            _rootRect.offsetMax = new Vector2(-right, -top);
        }

        /// <summary>
        /// Обновляет read-only diagnostics без сетевых или SDK-команд.
        /// </summary>
        public void RefreshPresentation()
        {
            if (_humanStatusText != null)
            {
                _humanStatusText.text = $"Аккаунт: {_service.GetHumanStatusText()}";
            }

            if (_readinessSummaryText != null)
            {
                _readinessSummaryText.text = _service.IsLocallyReadyForPlayerAccounts
                    ? "Готовность: Локально готово"
                    : "Готовность: Нужна настройка — откройте Диагностику";
            }

            if (_diagnosticsText != null)
            {
                _diagnosticsText.text = $"{_service.GetReadinessText()}\n\nТЕКУЩАЯ СЕССИЯ\n{_service.GetSessionText()}";
            }

            if (_lastResultText != null)
            {
                string result = _operationInProgress ? "Выполняется…" : CompactResult(_lastResult);
                _lastResultText.text = $"Последнее действие: {result}";
                _lastResultText.gameObject.SetActive(_operationInProgress || !string.IsNullOrWhiteSpace(_lastResult));
            }

            if (_sessionResultText != null)
            {
                string result = _operationInProgress ? "Выполняется…" : CompactResult(_lastResult);
                _sessionResultText.text = $"Результат: {result}";
                _sessionResultText.gameObject.SetActive(_operationInProgress || !string.IsNullOrWhiteSpace(_lastResult));
            }

            foreach (Button button in _actionButtons)
            {
                if (button != null)
                {
                    button.interactable = !_operationInProgress;
                }
            }

            if (_linkButton != null)
            {
                bool isLinked = _service.Snapshot.IsLinked;
                _linkButton.interactable = !_operationInProgress &&
                                           !isLinked &&
                                           _service.IsLocallyReadyForPlayerAccounts;
                _linkButtonText.text = isLinked ? "АККАУНТ ПРИВЯЗАН" : "ПРИВЯЗАТЬ АККАУНТ";
            }
        }

        private GameObject CreateAccountPage(Transform parent)
        {
            GameObject page = CreateStaticPage("AccountPage", parent, out Transform content);
            CreateSectionHeading("StateHeading", content, "СОСТОЯНИЕ");

            Transform stateCard = CreateCard("StateCard", content, _statusCardColor);
            _humanStatusText = CreateBodyText("HumanStatus", stateCard, string.Empty, FontStyle.Bold);
            _humanStatusText.fontSize = 18;
            _readinessSummaryText = CreateBodyText("ReadinessSummary", stateCard, string.Empty);
            _lastResultText = CreateBodyText("LastResult", stateCard, string.Empty);

            _linkButton = AddActionButton(
                content,
                "LinkAccountButton",
                "ПРИВЯЗАТЬ АККАУНТ",
                LinkAccount,
                _primaryColor,
                40f);
            _linkButtonText = _linkButton.GetComponentInChildren<Text>();
            AddActionButton(content, "RefreshAccountButton", "ОБНОВИТЬ СТАТУС", RefreshAccount);

            CreateNavigationButton(content, "SessionsButton", "УПРАВЛЕНИЕ СЕССИЯМИ", OpenSessionManagement);
            CreateNavigationButton(content, "DiagnosticsButton", "ДИАГНОСТИКА", OpenDiagnostics);
            CreateNavigationButton(content, "HelpButton", "СПРАВКА", OpenHelpIndex);
            return page;
        }

        private GameObject CreateSessionPage(Transform parent)
        {
            GameObject page = CreateScrollPage("SessionPage", parent, out Transform content);
            CreateSectionHeading("NormalActionsHeading", content, "РАСШИРЕННЫЕ ДЕЙСТВИЯ");
            CreateBodyText(
                "AdvancedActionsHint",
                content,
                "Технические команды для ручной проверки сессий. Для обычного игрового flow они не нужны.");
            _sessionResultText = CreateBodyText("SessionResult", content, string.Empty);
            AddActionButton(content, "EnsureSessionButton", "СОЗДАТЬ / ВОССТАНОВИТЬ ГОСТЕВУЮ СЕССИЮ", EnsureSession);
            AddActionButton(content, "SignOutUpaButton", "ВЫЙТИ ИЗ UNITY PLAYER ACCOUNT", SignOutPlayerAccount);
            AddActionButton(content, "SignOutUgsButton", "ЗАВЕРШИТЬ UGS-СЕССИЮ (КЭШ ОСТАВИТЬ)", SignOutUgs);

            Transform dangerCard = CreateCard("DangerousActionsCard", content, new Color(1f, 0.92f, 0.91f, 1f));
            CreateSectionHeading("DangerousActionsHeading", dangerCard, "ОПАСНЫЕ ДЕЙСТВИЯ");
            CreateBodyText(
                "DangerousActionsHint",
                dangerCard,
                "Эти действия могут лишить доступа к identity. Перед выполнением будет отдельное подтверждение.");
            AddActionButton(
                dangerCard,
                "UnlinkAccountButton",
                "ОТВЯЗАТЬ UNITY PLAYER ACCOUNT",
                RequestUnlink,
                _dangerColor);
            AddActionButton(
                dangerCard,
                "ClearIdentityButton",
                "ОЧИСТИТЬ ДАННЫЕ ВХОДА НА УСТРОЙСТВЕ",
                RequestClearIdentity,
                _dangerColor);
            return page;
        }

        private GameObject CreateDiagnosticsPage(Transform parent)
        {
            GameObject page = CreateScrollPage("DiagnosticsPage", parent, out Transform content);
            CreateSectionHeading("ReadinessHeading", content, "ГОТОВНОСТЬ");
            _diagnosticsText = CreateBodyText("DiagnosticsText", content, string.Empty);
            AddActionButton(content, "RefreshDiagnosticsButton", "ОБНОВИТЬ ДИАГНОСТИКУ", RefreshAccount);
            CreateNavigationButton(content, "DashboardButton", "ОТКРЫТЬ UNITY DASHBOARD", _service.OpenDashboard);
            return page;
        }

        private GameObject CreateHelpIndexPage(Transform parent)
        {
            GameObject page = CreateScrollPage("HelpIndexPage", parent, out Transform content);
            CreateBodyText(
                "HelpIntro",
                content,
                "Выбери тему. Каждый раздел открывается внутри этой же панели.",
                FontStyle.Bold);

            for (int index = 0; index < AccountDevToolsHelpContent.SectionCount; index++)
            {
                int capturedIndex = index;
                CreateButton(
                    $"HelpSection{index}",
                    content,
                    AccountDevToolsHelpContent.GetTitle(index).ToUpperInvariant(),
                    _navigationColor,
                    () => OpenHelpDetail(capturedIndex));
            }

            return page;
        }

        private GameObject CreateHelpDetailPage(Transform parent)
        {
            GameObject page = CreateScrollPage("HelpDetailPage", parent, out Transform content);
            _helpDetailText = CreateBodyText("HelpDetail", content, string.Empty);
            return page;
        }

        private GameObject CreateConfirmationPage(Transform parent)
        {
            GameObject page = CreateStaticPage("ConfirmationPage", parent, out Transform content);
            CreateSectionHeading("ConfirmationHeading", content, "ПОДТВЕРЖДЕНИЕ");
            _confirmationText = CreateBodyText("ConfirmationText", content, string.Empty, FontStyle.Bold);

            GameObject actionsRow = new GameObject(
                "ConfirmationActions",
                typeof(RectTransform),
                typeof(HorizontalLayoutGroup),
                typeof(LayoutElement));
            actionsRow.transform.SetParent(content, false);
            HorizontalLayoutGroup actionsLayout = actionsRow.GetComponent<HorizontalLayoutGroup>();
            actionsLayout.spacing = 8f;
            actionsLayout.childControlWidth = true;
            actionsLayout.childControlHeight = true;
            actionsLayout.childForceExpandWidth = true;
            actionsLayout.childForceExpandHeight = false;
            actionsRow.GetComponent<LayoutElement>().preferredHeight = 40f;

            CreateButton("CancelDangerousActionButton", actionsRow.transform, "ОТМЕНА", _buttonColor, CancelConfirmationAndReturn);
            _confirmButton = CreateButton(
                "ConfirmDangerousActionButton",
                actionsRow.transform,
                "ПРОДОЛЖИТЬ",
                _dangerColor,
                ConfirmDangerousAction);
            return page;
        }

        private void EnsureSession()
        {
            _ = RunOperationAsync(async () =>
            {
                AccountSnapshot snapshot = await _service.EnsureSessionAsync();
                return snapshot.IsSignedIn
                    ? $"UGS-сессия готова: {snapshot.State}, PlayerId={snapshot.PlayerId}"
                    : $"UGS-сессия не создана: {snapshot.State}. {snapshot.ErrorMessage}";
            });
        }

        private void RefreshAccount()
        {
            _ = RunOperationAsync(async () =>
            {
                AccountSnapshot snapshot = await _service.RefreshAsync();
                return snapshot.State == AccountState.Error
                    ? $"Статус не обновлён: {snapshot.ErrorMessage}"
                    : $"Статус обновлён: {snapshot.State}, linked={snapshot.IsLinked}";
            });
        }

        private void LinkAccount()
        {
            if (!_service.IsLocallyReadyForPlayerAccounts)
            {
                _lastResult = "Link не запущен: сначала исправь локальный cloudProjectId/clientId по инструкции.";
                RefreshPresentation();
                OpenHelpDetail(3);
                return;
            }

            _ = RunOperationAsync(async () =>
            {
                AccountLinkResult result = await _service.LinkAsync();
                if (result.Status == AccountLinkStatus.AlreadyLinked)
                {
                    return "КОНФЛИКТ: аккаунт уже связан с другим Player ID. Переключение заблокировано; текущая identity сохранена.";
                }

                return result.IsSuccess
                    ? $"Unity Player Account привязан. PlayerId={result.PlayerId}"
                    : $"Привязка не выполнена: {result.ErrorMessage}";
            });
        }

        private void RequestUnlink()
        {
            RequestConfirmation(
                "UNLINK удалит Unity Player Account из способов входа текущего UGS-игрока. " +
                "Если это единственная внешняя identity, после очистки кэша или переустановки доступ к Player ID может быть потерян.",
                async () =>
                {
                    AccountSnapshot snapshot = await _service.UnlinkAsync();
                    return snapshot.State == AccountState.Error || snapshot.IsLinked
                        ? $"Отвязка не выполнена: {snapshot.ErrorMessage}"
                        : $"Unity Player Account отвязан. State={snapshot.State}";
                });
        }

        private void SignOutUgs()
        {
            _ = RunOperationAsync(async () =>
            {
                await _service.SignOutUgsKeepingCredentialsAsync();
                return "UGS-сессия завершена. Cached credentials сохранены; Ensure восстановит тот же Player ID.";
            });
        }

        private void SignOutPlayerAccount()
        {
            try
            {
                _service.SignOutPlayerAccount();
                _lastResult = "Локальная UPA OAuth-сессия очищена. UGS Player ID и link не изменены.";
            }
            catch (Exception ex)
            {
                _lastResult = $"UPA sign out failed: {ex.Message}";
            }

            RefreshPresentation();
        }

        private void RequestClearIdentity()
        {
            RequestConfirmation(
                "Будут удалены локальные UGS credentials. Следующий anonymous sign-in создаст НОВЫЙ Player ID. " +
                "PlayerData этим не очищается: для чистого сценария отдельно используй Reset Progress. Продолжить?",
                async () =>
                {
                    await _service.ClearCachedIdentityAsync();
                    return "Cached UGS identity очищена. Игровые данные оставлены без изменений.";
                });
        }

        private void OpenSessionManagement()
        {
            ShowPage(_sessionPage);
        }

        private void OpenDiagnostics()
        {
            ShowPage(_diagnosticsPage);
        }

        private void OpenHelpIndex()
        {
            ShowPage(_helpIndexPage);
        }

        private void OpenHelpDetail(int index)
        {
            _helpDetailText.text = AccountDevToolsHelpContent.GetText(index);
            ShowPage(_helpDetailPage, AccountDevToolsHelpContent.GetTitle(index));
        }

        private void RequestConfirmation(string warning, Func<Task<string>> action)
        {
            _confirmationReturnPage = _currentPage;
            _pendingConfirmation = action;
            _confirmationText.text = warning;
            ShowPage(_confirmationPage, "Подтверждение");
        }

        private void ConfirmDangerousAction()
        {
            Func<Task<string>> action = _pendingConfirmation;
            int returnPage = _confirmationReturnPage;
            CancelConfirmation();
            ShowPage(returnPage);
            if (action != null)
            {
                _ = RunOperationAsync(action);
            }
        }

        private void CancelConfirmationAndReturn()
        {
            int returnPage = _confirmationReturnPage;
            CancelConfirmation();
            ShowPage(returnPage);
        }

        private void CancelConfirmation()
        {
            _pendingConfirmation = null;
            if (_confirmButton != null)
            {
                _confirmButton.interactable = true;
            }
        }

        private async Task RunOperationAsync(Func<Task<string>> operation)
        {
            if (_operationInProgress)
            {
                return;
            }

            _operationInProgress = true;
            _lastResult = "Операция выполняется...";
            RefreshPresentation();

            try
            {
                _lastResult = await operation();
            }
            catch (Exception ex)
            {
                _lastResult = $"Ошибка: {ex.Message}";
            }
            finally
            {
                _operationInProgress = false;
                RefreshPresentation();
            }
        }

        private void ShowPage(int page, string detailTitle = null)
        {
            _currentPage = page;
            _accountPageObject.SetActive(page == _accountPage);
            _sessionPageObject.SetActive(page == _sessionPage);
            _diagnosticsPageObject.SetActive(page == _diagnosticsPage);
            _helpIndexPageObject.SetActive(page == _helpIndexPage);
            _helpDetailPageObject.SetActive(page == _helpDetailPage);
            _confirmationPageObject.SetActive(page == _confirmationPage);

            if (page == _accountPage)
            {
                _setTitle("Аккаунт");
            }
            else if (page == _helpIndexPage)
            {
                _setTitle("Справка");
            }
            else if (page == _sessionPage)
            {
                _setTitle("Управление сессиями");
            }
            else if (page == _diagnosticsPage)
            {
                _setTitle("Диагностика");
            }
            else
            {
                _setTitle(detailTitle ?? "Аккаунт");
            }
        }

        private Button AddActionButton(
            Transform parent,
            string name,
            string label,
            UnityAction action,
            Color? color = null,
            float height = 38f)
        {
            Button button = CreateButton(name, parent, label, color ?? _buttonColor, action);
            button.GetComponent<LayoutElement>().preferredHeight = height;
            _actionButtons.Add(button);
            return button;
        }

        private Button CreateNavigationButton(
            Transform parent,
            string name,
            string label,
            UnityAction action)
        {
            return CreateButton(name, parent, label, _navigationColor, action);
        }

        private GameObject CreateStaticPage(string name, Transform parent, out Transform content)
        {
            GameObject page = CreateUiObject(name, parent);
            SetStretch(page.GetComponent<RectTransform>());

            GameObject contentObject = new GameObject(
                "Content",
                typeof(RectTransform),
                typeof(VerticalLayoutGroup));
            contentObject.transform.SetParent(page.transform, false);
            SetStretch(contentObject.GetComponent<RectTransform>());
            ConfigureVerticalLayout(contentObject.GetComponent<VerticalLayoutGroup>());
            content = contentObject.transform;
            return page;
        }

        private GameObject CreateScrollPage(string name, Transform parent, out Transform content)
        {
            GameObject page = CreateUiObject(name, parent);
            SetStretch(page.GetComponent<RectTransform>());

            GameObject viewport = new GameObject(
                "Viewport",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Mask));
            viewport.transform.SetParent(page.transform, false);
            SetStretch(viewport.GetComponent<RectTransform>());
            viewport.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.01f);
            viewport.GetComponent<Mask>().showMaskGraphic = false;

            GameObject contentObject = new GameObject(
                "Content",
                typeof(RectTransform),
                typeof(VerticalLayoutGroup),
                typeof(ContentSizeFitter));
            contentObject.transform.SetParent(viewport.transform, false);
            RectTransform contentRect = contentObject.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.sizeDelta = Vector2.zero;

            VerticalLayoutGroup layout = contentObject.GetComponent<VerticalLayoutGroup>();
            ConfigureVerticalLayout(layout);

            ContentSizeFitter fitter = contentObject.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            ScrollRect scrollRect = page.AddComponent<ScrollRect>();
            scrollRect.viewport = viewport.GetComponent<RectTransform>();
            scrollRect.content = contentRect;
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.scrollSensitivity = 22f;
            content = contentObject.transform;
            return page;
        }

        private Transform CreateCard(string name, Transform parent, Color color)
        {
            GameObject card = new GameObject(
                name,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(VerticalLayoutGroup),
                typeof(ContentSizeFitter));
            card.transform.SetParent(parent, false);
            card.GetComponent<Image>().color = color;

            VerticalLayoutGroup layout = card.GetComponent<VerticalLayoutGroup>();
            ConfigureVerticalLayout(layout);
            layout.padding = new RectOffset(12, 12, 10, 10);
            layout.spacing = 5f;

            ContentSizeFitter fitter = card.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return card.transform;
        }

        private Text CreateSectionHeading(string name, Transform parent, string text)
        {
            Text heading = CreateBodyText(name, parent, text, FontStyle.Bold);
            heading.fontSize = 15;
            return heading;
        }

        private static void ConfigureVerticalLayout(VerticalLayoutGroup layout)
        {
            layout.padding = new RectOffset(2, 4, 2, 8);
            layout.spacing = 8f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
        }

        private Button CreateButton(
            string name,
            Transform parent,
            string label,
            Color color,
            UnityAction action)
        {
            GameObject buttonObject = new GameObject(
                name,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Button),
                typeof(LayoutElement));
            buttonObject.transform.SetParent(parent, false);
            buttonObject.GetComponent<Image>().color = color;
            LayoutElement buttonLayout = buttonObject.GetComponent<LayoutElement>();
            buttonLayout.preferredHeight = 38f;
            buttonLayout.flexibleWidth = 1f;

            Button button = buttonObject.GetComponent<Button>();
            button.targetGraphic = buttonObject.GetComponent<Image>();
            button.onClick.AddListener(action);

            Text text = CreateText("Text", buttonObject.transform, label, TextAnchor.MiddleCenter, FontStyle.Bold);
            text.fontSize = 15;
            SetStretch(text.GetComponent<RectTransform>());
            return button;
        }

        private Text CreateBodyText(string name, Transform parent, string text, FontStyle style = FontStyle.Normal)
        {
            Text body = CreateText(name, parent, text, TextAnchor.UpperLeft, style);
            ContentSizeFitter fitter = body.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            body.horizontalOverflow = HorizontalWrapMode.Wrap;
            body.verticalOverflow = VerticalWrapMode.Overflow;
            return body;
        }

        private Text CreateText(
            string name,
            Transform parent,
            string text,
            TextAnchor anchor,
            FontStyle style)
        {
            GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            textObject.transform.SetParent(parent, false);
            Text uiText = textObject.GetComponent<Text>();
            uiText.text = text;
            uiText.font = _font;
            uiText.fontSize = 14;
            uiText.fontStyle = style;
            uiText.alignment = anchor;
            uiText.color = Color.black;
            uiText.raycastTarget = false;
            return uiText;
        }

        private static GameObject CreateUiObject(string name, Transform parent)
        {
            GameObject uiObject = new GameObject(name, typeof(RectTransform));
            uiObject.transform.SetParent(parent, false);
            return uiObject;
        }

        private static void SetStretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
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
