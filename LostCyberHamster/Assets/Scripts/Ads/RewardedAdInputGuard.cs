using System;
using LostCyberHamster.UI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GameAds
{
    /// <summary>Удерживает ввод до завершения показа и полного кадра после отпускания устройств.</summary>
    internal sealed class RewardedAdInputGuard : IDisposable
    {
        private readonly RewardedAdService _service;
        private RewardedAdRequest _request;
        private IDisposable _block;
        private int _quietFrame = -1;

        public RewardedAdInputGuard(RewardedAdService service)
        {
            _service = service;
            _service.Changed += OnChanged;
            OnChanged();
        }

        private void OnChanged()
        {
            var active = _service.ActiveRequest;
            if (active == null || !active.IsNativePending)
                return;

            // Новое поколение показа отменяет отложенное освобождение прежнего.
            if (_request != active)
            {
                _request = active;
                _quietFrame = -1;
            }
            _block ??= UiInputBlock.Acquire();
        }

        public void Tick()
        {
            if (_block == null)
                return;

            // Сохранение награды уже не удерживает UI; неизвестный результат ещё удерживает.
            if (_request.IsNativePending || !Application.isFocused || HasActiveInput())
            {
                _quietFrame = -1;
                return;
            }

            // Кадр отпускания тоже поглощается, включая сгенерированный ClickEvent.
            if (_quietFrame < 0)
                _quietFrame = Time.frameCount;
            else if (Time.frameCount > _quietFrame + 1)
                Release();
        }

        private static bool HasActiveInput()
        {
            foreach (var device in InputSystem.devices)
            {
                if (device is Mouse mouse &&
                    (mouse.leftButton.isPressed || mouse.leftButton.wasReleasedThisFrame ||
                     mouse.rightButton.isPressed || mouse.rightButton.wasReleasedThisFrame ||
                     mouse.middleButton.isPressed || mouse.middleButton.wasReleasedThisFrame))
                    return true;
                if (device is Touchscreen screen)
                    foreach (var touch in screen.touches)
                        if (touch.press.isPressed || touch.press.wasReleasedThisFrame)
                            return true;
                if (device is Keyboard keyboard &&
                    (keyboard.anyKey.isPressed || keyboard.anyKey.wasReleasedThisFrame))
                    return true;
                if (device is Gamepad gamepad &&
                    (gamepad.buttonSouth.isPressed || gamepad.buttonSouth.wasReleasedThisFrame ||
                     gamepad.buttonEast.isPressed || gamepad.buttonEast.wasReleasedThisFrame ||
                     gamepad.startButton.isPressed || gamepad.startButton.wasReleasedThisFrame))
                    return true;
            }
            return false;
        }

        private void Release()
        {
            _block?.Dispose();
            _block = null;
            _request = null;
            _quietFrame = -1;
        }

        public void Dispose()
        {
            _service.Changed -= OnChanged;
            Release();
        }
    }
}
