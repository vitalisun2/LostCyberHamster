using System;
using System.Collections.Generic;
using System.Linq;
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
    private const int ENERGY_COST_SUPER_JUMP = 10;
    private const string CLIP_SUPER_JUMP = "transform_super_jump";

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
    private List<Obstacle> _sameLineObstacles;

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
        _sameLineObstacles = new List<Obstacle>();

        _handlers = new()
        {
            { ObstacleTypeEnum.bigNotAlive,              HandleBigNotAlive              },
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

        _transformAnimatorController.SetSuperJumpAnimationTrigger(_hamsterState);
        _spriteAnimatorController.Jump();
    }

    /// <summary>
    /// Обходит препятствия впереди хомяка и, через словарь «тип → хэндлер»,
    /// определяет итог супер-прыжка.
    /// </summary>
    private JumpResult CalculateSuperJumpState()
    {
        if (_isDamaged.Value)
            return _noHit;

        _sameLineObstacles = ObstacleSpawner.Instance.SpawnedObstacles
            .Select(io => io.ObstacleScript)
            .Where(o => HelpMethods.IsOnSameLine(_isOnBottomLine.Value, o))
            .OrderBy(o => o.transform.position.x) // важно для корректного break
            .ToList();

        float hamsterX = _characterTransform.position.x;
        float reachShift = Mathf.Abs(_superJumpShift);                 // дальность по X

        foreach (var obs in _sameLineObstacles)
        {
            if (obs.transform.position.x <= hamsterX) continue;

            // корректный ранний выход: левый край препятствия правее максимально достижимого правого края хомяка
            if (CollisionUtils.ShouldBreakByReachRight(_characterTransform, _hamsterWidth, reachShift, obs))
                break;

            if (_handlers.TryGetValue(obs.ObstacleType.ObstacleTypeEnum, out var handler))
            {
                var res = handler(obs);
                if (res.State != _noHit.State) return res;
            }
        }

        return _noHit;
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

        if (IsJumpOver(obs))
            return new JumpResult(HamsterStateEnum.SuperJumpOver, obs);

        return _noHit;
    }

    private JumpResult HandleSmallAlive(Obstacle obs)
    {
        if (CollisionUtils.IsOverlapAtShift(_characterTransform,
                                            _hamsterWidth,
                                            _superJumpShift,
                                            obs))
            return new JumpResult(HamsterStateEnum.SuperJumpOnObstacle, obs);

        if (IsJumpOver(obs))
            return new JumpResult(HamsterStateEnum.SuperJumpOver, obs);

        return _noHit;
    }

    private JumpResult HandleSmallNotAliveRoad(Obstacle obs)
    {
        if (CollisionUtils.IsOverlapAtShift(_characterTransform,
                                            _hamsterWidth,
                                            _superJumpShift,
                                            obs))
            return new JumpResult(HamsterStateEnum.SuperJumpDamage, obs);

        if (IsJumpOver(obs))
            return new JumpResult(HamsterStateEnum.SuperJumpOver, obs);

        return _noHit;
    }

    private JumpResult HandleSmallNotAliveRoadAndRoof(Obstacle small)
    {
        if (CollisionUtils.IsOverlapAtShift(_characterTransform,
                                            _hamsterWidth,
                                            _superJumpShift,
                                            small))
        {
            if (CollisionUtils.TryFindBigNotAliveUnderSmallNotAlive(small,
                                                                   _sameLineObstacles,
                                                                   out var big))
            {
                bool hitSmall = CollisionUtils.IsHitSmallNotAliveOnRoof(_characterTransform, _hamsterWidth, _superJumpShift, _sameLineObstacles);
                var state = hitSmall ? HamsterStateEnum.SuperJumpOnRoofDamage : HamsterStateEnum.SuperJumpOnRoof;
                return new JumpResult(state, big);
            }

            return new JumpResult(HamsterStateEnum.SuperJumpDamage, small);
        }

        if (IsJumpOver(small))
            return new JumpResult(HamsterStateEnum.SuperJumpOver, small);

        return _noHit;
    }

    // ──────────────────────── helpers ──────────────────────────

    /// <summary>True, если хомяк полностью перелетает obstacle без оверлапа.</summary>
    private bool IsJumpOver(Obstacle obs)
    {
        CollisionUtils.GetObstacleXInterval(obs, obs.ColliderWidth, out var oL, out var oR);

        float hx = _characterTransform.position.x;
        float hHalf = _hamsterWidth * 0.5f;
        float hStartL = hx - hHalf;
        float hStartR = hx + hHalf;

        CollisionUtils.GetHamsterXIntervalAtJumpEnd(_characterTransform, _hamsterWidth,
                                                    _superJumpShift,
                                                    out var hEndL, out var hEndR);

        return CollisionUtils.IsJumpOverIntervals(hStartL, hStartR,
                                                  hEndL, hEndR,
                                                  oL, oR,
                                                  0f);
    }

}
