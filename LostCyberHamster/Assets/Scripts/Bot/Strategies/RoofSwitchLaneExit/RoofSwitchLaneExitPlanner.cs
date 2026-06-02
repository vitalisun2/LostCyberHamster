using System.Collections.Generic;
using Assets.Scripts.Bot.Perception;
using Assets.Scripts.Bot.Planning;
using Assets.Scripts.Bot.Planning.DecisionPoints;
using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.Bot.Strategies.Shared.JumpPlanning;
using Assets.Scripts.Bot.Strategies.Shared.Timing;
using Assets.Scripts.Bot.Strategies.SwitchLane;
using Assets.Scripts.Gameplay.Enums;

namespace Assets.Scripts.Bot.Strategies.RoofSwitchLaneExit
{
    /// <summary>
    /// Строит planning-модель схода с крыши через смену линии.
    /// </summary>
    internal sealed class RoofSwitchLaneExitPlanner
    {
        private const float ValidationEpsilon = 0.0001f;

        private readonly SwitchLaneFireWindowCalculator _fireWindowCalculator;

        public RoofSwitchLaneExitPlanner(SwitchLaneFireWindowCalculator fireWindowCalculator)
        {
            _fireWindowCalculator = fireWindowCalculator;
        }

        /// <summary>
        /// Собирает кандидаты roof switch-lane exit для текущей required decision.
        /// </summary>
        public IReadOnlyList<RoofSwitchLaneExitModel> CollectModels(
            PlanningState planningState,
            WorldSnapshot worldSnapshot,
            DecisionPoint decisionPoint,
            float runFromRoofTravel,
            IReadOnlyList<float> selectionRatios)
        {
            var models = new List<RoofSwitchLaneExitModel>();
            if (!TryResolveContext(
                    planningState,
                    worldSnapshot,
                    decisionPoint,
                    runFromRoofTravel,
                    out HamsterSnapshot hamster,
                    out ObstacleSnapshot contextObstacle,
                    out int contextObstacleIndex,
                    out bool targetBottomLine,
                    out float latestFireShift))
            {
                return models;
            }

            IReadOnlyList<SwitchLaneFireWindowSample> fireWindowSamples =
                _fireWindowCalculator.CollectFireWindowSamples(
                    worldSnapshot,
                    hamster,
                    targetBottomLine,
                    latestFireShift,
                    selectionRatios,
                    requireTargetRoofSupport: false);

            for (int sampleIndex = 0; sampleIndex < fireWindowSamples.Count; sampleIndex++)
            {
                SwitchLaneFireWindowSample sample = fireWindowSamples[sampleIndex];
                if (!IsFireShiftSafe(
                        worldSnapshot,
                        hamster,
                        targetBottomLine,
                        sample.FireShift,
                        runFromRoofTravel))
                {
                    continue;
                }

                models.Add(new RoofSwitchLaneExitModel(
                    contextObstacle,
                    contextObstacleIndex,
                    targetBottomLine,
                    sample,
                    runFromRoofTravel));
            }

            return models;
        }

