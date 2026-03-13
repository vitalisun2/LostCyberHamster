using System.Collections;
using Assets.Scripts.Common;
using Assets.Scripts.Gameplay;
using Assets.Scripts.Gameplay.Enums;
using Assets.Scripts.GameEngine.Controllers;
using Assets.Scripts.System;
using Assets.Scripts.Common.Models;
using UnityEngine;

namespace Assets.Scripts.Bot
{
    /// <summary>
    /// Отвечает за точный тайминг и исполнение шагов из CurrentPlan.
    /// Переносит логику ShouldDelay*, ExecuteAction, EnsureWorldShiftsCached
    /// из HamsterBot и управляет жизненным циклом шага:
    /// Ready → InProgress → (ожидание Run/RoofRun) → Completed.
    /// Требует MonoBehaviour-owner для запуска корутин.
    /// </summary>
    public class BotTimingPolicy
    {
        // ──────────────── Зависимости ────────────────

        private readonly MonoBehaviour _owner; // для StartCoroutine (SuperJump/SuperRoofJump)
        private readonly Hamster       _hamster;

        // ──────────────── Кэш worldShift ────────────────

        private float _jumpWorldShift     = -1f;
        private float _roofJumpWorldShift = -1f;

        // ──────────────── Счётчик действий ────────────────

        public int ActionsExecuted { get; private set; }

        // ══════════════════════════════════════════════
        //  Конструктор
        // ══════════════════════════════════════════════

        public BotTimingPolicy(MonoBehaviour owner, Hamster hamster)
        {
            _owner   = owner;
            _hamster = hamster;
        }

        /// <summary>Сбросить счётчик и кэш при рестарте уровня.</summary>
        public void Reset()
        {
            ActionsExecuted   = 0;
            _jumpWorldShift   = -1f;
            _roofJumpWorldShift = -1f;
        }

        // ══════════════════════════════════════════════
        //  Публичный API
        // ══════════════════════════════════════════════

        /// <summary>
        /// Пытается исполнить головной шаг плана.
        /// Возвращает true, если действие отправлено в этом кадре.
        /// </summary>
        public bool TryExecuteHead(CurrentPlan plan)
        {
            if (plan == null || plan.IsEmpty) return false;

            var step = plan.Head;
            if (step == null) return false;

            // ── InProgress: ждём завершения (хомяк вернулся в Run/RoofRun) ──
            if (step.Status == ChainStepStatus.InProgress)
            {
                var state = _hamster.HamsterState.Value;
                if (state == HamsterStateEnum.Run || state == HamsterStateEnum.RoofRun)
                    step.Status = ChainStepStatus.Completed;
                return false;
            }

            // ── Completed уже обработан CurrentPlan.RemoveCompletedFromHead() ──
            if (step.Status == ChainStepStatus.Completed) return false;

            // ── Ready: проверяем тайминг ──
            if (step.Action == BotAction.None) return false;

            // Если есть привязанная цель — ждём нужной дистанции
            if (step.TargetObstacle.HasValue)
            {
                var target = step.TargetObstacle.Value;

                // Live distance from ObstacleRef (snapshot values stale by 1 frame)
                float distance = target.DistanceToHamster;
                if (target.ObstacleRef != null)
                {
                    float obsLeftX = target.ObstacleRef.transform.position.x
                                   - target.ObstacleRef.ColliderWidth * 0.5f;
                    distance = obsLeftX - _hamster.RightX;
                }

                // Too late: obstacle already behind hamster → skip step
                if (distance < -0.3f)
                {
                    step.Status = ChainStepStatus.Completed;
                    return false;
                }

                if (distance > step.ExecuteAtDistance)
                    return false; // ещё далеко

                if (ShouldDelayJumpOver(step, target)) return false;
                if (ShouldDelayJumpOn(step, target))   return false;
            }

            // Final safety: re-check hamster state right before execution
            // (state may have changed since the IsControllableState check at top of Update)
            var preExecState = _hamster.HamsterState.Value;
            if (preExecState != HamsterStateEnum.Run && preExecState != HamsterStateEnum.RoofRun)
                return false;

            // Выполняем
            ExecuteAction(step);
            step.Status = ChainStepStatus.InProgress;
            return true;
        }

        // ══════════════════════════════════════════════
        //  Исполнение действий
        // ══════════════════════════════════════════════

