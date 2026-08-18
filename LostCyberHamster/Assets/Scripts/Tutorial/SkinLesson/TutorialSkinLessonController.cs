using System;
using System.Collections.Generic;
using GameManagement;
using LostCyberHamster.UI;

namespace Assets.Scripts.Tutorial
{
    /// <summary>
    /// Ведёт последовательность UI-шагов урока скина и валидирует их фактический результат.
    /// </summary>
    public sealed class TutorialSkinLessonController : IDisposable
    {
        public const int TargetSkinId = 2;

        private const int _fallbackSkinPrice = 20;

        private static readonly IReadOnlyList<TutorialSkinStep> _steps = new[]
        {
            new TutorialSkinStep(
                TutorialSkinAction.OpenCharacterScreen,
                new TutorialSkinPrompt(
                    ScreenEnum.HomeScreen,
                    TutorialSkinTarget.HomeCharacterButton,
                    "Откройте экипировку",
                    TutorialFocusShape.Circle),
                ScreenEnum.CharacterScreen),
            new TutorialSkinStep(
                TutorialSkinAction.SelectSkin,
                new TutorialSkinPrompt(
                    ScreenEnum.CharacterScreen,
                    TutorialSkinTarget.SkinCard,
                    "Выберите скин с молнией",
                    TutorialFocusShape.Circle)),
            new TutorialSkinStep(
                TutorialSkinAction.BuySkin,
                new TutorialSkinPrompt(
                    ScreenEnum.CharacterScreen,
                    TutorialSkinTarget.SkinChangeButton,
                    "Купите скин с молнией",
                    TutorialFocusShape.RoundedRect)),
            new TutorialSkinStep(
                TutorialSkinAction.EquipSkin,
                new TutorialSkinPrompt(
                    ScreenEnum.CharacterScreen,
                    TutorialSkinTarget.SkinChangeButton,
                    "Наденьте скин",
                    TutorialFocusShape.RoundedRect))
        };

        private readonly TutorialSkinLessonView _view;

        private int _currentStepIndex = -1;
        private bool _isActive;
        private bool _isDisposed;

        public TutorialSkinLessonController(TutorialSkinLessonView view)
        {
            _view = view ?? throw new ArgumentNullException(nameof(view));
            _view.AllowedActionPerformed += OnAllowedActionPerformed;
        }

        public bool IsActive => _isActive;

        public TutorialSkinStep CurrentStep =>
            _currentStepIndex >= 0 && _currentStepIndex < _steps.Count
                ? _steps[_currentStepIndex]
                : null;

        public int RequiredCrystals =>
            SkinManager.AvailableSkins.Find(skin => skin.Id == TargetSkinId)?.Price
            ?? _fallbackSkinPrice;

        public event Action Completed;

        /// <summary>
        /// Запускает урок с первого UI-шага; sandbox должен быть подготовлен orchestrator заранее.
        /// </summary>
        public void Activate()
        {
            ThrowIfDisposed();
            if (_isActive)
            {
                return;
            }

            _view.Reset();
            _currentStepIndex = 0;
            _isActive = true;
            Tick();
        }

        /// <summary>
        /// Отслеживает смену screen root и показывает текущую UI-подсказку.
        /// </summary>
        public void Tick()
        {
            ThrowIfDisposed();
            if (!_isActive || CurrentStep == null)
            {
                return;
            }

            // Шаг открытия завершается только после фактического появления CharacterScreen.
            if (CurrentStep.CompletionSurface.HasValue
                && _view.IsSurfaceVisible(CurrentStep.CompletionSurface.Value))
            {
                Advance();
                return;
            }

            _view.Show(CurrentStep);
        }

        /// <summary>
        /// Сбрасывает progress и UI урока без удаления controller.
        /// </summary>
        public void Reset()
        {
            if (_isDisposed)
            {
                return;
            }

            _isActive = false;
            _currentStepIndex = -1;
            _view.Reset();
        }

        /// <summary>
        /// Отписывает controller и освобождает принадлежащую ему view.
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            _isActive = false;
            _currentStepIndex = -1;
            _view.AllowedActionPerformed -= OnAllowedActionPerformed;
            _view.Dispose();
            Completed = null;
            _isDisposed = true;
        }

        private void OnAllowedActionPerformed(TutorialSkinAction action)
        {
            if (!_isActive || CurrentStep == null || CurrentStep.Action != action)
            {
                return;
            }

            // Tutorial не выполняет game-команду: проверяет state после штатного CharacterScreen handler.
            if (!IsCurrentStepCompleted())
            {
                return;
            }

            Advance();
        }

        private bool IsCurrentStepCompleted()
        {
            return CurrentStep.Action switch
            {
                TutorialSkinAction.SelectSkin =>
                    _view.IsSkinDisplayed(TargetSkinId),
                TutorialSkinAction.BuySkin =>
                    _view.IsSkinDisplayed(TargetSkinId) && IsTargetSkinPurchased(),
                TutorialSkinAction.EquipSkin =>
                    _view.IsSkinDisplayed(TargetSkinId) && SkinManager.CurrentSkin?.Id == TargetSkinId,
                TutorialSkinAction.OpenCharacterScreen =>
                    CurrentStep.CompletionSurface.HasValue
                    && _view.IsSurfaceVisible(CurrentStep.CompletionSurface.Value),
                _ => false
            };
        }

        private static bool IsTargetSkinPurchased()
        {
            return GameDataManager.PlayerData.PurchasedSkinIds?.Contains(TargetSkinId) == true;
        }

        private void Advance()
        {
            _view.Reset();
            _currentStepIndex++;
            if (_currentStepIndex >= _steps.Count)
            {
                Complete();
                return;
            }

            Tick();
        }

        private void Complete()
        {
            _isActive = false;
            _currentStepIndex = -1;
            Completed?.Invoke();
        }

        private void ThrowIfDisposed()
        {
            if (_isDisposed)
            {
                throw new ObjectDisposedException(nameof(TutorialSkinLessonController));
            }
        }

        public void OnSceneLoaded()
        {
            if (!_isDisposed)
            {
                _view.InvalidateDocumentCache();
            }
        }
    }
}
