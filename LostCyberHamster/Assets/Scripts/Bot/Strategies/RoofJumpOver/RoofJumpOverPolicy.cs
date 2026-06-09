using System.Collections.Generic;
using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.Bot.Strategies.Shared;
using Assets.Scripts.Bot.Strategies.Shared.RoofJumpOver;
using Assets.Scripts.Common;
using Assets.Scripts.GameEngine.Mechanics;
using Assets.Scripts.GameEngine.Mechanics.Models;
using Assets.Scripts.Gameplay.Enums;

namespace Assets.Scripts.Bot.Strategies.RoofJumpOver
{
    /// <summary>
    /// Описывает runtime-отличия обычного roof jump-over.
    /// </summary>
    internal sealed class RoofJumpOverPolicy : IRoofJumpOverPolicy
    {
        /// <summary>
        /// Имя animation clip обычного roof jump.
        /// </summary>
        private const string RoofJumpClipName = "transform_roof_jump";

        /// <summary>
        /// Имя animation clip обычного fallback jump-from-roof.
        /// </summary>
        private const string JumpFromRoofClipName = "transform_jump_from_roof";

        /// <summary>
        /// Имя fallback animation clip обычного roof jump для medium roof.
        /// </summary>
        private const string MediumRoofJumpClipName = "transform_medium_roof_jump";

        /// <summary>
        /// Имя fallback animation clip обычного jump-from-roof для medium roof.
        /// </summary>
        private const string MediumJumpFromRoofClipName = "transform_medium_jump_from_roof";

        /// <summary>
        /// Тип planned action обычного roof jump-over.
        /// </summary>
        public BotActionKind ActionKind => BotActionKind.RoofJumpOver;

        /// <summary>
        /// Стоимость обычного roof jump-over в энергии.
        /// </summary>
        public int EnergyCost => 10;

        /// <summary>
        /// Префикс описания обычного roof jump-over.
        /// </summary>
        public string DescriptionPrefix => "Roof jump over";

        /// <summary>
        /// Runtime-состояние успешного обычного roof jump-over.
        /// </summary>
        public HamsterStateEnum ExpectedSuccessState => HamsterStateEnum.RoofJump;

        /// <summary>
        /// Возвращает runtime-дистанции обычного roof jump-over.
        /// </summary>
        public bool TryGetTravel(out RoofJumpOverTravel travel)
        {
            // Считывает travel основных animation clips.
            if (!BotAnimationTravelProvider.TryGetTravel(RoofJumpClipName, out float roofJumpTravel)
                || !BotAnimationTravelProvider.TryGetTravel(JumpFromRoofClipName, out float jumpFromRoofTravel))
            {
                travel = default;
                return false;
            }

            // Считывает fallback для medium roof.
            if (roofJumpTravel <= 0f)
                BotAnimationTravelProvider.TryGetTravel(MediumRoofJumpClipName, out roofJumpTravel);

            if (jumpFromRoofTravel <= 0f)
                BotAnimationTravelProvider.TryGetTravel(MediumJumpFromRoofClipName, out jumpFromRoofTravel);

            // Возвращает travel model.
            travel = new RoofJumpOverTravel(roofJumpTravel, jumpFromRoofTravel);
            return roofJumpTravel > 0f && jumpFromRoofTravel > 0f;
        }

        /// <summary>
        /// Вызывает runtime resolver обычного roof jump.
        /// </summary>
        public JumpResolveResult Resolve(
            IReadOnlyList<JumpObstacleData> obstacles,
            RoofJumpResolveContext context)
        {
            // Делегирует расчет обычному runtime resolver.
            return RoofJumpOutcomeResolver.ResolveRoofJump(obstacles, context);
        }
    }
}
