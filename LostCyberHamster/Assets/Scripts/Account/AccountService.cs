using System;
using System.Threading.Tasks;
using UnityEngine;

namespace Assets.Scripts.Account
{

    /// <summary>
    /// Управляет определением, привязкой, переключением и тестовым сбросом аккаунта игрока.
    /// </summary>
    public sealed class AccountService
    {
        /// <summary>
        /// Шлюз аутентификации игрока в Unity Gaming Services.
        /// </summary>
        private readonly IAccountAuthenticationGateway _authenticationGateway;

        /// <summary>
        /// Шлюз сессии Unity Player Account.
        /// </summary>
        private readonly IUnityPlayerAccountGateway _playerAccountGateway;

        /// <summary>
        /// Версия текущей операции определения или изменения аккаунта.
        /// </summary>
        private int _resolutionVersion;

        /// <summary>
        /// Текущее состояние аккаунта игрока.
        /// </summary>
        public AccountState State { get; private set; } = AccountState.NotStarted;

        /// <summary>
        /// Возникает после изменения состояния аккаунта.
        /// </summary>
        public event Action<AccountState> StateChanged;

        /// <summary>
        /// Возникает после успешной привязки текущего гостя.
        /// </summary>
        public event Action<string> CurrentGuestLinked;

        public AccountService(
            IAccountAuthenticationGateway authenticationGateway,
            IUnityPlayerAccountGateway playerAccountGateway)
        {
            _authenticationGateway = authenticationGateway
                ?? throw new ArgumentNullException(nameof(authenticationGateway));
            _playerAccountGateway = playerAccountGateway
                ?? throw new ArgumentNullException(nameof(playerAccountGateway));
        }

        /// <summary>
        /// Возвращает Player ID только после подтверждённого определения связанного аккаунта.
        /// </summary>
        public bool TryGetLinkedPlayerId(out string playerId)
        {
            playerId = null;
            if (State != AccountState.Linked)
                return false;

            var resolvedPlayerId = _authenticationGateway.PlayerId;
            if (string.IsNullOrWhiteSpace(resolvedPlayerId))
                return false;

            playerId = resolvedPlayerId;
            return true;
        }

        /// <summary>
        /// Привязывает текущего гостя к Unity Player Account без смены Player ID.
        /// </summary>
        public async Task<AccountLinkResult> LinkCurrentGuestAsync()
        {
            // Привязка доступна только для подтверждённой гостевой сессии.
            if (State != AccountState.Guest)
                return AccountLinkResult.Failed;

            // Фиксируем гостя и помечаем операцию как текущую.
            var playerId = _authenticationGateway.PlayerId;
            var resolutionVersion = ++_resolutionVersion;
            SetState(AccountState.Linking);

            try
            {
                // Получаем токен Player Account и привязываем его к гостю.
                var accessToken = await _playerAccountGateway.SignInAsync();
                if (resolutionVersion != _resolutionVersion)
                    return AccountLinkResult.Failed;

                var result = await _authenticationGateway.LinkWithUnityAsync(accessToken);
                if (resolutionVersion != _resolutionVersion)
                    return AccountLinkResult.Failed;

                // Конфликт сохраняет исходную гостевую сессию.
                if (result == AccountLinkResult.Conflict)
                {
                    var playerIdPreserved = _authenticationGateway.PlayerId == playerId;
                    SetState(AccountState.Guest);
                    Debug.Log($"[Account] Link conflict: external account already linked. PlayerId preserved: {playerIdPreserved}.");
                    return AccountLinkResult.Conflict;
                }

                // Любой другой неуспешный результат возвращает гостевое состояние.
                if (result != AccountLinkResult.Linked)
                {
                    SetState(AccountState.Guest);
                    return result;
                }

                // Успех допустим только без смены идентификатора текущего гостя.
                if (_authenticationGateway.PlayerId != playerId)
                {
                    SetState(AccountState.Guest);
                    return AccountLinkResult.Failed;
                }

                // Публикуем подтверждённую привязку гостя.
                SetState(AccountState.Linked);
                NotifyCurrentGuestLinked(playerId);
                return AccountLinkResult.Linked;
            }
            catch
            {
                // Актуальная неудачная операция возвращает гостевое состояние.
                if (resolutionVersion == _resolutionVersion)
                    SetState(AccountState.Guest);

                return AccountLinkResult.Failed;
            }
        }

