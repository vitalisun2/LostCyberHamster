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
        private const string SuperJumpClipName = "transform_super_jump";

        public BotActionKind ActionKind => BotActionKind.SuperJumpOn;
        public int EnergyCost => 20;
        public string DescriptionPrefix => "Super jump on";
        public string LogTag => "SuperJumpOn";
        public HamsterStateEnum ExpectedJumpOnState => HamsterStateEnum.SuperJumpOnObstacle;

        public bool TryGetTravel(out float travel)
        {
            TransformAnimatorController controller = Object.FindAnyObjectByType<TransformAnimatorController>();
            if (controller == null)
            {
                travel = 0f;
                return false;
            }

            float upgradeDelayTravel = GetSuperJumpUpgradeDelayTravel();
            travel = HelpMethods.GetWorldShiftForClip(controller, SuperJumpClipName) + upgradeDelayTravel;
            return true;
        }

        public void GetResolveInput(
            float fireShift,
            float jumpTravel,
            out float resolveFireShift,
            out float resolveTravel)
        {
            float upgradeDelayTravel = GetSuperJumpUpgradeDelayTravel();
            resolveFireShift = fireShift + upgradeDelayTravel;
            resolveTravel = jumpTravel - upgradeDelayTravel;
        }

        public JumpResolveResult Resolve(
            IReadOnlyList<JumpObstacleData> obstacles,
            JumpResolveContext context)
        {
            return SuperJumpOutcomeResolver.ResolveSuperJump(obstacles, context);
        }

        private static float GetSuperJumpUpgradeDelayTravel()
        {
            float halfDoubleJumpWindowSeconds = DoubleJumpDetector.DoubleJumpThreshold / 2f;
            return halfDoubleJumpWindowSeconds * Assets.Scripts.Consts.GameSpeedBase;
        }
    }
}
