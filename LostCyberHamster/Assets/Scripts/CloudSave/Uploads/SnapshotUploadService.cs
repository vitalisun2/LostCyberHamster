using System;
using System.Globalization;
using System.Threading.Tasks;

namespace GameManagement.CloudSave
{
    /// <summary>Хранит и последовательно обрабатывает состояние pending upload.</summary>
    public sealed class SnapshotUploadService
    {
        /// <summary>Последний снимок, ожидающий отправки.</summary>
        private CloudSaveSnapshotDto _pendingSnapshot;

        /// <summary>Первый снимок после привязки, ожидающий подтверждения.</summary>
        private CloudSaveSnapshotDto _firstSnapshotAwaitingConfirmation;

        /// <summary>Владелец загруженного runtime-состояния очереди.</summary>
        private string _currentPlayerId;

        /// <summary>Владелец снимка с ранее обнаруженной потерей cloud base.</summary>
        private string _cloudMissingPlayerId;

        /// <summary>Revision снимка с ранее обнаруженной потерей cloud base.</summary>
        private string _cloudMissingRevision;

        /// <summary>Признак выполняющегося последовательного upload.</summary>
        private bool _isUploadActive;

        /// <summary>Следующая локальная revision текущей сессии.</summary>
        private long _nextLocalRevision = 1;

        /// <summary>Восстанавливает durable pending указанного владельца при готовности аккаунта.</summary>
        public void RestorePendingSnapshot(string playerId)
        {
            // Проверяем владельца и пропускаем уже загруженное состояние.
            if (string.IsNullOrWhiteSpace(playerId))
                throw new ArgumentException("Player ID must be provided.", nameof(playerId));
            if (string.Equals(_currentPlayerId, playerId, StringComparison.Ordinal))
                return;

            // Загружаем durable pending и продолжаем локальную нумерацию.
            _currentPlayerId = playerId;
            _pendingSnapshot = PendingSnapshotStore.Load(playerId);
            AdvanceLocalRevisionPast(_pendingSnapshot);

            // Сбрасываем first marker прошлого владельца.
            if (_firstSnapshotAwaitingConfirmation != null &&
                !string.Equals(
                    _firstSnapshotAwaitingConfirmation.PlayerId,
                    playerId,
                    StringComparison.Ordinal))
            {
                _firstSnapshotAwaitingConfirmation = null;
            }
        }

        /// <summary>Последний снимок, ожидающий отправки.</summary>
        public CloudSaveSnapshotDto PendingSnapshot => _pendingSnapshot;

        /// <summary>Один снимок сейчас проходит upload attempt.</summary>
        public bool IsUploadActive => _isUploadActive;

        /// <summary>Первый снимок после link ещё не подтверждён.</summary>
        public bool HasPendingFirstSnapshot => _firstSnapshotAwaitingConfirmation != null;

        /// <summary>Заменяет текущий pending последним снимком и сразу сохраняет его.</summary>
        public void SetPendingSnapshot(CloudSaveSnapshotDto snapshot)
        {
            // Проверяем входной снимок.
            if (snapshot == null)
                throw new ArgumentNullException(nameof(snapshot));

            // Сначала фиксируем durable-копию, затем меняем runtime-состояние.
            PendingSnapshotStore.Save(snapshot);
            var replacedPendingFirst = _firstSnapshotAwaitingConfirmation != null &&
                                       ReferenceEquals(
                                           _pendingSnapshot,
                                           _firstSnapshotAwaitingConfirmation);
            _pendingSnapshot = snapshot;
            _currentPlayerId = snapshot.PlayerId;

            // Маркер следует за тем же first snapshot и сбрасывается при его замене.
            if (replacedPendingFirst)
            {
                _firstSnapshotAwaitingConfirmation = IsSameSnapshot(
                    snapshot,
                    _firstSnapshotAwaitingConfirmation)
                    ? snapshot
                    : null;
            }
        }

