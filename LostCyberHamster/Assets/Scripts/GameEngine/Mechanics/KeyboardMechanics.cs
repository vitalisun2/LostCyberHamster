using Assets.Scripts.Common;
using Assets.Scripts.GameManagerLogic;
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
        private readonly AtomicEvent _ultaEvent;
        private readonly UIManager _uiManager;
        private readonly GameManager _gameManager;
        private readonly Keyboard _keyboard;
        private readonly AtomicEvent _tapRequest;
        private readonly Hamster _character;
        private DoubleJumpDetector _doubleJumpDetector;

        public KeyboardMechanics(Hamster hamster, UIManager uiManager, GameManager gameManager)
        {
            _characterHamsterState = hamster.HamsterState;
            _character = hamster;
            _roofJumpRequest = hamster.RoofJumpRequest;
            _superRoofJumpRequest = hamster.SuperRoofJumpRequest;
            _jumpEvent = hamster.JumpRequest;
            _superJumpEvent = hamster.SuperJumpRequest;
            _ultaEvent = hamster.UltaEvent;
            _gameManager = gameManager;
            _keyboard = Keyboard.current;
            _tapRequest = hamster.TapRequest;

            _uiManager = uiManager;

            _doubleJumpDetector = new DoubleJumpDetector();
            _doubleJumpDetector.Reset();
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

            if (_keyboard.escapeKey.wasPressedThisFrame)
            {
                TogglePauseResume();
            }

            if (_keyboard.bKey.wasPressedThisFrame)
            {
                OnUlta();
            }
        }

        private void OnUlta()
        {
            _ultaEvent?.Invoke();
        }

        private void OnShift()
        {
            _tapRequest?.Invoke();
        }

        private void OnJump()
        {
            if (_characterHamsterState.Value == HamsterStateEnum.RoofRun)
                _roofJumpRequest.Invoke();

            if (_characterHamsterState.Value == HamsterStateEnum.Run ||
                _character.IsDamaged.Value)
                _jumpEvent?.Invoke();
        }

        private void OnSuperJump()
        {
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

        private void TogglePauseResume()
        {
            if (_gameManager.State == GameState.PAUSED)
            {
                _uiManager.HideModal(ScreenEnum.PauseModal);
                _gameManager.Resume();
            }
            else
            {
                _uiManager.ShowModalAsync(ScreenEnum.PauseModal);
                _gameManager.Pause();
            }
        }
    }
}
