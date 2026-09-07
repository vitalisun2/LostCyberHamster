using Assets.Scripts.Bot;
using Assets.Scripts.Common;
using Assets.Scripts.Gameplay;
using Assets.Scripts.Gameplay.Enums;
using Atomic.Elements;
using LostCyberHamster.UI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Assets.Scripts.GameEngine.Mechanics
{
    public class KeyboardMechanics
    {
        private readonly AtomicVariable<HamsterStateEnum> _characterHamsterState;
        private readonly AtomicEvent _roofJumpRequest;
        private readonly AtomicEvent _superRoofJumpRequest;
        private readonly AtomicEvent _jumpEvent;
        private readonly AtomicEvent _superJumpEvent;
        private readonly Keyboard _keyboard;
        private readonly AtomicEvent _tapRequest;
        private readonly Hamster _character;
        private readonly GameScreenController _gameScreenController;
        private DoubleJumpDetector _doubleJumpDetector;
        private bool _wasSkateboardActive;

        public KeyboardMechanics(Hamster hamster, UIManager uiManager)
        {
            _characterHamsterState = hamster.HamsterState;
            _character = hamster;
            _roofJumpRequest = hamster.RoofJumpRequest;
            _superRoofJumpRequest = hamster.SuperRoofJumpRequest;
            _jumpEvent = hamster.JumpRequest;
            _superJumpEvent = hamster.SuperJumpRequest;
            _keyboard = Keyboard.current;
            _tapRequest = hamster.TapRequest;

            _gameScreenController =
                uiManager.GetController<GameScreenController>();

            _doubleJumpDetector = new DoubleJumpDetector();
            _doubleJumpDetector.Reset();
            _wasSkateboardActive = hamster.ActorSwitcher.IsSkateboardActive;
        }

        public void Subscribe()
        {
            // No subscription needed for keyboard input, handled in OnUpdate
        }

        public void Unsubscribe()
        {
            // No unsubscription needed
        }

        public void OnUpdate()
        {
            ResetJumpSequenceIfModeChanged();

            // Пауза доступна в обучении через тот же маршрут, что и кнопка HUD.
            if (_keyboard != null && _keyboard.escapeKey.wasPressedThisFrame)
            {
                _gameScreenController.RequestPause();
                return;
            }

            if (GameplayInputGate.IsBlocked || _keyboard == null)
            {
                return;
            }

            if (_keyboard.upArrowKey.wasPressedThisFrame || _keyboard.downArrowKey.wasPressedThisFrame)
            {
                OnShift();
            }

            if (_keyboard.spaceKey.wasPressedThisFrame)
            {
                bool isDoubleJump = _doubleJumpDetector.RegisterJump();

                 if (isDoubleJump)
                {
                    OnSuperJump();
                }
                else
                {
                    OnJump();
                }
            }

            if (_keyboard.bKey.wasPressedThisFrame)
            {
                _gameScreenController.TryActivateUltra();
            }

            // Bot hotkey
            if (_keyboard.f1Key.wasPressedThisFrame)
            {
                var bot = Object.FindAnyObjectByType<RuntimeBotController>();
                if (bot != null)
                {
                    bot.ToggleEnabled();
                }
            }
        }

        private void OnShift()
        {
            _tapRequest?.Invoke();
        }

        private void OnJump()
        {
            if (_character.ActorSwitcher.IsSkateboardActive)
            {
                _jumpEvent?.Invoke();
                return;
            }

            if (_characterHamsterState.Value == HamsterStateEnum.RoofRun)
                _roofJumpRequest.Invoke();

            if (_characterHamsterState.Value == HamsterStateEnum.Run ||
                _character.IsDamaged.Value)
                _jumpEvent?.Invoke();
        }

        private void OnSuperJump()
        {
            if (_character.ActorSwitcher.IsSkateboardActive)
            {
                _superJumpEvent?.Invoke();
                return;
            }

            if(_characterHamsterState.Value == HamsterStateEnum.RoofJump ||
                _characterHamsterState.Value == HamsterStateEnum.RoofJumpDamage ||
                _characterHamsterState.Value == HamsterStateEnum.JumpFromRoof ||
                _characterHamsterState.Value == HamsterStateEnum.JumpFromRoofDamage ||
                _characterHamsterState.Value == HamsterStateEnum.JumpOnObstacleFromRoof
               )
                _superRoofJumpRequest.Invoke();

            if (_characterHamsterState.Value == HamsterStateEnum.Jump ||
                _characterHamsterState.Value == HamsterStateEnum.JumpOver ||
                _characterHamsterState.Value == HamsterStateEnum.JumpOnObstacle ||
                _characterHamsterState.Value == HamsterStateEnum.JumpOnRoof ||
                _characterHamsterState.Value == HamsterStateEnum.JumpDamageForSmallAlive ||
                _characterHamsterState.Value == HamsterStateEnum.JumpDamageForSmallNotAlive ||
                _characterHamsterState.Value == HamsterStateEnum.JumpDamageForBigAlive ||
                _characterHamsterState.Value == HamsterStateEnum.JumpOnRoofDamage
                )
            {
                _superJumpEvent?.Invoke();
            }
        }

        private void ResetJumpSequenceIfModeChanged()
        {
            bool isSkateboardActive = _character.ActorSwitcher.IsSkateboardActive;
            if (isSkateboardActive == _wasSkateboardActive)
                return;

            _doubleJumpDetector.Reset();
            _wasSkateboardActive = isSkateboardActive;
        }
    }
}