        /// <summary>
        /// Переключает текущего гостя на существующий UGS-аккаунт, связанный с Unity Player Account.
        /// </summary>
        public async Task<bool> SignInExistingAccountAsync(
            Func<string, Task<bool>> acceptSignedInAccountAsync)
        {
            // Переключение доступно только из подтверждённой гостевой сессии.
            if (State != AccountState.Guest)
                return false;

            if (acceptSignedInAccountAsync == null)
                throw new ArgumentNullException(nameof(acceptSignedInAccountAsync));

            // Проверяем исходного гостя до смены identity.
            var originalPlayerId = _authenticationGateway.PlayerId;
            if (!IsOriginalGuestSession(originalPlayerId))
            {
                SetState(AccountState.Error);
                return false;
            }

            var resolutionVersion = ++_resolutionVersion;
            SetState(AccountState.SigningIn);

            try
            {
                // Получаем токен Player Account до выхода из гостевой сессии.
                var accessToken = await _playerAccountGateway.SignInAsync();
                EnsureCurrentOperation(resolutionVersion);
                if (string.IsNullOrWhiteSpace(accessToken))
                    throw new InvalidOperationException("Unity Player Account access token is unavailable.");

                // Сохраняем анонимный токен, чтобы при ошибке восстановить исходного гостя.
                _authenticationGateway.SignOutPreservingCredentials();
                EnsureCurrentOperation(resolutionVersion);

                // Входим в связанный аккаунт и проверяем смену identity.
                await _authenticationGateway.SignInWithUnityAsync(accessToken);
                EnsureCurrentOperation(resolutionVersion);

                if (!_authenticationGateway.IsSignedIn ||
                    !_authenticationGateway.IsUnityPlayerAccountLinked ||
                    string.IsNullOrWhiteSpace(_authenticationGateway.PlayerId) ||
                    _authenticationGateway.PlayerId == originalPlayerId)
                {
                    throw new InvalidOperationException("Existing linked account verification failed.");
                }

                // Перед commit передаём найденный аккаунт вызывающему сценарию.
                if (!await acceptSignedInAccountAsync(_authenticationGateway.PlayerId))
                    throw new InvalidOperationException("Existing linked account was not accepted.");
            }
            catch (Exception exception)
            {
                // При любой ошибке пытаемся вернуть исходную гостевую сессию.
                var restored = await TryRestoreOriginalGuestAsync(originalPlayerId);
                SetState(restored ? AccountState.Guest : AccountState.Error);
                Debug.LogError($"[Account] Existing account sign-in failed. Original guest restored: {restored}. Error type: {exception.GetType().Name}.");
                return false;
            }

            // Успешный restore — финальная commit-точка: state notification не откатывает identity.
            SetState(AccountState.Linked);
            Debug.Log("[Account] Existing linked account signed in.");
            return true;
        }

