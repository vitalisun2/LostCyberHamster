using System.Collections.Generic;
using Assets.Scripts.Common;
using Assets.Scripts.GameEngine.Controllers;
using UnityEngine;

namespace Assets.Scripts.Bot.Strategies.Shared
{
    /// <summary>
    /// Кеширует runtime-параметры animation clips для planning-стратегий бота.
    /// </summary>
    internal static class BotAnimationTravelProvider
    {
        private static readonly Dictionary<string, float> _travelByClipName = new();
        private static readonly Dictionary<string, float> _rootYAtHalfByClipName = new();

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

        /// <summary>
        /// Возвращает root-Y offset в mid-point указанного animation clip.
        /// </summary>
        public static bool TryGetRootYAtHalf(string clipName, out float rootY)
        {
            rootY = 0f;
            if (string.IsNullOrWhiteSpace(clipName))
                return false;

            TransformAnimatorController controller = ResolveController();
            if (controller == null)
                return false;

            if (_rootYAtHalfByClipName.TryGetValue(clipName, out rootY))
                return true;

            rootY = HelpMethods.GetClipRootYAtHalf(controller, clipName);
            _rootYAtHalfByClipName[clipName] = rootY;
            return true;
        }

        public static void Reset()
        {
            _controller = null;
            _travelByClipName.Clear();
            _rootYAtHalfByClipName.Clear();
        }

        private static TransformAnimatorController ResolveController()
        {
            if (_controller != null)
                return _controller;

            _controller = Object.FindAnyObjectByType<TransformAnimatorController>();
            _travelByClipName.Clear();
            _rootYAtHalfByClipName.Clear();
            return _controller;
        }
    }
}
