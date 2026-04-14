using System.Collections.Generic;
using Assets.Scripts.Gameplay;
using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.System;
using UnityEngine;

namespace Assets.Scripts.Bot.Execution
{
    public sealed class PlanExecutor
    {
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
            if (!_isCurrentActionFired)
            {
                if (TryFire(hamster, action))
                    _isCurrentActionFired = true;

                return;
            }

            if (IsCompleted(hamster, action))
                AdvanceHead();
        }

        private bool TryFire(Hamster hamster, PlannedAction action)
        {
            if (action.Kind != BotActionKind.Tap || !action.TargetObstacleInstanceId.HasValue)
                return false;

            Obstacle obstacle = FindLiveObstacle(action.TargetObstacleInstanceId.Value);
            if (obstacle == null)
            {
                AdvanceHead();
                return false;
            }

            BoxCollider2D collider = obstacle.GetComponentInChildren<BoxCollider2D>();
            if (collider == null)
            {
                AdvanceHead();
                return false;
            }

            if (collider.bounds.min.x > action.TriggerX)
                return false;

            hamster.TapRequest.Invoke();
            return true;
        }

        private bool IsCompleted(Hamster hamster, PlannedAction action)
        {
            if (action.Kind != BotActionKind.Tap)
                return true;

            if (hamster.IsShifting.Value)
                return false;

            if (!action.TargetBottomLine.HasValue)
                return true;

            return hamster.IsOnBottomLine.Value == action.TargetBottomLine.Value;
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
