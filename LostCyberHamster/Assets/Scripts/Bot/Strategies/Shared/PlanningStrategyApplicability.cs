using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.Planning;
using Assets.Scripts.Bot.Planning.DecisionPoints;
using Assets.Scripts.Gameplay.Enums;

namespace Assets.Scripts.Bot.Strategies.Shared
{
    /// <summary>
    /// Содержит дешевые проверки применимости planning-strategies без resolver/window поиска.
    /// </summary>
    internal static class PlanningStrategyApplicability
    {
        /// <summary>
        /// Проверяет общий contract planning-ситуации.
        /// </summary>
        public static bool HasContext(
            PlanningState planningState,
            DecisionPoint decisionPoint)
        {
            return planningState?.Hamster != null
                && decisionPoint?.Chain != null
                && decisionPoint.Chain.Count > 0;
        }

        /// <summary>
        /// Возвращает true, если chain относится к текущей линии хомяка.
        /// </summary>
        public static bool IsCurrentLane(
            PlanningState planningState,
            DecisionPoint decisionPoint)
        {
            return HasContext(planningState, decisionPoint)
                && decisionPoint.Chain.First.IsBottomLine == planningState.Hamster.IsOnBottomLine;
        }

        /// <summary>
        /// Возвращает true, если chain относится к противоположной линии.
        /// </summary>
        public static bool IsOppositeLane(
            PlanningState planningState,
            DecisionPoint decisionPoint)
        {
            return HasContext(planningState, decisionPoint)
                && decisionPoint.Chain.First.IsBottomLine != planningState.Hamster.IsOnBottomLine;
        }

        /// <summary>
        /// Возвращает true, если decision point описывает указанную границу движения.
        /// </summary>
        public static bool IsMovingBoundary(
            PlanningState planningState,
            DecisionPoint decisionPoint,
            MovingBoundaryKind movingBoundaryKind)
        {
            return planningState?.Hamster != null
                && decisionPoint != null
                && decisionPoint.Kind == DecisionPointKind.MovingBoundary
                && decisionPoint.MovingBoundaryKind == movingBoundaryKind;
        }

        /// <summary>
        /// Проверяет дорожное состояние, допускающее ground actions.
        /// </summary>
        public static bool CanPlanGroundRun(HamsterSnapshot hamster)
        {
            return hamster != null
                && hamster.HamsterState == HamsterStateEnum.Run
                && !hamster.IsOnRoof
                && !hamster.IsShifting;
        }

        /// <summary>
        /// Проверяет состояние движения по крыше, допускающее roof actions.
        /// </summary>
        public static bool CanPlanRoofRun(HamsterSnapshot hamster)
        {
            return hamster != null
                && hamster.HamsterState == HamsterStateEnum.RoofRun
                && hamster.IsOnRoof
                && hamster.RoofSupportInstanceId.HasValue
                && !hamster.IsShifting;
        }

        /// <summary>
        /// Проверяет состояние, в котором возможен no-input pickup.
        /// </summary>
        public static bool CanPlanPassiveCollect(HamsterSnapshot hamster)
        {
            return hamster != null
                && !hamster.IsShifting
                && (hamster.HamsterState == HamsterStateEnum.Run
                    || hamster.HamsterState == HamsterStateEnum.RoofRun
                    || hamster.HamsterState == HamsterStateEnum.RunFromRoof);
        }

        /// <summary>
        /// Проверяет ground-run ситуацию на текущей линии.
        /// </summary>
        public static bool IsGroundRunCurrentLane(
            PlanningState planningState,
            DecisionPoint decisionPoint)
        {
            return HasContext(planningState, decisionPoint)
                && CanPlanGroundRun(planningState.Hamster)
                && IsCurrentLane(planningState, decisionPoint);
        }

        /// <summary>
        /// Проверяет roof-run ситуацию на текущей линии.
        /// </summary>
        public static bool IsRoofRunCurrentLane(
            PlanningState planningState,
            DecisionPoint decisionPoint)
        {
            return HasContext(planningState, decisionPoint)
                && CanPlanRoofRun(planningState.Hamster)
                && IsCurrentLane(planningState, decisionPoint);
        }

        /// <summary>
        /// Проверяет наличие роли в текущей chain.
        /// </summary>
        public static bool HasRole(
            DecisionPoint decisionPoint,
            ObstacleRole role)
        {
            return decisionPoint?.Chain != null
                && decisionPoint.Chain.TryFindFirstWithRole(role, out _, out _);
        }

        /// <summary>
        /// Проверяет роль первого obstacle текущей chain.
        /// </summary>
        public static bool FirstHasRole(
            DecisionPoint decisionPoint,
            ObstacleRole role)
        {
            return decisionPoint?.Chain?.First != null
                && decisionPoint.Chain.First.HasRole(role);
        }
    }
}
