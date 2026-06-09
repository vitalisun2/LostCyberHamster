using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.Planning;
using Assets.Scripts.Bot.Planning.DecisionPoints;

namespace Assets.Scripts.Bot.Strategies.Shared.JumpFromRoof
{
    /// <summary>
    /// Выбирает road threat, из-за которого нужен активный прыжок с крыши.
    /// </summary>
    internal sealed class JumpFromRoofActionResolver
    {
        /// <summary>
        /// Возвращает первый blocking threat и последнюю passive roof, если автоматический сход опасен.
        /// </summary>
        public bool TryResolve(
            PlanningState planningState,
            WorldSnapshot projectedWorldSnapshot,
            ObstacleChain chain,
            JumpFromRoofTravel travel,
            out ObstacleSnapshot blockingThreat,
            out int blockingThreatIndex,
            out ObstacleSnapshot lastRoof)
        {
            // Инициализирует результат и проверяет вход.
            blockingThreat = null;
            blockingThreatIndex = -1;
            lastRoof = null;
            if (planningState?.Hamster == null
                || projectedWorldSnapshot == null
                || chain == null
                || chain.Count <= 0
                || travel.RunFromRoofTravel <= 0f)
            {
                return false;
            }

            // Выбирает первый road blocking threat текущей role-based ситуации.
            ObstacleChainElement firstElement = chain.First;
            if (!IsRoadBlockingThreat(firstElement))
                return false;

            // Находит последнюю крышу, с которой хомяк пассивно сойдет на дорогу.
            if (!RoofRunProjection.TryFindLastPassiveRoof(
                    planningState,
                    projectedWorldSnapshot,
                    out lastRoof,
                    out _))
            {
                return false;
            }

            // Проверяет, что passive exit приведет к контакту с threat.
            blockingThreat = firstElement.Obstacle;
            float gap = blockingThreat.LeftX - lastRoof.RightX;
            if (gap >= travel.RunFromRoofTravel)
                return false;

            blockingThreatIndex = firstElement.WorldIndex;
            return true;
        }

        /// <summary>
        /// Проверяет, что element является дорожной угрозой, а не крышей или roof occupant hazard.
        /// </summary>
        private static bool IsRoadBlockingThreat(ObstacleChainElement element)
        {
            if (element == null)
                return false;

            if (!element.HasRole(ObstacleRole.BlockingThreat))
                return false;

            if (element.HasRole(ObstacleRole.RoofSupport)
                || element.HasRole(ObstacleRole.RoofOccupantHazard))
            {
                return false;
            }

            return ObstacleClassifier.DamagesOnGroundContact(element.Obstacle.ObstacleType);
        }
    }
}
