using System;
using System.Threading;
using System.Threading.Tasks;
using Assets.Scripts.Account;
using UnityEngine;

namespace GameManagement.CloudSave
{
    /// <summary>
    /// Координирует создание, отправку, восстановление и разрешение конфликтов облачных снимков.
    /// </summary>
    public sealed class CloudSyncService : IDisposable
    {
        /// <summary>Выполняет сетевые операции Cloud Save.</summary>
        private readonly ICloudSaveGateway _gateway;

        /// <summary>Предоставляет состояние и идентификатор текущего аккаунта.</summary>
        private readonly AccountService _accountService;

        /// <summary>Обнаруживает и разрешает конфликты снимков.</summary>
        private readonly CloudSaveConflictService _conflictService;

        /// <summary>Хранит и последовательно отправляет pending-снимки.</summary>
        private readonly SnapshotUploadService _uploadService;

        /// <summary>Отменяет устаревшие async-операции при смене состояния аккаунта.</summary>
        private CancellationTokenSource _lifecycleCancellation = new CancellationTokenSource();

        /// <summary>Показывает, что сервис освобождён и больше не обрабатывает события.</summary>
        private bool _isDisposed;

        /// <summary>Показывает, что проверка cloud-only изменений уже выполняется.</summary>
        private bool _isCloudRefreshActive;

        /// <summary>Владелец текущей подтверждённой облачной версии.</summary>
        private string _currentCloudVersionPlayerId;

        public CloudSyncService(
            ICloudSaveGateway gateway,
            AccountService accountService,
            CloudSaveConflictService conflictService,
            SnapshotUploadService uploadService)
        {
            // Проверяем обязательные зависимости.
            _gateway = gateway ?? throw new ArgumentNullException(nameof(gateway));
            if (accountService == null)
            {
                throw new ArgumentNullException(nameof(accountService));
            }

            _accountService = accountService;
            _conflictService = conflictService
                ?? throw new ArgumentNullException(nameof(conflictService));
            _uploadService = uploadService
                ?? throw new ArgumentNullException(nameof(uploadService));

            // Подписываемся на источники синхронизации и продолжаем durable pending.
            accountService.CurrentGuestLinked += OnCurrentGuestLinked;
            accountService.StateChanged += OnAccountStateChanged;
            PlayerProgressCommitter.CommitCompleted += OnCheckpointCommitted;
            PlayerProgressLifecycleCheckpoint.ApplicationResumed += OnApplicationResumed;
            TryUploadPendingForCurrentAccount();
        }

        /// <summary>Последняя подтверждённая сервером версия текущего процесса игры.</summary>
        public CloudSaveWriteResult CurrentCloudVersion { get; private set; }

        /// <summary>Текущие две независимо изменённые ветки, ожидающие выбора.</summary>
        public CloudSaveConflictModel CurrentConflict => _conflictService.CurrentConflict;

        /// <summary>Возникает при обнаружении или обновлении данных конфликта.</summary>
        public event Action<CloudSaveConflictModel> ConflictDetected
        {
            add => _conflictService.ConflictDetected += value;
            remove => _conflictService.ConflictDetected -= value;
        }

        /// <summary>Есть первый снимок, который облако ещё не подтвердило.</summary>
        public bool HasPendingFirstSnapshot => _uploadService.HasPendingFirstSnapshot;

