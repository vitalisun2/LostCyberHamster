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

namespace Assets.Scripts.Bot.Strategies.JumpFromRoofOnRoof
{
    /// <summary>
    /// Описывает runtime-отличия обычного прыжка с крыши на следующую крышу.
    /// </summary>
    internal sealed class JumpFromRoofOnRoofPolicy : IJumpFromRoofOnRoofPolicy
    {
        private const string _runFromRoofClipName = "transform_run_from_roof";
        private const string _roofJumpClipName = "transform_roof_jump";
        private const string _jumpFromRoofClipName = "transform_jump_from_roof";
        private const string _mediumRunFromRoofClipName = "transform_medium_run_from_roof";
        private const string _mediumRoofJumpClipName = "transform_medium_roof_jump";
        private const string _mediumJumpFromRoofClipName = "transform_medium_jump_from_roof";

        /// <summary>
        /// Тип action для обычного прыжка с крыши на крышу.
        /// </summary>
        public BotActionKind ActionKind => BotActionKind.JumpFromRoofOnRoof;

        /// <summary>
        /// Стоимость обычного roof jump.
        /// </summary>
        public int EnergyCost => 10;

        /// <summary>
        /// Префикс описания planned action.
        /// </summary>
        public string DescriptionPrefix => "Jump from roof on roof";

        /// <summary>
        /// Runtime outcome, который считается успешной посадкой на новую крышу.
        /// </summary>
        public HamsterStateEnum ExpectedSuccessState => HamsterStateEnum.RoofJump;

        /// <summary>
        /// Дополнительный отступ перед bigAlive, чтобы roof-to-roof input срабатывал до опасного контакта в RoofRun.
        /// </summary>
        public float BigAliveCollisionPaddingRatio => CollisionController.BigAliveJumpDamageOverlapThreshold;

        /// <summary>
        /// Возвращает runtime-дистанции обычного roof-to-roof прыжка.
        /// </summary>
        public bool TryGetTravel(out JumpFromRoofOnRoofTravel travel)
        {
            if (!BotAnimationTravelProvider.TryGetTravel(_runFromRoofClipName, out float runFromRoofTravel)
                || !BotAnimationTravelProvider.TryGetTravel(_roofJumpClipName, out float roofJumpTravel)
                || !BotAnimationTravelProvider.TryGetTravel(_jumpFromRoofClipName, out float jumpFromRoofTravel))
            {
                travel = default;
                return false;
            }

            // Считывает medium fallback clips.
            if (runFromRoofTravel <= 0f)
                BotAnimationTravelProvider.TryGetTravel(_mediumRunFromRoofClipName, out runFromRoofTravel);

            if (roofJumpTravel <= 0f)
                BotAnimationTravelProvider.TryGetTravel(_mediumRoofJumpClipName, out roofJumpTravel);

            if (jumpFromRoofTravel <= 0f)
                BotAnimationTravelProvider.TryGetTravel(_mediumJumpFromRoofClipName, out jumpFromRoofTravel);

            // Возвращает travel model.
            travel = new JumpFromRoofOnRoofTravel(runFromRoofTravel, roofJumpTravel, jumpFromRoofTravel);
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
