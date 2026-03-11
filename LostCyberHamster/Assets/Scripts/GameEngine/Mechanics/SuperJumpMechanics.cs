using System;
using System.Collections.Generic;
using Assets.Scripts;
using Assets.Scripts.Common;
using Assets.Scripts.Common.Models;
using Assets.Scripts.GameEngine.Controllers;
using Assets.Scripts.GameEngine.Mechanics.Models;
using Assets.Scripts.Gameplay;
using Assets.Scripts.Gameplay.Enums;
using Assets.Scripts.System;
using Atomic.Elements;
using UnityEngine;

/// <summary>
/// Механика супер-прыжка.
/// На основе энергии, положения хомяка и расположения препятствий
/// вычисляет финальный <see cref="HamsterStateEnum"/> и запускает анимацию.
/// </summary>
public class SuperJumpMechanics
{
    // ──────────────────────── constants ────────────────────────
    private const int ENERGY_COST_SUPER_JUMP = 20;
    private const string CLIP_SUPER_JUMP = "transform_super_jump";
    private const float RIGHT_EDGE_TOL_RATIO = 0.2f; // 20 % ширины хомяка

    // ──────────────────────── injected refs ────────────────────
    private readonly AtomicEvent _superJumpRequest;
    private readonly AtomicVariable<int> _energy;
    private readonly AtomicVariable<bool> _isOnBottomLine;
    private readonly AtomicVariable<HamsterStateEnum> _hamsterState;
    private readonly AtomicVariable<bool> _isDamaged;
    private readonly TransformAnimatorController _transformAnimatorController;
    private readonly SpriteAnimatorController _spriteAnimatorController;
    private readonly Transform _characterTransform;
    private readonly AtomicVariable<Obstacle> _lastObstacle;

    // ──────────────────────── cached geometry ──────────────────
    private readonly float _hamsterWidth;
    private readonly float _superJumpShift;

    // список препятствий, уже лежащих на нужной линии
    private IReadOnlyList<Obstacle> _sameLineObstacles;

    // ──────────────────────── handlers ─────────────────────────
    private readonly Dictionary<ObstacleTypeEnum, Func<Obstacle, JumpResult>> _handlers;
    private readonly JumpResult _noHit = new(HamsterStateEnum.SuperJump, null);

    public SuperJumpMechanics(
        AtomicEvent superJumpRequest,
        AtomicVariable<int> energy,
        AtomicVariable<bool> isOnBottomLine,
        AtomicVariable<HamsterStateEnum> hamsterState,
        AtomicVariable<bool> isDamaged,
        TransformAnimatorController transformAnimatorController,
        SpriteAnimatorController spriteAnimatorController,
        Transform characterTransform,
        AtomicVariable<Obstacle> lastObstacle,
        float hamsterWidthInUnits)
    {
        _superJumpRequest = superJumpRequest;
        _energy = energy;
        _isOnBottomLine = isOnBottomLine;
        _hamsterState = hamsterState;
        _isDamaged = isDamaged;
        _transformAnimatorController = transformAnimatorController;
        _spriteAnimatorController = spriteAnimatorController;
        _characterTransform = characterTransform;
        _lastObstacle = lastObstacle;

        _hamsterWidth = hamsterWidthInUnits;
        _superJumpShift = HelpMethods.GetWorldShiftForClip(_transformAnimatorController, CLIP_SUPER_JUMP);

        // будет заполнено при вычислении состояния прыжка
        _sameLineObstacles = Array.Empty<Obstacle>();

        _handlers = new()
        {
            { ObstacleTypeEnum.bigNotAlive,              HandleBigNotAlive              },
            { ObstacleTypeEnum.mediumNotAlive,           HandleBigNotAlive              },
            { ObstacleTypeEnum.bigAlive,                 HandleBigAlive                 },
            { ObstacleTypeEnum.smallAlive,               HandleSmallAlive               },
            { ObstacleTypeEnum.smallNotAliveRoad,        HandleSmallNotAliveRoad        },
            { ObstacleTypeEnum.smallNotAliveRoadAndRoof, HandleSmallNotAliveRoadAndRoof }
        };
    }

