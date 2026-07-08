using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Assets.Scripts.System
{
    public static class VibrationManager
    {
        public static bool EnableVibration { get; set; } = true;

        /// <summary>
        /// Вызывает вибрацию устройства/геймпада.
        /// </summary>
        public static void Vibrate()
        {
            if (!EnableVibration) return;
#if UNITY_EDITOR
            return;
#else
            try
            {
#if UNITY_ANDROID || UNITY_IOS
        Handheld.Vibrate();
#else
                DebugManager.Log("Вибрация поддерживается только на Android/iOS.");
#endif
            }
            catch
            {
                DebugManager.Log("Не удалось вызвать вибрацию при помощи функции Handheld.Vibrate().");
            }
#endif
        }

        public static void OnDisable()
        {
            GameEventsManager.OnLivesLost -= OnLivesLost;
        }

        public static void OnEnable()
        {
            GameEventsManager.OnLivesLost += OnLivesLost;
        }

        private static void OnLivesLost(int value)
        {
            Vibrate();
        }
    }
}
