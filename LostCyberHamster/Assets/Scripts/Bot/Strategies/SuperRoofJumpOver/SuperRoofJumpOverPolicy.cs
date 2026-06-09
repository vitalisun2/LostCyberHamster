using System.Collections.Generic;
using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.Bot.Strategies.Shared;
using Assets.Scripts.Bot.Strategies.Shared.RoofJumpOver;
using Assets.Scripts.Common;
using Assets.Scripts.GameEngine.Controllers;
using Assets.Scripts.GameEngine.Mechanics;
using Assets.Scripts.GameEngine.Mechanics.Models;
using Assets.Scripts.Gameplay.Enums;

namespace Assets.Scripts.Bot.Strategies.SuperRoofJumpOver
{
    /// <summary>
    /// Описывает runtime-отличия super roof jump-over.
    /// </summary>
    internal sealed class SuperRoofJumpOverPolicy : IRoofJumpOverPolicy
    {
        /// <summary>
        /// Имя animation clip super roof jump.
        /// </summary>
        private const string SuperRoofJumpClipName = "transform_super_roof_jump";

        /// <summary>
        /// Имя animation clip super fallback jump-from-roof.
        /// </summary>
        private const string SuperJumpFromRoofClipName = "transform_super_jump_from_roof";

        /// <summary>
        /// Имя fallback animation clip super roof jump для medium roof.
        /// </summary>
        private const string MediumSuperRoofJumpClipName = "transform_medium_super_roof_jump";

        /// <summary>
        /// Имя fallback animation clip super jump-from-roof для medium roof.
        /// </summary>
        private const string MediumSuperJumpFromRoofClipName = "transform_medium_super_jump_from_roof";

        /// <summary>
        /// Тип planned action super roof jump-over.
        /// </summary>
        public BotActionKind ActionKind => BotActionKind.SuperRoofJumpOver;

        /// <summary>
        /// Стоимость super roof jump-over в энергии.
        /// </summary>
        public int EnergyCost => 20;

        /// <summary>
        /// Префикс описания super roof jump-over.
        /// </summary>
        public string DescriptionPrefix => "Super roof jump over";

        /// <summary>
        /// Runtime-состояние успешного super roof jump-over.
        /// </summary>
        public HamsterStateEnum ExpectedSuccessState => HamsterStateEnum.SuperRoofJump;

        /// <summary>
        /// Возвращает runtime-дистанции super roof jump-over с учетом delay второго input.
        /// </summary>
        public bool TryGetTravel(out RoofJumpOverTravel travel)
        {
            // Считывает travel основных animation clips.
            if (!BotAnimationTravelProvider.TryGetTravel(SuperRoofJumpClipName, out float roofJumpTravel)
                || !BotAnimationTravelProvider.TryGetTravel(SuperJumpFromRoofClipName, out float jumpFromRoofTravel))
            {
                travel = default;
                return false;
            }

            // Считывает fallback для medium roof.
            if (roofJumpTravel <= 0f)
                BotAnimationTravelProvider.TryGetTravel(MediumSuperRoofJumpClipName, out roofJumpTravel);

            if (jumpFromRoofTravel <= 0f)
                BotAnimationTravelProvider.TryGetTravel(MediumSuperJumpFromRoofClipName, out jumpFromRoofTravel);

            // Добавляет travel задержки второго input.
            float upgradeDelayTravel = GetSuperRoofJumpUpgradeDelayTravel();
            roofJumpTravel += upgradeDelayTravel;
            jumpFromRoofTravel += upgradeDelayTravel;

            // Возвращает travel model.
            travel = new RoofJumpOverTravel(roofJumpTravel, jumpFromRoofTravel);
            return roofJumpTravel > 0f && jumpFromRoofTravel > 0f;
        }

        /// <summary>
        /// Вызывает runtime resolver super roof jump.
        /// </summary>
        public JumpResolveResult Resolve(
            IReadOnlyList<JumpObstacleData> obstacles,
            RoofJumpResolveContext context)
        {
            // Делегирует расчет super runtime resolver.
            return SuperRoofJumpOutcomeResolver.ResolveSuperRoofJump(obstacles, context);
        }

        /// <summary>
        /// Возвращает пройденную дистанцию за половину окна double-jump upgrade.
        /// </summary>
        private static float GetSuperRoofJumpUpgradeDelayTravel()
        {
            // Переводит delay второго input в world travel.
            float halfDoubleJumpWindowSeconds = DoubleJumpDetector.DoubleJumpThreshold / 2f;
            return halfDoubleJumpWindowSeconds * Assets.Scripts.Consts.GameSpeedBase;
        }
    }
}