        private void ExecuteAction(ChainStep step)
        {
            switch (step.Action)
            {
                case BotAction.Jump:
                    // Safety: если уже на крыше — используем RoofJump
                    if (_hamster.HamsterState.Value == HamsterStateEnum.RoofRun)
                        _hamster.RoofJumpRequest.Invoke();
                    else
                        _hamster.JumpRequest.Invoke();
                    break;

                case BotAction.SuperJump:
                    // double-tap: сначала Jump → hamster_jump, потом SuperJump
                    _hamster.JumpRequest.Invoke();
                    _owner.StartCoroutine(DelayedSuperJump());
                    break;

                case BotAction.RoofJump:
                    _hamster.RoofJumpRequest.Invoke();
                    break;

                case BotAction.SuperRoofJump:
                    // double-tap on roof: сначала RoofJump, потом SuperRoofJump
                    _hamster.RoofJumpRequest.Invoke();
                    _owner.StartCoroutine(DelayedSuperRoofJump());
                    break;

                case BotAction.SwitchLane:
                    _hamster.TapRequest.Invoke();
                    break;

                case BotAction.UseUlta:
                    _hamster.UltaEvent.Invoke();
                    break;
            }

            ActionsExecuted++;

            string nearInfo = "";
            if (step.TargetObstacle.HasValue)
            {
                var t = step.TargetObstacle.Value;
                // Use live distance from ObstacleRef when available
                float dist = t.DistanceToHamster;
                if (t.ObstacleRef != null)
                {
                    float obsLeftX = t.ObstacleRef.transform.position.x
                                   - t.ObstacleRef.ColliderWidth * 0.5f;
                    dist = obsLeftX - _hamster.RightX;
                }
                nearInfo = $" | {t.Type}({t.Category}) liveDist={dist:F2}";
            }

            DebugManager.DiagLog(
                $"[TimingPolicy] EXEC #{ActionsExecuted}: {step.Action} " +
                $"state={_hamster.HamsterState.Value} pos={_hamster.RightX:F2}{nearInfo}");
        }

        private IEnumerator DelayedSuperJump()
        {
            yield return null;
            _hamster.SuperJumpRequest.Invoke();
        }

        private IEnumerator DelayedSuperRoofJump()
        {
            yield return null;
            _hamster.SuperRoofJumpRequest.Invoke();
        }

        // ══════════════════════════════════════════════
        //  Delay-проверки (тайминг прыжков)
        // ══════════════════════════════════════════════

        /// <summary>
        /// Для JumpOver: проверяет, что прыжок прямо сейчас не приведёт к налеганию на цель.
        /// Не применяется к JumpOn (там хотим приземлиться НА препятствие).
        /// </summary>
        private bool ShouldDelayJumpOver(ChainStep step, ObstacleInfo target)
        {
            if (step.Action != BotAction.Jump && step.Action != BotAction.RoofJump)
                return false;
            if (step.Reason != null && step.Reason.StartsWith("JumpOn"))
                return false;
            if (target.Type != ObstacleTypeEnum.smallNotAliveRoad &&
                target.Type != ObstacleTypeEnum.smallNotAliveRoadAndRoof &&
                target.Type != ObstacleTypeEnum.smallAlive)
                return false;

            var obsRef = target.ObstacleRef;
            if (obsRef == null) return false;

            EnsureWorldShiftsCached();
            float worldShift = step.Action == BotAction.RoofJump
                ? _roofJumpWorldShift : _jumpWorldShift;
            if (worldShift <= 0f) return false;

            bool wouldOverlap = CollisionUtils.IsOverlapAtShift(
                _hamster.transform, _hamster.ColliderWidth, worldShift, obsRef);

            if (!wouldOverlap) return false;

            // Failsafe: вплотную — прыгаем всё равно
            float realDist = obsRef.transform.position.x
                - obsRef.ColliderWidth * 0.5f - _hamster.RightX;
            return realDist > 0.1f;
        }

        /// <summary>
        /// Для JumpOn (Target): ждёт момент, когда центр хомяка попадёт внутрь цели.
        /// </summary>
        private bool ShouldDelayJumpOn(ChainStep step, ObstacleInfo target)
        {
            if (step.Action != BotAction.Jump && step.Action != BotAction.RoofJump)
                return false;
            if (step.Reason == null || !step.Reason.StartsWith("JumpOn"))
                return false;

            var obsRef = target.ObstacleRef;
            if (obsRef == null) return false;

            EnsureWorldShiftsCached();
            float worldShift = step.Action == BotAction.RoofJump
                ? _roofJumpWorldShift : _jumpWorldShift;
            if (worldShift <= 0f) return false;

            float rightTol = _hamster.ColliderWidth * 0.2f;
            bool wouldLandOn = CollisionUtils.IsHamsterCenterInsideObstacleAtShift(
                _hamster.transform, worldShift, obsRef, rightTol);

            if (wouldLandOn) return false; // идеальный момент — прыгаем

            // Failsafe: слишком близко — прыгаем всё равно
            float realDist = obsRef.transform.position.x
                - obsRef.ColliderWidth * 0.5f - _hamster.RightX;
            return realDist > 0.5f;
        }

        // ══════════════════════════════════════════════
        //  WorldShift кэш
        // ══════════════════════════════════════════════

        private void EnsureWorldShiftsCached()
        {
            if (_jumpWorldShift >= 0f) return;

            var ctrl = _hamster.GetComponentInChildren<TransformAnimatorController>();
            if (ctrl == null) return;

            _jumpWorldShift     = HelpMethods.GetWorldShiftForClip(ctrl, "transform_jump");
            _roofJumpWorldShift = HelpMethods.GetWorldShiftForClip(ctrl, "transform_roof_jump");

            DebugManager.DiagLog(
                $"[TimingPolicy] Cached worldShifts: jump={_jumpWorldShift:F2}, roofJump={_roofJumpWorldShift:F2}");
        }
    }
}
