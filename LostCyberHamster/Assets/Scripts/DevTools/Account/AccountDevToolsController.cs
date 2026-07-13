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
        private readonly IAccountDevToolsService _service;
        private bool _isBusy;
        private string _lastResult = string.Empty;
        private string _lastTechnicalDetails = string.Empty;

        /// <summary>
        /// Создаёт controller поверх заменяемого сервиса account DEV-инструментов.
        /// </summary>
        public AccountDevToolsController(IAccountDevToolsService service)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
        }

        public event Action PresentationChanged;

        public bool IsLocallyReady => _service.IsLocallyReadyForPlayerAccounts;

        /// <summary>
        /// Формирует текущее пользовательское и диагностическое presentation-состояние.
        /// </summary>
        public AccountDevToolsViewState GetViewState()
        {
            string operationDetails = string.IsNullOrWhiteSpace(_lastTechnicalDetails)
                ? "—"
                : _lastTechnicalDetails;

            return new AccountDevToolsViewState(
                _service.GetHumanStatusText(),
                $"{_service.GetReadinessText()}\n\nТЕКУЩАЯ СЕССИЯ\n{_service.GetSessionText()}\n\n" +
                $"ПОСЛЕДНЯЯ ОПЕРАЦИЯ\n{operationDetails}",
                _lastResult,
                _isBusy,
                _service.Snapshot.IsLinked,
                _service.IsLocallyReadyForPlayerAccounts);
        }

        /// <summary>
        /// Создаёт или восстанавливает гостевую UGS-сессию и записывает краткий результат.
        /// </summary>
        public Task EnsureSessionAsync()
        {
            return RunOperationAsync(async () =>
            {
                AccountSnapshot snapshot = await _service.EnsureSessionAsync();
                string userMessage = snapshot.IsSignedIn
                    ? snapshot.IsLinked
                        ? "Сессия готова: аккаунт привязан."
                        : "Гостевая сессия готова."
                    : "Сессию создать не удалось. Подробности — в диагностике.";
                return RecordResult(userMessage, FormatSnapshot(snapshot));
            });
        }

        /// <summary>
        /// Обновляет linked-state текущего игрока и presentation-состояние.
        /// </summary>
        public Task RefreshAsync()
        {
            return RunOperationAsync(async () =>
            {
                AccountSnapshot snapshot = await _service.RefreshAsync();
                string userMessage = snapshot.State == AccountState.Error
                    ? "Статус не обновлён. Подробности — в диагностике."
                    : snapshot.IsLinked
                        ? "Статус обновлён: аккаунт привязан."
                        : "Статус обновлён: гостевая сессия.";
                return RecordResult(userMessage, FormatSnapshot(snapshot));
            });
        }

        /// <summary>
        /// Запускает привязку Unity Player Account и безопасно отображает конфликт identity.
        /// </summary>
        public Task LinkAsync()
        {
            return RunOperationAsync(async () =>
            {
                AccountLinkResult result = await _service.LinkAsync();
                if (result.Status == AccountLinkStatus.AlreadyLinked)
                {
                    return RecordResult(
                        "Конфликт: аккаунт уже связан с другим игроком. Переключение заблокировано.",
                        FormatLinkResult(result));
                }

                string userMessage = result.IsSuccess
                    ? "Unity Player Account привязан."
                    : "Привязка не выполнена. Подробности — в диагностике.";
                return RecordResult(userMessage, FormatLinkResult(result));
            });
        }

        /// <summary>
        /// Отвязывает Unity Player Account и публикует итог операции.
        /// </summary>
        public Task UnlinkAsync()
        {
            return RunOperationAsync(async () =>
            {
                AccountSnapshot snapshot = await _service.UnlinkAsync();
                string userMessage = snapshot.State == AccountState.Error || snapshot.IsLinked
                    ? "Отвязка не выполнена. Подробности — в диагностике."
                    : "Unity Player Account отвязан.";
                return RecordResult(userMessage, FormatSnapshot(snapshot));
            });
        }

        /// <summary>
        /// Завершает UGS-сессию, сохраняя локальные credentials для восстановления.
        /// </summary>
        public Task SignOutUgsAsync()
        {
            return RunOperationAsync(async () =>
            {
                await _service.SignOutUgsKeepingCredentialsAsync();
                return RecordResult(
                    "Сессия завершена; данные входа сохранены.",
                    "UGS sign-out completed with cached credentials preserved.");
            });
        }

        /// <summary>
        /// Завершает локальную Unity Player Accounts OAuth-сессию без изменения UGS identity.
        /// </summary>
        public void SignOutPlayerAccount()
        {
            if (_isBusy)
            {
                return;
            }

            try
            {
                _service.SignOutPlayerAccount();
                _lastResult = "Сессия Unity Player Account завершена.";
                _lastTechnicalDetails =
                    "Local UPA OAuth session cleared; UGS Player ID and linked-state were not changed.";
            }
            catch (Exception ex)
            {
                _lastResult = "Выйти из Unity Player Account не удалось. Подробности — в диагностике.";
                _lastTechnicalDetails = ex.ToString();
            }

            PresentationChanged?.Invoke();
        }

        /// <summary>
        /// Удаляет локальные UGS credentials и публикует итог операции.
        /// </summary>
        public Task ClearCachedIdentityAsync()
        {
            return RunOperationAsync(async () =>
            {
                await _service.ClearCachedIdentityAsync();
                return RecordResult(
                    "Данные входа на устройстве очищены.",
                    "Cached UGS identity cleared; PlayerData was not changed.");
            });
        }

        /// <summary>
        /// Сообщает о недостающей локальной конфигурации и направляет пользователя в справку.
        /// </summary>
        public void ReportMissingConfiguration()
        {
            _lastResult = "Привязка не запущена: сначала выполни подготовку из справки.";
            _lastTechnicalDetails = "Local cloudProjectId or Unity Player Accounts clientId is empty.";
            PresentationChanged?.Invoke();
        }

        /// <summary>
        /// Делегирует открытие Unity Dashboard сервису DEV-инструментов.
        /// </summary>
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
                _lastResult = "Операция завершилась ошибкой. Подробности — в диагностике.";
                _lastTechnicalDetails = ex.ToString();
            }
            finally
            {
                _isBusy = false;
                PresentationChanged?.Invoke();
            }
        }

        private string RecordResult(string userMessage, string technicalDetails)
        {
            _lastTechnicalDetails = technicalDetails ?? string.Empty;
            return userMessage ?? string.Empty;
        }

        private static string FormatSnapshot(AccountSnapshot snapshot)
        {
            return $"State={snapshot.State}; PlayerId={snapshot.PlayerId}; " +
                   $"SignedIn={snapshot.IsSignedIn}; Linked={snapshot.IsLinked}; Error={snapshot.ErrorMessage}";
        }

        private static string FormatLinkResult(AccountLinkResult result)
        {
            return $"Status={result.Status}; PlayerId={result.PlayerId}; Error={result.ErrorMessage}";
        }
    }
}
#endif
