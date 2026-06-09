using System.Collections.Generic;
using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.Bot.Planning;
using Assets.Scripts.Bot.Strategies.Shared;
using Assets.Scripts.Bot.Strategies.Shared.JumpOver;
using Assets.Scripts.Common;
using Assets.Scripts.Common.Models;
using Assets.Scripts.GameEngine.Controllers;
using Assets.Scripts.GameEngine.Mechanics;
using Assets.Scripts.GameEngine.Mechanics.Models;
using Assets.Scripts.Gameplay.Enums;
using UnityEngine;

namespace Assets.Scripts.Bot.Strategies.SuperJumpOver
{
    /// <summary>
    /// Описывает runtime-отличия ground super-jump-over.
    /// </summary>
    internal sealed class SuperJumpOverPolicy : IJumpOverPolicy
    {
        private const string SuperJumpClipName = "transform_super_jump";

        public BotActionKind ActionKind => BotActionKind.SuperJumpOver;
        public int EnergyCost => 20;
        public string DescriptionPrefix => "Super jump over";
        public HamsterStateEnum ExpectedOverState => HamsterStateEnum.SuperJumpOver;
        public bool DamageBigAliveWithoutYByReach => false;
        public float BigAliveCollisionPaddingRatio => CollisionController.BigAliveJumpDamageOverlapThreshold;

        public bool CanJumpOverObstacle(ObstacleTypeEnum obstacleType)
        {
            return ObstacleClassifier.CanSuperJumpOverOnGround(obstacleType);
        }

        public bool TryGetTravel(out float travel)
        {
            if (!BotAnimationTravelProvider.TryGetTravel(SuperJumpClipName, out float clipTravel))
            {
                travel = default;
                return false;
            }

            float upgradeDelayTravel = GetSuperJumpUpgradeDelayTravel();
            travel = clipTravel + upgradeDelayTravel;
            return true;
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
