using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Assets.Scripts.System;
using GameManagement.CloudSave.Models;
using GameManagement.Progress;
using UnityEngine;
using UnityEngine.SceneManagement;
using Vues.GameCore;

namespace GameManagement
{
    /// <summary>Сохраняет игровой прогресс и технические журналы одним зашифрованным снимком.</summary>
    public static class GameDataManager
    {
        public static PlayerData PlayerData = new PlayerData();
        public static SettingsData Settings = new SettingsData();
        public static event Action PlayerDataReplaced;
        public static event Action ProfileChanged;
        public static event Action JournalsChanged;
        public static event Action<Exception> SaveFailed;

        private const string _playerDataKey = "PlayerData";
        private const string _playerDataBackupKey = "PlayerData.Backup";
        private const string _settingsKey = "Settings";
        private static readonly ICryptoService _cryptoService = new AesCryptoService();
        private static LocalSaveEnvelope _envelope;
        private static string _durableEnvelopeJson;
        private static string _savedPlayerDataJson;
        private static bool _inTransaction;
        private static int _replacementBlocks;
        private static Exception _pendingStorageFailure;

        public static bool IsGameJustStarted = true;
        public static bool IsLoaded { get; private set; }
        public static bool IsRestoringAfterFailure { get; private set; }
        public static string ProfileId => _envelope?.ProfileId;
        public static string OwnerPlayerId => _envelope?.OwnerPlayerId;
        public static long Generation { get; private set; }
        public static long LocalRevision => _envelope?.LocalRevision ?? 0;
        public static long LastSyncedRevision => _envelope?.LastSyncedRevision ?? 0;
        public static string BaseCloudRevision => _envelope?.BaseCloudRevision;
        public static string LastCloudSyncUtc => _envelope?.LastCloudSyncUtc;
        public static bool IsLegacyOwnerUnassigned => _envelope?.LegacyOwnerUnassigned == true;
        public static bool HasUnsyncedProgress => IsLoaded && LocalRevision > LastSyncedRevision;
        public static bool IsProfileReplacementBlocked => _replacementBlocks > 0;
        public static string ActiveConflictOwner => _envelope?.ActiveConflictOwner;
        public static bool HasUncommittedProgress => IsLoaded &&
            !string.Equals(_savedPlayerDataJson, PlayerData.ToJson(), StringComparison.Ordinal);
        public static bool CanApplyCloudProgress => IsLoaded && !IsProfileReplacementBlocked &&
            !HasUncommittedProgress && SceneManager.GetActiveScene().name == "Menu";

        /// <summary>Возвращает последний durable игровой payload, не затрагивая текущий забег.</summary>
        public static string GetSavedPlayerDataJson() => _savedPlayerDataJson;
        public static CloudUploadAttempt LastUploadAttempt => _envelope?.UploadAttempt == null
            ? null : JsonUtility.FromJson<CloudUploadAttempt>(JsonUtility.ToJson(_envelope.UploadAttempt));

        /// <summary>Восстанавливает envelope либо безопасно переносит старый PlayerData.</summary>
        public static Task LoadDataAsync()
        {
            var fromPrimary = TryReadEnvelope(_playerDataKey, out var loaded, out _);
            if (!fromPrimary && !TryReadEnvelope(_playerDataBackupKey, out loaded, out _))
                loaded = CreateEnvelope(CreateDefaultPlayerData(), legacy: false);

            // Приводим игровой снимок к текущему каталогу до публикации профиля.
            _envelope = loaded;
            PlayerData = loaded.PlayerData;
            var beforeAlignment = PlayerData.ToJson();
            EnsureProgressConsistency();
            EnsureValidated(PlayerData);
            if (!string.Equals(beforeAlignment, PlayerData.ToJson(), StringComparison.Ordinal))
                _envelope.LocalRevision++;
            _envelope.PlayerData = PlayerData;
            IsLoaded = true;
            Generation++;
            try { PersistEnvelope(rotateValidPrimary: fromPrimary); }
            catch (Exception exception) { ReportSaveFailure(exception); throw; }
            Notify(PlayerDataReplaced);
            Notify(ProfileChanged);
            return Task.CompletedTask;
        }

