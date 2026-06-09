using System.Collections.Generic;
using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.GameEngine.Mechanics.Models;
using Assets.Scripts.Gameplay.Enums;

namespace Assets.Scripts.Bot.Strategies.Shared.JumpOn
{
    /// <summary>
    /// Описывает runtime-различия вариантов напрыгивания на дорожный target.
    /// </summary>
    internal interface IJumpOnPolicy
    {
        /// <summary>
        /// Тип action для конкретного варианта jump-on.
        /// </summary>
        BotActionKind ActionKind { get; }

        /// <summary>
        /// Стоимость action в энергии.
        /// </summary>
        int EnergyCost { get; }

        /// <summary>
        /// Префикс человекочитаемого описания action.
        /// </summary>
        string DescriptionPrefix { get; }

        /// <summary>
        /// Тег диагностических сообщений стратегии.
        /// </summary>
        string LogTag { get; }

        /// <summary>
        /// Runtime-состояние, которое подтверждает успешное напрыгивание.
        /// </summary>
        HamsterStateEnum ExpectedJumpOnState { get; }

        /// <summary>
        /// Возвращает runtime-дистанции для текущего варианта jump-on действия.
        /// </summary>
        bool TryGetTravel(out JumpOnTravel travel);

        /// <summary>
        /// Вызывает runtime resolver для конкретного варианта прыжка.
        /// </summary>
        JumpResolveResult Resolve(
            IReadOnlyList<JumpObstacleData> obstacles,
            JumpResolveContext context);
    }
}
