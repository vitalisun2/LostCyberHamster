using System.Collections.Generic;
using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.Bot.Strategies.Shared.JumpPlanning.JumpOnFromRoof;
using Assets.Scripts.Common;
using Assets.Scripts.GameEngine.Controllers;
using Assets.Scripts.GameEngine.Mechanics;
using Assets.Scripts.GameEngine.Mechanics.Models;
using Assets.Scripts.Gameplay.Enums;
using UnityEngine;

namespace Assets.Scripts.Bot.Strategies.JumpOnFromRoof
{
    /// <summary>
    /// Описывает runtime-отличия обычного roof-to-road jump-on.
    /// </summary>
    internal sealed class JumpOnFromRoofPolicy : IJumpOnFromRoofPolicy
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
        /// Имя клипа, используемого roof-jump resolver-ом для схода на дорогу.
        /// </summary>
        private const string JumpFromRoofClipName = "transform_jump_from_roof";

        /// <summary>
        /// Имя полного клипа обычного напрыгивания с крыши.
        /// </summary>
        private const string JumpOnFromRoofClipName = "transform_jump_on_from_roof";

        /// <summary>
        /// Имя medium fallback-клипа автоматического схода с крыши.
        /// </summary>
        private const string MediumRunFromRoofClipName = "transform_medium_run_from_roof";

        /// <summary>
        /// Имя medium fallback-клипа roof jump.
        /// </summary>
        private const string MediumRoofJumpClipName = "transform_medium_roof_jump";

        /// <summary>
        /// Имя medium fallback-клипа схода на дорогу.
        /// </summary>
        private const string MediumJumpFromRoofClipName = "transform_medium_jump_from_roof";

        /// <summary>
        /// Имя medium fallback-клипа напрыгивания с крыши.
        /// </summary>
        private const string MediumJumpOnFromRoofClipName = "transform_medium_jump_on_from_roof";

        /// <summary>
        /// Тип ordinary roof-to-road jump-on action.
        /// </summary>
        public BotActionKind ActionKind => BotActionKind.JumpOnFromRoof;

        /// <summary>
        /// Стоимость обычного roof-to-road jump-on.
        /// </summary>
        public int EnergyCost => 10;

        /// <summary>
        /// Префикс описания planned action.
        /// </summary>
        public string DescriptionPrefix => "Jump on from roof";

        /// <summary>
        /// Тег диагностических сообщений ordinary roof-to-road jump-on.
        /// </summary>
        public string LogTag => "JumpOnFromRoof";

        /// <summary>
        /// Runtime outcome, который подтверждает успешное напрыгивание с крыши.
        /// </summary>
        public HamsterStateEnum ExpectedJumpOnState => HamsterStateEnum.JumpOnObstacleFromRoof;

        /// <summary>
        /// Возвращает runtime-дистанции ordinary roof-to-road jump-on.
        /// </summary>
        public bool TryGetTravel(out JumpOnFromRoofTravel travel)
        {
            // Находит контроллер анимаций.
            TransformAnimatorController controller = Object.FindAnyObjectByType<TransformAnimatorController>();
            if (controller == null)
            {
                travel = default;
                return false;
            }

            // Считывает основные runtime clips.
            float runFromRoofTravel = HelpMethods.GetWorldShiftForClip(controller, RunFromRoofClipName);
            float roofJumpTravel = HelpMethods.GetWorldShiftForClip(controller, RoofJumpClipName);
            float resolveTravel = HelpMethods.GetWorldShiftForClip(controller, JumpFromRoofClipName);
            float actionTravel = HelpMethods.GetWorldShiftForClip(controller, JumpOnFromRoofClipName);

            // Считывает medium fallback clips.
            if (runFromRoofTravel <= 0f)
                runFromRoofTravel = HelpMethods.GetWorldShiftForClip(controller, MediumRunFromRoofClipName);

            if (roofJumpTravel <= 0f)
                roofJumpTravel = HelpMethods.GetWorldShiftForClip(controller, MediumRoofJumpClipName);

            if (resolveTravel <= 0f)
                resolveTravel = HelpMethods.GetWorldShiftForClip(controller, MediumJumpFromRoofClipName);

            if (actionTravel <= 0f)
                actionTravel = HelpMethods.GetWorldShiftForClip(controller, MediumJumpOnFromRoofClipName);

            // Возвращает travel model.
            travel = new JumpOnFromRoofTravel(
                runFromRoofTravel,
                roofJumpTravel,
                resolveTravel,
                actionTravel,
                resolveFireShiftOffset: 0f);
            return runFromRoofTravel > 0f
                && roofJumpTravel > 0f
                && resolveTravel > 0f
                && actionTravel > 0f;
        }

        /// <summary>
        /// Вызывает runtime resolver ordinary roof jump.
        /// </summary>
        public JumpResolveResult Resolve(
            IReadOnlyList<JumpObstacleData> obstacles,
            RoofJumpResolveContext context)
        {
            return RoofJumpOutcomeResolver.ResolveRoofJump(obstacles, context);
        }
    }
}
