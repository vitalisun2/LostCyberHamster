using System.Collections.Generic;
using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.Bot.Planning;
using Assets.Scripts.Bot.Strategies.Shared;
using Assets.Scripts.Bot.Strategies.Shared.JumpPlanning.JumpOver;
using Assets.Scripts.Common;
using Assets.Scripts.Common.Models;
using Assets.Scripts.GameEngine.Controllers;
using Assets.Scripts.GameEngine.Mechanics;
using Assets.Scripts.GameEngine.Mechanics.Models;
using Assets.Scripts.Gameplay.Enums;
using UnityEngine;

namespace Assets.Scripts.Bot.Strategies.JumpOver
{
    /// <summary>
    /// Описывает runtime-отличия обычного ground jump-over.
    /// </summary>
    internal sealed class JumpOverPolicy : IJumpOverPolicy
    {
        private const string JumpClipName = "transform_jump";

        public BotActionKind ActionKind => BotActionKind.JumpOver;
        public int EnergyCost => 10;
        public string DescriptionPrefix => "Jump over";
        public HamsterStateEnum ExpectedOverState => HamsterStateEnum.JumpOver;
        public bool DamageBigAliveWithoutYByReach => true;
        public float BigAliveCollisionPaddingRatio => 0f;

        public bool CanJumpOverObstacle(ObstacleTypeEnum obstacleType)
        {
            return ObstacleClassifier.CanJumpOverOnGround(obstacleType);
        }

        public bool TryGetTravel(out float travel)
        {
            return BotAnimationTravelProvider.TryGetTravel(JumpClipName, out travel);
        }

        public JumpResolveResult Resolve(
            IReadOnlyList<JumpObstacleData> obstacles,
            JumpResolveContext context)
        {
            return JumpOutcomeResolver.ResolveJump(obstacles, context);
        }
    }
}