        /// <summary>Создаёт профиль без сетевой identity; legacy требует явного принятия владельца.</summary>
        private static LocalSaveEnvelope CreateEnvelope(PlayerData data, bool legacy)
        {
            return new LocalSaveEnvelope
            {
                ProfileId = Guid.NewGuid().ToString("N"),
                PlayerData = data,
                LegacyOwnerUnassigned = legacy,
                LocalRevision = 1
            };
        }

        private static bool TryReadEnvelope(string key, out LocalSaveEnvelope envelope, out bool migrated)
        {
            envelope = null;
            migrated = false;
            if (!PlayerPrefs.HasKey(key))
                return false;

            try
            {
                var json = _cryptoService.Decrypt(PlayerPrefs.GetString(key));
                if (json.IndexOf("\"Format\"", StringComparison.Ordinal) >= 0)
                {
                    envelope = JsonUtility.FromJson<LocalSaveEnvelope>(json);
                    if (envelope == null || envelope.Format != "LostCyberHamster.LocalSave")
                        throw new InvalidOperationException("Local save format is invalid.");
                    if (envelope.Schema != 1)
                        throw new NotSupportedException("Local save schema requires another game version.");
                    if (string.IsNullOrWhiteSpace(envelope.ProfileId) ||
                        envelope.LocalRevision < 1 || envelope.LastSyncedRevision < 0 ||
                        envelope.LastSyncedRevision > envelope.LocalRevision)
                        throw new InvalidOperationException("Local save metadata is invalid.");
                    envelope.Journals ??= new List<LocalFeatureJournal>();
                    if (envelope.Journals.Any(entry => entry == null || string.IsNullOrWhiteSpace(entry.Feature) ||
                        string.IsNullOrWhiteSpace(entry.Owner)))
                        throw new InvalidOperationException("Local save journal metadata is invalid.");
                    // JsonUtility записывает null вложенного класса как объект с пустыми полями.
                    // Только полностью пустая запись означает отсутствие попытки загрузки.
                    var attempt = envelope.UploadAttempt;
                    if (attempt != null && attempt.LocalRevision == 0 &&
                        string.IsNullOrEmpty(attempt.ProfileId) &&
                        string.IsNullOrEmpty(attempt.OwnerPlayerId) &&
                        string.IsNullOrEmpty(attempt.PayloadHash) &&
                        string.IsNullOrEmpty(attempt.ExpectedCloudRevision))
                        envelope.UploadAttempt = null;
                    if (envelope.UploadAttempt != null && (envelope.UploadAttempt.ProfileId != envelope.ProfileId ||
                        envelope.UploadAttempt.OwnerPlayerId != envelope.OwnerPlayerId ||
                        envelope.UploadAttempt.LocalRevision < 1 || envelope.UploadAttempt.LocalRevision > envelope.LocalRevision ||
                        string.IsNullOrWhiteSpace(envelope.UploadAttempt.PayloadHash)))
                        throw new InvalidOperationException("Local save upload metadata is invalid.");
                    if (envelope.PlayerData == null) throw new InvalidOperationException("Local save gameplay is missing.");
                    envelope.PlayerData = PlayerData.FromJson(envelope.PlayerData.ToJson());
                }
                else
                {
                    envelope = CreateEnvelope(PlayerData.FromJson(json), legacy: true);
                    migrated = true;
                }

                var beforeValidation = envelope.PlayerData?.ToJson();
                EnsureValidated(envelope.PlayerData);
                if (!migrated && !string.Equals(beforeValidation, envelope.PlayerData.ToJson(), StringComparison.Ordinal))
                    envelope.LocalRevision++;
                return true;
            }
            catch (NotSupportedException)
            {
                // Не заменяем данные будущей версии пустым профилем.
                throw;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[GameData] Local snapshot rejected ({exception.GetType().Name}).");
                envelope = null;
                return false;
            }
        }

