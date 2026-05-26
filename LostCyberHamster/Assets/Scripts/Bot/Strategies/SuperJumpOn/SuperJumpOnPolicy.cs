using System.Collections.Generic;
using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.Bot.Strategies.Shared.JumpPlanning.JumpOn;
using Assets.Scripts.Common;
using Assets.Scripts.GameEngine.Controllers;
using Assets.Scripts.GameEngine.Mechanics;
using Assets.Scripts.GameEngine.Mechanics.Models;
using Assets.Scripts.Gameplay.Enums;
using UnityEngine;

namespace Assets.Scripts.Bot.Strategies.SuperJumpOn
{
    /// <summary>
    /// Описывает runtime-отличия ground super-jump-on.
    /// </summary>
    internal sealed class SuperJumpOnPolicy : IJumpOnPolicy
    {
        /// <summary>
        /// Название клипа super-jump, используемого runtime resolver-ом.
        /// </summary>
        private const string SuperJumpClipName = "transform_super_jump";

        /// <summary>
        /// Название клипа полного super-jump-on action.
        /// </summary>
        private const string SuperJumpOnClipName = "transform_super_jump_on";

        /// <summary>
        /// Возвращает тип super-jump-on action.
        /// </summary>
        public BotActionKind ActionKind => BotActionKind.SuperJumpOn;

        /// <summary>
        /// Возвращает стоимость super-jump-on в энергии.
        /// </summary>
        public int EnergyCost => 20;

        /// <summary>
        /// Возвращает префикс описания super-jump-on action.
        /// </summary>
        public string DescriptionPrefix => "Super jump on";

        /// <summary>
        /// Возвращает тег диагностических сообщений super-jump-on.
        /// </summary>
        public string LogTag => "SuperJumpOn";

        /// <summary>
        /// Возвращает runtime-состояние, подтверждающее успешное super-напрыгивание.
        /// </summary>
        public HamsterStateEnum ExpectedJumpOnState => HamsterStateEnum.SuperJumpOnObstacle;

        /// <summary>
        /// Считывает runtime-дистанции super-jump-on из animation clips и double-jump задержки.
        /// </summary>
        public bool TryGetTravel(out JumpOnTravel travel)
        {
            // Находит controller с jump animation clips.
            TransformAnimatorController controller = Object.FindAnyObjectByType<TransformAnimatorController>();
            if (controller == null)
            {
                travel = default;
                return false;
            }

            // Собирает дистанции resolver-точки и полного action.
            float upgradeDelayTravel = GetSuperJumpUpgradeDelayTravel();
            float actionTravel = HelpMethods.GetWorldShiftForClip(controller, SuperJumpOnClipName) + upgradeDelayTravel;
            float resolveTravel = HelpMethods.GetWorldShiftForClip(controller, SuperJumpClipName);
            travel = new JumpOnTravel(
                actionTravel,
                resolveTravel,
                resolveFireShiftOffset: upgradeDelayTravel);
            return true;
        }

        /// <summary>
        /// Вызывает runtime resolver super-jump.
        /// </summary>
        public JumpResolveResult Resolve(
            IReadOnlyList<JumpObstacleData> obstacles,
            JumpResolveContext context)
        {
            return SuperJumpOutcomeResolver.ResolveSuperJump(obstacles, context);
        }

        /// <summary>
        /// Возвращает дистанцию, которую мир проходит до второго tap для super-jump.
        /// </summary>
        private static float GetSuperJumpUpgradeDelayTravel()
        {
            float halfDoubleJumpWindowSeconds = DoubleJumpDetector.DoubleJumpThreshold / 2f;
            return halfDoubleJumpWindowSeconds * Assets.Scripts.Consts.GameSpeedBase;
        }
    }
}