        /// <summary>
        /// Сначала сохраняет полный прогресс локально, затем отправляет его первый снимок в облако.
        /// </summary>
        public async Task UploadFirstSnapshotAsync(string playerId)
        {
            // Не запускаем повторный first upload.
            if (_isDisposed ||
                _uploadService.IsUploadActive ||
                _uploadService.PendingSnapshot != null ||
                _uploadService.HasPendingFirstSnapshot ||
                CurrentCloudVersion != null)
            {
                Debug.Log("[CloudSave] First snapshot upload skipped: already started.");
                return;
            }

            try
            {
                // Фиксируем согласованное локальное состояние.
                PlayerProgressCommitter.Commit(CheckpointReason.AccountLinked);
                Debug.Log("[CloudSave] Local commit completed: AccountLinked.");

                // Создаём и сохраняем первый снимок владельца.
                var snapshot = CloudSaveSnapshotCodec.Capture(
                    GameDataManager.PlayerData,
                    playerId,
                    _uploadService.GetNextLocalRevision());
                _uploadService.SetFirstPendingSnapshot(snapshot);

                // Запускаем последовательную отправку pending.
                await UploadPendingSnapshotAsync(isRetry: false);
            }
            catch (Exception exception)
            {
                // Оставляем durable pending для следующей попытки.
                Debug.LogError($"[CloudSave] First snapshot upload failed ({exception.GetType().Name}).");
            }
        }

        /// <summary>Повторно отправляет тот же неподтверждённый снимок.</summary>
        public Task RetryPendingSnapshotAsync()
        {
            // Пропускаем retry после освобождения сервиса.
            if (_isDisposed)
                return Task.CompletedTask;

            // Не запускаем параллельную отправку.
            if (_uploadService.IsUploadActive)
            {
                Debug.Log("[CloudSave] Snapshot retry skipped: upload active.");
                return Task.CompletedTask;
            }

            // Пропускаем retry без ожидающего снимка.
            if (_uploadService.PendingSnapshot == null)
            {
                Debug.Log("[CloudSave] Snapshot retry skipped: no pending snapshot.");
                return Task.CompletedTask;
            }

            // Отправляем сохранённый pending как retry.
            return UploadPendingSnapshotAsync(isRetry: true);
        }

        /// <summary>Проверяет актуальность выбранного облачного снимка и целиком применяет его локально.</summary>
        public async Task<bool> ResolveConflictWithCloudAsync()
        {
            // Пропускаем выбор после освобождения сервиса.
            if (_isDisposed)
                return false;

            // Получаем результат выбора с исходным конфликтом и актуальной cloud-веткой.
            var operationToken = _lifecycleCancellation.Token;
            var outcome = await _conflictService.TryResolveWithCloudAsync(operationToken);
            var conflict = outcome.Conflict;

            // Отбрасываем отменённый или устаревший результат.
            if (conflict == null ||
                !outcome.IsSuccessful ||
                !IsSnapshotOperationCurrent(conflict.LocalSnapshot.PlayerId, operationToken))
            {
                return false;
            }

            try
            {
                // Фиксируем cloud choice во всех runtime и durable состояниях.
                var playerId = conflict.LocalSnapshot.PlayerId;
                _uploadService.DiscardForOwner(playerId);
                _uploadService.ClearCloudMissingRecord();
                SetCurrentCloudVersion(playerId, outcome.Version);
                _conflictService.CompleteResolution(conflict);
                return true;
            }
            catch (Exception exception)
            {
                // Сохраняем конфликт для повторного выбора.
                Debug.LogError($"[CloudSave] Cloud conflict choice failed ({exception.GetType().Name}).");
                return false;
            }
        }

        /// <summary>Записывает выбранный локальный снимок поверх актуальной облачной версии.</summary>
        public async Task<bool> ResolveConflictWithLocalAsync()
        {
            // Пропускаем выбор после освобождения сервиса.
            if (_isDisposed)
                return false;

            // Получаем результат записи вместе с исходным конфликтом.
            var operationToken = _lifecycleCancellation.Token;
            var outcome = await _conflictService.TryResolveWithLocalAsync(operationToken);
            var conflict = outcome.Conflict;

            // Отбрасываем отменённый или устаревший результат.
            if (conflict == null ||
                !IsSnapshotOperationCurrent(conflict.LocalSnapshot.PlayerId, operationToken))
            {
                return false;
            }

            // Сохраняем неподтверждённый выбор для retry.
            if (!outcome.IsSuccessful)
            {
                if (_uploadService.IsPending(conflict.LocalSnapshot))
                    _uploadService.SetPendingSnapshot(conflict.LocalSnapshot);

                return false;
            }

            try
            {
                // Подтверждаем local choice и продолжаем newest pending.
                SetCurrentCloudVersion(conflict.LocalSnapshot.PlayerId, outcome.Version);
                _uploadService.Confirm(conflict.LocalSnapshot, outcome.Version.ServerRevision);
                _conflictService.CompleteResolution(conflict);
                if (_uploadService.PendingSnapshot != null)
                    _ = UploadPendingSnapshotAsync(isRetry: false);

                return true;
            }
            catch (Exception exception)
            {
                // Восстанавливаем local choice для повторной попытки.
                if (_uploadService.PendingSnapshot == null)
                    _uploadService.SetPendingSnapshot(conflict.LocalSnapshot);

                Debug.LogError($"[CloudSave] Local conflict choice failed ({exception.GetType().Name}).");
                return false;
            }
        }

