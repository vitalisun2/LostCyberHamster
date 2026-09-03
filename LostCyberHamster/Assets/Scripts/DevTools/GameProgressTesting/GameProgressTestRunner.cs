#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Assets.Scripts.Common.Models;
using Assets.Scripts.DevTools.Gameplay;
using Assets.Scripts.GameManagerLogic;
using Assets.Scripts.Gameplay;
using Assets.Scripts.System;
using GameManagement;
using GameManagement.Progress;
using LostCyberHamster.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Assets.Scripts.DevTools.GameProgressTesting
{
    /// <summary>Управляет простым ручным тестом поверх штатного игрового прогресса.</summary>
    public sealed class GameProgressTestRunner
    {
        private enum ActiveOperationKind
        {
            None,
            SelectionNavigation,
            LevelCompletion
        }

        private const string PlayerPrefsStateKey = "DevTools.GameProgressTesting.RunState";
        private const string ConsoleLogTag = "[Game Progress Testing]";
        private const int MaxRandomScore = 50;
        private const int PreparedExperiencePoints =
            PlayerExperienceService.PlayerLevelThreshold - 1;
        private const int PollDelayMilliseconds = 100;
        private const int VisibleActionDelayMilliseconds = 250;
        private const double DefaultTimeoutSeconds = 20d;
        private const double RuntimeObservationIntervalSeconds = 0.1d;

        private readonly GameProgressTestRunState _state;
        private CancellationTokenSource _operationCancellation;
        private ActiveOperationKind _activeOperationKind;
        private string _busyActionTitle = string.Empty;
        private bool _selectionNavigationReachedMenu;
        private int _operationVersion;
        private double _nextRuntimeObservationAt;
        private bool _reconcilePending;
        private bool _waitingForGameData;

        private GameProgressTestRunner()
        {
            _state = LoadState();
            if (_state.IsBusy)
            {
                _state.IsBusy = false;
                SetStatus(
                    "Предыдущее действие прервано domain reload.",
                    "Domain reload: незавершённое действие остановлено.",
                    LogType.Warning);
                SaveState();
            }

            if (Application.isPlaying && _state.IsActive)
                RequestReconcile();
        }

        public static GameProgressTestRunner Shared { get; } = new GameProgressTestRunner();

        public event Action Changed;

        public string Status => _state.Status;

        public string CurrentAction => _state.CurrentAction;

        public bool IsBusy => _state.IsBusy;

        public bool CanUsePrimaryAction =>
            Application.isPlaying &&
            IsGameDataReady() &&
            !_state.IsBusy &&
            !IsTargetIntroActive();

        public bool CanCancel =>
            !_state.IsBusy &&
            _state.IsActive;

        public bool CanResetProgress =>
            Application.isPlaying &&
            IsGameDataReady() &&
            !_state.IsBusy;

        public bool CanWinCurrentLevelWithRandomValues =>
            Application.isPlaying &&
            IsGameDataReady() &&
            !_state.IsBusy;

        public bool CanPrepareLevelUp =>
            Application.isPlaying &&
            IsGameDataReady() &&
            !_state.IsBusy &&
            !IsLevelUpPrepared();

        public string PrimaryActionTitle
        {
            get
            {
                if (Application.isPlaying && !IsGameDataReady())
                    return "Waiting for game data";

                if (IsTargetIntroActive())
                    return "Skip intro manually";

                if (IsTargetGameplayActive())
                    return "Win with random score";

                if (_state.IsBusy)
                    return string.IsNullOrWhiteSpace(_busyActionTitle)
                        ? "Opening Select Level"
                        : _busyActionTitle;

                if (!_state.IsActive)
                    return "Start";

                return "Continue";
            }
        }

        public string CurrentPoint
        {
            get
            {
                var currentLevel =
                    GameDataManager.PlayerData?.CurrentLevel?.Trim();
                return string.IsNullOrWhiteSpace(currentLevel)
                    ? "Текущий уровень не определён"
                    : FormatTarget(currentLevel);
            }
        }

        /// <summary>Открывает текущий уровень, задаёт случайный score и завершает его с тремя звёздами.</summary>
        public void WinCurrentLevelWithRandomValues()
        {
            if (!CanWinCurrentLevelWithRandomValues)
                return;

            RunOperationAsync(
                WinSavedCurrentLevelWithRandomValuesAsync,
                ActiveOperationKind.LevelCompletion,
                "Opening current level");
        }

        /// <summary>Запускает Start, завершение текущего target или возврат в Select Level.</summary>
        public void RunPrimaryAction()
        {
            if (!Application.isPlaying || _state.IsBusy || IsTargetIntroActive())
                return;

            if (!IsGameDataReady())
            {
                RequestReconcile();
                return;
            }

            if (!_state.IsActive)
            {
                var firstLevel = GetOrderedLevels().FirstOrDefault()?.Address;
                RunOperationAsync(
                    StartAsync,
                    ActiveOperationKind.SelectionNavigation,
                    BuildOpeningActionTitle(firstLevel));
                return;
            }

            if (IsTargetGameplayActive())
            {
                RunOperationAsync(
                    WinCurrentLevelAsync,
                    ActiveOperationKind.LevelCompletion,
                    $"Completing {FormatShortTarget(_state.TargetLevelAddress)}");
                return;
            }

            RunOperationAsync(
                ContinueToSelectionAsync,
                ActiveOperationKind.SelectionNavigation,
                BuildOpeningActionTitle(_state.TargetLevelAddress));
        }

        /// <summary>Отменяет только навигационный контекст теста, не изменяя реальный прогресс.</summary>
        public void Cancel()
        {
            if (!CanCancel)
                return;

            CancelTransientOperation();
            ClearNavigationContext(
                "Тест отменён. Реальный игровой прогресс сохранён.",
                "Cancel: navigation context очищен; реальный игровой прогресс сохранён.");
            SaveState();
            Changed?.Invoke();
        }

        /// <summary>Повторяет Reset Progress из Gameplay DevTools и очищает контекст runner.</summary>
        public void ResetProgress()
        {
            if (!CanResetProgress)
                return;

            CancelTransientOperation();

            // Используем тот же runtime action, что и Gameplay DevTools.
            var resetResult = new GameplayDevToolsService().ResetProgress();

            ClearNavigationContext(
                resetResult.Message,
                $"Reset Progress: {resetResult.Message}; navigation context очищен.");
            SaveState();
            Changed?.Invoke();
        }

        /// <summary>Сохраняет XP в точке перед Level Up, не начисляя награду и не вызывая Level Up flow.</summary>
        public void PrepareLevelUp()
        {
            if (!CanPrepareLevelUp)
                return;

            var playerData = GameDataManager.PlayerData;
            if (playerData == null)
                return;

            playerData.ExperiencePoints = PreparedExperiencePoints;
            GameDataManager.SaveData();
            UIManager.OnRepaintScreen?.Invoke();

            var status =
                $"Level Up prepared: {PreparedExperiencePoints} / " +
                PlayerExperienceService.PlayerLevelThreshold;
            SetStatus(status, status);
            SaveState();
            Changed?.Invoke();
        }

        /// <summary>Останавливает текущую операцию при выходе из Play Mode.</summary>
        public void HandlePlayModeStopped()
        {
            CancelTransientOperation();
            _reconcilePending = _state.IsActive;
            _waitingForGameData = false;
            SetStatus(
                "Play Mode остановлен.",
                "Play Mode stopped.");
            SaveState();
            Changed?.Invoke();
        }

        /// <summary>Запрашивает безопасную сверку контекста после готовности Play Mode game data.</summary>
        public void HandlePlayModeStarted()
        {
            if (_state.IsActive)
            {
                RequestReconcile();
            }
            else
            {
                SetStatus(
                    "Play Mode готов. Команды доступны.",
                    "Play Mode entered: команды доступны.");
            }

            _nextRuntimeObservationAt = 0d;
            SaveState();
            Changed?.Invoke();
        }

        /// <summary>Обновляет ожидание каталога и наблюдаемое runtime-состояние.</summary>
        public void Tick()
        {
            ObserveRuntimeState();
        }

        private async void RunOperationAsync(
            Func<CancellationToken, Task> operation,
            ActiveOperationKind operationKind,
            string busyActionTitle)
        {
            var operationVersion = ++_operationVersion;
            _operationCancellation?.Dispose();
            _operationCancellation = new CancellationTokenSource();
            var token = _operationCancellation.Token;

            _activeOperationKind = operationKind;
            _busyActionTitle = busyActionTitle;
            _selectionNavigationReachedMenu = false;
            _state.IsBusy = true;
            RecordAction($"{busyActionTitle}.");
            SaveState();
            Changed?.Invoke();

            try
            {
                await operation(token);
            }
            catch (OperationCanceledException)
            {
                if (operationVersion == _operationVersion)
                {
                    SetStatus(
                        "Действие отменено.",
                        $"{busyActionTitle}: действие отменено.",
                        LogType.Warning);
                }
            }
            catch (Exception exception)
            {
                if (operationVersion == _operationVersion)
                {
                    SetStatus(
                        $"Ошибка: {exception.Message}",
                        $"{busyActionTitle}: {exception.Message}",
                        LogType.Error,
                        exception.ToString());
                }
            }
            finally
            {
                if (operationVersion == _operationVersion)
                {
                    _state.IsBusy = false;
                    _activeOperationKind = ActiveOperationKind.None;
                    _busyActionTitle = string.Empty;
                    _selectionNavigationReachedMenu = false;
                    SaveState();
                    Changed?.Invoke();
                }
            }
        }

        private async Task StartAsync(CancellationToken token)
        {
            var firstLevel = GetOrderedLevels().FirstOrDefault();
            if (firstLevel == null || string.IsNullOrWhiteSpace(firstLevel.Address))
            {
                RequestReconcile();
                return;
            }

            // Start создаёт fresh реальный save и отключает только tutorial redirect.
            GameDataManager.ResetPlayerProgress();
            DevToolsRuntimeState.UnlockAllLevels = false;
            GameDataManager.PlayerData.IsTutorialCompleted = true;
            GameDataManager.SaveData();
            UIManager.OnRepaintScreen?.Invoke();

            _state.IsActive = true;
            _state.TargetLevelAddress = firstLevel.Address.Trim();
            _state.LastCompletedLevelAddress = string.Empty;
            _state.LastWinScore = 0;
            SetStatus(
                $"Fresh progress создан. Открывается {FormatTarget(_state.TargetLevelAddress)}.",
                $"Start: fresh progress создан. Next: {FormatTarget(_state.TargetLevelAddress)}.");

            await OpenSelectLevelAsync(
                _state.TargetLevelAddress,
                openTargetDayPart: true,
                token: token);
            token.ThrowIfCancellationRequested();

            SetStatus(
                $"Select Level открыт на target day part. Вручную выберите {FormatTarget(_state.TargetLevelAddress)}.",
                $"Start completed: открыт target day part для {FormatTarget(_state.TargetLevelAddress)}.");
        }

        private async Task WinCurrentLevelAsync(CancellationToken token)
        {
            var currentLevel = GameDataManager.PlayerData?.CurrentLevel?.Trim();
            if (!string.Equals(
                    currentLevel,
                    _state.TargetLevelAddress?.Trim(),
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"Открыт {FormatTarget(currentLevel)}; target теста " +
                    $"{FormatTarget(_state.TargetLevelAddress)}. " +
                    "Нажмите Continue и выберите target.");
            }

            var levelController = RequireLevelController();
            var hamster = RequireHamster();

            // Доводим live score до случайного итогового значения штатными coin events.
            DevToolsRuntimeState.UnlockAllLevels = false;
            UIManager.OnRepaintScreen?.Invoke();
            var finalScore = hamster.RunScore > MaxRandomScore
                ? hamster.RunScore
                : UnityEngine.Random.Range(hamster.RunScore, MaxRandomScore + 1);
            hamster.Lives.Value = 3;
            for (var score = hamster.RunScore; score < finalScore; score++)
                hamster.CollectableCollectedEvent.Invoke(ObstacleTypeEnum.collectableCoin);

            _state.PendingCompletionLevelAddress = currentLevel;
            _state.LastWinScore = finalScore;
            SetStatus(
                $"Завершается {FormatTarget(currentLevel)}: 3 stars, score={finalScore}.",
                $"Win requested: {FormatTarget(currentLevel)}, 3 stars, score={finalScore}.");
            SaveState();
            await Task.Delay(VisibleActionDelayMilliseconds, token);
            levelController.Finish();

            await WaitUntilAsync(
                () => IsRealWinShown() && GetRealLevelStars(currentLevel) == 3,
                "Реальный Win с тремя звёздами не подтверждён.",
                token);

            CommitCompletedTarget(currentLevel, finalScore);
        }

        private async Task WinSavedCurrentLevelWithRandomValuesAsync(
            CancellationToken token)
        {
            var currentLevel =
                GameDataManager.PlayerData?.CurrentLevel?.Trim();
            if (string.IsNullOrWhiteSpace(currentLevel))
            {
                throw new InvalidOperationException(
                    "Текущий gameplay-уровень не определён.");
            }

            // Запускаем тот же CurrentLevel, который использует Play на Home.
            DevToolsRuntimeState.UnlockAllLevels = false;
            if (!IsGameSceneForLevel(currentLevel) ||
                GetGameState() == GameState.FINISHED)
            {
                SetStatus(
                    $"Открывается {FormatTarget(currentLevel)}.",
                    $"Win Current Level: открывается {FormatTarget(currentLevel)}.");
                await LoadSceneAndWaitAsync("Game", token);
            }
            else
            {
                SetStatus(
                    $"{FormatTarget(currentLevel)} уже открыт.",
                    $"Win Current Level: текущий уровень уже открыт; reload пропущен.");
            }

            // Возобновляем штатную Pause modal либо прямую runtime-паузу.
            if (GetGameState() == GameState.PAUSED)
            {
                var resumeButton = FindVisibleElement<Button>("btn__play");
                if (resumeButton != null)
                {
                    SendClick(resumeButton);
                }
                else
                {
                    var gameManager = RequireLevelController()
                        .LevelData?.GameManager ??
                        throw new InvalidOperationException(
                            "GameManager текущего уровня недоступен.");
                    gameManager.Resume();
                }
            }

            await WaitUntilAsync(
                () => IsCurrentLevelInState(
                    currentLevel,
                    GameState.INTRO,
                    GameState.PLAYING),
                $"{FormatTarget(currentLevel)} не перешёл в Intro или Gameplay.",
                token);

            // Автоматически пропускаем intro и ждём полностью готовый gameplay UI.
            if (GetGameState() == GameState.INTRO)
            {
                SetStatus(
                    $"Пропускается Intro: {FormatTarget(currentLevel)}.",
                    $"Win Current Level: пропускается Intro для {FormatTarget(currentLevel)}.");
                RequireLevelController().SkipIntro();
            }

            await WaitUntilAsync(
                () => IsGameplayReady(currentLevel),
                $"Gameplay для {FormatTarget(currentLevel)} не готов.",
                token);

            // Доводим score штатными coin events и сохраняем гарантированные три звезды.
            var levelController = RequireLevelController();
            var hamster = RequireHamster();
            var finalScore = hamster.RunScore >= MaxRandomScore
                ? hamster.RunScore
                : UnityEngine.Random.Range(hamster.RunScore, MaxRandomScore + 1);
            for (var score = hamster.RunScore; score < finalScore; score++)
                hamster.CollectableCollectedEvent.Invoke(ObstacleTypeEnum.collectableCoin);

            hamster.Lives.Value = 3;
            SetStatus(
                $"Завершается {FormatTarget(currentLevel)}: 3 stars, score={finalScore}.",
                $"Win Current Level: {FormatTarget(currentLevel)}, 3 stars, score={finalScore}.");
            if (GetGameState() != GameState.PLAYING)
                throw new InvalidOperationException("Gameplay завершился до команды Win.");

            levelController.Finish();

            await WaitUntilAsync(
                () => IsRealWinShown() || IsJourneyCompleteShown(),
                "Штатное окно результата уровня не появилось.",
                token);

            var resultName = IsRealWinShown()
                ? "Win modal"
                : "Journey Complete modal";
            SetStatus(
                $"{resultName}: {FormatTarget(currentLevel)}, 3 stars, score={finalScore}.",
                $"Completed: {FormatTarget(currentLevel)}, 3 stars, score={finalScore}; {resultName} shown.");
        }

        private async Task ContinueToSelectionAsync(CancellationToken token)
        {
            if (!ReconcileNavigationContext())
                return;

            var requestedTarget = _state.TargetLevelAddress;
            var focusLevel = ResolveSelectionFocusLevel(requestedTarget);
            if (string.IsNullOrWhiteSpace(focusLevel))
                throw new InvalidOperationException("Не удалось определить уровень для фокуса Select Level.");

            var isTargetOpen =
                !string.IsNullOrWhiteSpace(requestedTarget) &&
                IsRealLevelOpen(requestedTarget);
            await OpenSelectLevelAsync(
                focusLevel,
                openTargetDayPart: isTargetOpen,
                token: token);
            token.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(requestedTarget))
            {
                SetStatus(
                    "Select Level открыт. Все gameplay-уровни завершены.",
                    "Continue completed: Select Level открыт; все gameplay-уровни завершены.");
                return;
            }

            var status = isTargetOpen
                ? $"Select Level открыт на target day part. Вручную выберите {FormatTarget(requestedTarget)}."
                : $"Next target пока locked: {FormatTarget(requestedTarget)}. " +
                  "Select Level открыт на последней доступной точке для проверки unlock.";
            SetStatus(status, $"Continue completed: {status}");
        }

        private async Task OpenSelectLevelAsync(
            string focusLevelAddress,
            bool openTargetDayPart,
            CancellationToken token)
        {
            RequireLevelController();
            var playerData = GameDataManager.PlayerData ??
                             throw new InvalidOperationException("PlayerData недоступен.");
            var previousCurrentLevel = playerData.CurrentLevel;

            // SelectLevelScreenController читает location index при создании.
            // Target подставляется transient, без Commit/Save, и сразу возвращается.
            SetStatus(
                $"Открывается Main Menu для {FormatTarget(focusLevelAddress)}.",
                $"Navigation: loading Main Menu for {FormatTarget(focusLevelAddress)}.");
            playerData.CurrentLevel = focusLevelAddress;
            try
            {
                await LoadSceneAndWaitAsync("Menu", token);
            }
            finally
            {
                playerData.CurrentLevel = previousCurrentLevel;
            }

            await WaitUntilAsync(
                IsMainMenuShown,
                "Main Menu не загрузился или перекрыт modal.",
                token);
            _selectionNavigationReachedMenu = true;

            SetStatus(
                $"Открывается Select Level для {FormatTarget(focusLevelAddress)}.",
                $"Navigation: opening Select Level for {FormatTarget(focusLevelAddress)}.");

            // UI route ждёт transition gate, пока HomeScreen завершает async-загрузку.
            var showScreen = UIManager.OnScreenShow ??
                             throw new InvalidOperationException(
                                 "UI navigation route недоступен.");
            showScreen(ScreenEnum.SelectLevelScreen);
            await WaitUntilAsync(
                IsSelectLevelScreenShown,
                "Select Level не открылся.",
                token);

            if (openTargetDayPart)
            {
                SetStatus(
                    $"Открывается target day part для {FormatTarget(focusLevelAddress)}.",
                    $"Navigation: opening target day part for {FormatTarget(focusLevelAddress)}.");
                await OpenTargetDayPartAsync(focusLevelAddress, token);
            }
        }

        private async Task OpenTargetDayPartAsync(
            string targetLevelAddress,
            CancellationToken token)
        {
            if (!TryResolvePart(targetLevelAddress, out var part))
            {
                throw new InvalidOperationException(
                    $"Day part для {FormatTarget(targetLevelAddress)} не найден.");
            }

            var firstLevel = part.Levels.FirstOrDefault();
            if (firstLevel == null || string.IsNullOrWhiteSpace(firstLevel.Address))
            {
                throw new InvalidOperationException(
                    $"Day part {part.DisplayName} не содержит gameplay-уровней.");
            }

            var firstLevelAddress = firstLevel.Address;
            LevelItem dayPartCard = null;
            await WaitUntilAsync(
                () =>
                {
                    dayPartCard = FindVisibleLevelItem(firstLevelAddress);
                    return dayPartCard != null &&
                           !dayPartCard.IsLocked &&
                           dayPartCard.resolvedStyle.opacity >= 0.8f;
                },
                $"Day part {part.DisplayName} ещё недоступен.",
                token);

            // Select Level становится видимым раньше завершения async Init().
            // Ждём готовую карточку и подписанный реальный обработчик клика.
            await Task.Delay(VisibleActionDelayMilliseconds, token);

            SendClick(dayPartCard);
            await WaitUntilAsync(
                () => IsTargetPartLevelsShown(part) ||
                      IsGameSceneForPart(part),
                $"Day part {part.DisplayName} не открылся.",
                token);

            if (IsTargetPartLevelsShown(part))
            {
                RecordAction(
                    $"Navigation: day part {part.DisplayName} открыт; ожидается ручной выбор уровня.");
            }
        }

        private void RequestReconcile()
        {
            _reconcilePending = true;
            if (_waitingForGameData)
                return;

            _waitingForGameData = true;
            SetStatus(
                "Waiting for Bootstrap game data and level catalog.",
                "Waiting for game data.");
            SaveState();
            Changed?.Invoke();
        }

        private static bool IsGameDataReady()
        {
            if (!Application.isPlaying ||
                !LevelCatalogService.HasCatalog ||
                LevelCatalogService.Catalog.IsEmpty ||
                GameDataManager.PlayerData == null)
            {
                return false;
            }

            var sceneName = SceneManager.GetActiveScene().name;
            return string.Equals(sceneName, "Menu", StringComparison.Ordinal) ||
                   string.Equals(sceneName, "Game", StringComparison.Ordinal);
        }

        private bool ReconcileNavigationContext()
        {
            if (!_state.IsActive)
                return true;

            if (!IsGameDataReady())
            {
                RequestReconcile();
                return false;
            }

            var orderedLevels = GetOrderedLevels();
            if (orderedLevels.Count == 0)
            {
                RequestReconcile();
                return false;
            }

            _reconcilePending = false;
            _waitingForGameData = false;

            // Завершаем только runner-owned pending Win, переживший domain reload.
            if (!string.IsNullOrWhiteSpace(_state.PendingCompletionLevelAddress))
            {
                var pendingLevel = _state.PendingCompletionLevelAddress.Trim();
                if (GetRealLevelStars(pendingLevel) == 3)
                {
                    CommitCompletedTarget(pendingLevel, _state.LastWinScore);
                    RecordAction(
                        $"Recovered completed target from real progress: {FormatTarget(pendingLevel)}.");
                    return true;
                }

                _state.PendingCompletionLevelAddress = string.Empty;
                RecordAction(
                    $"Pending completion для {FormatTarget(pendingLevel)} снят: real progress не содержит 3 stars.",
                    LogType.Warning);
            }

            // Session target не меняется от ручных menu/settings/leaderboard detours.
            var target = FindLevel(orderedLevels, _state.TargetLevelAddress);
            if (target != null &&
                (string.IsNullOrWhiteSpace(_state.LastCompletedLevelAddress) ||
                 GetRealLevelStars(_state.LastCompletedLevelAddress) == 3))
            {
                return true;
            }

            if (string.IsNullOrWhiteSpace(_state.TargetLevelAddress) &&
                !string.IsNullOrWhiteSpace(_state.LastCompletedLevelAddress))
            {
                return true;
            }

            // Stale/absent context восстанавливаем из raw сохранённого progress.
            var nextLevel = orderedLevels.FirstOrDefault(level =>
                GetRealLevelStars(level.Address) < 3);
            _state.TargetLevelAddress = nextLevel?.Address?.Trim() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(_state.TargetLevelAddress))
            {
                SetStatus(
                    "Реальный progress подтверждает завершение всех gameplay-уровней.",
                    "Navigation context восстановлен: real progress подтверждает завершение всех gameplay-уровней.");
            }
            else
            {
                SetStatus(
                    $"Navigation context восстановлен из real progress. Next: {FormatTarget(_state.TargetLevelAddress)}.",
                    $"Navigation context восстановлен из real progress. Next: {FormatTarget(_state.TargetLevelAddress)}.");
            }

            return true;
        }

        private string ResolveSelectionFocusLevel(string requestedTarget)
        {
            if (!string.IsNullOrWhiteSpace(requestedTarget) &&
                IsRealLevelOpen(requestedTarget))
            {
                return requestedTarget;
            }

            if (!string.IsNullOrWhiteSpace(_state.LastCompletedLevelAddress))
                return _state.LastCompletedLevelAddress;

            var currentLevel = GameDataManager.PlayerData?.CurrentLevel;
            if (!string.IsNullOrWhiteSpace(currentLevel))
                return currentLevel.Trim();

            return GetOrderedLevels().FirstOrDefault()?.Address?.Trim() ?? string.Empty;
        }

        private bool IsTargetGameplayActive()
        {
            return _state.IsActive &&
                   !string.IsNullOrWhiteSpace(_state.TargetLevelAddress) &&
                   string.Equals(
                       SceneManager.GetActiveScene().name,
                       "Game",
                       StringComparison.Ordinal) &&
                   GetGameState() == GameState.PLAYING &&
                   !HasBlockingModal() &&
                   string.Equals(
                       GameDataManager.PlayerData?.CurrentLevel?.Trim(),
                       _state.TargetLevelAddress.Trim(),
                       StringComparison.OrdinalIgnoreCase);
        }

        private bool IsTargetIntroActive()
        {
            return _state.IsActive &&
                   !string.IsNullOrWhiteSpace(_state.TargetLevelAddress) &&
                   string.Equals(
                       SceneManager.GetActiveScene().name,
                       "Game",
                       StringComparison.Ordinal) &&
                   GetGameState() == GameState.INTRO &&
                   string.Equals(
                       GameDataManager.PlayerData?.CurrentLevel?.Trim(),
                       _state.TargetLevelAddress.Trim(),
                       StringComparison.OrdinalIgnoreCase);
        }

        private void CommitCompletedTarget(string completedLevelAddress, int finalScore)
        {
            _state.PendingCompletionLevelAddress = string.Empty;
            _state.LastCompletedLevelAddress = completedLevelAddress;
            _state.LastWinScore = finalScore;
            _state.TargetLevelAddress = LevelManager.TryGetNextLevelKey(
                completedLevelAddress,
                out var nextLevelAddress)
                ? nextLevelAddress.Trim()
                : string.Empty;

            var nextPoint = string.IsNullOrWhiteSpace(_state.TargetLevelAddress)
                ? "all levels completed"
                : FormatTarget(_state.TargetLevelAddress);
            var status =
                $"Completed: {FormatTarget(completedLevelAddress)}, stars=3, score={finalScore}. " +
                $"Next: {nextPoint}.";
            SetStatus(status, status);
        }

        private static IReadOnlyList<LevelProgress> GetOrderedLevels()
        {
            return LevelSelectionModel.Create().FlattenedLevels;
        }

        private static int GetRealLevelStars(string levelAddress)
        {
            return TryResolveProgressKey(levelAddress, out var progressKey)
                ? GameDataManager.PlayerData?.Progress?.GetStars(progressKey) ?? 0
                : 0;
        }

        private static bool IsRealLevelOpen(string levelAddress)
        {
            return TryResolveProgressKey(levelAddress, out var progressKey) &&
                   (GameDataManager.PlayerData?.Progress?.IsLevelUnlocked(progressKey) ?? false);
        }

        private static bool TryResolveProgressKey(
            string levelAddress,
            out LevelProgressKey progressKey)
        {
            var level = FindLevel(GetOrderedLevels(), levelAddress);
            if (level == null)
            {
                progressKey = default;
                return false;
            }

            progressKey = new LevelProgressKey(
                level.LocationId,
                level.PartOfDayId,
                level.LevelIndex);
            return true;
        }

        private static string FormatTarget(string levelAddress)
        {
            var model = LevelSelectionModel.Create();
            var level = FindLevel(model.FlattenedLevels, levelAddress);

            if (level == null)
                return "unknown location / unknown part of day / level";

            var location = model.Locations.FirstOrDefault(candidate =>
                candidate.Index == level.LocationIndex);
            var part = location?.Parts.FirstOrDefault(candidate =>
                candidate.Index == level.PartIndex);
            return $"{location?.DisplayName ?? "unknown location"} / " +
                   $"{part?.DisplayName ?? "unknown part of day"} / " +
                   $"level {level.LevelIndex + 1}";
        }

        private static string BuildOpeningActionTitle(string levelAddress)
        {
            return string.IsNullOrWhiteSpace(levelAddress)
                ? "Opening Select Level"
                : $"Opening {FormatLocationAndPart(levelAddress)}";
        }

        private static string FormatShortTarget(string levelAddress)
        {
            var model = LevelSelectionModel.Create();
            var level = FindLevel(model.FlattenedLevels, levelAddress);
            if (level == null)
                return "unknown location / unknown part of day / level";

            return $"{FormatLocationAndPart(levelAddress)} / level {level.LevelIndex + 1}";
        }

        private static string FormatLocationAndPart(string levelAddress)
        {
            var model = LevelSelectionModel.Create();
            var level = FindLevel(model.FlattenedLevels, levelAddress);
            if (level == null)
                return "Select Level";

            var location = model.Locations.FirstOrDefault(candidate =>
                candidate.Index == level.LocationIndex);
            var part = location?.Parts.FirstOrDefault(candidate =>
                candidate.Index == level.PartIndex);
            return $"{location?.DisplayName ?? "unknown location"} / " +
                   $"{part?.DisplayName ?? "unknown part of day"}";
        }

        private static bool TryResolvePart(
            string levelAddress,
            out PartView part)
        {
            var model = LevelSelectionModel.Create();
            var level = FindLevel(model.FlattenedLevels, levelAddress);
            if (level == null)
            {
                part = null;
                return false;
            }

            var location = model.Locations.FirstOrDefault(candidate =>
                candidate.Index == level.LocationIndex);
            part = location?.Parts.FirstOrDefault(candidate =>
                candidate.Index == level.PartIndex);
            return part != null;
        }

        /// <summary>
        /// Finds a catalog level by address or returns <see langword="null"/> when the address is unavailable.
        /// </summary>
        private static LevelProgress FindLevel(
            IReadOnlyList<LevelProgress> levels,
            string levelAddress)
        {
            if (levels == null || string.IsNullOrWhiteSpace(levelAddress))
                return null;

            var normalizedAddress = levelAddress.Trim();
            return levels.FirstOrDefault(candidate =>
                !string.IsNullOrWhiteSpace(candidate?.Address) &&
                string.Equals(
                    candidate.Address.Trim(),
                    normalizedAddress,
                    StringComparison.OrdinalIgnoreCase));
        }

        private void ClearNavigationContext(
            string status,
            string action,
            LogType logType = LogType.Log)
        {
            _state.IsActive = false;
            _state.IsBusy = false;
            _state.TargetLevelAddress = string.Empty;
            _state.LastCompletedLevelAddress = string.Empty;
            _state.PendingCompletionLevelAddress = string.Empty;
            _state.LastWinScore = 0;
            SetStatus(status, action, logType);
        }

        private void CancelTransientOperation()
        {
            _operationVersion++;
            _operationCancellation?.Cancel();
            _state.IsBusy = false;
            _activeOperationKind = ActiveOperationKind.None;
            _busyActionTitle = string.Empty;
            _selectionNavigationReachedMenu = false;
        }

        private void ObserveRuntimeState()
        {
            if (!Application.isPlaying ||
                Time.realtimeSinceStartupAsDouble < _nextRuntimeObservationAt)
            {
                return;
            }

            _nextRuntimeObservationAt =
                Time.realtimeSinceStartupAsDouble + RuntimeObservationIntervalSeconds;

            if (!IsGameDataReady())
            {
                RequestReconcile();
                return;
            }

            if (_reconcilePending || _waitingForGameData)
            {
                _reconcilePending = false;
                _waitingForGameData = false;
                if (_state.IsActive)
                {
                    ReconcileNavigationContext();
                }
                else
                {
                    SetStatus(
                        "Play Mode готов. Команды доступны.",
                        "Game data ready: команды доступны.");
                }

                SaveState();
                Changed?.Invoke();
            }

            if (!_state.IsActive ||
                !TryGetActiveRuntimeLevel(out var currentLevel, out var gameState))
            {
                return;
            }

            if (_state.IsBusy)
            {
                if (_activeOperationKind == ActiveOperationKind.LevelCompletion)
                    return;

                if (_activeOperationKind == ActiveOperationKind.SelectionNavigation)
                {
                    if (!_selectionNavigationReachedMenu)
                        return;

                    CancelSupersededSelectionNavigation(currentLevel, gameState);
                }
            }

            var isTarget = string.Equals(
                currentLevel,
                _state.TargetLevelAddress?.Trim(),
                StringComparison.OrdinalIgnoreCase);
            var status = gameState switch
            {
                GameState.INTRO when isTarget =>
                    $"Intro: {FormatTarget(currentLevel)}. Skip intro manually.",
                GameState.PLAYING when isTarget && HasBlockingModal() =>
                    $"Gameplay: {FormatTarget(currentLevel)} перекрыт modal. Закройте его вручную.",
                GameState.PLAYING when isTarget && IsLevelUpPrepared() =>
                    $"Level Up prepared: {PreparedExperiencePoints} / " +
                    PlayerExperienceService.PlayerLevelThreshold,
                GameState.PLAYING when isTarget =>
                    $"Gameplay ready: {FormatTarget(currentLevel)}. Можно выполнить Win with random score.",
                GameState.PAUSED when isTarget =>
                    $"Gameplay paused: {FormatTarget(currentLevel)}. " +
                    "Continue вернёт к target selection.",
                _ =>
                    $"Вне target теста: открыт {FormatTarget(currentLevel)}. " +
                    $"Continue вернёт к {FormatTarget(_state.TargetLevelAddress)}."
            };

            if (string.Equals(_state.Status, status, StringComparison.Ordinal))
                return;

            SetStatus(status, $"Runtime state detected: {status}");
            SaveState();
            Changed?.Invoke();
        }

        private static bool IsLevelUpPrepared()
        {
            return GameDataManager.PlayerData?.ExperiencePoints ==
                   PreparedExperiencePoints;
        }

        private void CancelSupersededSelectionNavigation(
            string currentLevel,
            GameState gameState)
        {
            _operationVersion++;
            _operationCancellation?.Cancel();
            _state.IsBusy = false;
            _activeOperationKind = ActiveOperationKind.None;
            _busyActionTitle = string.Empty;
            _selectionNavigationReachedMenu = false;
            RecordAction(
                $"Stale selection navigation stopped: runtime entered {gameState} at " +
                $"{FormatTarget(currentLevel)}.");
        }

        private static async Task LoadSceneAndWaitAsync(
            string sceneName,
            CancellationToken token)
        {
            var sceneLoaded = false;

            void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
            {
                if (string.Equals(scene.name, sceneName, StringComparison.Ordinal))
                    sceneLoaded = true;
            }

            SceneManager.sceneLoaded += HandleSceneLoaded;
            try
            {
                SceneManager.LoadScene(sceneName);
                await WaitUntilAsync(
                    () => sceneLoaded,
                    $"Сцена {sceneName} не завершила загрузку.",
                    token);
            }
            finally
            {
                SceneManager.sceneLoaded -= HandleSceneLoaded;
            }
        }

        private static async Task WaitUntilAsync(
            Func<bool> predicate,
            string timeoutMessage,
            CancellationToken token,
            double timeoutSeconds = DefaultTimeoutSeconds)
        {
            var deadline = Time.realtimeSinceStartupAsDouble + timeoutSeconds;
            while (!predicate())
            {
                token.ThrowIfCancellationRequested();
                if (Time.realtimeSinceStartupAsDouble >= deadline)
                    throw new TimeoutException(timeoutMessage);

                await Task.Delay(PollDelayMilliseconds, token);
            }
        }

        private static bool IsMainMenuShown()
        {
            return string.Equals(
                       SceneManager.GetActiveScene().name,
                       "Menu",
                       StringComparison.Ordinal) &&
                   FindVisibleElement<VisualElement>("homescreen") != null &&
                   !HasBlockingModal();
        }

        private static bool IsSelectLevelScreenShown()
        {
            return string.Equals(
                       SceneManager.GetActiveScene().name,
                       "Menu",
                       StringComparison.Ordinal) &&
                   FindVisibleElement<VisualElement>("selectlevelscreen") != null &&
                   !HasBlockingModal();
        }

        private static bool IsTargetPartLevelsShown(
            PartView part)
        {
            if (!IsSelectLevelScreenShown() ||
                FindVisibleElement<Button>("btn_back") == null)
            {
                return false;
            }

            return FindVisibleElements<LevelItem>().Any(item =>
                PartContainsLevel(part, item.LevelName));
        }

        private static bool IsGameSceneForPart(
            PartView part)
        {
            return string.Equals(
                       SceneManager.GetActiveScene().name,
                       "Game",
                       StringComparison.Ordinal) &&
                   PartContainsLevel(
                       part,
                       GameDataManager.PlayerData?.CurrentLevel);
        }

        private static bool PartContainsLevel(
            PartView part,
            string levelAddress)
        {
            return part?.Levels != null &&
                   part.Levels.Any(level =>
                       string.Equals(
                           level.Address?.Trim(),
                           levelAddress?.Trim(),
                           StringComparison.OrdinalIgnoreCase));
        }

        private static bool IsRealWinShown()
        {
            return GetGameState() == GameState.FINISHED &&
                   FindVisibleElement<VisualElement>("win_result") != null;
        }

        private static bool IsJourneyCompleteShown()
        {
            return GetGameState() == GameState.FINISHED &&
                   FindVisibleElement<VisualElement>(
                       "journey-complete-modal") != null;
        }

        private static bool HasBlockingModal()
        {
            return FindVisibleElement<VisualElement>("modal__container") != null;
        }

        private static GameState GetGameState()
        {
            return LevelController.Instance?.LevelData?.GameManager?.State ??
                   GameState.OFF;
        }

        private static bool TryGetActiveRuntimeLevel(
            out string currentLevel,
            out GameState gameState)
        {
            currentLevel = GameDataManager.PlayerData?.CurrentLevel?.Trim();
            gameState = GetGameState();
            return string.Equals(
                       SceneManager.GetActiveScene().name,
                       "Game",
                       StringComparison.Ordinal) &&
                   !string.IsNullOrWhiteSpace(currentLevel) &&
                   gameState is GameState.INTRO or
                       GameState.PLAYING or
                       GameState.PAUSED;
        }

        private static bool IsCurrentLevelInState(
            string levelAddress,
            params GameState[] expectedStates)
        {
            return IsGameSceneForLevel(levelAddress) &&
                   expectedStates.Contains(GetGameState());
        }

        private static bool IsGameSceneForLevel(string levelAddress)
        {
            return string.Equals(
                       SceneManager.GetActiveScene().name,
                       "Game",
                       StringComparison.Ordinal) &&
                   string.Equals(
                       GameDataManager.PlayerData?.CurrentLevel?.Trim(),
                       levelAddress,
                       StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsGameplayReady(string levelAddress)
        {
            return IsCurrentLevelInState(levelAddress, GameState.PLAYING) &&
                   FindVisibleElement<VisualElement>("gamescreen") != null &&
                   UnityEngine.Object.FindAnyObjectByType<Hamster>(
                       FindObjectsInactive.Include) != null;
        }

        private static LevelController RequireLevelController()
        {
            var controller = LevelController.Instance;
            if (controller == null)
                throw new InvalidOperationException("LevelController недоступен.");

            return controller;
        }

        private static Hamster RequireHamster()
        {
            var hamster = UnityEngine.Object.FindAnyObjectByType<Hamster>(
                FindObjectsInactive.Include);
            if (hamster == null)
                throw new InvalidOperationException("Хомяк не найден на игровой сцене.");

            return hamster;
        }

        private static T FindVisibleElement<T>(string name)
            where T : VisualElement
        {
            return FindVisibleElements<T>(name).FirstOrDefault();
        }

        private static LevelItem FindVisibleLevelItem(string levelAddress)
        {
            return FindVisibleElements<LevelItem>()
                .FirstOrDefault(item =>
                    string.Equals(
                        item.LevelName?.Trim(),
                        levelAddress?.Trim(),
                        StringComparison.OrdinalIgnoreCase));
        }

        private static IEnumerable<T> FindVisibleElements<T>(string name = null)
            where T : VisualElement
        {
            var documents = UnityEngine.Object.FindObjectsByType<UIDocument>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            foreach (var document in documents)
            {
                var root = document?.rootVisualElement;
                if (root == null)
                    continue;

                foreach (var element in root.Query<T>(name).ToList())
                {
                    if (IsElementShown(element))
                        yield return element;
                }
            }
        }

        private static bool IsElementShown(VisualElement element)
        {
            if (element?.panel == null)
                return false;

            for (var current = element; current != null; current = current.parent)
            {
                if (current.resolvedStyle.display == DisplayStyle.None)
                    return false;
            }

            return true;
        }

        private static void SendClick(VisualElement element)
        {
            using var clickEvent = ClickEvent.GetPooled();
            clickEvent.target = element;
            element.SendEvent(clickEvent);
        }

        private void SetStatus(
            string status,
            string action,
            LogType logType = LogType.Log,
            string consoleDetails = null)
        {
            _state.Status = status;
            RecordAction(action, logType, consoleDetails);
            Changed?.Invoke();
        }

        private void RecordAction(
            string message,
            LogType logType = LogType.Log,
            string consoleDetails = null)
        {
            var line = $"[{DateTime.Now:HH:mm:ss}] {message}";
            _state.CurrentAction = line;
            var consoleLine = string.IsNullOrWhiteSpace(consoleDetails)
                ? line
                : $"{line}\n{consoleDetails}";

            switch (logType)
            {
                case LogType.Error:
                case LogType.Exception:
                case LogType.Assert:
                    Debug.LogError($"{ConsoleLogTag} {consoleLine}");
                    break;
                case LogType.Warning:
                    Debug.LogWarning($"{ConsoleLogTag} {consoleLine}");
                    break;
                default:
                    Debug.Log($"{ConsoleLogTag} {consoleLine}");
                    break;
            }
        }

        private static GameProgressTestRunState LoadState()
        {
            var json = PlayerPrefs.GetString(PlayerPrefsStateKey, string.Empty);
            if (string.IsNullOrWhiteSpace(json))
                return new GameProgressTestRunState();

            try
            {
                var state = JsonUtility.FromJson<GameProgressTestRunState>(json);
                if (state == null ||
                    state.Version != GameProgressTestRunState.CurrentVersion)
                {
                    Debug.LogWarning(
                        $"{ConsoleLogTag} Navigation context отсутствует либо имеет неподдерживаемую версию.");
                    return new GameProgressTestRunState();
                }

                state.Status ??= "Запустите игру через Bootstrap.";
                state.CurrentAction = string.IsNullOrWhiteSpace(state.CurrentAction)
                    ? "Контекст теста восстановлен. Следующее действие ещё не выполнялось."
                    : state.CurrentAction;
                return state;
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    $"{ConsoleLogTag} Не удалось прочитать navigation context: {exception.Message}");
                return new GameProgressTestRunState();
            }
        }

        private void SaveState()
        {
            PlayerPrefs.SetString(PlayerPrefsStateKey, JsonUtility.ToJson(_state));
            PlayerPrefs.Save();
        }
    }
}
#endif
