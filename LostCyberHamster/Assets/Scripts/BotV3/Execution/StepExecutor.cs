using Assets.Scripts.Common;
using Assets.Scripts.Common.Models;
using Assets.Scripts.GameEngine.Controllers;
using Assets.Scripts.Gameplay;
using Assets.Scripts.Gameplay.Enums;
using Assets.Scripts.System;
using UnityEngine;

namespace Assets.Scripts.BotV3
{
    public enum StepExecutionTickResult
    {
        None,
        StepCompleted
    }

    /// <summary>
    /// Ждёт нужной дистанции до объекта и отправляет игровую команду.
    /// Поддерживает SwitchLane и Jump.
    /// </summary>
    public class StepExecutor
    {
        private readonly Hamster _hamster;
        private BranchStep _step;
        private float _switchLaneExecTime;
        private float _jumpExecTime;
        private float _jumpWorldShift = -1f;

        public bool HasActiveStep => _step != null && _step.Status != BranchStepStatus.Completed;
        public bool IsStepInProgress => _step != null && _step.Status == BranchStepStatus.InProgress;

        public StepExecutor(Hamster hamster)
        {
            _hamster = hamster;
        }

        public void SetStep(BranchStep step)
        {
            _step = step;
        }

        public void ClearStep()
        {
            _step = null;
        }

        public StepExecutionTickResult TryExecute()
        {
            if (_step == null || _step.Status == BranchStepStatus.Completed)
                return StepExecutionTickResult.None;

            if (_step.Status == BranchStepStatus.InProgress)
                return TryCompleteInProgressStep();

            float dist = GetLiveDistance(_step.TargetObstacle);

            if (dist < BotExecutionConsts.StepTooLateThreshold)
            {
                MarkStepSkippedAsTooLate(dist);
                return StepExecutionTickResult.StepCompleted;
            }

            if (ShouldWaitForFire(dist))
                return StepExecutionTickResult.None;

            if (!CanFireInCurrentHamsterState())
                return StepExecutionTickResult.None;

            if (_step.Action == BotAction.Jump && ShouldDelayJumpOver())
                return StepExecutionTickResult.None;

            Fire(dist);
            return StepExecutionTickResult.None;
        }

        private void Fire(float dist)
        {
            switch (_step.Action)
            {
                case BotAction.SwitchLane:
                    _hamster.TapRequest.Invoke();
                    _switchLaneExecTime = Time.time;
                    break;

                case BotAction.Jump:
                    if (_hamster.HamsterState.Value == HamsterStateEnum.RoofRun)
                        _hamster.RoofJumpRequest.Invoke();
                    else
                        _hamster.JumpRequest.Invoke();
                    _jumpExecTime = Time.time;
                    break;
            }

            _step.MarkInProgress();
            DebugManager.DiagLog($"[BotV3 EXEC] {_step.Action} FIRE dist={dist:F2} reason={_step.Reason}");
        }

        private bool ShouldWaitForFire(float dist)
        {
            return dist > _step.ExecuteAtDistance;
        }

        private bool CanFireInCurrentHamsterState()
        {
            return _hamster.HamsterState.Value == HamsterStateEnum.Run
                || _hamster.HamsterState.Value == HamsterStateEnum.RoofRun;
        }

        private void MarkStepSkippedAsTooLate(float dist)
        {
            _step.MarkCompleted();
            DebugManager.DiagLog($"[BotV3 EXEC] {_step.Action} SKIPPED (too late) dist={dist:F2}");
        }

        private StepExecutionTickResult TryCompleteInProgressStep()
        {
            if (_step.Action == BotAction.SwitchLane)
                return TryCompleteSwitchLane();

            if (_step.Action == BotAction.Jump)
                return TryCompleteJump();

            return StepExecutionTickResult.None;
        }

        private StepExecutionTickResult TryCompleteSwitchLane()
        {
            bool timeElapsed = Time.time - _switchLaneExecTime >= BotExecutionConsts.SwitchLaneMinElapsed;
            if (!timeElapsed || _hamster.IsShifting.Value)
                return StepExecutionTickResult.None;

            ValidateSwitchLaneCompletionContract();
            _step.MarkCompleted();
            DebugManager.DiagLog("[BotV3 EXEC] SwitchLane completed");
            return StepExecutionTickResult.StepCompleted;
        }

