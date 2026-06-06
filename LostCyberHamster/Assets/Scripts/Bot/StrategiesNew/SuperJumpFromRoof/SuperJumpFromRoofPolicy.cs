using System.Collections.Generic;
using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.Bot.Strategies.Shared;
using Assets.Scripts.Bot.StrategiesNew.Shared.JumpFromRoof;
using Assets.Scripts.Common;
using Assets.Scripts.GameEngine.Controllers;
using Assets.Scripts.GameEngine.Mechanics;
using Assets.Scripts.GameEngine.Mechanics.Models;
using Assets.Scripts.Gameplay.Enums;

namespace Assets.Scripts.Bot.StrategiesNew.SuperJumpFromRoof
{
    /// <summary>
    /// Описывает runtime-отличия super-прыжка с крыши на дорогу.
    /// </summary>
    internal sealed class SuperJumpFromRoofPolicy : IJumpFromRoofPolicy
    {
        private const string RunFromRoofClipName = "transform_run_from_roof";
        private const string SuperRoofJumpClipName = "transform_super_roof_jump";
        private const string SuperJumpFromRoofClipName = "transform_super_jump_from_roof";
        private const string MediumRunFromRoofClipName = "transform_medium_run_from_roof";
        private const string MediumSuperRoofJumpClipName = "transform_medium_super_roof_jump";
        private const string MediumSuperJumpFromRoofClipName = "transform_medium_super_jump_from_roof";

        public BotActionKind ActionKind => BotActionKind.SuperJumpFromRoof;
        public int EnergyCost => 20;
        public string DescriptionPrefix => "Super jump from roof";
        public HamsterStateEnum ExpectedSuccessState => HamsterStateEnum.SuperJumpFromRoof;
        public float BigAliveCollisionPaddingRatio => 0f;

        /// <summary>
        /// Возвращает runtime-дистанции super-прыжка с крыши.
        /// </summary>
        public bool TryGetTravel(out JumpFromRoofTravel travel)
        {
            if (!BotAnimationTravelProvider.TryGetTravel(RunFromRoofClipName, out float runFromRoofTravel)
                || !BotAnimationTravelProvider.TryGetTravel(SuperRoofJumpClipName, out float roofJumpTravel)
                || !BotAnimationTravelProvider.TryGetTravel(SuperJumpFromRoofClipName, out float jumpFromRoofTravel))
            {
                travel = default;
                return false;
            }

            // Считывает medium fallback clips.
            if (runFromRoofTravel <= 0f)
                BotAnimationTravelProvider.TryGetTravel(MediumRunFromRoofClipName, out runFromRoofTravel);

            if (roofJumpTravel <= 0f)
                BotAnimationTravelProvider.TryGetTravel(MediumSuperRoofJumpClipName, out roofJumpTravel);

            if (jumpFromRoofTravel <= 0f)
                BotAnimationTravelProvider.TryGetTravel(MediumSuperJumpFromRoofClipName, out jumpFromRoofTravel);

            // Учитывает путь мира до второго input super roof jump.
            float upgradeDelayTravel = GetSuperRoofJumpUpgradeDelayTravel();
            roofJumpTravel += upgradeDelayTravel;
            jumpFromRoofTravel += upgradeDelayTravel;

            // Возвращает travel model.
            travel = new JumpFromRoofTravel(runFromRoofTravel, roofJumpTravel, jumpFromRoofTravel);
            return runFromRoofTravel > 0f && roofJumpTravel > 0f && jumpFromRoofTravel > 0f;
        }

        /// <summary>
        /// Вызывает runtime resolver super roof jump.
        /// </summary>
        public JumpResolveResult Resolve(
            IReadOnlyList<JumpObstacleData> obstacles,
            RoofJumpResolveContext context)
        {
            return SuperRoofJumpOutcomeResolver.ResolveSuperRoofJump(obstacles, context);
        }

        /// <summary>
        /// Возвращает путь мира за задержку upgrade input.
        /// </summary>
        private static float GetSuperRoofJumpUpgradeDelayTravel()
        {
            float halfDoubleJumpWindowSeconds = DoubleJumpDetector.DoubleJumpThreshold / 2f;
            return halfDoubleJumpWindowSeconds * Assets.Scripts.Consts.GameSpeedBase;
        }
    }
}
