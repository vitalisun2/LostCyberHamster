using System.Collections.Generic;
using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.Bot.Strategies.Shared.JumpPlanning.JumpOnRoof;
using Assets.Scripts.Common;
using Assets.Scripts.GameEngine.Controllers;
using Assets.Scripts.GameEngine.Mechanics;
using Assets.Scripts.GameEngine.Mechanics.Models;
using Assets.Scripts.Gameplay.Enums;
using UnityEngine;

namespace Assets.Scripts.Bot.Strategies.JumpOnRoof
{
    /// <summary>
    /// Описывает runtime-отличия обычного jump-on-roof.
    /// </summary>
    internal sealed class JumpOnRoofPolicy : IJumpOnRoofPolicy
    {
        private const string _jumpClipName = "transform_jump";

        public BotActionKind ActionKind => BotActionKind.JumpOnRoof;
        public int EnergyCost => 10;
        public string DescriptionPrefix => "Jump on roof";
        public string LogTag => "JumpOnRoof";
        public HamsterStateEnum ExpectedRoofState => HamsterStateEnum.JumpOnRoof;
        public bool DamageBigAliveWithoutYByReach => true;

        /// <summary>
        /// Возвращает runtime-дистанцию обычного jump animation clip.
        /// </summary>
        public bool TryGetTravel(out float travel)
        {
            // Находит контроллер анимаций в активной сцене.
            TransformAnimatorController controller = Object.FindAnyObjectByType<TransformAnimatorController>();
            if (controller == null)
            {
                travel = 0f;
                return false;
            }

            // Считывает world shift для клипа прыжка.
            travel = HelpMethods.GetWorldShiftForClip(controller, _jumpClipName);
            return true;
        }

        /// <summary>
        /// Оставляет resolver-точку обычного прыжка равной точке первого input.
        /// </summary>
        public void GetResolveInput(
            float fireShift,
            float jumpTravel,
            out float resolveFireShift,
            out float resolveTravel)
        {
            // Передает обычный jump в resolver без смещения.
            resolveFireShift = fireShift;
            resolveTravel = jumpTravel;
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
