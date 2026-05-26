using System.Collections.Generic;
using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.GameEngine.Mechanics.Models;
using Assets.Scripts.Gameplay.Enums;

namespace Assets.Scripts.Bot.Strategies.Shared.JumpPlanning.JumpOn
{
    /// <summary>
    /// Описывает runtime-различия вариантов напрыгивания на дорожный smallAlive.
    /// </summary>
    internal interface IJumpOnPolicy
    {
        BotActionKind ActionKind { get; }
        int EnergyCost { get; }
        string DescriptionPrefix { get; }
        string LogTag { get; }
        HamsterStateEnum ExpectedJumpOnState { get; }

        /// <summary>
        /// Возвращает runtime-дистанцию прыжка для текущего варианта действия.
        /// </summary>
        bool TryGetTravel(out float travel);

        /// <summary>
        /// Переводит planning fire shift в runtime-точку resolver'а и дистанцию resolver'а.
        /// </summary>
        void GetResolveInput(
            float fireShift,
            float jumpTravel,
            out float resolveFireShift,
            out float resolveTravel);

        /// <summary>
        /// Вызывает runtime resolver для конкретного варианта прыжка.
        /// </summary>
        JumpResolveResult Resolve(
            IReadOnlyList<JumpObstacleData> obstacles,
            JumpResolveContext context);
    }
}
