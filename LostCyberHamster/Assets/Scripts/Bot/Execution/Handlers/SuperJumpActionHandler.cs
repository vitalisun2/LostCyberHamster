using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.Gameplay;
using Assets.Scripts.Gameplay.Enums;
using Assets.Scripts.System;
using UnityEngine;

namespace Assets.Scripts.Bot.Execution.Handlers
{
    /// <summary>
    /// Исполняет super jump как двухфазный ввод: сначала Jump, затем SuperJump.
    /// </summary>
    internal sealed class SuperJumpActionHandler : IActionExecutionHandler
    {
        public ActionFireResult TryFire(Hamster hamster, PlannedAction action)
        {
            if (hamster == null || action == null || action.Kind != BotActionKind.SuperJump)
                return ActionFireResult.Cancelled;

            if (!action.TargetObstacleInstanceId.HasValue)
                return ActionFireResult.Cancelled;

            if (hamster.HamsterState.Value != HamsterStateEnum.Run && !hamster.IsDamaged.Value)
                return ActionFireResult.Cancelled;

            if (hamster.Energy.Value < action.EnergyCost)
                return ActionFireResult.Cancelled;

            Obstacle obstacle = FindLiveObstacle(action.TargetObstacleInstanceId.Value);
            if (obstacle == null)
                return ActionFireResult.Cancelled;

            BoxCollider2D collider = obstacle.GetComponentInChildren<BoxCollider2D>();
            if (collider == null)
                return ActionFireResult.Cancelled;

            if (collider.bounds.min.x > action.TriggerX)
                return ActionFireResult.Waiting;

            DebugManager.DiagLog(
                $"[Bot EXEC] FIRE kind={action.Kind} " +
                $"triggerX={action.TriggerX:F2} obstacleLeftX={collider.bounds.min.x:F2} " +
                $"desc={action.Description}");

            hamster.JumpRequest.Invoke();

            if (!CanUpgradeToSuperJump(hamster.HamsterState.Value))
            {
                DebugManager.DiagLog(
                    $"[Bot EXEC] CANCEL kind={action.Kind} " +
                    $"stateAfterJump={hamster.HamsterState.Value} " +
                    $"desc={action.Description}");
                return ActionFireResult.Cancelled;
            }

            hamster.SuperJumpRequest.Invoke();
            return ActionFireResult.Fired;
        }

        public bool IsCompleted(Hamster hamster, PlannedAction action)
        {
            if (hamster == null || action == null)
                return true;

            bool completed = hamster.HamsterState.Value == HamsterStateEnum.Run;
            if (completed)
            {
                DebugManager.DiagLog(
                    $"[Bot EXEC] COMPLETE kind={action.Kind} " +
                    $"state={hamster.HamsterState.Value} " +
                    $"desc={action.Description}");
            }

            return completed;
        }

        private static bool CanUpgradeToSuperJump(HamsterStateEnum hamsterState)
        {
            return hamsterState == HamsterStateEnum.Jump
                   || hamsterState == HamsterStateEnum.JumpOver
                   || hamsterState == HamsterStateEnum.JumpOnObstacle
                   || hamsterState == HamsterStateEnum.JumpOnRoof
                   || hamsterState == HamsterStateEnum.JumpDamageForSmallAlive
                   || hamsterState == HamsterStateEnum.JumpDamageForSmallNotAlive
                   || hamsterState == HamsterStateEnum.JumpDamageForBigAlive
                   || hamsterState == HamsterStateEnum.JumpOnRoofDamage;
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