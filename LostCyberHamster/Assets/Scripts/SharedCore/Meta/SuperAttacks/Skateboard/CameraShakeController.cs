using System;
using UnityEngine;

namespace Vues.GameCore
{
    /// <summary>
    /// Добавляет затухающий offset поверх исходной позиции игровой камеры.
    /// </summary>
    public sealed class CameraShakeController : ICameraShake
    {
        public const float DefaultAmplitude = 0.08f;
        public const float DefaultDuration = 0.18f;
        public const float DefaultFrequency = 24f;

        /// <summary>
        /// Пауза в кадрах при 60 FPS от landing impact до старта Camera Shake.
        /// Ноль запускает shake сразу.
        /// </summary>
        public const int DefaultDelayAfterLandingImpactFrames = 0;

        private const float DefaultDelayAfterLandingImpact = 0f;

        private readonly Camera _camera;
        private readonly float _amplitude;
        private readonly float _duration;
        private readonly float _frequency;
        private readonly float _delayAfterLandingImpact;

        private Vector3 _basePosition;
        private float _activeDuration;
        private float _activeFrequency;
        private float _delayRemaining;
        private float _elapsed;
        private float _multiplier;
        private bool _isActive;
        private bool _isPaused;

        public CameraShakeController(
            Camera camera,
            float amplitude = DefaultAmplitude,
            float duration = DefaultDuration,
            float frequency = DefaultFrequency,
            float delayAfterLandingImpact = DefaultDelayAfterLandingImpact)
        {
            _camera = camera ?? throw new ArgumentNullException(nameof(camera));

            if (amplitude < 0f)
                throw new ArgumentOutOfRangeException(nameof(amplitude));
            if (duration <= 0f)
                throw new ArgumentOutOfRangeException(nameof(duration));
            if (frequency <= 0f)
                throw new ArgumentOutOfRangeException(nameof(frequency));
            if (delayAfterLandingImpact < 0f)
                throw new ArgumentOutOfRangeException(nameof(delayAfterLandingImpact));

            _amplitude = amplitude;
            _duration = duration;
            _frequency = frequency;
            _delayAfterLandingImpact = delayAfterLandingImpact;
            _basePosition = _camera.transform.position;
        }

        public void Play(
            float amplitudeMultiplier,
            float durationMultiplier,
            float frequencyMultiplier)
        {
            // Отбрасываем импульсы без положительной амплитуды или длительности.
            if (amplitudeMultiplier <= 0f ||
                durationMultiplier <= 0f ||
                frequencyMultiplier <= 0f)
            {
                return;
            }

            // Перезапускаем импульс от исходной позиции с новым временем жизни.
            if (!_isActive)
                _basePosition = _camera.transform.position;

            _activeDuration = _duration * durationMultiplier;
            _activeFrequency = _frequency * frequencyMultiplier;
            _delayRemaining = _delayAfterLandingImpact;
            _elapsed = 0f;
            _multiplier = amplitudeMultiplier;
            _isActive = true;
            if (_delayRemaining <= 0f)
                ApplyOffset();
            else
                _camera.transform.position = _basePosition;
        }

        public void Tick(float deltaTime)
        {
            if (!_isActive || _isPaused || deltaTime <= 0f)
                return;

            float activeDeltaTime = deltaTime;
            if (_delayRemaining > 0f)
            {
                _delayRemaining -= activeDeltaTime;
                if (_delayRemaining > 0f)
                    return;

                activeDeltaTime = -_delayRemaining;
                _delayRemaining = 0f;
            }

            _elapsed += activeDeltaTime;
            if (_elapsed >= _activeDuration)
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
            _activeDuration = 0f;
            _activeFrequency = 0f;
            _delayRemaining = 0f;
            _multiplier = 0f;
            _isActive = false;
        }

        private void ApplyOffset()
        {
            if (_isPaused)
                return;

            float progress = Mathf.Clamp01(_elapsed / _activeDuration);
            float envelope = 1f - progress;
            float phase = _elapsed * _activeFrequency * Mathf.PI * 2f;
            float strength = _amplitude * _multiplier * envelope;
            Vector3 offset = new(
                Mathf.Sin(phase) * strength,
                Mathf.Sin(phase * 1.37f + 1.1f) * strength,
                0f);
            _camera.transform.position = _basePosition + offset;
        }
    }
}