        /// <summary>Сохраняет первый снимок после привязки и помечает его ожидающим подтверждения.</summary>
        public void SetFirstPendingSnapshot(CloudSaveSnapshotDto snapshot)
        {
            // Сохраняем снимок как обычный newest pending.
            SetPendingSnapshot(snapshot);

            // Помечаем этот снимок как первый после привязки.
            _firstSnapshotAwaitingConfirmation = snapshot;
        }

        /// <summary>Сохраняет изменённый снимок без замены pending в памяти.</summary>
        public void PersistSnapshot(CloudSaveSnapshotDto snapshot)
        {
            PendingSnapshotStore.Save(
                snapshot ?? throw new ArgumentNullException(nameof(snapshot)));
        }

        /// <summary>
        /// Последовательно отправляет текущий и появившиеся во время upload более новые снимки.
        /// </summary>
        public async Task DrainPendingAsync(
            Func<CloudSaveSnapshotDto, bool, Task<string>> attemptAsync,
            bool isRetry)
        {
            // Проверяем готовность очереди и upload callback.
            if (_isUploadActive || _pendingSnapshot == null)
                return;
            if (attemptAsync == null)
                throw new ArgumentNullException(nameof(attemptAsync));

            // Блокируем параллельный drain до завершения всей очереди.
            _isUploadActive = true;
            try
            {
                while (_pendingSnapshot != null)
                {
                    // Переносим pending в active, чтобы новый checkpoint мог заменить pending.
                    var activeSnapshot = _pendingSnapshot;
                    _pendingSnapshot = null;

                    // Подтверждение очищает только active; ошибка оставляет newest pending для retry.
                    string confirmedRevision;
                    try
                    {
                        confirmedRevision = await attemptAsync(activeSnapshot, isRetry);
                    }
                    catch
                    {
                        RetainForRetry(activeSnapshot);
                        throw;
                    }

                    if (string.IsNullOrWhiteSpace(confirmedRevision))
                    {
                        RetainForRetry(activeSnapshot);
                        break;
                    }

                    Confirm(activeSnapshot, confirmedRevision);
                    isRetry = false;
                }
            }
            finally
            {
                // Всегда освобождаем очередь после успеха или исключения.
                _isUploadActive = false;
            }
        }

        /// <summary>Очищает подтверждённый снимок и перебазирует более новый pending.</summary>
        public void Confirm(CloudSaveSnapshotDto snapshot, string serverRevision)
        {
            // Проверяем подтверждённый снимок и server revision.
            if (snapshot == null)
                throw new ArgumentNullException(nameof(snapshot));
            if (string.IsNullOrWhiteSpace(serverRevision))
                throw new ArgumentException("Server revision must be provided.", nameof(serverRevision));

            // Очищаем только точный подтверждённый снимок.
            PendingSnapshotStore.ClearIfMatches(snapshot);
            if (IsSameSnapshot(_pendingSnapshot, snapshot))
                _pendingSnapshot = null;

            // Завершаем ожидание первого снимка при его подтверждении.
            if (IsSameSnapshot(_firstSnapshotAwaitingConfirmation, snapshot))
                _firstSnapshotAwaitingConfirmation = null;

            // Перебазируем более новый pending на подтверждённую версию.
            RebasePendingTo(serverRevision);
        }

        /// <summary>Удаляет pending указанного владельца из памяти и durable-хранилища.</summary>
        public void DiscardForOwner(string playerId)
        {
            // Удаляем runtime pending либо его durable-копию.
            if (_pendingSnapshot != null &&
                string.Equals(_pendingSnapshot.PlayerId, playerId, StringComparison.Ordinal))
            {
                PendingSnapshotStore.ClearIfMatches(_pendingSnapshot);
                _pendingSnapshot = null;
            }
            else
            {
                var durablePending = PendingSnapshotStore.Load(playerId);
                if (durablePending != null &&
                    string.Equals(durablePending.PlayerId, playerId, StringComparison.Ordinal))
                {
                    PendingSnapshotStore.ClearIfMatches(durablePending);
                }
            }

            // Очищаем first marker этого владельца.
            if (_firstSnapshotAwaitingConfirmation != null &&
                string.Equals(
                    _firstSnapshotAwaitingConfirmation.PlayerId,
                    playerId,
                    StringComparison.Ordinal))
            {
                _firstSnapshotAwaitingConfirmation = null;
            }
        }

