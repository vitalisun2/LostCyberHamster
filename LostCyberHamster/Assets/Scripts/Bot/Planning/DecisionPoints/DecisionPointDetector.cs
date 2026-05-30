using System;
using System.Collections.Generic;
using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.Planning;
using Assets.Scripts.GameEngine.Mechanics;

namespace Assets.Scripts.Bot.Planning.DecisionPoints
{
    /// <summary>
    /// Координирует chain builders и возвращает готовые planning-ситуации.
    /// </summary>
    public sealed class DecisionPointDetector
    {
        /// <summary>
        /// Builders обязательных ситуаций в порядке приоритета.
        /// </summary>
        private readonly IReadOnlyList<IDecisionPointChainBuilder> _requiredBuilders =
            new IDecisionPointChainBuilder[]
            {
                new RoofOccupantHazardChainBuilder(),
                new JumpOnFromRoofTargetChainBuilder(),
                new CurrentLaneGroundJumpOnTargetChainBuilder(),
                new BlockingThreatChainBuilder()
            };

        /// <summary>
        /// Builders optional jump-on objectives в порядке приоритета.
        /// </summary>
        private readonly IReadOnlyList<IDecisionPointChainBuilder> _optionalBuilders =
            new IDecisionPointChainBuilder[]
            {
                new OtherLaneGroundJumpOnTargetChainBuilder(),
                new RoofJumpOnTargetChainBuilder()
            };

        /// <summary>
        /// Пытается найти ближайшую обязательную planning-ситуацию.
        /// </summary>
        public bool TryDetectRequiredDecisionPoint(
            PlanningState planningState,
            WorldSnapshot worldSnapshot,
            out DecisionPoint decisionPoint)
        {
            // Подготавливает результат и проверяет вход.
            decisionPoint = null;
            if (planningState == null || worldSnapshot == null)
                return false;

            // Запускает required builders с target horizon в пределах vision.
            DecisionPointBuildContext context = CreateBuildContext(
                planningState,
                worldSnapshot,
                maxFirstObstacleLeftX: worldSnapshot.ScreenRightEdgeX,
                maxTargetLeftX: worldSnapshot.VisionRightEdgeX);
            return TryBuildFirst(_requiredBuilders, context, out decisionPoint);
        }

        /// <summary>
        /// Возвращает optional jump-on objective decision points.
        /// </summary>
        public IReadOnlyList<DecisionPoint> DetectOptionalDecisionPoints(
            PlanningState planningState,
            WorldSnapshot worldSnapshot)
        {
            // Подготавливает результат и проверяет вход.
            var decisionPoints = new List<DecisionPoint>();
            if (planningState == null || worldSnapshot == null)
                return decisionPoints;

            // Optional objectives ищутся только когда planner охотится за jump-on target.
            if (!CanSearchJumpOnObjective(planningState))
                return decisionPoints;

            // Ground optional target ограничен экраном, roof optional target может смотреть до vision horizon.
            TryAddOptionalDecisionPoint(
                _optionalBuilders[0],
                planningState,
                worldSnapshot,
                maxFirstObstacleLeftX: worldSnapshot.ScreenRightEdgeX,
                maxTargetLeftX: worldSnapshot.ScreenRightEdgeX,
                decisionPoints);
            TryAddOptionalDecisionPoint(
                _optionalBuilders[1],
                planningState,
                worldSnapshot,
                maxFirstObstacleLeftX: worldSnapshot.ScreenRightEdgeX,
                maxTargetLeftX: worldSnapshot.VisionRightEdgeX,
                decisionPoints);

            return decisionPoints;
        }

        /// <summary>
        /// Пытается найти decision point, chain которого содержит уже выбранный retained target.
        /// </summary>
        public bool TryDetectDecisionPointForRetainedTarget(
            PlanningState planningState,
            WorldSnapshot worldSnapshot,
            ObstacleSnapshot retainedTarget,
            out DecisionPoint decisionPoint)
        {
            // Подготавливает результат и проверяет вход.
            decisionPoint = null;
            if (planningState == null || worldSnapshot == null || retainedTarget == null)
                return false;

            // Ищет target внутри required situations с расширенным horizon.
            float retainedTargetHorizon = Math.Max(worldSnapshot.VisionRightEdgeX, retainedTarget.LeftX);
            DecisionPointBuildContext requiredContext = CreateBuildContext(
                planningState,
                worldSnapshot,
                maxFirstObstacleLeftX: worldSnapshot.ScreenRightEdgeX,
                maxTargetLeftX: retainedTargetHorizon);
            if (TryBuildFirstContainingTarget(
                    _requiredBuilders,
                    requiredContext,
                    retainedTarget,
                    out decisionPoint))
            {
                return true;
            }

            // Ищет target внутри optional objectives с расширенным horizon.
            if (!CanSearchJumpOnObjective(planningState))
                return false;

            float optionalGroundTargetHorizon = Math.Max(worldSnapshot.ScreenRightEdgeX, retainedTarget.LeftX);
            DecisionPointBuildContext optionalGroundContext = CreateBuildContext(
                planningState,
                worldSnapshot,
                maxFirstObstacleLeftX: worldSnapshot.ScreenRightEdgeX,
                maxTargetLeftX: optionalGroundTargetHorizon);
            if (TryBuildContainingTarget(
                    _optionalBuilders[0],
                    optionalGroundContext,
                    retainedTarget,
                    out decisionPoint))
            {
                return true;
            }

            DecisionPointBuildContext optionalRoofContext = CreateBuildContext(
                planningState,
                worldSnapshot,
                maxFirstObstacleLeftX: worldSnapshot.ScreenRightEdgeX,
                maxTargetLeftX: retainedTargetHorizon);
            return TryBuildContainingTarget(
                _optionalBuilders[1],
                optionalRoofContext,
                retainedTarget,
                out decisionPoint);
        }

