using System;
using System.Collections.Generic;
using Assets.Scripts.Gameplay;
using Assets.Scripts.Gameplay.Enums;
using Assets.Scripts.System;
using UnityEngine;

namespace Assets.Scripts.Bot
{
    /// <summary>
    /// Тонкий диспетчер: следит за дистанцией, делегирует Fire/IsCompleted handler'у действия.
    /// Стреляет OnStepCompleted при завершении (нормальном или skip too-late).
    /// </summary>
    public class StepExecutor
    {
        private readonly Hamster _hamster;
        private readonly Dictionary<BotAction, IActionHandler> _handlers;
        private BranchStep _step;
        private IActionHandler _activeHandler;

        public event Action OnStepCompleted;

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
        /// Тикает каждый кадр пока шаг в процессе. Стреляет OnStepCompleted при завершении.
        /// </summary>
        public void PollCompletion()
        {
            if (_step == null || _step.Status != BranchStepStatus.InProgress)
                return;

            if (_activeHandler == null || !_activeHandler.IsCompleted(_hamster, _step))
                return;

            _step.MarkCompleted();
            DebugManager.DiagLog($"[Bot EXEC] {_step.Action} completed");
            OnStepCompleted?.Invoke();
        }

        /// <summary>
        /// Пытается запустить шаг (только из Pending). Стреляет OnStepCompleted при skip too-late.
        /// </summary>
        public void TryFire()
        {
            if (_step == null || _step.Status != BranchStepStatus.Ready)
                return;

            // Пропустить, если момент упущен
            float dist = LiveObstacleFinder.GetDistance(_step.TargetObstacle, _hamster.RightX);

            if (dist < BotConsts.StepTooLateThreshold)
            {
                _step.MarkCompleted();
                DebugManager.DiagLog($"[Bot EXEC] {_step.Action} SKIPPED (too late) dist={dist:F2}");
                OnStepCompleted?.Invoke();
                return;
            }

            // Ждать, пока дистанция не достигнет порога
            if (dist > _step.ExecuteAtDistance)
                return;

            if (!CanFire())
                return;

            // Запустить действие через handler
            var handler = GetHandler(_step.Action);
            if (handler == null)
                return;

            if (handler is IPreFireDelayHandler delayHandler &&
                delayHandler.ShouldDelay(_hamster, _step))
                return;

            handler.Fire(_hamster, _step);
            _activeHandler = handler;
            _step.MarkInProgress();
            DebugManager.DiagLog($"[Bot EXEC] {_step.Action} FIRE dist={dist:F2} reason={_step.Reason}");
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
