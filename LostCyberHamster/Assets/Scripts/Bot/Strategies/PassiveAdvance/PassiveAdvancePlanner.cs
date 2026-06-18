using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.Planning;
using Assets.Scripts.Bot.Planning.DecisionPoints;
using Assets.Scripts.Gameplay.Enums;

namespace Assets.Scripts.Bot.Strategies.PassiveAdvance
{
    /// <summary>
    /// Строит model безопасного no-input продвижения мира до следующей точки анализа.
    /// </summary>
    internal static class PassiveAdvancePlanner
    {
        private const float BoundaryEpsilon = 0.01f;

        /// <summary>
        /// Возвращает model, если можно безопасно пробежать до ухода opposite-lane chain.
        /// </summary>
        public static bool TryBuildModel(
            PlanningState planningState,
            WorldSnapshot worldSnapshot,
            DecisionPoint decisionPoint,
            out PassiveAdvanceModel model)
        {
            // Проверяет базовый contract и режим хомяка.
            model = default;
            if (planningState?.Hamster == null
                || worldSnapshot?.Obstacles == null
                || decisionPoint?.Chain == null
                || !CanAdvancePassively(planningState.Hamster))
            {
                return false;
            }

            // Passive advance нужен только для opposite-lane situation, которую нельзя анализировать прямо сейчас.
            ObstacleChain chain = decisionPoint.Chain;
            if (chain.First.IsBottomLine == planningState.Hamster.IsOnBottomLine)
                return false;

            // Рассчитывает минимальный shift, после которого boundary obstacle перестает быть active.
            ObstacleChainElement boundaryElement = chain.Elements[chain.Count - 1];
            ObstacleSnapshot boundaryObstacle = boundaryElement.Obstacle;
            float completionWorldShift = boundaryObstacle.RightX - planningState.Hamster.HamsterLeftX + BoundaryEpsilon;
            if (completionWorldShift <= BoundaryEpsilon)
                return false;

            // Разрешает ожидание только если текущая линия безопасна на всем отрезке.
            if (!IsCurrentLaneSafeUntil(planningState.Hamster, worldSnapshot, completionWorldShift))
                return false;

            model = new PassiveAdvanceModel(
                boundaryObstacle,
                boundaryElement.WorldIndex,
                completionWorldShift);
            return true;
        }

        private static bool CanAdvancePassively(HamsterSnapshot hamster)
        {
            return hamster != null
                && !hamster.IsShifting
                && !hamster.IsOnRoof
                && hamster.HamsterState == HamsterStateEnum.Run;
        }

        private static bool IsCurrentLaneSafeUntil(
            HamsterSnapshot hamster,
            WorldSnapshot worldSnapshot,
            float completionWorldShift)
        {
            for (int obstacleIndex = 0; obstacleIndex < worldSnapshot.Obstacles.Count; obstacleIndex++)
            {
                ObstacleSnapshot obstacle = worldSnapshot.Obstacles[obstacleIndex];
                if (!CanBlockPassiveAdvance(hamster, obstacle))
                    continue;

                if (IntersectsHamsterPath(hamster, obstacle, completionWorldShift))
                    return false;
            }

            return true;
        }

        private static bool CanBlockPassiveAdvance(
            HamsterSnapshot hamster,
            ObstacleSnapshot obstacle)
        {
            return obstacle != null
                && !obstacle.IsRemovedInPlanning
                && obstacle.IsBottomLine == hamster.IsOnBottomLine
                && ObstacleClassifier.DamagesOnGroundContact(obstacle.ObstacleType);
        }

        private static bool IntersectsHamsterPath(
            HamsterSnapshot hamster,
            ObstacleSnapshot obstacle,
            float completionWorldShift)
        {
            float unsafeStart = obstacle.LeftX - hamster.HamsterRightX;
            float unsafeEnd = obstacle.RightX - hamster.HamsterLeftX;
            return unsafeEnd > 0f && unsafeStart < completionWorldShift;
        }
    }
}
