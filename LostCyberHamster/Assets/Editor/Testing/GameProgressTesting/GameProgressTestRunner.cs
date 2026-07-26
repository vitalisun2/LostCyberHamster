using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Assets.Scripts.Common.Models;
using Assets.Scripts.GameManagerLogic;
using Assets.Scripts.Gameplay;
using Assets.Scripts.System;
using GameManagement;
using LostCyberHamster.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace LostCyberHamster.Editor.Testing.GameProgress
{
    /// <summary>Выполняет по одному наблюдаемому шагу ручного теста прогресса.</summary>
    internal sealed class GameProgressTestRunner : IDisposable
    {
        private const string SessionStateKey = "GameProgressTesting.RunState";
        private const int LevelsPerPage = 4;
        private const int MaxRandomScore = 50;
        private const int PollDelayMilliseconds = 100;
        private const int VisibleActionDelayMilliseconds = 250;
        private const double DefaultTimeoutSeconds = 20d;

        private readonly GameProgressTestRunState _state;
        private CancellationTokenSource _operationCancellation;
        private int _operationVersion;
        private bool _disposed;
        private bool _sessionCleared;

        public GameProgressTestRunner()
        {
            _state = LoadState();
            if (_state.IsBusy)
            {
                _state.IsBusy = false;
                _state.Status = "Восстановлена последняя подтверждённая точка.";
                AddLog("Domain reload: восстановлена последняя подтверждённая точка.");
                SaveState();
            }
        }

        public event Action Changed;

        public IReadOnlyList<string> Log => _state.Log;

        public string Status => _state.Status;

        public string CurrentPoint =>
            $"{_state.Stage}: {ResolveCurrentLevelForDisplay()}";

        public bool CanUsePrimaryAction =>
            EditorApplication.isPlaying &&
            !_state.IsBusy &&
            (_state.Stage != GameProgressTestRunState.FlowStage.Completed ||
             !IsAtExpectedStage());

        public bool CanCancel =>
            !_state.IsBusy &&
            (_state.IsActive ||
             _state.Stage == GameProgressTestRunState.FlowStage.Completed);

        public string PrimaryActionTitle
        {
            get
            {
                if (_state.IsBusy)
                    return "Выполняется...";

                return IsAtExpectedStage()
                    ? "Continue"
                    : "Back to Progress Test";
            }
        }

        /// <summary>Запускает Continue либо возвращает к сохранённому checkpoint.</summary>
        public void RunPrimaryAction()
        {
            if (!CanUsePrimaryAction)
                return;

            if (!IsAtExpectedStage())
            {
                RunOperationAsync(RestoreCheckpointAsync);
                return;
            }

            RunOperationAsync(ContinueAsync);
        }

        /// <summary>Явно отменяет сессию и очищает сохранённый checkpoint.</summary>
        public void Cancel()
        {
            if (!CanCancel)
                return;

            _operationVersion++;
            _operationCancellation?.Cancel();
            _state.IsActive = false;
            _state.IsBusy = false;
            _state.Stage = GameProgressTestRunState.FlowStage.Cancelled;
            _state.Checkpoint = GameProgressTestRunState.CheckpointKind.None;
            _state.CheckpointLevelAddress = string.Empty;
            _state.Status = "Тест отменён.";
            AddLog("Тест отменён пользователем.");
            _sessionCleared = true;
            SessionState.EraseString(SessionStateKey);
            Changed?.Invoke();
        }

        /// <summary>Обновляет состояние окна при смене Play Mode.</summary>
        public void HandlePlayModeStateChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.ExitingPlayMode)
                return;

            _operationVersion++;
            _operationCancellation?.Cancel();
            _state.IsBusy = false;
            _state.Status = "Play Mode остановлен. Checkpoint сохранён для текущей Editor-сессии.";
            SaveState();
            Changed?.Invoke();
        }

        /// <summary>Отменяет только transient-ожидание, сохраняя семантическую сессию.</summary>
        public void Dispose()
        {
            if (_disposed)
                return;

            _operationVersion++;
            _operationCancellation?.Cancel();
            _operationCancellation?.Dispose();
            _operationCancellation = null;
            _state.IsBusy = false;
            SaveState();
            _disposed = true;
        }

        private async void RunOperationAsync(Func<CancellationToken, Task> operation)
        {
            var operationVersion = ++_operationVersion;
            _operationCancellation?.Dispose();
            _operationCancellation = new CancellationTokenSource();
            var token = _operationCancellation.Token;

            _state.IsBusy = true;
            SaveState();
            Changed?.Invoke();

            try
            {
                await operation(token);
            }
            catch (OperationCanceledException)
            {
                if (operationVersion == _operationVersion)
                    _state.Status = "Действие отменено.";
            }
            catch (Exception exception)
            {
                if (operationVersion == _operationVersion)
                {
                    _state.Status = $"Ошибка: {exception.Message}";
                    AddLog(_state.Status);
                }
            }
            finally
            {
                if (operationVersion == _operationVersion)
                {
                    _state.IsBusy = false;
                    SaveState();
                    Changed?.Invoke();
                }
            }
        }

        private async Task ContinueAsync(CancellationToken token)
        {
            if (!_state.IsActive)
                await StartNewRunAsync(token);

            switch (_state.Stage)
            {
                case GameProgressTestRunState.FlowStage.MainMenu:
                    await OpenSelectLevelAsync(token);
                    break;
                case GameProgressTestRunState.FlowStage.SelectDayPart:
                    await OpenTargetDayPartAsync(token);
                    break;
                case GameProgressTestRunState.FlowStage.SelectLevel:
                    await SelectTargetLevelAsync(token);
                    break;
                case GameProgressTestRunState.FlowStage.Intro:
                    await SkipIntroAsync(token);
                    break;
                case GameProgressTestRunState.FlowStage.Gameplay:
                    await CompleteGameplayAsync(token);
                    break;
                case GameProgressTestRunState.FlowStage.Win:
                    await ReturnFromWinToSelectLevelAsync(token);
                    break;
                case GameProgressTestRunState.FlowStage.Cancelled:
                    _state.Stage = GameProgressTestRunState.FlowStage.MainMenu;
                    await OpenSelectLevelAsync(token);
                    break;
                case GameProgressTestRunState.FlowStage.Completed:
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private async Task StartNewRunAsync(CancellationToken token)
        {
            // Сбрасываем сохраняемый прогресс и строим цель из реального runtime catalog.
            GameDataManager.ResetPlayerProgress();
            GameDataManager.PlayerData.IsTutorialCompleted = true;
            GameDataManager.SaveData();
            var selection = LevelSelectionModel.Create();
            var firstLevel = selection.FlattenedLevels.FirstOrDefault();
            if (string.IsNullOrWhiteSpace(firstLevel.Address))
                throw new InvalidOperationException("Каталог gameplay-уровней пуст.");

            // Создаём новую сессию с чистым журналом.
            _state.Log.Clear();
            _state.IsActive = true;
            _state.Stage = GameProgressTestRunState.FlowStage.MainMenu;
            _state.TargetLevelAddress = firstLevel.Address.Trim();
            _state.Checkpoint = GameProgressTestRunState.CheckpointKind.None;
            _state.CheckpointLevelAddress = string.Empty;
            _state.CheckpointScore = 0;
            _state.Status = "Fresh progress создан без tutorial redirect. Загружается Main Menu.";
            AddLog($"Fresh progress: tutorial отмечен завершённым, цель {_state.TargetLevelAddress}.");
            _sessionCleared = false;
            SaveState();

            // Пересоздаём Menu controllers после reset, чтобы их cached location совпала с fresh save.
            await LoadSceneAndWaitAsync("Menu", token);
            await WaitUntilAsync(
                IsMainMenuShown,
                "Main Menu не загрузился после сброса прогресса.",
                token);
        }

        private async Task OpenSelectLevelAsync(CancellationToken token)
        {
            var button = FindVisibleElement<Button>("btn_select-level");
            if (button == null)
                throw new InvalidOperationException("Кнопка Select Level не найдена на Main Menu.");

            SendClick(button);
            await WaitUntilAsync(
                IsSelectLevelScreenShown,
                "Select Level не открылся.",
                token);

            _state.Stage = GameProgressTestRunState.FlowStage.SelectDayPart;
            _state.Status = $"Select Level открыт. Следующая цель: {_state.TargetLevelAddress}.";
            AddLog($"Main Menu: открыт Select Level. Точка {_state.TargetLevelAddress}.");
        }

        private async Task OpenTargetDayPartAsync(CancellationToken token)
        {
            var route = ResolveTargetRoute();

            // Показываем нужную открытую локацию реальными стрелками экрана.
            await ShowTargetLocationAsync(route.Location, token);

            // Нажимаем карточку нужной части дня.
            var partCardAddress = route.Part.Levels[0].Address.Trim();
            var partCard = FindVisibleLevelItem(partCardAddress);
            if (partCard == null || partCard.IsLocked)
                throw new InvalidOperationException(
                    $"Часть дня {route.Part.DisplayName} недоступна.");

            SendClick(partCard);
            await WaitUntilAsync(
                () => IsTargetPartShown(route.Part),
                $"Часть дня {route.Part.DisplayName} не открылась.",
                token);

            _state.Stage = GameProgressTestRunState.FlowStage.SelectLevel;
            _state.Status = $"{route.Location.DisplayName}, {route.Part.DisplayName}: выберите уровень.";
            AddLog(
                $"Select Level: открыты {route.Location.DisplayName} / {route.Part.DisplayName}.");
        }

        private async Task ShowTargetLocationAsync(
            LevelSelectionModel.LocationView targetLocation,
            CancellationToken token)
        {
            for (var attempt = 0; attempt < LevelSelectionModel.Create().Locations.Count; attempt++)
            {
                var currentTitle = GetVisibleLabelText("location__title");
                if (string.Equals(
                        currentTitle,
                        targetLocation.DisplayName,
                        StringComparison.Ordinal))
                {
                    return;
                }

                var nextButton = FindVisibleElement<Button>("btn__location-next");
                if (nextButton == null || !nextButton.enabledSelf)
                    throw new InvalidOperationException(
                        $"Локация {targetLocation.DisplayName} ещё не открыта.");

                SendClick(nextButton);
                await WaitUntilAsync(
                    () => !string.Equals(
                        GetVisibleLabelText("location__title"),
                        currentTitle,
                        StringComparison.Ordinal),
                    "Переключение локации не завершилось.",
                    token);
                await Task.Delay(VisibleActionDelayMilliseconds, token);
            }

            throw new InvalidOperationException(
                $"Локация {targetLocation.DisplayName} не найдена на экране.");
        }

        private async Task SelectTargetLevelAsync(CancellationToken token)
        {
            var route = ResolveTargetRoute();
            if (!IsTargetPartShown(route.Part))
                throw new InvalidOperationException(
                    $"Открыта не целевая часть дня {route.Part.DisplayName}.");

            // Открываем страницу, содержащую целевой уровень.
            var targetPage = route.Level.LevelIndex / LevelsPerPage;
            for (var page = 0; page < targetPage; page++)
            {
                var nextButton = FindVisibleElement<Button>("btn__location-next");
                if (nextButton == null || !nextButton.enabledSelf)
                    throw new InvalidOperationException("Следующая страница уровней недоступна.");

                SendClick(nextButton);
                await WaitUntilAsync(
                    () => FindVisibleLevelItem(route.Level.Address) != null,
                    "Страница целевого уровня не открылась.",
                    token);
                await Task.Delay(VisibleActionDelayMilliseconds, token);
            }

            // Нажимаем реальную карточку и ждём загрузку Intro либо Gameplay.
            var levelCard = FindVisibleLevelItem(route.Level.Address);
            if (levelCard == null || levelCard.IsLocked)
                throw new InvalidOperationException(
                    $"Уровень {route.Level.Address} недоступен.");

            SendClick(levelCard);
            await WaitForGameReadyAsync(route.Level.Address, token);
            CommitLoadedGameCheckpoint(route.Level.Address);
        }

        private async Task SkipIntroAsync(CancellationToken token)
        {
            var levelController = RequireLevelController();
            levelController.SkipIntro();
            await WaitUntilAsync(
                () => GetGameState() == GameState.PLAYING,
                "Skip Intro не перевёл игру в Gameplay.",
                token);

            _state.Stage = GameProgressTestRunState.FlowStage.Gameplay;
            SetCheckpoint(
                GameProgressTestRunState.CheckpointKind.Gameplay,
                _state.TargetLevelAddress,
                0);
            _state.Status = $"Gameplay: {_state.TargetLevelAddress}.";
            AddLog($"Intro: нажат Skip. Gameplay {_state.TargetLevelAddress}.");
        }

        private async Task CompleteGameplayAsync(CancellationToken token)
        {
            var hamster = RequireHamster();
            if (hamster.RunScore > MaxRandomScore)
            {
                AddLog(
                    $"Gameplay score {hamster.RunScore} выше {MaxRandomScore}; " +
                    "checkpoint перезагружается без завершения уровня.");
                await RestoreCheckpointAsync(token);
                _state.Status =
                    $"Gameplay восстановлен: score снова в диапазоне 0–{MaxRandomScore}. Нажмите Continue.";
                return;
            }

            var score = UnityEngine.Random.Range(hamster.RunScore, MaxRandomScore + 1);
            var levelController = PrepareScore(score);
            await Task.Delay(VisibleActionDelayMilliseconds, token);
            levelController.Finish();
            await WaitUntilAsync(
                IsRealWinShown,
                "Реальный Win не появился.",
                token);

            SetCheckpoint(
                GameProgressTestRunState.CheckpointKind.Win,
                _state.TargetLevelAddress,
                score);
            AddLog(
                $"Gameplay: победа {_state.TargetLevelAddress}, stars=3, score={score}.");

            if (LevelManager.TryGetNextLevelKey(
                    _state.TargetLevelAddress,
                    out _))
            {
                _state.Stage = GameProgressTestRunState.FlowStage.Win;
                _state.Status = $"Win: {_state.TargetLevelAddress}, 3 stars, score {score}.";
                return;
            }

            _state.Stage = GameProgressTestRunState.FlowStage.Completed;
            _state.Status = $"Финальный Win показан. Все уровни завершены.";
            AddLog("Game Progress Testing завершён на последнем реальном Win.");
        }

        private LevelController PrepareScore(int score)
        {
            var levelController = RequireLevelController();
            if (GetGameState() != GameState.PLAYING)
                throw new InvalidOperationException("Победа доступна только в Gameplay.");

            var hamster = RequireHamster();
            if (hamster.RunScore > score)
            {
                throw new InvalidOperationException(
                    $"Текущий score {hamster.RunScore} выше целевого {score}.");
            }

            // Генерируем реальный run score через существующее игровое событие.
            hamster.Lives.Value = 3;
            for (var index = hamster.RunScore; index < score; index++)
            {
                hamster.CollectableCollectedEvent.Invoke(
                    ObstacleTypeEnum.collectableCoin);
            }

            return levelController;
        }

        private async Task ReturnFromWinToSelectLevelAsync(CancellationToken token)
        {
            var hasNextLevel = LevelManager.TryGetNextLevelKey(
                _state.TargetLevelAddress,
                out var nextLevelAddress);

            // Не используем Home, Retry или Next из Win: грузим Menu напрямую.
            if (hasNextLevel)
                _state.TargetLevelAddress = nextLevelAddress.Trim();

            await LoadSceneAndWaitAsync("Menu", token);
            await WaitUntilAsync(
                IsMainMenuShown,
                "Main Menu не загрузился после Win.",
                token);

            // Открываем Select Level реальной кнопкой главного меню.
            var selectLevelButton = FindVisibleElement<Button>("btn_select-level");
            if (selectLevelButton == null)
                throw new InvalidOperationException("Кнопка Select Level не найдена.");

            SendClick(selectLevelButton);
            await WaitUntilAsync(
                IsSelectLevelScreenShown,
                "Select Level не открылся после Win.",
                token);

            SetCheckpoint(
                GameProgressTestRunState.CheckpointKind.None,
                string.Empty,
                0);

            if (hasNextLevel)
            {
                _state.Stage = GameProgressTestRunState.FlowStage.SelectDayPart;
                _state.Status = $"Новые stars/unlocks видимы. Следующая цель: {_state.TargetLevelAddress}.";
                AddLog($"Win: переход в Select Level. Следующая точка {_state.TargetLevelAddress}.");
                return;
            }

            _state.Stage = GameProgressTestRunState.FlowStage.Completed;
            _state.Status = "Все gameplay-уровни завершены. Итоговый прогресс открыт в Select Level.";
            AddLog("Final Win: открыт Select Level с завершённым прогрессом.");
        }

        private async Task RestoreCheckpointAsync(CancellationToken token)
        {
            if (_state.Checkpoint == GameProgressTestRunState.CheckpointKind.None)
            {
                await RestoreMainMenuAsync(token);
                return;
            }

            if (_state.Checkpoint == GameProgressTestRunState.CheckpointKind.Win)
            {
                AddLog(
                    "Back to Progress Test: Win не переигрывается; " +
                    "открывается следующий шаг Select Level.");
                await ReturnFromWinToSelectLevelAsync(token);
                return;
            }

            var checkpointLevel = _state.CheckpointLevelAddress;
            var levelController = RequireLevelController();
            levelController.SetCurrentLevel(checkpointLevel);
            await LoadSceneAndWaitAsync("Game", token);
            await WaitForGameReadyAsync(checkpointLevel, token);

            switch (_state.Checkpoint)
            {
                case GameProgressTestRunState.CheckpointKind.Intro:
                    RestoreIntroCheckpoint(checkpointLevel);
                    break;
                case GameProgressTestRunState.CheckpointKind.Gameplay:
                    await RestoreGameplayCheckpointAsync(checkpointLevel, token);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            AddLog($"Back to Progress Test: восстановлен {_state.Checkpoint} {checkpointLevel}.");
        }

        private async Task RestoreMainMenuAsync(CancellationToken token)
        {
            if (!string.Equals(
                    SceneManager.GetActiveScene().name,
                    "Menu",
                    StringComparison.Ordinal))
            {
                await LoadSceneAndWaitAsync("Menu", token);
                await WaitUntilAsync(
                    IsMainMenuShown,
                    "Main Menu не загрузился.",
                    token);
            }
            else if (!IsMainMenuShown())
            {
                await WaitUntilAsync(
                    () => UIManager.OnScreenShow != null,
                    "UIManager меню не готов.",
                    token);
                UIManager.OnScreenShow?.Invoke(ScreenEnum.HomeScreen);
                await WaitUntilAsync(
                    IsMainMenuShown,
                    "HomeScreen не восстановлен.",
                    token);
            }

            _state.Stage = GameProgressTestRunState.FlowStage.MainMenu;
            _state.Status = "Main Menu восстановлен.";
            AddLog("Back to Progress Test: восстановлен Main Menu.");
        }

        private void RestoreIntroCheckpoint(string checkpointLevel)
        {
            if (GetGameState() != GameState.INTRO)
            {
                throw new InvalidOperationException(
                    "Сохранённый Intro уже завершился и не может быть восстановлен текущим runtime.");
            }

            _state.Stage = GameProgressTestRunState.FlowStage.Intro;
            _state.TargetLevelAddress = checkpointLevel;
            _state.Status = $"Intro восстановлен: {checkpointLevel}.";
        }

        private async Task RestoreGameplayCheckpointAsync(
            string checkpointLevel,
            CancellationToken token)
        {
            if (GetGameState() == GameState.INTRO)
                RequireLevelController().SkipIntro();

            await WaitUntilAsync(
                () => GetGameState() == GameState.PLAYING,
                "Gameplay checkpoint не восстановлен.",
                token);

            _state.Stage = GameProgressTestRunState.FlowStage.Gameplay;
            _state.TargetLevelAddress = checkpointLevel;
            _state.Status = $"Gameplay восстановлен: {checkpointLevel}.";
        }

        private void CommitLoadedGameCheckpoint(string levelAddress)
        {
            var state = GetGameState();
            switch (state)
            {
                case GameState.INTRO:
                    _state.Stage = GameProgressTestRunState.FlowStage.Intro;
                    SetCheckpoint(
                        GameProgressTestRunState.CheckpointKind.Intro,
                        levelAddress,
                        0);
                    _state.Status = $"Intro: {levelAddress}.";
                    AddLog($"Level Select: загружен Intro {levelAddress}.");
                    break;
                case GameState.PLAYING:
                    _state.Stage = GameProgressTestRunState.FlowStage.Gameplay;
                    SetCheckpoint(
                        GameProgressTestRunState.CheckpointKind.Gameplay,
                        levelAddress,
                        0);
                    _state.Status = $"Gameplay: {levelAddress}. Intro отсутствует.";
                    AddLog($"Level Select: запущен Gameplay без Intro {levelAddress}.");
                    break;
                default:
                    throw new InvalidOperationException(
                        $"Неожиданное состояние уровня: {state}.");
            }
        }

        private void SetCheckpoint(
            GameProgressTestRunState.CheckpointKind kind,
            string levelAddress,
            int score)
        {
            _state.Checkpoint = kind;
            _state.CheckpointLevelAddress = levelAddress?.Trim() ?? string.Empty;
            _state.CheckpointScore = score;
        }

        private TargetRoute ResolveTargetRoute()
        {
            var selection = LevelSelectionModel.Create();
            foreach (var location in selection.Locations)
            {
                foreach (var part in location.Parts)
                {
                    foreach (var level in part.Levels)
                    {
                        if (string.Equals(
                                level.Address?.Trim(),
                                _state.TargetLevelAddress?.Trim(),
                                StringComparison.OrdinalIgnoreCase))
                        {
                            return new TargetRoute(location, part, level);
                        }
                    }
                }
            }

            throw new InvalidOperationException(
                $"Целевой уровень {_state.TargetLevelAddress} отсутствует в catalog.");
        }

        private async Task WaitForGameReadyAsync(
            string levelAddress,
            CancellationToken token)
        {
            await WaitUntilAsync(
                () =>
                {
                    if (!string.Equals(
                            SceneManager.GetActiveScene().name,
                            "Game",
                            StringComparison.Ordinal))
                    {
                        return false;
                    }

                    var currentLevel = GameDataManager.PlayerData?.CurrentLevel;
                    if (!string.Equals(
                            currentLevel?.Trim(),
                            levelAddress?.Trim(),
                            StringComparison.OrdinalIgnoreCase))
                    {
                        return false;
                    }

                    var gameState = GetGameState();
                    return gameState == GameState.INTRO ||
                           gameState == GameState.PLAYING;
                },
                $"Уровень {levelAddress} не загрузился.",
                token);
        }

        private static async Task WaitUntilAsync(
            Func<bool> predicate,
            string timeoutMessage,
            CancellationToken token,
            double timeoutSeconds = DefaultTimeoutSeconds)
        {
            var deadline = EditorApplication.timeSinceStartup + timeoutSeconds;
            while (!predicate())
            {
                token.ThrowIfCancellationRequested();
                if (EditorApplication.timeSinceStartup >= deadline)
                    throw new TimeoutException(timeoutMessage);

                await Task.Delay(PollDelayMilliseconds, token);
            }
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

        private bool IsAtExpectedStage()
        {
            if (!EditorApplication.isPlaying)
                return false;

            if (!_state.IsActive)
                return IsMainMenuShown();

            try
            {
                return _state.Stage switch
                {
                    GameProgressTestRunState.FlowStage.MainMenu => IsMainMenuShown(),
                    GameProgressTestRunState.FlowStage.SelectDayPart =>
                        IsTargetDayPartCardShown(ResolveTargetRoute().Part),
                    GameProgressTestRunState.FlowStage.SelectLevel =>
                        IsTargetLevelCardShown(ResolveTargetRoute()),
                    GameProgressTestRunState.FlowStage.Intro =>
                        IsExpectedGameState(GameState.INTRO),
                    GameProgressTestRunState.FlowStage.Gameplay =>
                        IsExpectedGameState(GameState.PLAYING),
                    GameProgressTestRunState.FlowStage.Win =>
                        IsExpectedGameState(GameState.FINISHED) && IsRealWinShown(),
                    GameProgressTestRunState.FlowStage.Completed =>
                        (IsExpectedGameState(GameState.FINISHED) && IsRealWinShown()) ||
                        IsSelectLevelScreenShown(),
                    GameProgressTestRunState.FlowStage.Cancelled => IsMainMenuShown(),
                    _ => false
                };
            }
            catch (Exception)
            {
                return false;
            }
        }

        private bool IsExpectedGameState(GameState expectedState)
        {
            if (!string.Equals(
                    SceneManager.GetActiveScene().name,
                    "Game",
                    StringComparison.Ordinal))
            {
                return false;
            }

            if (expectedState != GameState.FINISHED && HasBlockingModal())
                return false;

            var expectedLevel = _state.CheckpointLevelAddress;
            var currentLevel = GameDataManager.PlayerData?.CurrentLevel;
            return string.Equals(
                       expectedLevel?.Trim(),
                       currentLevel?.Trim(),
                       StringComparison.OrdinalIgnoreCase) &&
                   GetGameState() == expectedState;
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

        private static bool IsRealWinShown()
        {
            return GetGameState() == GameState.FINISHED &&
                   FindVisibleElement<VisualElement>("win_result") != null;
        }

        private static bool IsTargetPartShown(LevelSelectionModel.PartView part)
        {
            if (!IsSelectLevelScreenShown())
                return false;

            return string.Equals(
                GetVisibleLabelText("location__title"),
                part.DisplayName,
                StringComparison.Ordinal);
        }

        private static bool IsTargetDayPartCardShown(LevelSelectionModel.PartView part)
        {
            return IsSelectLevelScreenShown() &&
                   part.Levels.Count > 0 &&
                   FindVisibleLevelItem(part.Levels[0].Address) != null;
        }

        private static bool IsTargetLevelCardShown(TargetRoute route)
        {
            return IsTargetPartShown(route.Part) &&
                   FindVisibleLevelItem(route.Level.Address) != null;
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

        private static LevelItem FindVisibleLevelItem(string levelAddress)
        {
            return FindVisibleElements<LevelItem>()
                .FirstOrDefault(item =>
                    string.Equals(
                        item.LevelName?.Trim(),
                        levelAddress?.Trim(),
                        StringComparison.OrdinalIgnoreCase));
        }

        private static string GetVisibleLabelText(string name)
        {
            return FindVisibleElement<Label>(name)?.text?.Trim() ?? string.Empty;
        }

        private static T FindVisibleElement<T>(string name)
            where T : VisualElement
        {
            return FindVisibleElements<T>(name).FirstOrDefault();
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

        private string ResolveCurrentLevelForDisplay()
        {
            if (!string.IsNullOrWhiteSpace(_state.TargetLevelAddress))
                return _state.TargetLevelAddress;

            return GameDataManager.PlayerData?.CurrentLevel ?? "уровень не выбран";
        }

        private void AddLog(string message)
        {
            _state.Log ??= new List<string>();
            _state.Log.Add($"[{DateTime.Now:HH:mm:ss}] {message}");
        }

        private static GameProgressTestRunState LoadState()
        {
            var json = SessionState.GetString(SessionStateKey, string.Empty);
            if (string.IsNullOrWhiteSpace(json))
                return new GameProgressTestRunState();

            try
            {
                var state = JsonUtility.FromJson<GameProgressTestRunState>(json);
                if (state == null ||
                    state.Version != GameProgressTestRunState.CurrentVersion)
                {
                    return new GameProgressTestRunState();
                }

                state.Log ??= new List<string>();
                return state;
            }
            catch (Exception)
            {
                return new GameProgressTestRunState();
            }
        }

        private void SaveState()
        {
            if (_disposed || _sessionCleared)
                return;

            SessionState.SetString(SessionStateKey, JsonUtility.ToJson(_state));
        }

        private readonly struct TargetRoute
        {
            public TargetRoute(
                LevelSelectionModel.LocationView location,
                LevelSelectionModel.PartView part,
                LevelSelectionModel.LevelReference level)
            {
                Location = location;
                Part = part;
                Level = level;
            }

            public LevelSelectionModel.LocationView Location { get; }
            public LevelSelectionModel.PartView Part { get; }
            public LevelSelectionModel.LevelReference Level { get; }
        }
    }
}
