using System.Collections.Generic;
using Assets.Scripts.Bot.Execution.Handlers;
using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.Gameplay;
using Assets.Scripts.System;

namespace Assets.Scripts.Bot.Execution
{
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
