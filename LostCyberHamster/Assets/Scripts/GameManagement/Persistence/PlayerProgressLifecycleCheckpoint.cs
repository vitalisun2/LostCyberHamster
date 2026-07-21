using System;
using UnityEngine;

namespace GameManagement
{
    public sealed class PlayerProgressLifecycleCheckpoint : MonoBehaviour
    {
        private static PlayerProgressLifecycleCheckpoint _instance;

        /// <summary>Возникает при возврате приложения из background.</summary>
        public static event Action ApplicationResumed;

        /// <summary>Создаёт единый lifecycle checkpoint host на всё приложение.</summary>
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

        /// <summary>Сохраняет перед background и уведомляет о возврате приложения.</summary>
        private void OnApplicationPause(bool isPaused)
        {
            HandleApplicationPause(isPaused);
        }

        /// <summary>Обрабатывает pause-сигнал Unity без зависимости от MonoBehaviour dispatch.</summary>
        public static void HandleApplicationPause(bool isPaused)
        {
            if (isPaused)
            {
                PlayerProgressCommitter.Commit(CheckpointReason.AppBackgrounded);
                return;
            }

            ApplicationResumed?.Invoke();
        }
    }
}