        /// <summary>
        /// Проверяет сохраненный action без привязки к sampling-ratio.
        /// </summary>
        public bool IsActionStillValid(
            PlanningState planningState,
            WorldSnapshot worldSnapshot,
            DecisionPoint decisionPoint,
            PlannedAction action,
            float runFromRoofTravel)
        {
            if (action == null
                || action.Kind != BotActionKind.RoofSwitchLaneExit
                || !action.TargetBottomLine.HasValue
                || !action.TargetObstacleInstanceId.HasValue)
            {
                return false;
            }

            if (!TryResolveContext(
                    planningState,
                    worldSnapshot,
                    decisionPoint,
                    runFromRoofTravel,
                    out HamsterSnapshot hamster,
                    out ObstacleSnapshot contextObstacle,
                    out _,
                    out bool targetBottomLine,
                    out float latestFireShift))
            {
                return false;
            }

            if (contextObstacle.InstanceId != action.TargetObstacleInstanceId.Value)
                return false;

            if (targetBottomLine != action.TargetBottomLine.Value)
                return false;

            float projectedTriggerX = action.TriggerX - planningState.ProjectionWorldShift;
            float fireShift = contextObstacle.LeftX - projectedTriggerX;
            if (fireShift < 0f || fireShift > latestFireShift + ValidationEpsilon)
                return false;

            if (!IsFireShiftSafe(worldSnapshot, hamster, targetBottomLine, fireShift, runFromRoofTravel))
                return false;

            List<SafeInterval> safeIntervals = _fireWindowCalculator.CollectSafeFireIntervals(
                worldSnapshot,
                hamster,
                targetBottomLine,
                latestFireShift,
                requireTargetRoofSupport: false);

            for (int intervalIndex = 0; intervalIndex < safeIntervals.Count; intervalIndex++)
            {
                SafeInterval interval = safeIntervals[intervalIndex];
                if (fireShift >= interval.Start - ValidationEpsilon
                    && fireShift <= interval.End + ValidationEpsilon)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Находит контекст планирования для схода с крыши через смену линии.
        /// </summary>
        private bool TryResolveContext(
            PlanningState planningState,
            WorldSnapshot worldSnapshot,
            DecisionPoint decisionPoint,
            float runFromRoofTravel,
            out HamsterSnapshot hamster,
            out ObstacleSnapshot contextObstacle,
            out int contextObstacleIndex,
            out bool targetBottomLine,
            out float latestFireShift)
        {
            hamster = planningState?.Hamster;
            contextObstacle = null;
            contextObstacleIndex = -1;
            targetBottomLine = false;
            latestFireShift = 0f;

            if (planningState == null
                || worldSnapshot == null
                || decisionPoint == null
                || decisionPoint.Chain == null
                || !decisionPoint.IsDecisionRequired
                || runFromRoofTravel <= 0f)
            {
                return false;
            }

            if (!CanSwitchLaneExitFromRoof(hamster))
                return false;

            contextObstacle = decisionPoint.Chain.FirstObstacle;
            contextObstacleIndex = decisionPoint.Chain.FirstIndex;
            if (contextObstacle == null
                || !ObstacleClassifier.DamagesOnGroundContact(contextObstacle.ObstacleType))
            {
                return false;
            }

            targetBottomLine = !hamster.IsOnBottomLine;
            if (!_fireWindowCalculator.TryGetLatestFireShift(hamster, contextObstacle, out latestFireShift))
                return false;

            if (decisionPoint.HasFireBeforeObstacle
                && !TryClampLatestFireShiftBeforeDeadline(hamster, decisionPoint.FireBeforeObstacle, ref latestFireShift))
            {
                return false;
            }

            return latestFireShift > 0f;
        }

        /// <summary>
        /// Проверяет, что нажатие переводит хомяка в безопасный RunFromRoof на целевой линии.
        /// </summary>
        private bool IsFireShiftSafe(
            WorldSnapshot worldSnapshot,
            HamsterSnapshot hamster,
            bool targetBottomLine,
            float fireShift,
            float runFromRoofTravel)
        {
            if (_fireWindowCalculator.TryFindTargetRoofSupportAtFireShift(
                    worldSnapshot,
                    hamster,
                    targetBottomLine,
                    fireShift,
                    out _))
            {
                return false;
            }

            return RoofExitSafety.IsSafeDuringRunFromRoof(
                hamster,
                worldSnapshot,
                targetBottomLine,
                fireShift,
                fireShift + runFromRoofTravel);
        }

        /// <summary>
        /// Возвращает true, если хомяк может начать сход с крыши через смену линии.
        /// </summary>
        private static bool CanSwitchLaneExitFromRoof(HamsterSnapshot hamster)
        {
            return hamster != null
                && hamster.HamsterState == HamsterStateEnum.RoofRun
                && hamster.IsOnRoof
                && !hamster.IsShifting
                && hamster.RoofSupportInstanceId.HasValue;
        }

        /// <summary>
        /// Ограничивает последний момент запуска дедлайном до ближайшей обязательной угрозы.
        /// </summary>
        private bool TryClampLatestFireShiftBeforeDeadline(
            HamsterSnapshot hamster,
            ObstacleSnapshot deadlineObstacle,
            ref float latestFireShift)
        {
            if (deadlineObstacle == null)
                return latestFireShift > 0f;

            if (!_fireWindowCalculator.TryGetLatestFireShift(hamster, deadlineObstacle, out float deadlineLatestFireShift))
                return false;

            if (deadlineLatestFireShift < latestFireShift)
                latestFireShift = deadlineLatestFireShift;

            return latestFireShift > 0f;
        }
    }
}
