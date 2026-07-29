using System;

namespace Vues.GameCore.Quests
{
    /// <summary>
    /// Сообщает о выполненном игроком действии.
    /// </summary>
    public sealed class ActionQuestEvent : QuestEvent
    {
        /// <summary>
        /// Идентификатор действия «перепрыгнуть препятствие».
        /// </summary>
        public const string ObstacleJumpedOverActionId =
            "obstacle_jumped_over";

        /// <summary>
        /// Идентификатор выполненного действия.
        /// </summary>
        public string ActionId { get; }

        /// <summary>
        /// Сколько раз действие выполнено.
        /// </summary>
        public int Count { get; }

        /// <summary>
        /// Создаёт событие выполненного действия.
        /// </summary>
        public ActionQuestEvent(string actionId, int count)
        {
            if (string.IsNullOrWhiteSpace(actionId))
            {
                throw new ArgumentException(
                    "Идентификатор действия не должен быть пустым.",
                    nameof(actionId));
            }

            if (count <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(count),
                    count,
                    "Количество действий должно быть положительным.");
            }

            ActionId = actionId;
            Count = count;
        }
    }
}
