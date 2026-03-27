using Assets.Scripts.Common;
using Assets.Scripts.Common.Models;
using Assets.Scripts.GameEngine.Controllers;
using Assets.Scripts.Gameplay;
using Assets.Scripts.Gameplay.Enums;
using Assets.Scripts.System;
using UnityEngine;

namespace Assets.Scripts.BotV3
{
    /// <summary>
    /// Ждёт нужной дистанции до объекта и отправляет игровую команду.
    /// Поддерживает SwitchLane и Jump.
    /// </summary>
    public class StepExecutor
    {
        private const float JumpMinCompletionDelay = 0.3f;
        private const float JumpLateFallbackDistance = 0.1f;
        private const float TooLateThreshold = -0.3f;
        private const float SwitchLaneMinElapsed = 0.1f;
        private const float SwitchLaneCompletionTolerance = 0.12f;

        private readonly Hamster _hamster;
        private BranchStep _step;
        private float _switchLaneExecTime;
        private float _jumpExecTime;
        private float _jumpWorldShift = -1f;

        public bool WasCancelled { get; private set; }
        public bool HasActiveStep => _step != null && _step.Status != BranchStepStatus.Completed;
        public bool IsStepInProgress => _step != null && _step.Status == BranchStepStatus.InProgress;

        public StepExecutor(Hamster hamster)
        {
            _hamster = hamster;
        }

        public void SetStep(BranchStep step)
        {
            _step = step;
            WasCancelled = false;
        }

        public void ClearStep()
        {
            _step = null;
        }

        public void TryExecute()
        {
            if (_step == null || _step.Status == BranchStepStatus.Completed)
                return;

            if (_step.Status == BranchStepStatus.InProgress)
            {
                CheckCompletion();
                return;
            }

            float dist = GetLiveDistance(_step.TargetObstacle);

            if (dist < TooLateThreshold)
            {
                _step.Status = BranchStepStatus.Completed;
                DebugManager.DiagLog($"[BotV3 EXEC] {_step.Action} SKIPPED (too late) dist={dist:F2}");
                return;
            }

            if (dist > _step.ExecuteAtDistance)
                return;

            if (_hamster.HamsterState.Value != HamsterStateEnum.Run &&
                _hamster.HamsterState.Value != HamsterStateEnum.RoofRun)
                return;

            if (_step.Action == BotAction.Jump && ShouldDelayJumpOver())
                return;

            Fire(dist);
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

            _step.Status = BranchStepStatus.InProgress;
            DebugManager.DiagLog($"[BotV3 EXEC] {_step.Action} FIRE dist={dist:F2} reason={_step.Reason}");
        }

        private void CheckCompletion()
        {
            if (_step.Action == BotAction.SwitchLane)
            {
                bool timeElapsed = Time.time - _switchLaneExecTime >= SwitchLaneMinElapsed;
                if (timeElapsed && !_hamster.IsShifting.Value)
                {
                    ValidateSwitchLaneCompletionContract();
                    _step.Status = BranchStepStatus.Completed;
                    DebugManager.DiagLog("[BotV3 EXEC] SwitchLane completed");
                }
            }
            else if (_step.Action == BotAction.Jump)
            {
                if (Time.time - _jumpExecTime < JumpMinCompletionDelay)
                    return;

                if (!IsActiveJumpState(_hamster.HamsterState.Value))
                {
                    _step.Status = BranchStepStatus.Completed;
                    DebugManager.DiagLog($"[BotV3 EXEC] Jump completed (state={_hamster.HamsterState.Value})");
                }
            }
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

            if (liveDist <= JumpLateFallbackDistance)
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

            _jumpWorldShift = HelpMethods.GetWorldShiftForClip(ctrl, "transform_jump");
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

            if (Mathf.Abs(delta) <= SwitchLaneCompletionTolerance)
                return;

            Debug.LogError(
                $"[BotV3 CONTRACT] SwitchLane completion drift detected. " +
                $"planned={expectedDuration:F3}s actual={actualDuration:F3}s delta={delta:F3}s " +
                $"reason={_step.Reason}");
        }
    }
}
