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
    /// Управляет вложенными account/help-экранами внутри существующей панели DEV-меню.
    /// </summary>
    internal sealed class AccountDevToolsScreen
    {
        private const int _accountPage = 0;
        private const int _helpIndexPage = 1;
        private const int _helpDetailPage = 2;
        private const int _confirmationPage = 3;

        private static readonly Color _buttonColor = Color.white;
        private static readonly Color _warningColor = new Color(1f, 0.84f, 0.62f, 1f);
        private static readonly Color _dangerColor = new Color(1f, 0.72f, 0.68f, 1f);

        private readonly AccountDevToolsService _service;
        private readonly Action _returnToRoot;
        private readonly Action<string> _setTitle;
        private readonly Font _font;
        private readonly List<Button> _actionButtons = new List<Button>();

        private readonly GameObject _rootObject;
        private readonly RectTransform _rootRect;
        private readonly GameObject _accountPageObject;
        private readonly GameObject _helpIndexPageObject;
        private readonly GameObject _helpDetailPageObject;
        private readonly GameObject _confirmationPageObject;

        private Text _readinessText;
        private Text _sessionText;
        private Text _lastResultText;
        private Text _helpDetailText;
        private Text _confirmationText;
        private Button _confirmButton;
        private Func<Task<string>> _pendingConfirmation;
        private int _currentPage;
        private bool _operationInProgress;
        private string _lastResult = "Действия ещё не выполнялись.";

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
                CancelConfirmation();
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
            if (_readinessText != null)
            {
                _readinessText.text = _service.GetReadinessText();
            }

            if (_sessionText != null)
            {
                _sessionText.text = _service.GetSessionText();
            }

            if (_lastResultText != null)
            {
                _lastResultText.text = $"Последний результат:\n{_lastResult}";
            }

            foreach (Button button in _actionButtons)
            {
                if (button != null)
                {
                    button.interactable = !_operationInProgress;
                }
            }
        }

        private GameObject CreateAccountPage(Transform parent)
        {
            GameObject page = CreateScrollPage("AccountPage", parent, out Transform content);
            _readinessText = CreateBodyText("Readiness", content, string.Empty, FontStyle.Bold);
            _sessionText = CreateBodyText("SessionState", content, string.Empty);
            _lastResultText = CreateBodyText("LastResult", content, string.Empty);

            AddActionButton(content, "EnsureSessionButton", "Обеспечить UGS-сессию", EnsureSession);
            AddActionButton(content, "RefreshAccountButton", "Обновить статус", RefreshAccount);
            AddActionButton(content, "LinkAccountButton", "Привязать Unity Player Account", LinkAccount);
            AddActionButton(content, "UnlinkAccountButton", "Отвязать Unity Player Account", RequestUnlink, _dangerColor);
            AddActionButton(content, "SignOutUgsButton", "Выйти из UGS (кэш оставить)", SignOutUgs);
            AddActionButton(content, "SignOutUpaButton", "Выйти из UPA OAuth", SignOutPlayerAccount);
            AddActionButton(content, "ClearIdentityButton", "Очистить cached identity", RequestClearIdentity, _dangerColor);
            AddActionButton(content, "PreparationButton", "Подготовка авторизации", OpenEditorPreparation, _warningColor);
            AddActionButton(content, "DashboardButton", "Открыть Unity Dashboard", _service.OpenDashboard);
            AddActionButton(content, "HelpButton", "Справка", OpenHelpIndex);
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
                    AccountDevToolsHelpContent.GetTitle(index),
                    _buttonColor,
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
            GameObject page = CreateScrollPage("ConfirmationPage", parent, out Transform content);
            _confirmationText = CreateBodyText("ConfirmationText", content, string.Empty, FontStyle.Bold);
            _confirmButton = CreateButton(
                "ConfirmDangerousActionButton",
                content,
                "ПОДТВЕРДИТЬ",
                _dangerColor,
                ConfirmDangerousAction);
            CreateButton("CancelDangerousActionButton", content, "Отмена", _buttonColor, CancelConfirmationAndReturn);
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
                OpenHelpDetail(4);
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

        private void OpenEditorPreparation()
        {
            OpenHelpDetail(4);
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
            _pendingConfirmation = action;
            _confirmationText.text = warning;
            ShowPage(_confirmationPage, "Требуется подтверждение");
        }

        private void ConfirmDangerousAction()
        {
            Func<Task<string>> action = _pendingConfirmation;
            CancelConfirmation();
            ShowPage(_accountPage);
            if (action != null)
            {
                _ = RunOperationAsync(action);
            }
        }

        private void CancelConfirmationAndReturn()
        {
            CancelConfirmation();
            ShowPage(_accountPage);
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
            else
            {
                _setTitle(detailTitle ?? "Аккаунт");
            }
        }

        private void AddActionButton(
            Transform parent,
            string name,
            string label,
            UnityAction action,
            Color? color = null)
        {
            Button button = CreateButton(name, parent, label, color ?? _buttonColor, action);
            _actionButtons.Add(button);
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
            layout.padding = new RectOffset(4, 8, 4, 12);
            layout.spacing = 7f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

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
            buttonObject.GetComponent<LayoutElement>().preferredHeight = 36f;

            Button button = buttonObject.GetComponent<Button>();
            button.targetGraphic = buttonObject.GetComponent<Image>();
            button.onClick.AddListener(action);

            Text text = CreateText("Text", buttonObject.transform, label, TextAnchor.MiddleCenter, FontStyle.Bold);
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
    }
}
#endif
