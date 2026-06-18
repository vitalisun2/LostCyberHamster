using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.PlanState;

namespace Assets.Scripts.Bot.Strategies.PassiveCollect
{
    /// <summary>
    /// Хранит planning-модель passive pickup collectable.
    /// </summary>
    internal readonly struct PassiveCollectModel
    {
        /// <summary>
        /// Создает модель passive collect action.
        /// </summary>
        public PassiveCollectModel(
            ObstacleSnapshot targetCollectible,
            int targetCollectibleIndex,
            float completionWorldShift,
            CollectibleObjectiveValue objectiveValue)
        {
            TargetCollectible = targetCollectible;
            TargetCollectibleIndex = targetCollectibleIndex;
            CompletionWorldShift = completionWorldShift;
            ObjectiveValue = objectiveValue;
        }

        public ObstacleSnapshot TargetCollectible { get; }
        public int TargetCollectibleIndex { get; }
        public float CompletionWorldShift { get; }
        public CollectibleObjectiveValue ObjectiveValue { get; }
    }
}
