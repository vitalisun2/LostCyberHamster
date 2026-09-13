using System;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace LostCyberHamster.UI
{
    /// <summary>Коротко удерживает ввод после UI-навигации, чтобы следующий экран не получил тот же tap/click.</summary>
    internal static class UiInputCarryoverBlock
    {
        private const int DefaultDurationMs = 1000;

        private static IDisposable _block;
        private static long _expiresAt;

        public static void Arm(int durationMs = DefaultDurationMs)
        {
            if (durationMs <= 0)
                throw new ArgumentOutOfRangeException(nameof(durationMs));

            _block?.Dispose();
            _block = UiInputBlock.Acquire();
            _expiresAt = Environment.TickCount64 + durationMs;
        }

        public static void LoadScene(string sceneName, int durationMs = DefaultDurationMs)
        {
            if (string.IsNullOrWhiteSpace(sceneName))
                throw new ArgumentException("Scene name must be provided.", nameof(sceneName));

            Arm(durationMs);
            SceneManager.LoadScene(sceneName);
        }

        public static bool TryConsume(EventBase evt)
        {
            if (_block == null)
                return false;

            if (Environment.TickCount64 >= _expiresAt)
            {
                Release();
                return false;
            }

            if (evt is PointerUpEvent ||
                evt is ClickEvent ||
                evt is KeyUpEvent ||
                evt is NavigationSubmitEvent ||
                evt is NavigationCancelEvent)
            {
                Release();
            }

            return true;
        }

        private static void Release()
        {
            _block?.Dispose();
            _block = null;
            _expiresAt = 0;
        }
    }
}