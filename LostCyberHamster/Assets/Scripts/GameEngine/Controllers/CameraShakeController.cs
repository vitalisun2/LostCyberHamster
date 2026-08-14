using System;
using UnityEngine;

namespace Assets.Scripts.GameEngine.Controllers
{
    /// <summary>
    /// Добавляет затухающий offset поверх исходной позиции игровой камеры.
    /// </summary>
    public sealed class CameraShakeController : ICameraShake
    {
        public const float DefaultAmplitude = 0.08f;
        public const float DefaultDuration = 0.18f;
        public const float DefaultFrequency = 24f;

        private readonly Camera _camera;
        private readonly float _amplitude;
        private readonly float _duration;
        private readonly float _frequency;

        private Vector3 _basePosition;
        private float _elapsed;
        private float _multiplier;
        private bool _isActive;
        private bool _isPaused;

        public CameraShakeController(
            Camera camera,
            float amplitude = DefaultAmplitude,
            float duration = DefaultDuration,
            float frequency = DefaultFrequency)
        {
            _camera = camera ?? throw new ArgumentNullException(nameof(camera));

            if (amplitude < 0f)
                throw new ArgumentOutOfRangeException(nameof(amplitude));
            if (duration <= 0f)
                throw new ArgumentOutOfRangeException(nameof(duration));
            if (frequency <= 0f)
                throw new ArgumentOutOfRangeException(nameof(frequency));

            _amplitude = amplitude;
            _duration = duration;
            _frequency = frequency;
            _basePosition = _camera.transform.position;
        }

        public void Play(float multiplier)
        {
            if (multiplier <= 0f)
                return;

            if (!_isActive)
                _basePosition = _camera.transform.position;

            _elapsed = 0f;
            _multiplier = multiplier;
            _isActive = true;
            ApplyOffset();
        }

        public void Tick(float deltaTime)
        {
            if (!_isActive || _isPaused || deltaTime <= 0f)
                return;

            _elapsed += deltaTime;
            if (_elapsed >= _duration)
            {
                Stop();
                return;
            }

            ApplyOffset();
        }

        public void SetPaused(bool isPaused)
        {
            if (_isPaused == isPaused)
                return;

            _isPaused = isPaused;
            if (_isPaused && _isActive)
                _camera.transform.position = _basePosition;
        }

        public void Stop()
        {
            if (_isActive)
                _camera.transform.position = _basePosition;

            _elapsed = 0f;
            _multiplier = 0f;
            _isActive = false;
        }

        private void ApplyOffset()
        {
            if (_isPaused)
                return;

            float progress = Mathf.Clamp01(_elapsed / _duration);
            float envelope = 1f - progress;
            float phase = _elapsed * _frequency * Mathf.PI * 2f;
            float strength = _amplitude * _multiplier * envelope;
            Vector3 offset = new(
                Mathf.Sin(phase) * strength,
                Mathf.Sin(phase * 1.37f + 1.1f) * strength,
                0f);
            _camera.transform.position = _basePosition + offset;
        }
    }
}