        /// <summary>Фиксирует уже изменённый игровой снимок; сеть в этой операции не участвует.</summary>
        public static void SaveData()
        {
            if (IsAutomationRun())
                return;
            EnsureEnvelope();
            if (_inTransaction)
                throw new InvalidOperationException("Nested gameplay save is not supported.");

            // Gameplay уже изменён до checkpoint: при ошибке сохраняем те же runtime-ссылки
            // и заработанный прогресс для повтора, откатывая только metadata этой попытки.
            var previousRevision = _envelope.LocalRevision;
            var previousSaveDate = PlayerData.LastSaveDate;
            try
            {
                EnsureValidated(PlayerData);
                if (!string.Equals(_savedPlayerDataJson, PlayerData.ToJson(), StringComparison.Ordinal))
                {
                    _envelope.LocalRevision++;
                    PlayerData.LastSaveDate = DateTime.UtcNow.ToString("o");
                }
                _envelope.PlayerData = PlayerData;
                PersistEnvelope(rotateValidPrimary: true);
            }
            catch (Exception exception)
            {
                _envelope.LocalRevision = previousRevision;
                PlayerData.LastSaveDate = previousSaveDate;
                _envelope.PlayerData = PlayerData;
                ReportSaveFailure(exception);
                throw;
            }
        }

        /// <summary>Изменяет gameplay и журналы; публикует события только после успешной записи.</summary>
        public static void ExecuteTransaction(CheckpointReason reason, Action mutation, Action afterCommit = null)
        {
            if (mutation == null) throw new ArgumentNullException(nameof(mutation));
            if (IsAutomationRun()) return;
            ExecuteMutation(mutation, gameplay: true);
            PlayerProgressCommitter.NotifyCommitCompleted(reason);
            Notify(afterCommit);
            Notify(JournalsChanged);
        }

        /// <summary>Сохраняет технические данные без изменения игровой revision и даты сохранения.</summary>
        public static void ExecuteTechnicalTransaction(Action mutation)
        {
            if (mutation == null) throw new ArgumentNullException(nameof(mutation));
            if (IsAutomationRun()) return;
            ExecuteMutation(mutation, gameplay: false);
            Notify(JournalsChanged);
        }

        private static void ExecuteMutation(Action mutation, bool gameplay)
        {
            EnsureEnvelope();
            if (_inTransaction) throw new InvalidOperationException("Nested save transaction is not supported.");
            if (IsAutomationRun()) return;
            _envelope.PlayerData = PlayerData;
            var before = JsonUtility.ToJson(_envelope);
            var previousGameplay = PlayerData.ToJson();
            _inTransaction = true;
            try
            {
                mutation();
                EnsureValidated(PlayerData);
                if (gameplay)
                {
                    if (!string.Equals(_savedPlayerDataJson, PlayerData.ToJson(), StringComparison.Ordinal))
                    {
                        _envelope.LocalRevision++;
                        PlayerData.LastSaveDate = DateTime.UtcNow.ToString("o");
                    }
                }
                else if (!string.Equals(previousGameplay, PlayerData.ToJson(), StringComparison.Ordinal))
                    throw new InvalidOperationException("Technical transaction changed gameplay data.");
                _envelope.PlayerData = PlayerData;
                PersistEnvelope(rotateValidPrimary: true, useSavedGameplay: !gameplay);
            }
            catch (Exception exception)
            {
                RestoreEnvelope(before);
                ReportSaveFailure(exception);
                throw;
            }
            finally { _inTransaction = false; }
        }

        /// <summary>Читает журнал активного либо явно указанного владельца.</summary>
        public static string GetJournalJson(string feature, string ownerPlayerId = null)
        {
            if (_envelope == null) return null;
            var owner = JournalOwner(ownerPlayerId);
            return _envelope.Journals.FirstOrDefault(entry => entry.Feature == feature && entry.Owner == owner)?.Json;
        }

        /// <summary>Меняет журнал внутри общей транзакции сохранения.</summary>
        public static void SetJournalJson(string feature, string json, string ownerPlayerId = null)
        {
            if (!_inTransaction) throw new InvalidOperationException("Journal update requires a save transaction.");
            if (string.IsNullOrWhiteSpace(feature)) throw new ArgumentException("Journal feature is required.", nameof(feature));
            var owner = JournalOwner(ownerPlayerId);
            var entry = _envelope.Journals.FirstOrDefault(item => item.Feature == feature && item.Owner == owner);
            if (entry == null)
            {
                entry = new LocalFeatureJournal { Feature = feature, Owner = owner };
                _envelope.Journals.Add(entry);
            }
            entry.Json = json;
        }