        /// <summary>
        /// Загружает и целиком применяет снимок подтверждённого существующего аккаунта.
        /// </summary>
        public async Task<ExistingAccountRestoreResult> LoadExistingAccountAsync(string playerId)
        {
            // Пропускаем restore после освобождения сервиса.
            if (_isDisposed)
                return ExistingAccountRestoreResult.LoadFailed;

            // Загружаем снимок существующего аккаунта.
            var operationToken = _lifecycleCancellation.Token;
            CloudSaveReadResult readResult;
            try
            {
                readResult = await _gateway.LoadSnapshotAsync();
            }
            catch (Exception exception)
            {
                Debug.LogError($"[CloudSave] Existing account load failed ({exception.GetType().Name}).");
                return ExistingAccountRestoreResult.LoadFailed;
            }

            // Отбрасываем ответ прошлого lifecycle.
            if (!IsLifecycleCurrent(operationToken))
                return ExistingAccountRestoreResult.LoadFailed;

            // Проверяем наличие и владельца снимка.
            if (readResult == null)
            {
                Debug.LogWarning("[CloudSave] Existing account snapshot missing.");
                return ExistingAccountRestoreResult.SnapshotMissing;
            }

            if (!string.Equals(readResult.Snapshot.PlayerId, playerId, StringComparison.Ordinal))
            {
                Debug.LogWarning("[CloudSave] Existing account snapshot owner mismatch.");
                return ExistingAccountRestoreResult.OwnerMismatch;
            }

            // Восстанавливаем только пригодные игровые данные.
            if (!CloudSaveSnapshotRestorer.TryRestore(
                    readResult.Snapshot,
                    out var restoredData,
                    out var rejectionReason))
            {
                Debug.LogWarning($"[CloudSave] Existing account snapshot rejected ({rejectionReason}).");
                return ExistingAccountRestoreResult.SnapshotRejected;
            }

            try
            {
                // Повторно проверяем lifecycle перед атомарной заменой.
                if (!IsLifecycleCurrent(operationToken))
                    return ExistingAccountRestoreResult.LoadFailed;

                GameDataManager.ReplacePlayerData(restoredData);
            }
            catch (Exception exception)
            {
                Debug.LogError($"[CloudSave] Existing account snapshot apply failed ({exception.GetType().Name}).");
                return ExistingAccountRestoreResult.ApplyFailed;
            }

            // После атомарной замены данных bookkeeping не должен откатывать успешный auth flow.
            try
            {
                SetCurrentCloudVersion(playerId, readResult);
            }
            catch (Exception exception)
            {
                Debug.LogError($"[CloudSave] Existing account version cache update failed ({exception.GetType().Name}).");
            }

            // Удаляем pending восстановленного владельца без отката применённых данных.
            try
            {
                _uploadService.DiscardForOwner(playerId);
            }
            catch (Exception exception)
            {
                Debug.LogError($"[CloudSave] Existing account pending cleanup failed ({exception.GetType().Name}).");
            }

            // Завершаем runtime-состояние успешного restore.
            _uploadService.ClearCloudMissingRecord();
            _conflictService.ClearConflict();
            Debug.Log("[CloudSave] Existing account snapshot restored.");
            return ExistingAccountRestoreResult.Restored;
        }

