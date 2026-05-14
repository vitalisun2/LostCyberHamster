using System.Collections.Generic;
using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.Bot.Strategies.Shared.JumpPlanning.RoofJumpOver;
using Assets.Scripts.Common;
using Assets.Scripts.GameEngine.Controllers;
using Assets.Scripts.GameEngine.Mechanics;
using Assets.Scripts.GameEngine.Mechanics.Models;
using Assets.Scripts.Gameplay.Enums;
using UnityEngine;

namespace Assets.Scripts.Bot.Strategies.SuperRoofJumpOver
{
    /// <summary>
    /// Описывает runtime-отличия super roof jump-over.
    /// </summary>
    internal sealed class SuperRoofJumpOverPolicy : IRoofJumpOverPolicy
    {
        private const string SuperRoofJumpClipName = "transform_super_roof_jump";
        private const string SuperJumpFromRoofClipName = "transform_super_jump_from_roof";
        private const string MediumSuperRoofJumpClipName = "transform_medium_super_roof_jump";
        private const string MediumSuperJumpFromRoofClipName = "transform_medium_super_jump_from_roof";

        public BotActionKind ActionKind => BotActionKind.SuperRoofJumpOver;
        public int EnergyCost => 20;
        public string DescriptionPrefix => "Super roof jump over";
        public HamsterStateEnum ExpectedSuccessState => HamsterStateEnum.SuperRoofJump;

        public bool TryGetTravel(out RoofJumpOverTravel travel)
        {
            TransformAnimatorController controller = Object.FindAnyObjectByType<TransformAnimatorController>();
            if (controller == null)
            {
                travel = default;
                return false;
            }

            float roofJumpTravel = HelpMethods.GetWorldShiftForClip(controller, SuperRoofJumpClipName);
            float jumpFromRoofTravel = HelpMethods.GetWorldShiftForClip(controller, SuperJumpFromRoofClipName);
            if (roofJumpTravel <= 0f)
                roofJumpTravel = HelpMethods.GetWorldShiftForClip(controller, MediumSuperRoofJumpClipName);

            if (jumpFromRoofTravel <= 0f)
                jumpFromRoofTravel = HelpMethods.GetWorldShiftForClip(controller, MediumSuperJumpFromRoofClipName);

            float upgradeDelayTravel = GetSuperRoofJumpUpgradeDelayTravel();
            roofJumpTravel += upgradeDelayTravel;
            jumpFromRoofTravel += upgradeDelayTravel;

            travel = new RoofJumpOverTravel(roofJumpTravel, jumpFromRoofTravel);
            return roofJumpTravel > 0f && jumpFromRoofTravel > 0f;
        }

        public JumpResolveResult Resolve(
            IReadOnlyList<JumpObstacleData> obstacles,
            RoofJumpResolveContext context)
        {
            return SuperRoofJumpOutcomeResolver.ResolveSuperRoofJump(obstacles, context);
        }

        private static float GetSuperRoofJumpUpgradeDelayTravel()
        {
            float halfDoubleJumpWindowSeconds = DoubleJumpDetector.DoubleJumpThreshold / 2f;
            return halfDoubleJumpWindowSeconds * Assets.Scripts.Consts.GameSpeedBase;
        }
    }
}