        private static string JournalOwner(string owner) => !string.IsNullOrWhiteSpace(owner)
            ? owner : OwnerPlayerId ?? "profile:" + ProfileId;

        /// <summary>Привязывает новый локальный профиль после подтверждённой авторизации.</summary>
        public static bool TryBindAuthenticatedOwner(string playerId)
        {
            if (!IsLoaded || string.IsNullOrWhiteSpace(playerId)) return false;
            if (OwnerPlayerId == playerId) return true;
            if (OwnerPlayerId != null || IsLegacyOwnerUnassigned || IsProfileReplacementBlocked) return false;
            BindOwner(playerId);
            return true;
        }

        /// <summary>Принимает владельца локальной ветки; legacy допускается только при явном выборе.</summary>
        public static void BindOwner(string playerId, bool allowLegacyAdoption = false)
        {
            if (string.IsNullOrWhiteSpace(playerId)) throw new ArgumentException("Player ID is required.", nameof(playerId));
            if (OwnerPlayerId == playerId) return;
            if (IsProfileReplacementBlocked) throw new InvalidOperationException("Profile has an unsettled operation.");
            if (OwnerPlayerId != null || IsLegacyOwnerUnassigned && !allowLegacyAdoption)
                throw new InvalidOperationException("Local profile owner requires an explicit choice.");
            ExecuteTechnicalTransaction(() =>
            {
                var oldOwner = "profile:" + ProfileId;
                foreach (var journal in _envelope.Journals.Where(entry => entry.Owner == oldOwner))
                    journal.Owner = playerId;
                _envelope.OwnerPlayerId = playerId;
                _envelope.LegacyOwnerUnassigned = false;
            });
            Generation++;
            Notify(ProfileChanged);
        }

        /// <summary>Блокирует замену профиля на время незавершённой внешней награды.</summary>
        public static IDisposable AcquireProfileReplacementBlock()
        {
            _replacementBlocks++;
            return new ProfileReplacementLease(() => _replacementBlocks--);
        }

        /// <summary>Применяет выбранный cloud baseline, сохраняя журналы всех владельцев.</summary>
        public static void ApplyCloudPlayerData(PlayerData replacement, string playerId, string serverRevision)
        {
            if (replacement == null) throw new ArgumentNullException(nameof(replacement));
            if (string.IsNullOrWhiteSpace(playerId) || string.IsNullOrWhiteSpace(serverRevision))
                throw new ArgumentException("Cloud owner and revision are required.");
            EnsureEnvelope();
            if (!CanApplyCloudProgress) throw new InvalidOperationException("Cloud restore requires a settled menu checkpoint.");
            EnsureValidated(replacement);
            var before = JsonUtility.ToJson(_envelope);
            try
            {
                if (!string.Equals(OwnerPlayerId, playerId, StringComparison.Ordinal))
                    _envelope.ProfileId = Guid.NewGuid().ToString("N");
                PlayerData = replacement;
                _envelope.PlayerData = replacement;
                _envelope.OwnerPlayerId = playerId;
                _envelope.LegacyOwnerUnassigned = false;
                _envelope.LocalRevision++;
                _envelope.LastSyncedRevision = _envelope.LocalRevision;
                _envelope.BaseCloudRevision = serverRevision;
                _envelope.LastCloudSyncUtc = DateTime.UtcNow.ToString("o");
                _envelope.UploadAttempt = null;
                _envelope.DeferredConflictOwner = null;
                _envelope.DeferredConflictRevision = null;
                ClearConflictMetadata(playerId);
                PersistEnvelope(rotateValidPrimary: true);
            }
            catch (Exception exception) { RestoreEnvelope(before); ReportSaveFailure(exception); throw; }
            Generation++;
            Notify(PlayerDataReplaced);
            Notify(ProfileChanged);
        }

        /// <summary>Запоминает exact upload до обращения к сети.</summary>
        public static void RecordCloudUploadAttempt(CloudUploadAttempt attempt)
        {
            if (attempt == null || attempt.ProfileId != ProfileId || attempt.OwnerPlayerId != OwnerPlayerId)
                throw new InvalidOperationException("Cloud upload owner changed.");
            ExecuteTechnicalTransaction(() => _envelope.UploadAttempt = attempt);
        }