        /// <summary>
        /// Создает общий build context для builders.
        /// </summary>
        private static DecisionPointBuildContext CreateBuildContext(
            PlanningState planningState,
            WorldSnapshot worldSnapshot,
            float maxFirstObstacleLeftX,
            float maxTargetLeftX)
        {
            return new DecisionPointBuildContext(
                planningState,
                worldSnapshot,
                GetFirstDetectionIndex(planningState, worldSnapshot),
                maxFirstObstacleLeftX,
                maxTargetLeftX);
        }

        /// <summary>
        /// Пытается построить первый decision point из списка builders.
        /// </summary>
        private static bool TryBuildFirst(
            IReadOnlyList<IDecisionPointChainBuilder> builders,
            DecisionPointBuildContext context,
            out DecisionPoint decisionPoint)
        {
            // Запускает builders в заданном порядке.
            decisionPoint = null;
            for (int builderIndex = 0; builderIndex < builders.Count; builderIndex++)
            {
                if (builders[builderIndex].TryBuild(context, out decisionPoint))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Пытается построить первый decision point, chain которого содержит target.
        /// </summary>
        private static bool TryBuildFirstContainingTarget(
            IReadOnlyList<IDecisionPointChainBuilder> builders,
            DecisionPointBuildContext context,
            ObstacleSnapshot target,
            out DecisionPoint decisionPoint)
        {
            // Запускает builders в заданном порядке до chain с target.
            decisionPoint = null;
            for (int builderIndex = 0; builderIndex < builders.Count; builderIndex++)
            {
                if (TryBuildContainingTarget(
                        builders[builderIndex],
                        context,
                        target,
                        out decisionPoint))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Пытается построить decision point, chain которого содержит target.
        /// </summary>
        private static bool TryBuildContainingTarget(
            IDecisionPointChainBuilder builder,
            DecisionPointBuildContext context,
            ObstacleSnapshot target,
            out DecisionPoint decisionPoint)
        {
            // Проверяет результат builder'а на принадлежность target chain.
            decisionPoint = null;
            if (!builder.TryBuild(context, out DecisionPoint candidate))
                return false;

            if (!candidate.Chain.ContainsObstacle(target))
                return false;

            decisionPoint = candidate;
            return true;
        }

        /// <summary>
        /// Добавляет optional decision point, если builder смог его построить.
        /// </summary>
        private static void TryAddOptionalDecisionPoint(
            IDecisionPointChainBuilder builder,
            PlanningState planningState,
            WorldSnapshot worldSnapshot,
            float maxFirstObstacleLeftX,
            float maxTargetLeftX,
            List<DecisionPoint> decisionPoints)
        {
            DecisionPointBuildContext context = CreateBuildContext(
                planningState,
                worldSnapshot,
                maxFirstObstacleLeftX,
                maxTargetLeftX);
            if (builder.TryBuild(context, out DecisionPoint decisionPoint))
                decisionPoints.Add(decisionPoint);
        }

        /// <summary>
        /// Возвращает index obstacle, с которого detector должен начать поиск decision point.
        /// </summary>
        private static int GetFirstDetectionIndex(
            PlanningState planningState,
            WorldSnapshot worldSnapshot)
        {
            // Готовит default start.
            int defaultDetectionIndex = planningState.NextObstacleIndex;
            HamsterSnapshot hamster = planningState.Hamster;

            // Разделяет ground и roof-сценарии.
            if (hamster == null || !hamster.IsOnRoof)
                return defaultDetectionIndex;

            // Пробует пропустить passive roof chain.
            if (RoofRunProjection.TryFindLastPassiveRoof(
                    planningState,
                    worldSnapshot,
                    out ObstacleSnapshot lastRoof,
                    out int lastRoofIndex))
            {
                int firstIndexAfterPassiveRoofs = lastRoofIndex + 1;
                if (firstIndexAfterPassiveRoofs > defaultDetectionIndex)
                {
                    DebugManager.DiagLogVerbose(
                        $"[Bot PLAN] SKIP_PASSIVE_ROOF_CHAIN lastRoof={lastRoof.ObstacleType} " +
                        $"index={lastRoofIndex} instanceId={lastRoof.InstanceId} " +
                        $"leftX={lastRoof.LeftX:F2} rightX={lastRoof.RightX:F2}");

                    return firstIndexAfterPassiveRoofs;
                }
            }

            // Возвращает default fallback.
            return defaultDetectionIndex;
        }

        /// <summary>
        /// Проверяет, можно ли искать optional jump-on objective.
        /// </summary>
        private static bool CanSearchJumpOnObjective(PlanningState planningState)
        {
            HamsterSnapshot hamster = planningState.Hamster;
            return hamster != null
                && !hamster.IsOnRoof
                && !hamster.IsShifting
                && JumpOnObjectiveRules.HasEnergyForJumpOnObjective(hamster);
        }
    }
}
