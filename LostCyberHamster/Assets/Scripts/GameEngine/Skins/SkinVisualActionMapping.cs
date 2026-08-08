using System;
using UnityEngine;

namespace Assets.Scripts.GameEngine.Skins
{
    /// <summary>
    /// Правило выбора Animator state для одного семантического действия с wildcard-вариантами.
    /// </summary>
    [Serializable]
    public sealed class SkinVisualActionMapping
    {
        public SkinVisualAction Action;
        public bool MatchAnyVariant = true;
        public SkinVisualVariant Variant;
        public bool MatchAnyOutcome = true;
        public SkinVisualOutcome Outcome;
        public string StateName;
        public AnimationClip Clip;
        public bool Loop;

        /// <summary>
        /// Проверяет соответствие правила runtime-контексту.
        /// </summary>
        public bool Matches(in SkinActionContext context)
        {
            return Action == context.Action
                   && (MatchAnyVariant || Variant == context.Variant)
                   && (MatchAnyOutcome || Outcome == context.Outcome);
        }

        public int Specificity => (MatchAnyVariant ? 0 : 1) + (MatchAnyOutcome ? 0 : 1);
    }
}
