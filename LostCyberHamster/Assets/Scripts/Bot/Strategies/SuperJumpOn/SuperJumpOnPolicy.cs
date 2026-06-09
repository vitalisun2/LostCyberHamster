using System.Collections.Generic;
using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.Bot.Strategies.Shared;
using Assets.Scripts.Bot.Strategies.Shared.JumpOn;
using Assets.Scripts.Common;
using Assets.Scripts.GameEngine.Controllers;
using Assets.Scripts.GameEngine.Mechanics;
using Assets.Scripts.GameEngine.Mechanics.Models;
using Assets.Scripts.Gameplay.Enums;

namespace Assets.Scripts.Bot.Strategies.SuperJumpOn
{
    /// <summary>
    /// Описывает runtime-отличия ground super-jump-on.
    /// </summary>
    internal sealed class SuperJumpOnPolicy : IJumpOnPolicy
    {
        private const string SuperJumpClipName = "transform_super_jump";
        private const string SuperJumpOnClipName = "transform_super_jump_on";

        public BotActionKind ActionKind => BotActionKind.SuperJumpOn;
        public int EnergyCost => 20;
        public string DescriptionPrefix => "Super jump on";
        public string LogTag => "SuperJumpOn";
        public HamsterStateEnum ExpectedJumpOnState => HamsterStateEnum.SuperJumpOnObstacle;

        /// <summary>
        /// Считывает runtime-дистанции super-jump-on из animation clips и double-jump задержки.
        /// </summary>
        public bool TryGetTravel(out JumpOnTravel travel)
        {
            if (!BotAnimationTravelProvider.TryGetTravel(SuperJumpOnClipName, out float actionClipTravel)
                || !BotAnimationTravelProvider.TryGetTravel(SuperJumpClipName, out float resolveTravel))
            {
                travel = default;
                return false;
            }

            // Собирает дистанции resolver-точки и полного action.
            float upgradeDelayTravel = GetSuperJumpUpgradeDelayTravel();
            float actionTravel = actionClipTravel + upgradeDelayTravel;
            travel = new JumpOnTravel(
                actionTravel,
                resolveTravel,
                resolveFireShiftOffset: upgradeDelayTravel);
            return actionTravel > 0f && resolveTravel > 0f;
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
