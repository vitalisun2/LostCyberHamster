using System.Collections.Generic;
using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.Bot.Strategies.Shared;
using Assets.Scripts.Bot.Strategies.Shared.JumpOnRoof;
using Assets.Scripts.Common;
using Assets.Scripts.GameEngine.Controllers;
using Assets.Scripts.GameEngine.Mechanics;
using Assets.Scripts.GameEngine.Mechanics.Models;
using Assets.Scripts.Gameplay.Enums;

namespace Assets.Scripts.Bot.Strategies.SuperJumpOnRoof
{
    /// <summary>
    /// Описывает runtime-отличия super-jump-on-roof.
    /// </summary>
    internal sealed class SuperJumpOnRoofPolicy : IJumpOnRoofPolicy
    {
        private const string SuperJumpClipName = "transform_super_jump";

        public BotActionKind ActionKind => BotActionKind.SuperJumpOnRoof;
        public int EnergyCost => 20;
        public string DescriptionPrefix => "Super jump on roof";
        public string LogTag => "SuperJumpOnRoof";
        public HamsterStateEnum ExpectedRoofState => HamsterStateEnum.SuperJumpOnRoof;
        public bool DamageBigAliveWithoutYByReach => false;

        /// <summary>
        /// Возвращает runtime-дистанцию super jump с учетом задержки upgrade input.
        /// </summary>
        public bool TryGetTravel(out float travel)
        {
            if (!BotAnimationTravelProvider.TryGetTravel(SuperJumpClipName, out float clipTravel))
            {
                travel = default;
                return false;
            }

            float upgradeDelayTravel = GetSuperJumpUpgradeDelayTravel();
            travel = clipTravel + upgradeDelayTravel;
            return travel > 0f;
        }

        /// <summary>
        /// Переносит resolver-точку super jump на момент второго input.
        /// </summary>
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

        /// <summary>
        /// Вызывает runtime resolver super jump.
        /// </summary>
        public JumpResolveResult Resolve(
            IReadOnlyList<JumpObstacleData> obstacles,
            JumpResolveContext context)
        {
            return SuperJumpOutcomeResolver.ResolveSuperJump(obstacles, context);
        }

        /// <summary>
        /// Возвращает путь мира за задержку upgrade input.
        /// </summary>
        private static float GetSuperJumpUpgradeDelayTravel()
        {
            float halfDoubleJumpWindowSeconds = DoubleJumpDetector.DoubleJumpThreshold / 2f;
            return halfDoubleJumpWindowSeconds * Assets.Scripts.Consts.GameSpeedBase;
        }
    }
}
