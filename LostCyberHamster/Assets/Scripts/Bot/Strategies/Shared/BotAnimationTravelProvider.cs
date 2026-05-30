using System.Collections.Generic;
using Assets.Scripts.Common;
using Assets.Scripts.GameEngine.Controllers;
using UnityEngine;

namespace Assets.Scripts.Bot.Strategies.Shared
{
    /// <summary>
    /// Кеширует runtime-дистанции animation clips для planning-стратегий бота.
    /// </summary>
    internal static class BotAnimationTravelProvider
    {
        private static readonly Dictionary<string, float> _travelByClipName = new();

        private static TransformAnimatorController _controller;

        public static bool TryGetTravel(string clipName, out float travel)
        {
            travel = 0f;
            if (string.IsNullOrWhiteSpace(clipName))
                return false;

            TransformAnimatorController controller = ResolveController();
            if (controller == null)
                return false;

            if (_travelByClipName.TryGetValue(clipName, out travel))
                return true;

            travel = HelpMethods.GetWorldShiftForClip(controller, clipName);
            _travelByClipName[clipName] = travel;
            return true;
        }

        public static void Reset()
        {
            _controller = null;
            _travelByClipName.Clear();
        }

        private static TransformAnimatorController ResolveController()
        {
            if (_controller != null)
                return _controller;

            _controller = Object.FindAnyObjectByType<TransformAnimatorController>();
            _travelByClipName.Clear();
            return _controller;
        }
    }
}