        /// <summary>
        /// Переводит аккаунт в состояние определения гостя и запускает его без блокировки игры.
        /// </summary>
        public void Start()
        {
            // Не запускаем определение аккаунта повторно.
            if (State != AccountState.NotStarted)
                return;

            // Публикуем начало определения аккаунта.
            SetState(AccountState.Resolving);
            Debug.Log("[Account] State: Resolving");

            // Запускаем незавершённую пока логику гостя без ожидания.
            var resolutionVersion = ++_resolutionVersion;
            _ = ResolveGuestAsync(resolutionVersion);
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>
        /// Очищает локальные сессии Unity Authentication и Player Accounts.
        /// </summary>
        public void ResetLocalAccountStateForTesting()
        {
            _resolutionVersion++;
            ClearLocalAccountState();
            Debug.Log("[Account] Local reset completed. Local account credentials cleared.");
        }

        /// <summary>
        /// Сохраняет совместимость существующих тестов с локальным сбросом аккаунта.
        /// </summary>
        public void ResetForTesting()
        {
            ResetLocalAccountStateForTesting();
        }

        /// <summary>
        /// Входит в связанный серверный аккаунт, отвязывает Unity Player Account и очищает локальные сессии.
        /// </summary>
        public async Task FullResetTestAccountAsync()
        {
            // Фиксируем операцию и этапы частичного сброса.
            var resolutionVersion = ++_resolutionVersion;
            var localUgsIdentityCleared = false;
            var serverAccountUnlinked = false;
            Debug.Log("[Account] Full reset started. Resolving Player Accounts session.");

            try
            {
                // Получаем действующий access token до очистки локальной identity гостя.
                var accessToken = await _playerAccountGateway.SignInAsync();
                if (resolutionVersion != _resolutionVersion)
                    throw new OperationCanceledException("Account operation was invalidated.");
                if (string.IsNullOrWhiteSpace(accessToken))
                    throw new InvalidOperationException("Unity Player Account access token is unavailable.");

                Debug.Log("[Account] Full reset stage: Player Accounts access token acquired.");

                // Очищаем только UGS identity, сохраняя Player Accounts session для серверного входа.
                try
                {
                    _authenticationGateway.SignOutAndClearLocalCredentials();
                    localUgsIdentityCleared = true;
                }
                catch (Exception exception)
                {
                    SetState(AccountState.Error);
                    Debug.LogError($"[Account] Full reset failed at Unity Authentication sign-out. Error type: {exception.GetType().Name}.");
                    throw;
                }

                SetState(AccountState.Resolving);
                Debug.Log("[Account] Full reset stage: local UGS identity cleared.");

                // Входим именно в серверный аккаунт-владелец и проверяем его связь.
                await _authenticationGateway.SignInWithUnityAsync(accessToken);
                EnsureCurrentOperation(resolutionVersion);

                if (!_authenticationGateway.IsSignedIn || !_authenticationGateway.IsUnityPlayerAccountLinked)
                    throw new InvalidOperationException("Signed-in server account is not linked to Unity Player Accounts.");

                Debug.Log("[Account] Full reset stage: linked server account verified.");

                // Удаляем только связь, затем снова очищаем локальную identity.
                await _authenticationGateway.UnlinkUnityAsync();
                serverAccountUnlinked = true;
                EnsureCurrentOperation(resolutionVersion);

                Debug.Log("[Account] Full reset stage: Unity Player Account unlinked.");
                ClearLocalAccountState();
                Debug.Log("[Account] Full reset completed. Server link and local account state cleared.");
            }
            catch (Exception exception)
            {
                // После очистки UGS identity завершаем локальную очистку при любой ошибке.
                if (localUgsIdentityCleared)
                {
                    try
                    {
                        ClearLocalAccountState();
                    }
                    catch (Exception cleanupException)
                    {
                        Debug.LogError($"[Account] Full reset failure cleanup failed. Error type: {cleanupException.GetType().Name}.");
                    }

                    SetState(AccountState.Error);
                }

                if (serverAccountUnlinked)
                    Debug.LogError("[Account] Full reset partially completed: server link removed, but local completion failed.");

                // Разделяем отмену устаревшей операции и фактическую ошибку сброса.
                if (exception is OperationCanceledException)
                {
                    Debug.LogWarning($"[Account] Full reset cancelled. Local UGS identity cleared: {localUgsIdentityCleared}.");
                }
                else
                {
                    Debug.LogError($"[Account] Full reset failed. Local UGS identity cleared: {localUgsIdentityCleared}. Error type: {exception.GetType().Name}.");
                }

                throw;
            }
        }

        /// <summary>
        /// Очищает локальные данные Unity Authentication и активную сессию Player Account.
        /// </summary>
        private void ClearLocalAccountState()
        {
            Exception authenticationException = null;
            Exception playerAccountException = null;

            // Независимо очищаем Unity Authentication.
            try
            {
                _authenticationGateway.SignOutAndClearLocalCredentials();
            }
            catch (Exception exception)
            {
                authenticationException = exception;
                Debug.LogError($"[Account] Local cleanup failed at Unity Authentication sign-out. Error type: {exception.GetType().Name}.");
            }

            // Независимо завершаем активную сессию Player Account.
            try
            {
                if (_playerAccountGateway.IsSignedIn)
                    _playerAccountGateway.SignOut();
            }
            catch (Exception exception)
            {
                playerAccountException = exception;
                Debug.LogError($"[Account] Local cleanup failed at Player Accounts sign-out. Error type: {exception.GetType().Name}.");
            }

            // Публикуем ошибку и сохраняем сведения обо всех неудачных этапах.
            if (authenticationException != null || playerAccountException != null)
            {
                SetState(AccountState.Error);
                if (authenticationException != null && playerAccountException != null)
                    throw new AggregateException(authenticationException, playerAccountException);

                throw authenticationException ?? playerAccountException;
            }

            // Полностью очищенная локальная identity требует нового определения аккаунта.
            SetState(AccountState.NotStarted);
        }
#endif

        /// <summary>
        /// Прерывает продолжение операции, если её версия уже не актуальна.
        /// </summary>
        private void EnsureCurrentOperation(int resolutionVersion)
        {
            if (resolutionVersion == _resolutionVersion)
                return;

            throw new OperationCanceledException("Account operation was invalidated.");
        }

        /// <summary>
        /// Пытается восстановить исходную гостевую сессию после неудачного переключения аккаунта.
        /// </summary>
        private async Task<bool> TryRestoreOriginalGuestAsync(string originalPlayerId)
        {
            try
            {
                // Уже восстановленная сессия не требует повторного входа.
                if (IsOriginalGuestSession(originalPlayerId))
                    return true;

                // Освобождаем текущую identity, сохраняя локальные гостевые credentials.
                if (_authenticationGateway.IsSignedIn)
                    _authenticationGateway.SignOutPreservingCredentials();

                // Восстанавливаем существующего гостя без создания нового аккаунта.
                await _authenticationGateway.SignInAnonymouslyAsync(createAccount: false);
                return IsOriginalGuestSession(originalPlayerId);
            }
            catch (Exception exception)
            {
                Debug.LogError($"[Account] Original guest restoration failed. Error type: {exception.GetType().Name}.");
                return false;
            }
        }

        /// <summary>
        /// Проверяет, соответствует ли активная сессия исходному непривязанному гостю.
        /// </summary>
        private bool IsOriginalGuestSession(string originalPlayerId)
        {
            return !string.IsNullOrWhiteSpace(originalPlayerId) &&
                   _authenticationGateway.IsSignedIn &&
                   !_authenticationGateway.IsUnityPlayerAccountLinked &&
                   _authenticationGateway.PlayerId == originalPlayerId;
        }

        /// <summary>
        /// Уведомляет подписчиков об успешной привязке гостя, изолируя ошибки обработчиков.
        /// </summary>
        private void NotifyCurrentGuestLinked(string playerId)
        {
            // Фиксируем текущий список подписчиков перед рассылкой.
            var handlers = CurrentGuestLinked;
            if (handlers == null)
                return;

            // Ошибка одного подписчика не прерывает остальных.
            foreach (Action<string> handler in handlers.GetInvocationList())
            {
                try
                {
                    handler(playerId);
                }
                catch (Exception exception)
                {
                    Debug.LogError($"[Account] Guest link subscriber failed: {exception.GetType().Name}.");
                }
            }
        }

        /// <summary>
        /// Восстанавливает существующего гостя или создаёт нового по локальной сессии.
        /// </summary>
        private async Task ResolveGuestAsync(int resolutionVersion)
        {
            try
            {
                // Выбираем ровно один сценарий по наличию локальной сессии.
                var restoreGuest = _authenticationGateway.SessionTokenExists;

                if (restoreGuest)
                    Debug.Log("[Account] Scenario selected: RestoreGuest.");
                else
                    Debug.Log("[Account] Scenario selected: CreateGuest.");

                // Выполняем только выбранный сценарий без fallback на создание.
                await _authenticationGateway.SignInAnonymouslyAsync(createAccount: !restoreGuest);

                if (resolutionVersion != _resolutionVersion)
                {
                    _authenticationGateway.SignOutAndClearLocalCredentials();
                    return;
                }

                SetState(_authenticationGateway.IsUnityPlayerAccountLinked
                    ? AccountState.Linked
                    : AccountState.Guest);
                Debug.Log(restoreGuest
                    ? "[Account] Guest restored."
                    : "[Account] Guest created.");
            }
            catch (Exception exception)
            {
                if (resolutionVersion != _resolutionVersion)
                    return;

                SetState(AccountState.Error);
                Debug.LogError($"[Account] Guest resolution failed. Error type: {exception.GetType().Name}.");
            }
        }

        /// <summary>
        /// Изменяет состояние аккаунта и уведомляет активных потребителей.
        /// </summary>
        private void SetState(AccountState state)
        {
            // Повторное состояние не создаёт лишнее уведомление.
            if (State == state)
                return;

            // Фиксируем состояние и текущий список подписчиков.
            State = state;
            var handlers = StateChanged;
            if (handlers == null)
                return;

            // Ошибка одного подписчика не прерывает остальных.
            foreach (Action<AccountState> handler in handlers.GetInvocationList())
            {
                try
                {
                    handler(state);
                }
                catch (Exception exception)
                {
                    Debug.LogError($"[Account] State subscriber failed: {exception.GetType().Name}.");
                }
            }
        }
    }
}
