using Assets.Scripts.Bot.Strategies.Shared.Models;
using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.Gameplay;
using UnityEngine;

namespace Assets.Scripts.Bot.Strategies.Shared.Execution
{
    /// <summary>
    /// Проверяет, дошёл ли live obstacle до рассчитанной точки запуска action.
    /// </summary>
    internal sealed class ActionTriggerGate
    {
        private readonly LiveObstacleResolver _liveObstacleResolver;

        public ActionTriggerGate(LiveObstacleResolver liveObstacleResolver)
        {
            _liveObstacleResolver = liveObstacleResolver;
        }

        /// <summary>
        /// Проверяет, можно ли сработать по текущему положению целевого препятствия.
        /// </summary>
        public ActionFireResult Check(PlannedAction action, out float obstacleLeftX)
        {
            // Сбрасывает выходную координату и определяет obstacle-триггер.
            obstacleLeftX = 0f;
            int? triggerObstacleInstanceId = action?.TriggerObstacleInstanceId ?? action?.TargetObstacleInstanceId;

            // Отменяет проверку без доступного obstacle-триггера.
            if (!triggerObstacleInstanceId.HasValue)
                return ActionFireResult.Cancelled;

            // Находит live obstacle по сохранённому идентификатору.
            Obstacle obstacle = _liveObstacleResolver.Find(triggerObstacleInstanceId.Value);
            if (obstacle == null)
                return ActionFireResult.Cancelled;

            // Получает collider препятствия для расчёта левой границы.
            BoxCollider2D collider = obstacle.GetComponentInChildren<BoxCollider2D>();
            if (collider == null)
                return ActionFireResult.Cancelled;

            // Сравнивает текущую позицию препятствия с точкой запуска.
            obstacleLeftX = collider.bounds.min.x;
            return obstacleLeftX > action.TriggerX
                ? ActionFireResult.Waiting
                : ActionFireResult.Fired;
        }
    }
}
