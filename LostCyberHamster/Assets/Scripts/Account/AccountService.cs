using System;
using System.Threading.Tasks;
using UnityEngine;
using Assets.Scripts.Online;
using GameManagement;

namespace Assets.Scripts.Account
{

    /// <summary>
    /// Управляет определением, привязкой, переключением и тестовым сбросом аккаунта игрока.
    /// </summary>
    public sealed class AccountService : IDisposable
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
        private IDisposable _retryRegistration;
        private Task _resolutionTask;
        private bool _watchingExpiration;
        private string _knownPlayerId;
        private AccountTransitionScope _transition;

        private bool IsAuthorized => (_authenticationGateway as IAccountSessionStatus)?.IsAuthorized
            ?? _authenticationGateway.IsSignedIn;

        /// <summary>Сохраняет известную связь владельца локального прогресса после expiry и перезапуска.</summary>
        public bool HasKnownLinkedIdentity => GameDataManager.IsLoaded &&
            !string.IsNullOrWhiteSpace(GameDataManager.OwnerPlayerId) &&
            (!string.IsNullOrWhiteSpace(GameDataManager.BaseCloudRevision) ||
                AccountProfileStore.IsKnownLinkedPlayer(GameDataManager.OwnerPlayerId));

        /// <summary>
        /// Текущее состояние аккаунта игрока.
        /// </summary>
        public AccountState State { get; private set; } = AccountState.NotStarted;

        /// <summary>
        /// Возвращает полное публичное имя текущего игрока.
        /// </summary>
        public string PlayerName => _authenticationGateway.PlayerName;

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
            if (!IsLinkedPlayerSession(resolvedPlayerId))
                return false;