        /// <summary>Отписывает сервис от источников checkpoint.</summary>
        public void Dispose()
        {
            // Выполняем освобождение только один раз.
            if (_isDisposed)
                return;

            // Отменяем все ответы текущего lifecycle.
            _isDisposed = true;
            _lifecycleCancellation.Cancel();
            _lifecycleCancellation.Dispose();

            // Отписываемся от источников синхронизации.
            _accountService.CurrentGuestLinked -= OnCurrentGuestLinked;
            _accountService.StateChanged -= OnAccountStateChanged;
            PlayerProgressCommitter.CommitCompleted -= OnCheckpointCommitted;
            PlayerProgressLifecycleCheckpoint.ApplicationResumed -= OnApplicationResumed;
        }

        /// <summary>
        /// Отправляет текущий pending и после него сразу продолжает с самым новым снимком.
        /// </summary>
        private async Task UploadPendingSnapshotAsync(bool isRetry)
        {
            // Не запускаем drain во время другого upload или конфликта.
            if (_isDisposed || _uploadService.IsUploadActive || CurrentConflict != null)
                return;

            // Один token защищает весь drain от смены account lifecycle.
            var operationToken = _lifecycleCancellation.Token;
            try
            {
                // Одним drain отправляем active и все появившиеся newest pending.
                await _uploadService.DrainPendingAsync(
                    (snapshot, retry) => TryUploadSnapshotAsync(
                        snapshot,
                        retry,
                        operationToken),
                    isRetry);
            }
            catch (Exception exception)
            {
                // Pending уже сохранён upload service для retry.
                Debug.LogError($"[CloudSave] Snapshot upload failed ({exception.GetType().Name}).");
            }
        }

        /// <summary>Проверяет cloud state и отправляет один active snapshot.</summary>
        private async Task<string> TryUploadSnapshotAsync(
            CloudSaveSnapshotDto snapshot,
            bool isRetry,
            CancellationToken operationToken)
        {
            // Проверяем lifecycle операции и владельца.
            if (!IsSnapshotOperationCurrent(snapshot.PlayerId, operationToken))
                return null;

            // Загружаем актуальное состояние облака.
            var cloudVersion = await _gateway.LoadSnapshotAsync();
            if (!IsSnapshotOperationCurrent(snapshot.PlayerId, operationToken))
                return null;

            // Используем newest pending как текущую local-ветку конфликта.
            var latestLocalSnapshot = _uploadService.PendingSnapshot ?? snapshot;

            // Первое исчезновение известной cloud base оставляем для подтверждающего retry.
            var cloudMissing = cloudVersion == null;
            if (cloudMissing &&
                !string.IsNullOrWhiteSpace(snapshot.BaseRevision) &&
                !_uploadService.IsCloudMissingRecordedFor(latestLocalSnapshot))
            {
                _uploadService.RecordCloudMissing(latestLocalSnapshot);
                Debug.LogError("[CloudSave] Pending base is missing in cloud; retry required.");
                return null;
            }

            // После подтверждённого отсутствия cloud создаём запись заново.
            if (cloudMissing && !string.IsNullOrWhiteSpace(snapshot.BaseRevision))
            {
                snapshot.BaseRevision = null;
                if (_uploadService.PendingSnapshot == null)
                    _uploadService.PersistSnapshot(snapshot);
            }

            // Не отправляем снимок в cloud другого владельца.
            if (!cloudMissing &&
                !string.Equals(
                    cloudVersion.Snapshot.PlayerId,
                    snapshot.PlayerId,
                    StringComparison.Ordinal))
            {
                Debug.LogError("[CloudSave] Pending snapshot owner mismatch.");
                return null;
            }

            // Подтверждаем snapshot, уже применённый сервером.
            if (!cloudMissing &&
                CloudSaveConflictService.AreSnapshotsEquivalent(
                    snapshot,
                    cloudVersion.Snapshot))
            {
                _uploadService.ClearCloudMissingRecord();
                SetCurrentCloudVersion(snapshot.PlayerId, cloudVersion);
                return cloudVersion.ServerRevision;
            }

            // Публикуем divergence вместо автоматической перезаписи.
            if (!cloudMissing &&
                _conflictService.TryDetectConflict(
                    snapshot,
                    latestLocalSnapshot,
                    cloudVersion))
            {
                _uploadService.ClearCloudMissingRecord();
                return null;
            }

            // Сбрасываем marker после согласованной классификации cloud state.
            _uploadService.ClearCloudMissingRecord();

            // Запускаем условную запись снимка.
            Debug.Log(isRetry
                ? "[CloudSave] Snapshot retry started."
                : "[CloudSave] Snapshot upload started.");

            if (!IsSnapshotOperationCurrent(snapshot.PlayerId, operationToken))
                return null;

            var result = await _gateway.SaveSnapshotAsync(snapshot)
                ?? throw new InvalidOperationException("Cloud Save returned no write result.");

            // Отбрасываем подтверждение прошлого lifecycle.
            if (!IsSnapshotOperationCurrent(snapshot.PlayerId, operationToken))
                return null;

            // Фиксируем подтверждённую cloud version.
            SetCurrentCloudVersion(snapshot.PlayerId, result);

            Debug.Log(isRetry
                ? "[CloudSave] Snapshot retry completed."
                : "[CloudSave] Snapshot upload completed.");
            return result.ServerRevision;
        }

