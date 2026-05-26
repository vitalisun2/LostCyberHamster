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
        private const string JumpClipName = "transform_jump";

        public BotActionKind ActionKind => BotActionKind.JumpOn;
        public int EnergyCost => 10;
        public string DescriptionPrefix => "Jump on";
        public string LogTag => "JumpOn";
        public HamsterStateEnum ExpectedJumpOnState => HamsterStateEnum.JumpOnObstacle;

        public bool TryGetTravel(out float travel)
        {
            TransformAnimatorController controller = Object.FindAnyObjectByType<TransformAnimatorController>();
            if (controller == null)
            {
                travel = 0f;
                return false;
            }

            travel = HelpMethods.GetWorldShiftForClip(controller, JumpClipName);
            return true;
        }

        public void GetResolveInput(
            float fireShift,
            float jumpTravel,
            out float resolveFireShift,
            out float resolveTravel)
        {
            resolveFireShift = fireShift;
            resolveTravel = jumpTravel;
        }

        public JumpResolveResult Resolve(
            IReadOnlyList<JumpObstacleData> obstacles,
            JumpResolveContext context)
        {
            return JumpOutcomeResolver.ResolveJump(obstacles, context);
        }
    }
}
