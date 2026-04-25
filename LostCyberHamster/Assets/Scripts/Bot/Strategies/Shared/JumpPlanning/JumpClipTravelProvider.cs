using Assets.Scripts.Common;
using Assets.Scripts.GameEngine.Controllers;
using UnityEngine;

namespace Assets.Scripts.Bot.Strategies.Shared.JumpPlanning
{
    /// <summary>
    /// Возвращает world shift для runtime animation clip.
    /// </summary>
    internal sealed class JumpClipTravelProvider
    {
        private readonly string _clipName;
        private readonly float _extraTravel;
        private readonly bool _throwIfMissing;
        private float? _cachedTravel;

        public JumpClipTravelProvider(string clipName, float extraTravel = 0f, bool throwIfMissing = false)
        {
            _clipName = clipName;
            _extraTravel = extraTravel;
            _throwIfMissing = throwIfMissing;
        }

        public bool TryGetTravel(out float travel)
        {
            if (_cachedTravel.HasValue)
            {
                travel = _cachedTravel.Value;
                return true;
            }

            TransformAnimatorController controller = Object.FindAnyObjectByType<TransformAnimatorController>();
            if (controller == null)
            {
                if (_throwIfMissing)
                    Guard.NotNull((controller, nameof(TransformAnimatorController)));

                travel = 0f;
                return false;
            }

            _cachedTravel = HelpMethods.GetWorldShiftForClip(controller, _clipName) + _extraTravel;
            travel = _cachedTravel.Value;
            return true;
        }
    }
}
