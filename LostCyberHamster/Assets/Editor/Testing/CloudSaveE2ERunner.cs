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

        /// <summary>Показывает, можно ли продолжить ручной этап.</summary>
        public bool CanContinue =>
            State == CloudSaveE2ERunState.WaitingForUser &&
            !_waitsForExternalAction;

        /// <summary>Пауза между автоматическими шагами в секундах.</summary>
        public int StepDelaySeconds { get; set; } = 2;

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
            WriteStep($"Запущен сценарий: {CloudSaveE2EScenarioCatalog.GetTitle(scenario)}.");

            // Подключаемся только к работающей игре.
            if (!EditorApplication.isPlaying)
            {
                Fail("Сценарий доступен только в Play Mode.");
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
            WriteStep("Сценарий отменён.");
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

            await RunStepAsync("Ждём завершения первого сохранения.", async () =>
            {
                await WaitForSavedCloudAsync(token);
            }, token);

            await RunStepAsync("Проверяем сохранённый снимок.", async () =>
            {
                RequireLinkedPlayer();
                var cloudSave = await LoadCloudAsync(token);
                Require(cloudSave != null, "Облачный снимок не создан.");
                Require(
                    cloudSave.Snapshot.PlayerId == _playerId,
                    "Облачный снимок принадлежит другому игроку.");
                RequirePlayerDataEqual(
                    cloudSave.Snapshot.PlayerDataJson,
                    GameDataManager.PlayerData.ToJson(),
                    "Облако не совпадает с текущим прогрессом.");
                RequireConfirmedVersion(cloudSave);
                RequireSavedState();
            }, token);

            Pass();
        }

        /// <summary>Проверяет автоматическую синхронизацию.</summary>
        private async Task RunAutomaticSynchronizationAsync(CancellationToken token)
        {
            await RunStepAsync("Проверяем исходное сохранение.", async () =>
            {
                var cloudSave = await EnsureSavedCloudAsync(token);
                _initialRevision = cloudSave.Version.ServerRevision;
                _expectedMoney = GameDataManager.PlayerData.Money + 1;
            }, token);

            await RunStepAsync("Меняем прогресс и создаём checkpoint.", () =>
            {
                GameDataManager.PlayerData.Money = _expectedMoney;
                PlayerProgressCommitter.Commit(CheckpointReason.LevelCompleted);
            }, token);

            await WaitForNewSavedRevisionAsync(token);

            await RunStepAsync("Проверяем новый снимок в облаке.", async () =>
            {
                var cloudSave = await LoadCloudAsync(token);
                Require(cloudSave != null, "Новый облачный снимок не найден.");
                Require(
                    GetMoney(cloudSave.Snapshot.PlayerDataJson) == _expectedMoney,
                    "Облако не получило новое количество монет.");
                RequireConfirmedVersion(cloudSave);
                RequireSavedState();
            }, token);

            Pass();
        }

        /// <summary>Готовит проверку отложенной синхронизации.</summary>
        private async Task PrepareDeferredSynchronizationAsync(CancellationToken token)
        {
            await RunStepAsync("Проверяем исходное сохранение.", async () =>
            {
                var cloudSave = await EnsureSavedCloudAsync(token);
                _initialRevision = cloudSave.Version.ServerRevision;
                _expectedMoney = GameDataManager.PlayerData.Money + 1;
            }, token);

            WaitForUser(
                "Отключите сеть. Затем нажмите Continue, чтобы создать новое сохранение.");
        }

        /// <summary>Продолжает проверку отложенной синхронизации.</summary>
        private async Task ContinueDeferredSynchronizationAsync(CancellationToken token)
        {
            if (_manualStage == 0)
            {
                await RunStepAsync("Создаём сохранение без сети.", () =>
                {
                    GameDataManager.PlayerData.Money = _expectedMoney;
                    PlayerProgressCommitter.Commit(CheckpointReason.LevelCompleted);
                }, token);

                await RunStepAsync("Проверяем сохранённый pending.", async () =>
                {
                    await WaitUntilAsync(
                        () => _snapshotService.Snapshot != null &&
                              _cloudSyncService.Status == CloudSyncStatusEnum.Pending,
                        "Pending не был сохранён.",
                        token);
                }, token);

                _manualStage = 1;
                WaitForUser(
                    "Включите сеть. Затем нажмите Continue, чтобы повторить отправку.");
                return;
            }

            await RunStepAsync("Повторяем синхронизацию после возврата сети.", () =>
            {
                PlayerProgressLifecycleCheckpoint.HandleApplicationPause(isPaused: false);
            }, token);

            await WaitForNewSavedRevisionAsync(token);

            await RunStepAsync("Проверяем отправленный pending.", async () =>
            {
                var cloudSave = await LoadCloudAsync(token);
                Require(cloudSave != null, "Облачный снимок не найден.");
                Require(
                    GetMoney(cloudSave.Snapshot.PlayerDataJson) == _expectedMoney,
                    "Ожидающий прогресс не попал в облако.");
                RequireConfirmedVersion(cloudSave);
                RequireSavedState();
            }, token);

            Pass();
        }

        /// <summary>Готовит проверку восстановления прогресса.</summary>
        private async Task PrepareRestoreProgressAsync(CancellationToken token)
        {
            await PrepareFreshGuestAsync(
                unlinkServerAccount: false,
                token);

            await RunStepAsync("Создаём отдельный прогресс гостя.", () =>
            {
                GameDataManager.PlayerData.Money += 7;
                PlayerProgressCommitter.Commit(CheckpointReason.LevelCompleted);
                _initialLocalPlayerDataJson = GameDataManager.PlayerData.ToJson();
            }, token);

            await OpenSettingsAndWaitForLinkedAccountAsync(token);
            await VerifyRestoreProgressAsync(token);
        }

        /// <summary>Проверяет восстановленный прогресс.</summary>
        private async Task VerifyRestoreProgressAsync(CancellationToken token)
        {
            await RunStepAsync("Ждём завершения входа и восстановления.", async () =>
            {
                await WaitUntilAsync(
                    () => _accountService.TryGetLinkedPlayerId(out _) &&
                          _snapshotService.Snapshot == null &&
                          _cloudSyncService.Status == CloudSyncStatusEnum.Saved,
                    "Восстановление аккаунта не завершилось.",
                    token);
            }, token);

            await RunStepAsync("Проверяем восстановленный прогресс.", async () =>
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
            }, token);

            Pass();
        }

        /// <summary>Проверяет получение прогресса другого устройства.</summary>
        private async Task RunMultipleDevicesAsync(CancellationToken token)
        {
            await RunStepAsync("Создаём виртуальные устройства A и B.", async () =>
            {
                var cloudSave = await EnsureSavedCloudAsync(token);
                _initialRevision = cloudSave.Version.ServerRevision;
                _virtualDevices.Initialize(_playerId);
                _virtualDevices.SwitchTo(CloudSaveVirtualDeviceStorage.DeviceB);
                _expectedMoney = GameDataManager.PlayerData.Money + 10;
            }, token);

            await RunStepAsync("Устройство B сохраняет новый прогресс.", () =>
            {
                GameDataManager.PlayerData.Money = _expectedMoney;
                PlayerProgressCommitter.Commit(CheckpointReason.LevelCompleted);
            }, token);

            await WaitForNewSavedRevisionAsync(token);

            await RunStepAsync("Запоминаем состояние B и возвращаемся на A.", async () =>
            {
                var cloudSave = await LoadCloudAsync(token);
                Require(cloudSave != null, "Устройство B не обновило облако.");
                _expectedCloudPlayerDataJson = cloudSave.Snapshot.PlayerDataJson;
                _initialRevision = cloudSave.Version.ServerRevision;
                _virtualDevices.SwitchTo(CloudSaveVirtualDeviceStorage.DeviceA);
            }, token);

            await RunStepAsync("Устройство A проверяет облако.", () =>
            {
                PlayerProgressLifecycleCheckpoint.HandleApplicationPause(isPaused: false);
            }, token);

            await RunStepAsync("Проверяем применение прогресса B.", async () =>
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
            }, token);

            Pass();
        }

        /// <summary>Создаёт конфликт двух виртуальных устройств.</summary>
        private async Task PrepareConflictAsync(CancellationToken token)
        {
            await RunStepAsync("Создаём одинаковые устройства A и B.", async () =>
            {
                var cloudSave = await EnsureSavedCloudAsync(token);
                _initialRevision = cloudSave.Version.ServerRevision;
                _virtualDevices.Initialize(_playerId);
                _virtualDevices.SwitchTo(CloudSaveVirtualDeviceStorage.DeviceB);
                _expectedMoney = GameDataManager.PlayerData.Money + 100;
            }, token);

            await RunStepAsync("Устройство B меняет облачный прогресс.", () =>
            {
                GameDataManager.PlayerData.Money = _expectedMoney;
                PlayerProgressCommitter.Commit(CheckpointReason.LevelCompleted);
            }, token);

            await WaitForNewSavedRevisionAsync(token);

            await RunStepAsync("Возвращаем старое состояние устройства A.", async () =>
            {
                var cloudSave = await LoadCloudAsync(token);
                Require(cloudSave != null, "Устройство B не обновило облако.");
                _expectedCloudPlayerDataJson = cloudSave.Snapshot.PlayerDataJson;
                _virtualDevices.SwitchTo(CloudSaveVirtualDeviceStorage.DeviceA);
            }, token);

            await RunStepAsync("Устройство A создаёт локальное изменение.", () =>
            {
                GameDataManager.PlayerData.Crystals++;
                PlayerProgressCommitter.Commit(CheckpointReason.LevelCompleted);
            }, token);

            await RunStepAsync("Проверяем конфликт и окно выбора.", async () =>
            {
                await WaitUntilAsync(
                    () => _conflictService.CurrentConflict != null &&
                          _cloudSyncService.Status == CloudSyncStatusEnum.Conflict,
                    "Конфликт не был обнаружен.",
                    token);

                var conflict = _conflictService.CurrentConflict;
                _expectedLocalPlayerDataJson = conflict.LocalSnapshot.PlayerDataJson;
                await WaitUntilAsync(
                    () => IsElementShown(FindElement<Button>("cloud-conflict__choose-cloud")) &&
                          IsElementShown(FindElement<Button>("cloud-conflict__choose-device")),
                    "Окно выбора конфликта не открылось.",
                    token);
            }, token);

            var choice = CurrentScenario == CloudSaveE2EScenario.ConflictChooseCloud
                ? "облачный прогресс"
                : "прогресс устройства";
            WaitForUser(
                $"В открытом окне выберите {choice}. После завершения нажмите Continue.");
        }

        /// <summary>Проверяет выбранное разрешение конфликта.</summary>
        private async Task VerifyConflictResolutionAsync(CancellationToken token)
        {
            await RunStepAsync("Ждём завершения выбора.", async () =>
            {
                await WaitUntilAsync(
                    () => _conflictService.CurrentConflict == null &&
                          _snapshotService.Snapshot == null &&
                          _cloudSyncService.Status == CloudSyncStatusEnum.Saved,
                    "Конфликт не был завершён.",
                    token);
            }, token);

            await RunStepAsync("Проверяем выбранный прогресс.", async () =>
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
                    Require(cloudSave != null, "Облачный снимок не найден.");
                    RequirePlayerDataEqual(
                        cloudSave.Snapshot.PlayerDataJson,
                        _expectedLocalPlayerDataJson,
                        "Локальный прогресс не записан в облако.");
                    RequireConfirmedVersion(cloudSave);
                }

                RequireSavedState();
            }, token);

            Pass();
        }

        /// <summary>Готовит проверку статуса синхронизации.</summary>
        private async Task PrepareSynchronizationStatusAsync(CancellationToken token)
        {
            await RunStepAsync("Проверяем исходное сохранение.", async () =>
            {
                var cloudSave = await EnsureSavedCloudAsync(token);
                _initialRevision = cloudSave.Version.ServerRevision;
                _expectedMoney = GameDataManager.PlayerData.Money + 1;
            }, token);

            WaitForUser(
                "Откройте Settings и оставьте окно открытым. Затем нажмите Continue.");
        }

        /// <summary>Проверяет статус во время отправки и после неё.</summary>
        private async Task RunSynchronizationStatusAsync(CancellationToken token)
        {
            var statusRow = FindElement<VisualElement>("settings__cloud-sync-status");
            var statusLabel = FindElement<Label>("settings__lbl-cloud-sync-status");
            Require(
                statusRow != null && statusLabel != null,
                "В Settings не найдена строка статуса.");
            Require(
                statusRow.resolvedStyle.display != DisplayStyle.None,
                "Строка статуса в Settings скрыта.");

            _observedStatuses.Clear();
            _cloudSyncService.StatusChanged += OnStatusChanged;
            try
            {
                await RunStepAsync("Запускаем синхронизацию прогресса.", () =>
                {
                    GameDataManager.PlayerData.Money = _expectedMoney;
                    PlayerProgressCommitter.Commit(CheckpointReason.LevelCompleted);
                }, token);

                await WaitForNewSavedRevisionAsync(token);
            }
            finally
            {
                _cloudSyncService.StatusChanged -= OnStatusChanged;
            }

            await RunStepAsync("Проверяем показанные статусы.", async () =>
            {
                Require(
                    _observedStatuses.Contains(CloudSyncStatusEnum.Synchronizing),
                    "Статус Synchronizing не был показан.");

                var savedText = LocalizationManager.GetLocalizedString(
                    "cloud_sync_status_saved");
                await WaitUntilAsync(
                    () => statusLabel.text == savedText,
                    "Settings не показал финальный статус Saved.",
                    token);
                Require(
                    statusRow.resolvedStyle.display != DisplayStyle.None,
                    "Строка статуса скрылась после синхронизации.");
                RequireSavedState();
            }, token);

            Pass();
        }

        /// <summary>Ждёт новую подтверждённую версию и чистый pending.</summary>
        private Task WaitForNewSavedRevisionAsync(CancellationToken token)
        {
            return RunStepAsync("Ждём подтверждения новой версии.", async () =>
            {
                await WaitUntilAsync(
                    () => _snapshotService.Snapshot == null &&
                          _cloudSyncService.Status == CloudSyncStatusEnum.Saved &&
                          !string.Equals(
                              _versionStore.GetConfirmedRevision(_playerId),
                              _initialRevision,
                              StringComparison.Ordinal),
                    "Новая облачная версия не была подтверждена.",
                    token);
            }, token);
        }

        /// <summary>Ждёт готовое состояние аккаунта.</summary>
        private Task EnsureAccountReadyAsync(CancellationToken token)
        {
            return RunStepAsync("Проверяем состояние аккаунта.", async () =>
            {
                Require(
                    _conflictService.CurrentConflict == null,
                    "Сначала завершите текущий конфликт.");

                if (_accountService.State == AccountState.NotStarted)
                    _accountService.Start();

                await WaitUntilAsync(
                    () => _accountService.State == AccountState.Guest ||
                          _accountService.State == AccountState.Linked ||
                          _accountService.State == AccountState.Error,
                    "Аккаунт не завершил запуск.",
                    token);
                Require(
                    _accountService.State != AccountState.Error,
                    "Аккаунт завершил запуск с ошибкой.");
            }, token);
        }

        /// <summary>Запускает сброс через игровой экран Dev Tools.</summary>
        private async Task RunAccountResetThroughDevToolsAsync(
            bool fullReset,
            CancellationToken token)
        {
            if (fullReset)
                await EnsurePlayerAccountConfiguredAsync(token);

            await RunStepAsync("Открываем сброс аккаунта в Dev Tools.", () =>
            {
                OpenDevToolsAccountScreen();
            }, token);

            var buttonName = fullReset
                ? "FullResetTestAccountButton"
                : "ResetLocalAccountStateButton";
            var step = fullReset
                ? "Запускаем Full Reset Linked Account."
                : "Запускаем Local Account Reset.";
            await RunStepAsync(step, () =>
            {
                ClickLegacyButton(buttonName);
            }, token);

            if (fullReset)
            {
                await WaitForExternalActionAsync(
                    "Завершите Google-вход. После ответа Full Reset продолжится сам.",
                    () => _accountService.State == AccountState.NotStarted,
                    GetAccountResetError,
                    token);
            }

            Require(
                _accountService.State == AccountState.NotStarted,
                "Сброс аккаунта не завершился.");

            await RunStepAsync("Закрываем Dev Tools.", () =>
            {
                ClickLegacyButton("CloseButton");
            }, token);
        }

        /// <summary>Открывает Settings и ждёт завершения входа.</summary>
        private async Task OpenSettingsAndWaitForLinkedAccountAsync(
            CancellationToken token,
            bool requireNewAccount = false)
        {
            await EnsurePlayerAccountConfiguredAsync(token);
            var guestPlayerId = requireNewAccount
                ? AuthenticationService.Instance.PlayerId
                : null;

            await RunStepAsync("Открываем Settings.", async () =>
            {
                Require(
                    UIManager.OnModalShow != null,
                    "Игровой UI ещё не готов.");
                UIManager.OnModalShow.Invoke(ScreenEnum.SettingsModal);
                await WaitUntilAsync(
                    () => IsElementShown(
                        FindElement<Button>("settings__btn-link-account")),
                    "Кнопка аккаунта в Settings не открылась.",
                    token);
            }, token);

            var instruction = requireNewAccount
                ? "В Settings нажмите кнопку аккаунта и выберите свободный тестовый Google account."
                : "В Settings нажмите кнопку аккаунта. Для существующего аккаунта подтвердите вход ещё раз.";
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
                string.Equals(_playerId, guestPlayerId, StringComparison.Ordinal),
                "Выбран существующий аккаунт. Для первого сохранения нужен свободный тестовый аккаунт.");
        }

        /// <summary>Ждёт настройки Unity Player Accounts.</summary>
        private async Task EnsurePlayerAccountConfiguredAsync(
            CancellationToken token)
        {
            if (HasPlayerAccountClientId())
                return;

            await RunStepAsync("Открываем настройки Unity Player Accounts.", () =>
            {
                SettingsService.OpenProjectSettings(
                    PlayerAccountProjectSettings);
            }, token);

            await WaitForExternalActionAsync(
                "Настройте Unity Player Accounts Client ID. Тест продолжится сам.",
                HasPlayerAccountClientId,
                getError: null,
                token);
        }

        /// <summary>Создаёт новую локальную гостевую сессию.</summary>
        private async Task PrepareFreshGuestAsync(
            bool unlinkServerAccount,
            CancellationToken token)
        {
            await EnsureAccountReadyAsync(token);

            await RunStepAsync("Очищаем ожидающий снимок.", () =>
            {
                _snapshotService.Clear();
            }, token);

            var fullReset =
                unlinkServerAccount &&
                _accountService.State == AccountState.Linked;
            await RunAccountResetThroughDevToolsAsync(fullReset, token);

            await RunStepAsync("Запускаем свежего гостя.", async () =>
            {
                Require(
                    _accountService.State == AccountState.NotStarted,
                    "Сброс аккаунта не завершился.");
                _accountService.Start();
                await WaitUntilAsync(
                    () => _accountService.State == AccountState.Guest ||
                          _accountService.State == AccountState.Error,
                    "Свежий гость не был создан.",
                    token);
                Require(
                    _accountService.State == AccountState.Guest,
                    "Создание свежего гостя завершилось с ошибкой.");
            }, token);

            if (!unlinkServerAccount)
                return;

            await RunStepAsync("Проверяем пустое облако гостя.", async () =>
            {
                var cloudSave = await LoadCloudAsync(token);
                Require(
                    cloudSave == null,
                    "У свежего гостя уже есть облачное сохранение.");
            }, token);
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

        /// <summary>Подготавливает связанный аккаунт и согласованное облако.</summary>
        private async Task<CloudSaveReadResult> EnsureSavedCloudAsync(
            CancellationToken token)
        {
            await EnsureLinkedAccountAsync(token);
            Require(
                _conflictService.CurrentConflict == null,
                "Сначала завершите текущий конфликт.");

            await RunStepAsync("Синхронизируем исходное состояние.", () =>
            {
                PlayerProgressLifecycleCheckpoint.HandleApplicationPause(
                    isPaused: false);
            }, token);

            CloudSaveReadResult cloudSave = null;
            await RunStepAsync("Ждём готовое облачное сохранение.", async () =>
            {
                cloudSave = await WaitForSavedCloudAsync(token);
            }, token);
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
                    "Сначала завершите текущий конфликт.");

                var cloudSave = await LoadCloudAsync(token);
                if (cloudSave != null &&
                    _snapshotService.Snapshot == null &&
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
                "Локальное состояние и облако не были согласованы.");
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
                _snapshotService.Snapshot == null,
                "После синхронизации остался pending.");
            Require(
                _cloudSyncService.Status == CloudSyncStatusEnum.Saved,
                $"Ожидался статус Saved, получен {_cloudSyncService.Status}.");
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
                "Локальная подтверждённая версия не совпадает с облаком.");
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
                throw new TimeoutException("UGS не ответил за отведённое время.");
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
                    throw new InvalidOperationException("Play Mode остановлен.");
                if (DateTime.UtcNow >= deadline)
                    throw new TimeoutException(timeoutMessage);

                await Task.Delay(PollDelayMilliseconds, token);
            }
        }

        /// <summary>Выполняет асинхронный шаг после паузы.</summary>
        private async Task RunStepAsync(
            string description,
            Func<Task> action,
            CancellationToken token)
        {
            WriteStep(description);
            var delaySeconds = Math.Max(
                MinStepDelaySeconds,
                StepDelaySeconds);
            await Task.Delay(
                TimeSpan.FromSeconds(delaySeconds),
                token);
            await action();
        }

        /// <summary>Выполняет обычный шаг после паузы.</summary>
        private Task RunStepAsync(
            string description,
            Action action,
            CancellationToken token)
        {
            return RunStepAsync(
                description,
                () =>
                {
                    action();
                    return Task.CompletedTask;
                },
                token);
        }

        /// <summary>Переводит сценарий в ожидание пользователя.</summary>
        private void WaitForUser(string instruction)
        {
            State = CloudSaveE2ERunState.WaitingForUser;
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
            WriteStep(instruction);

            try
            {
                while (!isCompleted())
                {
                    token.ThrowIfCancellationRequested();
                    if (!EditorApplication.isPlaying)
                        throw new InvalidOperationException("Play Mode остановлен.");

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
            WriteStep("Сценарий пройден.");
        }

        /// <summary>Завершает сценарий с ошибкой.</summary>
        private void Fail(string message)
        {
            StopCurrentRun();
            State = CloudSaveE2ERunState.Failed;
            WriteStep($"Ошибка: {message}");
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
                Debug.LogError($"[Cloud Save E2E] {message}");
            else
                Debug.Log($"[Cloud Save E2E] {message}");
            Changed?.Invoke();
        }

        /// <summary>Запоминает опубликованный статус.</summary>
        private void OnStatusChanged(CloudSyncStatusEnum status)
        {
            _observedStatuses.Add(status);
            WriteStep($"Статус: {status}.");
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
            Require(button != null, $"В Dev Tools не найдена кнопка {name}.");
            Require(
                button.gameObject.activeInHierarchy,
                $"Кнопка {name} сейчас скрыта.");
            button.onClick.Invoke();
        }

        /// <summary>Возвращает ошибку сброса аккаунта.</summary>
        private string GetAccountResetError()
        {
            if (_accountService.State == AccountState.Error)
                return "Full Reset завершился с ошибкой.";

            var result = FindLegacyComponent<LegacyText>("ResetResult");
            if (result == null)
                return null;
            if (result.text.StartsWith(
                    "Full reset was cancelled.",
                    StringComparison.Ordinal))
            {
                return "Full Reset был отменён.";
            }

            return result.text.StartsWith("Error.", StringComparison.Ordinal)
                ? "Full Reset не был завершён. Проверьте Dev Tools и Console."
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
                error = "ProjectContext ещё не создан. Запустите игру через Bootstrap.";
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

            error = $"В DI не найдены сервисы: {string.Join(", ", missingServices)}.";
            return false;
        }
    }
}
