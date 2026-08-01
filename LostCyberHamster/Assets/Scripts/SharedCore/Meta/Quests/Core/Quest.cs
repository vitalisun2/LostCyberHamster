using System;
using System.Collections.Generic;

namespace Vues.GameCore.Quests
{
    /// <summary>
    /// Хранит состояние и управляет прогрессом одного квеста игрока.
    /// </summary>
    [Serializable]
    public sealed class Quest
    {
        [NonSerialized]
        private QuestDefinition _definition;

        [NonSerialized]
        private IQuestStrategy _strategy;

        /// <summary>
        /// Идентификатор определения квеста.
        /// </summary>
        public string QuestId;

        /// <summary>
        /// Текущий прогресс.
        /// </summary>
        public int CurrentProgress;

        /// <summary>
        /// Признак достижения цели.
        /// </summary>
        public bool IsCompleted;

        /// <summary>
        /// Признак уже полученной награды.
        /// </summary>
        public bool IsRewardClaimed;

        /// <summary>
        /// Уровни, уже засчитанные квестом с уникальными результатами.
        /// </summary>
        public List<string> CountedLevelKeys = new();

        /// <summary>
        /// Подключённое описание квеста.
        /// </summary>
        public QuestDefinition Definition => _definition;

        public string Id => _definition?.Id ?? QuestId;
        public string TitleLocalizationKey =>
            _definition?.TitleLocalizationKey ?? QuestId;
        public QuestCategory Category =>
            _definition?.Category ?? QuestCategory.None;
        public QuestType Type => _definition?.Type ?? QuestType.None;
        public string ActionId => _definition?.ActionId;
        public int TargetAmount => _definition?.TargetAmount ?? 0;
        public ResourceType RewardType =>
            _definition != null
                ? _definition.RewardType
                : default;
        public int RewardAmount => _definition?.RewardAmount ?? 0;
        public bool CanClaimReward =>
            IsCompleted && !IsRewardClaimed;

        /// <summary>
        /// Подключает описание и стратегию к новому или сохранённому квесту.
        /// </summary>
        public void Bind(
            QuestDefinition definition,
            IQuestStrategy strategy)
        {
            // Проверяем описание и стратегию активного типа.
            QuestValidator.ValidateBinding(definition, strategy);

            // Подключаем runtime-данные и восстанавливаем состояние.
            _definition = definition;
            _strategy = strategy;
            BindState();
        }

        /// <summary>
        /// Применяет подходящее событие и сообщает, изменился ли прогресс.
        /// </summary>
        public bool Handle(QuestEvent questEvent)
        {
            if (_definition == null || _strategy == null)
            {
                throw new InvalidOperationException(
                    "Квест не подключён к описанию и стратегии.");
            }

            if (questEvent == null)
            {
                throw new ArgumentNullException(nameof(questEvent));
            }

            if (IsCompleted)
            {
                return false;
            }

            // Стратегия решает, относится ли событие к квесту.
            int progress = _strategy.CalculateProgress(
                _definition,
                questEvent);
            if (progress <= 0)
            {
                return false;
            }

            // Отсекаем повторный результат уровня для квестов на разные уровни.
            if (_definition.CountUniqueLevels)
            {
                if (questEvent is not LevelResultQuestEvent levelResult ||
                    string.IsNullOrWhiteSpace(levelResult.LevelKey))
                {
                    return false;
                }

                CountedLevelKeys ??= new List<string>();
                if (CountedLevelKeys.Contains(levelResult.LevelKey))
                {
                    return false;
                }

                CountedLevelKeys.Add(levelResult.LevelKey);
            }

            // Добавляем прогресс в пределах цели.
            int remainingProgress =
                _definition.TargetAmount - CurrentProgress;
            CurrentProgress += Math.Min(
                progress,
                remainingProgress);
            IsCompleted =
                CurrentProgress >= _definition.TargetAmount;
            return true;
        }

        /// <summary>
        /// Очищает прогресс и статус награды.
        /// </summary>
        public void Reset()
        {
            if (_definition == null)
            {
                throw new InvalidOperationException(
                    "Квест не подключён к описанию.");
            }

            QuestId = _definition.Id;
            CurrentProgress = 0;
            IsCompleted = false;
            IsRewardClaimed = false;
            CountedLevelKeys ??= new List<string>();
            CountedLevelKeys.Clear();
        }

        /// <summary>
        /// Отмечает награду завершённого квеста как полученную.
        /// </summary>
        public bool MarkRewardClaimed()
        {
            if (!CanClaimReward)
            {
                return false;
            }

            IsRewardClaimed = true;
            return true;
        }

        private void BindState()
        {
            if (QuestId != _definition.Id)
            {
                Reset();
                return;
            }

            CountedLevelKeys ??= new List<string>();
            if (_definition.CountUniqueLevels)
            {
                CurrentProgress = Math.Clamp(
                    CountedLevelKeys.Count,
                    0,
                    _definition.TargetAmount);
            }
            else
            {
                CountedLevelKeys.Clear();
                CurrentProgress = Math.Clamp(
                    CurrentProgress,
                    0,
                    _definition.TargetAmount);
            }
            IsCompleted =
                CurrentProgress >= _definition.TargetAmount;
            if (!IsCompleted)
            {
                IsRewardClaimed = false;
            }
        }

    }
}
