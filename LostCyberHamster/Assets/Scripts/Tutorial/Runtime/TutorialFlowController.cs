using System;
using Assets.Scripts.GameManagerLogic;
using Assets.Scripts.Gameplay;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Assets.Scripts.Tutorial
{
    /// <summary>
    /// Единственный orchestrator крупных фаз tutorial workflow.
    /// </summary>
    public sealed class TutorialFlowController : IDisposable
    {
        private readonly TutorialSession _session;

        private TutorialGameplayController _gameplay;
        private TutorialPhase _phase;
        private string _activeGameplayLevel;
        private bool _disposed;

        public TutorialFlowController(TutorialSession session)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
        }

        public TutorialPhase Phase => _phase;

        public bool RequiresExclusiveInput => _session.IsActive || _phase != TutorialPhase.None;

        public bool RequiresGameplayRoot => _gameplay != null && _phase != TutorialPhase.Completed;

        public bool CanShutdown =>
            _phase == TutorialPhase.Completed && !_session.IsActive && _gameplay == null;

        /// <summary>
        /// Запускает основной gameplay-урок для tutorial level.
        /// </summary>
        public void EnsureGameplay(string levelAddress, GameManager gameManager, Hamster hamster)
        {
            ThrowIfDisposed();
            if (_phase == TutorialPhase.Completion || _phase == TutorialPhase.Completed
                || (_gameplay != null && string.Equals(_activeGameplayLevel, levelAddress, StringComparison.Ordinal)))
            {
                return;
            }

            DisposeGameplay();
            if (TutorialConstants.IsCoreLessonLevel(levelAddress))
            {
                StartGameplayScenario(levelAddress, gameManager, hamster);
            }
        }

        public void AttachGameplayRoot(VisualElement root)
        {
            ThrowIfDisposed();
            _gameplay?.Attach(root);
        }

        public void Tick()
        {
            ThrowIfDisposed();
            _gameplay?.Tick();
        }

        /// <summary>Освобождает scene-bound UI после фактической загрузки новой сцены.</summary>
        public void OnSceneLoaded()
        {
            if (_disposed)
            {
                return;
            }

            DisposeGameplay(resumeGame: false);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            // Teardown может идти после уничтожения объектов мира, без sceneLoaded.
            DisposeGameplay(resumeGame: false);
            _disposed = true;
        }

        private void StartGameplayScenario(
            string levelAddress,
            GameManager gameManager,
            Hamster hamster)
        {
            _session.PrepareCoreLesson();
            _phase = TutorialPhase.CoreControls;

            _gameplay = new TutorialGameplayController(
                new TutorialGameplayWorldAdapter(gameManager, hamster));
            _gameplay.ScenarioCompleted += HandleGameplayScenarioCompleted;
            _gameplay.SkipRequested += HandleSkipRequested;
            _activeGameplayLevel = levelAddress;
        }

        /// <summary>Сохраняет завершение восьми шагов перед показом успешного результата.</summary>
        private void HandleGameplayScenarioCompleted()
        {
            if (_gameplay == null || _phase != TutorialPhase.CoreControls)
            {
                return;
            }

            _phase = TutorialPhase.Completion;
            CompleteTutorial(continueToGame: false);
        }

        /// <summary>Сохраняет завершение и продолжает прямой маршрут Skip в первый уровень.</summary>
        private void HandleSkipRequested()
        {
            if (_gameplay == null || _phase != TutorialPhase.CoreControls)
            {
                return;
            }

            _phase = TutorialPhase.Completion;
            CompleteTutorial(continueToGame: true);
        }

        /// <summary>Повторяет обязательную запись при ошибке и продолжает исходное действие после успеха.</summary>
        private void CompleteTutorial(bool continueToGame)
        {
            if (_disposed || _gameplay == null || _phase != TutorialPhase.Completion)
            {
                return;
            }

            // Success UI доступен только после восстановленного и сохранённого snapshot.
            try
            {
                _session.Complete(TutorialConstants.FirstGameplayLevelAddress);
            }
            catch (Exception exception)
            {
                Debug.LogError($"Tutorial completion save failed ({exception.GetType().Name}).");
                _gameplay.ShowCompletion(
                    Localize("tutorial_title"),
                    Localize("tutorial_complete_save_error"),
                    Localize("btn_retry"),
                    () => CompleteTutorial(continueToGame),
                    isError: true);
                return;
            }

            // Retry сохраняет исходный маршрут: успешное окно или прямой Skip.
            if (continueToGame)
            {
                StartFirstGameplayLevel();
            }
            else
            {
                ShowSuccessfulCompletion();
            }
        }

        /// <summary>Показывает единственную Play для уже сохранённого завершения.</summary>
        private void ShowSuccessfulCompletion()
        {
            _gameplay?.ShowCompletion(
                Localize("tutorial_complete_title"),
                Localize("tutorial_complete_message"),
                Localize("btn_play"),
                StartFirstGameplayLevel);
        }

        /// <summary>Запускает первый уровень с уже сохранёнными данными; освобождение UI завершает sceneLoaded.</summary>
        private void StartFirstGameplayLevel()
        {
            if (_disposed || _phase != TutorialPhase.Completion || _session.IsActive)
            {
                return;
            }

            // Flow владеет навигацией; Play повторно не сохраняет tutorial.
            _phase = TutorialPhase.Completed;
            try
            {
                SceneManager.LoadScene(TutorialConstants.GameSceneName);
            }
            catch
            {
                // Неудачный запрос навигации оставляет сохранённый результат и доступную Play.
                _phase = TutorialPhase.Completion;
                ShowSuccessfulCompletion();
                throw;
            }
        }

        /// <summary>Отсоединяет gameplay и выбирает освобождение до либо после уничтожения сцены.</summary>
        private void DisposeGameplay(bool resumeGame = true)
        {
            if (_gameplay != null)
            {
                _gameplay.ScenarioCompleted -= HandleGameplayScenarioCompleted;
                _gameplay.SkipRequested -= HandleSkipRequested;
                if (resumeGame)
                {
                    _gameplay.Dispose();
                }
                else
                {
                    _gameplay.DisposeAfterSceneChange();
                }
            }

            _gameplay = null;
            _activeGameplayLevel = null;
        }

        private static string Localize(string key)
        {
            string text = LocalizationManager.GetLocalizedString(key);
            return string.IsNullOrWhiteSpace(text) ? key : text;
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(TutorialFlowController));
            }
        }
    }
}
