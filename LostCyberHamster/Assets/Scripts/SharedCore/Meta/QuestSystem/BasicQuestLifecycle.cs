using GameManagement;
using Vues.GameCore.Quests;

namespace Vues.GameCore
{
    /// <summary>
    /// Сохраняет старый вход к первому квесту поверх нового ядра.
    /// </summary>
    public sealed class BasicQuestLifecycle
    {
        private readonly QuestSystem _questSystem;
        private bool _isTracking;

        /// <summary>
        /// Связывает старое описание квеста с новым ядром и состоянием игрока.
        /// </summary>
        public BasicQuestLifecycle(Quest definition, PlayerData playerData)
        {
            if (definition == null)
            {
                throw new System.ArgumentNullException(nameof(definition));
            }

            if (playerData == null)
            {
                throw new System.ArgumentNullException(nameof(playerData));
            }

            var questDefinition = new QuestDefinition
            {
                Id = definition.Id,
                Title = string.IsNullOrWhiteSpace(definition.Title)
                    ? definition.Id
                    : definition.Title,
                Type = QuestType.ActionCounter,
                ActionId =
                    ActionQuestEvent.ObstacleJumpedOverActionId,
                TargetAmount = definition.TargetAmount
            };
            playerData.BasicQuest ??= new QuestState();
            _questSystem = new QuestSystem(
                questDefinition,
                playerData.BasicQuest,
                new ActionCounterQuestStrategy());
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

            GameEventsManager.OnActionQuestEvent += HandleActionQuestEvent;
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

            GameEventsManager.OnActionQuestEvent -= HandleActionQuestEvent;
            _isTracking = false;
        }

        private void HandleActionQuestEvent(
            ActionQuestEvent questEvent)
        {
            _questSystem.Handle(questEvent);
        }
    }
}