        /// <summary>Продолжает pending владельца или создаёт первый снимок нового владельца.</summary>
        private void OnCurrentGuestLinked(string playerId)
        {
            // Игнорируем событие после освобождения сервиса.
            if (_isDisposed)
                return;

            // Восстанавливаем очередь владельца и запускаем first-upload flow.
            _uploadService.RestorePendingSnapshot(playerId);
            _ = UploadFirstSnapshotAsync(playerId);
        }

        /// <summary>Запускает durable retry после определения связанного аккаунта.</summary>
        private void OnAccountStateChanged(AccountState state)
        {
            // Игнорируем событие после освобождения сервиса.
            if (_isDisposed)
                return;

            // Отменяем ответы прошлого account lifecycle.
            var previousCancellation = _lifecycleCancellation;
            _lifecycleCancellation = new CancellationTokenSource();
            previousCancellation.Cancel();
            previousCancellation.Dispose();

            // Возобновляем синхронизацию только связанного аккаунта.
            if (state == AccountState.Linked)
                TryUploadPendingForCurrentAccount();
        }

        /// <summary>Повторяет durable pending после возврата приложения.</summary>
        private void OnApplicationResumed()
        {
            // Игнорируем событие после освобождения сервиса.
            if (_isDisposed)
                return;

            // Продолжаем pending либо проверяем cloud-only изменения.
            TryUploadPendingForCurrentAccount();
        }

        /// <summary>Фиксирует успешный локальный checkpoint для связанного аккаунта.</summary>
        private void OnCheckpointCommitted(CheckpointReason reason)
        {
            // Игнорируем checkpoint после освобождения сервиса.
            if (_isDisposed)
                return;

            // AccountLinked уже создаёт первый снимок в отдельном Task 02 flow.
            if (reason == CheckpointReason.AccountLinked ||
                !_accountService.TryGetLinkedPlayerId(out var playerId))
            {
                return;
            }

            // JSON payload отделяет снимок от последующих изменений PlayerData.
            var snapshot = CloudSaveSnapshotCodec.Capture(
                GameDataManager.PlayerData,
                playerId,
                _uploadService.GetNextLocalRevision(),
                CurrentCloudVersion?.ServerRevision);
            _uploadService.SetPendingSnapshot(snapshot);

            // Во время конфликта обновляем только local-ветку выбора.
            if (_conflictService.TryUpdateLocalSnapshot(snapshot))
                return;

            // Запускаем drain, если очередь ещё не обрабатывается.
            if (!_uploadService.IsUploadActive)
                _ = UploadPendingSnapshotAsync(isRetry: false);
        }

