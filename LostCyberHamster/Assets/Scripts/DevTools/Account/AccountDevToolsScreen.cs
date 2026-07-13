#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Threading.Tasks;
using Assets.Scripts.DevTools.Core;
using LostCyberHamster.Account;
using UnityEngine;

namespace Assets.Scripts.DevTools.Account
{
    /// <summary>
    /// Координирует lifecycle, навигацию и связи компонентов account-раздела DEV-меню.
    /// </summary>
    internal sealed class AccountDevToolsScreen : IDevToolsScreen
    {
        private const string _unlinkWarning =
            "UNLINK удалит Unity Player Account из способов входа текущего UGS-игрока. " +
            "Если это единственная внешняя identity, после очистки кэша или переустановки доступ к Player ID может быть потерян.";

        private const string _clearIdentityWarning =
            "Будут удалены локальные UGS credentials. Следующий anonymous sign-in создаст НОВЫЙ Player ID. " +
            "PlayerData этим не очищается: для чистого сценария отдельно используй Reset Progress. Продолжить?";

        private readonly Action _returnToRoot;
        private readonly AccountDevToolsController _controller;
        private readonly AccountDevToolsView _view;
        private readonly DevToolsNavigation<AccountDevToolsPage> _navigation =
            new DevToolsNavigation<AccountDevToolsPage>();
        private readonly AccountDevToolsConfirmationState _confirmation =
            new AccountDevToolsConfirmationState();

        private string _detailTitle;

        /// <summary>
        /// Собирает account screen, связывает controller с view и настраивает начальную навигацию.
        /// </summary>
        public AccountDevToolsScreen(
            Transform parent,
            Font font,
            Action returnToRoot,
            Action<string> setTitle)
        {
            _returnToRoot = returnToRoot;
            AccountDevToolsService service = new AccountDevToolsService(AccountServiceProvider.Current);
            _controller = new AccountDevToolsController(service);
            _view = new AccountDevToolsView(parent, font, setTitle);
            WireEvents();
            _navigation.Reset(AccountDevToolsPage.Account);
        }

        public GameObject RootObject => _view.RootObject;

        /// <summary>
        /// Открывает account screen с основной страницы и обновляет presentation-состояние.
        /// </summary>
        public void Show()
        {
            _confirmation.Cancel();
            _navigation.Reset(AccountDevToolsPage.Account);
            _view.SetVisible(true);
            RenderCurrentPage();
            RefreshPresentation();
        }

        /// <summary>
        /// Скрывает account screen.
        /// </summary>
        public void Hide()
        {
            _view.SetVisible(false);
        }

        /// <summary>
        /// Возвращает на предыдущую account-страницу или в корневое DEV-меню.
        /// </summary>
        public void GoBack()
        {
            if (_navigation.Current == AccountDevToolsPage.Confirmation)
            {
                _confirmation.Cancel();
            }

            if (_navigation.TryGoBack(out AccountDevToolsPage page))
            {
                RenderPage(page);
                return;
            }

            Hide();
            _returnToRoot();
        }

        /// <summary>
        /// Применяет внутренние отступы feature-панели к account view.
        /// </summary>
        public void ApplyLayout(float left, float top, float right, float bottom)
        {
            _view.ApplyLayout(left, top, right, bottom);
        }

        /// <summary>
        /// Перерисовывает account view из текущего controller state.
        /// </summary>
        public void RefreshPresentation()
        {
            _view.Render(_controller.GetViewState());
        }

        private void WireEvents()
        {
            _controller.PresentationChanged += RefreshPresentation;
            _view.LinkRequested += LinkAccount;
            _view.RefreshRequested += () => _ = _controller.RefreshAsync();
            _view.SessionsRequested += () => NavigateTo(AccountDevToolsPage.Sessions);
            _view.DiagnosticsRequested += () => NavigateTo(AccountDevToolsPage.Diagnostics);
            _view.HelpRequested += () => NavigateTo(AccountDevToolsPage.HelpIndex);
            _view.EnsureSessionRequested += () => _ = _controller.EnsureSessionAsync();
            _view.SignOutUpaRequested += _controller.SignOutPlayerAccount;
            _view.SignOutUgsRequested += () => _ = _controller.SignOutUgsAsync();
            _view.UnlinkRequested += RequestUnlink;
            _view.ClearIdentityRequested += RequestClearIdentity;
            _view.DashboardRequested += _controller.OpenDashboard;
            _view.HelpSectionRequested += OpenHelpDetail;
            _view.ConfirmRequested += ConfirmDangerousAction;
            _view.CancelRequested += GoBack;
        }

        private void LinkAccount()
        {
            if (_controller.IsLocallyReady)
            {
                _ = _controller.LinkAsync();
                return;
            }

            _controller.ReportMissingConfiguration();
            OpenHelpDetail(AccountDevToolsHelpContent.EditorSetupSectionIndex);
        }

        private void OpenHelpDetail(int index)
        {
            _detailTitle = AccountDevToolsHelpContent.GetTitle(index);
            _view.SetHelpDetail(AccountDevToolsHelpContent.GetText(index));
            NavigateTo(AccountDevToolsPage.HelpDetail);
        }

        private void RequestUnlink()
        {
            RequestConfirmation(_unlinkWarning, _controller.UnlinkAsync);
        }

        private void RequestClearIdentity()
        {
            RequestConfirmation(_clearIdentityWarning, _controller.ClearCachedIdentityAsync);
        }

        private void RequestConfirmation(string warning, Func<Task> action)
        {
            _confirmation.Request(warning, action);
            _view.SetConfirmationText(_confirmation.Warning);
            NavigateTo(AccountDevToolsPage.Confirmation);
        }

        private void ConfirmDangerousAction()
        {
            if (!_confirmation.TryConsume(out Func<Task> action))
            {
                return;
            }

            if (_navigation.TryGoBack(out AccountDevToolsPage page))
            {
                RenderPage(page);
            }

            _ = action();
        }

        private void NavigateTo(AccountDevToolsPage page)
        {
            _navigation.NavigateTo(page);
            RenderPage(page);
        }

        private void RenderCurrentPage()
        {
            RenderPage(_navigation.Current);
        }

        private void RenderPage(AccountDevToolsPage page)
        {
            _view.SetPage(page, _detailTitle);
            RefreshPresentation();
        }
    }
}
#endif