    public void OnEnable() => _superJumpRequest.Subscribe(OnSuperJump);
    public void OnDisable() => _superJumpRequest.Unsubscribe(OnSuperJump);

    /// <summary>
    /// Точка входа для супер-прыжка:
    /// • проверяем энергию; • вычисляем финальный <see cref="HamsterStateEnum"/>;
    /// • отправляем события; • запускаем анимацию.
    /// </summary>
    private void OnSuperJump()
    {
        if (_energy.Value < ENERGY_COST_SUPER_JUMP) return;

        JumpResult result = CalculateSuperJumpState();

        _hamsterState.Value = result.State;
        if (result.Target != null) _lastObstacle.Value = result.Target;

        if (result.State == HamsterStateEnum.SuperJumpOnObstacle)
            GameEventsManager.ObstacleJumpedOn(result.Target!.name);

        if (result.State == HamsterStateEnum.SuperJumpOver)
            GameEventsManager.ObstacleJumpedOver(result.Target!.name);

        SwapRoofClipsIfNeeded(result);
        _transformAnimatorController.SetSuperJumpAnimationTrigger(_hamsterState);
        _spriteAnimatorController.Jump();
    }

    private void SwapRoofClipsIfNeeded(JumpResult result)
    {
        bool isMedium = result.Target != null &&
                        result.Target.ObstacleType.ObstacleTypeEnum == ObstacleTypeEnum.mediumNotAlive;
        _transformAnimatorController.SwapRoofClips(isMedium);
    }

    /// <summary>
    /// Обходит препятствия впереди хомяка и, через словарь «тип → хэндлер»,
    /// определяет итог супер-прыжка.
    /// </summary>
    private JumpResult CalculateSuperJumpState()
    {
        if (_isDamaged.Value) return _noHit;

        var obstacles = CollisionUtils.GetValidObstaclesAhead(_characterTransform, _isOnBottomLine.Value);
        _sameLineObstacles = obstacles;

        float reachShift = _superJumpShift;
        JumpResult overResult = _noHit; // сохраняем Over, если встретится

        foreach (var obs in obstacles)
        {
            if (CollisionUtils.ShouldBreakByReachRight(_characterTransform, _hamsterWidth, reachShift, obs))
                break;

            if (_handlers.TryGetValue(obs.ObstacleType.ObstacleTypeEnum, out var handler))
            {
                var res = handler(obs);

                if (res.State == HamsterStateEnum.SuperJumpOver) // Over — запоминаем и ищем дальше
                {
                    overResult = res;
                    continue;
                }

                if (res.State != _noHit.State) // любой другой результат — сразу возврат
                    return res;
            }
        }

        return overResult; // вернём Over, если ничего важнее не нашли
    }

    // ──────────────────────── handlers ─────────────────────────

    private JumpResult HandleBigNotAlive(Obstacle obs)
    {
        if (CollisionUtils.IsOverlapAtShift(_characterTransform,
                                            _hamsterWidth,
                                            _superJumpShift,
                                            obs))
        {
            bool hitSmall = CollisionUtils.IsHitSmallNotAliveOnRoof(_characterTransform, _hamsterWidth, _superJumpShift, _sameLineObstacles);
            var state = hitSmall ? HamsterStateEnum.SuperJumpOnRoofDamage : HamsterStateEnum.SuperJumpOnRoof;
            return new JumpResult(state, obs);
        }

        return _noHit;
    }

    private JumpResult HandleBigAlive(Obstacle obs)
    {
        if (CollisionUtils.IsOverlapAtShift(_characterTransform,
                                            _hamsterWidth,
                                            _superJumpShift,
                                            obs))
            return new JumpResult(HamsterStateEnum.SuperJumpDamage, obs);

        if (CollisionUtils.IsJumpOver(_characterTransform, _hamsterWidth, _superJumpShift, obs))
            return new JumpResult(HamsterStateEnum.SuperJumpOver, obs);

        return _noHit;
    }

