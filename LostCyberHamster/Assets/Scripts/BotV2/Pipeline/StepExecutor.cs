using Assets.Scripts.Gameplay;
using Assets.Scripts.Gameplay.Enums;
using Assets.Scripts.System;
using UnityEngine;

namespace Assets.Scripts.BotV2
{
    /// <summary>
    /// Ждёт нужной живой дистанции до объекта и отправляет игровую команду.
    /// Живую дистанцию получает через StableId из ObstacleSpawner.
    /// </summary>
    public class StepExecutor
    {
        private readonly Hamster _hamster;
        private ChainStep _step;

        private float _switchLaneExecTime;

        public bool HasActiveStep => _step != null && _step.Status != ChainStepStatus.Completed;

        public StepExecutor(Hamster hamster)
        {
            _hamster = hamster;
        }

        public void SetStep(ChainStep step)
        {
            _step = step;
        }

        public void ClearStep()
        {
            _step = null;
        }

        /// <summary>
        /// Вызывается каждый кадр. Пытается исполнить активный шаг.
        /// </summary>
        public void TryExecute()
        {
            if (_step == null) return;
            if (_step.Status == ChainStepStatus.Completed) return;

            // InProgress: ждём завершения действия
            if (_step.Status == ChainStepStatus.InProgress)
            {
                CheckCompletion();
                return;
            }

            // Ready: проверяем живую дистанцию
            float dist = GetLiveDistance(_step.TargetObstacle);

            // Объект уже позади — пропускаем
            if (dist < -0.3f)
            {
                _step.Status = ChainStepStatus.Completed;
                BotLogger.Log(BotLogLevel.Normal,
                    $"[EXECUTE] {_step.Action} SKIPPED (too late) dist={dist:F2}");
                return;
            }

            // Ещё слишком далеко
            if (dist > _step.ExecuteAtDistance) return;

            // Финальная проверка состояния хомяка
            if (_hamster.HamsterState.Value != HamsterStateEnum.Run) return;

            Fire(dist);
        }

        private void CheckCompletion()
        {
            if (_step.Action == BotAction.SwitchLane)
            {
                bool timeElapsed = Time.time - _switchLaneExecTime >= 0.1f;
                if (timeElapsed && !_hamster.IsShifting.Value)
                {
                    _step.Status = ChainStepStatus.Completed;
                    BotLogger.Log(BotLogLevel.Normal,
                        $"[RESULT] SwitchLane completed → lane={(  _hamster.IsOnBottomLine.Value ? "bottom" : "top")} lives={_hamster.Lives.Value}");
                }
            }
            else
            {
                var state = _hamster.HamsterState.Value;
                if (state == HamsterStateEnum.Run)
                {
                    _step.Status = ChainStepStatus.Completed;
                    BotLogger.Log(BotLogLevel.Normal,
                        $"[RESULT] {_step.Action} completed → lane={(  _hamster.IsOnBottomLine.Value ? "bottom" : "top")} lives={_hamster.Lives.Value}");
                }
            }
        }

        private void Fire(float liveDist)
        {
            switch (_step.Action)
            {
                case BotAction.SwitchLane:
                    _hamster.TapRequest.Invoke();
                    _switchLaneExecTime = Time.time;
                    break;
                case BotAction.Jump:
                    _hamster.JumpRequest.Invoke();
                    break;
            }

            _step.Status = ChainStepStatus.InProgress;
            BotLogger.Log(BotLogLevel.Normal,
                $"[EXECUTE] {_step.Action}: liveDist={liveDist:F2} → FIRE");
        }

        /// <summary>
        /// Получает текущую живую дистанцию до объекта через ObstacleSpawner.
        /// При отсутствии объекта возвращает snapshot-дистанцию.
        /// </summary>
        private float GetLiveDistance(ObstacleInfo target)
        {
            var spawner = ObstacleSpawner.Instance;
            if (spawner == null) return target.DistanceToHamster;

            var spawned = spawner.SpawnedObstacles;
            for (int i = 0; i < spawned.Count; i++)
            {
                var inst = spawned[i];
                if (inst?.ObstacleScript == null) continue;
                if (inst.ObstacleScript.GetInstanceID() != target.StableId) continue;

                float leftX = inst.ObstacleScript.transform.position.x
                            - inst.ObstacleScript.ColliderWidth * 0.5f;
                return leftX - _hamster.RightX;
            }

            // Объект не найден (уже убран со сцены)
            return target.DistanceToHamster;
        }
    }
}
