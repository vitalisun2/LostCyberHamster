using Assets.Scripts.Bot;
using Assets.Scripts.Gameplay;
using LostCyberHamster.UI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Assets.Scripts.GameEngine.Mechanics
{
    public class KeyboardMechanics
    {
        private readonly Keyboard _keyboard;
        private readonly Hamster _character;
        private readonly GameScreenController _gameScreenController;
        private readonly PlayerJumpInputSequencer _jumpInputSequencer;

        public KeyboardMechanics(
            Hamster hamster,
            UIManager uiManager,
            PlayerJumpInputSequencer jumpInputSequencer)
        {
            _character = hamster;
            _keyboard = Keyboard.current;
            _jumpInputSequencer = jumpInputSequencer;

            _gameScreenController =
                uiManager.GetController<GameScreenController>();
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
                _jumpInputSequencer.HandleJumpInput();
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
            _character.TapRequest?.Invoke();
        }
    }
}
