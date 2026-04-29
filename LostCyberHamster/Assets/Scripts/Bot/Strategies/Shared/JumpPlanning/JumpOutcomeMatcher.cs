using System.Collections.Generic;
using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.Planning;
using Assets.Scripts.GameEngine.Mechanics.Models;
using Assets.Scripts.Gameplay.Enums;

namespace Assets.Scripts.Bot.Strategies.Shared.JumpPlanning
{
    /// <summary>
    /// Сравнивает runtime jump resolve result с ожидаемым planning outcome.
    /// </summary>
    internal sealed class JumpOutcomeMatcher
    {
        private readonly HamsterStateEnum _expectedState;
        private readonly bool _damageBigAliveWithoutYByReach;
        private readonly JumpResolveDelegate _resolver;

        public JumpOutcomeMatcher(
            HamsterStateEnum expectedState,
            bool damageBigAliveWithoutYByReach,
            JumpResolveDelegate resolver)
        {
            _expectedState = expectedState;
            _damageBigAliveWithoutYByReach = damageBigAliveWithoutYByReach;
            _resolver = resolver;
        }

        public bool IsExactOutcomeAtShift(
            HamsterSnapshot hamster,
            IReadOnlyList<JumpObstacleData> baseObstacles,
            List<JumpObstacleData> shiftedObstacles,
            float fireShift,
            float actionTravel,
            int targetObstacleIndex)
        {
            JumpResolveResult result = ResolveAtShift(
                hamster,
                baseObstacles,
                shiftedObstacles,
                fireShift,
                actionTravel);

            return result.State == _expectedState
                   && IsTargetMatch(shiftedObstacles, targetObstacleIndex, result.TargetIndex);
        }

        public JumpResolveResult ResolveAtShift(
            HamsterSnapshot hamster,
            IReadOnlyList<JumpObstacleData> baseObstacles,
            List<JumpObstacleData> shiftedObstacles,
            float fireShift,
            float actionTravel)
        {
            JumpObstacleProjection.BuildShifted(baseObstacles, fireShift, shiftedObstacles);

            JumpResolveContext context = new(
                hamster.IsOnBottomLine,
                hamster.HamsterLeftX,
                hamster.HamsterRightX,
                hamster.CenterX,
                hamster.Width,
                actionTravel,
                actionTravel,
                damageBigAliveWithoutYByReach: _damageBigAliveWithoutYByReach);

            return _resolver(shiftedObstacles, context);
        }

        public static bool IsTargetMatch(
            IReadOnlyList<JumpObstacleData> shiftedObstacles,
            int targetObstacleIndex,
            int resolvedTargetIndex)
        {
            return resolvedTargetIndex == targetObstacleIndex
                   || IsRoadSmallChainOverResult(shiftedObstacles, targetObstacleIndex, resolvedTargetIndex);
        }

        private static bool IsRoadSmallChainOverResult(
            IReadOnlyList<JumpObstacleData> shiftedObstacles,
            int targetObstacleIndex,
            int resolvedTargetIndex)
        {
            if (shiftedObstacles == null)
                return false;

            if (targetObstacleIndex < 0
                || resolvedTargetIndex < targetObstacleIndex
                || resolvedTargetIndex >= shiftedObstacles.Count)
            {
                return false;
            }

            JumpObstacleData targetObstacle = shiftedObstacles[targetObstacleIndex];
            if (!ObstacleClassifier.IsRoadSmallOverChainObstacle(targetObstacle.Type))
                return false;

            bool isBottomLine = targetObstacle.IsBottomLine;
            for (int obstacleIndex = targetObstacleIndex; obstacleIndex <= resolvedTargetIndex; obstacleIndex++)
            {
                JumpObstacleData obstacle = shiftedObstacles[obstacleIndex];
                if (obstacle.IsBottomLine != isBottomLine)
                    continue;

                if (!ObstacleClassifier.DamagesOnGroundContact(obstacle.Type))
                    continue;

                if (!ObstacleClassifier.IsRoadSmallOverChainObstacle(obstacle.Type))
                    return false;
            }

            return true;
        }
    }
}