using Assets.Scripts.Gameplay;
using Assets.Scripts.Gameplay.Enums;
using Assets.Scripts.System;
using UnityEngine;

namespace Assets.Scripts.BotV3
{
    /// <summary>
    /// Ждёт нужной дистанции до объекта и отправляет игровую команду.
    /// V3 minimal: только SwitchLane (TapRequest).
    /// </summary>
    public class StepExecutor
    {
        private readonly Hamster _hamster;
        private BranchStep _step;
        private float _switchLaneExecTime;

        public bool WasCancelled { get; private set; }
        public bool HasActiveStep => _step != null && _step.Status != BranchStepStatus.Completed;

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

            if (dist < -0.3f)
            {
                _step.Status = BranchStepStatus.Completed;
                DebugManager.DiagLog($"[BotV3 EXEC] SwitchLane SKIPPED (too late) dist={dist:F2}");
                return;
            }

            if (dist > _step.ExecuteAtDistance)
                return;

            if (_hamster.HamsterState.Value != HamsterStateEnum.Run &&
                _hamster.HamsterState.Value != HamsterStateEnum.RoofRun)
                return;

            if (!SwitchLaneSafety.IsImmediatelySafe(_hamster))
            {
                if (dist > ActionGenerator.SwitchLaneLatestSafeDist)
                    return;

                _step.Status = BranchStepStatus.Completed;
                WasCancelled = true;
                DebugManager.DiagLog($"[BotV3 EXEC] SwitchLane CANCELLED — unsafe near deadline, dist={dist:F2}");
                return;
            }

            _hamster.TapRequest.Invoke();
            _step.Status = BranchStepStatus.InProgress;
            _switchLaneExecTime = Time.time;
            DebugManager.DiagLog($"[BotV3 EXEC] SwitchLane FIRE dist={dist:F2} reason={_step.Reason}");
        }

        private void CheckCompletion()
        {
            bool timeElapsed = Time.time - _switchLaneExecTime >= 0.1f;
            if (timeElapsed && !_hamster.IsShifting.Value)
            {
                _step.Status = BranchStepStatus.Completed;
                DebugManager.DiagLog("[BotV3 EXEC] SwitchLane completed");
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
    }
}
