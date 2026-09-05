using UnityEngine;

namespace Assets.Scripts.Online
{
    /// <summary>Передаёт планировщику foreground lifecycle и подсказку о смене подключения.</summary>
    public sealed class OnlineServicesRunner : MonoBehaviour
    {
        private bool _paused;
        private bool _focused = true;
        private NetworkReachability _reachability;
        private double _nextTick;

        private void Awake() => _reachability = Application.internetReachability;

        private void Update()
        {
            if (_paused || !_focused || UnityGameClock.Instance.RealtimeSeconds < _nextTick) return;
            _nextTick = UnityGameClock.Instance.RealtimeSeconds + 0.5;
            var current = Application.internetReachability;
            if (current != _reachability)
            {
                _reachability = current;
                if (current != NetworkReachability.NotReachable) OnlineServicesCoordinator.Resume();
            }
            OnlineServicesCoordinator.Tick();
        }

        private void OnApplicationPause(bool paused)
        {
            _paused = paused;
            if (!paused) OnlineServicesCoordinator.Resume();
        }

        private void OnApplicationFocus(bool focused)
        {
            _focused = focused;
            if (focused && !_paused) OnlineServicesCoordinator.Resume();
        }

        private void OnApplicationQuit() => OnlineServicesCoordinator.Quit();
    }
}
