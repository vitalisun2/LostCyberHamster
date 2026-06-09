using System.Collections.Generic;
using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.Bot.Strategies.Shared;
using Assets.Scripts.Bot.Strategies.Shared.JumpFromRoofOnRoof;
using Assets.Scripts.Common;
using Assets.Scripts.GameEngine.Controllers;
using Assets.Scripts.GameEngine.Mechanics;
using Assets.Scripts.GameEngine.Mechanics.Models;
using Assets.Scripts.Gameplay.Enums;

namespace Assets.Scripts.Bot.Strategies.SuperJumpFromRoofOnRoof
{
    /// <summary>
    /// Описывает runtime-отличия super-прыжка с текущей крыши на следующую крышу.
    /// </summary>
    internal sealed class SuperJumpFromRoofOnRoofPolicy : IJumpFromRoofOnRoofPolicy
    {
        /// <summary>
        /// Имя animation clip автоматического схода с крыши.
        /// </summary>
        private const string _runFromRoofClipName = "transform_run_from_roof";

        /// <summary>
        /// Имя animation clip super roof jump.
        /// </summary>
        private const string _superRoofJumpClipName = "transform_super_roof_jump";

        /// <summary>
        /// Имя animation clip super fallback jump-from-roof.
        /// </summary>
        private const string _superJumpFromRoofClipName = "transform_super_jump_from_roof";

        /// <summary>
        /// Имя fallback animation clip автоматического схода с medium roof.
        /// </summary>
        private const string _mediumRunFromRoofClipName = "transform_medium_run_from_roof";

        /// <summary>
        /// Имя fallback animation clip super roof jump для medium roof.
        /// </summary>
        private const string _mediumSuperRoofJumpClipName = "transform_medium_super_roof_jump";

        /// <summary>
        /// Имя fallback animation clip super jump-from-roof для medium roof.
        /// </summary>
        private const string _mediumSuperJumpFromRoofClipName = "transform_medium_super_jump_from_roof";

        /// <summary>
        /// Тип planned action super roof-to-roof прыжка.
        /// </summary>
        public BotActionKind ActionKind => BotActionKind.SuperJumpFromRoofOnRoof;

        /// <summary>
        /// Стоимость super roof-to-roof прыжка в энергии.
        /// </summary>
        public int EnergyCost => 20;

        /// <summary>
        /// Префикс описания super roof-to-roof action.
        /// </summary>
        public string DescriptionPrefix => "Super jump from roof on roof";

        /// <summary>
        /// Runtime-состояние успешного super roof-to-roof прыжка.
        /// </summary>
        public HamsterStateEnum ExpectedSuccessState => HamsterStateEnum.SuperRoofJump;

        /// <summary>
        /// Дополнительный отступ перед bigAlive для раннего fire.
        /// </summary>
        public float BigAliveCollisionPaddingRatio => CollisionController.BigAliveJumpDamageOverlapThreshold;

        /// <summary>
        /// Возвращает runtime-дистанции super roof-to-roof прыжка.
        /// </summary>
        public bool TryGetTravel(out JumpFromRoofOnRoofTravel travel)
        {
            // Считывает travel основных animation clips.
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
            // Делегирует расчет super runtime resolver.
            return SuperRoofJumpOutcomeResolver.ResolveSuperRoofJump(obstacles, context);
        }
    }
}
