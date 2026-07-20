using System;
using Assets.Scripts.Bot;
using UnityEngine;

namespace Assets.Scripts.TutorialOld
{
    /// <summary>
    /// На время tutorial отключает внешние runtime-источники ввода, которые могли бы пройти урок за игрока.
    /// </summary>
    public sealed class TutorialExternalInputSuppressor : IDisposable
    {
        private RuntimeBotController _disabledBot;

        public void Suppress()
        {
            if (_disabledBot != null)
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
            _disabledBot = bot;
        }

        public void Restore()
        {
            if (_disabledBot == null)
            {
                return;
            }

            if (!_disabledBot.IsEnabled)
            {
                _disabledBot.ToggleEnabled();
            }

            _disabledBot = null;
        }

        public void Dispose()
        {
            Restore();
        }
    }
}
