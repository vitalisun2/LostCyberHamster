using System.Collections.Generic;
using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.Bot.Strategies.Shared.JumpPlanning.JumpOn;
using Assets.Scripts.Common;
using Assets.Scripts.GameEngine.Controllers;
using Assets.Scripts.GameEngine.Mechanics;
using Assets.Scripts.GameEngine.Mechanics.Models;
using Assets.Scripts.Gameplay.Enums;
using UnityEngine;

namespace Assets.Scripts.Bot.Strategies.JumpOn
{
    /// <summary>
    /// Описывает runtime-отличия обычного ground jump-on.
    /// </summary>
    internal sealed class JumpOnPolicy : IJumpOnPolicy
    {
        /// <summary>
        /// Название клипа обычного прыжка, используемого runtime resolver-ом.
        /// </summary>
        private const string JumpClipName = "transform_jump";

        /// <summary>
        /// Название клипа полного обычного jump-on action.
        /// </summary>
        private const string JumpOnClipName = "transform_jump_on";

        /// <summary>
        /// Возвращает тип обычного jump-on action.
        /// </summary>
        public BotActionKind ActionKind => BotActionKind.JumpOn;

        /// <summary>
        /// Возвращает стоимость обычного jump-on в энергии.
        /// </summary>
        public int EnergyCost => 10;

        /// <summary>
        /// Возвращает префикс описания обычного jump-on action.
        /// </summary>
        public string DescriptionPrefix => "Jump on";

        /// <summary>
        /// Возвращает тег диагностических сообщений обычного jump-on.
        /// </summary>
        public string LogTag => "JumpOn";

        /// <summary>
        /// Возвращает runtime-состояние, подтверждающее успешное напрыгивание.
        /// </summary>
        public HamsterStateEnum ExpectedJumpOnState => HamsterStateEnum.JumpOnObstacle;

        /// <summary>
        /// Считывает runtime-дистанции обычного jump-on из animation clips.
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
            float actionTravel = HelpMethods.GetWorldShiftForClip(controller, JumpOnClipName);
            float resolveTravel = HelpMethods.GetWorldShiftForClip(controller, JumpClipName);
            travel = new JumpOnTravel(
                actionTravel,
                resolveTravel,
                resolveFireShiftOffset: 0f);
            return true;
        }

        /// <summary>
        /// Вызывает runtime resolver обычного прыжка.
        /// </summary>
        public JumpResolveResult Resolve(
            IReadOnlyList<JumpObstacleData> obstacles,
            JumpResolveContext context)
        {
            return JumpOutcomeResolver.ResolveJump(obstacles, context);
        }
    }
}