        /// <summary>Обновляет подтверждённую серверную базу для совместимых editor-инструментов.</summary>
        public static void SetCloudBaseRevision(string revision)
        {
            ExecuteTechnicalTransaction(() => _envelope.BaseCloudRevision = revision);
        }

        /// <summary>Удаляет завершённую попытку отправки без изменения игрового снимка.</summary>
        public static void ClearCloudUploadAttempt()
        {
            ExecuteTechnicalTransaction(() => _envelope.UploadAttempt = null);
        }

        /// <summary>Подтверждает отправленную revision, сохраняя более новые локальные изменения.</summary>
        public static void AcknowledgeCloudUpload(CloudUploadAttempt attempt, string revision)
        {
            if (attempt == null || attempt.ProfileId != ProfileId || attempt.OwnerPlayerId != OwnerPlayerId ||
                attempt.LocalRevision > LocalRevision || string.IsNullOrWhiteSpace(revision))
                throw new InvalidOperationException("Cloud acknowledgement is stale.");
            ExecuteTechnicalTransaction(() =>
            {
                _envelope.LastSyncedRevision = Math.Max(_envelope.LastSyncedRevision, attempt.LocalRevision);
                _envelope.BaseCloudRevision = revision;
                _envelope.LastCloudSyncUtc = DateTime.UtcNow.ToString("o");
                if (_envelope.UploadAttempt?.PayloadHash == attempt.PayloadHash)
                    _envelope.UploadAttempt = null;
                ClearConflictMetadata(attempt.OwnerPlayerId);
            });
        }

        /// <summary>Связывает durable конфликт с аккаунтом внутри технической транзакции.</summary>
        public static void SetActiveConflictOwner(string playerId)
        {
            if (!_inTransaction) throw new InvalidOperationException("Conflict update requires a save transaction.");
            _envelope.ActiveConflictOwner = playerId;
        }

        private static void ClearConflictMetadata(string playerId)
        {
            _envelope.Journals.RemoveAll(entry => entry.Feature == "cloud-conflict" && entry.Owner == playerId);
            if (_envelope.ActiveConflictOwner == playerId) _envelope.ActiveConflictOwner = null;
            if (_envelope.DeferredConflictOwner == playerId)
            {
                _envelope.DeferredConflictOwner = null;
                _envelope.DeferredConflictRevision = null;
            }
        }

        public static bool IsConflictDeferred(string playerId, string revision) =>
            _envelope?.DeferredConflictOwner == playerId && _envelope?.DeferredConflictRevision == revision;

        public static void SetConflictDeferred(string playerId, string revision)
        {
            ExecuteTechnicalTransaction(() =>
            {
                _envelope.DeferredConflictOwner = playerId;
                _envelope.DeferredConflictRevision = revision;
            });
        }

        /// <summary>Заменяет игровой снимок локально, сохраняя owner и технические журналы.</summary>
        public static void ReplacePlayerData(PlayerData replacement)
        {
            if (replacement == null) throw new ArgumentNullException(nameof(replacement));
            if (IsProfileReplacementBlocked) throw new InvalidOperationException("Profile has an unsettled operation.");
            ExecuteMutation(() => PlayerData = replacement, gameplay: true);
            Generation++;
            Notify(PlayerDataReplaced);
            Notify(ProfileChanged);
        }

        private static void EnsureEnvelope()
        {
            if (_envelope != null) return;
            EnsureValidated(PlayerData);
            _envelope = CreateEnvelope(PlayerData, legacy: false);
            IsLoaded = true;
            Generation++;
        }

        private static void EnsureValidated(PlayerData data)
        {
            if (data == null) throw new InvalidOperationException("Player data is missing.");
            data.AppliedWeeklyRewardRunIds ??= new List<string>();
            data.AppliedRewardedRequestIds ??= new List<string>();
            var validation = PlayerDataValidator.Validate(data);
            if (validation.Status == PlayerDataValidationStatus.Repairable)
            {
                PlayerDataValidator.RepairSafe(data, validation);
                validation = PlayerDataValidator.Validate(data);
            }
            if (validation.Status != PlayerDataValidationStatus.Valid)
                throw new InvalidOperationException($"Player data rejected: {validation.Reason}");
        }