        /// <summary>Отправляет durable pending только после определения его владельца.</summary>
        private void TryUploadPendingForCurrentAccount()
        {
            // Ждём живой сервис и связанного владельца.
            if (_isDisposed ||
                !_accountService.TryGetLinkedPlayerId(out var playerId))
            {
                return;
            }

            // Восстанавливаем durable состояние владельца.
            _uploadService.RestorePendingSnapshot(playerId);
            RestoreCurrentCloudVersion(playerId);

            // Не вмешиваемся в активную операцию или выбор конфликта.
            if (_uploadService.IsUploadActive ||
                _conflictService.IsResolutionActive ||
                CurrentConflict != null)
            {
                return;
            }

            // Сначала отправляем owned pending.
            if (_uploadService.PendingSnapshot != null)
            {
                if (string.Equals(
                        _uploadService.PendingSnapshot.PlayerId,
                        playerId,
                        StringComparison.Ordinal))
                    _ = UploadPendingSnapshotAsync(isRetry: true);

                return;
            }

            // Без pending проверяем cloud-only изменения.
            if (CurrentCloudVersion != null && !_isCloudRefreshActive)
                _ = RefreshCloudOnlyAsync(playerId);
        }

        /// <summary>Проверяет cloud-only lag после готовности аккаунта или resume.</summary>
        private async Task RefreshCloudOnlyAsync(string playerId)
        {
            // Фиксируем lifecycle и исходную подтверждённую revision.
            var operationToken = _lifecycleCancellation.Token;
            var startingServerRevision = CurrentCloudVersion?.ServerRevision;
            if (!IsRefreshCurrent(playerId, operationToken, startingServerRevision))
                return;

            // Блокируем параллельный refresh до завершения запроса.
            _isCloudRefreshActive = true;
            var retryPending = false;
            try
            {
                // Загружаем актуальную cloud version.
                var cloudVersion = await _gateway.LoadSnapshotAsync();

                // Отбрасываем stale response и запоминаем появившийся pending.
                if (!IsRefreshCurrent(playerId, operationToken, startingServerRevision))
                {
                    retryPending = !_uploadService.IsUploadActive &&
                                   _uploadService.PendingSnapshot != null;
                    return;
                }

                // Потерянную подтверждённую запись переводим в durable retry.
                if (cloudVersion == null)
                {
                    var snapshot = CloudSaveSnapshotCodec.Capture(
                        GameDataManager.PlayerData,
                        playerId,
                        _uploadService.GetNextLocalRevision(),
                        CurrentCloudVersion.ServerRevision);
                    _uploadService.SetPendingSnapshot(snapshot);
                    _uploadService.RecordCloudMissing(snapshot);
                    Debug.LogError("[CloudSave] Confirmed cloud snapshot missing; local retry retained.");
                    return;
                }

                // Не применяем cloud другого владельца.
                if (!string.Equals(cloudVersion.Snapshot.PlayerId, playerId, StringComparison.Ordinal))
                {
                    Debug.LogError("[CloudSave] Cloud-only refresh owner mismatch.");
                    return;
                }

                // Завершаем refresh без изменений при той же revision.
                if (string.Equals(
                        cloudVersion.ServerRevision,
                        CurrentCloudVersion.ServerRevision,
                        StringComparison.Ordinal))
                {
                    return;
                }

                // Восстанавливаем только пригодные игровые данные.
                if (!CloudSaveSnapshotRestorer.TryRestore(
                        cloudVersion.Snapshot,
                        out var restoredData,
                        out var rejectionReason))
                {
                    Debug.LogWarning($"[CloudSave] Cloud-only snapshot rejected ({rejectionReason}).");
                    return;
                }

                // Повторно проверяем state перед атомарным применением.
                if (!IsRefreshCurrent(playerId, operationToken, startingServerRevision))
                    return;

                GameDataManager.ReplacePlayerData(restoredData);
                SetCurrentCloudVersion(playerId, cloudVersion);
                Debug.Log("[CloudSave] Cloud-only lag applied.");
            }
            catch (Exception exception)
            {
                // Следующий resume повторит cloud-only refresh.
                Debug.LogError($"[CloudSave] Cloud-only refresh failed ({exception.GetType().Name}).");
            }
            finally
            {
                // Освобождаем refresh gate и продолжаем pending, появившийся во время запроса.
                _isCloudRefreshActive = false;
                if (retryPending &&
                    CurrentConflict == null &&
                    IsSnapshotOperationCurrent(playerId, operationToken))
                {
                    _ = UploadPendingSnapshotAsync(isRetry: true);
                }
            }
        }