        /// <summary>Проверяет владельца и revision текущего pending.</summary>
        public bool IsPending(CloudSaveSnapshotDto snapshot)
        {
            return IsSameSnapshot(_pendingSnapshot, snapshot);
        }

        /// <summary>Возвращает следующую локальную revision текущей сессии.</summary>
        public string GetNextLocalRevision()
        {
            return (_nextLocalRevision++).ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>Проверяет, было ли отсутствие cloud уже зафиксировано для этого снимка.</summary>
        internal bool IsCloudMissingRecordedFor(CloudSaveSnapshotDto snapshot)
        {
            return string.Equals(
                       _cloudMissingPlayerId,
                       snapshot.PlayerId,
                       StringComparison.Ordinal) &&
                   string.Equals(
                       _cloudMissingRevision,
                       snapshot.Revision,
                       StringComparison.Ordinal);
        }

        /// <summary>Запоминает снимок, для которого cloud отсутствовал.</summary>
        internal void RecordCloudMissing(CloudSaveSnapshotDto snapshot)
        {
            _cloudMissingPlayerId = snapshot.PlayerId;
            _cloudMissingRevision = snapshot.Revision;
        }

        /// <summary>Очищает запись об отсутствующем cloud.</summary>
        internal void ClearCloudMissingRecord()
        {
            _cloudMissingPlayerId = null;
            _cloudMissingRevision = null;
        }

        /// <summary>Возвращает неподтверждённый active snapshot в очередь без потери newer pending.</summary>
        private void RetainForRetry(CloudSaveSnapshotDto snapshot)
        {
            // Не возвращаем snapshot прошлого владельца.
            if (!string.Equals(snapshot.PlayerId, _currentPlayerId, StringComparison.Ordinal))
            {
                if (ReferenceEquals(_firstSnapshotAwaitingConfirmation, snapshot))
                    _firstSnapshotAwaitingConfirmation = null;

                return;
            }

            // Восстанавливаем active snapshot, если newer pending не появился.
            if (_pendingSnapshot == null)
            {
                PendingSnapshotStore.Save(snapshot);
                _pendingSnapshot = snapshot;
                return;
            }

            // Newer pending заменяет неподтверждённый first snapshot.
            if (ReferenceEquals(_firstSnapshotAwaitingConfirmation, snapshot))
                _firstSnapshotAwaitingConfirmation = null;
        }

        /// <summary>Назначает подтверждённую server revision базой нового pending.</summary>
        private void RebasePendingTo(string serverRevision)
        {
            if (_pendingSnapshot == null)
                return;

            _pendingSnapshot.BaseRevision = serverRevision;
            PendingSnapshotStore.Save(_pendingSnapshot);
        }

        /// <summary>Продолжает локальную нумерацию после восстановленного pending.</summary>
        private void AdvanceLocalRevisionPast(CloudSaveSnapshotDto snapshot)
        {
            if (snapshot != null &&
                long.TryParse(snapshot.Revision, NumberStyles.None, CultureInfo.InvariantCulture, out var revision) &&
                revision >= _nextLocalRevision)
            {
                _nextLocalRevision = revision + 1;
            }
        }

        /// <summary>Сравнивает identity двух снимков по владельцу и локальной revision.</summary>
        private static bool IsSameSnapshot(
            CloudSaveSnapshotDto first,
            CloudSaveSnapshotDto second)
        {
            return first != null &&
                   second != null &&
                   string.Equals(first.PlayerId, second.PlayerId, StringComparison.Ordinal) &&
                   string.Equals(first.Revision, second.Revision, StringComparison.Ordinal);
        }
    }
}