        private static void PersistEnvelope(bool rotateValidPrimary, bool useSavedGameplay = false)
        {
            var hadPrimary = PlayerPrefs.HasKey(_playerDataKey);
            var oldPrimary = PlayerPrefs.GetString(_playerDataKey, string.Empty);
            var hadBackup = PlayerPrefs.HasKey(_playerDataBackupKey);
            var oldBackup = PlayerPrefs.GetString(_playerDataBackupKey, string.Empty);
            // Технический ACK не превращает незавершённые coins текущего забега в checkpoint.
            var workingData = _envelope.PlayerData;
            var savedData = useSavedGameplay && !string.IsNullOrEmpty(_savedPlayerDataJson)
                ? PlayerData.FromJson(_savedPlayerDataJson) : workingData;
            string json;
            try
            {
                _envelope.PlayerData = savedData;
                json = JsonUtility.ToJson(_envelope);
            }
            finally { _envelope.PlayerData = workingData; }
            var encrypted = _cryptoService.Encrypt(json);
            try
            {
                if (rotateValidPrimary && TryReadEnvelope(_playerDataKey, out _, out _))
                    PlayerPrefs.SetString(_playerDataBackupKey, oldPrimary);
                PlayerPrefs.SetString(_playerDataKey, encrypted);
                PlayerPrefs.Save();
                _durableEnvelopeJson = json;
                _savedPlayerDataJson = savedData.ToJson();
            }
            catch (Exception exception)
            {
                _pendingStorageFailure = exception;
                RestorePreference(_playerDataKey, hadPrimary, oldPrimary);
                RestorePreference(_playerDataBackupKey, hadBackup, oldBackup);
                try { PlayerPrefs.Save(); }
                catch (Exception rollbackException) { Debug.LogError($"[GameData] Storage rollback failed ({rollbackException.GetType().Name})."); }
                throw;
            }
        }

        /// <summary>Сообщает о storage failure после восстановления runtime и PlayerPrefs.</summary>
        private static void ReportSaveFailure(Exception exception)
        {
            if (!ReferenceEquals(_pendingStorageFailure, exception)) return;
            _pendingStorageFailure = null;
            var handlers = SaveFailed;
            if (handlers == null) return;
            foreach (Action<Exception> handler in handlers.GetInvocationList())
            {
                try { handler(exception); }
                catch (Exception callbackException) { Debug.LogError($"[GameData] Save feedback failed ({callbackException.GetType().Name})."); }
            }
        }

        private static void RestoreEnvelope(string json)
        {
            if (string.IsNullOrEmpty(json)) return;
            _envelope = JsonUtility.FromJson<LocalSaveEnvelope>(json);
            PlayerData = PlayerData.FromJson(_envelope.PlayerData.ToJson());
            _envelope.PlayerData = PlayerData;
            IsRestoringAfterFailure = true;
            try { Notify(PlayerDataReplaced); }
            finally { IsRestoringAfterFailure = false; }
        }

        private static void RestorePreference(string key, bool existed, string value)
        {
            if (existed) PlayerPrefs.SetString(key, value);
            else PlayerPrefs.DeleteKey(key);
        }

        private static void Notify(Action handlers)
        {
            if (handlers == null) return;
            foreach (Action handler in handlers.GetInvocationList())
            {
                try { handler(); }
                catch (Exception exception) { Debug.LogError($"[GameData] Post-commit subscriber failed ({exception.GetType().Name})."); }
            }
        }

        private static bool IsAutomationRun()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            return AutomationRuntimePrefs.IsTestLevelAutomationRun();
#else
            return false;
#endif
        }

        public static void SaveSettings()
        {
            PlayerPrefs.SetString(_settingsKey, JsonUtility.ToJson(Settings));
            PlayerPrefs.Save();
        }

        public static void LoadSettings()
        {
            if (PlayerPrefs.HasKey(_settingsKey))
                Settings = JsonUtility.FromJson<SettingsData>(PlayerPrefs.GetString(_settingsKey)) ?? new SettingsData();
        }

