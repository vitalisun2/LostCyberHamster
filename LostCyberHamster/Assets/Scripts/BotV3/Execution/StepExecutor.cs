using System.Collections.Generic;
using Assets.Scripts.Gameplay;
using Assets.Scripts.Gameplay.Enums;
using Assets.Scripts.System;
using UnityEngine;

namespace Assets.Scripts.BotV3
{
    /// <summary>
    /// Тонкий диспетчер: следит за дистанцией, делегирует Fire/IsCompleted handler'у действия.
    /// </summary>
    public class StepExecutor
    {
        private readonly Hamster _hamster;
        private readonly Dictionary<BotAction, IActionHandler> _handlers;
        private BranchStep _step;
        private IActionHandler _activeHandler;

        public bool IsStepInProgress => _step != null && _step.Status == BranchStepStatus.InProgress;

        public StepExecutor(Hamster hamster)
        {
            _hamster = hamster;
            _handlers = new Dictionary<BotAction, IActionHandler>
            {
                { BotAction.SwitchLane, new SwitchLaneHandler() },
                { BotAction.Jump, new JumpHandler() },
            };
        }

        public void SetStep(BranchStep step)
        {
            _step = step;
            _activeHandler = null;
        }

        public void ClearStep()
        {
            _step = null;
            _activeHandler = null;
        }

        /// <summary>
        /// Тикает каждый кадр. Возвращает true, если шаг завершён.
        /// </summary>
        public bool TryExecute()
        {
            if (_step == null || _step.Status == BranchStepStatus.Completed)
                return false;

            if (_step.Status == BranchStepStatus.InProgress)
                return TryComplete();

            return TryFire();
        }

        private bool TryFire()
        {
            float dist = LiveObstacleFinder.GetDistance(_step.TargetObstacle, _hamster.RightX);

            if (dist < BotExecutionConsts.StepTooLateThreshold)
            {
                _step.MarkCompleted();
                DebugManager.DiagLog($"[BotV3 EXEC] {_step.Action} SKIPPED (too late) dist={dist:F2}");
                return true;
            }

            if (dist > _step.ExecuteAtDistance)
                return false;

            if (!CanFire())
                return false;

            var handler = GetHandler(_step.Action);
            if (handler == null)
                return false;

            if (handler.ShouldDelay(_hamster, _step))
                return false;

            handler.Fire(_hamster, _step);
            _activeHandler = handler;
            _step.MarkInProgress();
            DebugManager.DiagLog($"[BotV3 EXEC] {_step.Action} FIRE dist={dist:F2} reason={_step.Reason}");
            return false;
        }

        private bool TryComplete()
        {
            if (_activeHandler == null || !_activeHandler.IsCompleted(_hamster, _step))
                return false;

            _step.MarkCompleted();
            DebugManager.DiagLog($"[BotV3 EXEC] {_step.Action} completed");
            return true;
        }

        private bool CanFire()
        {
            return _hamster.HamsterState.Value == HamsterStateEnum.Run
                || _hamster.HamsterState.Value == HamsterStateEnum.RoofRun;
        }

        private IActionHandler GetHandler(BotAction action)
        {
            _handlers.TryGetValue(action, out var handler);
            return handler;
        }
    }
}
