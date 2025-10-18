using System;
using UnityEditor;
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
        }

        public static void OnDisable()
        {
            GameEventsManager.OnLivesLost -= (int value) => Vibrate();
        }

        public static void OnEnable()
        {
            GameEventsManager.OnLivesLost += (int value) => Vibrate();
        }
    }
}