        public static void ResetPlayerProgress()
        {
            if (IsProfileReplacementBlocked) throw new InvalidOperationException("Profile has an unsettled operation.");
            ExecuteMutation(() =>
            {
                var journalOwner = JournalOwner(null);
                _envelope.Journals.RemoveAll(entry => entry.Owner == journalOwner);
                _envelope.ProfileId = Guid.NewGuid().ToString("N");
                _envelope.LegacyOwnerUnassigned = false;
                _envelope.UploadAttempt = null;
                _envelope.ActiveConflictOwner = null;
                _envelope.DeferredConflictOwner = null;
                _envelope.DeferredConflictRevision = null;
                PlayerData = CreateDefaultPlayerData();
            }, gameplay: true);
            IsGameJustStarted = true;
            Generation++;
            Notify(PlayerDataReplaced);
            Notify(ProfileChanged);
        }

        public static void ResetSettings()
        {
            Settings = new SettingsData();
            SaveSettings();
        }
        private static void EnsureProgressConsistency()
        {
            if (!LevelCatalogService.HasCatalog)
            {
                return;
            }

            try
            {
                var catalog = LevelCatalogService.Catalog;
                if (catalog.IsEmpty)
                {
                    return;
                }

                var baseSnapshot = LevelProgressSnapshot.CreateFromCatalog(catalog);
                var existingSnapshot = PlayerData.Progress;

                var existingEntries = new Dictionary<LevelProgressKey, LevelProgressEntry>();
                foreach (var entry in existingSnapshot.Entries)
                {
                    existingEntries[entry.Key] = entry;
                }

                var mergedEntries = new List<LevelProgressEntry>();
                foreach (var entry in baseSnapshot.Entries)
                {
                    if (existingEntries.TryGetValue(entry.Key, out var existing))
                    {
                        mergedEntries.Add(existing);
                    }
                    else
                    {
                        mergedEntries.Add(entry);
                    }
                }

                PlayerData.Progress = new LevelProgressSnapshot(mergedEntries);
                EnsureCurrentLevelValid(catalog);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[GameDataManager] Failed to align player progress with catalog: {ex.Message}");
            }
        }

        /// <summary>
        /// Создаёт данные нового игрока с полным адресом первого уровня настроенного каталога.
        /// </summary>
        private static PlayerData CreateDefaultPlayerData()
        {
            // Требуем готовый непустой каталог до создания сохраняемых данных игрока.
            var catalog = LevelCatalogService.Catalog;
            if (catalog.IsEmpty)
            {
                throw new InvalidOperationException(
                    "Cannot create default player data: level catalog is not configured or empty.");
            }

            // Берём первый уровень в иерархическом порядке и сохраняем его полный address.
            var firstLevel = catalog.EnumerateLevels()
                .OrderBy(level => level.LocationIndex)
                .ThenBy(level => level.PartIndex)
                .ThenBy(level => level.LevelIndex)
                .FirstOrDefault();

            if (string.IsNullOrWhiteSpace(firstLevel.Address))
            {
                throw new InvalidOperationException(
                    "Cannot create default player data: first catalog level has no address.");
            }

            return new PlayerData
            {
                CurrentLevel = firstLevel.Address.Trim()
            };
        }

        private static void EnsureCurrentLevelValid(HierarchicalLevelCatalog catalog)
        {
            if (catalog.IsEmpty)
            {
                return;
            }

            if (LevelCatalogService.TryFindLevel(PlayerData.CurrentLevel, out var descriptor))
            {
                if (!string.IsNullOrWhiteSpace(descriptor.Address))
                {
                    PlayerData.CurrentLevel = descriptor.Address.Trim();
                }

                return;
            }

            var firstLevel = catalog.EnumerateLevels()
                .OrderBy(level => level.LocationIndex)
                .ThenBy(level => level.PartIndex)
                .ThenBy(level => level.LevelIndex)
                .FirstOrDefault();

            if (!string.IsNullOrWhiteSpace(firstLevel.Address))
            {
                PlayerData.CurrentLevel = firstLevel.Address.Trim();
            }
        }
    }
}
