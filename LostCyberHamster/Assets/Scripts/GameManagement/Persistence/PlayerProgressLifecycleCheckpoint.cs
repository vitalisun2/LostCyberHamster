using UnityEngine;

namespace GameManagement
{
    public sealed class PlayerProgressLifecycleCheckpoint : MonoBehaviour
    {
        private static PlayerProgressLifecycleCheckpoint _instance;

        public static void EnsureCreated()
        {
            if (_instance != null)
            {
                return;
            }

            var host = new GameObject(nameof(PlayerProgressLifecycleCheckpoint));
            _instance = host.AddComponent<PlayerProgressLifecycleCheckpoint>();
            DontDestroyOnLoad(host);
        }

        private void OnApplicationPause(bool isPaused)
        {
            if (isPaused)
            {
                PlayerProgressCommitter.Commit(CheckpointReason.AppBackgrounded);
            }
        }
    }
}
