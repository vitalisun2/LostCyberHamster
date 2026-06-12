using System.Collections.Generic;
using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.Bot.Strategies.Shared;
using Assets.Scripts.Bot.Strategies.Shared.JumpOn;
using Assets.Scripts.Common;
using Assets.Scripts.GameEngine.Controllers;
using Assets.Scripts.GameEngine.Mechanics;
using Assets.Scripts.GameEngine.Mechanics.Models;
using Assets.Scripts.Gameplay.Enums;

namespace Assets.Scripts.Bot.Strategies.JumpOn
{
    /// <summary>
    /// Описывает runtime-отличия обычного ground jump-on.
    /// </summary>
    internal sealed class JumpOnPolicy : IJumpOnPolicy
    {
        private const string JumpClipName = "transform_jump";
        private const string JumpOnClipName = "transform_jump_on";

        public BotActionKind ActionKind => BotActionKind.JumpOn;
        public int EnergyCost => 10;
        public string DescriptionPrefix => "Jump on";
        public string LogTag => "JumpOn";
        public HamsterStateEnum ExpectedJumpOnState => HamsterStateEnum.JumpOnObstacle;

        /// <summary>
        /// Считывает runtime-дистанции обычного jump-on из animation clips.
        /// </summary>
        public bool TryGetTravel(out JumpOnTravel travel)
        {
            if (!BotAnimationTravelProvider.TryGetTravel(JumpOnClipName, out float actionTravel)
                || !BotAnimationTravelProvider.TryGetTravel(JumpClipName, out float resolveTravel))
            {
                travel = default;
                return false;
            }

            travel = new JumpOnTravel(
                actionTravel,
                resolveTravel,
                resolveFireShiftOffset: 0f);
            return actionTravel > 0f && resolveTravel > 0f;
        }

        /// <summary>
        /// Считывает runtime root-Y offset обычного jump-клипа для проверки bigAlive.
        /// </summary>
        public bool TryGetJumpMidYShift(out float jumpMidYShift)
        {
            return BotAnimationTravelProvider.TryGetRootYAtHalf(JumpClipName, out jumpMidYShift);
        }

        /// <summary>
        /// Вызывает runtime resolver обычного прыжка.
        /// </summary>
        public JumpResolveResult Resolve(
            IReadOnlyList<JumpObstacleData> obstacles,
            JumpResolveContext context)
        {
            return JumpOutcomeResolver.ResolveJump(obstacles, context);
        }
    }
}
