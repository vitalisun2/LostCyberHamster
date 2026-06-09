using System.Collections.Generic;
using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.Bot.Strategies.Shared;
using Assets.Scripts.Bot.Strategies.Shared.JumpOnRoof;
using Assets.Scripts.Common;
using Assets.Scripts.GameEngine.Mechanics;
using Assets.Scripts.GameEngine.Mechanics.Models;
using Assets.Scripts.Gameplay.Enums;

namespace Assets.Scripts.Bot.Strategies.JumpOnRoof
{
    /// <summary>
    /// Описывает runtime-отличия обычного jump-on-roof.
    /// </summary>
    internal sealed class JumpOnRoofPolicy : IJumpOnRoofPolicy
    {
        private const string JumpClipName = "transform_jump";

        public BotActionKind ActionKind => BotActionKind.JumpOnRoof;
        public int EnergyCost => 10;
        public string DescriptionPrefix => "Jump on roof";
        public string LogTag => "JumpOnRoof";
        public HamsterStateEnum ExpectedRoofState => HamsterStateEnum.JumpOnRoof;
        public bool DamageBigAliveWithoutYByReach => true;

        /// <summary>
        /// Возвращает runtime-дистанцию обычного прыжка.
        /// </summary>
        public bool TryGetTravel(out float travel)
        {
            return BotAnimationTravelProvider.TryGetTravel(JumpClipName, out travel)
                && travel > 0f;
        }

        /// <summary>
        /// Передает обычный jump без смещения resolver-точки.
        /// </summary>
        public void GetResolveInput(
            float fireShift,
            float jumpTravel,
            out float resolveFireShift,
            out float resolveTravel)
        {
            resolveFireShift = fireShift;
            resolveTravel = jumpTravel;
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
