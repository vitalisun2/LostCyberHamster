using System.Collections.Generic;
using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.Bot.Strategies.Shared;
using Assets.Scripts.Bot.Strategies.Shared.JumpPlanning.JumpOnFromRoof;
using Assets.Scripts.Common;
using Assets.Scripts.GameEngine.Controllers;
using Assets.Scripts.GameEngine.Mechanics;
using Assets.Scripts.GameEngine.Mechanics.Models;
using Assets.Scripts.Gameplay.Enums;
using UnityEngine;

namespace Assets.Scripts.Bot.Strategies.SuperJumpOnFromRoof
{
    /// <summary>
    /// Описывает runtime-отличия super roof-to-road jump-on.
    /// </summary>
    internal sealed class SuperJumpOnFromRoofPolicy : IJumpOnFromRoofPolicy
    {
        /// <summary>
        /// Имя клипа автоматического схода с крыши.
        /// </summary>
        private const string RunFromRoofClipName = "transform_run_from_roof";

        /// <summary>
        /// Имя клипа super roof jump.
        /// </summary>
        private const string SuperRoofJumpClipName = "transform_super_roof_jump";

        /// <summary>
        /// Имя клипа, используемого super roof-jump resolver-ом для схода на дорогу.
        /// </summary>
        private const string SuperJumpFromRoofClipName = "transform_super_jump_from_roof";

        /// <summary>
        /// Имя полного клипа super-напрыгивания с крыши.
        /// </summary>
        private const string SuperJumpOnObstacleFromRoofClipName = "transform_super_jump_on_obstacle_from_roof";

        /// <summary>
        /// Имя medium fallback-клипа автоматического схода с крыши.
        /// </summary>
        private const string MediumRunFromRoofClipName = "transform_medium_run_from_roof";

        /// <summary>
        /// Имя medium fallback-клипа super roof jump.
        /// </summary>
        private const string MediumSuperRoofJumpClipName = "transform_medium_super_roof_jump";

        /// <summary>
        /// Имя medium fallback-клипа super-схода на дорогу.
        /// </summary>
        private const string MediumSuperJumpFromRoofClipName = "transform_medium_super_jump_from_roof";

        /// <summary>
        /// Имя medium fallback-клипа super-напрыгивания с крыши.
        /// </summary>
        private const string MediumSuperJumpOnObstacleFromRoofClipName = "transform_medium_super_jump_on_obstacle_from_roof";

        /// <summary>
        /// Тип super roof-to-road jump-on action.
        /// </summary>
        public BotActionKind ActionKind => BotActionKind.SuperJumpOnFromRoof;

        /// <summary>
        /// Стоимость super roof-to-road jump-on.
        /// </summary>
        public int EnergyCost => 20;

        /// <summary>
        /// Префикс описания planned action.
        /// </summary>
        public string DescriptionPrefix => "Super jump on from roof";

        /// <summary>
        /// Тег диагностических сообщений super roof-to-road jump-on.
        /// </summary>
        public string LogTag => "SuperJumpOnFromRoof";

        /// <summary>
        /// Runtime outcome, который подтверждает успешное super-напрыгивание с крыши.
        /// </summary>
        public HamsterStateEnum ExpectedJumpOnState => HamsterStateEnum.SuperJumpOnObstacleFromRoof;

        /// <summary>
        /// Возвращает runtime-дистанции super roof-to-road jump-on.
        /// </summary>
        public bool TryGetTravel(out JumpOnFromRoofTravel travel)
        {
            if (!BotAnimationTravelProvider.TryGetTravel(RunFromRoofClipName, out float runFromRoofTravel)
                || !BotAnimationTravelProvider.TryGetTravel(SuperRoofJumpClipName, out float roofJumpTravel)
                || !BotAnimationTravelProvider.TryGetTravel(SuperJumpFromRoofClipName, out float resolveTravel)
                || !BotAnimationTravelProvider.TryGetTravel(SuperJumpOnObstacleFromRoofClipName, out float actionTravel))
            {
                travel = default;
                return false;
            }

            // Считывает medium fallback clips.
            if (runFromRoofTravel <= 0f)
                BotAnimationTravelProvider.TryGetTravel(MediumRunFromRoofClipName, out runFromRoofTravel);

            if (roofJumpTravel <= 0f)
                BotAnimationTravelProvider.TryGetTravel(MediumSuperRoofJumpClipName, out roofJumpTravel);

            if (resolveTravel <= 0f)
                BotAnimationTravelProvider.TryGetTravel(MediumSuperJumpFromRoofClipName, out resolveTravel);

            if (actionTravel <= 0f)
                BotAnimationTravelProvider.TryGetTravel(MediumSuperJumpOnObstacleFromRoofClipName, out actionTravel);

            // Учитывает путь мира до второго input super roof jump.
            actionTravel += SuperJumpOnFromRoofTiming.UpgradeDelayTravel;

            // Возвращает travel model.
            travel = new JumpOnFromRoofTravel(
                runFromRoofTravel,
                roofJumpTravel,
                resolveTravel,
                actionTravel,
                SuperJumpOnFromRoofTiming.UpgradeDelayTravel);
            return runFromRoofTravel > 0f
                && roofJumpTravel > 0f
                && resolveTravel > 0f
                && actionTravel > 0f;
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
