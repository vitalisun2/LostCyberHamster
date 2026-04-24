using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.Common;
using Assets.Scripts.Gameplay;
using Assets.Scripts.Gameplay.Enums;
using Assets.Scripts.System;
using UnityEngine;

namespace Assets.Scripts.Bot.Execution.Handlers
{
    /// <summary>
    /// Исполняет jump on roof в рантайме и ждёт перехода хомяка в RoofRun.
    /// </summary>
    internal sealed class JumpOnRoofActionHandler : IActionExecutionHandler
    {
        /// <summary>
        /// Пытается запустить jump on roof.
        /// </summary>
        public ActionFireResult TryFire(Hamster hamster, PlannedAction action)
        {
            // Проверяем обязательный контекст исполнения.
            Guard.NotNull(
                (hamster, nameof(hamster)),
                (action, nameof(action)));

            // Сначала проверяем, что runtime ещё позволяет исполнить запланированную посадку на крышу.
            if (action.Kind != BotActionKind.JumpOnRoof
                || !action.TargetObstacleInstanceId.HasValue)
            {
                return ActionFireResult.Cancelled;
            }

            if (hamster.Energy.Value < action.EnergyCost)
                return ActionFireResult.Cancelled;

            // Посадка на крышу остаётся валидной только при старте из run-state.
            if (hamster.HamsterState.Value != HamsterStateEnum.Run)
            {
                return hamster.HamsterState.Value == HamsterStateEnum.RunFromRoof
                    ? ActionFireResult.Waiting
                    : ActionFireResult.Cancelled;
            }

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
                $"[Bot EXEC] FIRE kind={action.Kind} " +
                $"triggerX={action.TriggerX:F2} obstacleLeftX={collider.bounds.min.x:F2} " +
                $"desc={action.Description}");
            hamster.JumpRequest.Invoke();
            return ActionFireResult.Fired;
        }

        /// <summary>
        /// Проверяет завершение jump on roof.
        /// </summary>
        public bool IsCompleted(Hamster hamster, PlannedAction action)
        {
            bool completed = hamster.HamsterState.Value == HamsterStateEnum.RoofRun;
            if (completed)
            {
                DebugManager.DiagLog(
                    $"[Bot EXEC] COMPLETE kind={action.Kind} " +
                    $"state={hamster.HamsterState.Value} " +
                    $"desc={action.Description}");
            }

            return completed;
        }

        /// <summary>
        /// Ищет живое obstacle по instance id.
        /// </summary>
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
