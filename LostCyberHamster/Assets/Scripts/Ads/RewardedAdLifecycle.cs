using UnityEngine;
using UnityEngine.SceneManagement;

namespace GameAds
{
    /// <summary>Продолжает callbacks и сохранение награды независимо от меню и уровня.</summary>
    public sealed class RewardedAdLifecycle : MonoBehaviour
    {
        internal RewardedAdService Service;
        private bool _paused;
        private bool _focused = true;
        private double _lastTick;
        private void OnEnable()
        {
            _lastTick = Time.realtimeSinceStartupAsDouble;
            SceneManager.sceneUnloaded += OnSceneUnloaded;
        }
        private void OnDisable() => SceneManager.sceneUnloaded -= OnSceneUnloaded;
        private void OnDestroy() => Service?.Shutdown();
        private void Update()
        {
            double now = Time.realtimeSinceStartupAsDouble;
            Service?.Tick(!_paused && _focused, now - _lastTick);
            _lastTick = now;
        }
        private void OnApplicationPause(bool paused) => _paused = paused;
        private void OnApplicationFocus(bool focused) => _focused = focused;
        private void OnSceneUnloaded(Scene scene) => Service?.SceneUnloaded(scene.handle);
    }
}
