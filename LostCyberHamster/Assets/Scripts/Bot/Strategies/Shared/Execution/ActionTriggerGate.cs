using Assets.Scripts.Bot.Strategies.Shared.Models;
using Assets.Scripts.Bot.PlanState;
using Assets.Scripts;
using Assets.Scripts.Gameplay;
using UnityEngine;

namespace Assets.Scripts.Bot.Strategies.Shared.Execution
{
    /// <summary>
    /// Проверяет, дошёл ли live obstacle до рассчитанной точки запуска action.
    /// </summary>
    internal sealed class ActionTriggerGate
    {
        private const float WindowBoundaryEpsilon = 0.001f;

        private readonly LiveObstacleResolver _liveObstacleResolver;
        private TriggerObservation _lastObservation;
        private bool _hasLastObservation;

        public ActionTriggerGate(LiveObstacleResolver liveObstacleResolver)
        {
            _liveObstacleResolver = liveObstacleResolver;
        }

        /// <summary>
        /// Проверяет, можно ли сработать по текущему положению целевого препятствия.
        /// </summary>
        public ActionFireResult Check(PlannedAction action, out float obstacleLeftX)
        {
            return Check(action, out obstacleLeftX, out _);
        }

        /// <summary>
        /// Проверяет trigger gate и возвращает diagnostic reason для non-fired результата.
        /// </summary>
        public ActionFireResult Check(
            PlannedAction action,
            out float obstacleLeftX,
            out string diagnosticReason)
        {
            // Сбрасывает выходную координату и определяет obstacle-триггер.
            obstacleLeftX = 0f;
            diagnosticReason = null;
            int? triggerObstacleInstanceId = action?.TriggerObstacleInstanceId ?? action?.TargetObstacleInstanceId;

            // Отменяет проверку без доступного obstacle-триггера.
            if (!triggerObstacleInstanceId.HasValue)
            {
                diagnosticReason = "missing-trigger-obstacle-id";
                return ActionFireResult.Cancelled;
            }

            // Находит live obstacle по сохранённому идентификатору.
            Obstacle obstacle = _liveObstacleResolver.Find(triggerObstacleInstanceId.Value);
            if (obstacle == null)
            {
                diagnosticReason = $"trigger-obstacle-not-found instanceId={triggerObstacleInstanceId.Value}";
                return ActionFireResult.Cancelled;
            }

            // Получает collider препятствия для расчёта левой границы.
            BoxCollider2D collider = obstacle.GetComponentInChildren<BoxCollider2D>();
            if (collider == null)
            {
                diagnosticReason = $"trigger-obstacle-has-no-collider instanceId={triggerObstacleInstanceId.Value}";
                return ActionFireResult.Cancelled;
            }

            // Сравнивает текущую позицию препятствия с рассчитанным trigger contract.
            obstacleLeftX = collider.bounds.min.x;
            float previousObstacleLeftX = 0f;
            bool hasPreviousObstacleLeftX = TryGetPreviousObstacleLeftX(
                action,
                triggerObstacleInstanceId.Value,
                out previousObstacleLeftX);
            StoreObservation(action, triggerObstacleInstanceId.Value, obstacleLeftX);

            if (action.TriggerWindow.HasValue && action.TriggerWindow.Value.IsValid)
            {
                return CheckWindow(
                    action,
                    action.TriggerWindow.Value,
                    obstacleLeftX,
                    hasPreviousObstacleLeftX,
                    previousObstacleLeftX,
                    out diagnosticReason);
            }

            return CheckPoint(action.TriggerX, obstacleLeftX, out diagnosticReason);
        }

        private static ActionFireResult CheckWindow(
            PlannedAction action,
            ActionTriggerWindow window,
            float obstacleLeftX,
            bool hasPreviousObstacleLeftX,
            float previousObstacleLeftX,
            out string diagnosticReason)
        {
            diagnosticReason = null;

            if (hasPreviousObstacleLeftX && DidCrossSelectedTrigger(window, action.TriggerX, previousObstacleLeftX, obstacleLeftX))
                return ActionFireResult.Fired;

            if (hasPreviousObstacleLeftX && window.WasCrossed(previousObstacleLeftX, obstacleLeftX))
                return ActionFireResult.Fired;

            if (IsAfterWindowClose(window, obstacleLeftX))
            {
                diagnosticReason =
                    $"after-window-close obstacleLeftX={obstacleLeftX:F2} latest={window.LatestTriggerX:F2} " +
                    $"prev={(hasPreviousObstacleLeftX ? previousObstacleLeftX.ToString("F2") : "none")}";
                return ActionFireResult.Cancelled;
            }

            if (IsBeforeWindowOpen(window, obstacleLeftX))
            {
                diagnosticReason =
                    $"before-window-open obstacleLeftX={obstacleLeftX:F2} earliest={window.EarliestTriggerX:F2}";
                return ActionFireResult.Waiting;
            }

            if (obstacleLeftX <= action.TriggerX + WindowBoundaryEpsilon)
                return ActionFireResult.Fired;

            if (IsNarrowForCurrentRuntimeStep(window, hasPreviousObstacleLeftX, previousObstacleLeftX, obstacleLeftX))
                return ActionFireResult.Fired;

            diagnosticReason =
                $"inside-window-waiting obstacleLeftX={obstacleLeftX:F2} triggerX={action.TriggerX:F2} " +
                $"window=[{window.EarliestTriggerX:F2},{window.LatestTriggerX:F2}]";
            return ActionFireResult.Waiting;
        }

