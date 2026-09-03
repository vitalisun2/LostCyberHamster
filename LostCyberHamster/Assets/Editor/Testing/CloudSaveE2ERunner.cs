using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Assets.Scripts.Account;
using GameManagement;
using GameManagement.CloudSave;
using GameManagement.CloudSave.Gateway;
using GameManagement.CloudSave.Models;
using GameManagement.CloudSave.Version;
using LostCyberHamster.UI;
using UnityEditor;
using UnityEngine;
using Unity.Services.Authentication;
using UnityEngine.UIElements;
using Vues.GameCore;
using Zenject;
using LegacyButton = UnityEngine.UI.Button;
using LegacyText = UnityEngine.UI.Text;

namespace LostCyberHamster.Editor.Testing
{
    /// <summary>Пошагово выполняет Cloud Save E2E-сценарии в Play Mode.</summary>
    public sealed class CloudSaveE2ERunner
    {
        /// <summary>Минимальная пауза между шагами.</summary>
        private const int MinStepDelaySeconds = 1;

        /// <summary>Интервал проверки ожидаемого результата.</summary>
        private const int PollDelayMilliseconds = 200;

        /// <summary>Предельное время одного ожидания.</summary>
        private const int TimeoutMilliseconds = 30000;

        /// <summary>Путь к настройкам Unity Player Accounts.</summary>
        private const string PlayerAccountSettingsPath =
            "Assets/Resources/UnityPlayerAccountSettings.asset";

        /// <summary>Раздел настроек Unity Player Accounts.</summary>
        private const string PlayerAccountProjectSettings =
            "Project/Services/Unity Player Accounts";

        /// <summary>Собирает текущую сессию аккаунта.</summary>
        private AccountService _accountService;

        /// <summary>Запускает облачную синхронизацию.</summary>
        private CloudSyncService _cloudSyncService;

        /// <summary>Разрешает конфликты сохранений.</summary>
        private ConflictService _conflictService;

        /// <summary>Хранит ожидающий снимок.</summary>
        private SnapshotService _snapshotService;

        /// <summary>Читает текущий снимок из UGS.</summary>
        private ICloudSaveGateway _cloudSaveGateway;

        /// <summary>Хранит подтверждённую облачную версию.</summary>
        private ICloudSaveVersionStore _versionStore;

        /// <summary>Переключает локальное состояние виртуальных устройств.</summary>
        private CloudSaveVirtualDeviceStorage _virtualDevices;

        /// <summary>Останавливает текущий асинхронный запуск.</summary>
        private CancellationTokenSource _cancellation;

        /// <summary>Отделяет завершения разных запусков.</summary>
        private int _runVersion;

        /// <summary>Текущий ручной этап сценария.</summary>
        private int _manualStage;

        /// <summary>Показывает, что тест ждёт действие вне окна Testing.</summary>
        private bool _waitsForExternalAction;

        /// <summary>Игрок текущего сценария.</summary>
        private string _playerId;

        /// <summary>Версия облака перед проверяемым изменением.</summary>
        private string _initialRevision;

        /// <summary>Локальный прогресс перед восстановлением.</summary>
        private string _initialLocalPlayerDataJson;

        /// <summary>Ожидаемый облачный прогресс.</summary>
        private string _expectedCloudPlayerDataJson;

        /// <summary>Ожидаемый локальный прогресс.</summary>
        private string _expectedLocalPlayerDataJson;

        /// <summary>Ожидаемое количество монет.</summary>
        private int _expectedMoney;

        /// <summary>Статусы, полученные во время проверки.</summary>
        private readonly List<CloudSyncStatusEnum> _observedStatuses =
            new List<CloudSyncStatusEnum>();

        /// <summary>Текущее состояние запуска.</summary>
        public CloudSaveE2ERunState State { get; private set; } =
            CloudSaveE2ERunState.Idle;

        /// <summary>Выбранный сценарий.</summary>
        public CloudSaveE2EScenario CurrentScenario { get; private set; }

        /// <summary>Показывает, выбран ли сценарий.</summary>
        public bool HasScenario { get; private set; }

        /// <summary>Описание текущего шага.</summary>
        public string CurrentStep { get; private set; } = string.Empty;

        /// <summary>Результат текущего шага.</summary>
        public string CurrentResult { get; private set; } = string.Empty;

        /// <summary>Показывает, можно ли продолжить ручной этап.</summary>
        public bool CanContinue =>
            State == CloudSaveE2ERunState.WaitingForUser &&
            !_waitsForExternalAction;

        /// <summary>Пауза между автоматическими шагами в секундах.</summary>
        public int StepDelaySeconds { get; set; } = 3;

        /// <summary>Показывает, выполняется ли сценарий.</summary>
        public bool IsActive =>
            State == CloudSaveE2ERunState.Running ||
            State == CloudSaveE2ERunState.WaitingForUser;

        /// <summary>Возникает после изменения запуска.</summary>
        public event Action Changed;

        /// <summary>Запускает выбранный сценарий.</summary>
        public void Start(CloudSaveE2EScenario scenario)
        {
            // Начинаем новый независимый запуск.
            StopCurrentRun();
            _runVersion++;
            _cancellation = new CancellationTokenSource();
            _manualStage = 0;
            _waitsForExternalAction = false;
            _observedStatuses.Clear();
            CurrentScenario = scenario;
            HasScenario = true;
            State = CloudSaveE2ERunState.Running;
            CurrentResult = string.Empty;
            WriteStep($"Запущен сценарий: {CloudSaveE2EScenarioCatalog.GetTitle(scenario)}.");

            // Подключаемся только к работающей игре.
            if (!EditorApplication.isPlaying)
            {
                Fail("Сначала запустите игру в Unity Editor.");
                return;
            }

            if (!TryResolveServices(out var error))
            {
                Fail(error);
                return;
            }

            _virtualDevices = new CloudSaveVirtualDeviceStorage(
                _snapshotService,
                _versionStore);
            _ = RunStartAsync(_runVersion, _cancellation.Token);
        }

        /// <summary>Продолжает сценарий после действия пользователя.</summary>
        public void Continue()
        {
            if (!CanContinue || _cancellation == null)
                return;

            State = CloudSaveE2ERunState.Running;
            Changed?.Invoke();
            _ = RunContinueAsync(_runVersion, _cancellation.Token);
        }