    private JumpResult HandleSmallAlive(Obstacle obs)
    {
        // 1. Центр внутри границ препятствия? → удачный напрыг
        float rightTol = _hamsterWidth * RIGHT_EDGE_TOL_RATIO;
        if (CollisionUtils.IsHamsterCenterInsideObstacleAtShift(
                _characterTransform,
                _superJumpShift,
                obs,
                rightTol))
            return new JumpResult(HamsterStateEnum.SuperJumpOnObstacle, obs);

        // 2. Иначе: есть ли вообще X-пересечение? → урон
        if (CollisionUtils.IsOverlapAtShift(
                _characterTransform,
                _hamsterWidth,
                _superJumpShift,
                obs))
            return new JumpResult(HamsterStateEnum.SuperJumpDamage, obs);

        // 3. Проверяем, перепрыгнули ли полностью
        if (CollisionUtils.IsJumpOver(
                _characterTransform,
                _hamsterWidth,
                _superJumpShift,
                obs))
            return new JumpResult(HamsterStateEnum.SuperJumpOver, obs);

        // 4. Вообще не задели
        return _noHit;
    }

    private JumpResult HandleSmallNotAliveRoad(Obstacle obs)
    {
        if (CollisionUtils.IsOverlapAtShift(_characterTransform,
                                            _hamsterWidth,
                                            _superJumpShift,
                                            obs))
            return new JumpResult(HamsterStateEnum.SuperJumpDamage, obs);

        if (CollisionUtils.IsJumpOver(_characterTransform, _hamsterWidth, _superJumpShift, obs))
            return new JumpResult(HamsterStateEnum.SuperJumpOver, obs);

        return _noHit;
    }

    private JumpResult HandleSmallNotAliveRoadAndRoof(Obstacle small)
    {
        // ──────────────────────────────────────────────────────────────
        // 1. Сначала проверяем: «Врезались ли мы в коробку к концу прыжка?»
        // ──────────────────────────────────────────────────────────────
        bool isOverlapSmall = CollisionUtils.IsOverlapAtShift(
                                  _characterTransform, _hamsterWidth, _superJumpShift, small);

        // Если прямого столкновения нет, спрашиваем: «Перепрыгнули ли полностью?»
        if (!isOverlapSmall)
        {
            bool isJumpOverSmall = CollisionUtils.IsJumpOver(
                                       _characterTransform, _hamsterWidth, _superJumpShift, small);

            // Ни столкновения, ни перелёта → коробка никак не затрагивает прыжок
            if (!isJumpOverSmall)
                return _noHit;
            // Перелёт возможен, но нужна ещё проверка «коробка на крыше» — продолжаем логику ниже
        }

        // ──────────────────────────────────────────────────────────────
        // 2. Проверяем сценарий «коробка стоит на крыше большой машины».
        //    Если BigNotAlive найден и мы реально садимся на крышу,
        //    то работаем как с приземлением на крышу.
        // ──────────────────────────────────────────────────────────────
        if (CollisionUtils.TryFindBigNotAliveUnderSmallNotAlive(
                small, _sameLineObstacles, out var big) &&
            CollisionUtils.IsOverlapAtShift(
                _characterTransform, _hamsterWidth, _superJumpShift, big))
        {
            bool hitSmallOnRoof = CollisionUtils.IsHitSmallNotAliveOnRoof(
                                      _characterTransform, _hamsterWidth, _superJumpShift, _sameLineObstacles);

            var state = hitSmallOnRoof
                        ? HamsterStateEnum.SuperJumpOnRoofDamage   // зацепили коробку → урон
                        : HamsterStateEnum.SuperJumpOnRoof;        // чистое приземление

            return new JumpResult(state, big);
        }

        // ──────────────────────────────────────────────────────────────
        // 3. Сценарий «коробка на дороге».
        //    Если столкнулись — урон; если нет → перепрыгнули.
        // ──────────────────────────────────────────────────────────────
        return isOverlapSmall
               ? new JumpResult(HamsterStateEnum.SuperJumpDamage, small) // наехали на коробку
               : new JumpResult(HamsterStateEnum.SuperJumpOver, small); // перелетели коробку
    }


}
