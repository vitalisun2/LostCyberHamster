using System.Collections.Generic;
using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.GameEngine.Mechanics.Models;
using Assets.Scripts.Gameplay.Enums;

namespace Assets.Scripts.Bot.StrategiesNew.Shared.JumpOnFromRoof
{
    /// <summary>
    /// Описывает runtime-отличия конкретного roof-to-road jump-on варианта.
    /// </summary>
    internal interface IJumpOnFromRoofPolicy
    {
        /// <summary>
        /// Тип action для конкретного варианта.
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
        /// Runtime-состояние, которое подтверждает успешное напрыгивание с крыши.
        /// </summary>
        HamsterStateEnum ExpectedJumpOnState { get; }

        /// <summary>
        /// Возвращает runtime-дистанции для текущего варианта roof-to-road jump-on.
        /// </summary>
        bool TryGetTravel(out JumpOnFromRoofTravel travel);

        /// <summary>
        /// Вызывает runtime roof-jump resolver для конкретного варианта прыжка.
        /// </summary>
        JumpResolveResult Resolve(
            IReadOnlyList<JumpObstacleData> obstacles,
            RoofJumpResolveContext context);
    }
}
