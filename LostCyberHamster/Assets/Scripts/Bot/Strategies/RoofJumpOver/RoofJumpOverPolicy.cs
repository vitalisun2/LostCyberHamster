using System.Collections.Generic;
using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.Bot.Strategies.Shared;
using Assets.Scripts.Bot.Strategies.Shared.JumpPlanning.RoofJumpOver;
using Assets.Scripts.Common;
using Assets.Scripts.GameEngine.Controllers;
using Assets.Scripts.GameEngine.Mechanics;
using Assets.Scripts.GameEngine.Mechanics.Models;
using Assets.Scripts.Gameplay.Enums;
using UnityEngine;

namespace Assets.Scripts.Bot.Strategies.RoofJumpOver
{
    /// <summary>
    /// Описывает runtime-отличия обычного roof jump-over.
    /// </summary>
    internal sealed class RoofJumpOverPolicy : IRoofJumpOverPolicy
    {
        private const string RoofJumpClipName = "transform_roof_jump";
        private const string JumpFromRoofClipName = "transform_jump_from_roof";
        private const string MediumRoofJumpClipName = "transform_medium_roof_jump";
        private const string MediumJumpFromRoofClipName = "transform_medium_jump_from_roof";

        public BotActionKind ActionKind => BotActionKind.RoofJumpOver;
        public int EnergyCost => 10;
        public string DescriptionPrefix => "Roof jump over";
        public HamsterStateEnum ExpectedSuccessState => HamsterStateEnum.RoofJump;

        public bool TryGetTravel(out RoofJumpOverTravel travel)
        {
            if (!BotAnimationTravelProvider.TryGetTravel(RoofJumpClipName, out float roofJumpTravel)
                || !BotAnimationTravelProvider.TryGetTravel(JumpFromRoofClipName, out float jumpFromRoofTravel))
            {
                travel = default;
                return false;
            }

            // Считывает основные roof-jump клипы и fallback для medium roof.
            if (roofJumpTravel <= 0f)
                BotAnimationTravelProvider.TryGetTravel(MediumRoofJumpClipName, out roofJumpTravel);

            if (jumpFromRoofTravel <= 0f)
                BotAnimationTravelProvider.TryGetTravel(MediumJumpFromRoofClipName, out jumpFromRoofTravel);

            travel = new RoofJumpOverTravel(roofJumpTravel, jumpFromRoofTravel);
            return roofJumpTravel > 0f && jumpFromRoofTravel > 0f;
        }

        public JumpResolveResult Resolve(
            IReadOnlyList<JumpObstacleData> obstacles,
            RoofJumpResolveContext context)
        {
            return RoofJumpOutcomeResolver.ResolveRoofJump(obstacles, context);
        }
    }
}
