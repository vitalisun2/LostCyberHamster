using System.Collections.Generic;
using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.Bot.Strategies.Shared;
using Assets.Scripts.Bot.StrategiesNew.Shared.JumpFromRoof;
using Assets.Scripts.Common;
using Assets.Scripts.GameEngine.Mechanics;
using Assets.Scripts.GameEngine.Mechanics.Models;
using Assets.Scripts.Gameplay.Enums;

namespace Assets.Scripts.Bot.StrategiesNew.JumpFromRoof
{
    /// <summary>
    /// Описывает runtime-отличия обычного прыжка с крыши на дорогу.
    /// </summary>
    internal sealed class JumpFromRoofPolicy : IJumpFromRoofPolicy
    {
        private const string RunFromRoofClipName = "transform_run_from_roof";
        private const string RoofJumpClipName = "transform_roof_jump";
        private const string JumpFromRoofClipName = "transform_jump_from_roof";
        private const string MediumRunFromRoofClipName = "transform_medium_run_from_roof";
        private const string MediumRoofJumpClipName = "transform_medium_roof_jump";
        private const string MediumJumpFromRoofClipName = "transform_medium_jump_from_roof";

        public BotActionKind ActionKind => BotActionKind.JumpFromRoof;
        public int EnergyCost => 10;
        public string DescriptionPrefix => "Jump from roof";
        public HamsterStateEnum ExpectedSuccessState => HamsterStateEnum.JumpFromRoof;
        public float BigAliveCollisionPaddingRatio => 0f;

        /// <summary>
        /// Возвращает runtime-дистанции обычного прыжка с крыши.
        /// </summary>
        public bool TryGetTravel(out JumpFromRoofTravel travel)
        {
            if (!BotAnimationTravelProvider.TryGetTravel(RunFromRoofClipName, out float runFromRoofTravel)
                || !BotAnimationTravelProvider.TryGetTravel(RoofJumpClipName, out float roofJumpTravel)
                || !BotAnimationTravelProvider.TryGetTravel(JumpFromRoofClipName, out float jumpFromRoofTravel))
            {
                travel = default;
                return false;
            }

            // Считывает medium fallback clips.
            if (runFromRoofTravel <= 0f)
                BotAnimationTravelProvider.TryGetTravel(MediumRunFromRoofClipName, out runFromRoofTravel);

            if (roofJumpTravel <= 0f)
                BotAnimationTravelProvider.TryGetTravel(MediumRoofJumpClipName, out roofJumpTravel);

            if (jumpFromRoofTravel <= 0f)
                BotAnimationTravelProvider.TryGetTravel(MediumJumpFromRoofClipName, out jumpFromRoofTravel);

            // Возвращает travel model.
            travel = new JumpFromRoofTravel(runFromRoofTravel, roofJumpTravel, jumpFromRoofTravel);
            return runFromRoofTravel > 0f && roofJumpTravel > 0f && jumpFromRoofTravel > 0f;
        }

        /// <summary>
        /// Вызывает runtime resolver обычного roof jump.
        /// </summary>
        public JumpResolveResult Resolve(
            IReadOnlyList<JumpObstacleData> obstacles,
            RoofJumpResolveContext context)
        {
            return RoofJumpOutcomeResolver.ResolveRoofJump(obstacles, context);
        }
    }
}
