using System.Collections.Generic;
using Assets.Scripts.Bot.PlanState;
using Assets.Scripts.GameEngine.Mechanics.Models;
using Assets.Scripts.Gameplay.Enums;

namespace Assets.Scripts.Bot.Strategies.Shared.JumpPlanning.JumpOnRoof
{
    /// <summary>
    /// Описывает различия между вариантами запрыгивания на крышу.
    /// </summary>
    internal interface IJumpOnRoofPolicy
    {
        BotActionKind ActionKind { get; }
        int EnergyCost { get; }
        string DescriptionPrefix { get; }
        string LogTag { get; }
        HamsterStateEnum ExpectedRoofState { get; }
        bool DamageBigAliveWithoutYByReach { get; }

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
