using System.Collections.Generic;
using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.Bot.Strategies.Shared.JumpPlanning.JumpFromRoof;
using Assets.Scripts.Common;
using Assets.Scripts.GameEngine.Controllers;
using Assets.Scripts.GameEngine.Mechanics;
using Assets.Scripts.GameEngine.Mechanics.Models;
using Assets.Scripts.Gameplay.Enums;
using UnityEngine;

namespace Assets.Scripts.Bot.Strategies.SuperJumpFromRoof
{
    /// <summary>
    /// Описывает runtime-отличия super-прыжка с крыши на дорогу.
    /// </summary>
    internal sealed class SuperJumpFromRoofPolicy : IJumpFromRoofPolicy
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
        /// Имя клипа super-прыжка с крыши на дорогу.
        /// </summary>
        private const string SuperJumpFromRoofClipName = "transform_super_jump_from_roof";

        /// <summary>
        /// Имя medium fallback-клипа автоматического схода с крыши.
        /// </summary>
        private const string MediumRunFromRoofClipName = "transform_medium_run_from_roof";

        /// <summary>
        /// Имя medium fallback-клипа super roof jump.
        /// </summary>
        private const string MediumSuperRoofJumpClipName = "transform_medium_super_roof_jump";

        /// <summary>
        /// Имя medium fallback-клипа super-прыжка с крыши на дорогу.
        /// </summary>
        private const string MediumSuperJumpFromRoofClipName = "transform_medium_super_jump_from_roof";

        /// <summary>
        /// Тип action для super-прыжка с крыши.
        /// </summary>
        public BotActionKind ActionKind => BotActionKind.SuperJumpFromRoof;

        /// <summary>
        /// Стоимость super-прыжка с крыши.
        /// </summary>
        public int EnergyCost => 20;

        /// <summary>
        /// Префикс описания planned action.
        /// </summary>
        public string DescriptionPrefix => "Super jump from roof";

        /// <summary>
        /// Runtime outcome, который считается успешным для этой strategy.
        /// </summary>
        public HamsterStateEnum ExpectedSuccessState => HamsterStateEnum.SuperJumpFromRoof;

        /// <summary>
        /// Super-прыжок с крыши не добавляет дополнительный отступ для bigAlive.
        /// </summary>
        public float BigAliveCollisionPaddingRatio => 0;

        /// <summary>
        /// Возвращает runtime-дистанции super-прыжка с крыши.
        /// </summary>
        public bool TryGetTravel(out JumpFromRoofTravel travel)
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
            float roofJumpTravel = HelpMethods.GetWorldShiftForClip(controller, SuperRoofJumpClipName);
            float jumpFromRoofTravel = HelpMethods.GetWorldShiftForClip(controller, SuperJumpFromRoofClipName);

            // Считывает medium fallback clips.
            if (runFromRoofTravel <= 0f)
                runFromRoofTravel = HelpMethods.GetWorldShiftForClip(controller, MediumRunFromRoofClipName);

            if (roofJumpTravel <= 0f)
                roofJumpTravel = HelpMethods.GetWorldShiftForClip(controller, MediumSuperRoofJumpClipName);

            if (jumpFromRoofTravel <= 0f)
                jumpFromRoofTravel = HelpMethods.GetWorldShiftForClip(controller, MediumSuperJumpFromRoofClipName);

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
        /// Возвращает путь мира за задержку upgrade-запроса super roof jump.
        /// </summary>
        private static float GetSuperRoofJumpUpgradeDelayTravel()
        {
            float halfDoubleJumpWindowSeconds = DoubleJumpDetector.DoubleJumpThreshold / 2f;
            return halfDoubleJumpWindowSeconds * Assets.Scripts.Consts.GameSpeedBase;
        }
    }
}
