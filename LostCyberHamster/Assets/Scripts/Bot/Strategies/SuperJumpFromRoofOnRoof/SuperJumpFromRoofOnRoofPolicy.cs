using System.Collections.Generic;
using Assets.Scripts.Bot.PlanState;
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
            // Находит контроллер анимаций.
            TransformAnimatorController controller = Object.FindAnyObjectByType<TransformAnimatorController>();
            if (controller == null)
            {
                travel = default;
                return false;
            }

            // Считывает основные runtime clips.
            float runFromRoofTravel = HelpMethods.GetWorldShiftForClip(controller, _runFromRoofClipName);
            float roofJumpTravel = HelpMethods.GetWorldShiftForClip(controller, _superRoofJumpClipName);
            float jumpFromRoofTravel = HelpMethods.GetWorldShiftForClip(controller, _superJumpFromRoofClipName);

            // Считывает medium fallback clips.
            if (runFromRoofTravel <= 0f)
                runFromRoofTravel = HelpMethods.GetWorldShiftForClip(controller, _mediumRunFromRoofClipName);

            if (roofJumpTravel <= 0f)
                roofJumpTravel = HelpMethods.GetWorldShiftForClip(controller, _mediumSuperRoofJumpClipName);

            if (jumpFromRoofTravel <= 0f)
                jumpFromRoofTravel = HelpMethods.GetWorldShiftForClip(controller, _mediumSuperJumpFromRoofClipName);

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
