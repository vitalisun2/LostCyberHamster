using System.Collections.Generic;
using System.Globalization;
using Assets.Scripts.Common;
using Assets.Scripts.GameEngine.Controllers;
using UnityEngine;

namespace Assets.Scripts.Bot.Strategies.Shared.JumpPlanning
{
    /// <summary>
    /// Возвращает world shift для runtime animation clip.
    /// </summary>
    internal static class JumpClipTravel
    {
        private static readonly Dictionary<string, float> _travelByCacheKey = new();

        public static bool TryGetTravel(
            string clipName,
            out float travel,
            float extraTravel = 0f,
            bool throwIfMissing = false)
        {
            string cacheKey = BuildCacheKey(clipName, extraTravel);
            if (_travelByCacheKey.TryGetValue(cacheKey, out travel))
                return true;

            TransformAnimatorController controller = Object.FindAnyObjectByType<TransformAnimatorController>();
            if (controller == null)
            {
                if (throwIfMissing)
                    Guard.NotNull((controller, nameof(TransformAnimatorController)));

                travel = 0f;
                return false;
            }

            travel = HelpMethods.GetWorldShiftForClip(controller, clipName) + extraTravel;
            _travelByCacheKey[cacheKey] = travel;
            return true;
        }

        private static string BuildCacheKey(string clipName, float extraTravel)
        {
            return clipName + ":" + extraTravel.ToString("R", CultureInfo.InvariantCulture);
        }
    }
}
