using System;
using GameManagement;

namespace Vues.GameCore
{
    /// <summary>
    /// Отслеживает выполнение одного квеста на перепрыгивание препятствий.
    /// </summary>
    public sealed class BasicQuestLifecycle
    {
        private readonly Quest _definition;
        private readonly BasicQuestState _state;
        private bool _isTracking;

        /// <summary>
        /// Связывает определение квеста с его состоянием в данных игрока.
        /// </summary>
        public BasicQuestLifecycle(Quest definition, PlayerData playerData)
        {
            _definition = definition ?? throw new ArgumentNullException(nameof(definition));
            if (playerData == null)
            {
                throw new ArgumentNullException(nameof(playerData));
            }

            _state = playerData.BasicQuest ??= new BasicQuestState();
            if (_state.QuestId != definition.Id)
            {
                _state.QuestId = definition.Id;
                _state.CurrentProgress = 0;
                _state.IsCompleted = false;
            }
        }

        /// <summary>
        /// Начинает учитывать успешные перепрыгивания препятствий.
        /// </summary>
        public void StartTracking()
        {
            if (_isTracking)
            {
                return;
            }

            GameEventsManager.OnObstacleJumpedOver += HandleObstacleJumpedOver;
            _isTracking = true;
        }

        /// <summary>
        /// Прекращает учитывать перепрыгивания препятствий.
        /// </summary>
        public void StopTracking()
        {
            if (!_isTracking)
            {
                return;
            }

            GameEventsManager.OnObstacleJumpedOver -= HandleObstacleJumpedOver;
            _isTracking = false;
        }

        private void HandleObstacleJumpedOver(string _)
        {
            if (_state.IsCompleted)
            {
                return;
            }

            _state.CurrentProgress = Math.Min(_state.CurrentProgress + 1, _definition.TargetAmount);
            _state.IsCompleted = _state.CurrentProgress >= _definition.TargetAmount;
        }
    }
}
