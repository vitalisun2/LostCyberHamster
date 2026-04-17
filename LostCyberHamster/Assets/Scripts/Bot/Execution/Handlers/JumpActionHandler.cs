using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.Gameplay;
using Assets.Scripts.Gameplay.Enums;
using Assets.Scripts.System;
using UnityEngine;

namespace Assets.Scripts.Bot.Execution.Handlers
{
    /// <summary>
    /// Исполняет обычный jump-over в рантайме и ждёт возврата хомяка в состояние Run.
    /// </summary>
    internal sealed class JumpActionHandler : IActionExecutionHandler
    {
        public ActionFireResult TryFire(Hamster hamster, PlannedAction action)
        {
            // Сначала проверяем, что runtime ещё позволяет исполнить запланированный прыжок.
            if (hamster == null || action == null || !action.TargetObstacleInstanceId.HasValue)
                return ActionFireResult.Cancelled;

            if (hamster.Energy.Value < action.EnergyCost)
                return ActionFireResult.Cancelled;

            if (hamster.HamsterState.Value != HamsterStateEnum.Run && !hamster.IsDamaged.Value)
                return ActionFireResult.Cancelled;

            Obstacle obstacle = FindLiveObstacle(action.TargetObstacleInstanceId.Value);
            if (obstacle == null)
                return ActionFireResult.Cancelled;

            BoxCollider2D collider = obstacle.GetComponentInChildren<BoxCollider2D>();
            if (collider == null)
                return ActionFireResult.Cancelled;

            // Ждём, пока obstacle дойдёт до заранее рассчитанной точки запуска.
            if (collider.bounds.min.x > action.TriggerX)
                return ActionFireResult.Waiting;

            DebugManager.DiagLog(
                $"[BotV2 EXEC] FIRE kind={action.Kind} " +
                $"triggerX={action.TriggerX:F2} obstacleLeftX={collider.bounds.min.x:F2} " +
                $"desc={action.Description}");
            hamster.JumpRequest.Invoke();
            return ActionFireResult.Fired;
        }

        public bool IsCompleted(Hamster hamster, PlannedAction action)
        {
            // Если runtime больше не доступен, ждать уже нечего.
            if (hamster == null || action == null)
                return true;

            bool completed = hamster.HamsterState.Value == HamsterStateEnum.Run;
            if (completed)
            {
                DebugManager.DiagLog(
                    $"[BotV2 EXEC] COMPLETE kind={action.Kind} " +
                    $"state={hamster.HamsterState.Value} " +
                    $"desc={action.Description}");
            }

            return completed;
        }

        private static Obstacle FindLiveObstacle(int instanceId)
        {
            ObstacleSpawner spawner = ObstacleSpawner.Instance;
            if (spawner == null)
                return null;

            for (int obstacleIndex = 0; obstacleIndex < spawner.SpawnedObstacles.Count; obstacleIndex++)
            {
                Obstacle obstacle = spawner.SpawnedObstacles[obstacleIndex]?.ObstacleScript;
                if (obstacle != null && obstacle.GetInstanceID() == instanceId)
                    return obstacle;
            }

            return null;
        }
    }
}