        /// <summary>Отменяет текущий сценарий.</summary>
        public void Cancel()
        {
            if (!IsActive)
                return;

            StopCurrentRun();
            _runVersion++;
            State = CloudSaveE2ERunState.Cancelled;
            WriteResult("Сценарий отменён.");
        }

        /// <summary>Запускает начальные шаги выбранного сценария.</summary>
        private async Task RunStartAsync(int runVersion, CancellationToken token)
        {
            try
            {
                switch (CurrentScenario)
                {
                    case CloudSaveE2EScenario.FirstCloudSave:
                        await RunFirstCloudSaveAsync(token);
                        break;

                    case CloudSaveE2EScenario.AutomaticSynchronization:
                        await RunAutomaticSynchronizationAsync(token);
                        break;

                    case CloudSaveE2EScenario.DeferredSynchronization:
                        await PrepareDeferredSynchronizationAsync(token);
                        break;

                    case CloudSaveE2EScenario.RestoreProgress:
                        await PrepareRestoreProgressAsync(token);
                        break;

                    case CloudSaveE2EScenario.MultipleDevices:
                        await RunMultipleDevicesAsync(token);
                        break;

                    case CloudSaveE2EScenario.ConflictChooseCloud:
                    case CloudSaveE2EScenario.ConflictChooseDevice:
                        await PrepareConflictAsync(token);
                        break;

                    case CloudSaveE2EScenario.SynchronizationStatus:
                        await PrepareSynchronizationStatusAsync(token);
                        break;

                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
            }
            catch (Exception exception)
            {
                if (runVersion == _runVersion)
                    Fail(exception.Message);
            }
        }

        /// <summary>Выполняет шаг после нажатия Continue.</summary>
        private async Task RunContinueAsync(int runVersion, CancellationToken token)
        {
            try
            {
                switch (CurrentScenario)
                {
                    case CloudSaveE2EScenario.DeferredSynchronization:
                        await ContinueDeferredSynchronizationAsync(token);
                        break;

                    case CloudSaveE2EScenario.ConflictChooseCloud:
                    case CloudSaveE2EScenario.ConflictChooseDevice:
                        await VerifyConflictResolutionAsync(token);
                        break;

                    case CloudSaveE2EScenario.SynchronizationStatus:
                        await RunSynchronizationStatusAsync(token);
                        break;

                    default:
                        throw new InvalidOperationException(
                            "Этот сценарий не ожидает ручного продолжения.");
                }
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
            }
            catch (Exception exception)
            {
                if (runVersion == _runVersion)
                    Fail(exception.Message);
            }
        }

        /// <summary>Подготавливает гостя и проверяет первое облачное сохранение.</summary>
        private async Task RunFirstCloudSaveAsync(CancellationToken token)
        {
            await PrepareFreshGuestAsync(
                unlinkServerAccount: true,
                token);

            await OpenSettingsAndWaitForLinkedAccountAsync(
                token,
                requireNewAccount: true);

            await RunStepAsync("Ждём первое сохранение в облаке.", async () =>
            {
                await WaitForSavedCloudAsync(token);
            }, () => "Первое сохранение появилось в облаке.", token);

            await RunStepAsync("Сравниваем прогресс устройства и облака.", async () =>
            {
                RequireLinkedPlayer();
                var cloudSave = await LoadCloudAsync(token);
                Require(cloudSave != null, "Первое облачное сохранение не создано.");
                Require(
                    cloudSave.Snapshot.PlayerId == _playerId,
                    "Облачное сохранение относится к другому аккаунту.");
                RequirePlayerDataEqual(
                    cloudSave.Snapshot.PlayerDataJson,
                    GameDataManager.PlayerData.ToJson(),
                    "Облако не совпадает с текущим прогрессом.");
                RequireConfirmedVersion(cloudSave);
                RequireSavedState();
            }, () => "Прогресс устройства полностью сохранён в облаке.", token);

            Pass();
        }

        /// <summary>Проверяет автоматическую синхронизацию.</summary>
        private async Task RunAutomaticSynchronizationAsync(CancellationToken token)
        {
            await RunStepAsync("Проверяем текущий прогресс в облаке.", async () =>
            {
                var cloudSave = await EnsureSavedCloudAsync(token);
                _initialRevision = cloudSave.Version.ServerRevision;
                _expectedMoney = GameDataManager.PlayerData.Money + 1;
            }, () => "Текущий прогресс уже сохранён в облаке.", token);

            await RunStepAsync("Добавляем монету и сохраняем игру.", () =>
            {
                GameDataManager.PlayerData.Money = _expectedMoney;
                PlayerProgressCommitter.Commit(CheckpointReason.LevelCompleted);
            }, () => $"На устройстве сохранено {_expectedMoney} монет.", token);

            await WaitForNewSavedRevisionAsync(token);

            await RunStepAsync("Проверяем новый прогресс в облаке.", async () =>
            {
                var cloudSave = await LoadCloudAsync(token);
                Require(cloudSave != null, "Новый прогресс не появился в облаке.");
                Require(
                    GetMoney(cloudSave.Snapshot.PlayerDataJson) == _expectedMoney,
                    "Облако не получило новое количество монет.");
                RequireConfirmedVersion(cloudSave);
                RequireSavedState();
            }, () => $"В облаке сохранено {_expectedMoney} монет.", token);

            Pass();
        }

        /// <summary>Готовит проверку отложенной синхронизации.</summary>
        private async Task PrepareDeferredSynchronizationAsync(CancellationToken token)
        {
            await RunStepAsync("Проверяем текущий прогресс в облаке.", async () =>
            {
                var cloudSave = await EnsureSavedCloudAsync(token);
                _initialRevision = cloudSave.Version.ServerRevision;
                _expectedMoney = GameDataManager.PlayerData.Money + 1;
            }, () => "Текущий прогресс уже сохранён в облаке.", token);

            WaitForUser(
                "Отключите интернет и нажмите Continue. Игра сохранит новый прогресс на устройстве.");
        }

        /// <summary>Продолжает проверку отложенной синхронизации.</summary>
        private async Task ContinueDeferredSynchronizationAsync(CancellationToken token)
        {
            if (_manualStage == 0)
            {
                await RunStepAsync("Добавляем монету и сохраняем игру без сети.", () =>
                {
                    GameDataManager.PlayerData.Money = _expectedMoney;
                    PlayerProgressCommitter.Commit(CheckpointReason.LevelCompleted);
                }, () => $"На устройстве сохранено {_expectedMoney} монет.", token);

                await RunStepAsync("Проверяем, что прогресс ждёт отправки.", async () =>
                {
                    await WaitUntilAsync(
                        () => GetTrackedPendingSnapshot() != null &&
                              _cloudSyncService.Status == CloudSyncStatusEnum.Pending,
                        "Прогресс не остался на устройстве для последующей отправки.",
                        token);
                }, () => "Прогресс сохранён на устройстве и ждёт отправки.", token);

                _manualStage = 1;
                WaitForUser(
                    "Включите интернет и нажмите Continue. Игра повторит отправку прогресса в облако.");
                return;
            }

            await RunStepAsync("Повторно сохраняем прогресс после включения интернета.", () =>
            {
                PlayerProgressLifecycleCheckpoint.HandleApplicationPause(isPaused: false);
            }, () => "Игра снова отправляет прогресс в облако.", token);

            await WaitForNewSavedRevisionAsync(token);

            await RunStepAsync("Проверяем прогресс в облаке.", async () =>
            {
                var cloudSave = await LoadCloudAsync(token);
                Require(cloudSave != null, "Облачное сохранение не найдено.");
                Require(
                    GetMoney(cloudSave.Snapshot.PlayerDataJson) == _expectedMoney,
                    "Ожидающий прогресс не попал в облако.");
                RequireConfirmedVersion(cloudSave);
                RequireSavedState();
            }, () => $"В облаке сохранено {_expectedMoney} монет.", token);

            Pass();
        }

        /// <summary>Готовит проверку восстановления прогресса.</summary>
        private async Task PrepareRestoreProgressAsync(CancellationToken token)
        {
            await PrepareFreshGuestAsync(
                unlinkServerAccount: false,
                token);

            await RunStepAsync("Добавляем гостю семь монет перед входом.", () =>
            {
                GameDataManager.PlayerData.Money += 7;
                PlayerProgressCommitter.Commit(CheckpointReason.LevelCompleted);
                _initialLocalPlayerDataJson = GameDataManager.PlayerData.ToJson();
            }, () => $"Гостевой прогресс сохранён: {GameDataManager.PlayerData.Money} монет.", token);

            await OpenSettingsAndWaitForLinkedAccountAsync(token);
            await VerifyRestoreProgressAsync(token);
        }

        /// <summary>Проверяет восстановленный прогресс.</summary>
        private async Task VerifyRestoreProgressAsync(CancellationToken token)
        {
            await RunStepAsync("Ждём восстановления прогресса аккаунта.", async () =>
            {
                await WaitUntilAsync(
                    () => _accountService.TryGetLinkedPlayerId(out _playerId) &&
                          GetTrackedPendingSnapshot() == null &&
                          _cloudSyncService.Status == CloudSyncStatusEnum.Saved,
                    "Восстановление аккаунта не завершилось.",
                    token);
            }, () => "Вход завершён, прогресс аккаунта восстановлен.", token);

            await RunStepAsync("Сравниваем прогресс устройства и облака.", async () =>
            {
                RequireLinkedPlayer();
                var cloudSave = await LoadCloudAsync(token);
                Require(cloudSave != null, "У существующего аккаунта нет снимка.");
                var localJson = GameDataManager.PlayerData.ToJson();
                RequirePlayerDataEqual(
                    cloudSave.Snapshot.PlayerDataJson,
                    localJson,
                    "Локальный прогресс не совпадает с облачным.");
                Require(
                    !ArePlayerDataEqual(
                        cloudSave.Snapshot.PlayerDataJson,
                        _initialLocalPlayerDataJson),
                    "Для теста нужен аккаунт с прогрессом, отличным от гостевого.");
                RequireConfirmedVersion(cloudSave);
                RequireSavedState();
            }, () => $"На устройстве восстановлено {GameDataManager.PlayerData.Money} монет.", token);

            Pass();
        }

        /// <summary>Проверяет получение прогресса другого устройства.</summary>
        private async Task RunMultipleDevicesAsync(CancellationToken token)
        {
            await RunStepAsync(
                "Готовим два устройства с одинаковым прогрессом.",
                async () =>
                {
                    var cloudSave = await EnsureSavedCloudAsync(token);
                    _initialRevision = cloudSave.Version.ServerRevision;
                    _virtualDevices.Initialize(_playerId);
                    _virtualDevices.SwitchTo(CloudSaveVirtualDeviceStorage.DeviceB);
                    _expectedMoney = GameDataManager.PlayerData.Money + 10;
                },
                () => "Оба устройства начинают с одного сохранения.",
                token);

            await RunStepAsync(
                "На устройстве B зарабатываем 10 монет.",
                () =>
                {
                    GameDataManager.PlayerData.Money = _expectedMoney;
                    PlayerProgressCommitter.Commit(CheckpointReason.LevelCompleted);
                },
                () => $"На устройстве B стало {_expectedMoney} монет.",
                token);

            await WaitForNewSavedRevisionAsync(token);

            await RunStepAsync(
                "Возвращаемся на устройство A.",
                async () =>
                {
                    var cloudSave = await LoadCloudAsync(token);
                    Require(cloudSave != null, "Устройство B не обновило облако.");
                    _expectedCloudPlayerDataJson = cloudSave.Snapshot.PlayerDataJson;
                    _initialRevision = cloudSave.Version.ServerRevision;
                    _virtualDevices.SwitchTo(CloudSaveVirtualDeviceStorage.DeviceA);
                },
                () => "Устройство A открыто со старым прогрессом.",
                token);

            await RunStepAsync(
                "Проверяем, появился ли на устройстве A прогресс с устройства B.",
                () =>
                {
                    PlayerProgressLifecycleCheckpoint.HandleApplicationPause(isPaused: false);
                },
                () => "Устройство A начало загружать изменения из облака.",
                token);

            await RunStepAsync(
                "Ждём прогресс устройства B на устройстве A.",
                async () =>
                {
                    await WaitUntilAsync(
                        () => string.Equals(
                                  _versionStore.GetConfirmedRevision(_playerId),
                                  _initialRevision,
                                  StringComparison.Ordinal) &&
                              ArePlayerDataEqual(
                                  GameDataManager.PlayerData.ToJson(),
                                  _expectedCloudPlayerDataJson),
                        "Устройство A не приняло прогресс устройства B.",
                        token);
                    RequireSavedState();
                },
                () => "Устройство A получило прогресс устройства B.",
                token);

            Pass();
        }

        /// <summary>Создаёт конфликт двух виртуальных устройств.</summary>
        private async Task PrepareConflictAsync(CancellationToken token)
        {
            await RunStepAsync(
                "Готовим два устройства с одинаковым прогрессом.",
                async () =>
                {
                    var cloudSave = await EnsureSavedCloudAsync(token);
                    _initialRevision = cloudSave.Version.ServerRevision;
                    _virtualDevices.Initialize(_playerId);
                    _virtualDevices.SwitchTo(CloudSaveVirtualDeviceStorage.DeviceB);
                    _expectedMoney = GameDataManager.PlayerData.Money + 100;
                },
                () => "Оба устройства начинают с одного сохранения.",
                token);

            await RunStepAsync(
                "На устройстве B зарабатываем 100 монет.",
                () =>
                {
                    GameDataManager.PlayerData.Money = _expectedMoney;
                    PlayerProgressCommitter.Commit(CheckpointReason.LevelCompleted);
                },
                () => $"На устройстве B стало {_expectedMoney} монет.",
                token);

            await WaitForNewSavedRevisionAsync(token);

            await RunStepAsync(
                "Возвращаемся на устройство A.",
                async () =>
                {
                    var cloudSave = await LoadCloudAsync(token);
                    Require(cloudSave != null, "Устройство B не обновило облако.");
                    _expectedCloudPlayerDataJson = cloudSave.Snapshot.PlayerDataJson;
                    _virtualDevices.SwitchTo(CloudSaveVirtualDeviceStorage.DeviceA);
                },
                () => "Устройство A открыто со старым прогрессом.",
                token);

            await RunStepAsync(
                "На устройстве A получаем один кристалл.",
                () =>
                {
                    GameDataManager.PlayerData.Crystals++;
                    PlayerProgressCommitter.Commit(CheckpointReason.LevelCompleted);
                },
                () => "На обоих устройствах теперь разные изменения.",
                token);

            await RunStepAsync(
                "Ждём предложение выбрать сохранение.",
                async () =>
                {
                    await WaitUntilAsync(
                        () => _conflictService.CurrentConflict != null &&
                              _cloudSyncService.Status == CloudSyncStatusEnum.Conflict,
                        "Игра не предложила выбрать между прогрессом устройства и облака.",
                        token);

                    var conflict = _conflictService.CurrentConflict;
                    _expectedLocalPlayerDataJson = conflict.LocalSnapshot.PlayerDataJson;
                    await WaitUntilAsync(
                        () => IsElementShown(FindElement<Button>("cloud-conflict__choose-cloud")) &&
                              IsElementShown(FindElement<Button>("cloud-conflict__choose-device")),
                        "Окно выбора конфликта не открылось.",
                        token);
                },
                () => "Игра показала выбор между облаком и устройством.",
                token);

            var choice = CurrentScenario == CloudSaveE2EScenario.ConflictChooseCloud
                ? "облачный прогресс"
                : "прогресс устройства";
            WaitForUser(
                $"В окне выбора сохранения выберите {choice}, дождитесь закрытия окна и нажмите Continue.");
        }

        /// <summary>Проверяет выбранное разрешение конфликта.</summary>
        private async Task VerifyConflictResolutionAsync(CancellationToken token)
        {
            await RunStepAsync(
                "Ждём применения выбора игрока.",
                async () =>
                {
                    await WaitUntilAsync(
                        () => _conflictService.CurrentConflict == null &&
                              GetTrackedPendingSnapshot() == null &&
                              _cloudSyncService.Status == CloudSyncStatusEnum.Saved,
                        "Конфликт не был завершён.",
                        token);
                },
                () => "Выбор применён, окно закрыто.",
                token);

            await RunStepAsync(
                "Проверяем выбранный прогресс.",
                async () =>
                {
                    if (CurrentScenario == CloudSaveE2EScenario.ConflictChooseCloud)
                    {
                        RequirePlayerDataEqual(
                            GameDataManager.PlayerData.ToJson(),
                            _expectedCloudPlayerDataJson,
                            "На устройстве не применён облачный прогресс.");
                    }
                    else
                    {
                        RequirePlayerDataEqual(
                            GameDataManager.PlayerData.ToJson(),
                            _expectedLocalPlayerDataJson,
                            "На устройстве изменился выбранный локальный прогресс.");

                        var cloudSave = await LoadCloudAsync(token);
                        Require(cloudSave != null, "Облачное сохранение не найдено.");
                        RequirePlayerDataEqual(
                            cloudSave.Snapshot.PlayerDataJson,
                            _expectedLocalPlayerDataJson,
                            "Локальный прогресс не записан в облако.");
                        RequireConfirmedVersion(cloudSave);
                    }

                    RequireSavedState();
                },
                () => CurrentScenario == CloudSaveE2EScenario.ConflictChooseCloud
                    ? "На устройстве сохранён выбранный облачный прогресс."
                    : "Прогресс устройства сохранён и в игре, и в облаке.",
                token);

            Pass();
        }

        /// <summary>Готовит проверку статуса синхронизации.</summary>
        private async Task PrepareSynchronizationStatusAsync(CancellationToken token)
        {
            await RunStepAsync(
                "Проверяем текущий прогресс в облаке.",
                async () =>
                {
                    var cloudSave = await EnsureSavedCloudAsync(token);
                    _initialRevision = cloudSave.Version.ServerRevision;
                    _expectedMoney = GameDataManager.PlayerData.Money + 1;
                },
                () => "Прогресс сохранён и готов к проверке.",
                token);

            WaitForUser(
                "Откройте настройки игры и оставьте их открытыми. Затем нажмите Continue.");
        }

        /// <summary>Проверяет статус во время отправки и после неё.</summary>
        private async Task RunSynchronizationStatusAsync(CancellationToken token)
        {
            var statusRow = FindElement<VisualElement>("settings__cloud-sync-status");
            var statusLabel = FindElement<Label>("settings__lbl-cloud-sync-status");
            Require(
                statusRow != null && statusLabel != null,
                "В настройках не показано состояние облачного сохранения.");
            Require(
                statusRow.resolvedStyle.display != DisplayStyle.None,
                "Состояние облачного сохранения скрыто в настройках.");

            _observedStatuses.Clear();
            _cloudSyncService.StatusChanged += OnStatusChanged;
            try
            {
                await RunStepAsync(
                    "Зарабатываем одну монету.",
                    () =>
                    {
                        GameDataManager.PlayerData.Money = _expectedMoney;
                        PlayerProgressCommitter.Commit(CheckpointReason.LevelCompleted);
                    },
                    () => "Игра начала сохранять новый прогресс.",
                    token);

                await WaitForNewSavedRevisionAsync(token);
            }
            finally
            {
                _cloudSyncService.StatusChanged -= OnStatusChanged;
            }

            await RunStepAsync(
                "Проверяем сообщения о сохранении.",
                async () =>
                {
                    Require(
                        _observedStatuses.Contains(CloudSyncStatusEnum.Synchronizing),
                        "Во время отправки игра не показала, что прогресс сохраняется.");

                    var savedText = LocalizationManager.GetLocalizedString(
                        "cloud_sync_status_saved");
                    await WaitUntilAsync(
                        () => statusLabel.text == savedText,
                        "После отправки настройки не показали, что прогресс сохранён.",
                        token);
                    Require(
                        statusRow.resolvedStyle.display != DisplayStyle.None,
                        "После отправки состояние облачного сохранения исчезло из настроек.");
                    RequireSavedState();
                },
                () => "Игрок увидел начало и завершение сохранения.",
                token);

            Pass();
        }

        /// <summary>Ждёт новую подтверждённую версию и чистый pending.</summary>
        private Task WaitForNewSavedRevisionAsync(CancellationToken token)
        {
            return RunStepAsync(
                "Ждём сохранение нового прогресса в облаке.",
                async () =>
                {
                    await WaitUntilAsync(
                        () => GetTrackedPendingSnapshot() == null &&
                              _cloudSyncService.Status == CloudSyncStatusEnum.Saved &&
                              !string.Equals(
                                  _versionStore.GetConfirmedRevision(_playerId),
                                  _initialRevision,
                                  StringComparison.Ordinal),
                        "Облако не подтвердило сохранение нового прогресса.",
                        token);
                },
                () => "Новый прогресс сохранён в облаке.",
                token);
        }

        /// <summary>Готовит аккаунт к запуску теста.</summary>
        private Task EnsureAccountReadyAsync(CancellationToken token)
        {
            return RunStepAsync(
                "Определяем, как игрок вошёл в игру.",
                async () =>
                {
                    Require(
                        _conflictService.CurrentConflict == null,
                        "Сначала выберите, какой прогресс сохранить в текущем конфликте.");

                    if (_accountService.State == AccountState.NotStarted)
                        _accountService.Start();

                    await WaitUntilAsync(
                        () => _accountService.State == AccountState.Guest ||
                              _accountService.State == AccountState.Linked ||
                              _accountService.State == AccountState.Error,
                        "Гостевой или связанный аккаунт не успел загрузиться.",
                        token);
                    Require(
                        _accountService.State != AccountState.Error,
                        "Не удалось загрузить гостевой или связанный аккаунт.");
                },
                () => _accountService.State == AccountState.Linked
                    ? "Игрок продолжает с привязанным аккаунтом."
                    : "Игрок продолжает как гость.",
                token);
        }

        /// <summary>Удаляет привязку аккаунта или данные входа на устройстве.</summary>
        private async Task RunAccountResetThroughDevToolsAsync(
            bool fullReset,
            CancellationToken token)
        {
            if (fullReset)
                await EnsurePlayerAccountConfiguredAsync(token);

            OpenDevToolsAccountScreen();

            var buttonName = fullReset
                ? "FullResetTestAccountButton"
                : "ResetLocalAccountStateButton";
            var step = fullReset
                ? "Готовим игру как на новом устройстве."
                : "Выходим из аккаунта на этом устройстве.";
            var stepResult = fullReset
                ? "Привязка аккаунта удалена, игрок вышел из аккаунта."
                : "Игрок вышел из аккаунта на этом устройстве.";
            await RunStepAsync(
                step,
                async () =>
                {
                    ClickLegacyButton(buttonName);
                    if (fullReset)
                    {
                        await WaitForExternalActionAsync(
                            "Подтвердите аккаунт через Google для удаления привязки.",
                            () => _accountService.State == AccountState.NotStarted,
                            GetAccountResetError,
                            token);
                    }

                    Require(
                        _accountService.State == AccountState.NotStarted,
                        "Сброс аккаунта не завершился.");
                },
                () => stepResult,
                token);

            ClickLegacyButton("CloseButton");
        }

        /// <summary>Открывает настройки игры и ждёт завершения входа.</summary>
        private async Task OpenSettingsAndWaitForLinkedAccountAsync(
            CancellationToken token,
            bool requireNewAccount = false)
        {
            await EnsurePlayerAccountConfiguredAsync(token);
            var guestPlayerId = requireNewAccount
                ? AuthenticationService.Instance.PlayerId
                : null;

            var instruction = requireNewAccount
                ? "В настройках игры нажмите кнопку аккаунта и выберите свободный тестовый Google-аккаунт."
                : "В настройках игры нажмите кнопку аккаунта. Для существующего аккаунта подтвердите вход ещё раз.";
            var step = requireNewAccount
                ? "Привязываем гостевой прогресс к новому аккаунту."
                : "Входим в существующий аккаунт.";
            await RunStepAsync(
                step,
                async () =>
                {
                    Require(
                        UIManager.OnScreenShow != null,
                        "Интерфейс игры ещё не загрузился.");
                    SettingsScreenController.OpenFrom(ScreenEnum.HomeScreen);
                    await WaitUntilAsync(
                        () => IsElementShown(
                            FindElement<Button>("settings__btn-link-account")),
                        "В настройках не появилась кнопка входа в аккаунт.",
                        token);
                    await WaitForExternalActionAsync(
                        instruction,
                        () => _accountService.State == AccountState.Linked,
                        () => _accountService.State == AccountState.Error
                            ? "Вход в аккаунт завершился с ошибкой."
                            : null,
                        token);

                    RequireLinkedPlayer();
                    Require(
                        !requireNewAccount ||
                        string.Equals(
                            _playerId,
                            guestPlayerId,
                            StringComparison.Ordinal),
                        "Выбран существующий аккаунт. Для первого сохранения нужен свободный тестовый аккаунт.");
                },
                () => requireNewAccount
                    ? "Гостевой прогресс привязан к новому аккаунту."
                    : "Игрок вошёл в существующий аккаунт.",
                token);
        }

        /// <summary>Открывает настройки входа, если они не заполнены.</summary>
        private async Task EnsurePlayerAccountConfiguredAsync(
            CancellationToken token)
        {
            if (HasPlayerAccountClientId())
                return;

            await RunStepAsync(
                "Готовим вход через Google.",
                async () =>
                {
                    SettingsService.OpenProjectSettings(
                        PlayerAccountProjectSettings);
                    await WaitForExternalActionAsync(
                        "Настройте вход через Google в открытых Project Settings.",
                        HasPlayerAccountClientId,
                        getError: null,
                        token);
                },
                () => "Вход через Google настроен.",
                token);
        }

        /// <summary>Создаёт новую локальную гостевую сессию.</summary>
        private async Task PrepareFreshGuestAsync(
            bool unlinkServerAccount,
            CancellationToken token)
        {
            await EnsureAccountReadyAsync(token);

            ClearTrackedPendingSnapshot();

            var fullReset =
                unlinkServerAccount &&
                _accountService.State == AccountState.Linked;
            await RunAccountResetThroughDevToolsAsync(fullReset, token);

            await RunStepAsync(
                "Создаём нового гостя.",
                async () =>
                {
                    Require(
                        _accountService.State == AccountState.NotStarted,
                        "Сброс аккаунта не завершился.");
                    _accountService.Start();
                    await WaitUntilAsync(
                        () => _accountService.State == AccountState.Guest ||
                              _accountService.State == AccountState.Error,
                        "Новый гостевой аккаунт не был создан.",
                        token);
                    Require(
                        _accountService.State == AccountState.Guest,
                        "Не удалось создать новый гостевой аккаунт.");
                },
                () => "Создан новый гостевой аккаунт.",
                token);

            if (!unlinkServerAccount)
                return;

            await RunStepAsync(
                "Проверяем новый гостевой профиль.",
                async () =>
                {
                    var cloudSave = await LoadCloudAsync(token);
                    Require(
                        cloudSave == null,
                        "У свежего гостя уже есть облачное сохранение.");
                },
                () => "Новый гость начинает без облачного прогресса.",
                token);
        }

        /// <summary>Подготавливает новый связанный тестовый аккаунт.</summary>
        private async Task EnsureLinkedAccountAsync(CancellationToken token)
        {
            await EnsureAccountReadyAsync(token);
            if (_accountService.State == AccountState.Linked)
            {
                RequireLinkedPlayer();
                return;
            }

            await OpenSettingsAndWaitForLinkedAccountAsync(token);
        }

        /// <summary>Сохраняет текущий прогресс связанного аккаунта в облаке.</summary>
        private async Task<CloudSaveReadResult> EnsureSavedCloudAsync(
            CancellationToken token)
        {
            await EnsureLinkedAccountAsync(token);
            Require(
                _conflictService.CurrentConflict == null,
                "Сначала выберите, какой прогресс сохранить в текущем конфликте.");

            CloudSaveReadResult cloudSave = null;
            await RunStepAsync(
                "Сохраняем текущий прогресс в облаке.",
                async () =>
                {
                    PlayerProgressLifecycleCheckpoint.HandleApplicationPause(
                        isPaused: false);
                    cloudSave = await WaitForSavedCloudAsync(token);
                },
                () => "Прогресс игрока сохранён в облаке.",
                token);
            return cloudSave;
        }

        /// <summary>Ждёт согласованный локальный и облачный снимок.</summary>
        private async Task<CloudSaveReadResult> WaitForSavedCloudAsync(
            CancellationToken token)
        {
            RequireLinkedPlayer();
            var deadline = DateTime.UtcNow.AddMilliseconds(TimeoutMilliseconds);
            while (DateTime.UtcNow < deadline)
            {
                token.ThrowIfCancellationRequested();
                Require(
                    _conflictService.CurrentConflict == null,
                    "Сначала выберите, какой прогресс сохранить в текущем конфликте.");

                var cloudSave = await LoadCloudAsync(token);
                if (cloudSave != null &&
                    GetTrackedPendingSnapshot() == null &&
                    _cloudSyncService.Status == CloudSyncStatusEnum.Saved &&
                    string.Equals(
                        _versionStore.GetConfirmedRevision(_playerId),
                        cloudSave.Version.ServerRevision,
                        StringComparison.Ordinal))
                {
                    return cloudSave;
                }

                await Task.Delay(PollDelayMilliseconds, token);
            }

            throw new TimeoutException(
                "Прогресс на устройстве и в облаке не успел синхронизироваться.");
        }

        /// <summary>Получает Player ID связанного аккаунта.</summary>
        private void RequireLinkedPlayer()
        {
            Require(
                _accountService.TryGetLinkedPlayerId(out _playerId),
                "Для сценария нужен связанный аккаунт.");
        }

        /// <summary>Проверяет завершённое состояние синхронизации.</summary>
        private void RequireSavedState()
        {
            Require(
                GetTrackedPendingSnapshot() == null,
                "После синхронизации прогресс всё ещё ждёт отправки.");
            Require(
                _cloudSyncService.Status == CloudSyncStatusEnum.Saved,
                "Облачное сохранение не завершилось.");
        }

        /// <summary>Возвращает pending текущего тестового игрока.</summary>
        private CloudSaveSnapshot GetTrackedPendingSnapshot()
        {
            return string.IsNullOrWhiteSpace(_playerId)
                ? null
                : _snapshotService.GetPending(_playerId);
        }

        /// <summary>Удаляет pending текущего или последнего тестового игрока.</summary>
        private void ClearTrackedPendingSnapshot()
        {
            if (_accountService.TryGetLinkedPlayerId(out var linkedPlayerId))
            {
                _playerId = linkedPlayerId;
                _snapshotService.Clear(linkedPlayerId);
                return;
            }

            if (!string.IsNullOrWhiteSpace(_playerId))
                _snapshotService.Clear(_playerId);
        }

        /// <summary>Проверяет подтверждённую версию облака.</summary>
        private void RequireConfirmedVersion(CloudSaveReadResult cloudSave)
        {
            var confirmedRevision = _versionStore.GetConfirmedRevision(_playerId);
            Require(
                string.Equals(
                    confirmedRevision,
                    cloudSave.Version.ServerRevision,
                    StringComparison.Ordinal),
                "Игра не подтвердила, что устройство и облако содержат один прогресс.");
        }

        /// <summary>Загружает облако с ограничением времени.</summary>
        private async Task<CloudSaveReadResult> LoadCloudAsync(
            CancellationToken token)
        {
            var loadTask = _cloudSaveGateway.LoadSnapshotAsync();
            var timeoutTask = Task.Delay(TimeoutMilliseconds, token);
            var completedTask = await Task.WhenAny(loadTask, timeoutTask);
            if (completedTask != loadTask)
            {
                token.ThrowIfCancellationRequested();
                throw new TimeoutException("Облако не ответило за отведённое время.");
            }

            return await loadTask;
        }

        /// <summary>Ждёт выполнения условия без блокировки Editor.</summary>
        private static async Task WaitUntilAsync(
            Func<bool> condition,
            string timeoutMessage,
            CancellationToken token)
        {
            var deadline = DateTime.UtcNow.AddMilliseconds(TimeoutMilliseconds);
            while (!condition())
            {
                token.ThrowIfCancellationRequested();
                if (!EditorApplication.isPlaying)
                    throw new InvalidOperationException("Игра остановлена в Unity Editor.");
                if (DateTime.UtcNow >= deadline)
                    throw new TimeoutException(timeoutMessage);

                await Task.Delay(PollDelayMilliseconds, token);
            }
        }

        /// <summary>Выполняет асинхронный шаг после паузы.</summary>
        private async Task RunStepAsync(
            string description,
            Func<Task> action,
            Func<string> getResult,
            CancellationToken token)
        {
            CurrentResult = string.Empty;
            WriteStep(description);
            var delaySeconds = Math.Max(
                MinStepDelaySeconds,
                StepDelaySeconds);
            await Task.Delay(
                TimeSpan.FromSeconds(delaySeconds),
                token);
            await action();
            WriteResult(getResult());
            await Task.Delay(
                TimeSpan.FromSeconds(delaySeconds),
                token);
        }

        /// <summary>Выполняет обычный шаг после паузы.</summary>
        private Task RunStepAsync(
            string description,
            Action action,
            Func<string> getResult,
            CancellationToken token)
        {
            return RunStepAsync(
                description,
                () =>
                {
                    action();
                    return Task.CompletedTask;
                },
                getResult,
                token);
        }

        /// <summary>Переводит сценарий в ожидание пользователя.</summary>
        private void WaitForUser(string instruction)
        {
            State = CloudSaveE2ERunState.WaitingForUser;
            CurrentResult = string.Empty;
            WriteStep(instruction);
        }

        /// <summary>Ждёт действие пользователя вне окна Testing.</summary>
        private async Task WaitForExternalActionAsync(
            string instruction,
            Func<bool> isCompleted,
            Func<string> getError,
            CancellationToken token)
        {
            _waitsForExternalAction = true;
            State = CloudSaveE2ERunState.WaitingForUser;
            CurrentResult = string.Empty;
            WriteStep(instruction);

            try
            {
                while (!isCompleted())
                {
                    token.ThrowIfCancellationRequested();
                    if (!EditorApplication.isPlaying)
                        throw new InvalidOperationException("Игра остановлена в Unity Editor.");

                    var error = getError?.Invoke();
                    if (!string.IsNullOrWhiteSpace(error))
                        throw new InvalidOperationException(error);

                    await Task.Delay(PollDelayMilliseconds, token);
                }
            }
            finally
            {
                _waitsForExternalAction = false;
                if (State == CloudSaveE2ERunState.WaitingForUser &&
                    !token.IsCancellationRequested)
                {
                    State = CloudSaveE2ERunState.Running;
                    Changed?.Invoke();
                }
            }
        }

        /// <summary>Завершает сценарий успешно.</summary>
        private void Pass()
        {
            StopCurrentRun();
            State = CloudSaveE2ERunState.Passed;
            WriteResult("Сценарий пройден.");
        }

        /// <summary>Завершает сценарий с ошибкой.</summary>
        private void Fail(string message)
        {
            StopCurrentRun();
            State = CloudSaveE2ERunState.Failed;
            WriteResult($"Ошибка: {message}");
        }

        /// <summary>Останавливает текущие ожидания.</summary>
        private void StopCurrentRun()
        {
            _cancellation?.Cancel();
            _cancellation?.Dispose();
            _cancellation = null;
            _waitsForExternalAction = false;
        }

        /// <summary>Показывает текущий шаг и пишет его в Console.</summary>
        private void WriteStep(string message)
        {
            CurrentStep = message;
            if (State == CloudSaveE2ERunState.Failed)
                Debug.LogError($"[Cloud Save E2E] Шаг: {message}");
            else
                Debug.Log($"[Cloud Save E2E] Шаг: {message}");
            Changed?.Invoke();
        }

        /// <summary>Показывает результат шага и пишет его в Console.</summary>
        private void WriteResult(string message)
        {
            CurrentResult = message;
            if (State == CloudSaveE2ERunState.Failed)
                Debug.LogError($"[Cloud Save E2E] Результат: {message}");
            else
                Debug.Log($"[Cloud Save E2E] Результат: {message}");
            Changed?.Invoke();
        }

        /// <summary>Запоминает опубликованный статус.</summary>
        private void OnStatusChanged(CloudSyncStatusEnum status)
        {
            _observedStatuses.Add(status);
            var result = status switch
            {
                CloudSyncStatusEnum.Saved =>
                    "Прогресс сохранён в облаке.",
                CloudSyncStatusEnum.Synchronizing =>
                    "Прогресс отправляется в облако.",
                CloudSyncStatusEnum.Pending =>
                    "Прогресс сохранён на устройстве и ждёт отправки.",
                CloudSyncStatusEnum.Conflict =>
                    "Прогресс изменился на двух устройствах — нужно выбрать версию.",
                _ => throw new ArgumentOutOfRangeException(nameof(status), status, null)
            };
            WriteResult(result);
        }

        /// <summary>Проверяет настройку Unity Player Accounts.</summary>
        private static bool HasPlayerAccountClientId()
        {
            var settings = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(
                PlayerAccountSettingsPath);
            if (settings == null)
                return false;

            var serializedSettings = new SerializedObject(settings);
            var clientId = serializedSettings.FindProperty("clientId");
            return clientId != null &&
                   !string.IsNullOrWhiteSpace(clientId.stringValue);
        }

        /// <summary>Открывает экран аккаунта в Dev Tools.</summary>
        private static void OpenDevToolsAccountScreen()
        {
            var resetButton = FindLegacyComponent<LegacyButton>(
                "ResetLocalAccountStateButton");
            if (resetButton != null &&
                resetButton.gameObject.activeInHierarchy)
            {
                return;
            }

            var accountButton = FindLegacyComponent<LegacyButton>("AccountButton");
            if (accountButton != null &&
                accountButton.gameObject.activeInHierarchy)
            {
                accountButton.onClick.Invoke();
                return;
            }

            var backButton = FindLegacyComponent<LegacyButton>("BackButton");
            if (backButton != null &&
                backButton.gameObject.activeInHierarchy)
            {
                backButton.onClick.Invoke();
                ClickLegacyButton("AccountButton");
                return;
            }

            ClickLegacyButton("OpenButton");
            ClickLegacyButton("AccountButton");
        }

        /// <summary>Нажимает кнопку игрового Dev Tools.</summary>
        private static void ClickLegacyButton(string name)
        {
            var button = FindLegacyComponent<LegacyButton>(name);
            Require(button != null, "В меню разработчика не найдена нужная кнопка.");
            Require(
                button.gameObject.activeInHierarchy,
                "Нужная кнопка в меню разработчика недоступна.");
            button.onClick.Invoke();
        }

        /// <summary>Возвращает ошибку сброса аккаунта.</summary>
        private string GetAccountResetError()
        {
            if (_accountService.State == AccountState.Error)
                return "Не удалось удалить привязку аккаунта.";

            var result = FindLegacyComponent<LegacyText>("ResetResult");
            if (result == null)
                return null;
            if (result.text.StartsWith(
                    "Full reset was cancelled.",
                    StringComparison.Ordinal))
            {
                return "Удаление привязки аккаунта отменено.";
            }

            return result.text.StartsWith("Error.", StringComparison.Ordinal)
                ? "Не удалось удалить привязку аккаунта. Проверьте меню разработчика и Console."
                : null;
        }

        /// <summary>Находит компонент игрового Dev Tools.</summary>
        private static T FindLegacyComponent<T>(string name)
            where T : Component
        {
            var components = UnityEngine.Object.FindObjectsByType<T>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            foreach (var component in components)
            {
                if (component.name == name)
                    return component;
            }

            return null;
        }

        /// <summary>Находит UI-элемент в открытых документах.</summary>
        private static T FindElement<T>(string name)
            where T : VisualElement
        {
            var documents = UnityEngine.Object.FindObjectsByType<UIDocument>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            foreach (var document in documents)
            {
                var element = document.rootVisualElement?.Q<T>(name);
                if (element != null)
                    return element;
            }

            return null;
        }

        /// <summary>Проверяет, что UI-элемент действительно показан.</summary>
        private static bool IsElementShown(VisualElement element)
        {
            if (element == null)
                return false;

            for (var current = element; current != null; current = current.parent)
            {
                if (current.resolvedStyle.display == DisplayStyle.None)
                    return false;
            }

            return true;
        }

        /// <summary>Возвращает число монет из снимка.</summary>
        private static int GetMoney(string playerDataJson)
        {
            return PlayerData.FromJson(playerDataJson).Money;
        }

        /// <summary>Проверяет равенство прогресса без времени локальной записи.</summary>
        private static void RequirePlayerDataEqual(
            string firstJson,
            string secondJson,
            string message)
        {
            Require(ArePlayerDataEqual(firstJson, secondJson), message);
        }

        /// <summary>Сравнивает прогресс без времени локальной записи.</summary>
        private static bool ArePlayerDataEqual(string firstJson, string secondJson)
        {
            if (string.IsNullOrWhiteSpace(firstJson) ||
                string.IsNullOrWhiteSpace(secondJson))
            {
                return false;
            }

            var first = PlayerData.FromJson(firstJson);
            var second = PlayerData.FromJson(secondJson);
            first.LastSaveDate = string.Empty;
            second.LastSaveDate = string.Empty;
            return string.Equals(
                first.ToJson(),
                second.ToJson(),
                StringComparison.Ordinal);
        }

        /// <summary>Прерывает сценарий при невыполненном условии.</summary>
        private static void Require(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException(message);
        }

        /// <summary>Получает runtime-сервисы из ProjectContext.</summary>
        private bool TryResolveServices(out string error)
        {
            error = null;
            if (!ProjectContext.HasInstance)
            {
                error = "Игровые сервисы ещё не запущены. Запустите игру со сцены Bootstrap.";
                return false;
            }

            var container = ProjectContext.Instance.Container;
            _accountService = container.TryResolve<AccountService>();
            _cloudSyncService = container.TryResolve<CloudSyncService>();
            _conflictService = container.TryResolve<ConflictService>();
            _snapshotService = container.TryResolve<SnapshotService>();
            _cloudSaveGateway = container.TryResolve<ICloudSaveGateway>();
            _versionStore = container.TryResolve<ICloudSaveVersionStore>();

            var missingServices = new List<string>();
            if (_accountService == null)
                missingServices.Add(nameof(AccountService));
            if (_cloudSyncService == null)
                missingServices.Add(nameof(CloudSyncService));
            if (_conflictService == null)
                missingServices.Add(nameof(ConflictService));
            if (_snapshotService == null)
                missingServices.Add(nameof(SnapshotService));
            if (_cloudSaveGateway == null)
                missingServices.Add(nameof(ICloudSaveGateway));
            if (_versionStore == null)
                missingServices.Add(nameof(ICloudSaveVersionStore));

            if (missingServices.Count == 0)
                return true;

            error = "Не запустились необходимые сервисы аккаунта и облачных сохранений.";
            return false;
        }
    }
}
