using System.Collections.Generic;
using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.Gameplay;
using Assets.Scripts.System;
using UnityEngine;

namespace Assets.Scripts.Bot.Execution
{
    internal enum ActionFireResult
    {
        Waiting,
        Fired,
        Cancelled
    }

    internal interface IActionExecutionHandler
    {
        ActionFireResult TryFire(Hamster hamster, PlannedAction action);

        bool IsCompleted(Hamster hamster, PlannedAction action);
    }

    internal sealed class SwitchLaneActionHandler : IActionExecutionHandler
    {
        public ActionFireResult TryFire(Hamster hamster, PlannedAction action)
        {
            if (hamster == null || action == null || !action.TargetObstacleInstanceId.HasValue)
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
                $"[BotV2 EXEC] FIRE kind={action.Kind} " +
                $"triggerX={action.TriggerX:F2} obstacleLeftX={collider.bounds.min.x:F2} " +
                $"targetLane={(action.TargetBottomLine.HasValue ? (action.TargetBottomLine.Value ? "bottom" : "top") : "n/a")} " +
                $"desc={action.Description}");
            hamster.TapRequest.Invoke();
            return ActionFireResult.Fired;
        }

        public bool IsCompleted(Hamster hamster, PlannedAction action)
        {
            if (hamster == null || action == null)
                return true;

            if (hamster.IsShifting.Value)
                return false;

            if (!action.TargetBottomLine.HasValue)
                return true;

            bool completed = hamster.IsOnBottomLine.Value == action.TargetBottomLine.Value;
            if (completed)
            {
                DebugManager.DiagLog(
                    $"[BotV2 EXEC] COMPLETE kind={action.Kind} " +
                    $"lane={(hamster.IsOnBottomLine.Value ? "bottom" : "top")} " +
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

    public sealed class PlanExecutor
    {
        private readonly IReadOnlyDictionary<BotActionKind, IActionExecutionHandler> _handlers =
            new Dictionary<BotActionKind, IActionExecutionHandler>
            {
                { BotActionKind.Tap, new SwitchLaneActionHandler() }
            };

        private bool _isCurrentActionFired;

        public BotPlan CurrentPlan { get; private set; } = BotPlan.Empty();
        public bool IsActionInProgress => _isCurrentActionFired;
        public bool HasPendingActions => CurrentPlan.HasActions;

        public void SetPlan(BotPlan plan)
        {
            CurrentPlan = plan ?? BotPlan.Empty();
            _isCurrentActionFired = false;
        }

        public void Clear(float committedBoundaryX = 0f)
        {
            CurrentPlan = BotPlan.Empty(committedBoundaryX);
            _isCurrentActionFired = false;
        }

        public void Tick(Hamster hamster)
        {
            if (hamster == null || !CurrentPlan.HasActions)
                return;

            PlannedAction action = CurrentPlan.Actions[0];
            if (!TryGetHandler(action, out IActionExecutionHandler handler))
            {
                AdvanceHead();
                return;
            }

            if (!_isCurrentActionFired)
            {
                ActionFireResult fireResult = handler.TryFire(hamster, action);
                if (fireResult == ActionFireResult.Fired)
                    _isCurrentActionFired = true;
                else if (fireResult == ActionFireResult.Cancelled)
                    AdvanceHead();

                return;
            }

            if (handler.IsCompleted(hamster, action))
                AdvanceHead();
        }

        private bool TryGetHandler(PlannedAction action, out IActionExecutionHandler handler)
        {
            if (action != null && _handlers.TryGetValue(action.Kind, out handler))
                return true;

            handler = null;
            if (action == null)
                return false;

            DebugManager.DiagLog(
                $"[BotV2 EXEC] DROP unsupported kind={action.Kind} desc={action.Description}");
            return false;
        }

        private void AdvanceHead()
        {
            IReadOnlyList<PlannedAction> actions = CurrentPlan.Actions;
            if (actions.Count <= 1)
            {
                CurrentPlan = BotPlan.Empty(CurrentPlan.CommittedBoundaryX);
                _isCurrentActionFired = false;
                return;
            }

            var remainingActions = new List<PlannedAction>(actions.Count - 1);
            for (int actionIndex = 1; actionIndex < actions.Count; actionIndex++)
                remainingActions.Add(actions[actionIndex]);

            CurrentPlan = new BotPlan(remainingActions, CurrentPlan.CommittedBoundaryX, CurrentPlan.Score);
            _isCurrentActionFired = false;
        }
    }
}
