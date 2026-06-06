using System.Collections.Generic;
using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.Bot.Strategies.Shared;
using Assets.Scripts.Bot.StrategiesNew.Shared.JumpFromRoofOnRoof;
using Assets.Scripts.Common;
using Assets.Scripts.GameEngine.Controllers;
using Assets.Scripts.GameEngine.Mechanics;
using Assets.Scripts.GameEngine.Mechanics.Models;
using Assets.Scripts.Gameplay.Enums;

namespace Assets.Scripts.Bot.StrategiesNew.JumpFromRoofOnRoof
{
    /// <summary>
    /// Описывает runtime-отличия обычного прыжка с текущей крыши на следующую крышу.
    /// </summary>
    internal sealed class JumpFromRoofOnRoofPolicy : IJumpFromRoofOnRoofPolicy
    {
        /// <summary>
        /// Имя animation clip автоматического схода с крыши.
        /// </summary>
        private const string _runFromRoofClipName = "transform_run_from_roof";

        /// <summary>
        /// Имя animation clip обычного roof jump.
        /// </summary>
        private const string _roofJumpClipName = "transform_roof_jump";

        /// <summary>
        /// Имя animation clip обычного fallback jump-from-roof.
        /// </summary>
        private const string _jumpFromRoofClipName = "transform_jump_from_roof";

        /// <summary>
        /// Имя fallback animation clip автоматического схода с medium roof.
        /// </summary>
        private const string _mediumRunFromRoofClipName = "transform_medium_run_from_roof";

        /// <summary>
        /// Имя fallback animation clip обычного roof jump для medium roof.
        /// </summary>
        private const string _mediumRoofJumpClipName = "transform_medium_roof_jump";

        /// <summary>
        /// Имя fallback animation clip обычного jump-from-roof для medium roof.
        /// </summary>
        private const string _mediumJumpFromRoofClipName = "transform_medium_jump_from_roof";

        /// <summary>
        /// Тип planned action обычного roof-to-roof прыжка.
        /// </summary>
        public BotActionKind ActionKind => BotActionKind.JumpFromRoofOnRoof;

        /// <summary>
        /// Стоимость обычного roof-to-roof прыжка в энергии.
        /// </summary>
        public int EnergyCost => 10;

        /// <summary>
        /// Префикс описания ordinary roof-to-roof action.
        /// </summary>
        public string DescriptionPrefix => "Jump from roof on roof";

        /// <summary>
        /// Runtime-состояние успешного обычного roof-to-roof прыжка.
        /// </summary>
        public HamsterStateEnum ExpectedSuccessState => HamsterStateEnum.RoofJump;

        /// <summary>
        /// Дополнительный отступ перед bigAlive для раннего fire.
        /// </summary>
        public float BigAliveCollisionPaddingRatio => CollisionController.BigAliveJumpDamageOverlapThreshold;

        /// <summary>
        /// Возвращает runtime-дистанции обычного roof-to-roof прыжка.
        /// </summary>
        public bool TryGetTravel(out JumpFromRoofOnRoofTravel travel)
        {
            // Считывает travel основных animation clips.
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
            // Делегирует расчет обычному runtime resolver.
            return RoofJumpOutcomeResolver.ResolveRoofJump(obstacles, context);
        }
    }
}
