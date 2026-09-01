using System;
using GameManagement;
using LostCyberHamster.UI;
using UnityEngine;

namespace Assets.Scripts.GameEngine.Mechanics
{
    /// <summary>
    /// Продолжает выбранный маршрут результата после возможного Level Up.
    /// </summary>
    internal sealed class LevelResultNavigationCoordinator
    {
        private readonly UIManager _uiManager;
        private int _previousPlayerLevel;

        public LevelResultNavigationCoordinator(UIManager uiManager)
        {
            _uiManager = uiManager ??
                throw new ArgumentNullException(nameof(uiManager));
            _previousPlayerLevel =
                GameDataManager.PlayerData?.PlayerLevel ?? 0;
        }

        /// <summary>
        /// Закрывает result-модалку и выполняет действие после Level Up.
        /// </summary>
        public async void Continue(
            ScreenEnum sourceModal,
            Action action)
        {
            if (action == null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            bool actionInvoked = false;

            void InvokeActionOnce()
            {
                if (actionInvoked)
                {
                    return;
                }

                actionInvoked = true;
                action.Invoke();
            }

            try
            {
                int previousPlayerLevel = _previousPlayerLevel;
                int currentPlayerLevel =
                    GameDataManager.PlayerData?.PlayerLevel ??
                    previousPlayerLevel;
                int levelsGained =
                    currentPlayerLevel - previousPlayerLevel;

                // Фиксируем результат и освобождаем исходную модалку.
                _previousPlayerLevel = currentPlayerLevel;
                _uiManager.CloseModal(sourceModal);

                if (levelsGained <= 0)
                {
                    InvokeActionOnce();
                    return;
                }

                // Показываем Level Up перед выбранным переходом.
                var levelUpModalController =
                    _uiManager.GetController<LevelUpModalController>();
                levelUpModalController.SetLevelUpData(
                    previousPlayerLevel,
                    currentPlayerLevel,
                    levelsGained);
                levelUpModalController.SetOkAction(InvokeActionOnce);
                await _uiManager.ShowModalAsync(ScreenEnum.LevelUpModal);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                InvokeActionOnce();
            }
        }
    }
}
