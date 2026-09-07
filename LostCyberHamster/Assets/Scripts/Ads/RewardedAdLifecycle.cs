using UnityEngine;
using UnityEngine.SceneManagement;

namespace GameAds
{
    /// <summary>Продолжает callbacks и сохранение награды независимо от меню и уровня.</summary>
    public sealed class RewardedAdLifecycle : MonoBehaviour
    {
        private RewardedAdService _service;
        private RewardedAdInputGuard _inputGuard;
        internal RewardedAdService Service
        {
            get => _service;
            set
            {
                _inputGuard?.Dispose();
                _service = value;
                _inputGuard = value == null ? null : new RewardedAdInputGuard(value);
            }
        }
        private bool _paused;
        private bool _focused = true;
        private double _lastTick;
        private void OnEnable()
        {
            _lastTick = Time.realtimeSinceStartupAsDouble;
            SceneManager.sceneUnloaded += OnSceneUnloaded;
        }
        private void OnDisable() => SceneManager.sceneUnloaded -= OnSceneUnloaded;
        private void OnDestroy()
        {
            _inputGuard?.Dispose();
            Service?.Shutdown();
        }
        private void Update()
        {
            double now = Time.realtimeSinceStartupAsDouble;
            Service?.Tick(!_paused && _focused, now - _lastTick);
            _inputGuard?.Tick();
            _lastTick = now;
        }
        private void OnApplicationPause(bool paused) => _paused = paused;
        private void OnApplicationFocus(bool focused) => _focused = focused;
        private void OnSceneUnloaded(Scene scene) => Service?.SceneUnloaded(scene.handle);
    }
}