        /// <summary>Восстанавливает подтверждённую версию указанного владельца.</summary>
        private void RestoreCurrentCloudVersion(string playerId)
        {
            // Не перечитываем состояние уже выбранного владельца.
            if (string.Equals(
                    _currentCloudVersionPlayerId,
                    playerId,
                    StringComparison.Ordinal))
            {
                return;
            }

            // Загружаем per-player подтверждённую cloud version.
            CurrentCloudVersion = ConfirmedVersionStore.Load(playerId);
            _currentCloudVersionPlayerId = playerId;
        }

        /// <summary>Сохраняет metadata загруженной облачной версии.</summary>
        private void SetCurrentCloudVersion(
            string playerId,
            CloudSaveReadResult version)
        {
            // Проверяем результат чтения.
            if (version == null)
                throw new ArgumentNullException(nameof(version));

            // Приводим read metadata к общему confirmed state.
            SetCurrentCloudVersion(
                playerId,
                new CloudSaveWriteResult(
                    version.ServerRevision,
                    version.ServerModifiedAtUtc));
        }

        /// <summary>Сохраняет подтверждённую сервером версию указанного владельца.</summary>
        private void SetCurrentCloudVersion(
            string playerId,
            CloudSaveWriteResult version)
        {
            // Проверяем подтверждённую версию.
            if (version == null)
                throw new ArgumentNullException(nameof(version));

            // Обновляем runtime и durable состояние владельца.
            CurrentCloudVersion = version;
            _currentCloudVersionPlayerId = playerId;
            ConfirmedVersionStore.Save(playerId, version);
        }

        /// <summary>Проверяет, что async-операция относится к текущему lifecycle.</summary>
        private bool IsLifecycleCurrent(CancellationToken operationToken)
        {
            return !_isDisposed && !operationToken.IsCancellationRequested;
        }

        /// <summary>Проверяет lifecycle и владельца снимка.</summary>
        private bool IsSnapshotOperationCurrent(
            string playerId,
            CancellationToken operationToken)
        {
            // Сначала проверяем общий lifecycle операции.
            if (!IsLifecycleCurrent(operationToken))
                return false;

            // До завершения account resolving достаточно действующего lifecycle.
            return !_accountService.TryGetLinkedPlayerId(out var currentPlayerId) ||
                   string.Equals(currentPlayerId, playerId, StringComparison.Ordinal);
        }

        /// <summary>Проверяет, что refresh не устарел и локальное состояние не изменилось.</summary>
        private bool IsRefreshCurrent(
            string playerId,
            CancellationToken operationToken,
            string startingServerRevision)
        {
            return IsLifecycleCurrent(operationToken) &&
                   _accountService.TryGetLinkedPlayerId(out var currentPlayerId) &&
                   string.Equals(currentPlayerId, playerId, StringComparison.Ordinal) &&
                   !_uploadService.IsUploadActive &&
                   _uploadService.PendingSnapshot == null &&
                   string.Equals(
                       CurrentCloudVersion?.ServerRevision,
                       startingServerRevision,
                       StringComparison.Ordinal);
        }
    }
}
