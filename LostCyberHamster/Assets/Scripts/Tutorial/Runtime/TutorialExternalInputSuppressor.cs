using System;
using Assets.Scripts.Bot;
using Assets.Scripts.GameEngine.Mechanics;
using UnityEngine;

namespace Assets.Scripts.Tutorial
{
    /// <summary>
    /// Отключает внешнего gameplay-бота на время активного обучения.
    /// </summary>
    public sealed class TutorialExternalInputSuppressor : IDisposable
    {
        private RuntimeBotController _suppressedBot;

        public void SetSuppressed(bool shouldSuppress)
        {
            GameplayInputGate.SetBlocked(this, shouldSuppress);
            if (shouldSuppress)
            {
                Suppress();
                return;
            }

            Restore();
        }

        private void Suppress()
        {
            if (_suppressedBot != null)
            {
                return;
            }

            var bot = UnityEngine.Object.FindAnyObjectByType<RuntimeBotController>(
                FindObjectsInactive.Include);
            if (bot == null || !bot.IsEnabled)
            {
                return;
            }

            bot.ToggleEnabled();
            _suppressedBot = bot;
        }

        private void Restore()
        {
            if (_suppressedBot == null)
            {
                return;
            }

            if (!_suppressedBot.IsEnabled)
            {
                _suppressedBot.ToggleEnabled();
            }

            _suppressedBot = null;
        }

        public void Dispose()
        {
            GameplayInputGate.SetBlocked(this, false);
            Restore();
        }
    }
}
