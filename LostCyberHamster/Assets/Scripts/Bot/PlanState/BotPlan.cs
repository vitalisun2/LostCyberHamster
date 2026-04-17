using System;
using System.Collections.Generic;

namespace Assets.Scripts.Bot.PlanState
{
    /// <summary>
    /// Хранит последовательность действий и служебные данные текущего плана.
    /// </summary>
    public sealed class BotPlan
    {
        /// <summary>
        /// Создает новый план бота.
        /// </summary>
        public BotPlan(IReadOnlyList<PlannedAction> actions, float committedBoundaryX, float score = 0f)
        {
            Actions = actions ?? Array.Empty<PlannedAction>();
            CommittedBoundaryX = committedBoundaryX;
            Score = score;
        }

        public IReadOnlyList<PlannedAction> Actions { get; }
        public float CommittedBoundaryX { get; }
        public float Score { get; }
        public bool HasActions => Actions.Count > 0;

        /// <summary>
        /// Сравнивает планы по последовательности действий без учета служебных полей.
        /// </summary>
        public bool IsEquivalentTo(BotPlan other)
        {
            if (ReferenceEquals(this, other))
                return true;

            if (other == null || Actions.Count != other.Actions.Count)
                return false;

            for (int actionIndex = 0; actionIndex < Actions.Count; actionIndex++)
            {
                if (!Actions[actionIndex].IsEquivalentTo(other.Actions[actionIndex]))
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Создает пустой план без действий.
        /// </summary>
        public static BotPlan Empty(float committedBoundaryX = 0f)
        {
            return new BotPlan(Array.Empty<PlannedAction>(), committedBoundaryX);
        }
    }
}