            playerId = resolvedPlayerId;
            return true;
        }

        /// <summary>Возвращает владельца действующей гостевой или связанной сессии.</summary>
        public bool TryGetAuthenticatedPlayerId(out string playerId)
        {
            playerId = null;
            if ((State != AccountState.Guest && State != AccountState.Linked) || !IsAuthorized)
                return false;
            var current = _authenticationGateway.PlayerId;
            if (string.IsNullOrWhiteSpace(current)) return false;
            playerId = current;
            return true;
        }

        /// <summary>
        /// Проверяет владельца связанной или принимаемой account-сессии.
        /// </summary>
        internal bool IsCurrentPlayer(
            string playerId,
            bool allowSigningIn)
        {
            var stateMatches = State == AccountState.Linked || State == AccountState.Guest ||
                               allowSigningIn && State == AccountState.SigningIn;
            return stateMatches && IsAuthorized && !string.IsNullOrWhiteSpace(playerId) &&
                   string.Equals(_authenticationGateway.PlayerId, playerId, StringComparison.Ordinal);
        }

        /// <summary>
        /// Обновляет публичное имя текущего игрока и возвращает сохранённое полное имя.
        /// </summary>
        public Task<string> UpdatePlayerNameAsync(string playerName)
        {
            return _authenticationGateway.UpdatePlayerNameAsync(playerName);
        }

        /// <summary>
        /// Привязывает текущего гостя к Unity Player Account без смены Player ID.
        /// </summary>
        public async Task<AccountLinkResult> LinkCurrentGuestAsync()
        {
            // Привязка доступна только для подтверждённой гостевой сессии.
            if (State != AccountState.Guest || GameDataManager.IsProfileReplacementBlocked)
                return AccountLinkResult.Failed;
            using var transition = _transition = new AccountTransitionScope();

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
                if (GameDataManager.IsProfileReplacementBlocked)
                {
                    SetState(AccountState.Guest);
                    return AccountLinkResult.Failed;
                }

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
                try { ConfirmCurrentProfile(playerId); }
                catch (Exception exception)
                {
                    DebugManager.DiagStability($"[ACCOUNT] Linked profile persistence deferred: {exception.GetType().Name}.");
                }
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
            if (State != AccountState.Guest || GameDataManager.IsProfileReplacementBlocked)
                return false;
            using var transition = _transition = new AccountTransitionScope();

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
            var profiles = _authenticationGateway as IAccountProfileGateway;
            AccountProfileSwitch profileSwitch = null;
            bool accepted = false;

            try
            {
                // Получаем токен Player Account до выхода из гостевой сессии.
                var accessToken = await _playerAccountGateway.SignInAsync();
                EnsureCurrentOperation(resolutionVersion);
                if (string.IsNullOrWhiteSpace(accessToken))
                    throw new InvalidOperationException("Unity Player Account access token is unavailable.");

                if (GameDataManager.IsProfileReplacementBlocked)
                {
                    SetState(AccountState.Guest);
                    return false;
                }

                // Отдельный SDK profile сохраняет credentials исходного гостя при входе кандидата.
                if (profiles != null)
                    profileSwitch = AccountProfileStore.BeginSwitch(originalPlayerId, profiles.Profile);
                _authenticationGateway.SignOutPreservingCredentials();
                if (profileSwitch != null)
                    profiles.SwitchProfile(profileSwitch.CandidateProfile);
                EnsureCurrentOperation(resolutionVersion);

                // Входим в связанный аккаунт и проверяем смену identity.
                await _authenticationGateway.SignInWithUnityAsync(accessToken);
                EnsureCurrentOperation(resolutionVersion);

                var signedInPlayerId = _authenticationGateway.PlayerId;
                if (!IsLinkedPlayerSession(signedInPlayerId) ||
                    signedInPlayerId == originalPlayerId)
                {
                    throw new InvalidOperationException("Existing linked account verification failed.");
                }

                // Mapping кандидата должен попасть в envelope до принятия его облачного прогресса.
                if (profileSwitch != null)
                    AccountProfileStore.RecordCandidate(profileSwitch, signedInPlayerId);
                accepted = await acceptSignedInAccountAsync(signedInPlayerId);
                if (!accepted)
                    throw new InvalidOperationException("Existing linked account was not accepted.");
                EnsureCurrentOperation(resolutionVersion);
                if (!IsLinkedPlayerSession(signedInPlayerId))
                    throw new InvalidOperationException("Existing linked account changed during restore.");
            }
            catch (Exception exception)
            {
                // Durable cloud apply уже выбрал нового владельца: сбой финального ACK не откатывает его.
                if (profileSwitch != null &&
                    !string.IsNullOrWhiteSpace(profileSwitch.CandidatePlayerId) &&
                    GameDataManager.OwnerPlayerId == profileSwitch.CandidatePlayerId)
                {
                    bool sessionMatches = IsLinkedPlayerSession(profileSwitch.CandidatePlayerId);
                    if (resolutionVersion == _resolutionVersion)
                        SetState(sessionMatches ? AccountState.Linked : AccountState.Error);
                    return sessionMatches;
                }
                if (accepted)
                {
                    if (resolutionVersion == _resolutionVersion)
                        SetState(AccountState.Error);
                    return false;
                }
                // При любой ошибке пытаемся вернуть исходную гостевую сессию.
                var restored = await TryRestoreOriginalGuestAsync(
                    originalPlayerId,
                    resolutionVersion,
                    profileSwitch?.OriginalProfile);
                if (resolutionVersion != _resolutionVersion)
                    return false;

                SetState(restored ? AccountState.Guest : AccountState.Error);
                if (restored && profileSwitch != null)
                    TryCompleteProfileSwitch(originalPlayerId);
                Debug.LogError($"[Account] Existing account sign-in failed. Original guest restored: {restored}. Error type: {exception.GetType().Name}.");
                return false;
            }

            // Успешный restore — финальная commit-точка: state notification не откатывает identity.
            if (profileSwitch != null)
                TryCompleteProfileSwitch(profileSwitch.CandidatePlayerId);
            SetState(AccountState.Linked);
            Debug.Log("[Account] Existing linked account signed in.");
            return true;
        }

        /// <summary>
        /// Переводит аккаунт в состояние определения гостя и запускает его без блокировки игры.
        /// </summary>
        public void Start()
        {
            // Подключаем восстановление после локальной загрузки и готовности SDK.
            _retryRegistration ??= OnlineServicesCoordinator.Register("account", EnsureSessionAsync,
                () => OnlineServicesCoordinator.UnityServicesReady && GameDataManager.IsLoaded);
            OnlineServicesCoordinator.RequestRetry("account");
        }

        /// <summary>Восстанавливает ту же сетевую сессию, объединяя одновременные попытки.</summary>
        public Task EnsureSessionAsync()
        {
            if (_resolutionTask != null && !_resolutionTask.IsCompleted) return _resolutionTask;
            if (State == AccountState.Linking || State == AccountState.SigningIn) return Task.CompletedTask;
            PrepareDurableProfile();
            _knownPlayerId ??= GameDataManager.OwnerPlayerId;

            // Expired требует нового входа с сохранёнными credentials, а не нового гостя.
            if (!_watchingExpiration && _authenticationGateway is IAccountSessionStatus session)
            {
                session.SessionExpired += OnSessionExpired;
                _watchingExpiration = true;
            }
            if (TryGetAuthenticatedPlayerId(out var playerId))
            {
                if (!string.IsNullOrWhiteSpace(_knownPlayerId) && _knownPlayerId != playerId)
                    throw new InvalidOperationException("Authorized session belongs to another player.");
                GameDataManager.TryBindAuthenticatedOwner(playerId);
                ConfirmCurrentProfile(playerId);
                return Task.CompletedTask;
            }
            SetState(AccountState.Resolving);
            _resolutionTask = ResolveGuestAsync(++_resolutionVersion);
            return _resolutionTask;
        }

        private void OnSessionExpired()
        {
            if (State == AccountState.Linking || State == AccountState.SigningIn) return;
            SetState(AccountState.Error);
            OnlineServicesCoordinator.RequestRetry("account");
        }

        /// <summary>Выбирает credentials durable владельца до первого auth-запроса после запуска.</summary>
        private void PrepareDurableProfile()
        {
            if (!GameDataManager.IsLoaded || !(_authenticationGateway is IAccountProfileGateway profiles))
                return;
            var journal = AccountProfileStore.Read();
            string expectedPlayer = GameDataManager.OwnerPlayerId ??
                journal.Pending?.OriginalPlayerId ?? journal.LastConfirmedPlayerId;
            string profile = AccountProfileStore.ProfileFor(journal, expectedPlayer);

            // Старый default допускается только до проверки identity; неподтверждённый mapping не создаём.
            if (!string.IsNullOrWhiteSpace(expectedPlayer))
                _knownPlayerId = expectedPlayer;
            if (string.IsNullOrWhiteSpace(profile) || profiles.Profile == profile)
                return;
            if (GameDataManager.IsProfileReplacementBlocked || AccountTransitionScope.IsActive)
                throw new InvalidOperationException("Authentication profile change is temporarily blocked.");
            profiles.SwitchProfile(profile);
        }

        private void ConfirmCurrentProfile(string playerId)
        {
            if (!GameDataManager.IsLoaded || !(_authenticationGateway is IAccountProfileGateway profiles))
                return;
            AccountProfileStore.RecordVerifiedPlayer(playerId, profiles.Profile, confirm: true,
                isLinked: _authenticationGateway.IsUnityPlayerAccountLinked);
            if (AccountProfileStore.Read().Pending != null)
                AccountProfileStore.CompleteSwitch(playerId);
        }

        private static void TryCompleteProfileSwitch(string playerId)
        {
            try { AccountProfileStore.CompleteSwitch(playerId); }
            catch (Exception exception)
            {
                // Mapping уже durable; после перезапуска победит владелец принятого PlayerData.
                DebugManager.DiagStability(
                    $"[ACCOUNT] Profile acknowledgement deferred: {exception.GetType().Name}.");
            }
        }

        /// <summary>Отсоединяет фоновые повторы при завершении проектного контекста.</summary>
        public void Dispose()
        {
            _resolutionVersion++;
            if (State == AccountState.Linking || State == AccountState.SigningIn)
                _playerAccountGateway.SignOut();
            _transition?.Dispose();
            _retryRegistration?.Dispose();
            if (_watchingExpiration && _authenticationGateway is IAccountSessionStatus session)
                session.SessionExpired -= OnSessionExpired;
            _watchingExpiration = false;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>
        /// Очищает локальные сессии Unity Authentication и Player Accounts.
        /// </summary>
        public void ResetLocalAccountStateForTesting()
        {
            _resolutionVersion++;
            _knownPlayerId = null;
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
        /// Отвязывает Unity Player Account от текущего связанного аккаунта и очищает локальные сессии.
        /// </summary>
        public async Task FullResetTestAccountAsync()
        {
            if (!TryGetLinkedPlayerId(out var linkedPlayerId))
                throw new InvalidOperationException("Full reset requires a linked account.");

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

                if (!_authenticationGateway.IsSignedIn ||
                    !_authenticationGateway.IsUnityPlayerAccountLinked ||
                    !string.Equals(
                        _authenticationGateway.PlayerId,
                        linkedPlayerId,
                        StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Signed-in account does not match the current linked account.");
                }

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
        private async Task<bool> TryRestoreOriginalGuestAsync(
            string originalPlayerId,
            int resolutionVersion,
            string originalProfile = null)
        {
            try
            {
                if (resolutionVersion != _resolutionVersion)
                    return false;

                // Уже восстановленная сессия не требует повторного входа.
                if (IsOriginalGuestSession(originalPlayerId))
                    return true;

                // Освобождаем текущую identity, сохраняя локальные гостевые credentials.
                if (_authenticationGateway.IsSignedIn)
                    _authenticationGateway.SignOutPreservingCredentials();
                if (!string.IsNullOrWhiteSpace(originalProfile) &&
                    _authenticationGateway is IAccountProfileGateway profiles)
                    profiles.SwitchProfile(originalProfile);

                // Восстанавливаем существующего гостя без создания нового аккаунта.
                await _authenticationGateway.SignInAnonymouslyAsync(createAccount: false);
                if (resolutionVersion != _resolutionVersion)
                    return false;

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
                   IsAuthorized &&
                   !_authenticationGateway.IsUnityPlayerAccountLinked &&
                   _authenticationGateway.PlayerId == originalPlayerId;
        }

        /// <summary>
        /// Проверяет, что активна связанная UGS-сессия указанного игрока.
        /// </summary>
        private bool IsLinkedPlayerSession(string playerId)
        {
            return !string.IsNullOrWhiteSpace(playerId) &&
                   IsAuthorized &&
                   _authenticationGateway.IsUnityPlayerAccountLinked &&
                   string.Equals(
                       _authenticationGateway.PlayerId,
                       playerId,
                       StringComparison.Ordinal);
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
                if (!IsAuthorized && !restoreGuest && !string.IsNullOrWhiteSpace(_knownPlayerId))
                    throw new InvalidOperationException("Existing session credentials are unavailable.");

                if (restoreGuest)
                    Debug.Log("[Account] Scenario selected: RestoreGuest.");
                else
                    Debug.Log("[Account] Scenario selected: CreateGuest.");

                // Выполняем только выбранный сценарий без fallback на создание.
                if (!IsAuthorized)
                    await _authenticationGateway.SignInAnonymouslyAsync(createAccount: !restoreGuest);

                if (resolutionVersion != _resolutionVersion)
                {
                    return;
                }

                // Смена identity во время восстановления не принимает чужой локальный прогресс.
                var resolvedId = _authenticationGateway.PlayerId;
                if (!IsAuthorized || string.IsNullOrWhiteSpace(resolvedId) ||
                    !string.IsNullOrWhiteSpace(_knownPlayerId) && _knownPlayerId != resolvedId)
                    throw new InvalidOperationException("Restored session identity does not match.");
                _knownPlayerId = resolvedId;
                if (GameDataManager.IsLoaded) GameDataManager.TryBindAuthenticatedOwner(resolvedId);
                ConfirmCurrentProfile(resolvedId);

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
                DebugManager.DiagStability($"[ACCOUNT] Session unavailable: {exception.GetType().Name}.");
                throw;
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
            if ((state == AccountState.Guest || state == AccountState.Linked) && IsAuthorized)
                _knownPlayerId = _authenticationGateway.PlayerId;
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