        private StepExecutionTickResult TryCompleteJump()
        {
            if (Time.time - _jumpExecTime < BotExecutionConsts.JumpMinCompletionDelay)
                return StepExecutionTickResult.None;

            if (IsActiveJumpState(_hamster.HamsterState.Value))
                return StepExecutionTickResult.None;

            _step.MarkCompleted();
            DebugManager.DiagLog($"[BotV3 EXEC] Jump completed (state={_hamster.HamsterState.Value})");
            return StepExecutionTickResult.StepCompleted;
        }

        /// <summary>
        /// Задерживает прыжок, пока IsOverlapAtShift показывает коллизию.
        /// Когда хомяк достаточно близко, jump world shift перенесёт его ЗА препятствие.
        /// При дистанции <= JumpLateFallbackDistance прыгаем в любом случае.
        /// </summary>
        private bool ShouldDelayJumpOver()
        {
            var target = _step.TargetObstacle;
            if (target.Type != ObstacleTypeEnum.smallNotAliveRoad &&
                target.Type != ObstacleTypeEnum.smallNotAliveRoadAndRoof)
                return false;

            var liveObstacle = FindLiveObstacle(target.StableId);
            if (liveObstacle == null)
                return false;

            EnsureJumpWorldShiftCached();
            if (_jumpWorldShift <= 0f)
                return false;

            bool wouldOverlap = CollisionUtils.IsOverlapAtShift(
                _hamster.transform,
                _hamster.ColliderWidth,
                _jumpWorldShift,
                liveObstacle);

            if (!wouldOverlap)
                return false;

            float liveDist = liveObstacle.transform.position.x
                           - liveObstacle.ColliderWidth * 0.5f
                           - _hamster.RightX;

            if (liveDist <= BotExecutionConsts.JumpLateFallbackDistance)
                return false;

            return true;
        }

        private void EnsureJumpWorldShiftCached()
        {
            if (_jumpWorldShift >= 0f)
                return;

            var ctrl = _hamster.GetComponentInChildren<TransformAnimatorController>();
            if (ctrl == null)
                return;

            _jumpWorldShift = HelpMethods.GetWorldShiftForClip(ctrl, BotExecutionConsts.JumpClipName);
        }

        private static bool IsActiveJumpState(HamsterStateEnum state)
        {
            switch (state)
            {
                case HamsterStateEnum.Jump:
                case HamsterStateEnum.JumpOver:
                case HamsterStateEnum.JumpOnObstacle:
                case HamsterStateEnum.JumpOnRoof:
                case HamsterStateEnum.JumpFromRoof:
                case HamsterStateEnum.JumpOnObstacleFromRoof:
                case HamsterStateEnum.RoofJump:
                    return true;
                default:
                    return false;
            }
        }

        private float GetLiveDistance(ObstacleInfo target)
        {
            var spawner = ObstacleSpawner.Instance;
            if (spawner == null)
                return target.DistanceToHamster;

            var spawned = spawner.SpawnedObstacles;
            for (int i = 0; i < spawned.Count; i++)
            {
                var inst = spawned[i];
                if (inst?.ObstacleScript == null) continue;

                if (inst.ObstacleScript.GetInstanceID() == target.StableId)
                {
                    float leftX = inst.ObstacleScript.transform.position.x
                                - inst.ObstacleScript.ColliderWidth * 0.5f;
                    return leftX - _hamster.RightX;
                }
            }

            return target.DistanceToHamster;
        }

        private static Obstacle FindLiveObstacle(int stableId)
        {
            var spawner = ObstacleSpawner.Instance;
            if (spawner == null)
                return null;

            var spawned = spawner.SpawnedObstacles;
            for (int i = 0; i < spawned.Count; i++)
            {
                var inst = spawned[i];
                if (inst?.ObstacleScript == null) continue;

                if (inst.ObstacleScript.GetInstanceID() == stableId)
                    return inst.ObstacleScript;
            }

            return null;
        }

        private void ValidateSwitchLaneCompletionContract()
        {
            float plannedTravel = _step.CompletionWorldShift - _step.FireWorldShift;
            if (plannedTravel <= 0f)
                return;

            float expectedDuration = plannedTravel / BotPhysicsConsts.GameSpeedBase;
            float actualDuration = Time.time - _switchLaneExecTime;
            float delta = actualDuration - expectedDuration;

            if (Mathf.Abs(delta) <= BotExecutionConsts.SwitchLaneCompletionTolerance)
                return;

            Debug.LogError(
                $"[BotV3 CONTRACT] SwitchLane completion drift detected. " +
                $"planned={expectedDuration:F3}s actual={actualDuration:F3}s delta={delta:F3}s " +
                $"reason={_step.Reason}");
        }
    }
}
