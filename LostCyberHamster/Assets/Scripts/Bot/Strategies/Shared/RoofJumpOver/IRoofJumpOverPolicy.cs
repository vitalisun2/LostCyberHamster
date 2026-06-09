using System.Collections.Generic;
using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.GameEngine.Mechanics.Models;
using Assets.Scripts.Gameplay.Enums;

namespace Assets.Scripts.Bot.Strategies.Shared.RoofJumpOver
{
    /// <summary>
    /// Описывает runtime-различия вариантов перепрыгивания hazards во время RoofRun.
    /// </summary>
    internal interface IRoofJumpOverPolicy
    {
        /// <summary>
        /// Тип planned action, который создает стратегия с этой policy.
        /// </summary>
        BotActionKind ActionKind { get; }

        /// <summary>
        /// Стоимость action в энергии хомяка.
        /// </summary>
        int EnergyCost { get; }

        /// <summary>
        /// Префикс описания planned action для диагностик.
        /// </summary>
        string DescriptionPrefix { get; }

        /// <summary>
        /// Runtime-состояние, которое resolver должен вернуть для успешного roof jump-over.
        /// </summary>
        HamsterStateEnum ExpectedSuccessState { get; }

        /// <summary>
        /// Возвращает runtime-дистанции roof jump и fallback jump-from-roof.
        /// </summary>
        bool TryGetTravel(out RoofJumpOverTravel travel);

        /// <summary>
        /// Вызывает runtime resolver конкретного варианта roof jump.
        /// </summary>
        JumpResolveResult Resolve(
            IReadOnlyList<JumpObstacleData> obstacles,
            RoofJumpResolveContext context);
    }
}