        private static bool DidCrossSelectedTrigger(
            ActionTriggerWindow window,
            float triggerX,
            float previousObstacleLeftX,
            float obstacleLeftX)
        {
            return triggerX <= window.EarliestTriggerX + WindowBoundaryEpsilon
                && triggerX >= window.LatestTriggerX - WindowBoundaryEpsilon
                && previousObstacleLeftX > triggerX + WindowBoundaryEpsilon
                && obstacleLeftX <= triggerX + WindowBoundaryEpsilon;
        }

        private static bool IsAfterWindowClose(ActionTriggerWindow window, float obstacleLeftX)
        {
            return obstacleLeftX < window.LatestTriggerX - WindowBoundaryEpsilon;
        }

        private static bool IsBeforeWindowOpen(ActionTriggerWindow window, float obstacleLeftX)
        {
            return obstacleLeftX > window.EarliestTriggerX + WindowBoundaryEpsilon;
        }

        private static ActionFireResult CheckPoint(float triggerX, float obstacleLeftX, out string diagnosticReason)
        {
            if (obstacleLeftX > triggerX)
            {
                diagnosticReason = $"point-before-trigger obstacleLeftX={obstacleLeftX:F2} triggerX={triggerX:F2}";
                return ActionFireResult.Waiting;
            }

            diagnosticReason = null;
            return ActionFireResult.Fired;
        }

        private static bool IsNarrowForCurrentRuntimeStep(
            ActionTriggerWindow window,
            bool hasPreviousObstacleLeftX,
            float previousObstacleLeftX,
            float obstacleLeftX)
        {
            float runtimeStep = hasPreviousObstacleLeftX
                ? Mathf.Max(0f, previousObstacleLeftX - obstacleLeftX)
                : EstimateCurrentFrameTravel();

            return window.Width <= runtimeStep + 0.001f;
        }

        private static float EstimateCurrentFrameTravel()
        {
            return Consts.GameSpeedBase * Time.deltaTime;
        }

        private bool TryGetPreviousObstacleLeftX(
            PlannedAction action,
            int triggerObstacleInstanceId,
            out float previousObstacleLeftX)
        {
            previousObstacleLeftX = 0f;
            if (!_hasLastObservation || !_lastObservation.Matches(action, triggerObstacleInstanceId))
                return false;

            previousObstacleLeftX = _lastObservation.ObstacleLeftX;
            return true;
        }

        private void StoreObservation(
            PlannedAction action,
            int triggerObstacleInstanceId,
            float obstacleLeftX)
        {
            _lastObservation = new TriggerObservation(
                action.Kind,
                action.TargetObstacleInstanceId,
                triggerObstacleInstanceId,
                action.TriggerX,
                obstacleLeftX);
            _hasLastObservation = true;
        }

        private readonly struct TriggerObservation
        {
            private const float TriggerEpsilon = 0.001f;

            public TriggerObservation(
                BotActionKind kind,
                int? targetObstacleInstanceId,
                int triggerObstacleInstanceId,
                float triggerX,
                float obstacleLeftX)
            {
                Kind = kind;
                TargetObstacleInstanceId = targetObstacleInstanceId;
                TriggerObstacleInstanceId = triggerObstacleInstanceId;
                TriggerX = triggerX;
                ObstacleLeftX = obstacleLeftX;
            }

            private BotActionKind Kind { get; }
            private int? TargetObstacleInstanceId { get; }
            private int TriggerObstacleInstanceId { get; }
            private float TriggerX { get; }
            public float ObstacleLeftX { get; }

            public bool Matches(PlannedAction action, int triggerObstacleInstanceId)
            {
                return action != null
                    && Kind == action.Kind
                    && TargetObstacleInstanceId == action.TargetObstacleInstanceId
                    && TriggerObstacleInstanceId == triggerObstacleInstanceId
                    && Mathf.Abs(TriggerX - action.TriggerX) <= TriggerEpsilon;
            }
        }
    }
}
