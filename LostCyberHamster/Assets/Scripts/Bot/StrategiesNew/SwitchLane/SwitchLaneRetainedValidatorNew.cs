using System.Collections.Generic;
using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.Bot.Planning;
using Assets.Scripts.Bot.Planning.RetainedValidation;
using Assets.Scripts.Bot.Strategies.Shared.Timing;
using Assets.Scripts.Bot.Strategies.SwitchLane;
using Assets.Scripts.Gameplay.Enums;

namespace Assets.Scripts.Bot.StrategiesNew.SwitchLane
{
    /// <summary>
    /// Проверяет retained SwitchLane action для role-based planning path.
    /// </summary>
    internal sealed class SwitchLaneRetainedValidatorNew : IRetainedActionValidatorNew
    {
        private const float ValidationEpsilon = 0.0001f;

        private readonly SwitchLaneFireWindowCalculator _fireWindowCalculator;

        /// <summary>
        /// Создает role-based validator сохраненного SwitchLane.
        /// </summary>
        public SwitchLaneRetainedValidatorNew(SwitchLaneFireWindowCalculator fireWindowCalculator)
        {
            _fireWindowCalculator = fireWindowCalculator;
        }

        public BotActionKind ActionKind => BotActionKind.SwitchLane;

        /// <summary>
        /// Проверяет, что retained SwitchLane все еще актуален и безопасен.
        /// </summary>
        public bool IsStillValid(RetainedActionContextNew context)
        {
            // Проверяет базовую совместимость context и action.
            if (context?.PlanningState?.Hamster == null
                || context.ProjectedWorldSnapshot == null
                || context.RetainedObstacle == null
                || context.Action == null
                || context.Action.Kind != ActionKind
                || !context.Action.TargetBottomLine.HasValue
                || context.Action.ResultRoofSupportInstanceId.HasValue)
            {
                return false;
            }

            // Проверяет, что retained action остается дорожным SwitchLane.
            if (context.PlanningState.Hamster.HamsterState != HamsterStateEnum.Run
                || context.PlanningState.Hamster.IsOnRoof)
            {
                return false;
            }

            // Проверяет, что action все еще ведет на другую линию.
            if (context.Action.TargetBottomLine.Value == context.PlanningState.Hamster.IsOnBottomLine)
                return false;

            // Проверяет, что retained obstacle все еще является угрозой для switch-lane action.
            if (!ObstacleClassifier.DamagesOnGroundContact(context.RetainedObstacle.ObstacleType))
                return false;

            // Пересчитывает окно запуска относительно retained obstacle.
            if (!_fireWindowCalculator.TryGetLatestFireShift(
                    context.PlanningState.Hamster,
                    context.RetainedObstacle,
                    out float latestFireShift))
            {
                return false;
            }

            float projectedTriggerX = context.Action.TriggerX - context.PlanningState.ProjectionWorldShift;
            float fireShift = context.RetainedObstacle.LeftX - projectedTriggerX;
            if (fireShift < 0f || fireShift > latestFireShift + ValidationEpsilon)
                return false;

            // Проверяет, что retained fire shift все еще попадает в safe interval.
            List<SafeInterval> safeIntervals = _fireWindowCalculator.CollectSafeFireIntervals(
                context.ProjectedWorldSnapshot,
                context.PlanningState.Hamster,
                context.Action.TargetBottomLine.Value,
                latestFireShift);

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
    }
}
