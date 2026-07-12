#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Threading.Tasks;
using LostCyberHamster.Account;

namespace Assets.Scripts.DevTools.Account
{
    /// <summary>
    /// Выполняет async account-команды DEV-меню и публикует готовое presentation-состояние.
    /// </summary>
    internal sealed class AccountDevToolsController
    {
        private readonly AccountDevToolsService _service;
        private bool _isBusy;
        private string _lastResult = string.Empty;

        public AccountDevToolsController(AccountDevToolsService service)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
        }

        public event Action PresentationChanged;

        public bool IsLocallyReady => _service.IsLocallyReadyForPlayerAccounts;

        public AccountDevToolsViewState GetViewState()
        {
            return new AccountDevToolsViewState(
                _service.GetHumanStatusText(),
                $"{_service.GetReadinessText()}\n\nТЕКУЩАЯ СЕССИЯ\n{_service.GetSessionText()}",
                _lastResult,
                _isBusy,
                _service.Snapshot.IsLinked,
                _service.IsLocallyReadyForPlayerAccounts);
        }

        public Task EnsureSessionAsync()
        {
            return RunOperationAsync(async () =>
            {
                AccountSnapshot snapshot = await _service.EnsureSessionAsync();
                return snapshot.IsSignedIn
                    ? $"UGS-сессия готова: {snapshot.State}, PlayerId={snapshot.PlayerId}"
                    : $"UGS-сессия не создана: {snapshot.State}. {snapshot.ErrorMessage}";
            });
        }

        public Task RefreshAsync()
        {
            return RunOperationAsync(async () =>
            {
                AccountSnapshot snapshot = await _service.RefreshAsync();
                return snapshot.State == AccountState.Error
                    ? $"Статус не обновлён: {snapshot.ErrorMessage}"
                    : $"Статус обновлён: {snapshot.State}, linked={snapshot.IsLinked}";
            });
        }

        public Task LinkAsync()
        {
            return RunOperationAsync(async () =>
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

        public Task UnlinkAsync()
        {
            return RunOperationAsync(async () =>
            {
                AccountSnapshot snapshot = await _service.UnlinkAsync();
                return snapshot.State == AccountState.Error || snapshot.IsLinked
                    ? $"Отвязка не выполнена: {snapshot.ErrorMessage}"
                    : $"Unity Player Account отвязан. State={snapshot.State}";
            });
        }

        public Task SignOutUgsAsync()
        {
            return RunOperationAsync(async () =>
            {
                await _service.SignOutUgsKeepingCredentialsAsync();
                return "UGS-сессия завершена. Cached credentials сохранены; Ensure восстановит тот же Player ID.";
            });
        }

        public void SignOutPlayerAccount()
        {
            if (_isBusy)
            {
                return;
            }

            try
            {
                _service.SignOutPlayerAccount();
                _lastResult = "Локальная UPA OAuth-сессия очищена. UGS Player ID и link не изменены.";
            }
            catch (Exception ex)
            {
                _lastResult = $"UPA sign out failed: {ex.Message}";
            }

            PresentationChanged?.Invoke();
        }

        public Task ClearCachedIdentityAsync()
        {
            return RunOperationAsync(async () =>
            {
                await _service.ClearCachedIdentityAsync();
                return "Cached UGS identity очищена. Игровые данные оставлены без изменений.";
            });
        }

        public void ReportMissingConfiguration()
        {
            _lastResult = "Link не запущен: сначала исправь локальный cloudProjectId/clientId по инструкции.";
            PresentationChanged?.Invoke();
        }

        public void OpenDashboard()
        {
            _service.OpenDashboard();
        }

        private async Task RunOperationAsync(Func<Task<string>> operation)
        {
            if (_isBusy)
            {
                return;
            }

            _isBusy = true;
            PresentationChanged?.Invoke();

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
                _isBusy = false;
                PresentationChanged?.Invoke();
            }
        }
    }
}
#endif
