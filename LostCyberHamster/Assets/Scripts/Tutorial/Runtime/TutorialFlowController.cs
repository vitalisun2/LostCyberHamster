using System;
using Assets.Scripts.Diagnostics;
using Assets.Scripts.GameManagerLogic;
using Assets.Scripts.Gameplay;
using GameManagement;
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
        private readonly TutorialSkinLessonController _skinLesson;

        private TutorialGameplayController _gameplay;
        private TutorialPhase _phase;
        private string _activeGameplayLevel;
        private bool _disposed;

        public TutorialFlowController(TutorialSession session)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _skinLesson = new TutorialSkinLessonController(new TutorialSkinLessonView());
            _skinLesson.Completed += HandleSkinLessonCompleted;
        }

        public TutorialPhase Phase => _phase;

        public bool RequiresExclusiveInput => _session.IsActive || _phase != TutorialPhase.None;

        public bool CanShutdown => _phase == TutorialPhase.Completed && !_session.IsActive;

        /// <summary>
        /// Запускает основной gameplay-урок или завершает устаревший SuperHit-переход.
        /// </summary>
        public void EnsureGameplay(string levelAddress, GameManager gameManager, Hamster hamster)
        {
            ThrowIfDisposed();
            if (_gameplay != null && string.Equals(_activeGameplayLevel, levelAddress, StringComparison.Ordinal))
            {
                return;
            }

            DisposeGameplay();
            _skinLesson.OnSceneLoaded();
            if (TutorialConstants.IsCoreLessonLevel(levelAddress))
            {
                StartGameplayScenario(
                    levelAddress,
                    gameManager,
                    hamster);
                return;
            }

            if (TutorialConstants.IsSuperHitLessonLevel(levelAddress))
            {
                _session.Begin();
                StartFirstGameplayLevel();
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
            if (_phase == TutorialPhase.SkinLesson)
            {
                _skinLesson.Tick();
            }
        }

        /// <summary>
        /// Сбрасывает только scene-bound gameplay-controller; session и общий workflow сохраняются.
        /// </summary>
        public void OnSceneLoaded()
        {
            if (_disposed)
            {
                return;
            }

            DisposeGameplay();
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            DisposeGameplay();
            _skinLesson.Completed -= HandleSkinLessonCompleted;
            _skinLesson.Dispose();
            _disposed = true;
        }

        private void StartGameplayScenario(
            string levelAddress,
            GameManager gameManager,
            Hamster hamster)
        {
            _skinLesson.Reset();
            _session.PrepareCoreLesson();
            _phase = TutorialPhase.CoreControls;

            _gameplay = new TutorialGameplayController(
                new TutorialGameplayWorldAdapter(gameManager, hamster),
                TutorialGameplayScenario.CoreControls);
            _gameplay.ScenarioCompleted += HandleGameplayScenarioCompleted;
            _gameplay.SkipRequested += HandleSkipRequested;
            _activeGameplayLevel = levelAddress;
        }

        private void HandleGameplayScenarioCompleted()
        {
            if (_gameplay == null)
            {
                return;
            }

            StartSkinLessonTransition();
        }

        private void StartSkinLessonTransition()
        {
            _session.PrepareSkinLesson(_skinLesson.RequiredCrystals);
            _skinLesson.Activate();
            _phase = TutorialPhase.SkinLesson;

            _gameplay.ShowCompletion(
                "Вы освоили управление",
                $"Попробуйте купить скин с молнией за {_skinLesson.RequiredCrystals} учебных кристаллов.",
                "В меню",
                "В меню",
                showPrimaryButton: false,
                OpenMenuForSkinLesson,
                OpenMenuForSkinLesson);
        }

        private void HandleSkinLessonCompleted()
        {
            DeviceLogUploader.UploadDiagnosticLog("tutorial_completed");
            StartFirstGameplayLevel();
        }

        private void HandleSkipRequested()
        {
            _phase = TutorialPhase.Completed;
            StartFirstGameplayLevel();
        }

        private void OpenMenuForSkinLesson()
        {
            Time.timeScale = 1f;
            GameDataManager.IsGameJustStarted = false;
            GameDataManager.PlayerData.CurrentLevel = TutorialConstants.FirstGameplayLevelAddress;
            SceneManager.LoadScene(TutorialConstants.MenuSceneName);
        }

        private void StartFirstGameplayLevel()
        {
            CompleteSessionAtFirstGameplayLevel();
            SceneManager.LoadScene(TutorialConstants.GameSceneName);
        }

        private void CompleteSessionAtFirstGameplayLevel()
        {
            _session.Complete(TutorialConstants.FirstGameplayLevelAddress);
            _phase = TutorialPhase.Completed;
        }

        private void DisposeGameplay()
        {
            if (_gameplay != null)
            {
                _gameplay.ScenarioCompleted -= HandleGameplayScenarioCompleted;
                _gameplay.SkipRequested -= HandleSkipRequested;
                _gameplay.Dispose();
            }

            _gameplay = null;
            _activeGameplayLevel = null;
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
