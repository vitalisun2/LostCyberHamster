using System;

namespace Vues.GameCore.Quests
{
    /// <summary>
    /// Управляет прогрессом одного активного квеста MVP.
    /// </summary>
    public sealed class QuestSystem
    {
        private readonly IQuestStrategy _strategy;

        /// <summary>
        /// Описание активного квеста.
        /// </summary>
        public QuestDefinition Definition { get; }

        /// <summary>
        /// Текущее состояние активного квеста.
        /// </summary>
        public QuestState State { get; }

        /// <summary>
        /// Связывает описание, сохранённое состояние и стратегию.
        /// </summary>
        public QuestSystem(
            QuestDefinition definition,
            QuestState state,
            IQuestStrategy strategy)
        {
            // Проверяем описание и стратегию активного типа.
            ValidateDefinition(definition);
            if (strategy == null)
            {
                throw new ArgumentNullException(nameof(strategy));
            }

            if (strategy.Type != definition.Type)
            {
                throw new ArgumentException(
                    $"Стратегия {strategy.Type} не подходит типу {definition.Type}.",
                    nameof(strategy));
            }

            // Восстанавливаем либо очищаем состояние активного квеста.
            Definition = definition;
            State = state ?? throw new ArgumentNullException(nameof(state));
            _strategy = strategy;
            BindState();
        }

        /// <summary>
        /// Применяет подходящее событие и сообщает, изменился ли прогресс.
        /// </summary>
        public bool Handle(QuestEvent questEvent)
        {
            if (questEvent == null)
            {
                throw new ArgumentNullException(nameof(questEvent));
            }

            if (State.IsCompleted)
            {
                return false;
            }

            // Стратегия решает, относится ли событие к активному квесту.
            int progress = _strategy.CalculateProgress(
                Definition,
                questEvent);
            if (progress <= 0)
            {
                return false;
            }

            // Прогресс не выходит за цель, завершённый квест стабилен.
            int remainingProgress =
                Definition.TargetAmount - State.CurrentProgress;
            State.CurrentProgress += Math.Min(
                progress,
                remainingProgress);
            State.IsCompleted =
                State.CurrentProgress >= Definition.TargetAmount;
            return true;
        }

        private void BindState()
        {
            if (State.QuestId != Definition.Id)
            {
                State.QuestId = Definition.Id;
                State.CurrentProgress = 0;
                State.IsCompleted = false;
                State.IsRewardClaimed = false;
                return;
            }

            State.CurrentProgress = Math.Clamp(
                State.CurrentProgress,
                0,
                Definition.TargetAmount);
            State.IsCompleted =
                State.CurrentProgress >= Definition.TargetAmount;
            if (!State.IsCompleted)
            {
                State.IsRewardClaimed = false;
            }
        }

        internal static void ValidateDefinition(
            QuestDefinition definition)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            if (string.IsNullOrWhiteSpace(definition.Id) ||
                string.IsNullOrWhiteSpace(
                    definition.TitleLocalizationKey))
            {
                throw new ArgumentException(
                    "Описание квеста содержит пустые обязательные данные.",
                    nameof(definition));
            }

            if (!Enum.IsDefined(
                    typeof(QuestType),
                    definition.Type) ||
                definition.Type == QuestType.None)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(definition),
                    definition.Type,
                    "Тип квеста не задан.");
            }

            if (definition.Type == QuestType.ActionCounter &&
                !GameplayActionIds.IsKnown(definition.ActionId))
            {
                throw new ArgumentException(
                    "Действие квеста-счётчика не поддерживается.",
                    nameof(definition));
            }

            if (definition.TargetAmount <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(definition),
                    definition.TargetAmount,
                    "Цель квеста должна быть положительной.");
            }

            if (definition.RewardType != ResourceType.Coins &&
                definition.RewardType != ResourceType.Crystals)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(definition),
                    definition.RewardType,
                    "Тип награды квеста не поддерживается.");
            }

            if (definition.RewardAmount <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(definition),
                    definition.RewardAmount,
                    "Награда квеста должна быть положительной.");
            }
        }
    }
}
