using System.Collections.Generic;
using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.Bot.Strategies.Shared;
using Assets.Scripts.Bot.Strategies.Shared.JumpPlanning.JumpFromRoofOnRoof;
using Assets.Scripts.Common;
using Assets.Scripts.GameEngine.Controllers;
using Assets.Scripts.GameEngine.Mechanics;
using Assets.Scripts.GameEngine.Mechanics.Models;
using Assets.Scripts.Gameplay.Enums;
using UnityEngine;

namespace Assets.Scripts.Bot.Strategies.SuperJumpFromRoofOnRoof
{
    /// <summary>
    /// Описывает runtime-отличия super-прыжка с крыши на следующую крышу.
    /// </summary>
    internal sealed class SuperJumpFromRoofOnRoofPolicy : IJumpFromRoofOnRoofPolicy
    {
        private const string _runFromRoofClipName = "transform_run_from_roof";
        private const string _superRoofJumpClipName = "transform_super_roof_jump";
        private const string _superJumpFromRoofClipName = "transform_super_jump_from_roof";
        private const string _mediumRunFromRoofClipName = "transform_medium_run_from_roof";
        private const string _mediumSuperRoofJumpClipName = "transform_medium_super_roof_jump";
        private const string _mediumSuperJumpFromRoofClipName = "transform_medium_super_jump_from_roof";

        /// <summary>
        /// Тип action для super-прыжка с крыши на крышу.
        /// </summary>
        public BotActionKind ActionKind => BotActionKind.SuperJumpFromRoofOnRoof;

        /// <summary>
        /// Стоимость двухфазного super roof jump.
        /// </summary>
        public int EnergyCost => 20;

        /// <summary>
        /// Префикс описания planned action.
        /// </summary>
        public string DescriptionPrefix => "Super jump from roof on roof";

        /// <summary>
        /// Runtime outcome, который считается успешной посадкой на новую крышу.
        /// </summary>
        public HamsterStateEnum ExpectedSuccessState => HamsterStateEnum.SuperRoofJump;

        /// <summary>
        /// Дополнительный отступ перед bigAlive, чтобы roof-to-roof input срабатывал до опасного контакта в RoofRun.
        /// </summary>
        public float BigAliveCollisionPaddingRatio => CollisionController.BigAliveJumpDamageOverlapThreshold;

        /// <summary>
        /// Возвращает runtime-дистанции super roof-to-roof прыжка.
        /// </summary>
        public bool TryGetTravel(out JumpFromRoofOnRoofTravel travel)
        {
            if (!BotAnimationTravelProvider.TryGetTravel(_runFromRoofClipName, out float runFromRoofTravel)
                || !BotAnimationTravelProvider.TryGetTravel(_superRoofJumpClipName, out float roofJumpTravel)
                || !BotAnimationTravelProvider.TryGetTravel(_superJumpFromRoofClipName, out float jumpFromRoofTravel))
            {
                travel = default;
                return false;
            }

            // Считывает medium fallback clips.
            if (runFromRoofTravel <= 0f)
                BotAnimationTravelProvider.TryGetTravel(_mediumRunFromRoofClipName, out runFromRoofTravel);

            if (roofJumpTravel <= 0f)
                BotAnimationTravelProvider.TryGetTravel(_mediumSuperRoofJumpClipName, out roofJumpTravel);

            if (jumpFromRoofTravel <= 0f)
                BotAnimationTravelProvider.TryGetTravel(_mediumSuperJumpFromRoofClipName, out jumpFromRoofTravel);

            // Учитывает путь мира до второго input super roof jump.
            roofJumpTravel += SuperJumpFromRoofOnRoofTiming.UpgradeDelayTravel;
            jumpFromRoofTravel += SuperJumpFromRoofOnRoofTiming.UpgradeDelayTravel;

            // Возвращает travel model.
            travel = new JumpFromRoofOnRoofTravel(runFromRoofTravel, roofJumpTravel, jumpFromRoofTravel);
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

    }
}
