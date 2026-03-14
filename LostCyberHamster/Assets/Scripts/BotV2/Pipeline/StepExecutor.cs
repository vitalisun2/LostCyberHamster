using Assets.Scripts.Common;
using Assets.Scripts.Common.Models;
using Assets.Scripts.GameEngine.Controllers;
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
        private const float SwitchLaneLateCancelDistance = ActionGenerator.SwitchLaneLatestSafeDist;
        private const float LifeCollectibleLateCancelDistance = 0.7f;
        private const float JumpOnRightToleranceRatio = 0.2f;
        private const float JumpOnLateFallbackDistance = 0.1f;
        private const float JumpAfterSwitchMinDist = 0.8f;
        private const float JumpAfterSwitchMaxDist = 2.4f;

        private readonly Hamster _hamster;
        private ChainStep _step;

        private float _switchLaneExecTime;
        private float _jumpWorldShift = -1f;
        private float _stepStartedAt = -1f;
        private float _nextStallLogAt = -1f;
        private float _nextSwitchLaneUnsafeLogAt = -1f;
        private bool _tryJumpAfterSwitchLane;
        private bool _jumpAfterSwitchLaneFired;

        /// <summary>Шаг был отменён из-за изменившейся обстановки. Оркестратор перепланирует.</summary>
        public bool WasCancelled { get; private set; }

        public bool HasActiveStep => _step != null && _step.Status != ChainStepStatus.Completed;

        public StepExecutor(Hamster hamster)
        {
            _hamster = hamster;
        }

        public void SetStep(ChainStep step)
        {
            _step = step;
            WasCancelled = false;
            _stepStartedAt = -1f;
            _nextStallLogAt = -1f;
            _nextSwitchLaneUnsafeLogAt = -1f;
            _tryJumpAfterSwitchLane = false;
            _jumpAfterSwitchLaneFired = false;
        }

        public void ClearStep()
        {
            _step = null;
            _stepStartedAt = -1f;
            _nextStallLogAt = -1f;
            _nextSwitchLaneUnsafeLogAt = -1f;
            _tryJumpAfterSwitchLane = false;
            _jumpAfterSwitchLaneFired = false;
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
                TryFireJumpAfterSwitchLane();
                LogIfActionLooksStuck();
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
                    $"[EXECUTE] {_step.Action} SKIPPED (too late) dist={dist:F2}\n" +
                    $"  hamster: {BotLogger.FormatHamster(_hamster)}\n" +
                    $"  step: {BotLogger.FormatStep(_step)}\n" +
                    $"  live obstacles: {BotLogger.FormatLiveObstacles(_hamster, _step.TargetObstacle.StableId)}");
                return;
            }

            // Ещё слишком далеко
            if (dist > _step.ExecuteAtDistance) return;

            // Финальная проверка состояния хомяка
            if (_hamster.HamsterState.Value != HamsterStateEnum.Run) return;

            // Перепроверка безопасности SwitchLane перед исполнением:
            // отменяем только если live-данные показывают столкновение во время смещения
            // или в момент завершения lane switch.
            if (_step.Action == BotAction.SwitchLane && !IsSwitchLaneImmediatelySafeNow())
            {
                float lateCancelDistance = IsLifeCollectibleSwitchLane()
                    ? LifeCollectibleLateCancelDistance
                    : SwitchLaneLateCancelDistance;

                if (dist > lateCancelDistance)
                {
                    if (_nextSwitchLaneUnsafeLogAt < 0f || Time.time >= _nextSwitchLaneUnsafeLogAt)
                    {
                        _nextSwitchLaneUnsafeLogAt = Time.time + 0.5f;
                        BotLogger.Log(BotLogLevel.Verbose,
                            $"[EXECUTE] SwitchLane WAIT — target lane still unsafe, dist={dist:F2}\n" +
                            $"  hamster: {BotLogger.FormatHamster(_hamster)}\n" +
                            $"  step: {BotLogger.FormatStep(_step)}\n" +
                            $"  live obstacles: {BotLogger.FormatLiveObstacles(_hamster, _step.TargetObstacle.StableId)}");
                    }
                    return;
                }

                _step.Status = ChainStepStatus.Completed;
                WasCancelled = true;
                _stepStartedAt = -1f;
                _nextStallLogAt = -1f;
                _nextSwitchLaneUnsafeLogAt = -1f;
                BotLogger.Log(BotLogLevel.Normal,
                    $"[EXECUTE] SwitchLane CANCELLED — still unsafe near deadline, dist={dist:F2}\n" +
                    $"  hamster: {BotLogger.FormatHamster(_hamster)}\n" +
                    $"  step: {BotLogger.FormatStep(_step)}\n" +
                    $"  live obstacles: {BotLogger.FormatLiveObstacles(_hamster, _step.TargetObstacle.StableId)}");
                return;
            }

            if (ShouldDelayJumpOnTarget())
                return;

            if (ShouldDelayJumpOver())
                return;

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
                        $"[RESULT] SwitchLane completed\n" +
                        $"  hamster: {BotLogger.FormatHamster(_hamster)}\n" +
                        $"  step: {BotLogger.FormatStep(_step)}\n" +
                        $"  live obstacles: {BotLogger.FormatLiveObstacles(_hamster, _step.TargetObstacle.StableId)}");
                }
            }
            else
            {
                var state = _hamster.HamsterState.Value;
                if (state == HamsterStateEnum.Run)
                {
                    _step.Status = ChainStepStatus.Completed;
                    BotLogger.Log(BotLogLevel.Normal,
                        $"[RESULT] {_step.Action} completed\n" +
                        $"  hamster: {BotLogger.FormatHamster(_hamster)}\n" +
                        $"  step: {BotLogger.FormatStep(_step)}\n" +
                        $"  live obstacles: {BotLogger.FormatLiveObstacles(_hamster, _step.TargetObstacle.StableId)}");
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
                    _tryJumpAfterSwitchLane = ShouldTryJumpAfterSwitchLane();
                    _jumpAfterSwitchLaneFired = false;
                    break;
                case BotAction.Jump:
                    _hamster.JumpRequest.Invoke();
                    break;
                case BotAction.SuperJump:
                    _hamster.SuperJumpRequest.Invoke();
                    break;
            }

            _step.Status = ChainStepStatus.InProgress;
            _stepStartedAt = Time.time;
            _nextStallLogAt = Time.time + GetStallThreshold(_step.Action);
            BotLogger.Log(BotLogLevel.Normal,
                $"[EXECUTE] {_step.Action}: liveDist={liveDist:F2} → FIRE\n" +
                $"  hamster: {BotLogger.FormatHamster(_hamster)}\n" +
                $"  step: {BotLogger.FormatStep(_step)}\n" +
                $"  live obstacles: {BotLogger.FormatLiveObstacles(_hamster, _step.TargetObstacle.StableId)}");
        }

        private bool ShouldTryJumpAfterSwitchLane()
        {
            if (_step == null || _step.Action != BotAction.SwitchLane)
                return false;

            if (_hamster.Energy.Value < ActionGenerator.JumpEnergyCost)
                return false;

            if (_step.TargetObstacle.Category == ObjectCategory.Target &&
                _step.TargetObstacle.Type == ObstacleTypeEnum.smallAlive)
                return true;

            return FindCloseSameLaneSmallAlive(out _);
        }

        private void TryFireJumpAfterSwitchLane()
        {
            if (!_tryJumpAfterSwitchLane || _jumpAfterSwitchLaneFired)
                return;

            if (_step == null || _step.Action != BotAction.SwitchLane)
                return;

            if (_hamster.Energy.Value < ActionGenerator.JumpEnergyCost)
                return;

            if (_hamster.HamsterState.Value != HamsterStateEnum.Run)
                return;

            if (!FindCloseSameLaneSmallAlive(out float liveDist))
                return;

            _hamster.JumpRequest.Invoke();
            _jumpAfterSwitchLaneFired = true;
            BotLogger.Log(BotLogLevel.Normal,
                $"[EXECUTE] Jump-after-switch: liveDist={liveDist:F2} → FIRE\n" +
                $"  hamster: {BotLogger.FormatHamster(_hamster)}\n" +
                $"  step: {BotLogger.FormatStep(_step)}\n" +
                $"  live obstacles: {BotLogger.FormatLiveObstacles(_hamster, _step.TargetObstacle.StableId)}");
        }

        private bool FindCloseSameLaneSmallAlive(out float bestDist)
        {
            bestDist = float.MaxValue;
            var spawner = ObstacleSpawner.Instance;
            if (spawner == null)
                return false;

            bool hamsterOnBottom = _hamster.IsOnBottomLine.Value;
            var spawned = spawner.SpawnedObstacles;
            for (int i = 0; i < spawned.Count; i++)
            {
                var inst = spawned[i];
                if (inst?.ObstacleScript == null)
                    continue;

                var obstacle = inst.ObstacleScript;
                if (obstacle.ObstacleType.ObstacleTypeEnum != ObstacleTypeEnum.smallAlive)
                    continue;

                bool obstacleOnBottom = !obstacle.ObstacleType.IsTop;
                if (obstacleOnBottom != hamsterOnBottom)
                    continue;

                float leftX = obstacle.transform.position.x - obstacle.ColliderWidth * 0.5f;
                float dist = leftX - _hamster.RightX;
                if (dist < JumpAfterSwitchMinDist || dist > JumpAfterSwitchMaxDist)
                    continue;

                if (dist < bestDist)
                    bestDist = dist;
            }

            return bestDist != float.MaxValue;
        }

        /// <summary>
        /// Получает текущую живую дистанцию до объекта через ObstacleSpawner.
        /// При отсутствии объекта возвращает snapshot-дистанцию.
        /// </summary>
        private float GetLiveDistance(ObstacleInfo target)
        {
            var liveObstacle = FindLiveObstacle(target.StableId);
            if (liveObstacle != null)
            {
                float leftX = liveObstacle.transform.position.x
                            - liveObstacle.ColliderWidth * 0.5f;
                return leftX - _hamster.RightX;
            }

            // Объект не найден (уже убран со сцены)
            return target.DistanceToHamster;
        }

        private bool ShouldDelayJumpOver()
        {
            if (_step == null || (_step.Action != BotAction.Jump && _step.Action != BotAction.SuperJump))
                return false;

            if (_step.TargetObstacle.Category == ObjectCategory.Target)
                return false;

            var type = _step.TargetObstacle.Type;
            if (type != ObstacleTypeEnum.smallNotAliveRoad &&
                type != ObstacleTypeEnum.smallNotAliveRoadAndRoof &&
                type != ObstacleTypeEnum.smallAlive)
                return false;

            var liveObstacle = FindLiveObstacle(_step.TargetObstacle.StableId);
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

            if (liveDist <= 0.1f)
                return false;

            BotLogger.Log(BotLogLevel.Verbose,
                $"[EXECUTE] {_step.Action} delayed — overlap predicted, liveDist={liveDist:F2}\n" +
                $"  hamster: {BotLogger.FormatHamster(_hamster)}\n" +
                $"  step: {BotLogger.FormatStep(_step)}\n" +
                $"  live obstacles: {BotLogger.FormatLiveObstacles(_hamster, _step.TargetObstacle.StableId)}");
            return true;
        }

        private bool ShouldDelayJumpOnTarget()
        {
            if (_step == null || _step.Action != BotAction.Jump)
                return false;

            var target = _step.TargetObstacle;
            if (target.Category != ObjectCategory.Target)
                return false;

            if (target.Type != ObstacleTypeEnum.smallAlive)
                return false;

            var liveObstacle = FindLiveObstacle(target.StableId);
            if (liveObstacle == null)
                return false;

            EnsureJumpWorldShiftCached();
            if (_jumpWorldShift <= 0f)
                return false;

            float rightTolerance = _hamster.ColliderWidth * JumpOnRightToleranceRatio;
            bool wouldLandOn = CollisionUtils.IsHamsterCenterInsideObstacleAtShift(
                _hamster.transform,
                _jumpWorldShift,
                liveObstacle,
                rightTolerance);

            if (wouldLandOn)
                return false;

            float liveDist = liveObstacle.transform.position.x
                           - liveObstacle.ColliderWidth * 0.5f
                           - _hamster.RightX;

            if (liveDist <= JumpOnLateFallbackDistance)
                return false;

            BotLogger.Log(BotLogLevel.Verbose,
                $"[EXECUTE] Jump delayed — waiting JumpOn window, liveDist={liveDist:F2}\n" +
                $"  hamster: {BotLogger.FormatHamster(_hamster)}\n" +
                $"  step: {BotLogger.FormatStep(_step)}\n" +
                $"  live obstacles: {BotLogger.FormatLiveObstacles(_hamster, _step.TargetObstacle.StableId)}");
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

        private static Obstacle FindLiveObstacle(int stableId)
        {
            var spawner = ObstacleSpawner.Instance;
            if (spawner == null)
                return null;

            var spawned = spawner.SpawnedObstacles;
            for (int i = 0; i < spawned.Count; i++)
            {
                var inst = spawned[i];
                if (inst?.ObstacleScript == null)
                    continue;

                if (inst.ObstacleScript.GetInstanceID() == stableId)
                    return inst.ObstacleScript;
            }

            return null;
        }

        /// <summary>
        /// Живая проверка immediate safety для SwitchLane.
        /// Возвращает false только если сам манёвр приведёт к столкновению во время shift
        /// или ровно в момент его завершения.
        /// </summary>
        private bool IsSwitchLaneImmediatelySafeNow()
        {
            return SwitchLaneSafety.IsImmediatelySafe(_hamster, 0);
        }

        private bool IsLifeCollectibleSwitchLane()
        {
            return _step != null &&
                   _step.Action == BotAction.SwitchLane &&
                   _step.TargetObstacle.Category == ObjectCategory.Collectible &&
                   _step.TargetObstacle.Type == ObstacleTypeEnum.collectableLife;
        }

        private void LogIfActionLooksStuck()
        {
            if (_step == null || _stepStartedAt < 0f || Time.time < _nextStallLogAt)
                return;

            float elapsed = Time.time - _stepStartedAt;
            _nextStallLogAt = Time.time + 1f;

            BotLogger.Log(BotLogLevel.Normal,
                $"[STALL] {_step.Action} still in progress after {elapsed:F2}s\n" +
                $"  hamster: {BotLogger.FormatHamster(_hamster)}\n" +
                $"  step: {BotLogger.FormatStep(_step)}\n" +
                $"  live obstacles: {BotLogger.FormatLiveObstacles(_hamster, _step.TargetObstacle.StableId)}");
        }

        private static float GetStallThreshold(BotAction action)
        {
            switch (action)
            {
                case BotAction.SwitchLane:
                    return 0.6f;
                case BotAction.Jump:
                    return 2f;
                case BotAction.SuperJump:
                    return 2.5f;
                default:
                    return 2f;
            }
        }
    }
}
