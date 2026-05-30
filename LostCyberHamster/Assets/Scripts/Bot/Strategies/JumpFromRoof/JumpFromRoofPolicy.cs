using System.Collections.Generic;
using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.Bot.Strategies.Shared;
using Assets.Scripts.Bot.Strategies.Shared.JumpPlanning.JumpFromRoof;
using Assets.Scripts.Common;
using Assets.Scripts.GameEngine.Controllers;
using Assets.Scripts.GameEngine.Mechanics;
using Assets.Scripts.GameEngine.Mechanics.Models;
using Assets.Scripts.Gameplay.Enums;
using UnityEngine;

namespace Assets.Scripts.Bot.Strategies.JumpFromRoof
{
    /// <summary>
    /// Описывает runtime-отличия обычного прыжка с крыши на дорогу.
    /// </summary>
    internal sealed class JumpFromRoofPolicy : IJumpFromRoofPolicy
    {
        /// <summary>
        /// Имя клипа автоматического схода с крыши.
        /// </summary>
        private const string RunFromRoofClipName = "transform_run_from_roof";

        /// <summary>
        /// Имя клипа roof jump.
        /// </summary>
        private const string RoofJumpClipName = "transform_roof_jump";

        /// <summary>
        /// Имя клипа прыжка с крыши на дорогу.
        /// </summary>
        private const string JumpFromRoofClipName = "transform_jump_from_roof";

        /// <summary>
        /// Имя medium fallback-клипа автоматического схода с крыши.
        /// </summary>
        private const string MediumRunFromRoofClipName = "transform_medium_run_from_roof";

        /// <summary>
        /// Имя medium fallback-клипа roof jump.
        /// </summary>
        private const string MediumRoofJumpClipName = "transform_medium_roof_jump";

        /// <summary>
        /// Имя medium fallback-клипа прыжка с крыши на дорогу.
        /// </summary>
        private const string MediumJumpFromRoofClipName = "transform_medium_jump_from_roof";

        /// <summary>
        /// Тип action для обычного прыжка с крыши.
        /// </summary>
        public BotActionKind ActionKind => BotActionKind.JumpFromRoof;

        /// <summary>
        /// Стоимость обычного прыжка с крыши.
        /// </summary>
        public int EnergyCost => 10;

        /// <summary>
        /// Префикс описания planned action.
        /// </summary>
        public string DescriptionPrefix => "Jump from roof";

        /// <summary>
        /// Runtime outcome, который считается успешным для этой strategy.
        /// </summary>
        public HamsterStateEnum ExpectedSuccessState => HamsterStateEnum.JumpFromRoof;

        /// <summary>
        /// Обычный прыжок с крыши не добавляет дополнительный отступ для bigAlive.
        /// </summary>
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
