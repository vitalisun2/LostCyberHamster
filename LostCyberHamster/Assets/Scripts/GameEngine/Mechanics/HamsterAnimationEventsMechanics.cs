using Assets.Scripts.GameEngine.Controllers;
using Assets.Scripts.Gameplay;
using Assets.Scripts.Gameplay.Enums;
using Atomic.Elements;
using UnityEngine;

namespace Assets.Scripts.GameEngine.Mechanics
{
    public class HamsterAnimationEventsMechanics
    {
        private TransformAnimatorEventsDispatcher _transformAnimatorEventsDispatcher;
        private SpriteAnimatorEventsDispatcher _spriteAnimatorEventsDispatcher;
        private readonly AtomicVariable<HamsterStateEnum> _hamsterState;
        private readonly AtomicVariable<bool> _isDamaged;
        private readonly AtomicVariable<bool> _needCheckCollisionInRunFromRoofAfterShift;
        private readonly AtomicEvent _jumpOverEvent;
        private readonly AtomicEvent<Obstacle> _destroyObstacleEvent;
        private readonly AtomicVariable<Obstacle> _pendingJumpedOnObstacle;
        private readonly AtomicEvent _damageEvent;

        public HamsterAnimationEventsMechanics(TransformAnimatorEventsDispatcher transformAnimatorEventsDispatcher,
            SpriteAnimatorEventsDispatcher spriteAnimatorEventsDispatcher,
            AtomicVariable<HamsterStateEnum> hamsterState,
            AtomicVariable<bool> isDamaged,
            AtomicVariable<bool> needCheckCollisionInRunFromRoofAfterShift,
            AtomicEvent jumpOverEvent,
            AtomicEvent<Obstacle> destroyObstacleEvent,
            AtomicVariable<Obstacle> pendingJumpedOnObstacle,
            AtomicEvent damageEvent)
        {
            _transformAnimatorEventsDispatcher = transformAnimatorEventsDispatcher;
            _hamsterState = hamsterState;
            _isDamaged = isDamaged;
            _needCheckCollisionInRunFromRoofAfterShift = needCheckCollisionInRunFromRoofAfterShift;
            _jumpOverEvent = jumpOverEvent;
            _destroyObstacleEvent = destroyObstacleEvent;
            _pendingJumpedOnObstacle = pendingJumpedOnObstacle;
            _spriteAnimatorEventsDispatcher = spriteAnimatorEventsDispatcher;
            _damageEvent = damageEvent;
        }

        public void OnEnable()
        {
            _transformAnimatorEventsDispatcher.OnEvent += OnEvent;
            _spriteAnimatorEventsDispatcher.OnEvent += OnEvent;
        }

        public void OnDisable()
        {
            _transformAnimatorEventsDispatcher.OnEvent -= OnEvent;
            _spriteAnimatorEventsDispatcher.OnEvent -= OnEvent;
        }

        private void OnEvent(string animEvent)
        {
            if (animEvent == "transform_jumped_on")
            {
                CompletePendingJumpedOnObstacle();
            }

            JumpEvents(animEvent);

            if (animEvent == "transform_jump_on_roof_end")
            {
                _pendingJumpedOnObstacle.Value = null;

                if (_hamsterState.Value == HamsterStateEnum.JumpOnRoofDamage ||
                    _hamsterState.Value == HamsterStateEnum.SuperJumpOnRoofDamage)
                    _damageEvent?.Invoke();

                _hamsterState.Value = HamsterStateEnum.RoofRun;
            }

            if (animEvent == "transform_roof_jump_end")
            {
                _pendingJumpedOnObstacle.Value = null;

                if(_hamsterState.Value == HamsterStateEnum.RoofJumpDamage ||
                   _hamsterState.Value == HamsterStateEnum.SuperRoofJumpDamage)
                    _damageEvent?.Invoke();

                _hamsterState.Value = HamsterStateEnum.RoofRun;
            }

            if (animEvent == "transform_jump_from_roof_end")
            {
                _pendingJumpedOnObstacle.Value = null;

                if (_hamsterState.Value == HamsterStateEnum.JumpFromRoofDamage ||
                    _hamsterState.Value == HamsterStateEnum.SuperJumpFromRoofDamage)
                    _damageEvent?.Invoke();

                _hamsterState.Value = HamsterStateEnum.Run;
            }

            if (animEvent == "sprite_blink_end")
                _isDamaged.Value = false;

            if (animEvent == "check_collision_in_run_from_roof_after_shift")
                _needCheckCollisionInRunFromRoofAfterShift.Value = true;
        }

        private void CompletePendingJumpedOnObstacle()
        {
            if (!IsJumpOnObstacleState())
            {
                _pendingJumpedOnObstacle.Value = null;
                return;
            }

            Obstacle obstacle = _pendingJumpedOnObstacle.Value;
            if (obstacle == null)
                return;

            _destroyObstacleEvent?.Invoke(obstacle);
            _pendingJumpedOnObstacle.Value = null;
        }

        private bool IsJumpOnObstacleState()
        {
            return _hamsterState.Value == HamsterStateEnum.JumpOnObstacle
                   || _hamsterState.Value == HamsterStateEnum.SuperJumpOnObstacle
                   || _hamsterState.Value == HamsterStateEnum.JumpOnObstacleFromRoof
                   || _hamsterState.Value == HamsterStateEnum.SuperJumpOnObstacleFromRoof;
        }

        private void JumpEvents(string animEvent)
        {
            if (animEvent == "transform_jump_end")
            {
                if (_hamsterState.Value == HamsterStateEnum.JumpOver
                 || _hamsterState.Value == HamsterStateEnum.SuperJumpOver)
                {
                    _jumpOverEvent?.Invoke();
                }

                if (_hamsterState.Value == HamsterStateEnum.JumpDamageForSmallNotAlive
                    || _hamsterState.Value == HamsterStateEnum.JumpDamageForSmallAlive
                    || _hamsterState.Value == HamsterStateEnum.SuperJumpDamage)
                {
                    _damageEvent?.Invoke();
                }

                _pendingJumpedOnObstacle.Value = null;
                _hamsterState.Value = HamsterStateEnum.Run;
            }

            if (animEvent == "transform_jump_mid")
            {
                if (_hamsterState.Value == HamsterStateEnum.JumpDamageForBigAlive)
                    _damageEvent?.Invoke();
            }
        }
    }
}
