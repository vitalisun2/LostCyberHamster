using System.Collections.Generic;
using System.Threading;
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
        private static readonly object _syncRoot = new object();
        private static readonly string[] _knownClipNames =
        {
            "transform_jump",
            "transform_jump_from_roof",
            "transform_jump_on",
            "transform_jump_on_from_roof",
            "transform_medium_jump_from_roof",
            "transform_medium_jump_on_from_roof",
            "transform_medium_roof_jump",
            "transform_medium_run_from_roof",
            "transform_medium_super_jump_from_roof",
            "transform_medium_super_jump_on_obstacle_from_roof",
            "transform_medium_super_roof_jump",
            "transform_roof_jump",
            "transform_run_from_roof",
            "transform_super_jump",
            "transform_super_jump_from_roof",
            "transform_super_jump_on",
            "transform_super_jump_on_obstacle_from_roof",
            "transform_super_roof_jump"
        };

        private static readonly Dictionary<string, float> _travelByClipName = new();
        private static readonly Dictionary<string, float> _rootYAtHalfByClipName = new();

        private static TransformAnimatorController _controller;
        private static int _mainThreadId;
        private static bool _hasMainThreadId;

        public static bool TryGetTravel(string clipName, out float travel)
        {
            travel = 0f;
            if (string.IsNullOrWhiteSpace(clipName))
                return false;

            lock (_syncRoot)
            {
                if (_travelByClipName.TryGetValue(clipName, out travel))
                    return true;

                if (!CanUseUnityApi())
                    return false;

                TransformAnimatorController controller = ResolveController();
                if (controller == null)
                    return false;

                travel = HelpMethods.GetWorldShiftForClip(controller, clipName);
                _travelByClipName[clipName] = travel;
                return true;
            }
        }

        /// <summary>
        /// Возвращает root-Y offset в mid-point указанного animation clip.
        /// </summary>
        public static bool TryGetRootYAtHalf(string clipName, out float rootY)
        {
            rootY = 0f;
            if (string.IsNullOrWhiteSpace(clipName))
                return false;

            lock (_syncRoot)
            {
                if (_rootYAtHalfByClipName.TryGetValue(clipName, out rootY))
                    return true;

                if (!CanUseUnityApi())
                    return false;

                TransformAnimatorController controller = ResolveController();
                if (controller == null)
                    return false;

                rootY = HelpMethods.GetClipRootYAtHalf(controller, clipName);
                _rootYAtHalfByClipName[clipName] = rootY;
                return true;
            }
        }

        /// <summary>
        /// Прогревает cache animation travel на main thread перед async planning.
        /// </summary>
        public static void PrewarmKnownClipData()
        {
            for (int clipIndex = 0; clipIndex < _knownClipNames.Length; clipIndex++)
            {
                TryGetTravel(_knownClipNames[clipIndex], out _);
                TryGetRootYAtHalf(_knownClipNames[clipIndex], out _);
            }
        }

        public static void Reset()
        {
            lock (_syncRoot)
            {
                _mainThreadId = Thread.CurrentThread.ManagedThreadId;
                _hasMainThreadId = true;
                _controller = null;
                _travelByClipName.Clear();
                _rootYAtHalfByClipName.Clear();
            }
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

        private static bool CanUseUnityApi()
        {
            return !_hasMainThreadId || Thread.CurrentThread.ManagedThreadId == _mainThreadId;
        }
    }
}
