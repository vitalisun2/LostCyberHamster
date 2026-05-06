using System.Collections.Generic;
using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.Bot.Strategies.Shared.JumpPlanning;
using Assets.Scripts.Common;
using Assets.Scripts.GameEngine.Controllers;
using Assets.Scripts.GameEngine.Mechanics;
using Assets.Scripts.GameEngine.Mechanics.Models;
using Assets.Scripts.Gameplay.Enums;
using UnityEngine;

namespace Assets.Scripts.Bot.Strategies.SuperJumpOnRoof
{
    /// <summary>
    /// Описывает runtime-отличия super-jump-on-roof.
    /// </summary>
    internal sealed class SuperJumpOnRoofPolicy : IJumpOnRoofPolicy
    {
        private const string _superJumpClipName = "transform_super_jump";

        public BotActionKind ActionKind => BotActionKind.SuperJumpOnRoof;
        public int EnergyCost => 20;
        public string DescriptionPrefix => "Super jump on roof";
        public string LogTag => "SuperJumpOnRoof";
        public HamsterStateEnum ExpectedRoofState => HamsterStateEnum.SuperJumpOnRoof;
        public bool DamageBigAliveWithoutYByReach => false;

        /// <summary>
        /// Возвращает runtime-дистанцию super jump с учётом задержки upgrade-запроса.
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

            // Складывает дистанцию super jump clip и путь мира за половину double-jump окна.
            float halfDoubleJumpWindowSeconds = DoubleJumpDetector.DoubleJumpThreshold / 2f;
            float upgradeDelayTravel = halfDoubleJumpWindowSeconds * Assets.Scripts.Consts.GameSpeedBase;
            travel = HelpMethods.GetWorldShiftForClip(controller, _superJumpClipName) + upgradeDelayTravel;
            return true;
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
    }
}
